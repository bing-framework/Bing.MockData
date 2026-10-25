namespace Bing.MockData
{
    public sealed partial class MockContext
    {
        /// <summary>创建占用一级对象嵌套预算的派生上下文，供扩展包构建手动关联对象。</summary>
        /// <remarks>保留当前记录、字段、取消令牌及共享资源；超出深度预算时立即失败。不产生新随机种子或复制资源。</remarks>
        /// <returns>用于当前子对象的上下文。</returns>
        /// <exception cref="MockLimitException">对象嵌套深度超过预算。</exception>
        public MockContext ForObject() => EnterObject();
    }
}
