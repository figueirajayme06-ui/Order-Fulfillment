namespace OF.UI.Grid;

public static class GridConfig
{
    public const int DefaultPageSize = 1000;
    public static readonly IReadOnlyList<int> PageSizeOptions = new[] { 100, 250, 500, 1000 };
}
