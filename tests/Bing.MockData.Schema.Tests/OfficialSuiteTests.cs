#pragma warning disable xUnit1051
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bing.MockData.Schema;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bing.MockData.Schema.Tests;
public class OfficialSuiteTests
{
    private static string DataRoot()
    {var dir=new DirectoryInfo(AppContext.BaseDirectory);while(dir!=null){var candidate=Path.Combine(dir.FullName,"tools","data","json-schema-suite");if(Directory.Exists(candidate))return candidate;dir=dir.Parent;}throw new DirectoryNotFoundException("缺少固定版本官方套件。");}
    [Fact]public void Draft202012_official_required_and_optional_validation_matrix()
    {
        var root=DataRoot();var docs=Directory.GetFiles(Path.Combine(root,"remotes"),"*.json",SearchOption.AllDirectories).ToDictionary(p=>"http://localhost:1234/"+Path.GetRelativePath(Path.Combine(root,"remotes"),p).Replace('\\','/'),File.ReadAllText,StringComparer.Ordinal);
        var failures=new List<string>();int total=0,passed=0;
        foreach(var file in Directory.GetFiles(Path.Combine(root,"draft2020-12"),"*.json",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.Ordinal))
        {
            foreach(JObject group in (JArray)ExactJson.Parse(File.ReadAllText(file)))
            {
                string location=Path.GetRelativePath(root,file)+" :: "+(string)group["description"];
                try
                {
                    bool format=file.Contains(Path.DirectorySeparatorChar+"format"+Path.DirectorySeparatorChar);
                    using var schema=CompiledSchema.Compile(ExactJson.Write(group["schema"]),docs,new SchemaOptions{MaxDocuments=1000,MaxDocumentBytes=64*1024*1024,MaxDepth=256,AssertFormat=format?(bool?)true:null});
                    foreach(JObject test in (JArray)group["tests"]){total++;try{if(schema.Validate(ExactJson.Write(test["data"]))==(bool)test["valid"])passed++;else failures.Add(location+" :: "+(string)test["description"]);}catch(Exception e){failures.Add(location+" :: "+(string)test["description"]+" :: "+e.Message);}}
                }
                catch(Exception e){total+=((JArray)group["tests"]).Count;failures.Add(location+" :: COMPILE :: "+e.Message);}
            }
        }
        string asset=typeof(CompiledSchema).Assembly.GetCustomAttributes(typeof(System.Runtime.Versioning.TargetFrameworkAttribute),false).Cast<System.Runtime.Versioning.TargetFrameworkAttribute>().Single().FrameworkName.Contains("Standard")?"netstandard2.0":"net10.0";
        var report=new {Asset=asset,Commit=File.ReadAllText(Path.Combine(root,"COMMIT")).Trim(),Total=total,Passed=passed,Failures=failures};var output=Environment.GetEnvironmentVariable("MOCKDATA_EVIDENCE_DIRECTORY")??Path.GetFullPath(Path.Combine(root,"../../../artifacts/schema-validation",Guid.NewGuid().ToString("N")));Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"schema-official-suite-"+asset+".json"),Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
        Assert.True(failures.Count==0,$"官方套件 {passed}/{total}，失败 {failures.Count}："+string.Join(Environment.NewLine,failures.Take(12)));
    }
}
