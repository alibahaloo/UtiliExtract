using System.Globalization;
using System.Text.RegularExpressions;
using UtiliExtract.Models;

namespace UtiliExtract.Helpers
{
    public class EnmaxBillReader
    {
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

        // ── COMMON INFO PATTERNS ───────────────────────────────────────────────────
        private static readonly Regex AccountNumberPattern = new Regex(
            @"Account Number:\s*(\d+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );

        private static readonly Regex BillingDatePattern = new Regex(
            @"Current Bill Date:\s*([0-9]{4}\s+[A-Za-z]+\s+\d{1,2})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );

        private static readonly Regex ServiceAddressPattern = new Regex(
            @"Current Bill Date:.*?\r?\n(?<address>[\s\S]*?)\r?\nYou are on:",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );

        private static readonly Regex NamePattern = new Regex(
            @"^(?<name>.+?)\s*Account Number:",
            RegexOptions.Multiline
          | RegexOptions.Compiled
          | RegexOptions.CultureInvariant,
            RegexTimeout
        );

        // ── SECTION SPLITTING PATTERN ────────────────────────────────────────────
        // Matches either the ELECTRICITY block or the WATER TREATMENT AND SUPPLY block,
        // each from its header down through its “Summary $XX.XX” line.
        private static readonly Regex SectionPattern = new Regex(
            @"^(?:ELECTRICITY\s*Provided by|WATER TREATMENT AND SUPPLY)"  // header
          + @"[\s\S]*?"                                                  // everything (incl. newlines), non-greedy
          + @"^Summary[^\r\n]*\$\s*\d+\.\d{2}",                          // the Summary line with $XX.XX
            RegexOptions.Singleline    // dot matches newline
          | RegexOptions.Multiline     // ^ and $ match start/end of any line
          | RegexOptions.Compiled
          | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1)
        );

        // --- Bill Data
        private static readonly Regex UsagePattern = new Regex(
            @"([\d,]+\.\d+)\s*kWh\s*@",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );

        private static readonly Regex WaterUsagePattern = new Regex(
            @"([\d,]+\.\d+)\s*m3\s*@",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );

        // matches the “Summary … $ 410.95” line
        private static readonly Regex SectionTotalPattern = new Regex(
            @"^Summary[^\$]*\$\s*(\d+\.\d{2})",
            RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );

        public string FullText { get; }
        public string AccountNumber { get; }
        public DateTime? BillingDate { get; }
        public string ServiceAddress { get; }
        public string Name { get; }

        public EnmaxBillReader(string fullText)
        {
            FullText = fullText;
            AccountNumber = ExtractAccountNumber(fullText);
            BillingDate = ExtractBillingDate(fullText);
            ServiceAddress = ExtractServiceAddress(fullText);
            Name = ExtractAccountName(fullText);
        }


        /// <summary>
        /// Grabs the customer name from the line that precedes "Account Number:"
        /// e.g. "PR SALY CENTRE LTD. Account Number: 501722953" → "PR SALY CENTRE LTD."
        /// </summary>
        public static string ExtractAccountName(string text)
        {
            var m = NamePattern.Match(text);
            return m.Success
                ? m.Groups["name"].Value.Trim()
                : string.Empty;
        }

        private static string ExtractAccountNumber(string text)
        {
            var m = AccountNumberPattern.Match(text);
            return m.Success ? m.Groups[1].Value : string.Empty;
        }

        private static DateTime? ExtractBillingDate(string text)
        {
            var m = BillingDatePattern.Match(text);
            if (m.Success && DateTime.TryParse(m.Groups[1].Value, out var dt))
                return dt;
            return null;
        }

        private static string ExtractServiceAddress(string text)
        {
            var m = ServiceAddressPattern.Match(text);
            return m.Success
                ? m.Groups["address"].Value.Trim()
                : string.Empty;
        }

        /// <summary>
        /// Splits the fullText into each major section (Electricity, Water Treatment…).
        /// </summary>
        public List<string> GetSections()
        {
            var list = new List<string>();
            var matches = SectionPattern.Matches(FullText);
            foreach (Match m in matches)
                list.Add(m.Value.Trim());
            return list;
        }

        /// <summary>
        /// Placeholder – once you feed me the exact extraction rules
        /// (usage, duration, amount, metered?),
        /// we’ll implement this just like BcHydroBillReader.GetBillData.
        /// </summary>
        public BillData? GetBillData(string section)
        {
            UsageType usageType;
            var data = new BillData
            {
                Name = this.Name,
                AccountNumber = this.AccountNumber,
                ServiceAddress = this.ServiceAddress,
                BillingDate = this.BillingDate,
                IsMetered = true
            };

            // ── ELECTRICITY SECTION ─────────────────────────────────────────────────
            if (section.StartsWith("ELECTRICITY"))
            {
                usageType = UsageType.Electricity;

                // 1) Usage (e.g. “2,680.000 kWh @”)
                var um = UsagePattern.Match(section);
                if (um.Success)
                {
                    var raw = um.Groups[1].Value.Replace(",", "");
                    if (double.TryParse(raw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var usage))
                        data.Consumption = usage;
                }

                // 2) AmountDue (Summary $410.95)
                var tm = SectionTotalPattern.Match(section);
                if (tm.Success &&
                    decimal.TryParse(tm.Groups[1].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var total))
                {
                    data.Charges = total;
                }
            }
            // ── WATER TREATMENT AND SUPPLY SECTION ────────────────────────────────────
            else if (section.StartsWith("WATER TREATMENT AND SUPPLY"))
            {
                usageType = UsageType.Water;
 
                // 1) Usage (e.g. “115.000 m3 @”)
                var uw = WaterUsagePattern.Match(section);
                if (uw.Success)
                {
                    var raw = uw.Groups[1].Value.Replace(",", "");
                    if (double.TryParse(raw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var usage))
                        data.Consumption = usage;
                }

                // 2) AmountDue (Summary $323.93)
                var wm = SectionTotalPattern.Match(section);
                if (wm.Success &&
                    decimal.TryParse(wm.Groups[1].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var total))
                {
                    data.Charges = total;
                }
            }
            else
            {
                // Unknown section type
                return null;
            }

            data.UsageType = usageType;
            data.UsageUnit = BillMetadata.GetUsageUnit(usageType);

            // ── DURATION (common to all sections) ────────────────────────────────────
            // Looks for “(Mar6toApr3)” and parses start/end
            var durMatch = Regex.Match(
                section,
                @"\(\s*(?<startMon>[A-Za-z]{3})(?<startDay>\d{1,2})to(?<endMon>[A-Za-z]{3})(?<endDay>\d{1,2})\s*\)"
            );
            if (durMatch.Success && BillingDate.HasValue)
            {
                int year = BillingDate.Value.Year;
                var sDateStr = $"{durMatch.Groups["startMon"].Value} {durMatch.Groups["startDay"].Value} {year}";
                var eDateStr = $"{durMatch.Groups["endMon"].Value} {durMatch.Groups["endDay"].Value} {year}";

                if (DateTime.TryParseExact(sDateStr, "MMM d yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ds))
                    data.DurationStart = ds;
                if (DateTime.TryParseExact(eDateStr, "MMM d yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var de))
                    data.DurationEnd = de;
            }

            return data;
        }


    }
}
