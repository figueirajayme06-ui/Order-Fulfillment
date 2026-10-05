using OF.Common.Infrastructure.CloudSuite.Models.Agreements.BODs;
using OF.Common.Models;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace OF.Common.Utils
{
    public static class StringX
    {
        [return: NotNullIfNotNull(nameof(value))]
        public static string? Truncate(this string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return value.Length <= maxLength ? value : value[..maxLength];
        }

        public static int ConvertIntFromDecimalString(this string? value)
        {
            if (value is null)
            {
                return -1;
            }

            if (decimal.TryParse(value, out decimal result))
            {
                return Convert.ToInt32(result);
            }

            return -1;
        }

        public static bool IsExcludedWarehouse(this string? str)
        {
            // If this is null, return true

            if (str is null)
            {
                return true;
            }

            // If this is is all numeric, more than 3 characters or doesnt end in a 0 or 5, return true

            if (!Regex.IsMatch(str, @"^(?=.*[^0-9])(?=.*[05]$).{3}$"))
            {
                return true;
            }

            // If this is in the excludes list, return true

            return Constants.Warehouses.IsExcluded(str);
        }

        public static bool IsExcludedLine(this string? str)
        {
            // If this is null, return true

            if (str is null)
            {
                return true;
            }

            // If xxmisc this is not excluded

            if (str.StartsWith("XXMISC", StringComparison.CurrentCultureIgnoreCase))
            {
                return false;
            }

            // If this starts with any of the excludes, exclude it

            return Constants.Lines.Excludes.Any(i => str?.StartsWith(i, StringComparison.CurrentCultureIgnoreCase) == true);
        }

        public static string ReplaceHtmlTags(this string? str, string? replacement = null)
        {
            if (str is null)
            {
                return null!;
            }

            return Regex.Replace(str, "<.*?>", replacement ?? string.Empty);
        }

        public static T ParseToBODResponse<T>(this string? body) where T : class
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                throw new ArgumentNullException(nameof(body));
            }

            XmlSerializer serializer = new XmlSerializer(typeof(T));
            using StringReader reader = new StringReader(body);
            return (serializer.Deserialize(reader) as T)!;
        }

        public static bool IsTOrAAgreement(this string? agreementNumber)
        {
            if (string.IsNullOrWhiteSpace(agreementNumber))
            {
                return false;
            }

            var lowerAgreementNumber = agreementNumber.ToUpper();
            return lowerAgreementNumber.StartsWith("T") || lowerAgreementNumber.StartsWith("A");
        }

        public static string EnsureWarehouseIsZero(this string? input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            Regex regex = new Regex(@"^[A-Za-z]{2}\d+$");

            if (regex.IsMatch(input))
            {
                if (input[2] != '0')
                {
                    return input.Substring(0, 2) + "0";
                }
            }

            return input;
        }

        public static ItemsFromAttributes GetDefaultItemsFromBOD(this SyncAGKRentalOrderLine bod)
        {
            return new ItemsFromAttributes()
            {
                ItemNumber = bod.DataArea.AGKRentalOrderLine.AgreementLines.ItemNumber,
                LotNumber = bod.DataArea.AGKRentalOrderLine.AgreementLines.LotNumber ?? bod.DataArea.AGKRentalOrderLine.AgreementLines.ItemNumber,
                Warehouse = bod.DataArea.AGKRentalOrderLine.AgreementLines.FromWarehouse,
                Quantity = bod.DataArea.AGKRentalOrderLine.AgreementLines.OrderedQuantity
            };
        }

        public static ItemsFromAttributes GetItemsFromAttributeText(this string? input, SyncAGKRentalOrderLine bod)
        {
            var itemFromAttributes = GetDefaultItemsFromBOD(bod);

            if (string.IsNullOrWhiteSpace(input))
            {
                return itemFromAttributes;
            }

            // serialized 
            string serializedItemPattern = @"Item to Pick:\[([^\]]+)\]\s*\[([^\]]+)\]";
            Match match = Regex.Match(input, serializedItemPattern);

            if (match.Success)
            {
                itemFromAttributes.ItemNumber = match.Groups[1].Value;
                itemFromAttributes.LotNumber = match.Groups[2].Value;
                itemFromAttributes.ContainsAllocation = true;
                return itemFromAttributes;
            }

            // non-serialized
            string nonSerializedItemPattern = @"Item to Pick:\[([^\]]+)\]\s*x([\d.]+)";
            match = Regex.Match(input, nonSerializedItemPattern);

            if (match.Success)
            {
                itemFromAttributes.ItemNumber = match.Groups[1].Value;
                itemFromAttributes.LotNumber = match.Groups[1].Value;
                itemFromAttributes.Quantity = float.Parse(match.Groups[2].Value);
                itemFromAttributes.ContainsAllocation = true;
                return itemFromAttributes;
            }

            // depotfulfil
            string depotfulfilPattern = @"Depot fulfills from:\[([^\]]+)\]\s*\[([^\]]+)\]\s*Qty\s*:\s*x([\d.]+)";
            match = Regex.Match(input, depotfulfilPattern);

            if (match.Success)
            {
                itemFromAttributes.IsDepotFulfil = true;
                itemFromAttributes.Warehouse = match.Groups[1].Value;
                itemFromAttributes.Quantity = float.Parse(match.Groups[3].Value);
                itemFromAttributes.ContainsAllocation = true;
                return itemFromAttributes;
            }

            return itemFromAttributes;
        }

        /// <summary>
        /// Trims whitespace at the beginning, end and replaces all whitespace sequences
        /// in the middle with a single space
        /// </summary>
        [return: NotNullIfNotNull(nameof(value))]
        public static string? TrimAllWhiteSpace(this string? value)
        {
            if (value == null)
            {
                return null;
            }

            var sb = new StringBuilder();

            bool lastCharacterWasWhiteSpace = false;

            foreach (var ch in value.Trim())
            {
                if (char.IsWhiteSpace(ch))
                {
                    if (!lastCharacterWasWhiteSpace)
                    {
                        lastCharacterWasWhiteSpace = true;
                        sb.Append(' ');
                    }
                }
                else
                {
                    lastCharacterWasWhiteSpace = false;
                    sb.Append(ch);
                }
            }

            return sb.ToString();
        }
    }
}
