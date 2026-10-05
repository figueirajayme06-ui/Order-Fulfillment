using System.ComponentModel.DataAnnotations;

namespace OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs.Common
{
    public abstract class BaseValidatable
    {
        protected bool Validate(object item, out IList<ValidationResult> results)
        {
            results = new List<ValidationResult>();

            return Validator.TryValidateObject(item, new ValidationContext(item), results, true);
        }
    }
}
