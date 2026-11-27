using System;
using System.IO;
using System.Linq;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace HM.Editor
{
    /// <summary>按目标平台的资源大小和文件名顺序，给母包目录组分配唯一的分片 Label。</summary>
    internal static class HMLabelPacking
    {
        internal static void AssignLabels(AddressableAssetGroup group, int sizeK)
        {
            long limit = sizeK * 1024L;
            long used = 0;
            int sequence = 1;
            bool hasEntry = false;
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            var estimator = new HMAssetSizeEstimator(schema.Compression);
            // 只沿排序方向前进。单项超限后即使下一项为零字节，也须另开一片。
            foreach (var entry in group.entries.OrderBy(item => Path.GetFileName(item.AssetPath), StringComparer.Ordinal))
            {
                long size = estimator.Estimate(entry.AssetPath, entry.guid);
                if (hasEntry && size > limit - used)
                {
                    sequence++;
                    used = 0;
                }
                string label = group.Name + HMGroupLabelIndex.PartSeparator + sequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
                foreach (string previous in entry.labels.Where(value => value != label).ToArray())
                {
                    entry.SetLabel(previous, false);
                }
                entry.SetLabel(label, true, true);
                used += size;
                hasEntry = true;
            }
        }
    }
}
