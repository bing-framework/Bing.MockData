using System;
using Bing.MockData.Abstractions.Options;

namespace Bing.MockData.Core.Options
{
    /// <summary>
    /// 身份证
    /// </summary>
    public class ChineseIdCardFieldOptions:FieldOptionsBase,IIdCardFieldOptions
    {
        /// <summary>
        /// 身份证有效期起始年份
        /// </summary>
        public int BeginYear { get; set; } = 1970;

        /// <summary>
        /// 身份证有效期结束年份
        /// </summary>
        public int EndYear { get; set; } = 2080;

        /// <summary>
        /// 身份证有效年份长度
        /// </summary>
        public int ValidYear { get; set; } = 20;
    }
}
