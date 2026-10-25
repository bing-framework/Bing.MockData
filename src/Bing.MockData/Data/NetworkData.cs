using System;
using Bing.MockData.Core.Options;
using Bing.MockData.Core.Randomizers;

namespace Bing.MockData
{
    /// <summary>
    /// 提供 IP 地址、MAC 地址、GUID 和示例 URL 的生成入口。
    /// </summary>
    public readonly partial struct NetworkData
    {
        /// <summary>
        /// 提供字段种子和共享文本生成入口的上下文。
        /// </summary>
        private readonly MockContext _context;
        /// <summary>
        /// 绑定网络数据入口与其生成上下文。
        /// </summary>
        /// <param name="context">提供独立字段种子和文本生成能力的上下文。</param>
        internal NetworkData(MockContext context) { _context = context; }

        /// <summary>
        /// 生成指定闭区间内的 IPv4 地址。
        /// </summary>
        /// <param name="min">可选的点分十进制下界；为空时使用全零地址。</param>
        /// <param name="max">可选的点分十进制上界；为空时使用全一地址。</param>
        /// <returns>以点分十进制表示的 IPv4 地址。</returns>
        /// <exception cref="ArgumentException">地址不是四段十进制格式时抛出。</exception>
        /// <exception cref="ArgumentOutOfRangeException">上界小于下界时抛出。</exception>
        public string IPv4(string min = null, string max = null) => new IPv4AddressRandomizer(new IPv4AddressFieldOptions { Seed = _context.NextSeed(), MaxTextLength = _context.Budget.MaxTextLength, CancellationToken = _context.CancellationToken, Min = min, Max = max }).Generate();

        /// <summary>
        /// 生成指定闭区间内的 IPv6 地址。
        /// </summary>
        /// <param name="min">可选的 IPv6 下界；为空时使用全零地址。</param>
        /// <param name="max">可选的 IPv6 上界；为空时使用全一地址。</param>
        /// <returns>以标准压缩格式表示的 IPv6 地址。</returns>
        /// <exception cref="ArgumentException">地址格式、地址族无效或包含作用域标识时抛出。</exception>
        /// <exception cref="ArgumentOutOfRangeException">上界小于下界时抛出。</exception>
        public string IPv6(string min = null, string max = null) => new IPv6AddressRandomizer(new IPv6AddressFieldOptions { Seed = _context.NextSeed(), MaxTextLength = _context.Budget.MaxTextLength, CancellationToken = _context.CancellationToken, Min = min, Max = max }).Generate();

        /// <summary>
        /// 生成一个六字节 MAC 地址。
        /// </summary>
        /// <returns>以冒号分隔的大写十六进制 MAC 地址。</returns>
        public string Mac() => new MACAddressRandomizer(new MACAddressFieldOptions { Seed = _context.NextSeed(), MaxTextLength = _context.Budget.MaxTextLength, CancellationToken = _context.CancellationToken }).Generate();

        /// <summary>
        /// 生成一个 GUID。
        /// </summary>
        /// <returns>随机生成的 GUID 值；该值类型不能表示 <see langword="null"/>。</returns>
        public Guid Guid() => new GuidRandomizer(new GuidFieldOptions { Seed = _context.NextSeed(), MaxTextLength = _context.Budget.MaxTextLength, CancellationToken = _context.CancellationToken }).Generate();

        /// <summary>
        /// 为域名生成包含固定长度路径段的 HTTPS 示例 URL。
        /// </summary>
        /// <param name="domain">要使用的有效域名，默认值为 <c>example.com</c>。</param>
        /// <returns>格式为 <c>https://域名/路径</c> 的示例 URL。</returns>
        /// <exception cref="ArgumentException"><paramref name="domain"/> 不是有效域名时抛出。</exception>
        public string Url(string domain = "example.com") { TextData.ValidateDomain(domain);
            if ((long)domain.Length + 17 > _context.Budget.MaxTextLength) throw new MockLimitException(nameof(MockLimits.MaxTextLength), _context.Budget.MaxTextLength);
            return "https://" + domain + "/" + _context.Text.Random(8, 8); }
    }
}
