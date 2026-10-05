using Microsoft.AspNetCore.Mvc.Rendering;
using OF.Common.Infrastructure.MDP.Services;

namespace OF.UI.Shared.Helpers;

public static class DropDownHelpers
{
    public static IList<SelectListItem> SegmentItems(this IHtmlHelper html, string? value)
    {
        return SalesforcePickListService.Segments().Select(i =>
            new SelectListItem(i.Value, i.Key, value == null ? false : value.Equals(i.Value, StringComparison.InvariantCultureIgnoreCase)))
            .ToList();
    }

    public static IList<SelectListItem> SalutationsItems(this IHtmlHelper html, string? value)
    {
        return SalesforcePickListService.Salutations().Select(i =>
            new SelectListItem(i.Value, i.Key, value == null ? false : value.Equals(i.Value, StringComparison.InvariantCultureIgnoreCase)))
            .ToList();
    }
}
