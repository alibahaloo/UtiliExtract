using System.Globalization;
using System.Text.RegularExpressions;
using UtiliExtract.Models;

namespace UtiliExtract.Helpers
{
    public static class DirectEnergyBillReader
    {
        // Prevent hangs: compiled, culture-invariant with timeout
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

        // Precompiled, culture-invariant patterns
        private static readonly Regex BillingDatePattern = new Regex(
            @"^Invoice Date:\s*(\d{1,2}-[A-Za-z]{3}-\d{2})",
            RegexOptions.Multiline             // ← allow ^ to match start of any line
            | RegexOptions.IgnoreCase
            | RegexOptions.Compiled
            | RegexOptions.CultureInvariant,
            RegexTimeout
        );
        private static readonly Regex BillingPeriodPattern = new Regex(
            @"Billing Period:\s*([A-Za-z]+\s+\d{4})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );
        private static readonly Regex AmountDuePattern = new Regex(
            @"Subtotal:\s*\$\s*([\d,]+\.\d{2})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );
        private static readonly Regex UsagePattern = new Regex(
            @"Total Usage \(GJs\):\s*(\d+\.\d{2})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );
        private static readonly Regex AccountPattern = new Regex(
            @"Utility Account:\s*(.*)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );

        public static BillData GetInvoiceData(string fullText)
        {
            var usageType = UsageType.Gas;
            var data = new BillData
            {
                IsMetered = true,
                UsageType = usageType,
                UsageUnit = UsageUnit.GJ,
                AccountNumber = ExtractAccountNumber(fullText),
                Name = ExtractName(fullText),
                BillingDate = ExtractBillingDate(fullText),
                Charges = ExtractAmountDue(fullText),
                Consumption = ExtractUsage(fullText)
            };
            (data.DurationStart, data.DurationEnd) = TryExtractBillingPeriod(fullText);
            return data;
        }

        private static DateTime? ExtractBillingDate(string text)
        {
            // Because BillingDatePattern is compiled with RegexOptions.Multiline and ^ anchor,
            // this will only match the line that starts with "Invoice Date: dd-MMM-yy"
            var m = BillingDatePattern.Match(text);
            if (m.Success && DateTime.TryParseExact(
                    m.Groups[1].Value,
                    "dd-MMM-yy",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var dt))
            {
                return dt;
            }
            return null;
        }


        private static (DateTime? start, DateTime? end) TryExtractBillingPeriod(string text)
        {
            var line = GetLineContaining(text, "Billing Period:");
            if (line != null)
            {
                var m = BillingPeriodPattern.Match(line);
                if (m.Success && DateTime.TryParseExact(
                        m.Groups[1].Value,
                        "MMMM yyyy",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var start))
                {
                    var end = new DateTime(start.Year, start.Month, DateTime.DaysInMonth(start.Year, start.Month));
                    return (start, end);
                }
            }
            return (null, null);
        }

        private static decimal ExtractAmountDue(string text)
        {
            // look for the line that actually contains "Subtotal:"
            var line = GetLineContaining(text, "Subtotal:");
            if (line != null)
            {
                var m = AmountDuePattern.Match(line);
                if (m.Success
                    && decimal.TryParse(
                           m.Groups[1].Value.Replace(",", string.Empty),
                           NumberStyles.AllowDecimalPoint,
                           CultureInfo.InvariantCulture,
                           out var d))
                {
                    return d;
                }
            }
            return 0m;
        }


        private static string? ExtractName(string text)
        {
            // Look for 'Product:' and take everything before it as the name
            const string anchor = "Product:";
            int idx = text.IndexOf(anchor, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                // find start of the line
                int lineStart = text.LastIndexOf('\n', idx);
                int start = lineStart >= 0 ? lineStart + 1 : 0;
                // extract up to the anchor
                string segment = text.Substring(start, idx - start).Trim();
                return segment;
            }
            return null;
        }

        private static double ExtractUsage(string text)
        {
            var line = GetLineContaining(text, "Total Usage (GJs):");
            if (line != null)
            {
                var m = UsagePattern.Match(line);
                if (m.Success && double.TryParse(m.Groups[1].Value, out var d))
                    return d;
            }
            return 0;
        }

        private static string? ExtractAccountNumber(string text)
        {
            var line = GetLineContaining(text, "Utility Account:");
            if (line != null)
            {
                var m = AccountPattern.Match(line);
                if (m.Success) return m.Groups[1].Value.Trim();
            }
            return null;
        }

        private static string? GetLineContaining(string text, string anchor)
        {
            int idx = text.IndexOf(anchor, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            int start = text.LastIndexOf('\n', idx);
            if (start < 0) start = 0;
            else start += 1;
            int end = text.IndexOf('\n', idx);
            if (end < 0) end = text.Length;
            return text.Substring(start, end - start).Trim();
        }
    }
}
