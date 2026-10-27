using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Bing.MockData.Core.Options;

namespace Bing.MockData.Internals.Generators
{
    /// <summary>解析有限正则子集并按实例随机源生成文本。</summary>
    internal sealed class BoundedRegexGenerator
    {
        private readonly RegexNode _root;
        private readonly int _maxTextLength;
        private readonly long _characterIntervalCount;

        public BoundedRegexGenerator(string pattern, int maxTextLength, bool constrainOutput = false, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(pattern)) throw new ArgumentException("正则模式不能为空。", nameof(pattern));
            if (maxTextLength < 0) throw new ArgumentOutOfRangeException(nameof(maxTextLength));

            // 模式与生成步骤都受预算约束；空分支或深层分组不能绕过输出长度限制。
            long maximumSteps = Math.Max(1024L, (long)maxTextLength * 16);
            if (pattern.Length > maximumSteps) throw new MockLimitException("RegexGenerationSteps", maximumSteps);
            cancellationToken.ThrowIfCancellationRequested();
            var parser = new Parser(pattern, maximumSteps, cancellationToken);
            _root = parser.Parse();
            _characterIntervalCount = parser.CharacterIntervalCount;
            if (!constrainOutput && _root.MaximumLength > maxTextLength)
                throw new MockLimitException(nameof(FieldOptionsBase.MaxTextLength), maxTextLength);
            if (!constrainOutput && _root.MaximumSteps > maximumSteps) throw new MockLimitException("RegexGenerationSteps", maximumSteps);

            try
            {
                _ = new Regex(pattern);
            }
            catch (ArgumentException exception)
            {
                throw new ArgumentException("模式不是有效的 .NET 正则表达式。", nameof(pattern), exception);
            }
            cancellationToken.ThrowIfCancellationRequested();
            _maxTextLength = maxTextLength;
        }

        internal long CharacterIntervalCount => _characterIntervalCount;

        public string Generate(Random random, CancellationToken cancellationToken)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            cancellationToken.ThrowIfCancellationRequested();

            var builder = new StringBuilder((int)Math.Min(_root.MaximumLength, 128L));
            _root.AppendTo(builder, random, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (builder.Length > _maxTextLength)
                throw new MockLimitException(nameof(FieldOptionsBase.MaxTextLength), _maxTextLength);
            return builder.ToString();
        }

