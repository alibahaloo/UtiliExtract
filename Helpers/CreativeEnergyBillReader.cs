using System.Globalization;
using System.Text.RegularExpressions;
using UtiliExtract.Models;

namespace UtiliExtract.Helpers
{
    public static class CreativeEnergyBillReader
    {
        // Prevent hangs: compiled, culture-invariant with timeout
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

        // BILLING DATE: 5/31/2021
        private static readonly Regex BillingDatePattern = new Regex(
            @"BILLING\s+DATE:\s*(\d{1,2}/\d{1,2}/\d{4})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );

        // ACCT # 059
        private static readonly Regex AccountPattern = new Regex(
            @"ACCT\s*#\s*([0-9]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );

        // Header line to locate the data row
        private static readonly Regex HeaderPattern = new Regex(
            @"^Building\s+Date\s+From\s+Date\s+To\s+Reading\s+Prior\s+Reading\s+Current\s+Mult\s+Consumption",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline,
            RegexTimeout
        );

        // Data row: name, start date, end date, prior, current, multiplier, consumption
        private static readonly Regex DataLinePattern = new Regex(
            @"^(.*?)\s+(\d{1,2}/\d{1,2}/\d{4})\s+(\d{1,2}/\d{1,2}/\d{4})\s+[\d,]+\.\d{2}\s+[\d,]+\.\d{2}\s+\d+\s+([\d,]+\.\d{2})",
            RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Multiline,
            RegexTimeout
        );

        // Total Due
        private static readonly Regex DueAmountPattern = new Regex(
            @"Total\s+Due\s*\$\s*([\d,]+\.\d{2})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );

        public static BillData GetInvoiceData(string fullText)
        {
            var data = new BillData
            {
                BillingDate = ExtractBillingDate(fullText),
                AccountNumber = ExtractAccountNumber(fullText),
                IsMetered = true,
                UsageType = UsageType.Steam,
                UsageUnit = UsageUnit.LBS,
                AmountDue = ExtractDueAmount(fullText),
            };

            // Find the data line immediately after the header
            var headerMatch = HeaderPattern.Match(fullText);
            if (headerMatch.Success)
            {
                var afterHeader = fullText.Substring(headerMatch.Index + headerMatch.Length);
                var dataLineMatch = DataLinePattern.Match(afterHeader);
                if (dataLineMatch.Success)
                {
                    // Name is everything before the first date
                    data.Name = dataLineMatch.Groups[1].Value.Trim();
                    data.ServiceAddress = data.Name;

                    // Parse duration start/end
                    if (DateTime.TryParseExact(
                            dataLineMatch.Groups[2].Value,
                            "M/d/yyyy",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out var start))
                    {
                        data.DurationStart = start;
                    }
                    if (DateTime.TryParseExact(
                            dataLineMatch.Groups[3].Value,
                            "M/d/yyyy",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out var end))
                    {
                        data.DurationEnd = end;
                    }

                    // Parse consumption (last group), removing commas
                    var consStr = dataLineMatch.Groups[4].Value.Replace(",", "");
                    if (double.TryParse(consStr, out var usage))
                    {
                        data.Usage = usage;
                    }
                }
            }

            return data;
        }

        private static decimal? ExtractDueAmount(string text)
        {
            var line = GetLineContaining(text, "Total Due $");
            if (line != null)
            {
                var m = DueAmountPattern.Match(line);
                if (m.Success &&
                    decimal.TryParse(
                        m.Groups[1].Value.Replace(",", ""),
                        NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands,
                        CultureInfo.InvariantCulture,
                        out var amt))
                {
                    return amt;
                }
            }
            return null;
        }

        private static DateTime? ExtractBillingDate(string text)
        {
            var line = GetLineContaining(text, "BILLING DATE:");
            if (line != null)
            {
                var m = BillingDatePattern.Match(line);
                if (m.Success &&
                    DateTime.TryParseExact(
                        m.Groups[1].Value,
                        "M/d/yyyy",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var dt))
                {
                    return dt;
                }
            }
            return null;
        }

        private static string? ExtractAccountNumber(string text)
        {
            var line = GetLineContaining(text, "ACCT #");
            if (line != null)
            {
                var m = AccountPattern.Match(line);
                if (m.Success) return m.Groups[1].Value.Trim();
            }
            return null;
        }

        // Finds the full line containing the given anchor
        private static string? GetLineContaining(string text, string anchor)
        {
            var idx = text.IndexOf(anchor, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            var start = text.LastIndexOf('\n', idx);
            if (start < 0) start = 0; else start += 1;
            var end = text.IndexOf('\n', idx);
            if (end < 0) end = text.Length;
            return text.Substring(start, end - start).Trim();
        }
    }
}
