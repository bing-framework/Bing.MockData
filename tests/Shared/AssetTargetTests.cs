using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Xunit;

namespace Bing.MockData.AssetTests;

public class AssetTargetTests
{
    [Fact]
    public void Every_library_dependency_uses_the_requested_target_asset()
    {
#if MOCKDATA_NETSTANDARD
        const string expected=".NETStandard,Version=v2.0";
#else
        const string expected=".NETCoreApp,Version=v10.0";
#endif
        string test=typeof(AssetTargetTests).Assembly.GetName().Name;
        string[] packages=test.Contains("Domains")?new[]{"Bing.MockData","Bing.MockData.Catalogs","Bing.MockData.Transport","Bing.MockData.Education","Bing.MockData.Healthcare","Bing.MockData.Devices"}
            :new[]{"Bing.MockData",test.Replace(".Tests","")};
        foreach(string name in packages.Distinct())
        {
            var assembly=Assembly.Load(new AssemblyName(name));
            Assert.True(assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName==expected,name+" loaded the wrong target: "+assembly.Location);
        }
    }
}
