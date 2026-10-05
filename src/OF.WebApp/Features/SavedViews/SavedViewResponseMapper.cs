using System.Text.Json;
using OF.Data.Database;
using OF.WebApp.Controllers;
using OF.WebApp.Features.Authorization;

namespace OF.WebApp.Features.SavedViews;

internal static class SavedViewResponseMapper
{
    public static SavedViewDto Map(
        View view,
        User caller,
        string normalizedPage,
        JsonElement state,
        bool callerCanManage = false,
        IReadOnlyCollection<SavedViewRecipientDto>? recipientMetadata = null)
    {
        var isOwner = string.Equals(view.Owner, caller.LoginName, StringComparison.OrdinalIgnoreCase);
        var canManage = !ReadOnlyAccess.IsReadOnly(caller)
            && (callerCanManage || caller.IsAdmin || isOwner);
        var recipients = recipientMetadata
            ?? view.ViewRecipients
                .Select(recipient => new SavedViewRecipientDto
                {
                    LoginName = recipient.RecipientLoginName,
                    FullName = string.IsNullOrWhiteSpace(recipient.Recipient?.FullName)
                        ? recipient.RecipientLoginName
                        : recipient.Recipient.FullName.Trim(),
                })
                .ToArray();

        return new SavedViewDto
        {
            Id = view.Id,
            Name = view.Name,
            Page = normalizedPage,
            Scope = MapScope(view.ForEveryone, recipients.Count > 0),
            Owner = view.Owner,
            IsOwner = isOwner,
            CanEdit = canManage,
            CanDelete = canManage,
            Recipients = canManage ? recipients.ToArray() : [],
            IsDefault = view.Id == 1,
            State = state.Clone(),
        };
    }

    private static string MapScope(int forEveryone, bool hasRecipients) => forEveryone switch
    {
        1 => "global",
        2 => "division",
        0 when hasRecipients => "users",
        _ => "personal",
    };
}
