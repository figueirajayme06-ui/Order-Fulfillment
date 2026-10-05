using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using OF.Common.Infrastructure.MDP;

namespace OF.WebApp.Features.Assets;

public sealed class MdpAssetEnrichmentService(
    MDPDbContext context,
    ILogger<MdpAssetEnrichmentService> logger) : IAssetEnrichmentService
{
    private const int CommandTimeoutSeconds = 15;

    public async Task<AssetEnrichmentResponse> GetAsync(
        string individualItemNumber,
        int serviceLimit,
        CancellationToken cancellationToken)
    {
        try
        {
            var connection = context.Database.GetDbConnection();
            var openedHere = connection.State != ConnectionState.Open;
            if (openedHere)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                var location = await GetLocationAsync(connection, individualItemNumber, cancellationToken);
                var telemetry = await GetTelemetryAsync(connection, individualItemNumber, cancellationToken);
                var serviceHistory = await GetServiceHistoryAsync(
                    connection,
                    individualItemNumber,
                    serviceLimit,
                    cancellationToken);
                var retrofits = await GetRetrofitsAsync(connection, individualItemNumber, cancellationToken);
                var rentalHistory = await GetRentalHistoryAsync(connection, individualItemNumber, cancellationToken);
                return new AssetEnrichmentResponse(location, serviceHistory, retrofits, rentalHistory, telemetry);
            }
            finally
            {
                if (openedHere)
                {
                    await connection.CloseAsync();
                }
            }
        }
        catch (Exception exception) when (exception is DbException or TimeoutException or InvalidOperationException)
        {
            logger.LogWarning(exception, "Unable to retrieve MDP enrichment for asset {AssetId}", individualItemNumber);
            throw new AssetEnrichmentUnavailableException("The asset enrichment source is unavailable.", exception);
        }
    }

    private static async Task<AssetTelemetrySummary?> GetTelemetryAsync(
        DbConnection connection,
        string individualItemNumber,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH equipment AS (
                SELECT GPN, UID, COLLECTION_UID, MD_CDC_OPERATION_TYPE,
                    ROW_NUMBER() OVER (
                        PARTITION BY UID
                        ORDER BY MD_CDC_TIMESTAMP DESC, MD_BRONZE_TIMESTAMP DESC, MD_CDC_LOG_POSITION DESC) AS rn
                FROM dbo.sitewatch_config_equipment
                WHERE GPN = @assetId AND MD_CDC_OPERATION_TYPE <> 'BEFOREIMAGE'
            ),
            collections AS (
                SELECT UID, UNIT_UID, MD_CDC_OPERATION_TYPE,
                    ROW_NUMBER() OVER (
                        PARTITION BY UID
                        ORDER BY MD_CDC_TIMESTAMP DESC, MD_BRONZE_TIMESTAMP DESC, MD_CDC_LOG_POSITION DESC) AS rn
                FROM dbo.config_collection
                WHERE MD_CDC_OPERATION_TYPE <> 'BEFOREIMAGE'
            ),
            units AS (
                SELECT UID, MD_CDC_TIMESTAMP, MD_BRONZE_TIMESTAMP, MD_CDC_OPERATION_TYPE,
                    ROW_NUMBER() OVER (
                        PARTITION BY UID
                        ORDER BY MD_CDC_TIMESTAMP DESC, MD_BRONZE_TIMESTAMP DESC, MD_CDC_LOG_POSITION DESC) AS rn
                FROM dbo.unit_status_current
                WHERE MD_CDC_OPERATION_TYPE <> 'BEFOREIMAGE'
            )
            SELECT TOP (1)
                COALESCE(product.TelemetryStatus, 'Unknown'),
                CASE WHEN equipment.UID IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END,
                units.MD_CDC_TIMESTAMP,
                units.MD_BRONZE_TIMESTAMP
            FROM dbo.ProductHierarchy product
            LEFT JOIN equipment ON equipment.GPN = product.IndividualItemNumber
                AND equipment.rn = 1 AND equipment.MD_CDC_OPERATION_TYPE <> 'DELETE'
            LEFT JOIN collections ON collections.UID = equipment.COLLECTION_UID
                AND collections.rn = 1 AND collections.MD_CDC_OPERATION_TYPE <> 'DELETE'
            LEFT JOIN units ON units.UID = collections.UNIT_UID
                AND units.rn = 1 AND units.MD_CDC_OPERATION_TYPE <> 'DELETE'
            WHERE product.IndividualItemNumber = @assetId;
            """;

        await using var command = CreateCommand(connection, sql, individualItemNumber);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new AssetTelemetrySummary(
            reader.GetString(0),
            reader.GetBoolean(1),
            GetNullableDateTime(reader, 2),
            GetNullableDateTime(reader, 3));
    }

    private static async Task<AssetLocationObservation?> GetLocationAsync(
        DbConnection connection,
        string individualItemNumber,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH equipment AS (
                SELECT GPN, UID, COLLECTION_UID, ENABLED, PRIMARY_DEVICE, MD_CDC_OPERATION_TYPE,
                    ROW_NUMBER() OVER (
                        PARTITION BY UID
                        ORDER BY MD_CDC_TIMESTAMP DESC, MD_BRONZE_TIMESTAMP DESC, MD_CDC_LOG_POSITION DESC) AS rn
                FROM dbo.sitewatch_config_equipment
                WHERE GPN = @assetId AND MD_CDC_OPERATION_TYPE <> 'BEFOREIMAGE'
            ),
            latest_gps AS (
                SELECT gps.UID, gps.RAW_LATITUDE, gps.RAW_LONGITUDE, gps.[TIMESTAMP], gps.MD_BRONZE_TIMESTAMP,
                    gps.VALIDITY, gps.NUM_SATS, gps.HORIZ_ACCURACY, gps.ASSIGNED_GEO_ID,
                    gps.MD_CDC_OPERATION_TYPE,
                    ROW_NUMBER() OVER (
                        PARTITION BY gps.UID
                        ORDER BY gps.[TIMESTAMP] DESC, gps.MD_CDC_TIMESTAMP DESC,
                            gps.MD_BRONZE_TIMESTAMP DESC, gps.MD_CDC_LOG_POSITION DESC) AS rn
                FROM dbo.sitewatch_gps_raw gps
                INNER JOIN equipment equipmentMatch
                    ON equipmentMatch.COLLECTION_UID = gps.UID AND equipmentMatch.rn = 1
                WHERE gps.MD_CDC_OPERATION_TYPE <> 'BEFOREIMAGE'
            ),
            latest_geo AS (
                SELECT ID, NAME, MD_CDC_OPERATION_TYPE,
                    ROW_NUMBER() OVER (
                        PARTITION BY ID
                        ORDER BY MD_CDC_TIMESTAMP DESC, MD_BRONZE_TIMESTAMP DESC, MD_CDC_LOG_POSITION DESC) AS rn
                FROM dbo.sitewatch_config_geo_object
                WHERE MD_CDC_OPERATION_TYPE <> 'BEFOREIMAGE'
            )
            SELECT TOP (1) gps.RAW_LATITUDE, gps.RAW_LONGITUDE, gps.[TIMESTAMP], gps.MD_BRONZE_TIMESTAMP,
                gps.VALIDITY, gps.NUM_SATS, gps.HORIZ_ACCURACY, geo.NAME,
                COALESCE(product.TelemetryStatus, 'Unknown') AS TelemetryStatus
            FROM equipment
            INNER JOIN latest_gps gps ON gps.UID = equipment.COLLECTION_UID AND gps.rn = 1
            LEFT JOIN latest_geo geo ON geo.ID = gps.ASSIGNED_GEO_ID AND geo.rn = 1
                AND geo.MD_CDC_OPERATION_TYPE <> 'DELETE'
            LEFT JOIN dbo.ProductHierarchy product ON product.IndividualItemNumber = equipment.GPN
            WHERE equipment.rn = 1 AND equipment.MD_CDC_OPERATION_TYPE <> 'DELETE'
                AND gps.MD_CDC_OPERATION_TYPE <> 'DELETE'
                AND equipment.ENABLED = 1 AND equipment.PRIMARY_DEVICE = 1
                AND gps.RAW_LATITUDE BETWEEN -90 AND 90 AND gps.RAW_LONGITUDE BETWEEN -180 AND 180
                AND NOT (gps.RAW_LATITUDE = 0 AND gps.RAW_LONGITUDE = 0)
            ORDER BY gps.[TIMESTAMP] DESC;
            """;

        await using var command = CreateCommand(connection, sql, individualItemNumber);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new AssetLocationObservation(
            reader.GetDecimal(0),
            reader.GetDecimal(1),
            reader.GetDateTime(2),
            GetNullableDateTime(reader, 3),
            GetNullableInt32(reader, 4),
            GetNullableInt32(reader, 5),
            GetNullableDouble(reader, 6),
            GetNullableString(reader, 7),
            reader.GetString(8));
    }

    private static async Task<IReadOnlyList<AssetServiceHistoryItem>> GetServiceHistoryAsync(
        DbConnection connection,
        string individualItemNumber,
        int serviceLimit,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (@serviceLimit)
                service_order_number,
                COALESCE(service_order_job_number, 0) AS service_order_job_number,
                MAX(so_status) AS so_status,
                MAX(ServiceOrders_DateCreated) AS created_date,
                MAX(actual_finish_date) AS finished_date,
                MAX(ServiceOrders_TypeDescription) AS type_description,
                MAX(SMRValue) AS meter_reading,
                MAX(SMRdate) AS meter_date,
                MAX(ServiceOrders_ErrorSymptom) AS error_symptom,
                MAX(ServiceOrders_ErrorCause) AS error_cause,
                MAX(ServiceOrders_Action) AS action_code,
                MAX(ServiceOrders_ActionText) AS action_text,
                COUNT(*) AS detail_count
            FROM dbo.v_service_data
            WHERE ItemName = @assetId AND service_order_number IS NOT NULL
            GROUP BY service_order_number, service_order_job_number
            ORDER BY MAX(ServiceOrders_DateCreated) DESC, service_order_number DESC;
            """;

        await using var command = CreateCommand(connection, sql, individualItemNumber);
        AddParameter(command, "@serviceLimit", DbType.Int32, serviceLimit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<AssetServiceHistoryItem>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new AssetServiceHistoryItem(
                reader.GetString(0),
                reader.GetInt32(1),
                GetNullableString(reader, 2),
                GetNullableDateTime(reader, 3),
                GetNullableDateTime(reader, 4),
                GetNullableString(reader, 5),
                GetNullableDecimal(reader, 6),
                GetNullableDateTime(reader, 7),
                GetNullableString(reader, 8),
                GetNullableString(reader, 9),
                GetNullableString(reader, 10),
                GetNullableString(reader, 11),
                reader.GetInt32(12)));
        }

        return items;
    }

    private static async Task<IReadOnlyList<AssetRetrofitItem>> GetRetrofitsAsync(
        DbConnection connection,
        string individualItemNumber,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (20) Document, Description, RetrofitStatus, OpenDate, CompletedDate, ServiceOrder
            FROM dbo.RetrofitCache
            WHERE IndItem = @assetId AND Document IS NOT NULL
            ORDER BY CASE WHEN RetrofitStatus = 'Due' THEN 0 ELSE 1 END, OpenDate DESC, Document;
            """;

        await using var command = CreateCommand(connection, sql, individualItemNumber);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<AssetRetrofitItem>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new AssetRetrofitItem(
                reader.GetString(0),
                GetNullableString(reader, 1),
                GetNullableString(reader, 2),
                GetNullableDateTime(reader, 3),
                GetNullableDateTime(reader, 4),
                GetNullableString(reader, 5)));
        }

        return items;
    }

    private static async Task<IReadOnlyList<AssetRentalHistoryItem>> GetRentalHistoryAsync(
        DbConnection connection,
        string individualItemNumber,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DISTINCT TOP (100)
                lines.AgreementNumber, customer.Number, customer.Name,
                lines.ValidFromDate, lines.ValidToDate, lines.TerminationDate
            FROM dbo.AgreementLines lines
            LEFT JOIN dbo.AgreementHeader header ON header.AgreementHeaderID = lines.AgreementHeaderID
            LEFT JOIN dbo.Customer customer ON customer.CustomerId = header.CustomerID
            WHERE lines.IndividualItemNumber = @assetId AND lines.IsDeleted = 0
            ORDER BY lines.ValidFromDate DESC, lines.AgreementNumber DESC;
            """;

        await using var command = CreateCommand(connection, sql, individualItemNumber);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<AssetRentalHistoryItem>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new AssetRentalHistoryItem(
                reader.GetString(0),
                GetNullableString(reader, 1),
                GetNullableString(reader, 2),
                GetNullableDateTime(reader, 3),
                GetNullableDateTime(reader, 4),
                GetNullableDateTime(reader, 5)));
        }

        return items;
    }

    private static DbCommand CreateCommand(DbConnection connection, string sql, string individualItemNumber)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = CommandTimeoutSeconds;
        AddParameter(command, "@assetId", DbType.String, individualItemNumber);
        return command;
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string? GetNullableString(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static DateTime? GetNullableDateTime(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);

    private static int? GetNullableInt32(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

    private static double? GetNullableDouble(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : Convert.ToDouble(reader.GetValue(ordinal));

    private static decimal? GetNullableDecimal(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
}
