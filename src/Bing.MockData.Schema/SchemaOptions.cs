using System;

namespace Bing.MockData.Schema
{
    /// <summary>Schema 编译与生成预算。上下文捕获配置副本。</summary>
    public sealed class SchemaOptions
    {
        public long MaxDocumentBytes {get;set;}=16*1024*1024;
        public int MaxDocuments {get;set;}=256;
        public int MaxSearchSteps {get;set;}=100000;
        public int MaxDepth {get;set;}=8;
        public TimeSpan RegexTimeout {get;set;}=TimeSpan.FromSeconds(1);
        public bool? AssertFormat {get;set;}
        internal SchemaOptions Copy(){var c=(SchemaOptions)MemberwiseClone();if(c.MaxDocumentBytes<1||c.MaxDocuments<1||c.MaxSearchSteps<1||c.MaxDepth<1||c.RegexTimeout<=TimeSpan.Zero||c.RegexTimeout.TotalMilliseconds>int.MaxValue)throw new ArgumentOutOfRangeException(nameof(SchemaOptions));return c;}
    }
    public enum SchemaFailure { InvalidSchema, MissingReference, ProvenContradiction, BudgetExhausted }
    public sealed class SchemaGenerationException:Exception
    {
        public SchemaFailure Failure {get;} public string SchemaLocation {get;} public string InstanceLocation {get;} public ReplayInfo Replay {get;}
        public string SchemaIdentity {get;internal set;}
        public SchemaGenerationException(SchemaFailure failure,string message,string schemaLocation="#",string instanceLocation="",ReplayInfo replay=null,Exception inner=null):base(message,inner)
        {Failure=failure;SchemaLocation=schemaLocation;InstanceLocation=instanceLocation;Replay=replay;}
    }
    /// <summary>保留原始 JSON 数值精度的值；Newtonsoft 导出写入 JSON 值而非字符串。</summary>
    [Newtonsoft.Json.JsonConverter(typeof(JsonSampleConverter))]
    public sealed class JsonSample
    {
        public string Json {get;}
        public string SchemaIdentity {get;}
        public ReplayInfo Replay {get;}
        internal JsonSample(string json,string schemaIdentity,ReplayInfo replay){Json=json;SchemaIdentity=schemaIdentity;Replay=replay;}
        public override string ToString()=>Json;
    }
    public sealed class JsonSampleConverter:Newtonsoft.Json.JsonConverter
    {
        public override bool CanConvert(Type type)=>type==typeof(JsonSample);
        public override void WriteJson(Newtonsoft.Json.JsonWriter writer,object value,Newtonsoft.Json.JsonSerializer serializer)=>writer.WriteRawValue(((JsonSample)value).Json);
        public override bool CanRead=>false;
        public override object ReadJson(Newtonsoft.Json.JsonReader reader,Type type,object existing,Newtonsoft.Json.JsonSerializer serializer)=>throw new NotSupportedException();
    }
}