        // 区间分配只选择可能满足剩余长度的节点/重复次数；稀疏长度仍受重试和步骤预算约束。
        internal string Generate(Random random, CancellationToken cancellationToken, int minimum, int maximum, int attempts, bool codePoints = false)
        {
            if (minimum < 0 || maximum < minimum || maximum > _maxTextLength) throw new ArgumentOutOfRangeException(nameof(minimum));
            long steps = Math.Max(1024L, (long)_maxTextLength * 16);
            var output = new StringBuilder(Math.Min(maximum, 128));
            int generatedLength = 0;
            void Step() { cancellationToken.ThrowIfCancellationRequested(); if (--steps < 0) throw new MockLimitException("RegexGenerationSteps", Math.Max(1024L, (long)_maxTextLength * 16)); }
            long MinimumLength(RegexNode node) => codePoints ? node.MinimumCodePointLength : node.MinimumLength;
            long MaximumLength(RegexNode node) => codePoints ? node.MaximumCodePointLength : node.MaximumLength;
            bool Emit(RegexNode node, long low, long high)
            {
                Step(); low = Math.Max(low, MinimumLength(node)); high = Math.Min(high, MaximumLength(node));
                if (low > high) return false;
                if (node is AlternationNode alternative)
                {
                    var possible = alternative.Nodes.Where(x => MinimumLength(x) <= high && MaximumLength(x) >= low).ToArray();
                    if (possible.Length == 0) return false;
                    return Emit(possible[random.Next(possible.Length)], low, high);
                }
                if (node is SequenceNode sequence) return EmitSequence(sequence.Nodes, low, high);
                if (node is RepeatNode repeat)
                {
                    var repeatMinimumLength = MinimumLength(repeat.Node);
                    var repeatMaximumLength = MaximumLength(repeat.Node);
                    if (repeatMaximumLength == 0) return low == 0;
                    long first = Math.Max(repeat.MinimumCount, low / repeatMaximumLength + (low % repeatMaximumLength == 0 ? 0 : 1));
                    long last = Math.Min(repeat.MaximumCount, repeatMinimumLength > 0 ? high / repeatMinimumLength : Math.Max(first, high));
                    // 零长度节点无需额外重复；过多的空操作同样受步骤预算限制。
                    if (first > last || first > steps) return false;
                    last = Math.Min(last, steps);
                    int count = NextInclusive(random, (int)first, (int)last);
                    return EmitRepeated(repeat.Node, count, low, high);
                }
                var start = output.Length;
                node.AppendTo(output, random, cancellationToken);
                if (output.Length > _maxTextLength) return false;
                var addedUtf16 = output.Length - start;
                generatedLength += codePoints ? CountCodePoints(output, start, addedUtf16) : addedUtf16;
                return true;
            }
            bool EmitSequence(RegexNode[] nodes, long low, long high)
            {
                long remainingMin = SumMinimum(nodes, codePoints), remainingMax = SumMaximum(nodes, codePoints); int start = generatedLength;
                foreach (var child in nodes)
                {
                    remainingMin -= MinimumLength(child); remainingMax -= MaximumLength(child);
                    long used = generatedLength - start;
                    if (!Emit(child, Math.Max(0, low - used - remainingMax), high - used - remainingMin)) return false;
                }
                return generatedLength - start >= low && generatedLength - start <= high;
            }
            bool EmitRepeated(RegexNode node, int count, long low, long high)
            {
                int start = generatedLength;
                for (int i = 0; i < count; i++)
                {
                    long used = generatedLength - start, remaining = count - i - 1;
                    if (!Emit(node, Math.Max(0, low - used - Multiply(MaximumLength(node), (int)remaining)), high - used - Multiply(MinimumLength(node), (int)remaining))) return false;
                }
                return generatedLength - start >= low && generatedLength - start <= high;
            }
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                output.Clear(); generatedLength = 0; if (Emit(_root, minimum, maximum)) return output.ToString();
            }
            throw new InvalidOperationException("在预算内无法构造同时满足正则与长度约束的文本。");
        }

        private abstract class RegexNode
        {
            protected RegexNode(long minimumLength, long maximumLength, long minimumCodePointLength, long maximumCodePointLength, long maximumSteps = 1)
            {
                MinimumLength = minimumLength;
                MaximumLength = maximumLength;
                MinimumCodePointLength = minimumCodePointLength;
                MaximumCodePointLength = maximumCodePointLength;
                MaximumSteps = maximumSteps;
            }

            public long MinimumLength { get; }
            public long MaximumLength { get; }
            public long MinimumCodePointLength { get; }
            public long MaximumCodePointLength { get; }
            public long MaximumSteps { get; }
            public abstract void AppendTo(StringBuilder builder, Random random, CancellationToken cancellationToken);
        }

        private sealed class EmptyNode : RegexNode
        {
            public EmptyNode() : base(0, 0, 0, 0) { }
            public override void AppendTo(StringBuilder builder, Random random, CancellationToken cancellationToken) => cancellationToken.ThrowIfCancellationRequested();
        }

        private sealed class LiteralNode : RegexNode
        {
            private readonly string _value;
            private static int CodePointCount(string value)
            {
                var count = value.Length;
                for (var index = 0; index < value.Length; index++)
                {
                    if (char.IsHighSurrogate(value[index]) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
                    {
                        count--;
                        index++;
                    }
                }
                return count;
            }
            public LiteralNode(string value) : base(value.Length, value.Length, CodePointCount(value), CodePointCount(value)) { _value = value; }
            public override void AppendTo(StringBuilder builder, Random random, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                builder.Append(_value);
            }
        }

        private sealed class CharacterClassNode : RegexNode
        {
            private readonly CharacterSet _characters;
            public CharacterClassNode(CharacterSet characters) : base(1, 1, 1, 1) { _characters = characters; }
            public override void AppendTo(StringBuilder builder, Random random, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                builder.Append(_characters.Pick(random));
            }
        }

        private sealed class SequenceNode : RegexNode
        {
            private readonly RegexNode[] _nodes;
            internal RegexNode[] Nodes => _nodes;
            public SequenceNode(IList<RegexNode> nodes) : base(SumMinimum(nodes), SumMaximum(nodes), SumMinimum(nodes, true), SumMaximum(nodes, true), nodes.Aggregate(1L, (steps, node) => Add(steps, node.MaximumSteps)))
            {
                _nodes = nodes.ToArray();
            }
            public override void AppendTo(StringBuilder builder, Random random, CancellationToken cancellationToken)
            {
                foreach (var node in _nodes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    node.AppendTo(builder, random, cancellationToken);
                }
            }
        }

        private sealed class AlternationNode : RegexNode
        {
            private readonly RegexNode[] _alternatives;
            internal RegexNode[] Nodes => _alternatives;
            public AlternationNode(IList<RegexNode> alternatives) : base(Minimum(alternatives), Maximum(alternatives), Minimum(alternatives, true), Maximum(alternatives, true), Add(1, alternatives.Max(node => node.MaximumSteps)))
            {
                _alternatives = alternatives.ToArray();
            }
            public override void AppendTo(StringBuilder builder, Random random, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _alternatives[random.Next(_alternatives.Length)].AppendTo(builder, random, cancellationToken);
            }
        }

        private sealed class RepeatNode : RegexNode
        {
            private readonly RegexNode _node;
            private readonly int _minimumCount;
            private readonly int _maximumCount;
            internal RegexNode Node => _node;
            internal int MinimumCount => _minimumCount;
            internal int MaximumCount => _maximumCount;

            public RepeatNode(RegexNode node, int minimumCount, int maximumCount)
                : base(Multiply(node.MinimumLength, minimumCount), Multiply(node.MaximumLength, maximumCount), Multiply(node.MinimumCodePointLength, minimumCount), Multiply(node.MaximumCodePointLength, maximumCount),
                    node.MaximumLength == 0 ? 1 : Add(1, Multiply(node.MaximumSteps, maximumCount)))
            {
                _node = node;
                _minimumCount = minimumCount;
                _maximumCount = maximumCount;
            }

            public override void AppendTo(StringBuilder builder, Random random, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_node.MaximumLength == 0) return;

                var count = _minimumCount == _maximumCount
                    ? _minimumCount
                    : NextInclusive(random, _minimumCount, _maximumCount);
                for (var i = 0; i < count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _node.AppendTo(builder, random, cancellationToken);
                }
            }
        }

        private sealed class Parser
        {
            private const int MaximumGroupDepth = 64;
            private readonly string _pattern;
            private readonly CancellationToken _cancellationToken;
            private readonly long _maximumSteps;
            private long _remainingSteps;
            private int _position;
            private int _end;
            private int _groupDepth;
            private long _characterIntervalCount;

            public Parser(string pattern, long maximumSteps, CancellationToken cancellationToken)
            {
                _pattern = pattern;
                _maximumSteps = maximumSteps;
                _remainingSteps = maximumSteps;
                _cancellationToken = cancellationToken;
                _end = pattern.Length;
                StripOuterAnchors();
            }

            public long CharacterIntervalCount => _characterIntervalCount;

            public void Step(long operations = 1)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                if (operations <= 0) return;
                if (operations > _remainingSteps) throw new MockLimitException("RegexGenerationSteps", _maximumSteps);
                _remainingSteps -= operations;
            }

            public RegexNode Parse()
            {
                Step();
                var node = ParseAlternation();
                if (_position != _end) throw Error("括号或表达式结构不受支持。");
                return node;
            }

            private RegexNode ParseAlternation()
            {
                var alternatives = new List<RegexNode> { ParseSequence() };
                while (At('|'))
                {
                    Step();
                    _position++;
                    alternatives.Add(ParseSequence());
                }
                return alternatives.Count == 1 ? alternatives[0] : new AlternationNode(alternatives);
            }

            private RegexNode ParseSequence()
            {
                var nodes = new List<RegexNode>();
                while (_position < _end && !At('|') && !At(')'))
                {
                    var node = ParseAtom();
                    if (_position < _end)
                    {
                        if (At('?'))
                        {
                            Step();
                            _position++;
                            node = new RepeatNode(node, 0, 1);
                            if (At('?')) { Step(); _position++; }
                        }
                        else if (At('{'))
                        {
                            node = ParseBoundedRepeat(node);
                            if (At('?')) _position++;
                        }
                        else if (At('*') || At('+'))
                        {
                            throw Error("不支持无界量词；请改用 {最小次数,最大次数}。");
                        }
                    }
                    nodes.Add(node);
                }

                if (nodes.Count == 0) return new EmptyNode();
                return nodes.Count == 1 ? nodes[0] : new SequenceNode(nodes);
            }

            private RegexNode ParseAtom()
            {
                Step();
                if (_position >= _end) throw Error("量词前缺少表达式。");
                var current = _pattern[_position];
                if (current == '(') return ParseGroup();
                if (current == '[') return ParseCharacterClass();
                if (current == '.')
                {
                    _position++;
                    return CharacterClass(CharacterSets.Dot);
                }
                if (current == '\\')
                {
                    _position++;
                    var element = ParseEscape(false);
                    return element.IsLiteral
                        ? (RegexNode)new LiteralNode(element.Literal.ToString())
                        : CharacterClass(element.Characters);
                }
                if (current == '^' || current == '$') throw Error("锚点只支持位于整个模式两端。");
                if (current == '*' || current == '+' || current == '?' || current == '{' || current == '}')
                    throw Error("量词前缺少表达式或量词格式错误。");
                if (current == ')') throw Error("存在多余的右括号。");

                var start = _position++;
                if (char.IsHighSurrogate(current))
                {
                    if (_position < _end && char.IsLowSurrogate(_pattern[_position]))
                    {
                        _position++;
                        if (At('?') || At('{') || At('*') || At('+'))
                            throw Error("代理对后的量词需要显式分组，例如 (?:😀){2}。");
                        return new LiteralNode(_pattern.Substring(start, 2));
                    }
                    throw Error("不能生成孤立的 UTF-16 高代理项。");
                }
                if (char.IsLowSurrogate(current)) throw Error("不能生成孤立的 UTF-16 低代理项。");
                return new LiteralNode(current.ToString());
            }

            private RegexNode ParseGroup()
            {
                Step();
                _position++;
                if (At('?'))
                {
                    _position++;
                    if (!At(':')) throw Error("不支持断言、命名组或内联选项；仅支持普通或非捕获分组。");
                    _position++;
                }
                if (++_groupDepth > MaximumGroupDepth) throw Error("正则分组嵌套超过 64 层。");

                var node = ParseAlternation();
                if (!At(')')) throw Error("分组缺少右括号。");
                _position++;
                _groupDepth--;
                return node;
            }

            private RegexNode ParseBoundedRepeat(RegexNode node)
            {
                _position++;
                var minimum = ParseCount();
                int maximum;
                if (At('}'))
                {
                    maximum = minimum;
                }
                else if (At(','))
                {
                    _position++;
                    if (At('}')) throw Error("不支持无上限的 {最小次数,} 量词。");
                    maximum = ParseCount();
                }
                else
                {
                    throw Error("量词必须使用 {n} 或 {最小次数,最大次数}。");
                }

                if (!At('}')) throw Error("有界量词缺少右花括号。");
                Step();
                _position++;
                if (maximum < minimum) throw Error("量词最大次数不能小于最小次数。");
                return maximum == 0 ? new EmptyNode() : new RepeatNode(node, minimum, maximum);
            }

            private int ParseCount()
            {
                var start = _position;
                while (_position < _end && _pattern[_position] >= '0' && _pattern[_position] <= '9') { Step(); _position++; }
                if (start == _position) throw Error("量词次数必须是非负整数。");

                int count;
                if (!int.TryParse(_pattern.Substring(start, _position - start), NumberStyles.None, CultureInfo.InvariantCulture, out count))
                    throw Error("量词次数超出支持的整数范围。");
                return count;
            }

            private RegexNode ParseCharacterClass()
            {
                Step();
                _position++;
                var negate = At('^');
                if (negate) { Step(); _position++; }
                var builder = new CharacterSetBuilder(this);
                var hasElement = false;

                while (_position < _end && !At(']'))
                {
                    var isFirst = !hasElement;
                    var element = ParseClassElement();
                    hasElement = true;

                    if (element.IsLiteral && element.Literal == '-' && !element.IsEscaped && !isFirst &&
                        _position < _end && !At(']'))
                        throw Error("字符类中的连字符必须位于首尾或构成有效范围。");

                    if (element.IsLiteral && element.Literal != '-' && _position < _end && At('-') &&
                        _position + 1 < _end && _pattern[_position + 1] != ']')
                    {
                        Step();
                        _position++;
                        var end = ParseClassElement();
                        if (!end.IsLiteral) throw Error("字符范围的端点必须是单个字符。");
                        builder.AddRange(element.Literal, end.Literal);
                    }
                    else
                    {
                        builder.Add(element);
                    }
                }

                if (!At(']')) throw Error("字符类缺少右方括号。");
                Step();
                _position++;
                if (!hasElement) throw Error("字符类不能为空。");
                return CharacterClass(builder.Build(negate));
            }

            private CharacterClassElement ParseClassElement()
            {
                Step();
                if (_position >= _end) throw Error("字符类缺少右方括号。");
                if (At('\\'))
                {
                    _position++;
                    var escaped = ParseEscape(true);
                    return new CharacterClassElement(escaped.IsLiteral, escaped.Literal, escaped.Characters, true);
                }

                var value = _pattern[_position++];
                if (char.IsSurrogate(value)) throw Error("字符类不能输出 UTF-16 代理项。");
                return new CharacterClassElement(true, value, null, false);
            }

            private EscapedElement ParseEscape(bool inCharacterClass)
            {
                Step();
                if (_position >= _end) throw Error("反斜线后缺少转义字符。");
                var escaped = _pattern[_position++];
                switch (escaped)
                {
                    case 'd': return EscapedElement.Set(CharacterSets.Digits);
                    case 'D': return EscapedElement.Set(CharacterSets.NonDigits);
                    case 'w': return EscapedElement.Set(CharacterSets.Word);
                    case 'W': return EscapedElement.Set(CharacterSets.NonWord);
                    case 's': return EscapedElement.Set(CharacterSets.WhiteSpace);
                    case 'S': return EscapedElement.Set(CharacterSets.NonWhiteSpace);
                    case 'n': return EscapedElement.Character('\n');
                    case 'r': return EscapedElement.Character('\r');
                    case 't': return EscapedElement.Character('\t');
                    case 'f': return EscapedElement.Character('\f');
                    case 'v': return EscapedElement.Character('\v');
                    case 'a': return EscapedElement.Character('\a');
                    case 'e': return EscapedElement.Character('\u001b');
                    case 'b':
                        if (inCharacterClass) return EscapedElement.Character('\b');
                        throw Error("不支持零宽单词边界断言 \\b。");
                    case '0':
                        if (_position < _end && char.IsDigit(_pattern[_position]))
                            throw Error("不支持八进制转义。");
                        return EscapedElement.Character('\0');
                    case 'c': return EscapedElement.Character(ParseControlEscape());
                    case 'x': return EscapedElement.Character(ParseHexEscape(2, 2));
                    case 'u': return EscapedElement.Character(ParseHexEscape(4, 4));
                    case 'A': case 'z': case 'Z': case 'B':
                        throw Error("锚点或边界转义只支持整个模式两端的 ^、$、\\A 和 \\z。");
                    default:
                        if (char.IsLetterOrDigit(escaped)) throw Error("不支持转义 \\" + escaped + "。");
                        return EscapedElement.Character(escaped);
                }
            }

            private char ParseControlEscape()
            {
                Step();
                if (_position >= _end) throw Error("\\c 后必须跟一个 ASCII 字母。");
                var value = _pattern[_position++];
                if (!((value >= 'a' && value <= 'z') || (value >= 'A' && value <= 'Z')))
                    throw Error("\\c 后必须跟一个 ASCII 字母。");
                return (char)(char.ToUpperInvariant(value) & 0x1f);
            }

            private char ParseHexEscape(int minimumDigits, int maximumDigits)
            {
                Step();
                var value = 0;
                var digits = 0;
                while (_position < _end && digits < maximumDigits)
                {
                    Step();
                    var digit = HexValue(_pattern[_position]);
                    if (digit < 0) break;
                    value = value * 16 + digit;
                    _position++;
                    digits++;
                }
                if (digits < minimumDigits) throw Error("十六进制转义的位数不足。");
                var result = (char)value;
                if (char.IsSurrogate(result)) throw Error("转义不能生成孤立的 UTF-16 代理项。");
                return result;
            }

            private bool At(char value) => _position < _end && _pattern[_position] == value;

            private RegexNode CharacterClass(CharacterSet characters)
            {
                _characterIntervalCount += characters.IntervalCount;
                return new CharacterClassNode(characters);
            }

            private ArgumentException Error(string message) => new ArgumentException(message, "pattern");

            private void StripOuterAnchors()
            {
                while (_position < _end)
                {
                    Step();
                    if (_pattern[_position] == '^')
                    {
                        _position++;
                    }
                    else if (_position + 1 < _end && _pattern[_position] == '\\' && _pattern[_position + 1] == 'A')
                    {
                        _position += 2;
                    }
                    else break;
                }

                while (_end > _position)
                {
                    Step();
                    if (_pattern[_end - 1] == '$' && !IsEscapedAt(_end - 1)) { _end--; continue; }
                    if (HasUnescapedTrailingEscape('z')) { _end -= 2; continue; }
                    break;
                }
            }

            private bool HasUnescapedTrailingEscape(char escaped)
            {
                if (_end - _position < 2 || _pattern[_end - 1] != escaped || _pattern[_end - 2] != '\\') return false;
                var slashCount = 0;
                for (var index = _end - 2; index >= _position && _pattern[index] == '\\'; index--) { Step(); slashCount++; }
                return slashCount % 2 == 1;
            }

            private bool IsEscapedAt(int index)
            {
                var slashCount = 0;
                for (var cursor = index - 1; cursor >= _position && _pattern[cursor] == '\\'; cursor--) { Step(); slashCount++; }
                return slashCount % 2 == 1;
            }

            private static int HexValue(char value)
            {
                if (value >= '0' && value <= '9') return value - '0';
                if (value >= 'a' && value <= 'f') return value - 'a' + 10;
                if (value >= 'A' && value <= 'F') return value - 'A' + 10;
                return -1;
            }
        }

        private readonly struct CharacterRange
        {
            public CharacterRange(int start, int end) { Start = start; End = end; }
            public int Start { get; }
            public int End { get; }
            public int Count => End - Start + 1;
        }

        private sealed class CharacterSet
        {
            private readonly CharacterRange[] _ranges;
            private readonly int[] _cumulativeCounts;
            private readonly int _count;

            public CharacterSet(CharacterRange[] ranges)
            {
                _ranges = ranges;
                _cumulativeCounts = new int[ranges.Length];
                var count = 0;
                for (var index = 0; index < ranges.Length; index++)
                {
                    count += ranges[index].Count;
                    _cumulativeCounts[index] = count;
                }
                _count = count;
            }

            public int Count => _count;
            public int IntervalCount => _ranges.Length;
            public CharacterRange[] Ranges => _ranges;

            public char Pick(Random random)
            {
                var index = random.Next(_count);
                var low = 0;
                var high = _cumulativeCounts.Length - 1;
                while (low < high)
                {
                    var middle = low + (high - low) / 2;
                    if (index < _cumulativeCounts[middle]) high = middle;
                    else low = middle + 1;
                }
                var precedingCount = low == 0 ? 0 : _cumulativeCounts[low - 1];
                return (char)(_ranges[low].Start + index - precedingCount);
            }
        }

        private sealed class CharacterSetBuilder
        {
            private const int HighSurrogateStart = 0xD800;
            private const int LowSurrogateEnd = 0xDFFF;
            private readonly Parser _parser;
            private readonly List<CharacterRange> _ranges = new List<CharacterRange>();

            public CharacterSetBuilder(Parser parser) { _parser = parser; }

            public void Add(CharacterClassElement element)
            {
                if (element.IsLiteral) AddCharacter(element.Literal);
                else foreach (var range in element.Characters.Ranges) InsertRange(range.Start, range.End);
            }

            public void AddRange(char first, char last)
            {
                if (last < first) throw new ArgumentException("字符范围的结束值不能小于起始值。", "pattern");
                var start = (int)first;
                var end = (int)last;
                if (end < HighSurrogateStart || start > LowSurrogateEnd)
                {
                    InsertRange(start, end);
                    return;
                }
                if (start < HighSurrogateStart) InsertRange(start, HighSurrogateStart - 1);
                if (end > LowSurrogateEnd) InsertRange(LowSurrogateEnd + 1, end);
            }

            public CharacterSet Build(bool negate)
            {
                _parser.Step(_ranges.Count);
                var values = new CharacterSet(_ranges.ToArray());
                if (negate) values = CharacterSets.Complement(CharacterSets.Universe, values, _parser);
                if (values.Count == 0) throw new ArgumentException("字符类不包含可生成的非代理字符。", "pattern");
                return values;
            }

            private void AddCharacter(char value)
            {
                if (!char.IsSurrogate(value)) InsertRange(value, value);
            }

            private void InsertRange(int start, int end)
            {
                _parser.Step();
                var index = 0;
                while (index < _ranges.Count)
                {
                    _parser.Step();
                    if (_ranges[index].End + 1 >= start) break;
                    index++;
                }

                while (index < _ranges.Count)
                {
                    _parser.Step();
                    var current = _ranges[index];
                    if (current.Start > end + 1) break;
                    start = Math.Min(start, current.Start);
                    end = Math.Max(end, current.End);
                    _parser.Step(_ranges.Count - index - 1);
                    _ranges.RemoveAt(index);
                }

                _parser.Step(_ranges.Count - index);
                _ranges.Insert(index, new CharacterRange(start, end));
            }
        }

        private sealed class CharacterClassElement
        {
            public CharacterClassElement(bool isLiteral, char literal, CharacterSet characters, bool isEscaped)
            {
                IsLiteral = isLiteral;
                Literal = literal;
                Characters = characters;
                IsEscaped = isEscaped;
            }
            public bool IsLiteral { get; }
            public char Literal { get; }
            public CharacterSet Characters { get; }
            public bool IsEscaped { get; }
        }

        private sealed class EscapedElement
        {
            private EscapedElement(bool isLiteral, char literal, CharacterSet characters)
            {
                IsLiteral = isLiteral;
                Literal = literal;
                Characters = characters;
            }
            public bool IsLiteral { get; }
            public char Literal { get; }
            public CharacterSet Characters { get; }
            public static EscapedElement Character(char value) => new EscapedElement(true, value, null);
            public static EscapedElement Set(CharacterSet values) => new EscapedElement(false, default(char), values);
        }

        private static class CharacterSets
        {
            public static readonly CharacterSet Universe = Build(_ => true);
            public static readonly CharacterSet Digits = Build(character => char.GetUnicodeCategory(character) == UnicodeCategory.DecimalDigitNumber);
            public static readonly CharacterSet NonDigits = Complement(Universe, Digits, null);
            public static readonly CharacterSet Word = Build(IsWordCharacter);
            public static readonly CharacterSet NonWord = Complement(Universe, Word, null);
            public static readonly CharacterSet WhiteSpace = Build(char.IsWhiteSpace);
            public static readonly CharacterSet NonWhiteSpace = Complement(Universe, WhiteSpace, null);
            public static readonly CharacterSet Dot = Build(character => character != '\n');

            private static CharacterSet Build(Func<char, bool> include)
            {
                var ranges = new List<CharacterRange>();
                var start = -1;
                var end = -1;
                for (var value = 0; value <= char.MaxValue; value++)
                {
                    var character = (char)value;
                    if (!char.IsSurrogate(character) && include(character))
                    {
                        if (start < 0) start = value;
                        end = value;
                    }
                    else if (start >= 0)
                    {
                        ranges.Add(new CharacterRange(start, end));
                        start = -1;
                    }
                }
                if (start >= 0) ranges.Add(new CharacterRange(start, end));
                return new CharacterSet(ranges.ToArray());
            }

            public static CharacterSet Complement(CharacterSet universe, CharacterSet excluded, Parser parser)
            {
                var ranges = new List<CharacterRange>();
                foreach (var allowed in universe.Ranges)
                {
                    var cursor = allowed.Start;
                    foreach (var denied in excluded.Ranges)
                    {
                        parser?.Step();
                        if (denied.End < cursor) continue;
                        if (denied.Start > allowed.End) break;
                        if (denied.Start > cursor) ranges.Add(new CharacterRange(cursor, Math.Min(allowed.End, denied.Start - 1)));
                        cursor = Math.Max(cursor, denied.End + 1);
                        if (cursor > allowed.End) break;
                    }
                    if (cursor <= allowed.End) ranges.Add(new CharacterRange(cursor, allowed.End));
                }
                parser?.Step(ranges.Count);
                return new CharacterSet(ranges.ToArray());
            }

            private static bool IsWordCharacter(char character)
            {
                switch (char.GetUnicodeCategory(character))
                {
                    case UnicodeCategory.UppercaseLetter:
                    case UnicodeCategory.LowercaseLetter:
                    case UnicodeCategory.TitlecaseLetter:
                    case UnicodeCategory.ModifierLetter:
                    case UnicodeCategory.OtherLetter:
                    case UnicodeCategory.NonSpacingMark:
                    case UnicodeCategory.DecimalDigitNumber:
                    case UnicodeCategory.ConnectorPunctuation:
                        return true;
                    default:
                        return false;
                }
            }
        }

        private static long SumMinimum(IList<RegexNode> nodes) => nodes.Aggregate(0L, (length, node) => Add(length, node.MinimumLength));
        private static long SumMaximum(IList<RegexNode> nodes) => nodes.Aggregate(0L, (length, node) => Add(length, node.MaximumLength));
        private static long Minimum(IList<RegexNode> nodes) => nodes.Min(node => node.MinimumLength);
        private static long Maximum(IList<RegexNode> nodes) => nodes.Max(node => node.MaximumLength);
        private static long SumMinimum(IList<RegexNode> nodes, bool codePoints) => nodes.Aggregate(0L, (length, node) => Add(length, codePoints ? node.MinimumCodePointLength : node.MinimumLength));
        private static long SumMaximum(IList<RegexNode> nodes, bool codePoints) => nodes.Aggregate(0L, (length, node) => Add(length, codePoints ? node.MaximumCodePointLength : node.MaximumLength));
        private static long Minimum(IList<RegexNode> nodes, bool codePoints) => nodes.Min(node => codePoints ? node.MinimumCodePointLength : node.MinimumLength);
        private static long Maximum(IList<RegexNode> nodes, bool codePoints) => nodes.Max(node => codePoints ? node.MaximumCodePointLength : node.MaximumLength);
        private static long Add(long first, long second) => first > long.MaxValue - second ? long.MaxValue : first + second;
        private static long Multiply(long length, int count) => length == 0 || count == 0 ? 0 : length > long.MaxValue / count ? long.MaxValue : length * count;

        private static int CountCodePoints(StringBuilder value, int start, int length)
        {
            var count = length;
            var end = start + length;
            for (var index = start; index < end; index++)
            {
                if (char.IsHighSurrogate(value[index]) && index + 1 < end && char.IsLowSurrogate(value[index + 1]))
                {
                    count--;
                    index++;
                }
            }
            return count;
        }

        private static int NextInclusive(Random random, int minimum, int maximum)
        {
            if (maximum < int.MaxValue) return random.Next(minimum, maximum + 1);
            var bytes = new byte[4];
            random.NextBytes(bytes);
            var value = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
            var range = (uint)((long)maximum - minimum + 1);
            return minimum + (int)(value % range);
        }
    }
}
