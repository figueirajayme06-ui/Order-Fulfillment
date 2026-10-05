using OF.UI.Models.ViewPersistence;
using Infragistics.Web.Mvc;

namespace OF.UI.Grid
{
    public interface IGridFactory
    {
        GridModel CreateTaskModel(PersistedView? view, string? textFilter, bool ganttView, IDictionary<string, string>? parameters);
        GridModel CreateAssetModel(PersistedView? view, string? textFilter, bool ganttView, IDictionary<string, string>? parameters);
        List<GridColumn> GetTaskGridColumns(PersistedView persisted, bool ganttView);
        List<GridColumn> GetAssetGridColumns(PersistedView persisted, bool ganttView);
    }
}