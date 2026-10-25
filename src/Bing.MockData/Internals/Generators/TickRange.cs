namespace Bing.MockData.Internals.Generators
{
    // 返回商，避免极值处先加 step 或乘 step 造成溢出；负数按数学向上/向下取整。
    internal static class TickRange
    {
        public static long Ceiling(long ticks, long step) => ticks / step + (ticks % step > 0 ? 1 : 0);
        public static long Floor(long ticks, long step) => ticks / step - (ticks % step < 0 ? 1 : 0);
    }
}
