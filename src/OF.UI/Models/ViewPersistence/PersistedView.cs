using Infragistics.Web.Mvc;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace OF.UI.Models.ViewPersistence
{
    public class PersistedView
    {
        public FilterSetting[]? Filter { get; set; }
        public ColumnSetting[]? Columns { get; set; }
        public SortSetting[]? Sort { get; set; }
        public GroupSetting[]? Group { get; set; }
    }

    public class FilterSetting
    {
        public string? FieldName { get; set; }
        public string? Cond { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("expr")]
        public object? _expr
        {
            get { return this.Expr; }
            set { this.Expr = value == null ? null : value.ToString(); }
        }

        /// <summary>
        /// the value of the attribute, eg 10
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string? Expr { get; set; }

        public string? Logic { get; set; }
        public string? Type { get; set; }
    }

    public class ColumnSetting
    {
        public string? Key { get; set; }
        public bool? Hidden { get; set; }
        public string? Width { get; set; }
        public string? HeaderText { get; set; }
        public string? Regional { get; set; }
        public string? DataType { get; set; }
        public string? FormatterFunction { get; set; }
        public string? Mapper { get; set; }
        public string? Format { get; set; }
        public string? Template { get; set; }
        public string? HeaderCssClass { get; set; }
        public string? ColumnCssClass { get; set; }
        public int? RowSpan { get; set; }
        public int? ColSpan { get; set; }
        public int? RowIndex { get; set; }
        public int? ColumnIndex { get; set; }
        public int? NavigationIndex { get; set; }
    }

    public class SortSetting
    {
        public int Index { get; set; }
        public string? Key { get; set; }
        public string? Dir { get; set; }
    }

    public class GroupSetting
    {
        public string? Key { get; set; }
        public string? Dir { get; set; }
        public string? Layout { get; set; }
    }
}
