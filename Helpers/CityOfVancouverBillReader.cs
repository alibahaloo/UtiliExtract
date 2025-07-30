using System.Globalization;
using System.Text.RegularExpressions;
using UtiliExtract.Models;

namespace UtiliExtract.Helpers
{
    public static class CityOfVancouverBillReader
    {
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

        private static readonly Regex AccountNumberPattern = new Regex(
            @"ACCT\s*NUMBER:\s*([0-9]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );
        private static readonly Regex BillingDatePattern = new Regex(
            @"BILLING\s*DATE:\s*([A-Za-z]{3,9}\s+\d{1,2},\s*\d{4})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );
        private static readonly Regex NamePattern = new Regex(
            @"NAME:\s*(.*?)\s*\*",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline,
            RegexTimeout
        );
        private static readonly Regex ServiceAddressPattern = new Regex(
            @"FOR\s*SERVICE\s*AT:\s*(.+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );
        private static readonly Regex BillingPeriodPattern = new Regex(
            @"BILLING\s*PERIOD:\s*([A-Za-z]{3,9}\s+\d{1,2},\s*\d{4})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );
        private static readonly Regex DurationEndPattern = new Regex(
            @"TO:\s*([A-Za-z]{3,9}\s+\d{1,2},\s*\d{4})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );
        private static readonly Regex ConsumptionPattern = new Regex(
            @"(\d+)\s+UNITS",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );
        private static readonly Regex AmountDuePattern = new Regex(
            @"IF\s*PAID\s*ON\s*OR\s*BEFORE\s*DUE\s*DATE:\s*\$?([\d,]+\.\d{2})",
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
                AccountNumber = ExtractAccountNumber(fullText),
                BillingDate = ExtractBillingDate(fullText),
                Name = ExtractName(fullText),
                ServiceAddress = ExtractServiceAddress(fullText),
                Consumption = ExtractConsumption(fullText),
                Cost = ExtractAmountDue(fullText)
            };

            (data.DurationStart, data.DurationEnd) = ExtractDuration(fullText);
            return data;
        }

        private static string? ExtractAccountNumber(string text)
        {
            var line = GetLineContaining(text, "ACCT NUMBER:");
            if (line is null) return null;
            var m = AccountNumberPattern.Match(line);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        private static DateTime? ExtractBillingDate(string text)
        {
            var line = GetLineContaining(text, "BILLING DATE:");
            if (line is null) return null;
            var m = BillingDatePattern.Match(line);
            if (m.Success)
            {
                if (DateTime.TryParseExact(
                        m.Groups[1].Value.Trim(),
                        new[] { "MMM d, yyyy", "MMMM d, yyyy" },
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var dt))
                {
                    return dt;
                }
            }
            return null;
        }

        private static string? ExtractName(string text)
        {
            var line = GetLineContaining(text, "NAME:");
            if (line is null) return null;
            var m = NamePattern.Match(line);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        private static string? ExtractServiceAddress(string text)
        {
            var line = GetLineContaining(text, "FOR SERVICE AT:");
            if (line is null) return null;
            var m = ServiceAddressPattern.Match(line);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        private static (DateTime? start, DateTime? end) ExtractDuration(string text)
        {
            DateTime? start = null, end = null;

            var startLine = GetLineContaining(text, "BILLING PERIOD:");
            if (startLine != null)
            {
                var m = BillingPeriodPattern.Match(startLine);
                if (m.Success &&
                    DateTime.TryParseExact(
                        m.Groups[1].Value.Trim(),
                        new[] { "MMM d, yyyy", "MMMM d, yyyy" },
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var ds))
                {
                    start = ds;
                }
            }

            var endLine = GetLineContaining(text, "TO:");
            if (endLine != null)
            {
                var m = DurationEndPattern.Match(endLine);
                if (m.Success &&
                    DateTime.TryParseExact(
                        m.Groups[1].Value.Trim(),
                        new[] { "MMM d, yyyy", "MMMM d, yyyy" },
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var de))
                {
                    end = de;
                }
            }

            return (start, end);
        }

        private static double ExtractConsumption(string text)
        {
            var line = GetLineAfter(text, "CONSUMPTION AMOUNT");
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
            var line = GetLineContaining(text, "IF PAID ON OR BEFORE DUE DATE:");
            if (line != null)
            {
                var m = AmountDuePattern.Match(line);
                if (m.Success &&
                    decimal.TryParse(m.Groups[1].Value.Replace(",", ""), out var d))
                {
                    return d;
                }
            }
            return 0m;
        }

        // Helpers to grab lines
        private static string? GetLineContaining(string text, string anchor)
        {
            int idx = text.IndexOf(anchor, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            int lineStart = text.LastIndexOf('\n', idx);
            int start = lineStart >= 0 ? lineStart + 1 : 0;
            int lineEnd = text.IndexOf('\n', idx);
            int end = lineEnd >= 0 ? lineEnd : text.Length;
            return text.Substring(start, end - start).Trim();
        }

        private static string? GetLineAfter(string text, string anchor)
        {
            int idx = text.IndexOf(anchor, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            int eol = text.IndexOf('\n', idx);
            if (eol < 0) return null;
            int start = eol + 1;
            int nextEol = text.IndexOf('\n', start);
            int end = nextEol >= 0 ? nextEol : text.Length;
            return text.Substring(start, end - start).Trim();
        }
    }
}
