using System.Globalization;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OF.Common.Infrastructure.CloudSuite.Models.Agreements.DataLake;
using OF.Common.Infrastructure.IPG.Orders.Models.M3;

namespace OF.Common.Infrastructure.IPG.Orders;
public class AgreementLineFetcher : IAgreementLineFetcher
{
    private readonly IOrderIntegration _orderIntegration;
    private readonly ILogger<AgreementLineFetcher> _logger;

    public AgreementLineFetcher(IOrderIntegration orderIntegration, ILogger<AgreementLineFetcher> logger)
    {
        _orderIntegration = orderIntegration;
        _logger = logger;
    }

    public async Task<IList<IONAgreementLineData>> FetchLinesAsync(string agreementNumber, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            { "AGNB", agreementNumber },
            { "cono", Constants.IPG.Company }
        };

        var response = await _orderIntegration.ExecuteM3Program(
            "STS101MI", "LstRentalLine", parameters, cancellationToken);

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var m3Response = JsonConvert.DeserializeObject<M3Response>(content);

        if (m3Response?.Records == null || m3Response.Records.Count == 0)
        {
            return Array.Empty<IONAgreementLineData>();
        }

        var lines = new List<IONAgreementLineData>();

        foreach (var record in m3Response.Records)
        {
            if (record.NameValue is not { Count: > 0 })
            {
                continue;
            }

            var fields = record.NameValue.ToDictionary(nv => nv.Name, nv => nv.Value);
            var line = MapToAgreementLineData(fields);

            if (line != null)
            {
                lines.Add(line);
            }
        }

        return lines;
    }

    private IONAgreementLineData? MapToAgreementLineData(Dictionary<string, string> fields)
    {
        if (!fields.TryGetValue("AGNB", out var agnb) || string.IsNullOrWhiteSpace(agnb))
        {
            _logger.LogWarning("M3 record missing AGNB field, skipping.");
            return null;
        }

        if (!int.TryParse(GetField(fields, "PONR"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var lineNumber))
        {
            _logger.LogWarning("M3 record missing or invalid PONR field for AGNB {AGNB}, skipping.", agnb);
            return null;
        }

        if (!float.TryParse(GetField(fields, "ORQT"), NumberStyles.Float, CultureInfo.InvariantCulture, out var orderedQuantity))
        {
            orderedQuantity = 0;
        }

        return new IONAgreementLineData
        {
            AgreementNumber = agnb,
            LineNumber = lineNumber,
            LineSuffix = GetField(fields, "POSX"),
            Company = GetField(fields, "CONO"),
            Division = GetField(fields, "DIVI") ?? string.Empty,
            Facility = GetField(fields, "FACI") ?? string.Empty,
            ItemNumber = GetField(fields, "ITNO") ?? string.Empty,
            LineType = GetField(fields, "LTYP") ?? string.Empty,
            AgreementLineStatus = GetField(fields, "ASTH") ?? string.Empty,
            CustomerOrderNumber = GetField(fields, "ORNO"),
            CustomerSite = GetField(fields, "CUPL"),
            AddressNumber = GetField(fields, "SAID"),
            FromWarehouse = GetField(fields, "FWHL") ?? string.Empty,
            DeliveryOrderNumber = GetField(fields, "DOND"),
            DeliveryOrderLineNumber = GetField(fields, "DOLD"),
            CollectionOrderNumber = GetField(fields, "DONR"),
            CollectionOrderLineNumber = GetField(fields, "DOLR"),
            NumberOfShifts = GetField(fields, "ANOS"),
            OrderedQuantity = orderedQuantity,
            DeliveryDate = GetField(fields, "DLDT"),
            CollectionDateReturnDate = GetField(fields, "COLD"),
            ShipAddress1 = GetField(fields, "SAD1"),
            ShipAddress2 = GetField(fields, "SAD2"),
            ShipAddress3 = GetField(fields, "SAD3"),
            ShipAddress4 = GetField(fields, "SAD4"),
            TelephoneNumber = GetField(fields, "SPHN"),
            TerminationDate = GetField(fields, "TEDA"),
            ToWarehouse = GetField(fields, "TWHL"),
            AgreementLineTextId = GetField(fields, "TXID"),
            SubstituteFlag = GetField(fields, "DURT"),
            AgreementFromDate = GetField(fields, "FVDT") ?? string.Empty,
            AgreementToDate = GetField(fields, "LVDT"),
            QuoteLineId = GetField(fields, "UCA2"),
            OrderLineId = GetField(fields, "UCA3"),
            Source = GetField(fields, "UCA4"),
            NumberOfUsedDaysOnHire = GetField(fields, "UDAY"),
            TextIdentityDeliveryOrderText = GetField(fields, "DETX"),
            TextIdentityCollectionText = GetField(fields, "COTX"),
            TextIdentityPOText = GetField(fields, "POTX"),
            TextIdentityPRText = GetField(fields, "PRTX"),
            LotNumber = GetField(fields, "BANO"),
            SubstituteItem = GetField(fields, "ILIT"),
            GenericItem = GetField(fields, "GEIT"),
            DeliveryWindowStartDate = GetField(fields, "DETH"),
            DeliveryWindowStartTime = GetField(fields, "DETM"),
            CollectionWindowStartDate = GetField(fields, "COTH"),
            CollectionWindowStartTime = GetField(fields, "CLTM"),
            DeliveryWindowEnd = GetField(fields, "CFJ4"),
            CollectionWindowEnd = GetField(fields, "CFJ9"),
            PackageNumber = GetField(fields, "CFJ6"),
            PackageSortLine = GetField(fields, "CFJ8"),
            ProposalNumber = GetField(fields, "AYRF"),
            RateType = GetField(fields, "CCAP")
        };
    }

    private static string? GetField(Dictionary<string, string> fields, string key)
    {
        return fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
    }
}
