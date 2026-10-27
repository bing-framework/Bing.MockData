using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace Bing.MockData.Catalogs
{
    /// <summary>全球 BIN 来源记录；缺失字段保持未知，不推导完整卡号或实际开户信息。</summary>
    public sealed class BankBinRecord
    {
        /// <summary>目录稳定键。</summary>
        public string Key { get; }
        /// <summary>保留前导零的来源前缀。</summary>
        public string Bin { get; }
        /// <summary>来源卡组织或品牌。</summary>
        public string? Brand { get; }
        /// <summary>来源资金类型文本，不映射为旧银行卡枚举。</summary>
        public string? Type { get; }
        /// <summary>来源产品分类。</summary>
        public string? Category { get; }
        /// <summary>来源机构名称，不代表已归并的银行实体。</summary>
        public string? Issuer { get; }
        /// <summary>来源机构电话号码。</summary>
        public string? IssuerPhone { get; }
        /// <summary>来源机构网址；读取时不发起网络请求。</summary>
        public string? IssuerUrl { get; }
        /// <summary>来源两位国家代码。</summary>
        public string? CountryCode { get; }
        /// <summary>来源三位国家代码。</summary>
        public string? CountryAlpha3 { get; }
        /// <summary>来源国家名称。</summary>
        public string? CountryName { get; }
        /// <summary>合并目录的来源标签。</summary>
        public string? Source { get; }
        /// <summary>来源快照标记，不表示机构核验日期。</summary>
        public string? Seen { get; }
        /// <summary>当前目录的固定版本。</summary>
        public string DataVersion { get; }

        internal BankBinRecord(CatalogRecord row, string version)
        {
            var p = JObject.Parse(row.PayloadJson);
            var bin = Value(p, "bin");
            if (row.Kind != BankBinCatalog.RecordKind || bin == null || !BankBinCatalog.IsBin(bin) || row.Key != "bin|" + bin)
                throw new InvalidDataException("BIN 目录行身份与内容不一致：" + row.Key);
            Key = row.Key; Bin = bin; DataVersion = version;
            Brand = Value(p, "brand"); Type = Value(p, "type"); Category = Value(p, "category");
            Issuer = Value(p, "issuer"); IssuerPhone = Value(p, "issuer_phone"); IssuerUrl = Value(p, "issuer_url");
            CountryCode = Value(p, "alpha_2"); CountryAlpha3 = Value(p, "alpha_3"); CountryName = Value(p, "country");
            Source = Value(p, "source"); Seen = Value(p, "seen");
            if (CountryCode != row.CountryCode || (Issuer ?? "") != row.Name)
                throw new InvalidDataException("BIN 目录索引与内容不一致：" + row.Key);
        }

        private static string? Value(JObject payload, string name)
        {
            var value = payload[name];
            if (value == null || value.Type == JTokenType.Null) return null;
            if (value.Type != JTokenType.String) throw new InvalidDataException("BIN 字段必须是文本或 null：" + name);
            var text = (string?)value;
            return string.IsNullOrEmpty(text) ? null : text;
        }
    }

    /// <summary>借用已挂载目录的只读适配器；生命周期属于所属上下文。</summary>
    public sealed class BankBinCatalog
    {
        internal const string DataSetId = "bank-bins-techbuddie";
        internal const string RecordKind = "bank-bin";
        private readonly MockContext _context;
        private readonly IBingMockCatalog _catalog;
        private readonly string _mountName;

        internal BankBinCatalog(MockContext context, string mountName)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _mountName = mountName;
            _catalog = context.Catalog(mountName);
            if (_catalog.Info.Id != DataSetId)
                throw new ArgumentException("挂载项不是 bank-bins-techbuddie 数据集。", nameof(mountName));
        }

        /// <summary>来源、许可、版本及覆盖说明；上下文释放后不可读取。</summary>
        public DataSetInfo Info { get { Check(); return _catalog.Info; } }

        /// <summary>精确查找 1–8 位 ASCII 前缀；不截断完整卡号。</summary>
        public bool TryGet(string bin, out BankBinRecord? record)
        {
            Check();
            if (!IsBin(bin)) throw new ArgumentException("BIN 必须为 1–8 位 ASCII 数字前缀。", nameof(bin));
            bool found = _catalog.TryGet("bin|" + bin, out var row);
            Check();
            record = found ? Project(row!) : null;
            return found;
        }

        /// <summary>统计匹配记录，不物化结果；query 的修改不会影响当前操作。</summary>
        public long Count(CatalogQuery? query = null)
        {
            Check(); var snapshot = Snapshot(query);
            using (var cancellation = Link(_context.CancellationToken, snapshot.CancellationToken, default))
            {
                snapshot.CancellationToken = cancellation.Token;
                snapshot.CancellationToken.ThrowIfCancellationRequested();
                long count = HasFilter(snapshot) ? _catalog.Count(snapshot) : _catalog.Info.RecordCount;
                Check(); snapshot.CancellationToken.ThrowIfCancellationRequested();
                return count;
            }
        }

        /// <summary>按稳定键惰性分页；取消及提前结束均释放枚举所属资源。</summary>
        public IEnumerable<BankBinRecord> Query(CatalogQuery? query = null, CancellationToken cancellationToken = default)
        {
            Check(); var snapshot = Snapshot(query);
            snapshot.CancellationToken.ThrowIfCancellationRequested(); cancellationToken.ThrowIfCancellationRequested();
            return Enumerate(snapshot, cancellationToken);
        }

        private IEnumerable<BankBinRecord> Enumerate(CatalogQuery snapshot, CancellationToken token)
        {
            // 每次枚举独立创建令牌链接；不在未消费的 IEnumerable 中保留注册。
            Check(); var current = snapshot.Copy();
            using (var cancellation = Link(_context.CancellationToken, current.CancellationToken, token))
            {
                current.CancellationToken = cancellation.Token;
                foreach (var row in _catalog.Query(current, cancellation.Token))
                {
                    Check(); cancellation.Token.ThrowIfCancellationRequested();
                    yield return Project(row);
                }
                Check(); cancellation.Token.ThrowIfCancellationRequested();
            }
        }

        /// <summary>以当前上下文的随机位抽样；无筛选时直接按序号访问。</summary>
        public BankBinRecord Sample(CatalogQuery? query = null)
        {
            Check(); var snapshot = Snapshot(query);
            using (var cancellation = Link(_context.CancellationToken, snapshot.CancellationToken, default))
            {
                snapshot.CancellationToken = cancellation.Token;
                cancellation.Token.ThrowIfCancellationRequested();
                if (_catalog.Info.RecordCount == 0) throw new CatalogCoverageException(DataSetId, "empty");
                ulong selector = _context.Numbers.ULong();
                var row = HasFilter(snapshot) ? _catalog.Sample(selector, snapshot) : _catalog.Sample(selector);
                Check(); cancellation.Token.ThrowIfCancellationRequested();
                return Project(row);
            }
        }

        private BankBinRecord Project(CatalogRecord row) => new BankBinRecord(row, _catalog.Info.Version);
        private void Check() => _context.Catalog(_mountName);

        private static CatalogQuery Snapshot(CatalogQuery? query)
        {
            var snapshot = query?.Copy() ?? new CatalogQuery();
            snapshot.Validate();
            if (snapshot.Kind != null && snapshot.Kind != RecordKind)
                throw new ArgumentException("BIN 查询的 Kind 只能为空或 bank-bin。", nameof(query));
            snapshot.Kind = RecordKind;
            return snapshot;
        }

        private static bool HasFilter(CatalogQuery query) => query.CountryCode != null || query.NamePrefix != null || query.KeyPrefix != null;

        internal static bool IsBin(string? bin)
        {
            if (bin == null || bin.Length < 1 || bin.Length > 8) return false;
            foreach (char c in bin) if (c < '0' || c > '9') return false;
            return true;
        }

        private sealed class CancellationScope : IDisposable
        {
            private readonly CancellationTokenSource? _source;
            internal CancellationToken Token { get; }
            internal CancellationScope(CancellationToken token) { Token = token; }
            internal CancellationScope(CancellationToken[] tokens)
            { _source = CancellationTokenSource.CreateLinkedTokenSource(tokens); Token = _source.Token; }
            public void Dispose() => _source?.Dispose();
        }

        private static CancellationScope Link(CancellationToken a, CancellationToken b, CancellationToken c)
        {
            if (!a.CanBeCanceled) a = b;
            if (!b.CanBeCanceled || b == a) b = c;
            if (!b.CanBeCanceled || b == a) return new CancellationScope(a);
            if (!c.CanBeCanceled || c == a || c == b) return new CancellationScope(new[] { a, b });
            return new CancellationScope(new[] { a, b, c });
        }
    }

    /// <summary>从上下文获取借用目录的 BIN 适配器。</summary>
    public static class BankBinCatalogExtensions
    {
        /// <summary>使用已挂载的全球 BIN 目录，不创建额外连接。</summary>
        public static BankBinCatalog BankBins(this MockContext context, string mountName = "bank-bins") => new BankBinCatalog(context, mountName);
    }
}
