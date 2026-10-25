using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace Bing.MockData
{



    /// <summary>
    /// 定义导出列名称和记录取值器。
    /// </summary>
    /// <remarks>
    /// 输出列的顺序由传给导出方法的列列表决定。
    /// </remarks>
    public sealed class ExportColumn<T>
    {
        /// <summary>
        /// 获取导出列的名称。
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// 获取从记录中提取列值的委托。
        /// </summary>
        public Func<T, object> Value { get; }




        /// <summary>
        /// 创建具有指定名称和取值器的导出列。 名称不能为空，取值器必须提供。 取值器在导出每条记录时调用。
        /// </summary>
        /// <param name="name">写入导出结果的列名称。</param>
        /// <param name="value">从记录中取得该列值的委托。</param>
        /// <exception cref="ArgumentException"><paramref name="name"/> 为 <see langword="null"/> 或空字符串。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> 为 <see langword="null"/>。</exception>
        public ExportColumn(string name, Func<T, object> value)
        { if (string.IsNullOrEmpty(name)) throw new ArgumentException("列名不能为空。", nameof(name)); Name = name; Value = value ?? throw new ArgumentNullException(nameof(value)); }
    }




    /// <summary>
    /// 指定模拟数据的文本导出格式。
    /// </summary>
    /// <remarks>
    /// JSON 输出数组，JSON Lines 每行输出一条记录，CSV 输出表头和记录行并要求显式提供列。
    /// </remarks>
    public enum MockExportFormat
    {
        /// <summary>
        /// 将记录输出为一个 JSON 数组。
        /// </summary>
        Json,
        /// <summary>
        /// 将每条记录输出为单独一行的 JSON 文本。
        /// </summary>
        JsonLines,
        /// <summary>
        /// 将选定的列输出为带表头的逗号分隔值文本。
        /// </summary>
        Csv
    }




    /// <summary>
    /// 提供将记录序列化到文本或流的导出方法。
    /// </summary>
    /// <remarks>
    /// CSV 使用传入的列顺序，JSON 可序列化记录或投影列；取消或失败时已写入内容不会回滚。
    /// 输出对象默认保持打开。
    /// </remarks>
    public static class MockExport
    {



        /// <summary>
        /// 将记录写入流。 流使用 UTF-8 编码且不写入字节顺序标记。 默认保留流打开；取消或异常时流中可能已有部分内容。
        /// </summary>
        /// <typeparam name="T">输入记录的类型。</typeparam>
        /// <param name="source">要依次导出的记录序列。</param>
        /// <param name="output">接收文本的目标流。</param>
        /// <param name="format">要写入的格式。</param>
        /// <param name="columns">可选的列投影；CSV 必须提供至少一列。</param>
        /// <param name="leaveOpen"><see langword="true"/> 时保留目标流打开；否则在返回或异常时释放目标流。</param>
        /// <param name="cancellationToken">用于取消导出的令牌。</param>
        /// <exception cref="ArgumentNullException"><paramref name="output"/> 或 <paramref name="source"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentException">CSV 未提供列，或列为空、重名。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="format"/> 不是已定义格式。</exception>
        /// <exception cref="OperationCanceledException">导出被取消；已写入的内容不会回滚。</exception>
        public static void Write<T>(IEnumerable<T> source, Stream output, MockExportFormat format,
            IReadOnlyList<ExportColumn<T>> columns = null, bool leaveOpen = true, CancellationToken cancellationToken = default)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            using (var writer = new StreamWriter(output, new UTF8Encoding(false), 4096, leaveOpen))
                Write(source, writer, format, columns, true, cancellationToken);
        }




        /// <summary>
        /// 将记录写入文本写入器。 JSON 格式可直接序列化记录，或按指定列生成对象投影。 默认保留写入器打开；取消或异常时已写入内容不会回滚。
        /// </summary>
        /// <typeparam name="T">输入记录的类型。</typeparam>
        /// <param name="source">要依次导出的记录序列。</param>
        /// <param name="output">接收导出文本的写入器。</param>
        /// <param name="format">要写入的格式。</param>
        /// <param name="columns">可选的列投影；CSV 必须提供至少一列。</param>
        /// <param name="leaveOpen"><see langword="true"/> 时保留写入器打开；否则在返回或异常时释放写入器。</param>
        /// <param name="cancellationToken">用于取消导出的令牌。</param>
        /// <exception cref="ArgumentNullException"><paramref name="output"/> 或 <paramref name="source"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentException">CSV 未提供列，或列为空、重名。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="format"/> 不是已定义格式。</exception>
        /// <exception cref="OperationCanceledException">导出被取消；已写入的内容不会回滚。</exception>
        public static void Write<T>(IEnumerable<T> source, TextWriter output, MockExportFormat format,
            IReadOnlyList<ExportColumn<T>> columns = null, bool leaveOpen = true, CancellationToken cancellationToken = default)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            try
            {
                if (source == null) throw new ArgumentNullException(nameof(source)); var frozen = Validate(format, columns);
                cancellationToken.ThrowIfCancellationRequested(); var header = Header(format, frozen); output.Write(header);
                bool first = true;
                foreach (var record in source)
                {
                    cancellationToken.ThrowIfCancellationRequested(); var row = Row(record, format, frozen);
                    cancellationToken.ThrowIfCancellationRequested(); if (!first && format == MockExportFormat.Json) output.Write(',');
                    output.Write(row); first = false;
                }
                cancellationToken.ThrowIfCancellationRequested(); if (format == MockExportFormat.Json) output.Write(']'); output.Flush();
            }
            finally { if (!leaveOpen) output.Dispose(); }
        }




        /// <summary>
        /// 验证导出格式和列定义并复制列列表。 CSV 必须至少有一列，且所有格式的列名必须唯一。 返回的数组与调用方提供的可变列表相互独立。
        /// </summary>
        /// <typeparam name="T">输入记录的类型。</typeparam>
        /// <param name="format">要写入的格式。</param>
        /// <param name="columns">可选的导出列列表。</param>
        /// <returns>复制后的列数组；未提供列时返回 <see langword="null"/>。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="format"/> 不是已定义格式。</exception>
        /// <exception cref="ArgumentException">CSV 未提供列，或列为空、重名。</exception>
        private static ExportColumn<T>[] Validate<T>(MockExportFormat format, IReadOnlyList<ExportColumn<T>> columns)
        {
            if (!Enum.IsDefined(typeof(MockExportFormat), format)) throw new ArgumentOutOfRangeException(nameof(format));
            var copy = columns?.ToArray();
            if (format == MockExportFormat.Csv && (copy == null || copy.Length == 0)) throw new ArgumentException("CSV 需要显式列定义。", nameof(columns));
            if (copy != null && (copy.Any(c => c == null) || copy.Select(c => c.Name).Distinct(StringComparer.Ordinal).Count() != copy.Length)) throw new ArgumentException("列不能为空或重名。", nameof(columns));
            return copy;
        }




        /// <summary>
        /// 生成对应格式的文档或表头前缀。 JSON 返回数组起始符，CSV 返回列名行，JSON Lines 不需要表头。 CSV 表头采用 CRLF 行尾。
        /// </summary>
        /// <typeparam name="T">输入记录的类型。</typeparam>
        /// <param name="format">要写入的格式。</param>
        /// <param name="columns">已验证的列数组。</param>
        /// <returns>要写入目标的前缀文本。</returns>
        private static string Header<T>(MockExportFormat format, ExportColumn<T>[] columns)
            => format == MockExportFormat.Json ? "[" : format == MockExportFormat.Csv ? string.Join(",", columns.Select(c => Csv(c.Name))) + "\r\n" : "";




        /// <summary>
        /// 将一条记录序列化为指定格式的文本。 JSON 可输出原始记录或列投影；CSV 只输出指定列。 JSON Lines 和 CSV 记录均以换行符结束。
        /// </summary>
        /// <typeparam name="T">输入记录的类型。</typeparam>
        /// <param name="value">要序列化的记录。</param>
        /// <param name="format">要写入的格式。</param>
        /// <param name="columns">已验证的列数组，或 <see langword="null"/>。</param>
        /// <returns>该记录对应的文本。</returns>
        private static string Row<T>(T value, MockExportFormat format, ExportColumn<T>[] columns)
        {
            if (format == MockExportFormat.Csv) return string.Join(",", columns.Select(c => Csv(Convert.ToString(c.Value(value), CultureInfo.InvariantCulture)))) + "\r\n";
            object projected = columns == null ? (object)value : columns.ToDictionary(c => c.Name, c => c.Value(value), StringComparer.Ordinal);
            return JsonConvert.SerializeObject(projected, Formatting.None) + (format == MockExportFormat.JsonLines ? "\n" : "");
        }




        /// <summary>
        /// 转义单个 CSV 字段。 空值转换为空字符串，含逗号、引号或换行符时使用双引号包围。 字段内部的双引号按 CSV 规则重复转义。
        /// </summary>
        /// <param name="value">要转义的字段文本。</param>
        /// <returns>可直接写入 CSV 的字段文本。</returns>
        private static string Csv(string value)
        { value = value ?? ""; return value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value; }
