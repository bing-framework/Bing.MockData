using System.Collections.Generic;
using Bing.MockData.Abstractions.Options;

namespace Bing.MockData.Core.Options
{
    /// <summary>
    /// 字符串列表字段配置
    /// </summary>
    public class StringListFieldOptions:FieldOptionsBase,IStringFieldOptions
    {
        /// <summary>
        /// 值列表
        /// </summary>
        public List<string> Values { get; set; }
    }
}
