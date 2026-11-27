using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets.Initialization;
using UnityEngine.AddressableAssets.ResourceLocators;

namespace HM.Editor.HMAddressable.Editor
{
    public class CalculateAddressHelper
    {
        /// <summary>
        /// Result for checking for duplicates
        /// </summary>
        protected internal struct CheckDupeResult
        {
            public AddressableAssetGroup Group;
            public string DuplicatedFile;
            public string AssetPath;
            public GUID DuplicatedGroupGuid;
        }

        [SerializeField] internal HashSet<GUID> m_ImplicitAssets;
        [NonSerialized] internal ExtractDataTask m_ExtractData = null;

        [NonSerialized]
        internal readonly List<ContentCatalogDataEntry> m_Locations = new List<ContentCatalogDataEntry>();

        internal readonly List<AssetBundleBuild> m_AllBundleInputDefs = new List<AssetBundleBuild>();

        [NonSerialized]
        internal readonly Dictionary<string, string> m_BundleToAssetGroup = new Dictionary<string, string>();

        [NonSerialized] internal List<AddressableAssetEntry> m_AssetEntries = new List<AddressableAssetEntry>();

        [NonSerialized]
        internal readonly Dictionary<string, Dictionary<string, List<string>>> m_AllIssues =
            new Dictionary<string, Dictionary<string, List<string>>>();

        protected internal List<AssetBundleBuild> AllBundleInputDefs => m_AllBundleInputDefs;

        /// <summary>
        /// The BuildTask used to extract write data from the build.
        /// </summary>
        protected ExtractDataTask ExtractData => m_ExtractData;

        [NonSerialized] internal List<CheckDupeResult> m_ResultsData;


        /// <summary>用当前目标平台的 SBP 写入布局估算分片，再按最终 Bundle 归属提取重复依赖。</summary>
        public void CheckForDuplicateDependencies(AddressableAssetSettings settings, List<GroupInfo> myGroupInfos,
            out List<AddressableAssetGroup> newCreatGroups)
        {
            newCreatGroups = new List<AddressableAssetGroup>();
            if (!BuildUtility.CheckModifiedScenesAndAskToSave())
            {
                throw new OperationCanceledException("未保存场景或取消了保存，已停止重复依赖分析和后续打包。");
            }

            var groups = myGroupInfos.ToDictionary(info => info.Group.Guid);
            var splitGroups = myGroupInfos.Where(info =>
                info.BundleMode == BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel).ToList();
            var analysisEntries = AnalyzeLayout(settings, splitGroups.SelectMany(info => info.Group.entries));
            var dependencies = new Dictionary<string, List<string>>();
            var sharedCandidates = new Dictionary<string, List<AddressableAssetEntry>>();
            ClassifyDependencies(settings, groups, analysisEntries, dependencies, sharedCandidates, newCreatGroups);

            var candidatesByGroup = sharedCandidates.ToLookup(pair => pair.Value[0].parentGroup);
            var sharedByGroup = new List<(HMLabelPacking packing, List<AddressableAssetEntry> shared, HMLabelPackingSchema schema)>();
            foreach (var info in splitGroups)
            {
                var packing = new HMLabelPacking(info.Group, HMAddressablesEditor.ConfigHmAddressables.LabelSizeK);
                packing.AssignLabels(info.Group.entries, dependencies, false);

                // 同片共用依赖本来只写一份。只有最终分属不同 Bundle 才登记共享条目。
                var shared = new List<AddressableAssetEntry>();
                foreach (var pair in candidatesByGroup[info.Group])
                {
                    var first = pair.Value[0];
                    string firstLabel = first.labels.Single();
                    if (pair.Value.All(entry => entry.labels.Contains(firstLabel) && entry.IsScene == first.IsScene))
                    {
                        continue;
                    }
                    var dependency = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(pair.Key), info.Group,
                        false, false);
                    shared.Add(dependency);
                }
                var schema = info.Group.GetSchema<HMLabelPackingSchema>() ?? info.Group.AddSchema<HMLabelPackingSchema>();
                if (shared.Count > 0)
                {
                    sharedByGroup.Add((packing, shared, schema));
                }
                else
                {
                    schema.RecordPacking(EditorUserBuildSettings.activeBuildTarget, shared);
                }
            }

