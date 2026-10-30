using System.Collections.Generic;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using HM;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

/// <summary>
/// 多次分组更新测试
/// </summary>
public class HMAddressableMultipleUpdateTest : MonoBehaviour
{
    private string localResUrl = "Assets/Samples/Test/RES/LocalRes/MultipleTestLocal/MuitipleTestLocalCube.prefab";

    private string remoteResUrl =
        "Assets/Samples/Test/RES/RemoteRes/MultipleTestRemote/MuitipleTestRemoteSphere.prefab";

    private List<GameObject> instanceObjs = new List<GameObject>();
    private bool isUpdating;
    private AsyncOperationStatus updateStatus;
    private string updateMessage = string.Empty;

    void Start()
    {
        Addressables.InitializeAsync();
        Debug.Log(Application.persistentDataPath);
        Debug.Log(Application.dataPath);
        Object.DontDestroyOnLoad(this.gameObject);
        HMAddressableManager.OnLoadingResException += (resName, ex) => { Debug.Log($"资源加载失败:{resName} {ex}"); };
    }

    // Update is called once per frame
    void Update()
    {
    }

    private async void UpdateRes()
    {
        if (isUpdating) return;
        isUpdating = true;
        updateStatus = AsyncOperationStatus.None;
        updateMessage = string.Empty;
        System.Diagnostics.Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        try
        {
            await HM.HMAddressableManager.UpdateAddressablesAllAssets((status, progress, message, code) =>
            {
                updateStatus = status;
                updateMessage = $"Code={code} {message}";
            });
            Debug.Assert(updateStatus != AsyncOperationStatus.None, "资源更新结束后未收到最终状态。");
        }
        catch (System.Exception exception)
        {
            updateStatus = AsyncOperationStatus.Failed;
            updateMessage = exception.Message;
            Debug.LogException(exception);
            return;
        }
        finally
        {
            stopwatch.Stop();
            isUpdating = false;
        }

        if (updateStatus != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError($"更新资源失败，可手动重试：{updateMessage}");
            return;
        }
        Debug.Log($"更新资源成功，耗时:{stopwatch.ElapsedMilliseconds} {updateMessage}");
    }

    private void OnGUI()
    {
        bool previousEnabled = GUI.enabled;
        bool updateRequested;
        try
        {
            GUI.enabled = previousEnabled && !isUpdating;
            updateRequested = GUILayout.Button(isUpdating ? "更新中…" :
                updateStatus == AsyncOperationStatus.Failed ? "重试热更" : "热更");
        }
        finally
        {
            GUI.enabled = previousEnabled;
        }
        if (updateRequested) this.UpdateRes();

        if (GUILayout.Button("释放所有资源"))
        {
            HM.HMAddressableManager.ReleaseRes(this.localResUrl);
            HMAddressableManager.ReleaseRes((this.remoteResUrl));
        }

        if (GUILayout.Button("实例化本地"))
        {
            this.InstanceObj(this.localResUrl);
        }

        if (GUILayout.Button("实例化远程"))
        {
            this.InstanceObj(this.remoteResUrl);
        }

        if (GUILayout.Button("销毁所有实例"))
        {
            this.DestroyAllObj();
        }

        if (GUILayout.Button("预下载远程资源组"))
        {
            TestLoadRemoteRes().Forget();
        }

        if (GUILayout.Button("加载场景"))
        {
            TestLoadScene().Forget();
        }

        if (GUILayout.Button("卸载场景"))
        {
            TestUnloadScene().Forget();
        }


        if (GUILayout.Button("调用释放无用资源"))
        {
            Resources.UnloadUnusedAssets();
            System.GC.Collect();
        }
    }

    private async UniTask TestLoadScene()
    {
        // await HM.HMAddressableManager.LoadSceneAsync("Assets/Samples/Test/SeceneRes/TestLoadScene.unity");

        await HM.HMAddressableManager.LoadSceneAsync("TestLoadScene");
    }

    private async UniTask TestUnloadScene()
    {
        await HM.HMAddressableManager.LoadSceneAsync("TestScene");
        await UniTask.NextFrame();
        await HM.HMAddressableManager.UnloadSceneAsync("TestLoadScene");
    }

    private async UniTask TestLoadRemoteRes()
    {
        var a = await HM.HMAddressableManager.DownloadAssetsToLocal(
            new List<string>()
            {
                "Assets/Samples/Test/RES/RemoteRes/MultipleTestRemote/MuitipleTestRemoteSphere.prefab",
                "Assets/Samples/Test/RES/RemoteRes/Sphere/CapsuleCopy.prefab"
            },
            (long current, long total, float pro) =>
            {
                Debug.Log("预下载远程资源组进度:" + current + "/" + total + " pro:" + pro);
            });

      //  Debug.Log($"Caching.defaultCache.path:{Caching.defaultCache.path}");
    }

    private async void InstanceObj(string resUrl)
    {
        System.Diagnostics.Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        var objPrefab = await HM.HMAddressableManager.LoadAsync<GameObject>(resUrl);
        if (objPrefab == null) return;
        Debug.Log($"资源加载完成{resUrl}  资源是否为空:{objPrefab==null}");
        var obj = GameObject.Instantiate(objPrefab);
        this.instanceObjs.Add(obj);
        obj.transform.position = Random.insideUnitSphere * 3;
        stopwatch.Stop();
        Debug.Log($"实例化耗时:{stopwatch.ElapsedMilliseconds} 资源路径:{resUrl}");
    }

    private void DestroyAllObj()
    {
        for (int i = 0; i < this.instanceObjs.Count; i++)
        {
            Destroy(this.instanceObjs[i]);
        }

        this.instanceObjs.Clear();
    }
}
