using OF.Data.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace OF.Data.Database
{
    public partial class User
    {
        [NotMapped]
        public string LanguageName => ((Languages)Language).ToString();

        [NotMapped]
        public string[] ActiveRoles => Roles?.Split(',')?.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim())?.OrderBy(i => i)?.ToArray() ?? new string[0];
    }
}
