using OF.Data.Database;
using System.Globalization;

namespace OF.UI.Helpers
{
    public class DateHelper
    {
        public static string LocalDate(DateTime? date, User identity)
        {
            if (date == null)
            {
                return String.Empty;
            }

            return date.Value.ToString(identity.DateFormat, CultureInfo.InvariantCulture);
        }
    }
}
