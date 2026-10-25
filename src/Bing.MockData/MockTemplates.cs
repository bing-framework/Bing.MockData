using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Bing.MockData.Core.Options;
using Bing.MockData.Core.Randomizers;
using Bing.MockData.Geography;
using Bing.MockData.Models;

namespace Bing.MockData
{



    /// <summary>
    /// 提供常用业务对象的可覆盖生成模板。
    /// </summary>
    /// <remarks>
    /// 模板返回对象构建器，并通过关联上下文的区域设置、预算、种子和重放状态生成数据。
    /// </remarks>
    public sealed partial class MockTemplates
    {
        /// <summary>
        /// 保存模板使用的生成上下文。
        /// </summary>
        private readonly MockContext _context;
        /// <summary>
        /// 延迟缓存中国行政区划快照中的叶节点。
        /// </summary>
        private static readonly Lazy<ChinaDivision[]> Leaves = new Lazy<ChinaDivision[]>(() => ChinaDivisions.All.Where(d => ChinaDivisions.Children(d.Code).Count == 0).ToArray());




        /// <summary>
        /// 创建与指定上下文关联的模板集合。 此构造函数仅供 <see cref="MockContext"/> 创建模板门面。 模板门面不取得或释放上下文的所有权。
        /// </summary>
        /// <param name="context">模板生成时使用的上下文。</param>
        internal MockTemplates(MockContext context) { _context = context; }




        /// <summary>
        /// 创建模拟地址的对象构建器。 中文区域按可选区划代码筛选行政区划快照中的叶节点。 英文区域生成示例美国地址，且不接受中国区划代码。
        /// </summary>
        /// <param name="divisionCode">可选的中国省、市或区县代码；省市筛选会包含其下属叶节点。</param>
        /// <returns>可进一步配置的模拟地址构建器。</returns>
        /// <exception cref="ArgumentException">区划代码未知、筛选结果为空，或英文区域提供了区划代码。</exception>
        /// <exception cref="ObjectDisposedException">关联上下文已释放。</exception>
        public MockBuilder<MockAddress> Address(string divisionCode = null)
        {
            _context.Check();
            ChinaDivision[] candidates = null;
            if (_context.Locale == "zh-CN")
            {
                if (divisionCode != null && ChinaDivisions.Find(divisionCode) == null) throw new ArgumentException("未知区划代码。", nameof(divisionCode));
                candidates = divisionCode == null ? Leaves.Value : Leaves.Value.Where(d => ChinaDivisions.Path(d.Code).Any(p => p.Code == divisionCode)).ToArray();
                if (candidates.Length == 0) throw new ArgumentException("筛选条件没有可生成地址。", nameof(divisionCode));
            }
            else if (divisionCode != null) throw new ArgumentException("英文模拟地址不支持中国区划代码。", nameof(divisionCode));
            var fixedCandidates = candidates;
            return _context.Builder<MockAddress>().ConstructUsing(c =>
            {
                if (c.Locale == "en-US")
                    return new MockAddress { CountryCode = "US", Province = "Example State", City = "Sample City", Street = "Example Street ", Detail = c.Numbers.Int(1, 999).ToString(CultureInfo.InvariantCulture) };
                var selected = c.ForField("division").Collections.Pick(fixedCandidates); var path = ChinaDivisions.Path(selected.Code);
                var province = path.First(p => p.Level == ChinaDivisionLevel.Province);
                var city = path.FirstOrDefault(p => p.Level == ChinaDivisionLevel.Prefecture);
                var county = path.FirstOrDefault(p => p.Level == ChinaDivisionLevel.County);
                return new MockAddress { CountryCode = "CN", DivisionCode = selected.Code, ProvinceCode = province.Code, Province = province.Name,
                    CityCode = city?.Code, City = city?.Name, CountyCode = county?.Code, County = county?.Name };
            }).RuleFor(a => a.Street, c => c.Locale == "zh-CN" ? "示例路" : "Example Street ")
              .RuleFor(a => a.Detail, c => c.Numbers.Int(1, 999).ToString(CultureInfo.InvariantCulture) + (c.Locale == "zh-CN" ? "号" : ""));
        }




