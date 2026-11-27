using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Experimental;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace HM.Editor
{
    /// <summary>按当前目标平台的导入结果和组压缩设置，估算单个条目或依赖文件的占用。</summary>
    internal sealed class HMAssetSizeEstimator
    {
        // Unity 的音频 Inspector 用此属性显示 Imported Size。版本未提供该属性时使用当前导入数据。
        private static readonly PropertyInfo AudioImportedSize = typeof(AudioImporter).GetProperty("compSize",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private readonly HMAssetDataCompression _compression;
        private readonly string _platform;

        internal HMAssetSizeEstimator(BundledAssetGroupSchema.BundleCompressionMode compression)
        {
            _compression = new HMAssetDataCompression(compression);
            var target = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
#if UNITY_2021_2_OR_NEWER
            _platform = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(target).TargetName;
#else
            _platform = target == BuildTargetGroup.iOS ? "iPhone" : target.ToString();
#endif
        }

        /// <summary>返回本次分片使用的估算字节数，只统计当前资源自己的数据。</summary>
        internal long Estimate(string path, string guid)
        {
            // 文本直接读导入内容。音视频优先采用本次编码的大小，PCM 还可以按内容估算整包压缩。
            var importer = AssetImporter.GetAtPath(path);
            var mainType = AssetDatabase.GetMainAssetTypeAtPath(path);
            // 场景的编辑器导入对象只是 SceneAsset，场景的层级与组件保存在 .unity 文件中。
            if (mainType == typeof(SceneAsset))
            {
                return EstimateSourceFile(path);
            }
            if (mainType == typeof(TextAsset))
            {
                var text = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (text != null)
                {
#if UNITY_2021_2_OR_NEWER
                    return _compression.Estimate(text.GetData<byte>());
#else
                    using (var data = new MemoryStream(text.bytes, false))
                    {
                        return _compression.Estimate(data);
                    }
#endif
                }
            }
            if (importer is VideoClipImporter video && video.outputFileSize > 0)
            {
                return checked((long)video.outputFileSize);
            }

            long platformBytes = 0;
            bool crunched = false;
            if (importer is AudioImporter audio && AudioImportedSize?.GetValue(audio) is int importedBytes &&
                importedBytes > 0)
            {
                platformBytes = importedBytes;
                var settings = audio.ContainsSampleSettingsOverride(_platform)
                    ? audio.GetOverrideSampleSettings(_platform) : audio.defaultSampleSettings;
                if (settings.compressionFormat != AudioCompressionFormat.PCM)
                {
                    return platformBytes;
                }
            }
            else if (importer is TextureImporter textureImporter && textureImporter.textureShape == TextureImporterShape.Texture2D)
            {
                platformBytes = GetTextureSize(path, textureImporter, out var texture, out var format, out crunched);
                if (_compression.IsCompressed && !crunched && texture.isReadable &&
                    platformBytes <= int.MaxValue && texture.format == format)
                {
                    var pixels = texture.GetRawTextureData<byte>();
                    if (pixels.Length == platformBytes)
                    {
                        return _compression.Estimate(pixels);
                    }
                }
            }
            if (platformBytes > 0 && !_compression.IsCompressed && !crunched)
            {
                return platformBytes;
            }

            // 只查此 GUID 当前有效的主导入结果。模型的网格和动画、Prefab 的序列化对象都按本文件计一次。
            if (TryMeasureImportedData(guid, out long rawBytes, out long packedBytes))
            {
                if (platformBytes == 0)
                {
                    return packedBytes;
                }
                // Crunch 的磁盘数据可能小于 GPU 块大小。其他贴图仍以目标格式为基数，避免编辑器解压格式干扰。
                if (crunched)
                {
                    platformBytes = Math.Min(platformBytes, rawBytes);
                }
                if (rawBytes < platformBytes)
                {
                    // 导入结果未包含完整载荷时，只扣除已读数据实际估出的节省量。
                    return platformBytes - (rawBytes - packedBytes);
                }
                return (long)decimal.Ceiling((decimal)platformBytes * packedBytes / rawBytes);
            }
            if (platformBytes > 0)
            {
                return platformBytes;
            }

            return EstimateSourceFile(path);
        }

        private long EstimateSourceFile(string path)
        {
            // 原生序列化资源可能直接使用源文件。只读自身内容，不沿引用将外部资源重复计入。
            if (path.StartsWith("Packages/", StringComparison.Ordinal))
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
                if (package != null)
                {
                    path = Path.Combine(package.resolvedPath, path.Substring(package.assetPath.Length + 1));
                }
            }
            using (var source = File.OpenRead(path))
            {
                TrySeekObjectData(source);
                return _compression.Estimate(source);
            }
        }

        private bool TryMeasureImportedData(string guid, out long rawBytes, out long packedBytes)
        {
            rawBytes = 0;
            packedBytes = 0;
            var id = AssetDatabaseExperimental.LookupArtifact(new ArtifactKey(new GUID(guid)));
            if (!id.isValid || !AssetDatabaseExperimental.GetArtifactPaths(id, out var paths) ||
                paths == null || paths.Length == 0)
            {
                return false;
            }

            // 优先检查通常无扩展名的主文件，并用文件头确认。自定义导入器也可以返回命名的序列化文件。
            bool foundMain = false;
            foreach (string path in paths.OrderBy(Path.HasExtension))
            {
                if (IsExternalDataStream(path) || !File.Exists(path))
                {
                    continue;
                }
                using (var main = File.OpenRead(path))
                {
                    if (!TrySeekObjectData(main))
                    {
                        continue;
                    }
                    rawBytes = main.Length - main.Position;
                    packedBytes = _compression.Estimate(main);
                    foundMain = true;
                    break;
                }
            }
            if (!foundMain)
            {
                return false;
            }
            // 只累计 Unity 的外置资源流，跳过预览图、导入日志等编辑器文件。
            foreach (string path in paths)
            {
                if (!IsExternalDataStream(path))
                {
                    continue;
                }
                if (!File.Exists(path))
                {
                    return false;
                }
                using (var stream = File.OpenRead(path))
                {
                    rawBytes += stream.Length;
                    packedBytes += _compression.Estimate(stream);
                }
            }
            return rawBytes > 0;
        }

        private static bool IsExternalDataStream(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".resS", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".resource", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TrySeekObjectData(Stream stream)
        {
            // Unity 2020.3/2022.3 的 SerializedFile v22 文件头为大端序。只计对象区，排除编辑器类型树。
            // 遇到其他格式时复位，调用方改用资源自身的估值，不猜测未知文件的布局。
            if (stream.Length < 48)
            {
                return false;
            }
            byte[] header = new byte[48];
            int read = 0;
            while (read < header.Length)
            {
                int count = stream.Read(header, read, header.Length - read);
                if (count == 0)
                {
                    break;
                }
                read += count;
            }
            stream.Position = 0;
            if (read != header.Length || ReadBigEndian(header, 8, 4) != 22)
            {
                return false;
            }
            ulong length = ReadBigEndian(header, 24, 8);
            ulong start = ReadBigEndian(header, 32, 8);
            if (length != (ulong)stream.Length || start < 48 + ReadBigEndian(header, 20, 4) || start > length)
            {
                return false;
            }
            stream.Position = (long)start;
            return true;
        }

        private static ulong ReadBigEndian(byte[] data, int offset, int length)
        {
            ulong value = 0;
            for (int i = 0; i < length; i++)
            {
                value = (value << 8) | data[offset + i];
            }
            return value;
        }

        private long GetTextureSize(string path, TextureImporter importer, out Texture2D texture,
            out TextureFormat textureFormat, out bool crunched)
        {
            // 已导入的尺寸和 Mipmap 反映当前平台的缩放，格式按目标平台解析，避免采用编辑器显卡的解压格式。
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                throw new InvalidDataException($"无法读取目标平台 {_platform} 的二维贴图：{path}");
            }
            var settings = importer.GetPlatformTextureSettings(_platform);
            if (!settings.overridden)
            {
                settings = importer.GetDefaultPlatformTextureSettings();
            }
            var format = settings.format;
            if ((int)format < 0)
            {
                format = importer.GetAutomaticFormat(_platform);
            }
            if ((int)format < 0)
            {
                throw new InvalidDataException($"无法确定目标平台 {_platform} 的纹理格式：{path}");
            }
            // 旧平台设置中的 ATC 在新版 Unity 已由 ETC 格式替代，两套枚举的历史数值不同。
#pragma warning disable 618
            if (format == TextureImporterFormat.ATC_RGB4)
            {
                format = TextureImporterFormat.ETC_RGB4;
            }
            if (format == TextureImporterFormat.ATC_RGBA8)
            {
                format = TextureImporterFormat.ETC2_RGBA8;
            }
#pragma warning restore 618
            textureFormat = (TextureFormat)format;
            crunched = textureFormat == TextureFormat.DXT1Crunched || textureFormat == TextureFormat.DXT5Crunched ||
                textureFormat == TextureFormat.ETC_RGB4Crunched || textureFormat == TextureFormat.ETC2_RGBA8Crunched;
            if (settings.crunchedCompression && (textureFormat == TextureFormat.DXT1 || textureFormat == TextureFormat.DXT5 ||
                textureFormat == TextureFormat.ETC_RGB4 || textureFormat == TextureFormat.ETC2_RGBA8))
            {
                crunched = true;
            }
            switch (textureFormat)
            {
                case TextureFormat.DXT1Crunched: textureFormat = TextureFormat.DXT1; break;
                case TextureFormat.DXT5Crunched: textureFormat = TextureFormat.DXT5; break;
                case TextureFormat.ETC_RGB4Crunched: textureFormat = TextureFormat.ETC_RGB4; break;
                case TextureFormat.ETC2_RGBA8Crunched: textureFormat = TextureFormat.ETC2_RGBA8; break;
            }
            int width = texture.width;
            int height = texture.height;
            // Unity 的大小 API 返回 uint。大纹理分层累加，避免浮点纹理达到 4 GiB 后被计为零。
            long blockSize = GraphicsFormatUtility.GetBlockSize(textureFormat);
            long blockWidth = GraphicsFormatUtility.GetBlockWidth(textureFormat);
            long blockHeight = GraphicsFormatUtility.GetBlockHeight(textureFormat);
            if (blockSize == 0 || blockWidth == 0 || blockHeight == 0)
            {
                throw new InvalidDataException($"无法估算目标平台 {_platform} 的纹理格式 {format}：{path}");
            }
            long total = 0;
            for (int mip = 0; mip < texture.mipmapCount; mip++)
            {
                long blocksX = (width + blockWidth - 1) / blockWidth;
                long blocksY = (height + blockHeight - 1) / blockHeight;
                long size = blocksX * blocksY * blockSize;
                total += size > uint.MaxValue ? size : GraphicsFormatUtility.ComputeMipmapSize(width, height, textureFormat);
                width = Math.Max(1, width / 2);
                height = Math.Max(1, height / 2);
            }
            return total;
        }
    }
}
