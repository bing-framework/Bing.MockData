using System.Collections.Generic;
using System.Linq;
using Bing.MockData.Geography;
using Xunit;

namespace Bing.MockData.Tests.Capabilities
{
    public class GeographyTests
    {
        [Fact]
        public void Snapshot_contains_all_unique_records_with_valid_acyclic_parents()
        {
            Assert.Equal("2025-12-31", ChinaDivisions.Version);
            Assert.Equal(3212, ChinaDivisions.All.Count);
            Assert.Equal(ChinaDivisions.All.Count, ChinaDivisions.All.Select(division => division.Code).Distinct().Count());

            foreach (var division in ChinaDivisions.All)
            {
                Assert.Matches("^[0-9]{6}$", division.Code);
                Assert.False(string.IsNullOrWhiteSpace(division.Name));
                var path = ChinaDivisions.Path(division.Code);
                Assert.NotEmpty(path);
                Assert.Equal(division.Code, path[path.Count - 1].Code);
                Assert.Equal(path.Count, path.Select(item => item.Code).Distinct().Count());

                if (division.ParentCode == null)
                {
                    Assert.Equal(ChinaDivisionLevel.Province, division.Level);
                    Assert.Single(path);
                    continue;
                }

                var parent = ChinaDivisions.Find(division.ParentCode);
                Assert.NotNull(parent);
                Assert.True(parent.Level < division.Level);
                Assert.Equal(division.ParentCode, path[path.Count - 2].Code);
            }
        }

        [Theory]
        [InlineData("110101", "110000")]
        [InlineData("500101", "500000")]
        [InlineData("419001", "410000")]
        [InlineData("429004", "420000")]
        [InlineData("469001", "460000")]
        [InlineData("659001", "650000")]
        public void Direct_county_units_attach_to_the_real_parent(string code, string parentCode)
        {
            var division = ChinaDivisions.Find(code);
            Assert.NotNull(division);
            Assert.Equal(ChinaDivisionLevel.County, division.Level);
            Assert.Equal(parentCode, division.ParentCode);
        }

        [Theory]
        [InlineData("710000", "台湾省")]
        [InlineData("810000", "香港特别行政区")]
        [InlineData("820000", "澳门特别行政区")]
        public void Taiwan_hong_kong_and_macau_are_source_provided_province_roots(string code, string name)
        {
            var division = ChinaDivisions.Find(code);
            Assert.NotNull(division);
            Assert.Equal(name, division.Name);
            Assert.Equal(ChinaDivisionLevel.Province, division.Level);
            Assert.Null(division.ParentCode);
            Assert.Empty(ChinaDivisions.Children(code));
        }

        [Fact]
        public void Queries_return_read_only_results_and_empty_values_for_unknown_codes()
        {
            Assert.Null(ChinaDivisions.Find("999999"));
            Assert.Null(ChinaDivisions.Find(null));
            Assert.Empty(ChinaDivisions.Children("999999"));
            Assert.Empty(ChinaDivisions.Path("999999"));
            Assert.Empty(ChinaDivisions.Path(null));
            var all = Assert.IsAssignableFrom<IList<ChinaDivision>>(ChinaDivisions.All);
            Assert.True(all.IsReadOnly);
            Assert.Throws<System.NotSupportedException>(() => all[0] = all[1]);
            Assert.IsAssignableFrom<IReadOnlyList<ChinaDivision>>(ChinaDivisions.Children("420000"));
            Assert.Equal("420000", ChinaDivisions.Path("420102")[0].Code);
        }
    }
}
