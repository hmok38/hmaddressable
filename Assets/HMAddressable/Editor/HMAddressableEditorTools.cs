using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;


namespace HM.Editor
{
    public class HMAddressableEditorTools
    {
        [DidReloadScripts]
        static void DidReloadScripts()
        {
            var oldPath = "Assets/HMAddressables/ConfigHMAddressables.asset";
            var newPath = "Assets/HMAddressables/Resources/ConfigHMAddressables.asset";
            var config = AssetDatabase.LoadAssetAtPath<HMAddressablesConfig>(oldPath);
            if (config != null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/HMAddressables/Resources"))
                {
                    AssetDatabase.CreateFolder("Assets/HMAddressables", "Resources");
                }

                var erro = AssetDatabase.MoveAsset(oldPath, newPath);
                Debug.Log($"V4.0.0版本移动配置表文件到原目录下Resources目录下{erro}");
                AssetDatabase.SaveAssets();
            }
            else
            {
                config = AssetDatabase.LoadAssetAtPath<HMAddressablesConfig>(newPath);
                if (config == null)
                {
                    Debug.Log("HMAddressables/HMAddressablesConfig文件不存在,创建完毕");
                    if (!AssetDatabase.IsValidFolder("Assets/HMAddressables"))
                    {
                        AssetDatabase.CreateFolder("Assets", "HMAddressables");
                    }

                    if (!AssetDatabase.IsValidFolder("Assets/HMAddressables/Resources"))
                    {
                        AssetDatabase.CreateFolder("Assets/HMAddressables", "Resources");
                    }


                    config = ScriptableObject.CreateInstance<HMAddressablesConfig>();
                    AssetDatabase.CreateAsset(config, newPath);
                }
            }

        }
    }
}