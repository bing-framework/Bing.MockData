using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Bing.MockData.Schema
{
    // Schema-sized immutable plan. Each format refers to one private document, never one enum per node.
    internal sealed class FormatValidationPlan
    {
        private readonly SchemaDialect _dialects;
        private readonly bool? _assertion;
        private readonly string _prefix;
        private readonly Dictionary<string, string> _formats = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> _used = new HashSet<string>(StringComparer.Ordinal);
        internal readonly string Json;
        internal readonly Dictionary<string, byte[]> Documents = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        internal bool HasFormats => _formats.Count != 0;
        internal bool HasUsedFormats => _used.Count != 0;

        internal FormatValidationPlan(string json, IReadOnlyDictionary<string, string> documents, SchemaDialect dialects, bool? assertion, string identity)
        {
            _dialects = dialects;
            _assertion = assertion;
            _prefix = "urn:bing-mockdata:format:" + identity + ":";
            Json = Rewrite(json);
            foreach (var document in documents) Documents.Add(document.Key, Encoding.UTF8.GetBytes(Rewrite(document.Value)));
        }

        private string Rewrite(string json)
        {
            var root = ExactJson.Parse(json);
            void Visit(JToken token, string inherited)
            {
                if (!(token is JObject node)) return;
                string dialect = SchemaDialect.Declared(node, inherited);
                _dialects.RemoveInactive(node, dialect, _assertion == true);
                foreach (var child in FormatAssertions.Children(node, key => _dialects.Enabled(dialect, key)).ToArray()) Visit(child, dialect);
                // Private references need applicators; originally inactive keywords have already been removed.
                if (node["$vocabulary"] is JObject vocabulary)
                {
                    vocabulary["https://json-schema.org/draft/2020-12/vocab/validation"] = true;
                    vocabulary["https://json-schema.org/draft/2020-12/vocab/applicator"] = true;
                }
                if (!(_assertion ?? _dialects.AssertsFormat(dialect)) || node["format"]?.Type != JTokenType.String) return;
                string format = (string)node["format"];
                if (!FormatAssertions.Corrected.Contains(format)) return;
                string uri = _prefix + format;
                _formats[uri] = format;
                node.Remove("format");
                if (!(node["allOf"] is JArray all)) { all = new JArray(); node["allOf"] = all; }
                all.Add(new JObject { ["$ref"] = uri });
            }
            Visit(root, SchemaDialect.Default);
            return ExactJson.Write(root);
        }

        internal bool Resolve(string uri, IReadOnlyDictionary<string, string> constraints, out ReadOnlyMemory<byte> document)
        {
            if (_formats.ContainsKey(uri))
            {
                _used.Add(uri); // Compilation and evaluation are serialized by the owning CompiledSchema.
                document = Encoding.UTF8.GetBytes(constraints != null && constraints.TryGetValue(uri, out var json) ? json : "true");
                return true;
            }
            if (Documents.TryGetValue(uri, out var bytes)) { document = bytes; return true; }
            document = default;
            return false;
        }

        internal Dictionary<string, string> Constraints(string json, TimeSpan timeout, long maximumBytes, Action step)
        {
            var strings = new HashSet<string>(StringComparer.Ordinal);
            void Add(string value)
            {
                // Charge long strings as well as nodes; work before/between regex calls remains cancellable.
                for (int i = 0; i <= value.Length / 1024; i++) step();
                strings.Add(value);
            }
            void Collect(JToken token)
            {
                step();
                if (token.Type == JTokenType.String) Add((string)token);
                else if (token is JObject obj) foreach (var property in obj.Properties()) { Add(property.Name); Collect(property.Value); }
                else if (token is JArray array) foreach (var child in array) Collect(child);
            }
            Collect(ExactJson.Parse(json));
            var sorted = strings.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            long bytes = 0;
            foreach (string uri in _used.OrderBy(x => x, StringComparer.Ordinal))
            {
                var invalid = new JArray();
                foreach (string value in sorted)
                {
                    step();
                    if (!FormatAssertions.Valid(_formats[uri], value, timeout)) invalid.Add(value);
                }
                if (invalid.Count == 0) continue;
                string document = ExactJson.Write(new JObject { ["$schema"] = SchemaDialect.Default, ["not"] = new JObject { ["enum"] = invalid } });
                bytes = checked(bytes + Encoding.UTF8.GetByteCount(document));
                if (bytes > maximumBytes) throw new SchemaGenerationException(SchemaFailure.BudgetExhausted, "格式约束输入字节超限。");
                step();
                result.Add(uri, document);
            }
            return result;
        }
    }
}
