using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using Bing.MockData.Abstractions.Options;
using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Internals.Generators;

namespace Bing.MockData.Factories
{
    /// <summary>
    /// 随机生成器工厂
    /// </summary>
    public static class RandomizerFactory
    {
        /// <summary>
        /// 缓存字典
        /// </summary>
        private static readonly ConcurrentDictionary<string, object> Cache = new ConcurrentDictionary<string, object>();

        /// <summary>
        /// 获取字符串随机生成器
        /// </summary>
        /// <param name="options">配置</param>
        /// <returns></returns>
        public static IStringRandomizer GetRandomizer(IStringFieldOptions options)
        {
            return Create<IStringRandomizer>(options);
        }

        /// <summary>
        /// 获取Guid随机生成器
        /// </summary>
        /// <param name="options">配置</param>
        /// <returns></returns>
        public static IGuidRandomizer GetRandomizer(IGuidFieldOptions options)
        {
            return Create<IGuidRandomizer>(options);
        }

        /// <summary>
        /// 获取数值随机生成器
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="options">配置</param>
        /// <returns></returns>
        public static INumberRandomizer<T> GetRandomizer<T>(INumberFieldOptions<T> options) where T : struct
        {
            return Create<INumberRandomizer<T>>(options);
        }

        /// <summary>
        /// 获取日期时间随机生成器
        /// </summary>
        /// <param name="options">配置</param>
        /// <returns></returns>
        public static IDateTimeRandomizer GetRandomizer(IDateTimeFieldOptions options)
        {
            return Create<IDateTimeRandomizer>(options);
        }

        /// <summary>
        /// 获取时间跨度随机生成器
        /// </summary>
        /// <param name="options">配置</param>
        /// <returns></returns>
        public static ITimeSpanRandomizer GetRandomizer(ITimeSpanFieldOptions options)
        {
            return Create<ITimeSpanRandomizer>(options);
        }

        /// <summary>
        /// 获取手机号码随机生成器
        /// </summary>
        /// <param name="options">配置</param>
        /// <returns></returns>
        public static IMobileRandomizer GetRandomizer(IMobileFieldOptions options)
        {
            return Create<IMobileRandomizer>(options);
        }

        /// <summary>
        /// 获取身份证随机生成器
        /// </summary>
        /// <param name="options">配置</param>
        /// <returns></returns>
        public static IIdCardRandomizer GetRandomizer(IIdCardFieldOptions options)
        {
            return Create<IIdCardRandomizer>(options);
        }

        /// <summary>
        /// 获取随机生成器并转换成动态对象
        /// </summary>
        /// <param name="options">配置</param>
        /// <returns></returns>
        public static dynamic GetRandomizerAsDynamic(object options)
        {
            return Create<object>(options);
        }

        /// <summary>
        /// 创建随机生成器
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="options">配置</param>
        /// <returns></returns>
        private static T Create<T>(object options)
        {
            string key = $"{options.GetType().FullName}_{options.GetHashCode()}";

            if (!Cache.ContainsKey(key))
            {
                Cache.TryAdd(key, CreateRandomizer(options));
            }

            return (T) Cache[key];
        }

        /// <summary>
        /// 创建随机生成器
        /// </summary>
        /// <param name="options">配置</param>
        /// <returns></returns>
        private static object CreateRandomizer(object options)
        {
            Type optionsType = options.GetType();
            string optionsFullName = optionsType.FullName ?? string.Empty;

            string typeName;
            if (optionsType.GetTypeInfo().BaseType?.Name.StartsWith("Number`1") == true)
            {
                Type genericType = optionsType.GetTypeInfo().BaseType.GetTypeInfo().GetGenericArguments()
                    .FirstOrDefault();
                if (RandomValueGenerator.SupportedTypes.Contains(genericType))
                {
                    typeName = optionsFullName.Replace($"Options.{optionsType.Name}",
                        $"Randomizers.NumberRandomizer`1[{genericType}");
                }
                else
                {
                    throw new NotSupportedException($"类型 `{genericType}` 尚未支持");
                }
            }
            else
            {
                typeName = optionsFullName.Replace("FieldOptions", "Randomizer").Replace("Options", "Randomizers");
            }

            var type = Type.GetType(typeName, true);
            return Activator.CreateInstance(type, options);
        }
    }
}
