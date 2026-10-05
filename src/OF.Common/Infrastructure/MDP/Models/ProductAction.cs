using System.ComponentModel.DataAnnotations.Schema;

namespace OF.Common.Infrastructure.MDP.Models
{
    public class ProductAction
    {
        public ProductAction()
        {
        }

        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? Product { get; set; }

        public bool Required{ get; set; }

        public string? Rule { get; set; }

        public string? Type { get; set; }
    }
}
