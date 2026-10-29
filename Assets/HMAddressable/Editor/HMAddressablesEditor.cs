using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HM.Editor.HMAddressable.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.Compilation;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Presets;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
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
        public static HMAddressablesConfig ConfigHmAddressables
        {
            get
            {
                var platformConfig = PlatformConfig;
                return platformConfig == null ? null : platformConfig.CurrentConfig;
            }
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
            RefreshAASetting(false);
            BuildAsset();
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
            RefreshAASetting(true);
            BuildAsset();
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
        private static void RefreshAASetting(bool beTest)
        {
            var groupInfos = ValidateBuildInputs(false, beTest);
            CheckAndCreateSetting();
            SetDataBuilder();
            SetProfiles();
            SetActiveProfiles(beTest);
            CreatAndUpdateGroupAndContextFromConfig(groupInfos);
        }

        /// <summary>
        /// 刷新更新资源设置
        /// </summary>
        /// <param name="beTest"></param>
        private static void UpdateAASetting(bool beTest)
        {
            var groupInfos = ValidateBuildInputs(true, beTest);
            SetDataBuilder();
            SetProfiles();
            SetActiveProfiles(beTest);
            CreateGroups(groupInfos);
            SetAssetsToGroup(groupInfos, true);
            SetAllGroupSchema(groupInfos, true);
            CheckForContentUpdateRestructions();
        }

        private static List<GroupInfo> ValidateBuildInputs(bool beUpdate, bool beTest)
        {
            var config = ConfigHmAddressables;
            if (config == null || !config.CheckAllAssetsPathIsInList())
                throw new InvalidOperationException("HMAA 平台配置或资源目录无效，已停止打包，未修改资源组。");
            if (string.IsNullOrWhiteSpace(beTest ? config.TestRemoteLoadPath : config.RemoteLoadPath))
                throw new InvalidOperationException("当前构建环境的远程资源地址不能为空。");

            var groupInfos = GetGroupInfosByFolder(config);
            if (!groupInfos.Any(info => info.allAssetsInFolder.Any(IsPackableAssetPath)))
                throw new InvalidOperationException("配置目录中没有可打包的 Addressables 资源，已停止打包。");
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var info in groupInfos)
            {
                if (info.GroupName.IndexOf("Content Update", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    info.GroupName.IndexOf("Duplicate Asset Isolation", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    info.GroupName.IndexOf("Built In Data", StringComparison.OrdinalIgnoreCase) >= 0)
                    throw new InvalidOperationException($"资源目录使用了保留组名：{info.Path}。请先调整目录配置。");
                if (names.TryGetValue(info.GroupName, out var otherPath))
                    throw new InvalidOperationException($"目录 {otherPath} 和 {info.Path} 生成了相同组名：{info.GroupName}。");
                names.Add(info.GroupName, info.Path);
            }

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (beUpdate && settings == null)
                throw new InvalidOperationException("热更必须使用目标母包的 Addressables Settings，不能重新创建设置。");

            IDataBuilder builder = settings == null ? null : GetConfiguredDataBuilder(settings);
            ValidateSchemaProviders(null, builder);
            if (settings == null) return groupInfos;

            var profiles = settings.profileSettings;
            var defaultId = profiles.GetProfileId("Default");
            if (string.IsNullOrEmpty(defaultId))
                throw new InvalidOperationException("缺少 Addressables Default Profile，已停止打包。");
            foreach (var variable in new[] { AddressableAssetSettings.kLocalBuildPath,
                         AddressableAssetSettings.kLocalLoadPath, AddressableAssetSettings.kRemoteBuildPath,
                         AddressableAssetSettings.kRemoteLoadPath })
            {
                if (!profiles.GetVariableNames().Contains(variable))
                    throw new InvalidOperationException($"Addressables Profile 缺少变量：{variable}。");
            }

            var groupNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in settings.groups)
            {
                if (group == null)
                    throw new InvalidOperationException("Addressables Groups 中存在丢失引用，请先恢复或移除该引用。");
                if (!groupNames.Add(group.Name))
                    throw new InvalidOperationException($"存在重复的 Addressables 组名：{group.Name}。");
                if (group.HasSchema<PlayerDataGroupSchema>()) continue;
                if (group.GetSchema<BundledAssetGroupSchema>() == null ||
                    group.GetSchema<ContentUpdateGroupSchema>() == null)
                    throw new InvalidOperationException($"资源组 {group.Name} 缺少打包或内容更新 Schema。");
                if (group.ReadOnly)
                    throw new InvalidOperationException($"资源组 {group.Name} 为只读，不能自动整理。");
                if (!Enum.IsDefined(typeof(BundledAssetGroupSchema.BundlePackingMode), group.GetSchema<BundledAssetGroupSchema>().BundleMode))
                    throw new InvalidOperationException($"资源组 {group.Name} 的 BundleMode 无效。");
                bool keepsSchema = beUpdate && !names.ContainsKey(group.Name) &&
                    !IsContentUpdateGroup(group) && !IsDuplicateGroup(group);
                ValidateSchemaProviders(group.GetSchema<BundledAssetGroupSchema>(), builder, !keepsSchema);
                if (beUpdate && !names.ContainsKey(group.Name) &&
                    group.entries.Any(entry => !string.IsNullOrEmpty(entry.AssetPath) &&
                        !groupInfos.Any(info => string.Equals(info.Path, Path.GetDirectoryName(entry.AssetPath)?.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))) &&
                    !IsContentUpdateGroup(group) && !IsDuplicateGroup(group))
                    throw new InvalidOperationException($"母包资源组 {group.Name} 不在当前目录配置中，请恢复配置，不能在热更时删除该组。");
            }

            foreach (var info in groupInfos)
            {
                var path = settings.GroupFolder + "/" + info.GroupName + ".asset";
                var existing = AssetDatabase.LoadMainAssetAtPath(path);
                var registered = settings.FindGroup(info.GroupName);
                if (existing != null && existing != registered)
                    throw new InvalidOperationException($"组资源路径被未登记或其他类型的资源占用：{path}。");
            }

            if (beUpdate)
            {
                // 读取序列化 GUID，避免 DefaultGroup 的 getter 自动挑选其他组。
                var defaultGuid = new SerializedObject(settings).FindProperty("m_DefaultGroup").stringValue;
                var defaultGroup = settings.groups.Find(group => group.Guid == defaultGuid);
                if (defaultGroup == null || defaultGroup.GetSchema<BundledAssetGroupSchema>() == null)
                    throw new InvalidOperationException("母包默认组丢失，请恢复原默认组及其 GUID 后再热更。");
                var state = LoadContentStateForUpdate();
                var currentGroupGuids = new HashSet<string>(settings.groups.Select(group => group.Guid));
                var missingGroupGuids = state.cachedInfos.Where(info => info != null &&
                    !currentGroupGuids.Contains(info.groupGuid)).Select(info => info.groupGuid).Distinct().ToArray();
                if (missingGroupGuids.Length > 0)
                    throw new InvalidOperationException("母包状态引用的资源组已丢失，请恢复目标母包的组资源及 GUID：" + string.Join(", ", missingGroupGuids));
                if (!settings.BuildRemoteCatalog || string.IsNullOrEmpty(state.remoteCatalogLoadPath))
                    throw new InvalidOperationException("母包和当前设置都必须开启 Build Remote Catalog 才能热更。");
                var profileId = beTest ? profiles.GetProfileId("TestProfile") : defaultId;
                if (string.IsNullOrEmpty(profileId)) profileId = defaultId;
                var remoteLoadPath = beTest ? config.TestRemoteLoadPath : config.RemoteLoadPath;
                var catalogVariable = settings.RemoteCatalogLoadPath?.Id;
                var catalogPath = string.IsNullOrEmpty(catalogVariable) ||
                                  profiles.GetProfileDataById(catalogVariable)?.ProfileName == AddressableAssetSettings.kRemoteLoadPath
                    ? remoteLoadPath
                    : profiles.GetValueById(profileId, catalogVariable);
                catalogPath = UnityEngine.AddressableAssets.Initialization.AddressablesRuntimeProperties.EvaluateString(
                    catalogPath, '[', ']', variable =>
                    {
                        if (variable == AddressableAssetSettings.kRemoteLoadPath) return remoteLoadPath;
                        var value = profiles.GetValueByName(profileId, variable);
                        return string.IsNullOrEmpty(value) ? profiles.EvaluateString(profileId, "[" + variable + "]") : value;
                    });
                if (!string.Equals(state.remoteCatalogLoadPath, catalogPath, StringComparison.Ordinal))
                    throw new InvalidOperationException($"热更 Catalog 地址与母包不一致。母包：{state.remoteCatalogLoadPath}，当前：{catalogPath}。");
            }
            return groupInfos;
        }

        private static void ValidateSchemaProviders(BundledAssetGroupSchema source, IDataBuilder builder, bool applyPreset = true)
        {
            var schema = ScriptableObject.CreateInstance<BundledAssetGroupSchema>();
            try
            {
                schema.AssetBundleProviderType = source != null ? source.AssetBundleProviderType :
                    new UnityEngine.ResourceManagement.Util.SerializedType { Value = typeof(AssetBundleProvider) };
                schema.BundledAssetProviderType = source != null ? source.BundledAssetProviderType :
                    new UnityEngine.ResourceManagement.Util.SerializedType { Value = typeof(BundledAssetProvider) };
                if (applyPreset) ApplyBundledAssetGroupSchemaPreset(schema);
                var editorAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor).Select(x => x.name);
                var playerAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player).Select(x => x.name);
                var editorOnlyAssemblies = new HashSet<string>(editorAssemblies.Except(playerAssemblies));
                foreach (var provider in new[] { schema.BundledAssetProviderType.Value, schema.AssetBundleProviderType.Value })
                {
                    if (provider == null || !typeof(IResourceProvider).IsAssignableFrom(provider) ||
                        provider == typeof(AssetDatabaseProvider) || provider.IsAbstract || provider.ContainsGenericParameters ||
                        editorOnlyAssemblies.Contains(provider.Assembly.GetName().Name) ||
                        provider.Assembly.GetName().Name.StartsWith("UnityEditor", StringComparison.Ordinal))
                        throw new InvalidOperationException($"资源组 {source?.Group?.Name ?? "新建组"} 的 Provider 无效或仅编辑器可用：{provider}。请检查平台 preset。");
                    bool usesLegacyBuilder = builder is HMAACustomEncryptBuild;
#if !UNITY_2022_2_OR_NEWER
                    if (builder == null) usesLegacyBuilder = true;
#endif
                    if (typeof(HMAAEncrypt_AssetBundleProvider).IsAssignableFrom(provider) && !usesLegacyBuilder)
                        throw new InvalidOperationException($"Provider {provider.Name} 依赖旧加密打包数据，不能配合当前打包器使用。请在 preset 中改用兼容的 Provider。");
                }
            }
            finally
            {
                Object.DestroyImmediate(schema);
            }
        }

        private static string GetContentStatePath()
        {
            return Path.Combine(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
                PlatformMappingService.GetPlatformPathSubFolder(), "addressables_content_state.bin");
        }

        private static AddressablesContentState LoadContentStateForUpdate()
        {
            var path = GetContentStatePath();
            if (!File.Exists(path))
                throw new FileNotFoundException("缺少目标母包的 Addressables Content State：" + path, path);
            var state = ContentUpdateScript.LoadContentState(path);
            if (state == null || state.cachedInfos == null || state.cachedBundles == null || string.IsNullOrEmpty(state.playerVersion))
                throw new InvalidDataException("Addressables Content State 无效，不能检查或构建热更：" + path);
            return state;
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
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var builder = GetConfiguredDataBuilder(settings);
            settings.ActivePlayerDataBuilderIndex = settings.DataBuilders.IndexOf((ScriptableObject)builder);
            Debug.Log($"打包器选用:{builder.Name}");
        }

        private static IDataBuilder GetConfiguredDataBuilder(AddressableAssetSettings settings)
        {
            if (settings == null) throw new InvalidOperationException("Addressables Settings 不存在，不能选择打包器。");
#if UNITY_2022_2_OR_NEWER
            const string builderName = "BuildScriptPackedMode.asset";
#else
            const string builderName = "HMAAEncrypt.asset";
#endif
            var path = settings.ConfigFolder + "/DataBuilders/" + builderName;
            var builder = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) as IDataBuilder;
            if (builder == null || !settings.DataBuilders.Contains((ScriptableObject)builder) ||
                !builder.CanBuildData<AddressablesPlayerBuildResult>())
                throw new InvalidOperationException("打包器不存在、未登记或不能构建资源包：" + path);
            return builder;
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


            var contentStateDataPath = GetContentStatePath();
            var cacheDataOld = LoadContentStateForUpdate();

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


            if (!CheckBuildinShaderAsset(cacheDataOld, result))
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
        /// <param name="result"></param>
        /// <returns></returns>
        private static bool CheckBuildinShaderAsset(AddressablesContentState oldContent,
            AddressablesPlayerBuildResult result)
        {
            const string builtInShaderBundleMarker = "_unitybuiltinshaders";
            var oldBundles = oldContent.cachedBundles.Where(x =>
                IsBuiltInShaderBundle(x.bundleFileId, builtInShaderBundleMarker) ||
                IsBuiltInShaderBundle((x.data as AssetBundleRequestOptions)?.BundleName, builtInShaderBundleMarker)).ToList();
            var newBundles = result.AssetBundleBuildResults.Where(x =>
                IsBuiltInShaderBundle(x.InternalBundleName, builtInShaderBundleMarker)).ToList();
            if (oldBundles.Count == 0 && newBundles.Count == 0)
            {
                Debug.Log("更新资源检查通过：旧资源和新资源均未生成 Unity 内置 Shader Bundle。");
                return true;
            }

            if (oldBundles.Count == 1 && newBundles.Count == 1)
            {
                var oldBundle = oldBundles[0];
                var oldOptions = oldBundle.data as AssetBundleRequestOptions;
                var newBundle = newBundles[0];
                bool sameContent = false;
                if (!string.IsNullOrEmpty(oldOptions?.Hash))
                    sameContent = string.Equals(oldOptions.Hash, newBundle.Hash, StringComparison.OrdinalIgnoreCase);
                else if (oldOptions != null && oldOptions.Crc != 0)
                    sameContent = oldOptions.Crc == newBundle.Crc;
                else if (oldOptions != null && !string.IsNullOrEmpty(newBundle.Hash))
                    sameContent = GetBundleFileName(oldBundle.bundleFileId).IndexOf(newBundle.Hash, StringComparison.OrdinalIgnoreCase) >= 0;
                if (sameContent && string.Equals(oldOptions.BundleName, newBundle.InternalBundleName, StringComparison.Ordinal))
                {
                    Debug.Log($"更新资源检查通过：Unity 内置 Shader Bundle 内容未变：{newBundle.InternalBundleName}");
                    return true;
                }
            }

            Debug.LogError(
                "Unity 内置 Shader Bundle 的标识或内容发生变化，或母包未保留可比较的 Hash/CRC，不能确认热更兼容。" +
                "请恢复目标母包的默认组和 Shader 内容；确实需要变更时应重新发布母包。" +
                $" 旧 Bundle：{string.Join(", ", oldBundles.Select(x => x.bundleFileId))}；" +
                $"新 Bundle：{string.Join(", ", newBundles.Select(x => x.InternalBundleName + " / " + x.Hash))}");
            return false;
        }

        private static bool IsBuiltInShaderBundle(string internalId, string marker)
        {
            return !string.IsNullOrEmpty(internalId) &&
                   internalId.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetBundleFileName(string internalId)
        {
            string normalizedId = (internalId ?? string.Empty).Replace('\\', '/');
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


        private static List<GroupInfo> GetGroupInfosByFolder(HMAddressablesConfig config)
        {
            var groupInfos = new List<GroupInfo>();
            foreach (var path in config.AAAssetsPath.Select(HMAddressablesConfig.NormalizeAssetFolder).OrderBy(x => x.Length))
            {
                GetAllSubFolderAndCreateGroupInfo(path, ref groupInfos, null, true);
            }
            var loadPaths = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in config.LocalAseetsPaths) loadPaths.Add(HMAddressablesConfig.NormalizeAssetFolder(path), true);
            foreach (var path in config.RemoteAseetsPaths) loadPaths.Add(HMAddressablesConfig.NormalizeAssetFolder(path), false);
            foreach (var item in config.UnassignedAssetsPath)
            {
                loadPaths.Add(HMAddressablesConfig.NormalizeAssetFolder(item.GroupName),
                    item.BeLocal || (!item.BeRemote && config.UnassignedAssetsBeLocal));
            }
            foreach (var info in groupInfos)
            {
                var path = info.Path;
                info.BeLocalGroup = config.UnassignedAssetsBeLocal;
                while (!string.IsNullOrEmpty(path))
                {
                    if (loadPaths.TryGetValue(path, out var beLocal))
                    {
                        info.BeLocalGroup = beLocal;
                        break;
                    }
                    int slash = path.LastIndexOf('/');
                    path = slash < 0 ? null : path.Substring(0, slash);
                }
            }
            return groupInfos;
        }


        /// <summary>
        /// 同步母包资源组，应用最终设置后再分析重复依赖。
        /// </summary>
        /// <param name="groupInfos"></param>
        private static void CreatAndUpdateGroupAndContextFromConfig(List<GroupInfo> groupInfos)
        {
            if (ConfigHmAddressables.ForceRemoteAssetsToLocal)
            {
                Debug.LogWarning("配置已开启强制本地：普通资源组和重复依赖组将使用本地路径，打包模式仍由目录的分散开关决定。");
            }
            CreateGroups(groupInfos);
            SetAssetsToGroup(groupInfos);
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var obsoleteGroups = settings.groups.Where(group => group != null &&
                !group.HasSchema<PlayerDataGroupSchema>() && !groupInfos.Any(info => info.Group == group)).ToArray();
            var dependencyMetadata = new Dictionary<string, (string address, string[] labels, bool readOnly)>();
            foreach (var group in obsoleteGroups)
            {
                foreach (var entry in group.entries)
                {
                    var automaticLabel = FindAutomaticGroupLabel(entry);
                    dependencyMetadata[entry.guid] = (entry.address,
                        entry.labels.Where(label => label != automaticLabel).ToArray(), entry.ReadOnly);
                }
            }
            foreach (var group in obsoleteGroups) settings.RemoveGroup(group);
            SetAllGroupSchema(groupInfos);
            // 分析必须使用最终 Schema，否则修改分散开关后会沿用旧的依赖结果。
            var helper2 = new CalculateAddressHelper();
            helper2.CheckForDuplicateDependencies(AddressableAssetSettingsDefaultObject.Settings, groupInfos,
                out var newCreatGroups);
            foreach (var group in newCreatGroups)
            {
                foreach (var entry in group.entries)
                {
                    if (!dependencyMetadata.TryGetValue(entry.guid, out var metadata)) continue;
                    entry.SetAddress(metadata.address);
                    foreach (var label in metadata.labels) entry.SetLabel(label, true, true);
                    entry.ReadOnly = metadata.readOnly;
                    Debug.Assert(entry.address == metadata.address && metadata.labels.All(entry.labels.Contains),
                        $"重复依赖条目 {entry.guid} 的 Address 或自定义 Labels 未恢复。");
                }
                SetGroupSchema(group, ConfigHmAddressables.ForceRemoteAssetsToLocal ||
                    !ConfigHmAddressables.DuplicateDependenciesGroupBeRemote, true,
                    BundledAssetGroupSchema.BundlePackingMode.PackTogether);
            }
            DeleteEmptyGroup();
            ClearGroupMissingReferences();
        }

        private static void SetConfigsDefaultGroupInfo(List<GroupInfo> groupInfos)
        {
            var defaultGroup = groupInfos.OrderByDescending(info => info.BeLocalGroup)
                .Select(info => info.Group).FirstOrDefault(group => group != null && group.entries.Count > 0);
            if (defaultGroup == null)
                throw new InvalidOperationException("未找到可作为 Addressables 默认组的非空资源组，已停止打包。");
            AddressableAssetSettingsDefaultObject.Settings.DefaultGroup = defaultGroup;
        }

        /// <summary>
        /// 根据配置设置组，热更时保留各组的打包模式。
        /// </summary>
        private static void SetAllGroupSchema(List<GroupInfo> groupInfos, bool beUpdateAssetGroup = false)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var separatelyPaths = new HashSet<string>(
                ConfigHmAddressables.SeparatelyPackAssetsPaths.Select(HMAddressablesConfig.NormalizeAssetFolder),
                StringComparer.OrdinalIgnoreCase);
            foreach (var group in settings.groups)
            {
                if (group == null || group.HasSchema<PlayerDataGroupSchema>()) continue;
                var schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema == null)
                    throw new InvalidOperationException($"资源组 {group.Name} 缺少 BundledAssetGroupSchema。");
                var bundleMode = schema.BundleMode;
                if (IsContentUpdateGroup(group))
                {
                    SetGroupSchema(group, false, false, bundleMode);
                    continue;
                }

                var info = groupInfos.Find(x => x.Group == group);
                bool isDuplicate = IsDuplicateGroup(group);
                // 热更保留已退出目录配置的空组及默认组，避免改变母包的组标识。
                if (beUpdateAssetGroup && info == null && !isDuplicate) continue;
                if (info == null && !isDuplicate)
                    throw new InvalidOperationException($"资源组 {group.Name} 未匹配到目录配置。");
                if (!beUpdateAssetGroup)
                {
                    bundleMode = info != null && separatelyPaths.Contains(info.Path)
                        ? BundledAssetGroupSchema.BundlePackingMode.PackSeparately
                        : BundledAssetGroupSchema.BundlePackingMode.PackTogether;
                }
                bool beLocal = ConfigHmAddressables.ForceRemoteAssetsToLocal ||
                    (isDuplicate ? !ConfigHmAddressables.DuplicateDependenciesGroupBeRemote : info.BeLocalGroup);
                SetGroupSchema(group, beLocal, true, bundleMode);
            }

            if (!beUpdateAssetGroup) SetConfigsDefaultGroupInfo(groupInfos);
        }

        private static bool IsContentUpdateGroup(AddressableAssetGroup group)
        {
            if (group == null) return false;
            if (group.Name.StartsWith("Content Update", StringComparison.Ordinal)) return true;
            const string marker = "_Content Update_";
            int index = group.Name.LastIndexOf(marker, StringComparison.Ordinal);
            return index >= 0 && int.TryParse(group.Name.Substring(index + marker.Length), out var sequence) && sequence > 0;
        }

        private static bool IsDuplicateGroup(AddressableAssetGroup group)
        {
            return group != null && (group.Name == "Duplicate Asset Isolation" ||
                group.Name.StartsWith("Duplicate Asset Isolation ", StringComparison.Ordinal));
        }

        /// <summary>
        /// 增量同步目录资源，保留已有条目的 Address 和 Labels。
        /// </summary>
        private static void SetAssetsToGroup(List<GroupInfo> groupInfos, bool beUpdateAssets = false)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var desiredEntries = new Dictionary<string, GroupInfo>();
            foreach (var info in groupInfos)
            {
                foreach (var guid in AssetDatabase.FindAssets("", new[] { info.Path }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!IsPackableAssetPath(path)) continue;
                    var directory = Path.GetDirectoryName(path)?.Replace('\\', '/');
                    if (!string.Equals(directory, info.Path, StringComparison.OrdinalIgnoreCase)) continue;
                    desiredEntries[guid] = info;
                }
            }

            foreach (var pair in desiredEntries)
            {
                var entry = settings.FindAssetEntry(pair.Key);
                if (entry != null)
                {
                    SynchronizeDirectoryLabel(entry, pair.Value.Path);
                    if (beUpdateAssets && (IsContentUpdateGroup(entry.parentGroup) || IsDuplicateGroup(entry.parentGroup)))
                        continue;
                    if (entry.parentGroup != pair.Value.Group)
                        settings.MoveEntry(entry, pair.Value.Group, entry.ReadOnly);
                    Debug.Assert(ReferenceEquals(settings.FindAssetEntry(pair.Key), entry),
                        $"资源条目 {pair.Key} 在同步时被重新创建。");
                    continue;
                }
                entry = settings.CreateOrMoveEntry(pair.Key, pair.Value.Group);
                if (entry == null)
                    throw new InvalidOperationException("无法创建 Addressables 条目：" + AssetDatabase.GUIDToAssetPath(pair.Key));
                entry.SetLabel(pair.Value.Path, true, true);
            }

            // 完成移动后再移除失效条目，避免跨目录移动时丢失自定义 Address 和 Labels。
            foreach (var group in settings.groups.ToArray())
            {
                if (group == null || group.HasSchema<PlayerDataGroupSchema>() ||
                    IsContentUpdateGroup(group) || IsDuplicateGroup(group)) continue;
                foreach (var entry in group.entries.ToArray())
                {
                    if (!desiredEntries.ContainsKey(entry.guid))
                        settings.RemoveAssetEntry(entry.guid);
                }
                EditorUtility.SetDirty(group);
            }
            EditorUtility.SetDirty(settings);
        }

        private static void SynchronizeDirectoryLabel(AddressableAssetEntry entry, string directory)
        {
            var previousLabel = FindAutomaticGroupLabel(entry, directory);
            if (previousLabel != null && previousLabel != directory) entry.SetLabel(previousLabel, false);
            entry.SetLabel(directory, true, true);
            Debug.Assert(entry.labels.Contains(directory) &&
                (previousLabel == null || previousLabel == directory || !entry.labels.Contains(previousLabel)),
                $"资源条目 {entry.guid} 的目录标签未正确同步。");
        }

        private static string FindAutomaticGroupLabel(AddressableAssetEntry entry, string currentDirectory = null)
        {
            string groupName = entry.parentGroup.Name;
            const string marker = "_Content Update_";
            int markerIndex = groupName.LastIndexOf(marker, StringComparison.Ordinal);
            bool hasSourceGroup = markerIndex >= 0 &&
                int.TryParse(groupName.Substring(markerIndex + marker.Length), out var sequence) && sequence > 0;
            string sourceName = hasSourceGroup ? groupName.Substring(0, markerIndex) : groupName;
            bool truncatedSource = hasSourceGroup && sourceName.Length == 48;
            var candidates = entry.labels.Where(label => MatchesAutomaticGroupLabel(label, sourceName, truncatedSource)).ToArray();
            if (candidates.Length == 1) return candidates[0];

            // 历史条目没有标签来源记录，不能把目录形式的自定义标签直接删除。
            if ((currentDirectory == null || !entry.labels.Contains(currentDirectory)) &&
                entry.labels.Any(label => label == "Duplicate Asset Isolation" || IsDirectoryLabel(label)))
            {
                Debug.LogWarning($"资源 {entry.AssetPath}（{entry.guid}）在组 {groupName} 中的自动标签无法唯一识别，" +
                    "已保留原标签，请人工检查是否需要删除旧目录标签。");
            }
            return null;
        }

        private static bool MatchesAutomaticGroupLabel(string label, string sourceName, bool truncatedSource)
        {
            string normalized = label.Replace('\\', '/').TrimEnd('/');
            if (normalized == "Duplicate Asset Isolation") return sourceName == normalized;
            if (!IsDirectoryLabel(normalized)) return false;
            string directoryGroup = normalized.Replace('/', '-');
            string duplicateGroup = "Duplicate Asset Isolation " + directoryGroup;
            if (truncatedSource)
            {
                directoryGroup = directoryGroup.Substring(0, Math.Min(48, directoryGroup.Length));
                duplicateGroup = duplicateGroup.Substring(0, Math.Min(48, duplicateGroup.Length));
            }
            return string.Equals(sourceName, directoryGroup, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sourceName, duplicateGroup, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDirectoryLabel(string label)
        {
            string normalized = label.Replace('\\', '/').TrimEnd('/');
            return normalized == "Assets" || normalized.StartsWith("Assets/", StringComparison.Ordinal);
        }

        private static bool IsPackableAssetPath(string path)
        {
            return !AssetDatabase.IsValidFolder(path) && AddressableAssetUtility.IsPathValidForEntry(path);
        }

        private static void SetGroupSchema(AddressableAssetGroup group, bool beLocal,
            bool beStaticContent, BundledAssetGroupSchema.BundlePackingMode bundleMode)
        {
            var updateGroupSchema = group.GetSchema<ContentUpdateGroupSchema>();
            updateGroupSchema.StaticContent = beLocal || (beStaticContent ? true : false);
            string buildPath =
                beLocal ? AddressableAssetSettings.kLocalBuildPath : AddressableAssetSettings.kRemoteBuildPath;
            var bundledAssetGroupSchema = group.GetSchema<BundledAssetGroupSchema>();
            ApplyBundledAssetGroupSchemaPreset(bundledAssetGroupSchema);
            if (!bundledAssetGroupSchema.BuildPath.SetVariableByName(group.Settings, buildPath))
                throw new InvalidOperationException($"资源组 {group.Name} 无法设置 BuildPath：{buildPath}。");
            string loadPath =
                beLocal ? AddressableAssetSettings.kLocalLoadPath : AddressableAssetSettings.kRemoteLoadPath;
            if (!bundledAssetGroupSchema.LoadPath.SetVariableByName(group.Settings, loadPath))
                throw new InvalidOperationException($"资源组 {group.Name} 无法设置 LoadPath：{loadPath}。");

            // 打包模式由调用方决定，不能被 preset 中的默认值覆盖。
            bundledAssetGroupSchema.BundleMode = bundleMode;
            EditorUtility.SetDirty(updateGroupSchema);
            UnityEditor.EditorUtility.SetDirty(bundledAssetGroupSchema);
        }

        private static void ApplyBundledAssetGroupSchemaPreset(BundledAssetGroupSchema schema)
        {
            if (schema == null)
            {
                return;
            }

            var configuredPreset = ConfigHmAddressables?.BundledAssetGroupSchemaPreset;
            if (configuredPreset != null)
            {
                if (!configuredPreset.CanBeAppliedTo(schema))
                    throw new InvalidOperationException("平台配置中的 preset 不是 BundledAssetGroupSchema，已停止打包。");
                ApplyPresetAndKeepGroup(configuredPreset, schema);
                return;
            }

            var defaultSchema = ScriptableObject.CreateInstance<BundledAssetGroupSchema>();
            defaultSchema.AssetBundleProviderType = new UnityEngine.ResourceManagement.Util.SerializedType { Value = typeof(AssetBundleProvider) };
            defaultSchema.BundledAssetProviderType = new UnityEngine.ResourceManagement.Util.SerializedType { Value = typeof(BundledAssetProvider) };
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
            if (!preset.ApplyTo(schema))
                throw new InvalidOperationException("无法应用 BundledAssetGroupSchema preset：" + preset.name);

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
            if (groupInfos.Any(info => string.Equals(info.Path, folder, StringComparison.OrdinalIgnoreCase))) return;

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
            return HMAddressablesConfig.NormalizeAssetFolder(path).Replace('/', '-');
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
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            List<AddressableAssetGroup> needDeleteGroups = new List<AddressableAssetGroup>();
            for (int i = 0; i < AddressableAssetSettingsDefaultObject.Settings.groups.Count; i++)
            {
                var group = settings.groups[i];
                if (group != null && group != settings.DefaultGroup && !group.HasSchema<PlayerDataGroupSchema>()
                    && group.entries.Count <= 0)
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
        /// 创建缺失的目录组，不修改已有组的打包模式。
        /// </summary>
        /// <param name="groupInfos"></param>
        private static void CreateGroups(List<GroupInfo> groupInfos)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            foreach (var info in groupInfos)
            {
                info.Group = settings.FindGroup(info.GroupName);
                if (info.Group != null) continue;
                info.Group = settings.CreateGroup(info.GroupName, false, false, false, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
                bool separately = ConfigHmAddressables.SeparatelyPackAssetsPaths.Any(path =>
                    string.Equals(HMAddressablesConfig.NormalizeAssetFolder(path), info.Path, StringComparison.OrdinalIgnoreCase));
                var mode = separately ? BundledAssetGroupSchema.BundlePackingMode.PackSeparately :
                    BundledAssetGroupSchema.BundlePackingMode.PackTogether;
                SetGroupSchema(info.Group, ConfigHmAddressables.ForceRemoteAssetsToLocal || info.BeLocalGroup, true, mode);
                EditorUtility.SetDirty(info.Group);
                Debug.Log("创建" + info.GroupName);
            }
            EditorUtility.SetDirty(settings);
        }

        /// <summary>
        /// 设置配置文件
        /// </summary>
        private static void SetProfiles()
        {
            if (AddressableAssetSettingsDefaultObject.Settings == null)
            {
                throw new InvalidOperationException(
                    "Addressables Settings 未初始化，不能设置构建 Profile。");
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
            if (string.IsNullOrEmpty(AddressableAssetSettingsDefaultObject.Settings.RemoteCatalogBuildPath?.Id))
            {
                AddressableAssetSettingsDefaultObject.Settings.RemoteCatalogBuildPath = new ProfileValueReference();
                AddressableAssetSettingsDefaultObject.Settings.RemoteCatalogBuildPath.SetVariableByName(
                    AddressableAssetSettingsDefaultObject.Settings,
                    AddressableAssetSettings.kRemoteBuildPath);
            }

            if (string.IsNullOrEmpty(AddressableAssetSettingsDefaultObject.Settings.RemoteCatalogLoadPath?.Id))
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
            LoadContentStateForUpdate();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            // 原生入口会合并直接变化项和必须随之迁移的依赖项。
            var items = ContentUpdateScript.GatherModifiedEntries(settings, GetContentStatePath());
            if (items == null)
                throw new InvalidDataException("检查资源变更失败：" + GetContentStatePath());
            items = items.GroupBy(entry => entry.guid).Select(group => group.First()).ToList();
            if (items.Count == 0)
            {
                Debug.Log("没有发现需要更新的静态资源，或资源已移入更新组。");
                return;
            }

            var itemsByGroup = items.GroupBy(entry => entry.parentGroup).ToList();
            var groupNames = new Dictionary<AddressableAssetGroup, string>();
            var reservedNames = new HashSet<string>(settings.groups.Where(x => x != null).Select(x => x.Name),
                StringComparer.OrdinalIgnoreCase);
            foreach (var groupItems in itemsByGroup)
            {
                var sourceGroup = groupItems.Key;
                if (sourceGroup == null || sourceGroup.GetSchema<BundledAssetGroupSchema>() == null)
                    throw new InvalidOperationException("更新资源的来源组缺少 BundledAssetGroupSchema。");
                var sourceName = sourceGroup.Name.Length > 48 ? sourceGroup.Name.Substring(0, 48) : sourceGroup.Name;
                groupNames.Add(sourceGroup, FindUniqueGroupName(sourceName + "_Content Update_", reservedNames));
            }

            var createdGroups = new List<AddressableAssetGroup>();
            try
            {
                foreach (var groupItems in itemsByGroup)
                {
                    createdGroups.Add(CreatContentUpdateGroup(settings, groupItems.ToList(), groupItems.Key,
                        groupNames[groupItems.Key]));
                }
            }
            catch
            {
                // 只撤销本次迁移和本次创建的更新组，不记录或恢复 BundleMode。
                foreach (var groupItems in itemsByGroup)
                {
                    foreach (var entry in groupItems)
                    {
                        if (createdGroups.Contains(entry.parentGroup))
                            settings.MoveEntry(entry, groupItems.Key, entry.ReadOnly);
                    }
                }
                foreach (var group in createdGroups) settings.RemoveGroup(group);
                throw;
            }
        }

        private static AddressableAssetGroup CreatContentUpdateGroup(AddressableAssetSettings settings,
            List<AddressableAssetEntry> items, AddressableAssetGroup sourceGroup, string groupName)
        {
            var sourceSchema = sourceGroup.GetSchema<BundledAssetGroupSchema>();
            AddressableAssetGroup contentGroup = null;
            try
            {
                contentGroup = settings.CreateGroup(groupName, false, false, false, null);
                var schema = contentGroup.AddSchema<BundledAssetGroupSchema>();
                var contentUpdateSchema = contentGroup.AddSchema<ContentUpdateGroupSchema>();
                SetGroupSchema(contentGroup, false, false, sourceSchema.BundleMode);
                foreach (var entry in items) settings.MoveEntry(entry, contentGroup, entry.ReadOnly);
                Debug.Assert(schema.BundleMode == sourceSchema.BundleMode &&
                             items.All(entry => entry.parentGroup == contentGroup),
                    $"更新组 {contentGroup.Name} 未正确继承来源组的打包模式或资源。");
                EditorUtility.SetDirty(contentGroup);
                EditorUtility.SetDirty(schema);
                EditorUtility.SetDirty(contentUpdateSchema);
                return contentGroup;
            }
            catch
            {
                if (contentGroup != null)
                {
                    foreach (var entry in items)
                    {
                        if (entry.parentGroup == contentGroup)
                            settings.MoveEntry(entry, sourceGroup, entry.ReadOnly);
                    }
                    settings.RemoveGroup(contentGroup);
                }
                throw;
            }
        }

        private static string FindUniqueGroupName(string potentialName, HashSet<string> reservedNames)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            for (int index = 1; index <= 1000; index++)
            {
                var name = potentialName + index;
                var path = settings.GroupFolder + "/" + name + ".asset";
                if (!reservedNames.Contains(name) && !File.Exists(path) &&
                    string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                {
                    reservedNames.Add(name);
                    return name;
                }
            }
            throw new InvalidOperationException("无法为更新组生成唯一名称：" + potentialName);
        }



        private static void SetActiveProfiles(bool beTest = false)
        {
            if (AddressableAssetSettingsDefaultObject.Settings == null)
            {
                throw new InvalidOperationException(
                    "AddressableAssetSettingsDefaultObject.Settings 不存在，不能切换构建 Profile。");
            }

            string profileName = beTest ? "TestProfile" : "Default";
            string profileId = AddressableAssetSettingsDefaultObject.Settings.profileSettings
                .GetProfileId(profileName);
            if (string.IsNullOrEmpty(profileId))
            {
                throw new InvalidOperationException(
                    $"Addressables Profile 不存在：{profileName}");
            }

            AddressableAssetSettingsDefaultObject.Settings.activeProfileId = profileId;
        }


        private static void SaveAssetAndRefresh()
        {
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
            UnityEditor.EditorUtility.FocusProjectWindow();
            EditorUtility.RequestScriptReload();
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
