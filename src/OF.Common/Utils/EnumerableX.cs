using OF.Data.Enums;

namespace OF.Common.Utils
{
    public static class EnumerableX
    {
        public static IEnumerable<string> GetDefinedElements(this IEnumerable<string> source)
        {
            return source.Where(e => !string.IsNullOrWhiteSpace(e));
        }

        public static string GetCultureforLanguage(this Languages languages)
        {
            switch (languages)
            {
                case Languages.German:
                    return "de";
                case Languages.French:
                    return "fr";
                case Languages.Spanish:
                    return "es";
                case Languages.Italian:
                    return "it";
                default:
                    return "en";
            }
        }
    }
}
