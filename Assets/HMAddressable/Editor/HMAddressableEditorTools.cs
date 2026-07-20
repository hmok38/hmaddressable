using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;


namespace HM.Editor
{
    public class HMAddressableEditorTools
    {
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