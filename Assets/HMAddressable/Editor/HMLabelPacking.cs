using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace HM.Editor
{
    /// <summary>按目标平台的条目及随包依赖大小，顺序分配母包普通分片和组内共享分片。</summary>
    internal sealed class HMLabelPacking
    {
        private readonly HMAssetSizeEstimator _estimator;
        private readonly Dictionary<string, long> _sizes = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly AddressableAssetGroup _group;
        private readonly long _limit;

        internal HMLabelPacking(AddressableAssetGroup group, int sizeK)
        {
            _group = group;
            _limit = sizeK * 1024L;
            _estimator = new HMAssetSizeEstimator(group.GetSchema<BundledAssetGroupSchema>().Compression);
        }

        internal void AssignLabels(IEnumerable<AddressableAssetEntry> entries,
            Dictionary<string, List<string>> dependencies, bool shared)
        {
            long used = 0;
            int sequence = 1;
            bool hasEntry = false;
            var included = new HashSet<string>(StringComparer.Ordinal);
            string prefix = _group.Name + HMGroupLabelIndex.PartSeparator + (shared ? "s" : string.Empty);
            foreach (var entry in entries.OrderBy(item => Path.GetFileName(item.AssetPath), StringComparer.Ordinal)
                         .ThenBy(item => item.AssetPath, StringComparer.Ordinal))
            {
                long ownSize = GetSize(entry.AssetPath);
                long fullSize = ownSize;
                long addedSize = ownSize;
                // 每个条目的依赖路径已去重；同一片只收取首次出现的依赖，换片后重新计入。
                if (dependencies.TryGetValue(entry.guid, out var paths))
                {
                    foreach (string path in paths)
                    {
                        long bytes = GetSize(path);
                        fullSize += bytes;
                        if (!included.Contains(path))
                        {
                            addedSize += bytes;
                        }
                    }
                }
                if (hasEntry && addedSize > _limit - used)
                {
                    sequence++;
                    used = 0;
                    included.Clear();
                    addedSize = fullSize;
                }
                if (paths != null)
                {
                    included.UnionWith(paths);
                }
                string label = prefix + sequence.ToString(CultureInfo.InvariantCulture);
                foreach (string previous in entry.labels.Where(value => value != label).ToArray())
                {
                    entry.SetLabel(previous, false);
                }
                entry.SetLabel(label, true, true);
                used += addedSize;
                hasEntry = true;
            }
        }

        private long GetSize(string path)
        {
            if (!_sizes.TryGetValue(path, out long bytes))
            {
                bytes = _estimator.Estimate(path, AssetDatabase.AssetPathToGUID(path));
                _sizes.Add(path, bytes);
            }
            return bytes;
        }
    }
}
