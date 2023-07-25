using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine.Networking;
using Application = UnityEngine.Application;


namespace HM
{
    public static class AndroidGooglePlayAssetDeliveryHelper
    {
        public static async UniTask<bool> CopyGPADAssets(List<string> assetpackNames)
        {
#if UNITY_EDITOR || !UNITY_ANDROID
            return true;
#endif

            var allAssetPackNames = assetpackNames;
            HMRuntimeDialogHelper.DebugStopWatchInfo($"准备GPAD资源的复制,共:{allAssetPackNames.Count} 组");
            string ebPath = Path.Combine(Application.persistentDataPath, "eb");
            if (!Directory.Exists(ebPath))
            {
                Directory.CreateDirectory(ebPath);
                HMRuntimeDialogHelper.DebugStopWatchInfo($"创建AA资源目录:{ebPath} ");
            }


            for (int i = 0; i < allAssetPackNames.Count; i++)
            {
                var aaName = allAssetPackNames[i];

                var aaPath = Path.Combine(ebPath, aaName);
                if (File.Exists(aaPath))
                {
                    HMRuntimeDialogHelper.DebugStopWatchInfo($"资源已存在,跳过:{aaName} ");
                    continue;
                }


                var assetFileName = "HMAA" + aaName;
                var assetApkName = "split_" + assetFileName;

                //Application.streamingAssetsPath=  jar:file:///data/app/com.hmok.PadWithHMAA-1/base.apk!/assets
                var assetStreamPath = Application.streamingAssetsPath.Replace("base.", assetApkName + ".");
                assetStreamPath = Path.Combine(assetStreamPath, "assetpack");
                assetStreamPath = Path.Combine(assetStreamPath, assetFileName);

                HMRuntimeDialogHelper.DebugStopWatchInfo($"准备获取GPAD包:{assetFileName} 路径 {assetStreamPath} ");
                try
                {
                    using UnityWebRequest webRequest = UnityWebRequest.Get(assetStreamPath);
                    await webRequest.SendWebRequest();

                    if (webRequest.result == UnityWebRequest.Result.ConnectionError ||
                        webRequest.result == UnityWebRequest.Result.ProtocolError ||
                        webRequest.result == UnityWebRequest.Result.DataProcessingError)
                    {
                        HMRuntimeDialogHelper.DebugStopWatchInfo($"获取GPAD包失败request:{assetStreamPath} ");
                        return false;
                    }

                    if (webRequest.downloadHandler == null)
                    {
                        HMRuntimeDialogHelper.DebugStopWatchInfo($"获取GPAD包失败 webRequest.downloadHandler==null");
                        return false;
                    }

                    if (webRequest.downloadHandler.data == null)
                    {
                        HMRuntimeDialogHelper.DebugStopWatchInfo($"获取GPAD包失败 webRequest.downloadHandler.data==null");
                        return false;
                    }

                    HMRuntimeDialogHelper.DebugStopWatchInfo(
                        $"准备写入 {assetFileName} 路径:{aaPath} result={webRequest.result}" +
                        $"  大小:{(webRequest.downloadHandler.data.Length)}");

                    var sm = new FileStream(aaPath, FileMode.OpenOrCreate);

                    sm.Write(webRequest.downloadHandler.data, 0, webRequest.downloadHandler.data.Length);

                    //await sm.WriteAsync(webRequest.downloadHandler.data, 0, webRequest.downloadHandler.data.Length);

                    sm.Dispose();

                    HMRuntimeDialogHelper.DebugStopWatchInfo($"写入到AA资源路径完成:{aaPath} ");
                }
                catch (Exception e)
                {
                    HMRuntimeDialogHelper.DebugStopWatchInfo($"处理GPAD包发生错误:{assetFileName} error={e.Message}");
                    return false;
                }


                //await UniTask.NextFrame();
            }

            HMRuntimeDialogHelper.DebugStopWatchInfo($"结束所有GPAD资源的复制,共:{allAssetPackNames.Count} 组");

            await UniTask.NextFrame();
            //开始复制

            HMRuntimeDialogHelper.DebugStopWatchInfo(
                $"准备hash和json文件的创建,info= {HMAddressableManager.HMAAConfig.remoteInfo} ");
            if (string.IsNullOrEmpty(HMAddressableManager.HMAAConfig.remoteInfo))
            {
                HMRuntimeDialogHelper.DebugStopWatchInfo($"config中remoteInfo不存在,不创建 ");
                return false;
            }

            string unityAddressablesPath = Path.Combine(Application.persistentDataPath, "com.unity.addressables");
            if (!Directory.Exists(unityAddressablesPath))
            {
                Directory.CreateDirectory(unityAddressablesPath);
                HMRuntimeDialogHelper.DebugStopWatchInfo($"创建hash和json文件目录:{unityAddressablesPath} ");
            }

            var infos = HMAddressableManager.HMAAConfig.remoteInfo.Split('|');
            var hash = infos[0];
            var fileName = infos[1];

            var jsonPath = Path.Combine(unityAddressablesPath, fileName + ".json");
            var hashPath = Path.Combine(unityAddressablesPath, fileName + ".hash");
            if (File.Exists(jsonPath) || File.Exists(hashPath))
            {
                HMRuntimeDialogHelper.DebugStopWatchInfo($"以及有 json或者 hash文件了,跳过处理");
                return true;
            }


            //读取
            var settingPath = Path.Combine(Application.streamingAssetsPath, "aa");
            settingPath = Path.Combine(settingPath, "settings.json");


            using UnityWebRequest settingRequest = UnityWebRequest.Get(settingPath);
            await settingRequest.SendWebRequest();

            if (settingRequest.result == UnityWebRequest.Result.ConnectionError ||
                settingRequest.result == UnityWebRequest.Result.ProtocolError ||
                settingRequest.result == UnityWebRequest.Result.DataProcessingError)
            {
                HMRuntimeDialogHelper.DebugStopWatchInfo($"获取settings.json失败request:{settingPath} ");
                return false;
            }

            if (settingRequest.downloadHandler == null)
            {
                HMRuntimeDialogHelper.DebugStopWatchInfo($"获取settings.json失败 settingRequest.downloadHandler==null");
                return false;
            }

            if (string.IsNullOrEmpty(settingRequest.downloadHandler.text))
            {
                HMRuntimeDialogHelper.DebugStopWatchInfo(
                    $"获取settings.json失败 settingRequest.downloadHandler.text==null");
                return false;
            }

            File.WriteAllText(jsonPath, settingRequest.downloadHandler.text);
            File.WriteAllText(hashPath, hash);

            HMRuntimeDialogHelper.DebugStopWatchInfo($"写入json和hash完成");
            return true;
        }
    }
}