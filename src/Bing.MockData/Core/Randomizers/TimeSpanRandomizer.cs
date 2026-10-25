using System;
using System.Globalization;
using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Internals.Generators;
namespace Bing.MockData.Core.Randomizers
{
    /// <summary>闭区间时间跨度生成器。</summary>
    public class TimeSpanRandomizer : RandomizerBase<TimeSpanFieldOptions>, ITimeSpanRandomizer, IFormattedRandomizer
    {
        string IFormattedRandomizer.Format => _format;
        private readonly long _min;
        private readonly long _max;
        private readonly long _step;
        private readonly string _format;
        /// <summary>验证范围，并在需要时限定为区间内整秒。</summary>
        public TimeSpanRandomizer(TimeSpanFieldOptions options) : base(options)
        {
            RandomValueGenerator.Validate(Options.From, Options.To);
            _step = Options.IncludeMilliseconds ? 1 : TimeSpan.TicksPerSecond;
            _min = TickRange.Ceiling(Options.From.Ticks, _step);
            _max = TickRange.Floor(Options.To.Ticks, _step);
            if (_min > _max) throw new ArgumentException("区间内没有可生成的时间跨度。", nameof(options.To));
            _format = string.IsNullOrEmpty(Options.Format) ? TimeSpanFieldOptions.DefaultFormat : Options.Format;
            Options.From.ToString(_format, CultureInfo.InvariantCulture);
        }
        /// <summary>生成时间跨度。</summary>
        public TimeSpan Generate() => Execute<TimeSpan>(() => new TimeSpan(RandomValueGenerator.NextLong(Rnd, _min, _max) * _step));
    }
}
