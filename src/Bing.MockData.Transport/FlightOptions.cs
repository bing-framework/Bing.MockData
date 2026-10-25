using System;
using Bing.MockData.Catalogs;

namespace Bing.MockData.Transport
{
    /// <summary>定义可配置航班的目录记录、时间窗口和飞行时长。</summary>
    public sealed class FlightOptions
    {
        /// <summary>获取或设置出发机场筛选使用的国家代码。</summary>
        public string DepartureCountry { get; set; }

        /// <summary>获取或设置到达机场筛选使用的国家代码。</summary>
        public string ArrivalCountry { get; set; }

        /// <summary>获取或设置航空公司筛选使用的国家代码。</summary>
        public string AirlineCountry { get; set; }

        /// <summary>获取或设置显式指定的出发机场目录记录。</summary>
        public CatalogRecord Departure { get; set; }

        /// <summary>获取或设置显式指定的到达机场目录记录。</summary>
        public CatalogRecord Arrival { get; set; }

        /// <summary>获取或设置显式指定的航空公司目录记录。</summary>
        public CatalogRecord Airline { get; set; }

        /// <summary>获取或设置起飞窗口的最早时刻；默认使用上下文参考时间。</summary>
        public DateTimeOffset? EarliestDeparture { get; set; }

        /// <summary>获取或设置起飞窗口的最晚时刻；默认使用参考时间后 30 天。</summary>
        public DateTimeOffset? LatestDeparture { get; set; }

        /// <summary>获取或设置最短飞行时长；默认 30 分钟。</summary>
        public TimeSpan MinDuration { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>获取或设置最长飞行时长；默认 900 分钟。</summary>
        public TimeSpan MaxDuration { get; set; } = TimeSpan.FromMinutes(900);

        /// <summary>获取或设置航班座位号范围；默认使用 1 到 50 行及 A 到 F 列。</summary>
        public SeatOptions Seats { get; set; } = new SeatOptions();
    }

    /// <summary>定义模拟座位号的行号和 ASCII 列字母范围。</summary>
    public sealed class SeatOptions
    {
        /// <summary>获取或设置最小行号，默认值为 1。</summary>
        public int MinRow { get; set; } = 1;

        /// <summary>获取或设置最大行号，默认值为 50。</summary>
        public int MaxRow { get; set; } = 50;

        /// <summary>获取或设置最小列字母，默认值为 A。</summary>
        public char MinColumn { get; set; } = 'A';

        /// <summary>获取或设置最大列字母，默认值为 F。</summary>
        public char MaxColumn { get; set; } = 'F';
    }
}
