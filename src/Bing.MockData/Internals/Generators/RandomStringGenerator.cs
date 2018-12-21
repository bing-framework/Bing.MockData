using System;
using System.Collections.Generic;
using System.Linq;

namespace Bing.MockData.Internals.Generators
{
    /// <summary>
    /// 随机字符串生成器
    /// </summary>
    internal class RandomStringGenerator
    {
        /// <summary>
        /// 是否使用大写字符
        /// </summary>
        private bool _useUpperCaseCharacters;

        /// <summary>
        /// 是否使用小写字符
        /// </summary>
        private bool _useLowerCaseCharacters;

        /// <summary>
        /// 是否使用数字字符
        /// </summary>
        private bool _useNumericCharacters;

        /// <summary>
        /// 是否使用特殊字符
        /// </summary>
        private bool _useSpecialCharacters;

        /// <summary>
        /// 是否使用空白字符
        /// </summary>
        private bool _useSpaceCharacters;

        /// <summary>
        /// 驱动模式
        /// </summary>
        private bool _patternDriven;

        /// <summary>
        /// 当前大写字符列表
        /// </summary>
        private char[] _currentUpperCaseCharacters;

        /// <summary>
        /// 当前小写字符列表
        /// </summary>
        private char[] _currentLowerCaseCharacters;

        /// <summary>
        /// 当前数字字符
        /// </summary>
        private char[] _currentNumericCharacters;

        /// <summary>
        /// 当前特殊字符
        /// </summary>
        private char[] _currentSpecialCharacters;

        /// <summary>
        /// 当前空白字符
        /// </summary>
        private char[] _currentSpaceCharacters;

        /// <summary>
        /// 当前所有使用的字符
        /// </summary>
        private char[] _currentGeneralCharacters;

        /// <summary>
        /// 已存在的字符串
        /// </summary>
        private readonly List<string> _existingStrings;

        /// <summary>
        /// 随机数
        /// </summary>
        private readonly System.Security.Cryptography.RNGCryptoServiceProvider _random;

        /// <summary>
        /// 模式
        /// </summary>
        private string _pattern;

        /// <summary>
        /// 定义生成字符串时要遵循的模式。
        /// 如果它等于空字符串，则忽略该值。
        /// 模式：
        /// L - 大写字符
        /// l - 小写字符
        /// n - 数字字符
        /// s - 特殊字符
        /// _ - 空白字符
        /// * - 任意字符
        /// \ - 原样输出字符
        /// </summary>
        private string Pattern
        {
            get => _pattern;
            set
            {
                _patternDriven = !string.IsNullOrEmpty(value);
                _pattern = value;
            }
        }

        /// <summary>
        /// 是否可重复字符
        /// </summary>
        public bool RepeatCharacters;

        /// <summary>
        /// 是否唯一字符串
        /// </summary>
        public bool UniqueString;

        /// <summary>
        /// 是否使用大写字符
        /// </summary>
        public bool UseUpperCaseCharacters
        {
            get => _useUpperCaseCharacters;
            set
            {
                if (_currentUpperCaseCharacters != null)
                {
                    _currentGeneralCharacters = _currentGeneralCharacters.Except(_currentUpperCaseCharacters).ToArray();
                }

                if (_currentUpperCaseCharacters != null && value)
                {
                    _currentGeneralCharacters = _currentGeneralCharacters.Concat(_currentUpperCaseCharacters).ToArray();
                }

                _useUpperCaseCharacters = value;
            }
        }

        /// <summary>
        /// 大写字符列表
        /// </summary>
        public char[] UpperCaseCharacters
        {
            get => _currentUpperCaseCharacters;
            set
            {
                if (UseUpperCaseCharacters)
                {
                    if (_currentUpperCaseCharacters != null)
                    {
                        _currentGeneralCharacters =
                            _currentGeneralCharacters.Except(_currentUpperCaseCharacters).ToArray();
                    }

                    _currentGeneralCharacters = _currentGeneralCharacters.Concat(value).ToArray();
                }

                _currentUpperCaseCharacters = value;
            }
        }

