using Microsoft.JSInterop;
using System.Globalization;
using System.Text;
using System.Text.Json;
using UtiliExtract.Models;

namespace UtiliExtract.Helpers
{
    public class BillCsvExporter
    {
        // ---- Public API ------------------------------------------------------

        /// <summary>
        /// Export CSVs grouped by BillProvider and trigger downloads via JS interop.
        /// </summary>
        public async Task ExportByProviderAsync(IEnumerable<BillDto> records, IJSRuntime js, string? filePrefix = null)
        {
            if (records is null) return;

            var groups = records
                .Where(r => r is not null)
                .GroupBy(r => r.BillProvider);

            foreach (var g in groups)
            {
                var csv = BuildCsv(g);
                var fileName = BuildFileName(g.Key, filePrefix);
                await DownloadAsync(csv, fileName, js);
            }
        }

        /// <summary>
        /// Convenience overload that accepts the JSON payload you already produce.
        /// </summary>
        public async Task ExportByProviderFromJsonAsync(string json, JsonSerializerOptions options, IJSRuntime js, string? filePrefix = null)
        {
            var records = JsonSerializer.Deserialize<List<BillDto>>(json, options) ?? new List<BillDto>();
            await ExportByProviderAsync(records, js, filePrefix);
        }

        // ---- CSV construction ------------------------------------------------

        private static string BuildCsv(IEnumerable<BillDto> records)
        {
            var sb = new StringBuilder();

            // Header
            sb.AppendLine("MeterID,MeterName,ReadDate,Days,Consumption,Cost,Aux,Late,Charges,Estimated");

            foreach (var r in records)
            {
                var readDate = FormatDate(r.DurationEnd);
                var days = ComputeDaysInclusive(r.DurationStart, r.DurationEnd).ToString(CultureInfo.InvariantCulture);
                var consumption = FormatNumber(r.Usage);
                var cost = FormatNumber(r.Charges);

                var meterId = r.AccountNumber ?? string.Empty;
                var meterName = r.Name ?? string.Empty;

                // Order must match the header exactly
                var line = string.Join(",",
                    meterId,                 // MeterID (empty)
                    meterName,                 // MeterName (empty)
                    EscapeCsv(readDate),// ReadDate (DurationEnd)
                    days,               // Days (inclusive diff + 1)
                    consumption,        // Consumption (Usage)
                    cost,               // Cost (Charges)
                    "",                 // Aux (empty)
                    "",                 // Late (empty)
                    "",                 // Charges column (empty)
                    "0"                 // Estimated (0)
                );

                sb.AppendLine(line);
            }

            return sb.ToString();
        }

        private static string BuildFileName(BillProvider provider, string? prefix)
        {
            var slug = provider.ToString();
            var ts = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(prefix)
                ? $"{slug}_{ts}.csv"
                : $"{prefix}_{slug}_{ts}.csv";
        }

        private static async Task DownloadAsync(string csv, string fileName, IJSRuntime js)
        {
            var bytes = Encoding.UTF8.GetBytes(csv);
            using var ms = new MemoryStream(bytes);
            using var streamRef = new DotNetStreamReference(ms);
            await js.InvokeVoidAsync("downloadFileFromStream", fileName, streamRef);
        }

        // ---- Helpers ---------------------------------------------------------

        private static string EscapeCsv(string? s)
        {
            var v = s ?? string.Empty;
            // Quote only when needed: quotes, commas, or newlines
            if (v.Contains('"') || v.Contains(',') || v.Contains('\n') || v.Contains('\r'))
                return $"\"{v.Replace("\"", "\"\"")}\"";
            return v;
        }

        private static string FormatDate(DateTime? dt) =>
            dt.HasValue ? dt.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty;

        private static string FormatNumber(decimal? n) =>
            n.HasValue ? n.Value.ToString("0.############", CultureInfo.InvariantCulture) : string.Empty;

        private static string FormatNumber(double? n) =>
            n.HasValue ? n.Value.ToString("0.############", CultureInfo.InvariantCulture) : string.Empty;

        private static int ComputeDaysInclusive(DateTime? start, DateTime? end)
        {
            if (start.HasValue && end.HasValue)
            {
                var days = (int)Math.Round((end.Value.Date - start.Value.Date).TotalDays) + 1;
                return Math.Max(days, 0);
            }
            return 0;
        }
    }
}