            if (sharedByGroup.Count == 0)
            {
                return;
            }
            // 共享资源成为显式条目后可能携带自己的隐式引用，按新归属重新读取其随包数据。
            var sharedInputs = AnalyzeLayout(settings, sharedByGroup.SelectMany(item => item.shared));
            dependencies.Clear();
            foreach (var item in GetDependencyLocations())
            {
                foreach (string bundle in item.bundles)
                {
                    if (sharedInputs.TryGetValue(bundle, out var entry))
                    {
                        AddDependency(dependencies, entry, item.path);
                    }
                }
            }
            foreach (var item in sharedByGroup)
            {
                item.packing.AssignLabels(item.shared, dependencies, true);
                item.schema.RecordPacking(EditorUserBuildSettings.activeBuildTarget, item.shared);
            }
        }

        /// <summary>只计算当前目标平台的写入布局。单独分析指定条目，其他条目按现有 Bundle 归属参与。</summary>
        private Dictionary<string, AddressableAssetEntry> AnalyzeLayout(AddressableAssetSettings settings,
            IEnumerable<AddressableAssetEntry> entries)
        {
            var analysisEntries = new Dictionary<string, AddressableAssetEntry>();
            CalculateInputDefinitions(settings, entries.ToDictionary(entry => entry.AssetPath), analysisEntries);
            m_ExtractData = null;
            if (AllBundleInputDefs.Count > 0)
            {
                var code = RefreshBuild(GetBuildContext(settings));
                if (code < ReturnCode.Success)
                {
                    throw new InvalidOperationException("资源依赖布局分析失败，已停止后续打包：" + code);
                }
            }
            return analysisEntries;
        }

        private IEnumerable<(string path, string[] bundles, int fileCount)> GetDependencyLocations()
        {
            if (m_ExtractData == null)
            {
                yield break;
            }
            foreach (var pair in GetImplicitGuidToFilesMap())
            {
                string path = AssetDatabase.GUIDToAssetPath(pair.Key.ToString());
                if (!AddressableAssetUtility.IsPathValidForEntry(path))
                {
                    continue;
                }
                // Unity 的内置包没有业务组归属，只统计写进业务 Bundle 的文件。
                var files = pair.Value.Select(file => ExtractData.WriteData.FileToBundle[file])
                    .Where(m_BundleToAssetGroup.ContainsKey).ToArray();
                if (files.Length > 0)
                {
                    yield return (path, files.Distinct().ToArray(), files.Length);
                }
            }
        }

        private static void AddDependency(Dictionary<string, List<string>> dependencies, AddressableAssetEntry entry,
            string path)
        {
            if (!dependencies.TryGetValue(entry.guid, out var paths))
            {
                paths = new List<string>();
                dependencies.Add(entry.guid, paths);
            }
            paths.Add(path);
        }

