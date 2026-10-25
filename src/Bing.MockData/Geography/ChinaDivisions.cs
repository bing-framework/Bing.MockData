using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bing.MockData.Datas;

namespace Bing.MockData.Geography
{
    /// <summary>查询随程序发布的 2025 年中国行政区划快照。</summary>
    public static class ChinaDivisions
    {
        /// <summary>内置行政区划快照版本。</summary>
        public const string Version = "2025-12-31";

        private const int ExpectedCount = 3212;
        private static readonly IReadOnlyList<ChinaDivision> Empty = Array.AsReadOnly(new ChinaDivision[0]);
        private static readonly IReadOnlyList<ChinaDivision> _all;
        private static readonly Dictionary<string, ChinaDivision> _byCode;
        private static readonly Dictionary<string, IReadOnlyList<ChinaDivision>> _children;

        static ChinaDivisions()
        {
            var records = new DataLoader().GetResourceFromJson<List<DivisionRecord>>("ChinaDivisions2025");
            if (records == null || records.Count != ExpectedCount)
                throw new InvalidDataException("中国行政区划资源记录数不正确。");

            _byCode = new Dictionary<string, ChinaDivision>(records.Count, StringComparer.Ordinal);
            foreach (var record in records)
            {
                if (record == null || string.IsNullOrWhiteSpace(record.Code) || record.Code.Length != 6 ||
                    record.Code.Any(character => character < '0' || character > '9') || string.IsNullOrWhiteSpace(record.Name) ||
                    !Enum.IsDefined(typeof(ChinaDivisionLevel), record.Level))
                    throw new InvalidDataException("中国行政区划资源包含无效记录。");

                var division = new ChinaDivision(record.Code, record.Name, record.Level, record.ParentCode);
                if (_byCode.ContainsKey(division.Code))
                    throw new InvalidDataException("中国行政区划资源包含重复代码。");
                _byCode.Add(division.Code, division);
            }

            foreach (var division in _byCode.Values)
            {
                if (division.Level == ChinaDivisionLevel.Province)
                {
                    if (division.ParentCode != null)
                        throw new InvalidDataException("省级行政区不能有父级。");
                    continue;
                }

                ChinaDivision parent;
                if (string.IsNullOrEmpty(division.ParentCode) || !_byCode.TryGetValue(division.ParentCode, out parent) || parent.Level >= division.Level)
                    throw new InvalidDataException("中国行政区划资源包含孤儿或层级错误。");
            }

            ValidateAcyclic();

            var childLists = new Dictionary<string, List<ChinaDivision>>(StringComparer.Ordinal);
            foreach (var record in records)
            {
                var division = _byCode[record.Code];
                if (division.ParentCode == null) continue;
                List<ChinaDivision> siblings;
                if (!childLists.TryGetValue(division.ParentCode, out siblings))
                {
                    siblings = new List<ChinaDivision>();
                    childLists.Add(division.ParentCode, siblings);
                }
                siblings.Add(division);
            }

            _children = childLists.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<ChinaDivision>)Array.AsReadOnly(pair.Value.ToArray()),
                StringComparer.Ordinal);
            _all = Array.AsReadOnly(records.Select(record => _byCode[record.Code]).ToArray());
        }

        /// <summary>按源文件顺序返回全部 3212 条区划记录。</summary>
        public static IReadOnlyList<ChinaDivision> All => _all;

        /// <summary>按六位代码查找区划；代码不存在时返回 <see langword="null"/>。</summary>
        public static ChinaDivision Find(string code)
        {
            ChinaDivision division;
            return !string.IsNullOrEmpty(code) && _byCode.TryGetValue(code, out division) ? division : null;
        }

        /// <summary>返回指定区划的直接子级；代码不存在或没有子级时返回空列表。</summary>
        public static IReadOnlyList<ChinaDivision> Children(string code)
        {
            IReadOnlyList<ChinaDivision> children;
            return !string.IsNullOrEmpty(code) && _children.TryGetValue(code, out children) ? children : Empty;
        }

        /// <summary>返回从省级根节点到指定区划的路径；代码不存在时返回空列表。</summary>
        public static IReadOnlyList<ChinaDivision> Path(string code)
        {
            var current = Find(code);
            if (current == null) return Empty;

            var path = new List<ChinaDivision>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (current != null)
            {
                if (!seen.Add(current.Code))
                    throw new InvalidDataException("中国行政区划资源包含循环父级。");
                path.Add(current);
                current = current.ParentCode == null ? null : Find(current.ParentCode);
            }

            path.Reverse();
            return Array.AsReadOnly(path.ToArray());
        }

        private static void ValidateAcyclic()
        {
            foreach (var division in _byCode.Values)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var current = division;
                while (current.ParentCode != null)
                {
                    if (!seen.Add(current.Code))
                        throw new InvalidDataException("中国行政区划资源包含循环父级。");
                    current = _byCode[current.ParentCode];
                }
            }
        }

        private sealed class DivisionRecord
        {
            public DivisionRecord() { }

            public string Code { get; set; }
            public string Name { get; set; }
            public ChinaDivisionLevel Level { get; set; }
            public string ParentCode { get; set; }
        }
    }
}
