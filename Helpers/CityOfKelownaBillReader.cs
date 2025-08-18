using System.Globalization;
using System.Text.RegularExpressions;
using UtiliExtract.Models;

namespace UtiliExtract.Helpers
{
    public static class CityOfKelownaBillReader
    {
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

        // ── Patterns ──────────────────────────────────────────────────────────────
        private static readonly Regex AccountNumberPattern = new Regex(
            @"ACCT\s*NUMBER:\s*([0-9]+)\s+BILLING",
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

        private static readonly Regex BillingDatePattern = new Regex(
            @"BILLING\s*DATE:\s*([A-Za-z]{3,9}\s+\d{1,2},\s*\d{4})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );

        private static readonly Regex BillingStartPattern = new Regex(
            @"BILLING\s*PERIOD:\s*([A-Za-z]{3,9}\s+\d{1,2},\s*\d{4})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );

        private static readonly Regex BillingEndPattern = new Regex(
            @"TO:\s*([A-Za-z]{3,9}\s+\d{1,2},\s*\d{4})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );

        // For consumption lines that end with "CM" (e.g., "... 188 CM")
        private static readonly Regex LineEndCmPattern = new Regex(
            @"(\d+)\s*CM\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );

        // For charges on lines dated the billing date and ending with a currency/number (e.g., "... 161.33")
        private static readonly Regex TrailingMoneyPattern = new Regex(
            @"([\d,]+\.\d{2})\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );

        public static BillData GetInvoiceData(string fullText)
        {
            var data = new BillData
            {
                IsMetered = true,
                UsageType = UsageType.Water,
                UsageUnit = UsageUnit.m3,
                AccountNumber = ExtractAccountNumber(fullText),
                Name = ExtractName(fullText),
                ServiceAddress = ExtractServiceAddress(fullText),
                BillingDate = ExtractBillingDate(fullText),
            };

            (data.DurationStart, data.DurationEnd) = ExtractDuration(fullText);
            data.Consumption = ExtractConsumption(fullText);
            data.Charges = ExtractCharges(fullText, data.BillingDate);

            return data;
        }

        // ── Field extractors ──────────────────────────────────────────────────────
        private static string? ExtractAccountNumber(string text)
        {
            var line = GetLineContaining(text, "ACCT NUMBER:");
            if (line is null) return null;

            var m = AccountNumberPattern.Match(line);
            return m.Success ? m.Groups[1].Value.Trim() : null;
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

        private static DateTime? ExtractBillingDate(string text)
        {
            var line = GetLineContaining(text, "BILLING DATE:");
            if (line is null) return null;

            var m = BillingDatePattern.Match(line);
            if (!m.Success) return null;

            if (DateTime.TryParseExact(
                    m.Groups[1].Value.Trim(),
                    new[] { "MMM d, yyyy", "MMMM d, yyyy" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var dt))
            {
                return dt;
            }
            return null;
        }

        private static (DateTime? start, DateTime? end) ExtractDuration(string text)
        {
            DateTime? start = null, end = null;

            var startLine = GetLineContaining(text, "BILLING PERIOD:");
            if (startLine != null)
            {
                var m = BillingStartPattern.Match(startLine);
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
                var m = BillingEndPattern.Match(endLine);
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

        // Sum of lines that END with "CM" → take the integer before CM (e.g., 188, 0, 3, 30)
        private static double ExtractConsumption(string text)
        {
            double total = 0;
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (var raw in lines)
            {
                var line = raw.TrimEnd();
                if (line.EndsWith("CM", StringComparison.OrdinalIgnoreCase))
                {
                    var m = LineEndCmPattern.Match(line);
                    if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                        total += v;
                }
            }

            return total;
        }

        // Charges = sum of trailing amounts on lines that START with the billing date (e.g., "Apr 6, 2025 ... 161.33")
        // This matches the example selection and excludes prior-penalty rows dated differently.
        private static decimal ExtractCharges(string text, DateTime? billingDate)
        {
            if (billingDate is null) return 0m;

            string prefix = billingDate.Value.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
            decimal total = 0m;

            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var raw in lines)
            {
                var line = raw.TrimEnd();
                if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var m = TrailingMoneyPattern.Match(line);
                    if (m.Success &&
                        decimal.TryParse(m.Groups[1].Value.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var amt))
                    {
                        total += amt;
                    }
                }
            }

            return total;
        }

        // ── Line helpers (same style as your other readers) ───────────────────────
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
