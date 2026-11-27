using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace HM.Editor
{
    /// <summary>检查目录与分片 Label 的归属，为编辑器播放提供与构建设置一致的目录成员。</summary>
    internal static class HMGroupLabelsEditor
    {
        private static HashSet<string> _editorLabels;

        [InitializeOnLoadMethod]
        private static void RegisterEditorLookup()
        {
            HMAddressableManager.HasEditorSettings = () => AddressableAssetSettingsDefaultObject.Settings != null;
            HMAddressableManager.GetEditorGroupFiles = folder => AssetDatabase.IsValidFolder(folder)
                ? Directory.EnumerateFiles(folder).Select(file => file.Replace('\\', '/'))
                    .Where(HMAddressablesEditor.IsPackableAssetPath).ToList()
                : new List<string>();
            HMGroupLabelIndex.IncludeEditorLabelLocation = IncludeEditorLabelLocation;
            HMGroupLabelIndex.GetEditorPartGroups = () => AddressableAssetSettingsDefaultObject.Settings?.groups
                .Where(group => group != null && group.HasSchema<HMLabelPackingSchema>())
                .Select(group => group.Name).ToHashSet();
            AddressableAssetSettings.OnModificationGlobal += OnSettingsChanged;
        }

        private static void OnSettingsChanged(AddressableAssetSettings settings, AddressableAssetSettings.ModificationEvent change, object data)
        {
            _editorLabels = null;
            if (EditorApplication.isPlaying && settings.ActivePlayModeDataBuilder is BuildScriptFastMode)
            {
                // Use Asset Database 会就地更新同一个 Locator，下一次查询需要重新读取新分片。
                HMAddressableManager.InvalidateEditorGroupLabels();
            }
        }

        private static bool IncludeEditorLabelLocation(string label, IResourceLocation location)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null || !(settings.ActivePlayModeDataBuilder is BuildScriptFastMode) ||
                !location.InternalId.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return true;
            }
            // Use Asset Database 会列出未参与构建的条目，过滤它们以匹配 Player 中发布的 Label。
            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(location.InternalId), true);
            if (entry == null || entry.address != location.PrimaryKey)
            {
                return true;
            }
            var schema = entry.parentGroup.GetSchema<BundledAssetGroupSchema>();
            if (schema == null)
            {
                return true;
            }
            // 编辑器 Locator 同时返回匹配 Address 的条目。模拟 Player 导出设置时只保留实际携带该 Label 的条目。
            if (_editorLabels == null)
            {
                _editorLabels = new HashSet<string>(settings.GetLabels(), StringComparer.Ordinal);
            }
            return schema.IncludeInBuild && schema.IncludeLabelsInCatalog && _editorLabels.Contains(label) &&
                (entry.labels.Contains(label) || (entry.IsSubAsset && entry.ParentEntry?.labels.Contains(label) == true));
        }

        internal static void ValidateLabels(AddressableAssetSettings settings, List<GroupInfo> groupInfos, bool updating)
        {
            var folders = new HashSet<string>(groupInfos.Select(info => info.Path), StringComparer.Ordinal);
            var partOwners = groupInfos.Where(info => updating
                    ? info.Group != null && info.Group.HasSchema<HMLabelPackingSchema>()
                    : info.BundleMode == BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel)
                .ToDictionary(info => info.GroupName, info => info.Path, StringComparer.Ordinal);
            // Unity 只导出全局标签表仍然登记的 Label。删除全局 Label 后，条目的 labels 可能暂时保留旧值。
            var registered = new HashSet<string>(settings.GetLabels(), StringComparer.Ordinal);
            var labels = new HashSet<string>(StringComparer.Ordinal);
            var otherKeys = new List<(string key, AddressableAssetEntry entry)>();
            foreach (var group in settings.groups)
            {
                var schema = group == null ? null : group.GetSchema<BundledAssetGroupSchema>();
                if (schema == null || !schema.IncludeInBuild)
                {
                    continue;
                }
                bool publishedPartGroup = updating && group.HasSchema<HMLabelPackingSchema>();
                foreach (var entry in group.entries)
                {
                    string folder = Path.GetDirectoryName(entry.AssetPath)?.Replace('\\', '/');
                    bool member = folders.Contains(folder) && !HMAddressablesEditor.IsDuplicateGroup(group) &&
                        (group.Name == folder.Replace('/', '-') || HMAddressablesEditor.IsContentUpdateGroup(group));
                    if (schema.IncludeAddressInCatalog && !string.IsNullOrEmpty(entry.address) &&
                        !(schema.IncludeLabelsInCatalog && registered.Contains(entry.address) && entry.labels.Contains(entry.address)))
                    {
                        if (GetOwner(entry.address, folders, partOwners) != null)
                        {
                            throw new InvalidOperationException($"资源 {entry.AssetPath} 的 Address 占用了目录或分片 Label：{entry.address}。请修改 Address。");
                        }
                        otherKeys.Add((entry.address, entry));
                    }
                    if (schema.IncludeGUIDInCatalog && registered.Contains(entry.guid) &&
                        !(schema.IncludeLabelsInCatalog && entry.labels.Contains(entry.guid)))
                    {
                        otherKeys.Add((entry.guid, entry));
                    }
                    bool hasMemberLabel = false;
                    if (schema.IncludeLabelsInCatalog)
                    {
                        foreach (string label in entry.labels)
                        {
                            if (!registered.Contains(label))
                            {
                                continue;
                            }
                            labels.Add(label);
                            string owner = GetOwner(label, folders, partOwners);
                            if (owner == null)
                            {
                                continue;
                            }
                            if (!member || owner != folder)
                            {
                                throw new InvalidOperationException($"组 {group.Name} 中的资源 {entry.AssetPath} 使用了目录 {owner} 的 Label：{label}。请检查资源归属和 Label。");
                            }
                            hasMemberLabel = true;
                        }
                    }
                    // 母包中仍留在分片组的条目若附带其他 Label，原生打包器会按标签组合拆出额外 Bundle。
                    if (publishedPartGroup && entry.labels.Count > 1)
                    {
                        throw new InvalidOperationException($"母包分片组 {group.Name} 的资源 {entry.AssetPath} 带有多个 Label，请恢复原分片设置后再热更。");
                    }
                    if (member && !hasMemberLabel)
                    {
                        throw new InvalidOperationException($"组 {group.Name} 中的资源 {entry.AssetPath} 缺少可导出的目录或分片 Label。请检查 Label 和 Include Labels in Catalog 设置。");
                    }
                }
            }

            // 原生 Catalog 的 Address、GUID 和 Label 共用同一键空间，不能让查询额外命中其他资源。
            foreach (var item in otherKeys)
            {
                if (labels.Contains(item.key))
                {
                    throw new InvalidOperationException($"资源 {item.entry.AssetPath} 的 Address 或 GUID 与已发布的 Label 同名：{item.key}。请修改冲突的 Address 或 Label。");
                }
            }
        }

        private static string GetOwner(string key, HashSet<string> folders, Dictionary<string, string> partOwners)
        {
            if (folders.Contains(key))
            {
                return key;
            }
            return HMGroupLabelIndex.TryGetPartGroup(key, out var groupName) && partOwners.TryGetValue(groupName, out var folder)
                ? folder : null;
        }
    }
}
