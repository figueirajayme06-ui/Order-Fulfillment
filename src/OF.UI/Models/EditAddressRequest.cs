namespace OF.UI.Controllers
{
    public record EditAddressRequest(
        int Id,
        string AddressName,
        string Street,
        string City,
        string StateOrProvince,
        string ZipOrPostalCode,
        string Country,
        string M3number,
        string SalesforceId);
}
