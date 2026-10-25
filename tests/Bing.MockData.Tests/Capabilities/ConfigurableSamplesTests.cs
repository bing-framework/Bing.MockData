using System;
using System.Collections.Generic;
using System.Linq;
using Bing.MockData;
using Xunit;

namespace Bing.MockData.Tests.Capabilities;

public sealed class ConfigurableSamplesTests
{
    private static MockContext Context(string locale = "zh-CN", int? maxCollectionSize = null) => new(new MockOptions
    {
        Seed = 829,
        Locale = locale,
        Limits = maxCollectionSize.HasValue ? new MockLimits { MaxCollectionSize = maxCollectionSize.Value } : new MockLimits()
    });

    [Theory]
    [InlineData("zh-CN", "信息技术", "研发部", "软件工程师")]
    [InlineData("en-US", "Technology", "Engineering", "Software engineer")]
    public void Careers_are_broad_filterable_and_snapshot_custom_profiles(string locale, string industry, string department, string job)
    {
        using var context = Context(locale);
        Assert.True(context.Companies.Industries().Count >= 15);
        Assert.NotEmpty(context.Companies.Departments(industry));
        Assert.Equal(department, context.People.Career(new CareerProfileOptions { Industry = industry, Department = department }).Department);
        Assert.Equal(job, context.People.Job(industry, department));

        var profiles = new List<CareerProfile> { new("Test industry", "Test department", "Test role") };
        var options = new CareerProfileOptions { Profiles = profiles, Industry = "Test industry", Department = "Test department" };
        var generator = context.People.Careers(options);
        profiles.Add(new CareerProfile("Other industry", "Other department", "Other role"));
        options.Industry = "Other industry";
        var frozen = generator.At(9, TestContext.Current.CancellationToken);
        Assert.Equal("Test industry", frozen.Industry);
        Assert.Equal("Test department", frozen.Department);
        Assert.Equal("Test role", frozen.Job);
        Assert.Throws<ArgumentException>(() => context.People.Careers(new CareerProfileOptions { Industry = "missing" }));
    }

