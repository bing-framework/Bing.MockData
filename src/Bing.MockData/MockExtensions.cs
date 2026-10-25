using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Bing.MockData
{
    public sealed partial class MockOptions
    {
        internal readonly Dictionary<string, ExtensionRegistration> Extensions = new Dictionary<string, ExtensionRegistration>(StringComparer.Ordinal);
        internal IReadOnlyDictionary<string, string> ExpectedExtensionVersions;

        /// <summary>在构建阶段挂载有版本的扩展。实例默认借用；选项复用时建议使用工厂。</summary>
        public MockOptions UseExtension<T>(string name, T instance, string version, bool ownsResource = false) where T : class
        {
            ValidateExtension(name, version);
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            UseResource(instance, ownsResource);
            Extensions.Add(name, new ExtensionRegistration { Instance = instance, Version = version });
            return this;
        }

        /// <summary>每个根上下文创建独立实例并负责释放。构造失败时也释放已创建资源。</summary>
        public MockOptions UseExtensionFactory<T>(string name, Func<T> factory, string version) where T : class, IDisposable
        {
            ValidateExtension(name, version);
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            Extensions.Add(name, new ExtensionRegistration { Factory = () => factory(), Version = version });
            return this;
        }

        /// <summary>由已创建的资源确定版本身份，适用于构建阶段组合的目录集合。</summary>
        public MockOptions UseExtensionFactory<T>(string name, Func<T> factory, Func<T, string> versionFactory) where T : class, IDisposable
        {
            ValidateExtension(name, "instance-version");
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (versionFactory == null) throw new ArgumentNullException(nameof(versionFactory));
            Extensions.Add(name, new ExtensionRegistration { Factory = () => factory(), VersionFactory = resource => versionFactory((T)resource) });
            return this;
        }

        private void ValidateExtension(string name, string version)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(version)) throw new ArgumentException("扩展名称及版本不能为空。");
            if (Extensions.ContainsKey(name)) throw new ArgumentException("扩展已经注册：" + name);
            if (name.StartsWith("schema:", StringComparison.Ordinal) && System.Linq.Enumerable.Count(Extensions.Keys, key => key.StartsWith("schema:", StringComparison.Ordinal)) >= Limits.MaxCompiledSchemas)
                throw new MockLimitException(nameof(MockLimits.MaxCompiledSchemas), Limits.MaxCompiledSchemas);
        }
        internal sealed class ExtensionRegistration { internal object Instance; internal Func<object> Factory; internal string Version; internal Func<object,string> VersionFactory; }
    }
    public sealed partial class MockContext
    {
        /// <summary>返回冻结的扩展实例。访问遵守上下文的取消和释放状态。</summary>
        public T Extension<T>(string name) where T : class
        {
            Check();
            if (!_state.Extensions.TryGetValue(name, out var instance) || !(instance is T result))
                throw new InvalidOperationException("扩展未挂载或类型不匹配：" + name);
            return result;
        }
        /// <summary>获取所有已挂载扩展的数据或生成配置版本。</summary>
        public IReadOnlyDictionary<string, string> ExtensionVersions
        { get { Check(); return new ReadOnlyDictionary<string, string>(_state.ExtensionVersions); } }
    }
}
