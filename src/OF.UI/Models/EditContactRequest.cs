namespace OF.UI.Controllers
{
    public record EditContactRequest(
        int Id,
        string Title,
        string FirstName,
        string LastName,
        string Phone,
        string Mobile,
        string Email,
        string M3number,
        string SalesforceId);
}
