using System;
using System.Collections.Generic;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using HM;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

//多个资源列表加载和分开加载是一样的耗时
public class HMAddressableTest : MonoBehaviour
{
    string capsulePath = "Assets/Samples/Test/RES/LocalRes/Capsule/Capsule.prefab";
    string spherePath = "Assets/Samples/Test/RES/RemoteRes/Sphere/Sphere.prefab";
    private string cylinderPath = "Assets/Samples/Test/RES/LocalRes/Capsule/Cylinder.prefab";
    private string cylinder2Path = "Assets/Samples/Test/RES/LocalRes/New/Cylinder2.prefab";
    private string CapsuleCopyPath = "Assets/Samples/Test/RES/RemoteRes/Sphere/CapsuleCopy.prefab";


    private List<GameObject> allObjs = new List<GameObject>();

    void Start()
    {
#if !UNITY_EDITOR
         Addressables.InitializeAsync();
#endif
    }

    async void Wait()
    {
        await UniTask.Delay(TimeSpan.FromSeconds(2), DelayType.UnscaledDeltaTime);
        HMAddressableManager.ReleaseRes(capsulePath);
        // await UniTask.Delay(TimeSpan.FromSeconds(10), DelayType.UnscaledDeltaTime);
        // this.Capsule();
        // this.Capsule();
        // this.Capsule();
        // this.Capsule();
        // this.Capsule();
    }


    async void Capsule(bool beSecond = false)
    {
        var prefabUniTask = HMAddressableManager.LoadAsync<GameObject>(capsulePath);
        var prefab = await prefabUniTask;
        var gameObj = Instantiate(prefab);
        gameObj.transform.position = new Vector3(beSecond ? -1.5f : 0, 0, 2);
        allObjs.Add(gameObj);
    }


    void Sphere(bool beSecond = false)
    {
        var prefab = HMAddressableManager.Load<GameObject>(spherePath);

        var gameObj = Instantiate(prefab);
        gameObj.transform.position = new Vector3(beSecond ? -0.5f : 1, 0, 0);
        allObjs.Add(gameObj);
    }


    private void UpdateCb(AsyncOperationStatus status, float progeress, string message, UpdateStatusCode arg3)
    {
        switch (status)
        {
            case AsyncOperationStatus.Failed:
                Debug.LogError($"errorCode={arg3}");
                break;
            case AsyncOperationStatus.Succeeded:
                Debug.Log($"更新成功 Code={arg3}");
                this.Capsule(true);
                this.Sphere(true);
                break;
            case AsyncOperationStatus.None:
                Debug.Log($"正在下载,进度={progeress} Code={arg3}");
                break;
        }
    }

    private bool beListTest = false;
    private bool beListReleaseTest = false;

    private bool beTest = false;
    private bool beReleaseTest = false;

    private void Update()
    {
        if (beListTest)
        {
            beListTest = false;
            ListTest();
        }

        if (beListReleaseTest)
        {
            beListReleaseTest = false;
            ListReleaseObjTest();
        }

        if (beTest)
        {
            beTest = false;
            Test();
        }

        if (beReleaseTest)
        {
            beReleaseTest = false;
            ReleaseObjTest();
        }
    }

    private GameObject testObj;
    private List<GameObject> testObjs = new List<GameObject>();
    private AsyncOperationHandle teamHandle;
    private AsyncOperationHandle handle1;
    private AsyncOperationHandle handle2;
    private AsyncOperationHandle handle3;
    private AsyncOperationHandle handle4;
    private AsyncOperationHandle handle5;

    private async void ListTest()
    {
        List<string> list = new List<string>()
            { this.spherePath,  this.cylinder2Path, this.CapsuleCopyPath };
        Debug.Log("开始测试List加载");
        System.Diagnostics.Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();


        var objs = await HMAddressableManager.loadAssetsAsync<GameObject>(list);


        stopwatch.Stop();
        Debug.Log($"List加载耗时={stopwatch.ElapsedMilliseconds}");

        foreach (var gameObject in objs)
        {
            var g = Instantiate(gameObject);
            g.transform.position = new Vector3(Random.Range(-5f, 5f), 0, 0);
            this.allObjs.Add(g);
        }
    }

