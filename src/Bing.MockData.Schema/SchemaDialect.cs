using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Bing.MockData.Schema
{
    // One vocabulary decision is shared by reference discovery, generation and private format schemas.
    internal sealed class SchemaDialect
    {
        internal const string Default = "https://json-schema.org/draft/2020-12/schema";
        private readonly Dictionary<string, JObject> _vocabularies = new Dictionary<string, JObject>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> Keywords = BuildKeywords();

        internal SchemaDialect(IReadOnlyDictionary<string, string> documents)
        {
            foreach (var document in documents)
            {
                if (!(ExactJson.Parse(document.Value) is JObject root) || !(root["$vocabulary"] is JObject vocabulary)) continue;
                _vocabularies[document.Key] = vocabulary;
                if (root["$id"]?.Type == JTokenType.String)
                    _vocabularies[new Uri(new Uri(document.Key), (string)root["$id"]).AbsoluteUri] = vocabulary;
            }
        }

        internal static string Declared(JObject node, string inherited) =>
            node["$schema"]?.Type == JTokenType.String ? (string)node["$schema"] : inherited;

        internal bool Enabled(string dialect, string keyword)
        {
            if (!_vocabularies.TryGetValue(dialect, out var vocabulary) || !Keywords.TryGetValue(keyword, out var group)) return true;
            bool Has(string name) => vocabulary["https://json-schema.org/draft/2020-12/vocab/" + name] != null ||
                vocabulary["https://json-schema.org/draft/2019-09/vocab/" + name] != null;
            return group == "format" ? Has("format-annotation") || Has("format-assertion") || Has("format") : Has(group);
        }

        internal bool AssertsFormat(string dialect) => _vocabularies.TryGetValue(dialect, out var vocabulary) &&
            vocabulary["https://json-schema.org/draft/2020-12/vocab/format-assertion"]?.Value<bool>() == true;

        internal void RemoveInactive(JObject node, string dialect, bool keepFormat = false)
        {
            foreach (var property in node.Properties().ToArray())
                if (!(keepFormat && property.Name == "format") && !Enabled(dialect, property.Name)) property.Remove();
        }

        internal void Normalize(JToken token, string inherited = Default)
        {
            if (!(token is JObject node)) return;
            string dialect = Declared(node, inherited);
            RemoveInactive(node, dialect);
            foreach (var child in FormatAssertions.Children(node, key => Enabled(dialect, key))) Normalize(child, dialect);
        }

        private static Dictionary<string, string> BuildKeywords()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            void Add(string group, string names) { foreach (string name in names.Split(' ')) result.Add(name, group); }
            Add("core", "$id $schema $ref $dynamicRef $anchor $dynamicAnchor $vocabulary $defs $comment definitions");
            Add("validation", "type const enum multipleOf minimum maximum exclusiveMinimum exclusiveMaximum minLength maxLength pattern minItems maxItems uniqueItems minContains maxContains minProperties maxProperties required dependentRequired");
            Add("applicator", "allOf anyOf oneOf not if then else properties patternProperties additionalProperties dependentSchemas propertyNames prefixItems items contains");
            Add("unevaluated", "unevaluatedItems unevaluatedProperties");
            Add("content", "contentEncoding contentMediaType contentSchema");
            Add("format", "format");
            return result;
        }
    }
}
