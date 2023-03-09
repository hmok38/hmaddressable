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
using Object = UnityEngine.Object;

/// <summary>
/// 如果有报错,则请保证Addressables的版本号在1.19.17以上
/// </summary>
public class HMAddressablesEditor : MonoBehaviour
{
    private const string ConfigPath = "Assets/HMAddressables/ConfigHMAddressables.asset";

    private static HMAddressablesConfig configHMAddressables =>
        AssetDatabase.LoadAssetAtPath<HMAddressablesConfig>(ConfigPath);


    //=============================public=============================================
    [UnityEditor.MenuItem("Tools/HMAddressablesAssets/***选择并显示配置表***")]
    public static void ShowAndSelectConfigMenuItem()
    {
        Selection.activeObject = AssetDatabase.LoadAssetAtPath(ConfigPath, typeof(HMAddressablesConfig));
        EditorGUIUtility.PingObject(Selection.activeObject);
        EditorUtility.FocusProjectWindow();
        Debug.Log("已经选择并显示配置表");
    }

    [UnityEditor.MenuItem("Tools/HMAddressablesAssets/更新 资源分组(没有就创建)")]
    public static void BuildAddressablesSettingsMenuItem()
    {
        if (AddressableAssetSettingsDefaultObject.Settings == null)
        {
            AddressableAssetSettingsDefaultObject.Settings = AddressableAssetSettings.Create(
                AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
                AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName, true, true);

        }

        SetToSetting(configHMAddressables);
        Debug.Log("更新 资源分组(没有就创建) 完毕,请根据需要 去处理重复依赖");
    }

    [UnityEditor.MenuItem("Tools/HMAddressablesAssets/处理重复依赖将其独立出来-不清理之前的重复依赖组")]
    public static void CheckAndFixBundleDupeDependenciesMenuItem()
    {
        Debug.Log("处理重复依赖将其独立出来-不清理之前的重复依赖组:  开始");
        CheckAndFixDupDependencies();
        Debug.Log("处理重复依赖将其独立出来-不清理之前的重复依赖组:  完成");
    }

    [UnityEditor.MenuItem("Tools/HMAddressablesAssets/处理重复依赖将其独立出来-清理之前的重复依赖组")]
    public static void CheckAndFixBundleDupeDependenciesClearMenuItem()
    {
        Debug.Log("处理重复依赖将其独立出来-清理之前的重复依赖组:  开始");
        //删除重复依赖组,并重新分析自定义组的关系;
        DeleteDupDependenciesGroup();
        CheckAndFixDupDependencies();
        Debug.Log("处理重复依赖将其独立出来-清理之前的重复依赖组:  完成");
    }

    [UnityEditor.MenuItem("Tools/HMAddressablesAssets/****打包资源****")]
    public static void BuildAddressablesAssetsMenuItem()
    {
        Debug.Log(configHMAddressables.TestValues);

        BuildAsset();
    }

    [UnityEditor.MenuItem("Tools/HMAddressablesAssets/========================以下为谨慎选项==============================")]
    public static void Readme()
    {
    }

