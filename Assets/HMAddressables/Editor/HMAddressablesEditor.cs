using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

/// <summary>
/// 如果有报错,则请保证Addressables的版本号在1.19.17以上 V1.0 20230310 by HM
/// 依赖 newtonsoft.Json包(Unity2021后内置,2021前版本请在PackageManage的UnityRegistry中搜索)
/// 依赖 UniTask异步插件 请在PackageManage中点+号,选择git url
/// 输入: https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask 有时候会输入失败,多试几次
/// 实在不行就去 https://github.com/Cysharp/UniTask.git 下载unityPackage包
/// </summary>
public class HMAddressablesEditor : MonoBehaviour
{
    private const string ConfigPath = "Assets/HMAddressables/ConfigHMAddressables.asset";

    private static HMAddressablesConfig configHMAddressables =>
        AssetDatabase.LoadAssetAtPath<HMAddressablesConfig>(ConfigPath);


    //=============================public=============================================
    
    [UnityEditor.MenuItem(@"Tools/HMAddressablesAssets/*************************************HMAddresables资源管理插件<点我读说明>************************")]
    public static void Readme0()
    {
        Debug.Log(@"HMAddresables资源管理插件,它是基于UnityAddressablesAssets系统做得自动化打包管理工具,
资源分组和打包基于文件夹目录进行分组,并在发布游戏包体时一次性打包进入APK包,后续热更时采用增量更新的方式进行热更新,
它具有高度自动化和热更新体量小的特点,使用它完全不用关心太多资源包知识和原理,只要管理好资源目录即可");
    }
    
    [UnityEditor.MenuItem(@"Tools/HMAddressablesAssets/========================打包选项<点我读说明>==============================")]
    public static void Readme2()
    {
        Debug.Log(@"使用方法:
1:发布新游戏版本时使用 <一键打出包资源(正式包)> 选项
2:发布热更包时使用 <一键打更新资源包(正式包)> 选项
");
    }
    
    [UnityEditor.MenuItem("Tools/HMAddressablesAssets/***选择并显示配置表***")]
    public static void ShowAndSelectConfigMenuItem()
    {
        Selection.activeObject = AssetDatabase.LoadAssetAtPath(ConfigPath, typeof(HMAddressablesConfig));
        EditorGUIUtility.PingObject(Selection.activeObject);
        EditorUtility.FocusProjectWindow();
        Debug.Log("已经选择并显示配置表");
    }
    
    [UnityEditor.MenuItem("Tools/HMAddressablesAssets/****一键打出包资源(正式包)****")]
    public static void BuildAddressablesAssetsMenuItem()
    {
        //设置配置表选项
        SetProfiles();
        SetActiveProfiles(false);
        BuildAsset();
    }

    [UnityEditor.MenuItem("Tools/HMAddressablesAssets/****一键打更新资源包(正式包)****")]
    public static void BuildUpdateMenuItem()
    {
        //设置配置表选项
        SetProfiles();
        SetActiveProfiles(false);
        BuildUpdateAsset();
    }
    [UnityEditor.MenuItem(@"Tools/HMAddressablesAssets/========================不影响线上的热更测试<点我读说明>============================")]
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
    [UnityEditor.MenuItem("Tools/HMAddressablesAssets/********一键打出包资源(测试包)********")]
    public static void BuildAddressablesTestAssetsMenuItem()
    {
        //设置配置表选项
        SetProfiles();
        SetActiveProfiles(true);

        BuildAsset();
        
    }

    [UnityEditor.MenuItem("Tools/HMAddressablesAssets/********一键打更新资源包(测试包)********")]
    public static void BuildUpdateTestMenuItem()
    {
        //设置配置表选项
        SetProfiles();
        SetActiveProfiles(true);

        BuildUpdateAsset();
       
    }
    
    [UnityEditor.MenuItem(@"Tools/HMAddressablesAssets/====================独立配置<不需要可以无视>==========================")]
    public static void Readme3()
    {
    }
    
    [UnityEditor.MenuItem("Tools/HMAddressablesAssets/更新(创建)资源分组 <更新包阶段禁止使用>")]
    public static void BuildAddressablesSettingsMenuItem()
    {
        if (AddressableAssetSettingsDefaultObject.Settings == null)
        {
            AddressableAssetSettingsDefaultObject.Settings = AddressableAssetSettings.Create(
                AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
                AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName, true, true);
            AddressableAssetSettingsDefaultObject.Settings.BuildRemoteCatalog = true;
        }

        SetStaticGroupSetting(configHMAddressables);
        Debug.Log("更新 资源分组(没有就创建) 完毕,请根据需要 去处理重复依赖");
    }

    [UnityEditor.MenuItem("Tools/HMAddressablesAssets/处理重复依赖,不清理之前的重复依赖组 <更新包阶段禁止使用>")]
    public static void CheckAndFixBundleDupeDependenciesMenuItem()
    {
        Debug.Log("处理重复依赖将其独立出来-不清理之前的重复依赖组:  开始");
        CheckAndFixDupDependencies();
        Debug.Log("处理重复依赖将其独立出来-不清理之前的重复依赖组:  完成");
    }

    [UnityEditor.MenuItem("Tools/HMAddressablesAssets/处理重复依赖-清理之前的重复依赖组 <更新包阶段禁止使用>")]
    public static void CheckAndFixBundleDupeDependenciesClearMenuItem()
    {
        Debug.Log("处理重复依赖将其独立出来-清理之前的重复依赖组:  开始");
        //删除重复依赖组,并重新分析自定义组的关系;
        DeleteDupDependenciesGroup();
        CheckAndFixDupDependencies();
        Debug.Log("处理重复依赖将其独立出来-清理之前的重复依赖组:  完成");
    }
    
    [UnityEditor.MenuItem("Tools/HMAddressablesAssets/========================以下为谨慎选项<除非发包,否则禁止使用>==============================")]
    public static void Readme()
    {
    }

    [UnityEditor.MenuItem("Tools/HMAddressablesAssets/清理资源组设置(谨慎):打包时会全部资源重新命名,之前发布的包体会更新不到资源")]
    public static void CleanAddressGroupsMenuItem()
    {
        var groupPath = Path.Combine(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder, "AssetGroups");
        if (AssetDatabase.IsValidFolder(groupPath))
        {
            DeleteAllSubAssetsByFolderPath(groupPath, false, x => x.IndexOf("Built In Data") < 0);
            var schemasPath = Path.Combine(groupPath, "Schemas");
            if (AssetDatabase.IsValidFolder(schemasPath))
            {
                DeleteAllSubAssetsByFolderPath(schemasPath, false, x => x.IndexOf("Built In Data") < 0);
            }
        }

        ClearGroupMissingReferences();
        AssetDatabase.SaveAssets();
        Debug.Log("清理自定义组设置 完毕!");
    }

    [UnityEditor.MenuItem("Tools/HMAddressablesAssets/清理所有设置(谨慎):打包时会全部资源重新命名,之前发布的包体会更新不到资源")]
    public static void CleanAddressablesSettingsMenuItem()
    {
        AssetDatabase.DeleteAsset(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder);
        if (Directory.Exists("ServerData"))
        {
            Directory.Delete("ServerData",true);
        }
        Debug.Log("清理所有设置 完毕!已经删除Assets-AddressableAssetsData文件夹");
    }

    [UnityEditor.MenuItem("Tools/HMAddressablesAssets/测试")]
    public static void Test()
    {
        
        var has = Directory.Exists("ServerData");
    Debug.Log(has);
    }

    //-------------------------private------------------------------------------------

    private static void BuildAsset()
    {
        //没有就创建
        if (AddressableAssetSettingsDefaultObject.Settings == null)
        {
            BuildAddressablesSettingsMenuItem();
        }
        else
        {
            //更新组设置
            SetStaticGroupSetting(configHMAddressables);
        }
        
        
        AddressableAssetSettings settings
            = AddressableAssetSettingsDefaultObject.Settings;
        
        
        //检查依赖关系
        CheckAndFixBundleDupeDependenciesClearMenuItem();
        
        //打包
        IDataBuilder builder
            = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/AddressableAssetsData/DataBuilders/BuildScriptPackedMode.asset") as
                IDataBuilder;

        settings.ActivePlayerDataBuilderIndex
            = settings.DataBuilders.IndexOf((ScriptableObject) builder);

        AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);

        if (!string.IsNullOrEmpty(result.Error))
            Debug.LogError("打包错误:" + result.Error);

        else
        {
            Debug.Log("打包完成");
        }
    }

    private static void BuildUpdateAsset()
    {
        
        //没有就创建
        if (AddressableAssetSettingsDefaultObject.Settings == null)
        {
            BuildAddressablesSettingsMenuItem();
        }
        else
        {
            //更新组设置-采用升级资源组配置
           
            SetUpdateGroupSetting(configHMAddressables);
        }
        
        
        //检查依赖关系-升级包不能检查依赖关系,因为新的依赖关系组会发布成本地包
       // CheckAndFixDupDependencies();
        
        string assetPath = Path.Combine(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
            PlatformMappingService.GetPlatformPathSubFolder());
        var path = Path.Combine(assetPath, "addressables_content_state.bin");

        var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
        if (obj == null)
        {
            Debug.Log("还没打第一次的资源包:" + path);
            return;
        }
        
        // //检查静态组升级设置,设立升级组
        CheckForContentUpdateRestructions();
        
        
        
        //打资源包
        
        ContentUpdateScript.BuildContentUpdate(AddressableAssetSettingsDefaultObject.Settings,
            Path.Combine(assetPath, "addressables_content_state.bin"));
        Debug.Log("更新资源包 打包完成");
    }
    
    private static void SetStaticGroupSetting(HMAddressablesConfig config)
    {
        if (!AssetDatabase.IsValidFolder(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder))
        {
            BuildAddressablesSettingsMenuItem();
            return;
        }


        if (config.AseetsPaths == null || config.AseetsPaths.Length <= 0)
        {
            Debug.LogError($"未设置需要打包的资源路径,请检查{ConfigPath}的设置");
            ShowAndSelectConfigMenuItem();
            return;
        }

        var groupInfos = new List<GroupInfo>();

        foreach (var assetsPath in config.AseetsPaths)
        {
            GetAllSubFolderAndCreatGroupInfo(assetsPath, ref groupInfos);
        }

        if (groupInfos.Count <= 0)
        {
            Debug.LogError($"未设置需要打包的资源路径,请检查{ConfigPath}的设置");
            ShowAndSelectConfigMenuItem();
            return;
        }

        CreatAndClearGroupBySelectFolder(groupInfos);
        SetAssetsToGroup(groupInfos);
         DeleteEmptyGroup();
         ClearGroupMissingReferences();
    }

    private static void SetAssetsToGroup(List<GroupInfo> groupInfos,bool beUpdateAssets=false)
    {
        foreach (var groupInfo in groupInfos)
        {
            if (!groupInfo.Group.name.Equals("Built In Data") && !groupInfo.Group.name.Contains("Content Update") &&
                groupInfo.Group.entries.Count > 0)
            {
                //如果是打更新包,重复依赖组的不删除,因为打更新资源包的时候不再处理重复依赖关系
                if (beUpdateAssets&&groupInfo.Group.name.Contains("Duplicate Asset Isolation"))
                {
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

        //重新添加到 非特殊组
        foreach (var groupInfo in groupInfos)
        {
            //不对特殊组处理
            if (groupInfo.Group.name.Equals("Built In Data") ||
                groupInfo.Group.name.Contains("Content Update")) continue;
            
            var strs = AssetDatabase.FindAssets("", new[] {groupInfo.path});
            //Debug.Log($"{groupInfo.groupName}的要添加的资源为:{strs.Length}");
            
            DirectoryInfo folderInfo = new DirectoryInfo(groupInfo.path);
            
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
                AddressableAssetSettingsDefaultObject.Settings.CreateOrMoveEntry(assetGuid, groupInfo.Group);
            }
        }
    }

    private static void SetStaticAndLocalGroupSchema(AddressableAssetGroup group)
    {
        var updateGroupSchema = group.GetSchema<ContentUpdateGroupSchema>();
        updateGroupSchema.StaticContent = true;

        var bundledAssetGroupSchema = group.GetSchema<BundledAssetGroupSchema>();
        bundledAssetGroupSchema.BuildPath.SetVariableByName(group.Settings,
            AddressableAssetSettings.kLocalBuildPath);
        bundledAssetGroupSchema.LoadPath.SetVariableByName(group.Settings,
            AddressableAssetSettings.kLocalLoadPath);
    }
    
    private static void SetCanChangerAndRemoteGroupSchema(AddressableAssetGroup group)
    {
        var updateGroupSchema = group.GetSchema<ContentUpdateGroupSchema>();
        updateGroupSchema.StaticContent = false;

        var bundledAssetGroupSchema = group.GetSchema<BundledAssetGroupSchema>();
        bundledAssetGroupSchema.BuildPath.SetVariableByName(group.Settings,
            AddressableAssetSettings.kRemoteBuildPath);
        bundledAssetGroupSchema.LoadPath.SetVariableByName(group.Settings,
            AddressableAssetSettings.kRemoteLoadPath);
    }

    private static void GetAllSubFolderAndCreatGroupInfo(string folder, ref List<GroupInfo> groupInfos)
    {
        if (!AssetDatabase.IsValidFolder(folder)) return;
        var baseInfo = CreatGroupInfo(folder);
        if (baseInfo != null)
        {
            groupInfos.Add(baseInfo);
        }

        var subFolders = AssetDatabase.GetSubFolders(folder);
        if (subFolders == null || subFolders.Length <= 0) return;
        foreach (var subFolder in subFolders)
        {
            GetAllSubFolderAndCreatGroupInfo(subFolder, ref groupInfos);
        }
    }

    private static GroupInfo CreatGroupInfo(string groupPath)
    {
        var groupName = GroupNameByPath(groupPath);
        if (string.IsNullOrEmpty(groupName))
        {
            return null;
        }


        var info = new GroupInfo() {groupName = groupName, path = groupPath};

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

    private static void DeleteAllSubAssetsByFolderPath(string folderPath, bool beDeleteSubFolder = false,
        System.Predicate<string> predicate = null)
    {
        var strs = AssetDatabase.FindAssets("", new[] {folderPath});
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

    private static void DeleteDupDependenciesGroup()
    {
        DeleteAllSubAssetsByFolderPath(AddressableAssetSettingsDefaultObject.Settings.GroupFolder, false,
            x => x.Contains("Duplicate Asset Isolation"));
        ClearGroupMissingReferences();
    }

    private static void CheckAndFixDupDependencies()
    {
        UnityEditor.AddressableAssets.Build.AnalyzeRules.CheckBundleDupeDependencies a =
            new UnityEditor.AddressableAssets.Build.AnalyzeRules.CheckBundleDupeDependencies();
        a.FixIssues(AddressableAssetSettingsDefaultObject.Settings);
        SetToDupDependenciesGroup();
    }

    private static void SetToDupDependenciesGroup()
    {
        var groups = AddressableAssetSettingsDefaultObject.Settings.groups;
        for (int i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            if (group.Name.Contains("Duplicate Asset Isolation"))
            {
                SetStaticAndLocalGroupSchema(group);
            }
        }
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
    }
    
    private static void CreatAndClearGroupBySelectFolder(List<GroupInfo> groupInfos,bool beUpdateAssetGroup=false)
    {
        //创建组(已经存在了就不用了)
        foreach (var groupInfo in groupInfos)
        {
            var groupAssetPath = Path.Combine(AddressableAssetSettingsDefaultObject.Settings.GroupFolder,
                groupInfo.groupName + ".asset");
            groupInfo.Group = AssetDatabase.LoadAssetAtPath<AddressableAssetGroup>(groupAssetPath);

            if (groupInfo.Group == null)
            {
                //没有就创建
                groupInfo.Group = AddressableAssetSettingsDefaultObject.Settings.CreateGroup(groupInfo.groupName, false,
                    false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
                if (beUpdateAssetGroup)
                {
                    //是升级包中出现的新组,那么就设置为远程包且为可变组
                    SetCanChangerAndRemoteGroupSchema(groupInfo.Group);
                }
                else
                {
                    //不是升级包,那么就设置为本地的,
                    SetStaticAndLocalGroupSchema(groupInfo.Group);
                }
              
                Debug.Log("创建" + groupInfo.groupName);
            }
        }

        //清理除 builtInGroup 组以外的不包含在groupInfos里面的组
        List<AddressableAssetGroup> needDeleteGroups = new List<AddressableAssetGroup>();
        for (int i = 0; i < AddressableAssetSettingsDefaultObject.Settings.groups.Count; i++)
        {
            var group = AddressableAssetSettingsDefaultObject.Settings.groups[i];
            if (group != null && !group.name.Equals("Built In Data") && !group.name.Contains("Content Update") &&
                !groupInfos.Exists(x => x.Group == group))
            {
                if (beUpdateAssetGroup)
                {
                    //升级包不能删除之前的重复引用组
                    if (!group.name.Contains("Duplicate Asset Isolation"))
                    {
                        needDeleteGroups.Add(group);
                    }
                    
                }
                else
                {
                    needDeleteGroups.Add(group);
                }
               
            }
        }

        for (int i = 0; i < needDeleteGroups.Count; i++)
        {
            AddressableAssetSettingsDefaultObject.Settings.RemoveGroup(needDeleteGroups[i]);
        }
    }

    private static void SetProfiles()
    {
        if (AddressableAssetSettingsDefaultObject.Settings==null)
        {
            Debug.LogErrorFormat("未初始化系统,请先运行 更新资源分组(没有就创建)");
            return;
        }
        
        //设置Default设置
        var defaultId=  AddressableAssetSettingsDefaultObject.Settings.profileSettings.GetProfileId("Default");
        AddressableAssetSettingsDefaultObject.Settings.profileSettings.SetValue(defaultId,
            AddressableAssetSettings.kRemoteLoadPath, configHMAddressables.RemoteLoadPath);
        //创建和设置TestProfile设置
        var profileId= AddressableAssetSettingsDefaultObject.Settings.profileSettings.GetProfileId("TestProfile");
        if (string.IsNullOrEmpty(profileId))
        {
          
            profileId= AddressableAssetSettingsDefaultObject.Settings.profileSettings.AddProfile("TestProfile", defaultId);
        }
        AddressableAssetSettingsDefaultObject.Settings.profileSettings.SetValue(profileId,
            AddressableAssetSettings.kRemoteLoadPath, configHMAddressables.TestRemoteLoadPath);
       
        
    }
   
    private static void CheckForContentUpdateRestructions()
    {
        string assetPath = Path.Combine(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
            PlatformMappingService.GetPlatformPathSubFolder());
        var path = Path.Combine(assetPath, "addressables_content_state.bin");
        
        var modifiedEntries = ContentUpdateScript.GatherModifiedEntriesWithDependencies(AddressableAssetSettingsDefaultObject.Settings, path);
        List<AddressableAssetEntry> items = new List<AddressableAssetEntry>();
        foreach (var entry in modifiedEntries)
        {
            items.Add(entry.Key);
            Debug.Log(entry.Key.AssetPath);
        }

        if (items.Count > 0)
        {
            ContentUpdateScript.CreateContentUpdateGroup(AddressableAssetSettingsDefaultObject.Settings, items, "Content Update");
            
        }
        else
        {
            Debug.Log("没有发现需要更新的静态资源包");
        }
       
        
    }
    
    private static void SetUpdateGroupSetting(HMAddressablesConfig config)
    {
        if (!AssetDatabase.IsValidFolder(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder))
        {
            Debug.LogError($"没找到数据文件,不能设置升级包,请恢复代码");
            return;
        }


       

        var groupInfos = new List<GroupInfo>();

        foreach (var assetsPath in config.AseetsPaths)
        {
            GetAllSubFolderAndCreatGroupInfo(assetsPath, ref groupInfos);
        }

        if (groupInfos.Count <= 0)
        {
            Debug.LogError($"未设置需要打包的资源路径,请检查{ConfigPath}的设置");
            ShowAndSelectConfigMenuItem();
            return;
        }
        
        
        
        CreatAndClearGroupBySelectFolder(groupInfos,true);
        SetAssetsToGroup(groupInfos,true);
        DeleteEmptyGroup();
        ClearGroupMissingReferences();
    }

    private static void SetActiveProfiles(bool beTest=false)
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
    
    class GroupInfo
    {
        public string path;
        public string groupName;
        public AddressableAssetGroup Group;
    }
}