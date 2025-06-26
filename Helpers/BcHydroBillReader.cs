using UtiliExtract.Models;
using System.Text.RegularExpressions;

namespace UtiliExtract.Helpers
{
    public class BcHydroBillReader
    {
        public DateTime? BillingDate { get; }
        public string FullText { get; }

        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

        private static readonly Regex AccountPattern = new Regex(
            @"Member\s+account\s*#\s*([0-9\s]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );

        private static readonly Regex AddressPattern = new Regex(
            @"Service\s+address:\s*(.*?)(?=(\d+%|UNMETERED\s+CHARGES|Meter\s+reading|Your\s+bill\s+has\s+been\s+corrected|No\s+change\s+in\s+e|Your\s+account\s+has\s+a\s+charge\s+o|ELECTRICITY\s+CHARGES)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );

        private static readonly Regex UnmeteredUsagePattern = new Regex(
            @"Energy\s+charges?[\r\n]*\s*([0-9]{1,3}(?:,[0-9]{3})*)\s*kWh",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );

        private static readonly Regex MeteredUsagePattern = new Regex(
            @"([0-9]{1,3}(?:,[0-9]{3})*)\s*kWh\s+used\s+over",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );

        private static readonly Regex ChargesPattern = new Regex(
            @"CURRENT\s+CHARGES\s*(-?\$\d{1,3}(?:,\d{3})*\.\d{2})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );

        private static readonly Regex UnmeteredDateRangePattern;
        private static readonly Regex PageCountPattern = new Regex(
            @"\bof\s+(\d+)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout
        );

        static BcHydroBillReader()
        {
            string[] rateKinds = new[]
            {
                "Small General Service Rate",
                "Medium General Service Rate",
                "Large General Service Rate",
                "Traffic Service Rate",
                "Ornamental Street Lighting Rate",
                "Overhead Street Lighting Rate",
                "Residential Tiered Rate",
                "Transformer Owner discount"
            };
            var prefixGroup = string.Join("|", rateKinds.Select(Regex.Escape));
            const string monthList = "Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec";
            string datePattern = $@"(?:{monthList})\s+\d{{1,2}},\s*\d{{4}}";

            UnmeteredDateRangePattern = new Regex(
                $@"(?:Based\s+on\s+(?:{prefixGroup})\s*\d+|Continued).*?({datePattern})\s*to\s*({datePattern})",
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant,
                RegexTimeout
            );
        }

        public BcHydroBillReader(string fullText)
        {
            FullText = fullText;
            BillingDate = TryExtractCommonBillingDate(fullText);
        }

        public List<string> GetSections()
        {
            int totalPages = ExtractTotalPages(FullText);
            return SplitIntoSections(FullText, totalPages);
        }

        public BillData? GetBillData(string sec)
        {
            // ── ACCOUNT NUMBER ───────────────────────────────────
            var accountLine = GetLineContaining(sec, "Member account #");
            if (accountLine == null) return null;
            var accountMatch = AccountPattern.Match(accountLine);
            if (!accountMatch.Success) return null;
            var accountNumber = accountMatch.Groups[1].Value.Replace(" ", "").Trim();

            // ── NAME ─────────────────────────────────────────────
            string name = string.Empty;
            var lines = sec
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(l => l.Trim())
                .ToArray();

            // find the "Member account #" line
            int memberIdx = Array.FindIndex(lines, l =>
                l.IndexOf("Member account #", StringComparison.OrdinalIgnoreCase) >= 0);

            if (memberIdx > 0)
            {
                // if there's a "Total due for consolidated account" line before Member account, start after it
                int consolidatedIdx = Array.FindIndex(lines, l =>
                    l.IndexOf("Total due for consolidated account", StringComparison.OrdinalIgnoreCase) >= 0);

                int startIdx = (consolidatedIdx >= 0 && consolidatedIdx < memberIdx)
                    ? consolidatedIdx + 1
                    : 0;

                var nameParts = new List<string>();

                // collect all relevant lines between startIdx (inclusive) and memberIdx (exclusive)
                for (int i = startIdx; i < memberIdx; i++)
                {
                    var line = lines[i];

                    // skip the directive line if present
                    if (line.IndexOf("Bill details for member accounts", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    // strip off any trailing "xx%" and everything after it
                    line = Regex.Replace(line, @"\d+%.*$", "", RegexOptions.CultureInvariant).Trim();

                    // strip off any trailing "kW Peak..." info
                    line = Regex.Replace(line, @"\d+\s*kW\s*Peak.*$", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();

                    // strip off any trailing "No change..." info
                    line = Regex.Replace(line, @"No change.*$", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();

                    if (!string.IsNullOrEmpty(line))
                        nameParts.Add(line);
                }

                // combine one or two lines into the final name
                name = string.Join(" ", nameParts);
            }

            // ── SERVICE ADDRESS ────────────────────────────────
            var addrLine = GetLineContaining(sec, "Service address:");
            var serviceAddress = string.Empty;
            if (addrLine != null)
            {
                var addrMatch = AddressPattern.Match(addrLine);
                if (addrMatch.Success) serviceAddress = addrMatch.Groups[1].Value.Trim();
            }

            // ── USAGE ───────────────────────────────────────────
            bool isMetered = sec.IndexOf("Meter reading information", StringComparison.OrdinalIgnoreCase) >= 0;
            double usage = 0;
            if (isMetered)
            {
                var usageLine = GetLineContaining(sec, "kWh used");
                if (usageLine != null)
                {
                    var um = MeteredUsagePattern.Match(usageLine);
                    if (um.Success && double.TryParse(um.Groups[1].Value.Replace(",", ""), out var u))
                        usage = u;
                }
            }
            else
            {
                var usageLine = GetLineContaining(sec, "Energy charge");
                if (usageLine != null)
                {
                    var uu = UnmeteredUsagePattern.Match(usageLine);
                    if (uu.Success && double.TryParse(uu.Groups[1].Value.Replace(",", ""), out var u))
                        usage = u;
                }
            }

            // ── DURATION ────────────────────────────────────────
            DateTime? durationStart = null, durationEnd = null;
            if (isMetered)
            {
                var startLine = GetLineContaining(sec, "Starting ");
                if (startLine != null)
                {
                    var m = Regex.Match(startLine, @"Starting\s+([A-Za-z]{3}\s+\d{1,2},\s*\d{4})",
                                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                    if (m.Success && DateTime.TryParse(m.Groups[1].Value, out var ds))
                        durationStart = ds;
                }
                var endLine = GetLineContaining(sec, "Ending ");
                if (endLine != null)
                {
                    var m = Regex.Match(endLine, @"Ending\s+([A-Za-z]{3}\s+\d{1,2},\s*\d{4})",
                                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                    if (m.Success && DateTime.TryParse(m.Groups[1].Value, out var de))
                        durationEnd = de;
                }
            }
            else
            {
                // need both lines to match the two-line date-range regex
                var prefixLine = GetLineContaining(sec, "Based on");
                var dateLine = GetLineContaining(sec, " to ");
                if (prefixLine != null && dateLine != null)
                {
                    var combined = $"{prefixLine}\n{dateLine}";
                    var dr = UnmeteredDateRangePattern.Match(combined);
                    if (dr.Success)
                    {
                        if (DateTime.TryParse(dr.Groups[1].Value, out var ds)) durationStart = ds;
                        if (DateTime.TryParse(dr.Groups[2].Value, out var de)) durationEnd = de;
                    }
                }
            }

            // ── AMOUNT DUE ──────────────────────────────────────
            decimal amountDue = 0;
            var chargesLine = GetLineContaining(sec, "CURRENT CHARGES");
            if (chargesLine != null)
            {
                var cm = ChargesPattern.Match(chargesLine);
                if (cm.Success &&
                    decimal.TryParse(cm.Groups[1].Value.Replace("$", "").Replace(",", ""),
                                     out var ad))
                {
                    amountDue = ad;
                }
            }

            return new BillData
            {
                AccountNumber = accountNumber,
                Name = name,
                ServiceAddress = serviceAddress,
                BillingDate = BillingDate,
                DurationStart = durationStart,
                DurationEnd = durationEnd,
                Usage = usage,
                AmountDue = amountDue,
                IsMetered = isMetered
            };
        }

        //──────────────────────────────────────────────────────
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

        private static int ExtractTotalPages(string text)
        {
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (lines.Length < 2) return 0;
            var secondLine = lines[1];
            var match = PageCountPattern.Match(secondLine);
            return (match.Success && int.TryParse(match.Groups[1].Value, out var total))
                   ? total
                   : 0;
        }

        private static List<string> SplitIntoSections(string fullText, int totalPages)
        {
            if (totalPages > 0)
            {
                fullText = Regex.Replace(
                    fullText,
                    $@"of\s*{totalPages}",
                    "",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
                );
            }

            var pattern = new Regex(
                @"(.*?CURRENT\s+CHARGES\s*-?\$\d{1,3}(?:,\d{3})*\.\d{2})",
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant,
                RegexTimeout
            );

            return pattern.Matches(fullText)
                          .Cast<Match>()
                          .Select(m => m.Groups[1].Value.Trim())
                          .ToList();
        }

        private static DateTime? TryExtractCommonBillingDate(string text)
        {
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (lines.Length < 2) return null;
            var secondLine = lines[1];
            var match = Regex.Match(
                secondLine,
                @"([A-Za-z]{3,9}\s+\d{1,2},\s*\d{4})",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
            );
            if (match.Success && DateTime.TryParse(match.Groups[1].Value.Trim(), out var dt))
                return dt;
            return null;
        }
    }
}
