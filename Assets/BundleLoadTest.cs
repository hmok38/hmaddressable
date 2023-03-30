using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HM;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

public class BundleLoadTest : MonoBehaviour
{
    private bool beOver;
    public UnityEngine.UI.Text _text;
    async void Start()
    {
        beOver = false;
        HMRuntimeDialogHelper.StartStopwatch();
      await  HMAddressableManager.UpdateAddressablesAllAssets(OnCb);
      HMRuntimeDialogHelper.DebugStopWatchInfo("升级完毕");
     var list=  await HMAddressableManager.LoadAssetsAsyncByGroup<TextAsset>("Assets/TestBundle/Config");
      HMRuntimeDialogHelper.DebugStopWatchInfo($"配置表加载完毕,配置表数量:{list.Count}");
      var list2=  await HMAddressableManager.LoadAssetsAsyncByGroup<TextAsset>("Assets/TestBundle/Code");
      HMRuntimeDialogHelper.DebugStopWatchInfo($"配置代码加载完毕,配置表数量:{list2.Count}");
      var list3=  await HMAddressableManager.LoadAssetsAsyncByGroup<Texture2D>("Assets/TestBundle/sprite");
      HMRuntimeDialogHelper.DebugStopWatchInfo($"配置代码加载完毕,配置表数量:{list3.Count}");
      beOver = true;
    }

    private void OnCb(AsyncOperationStatus arg0, float arg1, string arg2)
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
            _text.text ="当前帧:"+ Time.frameCount.ToString()+$"最大帧{_lastMaxFrameIndex} 长度{_maxFrameLength}";
        }
    }
}