        /// <summary>
        /// 创建模拟个人资料的对象构建器。 年龄、姓名、地址和证件字段根据上下文区域设置及参考时间生成。 仅能从包含区县代码的地址生成身份证号码和签发机关。
        /// </summary>
        /// <param name="divisionCode">可选的中国省、市或区县代码，用于筛选个人地址。</param>
        /// <param name="minAge">最小周岁，包含。</param>
        /// <param name="maxAge">最大周岁，包含。</param>
        /// <returns>可进一步配置的个人资料构建器。</returns>
        /// <exception cref="ArgumentException">地址模板的区划筛选条件无效。</exception>
        /// <exception cref="ObjectDisposedException">关联上下文已释放。</exception>
        public MockBuilder<PersonProfile> Person(string divisionCode = null, int minAge = 18, int maxAge = 80)
        {
            if (minAge < 0 || maxAge < minAge || maxAge > 120 || _context.ReferenceTime.Year <= maxAge + 1) throw new ArgumentOutOfRangeException(nameof(minAge));
            var address = Address(divisionCode).Build();
            return _context.Builder<PersonProfile>()
                .RuleFor(p => p.Gender, c => c.Numbers.Enum<PersonGender>())
                .RuleFor(p => p.BirthDate, c => c.Dates.Between(c.ReferenceTime.UtcDateTime.Date.AddYears(-maxAge - 1).AddDays(1), c.ReferenceTime.UtcDateTime.Date.AddYears(-minAge)).Date)
                .RuleFor(p => p.Age, (c, p) => Age(p.BirthDate, c.ReferenceTime.UtcDateTime.Date))
                .RuleFor(p => p.Name, (c, p) => c.Locale == "zh-CN" ? c.Text.Name() : new FullNameRandomizer(new FullNameFieldOptions
                    { Seed = c.NextSeed(), Male = p.Gender == PersonGender.Male, Female = p.Gender == PersonGender.Female }).Generate())
                .RuleFor(p => p.Address, c => address.Evaluate(c))
                .RuleFor(p => p.IdCard, (c, p) => p.Address.CountyCode == null ? null : IdCard(c, p))
                .RuleFor(p => p.IssueOrganization, (c, p) => p.IdCard == null ? null : (p.Address.County ?? p.Address.City ?? p.Address.Province) + "公安局（模拟）")
                .RuleFor(p => p.ValidFrom, c => c.ReferenceTime.UtcDateTime.Date.AddYears(-1))
                .RuleFor(p => p.ValidTo, (_, p) => p.ValidFrom.AddYears(10));
        }




        /// <summary>
        /// 创建模拟用户资料的对象构建器。 用户标识按记录索引递增，个人资料和邮箱由关联上下文生成。 邮箱域名必须是有效的域名文本。
        /// </summary>
        /// <param name="emailDomain">用于生成邮箱地址的域名；默认为 <c>example.com</c>。</param>
        /// <returns>可进一步配置的用户资料构建器。</returns>
        /// <exception cref="ArgumentException"><paramref name="emailDomain"/> 无效。</exception>
        /// <exception cref="ObjectDisposedException">关联上下文已释放。</exception>
        public MockBuilder<UserProfile> User(string emailDomain = "example.com")
        {
            TextData.ValidateDomain(emailDomain); var person = Person().Build();
            return _context.Builder<UserProfile>().RuleFor(u => u.Id, c => checked(c.RecordIndex + 1))
                .RuleFor(u => u.Person, c => person.Evaluate(c)).RuleFor(u => u.Email, c => c.Text.Email(emailDomain))
                .RuleFor(u => u.Username, c => c.People.Username()).RuleFor(u => u.Nickname, c => c.People.Nickname())
                .RuleFor(u => u.Biography, c => c.People.Biography()).RuleFor(u => u.Status, c => c.Numbers.Enum<UserStatus>())
                .RuleFor(u => u.RegisteredAt, c => c.Dates.Past(TimeSpan.FromDays(365)))
                .RuleFor(u => u.LastLoginAt, (c,u) => c.Dates.Between(u.RegisteredAt,c.ReferenceTime))
                .RuleFor(u => u.AvatarSvg, (c,u) => c.Images.Avatar(ImageData.Initials(u.Username,2)));
        }