    private void ListReleaseObjTest()
    {
        List<string> list = new List<string>()
            {  this.spherePath, this.cylinder2Path, this.CapsuleCopyPath }; //this.cylinderPath,
        HMAddressableManager.ReleaseRes(list);


        Debug.Log("卸载资源");
    }

    private async void Test()
    {
        List<string> list = new List<string>()
            { this.capsulePath, this.spherePath, this.cylinderPath, this.cylinder2Path, this.CapsuleCopyPath };
        Debug.Log("开始单独加载测试");
        System.Diagnostics.Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        var objs = await HMAddressableManager.LoadAssetsAsyncByGroup<GameObject>(
            "Assets/Samples/Test/RES/LocalRes/Capsule");

        stopwatch.Stop();
        Debug.Log($"单个加载耗时={stopwatch.ElapsedMilliseconds} objs数量={objs.Count}");
        for (int i = 0; i < objs.Count; i++)
        {
            var g = Instantiate(objs[i]);
            g.transform.position = new Vector3(Random.Range(-5f, 5f), 0, 0);
            this.allObjs.Add(g);
        }
    }

    private void ReleaseObjTest()
    {
        HMAddressableManager.ReleaseResGroup("Assets/Samples/Test/RES/LocalRes/Capsule");

        Debug.Log("卸载资源");
    }

    private async void OnGUI()
    {
        if (GUILayout.Button("加载list多个对象"))
        {
            beListTest = true;

            //使用此接口进行统一的资源更新
            //HMAddressableManager.UpdateAddressablesAllAssets(UpdateCb);
        }

        if (GUILayout.Button("释放list多个对象"))
        {
            beListReleaseTest = true;

            //使用此接口进行统一的资源更新
            //HMAddressableManager.UpdateAddressablesAllAssets(UpdateCb);
        }

        if (GUILayout.Button("加载一组多个对象"))
        {
            beTest = true;

            //使用此接口进行统一的资源更新
            //HMAddressableManager.UpdateAddressablesAllAssets(UpdateCb);
        }

        if (GUILayout.Button("释放一组多个对象"))
        {
            beReleaseTest = true;

            //使用此接口进行统一的资源更新
            //HMAddressableManager.UpdateAddressablesAllAssets(UpdateCb);
        }


        if (GUILayout.Button("更新资源"))
        {
            //使用此接口进行统一的资源更新
            await HMAddressableManager.UpdateAddressablesAllAssets(UpdateCb);
        }

        if (GUILayout.Button("载入单个资源"))
        {
            await HMAddressableManager.LoadAsync<GameObject>(capsulePath);
        }

        if (GUILayout.Button("创建对象"))
        {
            this.Capsule();
        }

        if (GUILayout.Button("释放单个资源"))
        {
            HMAddressableManager.ReleaseRes(capsulePath);
        }

        if (GUILayout.Button("删除所有对象"))
        {
            for (int i = 0; i < this.allObjs.Count; i++)
            {
                Destroy(this.allObjs[i]);
            }

            this.allObjs.Clear();
        }


        if (GUILayout.Button("加载场景"))
        {
            await HMAddressableManager.LoadSceneAsync("Assets/Samples/Test/RES/Scenes/BeLoadScene.unity");
        }


        if (GUILayout.Button("加载a"))
        {
            TestLoad(capsulePath);
        }
        if (GUILayout.Button("释放a"))
        {
            Release(capsulePath);
        }
        if (GUILayout.Button("加载b"))
        {
            TestLoad(cylinderPath);
        }
        if (GUILayout.Button("释放b"))
        {
            Release(cylinderPath);
        }
    }

    private async void TestLoad(string url)
    {
        var prefabs = await HMAddressableManager.LoadAsync<GameObject>(url);
        var a = Instantiate(prefabs);
        a.transform.position = new Vector3(Random.Range(-5f, 5f), 0, 0);
        this.allObjs.Add(a);
    }

    private void Release(string url)
    {
        HMAddressableManager.ReleaseRes(url);
    }
}