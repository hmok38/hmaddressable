using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace HM.Editor
{
    /// <summary>保存母包分片的平台和共享条目的归属，供热更识别已发布的分片。</summary>
    public sealed class HMLabelPackingSchema : AddressableAssetGroupSchema
    {
        /// <summary>文件移动或条目迁入更新组后，仍可按母包 GUID 找回共享 Label。</summary>
        [Serializable]
        internal struct SharedEntry
        {
            [SerializeField] internal string Guid;
            [SerializeField] internal string Label;
        }

        [SerializeField, HideInInspector]
        private BuildTarget _buildTarget;

        [SerializeField, HideInInspector]
        private SharedEntry[] _sharedEntries = Array.Empty<SharedEntry>();

        internal BuildTarget BuildTarget => _buildTarget;

        internal void RecordPacking(BuildTarget target, List<AddressableAssetEntry> shared)
        {
            _buildTarget = target;
            _sharedEntries = shared.OrderBy(entry => entry.guid, StringComparer.Ordinal)
                .Select(entry => new SharedEntry { Guid = entry.guid, Label = entry.labels.Single() }).ToArray();
            SetDirty(true);
        }

        /// <summary>索引各母包组记录的共享条目；旧版本 Schema 没有共享记录。</summary>
        internal static Dictionary<string, (AddressableAssetGroup group, string label)> GetSharedEntries(
            AddressableAssetSettings settings)
        {
            var entries = new Dictionary<string, (AddressableAssetGroup, string)>();
            foreach (var group in settings.groups)
            {
                var schema = group.GetSchema<HMLabelPackingSchema>();
                if (schema == null)
                {
                    continue;
                }
                foreach (var record in schema._sharedEntries ?? Array.Empty<SharedEntry>())
                {
                    entries.Add(record.Guid, (group, record.Label));
                }
            }
            return entries;
        }
    }
}
