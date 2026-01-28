using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Cysharp.Threading.Tasks;
using HM;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Debug = UnityEngine.Debug;
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

    void Start()
    {
        Addressables.InitializeAsync();
        Debug.Log(Application.persistentDataPath);
        Debug.Log(Application.dataPath);
    }

    // Update is called once per frame
    void Update()
    {
    }

    private async void UpdateRes()
    {
        System.Diagnostics.Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        await HM.HMAddressableManager.UpdateAddressablesAllAssets(null);
        stopwatch.Stop();
        Debug.Log($"更新资源耗时:{stopwatch.ElapsedMilliseconds} ");
    }

    private void OnGUI()
    {
        if (GUILayout.Button("热更"))
        {
            this.UpdateRes();
        }

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

        Debug.Log($"Caching.defaultCache.path:{Caching.defaultCache.path}");
    }

    private async void InstanceObj(string resUrl)
    {
        System.Diagnostics.Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        var objPrefab = await HM.HMAddressableManager.LoadAsync<GameObject>(resUrl);
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