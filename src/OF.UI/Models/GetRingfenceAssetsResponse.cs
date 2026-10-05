using OF.Data.Database;

namespace OF.UI.Models
{
    public class GetRingfenceAssetsResponse
    {
        public VwAssetItem[] Assets { get; set; }

        public bool IsSuccess { get; set; }

        public string ErrorMessage { get; set; }
    }
}
