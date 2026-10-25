using System;
using System.Collections.Generic;
using System.Linq;

namespace Bing.MockData.Models
{



    /// <summary>
    /// 表示个人资料中使用的性别枚举。
    /// </summary>
    /// <remarks>
    /// 枚举值用于选择相应的模拟数据分支，不表示对现实身份信息的核验。
    /// </remarks>
    public enum PersonGender
    {
        /// <summary>
        /// 表示女性模拟值。
        /// </summary>
        Female,
        /// <summary>
        /// 表示男性模拟值。
        /// </summary>
        Male
    }




    /// <summary>
    /// 表示由国家、行政区划和地址文本组成的结构化地址。
    /// </summary>
    /// <remarks>
    /// 行政层级字段可能因区域数据而为空，街道及门牌文本仅为模拟内容。
    /// 属性值不会在设置时自动验证或规范化。
    /// </remarks>
    public sealed partial class MockAddress
    {



        /// <summary>
        /// 获取或设置地址所属国家或地区的代码。 该值由所用地址模板指定。 模型不会验证代码是否对应现实国家或地区。
        /// </summary>
        public string CountryCode { get; set; }




        /// <summary>
        /// 获取或设置完整行政区划代码。 对于中国地址，此代码指向选中的区划快照节点。 其他地区或自定义地址可以不提供此值。
        /// </summary>
        public string DivisionCode { get; set; }




        /// <summary>
        /// 获取或设置省级行政区划代码。 该代码在区划数据可用时对应地址路径中的省级节点。 模型不会自动根据名称查找代码。
        /// </summary>
        public string ProvinceCode { get; set; }




        /// <summary>
        /// 获取或设置省级行政区划名称。 该名称可以来自区划快照或其他地址模板。 空值会在 <see cref="FullAddress"/> 中省略。
        /// </summary>
        public string Province { get; set; }




        /// <summary>
        /// 获取或设置地级行政区划代码。 若地址路径不存在地级节点，此值可以为空。 模型不会自动根据名称查找代码。
        /// </summary>
        public string CityCode { get; set; }




        /// <summary>
        /// 获取或设置地级行政区划名称。 直辖市或其他不含该层级的地址可以没有此值。 空值会在 <see cref="FullAddress"/> 中省略。
        /// </summary>
        public string City { get; set; }




        /// <summary>
        /// 获取或设置区县级行政区划代码。 该值可能因区划层级或地址模板而为空。 模型不会自动根据名称查找代码。
        /// </summary>
        public string CountyCode { get; set; }




        /// <summary>
        /// 获取或设置区县级行政区划名称。 不包含区县层级的地址可以没有此值。 空值会在 <see cref="FullAddress"/> 中省略。
        /// </summary>
        public string County { get; set; }




        /// <summary>
        /// 获取或设置模拟街道文本。 该属性可能为空，也可能包含示例街道名称。 模型不验证街道文本是否真实存在。
        /// </summary>
        public string Street { get; set; }




        /// <summary>
        /// 获取或设置模拟门牌或地址补充文本。 该属性可能为空，也可能包含示例门牌信息。 模型不验证地址文本是否真实存在。
        /// </summary>
        public string Detail { get; set; }




        /// <summary>
        /// 获取由省、市、区县、街道和补充文本拼接的地址。 拼接时按层级顺序省略空值，且各部分之间不添加分隔符。 国家代码和行政区划代码不包含在此文本中。
        /// </summary>
        public string FullAddress => string.Join("", new[] { Province, City, County, Street, Detail }.Where(x => !string.IsNullOrEmpty(x)));
    }




    /// <summary>
    /// 表示包含姓名、年龄、地址和证件字段的个人资料。
    /// </summary>
    /// <remarks>
    /// 内置模板根据上下文生成字段，部分证件字段可能为空。
    /// 属性为可变字段，赋值后不会自动保持字段间一致性。
    /// </remarks>
    public sealed class PersonProfile
    {



        /// <summary>
        /// 获取或设置个人资料中的性别模拟值。 模板可以根据此值生成相关的模拟字段。 该属性允许调用方直接修改。
        /// </summary>
        public PersonGender Gender { get; set; }




        /// <summary>
        /// 获取或设置出生日期。 内置模板基于上下文参考时间生成该日期。 此属性不会自动同步年龄字段。
        /// </summary>
        public DateTime BirthDate { get; set; }




        /// <summary>
        /// 获取或设置个人年龄。 内置模板按出生日期和上下文参考日期计算周岁。 赋值后不会自动根据出生日期重新计算。
        /// </summary>
        public int Age { get; set; }




        /// <summary>
        /// 获取或设置个人姓名文本。 具体内容由区域设置和模板规则决定。 模型不验证姓名是否对应真实个人。
        /// </summary>
        public string Name { get; set; }




        /// <summary>
        /// 获取或设置个人地址。 地址可通过模板覆盖，也可以由调用方直接设置。 模型不保证地址与证件字段彼此匹配。
        /// </summary>
        public MockAddress Address { get; set; }




        /// <summary>
        /// 获取或设置模拟身份证号码。 模板可能在地址不含区县代码时不生成此值。 该字段仅供模拟用途，不表示真实身份或官方签发状态。
        /// </summary>
        public string IdCard { get; set; }




        /// <summary>
        /// 获取或设置模拟证件签发机关文本。 模板可能在没有身份证号码时不生成此值。 模型不验证签发机关是否真实存在。
        /// </summary>
        public string IssueOrganization { get; set; }




