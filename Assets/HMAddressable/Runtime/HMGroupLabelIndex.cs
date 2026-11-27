using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace HM
{
    /// <summary>从当前 Catalog 收集已配置目录实际存在的分片 Label，供按组加载和释放共用。</summary>
    internal sealed class HMGroupLabelIndex
    {
        internal const string PartSeparator = "-part-";
        private readonly Dictionary<string, List<string>> _keysByFolder =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _ownerByKey =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private readonly IResourceLocator[] _locators;
        private readonly IResourceLocator[] _catalogLocators;
        internal IEnumerable<KeyValuePair<string, List<string>>> Groups => _keysByFolder;
#if UNITY_EDITOR
        internal static Func<HashSet<string>> GetEditorPartGroups;
        internal static Func<string, IResourceLocation, bool> IncludeEditorLabelLocation;
#endif

        internal HMGroupLabelIndex(HMAddressablesConfig config, IEnumerable<IResourceLocator> locators)
        {
            _locators = locators.ToArray();
            // 动态子资源和旧 Resources Locator 也能响应字符串，但没有可发布的 Label。
            _catalogLocators = _locators.Where(locator => locator.Keys?.Any() == true).ToArray();
            if (config == null)
            {
                return;
            }
            var local = config.LocalAseetsPaths ?? Array.Empty<string>();
            var remote = config.RemoteAseetsPaths ?? Array.Empty<string>();
            var unassigned = config.UnassignedAssetsPath ?? Array.Empty<AssetGroupLoadPath>();
            foreach (var path in local.Concat(remote).Concat(unassigned.Select(item => item?.GroupName)))
            {
                string folder = HMAddressableManager.NormalizePath(path)?.TrimEnd('/');
                if (string.IsNullOrEmpty(folder) || _keysByFolder.ContainsKey(folder))
                {
                    continue;
                }
                _keysByFolder.Add(folder, new List<string> { folder });
                _ownerByKey.Add(folder, folder);
            }

            // Player 使用随母包发布的配置。编辑器使用已整理的母包 Schema，勾选下一版的配置不会改变本次热更归属。
            HashSet<string> partGroups = null;
#if UNITY_EDITOR
            partGroups = GetEditorPartGroups?.Invoke();
#endif
            if (partGroups == null)
            {
                partGroups = new HashSet<string>((config.LabelPackAssetsPaths ?? Array.Empty<string>())
                    .Where(path => !string.IsNullOrEmpty(path))
                    .Select(path => HMAddressableManager.NormalizePath(path).TrimEnd('/').Replace('/', '-')),
                    StringComparer.Ordinal);
            }
            var byGroupName = _keysByFolder.Keys.Where(folder => partGroups.Contains(folder.Replace('/', '-')))
                .ToDictionary(folder => folder.Replace('/', '-'), folder => folder, StringComparer.Ordinal);
            if (byGroupName.Count == 0)
            {
                return;
            }

            // 只索引已登记分片组的完整键，允许缺号和多个 Locator 重复发布同一个 Label。
            foreach (var locator in _catalogLocators)
            {
                foreach (var key in locator.Keys)
                {
                    if (key is string label && TryGetPartGroup(label, out var groupName) &&
                        byGroupName.TryGetValue(groupName, out var folder) && !_ownerByKey.ContainsKey(label))
                    {
                        _keysByFolder[folder].Add(label);
                        _ownerByKey.Add(label, folder);
                    }
                }
            }
        }

        /// <summary>在同一份 Catalog 中逐键查询 Label；调用方合并资源位置，或只匹配已持有的句柄。</summary>
        internal IEnumerable<IResourceLocation> Locate(IEnumerable<string> labels, Type type)
        {
            foreach (var label in labels)
            {
                foreach (var locator in _catalogLocators)
                {
                    if (!locator.Locate(label, type, out var locations))
                    {
                        continue;
                    }
                    foreach (var location in locations)
                    {
#if UNITY_EDITOR
                        if (IncludeEditorLabelLocation != null && !IncludeEditorLabelLocation(label, location))
                        {
                            continue;
                        }
#endif
                        yield return location;
                    }
                }
            }
        }

        /// <summary>原生 Locator 的增删或替换也会使索引失效。</summary>
        internal bool IsCurrent(IEnumerable<IResourceLocator> locators)
        {
            return _locators.SequenceEqual(locators);
        }

        /// <summary>查已登记目录的目录 Label 和所有分片 Label，未知目录返回空引用。</summary>
        internal List<string> GetGroupKeys(string folder)
        {
            return _keysByFolder.TryGetValue(folder, out var keys) ? keys : null;
        }

        /// <summary>查由插件管理的 Label 所属目录，供热更后释放已加载的旧位置。</summary>
        internal string GetOwner(string label)
        {
            return _ownerByKey.TryGetValue(label, out var folder) ? folder : null;
        }

        /// <summary>从普通分片 -part-N 或组内共享分片 -part-sN 读取组名，N 为不带前导零的正整数。</summary>
        internal static bool TryGetPartGroup(string key, out string groupName)
        {
            groupName = null;
            int separator = key.LastIndexOf(PartSeparator, StringComparison.Ordinal);
            if (separator <= 0)
            {
                return false;
            }
            int start = separator + PartSeparator.Length;
            if (start < key.Length && key[start] == 's')
            {
                start++;
            }
            if (start >= key.Length || key[start] < '1' || key[start] > '9')
            {
                return false;
            }
            for (int i = start + 1; i < key.Length; i++)
            {
                if (key[i] < '0' || key[i] > '9')
                {
                    return false;
                }
            }
            if (!int.TryParse(key.Substring(start), NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                return false;
            }
            groupName = key.Substring(0, separator);
            return true;
        }
    }
}
