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


        public void CheckForDuplicateDependencies(AddressableAssetSettings settings,List<GroupInfo> myGroupInfos)
        {
            if (!BuildUtility.CheckModifiedScenesAndAskToSave())
            {
                Debug.LogError("Cannot run Analyze with unsaved scenes");

                return;
            }

            CalculateInputDefinitions(settings);

            if (AllBundleInputDefs.Count > 0)
            {
                var context = GetBuildContext(settings);
                ReturnCode exitCode = RefreshBuild(context);
                if (exitCode < ReturnCode.Success)
                {
                    Debug.LogError("Analyze build failed. " + exitCode);
                    return;
                }
                
                var implicitGuids = GetImplicitGuidToFilesMap();
                var checkDupeResults = CalculateDuplicates(implicitGuids, context);
                foreach (var result in checkDupeResults)
                {
                    Debug.Log($"资源:{result.AssetPath}  被 {result.Group.Name} 组引用");
                }

               var map= this.GetAssetsDuplicateMap(myGroupInfos,checkDupeResults);

               AddressableAssetGroup duplicateGroup = null;
              
               foreach (var assetPathKeyValue in map)
               {
                   var parentGroup = GetSameParentGroupInfo(assetPathKeyValue.Value);
                   Debug.Log($"资源:{assetPathKeyValue.Key}  依赖的共同父组为{(parentGroup!=null?parentGroup.GroupName:"无")}");
                   if (parentGroup != null)
                   {
                       if (parentGroup.DuplicateAssetIsolationGroup == null)
                       {
                           parentGroup.DuplicateAssetIsolationGroup= CreatDGroup(settings, parentGroup.Group.Name);
                       }
                       var tmp=   settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(assetPathKeyValue.Key), parentGroup.DuplicateAssetIsolationGroup,false, false);
                       tmp.SetLabel(parentGroup.Path, true, true);
                   }
                   else
                   {
                       if (duplicateGroup == null)
                       {
                           duplicateGroup = CreatDGroup(settings,null);
                       }
                     var tmp=  settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(assetPathKeyValue.Key),duplicateGroup,false, false);
                       tmp.SetLabel(duplicateGroup.name, true, true);
                   }
                       
                  
               }
                // BuildImplicitDuplicatedAssetsSet(checkDupeResults);

            }
            else
            {
              Debug.Log("没有任何资源需要分析");
            }
           
        }

        private AddressableAssetGroup CreatDGroup(AddressableAssetSettings settings,string sameParentGroupName)
        {
            
           var group= settings.CreateGroup("Duplicate Asset Isolation"+(string.IsNullOrEmpty(sameParentGroupName)?"":" "+sameParentGroupName), false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            group.GetSchema<ContentUpdateGroupSchema>().StaticContent = true;
            return group;
        }
        private Dictionary<string, List<GroupInfo>> GetAssetsDuplicateMap(List<GroupInfo> allGroupInfos,IEnumerable<CheckDupeResult> checkDupeResults)
        {
            Dictionary<string, List<GroupInfo>> map = new Dictionary<string, List<GroupInfo>>();
            foreach (var checkDupeResult in checkDupeResults)
            {
                if (!map.ContainsKey(checkDupeResult.AssetPath))
                {
                    map.Add(checkDupeResult.AssetPath,new List<GroupInfo>());
                }

                var groupInfo = allGroupInfos.Find(x => x.GroupName == checkDupeResult.Group.name);
                map[checkDupeResult.AssetPath].Add(groupInfo);
            }

            return map;
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

        /// <summary>
        /// 获得所有的资源
        /// </summary>
        /// <param name="settings"></param>
        void CalculateInputDefinitions(AddressableAssetSettings settings)
        {
            m_AssetEntries.Clear();
            m_BundleToAssetGroup.Clear();
            m_AllBundleInputDefs.Clear();

            for (int groupIndex = 0; groupIndex < settings.groups.Count; ++groupIndex)
            {
                AddressableAssetGroup group = settings.groups[groupIndex];
                if (group == null)
                    continue;


                var schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema != null && schema.IncludeInBuild)
                {
                    List<AssetBundleBuild> bundleInputDefinitions = new List<AssetBundleBuild>();
                    m_AssetEntries.AddRange(
                        BuildScriptPackedMode.PrepGroupBundlePacking(group, bundleInputDefinitions, schema));

                    for (int i = 0; i < bundleInputDefinitions.Count; i++)
                    {
                        if (m_BundleToAssetGroup.ContainsKey(bundleInputDefinitions[i].assetBundleName))
                            bundleInputDefinitions[i] = CreateUniqueBundle(bundleInputDefinitions[i]);

                        m_BundleToAssetGroup.Add(bundleInputDefinitions[i].assetBundleName, schema.Group.Guid);
                    }

                    m_AllBundleInputDefs.AddRange(bundleInputDefinitions);
                }
            }

            //Debug.Log(Newtonsoft.Json.JsonConvert.SerializeObject(m_AllBundleInputDefs));
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
            IEnumerable<KeyValuePair<ObjectIdentifier, string>> validImplicitGuids =
                from fileToObject in m_ExtractData.WriteData.FileToObjects
                from objectId in fileToObject.Value
                where !m_ExtractData.WriteData.AssetToFiles.Keys.Contains(objectId.guid)
                select new KeyValuePair<ObjectIdentifier, string>(objectId, fileToObject.Key);

            //Build our Dictionary from our list of valid implicit guids (guids not already in explicit guids)
            foreach (var objectIdToFile in validImplicitGuids)
            {
                if (!implicitGuids.ContainsKey(objectIdToFile.Key.guid))
                    implicitGuids.Add(objectIdToFile.Key.guid, new List<string>());
                implicitGuids[objectIdToFile.Key.guid].Add(objectIdToFile.Value);
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

            buildTasks.Add(new GenerateBundleCommands());
            buildTasks.Add(new GenerateSubAssetPathMaps());
            buildTasks.Add(new GenerateBundleMaps());

            buildTasks.Add(new GenerateLocationListsTask());

            return buildTasks;
        }

        /// <summary>
        /// Check path is valid path for Addressables entry
        /// </summary>
        /// <param name="path"> The path to check</param>
        /// <returns>Whether path is valid</returns>
        protected bool IsValidPath(string path)
        {
            return AddressableAssetUtility.IsPathValidForEntry(path) &&
                   !path.ToLower().Contains("/resources/") &&
                   !path.ToLower().StartsWith("resources/");
        }
    }
}