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
         //return true;
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

                    HMRuntimeDialogHelper.DebugStopWatchInfo($"准备写入 {assetFileName} 路径:{aaPath}");

                    using (FileStream sm=new FileStream(aaPath,FileMode.Create))
                    {
                        await sm.WriteAsync(webRequest.downloadHandler.data,0,webRequest.downloadHandler.data.Length);
                    }
                
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
            return true;
        }

    }
}