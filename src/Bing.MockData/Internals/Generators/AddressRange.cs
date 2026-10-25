using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text.RegularExpressions;

namespace Bing.MockData.Internals.Generators
{
    internal sealed class AddressRange
    {
        private readonly BigInteger _min;
        private readonly BigInteger _width;
        private readonly int _length;
        private readonly byte[] _sample;
        private readonly byte _mask;
        public AddressRange(byte[] min, byte[] max)
        {
            _length = min.Length;
            _min = ToInteger(min);
            var last = ToInteger(max);
            if (last < _min) throw new ArgumentOutOfRangeException("Max", "最大地址不得小于最小地址。");
            _width = last - _min;
            var widthBytes = _width.ToByteArray();
            var length = widthBytes.Length;
            while (length > 1 && widthBytes[length - 1] == 0) length--;
            _sample = new byte[length + 1]; // 末尾符号位保持 0。
            var top = widthBytes[length - 1];
            var mask = 0;
            while (top > 0) { mask = (mask << 1) | 1; top >>= 1; }
            _mask = (byte)mask;
        }
        private static BigInteger ToInteger(byte[] networkBytes)
        {
            var bytes = new byte[networkBytes.Length + 1];
            for (var i = 0; i < networkBytes.Length; i++) bytes[i] = networkBytes[networkBytes.Length - 1 - i];
            return new BigInteger(bytes);
        }
        public byte[] Generate(Random random)
        {
            BigInteger offset = 0;
            if (_width != 0)
            {
                do
                {
                    random.NextBytes(_sample);
                    _sample[_sample.Length - 1] = 0;
                    _sample[_sample.Length - 2] &= _mask;
                    offset = new BigInteger(_sample);
                } while (offset > _width);
            }
            var bytes = (_min + offset).ToByteArray();
            var result = new byte[_length];
            for (var i = 0; i < Math.Min(bytes.Length, result.Length); i++) result[result.Length - 1 - i] = bytes[i];
            return result;
        }
        public static byte[] ParseIp(string value, AddressFamily family, bool maximum, string parameter)
        {
            var length = family == AddressFamily.InterNetwork ? 4 : 16;
            if (string.IsNullOrEmpty(value)) return Enumerable.Repeat(maximum ? (byte)255 : (byte)0, length).ToArray();
            if (family == AddressFamily.InterNetwork)
            {
                var parts = value.Split('.');
                if (parts.Length != 4 || parts.Any(p => !byte.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
                    throw new ArgumentException("IPv4 必须使用四段十进制表示。", parameter);
                return parts.Select(p => byte.Parse(p, NumberStyles.None, CultureInfo.InvariantCulture)).ToArray();
            }
            if (!IPAddress.TryParse(value, out var address) || address.AddressFamily != family ||
                (family == AddressFamily.InterNetworkV6 && address.ScopeId != 0))
                throw new ArgumentException("地址格式或地址族不正确；不支持作用域地址。", parameter);
            return address.GetAddressBytes();
        }
        public static byte[] ParseMac(string value, bool maximum, string parameter)
        {
            if (string.IsNullOrEmpty(value)) return Enumerable.Repeat(maximum ? (byte)255 : (byte)0, 6).ToArray();
            if (!Regex.IsMatch(value, @"\A(?:[0-9A-Fa-f]{12}|[0-9A-Fa-f]{2}(?::[0-9A-Fa-f]{2}){5}|[0-9A-Fa-f]{2}(?:-[0-9A-Fa-f]{2}){5})\z"))
                throw new ArgumentException("MAC 必须由六个十六进制字节组成。", parameter);
            var compact = value.Replace(":", "").Replace("-", "");
            return Enumerable.Range(0, 6).Select(i => byte.Parse(compact.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToArray();
        }
    }
}
