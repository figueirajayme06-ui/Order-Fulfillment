using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OF.Api.UseCases.Agreements.Exceptions;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.OF;
using OF.Common.Infrastructure.Storage;
using OF.Common.Utils;
using OF.Data;
using OF.Data.Database;
using static OF.Common.Enums;

namespace OF.Api.UseCases.Agreements
{
    public class ActivateOrderRequest
    {
        public int HeaderId { get; set; }
    }

    public class ActivateAgreementHandler
    {
        private readonly ApplicationDbContext dbContext;
        private readonly OrderManagementService orderIntegration;
        private readonly IActivateHeaderQueueClient headerQueue;
        private readonly ICoreFulfilmentEngine engine;
        private readonly ILogger logger;

        public ActivateAgreementHandler(
            ApplicationDbContext dbContext, 
            OrderManagementService orderIntegration, 
            IActivateHeaderQueueClient headerQueue, 
            ICoreFulfilmentEngine engine,
            ILogger logger)
        {
            this.dbContext = dbContext;
            this.orderIntegration = orderIntegration;
            this.headerQueue = headerQueue;
            this.engine = engine;
            this.logger = logger;
        }

        public async Task<string> Handle(string body)
        {
            ActivateOrderRequest request = JsonConvert.DeserializeObject<ActivateOrderRequest>(body)!;

            logger.LogInformation($"Sending agreement reservations from header Id {request.HeaderId}.");

            // Initially save the requested status of the header and lines

            var header = await dbContext.Headers
                                .Include(i => i.Lines)
                                .FirstAsync(i => i.Id == request.HeaderId);

            using var _ = logger.BeginScope(new { header.AgreementNumber, request.HeaderId });

            List<Reservation> reservations;

            try
            {
                reservations = await EnsureCorrectness(request, header);
            }
            catch (ActivationException exception)
            {
                var issue = $"{DateTime.UtcNow.ToString("yyyyMMddHHmmss")}: Fail: Error in setup regarding activation of Header '{request.HeaderId}', {exception.Message};";
                logger.LogError(exception, issue);
                header.ActivationErrors = issue;
                header.ActivationStatus = (int)ActivationStatus.Failed;
                header.LastUpdatedDate = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
                return header.AgreementNumber!;
            }

            header.ActivationStatus = header.IsActivated ? (int)ActivationStatus.Activated : (int)ActivationStatus.Requested;
            header.ActivationInstanceId = null;
            header.ActivationErrors = null;
            header.LastUpdatedDate = DateTime.UtcNow;

            var orphanedCount = engine.AbandonOrphanedQuoteLines(header.Id);

            foreach (var line in header.Lines.Where(TheyreNotActivatedOrQuotes))
            {
                if (line.IsDeleted && line.ActivationStatus == (int)ActivationStatus.Failed)
                {
                    line.ActivationStatus = (int)ActivationStatus.Activated;
                    line.ActivationErrors = null;
                    line.ActivationInstanceId = null;
                }
                else
                {
                    line.ActivationStatus = (int)ActivationStatus.Requested;
                }

                line.LastUpdatedDate = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync();

            header = await SplitUnactivatedLinesWithMultipleReservations(header, reservations);

            try
            {
                foreach (var line in header.Lines.Where(TheyreNotActivatedOrQuotes))
                {
                    if (line.IsDeleted)
                    {
                        await orderIntegration.DeleteLine(header, line);
                    }
                    else
                    {
                        var reservation = reservations?.FirstOrDefault(r => r.LineId == line.Id);

                        if (reservation != null)
                        {
                            if (line.IsSubline)
                            {
                                await orderIntegration.CreateLine(header, line, reservation);
                            }
                            else
                            {
                                await orderIntegration.UpdateLine(header, line, reservation);
                            }
                        }
                        else
                        {
                            // If someone has added a subline and put an excluded generic or item on it, get it ignored

                            if (line.IsSubline && line.ItemNumber.IsExcludedLine())
                            {
                                logger.LogWarning($"It appears that line {line.AgreementLineNumber} has been allocated an excluded generic code {line.ItemNumber}. Ignoring.");

                                line.ActivationStatus = (int)ActivationStatus.Activated;
                                line.ActivationInstanceId = null;
                                line.ActivationErrors = null;
                                line.LastUpdatedDate = DateTime.UtcNow;

                                continue;
                            }

                            logger.LogWarning($"It appears that line {line.AgreementLineNumber} has no reservation.");
                        }
                    }
                }

                await dbContext.SaveChangesAsync();

                if (!header.IsActivated)
                {
                    await headerQueue.QueueActivation(header.Id);
                }
            } 
            catch (Exception exception)
            {
                var issue = $"{DateTime.UtcNow.ToString("yyyyMMddHHmmss")}: Fail: Error in requesting activation of Header '{request.HeaderId}', {exception.GetBaseException().Message};";
                logger.LogError(exception, issue);
                header.ActivationErrors = issue;
                header.ActivationStatus = (int)ActivationStatus.Failed;
                header.LastUpdatedDate = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync();

            return header.AgreementNumber!;
        }

        private async Task<Header> SplitUnactivatedLinesWithMultipleReservations(Header header, List<Reservation> currentReservations)
        {
            var allLines = new List<Line>(header.Lines);
            var newLines = new List<Line>();

            foreach (var line in header.Lines.Where(TheyreNotDeletedActivatedOrQuotes))
            {
                var reservations = currentReservations.Where(r => r.LineId == line.Id).ToList();

                // Ignore this as its only got one reservation per line so doesnt need split

                if (reservations.Count() <= 1)
                {
                    continue;
                }

                Reservation firstReservation = reservations.First();

                foreach (var reservation in reservations)
                {
                    // ML; Leave the first reservation against the original line

                    if (reservation == firstReservation)
                    {
                        line.Quantity = reservation.Quantity;
                        continue;
                    }

                    var children = allLines.Where(l => !string.IsNullOrWhiteSpace(l.AgreementLineNumber) && l.AgreementLineNumber.StartsWith(line.AgreementLineNumber + ".")).ToArray();
          
                    var newLine = line.ShallowCopy();
                    newLine.AgreementLineNumber = engine.CalcNextAgreementLineIndex(line, children);
                    newLine.Quantity = reservation.Quantity;
                    dbContext.Lines.Add(newLine);
                    await dbContext.SaveChangesAsync();
                    newLines.Add(newLine);
                    allLines.Add(newLine);
                    reservation.LineId = newLine.Id;
                    await dbContext.SaveChangesAsync();
                }
            }

            foreach (var line in newLines)
            {
                header.Lines.Add(line);
            }

            await dbContext.SaveChangesAsync();

            return header;
        }

        private async Task<List<Reservation>> EnsureCorrectness(ActivateOrderRequest request, Header? header)
        {
            List<Reservation> reservations = new List<Reservation>();
            ActivationException? yourHandsInTheAirLikeYouJustDontCare = null;

            if (header == null)
            {
                yourHandsInTheAirLikeYouJustDontCare = new ActivationException($"No header found for Id {request.HeaderId}");
            }
            else
            {
                var lineIds = header.Lines.Where(TheyreNotDeletedActivatedOrQuotes).Select(i => i.Id).Distinct().ToList();

                if (!lineIds.Any())
                {
                    // If there are no lines which require processing then return to process the header.
                    return reservations;
                }

                reservations = await dbContext.Reservations.Where(i => lineIds.Contains(i.LineId)).ToListAsync();

                if (!reservations.Any())
                {
                    yourHandsInTheAirLikeYouJustDontCare = new ActivationException($"No reservations exist for header for Id {header.AgreementNumber}");
                }

                var reservationsLineIds = reservations.Select(i => i.LineId).Distinct().ToList();
                var linesIdsNotFullfilled = lineIds.Where(i => !reservationsLineIds.Contains(i)).ToList();
                if (yourHandsInTheAirLikeYouJustDontCare == null && linesIdsNotFullfilled.Any())
                {
                    var agreementLineNumbers = header.Lines.Where(i => linesIdsNotFullfilled.Contains(i.Id)).Select(i => i.AgreementLineNumber).ToList();
                    yourHandsInTheAirLikeYouJustDontCare = new ActivationException($"Line's {string.Join(",", agreementLineNumbers)} have no reservations for header Id {header.AgreementNumber}");
                }
            }

            if (yourHandsInTheAirLikeYouJustDontCare != null)
            {
                logger.LogError(yourHandsInTheAirLikeYouJustDontCare, $"Error validating header {request.HeaderId}");
                throw yourHandsInTheAirLikeYouJustDontCare;
            }

            return reservations;
        }

        private bool TheyreNotDeletedActivatedOrQuotes(Line line)
        {
            return line.IsDeleted == false && line.RequiresFulfilment && TheyreNotActivatedOrQuotes(line);
        }

        private bool TheyreNotActivatedOrQuotes(Line line)
        {
            return line.ActivationStatus != (int)ActivationStatus.Activated && (line.AgreementLineNumber?.StartsWith("Q", StringComparison.OrdinalIgnoreCase) == false);
        }
    }
}
