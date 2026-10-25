#pragma warning disable xUnit1051
using System;
using System.IO;
using System.Linq;
using Bing.MockData.Catalogs;
using Bing.MockData.Devices;
using Bing.MockData.Healthcare;
using Bing.MockData.Transport;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bing.MockData.Domains.Tests;

public class IndustryGapTests
{
    internal static string Data(string name)
    {
        var folder=new DirectoryInfo(AppContext.BaseDirectory);
        while(folder!=null){var path=Path.Combine(folder.FullName,"src","Bing.MockData.Catalogs","Data",name);if(File.Exists(path))return path;folder=folder.Parent;}
        throw new FileNotFoundException(name);
    }
    [Fact]
    public void Vehicles_expose_manufacturer_and_category_from_the_same_source_model()
    {
        using var c=new MockContext(new MockOptions{Seed=24}.UseCatalogFile("vehicles",Data("vehicles.sqlite3")));
        foreach(var vehicle in c.Transport().Vehicle("US").Build().Enumerate(20))
        {
            var source=JObject.Parse(vehicle.Model.PayloadJson);
            Assert.Equal("make|"+(string)source["manufacturerId"]!,vehicle.Manufacturer.Key);
            Assert.Equal((string)source["kind"]!,vehicle.Category);
            Assert.False(vehicle.HasVerifiedVinModelMapping);
        }
    }
    [Fact]
    public void Measurement_configuration_is_frozen_indexed_and_parallel_replayable()
    {
        using var c=new MockContext(new MockOptions{Seed=24});
        var device=new Device{SerialNumber="S-1"};
        var options=new MeasurementOptions{Metric="humidity",Unit="%",Minimum=10,Maximum=90,Interval=TimeSpan.FromMinutes(2),StartAt=new DateTimeOffset(2020,1,1,0,0,0,TimeSpan.Zero)};
        var generator=c.Devices().Measurement(device,options).Build();options.Metric="changed";device.SerialNumber="changed";
        var sample=generator.At(6);Assert.Equal("humidity",sample.Metric);Assert.Equal("S-1",sample.DeviceSerial);Assert.InRange(sample.Value,10,90);
        Assert.Equal(options.StartAt.Value.AddMinutes(12),sample.Timestamp);
        Assert.Equal(generator.Enumerate(20).Select(JsonConvert.SerializeObject),generator.EnumerateParallel(20,4).Select(JsonConvert.SerializeObject));
        Assert.Throws<ArgumentException>(()=>c.Devices().Measurement(device,new MeasurementOptions{Interval=TimeSpan.Zero}));
    }
    [Fact]
    public void Encounter_links_examinations_observations_and_versioned_medications()
    {
        using var c=new MockContext(new MockOptions{Seed=24}.UseCatalogFile("diagnoses",Data("icd10cm.sqlite3")).UseCatalogFile("medications",Data("rxnorm-cpc.sqlite3")));
        var patient=c.Healthcare().Patient().Build().At(0);
        var item=new ExaminationItem{Code="mock-a",Name="模拟项目",Unit="mock-unit",Minimum=10,Maximum=20};
        var options=new EncounterOptions{Examinations=3,Medications=2,Items=new[]{item}};
        var generator=c.Healthcare().Encounter(patient,options).Build();item.Maximum=-1;options.Medications=1000;
        foreach(var encounter in generator.Enumerate(5))
        {
            Assert.Same(patient,encounter.Patient);Assert.Equal(3,encounter.Examinations.Count);Assert.Equal(2,encounter.Medications.Count);
            Assert.Equal(c.Catalog("diagnoses").Info.Version,encounter.DiagnosisDataVersion);
            foreach(var exam in encounter.Examinations)
            {
                Assert.InRange(exam.Result,10,20);Assert.InRange(exam.OrderedAt,encounter.StartedAt,exam.CollectedAt);
                Assert.InRange(exam.CollectedAt,exam.OrderedAt,exam.ResultedAt);Assert.True(exam.ResultedAt<=encounter.EndedAt);
                Assert.Contains(encounter.Observations,o=>o.Code==exam.Item.Code&&o.Value==exam.Result&&o.Timestamp==exam.ResultedAt);
            }
            foreach(var medication in encounter.Medications)
            {
                Assert.Equal("medication",medication.Medication.Kind);Assert.Equal(c.Catalog("medications").Info.Version,medication.DataVersion);
                Assert.InRange(medication.RecordedAt,encounter.StartedAt,encounter.EndedAt);Assert.False(medication.HasVerifiedIndication);
            }
        }
        Assert.Equal(generator.Enumerate(10).Select(JsonConvert.SerializeObject),generator.EnumerateParallel(10,4).Select(JsonConvert.SerializeObject));
        using var diagnosesOnly=new MockContext(new MockOptions().UseCatalogFile("diagnoses",Data("icd10cm.sqlite3")));
        Assert.Empty(diagnosesOnly.Healthcare().Encounter(patient,new EncounterOptions{Medications=0}).Build().At(0).Medications);
        Assert.Throws<MockLimitException>(()=>c.Healthcare().Encounter(patient,new EncounterOptions{Examinations=1000}));
    }
}
