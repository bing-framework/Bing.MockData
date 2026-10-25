using System;
using System.Collections.Generic;
using Bing.MockData.Catalogs;
using Newtonsoft.Json.Linq;

namespace Bing.MockData.Devices
{
    public sealed class Device
    {public CatalogRecord Model {get;set;} public CatalogRecord Manufacturer {get;set;} public string SerialNumber {get;set;}public string Imei {get;set;}public bool HasVerifiedTacMapping=>false;public string Firmware {get;set;}}
    public sealed class DeviceMeasurement
    {public string DeviceSerial {get;set;}public DateTimeOffset Timestamp {get;set;}public string Metric {get;set;}public string Unit {get;set;}public double Value {get;set;}}
    /// <summary>模拟测量配置，在创建模板时冻结；不是设备能力目录。</summary>
    public sealed class MeasurementOptions
    {
        /// <summary>指标名称。</summary>
        public string Metric {get;set;}="temperature";
        /// <summary>测量单位。</summary>
        public string Unit {get;set;}="Cel";
        /// <summary>模拟数值下限。</summary>
        public double Minimum {get;set;}=15;
        /// <summary>模拟数值上限。</summary>
        public double Maximum {get;set;}=45;
        /// <summary>序号零的时间；默认上下文参考时间。</summary>
        public DateTimeOffset? StartAt {get;set;}
        /// <summary>相邻记录时间间隔。</summary>
        public TimeSpan Interval {get;set;}=TimeSpan.FromSeconds(1);
    }
    public sealed class DeviceData
    {
        private readonly MockContext _c;internal DeviceData(MockContext c){_c=c;}
        public DataSetInfo Coverage()=>_c.Catalog("devices").Info;
        public CatalogRecord Model()=>_c.Catalog("devices").Sample(_c.Numbers.ULong(),new CatalogQuery{Kind="device",CancellationToken=_c.CancellationToken});
        public IEnumerable<CatalogRecord> Find(string vendorId)=>_c.Catalog("devices").Query(new CatalogQuery{Kind="device",KeyPrefix="device|"+vendorId+"|"},_c.CancellationToken);
        public string Imei()
        {string body=_c.Text.Random(14,14,"0123456789");int sum=0;for(int i=0;i<body.Length;i++){int n=body[i]-'0';if(i%2==1){n*=2;if(n>9)n-=9;}sum+=n;}return body+(10-sum%10)%10;}
        public MockBuilder<Device> Device()
        {
            return _c.Builder<Device>().RuleFor(x=>x.Model,c=>c.Devices().Model())
                .RuleFor(x=>x.Manufacturer,(c,x)=>{string vendor=(string)JObject.Parse(x.Model.PayloadJson)["vendorId"];if(vendor==null)vendor=x.Model.Key.Split('|')[1];if(!c.Catalog("devices").TryGet("vendor|"+vendor,out var result))throw new CatalogCoverageException("devices","vendor|"+vendor);return result;})
                .RuleFor(x=>x.SerialNumber,c=>c.Identifiers.Business("DEV-"))
                .RuleFor(x=>x.Imei,c=>c.Devices().Imei()).RuleFor(x=>x.Firmware,c=>c.Numbers.Int(1,9)+"."+c.Numbers.Int(0,99)+"."+c.Numbers.Int(0,999));
        }
        /// <summary>生成可配置的模拟设备测量，不把指定指标解释为真实目录设备的能力。</summary>
        public MockBuilder<DeviceMeasurement> Measurement(Device device,MeasurementOptions options=null)
        {
            if(device==null)throw new ArgumentNullException(nameof(device));options=options??new MeasurementOptions();
            string serial=device.SerialNumber,metric=options.Metric,unit=options.Unit;double min=options.Minimum,max=options.Maximum;var interval=options.Interval;var start=options.StartAt??_c.ReferenceTime;
            if(string.IsNullOrWhiteSpace(serial)||string.IsNullOrWhiteSpace(metric)||string.IsNullOrWhiteSpace(unit)||interval<=TimeSpan.Zero||double.IsNaN(min)||double.IsNaN(max)||double.IsInfinity(min)||double.IsInfinity(max)||min>max)throw new ArgumentException("测量配置无效。");
            foreach(string value in new[]{serial,metric,unit})if(value.Length>_c.Limits.MaxTextLength)throw new MockLimitException(nameof(MockLimits.MaxTextLength),_c.Limits.MaxTextLength);
            return _c.Builder<DeviceMeasurement>().RuleFor(x=>x.DeviceSerial,_=>serial)
                .RuleFor(x=>x.Timestamp,c=>start.AddTicks(checked(c.RecordIndex*interval.Ticks)))
                .RuleFor(x=>x.Metric,_=>metric).RuleFor(x=>x.Unit,_=>unit).RuleFor(x=>x.Value,c=>c.Numbers.Double(min,max));
        }

    }
    public static class DeviceExtensions {public static DeviceData Devices(this MockContext context)=>new DeviceData(context??throw new ArgumentNullException(nameof(context)));}
}
