using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using OF.Api.UseCases.Reservations;
using OF.Data;
using OF.Data.Database;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace OF.Api;

[ExcludeFromCodeCoverage]
public class ReservationFunctions
{
    private readonly ILogger<ReservationRequestHandler> logger;
    private readonly ApplicationDbContext dbContext;
    private readonly ReservationRequestHandler reservationHandler;

    public ReservationFunctions(ILogger<ReservationRequestHandler> logger, ApplicationDbContext dbContext, ReservationRequestHandler handler)
    {
        this.logger = logger;
        this.dbContext = dbContext;
        this.reservationHandler = handler;
    }

    [OpenApiOperation(operationId: "GetReservationById", tags: new[] { "reservation" }, Summary = "Get a reservation by ID", Description = "Retrieves a reservation from the database using the reservation ID provided in the query string or path.")]
    [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(string), Summary = "Reservation ID", Description = "The unique identifier of the reservation to retrieve.")]
    [OpenApiSecurity("function_auth", SecuritySchemeType.ApiKey, In = OpenApiSecurityLocationType.Header, Name = "x-functions-key")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(Reservation), Description = "The reservation details.")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Reservation not found.")]
    [Function(nameof(ReservationssApi))]
    public async Task<HttpResponseData> ReservationssApi(
        [HttpTrigger(AuthorizationLevel.Function, "GET", Route = "reservations/{id?}")] HttpRequestData request, string id)
    {
        try
        {
            if (!int.TryParse(id, out var reservationId))
            {
                return await CreateProblemDetailsResponse(HttpStatusCode.BadRequest, "Bad request", "Unable to get reservation id from the path.", request);
            }

            logger.LogInformation($"{nameof(ReservationssApi)} handler invoked: {reservationId.ToString()}");

            var json = await reservationHandler.Handle(reservationId);

            if (json is null)
            {
                return await CreateProblemDetailsResponse(HttpStatusCode.NotFound, "Not found", "The requested resource was not found.", request);
            }

            var okResponse = request.CreateResponse();
            await okResponse.WriteAsJsonAsync(json);
            okResponse.StatusCode = HttpStatusCode.OK;
            return okResponse;
        }
        catch (Exception ex)
        {         
            return await CreateProblemDetailsResponse(HttpStatusCode.InternalServerError, "Internal Server Error", ex.Message, request);
        }
    }

    private static async Task<HttpResponseData> CreateProblemDetailsResponse
        (HttpStatusCode statusCode, string title, string detail, HttpRequestData request)
    {
        var errorResponse = request.CreateResponse();
        var errorDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = detail
        };

        await errorResponse.WriteAsJsonAsync(errorDetails);
        errorResponse.StatusCode = statusCode;
        return errorResponse;
    }
}

