using OF.Data.Database;

namespace OF.UI.Helpers
{
    public static class HeaderHelpers
    {
        public static bool IsActivatable(this Header header)
        {
            var isAgreement = (header.AgreementNumber?.ToUpper()?.StartsWith("T") == true || header.AgreementNumber?.ToUpper()?.StartsWith("A") == true);
            var isStillQuote = header.AgreementNumber.StartsWith("Q", StringComparison.OrdinalIgnoreCase) == true;

            if (!isAgreement || isStillQuote) return false;

            return true;
        }
    }
}
