using Microsoft.Extensions.Logging;
using OF.Data.Database;
using System.Web;

namespace OF.Common.Utils
{
    public static class LineX
    {
        public static void NormalizeItemDescription(this Line line, ILogger? logger)
        {
            if (line?.ItemDescription is not null)
            {
                var value = line.ItemDescription;

                var normalized = HttpUtility
                    .HtmlDecode(value?.ReplaceHtmlTags(" ") ?? string.Empty)
                    .TrimAllWhiteSpace()
                    .Truncate(120);

                if (normalized != value)
                {
                    logger?.LogInformation(
                        "Normalized ItemDescription on line [{LineId}] from [{ItemDescription}] to [{Normalized}]",
                        line.AgreementLineNumber,
                        value,
                        normalized);
                }

                line.ItemDescription = normalized;
            }
        }
    }
}