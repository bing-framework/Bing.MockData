using System;
using System.IO;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Collections.Generic;
using Bing.MockData;
using Bing.MockData.Catalogs;
using Bing.MockData.Devices;
using Bing.MockData.Education;
using Bing.MockData.Healthcare;
using Bing.MockData.Schema;
using Bing.MockData.Transport;
using Bing.MockData.Models;

public static class Program
{
    public sealed class Form
    {
        [EmailAddress,MaxLength(10)] public string Email {get;set;}
        [MinLength(2),MaxLength(3)] public bool[] Choices {get;set;}
        [StringLength(10,MinimumLength=10),RegularExpression("^[A-Z]{1,10000}$")] public string Code {get;set;}
        public Detail Nested {get;set;}
        [MinLength(2),MaxLength(2)] public IDictionary<string,Detail> Map {get;set;}
    }
    public sealed class Detail { [Range(1,5)] public int Value {get;set;} }
    public static void Main(string[] args)
    {
        string data=args[0];
        using(var banks=new MockContext(new MockOptions{Seed=39}.UseCatalogFile("bank-bins",Path.Combine(data,"bank-bins.sqlite3"))))
        {
            var card=banks.Finance.BankCard("308",Bing.MockData.Datas.Models.Banks.CardType.CreditCard);
            int sum=0;bool doubled=false;
            for(int i=card.Length-1;i>=0;i--){int n=card[i]-'0';if(doubled){n*=2;if(n>9)n-=9;}sum+=n;doubled=!doubled;}
            if(sum%10!=0||banks.Replay.GenerationContractVersion!="domain-v6")throw new Exception("Bank card contract failed.");
            var bins=banks.BankBins();
            if(bins.Count()!=458051||bins.Count(new CatalogQuery{CountryCode="CN"})!=18389)throw new Exception("BIN coverage failed.");
            var sample=bins.Sample();
            if(!bins.TryGet(sample.Bin,out var same)||same.Key!=sample.Key)throw new Exception("BIN lookup failed.");
            if(!banks.Generator(c=>c.BankBins().Sample().Key).Enumerate(20).SequenceEqual(banks.Generator(c=>c.BankBins().Sample().Key).EnumerateParallel(20,4)))throw new Exception("BIN replay failed.");
        }
        using(var phoneCatalog=SqliteMockCatalog.Open(Path.Combine(data,"libphonenumber.sqlite3")))
        using(var phoneContext=new MockContext(new MockOptions{Seed=39}.UsePhoneMetadataCatalog(phoneCatalog)))
        {
            var phone=phoneContext.Phone.NonGeographicNumber("870",extension:true);
            if(phone.CountryCode!="001"||!phone.E164.StartsWith("+870")||phone.Extension.Length!=4)throw new Exception("Non-geographic phone failed.");
        }
        using(var resolved=new MockContext(new MockOptions{Seed=39}.UseCatalogFile("devices",Path.Combine(data,"pci-ids-resolved.sqlite3"))))
        {
            if(resolved.Catalog("devices").Info.Id!="pci-ids-resolved"||resolved.Devices().Device().Build().At(0).Model==null)throw new Exception("Resolved PCI snapshot failed.");
        }
        var options=new MockOptions{Seed=101,ReferenceTime=new DateTimeOffset(2026,10,25,0,0,0,TimeSpan.Zero)}
            .UseCatalogFile("devices",Path.Combine(data,"pci-ids.sqlite3"))
            .UseCatalogFile("airports",Path.Combine(data,"ourairports.sqlite3"))
            .UseCatalogFile("vehicles",Path.Combine(data,"vehicles.sqlite3"))
            .UseCatalogFile("institutions",Path.Combine(data,"ror.sqlite3"))
            .UseCatalogFile("books",Path.Combine(data,"crossref-books.sqlite3"))
            .UseCatalogFile("diagnoses",Path.Combine(data,"icd10cm.sqlite3"))
            .UseCatalogFile("medications",Path.Combine(data,"rxnorm-cpc.sqlite3"))
            .UseCatalogFile("airlines",Path.Combine(data,"wikidata-airlines.sqlite3"))
            .UseSchema("uuid","{\"type\":\"string\",\"format\":\"uuid\"}",limits:new SchemaOptions{AssertFormat=true})
            .UseSchema("dependent","{\"type\":\"object\",\"properties\":{\"a\":{\"allOf\":[{\"enum\":[1,2]},{\"const\":1.0}]}},\"required\":[\"a\"],\"dependentSchemas\":{\"a\":{\"properties\":{\"b\":{\"const\":2}},\"required\":[\"b\"],\"dependentSchemas\":{\"b\":{\"properties\":{\"c\":{\"const\":3}},\"required\":[\"c\"]}}}},\"unevaluatedProperties\":false}")
            .UseSchema("payload","{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"integer\"}},\"required\":[\"id\"],\"additionalProperties\":false}");
        using(var context=new MockContext(options))
        {
            var device=context.Devices().Device().Build().At(42);
            if(device.Manufacturer==null||device.Imei.Length!=15)throw new Exception("Device invariant failed.");
            if(context.Transport().Airport("CN").CountryCode!="CN")throw new Exception("Catalog country failed.");
            if(context.Education().Isbn13().Length!=13)throw new Exception("ISBN failed.");
            if(string.IsNullOrEmpty(context.Healthcare().Department()))throw new Exception("Healthcare facade failed.");
            if(!context.Schema("payload").At(5).Json.Contains("id"))throw new Exception("Schema failed.");
            using(var stream=new MemoryStream())MockExport.Write(context.Schema("payload").Enumerate(10),stream,MockExportFormat.Json,leaveOpen:true);
            var form=context.AutoBuilder<Form>().Build().At(0);
            if(form.Email.Length>10||form.Choices.Length<2)throw new Exception("Auto constraints failed.");
            if(form.Code.Length!=10||form.Map.Count!=2||form.Nested.Value<1)throw new Exception("Nested auto constraints failed.");
            using(var shortEmail=CompiledSchema.Compile("{\"type\":\"string\",\"format\":\"email\",\"maxLength\":10}",options:new SchemaOptions{AssertFormat=true}))
                if(!shortEmail.Validate(shortEmail.Generate(context).Json))throw new Exception("Combined schema constraints failed.");
            using(var negated=CompiledSchema.Compile("{\"type\":\"integer\",\"minimum\":0,\"maximum\":1000000,\"not\":{\"anyOf\":[{\"maximum\":777776},{\"minimum\":777778}]}}"))
                if(negated.Generate(context).Json!="777777")throw new Exception("Schema complement failed.");
            if(context.Network.Client(new ClientProfileOptions{DeviceType=ClientDeviceType.Mobile}).DeviceType!=ClientDeviceType.Mobile)throw new Exception("Mobile client failed.");
            if(context.Colors.Palette().Count!=148)throw new Exception("Named palette failed.");
            var configuredProduct=context.Templates.ConfiguredProduct(new ProductSampleOptions{CategoryCode="electronics"}).Build().At(0);
            if(!context.Commerce.Brands("electronics").Contains(configuredProduct.Brand))throw new Exception("Product category brand failed.");
            if(context.Schema("uuid").At(0).Json.Length!=38)throw new Exception("Format generation failed.");
            var organization=context.Templates.Organization(new OrganizationOptions{Departments=2,Employees=4}).Build().At(0);
            if(organization.EmployeeRoles.Count!=4||organization.RolePermissions.Count==0)throw new Exception("Organization links failed.");
            var users=context.Templates.User().Build().GenerateMany(2);var products=context.Templates.Product().Build().GenerateMany(3);
            var order=context.Templates.Order(users,products,3,3,OrderStatus.Shipped).Build().At(0);
            var warehouse=context.Templates.Warehouse(2).Build().At(0);
            var shipment=context.Templates.Fulfillment(order,warehouse,2).Build().At(0);
            if(shipment.Parcels.SelectMany(p=>p.Lines).Sum(l=>l.Quantity)!=order.Items.Sum(i=>i.Quantity))throw new Exception("Parcel allocation failed.");
            var inventory=context.Templates.Inventory(products[0],warehouse.Locations[0],0,5).Build().At(0);
            if(inventory.Movements.Any(m=>m.Balance<0))throw new Exception("Inventory failed.");
            var trace=context.Templates.RequestTrace(4).Build().At(0);
            if(trace.Logs.Any(l=>l.TraceId!=trace.TraceId)||trace.Events.Count!=8)throw new Exception("Trace failed.");
            var major=context.Education().Major().Build().At(0);
            var cohort=context.Education().Cohort(context.Education().Institution(),major,1,2,2,2).Build().At(0);
            if(cohort.Enrollments.Count!=4||context.Education().BookDetails().Source==null)throw new Exception("Education failed.");
            var patient=context.Healthcare().Patient().Build().At(0);
            var encounter=context.Healthcare().Encounter(patient).Build().At(0);
            if(encounter.Examinations.Count!=2||encounter.Medications.Count!=1)throw new Exception("Encounter failed.");
            if(context.Transport().Vehicle().Build().At(0).Manufacturer==null)throw new Exception("Vehicle relation failed.");
            var measurement=context.Devices().Measurement(device,new MeasurementOptions{Metric="humidity",Unit="%",Minimum=0,Maximum=100}).Build().At(4);
            if(measurement.Metric!="humidity")throw new Exception("Measurement failed.");
            if(!context.Schema("dependent").At(7).Json.Contains("\"c\":3"))throw new Exception("Schema dependency closure failed.");
            var flight=context.Transport().ConfiguredFlight(new FlightOptions
            {
                DepartureCountry="CN",ArrivalCountry="US",
                EarliestDeparture=context.ReferenceTime.AddDays(1),LatestDeparture=context.ReferenceTime.AddDays(2),
                MinDuration=TimeSpan.FromHours(10),MaxDuration=TimeSpan.FromHours(12),
                Seats=new SeatOptions{MinRow=4,MaxRow=4,MinColumn='B',MaxColumn='B'}
            }).Build().At(7);
            if(flight.Departure.CountryCode!="CN"||flight.Arrival.CountryCode!="US"||flight.Seat!="4B"||flight.ArrivesAt<flight.DepartsAt.AddHours(10))throw new Exception("Configured flight failed.");
            if(context.Transport().Seat(new SeatOptions{MinRow=2,MaxRow=2,MinColumn='A',MaxColumn='A'})!="2A")throw new Exception("Seat configuration failed.");
        }
        Console.WriteLine("Dual asset consumer and native SQLite PASS.");
    }
}
