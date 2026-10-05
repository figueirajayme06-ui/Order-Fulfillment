using OF.Data.Database;

namespace OF.UI.ViewModels.ChangeOrders
{
    public class ChangeOrdersListViewModel
    {
        public Header Header { get; set; }

        public IList<ChangeOrder> ChangeOrders => Header?.ChangeOrders?.ToList() ?? new List<ChangeOrder>();

        public bool HasActiveChanges => Header?.ChangeOrders?.Any(c => c.IsActive) ?? false;

        public bool Success { get; internal set; }
    }
}
