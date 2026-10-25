using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Bing.MockData.Catalogs;
using Newtonsoft.Json.Linq;

namespace Bing.MockData.Transport
{
    public sealed class Vehicle
    { public CatalogRecord Model {get;set;}
        /// <summary>由车型记录的 manufacturerId 精确解析的厂商。</summary>
        public CatalogRecord Manufacturer {get;set;}
        /// <summary>来源提供的车辆类别，不自动翻译。</summary>
        public string Category {get;set;}
        public string Vin {get;set;} public bool HasVerifiedVinModelMapping=>false; public string Plate {get;set;} }
    public sealed class Flight
    { public CatalogRecord Airline {get;set;} public CatalogRecord Departure {get;set;} public CatalogRecord Arrival {get;set;} public string Number {get;set;} public DateTimeOffset DepartsAt {get;set;} public DateTimeOffset ArrivesAt {get;set;} public string Seat {get;set;} public bool IsSimulatedRoute=>true; }
    /// <summary>离线车辆与航空目录门面；生成航班不代表真实航线。</summary>
    public sealed class TransportData
    {
        private readonly MockContext _c;internal TransportData(MockContext c){_c=c;}
        public CatalogRecord Model(string country=null)=>Pick("vehicles","model",country);
        /// <summary>从符合国家筛选的车型解析厂商，抽样权重随该厂商符合条件的车型数变化。</summary>
        public CatalogRecord Manufacturer(string country=null)=>ManufacturerFor(Model(country));
        /// <summary>读取与车型同一条目录记录中的类别。</summary>
        public string Category(CatalogRecord model)
        {
            if(model==null||model.Kind!="model")throw new ArgumentException("需要车辆车型记录。",nameof(model));
            _c.CancellationToken.ThrowIfCancellationRequested();return (string)JObject.Parse(model.PayloadJson)["kind"];
        }
        /// <summary>根据车型稳定厂商键查找关联记录；缺失时报告覆盖错误。</summary>
        public CatalogRecord ManufacturerFor(CatalogRecord model)
        {
            if(model==null||model.Kind!="model")throw new ArgumentException("需要车辆车型记录。",nameof(model));
            string key=(string)JObject.Parse(model.PayloadJson)["manufacturerId"];
            if(key==null||!_c.Catalog("vehicles").TryGet("make|"+key,out var record))throw new CatalogCoverageException("vehicles","manufacturerId="+key);
            return record;
        }
        public CatalogRecord Airport(string country=null)=>Pick("airports","airport",country);
        public CatalogRecord Airline(string country=null)=>Pick("airlines","airline",country);
        public IEnumerable<CatalogRecord> Find(string catalog,CatalogQuery query)=>_c.Catalog(catalog).Query(query,_c.CancellationToken);
        public DataSetInfo Coverage(string catalog)=>_c.Catalog(catalog).Info;
        private CatalogRecord Pick(string name,string kind,string country)=>_c.Catalog(name).Sample(_c.Numbers.ULong(),new CatalogQuery{Kind=kind,CountryCode=country,CancellationToken=_c.CancellationToken});
        /// <summary>17 位格式及校验正确的模拟 VIN；不保证 WMI 或车型编码已分配。</summary>
        public string Vin()
        {
            const string chars="0123456789ABCDEFGHJKLMNPRSTUVWXYZ";string body=_c.Text.Random(17,17,chars);var output=body.ToCharArray();int[] weights={8,7,6,5,4,3,2,10,0,9,8,7,6,5,4,3,2};int sum=0;for(int i=0;i<17;i++)sum+=Value(body[i])*weights[i];int check=sum%11;output[8]=check==10?'X':(char)('0'+check);return new string(output);
        }
        private static int Value(char c){if(c>='0'&&c<='9')return c-'0';const string letters="ABCDEFGHJKLMNPRSTUVWXYZ";int[] values={1,2,3,4,5,6,7,8,1,2,3,4,5,7,9,2,3,4,5,6,7,8,9};return values[letters.IndexOf(c)];}
        public string ChinaPlate(string province="京",bool newEnergy=false)
        {
            if(province==null||province.Length!=1||!"京津沪渝冀豫云辽黑湘皖鲁新苏浙赣鄂桂甘晋蒙陕吉闽贵粤青藏川宁琼".Contains(province))throw new ArgumentException("需要中国省级号牌简称。",nameof(province));
            string city=_c.Text.Random(1,1,"ABCDEFGHJKLMNPQRSTUVWXYZ");return province+city+(newEnergy?_c.Collections.Pick(new[]{"D","F"})+_c.Text.Random(5,5,"0123456789"):_c.Text.Random(5,5,"0123456789ABCDEFGHJKLMNPQRSTUVWXYZ"));
        }
        public MockBuilder<Vehicle> Vehicle(string country=null)=>_c.Builder<Vehicle>().RuleFor(x=>x.Model,c=>c.Transport().Model(country)).RuleFor(x=>x.Manufacturer,(c,x)=>c.Transport().ManufacturerFor(x.Model)).RuleFor(x=>x.Category,(c,x)=>c.Transport().Category(x.Model)).RuleFor(x=>x.Vin,c=>c.Transport().Vin()).RuleFor(x=>x.Plate,c=>c.Transport().ChinaPlate());
        public MockBuilder<Flight> Flight(string country=null)
        {
            return _c.Builder<Flight>().RuleFor(x=>x.Airline,c=>c.Transport().Airline()).RuleFor(x=>x.Departure,c=>c.Transport().Airport(country))
                .RuleFor(x=>x.Arrival,(c,x)=>{for(int i=0;i<c.Limits.MaxAttempts;i++){c.CancellationToken.ThrowIfCancellationRequested();var a=c.Transport().Airport(country);if(a.Key!=x.Departure.Key)return a;}throw new CatalogCoverageException("airports","至少两个不同机场");})
                .RuleFor(x=>x.Number,c=>"MOCK"+c.Numbers.Int(100,9999).ToString(CultureInfo.InvariantCulture))
                .RuleFor(x=>x.DepartsAt,c=>c.Dates.Future(TimeSpan.FromDays(30)))
                .RuleFor(x=>x.ArrivesAt,(c,x)=>x.DepartsAt.AddMinutes(c.Numbers.Int(30,900)))
                .RuleFor(x=>x.Seat,c=>c.Numbers.Int(1,50).ToString(CultureInfo.InvariantCulture)+c.Text.Random(1,1,"ABCDEF"));
        }