        /// <summary>
        /// 是否使用小写字符
        /// </summary>
        public bool UseLowerCaseCharacters
        {
            get => _useLowerCaseCharacters;
            set
            {
                if (_currentUpperCaseCharacters != null)
                {
                    _currentGeneralCharacters = _currentGeneralCharacters.Except(_currentLowerCaseCharacters).ToArray();
                }

                if (_currentUpperCaseCharacters != null && value)
                {
                    _currentGeneralCharacters = _currentGeneralCharacters.Concat(_currentLowerCaseCharacters).ToArray();
                }

                _useLowerCaseCharacters = value;
            }
        }

        /// <summary>
        /// 小写字符列表
        /// </summary>
        public char[] LowerCaseCharacters
        {
            get => _currentLowerCaseCharacters;
            set
            {
                if (UseLowerCaseCharacters)
                {
                    if (_currentLowerCaseCharacters != null)
                    {
                        _currentGeneralCharacters =
                            _currentGeneralCharacters.Except(_currentLowerCaseCharacters).ToArray();
                    }

                    _currentGeneralCharacters = _currentGeneralCharacters.Concat(value).ToArray();
                }

                _currentLowerCaseCharacters = value;
            }
        }

        /// <summary>
        /// 是否使用数字字符
        /// </summary>
        public bool UseNumericCharacters
        {
            get => _useNumericCharacters;
            set
            {
                if (_currentNumericCharacters != null)
                {
                    _currentGeneralCharacters = _currentGeneralCharacters.Except(_currentNumericCharacters).ToArray();
                }

                if (_currentNumericCharacters != null && value)
                {
                    _currentGeneralCharacters = _currentGeneralCharacters.Concat(_currentNumericCharacters).ToArray();
                }

                _useNumericCharacters = value;
            }
        }

        /// <summary>
        /// 数字字符列表
        /// </summary>
        public char[] NumericCharacters
        {
            get => _currentNumericCharacters;
            set
            {
                if (UseNumericCharacters)
                {
                    if (_currentNumericCharacters != null)
                    {
                        _currentGeneralCharacters =
                            _currentGeneralCharacters.Except(_currentNumericCharacters).ToArray();
                    }

                    _currentGeneralCharacters = _currentGeneralCharacters.Concat(value).ToArray();
                }

                _currentNumericCharacters = value;
            }
        }

        /// <summary>
        /// 是否使用空白字符
        /// </summary>
        public bool UseSpaceCharacters
        {
            get => _useSpaceCharacters;
            set
            {
                if (_currentSpaceCharacters != null)
                {
                    _currentGeneralCharacters = _currentGeneralCharacters.Except(_currentSpaceCharacters).ToArray();
                }

                if (_currentSpaceCharacters != null && value)
                {
                    _currentGeneralCharacters = _currentGeneralCharacters.Concat(_currentSpaceCharacters).ToArray();
                }

                _useSpaceCharacters = value;
            }
        }

        /// <summary>
        /// 空白字符列表
        /// </summary>
        public char[] SpaceCharacters
        {
            get => _currentSpaceCharacters;
            set
            {
                if (UseSpaceCharacters)
                {
                    if (_currentSpaceCharacters != null)
                    {
                        _currentGeneralCharacters = _currentGeneralCharacters.Except(_currentSpaceCharacters).ToArray();
                    }

                    _currentGeneralCharacters = _currentGeneralCharacters.Concat(value).ToArray();
                }

                _currentSpaceCharacters = value;
            }
        }

        /// <summary>
        /// 是否使用特殊字符
        /// </summary>
        public bool UseSpecialCharacters
        {
            get => _useSpecialCharacters;
            set
            {
                if (_currentSpecialCharacters != null)
                {
                    _currentGeneralCharacters = _currentGeneralCharacters.Except(_currentSpecialCharacters).ToArray();
                }

                if (_currentSpecialCharacters != null && value)
                {
                    _currentGeneralCharacters = _currentGeneralCharacters.Concat(_currentSpecialCharacters).ToArray();
                }

                _useSpecialCharacters = value;
            }
        }

