using System;
using System.Collections.Generic;
using System.Linq;
using Bing.MockData.Catalogs;
using Bing.MockData.Models;

namespace Bing.MockData.Healthcare
{
    public sealed class Patient {public string Id {get;set;}public PersonProfile Person {get;set;}}
    public sealed class Observation {public string Code {get;set;}public string Name {get;set;}public string Unit {get;set;}public double Value {get;set;}public DateTimeOffset Timestamp {get;set;}public bool IsSimulated=>true;}
    public sealed partial class Encounter {public string Id {get;set;}public Patient Patient {get;set;}public string Department {get;set;}public DateTimeOffset StartedAt {get;set;}public DateTimeOffset EndedAt {get;set;}public CatalogRecord Diagnosis {get;set;}public IReadOnlyList<Observation> Observations {get;set;}}
    /// <summary>美国 ICD-10-CM 与 RxNorm CPC 目录引用；不推导临床适应关系。</summary>
    public sealed class HealthcareData
    {
        private readonly MockContext _c;internal HealthcareData(MockContext c){_c=c;}
        public CatalogRecord Diagnosis()=>_c.Catalog("diagnoses").Sample(_c.Numbers.ULong(),new CatalogQuery{Kind="diagnosis",CancellationToken=_c.CancellationToken});
        public CatalogRecord Medication()=>_c.Catalog("medications").Sample(_c.Numbers.ULong(),new CatalogQuery{Kind="medication",CancellationToken=_c.CancellationToken});
        public DataSetInfo Coverage(string catalog)=>_c.Catalog(catalog).Info;
        public IEnumerable<CatalogRecord> FindDiagnoses(string prefix)=>_c.Catalog("diagnoses").Query(new CatalogQuery{Kind="diagnosis",KeyPrefix=prefix},_c.CancellationToken);
        public string Department()=>_c.Collections.Pick(_c.Locale=="zh-CN"?new[]{"全科（模拟）","内科（模拟）","外科（模拟）"}:new[]{"General practice (simulated)","Internal medicine (simulated)","Surgery (simulated)"});
        public MockBuilder<Patient> Patient()=>_c.Builder<Patient>().RuleFor(x=>x.Id,c=>c.Identifiers.Business("PAT-")).RuleFor(x=>x.Person,c=>c.Person());
        /// <summary>生成模拟检查项目；数值范围仅用于造数，不是医学参考区间。</summary>
        public ExaminationItem ExaminationItem()
        {
            bool zh=_c.Locale=="zh-CN";
            return _c.Collections.Pick(new[]{
                new ExaminationItem{Code="mock-body-temperature",Name=zh?"模拟体温检查":"Simulated temperature test",Unit="Cel",Minimum=35,Maximum=39},
                new ExaminationItem{Code="mock-pulse",Name=zh?"模拟脉搏检查":"Simulated pulse test",Unit="/min",Minimum=50,Maximum=120},
                new ExaminationItem{Code="mock-sample-index",Name=zh?"模拟样本指标":"Simulated sample index",Unit="mock-unit",Minimum=0,Maximum=100}
            });
        }
        /// <summary>生成具检查和药品引用的就诊；药品数量为零时可只挂载诊断目录。</summary>
        public MockBuilder<Encounter> Encounter(Patient patient,EncounterOptions options=null)
        {
            if(patient==null)throw new ArgumentNullException(nameof(patient));options=options??new EncounterOptions();
            int tests=options.Examinations,medications=options.Medications;
            if(tests<0||medications<0)throw new ArgumentOutOfRangeException(nameof(options));
            // 检查和对应观察各算一个元素，额外保留一个基础体温观察。
            long size=2L*tests+medications+1;
            if(size>_c.Limits.MaxCollectionSize)throw new MockLimitException(nameof(MockLimits.MaxCollectionSize),_c.Limits.MaxCollectionSize);
            if(_c.Limits.MaxDepth<3)throw new MockLimitException(nameof(MockLimits.MaxDepth),_c.Limits.MaxDepth);
            ExaminationItem[] definitions=null;
            if(options.Items!=null)
            {
                if(options.Items.Count>_c.Limits.MaxCollectionSize)throw new MockLimitException(nameof(MockLimits.MaxCollectionSize),_c.Limits.MaxCollectionSize);
                if(tests>0&&options.Items.Count==0)throw new ArgumentException("检查项目不能为空。");
                definitions=options.Items.Select(CopyItem).ToArray();
                if(definitions.Select(x=>x.Code).Distinct(StringComparer.Ordinal).Count()!=definitions.Length)throw new ArgumentException("检查代码必须唯一。");
            }
            // 配置时确认所需目录存在；每条记录仍通过自己的随机上下文抽样。
            _=_c.Catalog("diagnoses");if(medications>0)_=_c.Catalog("medications");
            return _c.Builder<Encounter>().RuleFor(x=>x.Id,c=>c.Identifiers.Business("ENC-")).RuleFor(x=>x.Patient,_=>patient).RuleFor(x=>x.Department,c=>c.Healthcare().Department())
                .RuleFor(x=>x.EndedAt,c=>c.Dates.Past(TimeSpan.FromDays(30))).RuleFor(x=>x.StartedAt,(_,x)=>x.EndedAt.AddHours(-1))
                .RuleFor(x=>x.Diagnosis,c=>c.Healthcare().Diagnosis()).RuleFor(x=>x.DiagnosisDataVersion,c=>c.Catalog("diagnoses").Info.Version)
                .RuleFor(x=>x.Examinations,(c,x)=>
                {
                    var rows=new List<Examination>();
                    for(int i=0;i<tests;i++)
                    {
                        c.CancellationToken.ThrowIfCancellationRequested();var field=c.ForField("["+i+"]").ForObject();
                        var itemContext=field.ForObject();
                        var item=definitions==null?itemContext.Healthcare().ExaminationItem():itemContext.Healthcare().CopyItem(itemContext.Collections.Pick(definitions));
                        var from=x.StartedAt;long interval=(x.EndedAt-from).Ticks;
                        rows.Add(new Examination{Id=x.Id+"-TEST-"+(i+1),Item=item,OrderedAt=from.AddTicks(interval/6),CollectedAt=from.AddTicks(interval/3),ResultedAt=from.AddTicks(interval*2/3),Result=field.Numbers.Double(item.Minimum,item.Maximum)});
                    }
                    return (IReadOnlyList<Examination>)rows.AsReadOnly();
                })
                .RuleFor(x=>x.Observations,(c,x)=>
                {
                    c.ForObject();var rows=new List<Observation>{new Observation{Code="mock-body-temperature",Name=c.Locale=="zh-CN"?"模拟体温":"Simulated body temperature",Unit="Cel",Value=c.Numbers.Double(36,38),Timestamp=x.StartedAt.AddMinutes(15)}};
                    foreach(var test in x.Examinations){c.CancellationToken.ThrowIfCancellationRequested();rows.Add(new Observation{Code=test.Item.Code,Name=test.Item.Name,Unit=test.Item.Unit,Value=test.Result,Timestamp=test.ResultedAt});}
                    return (IReadOnlyList<Observation>)rows.OrderBy(o=>o.Timestamp).ToList().AsReadOnly();
                })
                .RuleFor(x=>x.Medications,(c,x)=>
                {
                    var rows=new List<MedicationReference>();
                    for(int i=0;i<medications;i++){c.CancellationToken.ThrowIfCancellationRequested();var field=c.ForField("["+i+"]").ForObject();rows.Add(new MedicationReference{Medication=field.Healthcare().Medication(),DataVersion=c.Catalog("medications").Info.Version,RecordedAt=c.Dates.Between(x.StartedAt,x.EndedAt)});}
                    return (IReadOnlyList<MedicationReference>)rows.AsReadOnly();
                })
                .ValidateWith((c,x)=>
                {
                    if(x.StartedAt>x.EndedAt||x.EndedAt>c.ReferenceTime)throw new InvalidOperationException("就诊时间顺序无效。");
                    if(x.Examinations.Any(t=>t.OrderedAt<x.StartedAt||t.CollectedAt<t.OrderedAt||t.ResultedAt<t.CollectedAt||t.ResultedAt>x.EndedAt)||x.Observations.Any(o=>o.Timestamp<x.StartedAt||o.Timestamp>x.EndedAt)||x.Medications.Any(m=>m.RecordedAt<x.StartedAt||m.RecordedAt>x.EndedAt))throw new InvalidOperationException("检查或药品记录不在就诊时间内。");
                });
        }
        private ExaminationItem CopyItem(ExaminationItem item)
        {
            _c.CancellationToken.ThrowIfCancellationRequested();
            if(item==null||string.IsNullOrWhiteSpace(item.Code)||string.IsNullOrWhiteSpace(item.Name)||string.IsNullOrWhiteSpace(item.Unit)||double.IsNaN(item.Minimum)||double.IsNaN(item.Maximum)||double.IsInfinity(item.Minimum)||double.IsInfinity(item.Maximum)||item.Minimum>item.Maximum)throw new ArgumentException("模拟检查项目无效。");
            foreach(string text in new[]{item.Code,item.Name,item.Unit})if(text.Length>_c.Limits.MaxTextLength)throw new MockLimitException(nameof(MockLimits.MaxTextLength),_c.Limits.MaxTextLength);
            return new ExaminationItem{Code=item.Code,Name=item.Name,Unit=item.Unit,Minimum=item.Minimum,Maximum=item.Maximum};
        }

    }
    public static class HealthcareExtensions {public static HealthcareData Healthcare(this MockContext context)=>new HealthcareData(context??throw new ArgumentNullException(nameof(context)));}
}