        /// <summary>按冻结的选项创建模拟航班；目录记录只证明其自身来源，不表示真实运营航线。</summary>
        /// <param name="options">航班筛选和时间配置。</param>
        /// <returns>可重复生成所配置航班的对象构建器。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> 为空。</exception>
        /// <exception cref="ArgumentException">记录类型、记录来源、国家筛选、起飞窗口或时长范围无效。</exception>
        /// <exception cref="CatalogCoverageException">目录没有满足条件的机场或航空公司。</exception>
        /// <exception cref="OperationCanceledException">当前上下文已取消。</exception>
        public MockBuilder<Flight> ConfiguredFlight(FlightOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            _c.ForField("FlightConfiguration");
            var frozen = FreezeFlightOptions(options);
            EnsureAirportCoverage(frozen);
            EnsureAirlineCoverage(frozen);
            if (_c.Limits.MaxTextLength < 7) throw new MockLimitException(nameof(MockLimits.MaxTextLength), _c.Limits.MaxTextLength);

            return _c.Builder<Flight>()
                .RuleFor(x => x.Airline, c => frozen.Airline ?? c.Transport().Airline(frozen.AirlineCountry))
                .RuleFor(x => x.Departure, c => frozen.Departure ?? (frozen.Arrival != null
                    ? PickDifferentAirport(c, frozen.DepartureCountry, frozen.Arrival)
                    : c.Transport().Airport(frozen.DepartureCountry)))
                .RuleFor(x => x.Arrival, (c, x) => frozen.Arrival ?? PickDifferentAirport(c, frozen.ArrivalCountry, x.Departure))
                .RuleFor(x => x.Number, c => "MOCK" + c.ForField("FlightNumber").Numbers.Int(100, c.Limits.MaxTextLength >= 8 ? 9999 : 999).ToString(CultureInfo.InvariantCulture))
                .RuleFor(x => x.DepartsAt, c => c.ForField("DepartureTime").Dates.Between(frozen.EarliestDeparture, frozen.LatestDeparture))
                .RuleFor(x => x.ArrivesAt, (c, x) => x.DepartsAt.Add(c.ForField("Duration").Dates.Duration(frozen.MinDuration, frozen.MaxDuration)))
                .RuleFor(x => x.Seat, c => GenerateSeat(c.ForField("Seat"), frozen.Seat));
        }

