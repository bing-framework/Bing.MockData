using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace Bing.MockData
{
    public sealed partial class MockContext
    {
        /// <summary>
        /// 复制并替换根上下文中的具名字典。
        /// </summary>
        /// <remarks>
        /// 仅复制列表结构，不会克隆条目对象；派生上下文和已有生成快照继续使用各自可见的目录快照。
        /// </remarks>
        /// <typeparam name="T">字典条目的类型。</typeparam>
        /// <param name="name">用于查找字典的非空名称。</param>
        /// <param name="values">要复制的只读候选列表；可以为空列表。</param>
        /// <exception cref="ArgumentNullException"><paramref name="values"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentException"><paramref name="name"/> 为 <see langword="null"/>、空字符串或空白字符串。</exception>
        /// <exception cref="InvalidOperationException">当前实例不是根上下文。</exception>
        /// <exception cref="MockLimitException">条目数超过字典条目预算。</exception>
        /// <exception cref="ObjectDisposedException">根上下文已释放。</exception>
        /// <exception cref="OperationCanceledException">操作期间上下文已取消。</exception>
        public void SetDictionary<T>(string name, IReadOnlyList<T> values)
        {
            Check(); if (values == null) throw new ArgumentNullException(nameof(values));
            if (values.Count > Budget.MaxDictionaryEntries) throw new MockLimitException(nameof(MockLimits.MaxDictionaryEntries), Budget.MaxDictionaryEntries);
            var copy = new T[values.Count]; for (int i = 0; i < copy.Length; i++) { Check(); copy[i] = values[i]; }
            StoreDictionary(name, copy);
        }

        /// <summary>
        /// 从 UTF-8 文本文件加载具名字典。
        /// </summary>
        /// <remarks>
        /// 文件中的每一行作为一个条目；文件由此方法打开并在读取后关闭。
        /// 只有完整读取和验证成功后，根目录中的原字典才会被替换。
        /// </remarks>
        /// <param name="name">用于查找字典的非空名称。</param>
        /// <param name="path">要读取的文本文件路径。</param>
        /// <param name="cancellationToken">用于取消文件读取的令牌。</param>
        /// <exception cref="ArgumentException"><paramref name="name"/> 为 <see langword="null"/>、空字符串或空白字符串。</exception>
        /// <exception cref="InvalidOperationException">当前实例不是根上下文。</exception>
        /// <exception cref="MockLimitException">输入字节数、行数或单行文本长度超过对应预算。</exception>
        /// <exception cref="DecoderFallbackException">输入不是有效的 UTF-8 文本。</exception>
        /// <exception cref="OperationCanceledException">读取期间操作被取消。</exception>
        public void LoadTextDictionary(string name, string path, CancellationToken cancellationToken = default)
        { Check(); ValidateDictionaryName(name); LoadTextDictionary(name, File.OpenRead(path), false, cancellationToken); }

        /// <summary>
        /// 从 UTF-8 文本流加载具名字典。
        /// </summary>
        /// <remarks>
        /// 每行成为一个条目，空行也会保留；读取完整并通过所有限制后才替换根目录中的原字典。
        /// 字节数、行数及单行长度受上下文预算限制。
        /// </remarks>
        /// <param name="name">用于查找字典的非空名称。</param>
        /// <param name="input">提供 UTF-8 文本的可读流。</param>
        /// <param name="leaveOpen"><see langword="true"/> 时保留输入流打开；默认为 <see langword="true"/>。</param>
        /// <param name="cancellationToken">用于取消读取的令牌。</param>
        /// <exception cref="ArgumentNullException"><paramref name="input"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentException"><paramref name="name"/> 为 <see langword="null"/>、空字符串或空白字符串。</exception>
        /// <exception cref="InvalidOperationException">当前实例不是根上下文。</exception>
        /// <exception cref="MockLimitException">输入字节数、行数或单行文本长度超过对应预算。</exception>
        /// <exception cref="DecoderFallbackException">输入不是有效的 UTF-8 文本。</exception>
        /// <exception cref="OperationCanceledException">读取期间操作被取消。</exception>
        public void LoadTextDictionary(string name, Stream input, bool leaveOpen = true, CancellationToken cancellationToken = default)
        {
            Check(); ValidateDictionaryName(name); if (input == null) throw new ArgumentNullException(nameof(input));
            using (var limited = new LimitedReadStream(input, Budget.MaxDictionaryBytes, leaveOpen, cancellationToken))
            using (var reader = new StreamReader(limited, new UTF8Encoding(false, true), false, 4096))
            {
                var values = new List<string>(); string line;
                while ((line = reader.ReadLine()) != null)
                {
                    Check(); cancellationToken.ThrowIfCancellationRequested();
                    if (values.Count >= Budget.MaxDictionaryEntries) throw new MockLimitException(nameof(MockLimits.MaxDictionaryEntries), Budget.MaxDictionaryEntries);
                    if (line.Length > Budget.MaxTextLength) throw new MockLimitException(nameof(MockLimits.MaxTextLength), Budget.MaxTextLength);
                    values.Add(line);
                }
                cancellationToken.ThrowIfCancellationRequested(); StoreDictionary(name, values.ToArray());
            }
        }

        /// <summary>
        /// 从 JSON 文件加载具名字典。
        /// </summary>
        /// <remarks>
        /// 文件必须包含一个 JSON 数组；文件由此方法打开并在读取后关闭。
        /// 只有完整反序列化和验证成功后，根目录中的原字典才会被替换。
        /// </remarks>
        /// <typeparam name="T">数组中各条目的目标类型。</typeparam>
        /// <param name="name">用于查找字典的非空名称。</param>
        /// <param name="path">要读取的 JSON 文件路径。</param>
        /// <param name="cancellationToken">用于取消文件读取的令牌。</param>
        /// <exception cref="ArgumentException"><paramref name="name"/> 为 <see langword="null"/>、空字符串或空白字符串。</exception>
        /// <exception cref="InvalidOperationException">当前实例不是根上下文。</exception>
        /// <exception cref="MockLimitException">输入字节数、条目数或 JSON 深度超过对应预算。</exception>
        /// <exception cref="JsonException">JSON 文档无效、不是完整的单个数组，或条目无法反序列化。</exception>
        /// <exception cref="DecoderFallbackException">输入不是有效的 UTF-8 文本。</exception>
        /// <exception cref="OperationCanceledException">读取期间操作被取消。</exception>
        public void LoadJsonDictionary<T>(string name, string path, CancellationToken cancellationToken = default)
        { Check(); ValidateDictionaryName(name); LoadJsonDictionary<T>(name, File.OpenRead(path), false, cancellationToken); }

        /// <summary>
        /// 从 JSON 流加载具名字典。
        /// </summary>
        /// <remarks>
        /// 顶层必须是单个数组，逐项反序列化并受字节数、条目数和深度预算限制。
        /// 不启用类型名称反序列化；成功读取全部内容后才替换原字典。
        /// </remarks>
        /// <typeparam name="T">数组中各条目的目标类型。</typeparam>
        /// <param name="name">用于查找字典的非空名称。</param>
        /// <param name="input">提供 UTF-8 JSON 文本的可读流。</param>
        /// <param name="leaveOpen"><see langword="true"/> 时保留输入流打开；默认为 <see langword="true"/>。</param>
        /// <param name="cancellationToken">用于取消读取和反序列化的令牌。</param>
        /// <exception cref="ArgumentNullException"><paramref name="input"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentException"><paramref name="name"/> 为 <see langword="null"/>、空字符串或空白字符串。</exception>
        /// <exception cref="InvalidOperationException">当前实例不是根上下文。</exception>
        /// <exception cref="MockLimitException">输入字节数、条目数或 JSON 深度超过对应预算。</exception>
        /// <exception cref="JsonException">JSON 文档无效、不是完整的单个数组，或条目无法反序列化。</exception>
        /// <exception cref="DecoderFallbackException">输入不是有效的 UTF-8 文本。</exception>
        /// <exception cref="OperationCanceledException">读取期间操作被取消。</exception>
        public void LoadJsonDictionary<T>(string name, Stream input, bool leaveOpen = true, CancellationToken cancellationToken = default)
        {
            Check(); ValidateDictionaryName(name); if (input == null) throw new ArgumentNullException(nameof(input));
            using (var limited = new LimitedReadStream(input, Budget.MaxDictionaryBytes, leaveOpen, cancellationToken))
            using (var reader = new StreamReader(limited, new UTF8Encoding(false, true), false, 4096))
            using (var json = new JsonTextReader(reader) { MaxDepth = Budget.MaxDepth })
            {
                var values = new List<T>(); var serializer = new JsonSerializer { TypeNameHandling = TypeNameHandling.None, MaxDepth = Budget.MaxDepth };
                if (!json.Read() || json.TokenType != JsonToken.StartArray) throw new JsonSerializationException("字典必须为 JSON 数组。");
                bool ended = false;
                while (json.Read())
                {
                    Check(); cancellationToken.ThrowIfCancellationRequested();
                    if (json.TokenType == JsonToken.EndArray) { ended = true; break; }
                    if (json.TokenType == JsonToken.Comment) continue;
                    if (values.Count >= Budget.MaxDictionaryEntries) throw new MockLimitException(nameof(MockLimits.MaxDictionaryEntries), Budget.MaxDictionaryEntries);
                    values.Add(serializer.Deserialize<T>(json));
                }
                if (!ended || json.Read()) throw new JsonSerializationException("数组未结束或存在多余 JSON 内容。");
                cancellationToken.ThrowIfCancellationRequested(); StoreDictionary(name, values.ToArray());
            }
        }

        /// <summary>
        /// 验证字典名称及当前实例是否允许更新字典。
        /// </summary>
        /// <param name="name">要验证的字典名称。</param>
        /// <exception cref="InvalidOperationException">当前实例不是根上下文。</exception>
        /// <exception cref="ArgumentException"><paramref name="name"/> 为 <see langword="null"/>、空字符串或空白字符串。</exception>
        private void ValidateDictionaryName(string name)
        { if (!_owner) throw new InvalidOperationException("只能在根上下文更新字典。"); if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("字典名称不能为空。", nameof(name)); }
    }

    /// <summary>
    /// 为输入流添加字节数上限和取消检查的只读包装流。
    /// </summary>
    /// <remarks>
    /// 释放包装流时根据 <c>leaveOpen</c> 参数决定是否释放底层流。
    /// </remarks>
    internal sealed class LimitedReadStream : Stream
    {
        /// <summary>
        /// 保存被包装的输入流。
        /// </summary>
        private readonly Stream _source;
        /// <summary>
        /// 保存允许读取的最大字节数。
        /// </summary>
        private readonly long _limit;
        /// <summary>
        /// 指示释放包装流时是否保留底层流。
        /// </summary>
        private readonly bool _leaveOpen;
        /// <summary>
        /// 保存读取期间检查的取消令牌。
        /// </summary>
        private readonly CancellationToken _token;
        /// <summary>
        /// 记录通过包装流累计读取的字节数。
        /// </summary>
        private long _read;
        /// <summary>
        /// 指示包装流是否已释放。
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 创建有读取上限的输入流包装器。
        /// </summary>
        /// <param name="source">要包装的输入流。</param>
        /// <param name="limit">允许读取的最大字节数。</param>
        /// <param name="leaveOpen"><see langword="true"/> 时释放包装器不会关闭底层流。</param>
        /// <param name="token">读取前后检查的取消令牌。</param>
        internal LimitedReadStream(Stream source, long limit, bool leaveOpen, CancellationToken token)
        { _source = source; _limit = limit; _leaveOpen = leaveOpen; _token = token; }

        /// <inheritdoc />
        /// <remarks>
        /// 每次读取前后检查取消状态；为识别超限输入，最多额外读取一个字节并在超限时抛出限制异常。
        /// </remarks>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LimitedReadStream)); _token.ThrowIfCancellationRequested();
            int wanted = (int)Math.Min(count, Math.Min(int.MaxValue - 1L, _limit - _read) + 1);
            int read = _source.Read(buffer, offset, wanted); _read += read;
            if (_read > _limit) throw new MockLimitException(nameof(MockLimits.MaxDictionaryBytes), _limit);
            _token.ThrowIfCancellationRequested(); return read;
        }

        /// <inheritdoc />
        /// <remarks>
        /// 释放时最多关闭包装流一次，并仅在 <c>leaveOpen</c> 为 <see langword="false"/> 时关闭底层流。
        /// </remarks>
        protected override void Dispose(bool disposing)
        { if (!_disposed) { _disposed = true; if (disposing && !_leaveOpen) _source.Dispose(); } base.Dispose(disposing); }

        /// <inheritdoc />
        /// <remarks>包装流已释放或底层流不可读时返回 <see langword="false"/>。</remarks>
        public override bool CanRead => !_disposed && _source.CanRead;

        /// <inheritdoc />
        /// <remarks>此包装流不支持定位。</remarks>
        public override bool CanSeek => false;

        /// <inheritdoc />
        /// <remarks>此包装流不支持写入。</remarks>
        public override bool CanWrite => false;

        /// <inheritdoc />
        /// <remarks>此包装流不支持查询总长度。</remarks>
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        /// <remarks>读取位置表示已读取字节数；不支持设置位置。</remarks>
        public override long Position { get => _read; set => throw new NotSupportedException(); }

        /// <inheritdoc />
        /// <remarks>刷新操作不执行任何操作，因为此包装流只读。</remarks>
        public override void Flush() { }

        /// <inheritdoc />
        /// <remarks>此包装流不支持定位。</remarks>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        /// <remarks>此包装流不支持设置长度。</remarks>
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        /// <remarks>此包装流不支持写入。</remarks>
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
