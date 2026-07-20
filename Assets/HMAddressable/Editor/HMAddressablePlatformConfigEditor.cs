using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HM
{
    /// <summary>
    /// Editor启动时注入BuildTarget→RuntimePlatform映射,使CurrentConfig在Editor下根据BuildTarget返回对应配置
    /// </summary>
    [CustomEditor(typeof(HMAddressablePlatformConfig))]
    public class HMAddressablePlatformConfigEditor : UnityEditor.Editor
    {
        private static readonly Dictionary<RuntimePlatform, string> PlatformDisplayNames =
            new Dictionary<RuntimePlatform, string>
            {
                { RuntimePlatform.WindowsPlayer, "Windows" },
                { RuntimePlatform.OSXPlayer, "MacOS" },
                { RuntimePlatform.LinuxPlayer, "Linux" },
                { RuntimePlatform.Android, "Android" },
                { RuntimePlatform.IPhonePlayer, "iOS" },
                { RuntimePlatform.WebGLPlayer, "WebGL" },
            };

        private const string ConfigAssetDir = "Assets/HMAddressables/Resources";


        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var currentPlatform = GetCurrentBuildTargetRuntimePlatform();
            var existingConfig = FindConfigForPlatform(currentPlatform);

            if (existingConfig == null)
            {
                EditorGUILayout.HelpBox(
                    $"当前构建目标平台 {currentPlatform} 尚未配置 HMAddressablesConfig",
                    MessageType.Warning);

                if (GUILayout.Button($"创建并关联 {currentPlatform} 平台的配置表"))
                {
                    CreateConfigForPlatform(currentPlatform);
                }

                EditorGUILayout.Space(10);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"当前构建目标平台 {currentPlatform} 已配置",
                    MessageType.Info);
            }

            DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();
        }

        private HMAddressablesConfig FindConfigForPlatform(RuntimePlatform platform)
        {
            HMAddressablePlatformConfig targetClass = this.target as HMAddressablePlatformConfig;
            return targetClass?.PlatformConfigEntries.FirstOrDefault(x => x.Platform == platform)?.Config;
        }

        private static RuntimePlatform GetCurrentBuildTargetRuntimePlatform()
        {
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            switch (buildTarget)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return RuntimePlatform.WindowsPlayer;
                case BuildTarget.StandaloneOSX:
                    return RuntimePlatform.OSXPlayer;
                case BuildTarget.StandaloneLinux64:
                    return RuntimePlatform.LinuxPlayer;
                case BuildTarget.Android:
                    return RuntimePlatform.Android;
                case BuildTarget.iOS:
                    return RuntimePlatform.IPhonePlayer;
                case BuildTarget.WebGL:
                    return RuntimePlatform.WebGLPlayer;
                default:
                    return RuntimePlatform.WindowsPlayer;
            }
        }

        private void CreateConfigForPlatform(RuntimePlatform platform)
        {
            if (!AssetDatabase.IsValidFolder(ConfigAssetDir))
            {
                var parent = "Assets/HMAddressables";
                if (!AssetDatabase.IsValidFolder(parent))
                    AssetDatabase.CreateFolder("Assets", "HMAddressables");
                AssetDatabase.CreateFolder(parent, "Resources");
            }

            var platformName = PlatformDisplayNames.TryGetValue(platform, out var name)
                ? name
                : platform.ToString();
            var assetPath = $"{ConfigAssetDir}/ConfigHMAddressablesFor{platformName}.asset";

            var existing = AssetDatabase.LoadAssetAtPath<HMAddressablesConfig>(assetPath);
            if (existing != null)
            {
                AddOrUpdateEntry(platform, existing);
                EditorUtility.SetDirty(target);
                AssetDatabase.SaveAssets();
                EditorGUIUtility.PingObject(existing);
                Debug.Log($"已关联已有的配置表: {assetPath}");
                return;
            }

            var newConfig = CreateInstance<HMAddressablesConfig>();
            AssetDatabase.CreateAsset(newConfig, assetPath);
            AssetDatabase.SaveAssets();

            AddOrUpdateEntry(platform, newConfig);
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();

            EditorGUIUtility.PingObject(newConfig);
            Debug.Log($"已创建并关联配置表: {assetPath}");
        }

        private void AddOrUpdateEntry(RuntimePlatform platform, HMAddressablesConfig asset)
        {
            HMAddressablePlatformConfig targetClass = this.target as HMAddressablePlatformConfig;
            targetClass?.PlatformConfigEntries.RemoveAll(x => x.Platform == platform);

            targetClass?.PlatformConfigEntries.Add(new PlatformConfigEntry()
            {
                Config = asset,
                Platform = platform
            });
            UnityEditor.EditorUtility.SetDirty(targetClass);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}