        /// <summary>
        /// 特殊字符列表
        /// </summary>
        public char[] SpecialCharacters
        {
            get => _currentSpecialCharacters;
            set
            {
                if (UseSpecialCharacters)
                {
                    if (_currentSpecialCharacters != null)
                    {
                        _currentGeneralCharacters =
                            _currentGeneralCharacters.Except(_currentSpecialCharacters).ToArray();
                    }

                    _currentGeneralCharacters = _currentGeneralCharacters.Concat(value).ToArray();
                }

                _currentSpecialCharacters = value;
            }
        }

        /// <summary>
        /// 最少大写字符数量
        /// </summary>
        public int MinUpperCaseCharacters { get; set; }

        /// <summary>
        /// 最少小写字符数量
        /// </summary>
        public int MinLowerCaseCharacters { get; set; }

        /// <summary>
        /// 最少数字字符数量
        /// </summary>
        public int MinNumericCharacters { get; set; }

        /// <summary>
        /// 最少特殊字符数量
        /// </summary>
        public int MinSpecialCharacters { get; set; }

        /// <summary>
        /// 最少空白字符数量
        /// </summary>
        public int MinSpaceCharacters { get; set; }

        /// <summary>
        /// 初始化一个<see cref="RandomStringGenerator"/>类型的实例
        /// </summary>
        /// <param name="useUpperCaseCharacters">是否使用大写字符</param>
        /// <param name="useLowerCaseCharacters">是否使用小写字符</param>
        /// <param name="useNumericCharacters">是否使用数字字符</param>
        /// <param name="useSpecialCharacters">是否使用特殊字符</param>
        /// <param name="useSpaceCharacters">是否使用空白字符</param>
        public RandomStringGenerator(bool useUpperCaseCharacters = true
            , bool useLowerCaseCharacters = true
            , bool useNumericCharacters = true
            , bool useSpecialCharacters = true
            , bool useSpaceCharacters = true)
        {
            _useUpperCaseCharacters = useUpperCaseCharacters;
            _useLowerCaseCharacters = useLowerCaseCharacters;
            _useNumericCharacters = useNumericCharacters;
            _useSpecialCharacters = useSpecialCharacters;
            _useSpaceCharacters = useSpaceCharacters;

            _currentGeneralCharacters = new char[0];

            UpperCaseCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
            LowerCaseCharacters = "abcdefghijklmnopqrstuvwxyz".ToCharArray();
            NumericCharacters = "0123456789".ToCharArray();
            SpecialCharacters = ",.;:?!/@#$%^&()=+*-_{}[]<>|~".ToCharArray();
            _currentSpaceCharacters = " ".ToCharArray();

            MinUpperCaseCharacters = MinLowerCaseCharacters =
                MinNumericCharacters = MinSpecialCharacters = MinSpaceCharacters = 0;
            RepeatCharacters = true;
            _patternDriven = false;
            Pattern = string.Empty;
            _existingStrings = new List<string>();
            _random = new System.Security.Cryptography.RNGCryptoServiceProvider();
        }

        /// <summary>
        /// 生成字符串，按照指定模式生成。
        /// 定义生成字符串时要遵循的模式。
        /// 如果它等于空字符串，则忽略该值。
        /// 模式：
        /// L - 大写字符
        /// l - 小写字符
        /// n - 数字字符
        /// s - 特殊字符
        /// _ - 空白字符
        /// * - 任意字符
        /// \ - 原样输出字符
        /// </summary>
        /// <param name="pattern">生成模板</param>
        /// <returns></returns>
        public string Generate(string pattern)
        {
            Pattern = pattern;
            string res = GenerateString(pattern.Length);
            Pattern = string.Empty;
            return res;
        }

        /// <summary>
        /// 生成一个指定范围的可变长度的字符串
        /// </summary>
        /// <param name="minLength">最小字符串长度</param>
        /// <param name="maxLength">最大字符串长度</param>
        /// <returns></returns>
        public string Generate(int minLength, int maxLength)
        {
            if (minLength < 0)
            {
                throw new ArgumentException("最小长度应大于0");
            }

            if (maxLength < minLength)
            {
                throw new ArgumentException("最大长度应该大于等于最小长度");
            }

            if (minLength == maxLength)
            {
                return GenerateString(minLength);
            }

            int length = minLength + (GetRandomInt() % (maxLength - minLength));
            return GenerateString(length);
        }

