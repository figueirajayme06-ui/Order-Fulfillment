using OF.Common.Infrastructure.CloudSuite.Models.Agreements.DataLake;

namespace OF.Common.Infrastructure.IPG.Orders;

public interface IAgreementLineFetcher
{
    Task<IList<IONAgreementLineData>> FetchLinesAsync(string agreementNumber, CancellationToken cancellationToken);
}