    [UnityEditor.MenuItem("Tools/HMAddressablesAssets/清理资源组设置(谨慎):打包时会全部资源重新命名,之前发布的包体会更新不到资源")]
    public static void CleanAddressGroupsMenuItem()
    {
        var groupPath = Path.Combine(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder, "AssetGroups");
        if (AssetDatabase.IsValidFolder(groupPath))
        {
            DeleteAllSubAssetsByFolderPath(groupPath,false,x=>x.IndexOf("Built In Data")<0);
            var schemasPath = Path.Combine(groupPath, "Schemas");
            if (AssetDatabase.IsValidFolder(schemasPath))
            {
                DeleteAllSubAssetsByFolderPath(schemasPath,false,x=>x.IndexOf("Built In Data")<0);
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
        Debug.Log("清理所有设置 完毕!已经删除Assets-AddressableAssetsData文件夹");
    }

    [UnityEditor.MenuItem("Tools/HMAddressablesAssets/测试")]
    public static void Test()
    {
        DeleteDupDependenciesGroup();
    }

    //-------------------------private------------------------------------------------

    private static void BuildAsset()
    {
        if (AddressableAssetSettingsDefaultObject.Settings == null)
        {
            BuildAddressablesSettingsMenuItem();
        }

        AddressableAssetSettings settings
            = AddressableAssetSettingsDefaultObject.Settings;

        settings.activeProfileId
            = settings.profileSettings.GetProfileId("Default");

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

    private static void SetToSetting(HMAddressablesConfig config)
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

    private static void SetAssetsToGroup(List<GroupInfo> groupInfos)
    {
        foreach (var groupInfo in groupInfos)
        {
            if (!groupInfo.Group.name.Equals("Built In Data") &&groupInfo.Group.entries.Count > 0)
            {
                //先移除
                var old = groupInfo.Group.entries.ToArray();
                for (int i = 0; i < old.Length; i++)
                {
                    groupInfo.Group.RemoveAssetEntry(old[i]);
                }
            }
        }

        //重新添加
        foreach (var groupInfo in groupInfos)
        {
            if(groupInfo.Group.name.Equals("Built In Data"))continue;
            var strs = AssetDatabase.FindAssets("", new[] {groupInfo.path});
            foreach (var assetGuid in strs)
            {
                if (!AssetDatabase.IsValidFolder(AssetDatabase.GUIDToAssetPath(assetGuid)))
                {
                    //不是文件夹才添加
                    AddressableAssetSettingsDefaultObject.Settings.CreateOrMoveEntry(assetGuid, groupInfo.Group);
                }
            }
        }
    }

    private static void SetGroupSchema(AddressableAssetGroup group)
    {
        var updateGroupSchema = group.GetSchema<ContentUpdateGroupSchema>();
        updateGroupSchema.StaticContent = false;

        var bundledAssetGroupSchema = group.GetSchema<BundledAssetGroupSchema>();
        bundledAssetGroupSchema.BuildPath.SetVariableByName(group.Settings,
            AddressableAssetSettings.kRemoteBuildPath);
        bundledAssetGroupSchema.LoadPath.SetVariableByName(group.Settings,
            AddressableAssetSettings.kRemoteLoadPath);
        bundledAssetGroupSchema.Timeout = 2000;
        bundledAssetGroupSchema.RetryCount = 5;
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
                SetGroupSchema(group);
            }
        }
    }

    private static void DeleteEmptyGroup()
    {
        List<AddressableAssetGroup> needDeleteGroups = new List<AddressableAssetGroup>();
        for (int i = 0; i < AddressableAssetSettingsDefaultObject.Settings.groups.Count; i++)
        {
            if (!AddressableAssetSettingsDefaultObject.Settings.groups[i].name.Equals("Built In Data") 
                &&AddressableAssetSettingsDefaultObject.Settings.groups[i].entries.Count <= 0)
            {
                needDeleteGroups.Add(AddressableAssetSettingsDefaultObject.Settings.groups[i]);
            }
        }

        for (int i = 0; i < needDeleteGroups.Count; i++)
        {
            AddressableAssetSettingsDefaultObject.Settings.RemoveGroup(needDeleteGroups[i]);
        }
    }

    private static void CreatBuiltInData()
    {
        
       var builtInDataGroup= AssetDatabase.LoadAssetAtPath<AddressableAssetGroup>(
            "Assets/AddressableAssetsData/AssetGroups/Built In Data.asset");
       if (builtInDataGroup != null)
       {
           Debug.Log("builtInDataGroup 存在");
           if (builtInDataGroup.Settings != AddressableAssetSettingsDefaultObject.Settings)
           {
               AddressableAssetSettingsDefaultObject.Settings = builtInDataGroup.Settings;
           }
       }
       else
       { 
           Debug.Log("builtInDataGroup 不存在");
           builtInDataGroup = AddressableAssetSettingsDefaultObject.Settings.CreateGroup("Built In Data", false, true, true,
               null, typeof(PlayerDataGroupSchema));
           var schema = builtInDataGroup.GetSchema<PlayerDataGroupSchema>();
           schema.IncludeResourcesFolders = true;
           schema.IncludeBuildSettingsScenes = true;
       }
    }

    private static void CreatAndClearGroupBySelectFolder( List<GroupInfo> groupInfos)
    {
        //创建组
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

                SetGroupSchema(groupInfo.Group );
            }
        }
        //清理除 builtInGroup 组以外的不包含在groupInfos里面的组
        List<AddressableAssetGroup> needDeleteGroups = new List<AddressableAssetGroup>();
        for (int i = 0; i < AddressableAssetSettingsDefaultObject.Settings.groups.Count; i++)
        {
            var group=AddressableAssetSettingsDefaultObject.Settings.groups[i];
            if (group != null && !group.name.Equals("Built In Data") && !groupInfos.Exists(x => x.Group == group))
            {
                needDeleteGroups.Add(group);
            }
        }

        for (int i = 0; i < needDeleteGroups.Count; i++)
        {
            AddressableAssetSettingsDefaultObject.Settings.RemoveGroup(needDeleteGroups[i]);
        }
    }
    
    class GroupInfo
    {
        public string path;
        public string groupName;
        public AddressableAssetGroup Group;
    }
}