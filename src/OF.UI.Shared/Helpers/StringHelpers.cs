using HtmlAgilityPack;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace OF.UI.Helpers
{
    public static class StringHelpers
    {
        public static string Safe(this IHtmlHelper html, object? input)
        {
            string? value = input?.ToString();
            return !string.IsNullOrWhiteSpace(value) ? value : "N/A";
        }

        public static IHtmlContent SafeRaw(this IHtmlHelper html, object? input)
        {
            string? value = input?.ToString();
            return html.Raw(!string.IsNullOrWhiteSpace(value) ? value : "N/A");
        }

        public static string RemoveHtmlAndTruncate(this IHtmlHelper html, string input, int maxLength = 250)
        {
            var plainText = RemoveHtmlTags(input);

            if (plainText.Length > maxLength)
            {
                return plainText.Substring(0, maxLength) + "...";
            }

            return plainText;
        }

        private static string RemoveHtmlTags(string input)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(input);
            return doc.DocumentNode.InnerText;
        }
    }
}
