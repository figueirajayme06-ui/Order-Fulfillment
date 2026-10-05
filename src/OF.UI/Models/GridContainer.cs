using Infragistics.Web.Mvc;
using OF.Data.Database;

namespace OF.UI.Models
{
    public class GridContainer
    {
        public GridModel? Grid { get; set; }

        public View? View { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }   

        public bool IsAssetView { get; set; }

        public bool IsGanttView { get; set; }
    }
}
