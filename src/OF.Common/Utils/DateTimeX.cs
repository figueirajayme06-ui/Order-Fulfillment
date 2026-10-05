using System.Data.SqlTypes;
using System.Globalization;

namespace OF.Common.Utils
{
    public static class DateTimeX
    {
        public static DateTime? ParseIonExact(this string? value)
        {
            if (value == null || value.Length < 8)
            {
                return null;
            }

            if (DateTime.TryParseExact(value.Substring(0, 8), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
            {
                return parsed;
            }

            return null;
        }

        public static DateTime? ToSqlDateRange(this DateTime? value)
        {
            if (value == null)
            {
                return null;
            }

            return value.Value.ToSqlDateRange();
        }

        public static DateTime ToSqlDateRange(this DateTime value)
        {
            if (value < SqlDateTime.MinValue.Value)
            {
                return SqlDateTime.MinValue.Value;
            }

            if (value > SqlDateTime.MaxValue.Value)
            {
                return SqlDateTime.MaxValue.Value;
            }

            return new SqlDateTime(value).Value;
        }

        public static DateTime? ConvertSalesforceDateTime(string value, string paramName, bool required = false)
        {
            if (string.IsNullOrEmpty(value))
            {
                if (required)
                {
                    throw new Exception($"CPQ value '{value}' for '{paramName}' cannot be converted to a DateTime.");
                }

                return null;
            }

            string[] formats = { "o", "yyyy-MM-dd", "yyyyMMdd", "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ssZ", "dd/MM/yyyy HH:mm:ss.fffZ", "dd-MM-yyyy HH:mm:ss.fffZ", "dd-MM-yyyy HH:mm:ss", "dd/MM/yyyy HH:mm:ss" };
            if (DateTime.TryParseExact(value, formats, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.None, out DateTime result))
            {
                return result;
            }

            if (required)
            {
                throw new Exception($"CPQ value '{value}' for '{paramName}' cannot be converted to a DateTime.");
            }

            return null;
        }

        public static DateTime? ParseIONUnixTime(this string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            if (long.TryParse(value, out long result))
            {
                return DateTimeOffset.FromUnixTimeSeconds(result).DateTime;
            }

            return null;
        }
    }
}
