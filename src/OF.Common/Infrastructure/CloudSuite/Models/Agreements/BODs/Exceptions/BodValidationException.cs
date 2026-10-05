namespace OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs.Exceptions
{
    public class BodValidationException : Exception
    {
        public BodValidationException(string bodName, IEnumerable<string> fields) : base($"Bod '{bodName}' Failed to validate, following fields have issues: " + string.Join(",", fields))
        {

        }

        public BodValidationException(string message) : base(message)
        {

        }
    }
}
