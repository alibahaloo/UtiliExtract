using System.Text.RegularExpressions;
using UtiliExtract.Models;
namespace UtiliExtract.Helpers
{
    public class FortisBCElecBillReader
    {
        public static BillData GetInvoiceData(string fullText)
        {
            var usageType = UsageType.Electricity;
            var data = new BillData
            {
                IsMetered = true,
                UsageType = usageType,
                UsageUnit = BillMetadata.GetUsageUnit(usageType),
                AccountNumber = TryExtractAccountNumber(fullText),
                Name = TryExtractName(fullText),
                ServiceAddress = TryExtractServiceAddress(fullText),
                BillingDate = TryExtractBillingDate(fullText),
                AmountDue = TryExtractAmountDue(fullText),
                Usage = TryExtractUsage(fullText),
            };
            (data.DurationStart, data.DurationEnd) = TryExtractBillingPeriod(fullText);

            return data;
        }
        private static string TryExtractAccountNumber(string text)
        {
            // Pattern: "Account number: 5257631506-4"
            var pattern = @"Account number[:\s]+([0-9\-]+)";
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }

        private static string TryExtractName(string text)
        {
            var match = Regex.Match(text, @"Name:\s*(.*?)\s+Service address:", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }

        private static string TryExtractServiceAddress(string text)
        {
            var match = Regex.Match(text, @"Service address:\s*(.*?)(?=Due)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }

        private static DateTime? TryExtractBillingDate(string text)
        {
            // Pattern: "Billing date: Jun 02, 2025"
            var pattern = @"Billing date[:\s]+([A-Za-z]{3,9} \d{1,2}, \d{4})";
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (DateTime.TryParse(match.Groups[1].Value.Trim(), out var parsedDate))
                {
                    return parsedDate;
                }
            }
            return null;
        }

        private static (DateTime? start, DateTime? end) TryExtractBillingPeriod(string text)
        {
            // Pattern: "Billing period: May 02-Jun 02, 2025"
            var pattern = @"Billing period[:\s]+([A-Za-z]{3,9} \d{1,2})\s*[-–]\s*([A-Za-z]{3,9} \d{1,2}),\s*(\d{4})";
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var monthDayStart = match.Groups[1].Value.Trim();
                var monthDayEnd = match.Groups[2].Value.Trim();
                var year = match.Groups[3].Value.Trim();

                if (DateTime.TryParse($"{monthDayStart}, {year}", out var parsedStart) &&
                    DateTime.TryParse($"{monthDayEnd}, {year}", out var parsedEnd))
                {
                    return (parsedStart, parsedEnd);
                }
            }
            return (null, null);
        }

        private static decimal TryConvertCurrency(string input)
        {
            if (decimal.TryParse(input.Replace("$", string.Empty).Replace(",", string.Empty), out var value))
            {
                return value;
            }
            return 0m;
        }

        private static decimal TryExtractAmountDue(string text)
        {
            // Pattern: "Amount due: $1,045.02"
            var pattern = @"Amount due[:\s]*\$([0-9,]+\.?[0-9]{0,2})";
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            return match.Success ? TryConvertCurrency(match.Groups[1].Value) : 0m;
        }

        private static double TryExtractUsage(string text)
        {
            // First try: pattern with "at" to get correct usage
            var patternAt = @"You used\s*([0-9,]+)\s*kWh\s*at";
            var matchAt = Regex.Match(text, patternAt, RegexOptions.IgnoreCase);
            if (matchAt.Success)
            {
                var numeric = matchAt.Groups[1].Value.Replace(",", string.Empty);
                if (int.TryParse(numeric, out var usage))
                {
                    return usage;
                }
            }

            // Fallback: any "You used X kWh"
            var pattern = @"You used\s*([0-9,]+)\s*kWh";
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var numeric = match.Groups[1].Value.Replace(",", string.Empty);
                if (double.TryParse(numeric, out var usage))
                {
                    return usage;
                }
            }
            return 0;
        }
    }
}
