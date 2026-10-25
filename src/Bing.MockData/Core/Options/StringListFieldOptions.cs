using System.Collections.Generic;
using Bing.MockData.Abstractions.Options;
namespace Bing.MockData.Core.Options
{
    /// <summary>字符串候选列表配置。</summary>
    public class StringListFieldOptions : FieldOptionsBase, IStringFieldOptions
    {
        /// <summary>非空候选列表，元素不能为 null。</summary>
        public List<string> Values { get; set; }
        /// <summary>复制配置及候选列表。</summary>
        public override FieldOptionsBase Clone()
        {
            var copy = (StringListFieldOptions)base.Clone();
            copy.Values = Values == null ? null : new List<string>(Values);
            return copy;
        }
    }
}