        /// <summary>
        /// 创建模拟企业资料的对象构建器。 企业名称和地址依上下文区域设置生成，网站使用指定域名。 域名必须符合文本生成器要求的有效域名格式。
        /// </summary>
        /// <param name="domain">用于生成网站地址的域名；默认为 <c>example.com</c>。</param>
        /// <returns>可进一步配置的企业资料构建器。</returns>
        /// <exception cref="ArgumentException"><paramref name="domain"/> 无效。</exception>
        /// <exception cref="ObjectDisposedException">关联上下文已释放。</exception>
        public MockBuilder<CompanyProfile> Company(string domain = "example.com")
        {
            TextData.ValidateDomain(domain); var address = Address().Build();
            return _context.Builder<CompanyProfile>()
                .RuleFor(x => x.Name, c => c.Companies.Name())
                .RuleFor(x => x.Address, c => address.Evaluate(c)).RuleFor(x => x.Website, c => c.Network.Url(domain))
                .RuleFor(x => x.ShortName, (c,x) => c.Companies.ShortName(x.Name))
                .RuleFor(x => x.Industry, c => c.Companies.Industry())
                .RuleFor(x => x.CreditCode, (c,x) => x.Address.CountryCode == "CN" && x.Address.CountyCode != null ? c.Companies.CreditCode(x.Address.CountyCode) : null);
        }




        /// <summary>
        /// 创建模拟商品的对象构建器。 标识按记录索引递增，商品名称依上下文区域设置选择示例文本。 商品价格在配置的正数范围内生成。
        /// </summary>
        /// <returns>可进一步配置的商品构建器。</returns>
        /// <exception cref="ObjectDisposedException">关联上下文已释放。</exception>
        public MockBuilder<Product> Product(CurrencyInfo currency = null)
            => ConfiguredProduct(null,currency);

        /// <summary>按冻结的分类、规格及品牌配置创建商品模板；返回的品牌与分类来自同一组候选。</summary>
        public MockBuilder<Product> ConfiguredProduct(ProductSampleOptions options, CurrencyInfo currency = null)
        {
            currency = currency ?? new CurrencyInfo("CNY",2);
            var samples=_context.Commerce.Products(options);
            return _context.Builder<Product>().ConstructUsing(c=>
                {
                    var sample=samples.Evaluate(c.ForField("catalog"));
                    return new Product{Category=new ProductCategory(sample.CategoryCode,sample.CategoryName,sample.Material,sample.Specification,sample.Unit),Brand=sample.Brand};
                }).RuleFor(x => x.Id, c => checked(c.RecordIndex + 1))
                .RuleFor(x => x.Category, (_,x) => x.Category)
                .RuleFor(x => x.Name, (_,x) => x.Category.Name)
                .RuleFor(x => x.Brand, (_,x) => x.Brand).RuleFor(x => x.Sku, c => c.Commerce.Sku())
                .RuleFor(x => x.Stock, c => c.Numbers.Int(0,1000)).RuleFor(x => x.Barcode, c => c.Commerce.Ean13())
                .RuleFor(x => x.Description, c => c.Text.Business(BusinessTextKind.ProductDescription))
                .RuleFor(x => x.Currency, _ => currency.Code).RuleFor(x => x.CurrencyDecimalPlaces, _ => currency.DecimalPlaces)
                .RuleFor(x => x.Price, c => c.Finance.Amount(currency,1m,1000m));
        }