        /// <summary>
        /// 获取或设置证件有效期的起始日期。 该值由模板规则生成或由调用方设置。 模型不会验证该日期与有效期结束日期的关系。
        /// </summary>
        public DateTime ValidFrom { get; set; }




        /// <summary>
        /// 获取或设置证件有效期的结束日期。 该值由模板规则生成或由调用方设置。 模型不会验证该日期与有效期起始日期的关系。
        /// </summary>
        public DateTime ValidTo { get; set; }
    }




    /// <summary>
    /// 表示包含编号、个人资料和邮箱的用户资料。
    /// </summary>
    /// <remarks>
    /// 内置模板会生成关联个人资料和示例邮箱地址。
    /// 模型不验证字段之间的关联。
    /// </remarks>
    public sealed partial class UserProfile
    {



        /// <summary>
        /// 获取或设置用户标识。 内置模板按记录索引生成递增标识。 调用方可以设置其他值。
        /// </summary>
        public long Id { get; set; }




        /// <summary>
        /// 获取或设置用户关联的个人资料。 内置模板会为该属性创建模拟个人资料。 模型允许该属性为空或被替换。
        /// </summary>
        public PersonProfile Person { get; set; }




        /// <summary>
        /// 获取或设置用户邮箱文本。 内置模板按配置域名生成示例邮箱。 模型不验证邮箱是否可投递或属于真实用户。
        /// </summary>
        public string Email { get; set; }
    }




    /// <summary>
    /// 表示包含名称、地址和网站的企业资料。
    /// </summary>
    /// <remarks>
    /// 内置模板生成示例企业名称、模拟地址和网站，不核验企业信息真实性。
    /// </remarks>
    public sealed partial class CompanyProfile
    {



        /// <summary>
        /// 获取或设置企业名称。 内置模板根据区域设置生成示例名称。 模型不验证企业登记信息。
        /// </summary>
        public string Name { get; set; }




        /// <summary>
        /// 获取或设置企业地址。 内置模板使用模拟地址生成规则。 模型允许该属性为空或被替换。
        /// </summary>
        public MockAddress Address { get; set; }




        /// <summary>
        /// 获取或设置企业网站地址。 内置模板使用配置域名生成示例 URL。 模型不检查网站是否可访问或归属该企业。
        /// </summary>
        public string Website { get; set; }
    }




    /// <summary>
    /// 表示包含编号、名称和价格的商品。
    /// </summary>
    /// <remarks>
    /// 内置模板生成示例名称和非负模拟价格；直接赋值不会自动执行价格校验。
    /// </remarks>
    public sealed partial class Product
    {



        /// <summary>
        /// 获取或设置商品标识。 内置模板按记录索引生成递增标识。 调用方可以设置其他值。
        /// </summary>
        public long Id { get; set; }




        /// <summary>
        /// 获取或设置商品名称。 内置模板根据区域设置选择示例名称。 模型不验证商品是否真实存在。
        /// </summary>
        public string Name { get; set; }




        /// <summary>
        /// 获取或设置商品单价。 内置模板在配置范围内生成非负价格。 直接赋值不会自动限制价格范围。
        /// </summary>
        public decimal Price { get; set; }
    }




    /// <summary>
    /// 表示包含商品、数量和单价的订单项。
    /// </summary>
    /// <remarks>
    /// 小计由数量乘以单价得出并使用 checked 运算；修改属性后小计会重新计算。
    /// </remarks>
    public sealed class OrderItem
    {



        /// <summary>
        /// 获取或设置订单项对应的商品。 内置订单模板会从提供的商品列表中选择商品。 模型不自动同步商品价格与单价属性。
        /// </summary>
        public Product Product { get; set; }




        /// <summary>
        /// 获取或设置购买数量。 内置订单模板生成正整数数量。 模型不会阻止调用方设置零或负数量。
        /// </summary>
        public int Quantity { get; set; }




        /// <summary>
        /// 获取或设置订单项采用的单价。 内置订单模板会复制所选商品的当前价格。 后续修改商品价格不会自动更新此属性。
        /// </summary>
        public decimal UnitPrice { get; set; }




        /// <summary>
        /// 获取此订单项的金额小计。 结果为当前数量与单价的 checked 乘积。 乘法溢出时会抛出 <see cref="OverflowException"/>。
        /// </summary>
        public decimal Subtotal => checked(Quantity * UnitPrice);
    }




    /// <summary>
    /// 表示包含用户、商品项和总额的订单。
    /// </summary>
    /// <remarks>
    /// 内置订单模板根据输入用户和商品列表生成关联数据。
    /// 修改订单项不会自动更新总额。
    /// </remarks>
    public sealed partial class Order
    {



        /// <summary>
        /// 获取或设置订单标识。 内置模板按记录索引生成递增标识。 调用方可以设置其他值。
        /// </summary>
        public long Id { get; set; }




        /// <summary>
        /// 获取或设置订单关联的用户。 内置模板从提供的用户列表中选择用户。 模型允许该属性为空或被替换。
        /// </summary>
        public UserProfile User { get; set; }




        /// <summary>
        /// 获取或设置订单项列表。 内置模板会按配置的数量范围生成列表。 列表和其中的订单项均可由调用方修改。
        /// </summary>
        public List<OrderItem> Items { get; set; }




        /// <summary>
        /// 获取或设置订单总额。 内置模板根据各订单项小计求和。 修改订单项后不会自动重新计算此值。
        /// </summary>
        public decimal Total => checked(Items.Sum(i => i.Subtotal) - Discount + Shipping + Tax);
    }
}
