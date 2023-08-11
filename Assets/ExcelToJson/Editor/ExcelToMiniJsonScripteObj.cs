using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ExcelToMiniJsonScripteObj : ScriptableObject
{
    public static ExcelToMiniJsonScripteObj Instance
    {
        get
        {
            var config =
                AssetDatabase.LoadAssetAtPath<ExcelToMiniJsonScripteObj>(
                    "Assets/ExcelToJson/Editor/ExcelToMiniJsonConfig.asset");
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<ExcelToMiniJsonScripteObj>();
                AssetDatabase.CreateAsset(config, "Assets/ExcelToJson/Editor/ExcelToMiniJsonConfig.asset");
                AssetDatabase.SaveAssets();
            }

            return config;
        }
    }
    [Header("Bat")]
    public string batPath = "../Tools/UnityTools/Excel2Json/GameConfig/excel_to_json/__export_unity_mini.bat";
    public string excelPath="../Excel";
    public string jsonOutPath="Assets/Bundles/ConfigMini";
    public string csCodeOutPath="Assets/Model/Generate/ConfigMini";
}