#if NET10_0_OR_GREATER



        /// <summary>
        /// 异步将记录写入文本写入器。 枚举期间传递取消令牌，并在枚举结束时释放异步枚举器。 默认保留写入器打开；取消或异常时已写入内容不会回滚。
        /// </summary>
        /// <typeparam name="T">输入记录的类型。</typeparam>
        /// <param name="source">要异步枚举的记录序列。</param>
        /// <param name="output">接收导出文本的写入器。</param>
        /// <param name="format">要写入的格式。</param>
        /// <param name="columns">可选的列投影；CSV 必须提供至少一列。</param>
        /// <param name="leaveOpen"><see langword="true"/> 时保留目标写入器打开；否则在返回或异常时异步释放。</param>
        /// <param name="cancellationToken">用于取消枚举和写入的令牌。</param>
        /// <exception cref="ArgumentNullException"><paramref name="output"/> 或 <paramref name="source"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentException">CSV 未提供列，或列为空、重名。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="format"/> 不是已定义格式。</exception>
        /// <exception cref="OperationCanceledException">异步导出被取消；已写入的内容不会回滚。</exception>
        public static async System.Threading.Tasks.Task WriteAsync<T>(IAsyncEnumerable<T> source, TextWriter output, MockExportFormat format,
            IReadOnlyList<ExportColumn<T>> columns = null, bool leaveOpen = true, CancellationToken cancellationToken = default)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            try
            {
                if (source == null) throw new ArgumentNullException(nameof(source)); var frozen = Validate(format, columns);
                cancellationToken.ThrowIfCancellationRequested(); await output.WriteAsync(Header(format, frozen).AsMemory(), cancellationToken).ConfigureAwait(false);
                bool first = true;
                await using (var iterator = source.GetAsyncEnumerator(cancellationToken))
                {
                    while (await iterator.MoveNextAsync().ConfigureAwait(false))
                    {
                        cancellationToken.ThrowIfCancellationRequested(); var row = Row(iterator.Current, format, frozen);
                        if (!first && format == MockExportFormat.Json) await output.WriteAsync(",".AsMemory(), cancellationToken).ConfigureAwait(false);
                        await output.WriteAsync(row.AsMemory(), cancellationToken).ConfigureAwait(false); first = false;
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (format == MockExportFormat.Json) await output.WriteAsync("]".AsMemory(), cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally { if (!leaveOpen) await output.DisposeAsync().ConfigureAwait(false); }
        }




        /// <summary>
        /// 异步将记录写入流。 流使用 UTF-8 编码且不写入字节顺序标记。 默认保留目标流打开；取消或异常时已写入内容不会回滚。
        /// </summary>
        /// <typeparam name="T">输入记录的类型。</typeparam>
        /// <param name="source">要异步枚举的记录序列。</param>
        /// <param name="output">接收文本的目标流。</param>
        /// <param name="format">要写入的格式。</param>
        /// <param name="columns">可选的列投影；CSV 必须提供至少一列。</param>
        /// <param name="leaveOpen"><see langword="true"/> 时保留目标流打开；否则在返回或异常时释放目标流。</param>
        /// <param name="cancellationToken">用于取消枚举和写入的令牌。</param>
        /// <exception cref="ArgumentNullException"><paramref name="output"/> 或 <paramref name="source"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentException">CSV 未提供列，或列为空、重名。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="format"/> 不是已定义格式。</exception>
        /// <exception cref="OperationCanceledException">异步导出被取消；已写入的内容不会回滚。</exception>
        public static async System.Threading.Tasks.Task WriteAsync<T>(IAsyncEnumerable<T> source, Stream output, MockExportFormat format,
            IReadOnlyList<ExportColumn<T>> columns = null, bool leaveOpen = true, CancellationToken cancellationToken = default)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            await using var writer = new StreamWriter(output, new UTF8Encoding(false), 4096, leaveOpen);
            await WriteAsync(source, writer, format, columns, true, cancellationToken).ConfigureAwait(false);
        }
#endif
    }
}
