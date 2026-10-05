using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OF.Data;

namespace OF.Api.UseCases.Reservations;
public class ReservationRequestHandler
{
    private ApplicationDbContext dbContext;
    private readonly ILogger<ReservationRequestHandler> logger;

    public ReservationRequestHandler(ApplicationDbContext dbContext, ILogger<ReservationRequestHandler> logger)
    {
        this.dbContext = dbContext;
        this.logger = logger;
    }

    public async Task<ReservationDto?> Handle(int? id)
    {
        var reservation = await dbContext.Reservations
            .Join(
                dbContext.Lines,
                r => r.LineId,
                l => l.Id,
                (r, l) => new ReservationDto
                {
                    Id = r.Id,
                    AssetId = r.AssetId,
                    ItemNumber = r.ItemNumber,
                    Quantity = r.Quantity,
                    LineId = r.LineId,
                    Attributes = l.Attributes
                })
            .SingleOrDefaultAsync(r => r.Id == id);

        return reservation;
    }
}

