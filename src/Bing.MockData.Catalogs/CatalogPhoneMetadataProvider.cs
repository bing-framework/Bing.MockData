using System;
using System.Collections.Generic;
using System.Linq;
using Bing.MockData;
using Newtonsoft.Json.Linq;

namespace Bing.MockData.Catalogs
{
    /// <summary>Adapts libphonenumber-derived rows to the core phone generation contract.</summary>
    public sealed class CatalogPhoneMetadataProvider : IPhoneMetadataProvider, INonGeographicPhoneMetadataProvider, IDisposable
    {
        private readonly IBingMockCatalog _catalog;
        private readonly bool _ownsCatalog;
        private bool _disposed;

        /// <summary>Returns the mounted dataset version used by this provider.</summary>
        public string Version => _catalog.Info.Version;

        public CatalogPhoneMetadataProvider(IBingMockCatalog catalog, bool ownsCatalog = false)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _ownsCatalog = ownsCatalog;
        }

        public PhonePattern GetPattern(string countryCode, PhoneNumberType type)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(countryCode)) throw new ArgumentException("国家或地区代码不能为空。", nameof(countryCode));
            if (!Enum.IsDefined(typeof(PhoneNumberType), type)) throw new ArgumentOutOfRangeException(nameof(type));
            var key = countryCode.Trim().ToUpperInvariant() + ":" + type.ToString().ToLowerInvariant();
            return ReadPattern(key, type);
        }

        /// <summary>按 001:类型:呼叫码稳定键读取非地理号码规则。</summary>
        public PhonePattern GetNonGeographicPattern(string callingCode, PhoneNumberType type)
        {
            ThrowIfDisposed();
            if (callingCode == null) throw new ArgumentNullException(nameof(callingCode));
            if (callingCode.Length < 1 || callingCode.Length > 3 || callingCode[0] == '0' || !callingCode.All(ch => ch >= '0' && ch <= '9'))
                throw new ArgumentException("呼叫码必须为无前导零的 1–3 位 ASCII 数字，不含加号。", nameof(callingCode));
            if (!Enum.IsDefined(typeof(PhoneNumberType), type)) throw new ArgumentOutOfRangeException(nameof(type));
            return ReadPattern("001:" + type.ToString().ToLowerInvariant() + ":" + callingCode, type);
        }

        private PhonePattern ReadPattern(string key, PhoneNumberType type)
        {
            if (!_catalog.TryGet(key, out var record) || record == null)
                throw new CatalogCoverageException(_catalog.Info.Id, key);

            var payload = JObject.Parse(record.PayloadJson);
            string pattern = CompactPattern(Optional(payload, "nationalPattern") ?? Required(payload, "sourcePattern"));
            var formats = (payload["formats"] as JArray ?? new JArray())
                .OfType<JObject>()
                .Select(item => new PhoneNumberFormat(
                    CompactPattern(Required(item, "pattern")),
                    Required(item, "replacement"),
                    CompactPattern(Optional(item, "leadingDigits") ?? string.Empty),
                    Optional(item, "nationalPrefixRule") ?? string.Empty))
                .ToArray();
            return new PhonePattern(
                Required(payload, "countryCode"),
                Required(payload, "callingCode"),
                type,
                pattern,
                Optional(payload, "nationalPrefix") ?? string.Empty,
                formats);
        }

        private static string Required(JObject payload, string name)
        {
            var value = (string?)payload[name];
            if (string.IsNullOrEmpty(value)) throw new FormatException($"电话元数据缺少 {name}。");
            return value;
        }

        private static string? Optional(JObject payload, string name) => (string?)payload[name];

        private static string CompactPattern(string value)
            => new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray());

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CatalogPhoneMetadataProvider));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_ownsCatalog) _catalog.Dispose();
        }
    }
}
