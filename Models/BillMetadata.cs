namespace UtiliExtract.Models
{
    public enum UsageUnit
    {
        kWh,
        GJ,
        m3
    }
    public enum UsageType
    {
        Electricity,
        Gas,
        Water
    }
    public enum BillProvider
    {
        BCHydro,
        FortisBCElec,
        DirectEnergy,
        Enmax
    }
    public static class BillMetadata
    {
        public static readonly Dictionary<BillProvider, List<UsageType>> ProviderUsageTypeMap = new()
        {
            { BillProvider.Enmax, new List<UsageType> { UsageType.Electricity, UsageType.Gas, UsageType.Water } },
            { BillProvider.BCHydro, new List<UsageType> { UsageType.Electricity } },
            { BillProvider.FortisBCElec, new List<UsageType> { UsageType.Electricity } },
            { BillProvider.DirectEnergy, new List<UsageType> { UsageType.Gas } }
        };

        public static readonly Dictionary<UsageType, UsageUnit> UsageTypeToUnitMap = new()
        {
            { UsageType.Electricity, UsageUnit.kWh },
            { UsageType.Gas, UsageUnit.GJ },
            { UsageType.Water, UsageUnit.m3 }
        };

        public static readonly Dictionary<BillProvider, List<string>> BillTypeKeywords = new()
        {
            { BillProvider.FortisBCElec, new List<string> { "fortisbc.com" } },
            { BillProvider.BCHydro, new List<string> { "bchydro.com" } },
            { BillProvider.Enmax, new List<string> { "enmax.com" } },
            { BillProvider.DirectEnergy, new List<string> { "directenergy.com" } },
            // Add more mappings here as needed
        };

        public static Dictionary<UsageType, UsageUnit> GetDefaultUsageTypes(BillProvider provider)
        {
            var result = new Dictionary<UsageType, UsageUnit>();
            if (ProviderUsageTypeMap.TryGetValue(provider, out var types))
            {
                foreach (var type in types)
                {
                    if (UsageTypeToUnitMap.TryGetValue(type, out var unit))
                    {
                        result[type] = unit;
                    }
                }
            }
            return result;
        }
    }
}