        /// <summary>生成独立的模拟座位号。</summary>
        /// <param name="options">行号和 ASCII 列字母范围；省略时使用 1 到 50 行、A 到 F 列。</param>
        /// <returns>由行号和单个大写 ASCII 字母组成的座位号。</returns>
        /// <exception cref="ArgumentOutOfRangeException">行号边界不是正数或范围倒置。</exception>
        /// <exception cref="ArgumentException">列范围不是有效的大写 ASCII 字母范围。</exception>
        /// <exception cref="MockLimitException">最大行号产生的座位号超过文本预算。</exception>
        /// <exception cref="OperationCanceledException">当前上下文已取消。</exception>
        public string Seat(SeatOptions options = null)
        {
            var optionsSnapshot = options ?? new SeatOptions();
            return GenerateSeat(_c, FreezeSeatOptions(optionsSnapshot, _c.Limits.MaxTextLength));
        }

        private FrozenFlightOptions FreezeFlightOptions(FlightOptions options)
        {
            ValidateCountry(options.DepartureCountry, nameof(options.DepartureCountry));
            ValidateCountry(options.ArrivalCountry, nameof(options.ArrivalCountry));
            ValidateCountry(options.AirlineCountry, nameof(options.AirlineCountry));

            var earliest = options.EarliestDeparture ?? _c.ReferenceTime;
            DateTimeOffset latest;
            try { latest = options.LatestDeparture ?? _c.ReferenceTime.AddDays(30); }
            catch (ArgumentOutOfRangeException e) { throw new ArgumentException("默认起飞窗口超出可表示日期范围，请显式指定有效窗口。", nameof(options), e); }
            if (earliest < _c.ReferenceTime) throw new ArgumentException("最早起飞时刻不能早于上下文参考时间。", nameof(options));
            if (latest < earliest) throw new ArgumentException("最晚起飞时刻不能早于最早起飞时刻。", nameof(options));
            if (options.MinDuration <= TimeSpan.Zero || options.MaxDuration < options.MinDuration)
                throw new ArgumentException("飞行时长必须为正数且最大时长不小于最小时长。", nameof(options));
            try { earliest.Add(options.MaxDuration); latest.Add(options.MaxDuration); }
            catch (ArgumentOutOfRangeException e) { throw new ArgumentException("最长飞行时长会使到达时刻超出可表示日期范围。", nameof(options), e); }

            var departure = ResolveReference("airports", "airport", options.Departure, options.DepartureCountry, nameof(options.Departure));
            var arrival = ResolveReference("airports", "airport", options.Arrival, options.ArrivalCountry, nameof(options.Arrival));
            var airline = ResolveReference("airlines", "airline", options.Airline, options.AirlineCountry, nameof(options.Airline));
            if (departure != null && arrival != null && departure.Key == arrival.Key)
                throw new ArgumentException("出发机场和到达机场不能相同。", nameof(options));

            return new FrozenFlightOptions(departure, arrival, airline, options.DepartureCountry, options.ArrivalCountry,
                options.AirlineCountry, earliest, latest, options.MinDuration, options.MaxDuration,
                FreezeSeatOptions(options.Seats ?? new SeatOptions(), _c.Limits.MaxTextLength));
        }

