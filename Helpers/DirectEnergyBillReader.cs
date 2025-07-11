﻿using System.Globalization;
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
            @"Invoice Date:\s*([0-9]{1,2}-[A-Za-z]{3}-\d{2})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );
        private static readonly Regex BillingPeriodPattern = new Regex(
            @"Billing Period:\s*([A-Za-z]+\s+\d{4})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );
        private static readonly Regex AmountDuePattern = new Regex(
            @"Amount Due Now:\s*\$\s*([\d,]+\.\d{2})",
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
                UsageUnit = BillMetadata.GetUsageUnit(usageType),
                AccountNumber = ExtractAccountNumber(fullText),
                Name = ExtractName(fullText),
                BillingDate = ExtractBillingDate(fullText),
                AmountDue = ExtractAmountDue(fullText),
                Usage = ExtractUsage(fullText)
            };
            (data.DurationStart, data.DurationEnd) = TryExtractBillingPeriod(fullText);
            return data;
        }

        private static DateTime? ExtractBillingDate(string text)
        {
            var line = GetLineContaining(text, "Invoice Date:");
            if (line != null)
            {
                var m = BillingDatePattern.Match(line);
                if (m.Success && DateTime.TryParseExact(
                        m.Groups[1].Value,
                        "dd-MMM-yy",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var dt))
                {
                    return dt;
                }
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
            var line = GetLineContaining(text, "Amount Due Now:");
            if (line != null)
            {
                var m = AmountDuePattern.Match(line);
                if (m.Success && decimal.TryParse(m.Groups[1].Value.Replace(",", string.Empty), out var d))
                    return d;
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
