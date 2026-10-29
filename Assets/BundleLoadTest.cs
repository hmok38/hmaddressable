using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HM;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

public class BundleLoadTest : MonoBehaviour
{
    private bool beOver;
    private bool isUpdating;
    private bool updateFailed;
    private string updateError;
    public UnityEngine.UI.Text _text;
    async void Start()
    {
        await UniTask.NextFrame();
        await UpdateAndLoadResourcesAsync();
    }

    private async UniTask UpdateAndLoadResourcesAsync()
    {
        if (isUpdating) return;
        isUpdating = true;
        updateFailed = false;
        updateError = string.Empty;
        beOver = false;
        _maxFrameLength = 0;
        _lastMaxFrameIndex = 0;
        var finalStatus = AsyncOperationStatus.None;
        try
        {
            HMRuntimeDialogHelper.StartStopwatch();
            HMRuntimeDialogHelper.DebugStopWatchInfo("开始");
            await HMAddressableManager.UpdateAddressablesAllAssets((status, progress, message, code) =>
            {
                finalStatus = status;
                if (status == AsyncOperationStatus.Failed) updateError = message;
                OnCb(status, progress, message, code);
            });
            if (finalStatus != AsyncOperationStatus.Succeeded)
            {
                updateFailed = true;
                if (string.IsNullOrEmpty(updateError)) updateError = "资源更新未成功，未继续加载示例资源。";
                Debug.LogError(updateError);
                return;
            }

            Debug.Assert(finalStatus == AsyncOperationStatus.Succeeded, "只有更新成功后才能加载示例资源。");
            HMRuntimeDialogHelper.DebugStopWatchInfo("升级完毕");
            var list = await HMAddressableManager.LoadAssetsAsyncByGroup<TextAsset>("Assets/TestBundle/Config");
            HMRuntimeDialogHelper.DebugStopWatchInfo($"配置表加载完毕,配置表数量:{list.Count}");
            var list2 = await HMAddressableManager.LoadAssetsAsyncByGroup<TextAsset>("Assets/TestBundle/Code");
            HMRuntimeDialogHelper.DebugStopWatchInfo($"配置代码加载完毕,配置表数量:{list2.Count}");
            var list3 = await HMAddressableManager.LoadAsync<Texture2D>("Assets/TestBundle/sprite/中世纪3.png");
            if (list3 == null) throw new InvalidOperationException("示例贴图加载失败：Assets/TestBundle/sprite/中世纪3.png");
            HMRuntimeDialogHelper.DebugStopWatchInfo($"配置代码加载完毕,sprite名:{list3.name}");
        }
        catch (Exception exception)
        {
            updateFailed = finalStatus != AsyncOperationStatus.Succeeded;
            if (updateFailed) updateError = "资源更新未完成：" + exception.Message;
            else Debug.LogError("资源更新已成功，但后续示例资源加载失败，不自动重新发起热更。");
            Debug.LogException(exception);
        }
        finally
        {
            isUpdating = false;
            beOver = true;
            Debug.Assert(!isUpdating, "本次更新流程结束后必须允许再次操作。");
        }
    }

    private void OnGUI()
    {
        if (!updateFailed || isUpdating) return;
        GUILayout.Label(updateError);
        if (GUILayout.Button("重试资源更新")) UpdateAndLoadResourcesAsync().Forget();
    }
    
    private void OnCb(AsyncOperationStatus arg0, float arg1, string arg2,UpdateStatusCode arg3)
    {
      HMRuntimeDialogHelper.DebugStopWatchInfo($"OnCB:{arg2}");
    }

    private float _maxFrameLength;
    private int _lastMaxFrameIndex;
    void Update()
    {
        if (!beOver)
        {
            if (_maxFrameLength < Time.deltaTime)
            {
                this._lastMaxFrameIndex =Time.frameCount;
                this._maxFrameLength = Time.deltaTime;
            }
            if (_text != null) _text.text ="当前帧:"+ Time.frameCount.ToString()+$"最大帧{_lastMaxFrameIndex} 长度{_maxFrameLength}";
        }
    }
}