        private void EnsureAirportCoverage(FrozenFlightOptions options)
        {
            if (options.Departure != null && options.Arrival != null) return;
            var catalog = _c.Catalog("airports");
            long departureCount = 0, arrivalCount = 0;
            CatalogQuery departureQuery = null, arrivalQuery = null;
            if (options.Departure == null)
            {
                departureQuery = new CatalogQuery { Kind = "airport", CountryCode = options.DepartureCountry, CancellationToken = _c.CancellationToken };
                departureCount = catalog.Count(departureQuery);
                if (departureCount == 0) throw new CatalogCoverageException("airports", options.DepartureCountry ?? "departure airport");
            }
            if (options.Arrival == null)
            {
                arrivalQuery = new CatalogQuery { Kind = "airport", CountryCode = options.ArrivalCountry, CancellationToken = _c.CancellationToken };
                arrivalCount = catalog.Count(arrivalQuery);
                if (arrivalCount == 0) throw new CatalogCoverageException("airports", options.ArrivalCountry ?? "arrival airport");
            }

            if (options.Departure == null && options.Arrival == null && options.DepartureCountry == options.ArrivalCountry && departureCount < 2)
                throw new CatalogCoverageException("airports", options.DepartureCountry ?? "at least two distinct airports");
            if (options.Departure != null && arrivalCount == 1 && catalog.Sample(0, arrivalQuery).Key == options.Departure.Key)
                throw new CatalogCoverageException("airports", "至少两个不同机场");
            if (options.Arrival != null && departureCount == 1 && catalog.Sample(0, departureQuery).Key == options.Arrival.Key)
                throw new CatalogCoverageException("airports", "至少两个不同机场");
            if (options.Departure == null && options.Arrival == null && departureCount == 1 && arrivalCount == 1 &&
                catalog.Sample(0, departureQuery).Key == catalog.Sample(0, arrivalQuery).Key)
                throw new CatalogCoverageException("airports", "至少两个不同机场");
        }

        private void EnsureAirlineCoverage(FrozenFlightOptions options)
        {
            if (options.Airline != null) return;
            var query = new CatalogQuery { Kind = "airline", CountryCode = options.AirlineCountry, CancellationToken = _c.CancellationToken };
            if (_c.Catalog("airlines").Count(query) == 0)
                throw new CatalogCoverageException("airlines", options.AirlineCountry ?? "airline");
        }

        private CatalogRecord ResolveReference(string catalogName, string kind, CatalogRecord record, string country, string parameterName)
        {
            if (record == null) return null;
            if (record.Kind != kind) throw new ArgumentException("目录记录类型必须为 " + kind + "。", parameterName);
            if (country != null && !string.Equals(record.CountryCode, country, StringComparison.Ordinal))
                throw new ArgumentException("显式目录记录与国家筛选不一致。", parameterName);
            if (!_c.Catalog(catalogName).TryGet(record.Key, out var canonical) || canonical == null ||
                canonical.Kind != record.Kind || canonical.CountryCode != record.CountryCode || canonical.Name != record.Name || canonical.PayloadJson != record.PayloadJson)
                throw new ArgumentException("目录记录不属于当前挂载的数据集。", parameterName);
            return canonical;
        }

        private void ValidateCountry(string country, string parameterName)
        {
            if (country == null) return;
            if (country.Length == 0 || country.IndexOf('\0') >= 0) throw new ArgumentException("国家代码不能为空且不能含 NUL。", parameterName);
            if (country.Length > _c.Limits.MaxTextLength) throw new MockLimitException(nameof(MockLimits.MaxTextLength), _c.Limits.MaxTextLength);
        }

