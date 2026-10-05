using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OF.Common.Infrastructure.IPG.Orders.Models.ChangeOrder;
using OF.Common.Infrastructure.IPG.Orders.SOQL;
using OF.Common.Infrastructure.IPG.Orders;
using OF.Common.Infrastructure.IPG.Pricing;
using OF.Common.Infrastructure.MDP;
using OF.Common.Infrastructure.MDP.Models;
using OF.Common.Infrastructure.MDP.Resources;
using OF.Common.Infrastructure.MDP.Services;
using OF.Data;
using OF.Data.Database;
using System.Text.RegularExpressions;
using OF.UI.Database;
using OF.UI.Identity;
using OF.UI.Shared.Controllers;

namespace ExternalConfigurator.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize()]
    public class SalesforceController : CommonController
    {
        private readonly IMemoryCache _memoryCache;
        private readonly IRulesEvaluator _rulesEvaluator;
        private readonly IPricingApi _pricingApi;
        private readonly IDataRepository _repository;
        private readonly IUserIdentity _userIdentity;
        private readonly IOrderManagementIntegration _orderManagementIntegration;
        private readonly FDPDbContext _dbContext;

        public SalesforceController(FDPDbContext dbContext, IMemoryCache memoryCache, IRulesEvaluator rulesEvaluator, IPricingApi pricingApi, IDataRepository repository, IUserIdentity userIdentity, IOrderManagementIntegration orderManagementIntegration) : base(repository)
        {
            _repository = repository;
            _userIdentity = userIdentity;
            _orderManagementIntegration = orderManagementIntegration;
            _dbContext = dbContext;
            _memoryCache = memoryCache;
            _rulesEvaluator = rulesEvaluator;
            _pricingApi = pricingApi;
        }

        [HttpGet]
        [Route("ProductId")]
        public async Task<ActionResult<ConfiguredProduct>> GetProductidFromCode(string productCode)
        {
            if (string.IsNullOrEmpty(productCode))
            {
                return BadRequest("Product code is required.");
            }

            if (_memoryCache.TryGetValue("productid-" + productCode, out ConfiguredProduct cachedProduct))
            {
                return Ok(cachedProduct);
            }

            if (!Regex.IsMatch(productCode, "^[a-zA-Z0-9- /]+$"))
            {
                return BadRequest("Invalid product code format.");
            }

            try
            {
                // Get the fields for the product from Salesforce
                const string soqlTemplate = @"
SELECT
id,
name,
productcode,
generic_code__c as genericcode
from salesforce_raw_bronze_db.product2
WHERE productcode = '{0}';";

                // Safely insert the product ID into the SOQL query
                var soql = string.Format(soqlTemplate, EscapeForSoql(productCode));

                // Query Salesforce using the authenticated client
                var productIds = await _dbContext.SqlQueryAsync<ConfiguredProduct>(soql);

                ConfiguredProduct? result = null;
                foreach (var id in productIds)
                {
                    result = id;
                    break;
                }

                if (result == null)
                {
                    return NotFound();
                }
                else
                {
                    _memoryCache.Set("productid-" + productCode, result, TimeSpan.FromMinutes(3600));
                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                // Return an error response
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet]
        [Route("Configuration")]
        public async Task<ActionResult<ConfiguredProduct>> GetConfigurationFieldsForProduct(string productId)
        {
            if (string.IsNullOrEmpty(productId))
            {
                return BadRequest("Product ID is required.");
            }

            if (_memoryCache.TryGetValue("product-" + productId, out ConfiguredProduct cachedProduct))
            {
                return Ok(cachedProduct);
            }

            if (!Regex.IsMatch(productId, "^[a-zA-Z0-9-]+$"))
            {
                return BadRequest("Invalid product code format.");
            }

            try
            {
                // Describe Product2 if it isn't already in the cache
                SObjectDescribeFull quoteLine = JsonConvert.DeserializeObject<SObjectDescribeFull>(SalesforceSchemas.QuoteLine);

                // Get the fields for the product from Salesforce
                const string soqlTemplate = @"
SELECT
    Id,
    Name,
    SBQQ__DisplayOrder__c as DisplayOrder,
    SBQQ__ColumnOrder__c as ColumnOrder,
    SBQQ__Hidden__c as Hidden,
    SBQQ__Position__c as Position,
    SBQQ__Required__c as Required,
    SBQQ__TargetField__c as TargetField,
    SBQQ__AppliedImmediately__c as AppliedImmediately,
    SBQQ__ShownValues__c as ShownValues,
    SBQQ__HiddenValues__c as HiddenValues,
    SBQQ__ApplyToProductOptions__c as ApplyToProductOptions
FROM salesforce_raw_bronze_db.SBQQ__ConfigurationAttribute__c
WHERE SBQQ__Product__c = '{0}' AND IsDeleted = FALSE
ORDER BY SBQQ__ColumnOrder__c, SBQQ__DisplayOrder__c";

                // Safely insert the product ID into the SOQL query
                var soql = string.Format(soqlTemplate, EscapeForSoql(productId));

                // Query Salesforce using the authenticated client
                var fields = await _dbContext.SqlQueryAsync<ConfigurationField>(soql);

                List<ConfigurationField> attributes = new List<ConfigurationField>();
                foreach (var field in fields)
                {
                    // Get the field metadata from the Product2 object
                    if (quoteLine != null && quoteLine.Fields != null)
                    {
                        field.FieldMetadata = quoteLine!.Fields!.FirstOrDefault(f => f.Name == field.TargetField);
                    }
                    attributes.Add(field);
                }

                const string soqlProductTemplate = @"
select
id,
name,
productcode,
generic_code__c as genericcode
from salesforce_raw_bronze_db.product2
where id = '{0}'";

                var productSoql = string.Format(soqlProductTemplate, EscapeForSoql(productId));

                var product = (await _dbContext.SqlQueryAsync<ConfiguredProduct>(productSoql)).FirstOrDefault();
                product.Attributes = attributes;
                product.Rules = await _rulesEvaluator.GetRulesForProduct(productId);

                _memoryCache.Set("product-" + productId, product, TimeSpan.FromMinutes(3600));
                return Ok(product);
            }
            catch (Exception ex)
            {
                // Return an error response

                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet]
        [Route("Generic")]
        public async Task<ActionResult<ConfiguredProduct>> GetConfigurationFieldsForGeneric(string genericCode)
        {
            if (string.IsNullOrEmpty(genericCode))
            {
                return BadRequest("Generic code is required.");
            }

            if (_memoryCache.TryGetValue("product-" + genericCode, out ConfiguredProduct cachedProduct))
            {
                return Ok(cachedProduct);
            }

            if (!Regex.IsMatch(genericCode, "^[a-zA-Z0-9-]+$"))
            {
                return BadRequest("Invalid product code format.");
            }

            try
            {
                // Describe Product2 if it isn't already in the cache
                SObjectDescribeFull quoteLine = JsonConvert.DeserializeObject<SObjectDescribeFull>(SalesforceSchemas.QuoteLine);

                const string soqlProductTemplate = @"
select
id,
name,
productcode,
generic_code__c as genericcode
from salesforce_raw_bronze_db.product2
where generic_code__c = '{0}' and isactive = TRUE";

                var productSoql = string.Format(soqlProductTemplate, EscapeForSoql(genericCode));

                var product = (await _dbContext.SqlQueryAsync<ConfiguredProduct>(productSoql)).FirstOrDefault();

                if (product == null)
                {
                    return NotFound();
                }

                // Get the fields for the product from Salesforce
                const string soqlTemplate = @"
SELECT
    Id,
    Name,
    SBQQ__DisplayOrder__c as DisplayOrder,
    SBQQ__ColumnOrder__c as ColumnOrder,
    SBQQ__Hidden__c as Hidden,
    SBQQ__Position__c as Position,
    SBQQ__Required__c as Required,
    SBQQ__TargetField__c as TargetField,
    SBQQ__AppliedImmediately__c as AppliedImmediately,
    SBQQ__ShownValues__c as ShownValues,
    SBQQ__HiddenValues__c as HiddenValues,
    SBQQ__ApplyToProductOptions__c as ApplyToProductOptions
FROM salesforce_raw_bronze_db.SBQQ__ConfigurationAttribute__c
WHERE SBQQ__Product__c = '{0}' AND IsDeleted = FALSE
ORDER BY SBQQ__ColumnOrder__c, SBQQ__DisplayOrder__c";

                // Safely insert the product ID into the SOQL query
                var soql = string.Format(soqlTemplate, EscapeForSoql(product.Id));

                // Query Salesforce using the authenticated client
                var fields = await _dbContext.SqlQueryAsync<ConfigurationField>(soql);

                List<ConfigurationField> attributes = new List<ConfigurationField>();
                foreach (var field in fields)
                {
                    // Get the field metadata from the Product2 object
                    if (quoteLine != null && quoteLine.Fields != null)
                    {
                        field.FieldMetadata = quoteLine!.Fields!.FirstOrDefault(f => f.Name == field.TargetField);
                    }
                    attributes.Add(field);
                }


                product.Attributes = attributes;
                product.Rules = await _rulesEvaluator.GetRulesForProduct(genericCode);

                _memoryCache.Set("product-" + genericCode, product, TimeSpan.FromMinutes(3600));
                return Ok(product);
            }
            catch (Exception ex)
            {
                // Return an error response

                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet]
        [Route("AttributesForNOF")]
        public ActionResult GetAttributesForNOFFromConfigurator(string json)
        {
            // Map attributes from the Configurator to NOF

            return new ObjectResult(new { attributes = "Shift factor:8 Running hours p/d;Telemetry:Yes;Voltage:240V 1-phase @ 50 Hz" });
        }

        [HttpGet]
        [Route("AttributesAndAdditionalItemsForConfigurator")]
        public ActionResult GetAttributesAndAdditionalItemsForConfigurator(string genericCode, string? attributes, string quantity)
        {
            decimal.TryParse(quantity, out decimal quantityParsed);

            // MAp attributes from NOF to the Configurator Salesforce fields

            var data = new
            {
                configurationAttributes = new
                {
                    SBQQ__Quantity__c = quantityParsed
                },
                optionConfigurations = new
                {
                    requiredItems = new string[0],
                    recommendedItems = new string[0],
                    additionalItems = new string[0],
                    services = new string[0]
                }
            };

            var json = JsonConvert.SerializeObject(data, Formatting.None, new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = null
                }
            });

            return Ok(json);
        }

        [HttpGet]
        [Route("PricesForProducts")]
        public async Task<ActionResult> GetPricesForProducts(string json)
        {
            var response = JsonConvert.DeserializeObject<ConfiguratorResponse>(json);

            var pricingResponse = await _pricingApi.CalculateAsync(response.CustomAttributes.PricingRequest);

            return Ok(pricingResponse);
        }

        [HttpGet]
        [Route("AdditionalInformationForHeaderAndLinesForPricing")]
        public async Task<ActionResult> GetAdditionalInformationForHeaderAndLinesForPricing(int headerId, int changeId)
        {
            var header = _repository.GetHeaderWithChanges(headerId);
             var change = header?.ChangeOrders?.FirstOrDefault(i => i.Id == changeId);

            if (change == null)
            {
                return BadRequest(new { success = false, message = "Change order not found" });
            }

            if (!change.IsEditable)
            {
                return BadRequest(new { success = false, message = $"Change is not longer editable, status: {((ChangeStatus)change.Status).ToString()}" });
            }

            if (string.IsNullOrWhiteSpace(header?.OrderNumber))
            {
                return BadRequest(new { success = false, message = $"Header does not have an associated SalesforceId" });
            }

            try
            {
                string soqlQuery = string.Format(OFSOQL.GetOrderChangeorder, header.OrderNumber);

                var response = await _orderManagementIntegration.Query<SalesforceChangeOrder>(soqlQuery);

                if (response?.Records == null || !response.Records.Any())
                {
                    return BadRequest(new { success = false, message = "No order found in salesforce with ID: " + header.OrderNumber });
                }

                var salesforceChangeOrder = response.Records.First();

                UpdateHeader(headerId, changeId, salesforceChangeOrder, change);

                if (salesforceChangeOrder.PrimaryContact != null)
                {
                    UpdateContact(ContactType.Primary, salesforceChangeOrder.PrimaryContact, change);
                }

                if (salesforceChangeOrder.BillingContact != null)
                {
                    UpdateContact(ContactType.Billing, salesforceChangeOrder.BillingContact, change);
                }

                if (salesforceChangeOrder.ArmContact != null)
                {
                    UpdateContact(ContactType.ARM, salesforceChangeOrder.ArmContact, change);
                }

                if (salesforceChangeOrder.SiteContact != null)
                {
                    UpdateContact(ContactType.Site, salesforceChangeOrder.SiteContact, change);
                }

                // Update the addresses if they exist
                if (salesforceChangeOrder.BillingAddress != null)
                {
                    UpdateAddress(AddressType.Invoice, salesforceChangeOrder.BillingAddress, change);
                }

                if (salesforceChangeOrder.ShippingAddress != null)
                {
                    UpdateAddress(AddressType.Shipping, salesforceChangeOrder.ShippingAddress, change);
                }

                // Fetch and process order lines
                await UpdateOrderLines(header.OrderNumber, change);

                return Ok(new { success = true, message = "Successfully enriched from Salesforce" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Error enriching from Salesforce: " + ex.Message });
            }
        }

        private async Task UpdateOrderLines(string orderNumber, ChangeOrder change)
        {
            // Fix the query to include the closing brace
            string soqlQuery = string.Format(OFSOQL.GetOrderLineChangeorder, orderNumber);

            var response = await _orderManagementIntegration.Query<SalesforceChangeOrderLine>(soqlQuery);

            if (response?.Records == null || !response.Records.Any())
            {
                return;
            }

            var salesforceOrderLines = response.Records;
            var existingChangeOrderLines = change.ChangeOrderLines?.ToList() ?? new List<ChangeOrderLine>();
            var linesToUpdate = new List<ChangeOrderLine>();
            var salesforceLineNumbers = salesforceOrderLines.Select(l => l.M3LineNumberId).Where(id => id != null).ToList();

            foreach (var salesforceLine in salesforceOrderLines)
            {
                var headerLine = change.Header.Lines.FirstOrDefault(l => l.AgreementLineNumber == salesforceLine.M3LineNumberId);

                var existingChangeLine = existingChangeOrderLines
                    .FirstOrDefault(cl => headerLine != null && cl.LineId == headerLine.Id);

                var changeLine = existingChangeLine ?? new ChangeOrderLine
                {
                    LineId = headerLine?.Id,
                    ChangeOrderId = change.Id,
                    Status = (int)ChangeStatus.Pending,
                    GenericItemNumber = salesforceLine.QuoteLine?.ProductName ?? headerLine?.GenericItemNumber,
                    ItemNumber = headerLine?.ItemNumber
                };

                DateTime? onHireDate = null;
                if (DateTime.TryParse(salesforceLine.OnHireDate, out var parsedOnHireDate))
                {
                    onHireDate = parsedOnHireDate;
                }

                DateTime? offHireDate = null;
                if (DateTime.TryParse(salesforceLine.OffHireDate, out var parsedOffHireDate))
                {
                    offHireDate = parsedOffHireDate;
                }

                changeLine.OnHireDate = onHireDate;
                changeLine.OffHireDate = offHireDate;
                changeLine.Quantity = (float?)salesforceLine.Quantity ?? headerLine.Quantity;
                changeLine.Attributes = headerLine?.Attributes;
                changeLine.Price = salesforceLine.TotalPrice;
                changeLine.DeliveryDate = headerLine?.DeliveryDate;
                changeLine.TerminationDate = headerLine?.TerminationDate;
                changeLine.CollectionDate = headerLine?.CollectionDate;
                changeLine.Warehouse = headerLine.Warehouse;

                linesToUpdate.Add(changeLine);
            }

            foreach (var headerLine in change.Header.Lines)
            {
                if (!salesforceLineNumbers.Contains(headerLine.AgreementLineNumber) && !headerLine.IsDeleted)
                {
                    headerLine.IsDeleted = true;
                    await _dbContext.SaveChangesAsync();
                }
            }

            _repository.UpsertChangeOrderLines(linesToUpdate);
        }

        private void UpdateHeader(int headerId, int changeId, SalesforceChangeOrder salesforceChangeOrder, ChangeOrder change)
        {
            var changeHeader = change.ChangeOrderHeader;
            bool isNew = false;

            if (changeHeader == null)
            {
                changeHeader = new ChangeOrderHeader()
                {
                    HeaderId = headerId,
                    Status = (int)ChangeStatus.Pending,
                    ChangeOrderId = changeId
                };
                isNew = true;
            }

            DateTime? onHireDate = null;
            if (DateTime.TryParse(salesforceChangeOrder.OnhireDate, out var parsedOnHireDate))
            {
                onHireDate = parsedOnHireDate;
            }

            DateTime? offHireDate = null;
            if (DateTime.TryParse(salesforceChangeOrder.OffhireDate, out var parsedOffHireDate))
            {
                offHireDate = parsedOffHireDate;
            }

            changeHeader.OnHireDate = onHireDate;
            changeHeader.OffHireDate = offHireDate;
            changeHeader.RateType = salesforceChangeOrder.RateType;
            changeHeader.JobAic = salesforceChangeOrder.JobAic;
            changeHeader.ProjectCode = salesforceChangeOrder.ProjectCode;
            changeHeader.Ponumber = salesforceChangeOrder.PoNumber;
            changeHeader.Poamount = salesforceChangeOrder.PoAmount?.ToString();

            int? rentalDuration = null;
            if (salesforceChangeOrder.RentalPeriod.HasValue)
            {
                rentalDuration = (int)salesforceChangeOrder.RentalPeriod.Value;
            }

            int? minimumRental = null;
            if (salesforceChangeOrder.QuoteReference?.MinimumRental.HasValue == true)
            {
                minimumRental = (int)salesforceChangeOrder.QuoteReference.MinimumRental.Value;
            }

            int? daysInWeek = null;
            if (int.TryParse(salesforceChangeOrder.DaysInWeek, out var parsedDaysInWeek))
            {
                daysInWeek = parsedDaysInWeek;
            }

            int? weeksInMonth = null;
            if (int.TryParse(salesforceChangeOrder.WeeksInMonth, out var parsedWeeksInMonth))
            {
                weeksInMonth = parsedWeeksInMonth;
            }

            changeHeader.RentalDuration = rentalDuration;
            changeHeader.MinimumRental = minimumRental;
            changeHeader.DaysInWeek = daysInWeek;
            changeHeader.WeeksInMonth = weeksInMonth;
            changeHeader.PriceBase = salesforceChangeOrder.QuoteReference?.TotalNetPrice;
            changeHeader.TargetCustomerAmount = salesforceChangeOrder.QuoteReference?.TargetCustomerAmount;
            changeHeader.PriceAdjustment = salesforceChangeOrder.QuoteReference?.PriceAdjustment;

            _repository.UpsertChangeOrderHeader(changeHeader, add: isNew);
        }

        private void UpdateContact(ContactType contactType, SalesforceChangeOrderContact salesforceContact, ChangeOrder change)
        {
            ChangeOrderContact existingContact = null;

            switch (contactType)
            {
                case ContactType.Primary:
                    existingContact = change.PrimaryContact;
                    break;
                case ContactType.Billing:
                    existingContact = change.BillingContact;
                    break;
                case ContactType.ARM:
                    existingContact = change.Armcontact;
                    break;
                case ContactType.Site:
                    existingContact = change.SiteContact;
                    break;
            }

            var contact = existingContact ?? new ChangeOrderContact
            {
                ContactType = (int)contactType
            };

            // Extract first and last name from the full name
            string firstName = string.Empty;
            string lastName = string.Empty;

            if (!string.IsNullOrEmpty(salesforceContact.Name) && salesforceContact.Name.Contains(" "))
            {
                var nameParts = salesforceContact.Name.Split(' ', 2);
                firstName = nameParts[0];
                lastName = nameParts[1];
            }

            contact.Title = salesforceContact.Salutation!;
            contact.FirstName = firstName;
            contact.LastName = lastName;
            contact.Phone = salesforceContact.Phone;
            contact.Mobile = salesforceContact.MobilePhone;
            contact.Email = salesforceContact.Email!;
            contact.M3number = salesforceContact.M3CustomerNumber;
            contact.SalesforceId = salesforceContact.M3ContactCode;

            var savedContact = _repository.UpsertChangeOrderContact(contact, existingContact == null);

            switch (contactType)
            {
                case ContactType.Primary:
                    change.PrimaryContactId = savedContact.Id;
                    break;
                case ContactType.Billing:
                    change.BillingContactId = savedContact.Id;
                    break;
                case ContactType.ARM:
                    change.ArmcontactId = savedContact.Id;
                    break;
                case ContactType.Site:
                    change.SiteContactId = savedContact.Id;
                    break;
            }

            _repository.UpdateChangeOrder(change);
        }

        private void UpdateAddress(AddressType addressType, SalesforceChangeOrderAddress salesforceAddress, ChangeOrder change)
        {
            var isInvoiceAddress = addressType == AddressType.Invoice;
            var existingAddress = isInvoiceAddress ? change.InvoiceAddress : change.SiteAddress;

            var address = existingAddress ?? new ChangeOrderAddress
            {
                AddressType = (int)addressType
            };

            // Generate a name for the address based on street and city
            var addressName = salesforceAddress.Street;
            if (!string.IsNullOrEmpty(salesforceAddress.City))
            {
                addressName = $"{salesforceAddress.Street}, {salesforceAddress.City}";
            }

            address.AddressName = addressName;
            address.Street = salesforceAddress.Street;
            address.City = salesforceAddress.City;
            address.StateOrProvince = salesforceAddress.State ?? salesforceAddress.StateCode;
            address.ZipOrPostalCode = salesforceAddress.PostalCode;
            address.Country = salesforceAddress.Country;

            var savedAddress = _repository.UpsertChangeOrderAddress(address, existingAddress == null);

            if (isInvoiceAddress)
            {
                change.InvoiceAddressId = savedAddress.Id;
            }
            else
            {
                change.SiteAddressId = savedAddress.Id;
            }

            _repository.UpdateChangeOrder(change);
        }

        private static string EscapeForSoql(string input)
        {
            if (input == null)
                return string.Empty;

            // Escape single quotes by doubling them (Salesforce SOQL requirement)
            return input.Replace("'", "''");
        }
    }
}

