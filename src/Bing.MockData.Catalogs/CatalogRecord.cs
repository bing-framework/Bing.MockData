namespace Bing.MockData.Catalogs
{
    /// <summary>A catalog row with a stable key and its original normalized JSON payload.</summary>
    public sealed class CatalogRecord
    {
        public string Key { get; }
        public string Kind { get; }
        public string? CountryCode { get; }
        public string Name { get; }
        public string PayloadJson { get; }

        internal CatalogRecord(string key, string kind, string? countryCode, string name, string payloadJson)
        {
            Key = key;
            Kind = kind;
            CountryCode = countryCode;
            Name = name;
            PayloadJson = payloadJson;
        }
    }
}
