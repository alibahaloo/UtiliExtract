using System.Globalization;
using System.Text.RegularExpressions;
using UtiliExtract.Models;

namespace UtiliExtract.Helpers
{
    public static class CityOfWilliamsLakeBillReader
    {
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

        private static readonly Regex NamePattern = new Regex(
            @"OWNER:\s*(.*?)\s+Amoun",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline,
            RegexTimeout
        );
        private static readonly Regex ServiceAddressPattern = new Regex(
            @"SERVICE ADDRESS:\s*(.*?)\s+Payments",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );
        private static readonly Regex ServicePeriodPattern = new Regex(
            @"([A-Za-z]{3}\s+\d{2}/\d{2})\s+([A-Za-z]{3}\s+\d{2}/\d{2})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );
        private static readonly Regex BillingInfoPattern = new Regex(
            @"^([A-Za-z]{3}\s+\d{2}/\d{2})\s+(\d+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );
        private static readonly Regex AmountDuePattern = new Regex(
            @"([0-9]+\.[0-9]{2})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );
        private static readonly Regex ConsumptionPattern = new Regex(
            @"(\d+)\s+units",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );

        public static BillData GetInvoiceData(string fullText)
        {
            var usageType = UsageType.Water;
            var data = new BillData
            {
                IsMetered = true,
                UsageType = usageType,
                UsageUnit = BillMetadata.GetUsageUnit(usageType),
                Name = ExtractName(fullText),
                ServiceAddress = ExtractServiceAddress(fullText),
                AccountNumber = ExtractAccountNumber(fullText),
                BillingDate = ExtractBillingDate(fullText),
                Consumption = ExtractConsumption(fullText),
                Charges = ExtractAmountDue(fullText)
            };

            (data.DurationStart, data.DurationEnd) = ExtractDuration(fullText);
            return data;
        }

        private static string? ExtractName(string text)
        {
            var line = GetLineContaining(text, "OWNER:");
            if (line == null) return null;

            var m = NamePattern.Match(line);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        private static string? ExtractServiceAddress(string text)
        {
            var line = GetLineContaining(text, "SERVICE ADDRESS:");
            if (line == null) return null;

            var m = ServiceAddressPattern.Match(line);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        private static (DateTime? start, DateTime? end) ExtractDuration(string text)
        {
            var line = GetLineAfter(text, "SERVICE PERIOD");
            if (line != null)
            {
                var m = ServicePeriodPattern.Match(line);
                if (m.Success)
                {
                    if (DateTime.TryParseExact(m.Groups[1].Value, "MMM dd/yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) &&
                        DateTime.TryParseExact(m.Groups[2].Value, "MMM dd/yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
                    {
                        return (start, end);
                    }
                }
            }
            return (null, null);
        }

        private static DateTime? ExtractBillingDate(string text)
        {
            var first = GetLineAfter(text, "BILLING DATE ACCOUNT NUMBER");
            if (first == null) return null;

            var line = GetLineAfter(text, first);
            if (line == null) return null;

            var m = BillingInfoPattern.Match(line);
            if (m.Success)
            {
                if (DateTime.TryParseExact(m.Groups[1].Value, "MMM dd/yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    return dt;
            }
            return null;
        }

        private static string? ExtractAccountNumber(string text)
        {
            var first = GetLineAfter(text, "BILLING DATE ACCOUNT NUMBER");
            if (first == null) return null;

            var line = GetLineAfter(text, first);
            if (line == null) return null;

            var m = BillingInfoPattern.Match(line);
            return m.Success ? m.Groups[2].Value.Trim() : null;
        }

        private static double ExtractConsumption(string text)
        {
            var line = GetLineContaining(text, "units");
            if (line != null)
            {
                var m = ConsumptionPattern.Match(line);
                if (m.Success && double.TryParse(m.Groups[1].Value, out var d))
                    return d;
            }
            return 0;
        }

        private static decimal ExtractAmountDue(string text)
        {
            var line = GetLineContaining(text, "TOTAL CURRENT");
            if (line != null)
            {
                // Pattern: "TOTAL CURRENT 141.80"
                var m = AmountDuePattern.Match(line);
                if (m.Success && decimal.TryParse(m.Groups[1].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d))
                    return d;
            }
            return 0m;
        }


        private static string? GetLineContaining(string text, string anchor)
        {
            var idx = text.IndexOf(anchor, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;

            var lineStart = text.LastIndexOf('\n', idx);
            var start = lineStart >= 0 ? lineStart + 1 : 0;
            var lineEnd = text.IndexOf('\n', idx);
            var end = lineEnd >= 0 ? lineEnd : text.Length;

            return text.Substring(start, end - start).Trim();
        }

        private static string? GetLineAfter(string text, string anchor)
        {
            var idx = text.IndexOf(anchor, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;

            var eol = text.IndexOf('\n', idx);
            if (eol < 0) return null;

            var start = eol + 1;
            var nextEol = text.IndexOf('\n', start);
            var end = nextEol >= 0 ? nextEol : text.Length;

            return text.Substring(start, end - start).Trim();
        }
    }
}
