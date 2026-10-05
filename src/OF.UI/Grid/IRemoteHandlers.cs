using Infragistics.Web.Mvc;

namespace OF.UI.Grid
{
    public interface IRemoteHandlers
    {
        IQueryable ApplyFiltering<T>(IQueryCollection queryString, IQueryable data, IGridModel grid);
        IQueryable ApplySorting(IQueryCollection queryString, IQueryable data, IGridModel grid);
    }
}