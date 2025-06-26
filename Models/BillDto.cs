namespace UtiliExtract.Models
{
    public class BillDto
    {
        public string? AccountNumber { get; set; }
        public DateTime? BillingDate { get; set; }
        public DateTime? DurationStart { get; set; }
        public DateTime? DurationEnd { get; set; }
        public double? Usage { get; set; }
        public BillProvider BillProvider { get; set; }
        public string? UsageType { get; set; }
        public string? UsageUnit { get; set; }
    }
}
