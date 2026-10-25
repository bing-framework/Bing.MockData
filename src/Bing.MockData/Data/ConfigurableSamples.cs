using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Bing.MockData
{
    /// <summary>表示一个相互匹配的模拟行业、部门和职业组合。</summary>
    public sealed class CareerProfile
    {
        /// <summary>获取行业名称。</summary>
        public string Industry { get; }
        /// <summary>获取部门名称。</summary>
        public string Department { get; }
        /// <summary>获取职业名称。</summary>
        public string Job { get; }

        /// <summary>创建自定义职业组合。</summary>
        public CareerProfile(string industry, string department, string job)
        {
            Industry = Required(industry, nameof(industry));
            Department = Required(department, nameof(department));
            Job = Required(job, nameof(job));
        }

        internal CareerProfile Copy() => new CareerProfile(Industry, Department, Job);
        private static string Required(string value, string name) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("职业资料字段不能为空。", name) : value;
    }

    /// <summary>配置职业组合生成范围；自定义组合会在创建生成器时复制。</summary>
    public sealed class CareerProfileOptions
    {
        /// <summary>获取或设置可选的自定义组合；为空时使用内置模拟样本。</summary>
        public IReadOnlyList<CareerProfile> Profiles { get; set; }
        /// <summary>获取或设置行业筛选；为空时不限制行业。</summary>
        public string Industry { get; set; }
        /// <summary>获取或设置部门筛选；为空时不限制部门。</summary>
        public string Department { get; set; }
    }

    internal static class CareerCatalog
    {
        private static readonly string[][] Rows =
        {
            new[] { "信息技术", "Technology", "研发部", "Engineering", "软件工程师", "Software engineer" },
            new[] { "信息技术", "Technology", "数据部", "Data", "数据分析师", "Data analyst" },
            new[] { "信息技术", "Technology", "安全部", "Security", "安全工程师", "Security engineer" },
            new[] { "信息技术", "Technology", "产品部", "Product", "产品经理", "Product manager" },
            new[] { "零售与电商", "Retail and commerce", "门店运营部", "Store operations", "门店主管", "Store manager" },
            new[] { "零售与电商", "Retail and commerce", "商品部", "Merchandising", "商品专员", "Merchandising specialist" },
            new[] { "零售与电商", "Retail and commerce", "客户服务部", "Customer service", "客户服务代表", "Customer service representative" },
            new[] { "制造业", "Manufacturing", "生产部", "Production", "生产计划员", "Production planner" },
            new[] { "制造业", "Manufacturing", "质量部", "Quality", "质量工程师", "Quality engineer" },
            new[] { "制造业", "Manufacturing", "设备部", "Maintenance", "设备技术员", "Maintenance technician" },
            new[] { "教育", "Education", "教学部", "Teaching", "教师", "Teacher" },
            new[] { "教育", "Education", "教务部", "Academic affairs", "教务专员", "Academic coordinator" },
            new[] { "教育", "Education", "研究部", "Research", "研究助理", "Research assistant" },
            new[] { "金融服务", "Financial services", "财务部", "Accounting", "会计", "Accountant" },
            new[] { "金融服务", "Financial services", "风险管理部", "Risk management", "风险分析师", "Risk analyst" },
            new[] { "金融服务", "Financial services", "客户服务部", "Client services", "理财顾问", "Financial adviser" },
            new[] { "医疗健康", "Healthcare", "护理部", "Nursing", "护士", "Nurse" },
            new[] { "医疗健康", "Healthcare", "检验部", "Laboratory", "检验技师", "Laboratory technician" },
            new[] { "医疗健康", "Healthcare", "行政部", "Administration", "医疗行政专员", "Medical administrator" },
            new[] { "物流运输", "Logistics and transport", "仓储部", "Warehouse", "仓库主管", "Warehouse supervisor" },
            new[] { "物流运输", "Logistics and transport", "运输部", "Transport", "物流调度员", "Logistics dispatcher" },
            new[] { "物流运输", "Logistics and transport", "采购部", "Procurement", "采购专员", "Procurement specialist" },
            new[] { "建筑工程", "Construction", "工程部", "Engineering", "土木工程师", "Civil engineer" },
            new[] { "建筑工程", "Construction", "项目部", "Projects", "项目协调员", "Project coordinator" },
            new[] { "媒体与设计", "Media and design", "设计部", "Design", "视觉设计师", "Visual designer" },
            new[] { "媒体与设计", "Media and design", "编辑部", "Editorial", "内容编辑", "Content editor" },
            new[] { "餐饮与酒店", "Hospitality", "餐饮部", "Food service", "厨师", "Chef" },
            new[] { "餐饮与酒店", "Hospitality", "客务部", "Guest services", "前台接待员", "Receptionist" },
            new[] { "能源与环境", "Energy and environment", "能源管理部", "Energy management", "能源分析师", "Energy analyst" },
            new[] { "能源与环境", "Energy and environment", "环境部", "Environment", "环境工程师", "Environmental engineer" },
            new[] { "农业与食品", "Agriculture and food", "农务部", "Farm operations", "农艺师", "Agronomist" },
            new[] { "农业与食品", "Agriculture and food", "食品安全部", "Food safety", "食品安全专员", "Food safety specialist" },
            new[] { "法律服务", "Legal services", "法务部", "Legal", "法务专员", "Legal specialist" },
            new[] { "公共服务", "Public services", "公共事务部", "Public affairs", "公共事务专员", "Public affairs officer" },
            new[] { "电信与通信", "Telecommunications", "网络部", "Network operations", "网络工程师", "Network engineer" },
            new[] { "电信与通信", "Telecommunications", "客户支持部", "Customer support", "技术支持专员", "Technical support specialist" },
            new[] { "科学研究", "Scientific research", "实验室", "Laboratory", "实验室技术员", "Laboratory technician" },
            new[] { "交通运输", "Transportation", "运营部", "Operations", "运输运营专员", "Transport operations specialist" },
            new[] { "交通运输", "Transportation", "安全部", "Safety", "安全检查员", "Safety inspector" },
        };

        internal static CareerProfile[] Profiles(string locale) => Rows.Select(row => new CareerProfile(row[locale == "zh-CN" ? 0 : 1], row[locale == "zh-CN" ? 2 : 3], row[locale == "zh-CN" ? 4 : 5])).ToArray();
        internal static CareerProfile[] Snapshot(MockContext context, CareerProfileOptions options)
        {
            context.Check(); var source = options?.Profiles ?? Profiles(context.Locale);
            if (options?.Profiles != null) DomainGuard.Count(context, source.Count);
            var candidates = new List<CareerProfile>();
            foreach (var item in source)
            {
                context.Check(); var profile = (item ?? throw new ArgumentException("职业组合不能包含 null。", nameof(options))).Copy();
                DomainGuard.Text(context, profile.Industry); DomainGuard.Text(context, profile.Department); DomainGuard.Text(context, profile.Job);
                if ((options?.Industry == null || profile.Industry == options.Industry) && (options?.Department == null || profile.Department == options.Department)) candidates.Add(profile);
            }
            if (candidates.Count == 0) throw new ArgumentException("筛选条件没有对应的模拟职业样本。", nameof(options));
            return candidates.ToArray();
        }
        internal static string[] Industries(string locale) => Profiles(locale).Select(x => x.Industry).Distinct(StringComparer.Ordinal).ToArray();
        internal static string[] Departments(string locale, string industry) => Profiles(locale).Where(x => industry == null || x.Industry == industry).Select(x => x.Department).Distinct(StringComparer.Ordinal).ToArray();
        internal static CareerProfile Pick(MockContext context, string industry, string department)
        {
            var candidates = Profiles(context.Locale).Where(x => (industry == null || x.Industry == industry) && (department == null || x.Department == department)).ToArray();
            if (candidates.Length == 0) throw new ArgumentException("指定行业和部门没有对应的模拟职业样本。");
            var selected=context.Collections.Pick(candidates);
            DomainGuard.Text(context,selected.Industry);DomainGuard.Text(context,selected.Department);DomainGuard.Text(context,selected.Job);
            return selected;
        }
    }

    /// <summary>表示可配置的多级商品分类节点及其关联样本。</summary>
    public sealed class ProductCategoryDefinition
    {
        /// <summary>获取稳定分类代码。</summary>
        public string Code { get; }
        /// <summary>获取父分类代码；根分类为 null。</summary>
        public string ParentCode { get; }
        /// <summary>获取中文分类名称。</summary>
        public string ChineseName { get; }
        /// <summary>获取英文分类名称。</summary>
        public string EnglishName { get; }
        /// <summary>获取适用于该分类的材质样本。</summary>
        public IReadOnlyList<string> Materials { get; }
        /// <summary>获取适用于该分类的规格样本。</summary>
        public IReadOnlyList<string> Specifications { get; }
        /// <summary>获取适用于该分类的计量单位样本。</summary>
        public IReadOnlyList<string> Units { get; }
        /// <summary>获取与该分类关联的模拟品牌。</summary>
        public IReadOnlyList<string> Brands { get; }
        /// <summary>指示此节点是否可直接生成商品。</summary>
        public bool IsSelectable => Materials.Count != 0 && Specifications.Count != 0 && Units.Count != 0 && Brands.Count != 0;

        /// <summary>创建分类节点；父节点可将所有商品样本列表设为空。</summary>
        public ProductCategoryDefinition(string code, string parentCode, string chineseName, string englishName,
            IEnumerable<string> materials = null, IEnumerable<string> specifications = null, IEnumerable<string> units = null, IEnumerable<string> brands = null)
        {
            Code = Required(code, nameof(code)); ParentCode = string.IsNullOrWhiteSpace(parentCode) ? null : parentCode;
            ChineseName = Required(chineseName, nameof(chineseName)); EnglishName = Required(englishName, nameof(englishName));
            Materials = CopyList(materials, nameof(materials)); Specifications = CopyList(specifications, nameof(specifications));
            Units = CopyList(units, nameof(units)); Brands = CopyList(brands, nameof(brands));
            bool any = Materials.Count + Specifications.Count + Units.Count + Brands.Count != 0;
            if (any && !IsSelectable) throw new ArgumentException("可选分类必须同时提供材质、规格、单位和品牌样本。");
        }

        internal ProductCategoryDefinition Copy() => new ProductCategoryDefinition(Code, ParentCode, ChineseName, EnglishName, Materials, Specifications, Units, Brands);
        internal ProductCategoryDefinition Localized(string locale) => new ProductCategoryDefinition(Code, ParentCode, ChineseName, EnglishName,
            Materials.Select(x => ProductCatalog.Localize(x, locale)), Specifications.Select(x => ProductCatalog.Localize(x, locale)),
            Units.Select(x => ProductCatalog.Localize(x, locale)), Brands.Select(x => ProductCatalog.Localize(x, locale)));
        internal string DisplayName(string locale) => locale == "zh-CN" ? ChineseName : EnglishName;
        private static string Required(string value, string name) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("分类字段不能为空。", name) : value;
        private static IReadOnlyList<string> CopyList(IEnumerable<string> values, string name)
        {
            var copy = (values ?? Array.Empty<string>()).ToArray();
            if (copy.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("样本项不能为空。", name);
            return Array.AsReadOnly(copy);
        }
    }

    /// <summary>配置商品分类和品牌过滤条件；分类快照在创建生成器时冻结。</summary>
    public sealed class ProductSampleOptions
    {
        /// <summary>获取或设置自定义分类树；为空时使用内置模拟分类。</summary>
        public IReadOnlyList<ProductCategoryDefinition> Categories { get; set; }
        /// <summary>获取或设置分类代码筛选；指定父分类时包含其后代分类。</summary>
        public string CategoryCode { get; set; }
        /// <summary>获取或设置品牌筛选；仅从该品牌实际关联的分类生成。</summary>
        public string Brand { get; set; }
    }

    /// <summary>包含同一分类路径、规格、材质、单位及品牌的商品样本。</summary>
    public sealed class ProductSample
    {
        /// <summary>获取从根节点到末级分类的只读路径。</summary>
        public IReadOnlyList<ProductCategoryDefinition> CategoryPath { get; }
        /// <summary>获取末级分类代码。</summary>
        public string CategoryCode { get; }
        /// <summary>获取末级分类的当前语言名称。</summary>
        public string CategoryName { get; }
        /// <summary>获取关联材质。</summary>
        public string Material { get; }
        /// <summary>获取关联规格。</summary>
        public string Specification { get; }
        /// <summary>获取关联单位。</summary>
        public string Unit { get; }
        /// <summary>获取分类关联品牌。</summary>
        public string Brand { get; }
        internal ProductSample(IReadOnlyList<ProductCategoryDefinition> path, string locale, string material, string specification, string unit, string brand)
        { CategoryPath = path; CategoryCode = path[path.Count - 1].Code; CategoryName = path[path.Count - 1].DisplayName(locale); Material = material; Specification = specification; Unit = unit; Brand = brand; }
    }

    internal sealed class ProductSamplePlan
    {
        internal readonly ProductCategoryDefinition[] Categories;
        internal readonly ProductChoice[] Choices;
        internal readonly string SelectedBrand;
        internal ProductSamplePlan(ProductCategoryDefinition[] categories, ProductChoice[] choices, string selectedBrand) { Categories = categories; Choices = choices; SelectedBrand = selectedBrand; }
    }

    internal sealed class ProductChoice
    {
        internal readonly ProductCategoryDefinition Category;
        internal readonly IReadOnlyList<ProductCategoryDefinition> Path;
        internal readonly string[] Materials;
        internal readonly string[] Specifications;
        internal readonly string[] Units;
        internal readonly string[] Brands;
        internal ProductChoice(ProductCategoryDefinition category, ProductCategoryDefinition[] path)
        {
            Category = category; Path = Array.AsReadOnly(path);
            Materials = category.Materials.ToArray(); Specifications = category.Specifications.ToArray();
            Units = category.Units.ToArray(); Brands = category.Brands.ToArray();
        }
    }

    internal static class ProductCatalog
    {
        private static readonly ProductCategoryDefinition[] BuiltIn = CreateBuiltIn();
        internal static ProductSamplePlan Snapshot(MockContext context, ProductSampleOptions options)
        {
            context.Check(); bool useBuiltIn = options?.Categories == null;
            var source = options?.Categories ?? BuiltIn;
            if (options?.Categories != null) DomainGuard.Count(context, source.Count);
            DomainGuard.Text(context, options?.CategoryCode ?? ""); DomainGuard.Text(context, options?.Brand ?? "");
            var categories = new ProductCategoryDefinition[source.Count];
            for (int i = 0; i < categories.Length; i++)
            {
                context.Check(); var input = source[i] ?? throw new ArgumentException("分类列表不能包含 null。", nameof(options));
                if (!useBuiltIn)
                {
                    DomainGuard.Count(context, input.Materials.Count); DomainGuard.Count(context, input.Specifications.Count);
                    DomainGuard.Count(context, input.Units.Count); DomainGuard.Count(context, input.Brands.Count);
                }
                var category = input.Copy();
                if (useBuiltIn) category = category.Localized(context.Locale);
                categories[i] = category;
                DomainGuard.Text(context, category.Code); DomainGuard.Text(context, category.ParentCode ?? "");
                DomainGuard.Text(context, category.ChineseName); DomainGuard.Text(context, category.EnglishName);
                foreach (var value in category.Materials.Concat(category.Specifications).Concat(category.Units).Concat(category.Brands)) { context.Check(); DomainGuard.Text(context, value); }
            }
            ValidateTree(context, categories);
            string filterCode = options?.CategoryCode;
            string filterBrand = options?.Brand;
            if (filterCode != null && !categories.Any(x => x.Code == filterCode)) throw new ArgumentException("分类筛选代码不存在。", nameof(options));
            if (filterBrand != null && string.IsNullOrWhiteSpace(filterBrand)) throw new ArgumentException("品牌筛选不能为空白。", nameof(options));
            var lookup = categories.ToDictionary(x => x.Code, StringComparer.Ordinal);
            var choices = new List<ProductChoice>();
            foreach (var category in categories)
            {
                context.Check();
                if (!category.IsSelectable) continue;
                if (filterBrand != null && !category.Brands.Contains(filterBrand, StringComparer.Ordinal)) continue;
                var path = PathOf(lookup, category);
                if (filterCode == null || path.Any(x => x.Code == filterCode))
                {
                    choices.Add(new ProductChoice(category, path));
                }
            }
            if (choices.Count == 0) throw new ArgumentException("筛选条件没有可生成的商品分类和品牌组合。", nameof(options));
            return new ProductSamplePlan(categories, choices.ToArray(), filterBrand);
        }

        internal static ProductSample Generate(MockContext context, ProductSamplePlan plan)
        {
            context.Check();
            var choice = context.ForField("category").Collections.Pick(plan.Choices);
            var leaf = choice.Category;
            var path = choice.Path;
            string material = context.ForField("material").Collections.Pick(choice.Materials);
            string specification = context.ForField("specification").Collections.Pick(choice.Specifications);
            string unit = context.ForField("unit").Collections.Pick(choice.Units);
            string brand = plan.SelectedBrand ?? context.ForField("brand").Collections.Pick(choice.Brands);
            foreach (var part in path) { DomainGuard.Text(context, part.Code); DomainGuard.Text(context, part.DisplayName(context.Locale)); }
            DomainGuard.Text(context, material); DomainGuard.Text(context, specification); DomainGuard.Text(context, unit); DomainGuard.Text(context, brand);
            return new ProductSample(path, context.Locale, material, specification, unit, brand);
        }

        internal static string[] Brands(MockContext context, string categoryCode)
        {
            var plan = Snapshot(context, new ProductSampleOptions { CategoryCode = categoryCode });
            return plan.Choices.SelectMany(x => x.Brands).Distinct(StringComparer.Ordinal).ToArray();
        }

        internal static string Localize(string value, string locale) => locale == "en-US" && EnglishValues.TryGetValue(value, out var english) ? english : value;

        private static readonly Dictionary<string, string> EnglishValues = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["纸张"]="Paper", ["再生纸"]="Recycled paper", ["塑料"]="Plastic", ["金属"]="Metal", ["帆布"]="Canvas", ["聚酯纤维"]="Polyester",
            ["陶瓷"]="Ceramic", ["玻璃"]="Glass", ["不锈钢"]="Stainless steel", ["棉"]="Cotton", ["尼龙"]="Nylon", ["实木"]="Solid wood",
            ["竹材"]="Bamboo", ["钢木"]="Steel and wood", ["网布"]="Mesh", ["铸铁"]="Cast iron", ["铝合金"]="Aluminum alloy", ["橡胶"]="Rubber", ["合成皮革"]="Synthetic leather",
            ["小号"]="Small", ["中号"]="Medium", ["大号"]="Large", ["3 号"]="Size 3", ["5 号"]="Size 5", ["7 号"]="Size 7", ["12 支装"]="Pack of 12", ["87 键"]="87-key", ["104 键"]="104-key", ["有线"]="Wired", ["无线"]="Wireless", ["静音"]="Silent", ["入耳式"]="In-ear", ["头戴式"]="Over-ear", ["标准款"]="Standard", ["可调节"]="Adjustable", ["带扶手"]="With armrests",
            ["本"]="Piece", ["支"]="Piece", ["盒"]="Box", ["个"]="Piece", ["件"]="Piece", ["只"]="Piece", ["张"]="Piece", ["把"]="Piece", ["口"]="Piece", ["副"]="Pair",
            ["青禾文具（模拟）"]="Greenfield Stationery Example", ["晨光里（模拟）"]="Morning Lane Stationery Example", ["远山创意（模拟）"]="Farhill Creative Example",
            ["青禾生活（模拟）"]="Greenfield Living Example", ["山岚日用（模拟）"]="Misty Ridge Homeware Example", ["远山户外（模拟）"]="Farhill Outdoor Example",
            ["云帆数码（模拟）"]="Cloud Sail Digital Example", ["星河设备（模拟）"]="Star River Devices Example", ["蓝桥科技（模拟）"]="Bluebridge Technology Example",
            ["青禾衣坊（模拟）"]="Greenfield Apparel Example", ["晨光衣舍（模拟）"]="Morning Lane Apparel Example", ["山岚家居（模拟）"]="Misty Ridge Home Example",
            ["青禾木作（模拟）"]="Greenfield Woodworks Example", ["云帆家具（模拟）"]="Cloud Sail Furniture Example", ["青禾厨具（模拟）"]="Greenfield Cookware Example", ["蓝桥运动（模拟）"]="Bluebridge Sports Example"
        };

        private static ProductCategoryDefinition[] PathOf(IReadOnlyDictionary<string, ProductCategoryDefinition> categories, ProductCategoryDefinition leaf)
        {
            var path = new List<ProductCategoryDefinition>(); var current = leaf;
            while (current != null)
            {
                path.Add(current);
                current = current.ParentCode == null ? null : categories[current.ParentCode];
            }
            path.Reverse(); return path.ToArray();
        }

        private static void ValidateTree(MockContext context, ProductCategoryDefinition[] categories)
        {
            if (categories.Length == 0) throw new ArgumentException("分类树不能为空。");
            var byCode = new Dictionary<string, ProductCategoryDefinition>(StringComparer.Ordinal);
            foreach (var category in categories) { context.Check(); if (byCode.ContainsKey(category.Code)) throw new ArgumentException("分类代码必须唯一。"); byCode.Add(category.Code, category); }
            foreach (var category in categories)
            {
                context.Check();
                if (category.ParentCode != null && !byCode.ContainsKey(category.ParentCode)) throw new ArgumentException("分类父级代码不存在：" + category.ParentCode);
                var visited = new HashSet<string>(StringComparer.Ordinal); var current = category;
                while (current != null)
                {
                    context.Check();
                    if (!visited.Add(current.Code)) throw new ArgumentException("分类树不能包含循环父级关系：" + category.Code);
                    if (visited.Count > context.Budget.MaxDepth) throw new MockLimitException(nameof(MockLimits.MaxDepth), context.Budget.MaxDepth);
                    current = current.ParentCode == null ? null : byCode[current.ParentCode];
                }
            }
        }

        private static ProductCategoryDefinition[] CreateBuiltIn() => new[]
        {
            Node("daily", null, "日用百货", "Daily goods"),
            Node("stationery", "daily", "文具", "Stationery"),
            Leaf("notebook", "stationery", "笔记本", "Notebook", new[]{"纸张","再生纸"}, new[]{"A5","A4","B5"}, new[]{"本"}, new[]{"青禾文具（模拟）","晨光里（模拟）"}),
            Leaf("writing", "stationery", "书写工具", "Writing tools", new[]{"塑料","金属"}, new[]{"0.5 mm","0.7 mm","12 支装"}, new[]{"支","盒"}, new[]{"青禾文具（模拟）","远山创意（模拟）"}),
            Leaf("bags", "daily", "收纳包袋", "Bags", new[]{"帆布","聚酯纤维"}, new[]{"小号","中号","大号"}, new[]{"个"}, new[]{"远山创意（模拟）","晨光里（模拟）"}),
            Node("drinkware", "daily", "饮具", "Drinkware"),
            Leaf("mugs", "drinkware", "马克杯", "Mugs", new[]{"陶瓷","玻璃"}, new[]{"250 ml","350 ml","450 ml"}, new[]{"个"}, new[]{"青禾生活（模拟）","山岚日用（模拟）"}),
            Leaf("bottles", "drinkware", "水壶", "Bottles", new[]{"不锈钢","玻璃","塑料"}, new[]{"350 ml","500 ml","750 ml"}, new[]{"个"}, new[]{"山岚日用（模拟）","远山户外（模拟）"}),
            Node("electronics", null, "电子产品", "Electronics"),
            Node("computing", "electronics", "电脑配件", "Computer accessories"),
            Leaf("keyboards", "computing", "键盘", "Keyboards", new[]{"塑料","铝合金"}, new[]{"87 键","104 键","无线"}, new[]{"把"}, new[]{"云帆数码（模拟）","星河设备（模拟）"}),
            Leaf("mice", "computing", "鼠标", "Mice", new[]{"塑料","铝合金"}, new[]{"有线","无线","静音"}, new[]{"只"}, new[]{"云帆数码（模拟）","星河设备（模拟）"}),
            Leaf("headphones", "electronics", "耳机", "Headphones", new[]{"塑料","铝合金"}, new[]{"入耳式","头戴式","无线"}, new[]{"副"}, new[]{"星河设备（模拟）","蓝桥科技（模拟）"}),
            Node("apparel", null, "服装", "Apparel"),
            Node("tops", "apparel", "上装", "Tops"),
            Leaf("shirts", "tops", "T 恤", "T-shirts", new[]{"棉","聚酯纤维"}, new[]{"S","M","L","XL"}, new[]{"件"}, new[]{"青禾衣坊（模拟）","晨光衣舍（模拟）"}),
            Leaf("jackets", "tops", "外套", "Jackets", new[]{"棉","尼龙"}, new[]{"S","M","L","XL"}, new[]{"件"}, new[]{"晨光衣舍（模拟）","远山户外（模拟）"}),
            Node("home", null, "家居", "Home"),
            Leaf("desks", "home", "书桌", "Desks", new[]{"实木","竹材","钢木"}, new[]{"100 x 60 cm","120 x 60 cm","140 x 70 cm"}, new[]{"张"}, new[]{"山岚家居（模拟）","青禾木作（模拟）"}),
            Leaf("chairs", "home", "椅子", "Chairs", new[]{"实木","网布","塑料"}, new[]{"标准款","可调节","带扶手"}, new[]{"把"}, new[]{"山岚家居（模拟）","云帆家具（模拟）"}),
            Node("kitchen", "home", "厨房用品", "Kitchenware"),
            Leaf("pans", "kitchen", "锅具", "Cookware", new[]{"不锈钢","铸铁","铝合金"}, new[]{"20 cm","24 cm","28 cm"}, new[]{"口"}, new[]{"青禾厨具（模拟）","山岚日用（模拟）"}),
            Node("sports", null, "运动户外", "Sports and outdoors"),
            Leaf("balls", "sports", "球类", "Balls", new[]{"橡胶","合成皮革"}, new[]{"3 号","5 号","7 号"}, new[]{"个"}, new[]{"远山户外（模拟）","蓝桥运动（模拟）"}),
        };

        private static ProductCategoryDefinition Node(string code, string parent, string zh, string en) => new ProductCategoryDefinition(code, parent, zh, en);
        private static ProductCategoryDefinition Leaf(string code, string parent, string zh, string en, string[] materials, string[] specs, string[] units, string[] brands) => new ProductCategoryDefinition(code, parent, zh, en, materials, specs, units, brands);
    }

    public readonly partial struct PeopleData
    {
        /// <summary>按可选行业与部门生成一个匹配的职业组合。</summary>
        public CareerProfile Career(CareerProfileOptions options = null)
        { var candidates = CareerCatalog.Snapshot(_c, options); return _c.Collections.Pick(candidates); }

        /// <summary>创建按行业和部门筛选的职业组合生成器；选项及其候选列表在创建时复制。</summary>
        public MockGenerator<CareerProfile> Careers(CareerProfileOptions options = null)
        {
            _c.Check(); var snapshot = CareerCatalog.Snapshot(_c, options);
            return _c.Generator(c => c.ForField("career").Collections.Pick(snapshot), "career-profile");
        }

        /// <summary>按可选行业与部门筛选职业名称。</summary>
        public string Job(string industry, string department) => CareerCatalog.Pick(_c, industry, department).Job;
    }

    public readonly partial struct CompanyData
    {
        /// <summary>获取指定行业对应的部门样本；行业名称必须来自当前语言的内置模拟样本。</summary>
        public IReadOnlyList<string> Departments(string industry)
        {
            _c.Check(); if (string.IsNullOrWhiteSpace(industry)) throw new ArgumentException("行业名称不能为空。", nameof(industry));
            var values = CareerCatalog.Departments(_c.Locale, industry);
            if (values.Length == 0) throw new ArgumentException("没有该行业的部门样本。", nameof(industry));
            DomainGuard.Count(_c, values.Length);
            return Array.AsReadOnly(values);
        }

        /// <summary>获取当前语言下的内置模拟行业样本。</summary>
        public IReadOnlyList<string> Industries()
        { _c.Check(); var values = CareerCatalog.Industries(_c.Locale); DomainGuard.Count(_c, values.Length); return Array.AsReadOnly(values); }
    }

    public readonly partial struct CommerceData
    {
        /// <summary>按可选分类代码或品牌立即生成一条关联一致的商品样本。</summary>
        public ProductSample Product(ProductSampleOptions options = null)
        { var snapshot = ProductCatalog.Snapshot(_c, options); return ProductCatalog.Generate(_c, snapshot); }

        /// <summary>创建商品样本生成器；分类树、筛选条件及关联品牌在创建时冻结。</summary>
        public MockGenerator<ProductSample> Products(ProductSampleOptions options = null)
        {
            var snapshot = ProductCatalog.Snapshot(_c, options);
            return _c.Generator(c => ProductCatalog.Generate(c, snapshot), "product-sample");
        }

        /// <summary>按分类筛选关联品牌；传入父分类时合并其后代可选分类的品牌。</summary>
        public IReadOnlyList<string> Brands(string categoryCode)
        { _c.Check(); var values = ProductCatalog.Brands(_c, categoryCode); DomainGuard.Count(_c, values.Length); return Array.AsReadOnly(values); }

        /// <summary>从指定分类或其后代分类已关联的品牌中抽取一个样本。</summary>
        public string Brand(string categoryCode)
        { _c.Check(); return _c.Collections.Pick(ProductCatalog.Brands(_c, categoryCode)); }

        /// <summary>按筛选条件生成末级分类、材质、规格和单位一致的分类样本。</summary>
        public ProductCategory Category(ProductSampleOptions options)
        { var sample = Product(options); return new ProductCategory(sample.CategoryCode, sample.CategoryName, sample.Material, sample.Specification, sample.Unit); }

        /// <summary>获取当前语言的分类树快照；返回的分类节点及其样本列表均为只读。</summary>
        public IReadOnlyList<ProductCategoryDefinition> Categories(ProductSampleOptions options = null)
        {
            var plan = ProductCatalog.Snapshot(_c, options);
            DomainGuard.Count(_c, plan.Categories.Length);
            return Array.AsReadOnly(plan.Categories.Select(x => x.Copy()).ToArray());
        }
    }

    /// <summary>描述可选的客户端浏览器、系统与设备类型过滤条件。</summary>
    public sealed class ClientProfileOptions
    {
        /// <summary>获取或设置浏览器名称；比较采用区分大小写的精确匹配。</summary>
        public string Browser { get; set; }
        /// <summary>获取或设置操作系统名称；比较采用区分大小写的精确匹配。</summary>
        public string OperatingSystem { get; set; }
        /// <summary>获取或设置设备类型。</summary>
        public ClientDeviceType? DeviceType { get; set; }
        /// <summary>获取或设置自定义客户端集合；为空时使用内置模拟样本。</summary>
        public IReadOnlyList<ClientProfile> Profiles { get; set; }
    }

    internal sealed class ClientProfilePlan
    {
        internal readonly ClientProfile[] Profiles;
        internal ClientProfilePlan(ClientProfile[] profiles) { Profiles = profiles; }
    }

    internal static class ClientSamples
    {
        private static readonly ClientProfile[] BuiltIn =
        {
            new ClientProfile("Chrome","Windows","Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36"),
            new ClientProfile("Firefox","Linux","Mozilla/5.0 (X11; Linux x86_64; rv:130.0) Gecko/20100101 Firefox/130.0"),
            new ClientProfile("Safari","macOS","Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Safari/605.1.15"),
            new ClientProfile("Edge","Windows","Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36 Edg/130.0.0.0"),
            new ClientProfile("Chrome","Android","Mozilla/5.0 (Linux; Android 15; Mobile) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Mobile Safari/537.36",ClientDeviceType.Mobile),
            new ClientProfile("Safari","iOS","Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1",ClientDeviceType.Mobile),
            new ClientProfile("Firefox","Android","Mozilla/5.0 (Android 15; Mobile; rv:130.0) Gecko/130.0 Firefox/130.0",ClientDeviceType.Mobile),
            new ClientProfile("Safari","iPadOS","Mozilla/5.0 (iPad; CPU OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1",ClientDeviceType.Tablet),
            new ClientProfile("Chrome","Android","Mozilla/5.0 (Linux; Android 15; Tablet) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36",ClientDeviceType.Tablet),
        };

        internal static ClientProfilePlan Snapshot(MockContext context, ClientProfileOptions options)
        {
            context.Check(); var source = options?.Profiles ?? BuiltIn;
            if (options?.Profiles != null) DomainGuard.Count(context, source.Count);
            var profiles = new List<ClientProfile>();
            foreach (var candidate in source)
            {
                context.Check();
                if (candidate == null) throw new ArgumentException("客户端配置不能包含 null。", nameof(options));
                var profile = new ClientProfile(candidate.Browser, candidate.OperatingSystem, candidate.UserAgent, candidate.DeviceType);
                DomainGuard.Text(context, profile.Browser); DomainGuard.Text(context, profile.OperatingSystem); DomainGuard.Text(context, profile.UserAgent);
                if (options != null && options.Browser != null && profile.Browser != options.Browser) continue;
                if (options != null && options.OperatingSystem != null && profile.OperatingSystem != options.OperatingSystem) continue;
                if (options != null && options.DeviceType.HasValue && profile.DeviceType != options.DeviceType.Value) continue;
                profiles.Add(profile);
            }
            if (profiles.Count == 0) throw new ArgumentException("筛选条件没有对应的客户端样本。", nameof(options));
            return new ClientProfilePlan(profiles.ToArray());
        }

        internal static ClientProfile Pick(MockContext context, ClientProfileOptions options)
        {
            var plan = Snapshot(context, options);
            var profile = context.Collections.Pick(plan.Profiles);
            DomainGuard.Text(context, profile.Browser); DomainGuard.Text(context, profile.OperatingSystem); DomainGuard.Text(context, profile.UserAgent);
            return profile;
        }
    }

    public readonly partial struct NetworkData
    {
        /// <summary>按浏览器、操作系统及设备类型筛选并生成客户端资料。</summary>
        public ClientProfile Client(ClientProfileOptions options) => ClientSamples.Pick(_context, options);

        /// <summary>创建客户端资料生成器；候选资料及筛选条件在创建时复制。</summary>
        public MockGenerator<ClientProfile> ClientProfiles(ClientProfileOptions options = null)
        {
            var snapshot = ClientSamples.Snapshot(_context, options);
            return _context.Generator(c =>
            {
                var profile = c.Collections.Pick(snapshot.Profiles);
                DomainGuard.Text(c, profile.Browser); DomainGuard.Text(c, profile.OperatingSystem); DomainGuard.Text(c, profile.UserAgent);
                return profile;
            }, "client-profile");
        }
    }

    /// <summary>CSS 标准命名色的常见色系。</summary>
    public enum ColorFamily
    {
        /// <summary>红色。</summary>
        Red,
        /// <summary>橙色。</summary>
        Orange,
        /// <summary>黄色。</summary>
        Yellow,
        /// <summary>绿色。</summary>
        Green,
        /// <summary>青色。</summary>
        Cyan,
        /// <summary>蓝色。</summary>
        Blue,
        /// <summary>紫色。</summary>
        Purple,
        /// <summary>粉色。</summary>
        Pink,
        /// <summary>棕色。</summary>
        Brown,
        /// <summary>灰色。</summary>
        Gray,
        /// <summary>白色。</summary>
        White,
        /// <summary>黑色。</summary>
        Black
    }

    internal static class CssNamedColors
    {
        private static readonly string[] Values = ("aliceblue F0F8FF|antiquewhite FAEBD7|aqua 00FFFF|aquamarine 7FFFD4|azure F0FFFF|beige F5F5DC|bisque FFE4C4|black 000000|blanchedalmond FFEBCD|blue 0000FF|blueviolet 8A2BE2|brown A52A2A|burlywood DEB887|cadetblue 5F9EA0|chartreuse 7FFF00|chocolate D2691E|coral FF7F50|cornflowerblue 6495ED|cornsilk FFF8DC|crimson DC143C|cyan 00FFFF|darkblue 00008B|darkcyan 008B8B|darkgoldenrod B8860B|darkgray A9A9A9|darkgreen 006400|darkgrey A9A9A9|darkkhaki BDB76B|darkmagenta 8B008B|darkolivegreen 556B2F|darkorange FF8C00|darkorchid 9932CC|darkred 8B0000|darksalmon E9967A|darkseagreen 8FBC8F|darkslateblue 483D8B|darkslategray 2F4F4F|darkslategrey 2F4F4F|darkturquoise 00CED1|darkviolet 9400D3|deeppink FF1493|deepskyblue 00BFFF|dimgray 696969|dimgrey 696969|dodgerblue 1E90FF|firebrick B22222|floralwhite FFFAF0|forestgreen 228B22|fuchsia FF00FF|gainsboro DCDCDC|ghostwhite F8F8FF|gold FFD700|goldenrod DAA520|gray 808080|green 008000|greenyellow ADFF2F|grey 808080|honeydew F0FFF0|hotpink FF69B4|indianred CD5C5C|indigo 4B0082|ivory FFFFF0|khaki F0E68C|lavender E6E6FA|lavenderblush FFF0F5|lawngreen 7CFC00|lemonchiffon FFFACD|lightblue ADD8E6|lightcoral F08080|lightcyan E0FFFF|lightgoldenrodyellow FAFAD2|lightgray D3D3D3|lightgreen 90EE90|lightgrey D3D3D3|lightpink FFB6C1|lightsalmon FFA07A|lightseagreen 20B2AA|lightskyblue 87CEFA|lightslategray 778899|lightslategrey 778899|lightsteelblue B0C4DE|lightyellow FFFFE0|lime 00FF00|limegreen 32CD32|linen FAF0E6|magenta FF00FF|maroon 800000|mediumaquamarine 66CDAA|mediumblue 0000CD|mediumorchid BA55D3|mediumpurple 9370DB|mediumseagreen 3CB371|mediumslateblue 7B68EE|mediumspringgreen 00FA9A|mediumturquoise 48D1CC|mediumvioletred C71585|midnightblue 191970|mintcream F5FFFA|mistyrose FFE4E1|moccasin FFE4B5|navajowhite FFDEAD|navy 000080|oldlace FDF5E6|olive 808000|olivedrab 6B8E23|orange FFA500|orangered FF4500|orchid DA70D6|palegoldenrod EEE8AA|palegreen 98FB98|paleturquoise AFEEEE|palevioletred DB7093|papayawhip FFEFD5|peachpuff FFDAB9|peru CD853F|pink FFC0CB|plum DDA0DD|powderblue B0E0E6|purple 800080|rebeccapurple 663399|red FF0000|rosybrown BC8F8F|royalblue 4169E1|saddlebrown 8B4513|salmon FA8072|sandybrown F4A460|seagreen 2E8B57|seashell FFF5EE|sienna A0522D|silver C0C0C0|skyblue 87CEEB|slateblue 6A5ACD|slategray 708090|slategrey 708090|snow FFFAFA|springgreen 00FF7F|steelblue 4682B4|tan D2B48C|teal 008080|thistle D8BFD8|tomato FF6347|turquoise 40E0D0|violet EE82EE|wheat F5DEB3|white FFFFFF|whitesmoke F5F5F5|yellow FFFF00|yellowgreen 9ACD32").Split('|');
        private static readonly MockColor[] Palette = Values.Select(Parse).ToArray();

        internal static IReadOnlyList<MockColor> All => Array.AsReadOnly(Palette);
        internal static MockColor[] ForFamily(ColorFamily? family)
        {
            if (family.HasValue && !Enum.IsDefined(typeof(ColorFamily), family.Value)) throw new ArgumentOutOfRangeException(nameof(family));
            return family.HasValue ? Palette.Where(x => Family(x) == family.Value).ToArray() : (MockColor[])Palette.Clone();
        }

        private static MockColor Parse(string value)
        {
            var pair = value.Split(' '); var hex = pair[1];
            return new MockColor(byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture), byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture), byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture), 1, pair[0]);
        }

        private static ColorFamily Family(MockColor color)
        {
            if (color.Lightness <= 0.12) return ColorFamily.Black;
            if (color.Lightness >= 0.92) return ColorFamily.White;
            if (color.Saturation < 0.12) return ColorFamily.Gray;
            if ((color.Hue < 15 || color.Hue >= 345) && color.Lightness > 0.72) return ColorFamily.Pink;
            if ((color.Hue < 15 || color.Hue >= 345)) return ColorFamily.Red;
            if (color.Hue < 45) return color.Lightness < 0.5 ? ColorFamily.Brown : ColorFamily.Orange;
            if (color.Hue < 65) return ColorFamily.Yellow;
            if (color.Hue < 170) return ColorFamily.Green;
            if (color.Hue < 200) return ColorFamily.Cyan;
            if (color.Hue < 260) return ColorFamily.Blue;
            if (color.Hue < 285) return ColorFamily.Purple;
            return ColorFamily.Pink;
        }
    }

    public readonly partial struct ColorData
    {
        /// <summary>从完整 CSS 命名色样本中抽取颜色；色名使用标准英文名称。</summary>
        public MockColor NamedColor(ColorFamily? family = null)
        {
            _c.Check(); var choices = CssNamedColors.ForFamily(family);
            if (choices.Length == 0) throw new ArgumentException("该色系没有命名颜色样本。", nameof(family));
            var selected=_c.Collections.Pick(choices);DomainGuard.Text(_c,selected.Name);return selected;
        }

        /// <summary>获取完整 CSS 命名色表或指定色系的只读副本。</summary>
        public IReadOnlyList<MockColor> Palette(ColorFamily? family = null)
        { _c.Check(); var values = CssNamedColors.ForFamily(family); DomainGuard.Count(_c, values.Length); return Array.AsReadOnly(values); }
    }
}