        /// <summary>按当前平台的实际写入对象归集单条目依赖，仅对跨业务组或普通分散包沿用隔离策略。</summary>
        private void ClassifyDependencies(AddressableAssetSettings settings, Dictionary<string, GroupInfo> groups,
            Dictionary<string, AddressableAssetEntry> analysisEntries, Dictionary<string, List<string>> dependencies,
            Dictionary<string, List<AddressableAssetEntry>> sharedCandidates, List<AddressableAssetGroup> isolationGroups)
        {
            AddressableAssetGroup externalGroup = null;
            foreach (var item in GetDependencyLocations())
            {
                var owners = item.bundles.Select(bundle => groups[m_BundleToAssetGroup[bundle]]).Distinct().ToList();
                bool canExtract = !IsResourcesPath(item.path);
                if (!canExtract || (owners.Count == 1 && analysisEntries.ContainsKey(item.bundles[0])))
                {
                    var entries = new List<AddressableAssetEntry>();
                    foreach (string bundle in item.bundles)
                    {
                        if (analysisEntries.TryGetValue(bundle, out var entry))
                        {
                            AddDependency(dependencies, entry, item.path);
                            entries.Add(entry);
                        }
                    }
                    if (canExtract && entries.Count > 1)
                    {
                        sharedCandidates.Add(item.path, entries);
                    }
                    continue;
                }
                if (item.fileCount < 2)
                {
                    continue;
                }
                var parent = GetSameParentGroupInfo(owners);
                var isolation = parent == null ? externalGroup : parent.DuplicateAssetIsolationGroup;
                if (isolation == null)
                {
                    isolation = CreatDGroup(settings, parent?.Group.Name);
                    isolationGroups.Add(isolation);
                    if (parent == null)
                    {
                        externalGroup = isolation;
                    }
                    else
                    {
                        parent.DuplicateAssetIsolationGroup = isolation;
                    }
                }
                var dependency = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(item.path), isolation, false, false);
                dependency.SetLabel(isolation.Name, true, true);
            }
        }

        private AddressableAssetGroup CreatDGroup(AddressableAssetSettings settings,string sameParentGroupName)
        {
           var group= settings.CreateGroup("Duplicate Asset Isolation"+(string.IsNullOrEmpty(sameParentGroupName)?"":" "+sameParentGroupName), false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = true;
            return group;
        }
        /// <summary>
        /// 获取共同的父组,如果没有共同的父组,那么返回null,需要添加 新的跨目录的重复依赖组
        /// </summary>
        /// <param name="beDupeDependenciesGroupInfos"></param>
        /// <returns></returns>
        private GroupInfo GetSameParentGroupInfo(List<GroupInfo> beDupeDependenciesGroupInfos )
        {
            //将每个依赖的组及其父组创造一个list.如依赖列表里面有3个组,那么就创造3个list,每个list里面是这3个组及其父对象,直到父物体为空
            var temp = new List<GroupInfo>[beDupeDependenciesGroupInfos.Count];
            for (int i = 0; i < temp.Length; i++)
            {
                temp[i] = new List<GroupInfo>();
                var group = beDupeDependenciesGroupInfos[i];
                temp[i].Add( group);
                while (true)
                {
                    if (group.MyParentGroupInfo != null)
                    {
                        temp[i].Add(group.MyParentGroupInfo);
                        group = group.MyParentGroupInfo;
                    }
                    else
                    {
                        break;
                    }
                      
                }
            }
            //只要遍历其中一个列表中的元素,
            //当其他列表中都有这个group的时候,代表这个group就是他们共同的父group
            GroupInfo parentGroupInfo = null;
            for (int i = 0; i < temp[0].Count; i++)
            {
                var group = temp[0][i];
                bool allHad = true;
                for (int j = 1; j < temp.Length; j++)
                {
                    if (temp[j].FindIndex(x => { return x == group; }) < 0)
                    {
                        allHad = false;
                        break;
                    }
                }

                if (allHad)
                {
                    parentGroupInfo = group;
                    break;
                }
            }

            return parentGroupInfo;

        }

        /// <summary>
        /// Get context for current Addressables settings
        /// </summary>
        /// <param name="settings"> The current Addressables settings object </param>
        /// <returns> The build context information </returns>
        protected internal AddressableAssetsBuildContext GetBuildContext(AddressableAssetSettings settings)
        {
            ResourceManagerRuntimeData runtimeData = new ResourceManagerRuntimeData();
            runtimeData.LogResourceManagerExceptions = settings.buildSettings.LogResourceManagerExceptions;

            var aaContext = new AddressableAssetsBuildContext
            {
                Settings = settings,
                runtimeData = runtimeData,
                bundleToAssetGroup = m_BundleToAssetGroup,
                locations = m_Locations,
                providerTypes = new HashSet<Type>(),
                assetEntries = m_AssetEntries,
                assetGroupToBundles = new Dictionary<AddressableAssetGroup, List<string>>()
            };
            return aaContext;
        }

        private void CalculateInputDefinitions(AddressableAssetSettings settings,
            Dictionary<string, AddressableAssetEntry> entriesByPath, Dictionary<string, AddressableAssetEntry> analysisEntries)
        {
            m_AssetEntries.Clear();
            m_BundleToAssetGroup.Clear();
            m_AllBundleInputDefs.Clear();

            foreach (var group in settings.groups)
            {
                var schema = group == null ? null : group.GetSchema<BundledAssetGroupSchema>();
                if (schema == null || !schema.IncludeInBuild)
                {
                    continue;
                }
                var definitions = new List<AssetBundleBuild>();
                m_AssetEntries.AddRange(BuildScriptPackedMode.PrepGroupBundlePacking(group, definitions, schema));
                foreach (var definition in definitions)
                {
                    var remaining = definition;
                    if (definition.assetNames.Any(entriesByPath.ContainsKey))
                    {
                        var paths = new List<string>();
                        var addresses = new List<string>();
                        for (int i = 0; i < definition.assetNames.Length; i++)
                        {
                            string path = definition.assetNames[i];
                            string address = definition.addressableNames[i];
                            if (!entriesByPath.TryGetValue(path, out var entry))
                            {
                                paths.Add(path);
                                addresses.Add(address);
                                continue;
                            }
                            string name = "hmaa_entry_" + entry.guid + ".bundle";
                            analysisEntries.Add(name, entry);
                            m_BundleToAssetGroup.Add(name, group.Guid);
                            m_AllBundleInputDefs.Add(new AssetBundleBuild
                            {
                                assetBundleName = name, assetNames = new[] { path }, addressableNames = new[] { address }
                            });
                        }
                        if (paths.Count == 0)
                        {
                            continue;
                        }
                        remaining.assetNames = paths.ToArray();
                        remaining.addressableNames = addresses.ToArray();
                    }
                    if (m_BundleToAssetGroup.ContainsKey(remaining.assetBundleName))
                    {
                        remaining = CreateUniqueBundle(remaining);
                    }
                    m_BundleToAssetGroup.Add(remaining.assetBundleName, group.Guid);
                    m_AllBundleInputDefs.Add(remaining);
                }
            }
        }

        internal AssetBundleBuild CreateUniqueBundle(AssetBundleBuild bid)
        {
            return CreateUniqueBundle(bid, m_BundleToAssetGroup);
        }

        /// <summary>
        /// Create new AssetBundleBuild
        /// </summary>
        /// <param name="bid">ID for new AssetBundleBuild</param>
        /// <param name="bundleToAssetGroup"> Map of bundle names to asset group Guids</param>
        /// <returns></returns>
        AssetBundleBuild CreateUniqueBundle(AssetBundleBuild bid, Dictionary<string, string> bundleToAssetGroup)
        {
            int count = 1;
            var newName = bid.assetBundleName;
            while (bundleToAssetGroup.ContainsKey(newName) && count < 1000)
                newName = bid.assetBundleName.Replace(".bundle", string.Format("{0}.bundle", count++));
            return new AssetBundleBuild
            {
                assetBundleName = newName,
                addressableNames = bid.addressableNames,
                assetBundleVariant = bid.assetBundleVariant,
                assetNames = bid.assetNames
            };
        }

        /// <summary>
        /// Refresh build to check bundles against current rules
        /// </summary>
        /// <param name="buildContext"> Context information for building</param>
        /// <returns> The return code of whether analyze build was successful, </returns>
        protected internal ReturnCode RefreshBuild(AddressableAssetsBuildContext buildContext)
        {
            var settings = buildContext.Settings;
            var context = new AddressablesDataBuilderInput(settings);

            var buildTarget = context.Target;
            var buildTargetGroup = context.TargetGroup;
            var buildParams = new AddressableAssetsBundleBuildParameters(settings, m_BundleToAssetGroup, buildTarget,
                buildTargetGroup, settings.buildSettings.bundleBuildPath);
            var builtinShaderBundleName =
                settings.DefaultGroup.Name.ToLower().Replace(" ", "").Replace('\\', '/').Replace("//", "/") +
                "_unitybuiltinshaders.bundle";
            var buildTasks = RuntimeDataBuildTasks(builtinShaderBundleName);
            m_ExtractData = new ExtractDataTask();
            buildTasks.Add(m_ExtractData);

            IBundleBuildResults buildResults;
            var exitCode = ContentPipeline.BuildAssetBundles(buildParams, new BundleBuildContent(m_AllBundleInputDefs),
                out buildResults, buildTasks, buildContext);

            return exitCode;
        }

        /// <summary>
        /// Build map of implicit guids to their bundle files
        /// </summary>
        /// <returns> Dictionary of implicit guids to their corresponding file</returns>
        protected internal Dictionary<GUID, List<string>> GetImplicitGuidToFilesMap()
        {
            if (m_ExtractData == null)
            {
                Debug.LogError("Build not run, RefreshBuild needed before GetImplicitGuidToFilesMap");
                return new Dictionary<GUID, List<string>>();
            }

            Dictionary<GUID, List<string>> implicitGuids = new Dictionary<GUID, List<string>>();
            foreach (var file in m_ExtractData.WriteData.FileToObjects)
            {
                foreach (var guid in file.Value.Select(obj => obj.guid).Distinct())
                {
                    if (m_ExtractData.WriteData.AssetToFiles.ContainsKey(guid))
                    {
                        continue;
                    }
                    if (!implicitGuids.TryGetValue(guid, out var files))
                    {
                        files = new List<string>();
                        implicitGuids.Add(guid, files);
                    }
                    files.Add(file.Key);
                }
            }

            return implicitGuids;
        }

        /// <summary>
        /// Calculate duplicate dependencies
        /// </summary>
        /// <param name="implicitGuids">Map of implicit guids to their bundle files</param>
        /// <param name="aaContext">The build context information</param>
        /// <returns>Enumerable of results from duplicates check</returns>
        protected internal IEnumerable<CheckDupeResult> CalculateDuplicates(
            Dictionary<GUID, List<string>> implicitGuids, AddressableAssetsBuildContext aaContext)
        {
            //Get all guids that have more than one bundle referencing them
            IEnumerable<KeyValuePair<GUID, List<string>>> validGuids =
                from dupeGuid in implicitGuids
                where dupeGuid.Value.Distinct().Count() > 1
                where IsValidPath(AssetDatabase.GUIDToAssetPath(dupeGuid.Key.ToString()))
                select dupeGuid;

            return
                from guidToFile in validGuids
                from file in guidToFile.Value

                //Get the files that belong to those guids
                let fileToBundle = ExtractData.WriteData.FileToBundle[file]

                //Get the bundles that belong to those files
                let bundleToGroup = aaContext.bundleToAssetGroup[fileToBundle]

                //Get the asset groups that belong to those bundles
                let selectedGroup =
                    aaContext.Settings.FindGroup(findGroup => findGroup != null && findGroup.Guid == bundleToGroup)
                select new CheckDupeResult
                {
                    Group = selectedGroup,
                    DuplicatedFile = file,
                    AssetPath = AssetDatabase.GUIDToAssetPath(guidToFile.Key.ToString()),
                    DuplicatedGroupGuid = guidToFile.Key
                };
        }

        internal void BuildImplicitDuplicatedAssetsSet(IEnumerable<CheckDupeResult> checkDupeResults)
        {
            m_ImplicitAssets = new HashSet<GUID>();

            foreach (var checkDupeResult in checkDupeResults)
            {
                Dictionary<string, List<string>> groupData;
                if (!m_AllIssues.TryGetValue(checkDupeResult.Group.Name, out groupData))
                {
                    groupData = new Dictionary<string, List<string>>();
                    m_AllIssues.Add(checkDupeResult.Group.Name, groupData);
                }

                List<string> assets;
                if (!groupData.TryGetValue(ExtractData.WriteData.FileToBundle[checkDupeResult.DuplicatedFile],
                        out assets))
                {
                    assets = new List<string>();
                    groupData.Add(ExtractData.WriteData.FileToBundle[checkDupeResult.DuplicatedFile], assets);
                }

                assets.Add(checkDupeResult.AssetPath);
                m_ImplicitAssets.Add(checkDupeResult.DuplicatedGroupGuid);
            }
        }

        internal IList<IBuildTask> RuntimeDataBuildTasks(string builtinShaderBundleName)
        {
            IList<IBuildTask> buildTasks = new List<IBuildTask>();

            // Setup
            buildTasks.Add(new SwitchToBuildPlatform());
            buildTasks.Add(new RebuildSpriteAtlasCache());

            // Player Scripts
            buildTasks.Add(new BuildPlayerScripts());

            // Dependency
            buildTasks.Add(new CalculateSceneDependencyData());
            buildTasks.Add(new CalculateAssetDependencyData());
            buildTasks.Add(new StripUnusedSpriteSources());
            buildTasks.Add(new CreateBuiltInShadersBundle(builtinShaderBundleName));

            // Packing
            buildTasks.Add(new GenerateBundlePacking());
            buildTasks.Add(new UpdateBundleObjectLayout());

            // 分片分析只读取 FileToObjects 和 FileToBundle，到这里已经具备全部写入布局。
            return buildTasks;
        }

        /// <summary>
        /// Check path is valid path for Addressables entry
        /// </summary>
        /// <param name="path"> The path to check</param>
        /// <returns>Whether path is valid</returns>
        protected bool IsValidPath(string path)
        {
            return AddressableAssetUtility.IsPathValidForEntry(path) && !IsResourcesPath(path);
        }

        private static bool IsResourcesPath(string path)
        {
            return path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase);
        }
    }
}
