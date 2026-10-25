using System;

namespace Bing.MockData
{
    /// <summary>
    /// 表示要显式生成的文本边界或无效值类别。
    /// </summary>
    public enum TextBoundary
    {
        /// <summary>
        /// 选择空字符串边界。
        /// </summary>
        Empty,
        /// <summary>
        /// 选择仅包含一个空格的文本边界。
        /// </summary>
        Whitespace,
        /// <summary>
        /// 选择业务最大长度对应的文本边界。
        /// </summary>
        AtMaximum,
        /// <summary>
        /// 选择比业务最大长度多一个字符的文本边界。
        /// </summary>
        Overlong,
        /// <summary>
        /// 选择不含邮箱分隔符的无效邮箱文本。
        /// </summary>
        InvalidEmail,
        /// <summary>
        /// 选择不符合 GUID 格式的无效文本。
        /// </summary>
        InvalidGuid
    }

    /// <summary>
    /// 根据显式选择生成边界值的测试入口。
    /// </summary>
    public readonly partial struct BoundaryData
    {
        /// <summary>
        /// 保存边界值生成所使用的字段上下文。
        /// </summary>
        private readonly MockContext _context;

        /// <summary>
        /// 创建与指定上下文关联的边界值入口。
        /// </summary>
        /// <param name="context">提供随机状态、预算和生命周期的上下文。</param>
        internal BoundaryData(MockContext context) { _context = context; }

        /// <summary>
        /// 生成指定类别的文本边界值。
        /// </summary>
        /// <remarks>
        /// <see cref="TextBoundary.Overlong"/> 会生成比业务最大长度多一个字符的文本，但仍受上下文文本预算限制。
        /// 无效邮箱和 GUID 分别返回固定的无效格式示例文本。
        /// </remarks>
        /// <param name="boundary">要生成的边界类别。</param>
        /// <param name="businessMaximumLength">业务文本最大长度，必须为非负数；默认值为 20。</param>
        /// <returns>所选边界对应的文本。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="businessMaximumLength"/> 为负数，或 <paramref name="boundary"/> 未定义。</exception>
        /// <exception cref="OverflowException">选择超长边界且业务最大长度为 <see cref="int.MaxValue"/>。</exception>
        /// <exception cref="MockLimitException">生成文本超过上下文文本长度预算。</exception>
        /// <exception cref="ObjectDisposedException">根上下文已释放。</exception>
        /// <exception cref="OperationCanceledException">生成期间上下文被取消。</exception>
        public string Text(TextBoundary boundary, int businessMaximumLength = 20)
        {
            _context.Check(); if (businessMaximumLength < 0) throw new ArgumentOutOfRangeException(nameof(businessMaximumLength));
            switch (boundary)
            {
                case TextBoundary.Empty: return "";
                case TextBoundary.Whitespace: return _context.Text.Random(1, 1, " ");
                case TextBoundary.AtMaximum: return _context.Text.Random(businessMaximumLength, businessMaximumLength);
                case TextBoundary.Overlong: int length = checked(businessMaximumLength + 1); return _context.Text.Random(length, length);
                case TextBoundary.InvalidEmail: return Invalid("missing-at-sign");
                case TextBoundary.InvalidGuid: return Invalid("not-a-guid");
                default: throw new ArgumentOutOfRangeException(nameof(boundary));
            }
        }

        /// <summary>
        /// 检查无效示例文本是否超过上下文文本长度预算。
        /// </summary>
        /// <param name="text">要作为无效示例返回的文本。</param>
        /// <returns>未超过预算的输入文本。</returns>
        /// <exception cref="MockLimitException">文本长度超过上下文文本预算。</exception>
        private string Invalid(string text)
        {
            if (text.Length > _context.Budget.MaxTextLength) throw new MockLimitException(nameof(MockLimits.MaxTextLength), _context.Budget.MaxTextLength);
            return text;
        }

        /// <summary>
        /// 校验数值范围并返回其最小端点。
        /// </summary>
        /// <typeparam name="T">范围端点的结构类型。</typeparam>
        /// <param name="min">有效闭区间的最小端点。</param>
        /// <param name="max">有效闭区间的最大端点。</param>
        /// <returns>经校验的 <paramref name="min"/> 值。</returns>
        /// <exception cref="ArgumentOutOfRangeException">范围上下界无效。</exception>
        /// <exception cref="ArgumentException">DateTime 上下界的 <see cref="DateTime.Kind"/> 不一致。</exception>
        /// <exception cref="NotSupportedException"><typeparamref name="T"/> 不是范围校验支持的类型。</exception>
        /// <exception cref="ObjectDisposedException">根上下文已释放。</exception>
        /// <exception cref="OperationCanceledException">当前上下文已取消。</exception>
        public T Minimum<T>(T min, T max) where T : struct { _context.Check(); Internals.Generators.RandomValueGenerator.Validate(min, max); return min; }

        /// <summary>
        /// 校验数值范围并返回其最大端点。
        /// </summary>
        /// <typeparam name="T">范围端点的结构类型。</typeparam>
        /// <param name="min">有效闭区间的最小端点。</param>
        /// <param name="max">有效闭区间的最大端点。</param>
        /// <returns>经校验的 <paramref name="max"/> 值。</returns>
        /// <exception cref="ArgumentOutOfRangeException">范围上下界无效。</exception>
        /// <exception cref="ArgumentException">DateTime 上下界的 <see cref="DateTime.Kind"/> 不一致。</exception>
        /// <exception cref="NotSupportedException"><typeparamref name="T"/> 不是范围校验支持的类型。</exception>
        /// <exception cref="ObjectDisposedException">根上下文已释放。</exception>
        /// <exception cref="OperationCanceledException">当前上下文已取消。</exception>
        public T Maximum<T>(T min, T max) where T : struct { _context.Check(); Internals.Generators.RandomValueGenerator.Validate(min, max); return max; }
    }
    public sealed partial class MockContext
    {
        /// <summary>
        /// 获取使用当前上下文的边界值生成入口。
        /// </summary>
        public BoundaryData Boundaries => new BoundaryData(this);
    }
}
