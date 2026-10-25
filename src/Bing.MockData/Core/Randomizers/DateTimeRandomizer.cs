using System;
using System.Globalization;
using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Internals.Generators;
namespace Bing.MockData.Core.Randomizers
{
    /// <summary>在闭区间内生成日期，并保留端点的 Kind。</summary>
    public class DateTimeRandomizer : RandomizerBase<DateTimeFieldOptions>, IDateTimeRandomizer, IFormattedRandomizer
    {
        string IFormattedRandomizer.Format => _format;
        private readonly long _min;
        private readonly long _max;
        private readonly long _step;
        private readonly string _format;
        /// <summary>验证日期范围及格式。</summary>
        public DateTimeRandomizer(DateTimeFieldOptions options) : base(options)
        {
            RandomValueGenerator.Validate(Options.From, Options.To);
            _step = Options.IncludeTime ? 1 : TimeSpan.TicksPerDay;
            _min = TickRange.Ceiling(Options.From.Ticks, _step);
            _max = TickRange.Floor(Options.To.Ticks, _step);
            if (_min > _max) throw new ArgumentException("区间内没有可生成的日期。", nameof(options.To));
            _format = string.IsNullOrEmpty(Options.Format) ? DateTimeFieldOptions.DefaultFormat : Options.Format;
            Options.From.ToString(_format, CultureInfo.InvariantCulture);
        }
        /// <summary>生成日期。</summary>
        public DateTime Generate() => Execute<DateTime>(() => new DateTime(RandomValueGenerator.NextLong(Rnd, _min, _max) * _step, Options.From.Kind));
    }
}
