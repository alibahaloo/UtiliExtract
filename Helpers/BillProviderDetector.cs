using UtiliExtract.Models;
namespace UtiliExtract.Helpers
{
    public static class BillProviderDetector
    {
        public static BillProvider DetectBillProvider(string pdfText)
        {
            if (string.IsNullOrWhiteSpace(pdfText))
                throw new InvalidDataException("DetectBillProvider: pdfText cannot be null");

            string normalized = pdfText.ToLowerInvariant();

            foreach (var kvp in BillMetadata.BillTypeKeywords)
            {
                BillProvider billType = kvp.Key;
                var requiredKeywords = kvp.Value;

                bool allFound = requiredKeywords.All(kw => normalized.Contains(kw.ToLowerInvariant()));
                if (allFound)
                {
                    return billType;
                }
            }

            throw new InvalidOperationException("Bill Provider Not Detected");
        }
    }

}