        private CatalogRecord PickDifferentAirport(MockContext context, string country, CatalogRecord departure)
        {
            var catalog = context.Catalog("airports");
            var query = new CatalogQuery { Kind = "airport", CountryCode = country, CancellationToken = context.CancellationToken };
            var count = catalog.Count(query);
            if (count == 0) throw new CatalogCoverageException("airports", country ?? "arrival airport");
            var first = context.Numbers.ULong() % (ulong)count;
            for (var i = 0; i < context.Limits.MaxAttempts; i++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                var candidate = catalog.Sample((first + (ulong)i) % (ulong)count, query);
                if (candidate.Key != departure.Key) return candidate;
            }
            throw new CatalogCoverageException("airports", "至少两个不同机场");
        }

        private static FrozenSeatOptions FreezeSeatOptions(SeatOptions options, int maxTextLength)
        {
            if (options.MinRow < 1 || options.MaxRow < options.MinRow) throw new ArgumentOutOfRangeException(nameof(options), "行号必须为正数且最大值不小于最小值。");
            if (options.MinColumn < 'A' || options.MaxColumn > 'Z' || options.MaxColumn < options.MinColumn)
                throw new ArgumentException("列范围必须是升序的大写 ASCII 字母 A 到 Z。", nameof(options));
            var maxRowLength = options.MaxRow.ToString(CultureInfo.InvariantCulture).Length;
            if ((long)maxRowLength + 1 > maxTextLength) throw new MockLimitException(nameof(MockLimits.MaxTextLength), maxTextLength);
            return new FrozenSeatOptions(options.MinRow, options.MaxRow, options.MinColumn, options.MaxColumn);
        }

        private static string GenerateSeat(MockContext context, FrozenSeatOptions options)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var row = context.Numbers.Int(options.MinRow, options.MaxRow).ToString(CultureInfo.InvariantCulture);
            var column = (char)context.Numbers.Int(options.MinColumn, options.MaxColumn);
            var result = row + column;
            if (result.Length > context.Limits.MaxTextLength) throw new MockLimitException(nameof(MockLimits.MaxTextLength), context.Limits.MaxTextLength);
            return result;
        }

        private sealed class FrozenFlightOptions
        {
            internal readonly CatalogRecord Departure, Arrival, Airline;
            internal readonly string DepartureCountry, ArrivalCountry, AirlineCountry;
            internal readonly DateTimeOffset EarliestDeparture, LatestDeparture;
            internal readonly TimeSpan MinDuration, MaxDuration;
            internal readonly FrozenSeatOptions Seat;
            internal FrozenFlightOptions(CatalogRecord departure, CatalogRecord arrival, CatalogRecord airline,
                string departureCountry, string arrivalCountry, string airlineCountry, DateTimeOffset earliest,
                DateTimeOffset latest, TimeSpan minDuration, TimeSpan maxDuration, FrozenSeatOptions seat)
            { Departure = departure; Arrival = arrival; Airline = airline; DepartureCountry = departureCountry; ArrivalCountry = arrivalCountry; AirlineCountry = airlineCountry; EarliestDeparture = earliest; LatestDeparture = latest; MinDuration = minDuration; MaxDuration = maxDuration; Seat = seat; }
        }

        private sealed class FrozenSeatOptions
        {
            internal readonly int MinRow, MaxRow;
            internal readonly char MinColumn, MaxColumn;
            internal FrozenSeatOptions(int minRow, int maxRow, char minColumn, char maxColumn)
            { MinRow = minRow; MaxRow = maxRow; MinColumn = minColumn; MaxColumn = maxColumn; }
        }
    }
    public static class TransportExtensions {public static TransportData Transport(this MockContext context)=>new TransportData(context??throw new ArgumentNullException(nameof(context)));}
}
