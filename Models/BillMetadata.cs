namespace UtiliExtract.Models
{
    public enum UsageUnit
    {
        kWh,
        GJ,
        m3,
        LBS
    }
    public enum UsageType
    {
        Electricity,
        Gas,
        Water,
        Steam
    }
    public enum BillProvider
    {
        BCHydro,
        FortisBCElec,
        DirectEnergy,
        Enmax,
        CreativeEnergy,
        CityOfVancouver,
        CityOfWilliamsLake
    }
    public static class BillMetadata
    {
        public static readonly Dictionary<BillProvider, List<UsageType>> ProviderUsageTypeMap = new()
        {
            { BillProvider.Enmax, new List<UsageType> { UsageType.Electricity, UsageType.Gas, UsageType.Water } },
            { BillProvider.BCHydro, new List<UsageType> { UsageType.Electricity } },
            { BillProvider.FortisBCElec, new List<UsageType> { UsageType.Electricity } },
            { BillProvider.DirectEnergy, new List<UsageType> { UsageType.Gas } },
            { BillProvider.CreativeEnergy, new List<UsageType>() { UsageType.Steam } },
            { BillProvider.CityOfVancouver, new List<UsageType>() {UsageType.Water } },
            { BillProvider.CityOfWilliamsLake, new List<UsageType>() {UsageType.Water } },
        };

        public static readonly Dictionary<UsageType, UsageUnit> UsageTypeToUnitMap = new()
        {
            { UsageType.Electricity, UsageUnit.kWh },
            { UsageType.Gas, UsageUnit.GJ },
            { UsageType.Water, UsageUnit.m3 },
            { UsageType.Steam, UsageUnit.LBS },
        };

        public static readonly Dictionary<BillProvider, List<string>> BillTypeKeywords = new()
        {
            { BillProvider.FortisBCElec, new List<string> { "fortisbc.com" } },
            { BillProvider.BCHydro, new List<string> { "bchydro.com" } },
            { BillProvider.Enmax, new List<string> { "enmax.com" } },
            { BillProvider.DirectEnergy, new List<string> { "directenergy.com" } },
            { BillProvider.CreativeEnergy, new List<string> { "Creativeenergycanada.com" } },
            { BillProvider.CityOfVancouver, new List<string> { "vancouver.ca/utilitybilling" } },
            { BillProvider.CityOfWilliamsLake, new List<string> { "www.williamslake.ca" } },
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
