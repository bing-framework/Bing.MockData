using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Security;
using System.Text;

namespace Bing.MockData
{
    internal static class DomainGuard
    {
        internal static string Text(MockContext c, string value) { c.Check(); if (value.Length > c.Budget.MaxTextLength) throw new MockLimitException(nameof(MockLimits.MaxTextLength), c.Budget.MaxTextLength); return value; }
        internal static void Count(MockContext c, int n) { c.Check(); if (n < 0) throw new ArgumentOutOfRangeException(nameof(n)); if (n > c.Budget.MaxCollectionSize) throw new MockLimitException(nameof(MockLimits.MaxCollectionSize), c.Budget.MaxCollectionSize); }
        internal static void Finite(double x, string name) { if (double.IsNaN(x) || double.IsInfinity(x)) throw new ArgumentOutOfRangeException(name); }
    }

    /// <summary>结构化标识；随机标识不代表全局唯一。</summary>
    public readonly struct IdentifierData
    {
        private readonly MockContext _c;
        internal IdentifierData(MockContext c) { _c = c; }
        /// <summary>生成含显式时间或冻结参考时间的 ULID（48 位毫秒及 80 位随机数）。</summary>
        public string Ulid(DateTimeOffset? timestamp = null)
        {
            long ms = (timestamp ?? _c.ReferenceTime).ToUnixTimeMilliseconds();
            if (ms < 0 || ms > 281474976710655L) throw new ArgumentOutOfRangeException(nameof(timestamp));
            var bytes = new byte[11]; _c.WithRandom(r => { r.NextBytes(bytes); return 0; }); bytes[10] = 0;
            var value = ((BigInteger)ms << 80) | new BigInteger(bytes);
            const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
            var result = new char[26]; for (int i = 25; i >= 0; i--) { result[i] = alphabet[(int)(value & 31)]; value >>= 5; }
            return DomainGuard.Text(_c, new string(result));
        }
        /// <summary>使用指定字符集生成短标识。</summary>
        public string Short(int length = 12, string alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ") => _c.Text.Random(length, length, alphabet);
        /// <summary>按记录序号生成常量内存编号。</summary>
        public string Business(string prefix = "MOCK", int digits = 10)
        { if (prefix == null) throw new ArgumentNullException(nameof(prefix)); if (digits < 1 || digits > 99) throw new ArgumentOutOfRangeException(nameof(digits)); return DomainGuard.Text(_c, prefix + checked(_c.RecordIndex + 1).ToString("D" + digits, CultureInfo.InvariantCulture)); }
    }

    /// <summary>包含可互相转换的颜色值。</summary>
    public sealed class MockColor
    {
        public byte R { get; } public byte G { get; } public byte B { get; } public double Alpha { get; }
        public string Name { get; }
        public string Hex => $"#{R:X2}{G:X2}{B:X2}";
        public string Rgb => $"rgb({R},{G},{B})";
        public string Rgba => string.Format(CultureInfo.InvariantCulture, "rgba({0},{1},{2},{3:0.###})", R, G, B, Alpha);
        public double Hue { get; } public double Saturation { get; } public double Lightness { get; }
        public MockColor(byte r, byte g, byte b, double alpha = 1, string name = null)
        {
            NumberData.Probability(alpha); R = r; G = g; B = b; Alpha = alpha; Name = name;
            double x = r / 255d, y = g / 255d, z = b / 255d, max = Math.Max(x, Math.Max(y, z)), min = Math.Min(x, Math.Min(y, z)), d = max - min;
            Lightness = (max + min) / 2; Saturation = d == 0 ? 0 : d / (1 - Math.Abs(2 * Lightness - 1));
            Hue = d == 0 ? 0 : 60 * (max == x ? ((y - z) / d + (y < z ? 6 : 0)) : max == y ? (z - x) / d + 2 : (x - y) / d + 4);
        }
    }
    public readonly partial struct ColorData
    {
        private readonly MockContext _c; internal ColorData(MockContext c) { _c = c; }
        public MockColor Rgb(double alpha = 1) => new MockColor((byte)_c.Numbers.Int(0,255),(byte)_c.Numbers.Int(0,255),(byte)_c.Numbers.Int(0,255),alpha);
        public MockColor Named() => NamedColor();
    }
    public readonly struct ImageData
    {
        private readonly MockContext _c; internal ImageData(MockContext c) { _c = c; }
        /// <summary>生成可离线使用的 SVG 文本，不执行网络或文件 IO。</summary>
        public string Placeholder(int width = 320, int height = 180, string text = "Mock", MockColor background = null)
        {
            _c.Check(); if (width < 1 || height < 1) throw new ArgumentOutOfRangeException(nameof(width));
            text = text ?? ""; DomainGuard.Text(_c, text); System.Xml.XmlConvert.VerifyXmlChars(text);
            var color = background ?? _c.Colors.Rgb();
            return DomainGuard.Text(_c, string.Format(CultureInfo.InvariantCulture, "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{0}\" height=\"{1}\" viewBox=\"0 0 {0} {1}\"><rect width=\"100%\" height=\"100%\" fill=\"{2}\"/><text x=\"50%\" y=\"50%\" text-anchor=\"middle\" dominant-baseline=\"middle\" fill=\"{3}\">{4}</text></svg>", width, height, color.Hex, color.Lightness > .5 ? "#000000" : "#FFFFFF", SecurityElement.Escape(text)));
        }
        internal static string Initials(string text,int count)
        {
            if(string.IsNullOrEmpty(text))return "";
            var elements=StringInfo.GetTextElementEnumerator(text);var value=new StringBuilder();
            while(count-->0&&elements.MoveNext())value.Append(elements.GetTextElement());
            return value.ToString();
        }
        public string Avatar(string initials, int size = 128) => Placeholder(size, size, initials);
    }
    /// <summary>WGS84 经纬度，单位为度。</summary>
    public sealed class GeoPoint
    {
        public double Latitude { get; } public double Longitude { get; }
        public GeoPoint(double latitude, double longitude)
        { DomainGuard.Finite(latitude,nameof(latitude)); DomainGuard.Finite(longitude,nameof(longitude)); if (latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180) throw new ArgumentOutOfRangeException(nameof(latitude)); Latitude=latitude; Longitude=longitude; }
        public double DistanceMeters(GeoPoint other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            const double d = Math.PI / 180; double a = Math.Pow(Math.Sin((other.Latitude-Latitude)*d/2),2)+Math.Cos(Latitude*d)*Math.Cos(other.Latitude*d)*Math.Pow(Math.Sin((other.Longitude-Longitude)*d/2),2);
            return 6371008.8*2*Math.Asin(Math.Sqrt(Math.Min(1,a)));
        }
    }
    public readonly partial struct GeographyData
    {
        private readonly MockContext _c; internal GeographyData(MockContext c) { _c=c; }
        public GeoPoint Coordinate() => new GeoPoint(Math.Asin(_c.Numbers.Double(-1,1))*180/Math.PI, _c.Numbers.Double(-180,180));
        /// <summary>球面近似附近点；距离上限使用同一平均地球半径，不保证行政边界。</summary>
        public GeoPoint Nearby(GeoPoint center, double radiusMeters = 1000)
        {
            if (center == null) throw new ArgumentNullException(nameof(center)); DomainGuard.Finite(radiusMeters,nameof(radiusMeters)); if (radiusMeters < 0 || radiusMeters > Math.PI*6371008.8) throw new ArgumentOutOfRangeException(nameof(radiusMeters));
            double angular=Math.Sqrt(_c.Numbers.Double())*radiusMeters/6371008.8, bearing=_c.Numbers.Double(0,2*Math.PI), lat=center.Latitude*Math.PI/180, lon=center.Longitude*Math.PI/180;
            double resultLat=Math.Asin(Math.Max(-1,Math.Min(1,Math.Sin(lat)*Math.Cos(angular)+Math.Cos(lat)*Math.Sin(angular)*Math.Cos(bearing))));
            double resultLon=lon+Math.Atan2(Math.Sin(bearing)*Math.Sin(angular)*Math.Cos(lat),Math.Cos(angular)-Math.Sin(lat)*Math.Sin(resultLat));
            return new GeoPoint(resultLat*180/Math.PI, ((resultLon*180/Math.PI+540)%360)-180);
        }
        public string Building() => DomainGuard.Text(_c, _c.Numbers.Int(1,99).ToString(CultureInfo.InvariantCulture)+(_c.Locale == "zh-CN" ? "栋" : " Building"));
        public string Room() => DomainGuard.Text(_c, _c.Numbers.Int(1,50).ToString(CultureInfo.InvariantCulture)+_c.Numbers.Int(1,30).ToString("D2",CultureInfo.InvariantCulture));
    }
    public readonly struct DistributionData
    {
        private readonly MockContext _c; internal DistributionData(MockContext c) { _c=c; }
        public double Normal(double mean = 0, double standardDeviation = 1)
        { DomainGuard.Finite(mean,nameof(mean)); DomainGuard.Finite(standardDeviation,nameof(standardDeviation)); if (standardDeviation < 0) throw new ArgumentOutOfRangeException(nameof(standardDeviation)); return mean+standardDeviation*Math.Sqrt(-2*Math.Log(1-_c.WithRandom(r => r.NextDouble())))*Math.Cos(2*Math.PI*_c.WithRandom(r => r.NextDouble())); }
        public double Exponential(double rate = 1)
        { DomainGuard.Finite(rate,nameof(rate)); if (rate <= 0) throw new ArgumentOutOfRangeException(nameof(rate)); return -Math.Log(1-_c.WithRandom(r => r.NextDouble()))/rate; }
    }
    public sealed class TimeSeriesOptions
    {
        public DateTimeOffset? Start { get; set; } public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(1);
        public double Baseline { get; set; } public double Trend { get; set; } public double Amplitude { get; set; }
        public int Period { get; set; } = 24; public double Noise { get; set; } = 1;
        public double MissingProbability { get; set; } public double OutlierProbability { get; set; }
    }
    public sealed class TimeSeriesPoint
    { public long Index { get; } public DateTimeOffset Timestamp { get; } public double? Value { get; } public bool IsOutlier { get; } internal TimeSeriesPoint(long i, DateTimeOffset t, double? v, bool o) { Index=i;Timestamp=t;Value=v;IsOutlier=o; } }
    public readonly struct TimeSeriesData
    {
        private readonly MockContext _c; internal TimeSeriesData(MockContext c) { _c=c; }
        public MockGenerator<TimeSeriesPoint> Create(TimeSeriesOptions options = null)
        {
            _c.Check(); options=options ?? new TimeSeriesOptions(); var start=options.Start ?? _c.ReferenceTime; var interval=options.Interval; double baseline=options.Baseline,trend=options.Trend,amplitude=options.Amplitude,noise=options.Noise,missing=options.MissingProbability,outlier=options.OutlierProbability; int period=options.Period;
            foreach(var x in new[]{baseline,trend,amplitude,noise}) DomainGuard.Finite(x,nameof(options)); if(interval.Ticks <= 0 || period < 1 || noise < 0) throw new ArgumentOutOfRangeException(nameof(options)); NumberData.Probability(missing);NumberData.Probability(outlier);
            return _c.Generator(c => { var t=start.AddTicks(checked(c.RecordIndex*interval.Ticks)); bool absent=c.ForField("missing").Numbers.Boolean(missing), unusual=c.ForField("outlier").Numbers.Boolean(outlier); double value=baseline+trend*c.RecordIndex+amplitude*Math.Sin(2*Math.PI*(c.RecordIndex%period)/period)+c.ForField("noise").Distributions.Normal(0,noise)+(unusual ? 10*Math.Max(1,noise) : 0); DomainGuard.Finite(value,"value"); return new TimeSeriesPoint(c.RecordIndex,t,absent ? (double?)null : value,!absent&&unusual); },"timeseries");
        }
    }
    public sealed partial class MockContext
    {
        public IdentifierData Identifiers => new IdentifierData(this);
        public ColorData Colors => new ColorData(this);
        public ImageData Images => new ImageData(this);
        public GeographyData Geography => new GeographyData(this);
        public DistributionData Distributions => new DistributionData(this);
        public TimeSeriesData TimeSeries => new TimeSeriesData(this);
    }
}