        /// <summary>
        /// 生成一个指定长度的字符串
        /// </summary>
        /// <param name="fixedLength">字符串长度</param>
        /// <returns></returns>
        public string Generate(int fixedLength)
        {
            if (fixedLength < 0)
            {
                throw new ArgumentException("最小长度应大于0");
            }

            return GenerateString(fixedLength);
        }

        /// <summary>
        /// 将字符串添加到历史数组以支持唯一字符串生成
        /// </summary>
        /// <param name="s">字符串</param>
        public void AddExistingString(string s)
        {
            _existingStrings.Add(s);
        }

        /// <summary>
        /// 生成字符串
        /// </summary>
        /// <param name="length">字符串长度</param>
        /// <returns></returns>
        private string GenerateString(int length)
        {
            if (length == 0)
            {
                throw new ArgumentException("生成字符串长度不能为0");
            }

            if (!UseUpperCaseCharacters && !UseLowerCaseCharacters && !UseNumericCharacters && !UseSpecialCharacters &&
                !UseSpaceCharacters)
            {
                throw new ArgumentException("应该至少使用一个字符集进行生成字符串");
            }

            if (!RepeatCharacters && (_currentGeneralCharacters.Length < length))
            {
                throw new ArgumentException("没有足够的字符来创建没有重复的字符串");
            }

            string result;
            if (_patternDriven)
            {
                result = PatternDrivenAlgo(Pattern);
            }
            else if (MinUpperCaseCharacters == 0 && MinLowerCaseCharacters == 0 && MinNumericCharacters == 0 &&
                      MinSpecialCharacters == 0 && MinSpaceCharacters == 0)
            {
                result = SimpleGenerateAlgo(length);
            }
            else
            {
                result = GenerateAlgoWithLimits(length);
            }

            if (UniqueString && _existingStrings.Contains(result))
            {
                return GenerateString(length);
            }

            AddExistingString(result);
            return result;
        }

        /// <summary>
        /// 按照模式生成随机字符串
        /// </summary>
        /// <param name="pattern">模式</param>
        /// <returns></returns>
        private string PatternDrivenAlgo(string pattern)
        {
            string result = string.Empty;
            var characters = new List<char>();
            bool bNextCharacterIsAsIs = false;
            foreach (var character in pattern)
            {
                char newChar;
                if (bNextCharacterIsAsIs)
                {
                    newChar = character;
                    bNextCharacterIsAsIs = false;
                }
                else
                {
                    switch (character)
                    {
                        case 'L':
                            newChar = GetRandomCharFromArray(_currentUpperCaseCharacters, characters);
                            break;
                        case 'l':
                            newChar = GetRandomCharFromArray(_currentLowerCaseCharacters, characters);
                            break;
                        case 'n':
                            newChar = GetRandomCharFromArray(_currentNumericCharacters, characters);
                            break;
                        case 's':
                            newChar = GetRandomCharFromArray(_currentSpecialCharacters, characters);
                            break;
                        case '_':
                            newChar = GetRandomCharFromArray(_currentSpaceCharacters, characters);
                            break;
                        case '*':
                            newChar = GetRandomCharFromArray(_currentGeneralCharacters, characters);
                            break;
                        case '\\':
                            // 下一个字符就是这样
                            bNextCharacterIsAsIs = true;
                            continue;
                        default:
                            throw new NotSupportedException($"字符 '{character}' 暂不支持");
                    }
                }
                characters.Add(newChar);
                result += newChar;
            }

            return result;
        }

        /// <summary>
        /// 简单生成一个随机字符串。该生成方式没有限制
        /// </summary>
        /// <param name="length">字符串长度</param>
        /// <returns></returns>
        private string SimpleGenerateAlgo(int length)
        {
            string result = string.Empty;
            for (var i = 0; i < length; i++)
            {
                char newChar = _currentGeneralCharacters[GetRandomInt() % _currentGeneralCharacters.Length];
                if (!RepeatCharacters && result.Contains(newChar))
                {
                    do
                    {
                        newChar= _currentGeneralCharacters[GetRandomInt() % _currentGeneralCharacters.Length];
                    } while (result.Contains(newChar));
                }

                result += newChar;
            }

            return result;
        }

