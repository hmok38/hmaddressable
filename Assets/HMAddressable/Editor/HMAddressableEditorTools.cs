using System;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Callbacks;
using UnityEngine;


namespace HM.Editor
{
    public class HMAddressableEditorTools : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (movedAssets.Length == 0 || !AddressableAssetSettingsDefaultObject.SettingsExists) return;
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return;

            // shortcut: 只同步 Unity 报告的移动；迁移历史地址前必须取得可核实的旧路径记录。
            foreach (var group in settings.groups)
            {
                if (group == null) continue;
                foreach (var entry in group.entries)
                {
                    // 直接按 GUID 取路径，避免依赖其他导入回调更新路径缓存的顺序。
                    var assetPath = AssetDatabase.GUIDToAssetPath(entry.guid);
                    for (int i = 0; i < movedAssets.Length; i++)
                    {
                        var movedPath = movedAssets[i];
                        if (assetPath != movedPath && !assetPath.StartsWith(movedPath + "/", StringComparison.Ordinal)) continue;
                        var previousPath = movedFromAssetPaths[i] + assetPath.Substring(movedPath.Length);
                        if (entry.address != previousPath) continue;
                        entry.SetAddress(assetPath);
                        Debug.Assert(entry.address == assetPath, "资源移动后默认地址未同步。");
                        break;
                    }
                }
            }
        }

        [DidReloadScripts(10)]
        static void DidReloadScripts()
        {
            var platformConfigPath = "Assets/HMAddressables/Resources/HMAddressablePlatformConfig.asset";

            var platformConfig = AssetDatabase.LoadAssetAtPath<HMAddressablePlatformConfig>(platformConfigPath);
            if (platformConfig == null)
            {
                Debug.Log($"{platformConfigPath}文件不存在,创建完毕");
                if (!AssetDatabase.IsValidFolder("Assets/HMAddressables"))
                {
                    AssetDatabase.CreateFolder("Assets", "HMAddressables");
                }

                if (!AssetDatabase.IsValidFolder("Assets/HMAddressables/Resources"))
                {
                    AssetDatabase.CreateFolder("Assets/HMAddressables", "Resources");
                }


                platformConfig = ScriptableObject.CreateInstance<HMAddressablePlatformConfig>();
                AssetDatabase.CreateAsset(platformConfig, platformConfigPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }
    }
}
