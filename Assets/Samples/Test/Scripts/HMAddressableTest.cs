using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using HM;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.SceneManagement;
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
    private const string LocalTogetherPath = "Assets/Samples/Test/RES/LocalRes/MultipleTestLocal/MuitipleTestLocalCube.prefab";
    private const string RemoteTogetherPath = "Assets/Samples/Test/RES/RemoteRes/MultipleTestRemote/MuitipleTestRemoteSphere.prefab";
    private const string RemoteScenePath = "Assets/Samples/Test/RES/RemoteRes/Scenes/BeLoadScene.unity";
    private const string LocalScenePath = "Assets/Samples/Test/SeceneRes/TestLoadScene.unity";
    private const string LocalGroup = "Assets/Samples/Test/RES/LocalRes/Capsule";
    private static readonly TimeSpan ValidationTimeout = TimeSpan.FromSeconds(15);
    private static bool isValidating;
    private static bool validationNeedsRestart;


    private List<GameObject> allObjs = new List<GameObject>();
    private bool isUpdating;
    private AsyncOperationStatus updateStatus;
    private string updateMessage = string.Empty;
    private string testMessage = "等待测试操作";
    private Vector2 scrollPosition;
    private GUIStyle buttonStyle;
    private GUIStyle labelStyle;

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
        if (prefab == null) return;
        var gameObj = Instantiate(prefab);
        gameObj.transform.position = new Vector3(beSecond ? -1.5f : 0, 0, 2);
        allObjs.Add(gameObj);
    }


    async void Sphere(bool beSecond = false)
    {
        var prefab = await HMAddressableManager.LoadAsync<GameObject>(spherePath);
        if (prefab == null) return;

        var gameObj = Instantiate(prefab);
        gameObj.transform.position = new Vector3(beSecond ? -0.5f : 1, 0, 0);
        allObjs.Add(gameObj);
    }


    private async void UpdateRes()
    {
        if (isUpdating) return;
        isUpdating = true;
        updateStatus = AsyncOperationStatus.None;
        updateMessage = string.Empty;
        try
        {
            await HMAddressableManager.UpdateAddressablesAllAssets(UpdateCb);
            Debug.Assert(updateStatus != AsyncOperationStatus.None, "资源更新结束后未收到最终状态。");
        }
        catch (Exception exception)
        {
            updateStatus = AsyncOperationStatus.Failed;
            updateMessage = exception.Message;
            Debug.LogException(exception);
            return;
        }
        finally
        {
            isUpdating = false;
        }

        if (updateStatus != AsyncOperationStatus.Succeeded) return;
        this.Capsule(true);
        this.Sphere(true);
    }

    private void UpdateCb(AsyncOperationStatus status, float progeress, string message, UpdateStatusCode arg3)
    {
        updateStatus = status;
        updateMessage = message;
        switch (status)
        {
            case AsyncOperationStatus.Failed:
                Debug.LogError($"更新失败，可手动重试。errorCode={arg3} {updateMessage}");
                break;
            case AsyncOperationStatus.Succeeded:
                Debug.Log($"更新成功 Code={arg3}");
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
            if (gameObject == null) continue;
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
            if (objs[i] == null) continue;
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

    private void OnGUI()
    {
        buttonStyle ??= new GUIStyle(GUI.skin.button) { fontSize = 18, fixedHeight = 44 };
        labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
        var previousMatrix = GUI.matrix;
        float scale = Mathf.Min(Screen.width, Screen.height) / 480f;
        var safeArea = Screen.safeArea;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        GUILayout.BeginArea(new Rect(safeArea.x / scale + 8, (Screen.height - safeArea.yMax) / scale + 8,
            Mathf.Min(360, safeArea.width / scale - 16), Mathf.Min(420, safeArea.height / scale - 16)), GUI.skin.box);
        try
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            try
            {
                GUILayout.Label("HMAddressables 功能演示", labelStyle);
                GUILayout.Label(testMessage, labelStyle);
                if (!string.IsNullOrEmpty(updateMessage)) GUILayout.Label(updateMessage, labelStyle);
                var previousEnabled = GUI.enabled;
                try
                {
                    GUI.enabled = previousEnabled && !isValidating && !validationNeedsRestart;
                    DrawTestButtons();
                }
                finally
                {
                    GUI.enabled = previousEnabled;
                }
            }
            finally
            {
                GUILayout.EndScrollView();
            }
        }
        finally
        {
            GUILayout.EndArea();
            GUI.matrix = previousMatrix;
        }
    }

    private void DrawTestButtons()
    {
        var previousValidationEnabled = GUI.enabled;
        try
        {
            GUI.enabled = previousValidationEnabled && !isUpdating;
            if (GUILayout.Button("校验资源加载", buttonStyle)) RunValidation("资源加载", ValidateResourceLoading);
            if (GUILayout.Button("校验资源释放", buttonStyle)) RunValidation("资源释放", ValidateResourceReleasing);
            if (GUILayout.Button("校验场景生命周期", buttonStyle)) RunValidation("场景生命周期", ValidateSceneLifecycle);
            if (GUILayout.Button("校验并发更新（Android）", buttonStyle)) RunValidation("并发更新", ValidateConcurrentUpdating);
        }
        finally
        {
            GUI.enabled = previousValidationEnabled;
        }

        if (GUILayout.Button("1. 加载本地合并资源", buttonStyle)) TestLoad(LocalTogetherPath);
        if (GUILayout.Button("2. 加载本地分散资源", buttonStyle)) TestLoad(capsulePath);
        if (GUILayout.Button("3. 加载远端合并资源", buttonStyle)) TestLoad(RemoteTogetherPath);
        if (GUILayout.Button("4. 加载远端分散资源", buttonStyle)) TestLoad(spherePath);

        if (GUILayout.Button("加载list多个对象", buttonStyle))
        {
            beListTest = true;

            //使用此接口进行统一的资源更新
            //HMAddressableManager.UpdateAddressablesAllAssets(UpdateCb);
        }

        if (GUILayout.Button("释放list多个对象", buttonStyle))
        {
            beListReleaseTest = true;

            //使用此接口进行统一的资源更新
            //HMAddressableManager.UpdateAddressablesAllAssets(UpdateCb);
        }

        if (GUILayout.Button("加载一组多个对象", buttonStyle))
        {
            beTest = true;

            //使用此接口进行统一的资源更新
            //HMAddressableManager.UpdateAddressablesAllAssets(UpdateCb);
        }

        if (GUILayout.Button("释放一组多个对象", buttonStyle))
        {
            beReleaseTest = true;

            //使用此接口进行统一的资源更新
            //HMAddressableManager.UpdateAddressablesAllAssets(UpdateCb);
        }


        bool previousEnabled = GUI.enabled;
        bool updateRequested;
        try
        {
            GUI.enabled = previousEnabled && !isUpdating;
            updateRequested = GUILayout.Button(isUpdating ? "更新中…" :
                updateStatus == AsyncOperationStatus.Failed ? "重试更新" : "更新资源", buttonStyle);
        }
        finally
        {
            GUI.enabled = previousEnabled;
        }
        if (updateRequested) UpdateRes();

        if (GUILayout.Button("载入单个资源", buttonStyle))
        {
            TestLoad(capsulePath, false);
        }

        if (GUILayout.Button("创建对象", buttonStyle))
        {
            this.Capsule();
        }

        if (GUILayout.Button("释放单个资源", buttonStyle))
        {
            HMAddressableManager.ReleaseRes(capsulePath);
        }

        if (GUILayout.Button("删除所有对象", buttonStyle))
        {
            DestroyTestInstances();
        }


        if (GUILayout.Button("加载远端场景（替换当前场景）", buttonStyle))
        {
            LoadRemoteScene();
        }


        if (GUILayout.Button("加载a", buttonStyle))
        {
            TestLoad(capsulePath);
        }
        if (GUILayout.Button("释放a", buttonStyle))
        {
            Release(capsulePath);
        }
        if (GUILayout.Button("加载b", buttonStyle))
        {
            TestLoad(cylinderPath);
        }
        if (GUILayout.Button("释放b", buttonStyle))
        {
            Release(cylinderPath);
        }
    }

    private async void TestLoad(string url, bool createInstance = true)
    {
        testMessage = "正在加载：" + url;
        try
        {
            var prefab = await HMAddressableManager.LoadAsync<GameObject>(url);
            Debug.Assert(prefab != null, "测试资源加载结果为空：" + url);
            if (prefab == null) throw new InvalidOperationException("测试资源加载结果为空：" + url);
            if (this == null) return;
            if (createInstance)
            {
                var instance = Instantiate(prefab);
                instance.transform.position = new Vector3(Random.Range(-1.5f, 1.5f), 0, 0);
                allObjs.Add(instance);
            }
            testMessage = "加载成功：" + url;
            Debug.Log("[HMAA测试] " + testMessage);
        }
        catch (Exception exception)
        {
            testMessage = "加载失败：" + url;
            Debug.LogError("[HMAA测试] " + testMessage);
            Debug.LogException(exception);
        }
    }

    private async void LoadRemoteScene()
    {
        try
        {
            var scene = await HMAddressableManager.LoadSceneAsync(RemoteScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("测试场景加载结果无效：" + RemoteScenePath);
            Debug.Log("[HMAA测试] 场景加载成功：" + RemoteScenePath);
        }
        catch (Exception exception)
        {
            testMessage = "场景加载失败：" + RemoteScenePath;
            Debug.LogException(exception);
        }
    }

    private void Release(string url)
    {
        HMAddressableManager.ReleaseRes(url);
    }

    private void DestroyTestInstances()
    {
        foreach (var instance in allObjs) Destroy(instance);
        allObjs.Clear();
    }

    private async void RunValidation(string name, Func<UniTask> validation)
    {
        if (isValidating || isUpdating || validationNeedsRestart) return;
        isValidating = true;
        testMessage = "正在校验：" + name;
        updateMessage = string.Empty;
        Debug.Log("[HMAA校验] 开始：" + name);
        try
        {
            DestroyTestInstances();
            await UniTask.NextFrame();
            await validation();
            testMessage = "校验通过：" + name;
            Debug.Log("[HMAA校验] " + testMessage);
        }
        catch (Exception exception)
        {
            // 超时的 Unity 操作可能仍在继续，失败后保留现场，重启再重试。
            validationNeedsRestart = true;
            testMessage = "校验失败：" + name + "\n" + exception.Message + "\n请反馈结果，重启后再测试。";
            Debug.LogError("[HMAA校验] " + testMessage);
            Debug.LogException(exception);
        }
        finally
        {
            isValidating = false;
        }
    }

    private static void RequireValidation(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static async UniTask ValidateConcurrentUpdating()
    {
        RequireValidation(Application.platform == RuntimePlatform.Android, "请在 Android 测试 APK 中执行并发更新校验。");
        var firstStatus = AsyncOperationStatus.None;
        var firstCode = default(UpdateStatusCode);
        var firstMessage = string.Empty;
        int firstCallbacks = 0;
        int firstFinalCallbacks = 0;
        int duplicateCallbacks = 0;
        var firstUpdate = HMAddressableManager.UpdateAddressablesAllAssets((status, progress, message, code) =>
        {
            firstCallbacks++;
            if (status == AsyncOperationStatus.None) return;
            firstFinalCallbacks++;
            firstStatus = status;
            firstCode = code;
            firstMessage = message;
        });

        try
        {
            for (int i = 0; i < 2; i++)
            {
                RequireValidation(firstUpdate.Status == UniTaskStatus.Pending,
                    "首个请求已结束，本次没有形成并发，请重启应用后重试。");
                bool rejected = false;
                try
                {
                    await HMAddressableManager.UpdateAddressablesAllAssets((status, progress, message, code) =>
                    {
                        duplicateCallbacks++;
                    }).Timeout(ValidationTimeout);
                }
                catch (InvalidOperationException)
                {
                    rejected = true;
                }
                RequireValidation(rejected, "更新尚未结束时，重复调用没有被拒绝。");
                RequireValidation(duplicateCallbacks == 0, "被拒绝的调用收到了更新回调。");
                Debug.Log($"[HMAA校验] 并发更新：重复调用 {i + 1} 被拒绝，未收到回调。");
            }
        }
        finally
        {
            // 即使重复调用的断言失败，也等待首个请求，避免留下未观察的更新任务。
            await firstUpdate.Timeout(ValidationTimeout);
        }

        RequireValidation(firstStatus == AsyncOperationStatus.Succeeded && firstFinalCallbacks == 1,
            $"首个请求未正常结束或终态回调不唯一：状态={firstStatus}，次数={firstFinalCallbacks}，Code={firstCode}，{firstMessage}");
        RequireValidation(duplicateCallbacks == 0, "重复调用抢走了首个请求的回调。");
        Debug.Log($"[HMAA校验] 并发更新：首个请求成功，终态回调 1 次，Code={firstCode}。");

        int firstCallbacksBeforeNext = firstCallbacks;
        int nextFinalCallbacks = 0;
        var nextStatus = AsyncOperationStatus.None;
        var nextCode = default(UpdateStatusCode);
        var nextMessage = string.Empty;
        await HMAddressableManager.UpdateAddressablesAllAssets((status, progress, message, code) =>
        {
            if (status == AsyncOperationStatus.None) return;
            nextFinalCallbacks++;
            nextStatus = status;
            nextCode = code;
            nextMessage = message;
        }).Timeout(ValidationTimeout);
        RequireValidation(nextStatus == AsyncOperationStatus.Succeeded && nextFinalCallbacks == 1,
            $"结束后再次更新失败或终态回调不唯一：状态={nextStatus}，次数={nextFinalCallbacks}，Code={nextCode}，{nextMessage}");
        RequireValidation(firstCallbacks == firstCallbacksBeforeNext && duplicateCallbacks == 0,
            "新一轮更新仍然通知了上一轮的回调。");
        Debug.Log($"[HMAA校验] 并发更新：结束后再次调用成功，旧回调未再触发，Code={nextCode}。");
    }

    private async UniTask ValidateResourceLoading()
    {
        var paths = new List<string> { spherePath, cylinder2Path, CapsuleCopyPath };
        var assets = await HMAddressableManager.loadAssetsAsync<GameObject>(paths).Timeout(ValidationTimeout);
        await ValidateAssetResults(assets, paths, true);

        var groupAssets = await HMAddressableManager.LoadAssetsAsyncByGroup<GameObject>(LocalGroup).Timeout(ValidationTimeout);
        await ValidateAssetResults(groupAssets, new[] { capsulePath, cylinderPath }, false);

        var capsule = await HMAddressableManager.LoadAsync<GameObject>(capsulePath).Timeout(ValidationTimeout);
        var oldHandles = GetValidationHandles(capsule);
        RequireValidation(HMAddressableManager.ReleaseRes(capsule), "并发加载准备失败：未释放旧 Capsule。");
        await WaitForValidationRelease(new[] { capsule }, oldHandles);
        var concurrent = await UniTask.WhenAll(new[]
        {
            HMAddressableManager.LoadAsync<GameObject>(capsulePath),
            HMAddressableManager.LoadAsync<GameObject>(capsulePath)
        }).Timeout(ValidationTimeout);
        RequireValidation(concurrent[0] != null && concurrent[0] == concurrent[1], "并发加载没有返回同一资源。");
        RequireValidation(GetValidationHandles(concurrent[0]).Count == 1, "并发加载生成了多个缓存记录。");
        Debug.Log("[HMAA校验] 并发加载通过：同一对象，1 条缓存记录。");

        await ValidatePreDownload();
        await ValidateDuplicateAddressLocations();
    }

    private static async UniTask ValidatePreDownload()
    {
        var asset = await HMAddressableManager.LoadAsync<GameObject>(RemoteTogetherPath).Timeout(ValidationTimeout);
        var handles = GetValidationHandles(asset);
        HMAddressableManager.ReleaseRes(asset);
        await WaitForValidationRelease(new[] { asset }, handles);

        // 仅清理这份示例资源的下载缓存，确保预下载不是已缓存时的空操作。
        var clearHandle = Addressables.ClearDependencyCacheAsync(RemoteTogetherPath, false);
        try
        {
            RequireValidation(await clearHandle.ToUniTask().Timeout(ValidationTimeout), "预下载准备失败：缓存未清理。");
        }
        finally
        {
            if (clearHandle.IsValid()) Addressables.Release(clearHandle);
        }
        var paths = new List<string> { RemoteTogetherPath };
        var before = await ReadValidationDownloadSize(paths);
        RequireValidation(before > 0, "清理示例缓存后待下载量仍为 0，不能验证首次预下载。");
        var download = await HMAddressableManager.DownloadAssetsToLocal(paths).Timeout(ValidationTimeout);
        RequireValidation(download.message == "Succeeded", "预下载失败：" + download.message);
        RequireValidation(await ReadValidationDownloadSize(paths) == 0, "预下载后仍有待下载资源。");
        Debug.Log($"[HMAA校验] 预下载通过：下载前={before}，下载后=0。");
    }

    private static async UniTask<long> ReadValidationDownloadSize(IList<string> paths)
    {
        var sizeHandle = Addressables.GetDownloadSizeAsync(paths);
        try
        {
            return await sizeHandle.ToUniTask().Timeout(ValidationTimeout);
        }
        finally
        {
            if (sizeHandle.IsValid()) Addressables.Release(sizeHandle);
        }
    }

    private static async UniTask ValidateAssetResults(IList<GameObject> assets, IList<string> paths, bool ordered)
    {
        RequireValidation(assets != null && assets.Count == paths.Count, "加载数量与输入不一致。");
        RequireValidation(assets.All(asset => asset != null), "加载结果包含空对象。");
        RequireValidation(assets.Select(asset => asset.GetInstanceID()).Distinct().Count() == paths.Count,
            "不同资源返回了重复对象。");
        for (var i = 0; i < paths.Count; i++)
        {
            var reference = Addressables.LoadAssetAsync<GameObject>(paths[i]);
            try
            {
                var expected = await reference.ToUniTask().Timeout(ValidationTimeout);
                RequireValidation(expected != null && (ordered ? assets[i] == expected : assets.Contains(expected)),
                    "加载结果不匹配原生 Addressables：" + paths[i]);
                Debug.Log($"[HMAA校验] 对象匹配：{paths[i]} -> {expected.name} id={expected.GetInstanceID()}");
            }
            finally
            {
                if (reference.IsValid()) Addressables.Release(reference);
            }
        }
        Debug.Log($"[HMAA校验] {(ordered ? "列表" : "组")}逐项检查通过：数量={assets.Count}");
    }

    private async UniTask ValidateDuplicateAddressLocations()
    {
        var paths = new[] { capsulePath, cylinderPath };
        var key = "HMAAValidation/" + Guid.NewGuid().ToString("N");
        var label = key + "/both";
        var secondLabel = key + "/second";
        var locator = new ResourceLocationMap(key);
        var assets = new GameObject[2];
        for (var i = 0; i < paths.Length; i++)
        {
            assets[i] = await HMAddressableManager.LoadAsync<GameObject>(paths[i]).Timeout(ValidationTimeout);
            var handles = GetValidationHandles(assets[i]);
            HMAddressableManager.ReleaseRes(assets[i]);
            await WaitForValidationRelease(new[] { assets[i] }, handles);
            var locations = Addressables.LoadResourceLocationsAsync(paths[i], typeof(GameObject));
            try
            {
                var result = await locations.ToUniTask().Timeout(ValidationTimeout);
                RequireValidation(result.Count == 1, "重名用例的原始位置不唯一：" + paths[i]);
                var source = result[0];
                var alias = new ResourceLocationBase(key, source.InternalId, source.ProviderId, source.ResourceType,
                    source.Dependencies.ToArray()) { Data = source.Data };
                locator.Add(key, alias);
                locator.Add(label, alias);
                if (i == 1) locator.Add(secondLabel, alias);
            }
            finally
            {
                if (locations.IsValid()) Addressables.Release(locations);
            }
        }

        var fixtureAssets = new List<GameObject>();
        Addressables.AddResourceLocator(locator);
        try
        {
            var second = await HMAddressableManager.LoadAssetsAsyncByGroup<GameObject>(secondLabel).Timeout(ValidationTimeout);
            fixtureAssets.AddRange(second);
            assets[1] = second.Count == 1 ? second[0] : null;
            await ValidateAssetResults(second, new[] { cylinderPath }, true);
            assets[0] = await HMAddressableManager.LoadAsync<GameObject>(key).Timeout(ValidationTimeout);
            fixtureAssets.Add(assets[0]);
            await ValidateAssetResults(new[] { assets[0] }, new[] { capsulePath }, true);

            var both = await HMAddressableManager.LoadAssetsAsyncByGroup<GameObject>(label).Timeout(ValidationTimeout);
            fixtureAssets.AddRange(both);
            await ValidateAssetResults(both, paths, false);
            second = await HMAddressableManager.LoadAssetsAsyncByGroup<GameObject>(secondLabel).Timeout(ValidationTimeout);
            fixtureAssets.AddRange(second);
            await ValidateAssetResults(second, new[] { cylinderPath }, true);

            var secondHandles = GetValidationHandles(assets[1]);
            var firstHandles = GetValidationHandles(assets[0]);
            HMAddressableManager.ReleaseResGroup(secondLabel);
            await WaitForValidationRelease(new[] { assets[1] }, secondHandles);
            RequireValidation(firstHandles.All(handle => handle.IsValid()) && GetValidationHandles(assets[0]).Count > 0,
                "按标签释放误伤了同 Address 的另一个资源。");
            Debug.Log("[HMAA校验] 重名 Address 通过：标签位置、地址首项及按标签释放均正确。");
        }
        finally
        {
            try
            {
                foreach (var asset in fixtureAssets.Distinct()) HMAddressableManager.ReleaseRes(asset);
            }
            finally
            {
                Addressables.RemoveResourceLocator(locator);
            }
        }
    }

    private async UniTask ValidateResourceReleasing()
    {
        var paths = new[] { capsulePath, cylinderPath };
        var other = await HMAddressableManager.LoadAsync<GameObject>(spherePath).Timeout(ValidationTimeout);
        var targets = new GameObject[2];
        try
        {
            var otherHandles = GetValidationHandles(other);
            for (var mode = 0; mode < 4; mode++)
            {
                var handles = new List<AsyncOperationHandle>();
                for (var i = 0; i < paths.Length; i++)
                {
                    targets[i] = await HMAddressableManager.LoadAsync<GameObject>(paths[i]).Timeout(ValidationTimeout);
                    handles.AddRange(GetValidationHandles(targets[i]));
                }
                switch (mode)
                {
                    case 0:
                        foreach (var target in targets)
                            RequireValidation(HMAddressableManager.ReleaseRes(target), "按对象释放返回 false。");
                        break;
                    case 1:
                        foreach (var path in paths) HMAddressableManager.ReleaseRes(path);
                        break;
                    case 2:
                        HMAddressableManager.ReleaseRes(paths.ToList());
                        break;
                    case 3:
                        HMAddressableManager.ReleaseResGroup(LocalGroup);
                        break;
                }
                await WaitForValidationRelease(targets, handles);
                RequireValidation(other != null && otherHandles.All(handle => handle.IsValid()), "释放影响了其他资源句柄。");
                var unchanged = await HMAddressableManager.LoadAsync<GameObject>(spherePath).Timeout(ValidationTimeout);
                RequireValidation(unchanged == other, "释放后其他资源对象发生变化。");
                var reloaded = await HMAddressableManager.loadAssetsAsync<GameObject>(paths.ToList()).Timeout(ValidationTimeout);
                await ValidateAssetResults(reloaded, paths, true);
                targets = reloaded.ToArray();
                Debug.Log($"[HMAA校验] 释放方式 {mode} 通过（0对象/1地址/2列表/3组）：句柄失效、记录移除、重载成功，其他资源保持有效。");
            }
        }
        finally
        {
            foreach (var target in targets) HMAddressableManager.ReleaseRes(target);
            HMAddressableManager.ReleaseRes(other);
        }
    }

    // 测试只读取内部记录，确保实际句柄已释放，而不只检查按钮后的日志。
    private static List<AsyncOperationHandle> GetValidationHandles(UnityEngine.Object asset, bool requirePresent = true)
    {
        var field = typeof(HMAddressableManager).GetField("ResourceLoads", BindingFlags.NonPublic | BindingFlags.Static);
        var cache = field?.GetValue(null) as IDictionary;
        RequireValidation(cache != null, "无法读取测试所需的 ResourceLoads，请检查测试 link.xml。");
        var handles = new List<AsyncOperationHandle>();
        foreach (DictionaryEntry entry in cache)
        {
            var type = entry.Value.GetType();
            var resourceField = type.GetField("Resource");
            var handleField = type.GetField("Handle");
            RequireValidation(resourceField != null && handleField != null, "资源记录的测试字段不可用。");
            if (ReferenceEquals(resourceField.GetValue(entry.Value), asset))
                handles.Add((AsyncOperationHandle)handleField.GetValue(entry.Value));
        }
        if (requirePresent)
            RequireValidation(asset != null && handles.Count > 0 && handles.All(handle => handle.IsValid()), "资源未保留有效句柄。");
        return handles;
    }

    private static async UniTask WaitForValidationRelease(IEnumerable<GameObject> assets, IList<AsyncOperationHandle> handles)
    {
        var deadline = Time.realtimeSinceStartup + (float)ValidationTimeout.TotalSeconds;
        while (handles.Any(handle => handle.IsValid()) || assets.Any(asset => GetValidationHandles(asset, false).Count != 0))
        {
            RequireValidation(Time.realtimeSinceStartup < deadline, "释放超时：旧句柄或缓存记录仍然有效。");
            await UniTask.NextFrame();
        }
    }

    private async UniTask ValidateSceneLifecycle()
    {
        var originalScene = gameObject.scene;
        var originalCount = SceneManager.sceneCount;
        foreach (var path in new[] { LocalScenePath, RemoteScenePath })
        {
            Scene scene = default;
            var needsCleanup = false;
            try
            {
                var loaded = await UniTask.WhenAll(new[]
                {
                    HMAddressableManager.LoadSceneAsync(path, LoadSceneMode.Additive, false),
                    HMAddressableManager.LoadSceneAsync(path, LoadSceneMode.Additive, false)
                }).Timeout(ValidationTimeout);
                scene = loaded[0];
                needsCleanup = scene.IsValid();
                RequireValidation(scene.IsValid() && scene == loaded[1] && !scene.isLoaded, "延迟激活或并发场景加载不符合预期：" + path);
                var rejected = false;
                try { await HMAddressableManager.UnloadSceneAsync(path).Timeout(ValidationTimeout); }
                catch (InvalidOperationException) { rejected = true; }
                RequireValidation(rejected, "未激活场景的卸载没有被拒绝。");

                await UniTask.WhenAll(HMAddressableManager.ActivateSceneAsync(scene),
                    HMAddressableManager.ActivateSceneAsync(path)).Timeout(ValidationTimeout);
                RequireValidation(scene.isLoaded && SceneManager.sceneCount == originalCount + 1, "场景激活后数量不正确：" + path);
                await UniTask.WhenAll(HMAddressableManager.UnloadSceneAsync(scene),
                    HMAddressableManager.UnloadSceneAsync(path)).Timeout(ValidationTimeout);
                needsCleanup = false;
                RequireValidation(!scene.IsValid() || !scene.isLoaded, "场景卸载后仍有效：" + path);

                scene = await HMAddressableManager.LoadSceneAsync(path, LoadSceneMode.Additive).Timeout(ValidationTimeout);
                needsCleanup = scene.IsValid();
                RequireValidation(scene.IsValid() && scene.isLoaded, "场景卸载后不能重新加载：" + path);
                await HMAddressableManager.UnloadSceneAsync(scene).Timeout(ValidationTimeout);
                needsCleanup = false;
                RequireValidation((!scene.IsValid() || !scene.isLoaded) && originalScene.isLoaded && SceneManager.sceneCount == originalCount,
                    "场景再次卸载后没有恢复原场景状态：" + path);
                Debug.Log("[HMAA校验] 场景生命周期通过：" + path);
            }
            finally
            {
                if (needsCleanup && scene.IsValid())
                {
                    try
                    {
                        if (!scene.isLoaded) await HMAddressableManager.ActivateSceneAsync(scene).Timeout(ValidationTimeout);
                        await HMAddressableManager.UnloadSceneAsync(scene).Timeout(ValidationTimeout);
                    }
                    catch (Exception cleanupException)
                    {
                        Debug.LogError("[HMAA校验] 场景清理失败：" + path);
                        Debug.LogException(cleanupException);
                    }
                }
            }
        }
    }
}
