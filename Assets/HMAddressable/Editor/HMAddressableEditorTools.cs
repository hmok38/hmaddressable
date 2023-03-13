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
            var path = "Assets/HMAddressables/ConfigHMAddressables.asset";
            var config = AssetDatabase.LoadAssetAtPath<HMAddressablesConfig>(path);
            if (config == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/HMAddressables"))
                {
                    AssetDatabase.CreateFolder("Assets", "HMAddressables");
                }
                

                config = ScriptableObject.CreateInstance<HMAddressablesConfig>();
                AssetDatabase.CreateAsset(config, path);
            }
        }
    }
}