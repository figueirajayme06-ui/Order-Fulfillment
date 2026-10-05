namespace OF.UI.Helpers
{
    public class StatusHelper
    {
        public static string HeaderStatus(string statusCode)
        {
            switch (statusCode)
            {
                case "05":
                    return "05 - Created";
                case "12":
                    return "12 - Customer on Stop";
                case "20":
                    return "20 - Line Created (No Asset Assigned Yet)";
                case "40":
                    return "40 - Terminated";
                case "50":
                    return "50 - On Hire";
                case "90":
                    return "90 - Invoiced"; 
                case "99":
                    return "99 - Completed";
                default:
                    return statusCode + " - Unknown";
            }
        }
    }
}