    [Fact]
    public void Product_samples_keep_category_spec_and_brand_relationships_and_freeze_configuration()
    {
        using var context = Context();
        var tree = context.Commerce.Categories();
        Assert.True(tree.Count >= 20);
        Assert.Contains(tree, x => x.ParentCode != null && tree.Any(p => p.Code == x.ParentCode));
        var options = new ProductSampleOptions { CategoryCode = "electronics" };
        var products = context.Commerce.Products(options);
        var values = products.GenerateMany(100, TestContext.Current.CancellationToken);
        Assert.All(values, sample =>
        {
            Assert.Equal("electronics", sample.CategoryPath[0].Code);
            var leaf = Assert.Single(tree, x => x.Code == sample.CategoryCode);
            Assert.Contains(sample.Material, leaf.Materials);
            Assert.Contains(sample.Specification, leaf.Specifications);
            Assert.Contains(sample.Unit, leaf.Units);
            Assert.Contains(sample.Brand, leaf.Brands);
        });

        var custom = new List<ProductCategoryDefinition>
        {
            new("custom", null, "自定义", "Custom"),
            new("widget", "custom", "组件", "Widget", new[]{"metal"}, new[]{"small","large"}, new[]{"piece"}, new[]{"Only Widget Brand", "Secondary Widget Brand"})
        };
        var customOptions = new ProductSampleOptions { Categories = custom, CategoryCode = "custom", Brand = "Only Widget Brand" };
        var frozen = context.Commerce.Products(customOptions);
        custom.Add(new ProductCategoryDefinition("another", null, "其他", "Other", new[]{"wood"}, new[]{"wide"}, new[]{"item"}, new[]{"Other Brand"}));
        customOptions.Brand = "Other Brand";
        var sample = frozen.At(2, TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "custom", "widget" }, sample.CategoryPath.Select(x => x.Code));
        Assert.Equal("Only Widget Brand", sample.Brand);
        Assert.All(frozen.GenerateMany(20, TestContext.Current.CancellationToken), value => Assert.Equal("Only Widget Brand", value.Brand));
        Assert.Equal("widget", context.Commerce.Category(new ProductSampleOptions { Categories = custom.Take(2).ToArray(), CategoryCode = "custom" }).Code);
        using var english = Context("en-US");
        var translated = english.Commerce.Product(new ProductSampleOptions { CategoryCode = "keyboards", Brand = "Cloud Sail Digital Example" });
        Assert.Equal("Cloud Sail Digital Example", translated.Brand);
        Assert.Equal("Keyboards", translated.CategoryName);
        Assert.Contains(translated.Material, new[] { "Plastic", "Aluminum alloy" });
        Assert.Contains(translated.Specification, new[] { "87-key", "104-key", "Wireless" });
        Assert.Equal("Piece", translated.Unit);
        Assert.Contains(translated.Brand, translated.CategoryPath.Last().Brands);
        Assert.Contains(translated.Material, translated.CategoryPath.Last().Materials);
        Assert.Throws<ArgumentException>(() => context.Commerce.Products(new ProductSampleOptions { CategoryCode = "missing" }));
        Assert.Throws<ArgumentException>(() => context.Commerce.Products(new ProductSampleOptions { Categories = new[] { new ProductCategoryDefinition("a", "missing", "A", "A") } }));
        Assert.Throws<ArgumentException>(() => context.Commerce.Products(new ProductSampleOptions
        {
            Categories = new[] { new ProductCategoryDefinition("a", "b", "A", "A"), new ProductCategoryDefinition("b", "a", "B", "B") }
        }));
    }

    [Fact]
    public void Built_in_english_product_candidates_match_generated_samples_and_custom_values_stay_unchanged()
    {
        using var context = Context("en-US");
        var categories = context.Commerce.Categories();
        var selectable = categories.Where(category => category.IsSelectable).ToArray();

        Assert.NotEmpty(selectable);
        foreach (var category in selectable)
        {
            Assert.All(category.Materials.Concat(category.Specifications).Concat(category.Units).Concat(category.Brands),
                candidate => Assert.False(HasCjk(candidate), $"Expected an English built-in candidate, got '{candidate}'."));

            var sample = context.Commerce.Product(new ProductSampleOptions
            {
                CategoryCode = category.Code,
                Brand = category.Brands[0]
            });

            Assert.Equal(category.Code, sample.CategoryCode);
            Assert.Contains(sample.Material, category.Materials);
            Assert.Contains(sample.Specification, category.Specifications);
            Assert.Contains(sample.Unit, category.Units);
            Assert.Contains(sample.Brand, category.Brands);
            Assert.False(HasCjk(sample.CategoryName));
            Assert.False(HasCjk(sample.Material));
            Assert.False(HasCjk(sample.Specification));
            Assert.False(HasCjk(sample.Unit));
            Assert.False(HasCjk(sample.Brand));
            var pathLeaf = sample.CategoryPath.Last();
            Assert.Equal(category.Code, pathLeaf.Code);
            Assert.Equal(category.Materials.ToArray(), pathLeaf.Materials.ToArray());
            Assert.Equal(category.Specifications.ToArray(), pathLeaf.Specifications.ToArray());
            Assert.Equal(category.Units.ToArray(), pathLeaf.Units.ToArray());
            Assert.Equal(category.Brands.ToArray(), pathLeaf.Brands.ToArray());
        }

        var customTree = new[]
        {
            new ProductCategoryDefinition("custom-root", null, "自定义根", "Custom root"),
            new ProductCategoryDefinition("custom-leaf", "custom-root", "自定义叶子", "Custom leaf",
                new[] { "塑料" }, new[] { "3 号" }, new[] { "只" }, new[] { "晨光里（模拟）" })
        };
        var customOptions = new ProductSampleOptions
        {
            Categories = customTree,
            CategoryCode = "custom-root",
            Brand = "晨光里（模拟）"
        };
        var customCategory = Assert.Single(context.Commerce.Categories(customOptions), category => category.Code == "custom-leaf");
        var customSample = context.Commerce.Product(customOptions);

        Assert.Equal("塑料", Assert.Single(customCategory.Materials));
        Assert.Equal("3 号", Assert.Single(customCategory.Specifications));
        Assert.Equal("只", Assert.Single(customCategory.Units));
        Assert.Equal("晨光里（模拟）", Assert.Single(customCategory.Brands));
        Assert.Equal("塑料", customSample.Material);
        Assert.Equal("3 号", customSample.Specification);
        Assert.Equal("只", customSample.Unit);
        Assert.Equal("晨光里（模拟）", customSample.Brand);
        Assert.Equal(customCategory.Materials.ToArray(), customSample.CategoryPath.Last().Materials.ToArray());
        Assert.Equal(customCategory.Brands.ToArray(), customSample.CategoryPath.Last().Brands.ToArray());
    }

    [Theory]
    [InlineData("zh-CN")]
    [InlineData("en-US")]
    public void Client_profiles_include_mobile_and_support_combined_filters_and_snapshots(string locale)
    {
        using var context = Context(locale);
        var mobile = context.Network.ClientProfiles(new ClientProfileOptions { DeviceType = ClientDeviceType.Mobile }).GenerateMany(40, TestContext.Current.CancellationToken);
        Assert.All(mobile, x => Assert.Equal(ClientDeviceType.Mobile, x.DeviceType));
        var androidChrome = context.Network.Client(new ClientProfileOptions
        {
            Browser = "Chrome", OperatingSystem = "Android", DeviceType = ClientDeviceType.Mobile
        });
        Assert.Contains("Android", androidChrome.UserAgent);

        var source = new List<ClientProfile> { new("OnlyBrowser", "OnlyOS", "Mock/1.0", ClientDeviceType.Tablet) };
        var options = new ClientProfileOptions { Profiles = source, DeviceType = ClientDeviceType.Tablet };
        var generator = context.Network.ClientProfiles(options);
        source.Clear(); options.DeviceType = ClientDeviceType.Mobile;
        Assert.Equal("OnlyBrowser", generator.At(12, TestContext.Current.CancellationToken).Browser);
        Assert.Throws<ArgumentException>(() => context.Network.ClientProfiles(new ClientProfileOptions { Browser = "not installed" }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClientProfile("Browser", "OS", "UA", (ClientDeviceType)99));
    }

    [Fact]
    public void Css_palette_contains_standard_names_and_family_sampling_is_limited()
    {
        using var context = Context();
        var palette = context.Colors.Palette();
        Assert.Equal(148, palette.Count);
        Assert.Contains(palette, color => color.Name == "rebeccapurple" && color.Hex == "#663399");
        Assert.Contains(palette, color => color.Name == "darkslategrey" && color.Hex == "#2F4F4F");
        foreach (var family in Enum.GetValues<ColorFamily>())
        {
            var familyPalette = context.Colors.Palette(family);
            Assert.NotEmpty(familyPalette);
            Assert.All(Enumerable.Range(0, 25), _ => Assert.Contains(context.Colors.NamedColor(family), familyPalette));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => context.Colors.Palette((ColorFamily)99));
    }

    [Fact]
    public void Configurable_sample_candidates_obey_context_collection_budget()
    {
        using var context = Context(maxCollectionSize: 2);
        Assert.NotNull(context.People.Career());
        Assert.NotNull(context.Commerce.Product());
        Assert.NotNull(context.Network.Client());
        Assert.Throws<MockLimitException>(() => context.People.Careers(new CareerProfileOptions
        {
            Profiles = new[] { new CareerProfile("a", "a", "a"), new CareerProfile("b", "b", "b"), new CareerProfile("c", "c", "c") }
        }));
        Assert.Throws<MockLimitException>(() => context.Commerce.Products(new ProductSampleOptions
        {
            Categories = new[]
            {
                new ProductCategoryDefinition("a", null, "A", "A"),
                new ProductCategoryDefinition("b", null, "B", "B"),
                new ProductCategoryDefinition("c", null, "C", "C")
            }
        }));
        Assert.Throws<MockLimitException>(() => context.Network.ClientProfiles(new ClientProfileOptions
        {
            Profiles = new[] { new ClientProfile("a", "a", "a"), new ClientProfile("b", "b", "b"), new ClientProfile("c", "c", "c") }
        }));
    }

    private static bool HasCjk(string value) => value.Any(character => character >= '\u4e00' && character <= '\u9fff');
}