        /// <summary>
        /// 创建模拟订单的对象构建器。 订单从输入用户和商品中选择关联记录，并在指定范围内生成订单项。 输入列表会在调用时复制，后续修改原列表不会改变此构建器使用的数据。
        /// </summary>
        /// <param name="users">可供订单选择的非空用户列表。</param>
        /// <param name="products">可供订单选择且价格非负的非空商品列表。</param>
        /// <param name="minItems">每个订单的最少订单项数，包含此值。</param>
        /// <param name="maxItems">每个订单的最多订单项数，包含此值。</param>
        /// <param name="status">决定支付、物流、完成及退款事件的状态。</param>
        /// <returns>可进一步配置的订单构建器。</returns>
        /// <exception cref="ArgumentException">用户或商品列表为空、包含空元素，或商品价格为负数。</exception>
        /// <exception cref="ArgumentOutOfRangeException">订单项范围无效。</exception>
        /// <exception cref="MockLimitException">订单项最大数量超过上下文集合预算。</exception>
        /// <exception cref="ObjectDisposedException">关联上下文已释放。</exception>
        public MockBuilder<Order> Order(IReadOnlyList<UserProfile> users, IReadOnlyList<Product> products, int minItems = 1, int maxItems = 5, OrderStatus status = OrderStatus.Paid)
        {
            if (users == null || users.Count == 0 || users.Any(x => x == null)) throw new ArgumentException("需要非空用户集合。", nameof(users));
            if (products == null || products.Count == 0 || products.Any(x => x == null || x.Price < 0)) throw new ArgumentException("需要价格非负的商品集合。", nameof(products));
            if (!Enum.IsDefined(typeof(OrderStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            if (products.Select(p => p.Currency).Distinct(StringComparer.Ordinal).Count() != 1) throw new ArgumentException("订单商品必须使用同一币种。", nameof(products));
            var fixedUsers = users.ToArray(); var fixedProducts = products.ToArray();
            return _context.Builder<Order>().RuleFor(x => x.Id, c => checked(c.RecordIndex + 1))
                .RuleFor(x => x.User, c => c.Collections.Pick(fixedUsers))
                .Collection(x => x.Items, minItems, maxItems, c => { var product = c.ForField("Product").Collections.Pick(fixedProducts); return new OrderItem { Product = product, Quantity = c.ForField("Quantity").Numbers.Int(1, 5), UnitPrice = product.Price }; })
                .RuleFor(x => x.Currency, (_,x) => fixedProducts[0].Currency)
                .RuleFor(x => x.CurrencyDecimalPlaces, (_,x) => fixedProducts[0].CurrencyDecimalPlaces)
                .RuleFor(x => x.Number, c => c.Identifiers.Business("ORD-"))
                .RuleFor(x => x.Status, _ => status)
                .RuleFor(x => x.CreatedAt, c => c.ReferenceTime.AddDays(-30))
                .RuleFor(x => x.Discount, _ => 0m).RuleFor(x => x.Shipping, _ => 0m).RuleFor(x => x.Tax, _ => 0m)
                .RuleFor(x => x.Payments, (c,x) => PaymentRecords(c,x))
                .RuleFor(x => x.Refunds, (c,x) => RefundRecords(c,x))
                .RuleFor(x => x.ShippedAt, (c,x) => x.Status == OrderStatus.Shipped || x.Status == OrderStatus.Completed || x.Status == OrderStatus.PartiallyRefunded || x.Status == OrderStatus.Refunded ? (DateTimeOffset?)x.CreatedAt.AddDays(2) : null)
                .RuleFor(x => x.CompletedAt, (c,x) => x.Status == OrderStatus.Completed || x.Status == OrderStatus.PartiallyRefunded || x.Status == OrderStatus.Refunded ? (DateTimeOffset?)x.CreatedAt.AddDays(5) : null)
                .RuleFor(x => x.CancelledAt, (c,x) => x.Status == OrderStatus.Cancelled ? (DateTimeOffset?)x.CreatedAt.AddDays(1) : null)
                .ValidateWith(ValidateOrder);
        }




        /// <summary>
        /// 根据生日和指定日期计算周岁。 生日在当前年龄对应的周年日之后时减去一岁。 此辅助方法不访问上下文或外部状态。
        /// </summary>
        /// <param name="birthday">出生日期。</param>
        /// <param name="today">用于计算年龄的日期。</param>
        /// <returns>按日历日期计算的周岁数。</returns>
        private static int Age(DateTime birthday, DateTime today)
        { int age = today.Year - birthday.Year; return birthday.Date > today.AddYears(-age) ? age - 1 : age; }




        /// <summary>
        /// 根据个人资料生成模拟身份证号码。 号码由地址区县代码、生日、性别相关序号和校验位组成。 该值仅用于模拟数据，不代表真实身份信息。
        /// </summary>
        /// <param name="context">用于生成序号部分的字段上下文。</param>
        /// <param name="person">包含地址、生日和性别的个人资料。</param>
        /// <returns>格式化的模拟身份证号码。</returns>
        private static string IdCard(MockContext context, PersonProfile person)
        {
            int suffix = context.Numbers.Int(1, 499) * 2 + (person.Gender == PersonGender.Male ? 1 : 0);
            var body = person.Address.CountyCode + person.BirthDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + suffix.ToString("D3", CultureInfo.InvariantCulture);
            int[] weights = { 7, 9, 10, 5, 8, 4, 2, 1, 6, 3, 7, 9, 10, 5, 8, 4, 2 }; const string check = "10X98765432";
            int sum = 0; for (int i = 0; i < weights.Length; i++) sum += (body[i] - '0') * weights[i]; return body + check[sum % 11];
        }
    }
    public sealed partial class MockContext
    {



        /// <summary>
        /// 获取当前上下文的业务对象模板门面。 每次访问都会创建新的轻量模板门面。 模板门面与此上下文共享生命周期，不会拥有上下文。
        /// </summary>
        /// <returns>使用当前上下文生成常见业务对象的模板集合。</returns>
        public MockTemplates Templates => new MockTemplates(this);




        /// <summary>
        /// 使用当前上下文立即生成一个模拟地址。 可选区划代码仅在中文地址模板中用于筛选中国行政区划。 生成过程使用当前上下文的种子、预算和参考时间状态。
        /// </summary>
        /// <param name="divisionCode">可选的中国省、市或区县代码。</param>
        /// <returns>生成的模拟地址。</returns>
        /// <exception cref="ArgumentException">地址模板的区划筛选条件无效。</exception>
        /// <exception cref="ObjectDisposedException">当前上下文已释放。</exception>
        public MockAddress Address(string divisionCode = null) => Templates.Address(divisionCode).Build().Evaluate(TemplateRecord());




        /// <summary>
        /// 使用当前上下文立即生成一个模拟个人资料。 个人年龄和日期字段基于当前上下文的参考时间计算。 生成结果遵循当前上下文的区域设置和预算。
        /// </summary>
        /// <returns>生成的模拟个人资料。</returns>
        /// <exception cref="ArgumentException">地址模板筛选条件无效。</exception>
        /// <exception cref="ObjectDisposedException">当前上下文已释放。</exception>
        public PersonProfile Person() => Templates.Person().Build().Evaluate(TemplateRecord());




        /// <summary>
        /// 使用配置快照和当前字段随机源调用任意内置生成器。 输入选项会先克隆，再由当前字段上下文覆盖种子、文本预算和取消令牌。 输入选项本身不会被修改，生成器结果类型必须与 <typeparamref name="T"/> 一致。
        /// </summary>
        /// <typeparam name="T">预期的生成结果类型。</typeparam>
        /// <param name="options">要使用的生成器选项。</param>
        /// <returns>由内置生成器产生的值。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentException">选项对应的生成器结果类型与 <typeparamref name="T"/> 不一致。</exception>
        /// <exception cref="ObjectDisposedException">当前上下文已释放。</exception>
        public T Generate<T>(Core.Options.FieldOptionsBase options)
        {
            Check(); if (options == null) throw new ArgumentNullException(nameof(options));
            var copy = options.Clone(); copy.Seed = NextSeed(); copy.MaxTextLength = Budget.MaxTextLength; copy.CancellationToken = CancellationToken;
            if (!(Factories.RandomizerFactory.Create(copy) is Abstractions.Randomizers.IRandomizer<T> generator)) throw new ArgumentException("生成器的结果类型与 T 不匹配。", nameof(options));
            return generator.Generate();
        }
    }
}
