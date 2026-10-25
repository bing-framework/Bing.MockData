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

        public BoundedRegexGenerator(string pattern, int maxTextLength, bool constrainOutput = false)
        {
            if (string.IsNullOrEmpty(pattern)) throw new ArgumentException("正则模式不能为空。", nameof(pattern));
            if (maxTextLength < 0) throw new ArgumentOutOfRangeException(nameof(maxTextLength));

            // 模式与生成步骤都受预算约束；空分支或深层分组不能绕过输出长度限制。
            long maximumSteps = Math.Max(1024L, (long)maxTextLength * 16);
            if (pattern.Length > maximumSteps) throw new MockLimitException("RegexGenerationSteps", maximumSteps);
            try
            {
                _ = new Regex(pattern);
            }
            catch (ArgumentException exception)
            {
                throw new ArgumentException("模式不是有效的 .NET 正则表达式。", nameof(pattern), exception);
            }

            _root = new Parser(pattern).Parse();
            if (!constrainOutput && _root.MaximumLength > maxTextLength)
                throw new MockLimitException(nameof(FieldOptionsBase.MaxTextLength), maxTextLength);
            if (!constrainOutput && _root.MaximumSteps > maximumSteps) throw new MockLimitException("RegexGenerationSteps", maximumSteps);
            _maxTextLength = maxTextLength;
        }

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
        internal string Generate(Random random, CancellationToken cancellationToken, int minimum, int maximum, int attempts)
        {
            if (minimum < 0 || maximum < minimum || maximum > _maxTextLength) throw new ArgumentOutOfRangeException(nameof(minimum));
            long steps = Math.Max(1024L, (long)_maxTextLength * 16);
            var output = new StringBuilder(Math.Min(maximum, 128));
            void Step() { cancellationToken.ThrowIfCancellationRequested(); if (--steps < 0) throw new MockLimitException("RegexGenerationSteps", Math.Max(1024L, (long)_maxTextLength * 16)); }
            bool Emit(RegexNode node, long low, long high)
            {
                Step(); low = Math.Max(low, node.MinimumLength); high = Math.Min(high, node.MaximumLength);
                if (low > high) return false;
                if (node is AlternationNode alternative)
                {
                    var possible = alternative.Nodes.Where(x => x.MinimumLength <= high && x.MaximumLength >= low).ToArray();
                    if (possible.Length == 0) return false;
                    return Emit(possible[random.Next(possible.Length)], low, high);
                }
                if (node is SequenceNode sequence) return EmitSequence(sequence.Nodes, low, high);
                if (node is RepeatNode repeat)
                {
                    if (repeat.Node.MaximumLength == 0) return low == 0;
                    long first = Math.Max(repeat.MinimumCount, low / repeat.Node.MaximumLength + (low % repeat.Node.MaximumLength == 0 ? 0 : 1));
                    long last = Math.Min(repeat.MaximumCount, repeat.Node.MinimumLength > 0 ? high / repeat.Node.MinimumLength : Math.Max(first, high));
                    // 零长度节点无需额外重复；过多的空操作同样受步骤预算限制。
                    if (first > last || first > steps) return false;
                    last = Math.Min(last, steps);
                    int count = NextInclusive(random, (int)first, (int)last);
                    return EmitRepeated(repeat.Node, count, low, high);
                }
                node.AppendTo(output, random, cancellationToken); return true;
            }
            bool EmitSequence(RegexNode[] nodes, long low, long high)
            {
                long remainingMin = SumMinimum(nodes), remainingMax = SumMaximum(nodes); int start = output.Length;
                foreach (var child in nodes)
                {
                    remainingMin -= child.MinimumLength; remainingMax -= child.MaximumLength;
                    long used = output.Length - start;
                    if (!Emit(child, Math.Max(0, low - used - remainingMax), high - used - remainingMin)) return false;
                }
                return output.Length - start >= low && output.Length - start <= high;
            }
            bool EmitRepeated(RegexNode node, int count, long low, long high)
            {
                int start = output.Length;
                for (int i = 0; i < count; i++)
                {
                    long used = output.Length - start, remaining = count - i - 1;
                    if (!Emit(node, Math.Max(0, low - used - Multiply(node.MaximumLength, (int)remaining)), high - used - Multiply(node.MinimumLength, (int)remaining))) return false;
                }
                return output.Length - start >= low && output.Length - start <= high;
            }
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                output.Clear(); if (Emit(_root, minimum, maximum)) return output.ToString();
            }
            throw new InvalidOperationException("在预算内无法构造同时满足正则与长度约束的文本。");
        }

        private abstract class RegexNode
        {
            protected RegexNode(long minimumLength, long maximumLength, long maximumSteps = 1)
            {
                MinimumLength = minimumLength;
                MaximumLength = maximumLength;
                MaximumSteps = maximumSteps;
            }

            public long MinimumLength { get; }
            public long MaximumLength { get; }
            public long MaximumSteps { get; }
            public abstract void AppendTo(StringBuilder builder, Random random, CancellationToken cancellationToken);
        }

        private sealed class EmptyNode : RegexNode
        {
            public EmptyNode() : base(0, 0) { }
            public override void AppendTo(StringBuilder builder, Random random, CancellationToken cancellationToken) => cancellationToken.ThrowIfCancellationRequested();
        }

        private sealed class LiteralNode : RegexNode
        {
            private readonly string _value;
            public LiteralNode(string value) : base(value.Length, value.Length) { _value = value; }
            public override void AppendTo(StringBuilder builder, Random random, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                builder.Append(_value);
            }
        }

        private sealed class CharacterClassNode : RegexNode
        {
            private readonly char[] _characters;
            public CharacterClassNode(char[] characters) : base(1, 1) { _characters = characters; }
            public override void AppendTo(StringBuilder builder, Random random, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                builder.Append(_characters[random.Next(_characters.Length)]);
            }
        }

        private sealed class SequenceNode : RegexNode
        {
            private readonly RegexNode[] _nodes;
            internal RegexNode[] Nodes => _nodes;
            public SequenceNode(IList<RegexNode> nodes) : base(SumMinimum(nodes), SumMaximum(nodes), nodes.Aggregate(1L, (steps, node) => Add(steps, node.MaximumSteps)))
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
            public AlternationNode(IList<RegexNode> alternatives) : base(Minimum(alternatives), Maximum(alternatives), Add(1, alternatives.Max(node => node.MaximumSteps)))
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
                : base(Multiply(node.MinimumLength, minimumCount), Multiply(node.MaximumLength, maximumCount),
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
            private int _position;
            private int _groupDepth;

            public Parser(string pattern) { _pattern = StripOuterAnchors(pattern); }

            public RegexNode Parse()
            {
                var node = ParseAlternation();
                if (_position != _pattern.Length) throw Error("括号或表达式结构不受支持。");
                return node;
            }

            private RegexNode ParseAlternation()
            {
                var alternatives = new List<RegexNode> { ParseSequence() };
                while (At('|'))
                {
                    _position++;
                    alternatives.Add(ParseSequence());
                }
                return alternatives.Count == 1 ? alternatives[0] : new AlternationNode(alternatives);
            }

            private RegexNode ParseSequence()
            {
                var nodes = new List<RegexNode>();
                while (_position < _pattern.Length && !At('|') && !At(')'))
                {
                    var node = ParseAtom();
                    if (_position < _pattern.Length)
                    {
                        if (At('?'))
                        {
                            _position++;
                            node = new RepeatNode(node, 0, 1);
                            if (At('?')) _position++;
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
                if (_position >= _pattern.Length) throw Error("量词前缺少表达式。");
                var current = _pattern[_position];
                if (current == '(') return ParseGroup();
                if (current == '[') return ParseCharacterClass();
                if (current == '.')
                {
                    _position++;
                    return new CharacterClassNode(CharacterSets.Dot);
                }
                if (current == '\\')
                {
                    _position++;
                    var element = ParseEscape(false);
                    return element.IsLiteral
                        ? (RegexNode)new LiteralNode(element.Literal.ToString())
                        : new CharacterClassNode(element.Characters);
                }
                if (current == '^' || current == '$') throw Error("锚点只支持位于整个模式两端。");
                if (current == '*' || current == '+' || current == '?' || current == '{' || current == '}')
                    throw Error("量词前缺少表达式或量词格式错误。");
                if (current == ')') throw Error("存在多余的右括号。");

                var start = _position++;
                if (char.IsHighSurrogate(current))
                {
                    if (_position < _pattern.Length && char.IsLowSurrogate(_pattern[_position]))
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
                _position++;
                if (maximum < minimum) throw Error("量词最大次数不能小于最小次数。");
                return new RepeatNode(node, minimum, maximum);
            }

            private int ParseCount()
            {
                var start = _position;
                while (_position < _pattern.Length && _pattern[_position] >= '0' && _pattern[_position] <= '9') _position++;
                if (start == _position) throw Error("量词次数必须是非负整数。");

                int count;
                if (!int.TryParse(_pattern.Substring(start, _position - start), NumberStyles.None, CultureInfo.InvariantCulture, out count))
                    throw Error("量词次数超出支持的整数范围。");
                return count;
            }

            private RegexNode ParseCharacterClass()
            {
                _position++;
                var negate = At('^');
                if (negate) _position++;
                var builder = new CharacterSetBuilder();
                var hasElement = false;

                while (_position < _pattern.Length && !At(']'))
                {
                    var isFirst = !hasElement;
                    var element = ParseClassElement();
                    hasElement = true;

                    if (element.IsLiteral && element.Literal == '-' && !element.IsEscaped && !isFirst &&
                        _position < _pattern.Length && !At(']'))
                        throw Error("字符类中的连字符必须位于首尾或构成有效范围。");

                    if (element.IsLiteral && element.Literal != '-' && _position < _pattern.Length && At('-') &&
                        _position + 1 < _pattern.Length && _pattern[_position + 1] != ']')
                    {
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
                _position++;
                if (!hasElement) throw Error("字符类不能为空。");
                return new CharacterClassNode(builder.Build(negate));
            }

            private CharacterClassElement ParseClassElement()
            {
                if (_position >= _pattern.Length) throw Error("字符类缺少右方括号。");
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
                if (_position >= _pattern.Length) throw Error("反斜线后缺少转义字符。");
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
                        if (_position < _pattern.Length && char.IsDigit(_pattern[_position]))
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
                if (_position >= _pattern.Length) throw Error("\\c 后必须跟一个 ASCII 字母。");
                var value = _pattern[_position++];
                if (!((value >= 'a' && value <= 'z') || (value >= 'A' && value <= 'Z')))
                    throw Error("\\c 后必须跟一个 ASCII 字母。");
                return (char)(char.ToUpperInvariant(value) & 0x1f);
            }

            private char ParseHexEscape(int minimumDigits, int maximumDigits)
            {
                var value = 0;
                var digits = 0;
                while (_position < _pattern.Length && digits < maximumDigits)
                {
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

            private bool At(char value) => _position < _pattern.Length && _pattern[_position] == value;

            private ArgumentException Error(string message) => new ArgumentException(message, "pattern");

            private static string StripOuterAnchors(string pattern)
            {
                var value = pattern;
                var changed = true;
                while (changed)
                {
                    changed = false;
                    if (value.StartsWith("^", StringComparison.Ordinal))
                    {
                        value = value.Substring(1);
                        changed = true;
                    }
                    else if (value.StartsWith(@"\A", StringComparison.Ordinal))
                    {
                        value = value.Substring(2);
                        changed = true;
                    }

                    if (value.Length > 0 && value[value.Length - 1] == '$' && !IsEscapedAt(value, value.Length - 1))
                    {
                        value = value.Substring(0, value.Length - 1);
                        changed = true;
                    }
                    else if (HasUnescapedTrailingEscape(value, 'z'))
                    {
                        value = value.Substring(0, value.Length - 2);
                        changed = true;
                    }
                }
                return value;
            }

            private static bool HasUnescapedTrailingEscape(string value, char escaped)
            {
                if (value.Length < 2 || value[value.Length - 1] != escaped || value[value.Length - 2] != '\\') return false;
                var slashCount = 0;
                for (var index = value.Length - 2; index >= 0 && value[index] == '\\'; index--) slashCount++;
                return slashCount % 2 == 1;
            }

            private static bool IsEscapedAt(string value, int index)
            {
                var slashCount = 0;
                for (var cursor = index - 1; cursor >= 0 && value[cursor] == '\\'; cursor--) slashCount++;
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

        private sealed class CharacterSetBuilder
        {
            private readonly HashSet<char> _characters = new HashSet<char>();

            public void Add(CharacterClassElement element)
            {
                if (element.IsLiteral) AddCharacter(element.Literal);
                else foreach (var character in element.Characters) AddCharacter(character);
            }

            public void AddRange(char first, char last)
            {
                if (last < first) throw new ArgumentException("字符范围的结束值不能小于起始值。", "pattern");
                for (var value = (int)first; value <= last; value++)
                {
                    var character = (char)value;
                    if (!char.IsSurrogate(character)) _characters.Add(character);
                }
            }

            public char[] Build(bool negate)
            {
                var values = negate
                    ? CharacterSets.Universe.Where(character => !_characters.Contains(character)).ToList()
                    : _characters.ToList();
                values.Sort();
                if (values.Count == 0) throw new ArgumentException("字符类不包含可生成的非代理字符。", "pattern");
                return values.ToArray();
            }

            private void AddCharacter(char value)
            {
                if (!char.IsSurrogate(value)) _characters.Add(value);
            }
        }

        private sealed class CharacterClassElement
        {
            public CharacterClassElement(bool isLiteral, char literal, char[] characters, bool isEscaped)
            {
                IsLiteral = isLiteral;
                Literal = literal;
                Characters = characters;
                IsEscaped = isEscaped;
            }
            public bool IsLiteral { get; }
            public char Literal { get; }
            public char[] Characters { get; }
            public bool IsEscaped { get; }
        }

        private sealed class EscapedElement
        {
            private EscapedElement(bool isLiteral, char literal, char[] characters)
            {
                IsLiteral = isLiteral;
                Literal = literal;
                Characters = characters;
            }
            public bool IsLiteral { get; }
            public char Literal { get; }
            public char[] Characters { get; }
            public static EscapedElement Character(char value) => new EscapedElement(true, value, null);
            public static EscapedElement Set(char[] values) => new EscapedElement(false, default(char), values);
        }

        private static class CharacterSets
        {
            public static readonly char[] Universe = Build(_ => true);
            public static readonly char[] Digits = Build(character => char.GetUnicodeCategory(character) == UnicodeCategory.DecimalDigitNumber);
            public static readonly char[] NonDigits = Complement(Digits);
            public static readonly char[] Word = Build(IsWordCharacter);
            public static readonly char[] NonWord = Complement(Word);
            public static readonly char[] WhiteSpace = Build(char.IsWhiteSpace);
            public static readonly char[] NonWhiteSpace = Complement(WhiteSpace);
            public static readonly char[] Dot = Build(character => character != '\n');

            private static char[] Build(Func<char, bool> include)
            {
                var values = new List<char>(char.MaxValue + 1 - 2048);
                for (var value = 0; value <= char.MaxValue; value++)
                {
                    var character = (char)value;
                    if (!char.IsSurrogate(character) && include(character)) values.Add(character);
                }
                return values.ToArray();
            }

            private static char[] Complement(char[] values)
            {
                var excluded = new HashSet<char>(values);
                return Universe.Where(character => !excluded.Contains(character)).ToArray();
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
        private static long Add(long first, long second) => first > long.MaxValue - second ? long.MaxValue : first + second;
        private static long Multiply(long length, int count) => length == 0 || count == 0 ? 0 : length > long.MaxValue / count ? long.MaxValue : length * count;

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
