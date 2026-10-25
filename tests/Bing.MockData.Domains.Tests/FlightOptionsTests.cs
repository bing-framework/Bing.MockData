#pragma warning disable xUnit1051
using System;
using System.IO;
using System.Linq;
using System.Threading;
using Bing.MockData.Catalogs;
using Bing.MockData.Transport;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Xunit;

namespace Bing.MockData.Domains.Tests;

public sealed class FlightOptionsTests
{
    private static readonly DateTimeOffset Reference = new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    private static string Data(string name) => IndustryGapTests.Data(name);

    private static MockContext CreateContext(int seed = 71, DateTimeOffset? referenceTime = null) =>
        new MockContext(new MockOptions { Seed = seed, ReferenceTime = referenceTime ?? Reference }
            .UseCatalogFile("airports", Data("ourairports.sqlite3"))
            .UseCatalogFile("airlines", Data("wikidata-airlines.sqlite3")));

    private static CatalogRecord First(MockContext context, string catalog, string kind, string country) =>
        context.Transport().Find(catalog, new CatalogQuery { Kind = kind, CountryCode = country }).First();

    private static string CreateFixture(string path, string id, params (string Key, string Kind, string Country, string Name, string Payload)[] records)
    {
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA application_id=1112364081; PRAGMA user_version=1; " +
                    "CREATE TABLE CatalogInfo (Id TEXT NOT NULL PRIMARY KEY, Version TEXT NOT NULL, SourceUri TEXT NOT NULL, License TEXT NOT NULL, SnapshotDate TEXT NOT NULL, RecordCount INTEGER NOT NULL, SourceSha256 TEXT NOT NULL, Coverage TEXT NOT NULL, FileFormatVersion INTEGER NOT NULL);" +
                    "CREATE TABLE Records (Ordinal INTEGER NOT NULL PRIMARY KEY, Key TEXT NOT NULL COLLATE BINARY UNIQUE, Kind TEXT NOT NULL, CountryCode TEXT NULL, Name TEXT NOT NULL, PayloadJson TEXT NOT NULL);";
                command.ExecuteNonQuery();
            }
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "INSERT INTO CatalogInfo VALUES ($id,'fixture-v1','https://example.invalid','CC0-1.0','2026-01-10',$count,$sha,'two-row deterministic test fixture',1);";
                command.Parameters.AddWithValue("$id", id);
                command.Parameters.AddWithValue("$count", records.Length);
                command.Parameters.AddWithValue("$sha", new string('0', 64));
                command.ExecuteNonQuery();
            }
            for (var i = 0; i < records.Length; i++)
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "INSERT INTO Records VALUES ($ordinal,$key,$kind,$country,$name,$payload);";
                    command.Parameters.AddWithValue("$ordinal", i + 1);
                    command.Parameters.AddWithValue("$key", records[i].Key);
                    command.Parameters.AddWithValue("$kind", records[i].Kind);
                    command.Parameters.AddWithValue("$country", records[i].Country);
                    command.Parameters.AddWithValue("$name", records[i].Name);
                    command.Parameters.AddWithValue("$payload", records[i].Payload);
                    command.ExecuteNonQuery();
                }
            }
        }
        return path;
    }

    [Fact]
    public void Configured_flight_freezes_explicit_records_and_time_ranges()
    {
        using var context = CreateContext();
        var departure = First(context, "airports", "airport", "US");
        var arrival = First(context, "airports", "airport", "GB");
        var airline = First(context, "airlines", "airline", "GB");
        var options = new FlightOptions
        {
            Departure = departure,
            Arrival = arrival,
            Airline = airline,
            DepartureCountry = "US",
            ArrivalCountry = "GB",
            AirlineCountry = "GB",
            EarliestDeparture = Reference.AddHours(4),
            LatestDeparture = Reference.AddHours(8),
            MinDuration = TimeSpan.FromMinutes(45),
            MaxDuration = TimeSpan.FromMinutes(70),
            Seats = new SeatOptions { MinRow = 17, MaxRow = 19, MinColumn = 'D', MaxColumn = 'F' }
        };

        var generator = context.Transport().ConfiguredFlight(options).Build();
        options.Departure = arrival;
        options.Arrival = departure;
        options.Airline = null;
        options.EarliestDeparture = Reference.AddDays(20);
        options.LatestDeparture = Reference.AddDays(21);
        options.MinDuration = TimeSpan.FromDays(2);
        options.MaxDuration = TimeSpan.FromDays(3);
        options.Seats.MinRow = 1;
        options.Seats.MaxRow = 1;
        options.Seats.MinColumn = 'A';
        options.Seats.MaxColumn = 'A';

        foreach (var flight in generator.Enumerate(30))
        {
            Assert.Equal(departure.Key, flight.Departure.Key);
            Assert.Equal(arrival.Key, flight.Arrival.Key);
            Assert.Equal(airline.Key, flight.Airline.Key);
            Assert.InRange(flight.DepartsAt, Reference.AddHours(4), Reference.AddHours(8));
            Assert.InRange(flight.ArrivesAt - flight.DepartsAt, TimeSpan.FromMinutes(45), TimeSpan.FromMinutes(70));
            Assert.Matches("^(1[7-9])[D-F]$", flight.Seat);
            Assert.True(flight.IsSimulatedRoute);
        }
    }

    [Fact]
    public void Country_filters_generate_from_each_requested_country_and_replay_in_parallel()
    {
        using var context = CreateContext();
        var generator = context.Transport().ConfiguredFlight(new FlightOptions
        {
            DepartureCountry = "CN",
            ArrivalCountry = "US",
            AirlineCountry = "GB",
            EarliestDeparture = Reference.AddDays(1),
            LatestDeparture = Reference.AddDays(3),
            MinDuration = TimeSpan.FromMinutes(30),
            MaxDuration = TimeSpan.FromMinutes(90)
        }).Build();

        foreach (var flight in generator.Enumerate(40))
        {
            Assert.Equal("CN", flight.Departure.CountryCode);
            Assert.Equal("US", flight.Arrival.CountryCode);
            Assert.Equal("GB", flight.Airline.CountryCode);
            Assert.NotEqual(flight.Departure.Key, flight.Arrival.Key);
        }
        Assert.Equal(generator.Enumerate(30).Select(JsonConvert.SerializeObject),
            generator.EnumerateParallel(30, 4).Select(JsonConvert.SerializeObject));
    }

    [Fact]
    public void Seat_uses_configured_positive_rows_and_ascii_columns()
    {
        using var context = CreateContext();
        var options = new SeatOptions { MinRow = 17, MaxRow = 19, MinColumn = 'D', MaxColumn = 'F' };
        foreach (var _ in Enumerable.Range(0, 30))
        {
            var seat = context.Transport().Seat(options);
            Assert.Matches("^(1[7-9])[D-F]$", seat);
        }
    }

    [Fact]
    public void Seat_and_flight_number_respect_the_context_text_budget()
    {
        using var context = new MockContext(new MockOptions
        {
            Seed = 9,
            ReferenceTime = Reference,
            Limits = new MockLimits { MaxTextLength = 2 }
        }.UseCatalogFile("airports", Data("ourairports.sqlite3"))
         .UseCatalogFile("airlines", Data("wikidata-airlines.sqlite3")));

        Assert.Matches("^[1-9][A-F]$", context.Transport().Seat(new SeatOptions { MinRow = 1, MaxRow = 9 }));
        Assert.Throws<MockLimitException>(() => context.Transport().Seat());
        Assert.Throws<MockLimitException>(() => context.Transport().ConfiguredFlight(new FlightOptions()));
    }

    [Fact]
    public void Invalid_routes_time_windows_and_seat_ranges_fail_explicitly()
    {
        using var context = CreateContext();
        var departure = First(context, "airports", "airport", "US");
        var arrival = First(context, "airports", "airport", "GB");
        var airline = First(context, "airlines", "airline", "GB");

        Assert.Throws<ArgumentNullException>(() => context.Transport().ConfiguredFlight(null));
        Assert.Throws<ArgumentException>(() => context.Transport().ConfiguredFlight(new FlightOptions { Departure = departure, Arrival = departure, Airline = airline }));
        Assert.Throws<ArgumentException>(() => context.Transport().ConfiguredFlight(new FlightOptions { Departure = airline, Arrival = arrival, Airline = airline }));
        Assert.Throws<ArgumentException>(() => context.Transport().ConfiguredFlight(new FlightOptions { Departure = departure, Arrival = arrival, Airline = airline, DepartureCountry = "GB" }));
        Assert.Throws<ArgumentException>(() => context.Transport().ConfiguredFlight(new FlightOptions { EarliestDeparture = Reference.AddHours(2), LatestDeparture = Reference.AddHours(1) }));
        Assert.Throws<ArgumentException>(() => context.Transport().ConfiguredFlight(new FlightOptions { MinDuration = TimeSpan.FromMinutes(60), MaxDuration = TimeSpan.FromMinutes(30) }));
        using var nearDateLimit = CreateContext(referenceTime: DateTimeOffset.MaxValue.AddDays(-1));
        Assert.Throws<ArgumentException>(() => nearDateLimit.Transport().ConfiguredFlight(new FlightOptions()));
        Assert.Throws<ArgumentException>(() => nearDateLimit.Transport().ConfiguredFlight(new FlightOptions
        {
            EarliestDeparture = nearDateLimit.ReferenceTime,
            LatestDeparture = nearDateLimit.ReferenceTime,
            MinDuration = TimeSpan.FromDays(2),
            MaxDuration = TimeSpan.FromDays(3)
        }));
        Assert.Throws<ArgumentOutOfRangeException>(() => context.Transport().Seat(new SeatOptions { MinRow = 0 }));
        Assert.Throws<ArgumentException>(() => context.Transport().Seat(new SeatOptions { MinColumn = 'a', MaxColumn = 'z' }));
    }

    [Fact]
    public void Cancelled_context_stops_configured_generation()
    {
        using var context = CreateContext();
        using var cancellation = new CancellationTokenSource();
        var record = context.ForRecord(0, cancellation.Token);
        var generator = record.Transport().ConfiguredFlight(new FlightOptions
        {
            DepartureCountry = "CN",
            ArrivalCountry = "US",
            AirlineCountry = "GB"
        }).Build();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => generator.At(0));
    }

    [Fact]
    public void Random_departure_avoids_explicit_arrival_with_two_airport_fixture()
    {
        var folder = Path.Combine(Path.GetTempPath(), "bing-mock-flight-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var airportsPath = CreateFixture(Path.Combine(folder, "airports.sqlite3"), "airports",
                ("airport|a", "airport", "CN", "Airport A", "{}"),
                ("airport|b", "airport", "CN", "Airport B", "{}"));
            var airlinesPath = CreateFixture(Path.Combine(folder, "airlines.sqlite3"), "airlines",
                ("airline|one", "airline", "GB", "Airline One", "{}"));

            using (var context = new MockContext(new MockOptions { Seed = 1234, ReferenceTime = Reference }
                .UseCatalogFile("airports", airportsPath).UseCatalogFile("airlines", airlinesPath)))
            {
                var query = new CatalogQuery { Kind = "airport", CountryCode = "CN" };
                var firstRecordSelector = context.ForRecord(0).ForField("Departure").Numbers.ULong();
                var arrival = context.Catalog("airports").Sample(firstRecordSelector, query);
                var airline = context.Transport().Airline("GB");
                var generator = context.Transport().ConfiguredFlight(new FlightOptions
                {
                    Arrival = arrival,
                    DepartureCountry = "CN",
                    Airline = airline,
                    EarliestDeparture = Reference,
                    LatestDeparture = Reference.AddHours(1),
                    MinDuration = TimeSpan.FromMinutes(30),
                    MaxDuration = TimeSpan.FromMinutes(30)
                }).Build();

                var repeated = JsonConvert.SerializeObject(generator.At(0));
                Assert.Equal(repeated, JsonConvert.SerializeObject(generator.At(0)));
                foreach (var flight in generator.Enumerate(20))
                {
                    Assert.Equal(arrival.Key, flight.Arrival.Key);
                    Assert.NotEqual(arrival.Key, flight.Departure.Key);
                }
            }
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
