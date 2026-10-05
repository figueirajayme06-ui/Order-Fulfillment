namespace OF.WebApp.Features.Assets;

public interface IAssetEnrichmentService
{
    Task<AssetEnrichmentResponse> GetAsync(string individualItemNumber, int serviceLimit, CancellationToken cancellationToken);
}