        /// <summary>
        /// 生成一个随机字符串，其中包含每个字符集指定的最小字符数
        /// </summary>
        /// <param name="length">字符串长度</param>
        /// <returns></returns>
        private string GenerateAlgoWithLimits(int length)
        {
            if (MinUpperCaseCharacters + MinLowerCaseCharacters + MinNumericCharacters + MinSpecialCharacters +
                MinSpaceCharacters > length)
            {
                throw new ArgumentException(
                    $"SUM(MinUpperCaseCharacters,MinLowerCaseCharacters,MinNumericCharacters,MinSpecialCharacters) 大于等于 {length}");
            }

            if (!RepeatCharacters && (MinUpperCaseCharacters > _currentUpperCaseCharacters.Length))
            {
                throw new ArgumentException($"无法生成字符串，数据源字符长度小于 MinUpperCaseCharacters");
            }
            if (!RepeatCharacters && (MinLowerCaseCharacters > _currentLowerCaseCharacters.Length))
            {
                throw new ArgumentException($"无法生成字符串，数据源字符长度小于 MinLowerCaseCharacters");
            }
            if (!RepeatCharacters && (MinNumericCharacters > _currentNumericCharacters.Length))
            {
                throw new ArgumentException($"无法生成字符串，数据源字符长度小于 MinNumericCharacters");
            }
            if (!RepeatCharacters && (MinSpaceCharacters > _currentSpaceCharacters.Length))
            {
                throw new ArgumentException($"无法生成字符串，数据源字符长度小于 MinSpaceCharacters");
            }
            if (!RepeatCharacters && (MinSpecialCharacters > _currentSpecialCharacters.Length))
            {
                throw new ArgumentException($"无法生成字符串，数据源字符长度小于 MinSpecialCharacters");
            }

            int allowedNumberOfGeneralCharacters = length - MinUpperCaseCharacters - MinLowerCaseCharacters -
                                                   MinNumericCharacters - MinSpecialCharacters - MinSpaceCharacters;
            string result = string.Empty;
            var characters = new List<char>();

            for (var i = 0; i < MinUpperCaseCharacters; i++)
            {
                characters.Add(GetRandomCharFromArray(UpperCaseCharacters, characters));
            }

            for (var i = 0; i < MinLowerCaseCharacters; i++)
            {
                characters.Add(GetRandomCharFromArray(LowerCaseCharacters, characters));
            }

            for (var i = 0; i < MinNumericCharacters; i++)
            {
                characters.Add(GetRandomCharFromArray(NumericCharacters, characters));
            }

            for (var i = 0; i < MinSpecialCharacters; i++)
            {
                characters.Add(GetRandomCharFromArray(SpecialCharacters, characters));
            }

            for (var i = 0; i < MinSpaceCharacters; i++)
            {
                characters.Add(GetRandomCharFromArray(SpaceCharacters, characters));
            }

            for (var i = 0; i < allowedNumberOfGeneralCharacters; i++)
            {
                characters.Add(GetRandomCharFromArray(_currentGeneralCharacters, characters));
            }

            for (int i = 0; i < length; i++)
            {
                int position = GetRandomInt() % characters.Count;
                char currentChar = characters[position];
                characters.RemoveAt(position);
                result += currentChar;
            }

            return result;
        }

        /// <summary>
        /// 随机生成16位整数
        /// </summary>
        /// <returns></returns>
        private int GetRandomInt()
        {
            byte[] buffer=new byte[2];// 16 bit = 2^16 = 65576
            _random.GetNonZeroBytes(buffer);
            int index = BitConverter.ToInt16(buffer, 0);
            if (index < 0)
            {
                index = -index;
            }

            return index;
        }

        /// <summary>
        /// 从所选的字符数组中获取随机字符
        /// </summary>
        /// <param name="array">字符数组</param>
        /// <param name="existenItems">已存在字符列表</param>
        /// <returns></returns>
        private char GetRandomCharFromArray(char[] array, List<char> existenItems)
        {
            char character;
            do
            {
                character = array[GetRandomInt() % array.Length];
            } while (!RepeatCharacters && existenItems.Contains(character));

            return character;
        }
    }
}
