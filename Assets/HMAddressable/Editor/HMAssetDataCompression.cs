using System;
using System.IO;
using System.IO.Compression;
using Unity.Collections;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace HM.Editor
{
    /// <summary>按组压缩模式读取本次资源的字节内容，估算写入分片的数据量。</summary>
    internal sealed class HMAssetDataCompression
    {
        private readonly BundledAssetGroupSchema.BundleCompressionMode _mode;
        private readonly byte[] _block;
        private readonly int[] _lastMatch;
        private readonly MemoryStream _deflateBuffer;

        /// <summary>当前组是否启用了 Bundle 压缩。</summary>
        internal bool IsCompressed => _mode != BundledAssetGroupSchema.BundleCompressionMode.Uncompressed;

        internal HMAssetDataCompression(BundledAssetGroupSchema.BundleCompressionMode mode)
        {
            _mode = mode;
            if (mode == BundledAssetGroupSchema.BundleCompressionMode.LZ4)
            {
                _block = new byte[128 * 1024];
                _lastMatch = new int[1 << 16];
            }
            else if (mode == BundledAssetGroupSchema.BundleCompressionMode.LZMA)
            {
                // 用分块 Deflate 估算内容的可压缩程度，不分配与整个资源等大的缓冲。
                _block = new byte[1024 * 1024];
                _deflateBuffer = new MemoryStream();
            }
            else if (mode != BundledAssetGroupSchema.BundleCompressionMode.Uncompressed)
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "无法估算该组的 Bundle 压缩模式。");
            }
        }

        /// <summary>从文件当前位置到末尾估算数据量，按块重用缓冲。</summary>
        internal long Estimate(Stream source)
        {
            if (!IsCompressed)
            {
                return source.Length - source.Position;
            }
            long size = 0;
            while (source.Position < source.Length)
            {
                int count = 0;
                while (count < _block.Length)
                {
                    int read = source.Read(_block, count, _block.Length - count);
                    if (read == 0)
                    {
                        break;
                    }
                    count += read;
                }
                if (count == 0)
                {
                    break;
                }
                size += EstimateBlock(count);
            }
            return size;
        }

        /// <summary>估算导入器提供的内存数据，所有权仍归 Unity。</summary>
        internal long Estimate(NativeArray<byte> source)
        {
            if (!IsCompressed)
            {
                return source.Length;
            }
            long size = 0;
            for (int offset = 0; offset < source.Length;)
            {
                int count = Math.Min(_block.Length, source.Length - offset);
                NativeArray<byte>.Copy(source, offset, _block, 0, count);
                size += EstimateBlock(count);
                offset += count;
            }
            return size;
        }

        private int EstimateBlock(int length)
        {
            if (_mode == BundledAssetGroupSchema.BundleCompressionMode.LZ4)
            {
                return EstimateLz4Block(length);
            }
            _deflateBuffer.SetLength(0);
            using (var deflate = new DeflateStream(_deflateBuffer, CompressionLevel.Optimal, true))
            {
                deflate.Write(_block, 0, length);
            }
            return Math.Min(length, (int)_deflateBuffer.Length);
        }

        private int EstimateLz4Block(int length)
        {
            // 逐块寻找 64 KiB 窗口内的四字节匹配。只计 token、偏移、长度扩展和原文，不生成压缩文件。
            // LZ4 最后一处匹配的起点距末尾至少 12 字节，末尾保留至少 5 个原文字节。
            if (length < 13)
            {
                return length;
            }
            Array.Clear(_lastMatch, 0, _lastMatch.Length);
            int bytes = 0;
            int anchor = 0;
            int pos = 0;
            while (pos <= length - 12)
            {
                uint word = ReadWord(pos);
                int slot = (int)(unchecked(word * 2654435761u) >> 16);
                int previous = _lastMatch[slot] - 1;
                _lastMatch[slot] = pos + 1;
                if (previous < 0 || pos - previous > ushort.MaxValue || ReadWord(previous) != word)
                {
                    pos++;
                    continue;
                }

                int literalCount = pos - anchor;
                int match = 4;
                while (pos + match < length - 5 && _block[previous + match] == _block[pos + match])
                {
                    match++;
                }
                bytes += 1 + LengthExtensionBytes(literalCount) + literalCount + 2 + LengthExtensionBytes(match - 4);
                pos += match;
                anchor = pos;
            }
            int tail = length - anchor;
            return Math.Min(length, bytes + 1 + LengthExtensionBytes(tail) + tail);
        }

        private uint ReadWord(int pos)
        {
            return (uint)_block[pos] | ((uint)_block[pos + 1] << 8) |
                ((uint)_block[pos + 2] << 16) | ((uint)_block[pos + 3] << 24);
        }

        private static int LengthExtensionBytes(int length)
        {
            return length < 15 ? 0 : (length - 15) / 255 + 1;
        }
    }
}
