using System;
using Bing.MockData.Core.Options;
using Xunit;

namespace Bing.MockData.Tests
{
    /// <summary>
    /// 测试基类
    /// </summary>
    public abstract class TestBase
    {
        /// <summary>
        /// 输出
        /// </summary>
        protected ITestOutputHelper Output;

        /// <summary>
        /// 初始化一个<see cref="TestBase"/>类型的实例
        /// </summary>
        /// <param name="output">输出</param>
        protected TestBase(ITestOutputHelper output)
        {
            Output = output;
        }

        protected void AssertSeeded<TOptions, TValue>(
            TOptions options,
            Func<TOptions, Func<TValue>> createGenerator,
            Action<TValue> assertContract)
            where TOptions : FieldOptionsBase
        {
            var firstOptions = (TOptions)options.Clone();
            var secondOptions = (TOptions)options.Clone();
            firstOptions.Seed = 94157;
            secondOptions.Seed = 94157;

            var first = createGenerator(firstOptions);
            var second = createGenerator(secondOptions);
            for (var i = 0; i < 100; i++)
            {
                var value = first();
                Assert.Equal(value, second());
                Assert.NotNull(value);
                assertContract(value);
                Output.WriteLine("{0}", value);
            }
        }
    }
}
