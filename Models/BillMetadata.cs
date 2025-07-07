namespace UtiliExtract.Models
{
    /// <summary>
    /// Represents the unit of measurement for utility usage values.
    /// These units correspond to the type of service being billed (e.g., electricity, gas).
    /// </summary>
    public enum UsageUnit
    {
        kWh,
        GJ,
        m3,
        LBS
    }

    /// <summary>
    /// Represents the type of utility usage on a bill.
    /// This defines the category of consumption, such as electricity or gas.
    /// </summary>
    public enum UsageType
    {
        Electricity,
        Gas,
        Water,
        Steam
    }

    /// <summary>
    /// Enumerates all supported utility bill providers.
    /// These values are used to associate PDF invoices with their issuing organization.
    /// 
    /// When supporting a new bill provider, be sure to add it to this enum.
    /// </summary>
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

        /// <summary>
        /// Maps each <see cref="UsageType"/> to its corresponding <see cref="UsageUnit"/>.
        /// This is used to determine the correct unit of measurement (e.g., kWh, GJ)
        /// for a given type of utility usage when processing or displaying bill data.
        /// </summary>
        public static readonly Dictionary<UsageType, UsageUnit> UsageTypeToUnitMap = new()
        {
            { UsageType.Electricity, UsageUnit.kWh },
            { UsageType.Gas, UsageUnit.GJ },
            { UsageType.Water, UsageUnit.m3 },
            { UsageType.Steam, UsageUnit.LBS },
        };

        /// <summary>
        /// Maps each <see cref="UsageType"/> to its corresponding Bootstrap icon class.
        /// This is used to visually represent the type of utility (e.g., electricity, gas)
        /// in the UI using Bootstrap Icons.
        /// </summary>
        public static readonly Dictionary<UsageType, string> UsageTypeToIconMap = new()
        {
            { UsageType.Electricity, "bi bi-plug"},
            { UsageType.Gas, "bi bi-fuel-pump" },
            { UsageType.Water, "bi bi-droplet" },
            { UsageType.Steam, "bi bi-wind" },
        };

        /// <summary>
        /// Maps unique identifying keywords to their corresponding <see cref="BillProvider"/>.
        /// This dictionary is used to detect the bill provider when reading PDF content,
        /// based on the presence of known strings (e.g. URLs or company identifiers).
        /// 
        /// To support a new bill provider, add a unique and specific keyword that appears
        /// reliably in their invoice (e.g., domain name, footer text, or provider name).
        /// </summary>
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

        public static UsageUnit GetUsageUnit(UsageType usageType)
        {
            return UsageTypeToUnitMap.TryGetValue(usageType, out var unit)
                ? unit
                : throw new ArgumentOutOfRangeException(nameof(usageType), $"No unit mapping for {usageType}");
        }

        public static string GetIconClass(UsageType usageType)
        {
            return UsageTypeToIconMap.TryGetValue(usageType, out var icon) ? icon : "bi bi-question-circle";
        }

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
