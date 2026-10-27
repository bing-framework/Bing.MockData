using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.MockData.Core.Options;
using Bing.MockData.Core.Randomizers;
using Bing.MockData.Internals.Generators;

namespace Bing.MockData
{
    /// <summary>
    /// 提供基于当前上下文生成文本的入口。
    /// </summary>
    /// <remarks>
    /// 文本长度按 UTF-16 单元计数；自定义字符表按完整 Unicode 标量抽样，避免生成孤立代理项。
    /// </remarks>
    public readonly partial struct TextData
    {
        /// <summary>
        /// 保存默认随机文本使用的 ASCII 字母和数字字符表。
        /// </summary>
        private const string DefaultAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        /// <summary>
        /// 保存文本生成所使用的字段上下文。
        /// </summary>
        private readonly MockContext _context;

        /// <summary>
        /// 创建与指定上下文关联的文本生成入口。
        /// </summary>
        /// <param name="context">提供随机状态、预算、取消和区域设置的上下文。</param>
        internal TextData(MockContext context) { _context = context; }

        /// <summary>
        /// 从指定字符表中生成随机文本。
        /// </summary>
        /// <remarks>
        /// 输出长度在闭区间内按 UTF-16 单元计数；若字符表只含补充平面字符，则只能生成偶数长度。
        /// 空字符表仅在允许的最大长度为零时可用于生成空文本。
        /// </remarks>
        /// <param name="min">允许的最小文本长度，默认值为 8。</param>
        /// <param name="max">允许的最大文本长度，默认值为 8。</param>
        /// <param name="alphabet">用于抽样的字符表；默认使用 ASCII 字母和数字。</param>
        /// <returns>长度在指定范围内且由字符表中的 Unicode 标量组成的文本。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="min"/> 小于零或 <paramref name="max"/> 小于 <paramref name="min"/>。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="alphabet"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentException">字符表包含孤立代理项、字符表为空但请求非空文本，或字符表无法组成指定长度。</exception>
        /// <exception cref="MockLimitException">请求的最大长度超过上下文文本预算。</exception>
        /// <exception cref="ObjectDisposedException">根上下文已释放。</exception>
        /// <exception cref="OperationCanceledException">生成期间上下文被取消。</exception>
        public string Random(int min = 8, int max = 8, string alphabet = DefaultAlphabet)
        {
            if (min < 0 || max < min) throw new ArgumentOutOfRangeException(nameof(min));
            if (max > _context.Budget.MaxTextLength) throw new MockLimitException(nameof(MockLimits.MaxTextLength), _context.Budget.MaxTextLength);
            if (alphabet == null) throw new ArgumentNullException(nameof(alphabet));
            _context.Check();
            if (alphabet == DefaultAlphabet)
            {
                var selectedLength = _context.Numbers.Int(min, max);
                var owner = _context;
                return owner.WithRandom(r =>
                {
                    var output = new StringBuilder(selectedLength);
                    for (int i = 0; i < selectedLength; i++) { owner.Check(); output.Append(DefaultAlphabet[r.Next(DefaultAlphabet.Length)]); }
                    return output.ToString();
                });
            }
            var tokens = new List<string>();
            for (int i = 0; i < alphabet.Length; i++)
            {
                _context.Check();
                if (char.IsHighSurrogate(alphabet[i]))
                { if (i + 1 >= alphabet.Length || !char.IsLowSurrogate(alphabet[i + 1])) throw new ArgumentException("字符表包含孤立代理项。", nameof(alphabet)); tokens.Add(alphabet.Substring(i++, 2)); }
                else { if (char.IsLowSurrogate(alphabet[i])) throw new ArgumentException("字符表包含孤立代理项。", nameof(alphabet)); tokens.Add(alphabet[i].ToString()); }
            }
            var single = tokens.Where(t => t.Length == 1).ToArray();
            if (max > 0 && tokens.Count == 0) throw new ArgumentException("字符表不能为空。", nameof(alphabet));
            int length;
            if (tokens.Count > 0 && single.Length == 0)
            { int first = min / 2 + min % 2, last = max / 2; if (first > last) throw new ArgumentException("字符表无法生成该长度。"); length = checked(_context.Numbers.Int(first, last) * 2); }
            else length = _context.Numbers.Int(min, max);
            var context = _context;
            return context.WithRandom(r =>
            {
                var result = new StringBuilder(length);
                while (result.Length < length)
                { context.Check(); var token = length - result.Length == 1 ? single[r.Next(single.Length)] : tokens[r.Next(tokens.Count)]; result.Append(token); }
                return result.ToString();
            });
        }

        /// <summary>
        /// 根据有限正则表达式子集生成文本。
        /// </summary>
        /// <remarks>
        /// 支持字面量、点、常见转义、字符类、普通或非捕获分组、选择、问号及有界量词。
        /// 不支持无界量词、回溯引用、零宽断言、命名组和内联选项；模式的最大输出长度在生成前检查。
        /// </remarks>
        /// <param name="pattern">符合生成器支持子集的 .NET 正则表达式。</param>
        /// <returns>符合所支持模式结构的生成文本。</returns>
        /// <exception cref="ArgumentException">模式为空、不是有效正则表达式或包含不支持的结构。</exception>
        /// <exception cref="MockLimitException">模式最大输出长度或生成步骤超过上下文预算。</exception>
        /// <exception cref="ObjectDisposedException">根上下文已释放。</exception>
        /// <exception cref="OperationCanceledException">生成期间上下文被取消。</exception>
        public string Regex(string pattern) => new TextRegexRandomizer(new TextRegexFieldOptions
        { Seed = _context.NextSeed(), Pattern = pattern, MaxTextLength = _context.Budget.MaxTextLength, CancellationToken = _context.CancellationToken }).Generate();

        /// <summary>在指定 UTF-16 长度区间内构造有限正则文本；无可用候选时明确失败。</summary>
        public string Regex(string pattern, int minLength, int maxLength)
        {
            return GenerateRegex(pattern, minLength, maxLength, false);
        }

        internal string RegexCodePoints(string pattern, int minLength, int maxLength) => GenerateRegex(pattern, minLength, maxLength, true);

        private string GenerateRegex(string pattern, int minLength, int maxLength, bool codePoints)
        {
            _context.Check();
            if (minLength < 0 || maxLength < minLength) throw new ArgumentOutOfRangeException(nameof(minLength));
            if (maxLength > _context.Budget.MaxTextLength) throw new MockLimitException(nameof(MockLimits.MaxTextLength), _context.Budget.MaxTextLength);
            var generator = new BoundedRegexGenerator(pattern, _context.Budget.MaxTextLength, true, _context.CancellationToken);
            var context = _context;
            return context.WithRandom(random => generator.Generate(random, context.CancellationToken, minLength, maxLength, context.Budget.MaxAttempts, codePoints));
        }

        /// <summary>
        /// 生成指定词数范围内的示例文本。
        /// </summary>
        /// <remarks>
        /// 词数范围为闭区间；词库生成和最终文本长度均受上下文限制。
        /// </remarks>
        /// <param name="min">最少词数，默认值为 3。</param>
        /// <param name="max">最多词数，默认值为 10。</param>
        /// <returns>包含指定范围词数的示例文本；词数为零时返回空字符串。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="min"/> 小于零或 <paramref name="max"/> 小于 <paramref name="min"/>。</exception>
        /// <exception cref="MockLimitException">请求词数或最终文本长度超过上下文预算。</exception>
        /// <exception cref="ObjectDisposedException">根上下文已释放。</exception>
        /// <exception cref="OperationCanceledException">生成期间上下文被取消。</exception>
        public string Words(int min = 3, int max = 10)
        {
            _context.Check();if(min<0||max<min)throw new ArgumentOutOfRangeException(nameof(min));
            if(max>_context.Budget.MaxTextLength)throw new MockLimitException(nameof(MockLimits.MaxTextLength),_context.Budget.MaxTextLength);
            string[] words=_context.Locale=="zh-CN"?new[]{"服务","用户","产品","团队","设计","品质","体验","计划","合作","创新","数据","方案"}:new[]{"service","customer","product","team","design","quality","experience","plan","cooperation","innovation","data","solution"};
            var result=new System.Text.StringBuilder();int count=_context.Numbers.Int(min,max);
            for(int i=0;i<count;i++){_context.Check();string word=_context.Collections.Pick(words);if(result.Length+word.Length+(i>0?1:0)>_context.Budget.MaxTextLength)throw new MockLimitException(nameof(MockLimits.MaxTextLength),_context.Budget.MaxTextLength);if(i>0)result.Append(' ');result.Append(word);}
            return result.ToString();
        }

        /// <summary>
        /// 生成指定数量的示例文本段落。
        /// </summary>
        /// <remarks>
        /// 每段由词库构成，生成结果以两个换行符分隔段落。
        /// </remarks>
        /// <param name="count">要生成的段落数，默认值为 1，可以为零。</param>
        /// <returns>由指定数量段落组成的示例文本；段落数为零时返回空字符串。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> 小于零。</exception>
        /// <exception cref="MockLimitException">段落数或最终文本长度超过上下文预算。</exception>
        /// <exception cref="ObjectDisposedException">根上下文已释放。</exception>
        /// <exception cref="OperationCanceledException">生成期间上下文被取消。</exception>
        public string Paragraphs(int count = 1)
        {
            _context.Check();if(count<0)throw new ArgumentOutOfRangeException(nameof(count));if(count>_context.Budget.MaxTextLength)throw new MockLimitException(nameof(MockLimits.MaxTextLength),_context.Budget.MaxTextLength);
            var result=new System.Text.StringBuilder();for(int i=0;i<count;i++){_context.Check();string paragraph=_context.ForField("paragraph["+i+"]").Text.BusinessParagraph(3,Math.Min(1000,_context.Budget.MaxTextLength));if(result.Length+paragraph.Length+(i>0?2:0)>_context.Budget.MaxTextLength)throw new MockLimitException(nameof(MockLimits.MaxTextLength),_context.Budget.MaxTextLength);if(i>0)result.Append("\n\n");result.Append(paragraph);}return result.ToString();
        }

        /// <summary>
        /// 根据当前区域设置生成一个示例姓名。
        /// </summary>
        /// <remarks>
        /// 中文区域使用中文姓名生成器，英文区域使用西文全名生成器。
        /// </remarks>
        /// <returns>由当前上下文区域设置选择的示例姓名。</returns>
        /// <exception cref="MockLimitException">生成姓名超过上下文文本长度预算。</exception>
        /// <exception cref="ObjectDisposedException">根上下文已释放。</exception>
        /// <exception cref="OperationCanceledException">生成期间上下文被取消。</exception>
        public string Name() => _context.Locale == "zh-CN"
            ? new ChineseNameRandomizer(new ChineseNameFieldOptions { Seed = _context.NextSeed(), MaxTextLength = _context.Budget.MaxTextLength, CancellationToken = _context.CancellationToken }).Generate()
            : new FullNameRandomizer(new FullNameFieldOptions { Seed = _context.NextSeed(), MaxTextLength = _context.Budget.MaxTextLength, CancellationToken = _context.CancellationToken }).Generate();

        /// <summary>
        /// 生成使用指定域名的示例邮箱地址。
        /// </summary>
        /// <remarks>
        /// 地址本地部分为 12 个小写英文字母；域名按 DNS 主机名规则验证。
        /// </remarks>
        /// <param name="domain">有效 DNS 域名；默认值为 <c>example.com</c>。</param>
        /// <returns>由随机本地部分和指定域名组成的邮箱文本。</returns>
        /// <exception cref="ArgumentException"><paramref name="domain"/> 不是有效 DNS 域名。</exception>
        /// <exception cref="MockLimitException">邮箱文本超过上下文最大文本长度。</exception>
        /// <exception cref="ObjectDisposedException">根上下文已释放。</exception>
        /// <exception cref="OperationCanceledException">生成期间上下文被取消。</exception>
        public string Email(string domain = "example.com")
        {
            ValidateDomain(domain);
            var value = Random(12, 12, "abcdefghijklmnopqrstuvwxyz") + "@" + domain;
            if (value.Length > _context.Budget.MaxTextLength) throw new MockLimitException(nameof(MockLimits.MaxTextLength), _context.Budget.MaxTextLength);
            return value;
        }

        /// <summary>
        /// 验证域名是否被识别为 DNS 主机名。
        /// </summary>
        /// <param name="domain">要检查的域名文本。</param>
        /// <exception cref="ArgumentException"><paramref name="domain"/> 为空白或不能识别为 DNS 主机名。</exception>
        internal static void ValidateDomain(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain) || Uri.CheckHostName(domain) != UriHostNameType.Dns)
                throw new ArgumentException("需要有效的 DNS 域名。", nameof(domain));
        }
    }
}
