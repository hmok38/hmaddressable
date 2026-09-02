using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HM.Editor.HMAddressable.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Presets;
using UnityEngine;
using UnityEngine.AddressableAssets;
using JsonConvert = Newtonsoft.Json.JsonConvert;
using Object = UnityEngine.Object;

namespace HM.Editor
{
    /// <summary>
    /// 依赖 com.unity.addressables.cn 包,不过会自动从unity中获取 by :hmok
    /// 输入: https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask 有时候会输入失败,多试几次
    /// 实在不行就去 https://github.com/Cysharp/UniTask.git 下载unityPackage包
    /// </summary>
    public class HMAddressablesEditor : MonoBehaviour
    {
        private const string ConfigPath = "Assets/HMAddressables/Resources/ConfigHMAddressables.asset";
        private static bool hadWrong;

        public static HMAddressablesConfig ConfigHmAddressables
        {
            get { return HMAddressableManager.HMAAConfig; }
        }

        public static HMAddressablePlatformConfig PlatformConfig
        {
            get { return HMAddressableManager.HMAddressablePlatformConfig; }
        }

        //=============================public=============================================

        [UnityEditor.MenuItem(
            @"HMAA资源管理/*************************************HMAddresablesAsset资源管理插件<点我读说明>************************",
            false, 0)]
        public static void Readme0()
        {
            Debug.Log(@"HMAddresablesAsset资源管理插件,它是基于UnityAddressablesAssets系统做得自动化打包管理工具,
资源分组和打包基于文件夹目录进行分组,并在发布游戏包体时一次性打包进入APK包,后续热更时采用增量更新的方式进行热更新,
它具有高度自动化和热更新体量小的特点,使用它完全不用关心太多资源包知识和原理,只要管理好资源目录即可");
            Debug.Log(@"
 如果有报错,则请保证Addressables的版本号在1.19.17以上 V1.0 20230310 by HM
 依赖 newtonsoft.Json包(Unity2021后内置,2021前版本请在PackageManage的UnityRegistry中搜索)
 依赖 UniTask异步插件 请在PackageManage中点+号,选择git url
 输入: https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask 有时候会输入失败,多试几次
 实在不行就去 https://github.com/Cysharp/UniTask.git 下载unityPackage包");
        }

        [UnityEditor.MenuItem(
            @"HMAA资源管理/========================打包选项<点我读说明>==============================", false, 2)]
        public static void Readme2()
        {
            Debug.Log(@"使用方法:
1:发布新游戏版本时使用 <一键打出包资源(正式包)> 选项
2:发布热更包时使用 <一键打更新资源包(正式包)> 选项
");
        }

        [UnityEditor.MenuItem("HMAA资源管理/***选择并显示配置表***", false, 1)]
        public static void ShowAndSelectConfigMenuItem()
        {
            if (ConfigHmAddressables == null)
            {
                Selection.activeObject = PlatformConfig;
                EditorGUIUtility.PingObject(Selection.activeObject);
                EditorUtility.FocusProjectWindow();
                Debug.Log("已经选择并显示配置表");
            }
            else
            {
                Selection.activeObject = ConfigHmAddressables;
                EditorGUIUtility.PingObject(Selection.activeObject);
                EditorUtility.FocusProjectWindow();
                Debug.Log("已经选择并显示配置表");
            }
        }

        [UnityEditor.MenuItem("HMAA资源管理/****一键打出包资源(正式包)****", false, 3)]
        public static void BuildAddressablesAssetsMenuItem()
        {
            hadWrong = false;
            //设置
            hadWrong = !RefreshAASetting(false);
            if (!hadWrong)
                //打包
                BuildAsset();
            if (!hadWrong)
                //刷新显示
                SaveAssetAndRefresh();
        }

        [UnityEditor.MenuItem("HMAA资源管理/****一键打更新资源包(正式包)****", false, 4)]
        public static void BuildUpdateMenuItem()
        {
            UpdateAASetting(false);

            BuildUpdateAsset();
            SaveAssetAndRefresh();
        }

        [UnityEditor.MenuItem(
            @"HMAA资源管理/========================不影响线上的热更测试<点我读说明>============================", false, 5)]
        public static void Readme4()
        {
            Debug.Log(@"测试包可以配合git使用,用来不影响线上产品的同时 测试热更是否正常,测试方式:
1,需要进行热更前,请将工程使用git回退到上一次发布的版本;
2,使用一键打出包资源(测试包) 选项打出测试资源,并打出游戏包;
3,将资源发布到测试用的资源服务器,并运行游戏包检查是否正常,此时已经准备好了跟线上游戏相同的游戏,只是资源地址不同
4,git还原修改,但不要还原数据文件(如:Assets/AddressableAssetsData/[发布平台]/addressables_content_state.bin),然后再切换到最新的版本,
5,使用一键打更新资源包(测试包) 打出测试用的热更包,然后发布到测试用的资源服务器,再次运行测试游戏包,即可在不影响线上产品的同时检查热更是否成功
");
        }

        [UnityEditor.MenuItem("HMAA资源管理/********一键打出包资源(测试包)********", false, 6)]
        public static void BuildAddressablesTestAssetsMenuItem()
        {
            hadWrong = false;
            //设置
            hadWrong = !RefreshAASetting(true);
            if (!hadWrong)
                //打包
                BuildAsset();
            if (!hadWrong)
                //刷新显示
                SaveAssetAndRefresh();
        }

        [UnityEditor.MenuItem("HMAA资源管理/********一键打更新资源包(测试包)********", false, 7)]
        public static void BuildUpdateTestMenuItem()
        {
            UpdateAASetting(true);
            BuildUpdateAsset();
            SaveAssetAndRefresh();
        }

        [UnityEditor.MenuItem(@"HMAA资源管理/====================独立配置<不需要可以无视>==========================", false, 8)]
        public static void Readme3()
        {
        }

        [UnityEditor.MenuItem("HMAA资源管理/更新(创建)资源分组并处理重复依赖 <更新包阶段禁止使用> 不会修改旧组的加密设定", false, 9)]
        private static void BuildAddressablesSettingsMenuItem()
        {
            BuildAddressablesSettingsMenuItem(false);
        }

        public static void BuildAddressablesSettingsMenuItem(bool beTest)
        {
            //设置
            RefreshAASetting(beTest);
            Debug.Log("\"更新(创建)资源分组并处理重复依赖 <更新包阶段禁止使用> 不会修改旧组的加密设定\" 完毕");
            //刷新显示
            SaveAssetAndRefresh();
        }

        [UnityEditor.MenuItem("HMAA资源管理/检查资源升级并设置升级组 <发布阶段禁止使用> ", false, 10)]
        private static void CheckForContentUpdateRestructionsMenuItem()
        {
            CheckForContentUpdateRestructionsMenuItem(false);
        }

        public static void CheckForContentUpdateRestructionsMenuItem(bool beTest)
        {
            UpdateAASetting(beTest);
            Debug.Log("\"更新(创建)资源分组并处理重复依赖 <更新包阶段禁止使用> 不会修改旧组的加密设定\" 完毕");
            SaveAssetAndRefresh();
        }

        [UnityEditor.MenuItem(
            "HMAA资源管理/========================以下为谨慎选项<除非发包,否则禁止使用>==============================", false, 11)]
        public static void Readme()
        {
        }


        [UnityEditor.MenuItem("HMAA资源管理/清理所有设置(谨慎):打包时会全部资源重新命名,之前发布的包体会更新不到资源", false, 12)]
        public static void CleanAddressablesSettingsMenuItem()
        {
            AssetDatabase.DeleteAsset(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder);
            if (Directory.Exists("ServerData"))
            {
                Directory.Delete("ServerData", true);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEditor.EditorUtility.FocusProjectWindow();

            Debug.Log("清理所有设置 完毕!已经删除Assets-AddressableAssetsData文件夹");
        }

        [UnityEditor.MenuItem("HMAA资源管理/设置打包器_打安卓IOS前请调用", false, 13)]
        public static void Test()
        {
            SetDataBuilder();
        }

        /// <summary>
        /// 设置为正式包资源
        /// </summary>
        /// <param name="beTest"></param>
        public static void SetOnlineProfiles(bool beOnline)
        {
            //设置配置表选项
            SetProfiles();
            SetActiveProfiles(!beOnline);
        }

        //========================private Coder Review=========

        /// <summary>
        /// 刷新AA资源设置
        /// </summary>
        private static bool RefreshAASetting(bool beTest)
        {
            var bo = CheckAllGroupInConfig();
            if (!bo) return false;
            //检查设置,没有就创建
            CheckAndCreateSetting();
            SetDataBuilder();
            //更新组及组内容
            CreatAndUpdateGroupAndContextFromConfig(ConfigHmAddressables);
            //设置配置表选项
            SetProfiles();
            SetActiveProfiles(beTest);

            //组设置(加密/不加密和远程/本地)
            SetAllGroupSchema();
            return true;
        }

        /// <summary>
        /// 检查是不是所有的资源组都进入了配置表
        /// </summary>
        private static bool CheckAllGroupInConfig()
        {
            return ConfigHmAddressables.CheckAllAssetsPathIsInList();
        }

        /// <summary>
        /// 刷新更新资源设置
        /// </summary>
        /// <param name="beTest"></param>
        private static void UpdateAASetting(bool beTest)
        {
            //检查设置,没有就创建
            CheckAndCreateSetting();
            //更新组设置-采用升级资源组配置
            SetUpdateGroupSetting(ConfigHmAddressables);
            //----------不确定上面2个选项会不会造成问题,因为会重新设置一些东西,但到现在还未发现有问题----------------

            //设置配置表选项
            SetProfiles();
            SetActiveProfiles(beTest);


            //检查静态组升级设置,设立升级组
            CheckForContentUpdateRestructions();
            //重新设置所有加密和远近设置
            SetAllGroupSchema();
            SetDataBuilder();
        }

        //-------------------------private------------------------------------------------
        /// <summary>
        /// 创建设置Addressables的设置文件
        /// </summary>
        private static void CheckAndCreateSetting()
        {
            if (AddressableAssetSettingsDefaultObject.Settings == null)
            {
                AddressableAssetSettingsDefaultObject.Settings = AddressableAssetSettings.Create(
                    AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
                    AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName, true, true);
                AddressableAssetSettingsDefaultObject.Settings.BuildRemoteCatalog = true;
                AddressableAssetSettingsDefaultObject.Settings.DisableCatalogUpdateOnStartup = true;
                AddressableAssetSettingsDefaultObject.Settings.MaxConcurrentWebRequests = 100;
                AddressableAssetSettingsDefaultObject.Settings.OverridePlayerVersion = string.Empty;
                HMAACustomEncryptBuild builder = ScriptableObject.CreateInstance<HMAACustomEncryptBuild>();

                if (!AssetDatabase.IsValidFolder("Assets/AddressableAssetsData"))
                {
                    AssetDatabase.CreateFolder("Assets", "AddressableAssetsData");
                }

                if (!AssetDatabase.IsValidFolder("Assets/AddressableAssetsData/DataBuilders"))
                {
                    AssetDatabase.CreateFolder("Assets/AddressableAssetsData", "DataBuilders");
                }


                AssetDatabase.CreateAsset(builder, "Assets/AddressableAssetsData/DataBuilders/HMAAEncrypt.asset");
                AssetDatabase.SaveAssets();

                // IDataBuilder builder
                //     = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                //             "Assets/AddressableAssetsData/DataBuilders/HMAAEncrypt.asset") as
                //         IDataBuilder;
                AddressableAssetSettingsDefaultObject.Settings.DataBuilders.Add(builder);
                UnityEditor.EditorUtility.SetDirty(builder);
                UnityEditor.EditorUtility.SetDirty(AddressableAssetSettingsDefaultObject.Settings);

                EditorUtility.FocusProjectWindow();
            }

            ConfigHmAddressables.CheckConfigTips();
        }

        public static void SetDataBuilder()
        {
            AddressableAssetSettings settings
                = AddressableAssetSettingsDefaultObject.Settings;


#if UNITY_2022_2_OR_NEWER
            //打包
            IDataBuilder builder
                = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                        "Assets/AddressableAssetsData/DataBuilders/BuildScriptPackedMode.asset") as
                    IDataBuilder;
#else
           //打包
            IDataBuilder builder
                = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                        "Assets/AddressableAssetsData/DataBuilders/HMAAEncrypt.asset") as
                    IDataBuilder;
#endif
            settings.ActivePlayerDataBuilderIndex
                = settings.DataBuilders.IndexOf((ScriptableObject)builder);
            Debug.Log($"打包器选用:{builder.Name}");
        }

        private static void SetUsePlayAssetDeliveryBundles(AddressablesPlayerBuildResult result)
        {
//             if (!ConfigHmAddressables.UseGooglePlayAssetDelivery) return;
// #if UNITY_ANDROID
//             Debug.Log(
//                 "重要提示:要使用谷歌资源分包 需要在打包或者导出安卓工程时在<BuildSettings>设置BuildAppBundle(GooglePlay) 或 Export for App Bundle");
// #endif
        }


        private static void BuildAsset()
        {
            SetDataBuilder();

            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (result == null)
            {
                throw new InvalidOperationException(
                    "Addressables 母包资源构建失败：BuildPlayerContent 未返回构建结果。");
            }

            if (!string.IsNullOrEmpty(result.Error))
            {
                throw new InvalidOperationException(
                    "Addressables 母包资源构建失败：" + result.Error);
            }

            Debug.Log("打出包资源完成");
            SetUsePlayAssetDeliveryBundles(result);
        }

        private static void BuildUpdateAsset()
        {
            //检查依赖关系-升级包不能检查依赖关系,因为新的依赖关系组会发布成本地包


            string assetPath = Path.Combine(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
                PlatformMappingService.GetPlatformPathSubFolder());
            var path = Path.Combine(assetPath, "addressables_content_state.bin");

            var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (obj == null)
            {
                throw new FileNotFoundException(
                    "还没打第一次的资源包，缺少 Addressables Content State：" + path,
                    path);
            }

            var contentStateDataPath = Path.Combine(assetPath, "addressables_content_state.bin");
            var cacheDataOld = ContentUpdateScript.LoadContentState(contentStateDataPath);
            if (cacheDataOld == null)
            {
                throw new InvalidDataException(
                    "Addressables Content State 读取失败：" + contentStateDataPath);
            }

            //打资源包

            AddressablesPlayerBuildResult result = ContentUpdateScript.BuildContentUpdate(
                AddressableAssetSettingsDefaultObject.Settings,
                contentStateDataPath);
            if (result == null)
            {
                throw new InvalidOperationException(
                    "Addressables 更新资源构建失败：BuildContentUpdate 未返回构建结果。" +
                    "请检查 Content State、Build Remote Catalog 和 Remote Catalog Load Path。所在路径：" +
                    contentStateDataPath);
            }

            if (!string.IsNullOrEmpty(result.Error))
            {
                throw new InvalidOperationException(
                    "Addressables 更新资源构建失败：" + result.Error);
            }


            var remoteCatalogBuildPath = AddressableAssetSettingsDefaultObject.Settings.RemoteCatalogBuildPath.GetValue(
                AddressableAssetSettingsDefaultObject.Settings);


            if (!CheckBuildinShaderAsset(cacheDataOld, remoteCatalogBuildPath))
            {
                throw new InvalidOperationException(
                    "Addressables 更新资源构建失败：Unity 内置 Shader Bundle 相比母包发生变化。" +
                    "请根据上一条错误日志修复，并恢复到打更新资源包之前再重新构建。");
            }

            Debug.Log("更新资源包打包完成");
        }

        /// <summary>
        /// 检查内置shader资源是否通过(不允许变更)
        /// </summary>
        /// <param name="oldContent"></param>
        /// <param name="outPath"></param>
        /// <returns></returns>
        private static bool CheckBuildinShaderAsset(AddressablesContentState oldContent,
            string outPath)
        {
            const string builtInShaderBundleMarker = "_unitybuiltinshaders";
            string oldName = string.Empty;
            if (oldContent.cachedBundles != null)
            {
                for (int i = 0; i < oldContent.cachedBundles.Length; i++)
                {
                    var cache = oldContent.cachedBundles[i];
                    if (IsBuiltInShaderBundle(cache.bundleFileId, builtInShaderBundleMarker))
                    {
                        oldName = GetBundleFileName(cache.bundleFileId);
                        break;
                    }
                }
            }

            var jsonPath = Path.Combine(outPath, "catalog_" + oldContent.playerVersion + ".json");
            var jsonTxt = File.ReadAllText(jsonPath);
            var obj = JsonConvert.DeserializeObject<JsonClass>(jsonTxt);
            var internalIds = obj?.m_InternalIds;

            string newName = string.Empty;
            if (internalIds != null)
            {
                for (int i = 0; i < internalIds.Length; i++)
                {
                    string internalId = internalIds[i];
                    if (IsBuiltInShaderBundle(internalId, builtInShaderBundleMarker))
                    {
                        newName = GetBundleFileName(internalId);
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(oldName) && string.IsNullOrEmpty(newName))
            {
                Debug.Log("更新资源检查通过：旧资源和新资源均未生成 Unity 内置 Shader Bundle。");
                return true;
            }

            if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"更新资源检查通过：Unity 内置 Shader Bundle 未发生变化：{oldName}");
                return true;
            }

            Debug.LogError(
                "更新资源时发现错误：Unity 内置 Shader Bundle 相比母包发生新增、删除或内容变化。" +
                "当前默认资源组不允许通过内容更新修改该共享 Bundle，请还原本次新增的内置 Shader 材质后，" +
                "恢复到打更新资源前的状态并重新构建；如果确实需要该变化，请重新发布母包。" +
                $" 旧 Bundle：{FormatBundleName(oldName)}，新 Bundle：{FormatBundleName(newName)}");

            return false;
        }

        private static bool IsBuiltInShaderBundle(string internalId, string marker)
        {
            return !string.IsNullOrEmpty(internalId) &&
                   internalId.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetBundleFileName(string internalId)
        {
            string normalizedId = internalId.Replace('\\', '/');
            int parameterIndex = normalizedId.IndexOfAny(new[] { '?', '#' });
            if (parameterIndex >= 0)
            {
                normalizedId = normalizedId.Substring(0, parameterIndex);
            }

            int slashIndex = normalizedId.LastIndexOf('/');
            return slashIndex >= 0
                ? normalizedId.Substring(slashIndex + 1)
                : normalizedId;
        }

        private static string FormatBundleName(string bundleName)
        {
            return string.IsNullOrEmpty(bundleName) ? "<不存在>" : bundleName;
        }


        private static List<GroupInfo> GetGroupInfosByFolder(HMAddressablesConfig config)
        {
            var groupInfos = new List<GroupInfo>();

            //根据配置表获取并创建资源目录结构数据
            foreach (var assetsPath in config.LocalAseetsPaths)
            {
                GetAllSubFolderAndCreateGroupInfo(assetsPath, ref groupInfos, null, true);
            }

            foreach (var assetsPath in config.RemoteAseetsPaths)
            {
                GetAllSubFolderAndCreateGroupInfo(assetsPath, ref groupInfos, null, false);
            }

            foreach (var assetsPath in config.UnassignedAssetsPath)
            {
                GetAllSubFolderAndCreateGroupInfo(assetsPath.GroupName, ref groupInfos, null,
                    assetsPath.BeLocal ? true : (assetsPath.BeRemote ? false : config.UnassignedAssetsBeLocal));
            }

            return groupInfos;
        }


        /// <summary>
        /// 刷新组设置(不更新旧组的加密设置)
        /// </summary>
        /// <param name="config"></param>
        private static void CreatAndUpdateGroupAndContextFromConfig(HMAddressablesConfig config)
        {
            if (config.ForceRemoteAssetsToLocal)
            {
                Debug.LogError("注意:强制将所有资源打入本地组的开关已经开启(配置表设置),如是有意设置可以忽略本提示!");
            }

            if (!AssetDatabase.IsValidFolder(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder))
            {
                BuildAddressablesSettingsMenuItem();
                return;
            }


            if (config.LocalAseetsPaths.Length <= 0 && config.RemoteAseetsPaths.Length <= 0 &&
                config.UnassignedAssetsPath.Length <= 0)
            {
                Debug.LogError($"未设置需要打包的资源路径,请检查{ConfigPath}的设置");
                ShowAndSelectConfigMenuItem();
                return;
            }

            var groupInfos = GetGroupInfosByFolder(config);

            if (groupInfos.Count <= 0)
            {
                Debug.LogError($"未设置需要打包的资源路径,请检查{ConfigPath}的设置");
                ShowAndSelectConfigMenuItem();
                return;
            }


            //根据配置表 创建和清理 组
            CreateAndClearGroup(groupInfos);
            //添加资源到组内
            SetAssetsToGroup(groupInfos);

            //处理重复依赖的外部资源
            var helper2 = new CalculateAddressHelper();
            helper2.CheckForDuplicateDependencies(AddressableAssetSettingsDefaultObject.Settings, groupInfos,
                out var newCreatGroups);


            //删除空组
            DeleteEmptyGroup();
            //清理空引用
            ClearGroupMissingReferences();
        }

        private static bool SetConfigsDefaultGroupInfo(List<GroupInfo> localGroupInfos)
        {
            if (localGroupInfos == null || localGroupInfos.Count <= 0)
            {
                Debug.LogError($"本地资源目录为空,请设置LocalAssetsPaths列表,并保证其至少有一个资源");

                return false;
            }

            GroupInfo defaultGroupInfo = null;
            AddressableAssetGroup defaultGroup = null;
            for (int i = 0; i < localGroupInfos.Count; i++)
            {
                var group = AddressableAssetSettingsDefaultObject.Settings.groups.Find(x =>
                {
                    return x.Name.Equals(localGroupInfos[i].GroupName);
                });
                if (group != null && group.entries.Count > 0)
                {
                    defaultGroupInfo = localGroupInfos[i];
                    defaultGroup = group;
                    break;
                }
            }

            if (defaultGroupInfo != null && defaultGroup != null)
            {
                AddressableAssetSettingsDefaultObject.Settings.DefaultGroup = defaultGroup;
                return true;
            }

            Debug.LogError(
                "未找到可作为 Addressables 默认组的非空本地资源组，请检查 LocalAseetsPaths 配置。");
            return false;
        }

        /// <summary>
        /// 根据配置,组设置(加密/不加密和远程/本地)
        /// </summary>
        private static void SetAllGroupSchema()
        {
            var groupInfos = GetGroupInfosByFolder(ConfigHmAddressables);
            if (groupInfos.Count <= 0)
            {
                Debug.LogError($"未设置需要打包的资源路径,请检查{ConfigPath}的设置");
                ShowAndSelectConfigMenuItem();
                return;
            }
            //创建本地及远程组的 DirectoryInfo 的列表

            var localDirectoryInfos = new List<DirectoryInfo>();
            foreach (var path in ConfigHmAddressables.LocalAseetsPaths)
            {
                localDirectoryInfos.Add(new DirectoryInfo(path));
            }

            var remoteDirectoryInfos = new List<DirectoryInfo>();
            foreach (var path in ConfigHmAddressables.RemoteAseetsPaths)
            {
                remoteDirectoryInfos.Add(new DirectoryInfo(path));
            }

            for (int i = 0; i < ConfigHmAddressables.UnassignedAssetsPath.Length; i++)
            {
                if (ConfigHmAddressables.UnassignedAssetsBeLocal)
                {
                    localDirectoryInfos.Add(new DirectoryInfo(ConfigHmAddressables.UnassignedAssetsPath[i].GroupName));
                }
                else
                {
                    remoteDirectoryInfos.Add(new DirectoryInfo(ConfigHmAddressables.UnassignedAssetsPath[i].GroupName));
                }
            }


            var separatelyPackDirectoryInfos = new List<DirectoryInfo>();
            foreach (var path in ConfigHmAddressables.SeparatelyPackAssetsPaths)
            {
                separatelyPackDirectoryInfos.Add(new DirectoryInfo(path));
            }

            //打包的时候才会进行修改
            for (int i = 0; i < AddressableAssetSettingsDefaultObject.Settings.groups.Count; i++)
            {
                var group = AddressableAssetSettingsDefaultObject.Settings.groups[i];
                if (group.name.Contains("Built In Data")) continue;

                if (group.name.Contains("Content Update")) //升级组
                {
                    SetGroupSchema(group, false, false);
                    continue;
                }

                //如果配置表开启了强制将远程包打入本地包,则强制修改为本地组
                if (ConfigHmAddressables.ForceRemoteAssetsToLocal)
                {
                    SetGroupSchema(group, true, true);
                }
                //没开启的话,按照其设定修改组
                else
                {
                    if (group.name.Contains("Duplicate Asset Isolation")) //重复依赖组
                    {
                        SetGroupSchema(group, !ConfigHmAddressables.DuplicateDependenciesGroupBeRemote, true,
                            CheckBeSeparatelyPackGroup(group, separatelyPackDirectoryInfos));
                    }

                    else //文件夹组
                    {
                        SetGroupSchema(group,
                            CheckGroupAssetsBeLocalGroup(group, localDirectoryInfos, remoteDirectoryInfos),
                            true, CheckBeSeparatelyPackGroup(group, separatelyPackDirectoryInfos));
                        // //根据文件夹进行分类
                        // var groupInfo = groupInfos.Find(x => x.GroupName.Replace('/', '-') == group.Name);
                        // if (groupInfo == null) Debug.Log(group.name + " 为空");
                        // if (groupInfo.BeLocalGroup)
                        // {
                        //     SetGroupSchema(group, true, true);
                        // }
                        // else
                        // {
                        //     SetGroupSchema(group, groupInfo.BeLocalGroup, true);
                        // }
                    }
                }
            }


            if (!SetConfigsDefaultGroupInfo(groupInfos))
            {
                hadWrong = true;
            }
        }

        /// <summary>
        /// 打更新包时的 设置升级组设置
        /// </summary>
        /// <param name="config"></param>
        private static void SetUpdateGroupSetting(HMAddressablesConfig config)
        {
            if (!AssetDatabase.IsValidFolder(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder))
            {
                Debug.LogError($"没找到数据文件,不能设置升级包,请恢复代码");
                return;
            }

            if (config.LocalAseetsPaths.Length <= 0 && config.RemoteAseetsPaths.Length <= 0 &&
                config.UnassignedAssetsPath.Length <= 0)
            {
                Debug.LogError($"未设置需要打包的资源路径,请检查{ConfigPath}的设置");
                ShowAndSelectConfigMenuItem();
                return;
            }

            var groupInfos = new List<GroupInfo>();

            //根据配置表获取并创建资源目录结构数据
            foreach (var assetsPath in config.LocalAseetsPaths)
            {
                GetAllSubFolderAndCreateGroupInfo(assetsPath, ref groupInfos, null, true);
            }

            foreach (var assetsPath in config.RemoteAseetsPaths)
            {
                GetAllSubFolderAndCreateGroupInfo(assetsPath, ref groupInfos, null, false);
            }

            foreach (var assetsPath in config.UnassignedAssetsPath)
            {
                GetAllSubFolderAndCreateGroupInfo(assetsPath.GroupName, ref groupInfos, null,
                    assetsPath.BeLocal ? true : (assetsPath.BeRemote ? false : config.UnassignedAssetsBeLocal));
            }

            if (groupInfos.Count <= 0)
            {
                Debug.LogError($"未设置需要打包的资源路径,请检查{ConfigPath}的设置");
                ShowAndSelectConfigMenuItem();
                return;
            }

            CreateAndClearGroup(groupInfos, true);
            SetAssetsToGroup(groupInfos, true);
            //不处理依赖

            DeleteEmptyGroup();
            ClearGroupMissingReferences();
            //不处理组模式Schema
        }


        /// <summary>
        /// 根据目录将资源添加到组内
        /// </summary>
        /// <param name="groupInfos"></param>
        /// <param name="beUpdateAssets"></param>
        private static void SetAssetsToGroup(List<GroupInfo> groupInfos, bool beUpdateAssets = false)
        {
            //先移除所有非特殊组的资源
            foreach (var groupInfo in groupInfos)
            {
                if (!groupInfo.Group.name.Equals("Built In Data")
                    && groupInfo.Group.entries.Count > 0)
                {
                    if (beUpdateAssets)
                    {
                        //如果是打更新包,重复依赖组的不删除,因为打更新资源包的时候不再处理重复依赖关系
                        //升级组也不能移除,因为可能是之前的升级组
                        if (groupInfo.Group.name.Contains("Duplicate Asset Isolation") ||
                            groupInfo.Group.name.Contains("Content Update"))
                            continue;
                    }

                    //先移除 除特殊组合更新组的 所有资源
                    var old = groupInfo.Group.entries.ToArray();
                    for (int i = 0; i < old.Length; i++)
                    {
                        groupInfo.Group.RemoveAssetEntry(old[i]);
                    }
                }
            }

            EditorUtility.SetDirty(AddressableAssetSettingsDefaultObject.Settings);
            UnityEditor.EditorUtility.FocusProjectWindow();
            //重新添加到 非特殊组
            foreach (var groupInfo in groupInfos)
            {
                //不对特殊组处理
                if (groupInfo.Group.name.Equals("Built In Data") ||
                    groupInfo.Group.name.Contains("Content Update"))
                    continue;

                var strs = AssetDatabase.FindAssets("", new[] { groupInfo.Path });
                //Debug.Log($"{groupInfo.groupName}的要添加的资源为:{strs.Length}");

                DirectoryInfo folderInfo = new DirectoryInfo(groupInfo.Path);

                foreach (var assetGuid in strs)
                {
                    // 文件夹就不添加
                    if (AssetDatabase.IsValidFolder(AssetDatabase.GUIDToAssetPath(assetGuid))) continue;
                    //判断是目录下的资源,而不是子目录下的资源
                    FileInfo fileInfo = new FileInfo(AssetDatabase.GUIDToAssetPath(assetGuid));
                    //Debug.Log(fileInfo.DirectoryName + " " + folderInfo.FullName);
                    //是子目录的资源就不添加
                    if (!fileInfo.DirectoryName.Equals(folderInfo.FullName))
                    {
                        //Debug.Log(!fileInfo.DirectoryName.Equals(folderInfo.FullName));
                        continue;
                    }

                    var address = AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(assetGuid);
                    //没找到就添加-防止从升级组移出来
                    if (address != null)
                    {
                        //Debug.Log("已经存在的不添加了: "+address.AssetPath );
                        continue;
                    }

                    //Debug.Log("添加了: "+fileInfo.Name );
                    //不是文件夹才添加,且不在 升级组 里面
                    var tmp = AddressableAssetSettingsDefaultObject.Settings.CreateOrMoveEntry(assetGuid,
                        groupInfo.Group);
                    tmp.SetLabel(groupInfo.Path, true, true);
                    EditorUtility.SetDirty(groupInfo.Group);
                }

                EditorUtility.SetDirty(AddressableAssetSettingsDefaultObject.Settings);
                UnityEditor.EditorUtility.FocusProjectWindow();
            }
        }

        private static void SetGroupSchema(AddressableAssetGroup group, bool beLocal,
            bool beStaticContent, bool beSeparatelyPack = false)
        {
            var updateGroupSchema = group.GetSchema<ContentUpdateGroupSchema>();
            updateGroupSchema.StaticContent = beLocal || (beStaticContent ? true : false);
            string buildPath =
                beLocal ? AddressableAssetSettings.kLocalBuildPath : AddressableAssetSettings.kRemoteBuildPath;
            var bundledAssetGroupSchema = group.GetSchema<BundledAssetGroupSchema>();
            ApplyBundledAssetGroupSchemaPreset(bundledAssetGroupSchema);
            bundledAssetGroupSchema.BuildPath.SetVariableByName(group.Settings,
                buildPath);
            string loadPath =
                beLocal ? AddressableAssetSettings.kLocalLoadPath : AddressableAssetSettings.kRemoteLoadPath;
            bundledAssetGroupSchema.LoadPath.SetVariableByName(group.Settings,
                loadPath);

            //统一打包或者分散打包
            bundledAssetGroupSchema.BundleMode = beSeparatelyPack
                ? BundledAssetGroupSchema.BundlePackingMode.PackSeparately
                : BundledAssetGroupSchema.BundlePackingMode.PackTogether;


            UnityEditor.EditorUtility.SetDirty(bundledAssetGroupSchema);
        }

        private static void ApplyBundledAssetGroupSchemaPreset(BundledAssetGroupSchema schema)
        {
            if (schema == null)
            {
                return;
            }

            var configuredPreset = ConfigHmAddressables?.BundledAssetGroupSchemaPreset;
            if (configuredPreset != null && configuredPreset.CanBeAppliedTo(schema))
            {
                ApplyPresetAndKeepGroup(configuredPreset, schema);
                return;
            }

            var defaultSchema = ScriptableObject.CreateInstance<BundledAssetGroupSchema>();
            var defaultPreset = new Preset(defaultSchema)
            {
                excludedProperties = new[] { "m_Group" }
            };
            try
            {
                ApplyPresetAndKeepGroup(defaultPreset, schema);
            }
            finally
            {
                Object.DestroyImmediate(defaultPreset);
                Object.DestroyImmediate(defaultSchema);
            }
        }

        private static void ApplyPresetAndKeepGroup(Preset preset, BundledAssetGroupSchema schema)
        {
            var group = schema.Group;
            preset.ApplyTo(schema);

            var serializedSchema = new SerializedObject(schema);
            var groupProperty = serializedSchema.FindProperty("m_Group");
            if (groupProperty != null)
            {
                groupProperty.objectReferenceValue = group;
                serializedSchema.ApplyModifiedPropertiesWithoutUndo();
            }
        }


        /// <summary>
        /// 获得某个文件夹的所有子文件夹,并创建组信息
        /// </summary>
        /// <param name="folder"></param>
        /// <param name="groupInfos"></param>
        /// <param name="parentGroupInfo"></param>
        /// <param name="beLocalGroup"></param>
        private static void GetAllSubFolderAndCreateGroupInfo(string folder, ref List<GroupInfo> groupInfos,
            GroupInfo parentGroupInfo, bool beLocalGroup)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return;

            var baseInfo = CreateGroupInfo(folder, parentGroupInfo, beLocalGroup);
            if (baseInfo != null)
            {
                groupInfos.Add(baseInfo);
            }

            var allAssetsGuids = AssetDatabase.FindAssets("", new[] { folder });
            var folderDirInfo = new System.IO.DirectoryInfo(folder);
            for (int i = 0; i < allAssetsGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(allAssetsGuids[i]);
                if (AssetDatabase.IsValidFolder(path)) continue;
                var dic = new System.IO.DirectoryInfo(path);
                if (dic.Parent.FullName == folderDirInfo.FullName)
                {
                    baseInfo.allAssetsInFolder.Add(path);
                }
            }


            var subFolders = AssetDatabase.GetSubFolders(folder);
            if (subFolders == null || subFolders.Length <= 0) return;
            foreach (var subFolder in subFolders)
            {
                GetAllSubFolderAndCreateGroupInfo(subFolder, ref groupInfos, baseInfo, beLocalGroup);
            }
        }

        /// <summary>
        /// 创建组信息
        /// </summary>
        /// <param name="groupPath"></param>
        /// <param name="parentGroupInfo"></param>
        /// <param name="beLocalGroup"></param>
        /// <returns></returns>
        private static GroupInfo CreateGroupInfo(string groupPath, GroupInfo parentGroupInfo, bool beLocalGroup)
        {
            var groupName = GroupNameByPath(groupPath);
            if (string.IsNullOrEmpty(groupName))
            {
                return null;
            }


            var info = new GroupInfo()
            {
                GroupName = groupName, Path = groupPath,
                MyDirectoryInfo = new DirectoryInfo(groupPath),
                MyParentGroupInfo = parentGroupInfo,

                MyChildrenGroupInfos = new List<GroupInfo>(),
                BeLocalGroup = beLocalGroup
            };
            if (parentGroupInfo != null) parentGroupInfo.MyChildrenGroupInfos.Add(info);

            return info;
        }

        private static string GroupNameByPath(string path)
        {
            System.IO.DirectoryInfo a;
            try
            {
                a = new DirectoryInfo(path);
            }
            catch
            {
                Debug.LogError($"路径不是文件夹:{path}");
                return "";
            }

            List<string> folderNames = new List<string>();
            DirectoryInfo tmpDir = a;
            while (true)
            {
                if (tmpDir.Name.Equals("Assets"))
                {
                    break;
                }

                folderNames.Add(tmpDir.Name);
                tmpDir = tmpDir.Parent;
            }

            StringBuilder s = new StringBuilder();
            s.Append("Assets");
            for (int i = folderNames.Count - 1; i >= 0; i--)
            {
                s.Append("-");
                s.Append(folderNames[i]);
            }

            return s.ToString();
        }

        private static void ClearGroupMissingReferences()
        {
            var groups = AddressableAssetSettingsDefaultObject.Settings.groups;
            List<int> missingGroupsIndices = new List<int>();
            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                if (g == null)
                    missingGroupsIndices.Add(i);
            }

            if (missingGroupsIndices.Count > 0)
            {
                // Debug.Log("Addressable settings contains " + missingGroupsIndices.Count +
                //     " group reference(s) that are no longer there. Removing reference(s).");
                for (int i = missingGroupsIndices.Count - 1; i >= 0; i--)
                {
                    groups.RemoveAt(missingGroupsIndices[i]);
                }

                AddressableAssetSettingsDefaultObject.Settings.SetDirty(
                    AddressableAssetSettings.ModificationEvent.GroupRemoved, null, true, true);
            }
        }

        /// <summary>
        /// 删除某个文件夹下所有资源
        /// </summary>
        /// <param name="folderPath"></param>
        /// <param name="beDeleteSubFolder"></param>
        /// <param name="predicate"></param>
        private static void DeleteAllSubAssetsByFolderPath(string folderPath, bool beDeleteSubFolder = false,
            System.Predicate<string> predicate = null)
        {
            var strs = AssetDatabase.FindAssets("", new[] { folderPath });
            List<string> paths = new List<string>();
            for (int i = 0; i < strs.Length; i++)
            {
                var pathTmp = AssetDatabase.GUIDToAssetPath(strs[i]);
                if (beDeleteSubFolder || !AssetDatabase.IsValidFolder(pathTmp))
                {
                    if (predicate == null || predicate.Invoke(pathTmp))
                    {
                        paths.Add(pathTmp);
                    }
                }
            }

            if (paths.Count <= 0) return;
            AssetDatabase.DeleteAssets(paths.ToArray(), new List<string> { });
            AssetDatabase.SaveAssets();
        }

        private static void DeleteEmptyGroup()
        {
            List<AddressableAssetGroup> needDeleteGroups = new List<AddressableAssetGroup>();
            for (int i = 0; i < AddressableAssetSettingsDefaultObject.Settings.groups.Count; i++)
            {
                if (!AddressableAssetSettingsDefaultObject.Settings.groups[i].name.Equals("Built In Data")
                    && AddressableAssetSettingsDefaultObject.Settings.groups[i].entries.Count <= 0)
                {
                    needDeleteGroups.Add(AddressableAssetSettingsDefaultObject.Settings.groups[i]);
                }
            }

            for (int i = 0; i < needDeleteGroups.Count; i++)
            {
                AddressableAssetSettingsDefaultObject.Settings.RemoveGroup(needDeleteGroups[i]);
            }

            UnityEditor.EditorUtility.SetDirty(AddressableAssetSettingsDefaultObject.Settings);
            EditorUtility.FocusProjectWindow();
        }

        /// <summary>
        /// 清理和创建组
        /// </summary>
        /// <param name="groupInfos"></param>
        /// <param name="beUpdateAssetGroup"></param>
        private static void CreateAndClearGroup(List<GroupInfo> groupInfos,
            bool beUpdateAssetGroup = false)
        {
            //创建组(已经存在了就不用了)
            foreach (var groupInfo in groupInfos)
            {
                var groupAssetPath = Path.Combine(AddressableAssetSettingsDefaultObject.Settings.GroupFolder,
                    groupInfo.GroupName + ".asset");
                groupInfo.Group = AssetDatabase.LoadAssetAtPath<AddressableAssetGroup>(groupAssetPath);

                if (groupInfo.Group == null)
                {
                    //没有就创建
                    groupInfo.Group = AddressableAssetSettingsDefaultObject.Settings.CreateGroup(groupInfo.GroupName,
                        false,
                        false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));

                    UnityEditor.EditorUtility.SetDirty(groupInfo.Group);

                    Debug.Log("创建" + groupInfo.GroupName);
                }
            }

            UnityEditor.EditorUtility.SetDirty(AddressableAssetSettingsDefaultObject.Settings);
            EditorUtility.FocusProjectWindow();

            //清理除 builtInGroup 组以外的不包含在groupInfos里面的组
            List<AddressableAssetGroup> needDeleteGroups = new List<AddressableAssetGroup>();
            for (int i = 0; i < AddressableAssetSettingsDefaultObject.Settings.groups.Count; i++)
            {
                var group = AddressableAssetSettingsDefaultObject.Settings.groups[i];


                if (groupInfos.Exists(x => x.Group == group)) continue; //包含的组就不用删除

                //打升级包的时候,builtInData/Content Update/Duplicate Asset Isolation组都不能删除
                if (beUpdateAssetGroup)
                {
                    if (group != null
                        && !group.name.Equals("Built In Data")
                        && !group.name.Contains("Content Update")
                        && !group.name.Contains("Duplicate Asset Isolation"))
                    {
                        needDeleteGroups.Add(group); //其他的都删除掉
                    }
                }
                else
                {
                    //不是升级的时候,除了Built In Data其他的都删除掉,重复依赖组后面重新处理
                    if (group != null
                        && !group.name.Equals("Built In Data"))
                    {
                        needDeleteGroups.Add(group); //其他的都删除掉
                    }
                }
            }

            for (int i = 0; i < needDeleteGroups.Count; i++)
            {
                AddressableAssetSettingsDefaultObject.Settings.RemoveGroup(needDeleteGroups[i]);
            }

            UnityEditor.EditorUtility.SetDirty(AddressableAssetSettingsDefaultObject.Settings);
            EditorUtility.FocusProjectWindow();
        }

        /// <summary>
        /// 设置配置文件
        /// </summary>
        private static void SetProfiles()
        {
            if (AddressableAssetSettingsDefaultObject.Settings == null)
            {
                Debug.LogErrorFormat("未初始化系统,请先运行 更新资源分组(没有就创建)");
                return;
            }

            //设置Default设置
            var defaultId = AddressableAssetSettingsDefaultObject.Settings.profileSettings.GetProfileId("Default");
            AddressableAssetSettingsDefaultObject.Settings.profileSettings.SetValue(defaultId,
                AddressableAssetSettings.kRemoteLoadPath, ConfigHmAddressables.RemoteLoadPath);
            //创建和设置TestProfile设置
            var profileId = AddressableAssetSettingsDefaultObject.Settings.profileSettings.GetProfileId("TestProfile");
            if (string.IsNullOrEmpty(profileId))
            {
                profileId = AddressableAssetSettingsDefaultObject.Settings.profileSettings.AddProfile("TestProfile",
                    defaultId);
            }

            AddressableAssetSettingsDefaultObject.Settings.profileSettings.SetValue(profileId,
                AddressableAssetSettings.kRemoteLoadPath, ConfigHmAddressables.TestRemoteLoadPath);

            //修复AA包中AddressableAssetSettings类m_RemoteCatalogLoadPath.Id == null 和 m_RemoteCatalogBuildPath.Id == null 的bug
            if (string.IsNullOrEmpty(AddressableAssetSettingsDefaultObject.Settings.RemoteCatalogBuildPath.Id))
            {
                AddressableAssetSettingsDefaultObject.Settings.RemoteCatalogBuildPath = new ProfileValueReference();
                AddressableAssetSettingsDefaultObject.Settings.RemoteCatalogBuildPath.SetVariableByName(
                    AddressableAssetSettingsDefaultObject.Settings,
                    AddressableAssetSettings.kRemoteBuildPath);
            }

            if (string.IsNullOrEmpty(AddressableAssetSettingsDefaultObject.Settings.RemoteCatalogLoadPath.Id))
            {
                AddressableAssetSettingsDefaultObject.Settings.RemoteCatalogLoadPath = new ProfileValueReference();
                AddressableAssetSettingsDefaultObject.Settings.RemoteCatalogLoadPath.SetVariableByName(
                    AddressableAssetSettingsDefaultObject.Settings,
                    AddressableAssetSettings.kRemoteLoadPath);
            }

            //设置请求Catlog文件的超时时间,大概300K左右
            AddressableAssetSettingsDefaultObject.Settings.CatalogRequestsTimeout = 10;
        }

        private static void CheckForContentUpdateRestructions()
        {
            string assetPath = Path.Combine(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
                PlatformMappingService.GetPlatformPathSubFolder());
            var path = Path.Combine(assetPath, "addressables_content_state.bin");


            var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (obj == null)
            {
                Debug.Log("还没打第一次的资源包:" + path);
                return;
            }

            var modifiedEntries =
                ContentUpdateScript.GatherModifiedEntriesWithDependencies(
                    AddressableAssetSettingsDefaultObject.Settings,
                    path);
            List<AddressableAssetEntry> items = new List<AddressableAssetEntry>();
            foreach (var entry in modifiedEntries)
            {
                items.Add(entry.Key);
                Debug.Log(entry.Key.AssetPath);
            }

            if (items.Count > 0)
            {
                CreatContentUpdateGroup(AddressableAssetSettingsDefaultObject.Settings, items,
                    "Content Update");
            }
            else
            {
                Debug.Log("没有发现需要更新的静态资源包,或之前已经 检查资源升级并设置升级组");
            }
        }

        /// <summary>
        /// 创建升级组
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="items"></param>
        /// <param name="groupName"></param>
        private static void CreatContentUpdateGroup(AddressableAssetSettings settings,
            List<AddressableAssetEntry> items, string groupName)
        {
            var contentGroup = settings.CreateGroup(FindUniqueGroupName(groupName), false, false, true, null);
            var schema = contentGroup.AddSchema<BundledAssetGroupSchema>();
            ApplyBundledAssetGroupSchemaPreset(schema);
            schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
            schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;

            var contentUpdateSchema = contentGroup.AddSchema<ContentUpdateGroupSchema>();
            contentUpdateSchema.StaticContent = false;
            settings.MoveEntries(items, contentGroup);


            UnityEditor.EditorUtility.SetDirty(contentGroup);
            UnityEditor.EditorUtility.SetDirty(schema);
            UnityEditor.EditorUtility.SetDirty(contentUpdateSchema);

            EditorUtility.FocusProjectWindow();
        }

        private static string FindUniqueGroupName(string potentialName)
        {
            var cleanedName = potentialName.Replace('/', '-');
            cleanedName = cleanedName.Replace('\\', '-');
            if (cleanedName != potentialName)
                Addressables.Log("Group names cannot include '\\' or '/'.  Replacing with '-'. " + cleanedName);
            var validName = cleanedName;
            int index = 1;
            bool foundExisting = true;
            while (foundExisting)
            {
                if (index > 1000)
                {
                    Addressables.LogError("Unable to create valid name for new Addressable Assets group.");
                    return cleanedName;
                }

                foundExisting = IsNotUniqueGroupName(validName);
                if (foundExisting)
                {
                    validName = cleanedName + index;
                    index++;
                }
            }

            return validName;
        }

        private static bool IsNotUniqueGroupName(string groupName)
        {
            bool foundExisting = false;
            foreach (var g in AddressableAssetSettingsDefaultObject.Settings.groups)
            {
                if (g != null && g.Name == groupName)
                {
                    foundExisting = true;
                    break;
                }
            }

            return foundExisting;
        }


        private static void SetActiveProfiles(bool beTest = false)
        {
            if (AddressableAssetSettingsDefaultObject.Settings == null)
            {
                Debug.LogError("AddressableAssetSettingsDefaultObject.Settings 不存在");
                return;
            }

            AddressableAssetSettingsDefaultObject.Settings.activeProfileId =
                AddressableAssetSettingsDefaultObject.Settings.profileSettings.GetProfileId(
                    beTest ? "TestProfile" : "Default");
        }

        private static DirectoryInfo assetFolderDirectoryInfo =
            new DirectoryInfo(Application.dataPath);

        private static bool CheckGroupAssetsBeLocalGroup(AddressableAssetGroup aagroup,
            List<DirectoryInfo> localDirectoryInfos, List<DirectoryInfo> remoteDirectoryInfos)
        {
            if (aagroup.entries.Count <= 0) return true; //没有资源的话会被删除,可以随便返回什么
            var entry = aagroup.entries.First();
            if (entry == null) return true;
            FileInfo entryDirectoryInfo = new FileInfo(entry.AssetPath);
            //然后一层一层父物体向上找,在哪个列表(本地/远端)组找到就是它所属的

            var parenDirectInfo = entryDirectoryInfo.Directory;
            while (true)
            {
                if (parenDirectInfo == null || assetFolderDirectoryInfo.FullName.Equals(parenDirectInfo.FullName))
                    throw new Exception($"错误:文件夹未在工程目录中 {entryDirectoryInfo.Directory.FullName}");
                if (remoteDirectoryInfos.FindIndex(x => x.FullName.Equals(parenDirectInfo.FullName)) >= 0)
                {
                    return false;
                }

                if (localDirectoryInfos.FindIndex(x => x.FullName.Equals(parenDirectInfo.FullName)) >= 0)
                {
                    return true;
                }

                parenDirectInfo = parenDirectInfo.Parent;
            }

            return true;
        }

        private static void SaveAssetAndRefresh()
        {
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
            UnityEditor.EditorUtility.FocusProjectWindow();
            EditorUtility.RequestScriptReload();
        }

        private static bool CheckBeSeparatelyPackGroup(AddressableAssetGroup aagroup,
            List<DirectoryInfo> separatelyDirectoryInfos)
        {
            if (aagroup == null || aagroup.entries.Count <= 0 || separatelyDirectoryInfos.Count <= 0)
                return false;

            var entry = aagroup.entries.FirstOrDefault(x => x != null && !string.IsNullOrEmpty(x.AssetPath));
            var entryDirectoryInfo = entry == null ? null : new FileInfo(entry.AssetPath).Directory;
            if (entryDirectoryInfo == null) return false;

            // Inspector 中每个资源目录都有独立的“分散”开关，因此这里只匹配当前组所在目录，
            // 不再向父目录查找，避免父目录开启后所有子目录组也被设置为 PackSeparately。
            return separatelyDirectoryInfos.Any(x =>
                string.Equals(x.FullName, entryDirectoryInfo.FullName, StringComparison.OrdinalIgnoreCase));
        }
    }

    public class GroupInfo
    {
        public string Path;
        public string GroupName;
        public System.IO.DirectoryInfo MyDirectoryInfo;
        public GroupInfo MyParentGroupInfo;
        public List<GroupInfo> MyChildrenGroupInfos;
        public AddressableAssetGroup Group;
        public bool BeLocalGroup;
        public List<string> allAssetsInFolder = new List<string>();
        public AddressableAssetGroup DuplicateAssetIsolationGroup;
    }

    public class JsonClass
    {
        public string m_LocatorId;
        public string[] m_InternalIds;
    }
}
