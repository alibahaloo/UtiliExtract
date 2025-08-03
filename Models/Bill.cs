namespace UtiliExtract.Models
{
    public class Bill
    {
        public string FileName { get; set; } = string.Empty;
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public BillProvider BillProvider { get; set; }
        public List<BillData> BillData { get; set; } = [];
        public string? PreviewUrl { get; set; }
        public bool ShowPreview { get; set; } = false;
    }
}
