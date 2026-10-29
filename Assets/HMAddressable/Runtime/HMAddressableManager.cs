using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Events;
using UnityEngine.AddressableAssets.ResourceLocators;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine.ResourceManagement.ResourceLocations;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HM
{
    /// <summary>
    /// HMAddresablesAsset资源管理系统的运行时代码,可以通过它进行资源升级和加载
    /// </summary>
    public static class HMAddressableManager
    {
        //-------防止代码裁剪的类
        private static HMAAEncrypt_AssetBundleProvider test = new HMAAEncrypt_AssetBundleProvider();
        private static HMAAEncrypt_AssetBundleProvider_AES test6 = new HMAAEncrypt_AssetBundleProvider_AES();

        private static HMAAEncrypt_AssetBundleProvider_AESWithSeek test7 =
            new HMAAEncrypt_AssetBundleProvider_AESWithSeek();

        private static AESStreamProcessor test2 = new AESStreamProcessor();
        private static AESStreamProcessorWithSeek test4 = new AESStreamProcessorWithSeek();

        private static AssetBundleProvider test5 = new AssetBundleProvider();

        //-------防止代码裁剪的类
        private const string PrefsName = "ADDRESSABLES_NEEDUPDATE_NEWV3";

        static readonly Dictionary<string, Object> ResMap =
            new Dictionary<string, Object>();

        private static readonly Dictionary<string, UniTaskCompletionSource<Object>> LoadingMap =
            new Dictionary<string, UniTaskCompletionSource<Object>>();
        private static readonly Dictionary<string, AsyncOperationHandle> ResourceHandles =
            new Dictionary<string, AsyncOperationHandle>();

        private static Dictionary<string, SceneInstance> LoadedSceneMap = new Dictionary<string, SceneInstance>();
        private static readonly Dictionary<string, UniTaskCompletionSource<Scene>> LoadingSceneMap =
            new Dictionary<string, UniTaskCompletionSource<Scene>>();
        private static readonly Dictionary<string, UniTaskCompletionSource> ActivatingSceneMap =
            new Dictionary<string, UniTaskCompletionSource>();
        private static readonly Dictionary<string, UniTaskCompletionSource> UnloadingSceneMap =
            new Dictionary<string, UniTaskCompletionSource>();


        public static Action<string, Exception> OnLoadingResException;

        /// <summary>
        /// HMAA的配置表,位于Assets/HMAddressables/Resource/ConfigHMAddressables.asset
        /// </summary>
        public static HMAddressablesConfig HMAAConfig
        {
            get
            {
                var platformConfig = HMAddressablePlatformConfig;

                return platformConfig.CurrentConfig;
            }
        }

        /// <summary>
        /// 多平台配置表
        /// </summary>
        public static HMAddressablePlatformConfig HMAddressablePlatformConfig
        {
            get
            {
                var platformConfig = Resources.Load<HMAddressablePlatformConfig>("HMAddressablePlatformConfig");
                if (platformConfig == null)
                {
                    Debug.LogError("未找到HMAA插件的平台配置表,请创建和配置,位于:Assets/HMAddressables/Resource/");
                    return null;
                }

                return platformConfig;
            }
        }

        /// <summary>
        /// 统一化路径分隔符，将反斜杠(\)转为正斜杠(/)。
        /// 运行时字符串中转义符已被解析为控制字符，不会被误修改。
        /// 正斜杠 Unity 全平台兼容。
        /// </summary>
        /// <param name="path">原始路径</param>
        /// <param name="removeTrailingSeparator">是否移除末尾分隔符</param>
        /// <returns>规范化后的路径</returns>
        public static string NormalizePath(string path, bool removeTrailingSeparator = false)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            // 统一分隔符：反斜杠 → 正斜杠
            var normalized = path.Replace('\\', '/');

            // // 移除末尾分隔符
            // if (removeTrailingSeparator && normalized.Length > 1 && normalized.EndsWith("/"))
            //     normalized = normalized.TrimEnd('/');

            return normalized;
        }

        /// <summary>
        /// 同步加载资源。WebGL 仅能直接返回已缓存资源，未缓存资源请使用 LoadAsync。
        /// </summary>
        /// <param name="resNameP"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T Load<T>(string resNameP) where T : UnityEngine.Object
        {
            var resName = NormalizePath(resNameP);
            if (string.IsNullOrEmpty(resName)) throw new ArgumentException("资源地址不能为空。", nameof(resNameP));
            if (ResMap.TryGetValue(resName, out var cached)) return cached as T;
            RequireSynchronousLoading(nameof(LoadAsync));
            if (LoadingMap.ContainsKey(resName))
                throw new InvalidOperationException($"资源 {resName} 正在异步加载，请使用 LoadAsync 等待同一次加载。");

            var completion = new UniTaskCompletionSource<Object>();
            LoadingMap.Add(resName, completion);
            AsyncOperationHandle<T> operation = default;
            T result = null;
            Exception failure = null;
            try
            {
#if UNITY_EDITOR
                if (!HasAssets(resName)) result = AssetDatabase.LoadAssetAtPath<T>(resName);
#endif
                if (result == null)
                {
                    operation = Addressables.LoadAssetAsync<T>(resName);
                    result = operation.WaitForCompletion();
                    if (operation.Status != AsyncOperationStatus.Succeeded || result == null)
                        throw operation.OperationException ?? new InvalidOperationException("资源加载结果为空：" + resName);
                    ResourceHandles[resName] = operation;
                    operation = default;
                }
                ResMap[resName] = result;
            }
            catch (Exception exception)
            {
                result = null;
                failure = exception;
            }
            finally
            {
                if (operation.IsValid()) Addressables.Release(operation);
                LoadingMap.Remove(resName);
                Debug.Assert(!LoadingMap.ContainsKey(resName), "资源加载结束后仍存在加载标记。");
                completion.TrySetResult(result);
            }
            if (failure != null) NotifyLoadingFailure(resName, failure);
            return result;
        }

        /// <summary>
        /// 异步加载资源。同一地址的并发请求共用一次加载；失败返回 null 并通知 OnLoadingResException。
        /// </summary>
        public static async UniTask<T> LoadAsync<T>(string resNameP) where T : UnityEngine.Object
        {
            var resName = NormalizePath(resNameP);
            if (string.IsNullOrEmpty(resName)) throw new ArgumentException("资源地址不能为空。", nameof(resNameP));
            if (ResMap.TryGetValue(resName, out var cached)) return cached as T;
            if (LoadingMap.TryGetValue(resName, out var pending)) return await pending.Task as T;

            var completion = new UniTaskCompletionSource<Object>();
            LoadingMap.Add(resName, completion);
            AsyncOperationHandle<T> operation = default;
            T result = null;
            Exception failure = null;
            try
            {
#if UNITY_EDITOR
                if (!await HasAssetsAsync(resName)) result = AssetDatabase.LoadAssetAtPath<T>(resName);
#endif
                if (result == null)
                {
                    operation = Addressables.LoadAssetAsync<T>(resName);
                    result = await operation.ToUniTask();
                    if (result == null)
                        throw new InvalidOperationException("资源加载结果为空：" + resName);
                    ResourceHandles[resName] = operation;
                    operation = default;
                }
                ResMap[resName] = result;
            }
            catch (Exception exception)
            {
                result = null;
                failure = exception;
            }
            finally
            {
                if (operation.IsValid()) Addressables.Release(operation);
                LoadingMap.Remove(resName);
                Debug.Assert(!LoadingMap.ContainsKey(resName), "资源加载结束后仍存在加载标记。");
                completion.TrySetResult(result);
            }
            if (failure != null) NotifyLoadingFailure(resName, failure);
            return result;
        }

        private static void RequireSynchronousLoading(string asyncMethod)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            throw new PlatformNotSupportedException($"WebGL 不支持同步等待 Addressables，请使用 {asyncMethod}。");
#endif
        }

        private static void NotifyLoadingFailure(string key, Exception exception)
        {
            var callbacks = OnLoadingResException;
            if (callbacks == null)
            {
                Debug.LogException(exception);
                return;
            }
            foreach (Action<string, Exception> callback in callbacks.GetInvocationList())
            {
                try { callback(key, exception); }
                catch (Exception callbackException) { Debug.LogException(callbackException); }
            }
        }

        /// <summary>
        /// 加载多个资源
        /// </summary>
        /// <param name="resNames"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static async UniTask<List<T>> loadAssetsAsync<T>(List<string> resNames) where T : UnityEngine.Object
        {
            UniTask<T>[] uniTasks = new UniTask<T>[resNames.Count];
            for (int i = 0; i < resNames.Count; i++)
            {
                uniTasks[i] = (LoadAsync<T>(resNames[i]));
            }

            var objs = await UniTask.WhenAll(uniTasks);

            return objs.ToList();
        }

        /// <summary>
        /// 通过组或者标签加载多个资源
        /// </summary>
        /// <param name="groupNameOrLabel"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static async UniTask<List<T>> LoadAssetsAsyncByGroup<T>(string groupNameOrLabel)
            where T : UnityEngine.Object
        {
            List<string> names = null;
#if UNITY_EDITOR
            names = GetAllResouceAssetsByFolder<T>(groupNameOrLabel);
#endif
            if (names == null || names.Count <= 0)
            {
                names = await GetResourceNamesAsync(groupNameOrLabel, typeof(T));
            }

            return await loadAssetsAsync<T>(names);
        }

        /// <summary>
        /// 同步判断是否有资源；WebGL 请使用 HasAssetsAsync。
        /// </summary>
        /// <param name="resName"></param>
        /// <returns></returns>
        public static bool HasAssets(string resName)
        {
            resName = NormalizePath(resName);
            if (string.IsNullOrEmpty(resName)) return false;
            RequireSynchronousLoading(nameof(HasAssetsAsync));
#if UNITY_EDITOR
            //编辑器下检查是不是有设置,没有就直接返回,有就真的判断是不是存在资源
            var sett = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/AddressableAssetsData/AddressableAssetSettings.asset");
            if (sett == null)
            {
                return false;
            }
#endif
            var rs = Addressables.LoadResourceLocationsAsync(resName);

            try
            {
                rs.WaitForCompletion();
                return rs.Status == AsyncOperationStatus.Succeeded && rs.Result != null && rs.Result.Count > 0;
            }
            finally
            {
                if (rs.IsValid()) Addressables.Release(rs);
            }
        }

        /// <summary>异步判断 Catalog 中是否存在资源，可在 WebGL 使用；查询异常会传给调用方。</summary>
        public static async UniTask<bool> HasAssetsAsync(string resName)
        {
            resName = NormalizePath(resName);
            if (string.IsNullOrEmpty(resName)) return false;
#if UNITY_EDITOR
            if (AssetDatabase.LoadAssetAtPath<Object>("Assets/AddressableAssetsData/AddressableAssetSettings.asset") == null)
                return false;
#endif
            var operation = Addressables.LoadResourceLocationsAsync(resName);
            try
            {
                var locations = await operation.ToUniTask();
                return locations != null && locations.Count > 0;
            }
            finally
            {
                if (operation.IsValid()) Addressables.Release(operation);
            }
        }

        private static async UniTask<List<string>> GetResourceNamesAsync(string key, Type type = null)
        {
            var operation = Addressables.LoadResourceLocationsAsync(key, type);
            try
            {
                var locations = await operation.ToUniTask();
                return locations.Select(location => location.PrimaryKey).Distinct().ToList();
            }
            finally
            {
                if (operation.IsValid()) Addressables.Release(operation);
            }
        }

        /// <summary>
        /// 判断是否有资源
        /// </summary>
        /// <param name="resName"></param>
        /// <returns></returns>
        public static bool HasSceneAssets(string resName)
        {
#if UNITY_EDITOR
            //编辑器下检查是不是有设置,没有就直接返回,有就真的判断是不是存在资源
            var sett = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/AddressableAssetsData/AddressableAssetSettings.asset");
            if (sett == null)
            {
                return false;
            }
#endif
            //对于场景来说,有AA资源就用aa加载.如果没有设置为AA资源的情况下,只要加入了Build Settings中,并勾选了也可以通过aa加载出来
            //如果没有设置为AA资源,并且也没有勾选,则会报错,没有AA路径-因为默认加入了Build Settings就会在打包时成为AA资源
            return true;
        }

        /// <summary>
        /// 释放资源,
        /// </summary>
        /// <param name="res"></param>
        /// <returns></returns>
        public static bool ReleaseRes(Object res)
        {
            if (res == null) return false;
            var item = ResMap.FirstOrDefault(pair => pair.Value == res);
            return item.Key != null && ReleaseLoadedResource(item.Key);
        }

        /// <summary>释放资源；若正在加载，等待本次加载结束后释放，不会重新发起加载。</summary>
        public static async void ReleaseRes(string resNameP)
        {
            var resName = NormalizePath(resNameP);
            if (string.IsNullOrEmpty(resName)) return;
            try
            {
                if (LoadingMap.TryGetValue(resName, out var pending))
                {
                    var loaded = await pending.Task;
                    if (loaded == null) return;
                }
                ReleaseLoadedResource(resName);
            }
            catch (Exception exception)
            {
                NotifyLoadingFailure(resName, exception);
            }
        }

        private static bool ReleaseLoadedResource(string resName)
        {
            if (!ResMap.Remove(resName)) return false;
            if (ResourceHandles.TryGetValue(resName, out var handle))
            {
                ResourceHandles.Remove(resName);
                if (handle.IsValid()) Addressables.Release(handle);
            }
            return true;
        }

        /// <summary>释放多个资源。</summary>
        public static void ReleaseRes(List<string> resNames)
        {
            foreach (var name in resNames) ReleaseRes(name);
        }

        /// <summary>通过组名或者标签释放多个资源。</summary>
        public static async void ReleaseResGroup(string groupNameOrLabel)
        {
            try
            {
                List<string> names = null;
#if UNITY_EDITOR
                names = GetAllResouceAssetsByFolder<Object>(groupNameOrLabel);
#endif
                if (names == null || names.Count == 0)
                    names = await GetResourceNamesAsync(groupNameOrLabel);
                ReleaseRes(names);
            }
            catch (Exception exception)
            {
                NotifyLoadingFailure(groupNameOrLabel, exception);
            }
        }

        /// <summary>
        /// 异步加载场景，同一地址的并发请求共用一次加载。未激活场景按加载操作完成判断，不等待 isLoaded。
        /// </summary>
        public static async UniTask<Scene> LoadSceneAsync(string sceneName,
            LoadSceneMode loadSceneMode = LoadSceneMode.Single, bool activeteOnLoad = true)
        {
            sceneName = NormalizePath(sceneName);
            if (string.IsNullOrEmpty(sceneName)) throw new ArgumentException("场景地址不能为空。", nameof(sceneName));
            if (UnloadingSceneMap.TryGetValue(sceneName, out var unloading)) await unloading.Task;
            if (LoadingSceneMap.TryGetValue(sceneName, out var pending)) return await pending.Task;
            if (LoadedSceneMap.TryGetValue(sceneName, out var loaded) && loaded.Scene.IsValid()) return loaded.Scene;
            LoadedSceneMap.Remove(sceneName);

            var completion = new UniTaskCompletionSource<Scene>();
            LoadingSceneMap.Add(sceneName, completion);
            return await LoadSceneCoreAsync(sceneName, loadSceneMode, activeteOnLoad, completion);
        }

        private static async UniTask<Scene> LoadSceneCoreAsync(string sceneName, LoadSceneMode mode, bool activate,
            UniTaskCompletionSource<Scene> completion, AsyncOperationHandle<SceneInstance> operation = default)
        {
            Scene result = default;
            Exception failure = null;
            try
            {
#if UNITY_EDITOR
                if (!operation.IsValid() && !HasSceneAssets(sceneName))
                {
                    var editorOperation = SceneManager.LoadSceneAsync(sceneName, mode);
                    if (editorOperation == null) throw new InvalidOperationException("无法加载场景：" + sceneName);
                    await editorOperation.ToUniTask();
                    result = GetSceneByPathOrName(sceneName);
                    if (!result.IsValid()) throw new InvalidOperationException("场景加载结果无效：" + sceneName);
                }
                else
#endif
                {
                    if (!operation.IsValid()) operation = Addressables.LoadSceneAsync(sceneName, mode, activate);
                    var instance = await operation.ToUniTask();
                    if (!instance.Scene.IsValid()) throw new InvalidOperationException("场景加载结果无效：" + sceneName);
                    LoadedSceneMap[sceneName] = instance;
                    result = instance.Scene;
                    operation = default; // 成功句柄交给场景卸载流程释放。
                }
            }
            catch (Exception exception)
            {
                LoadedSceneMap.Remove(sceneName);
                failure = exception;
            }
            finally
            {
                if (operation.IsValid()) Addressables.Release(operation);
                LoadingSceneMap.Remove(sceneName);
                Debug.Assert(!LoadingSceneMap.ContainsKey(sceneName), "场景加载结束后仍存在加载标记。");
                completion.TrySetResult(result);
            }
            if (failure != null) NotifyLoadingFailure(sceneName, failure);
            return result;
        }

        /// <summary>
        /// 同步取得场景。WebGL 请使用 LoadSceneAsync；其他平台的场景激活仍可能等待下一帧。
        /// </summary>
        public static Scene LoadScene(string sceneName, LoadSceneMode loadSceneMode = LoadSceneMode.Single,
            bool activeteOnLoad = true)
        {
            sceneName = NormalizePath(sceneName);
            if (string.IsNullOrEmpty(sceneName)) throw new ArgumentException("场景地址不能为空。", nameof(sceneName));
            if (LoadingSceneMap.ContainsKey(sceneName) || UnloadingSceneMap.ContainsKey(sceneName))
                throw new InvalidOperationException("场景正在加载或卸载，请使用 LoadSceneAsync：" + sceneName);
            if (LoadedSceneMap.TryGetValue(sceneName, out var loaded) && loaded.Scene.IsValid()) return loaded.Scene;
            RequireSynchronousLoading(nameof(LoadSceneAsync));
            LoadedSceneMap.Remove(sceneName);

            var completion = new UniTaskCompletionSource<Scene>();
            LoadingSceneMap.Add(sceneName, completion);
            AsyncOperationHandle<SceneInstance> operation = default;
            Scene result = default;
            Exception failure = null;
            bool delegated = false;
            try
            {
#if UNITY_EDITOR
                if (!HasSceneAssets(sceneName))
                {
                    SceneManager.LoadScene(sceneName, loadSceneMode);
                    result = GetSceneByPathOrName(sceneName);
                }
                else
#endif
                {
                    operation = Addressables.LoadSceneAsync(sceneName, loadSceneMode, activeteOnLoad);
                    var instance = operation.WaitForCompletion();
                    if (!operation.IsDone)
                    {
                        // 场景激活可能尚未结束，保留句柄并由异步流程完成清理。
                        if (instance.Scene.IsValid()) LoadedSceneMap[sceneName] = instance;
                        delegated = true;
                        LoadSceneCoreAsync(sceneName, loadSceneMode, activeteOnLoad, completion, operation).Forget();
                        operation = default;
                        return instance.Scene;
                    }
                    if (operation.Status != AsyncOperationStatus.Succeeded || !instance.Scene.IsValid())
                        throw operation.OperationException ?? new InvalidOperationException("场景加载结果无效：" + sceneName);
                    LoadedSceneMap[sceneName] = instance;
                    result = instance.Scene;
                    operation = default;
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                if (operation.IsValid()) Addressables.Release(operation);
                if (!delegated)
                {
                    LoadingSceneMap.Remove(sceneName);
                    completion.TrySetResult(result);
                }
            }
            if (failure != null) NotifyLoadingFailure(sceneName, failure);
            return result;
        }

        /// <summary>
        /// 显式激活已加载的场景，同一地址的并发请求共用一次激活；失败会传给调用方。
        /// </summary>
        public static async UniTask ActivateSceneAsync(string scenePath)
        {
            scenePath = NormalizePath(scenePath);
            if (string.IsNullOrEmpty(scenePath)) throw new ArgumentException("场景地址不能为空。", nameof(scenePath));
            if (LoadingSceneMap.TryGetValue(scenePath, out var loading)) await loading.Task;
            if (UnloadingSceneMap.ContainsKey(scenePath))
                throw new InvalidOperationException("场景正在卸载，不能激活：" + scenePath);
            if (ActivatingSceneMap.TryGetValue(scenePath, out var pending))
            {
                await pending.Task;
                return;
            }
            if (!LoadedSceneMap.TryGetValue(scenePath, out var instance) || !instance.Scene.IsValid())
                throw new InvalidOperationException("场景尚未加载或已经失效，不能激活：" + scenePath);
            if (instance.Scene.isLoaded) return;

            var completion = new UniTaskCompletionSource();
            ActivatingSceneMap.Add(scenePath, completion);
            Exception failure = null;
            try
            {
                var operation = instance.ActivateAsync();
                if (operation == null) throw new InvalidOperationException("场景未返回有效的激活操作：" + scenePath);
                await operation.ToUniTask();
                if (!instance.Scene.IsValid() || !instance.Scene.isLoaded)
                    throw new InvalidOperationException("场景激活后仍未加载完成：" + scenePath);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                ActivatingSceneMap.Remove(scenePath);
                Debug.Assert(!ActivatingSceneMap.ContainsKey(scenePath), "场景激活结束后仍存在激活标记。");
                if (failure == null) completion.TrySetResult();
                else completion.TrySetException(failure);
            }
            await completion.Task;
        }

        /// <summary>按实际 Scene 激活已保存的 SceneInstance，不依赖场景短名称。</summary>
        public static async UniTask ActivateSceneAsync(Scene scene)
        {
            if (!scene.IsValid()) throw new ArgumentException("场景无效，不能激活。", nameof(scene));
            var entry = LoadedSceneMap.FirstOrDefault(pair => pair.Value.Scene == scene);
            if (entry.Key == null) throw new InvalidOperationException("场景未由 HMAddressableManager 加载，不能激活：" + scene.path);
            await ActivateSceneAsync(entry.Key);
        }

        /// <summary>
        /// 按加载地址卸载，并发卸载共用同一次操作；失败会传给调用方。未激活、正在激活或最后一个已加载场景会被拒绝卸载。
        /// </summary>
        public static async UniTask UnloadSceneAsync(string scenePath)
        {
            scenePath = NormalizePath(scenePath);
            if (string.IsNullOrEmpty(scenePath)) return;
            if (LoadingSceneMap.TryGetValue(scenePath, out var loading)) await loading.Task;
            if (UnloadingSceneMap.TryGetValue(scenePath, out var pending))
            {
                await pending.Task;
                return;
            }
            if (!LoadedSceneMap.TryGetValue(scenePath, out var instance)) return;
            if (!instance.Scene.IsValid())
            {
                LoadedSceneMap.Remove(scenePath);
                return;
            }

            var completion = new UniTaskCompletionSource();
            UnloadingSceneMap.Add(scenePath, completion);
            AsyncOperationHandle<SceneInstance> operation = default;
            Exception failure = null;
            try
            {
                if (ActivatingSceneMap.ContainsKey(scenePath))
                    throw new InvalidOperationException("场景正在激活，请等待 ActivateSceneAsync 完成后再卸载：" + scenePath);
                if (!instance.Scene.isLoaded)
                    throw new InvalidOperationException("场景尚未激活，不能直接卸载：" + scenePath);
                if (!HasOtherLoadedScene(instance.Scene))
                    throw new InvalidOperationException("不能卸载最后一个已加载场景，请先加载其他场景：" + scenePath);
                operation = Addressables.UnloadSceneAsync(instance, UnloadSceneOptions.UnloadAllEmbeddedSceneObjects, false);
                await operation.ToUniTask();
                if (instance.Scene.IsValid() && instance.Scene.isLoaded)
                    throw new InvalidOperationException("场景卸载操作结束，但场景仍处于已加载状态：" + scenePath);
                LoadedSceneMap.Remove(scenePath);
                Debug.Assert(!LoadedSceneMap.ContainsKey(scenePath), "场景卸载成功后仍存在场景记录。");
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                // SceneProvider 会释放加载句柄，此处只释放卸载操作。
                if (operation.IsValid()) Addressables.Release(operation);
                UnloadingSceneMap.Remove(scenePath);
                if (failure == null) completion.TrySetResult();
                else completion.TrySetException(failure);
            }
            await completion.Task;
        }

        private static bool HasOtherLoadedScene(Scene targetScene)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded || scene == targetScene) continue;
                // 已开始卸载的场景不能作为另一个并发卸载请求的保留场景。
                if (LoadedSceneMap.Any(pair => pair.Value.Scene == scene && UnloadingSceneMap.ContainsKey(pair.Key))) continue;
                return true;
            }
            return false;
        }

        /// <summary>按实际 Scene 匹配保存的 SceneInstance，不依赖场景短名称。</summary>
        public static async UniTask UnloadSceneAsync(Scene scene)
        {
            if (!scene.IsValid()) return;
            var entry = LoadedSceneMap.FirstOrDefault(pair => pair.Value.Scene == scene);
            if (entry.Key != null) await UnloadSceneAsync(entry.Key);
        }

#if UNITY_EDITOR
        private static Scene GetSceneByPathOrName(string path)
        {
            var scene = SceneManager.GetSceneByPath(path);
            return scene.IsValid() ? scene : SceneManager.GetSceneByName(Path.GetFileNameWithoutExtension(path));
        }
#endif



        private static List<string> GetAllResouceAssetsByFolder<T>(string folderPath) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folderPath });
                List<string> list = new List<string>();
                for (int i = 0; i < guids.Length; i++)
                {
                    list.Add(AssetDatabase.GUIDToAssetPath(guids[i]));
                }

                return list;
            }
            else
            {
                return null;
            }
#endif
            return null;
        }

        #region ============================资源更新相关==================================================================

        /// <summary>
        /// 设置是否需要额外的Debug信息
        /// </summary>
        public static bool BeOtherDebug = true;

        /// <summary>
        /// 整体更新的状态
        /// </summary>
        private static AsyncOperationStatus _updateStatus = AsyncOperationStatus.Succeeded;

        /// <summary>
        /// 整体更新的信息
        /// </summary>
        private static string _resultMessage;

        private static float _progressValue;
        private static long _totalDownloadSize;
        private static UpdateStatusCode _updateStatusCode;

        /// <summary>
        /// 整体更新的回调
        /// </summary>
        private static UnityAction<AsyncOperationStatus, float, string, UpdateStatusCode> _updateCb;


        private static List<string> _needUpdateCatalogs;
        static readonly List<object> NeedUpdateKeys = new List<object>();

        private static bool _isUpdating;

        /// <summary>
        /// 更新所有资源。重复调用会报错；回调仅属于本次更新，回调异常不会中断下载。
        /// </summary>
        public static async UniTask UpdateAddressablesAllAssets(
            UnityAction<AsyncOperationStatus, float, string, UpdateStatusCode> updateCb)
        {
            if (_isUpdating)
                throw new InvalidOperationException("正在更新所有资源，请等待当前更新结束后再调用。");

            _isUpdating = true;
            _updateCb = updateCb;
            _updateStatus = AsyncOperationStatus.None;
            _updateStatusCode = UpdateStatusCode.CHECKING_RESOURCE_LIST;
            _resultMessage = string.Empty;
            _progressValue = 0;
            _totalDownloadSize = 0;
            _needUpdateCatalogs = null;
            NeedUpdateKeys.Clear();
            try
            {
#if UNITY_EDITOR
                _updateStatus = AsyncOperationStatus.Succeeded;
                _updateStatusCode = UpdateStatusCode.NO_UPDATES_NEEDED;
#else
                var platformConfig = HMAddressablePlatformConfig;
                var config = platformConfig == null ? null : platformConfig.CurrentConfig;
                if (config == null) throw new InvalidOperationException("缺少当前平台的 HMAddressables 配置，不能检查热更。");
                if (config.BeSkipUpdateCheck)
                {
                    _updateStatus = AsyncOperationStatus.Succeeded;
                    _updateStatusCode = UpdateStatusCode.NO_UPDATES_NEEDED;
                }
                else
                {
                    await UpdateWork();
                }
#endif
                if (_updateStatus == AsyncOperationStatus.Succeeded)
                {
                    _progressValue = 1;
                    PlayerPrefs.SetString(PrefsName, string.Empty);
                }
            }
            catch (Exception exception)
            {
                _updateStatus = AsyncOperationStatus.Failed;
                _resultMessage = exception.Message;
                switch (_updateStatusCode)
                {
                    case UpdateStatusCode.CHECKING_CONTENT_OF_UPDATING_RESOURCES:
                        _updateStatusCode = UpdateStatusCode.ERROR_UPDATING_RESOURCE_LIST;
                        break;
                    case UpdateStatusCode.GETTING_TOTAL_SIZE_OF_RESOURCE_FILES:
                        _updateStatusCode = UpdateStatusCode.ERROR_GETTING_RESOURCE_FILE_SIZE;
                        break;
                    case UpdateStatusCode.DOWNLOADING_RESOURCES:
                        _updateStatusCode = UpdateStatusCode.ERROR_DOWNLOADING_RESOURCES;
                        break;
                    default:
                        _updateStatusCode = UpdateStatusCode.ERROR_CHECKING_RESOURCE_LIST;
                        break;
                }
                Debug.LogException(exception);
            }
            finally
            {
                try
                {
                    DispatchUpdateCallback();
                }
                finally
                {
                    _updateCb = null;
                    _needUpdateCatalogs = null;
                    NeedUpdateKeys.Clear();
                    _isUpdating = false;
                    Debug.Assert(_updateStatus != AsyncOperationStatus.None, "资源更新结束后仍处于更新中。");
                }
            }
        }

        private static async UniTask UpdateWork()
        {
            // 原生初始化可能复用其他调用方的句柄，只释放本次 Acquire 的引用。
            var initialization = Addressables.ResourceManager.Acquire(Addressables.InitializeAsync());
            try
            {
                var locator = await initialization.ToUniTask();
                if (locator == null)
                    throw new InvalidOperationException("Addressables 初始化未返回有效 Catalog，请检查初始化错误，必要时重启客户端后重试。");
            }
            finally
            {
                if (initialization.IsValid()) Addressables.Release(initialization);
            }

            await CheckUpdateMainCatalog();
            await CheckNeedUpdateRecourceLocators();
            if (_updateStatus != AsyncOperationStatus.None) return;
            await CheckNeedDownloadSize();
            if (_updateStatus != AsyncOperationStatus.None) return;
            await DownLoadAssets();
        }

        private static async UniTask CheckUpdateMainCatalog()
        {
            _updateStatusCode = UpdateStatusCode.CHECKING_RESOURCE_LIST;
            var operation = Addressables.CheckForCatalogUpdates(false);
            try
            {
                while (!operation.IsDone)
                {
                    DispatchUpdateCallback();
                    await UniTask.NextFrame();
                }
                if (operation.Status != AsyncOperationStatus.Succeeded)
                    throw operation.OperationException ?? new InvalidOperationException("检查 Catalog 更新失败。");
                _needUpdateCatalogs = operation.Result == null ? new List<string>() : operation.Result.Distinct().ToList();
            }
            finally
            {
                if (operation.IsValid()) Addressables.Release(operation);
            }
        }

        private static async UniTask CheckNeedUpdateRecourceLocators()
        {
            _updateStatusCode = UpdateStatusCode.CHECKING_CONTENT_OF_UPDATING_RESOURCES;
            if (_needUpdateCatalogs != null && _needUpdateCatalogs.Count > 0)
            {
                var operation = Addressables.UpdateCatalogs(_needUpdateCatalogs, false);
                try
                {
                    while (!operation.IsDone)
                    {
                        DispatchUpdateCallback();
                        await UniTask.NextFrame();
                    }
                    if (operation.Status != AsyncOperationStatus.Succeeded)
                        throw operation.OperationException ?? new InvalidOperationException("更新 Catalog 失败。");
                }
                finally
                {
                    if (operation.IsValid()) Addressables.Release(operation);
                }
            }

            // Catalog 已更新但资源下载中断时，下次仍须检查当前 Catalog 中缺少的资源。
            var keys = new HashSet<object>();
            foreach (var locator in Addressables.ResourceLocators)
            {
                var locatorKeys = locator.Keys;
                if (locatorKeys == null) continue;
                foreach (var key in locatorKeys)
                {
                    if (key != null) keys.Add(key);
                }
            }
            NeedUpdateKeys.Clear();
            NeedUpdateKeys.AddRange(keys);
            if (NeedUpdateKeys.Count == 0)
            {
                _updateStatus = AsyncOperationStatus.Succeeded;
                _updateStatusCode = UpdateStatusCode.NO_UPDATES_NEEDED;
            }
        }

        private static async UniTask CheckNeedDownloadSize()
        {
            _updateStatusCode = UpdateStatusCode.GETTING_TOTAL_SIZE_OF_RESOURCE_FILES;
            var operation = Addressables.GetDownloadSizeAsync(NeedUpdateKeys);
            try
            {
                while (!operation.IsDone)
                {
                    DispatchUpdateCallback();
                    await UniTask.NextFrame();
                }
                if (operation.Status != AsyncOperationStatus.Succeeded)
                    throw operation.OperationException ?? new InvalidOperationException("获取资源下载大小失败。");
                _totalDownloadSize = operation.Result;
                if (_totalDownloadSize <= 0)
                {
                    _updateStatus = AsyncOperationStatus.Succeeded;
                    _resultMessage = "0";
                    _updateStatusCode = UpdateStatusCode.DETECTED_SIZE_OF_RESOURCES_TO_DOWNLOAD;
                }
            }
            finally
            {
                if (operation.IsValid()) Addressables.Release(operation);
            }
        }

        private static async UniTask DownLoadAssets()
        {
            _updateStatusCode = UpdateStatusCode.DOWNLOADING_RESOURCES;
            var operation = Addressables.DownloadDependenciesAsync(NeedUpdateKeys, Addressables.MergeMode.Union, false);
            try
            {
                while (!operation.IsDone)
                {
                    var download = operation.GetDownloadStatus();
                    _progressValue = download.Percent;
                    _resultMessage = $"{download.DownloadedBytes}/{_totalDownloadSize}";
                    DispatchUpdateCallback();
                    await UniTask.NextFrame();
                }
                if (operation.Status != AsyncOperationStatus.Succeeded)
                    throw operation.OperationException ?? new InvalidOperationException("下载更新资源失败。");
                _updateStatus = AsyncOperationStatus.Succeeded;
                _resultMessage = string.Empty;
                _updateStatusCode = UpdateStatusCode.FINISHED_DOWNLOADING_RESOURCES;
            }
            finally
            {
                if (operation.IsValid()) Addressables.Release(operation);
            }
        }

        private static void DispatchUpdateCallback()
        {
            var callbacks = _updateCb;
            if (callbacks == null) return;
            foreach (UnityAction<AsyncOperationStatus, float, string, UpdateStatusCode> callback in callbacks.GetInvocationList())
            {
                try { callback(_updateStatus, _progressValue, _resultMessage, _updateStatusCode); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }


        public static string ListToStr(List<string> listStr)
        {
            if (listStr.Count <= 0)
            {
                return "";
            }

            StringBuilder a = new StringBuilder();
            a.Append(listStr[0]);
            for (int i = 1; i < listStr.Count; i++)
            {
                a.Append("|");
                a.Append(listStr[i]);
            }

            return a.ToString();
        }

        public static List<string> StrToList(string str)
        {
            if (string.IsNullOrEmpty(str)) return new List<string>();
            var listStr = str.Split('|');
            return listStr.ToList();
        }

        #endregion

        /// <summary>清理之前版本的缓存资源</summary>
        public static void ClearLastVerCacheRes()
        {
            var key = $"HMAddressableResVer{Application.version}";
            if (PlayerPrefs.GetInt(key, 0) != 1)
            {
                var path = HMAA_AssetBundleResource.GetEncryptedCachePath();
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true); //删除目录
                    Debug.Log("删除之前的资源缓存");
                }

                PlayerPrefs.SetInt(key, 1);
            }
        }

        /// <summary>
        /// 下载一些资源到本地缓存,但不加载,如果已经下载过则不会重复下载
        /// 整个组的资源都会下载
        /// 所有地址必须有效；输入列表会在等待前复制，失败仍通过返回消息报告。
        /// </summary>
        /// <param name="assetsNames"></param>
        /// <param name="progressCb"></param>
        /// <returns></returns>
        public static async UniTask<(List<string> assetsNames, string message)> DownloadAssetsToLocal(
            List<string> assetsNames, UnityAction<long, long, float> progressCb = null)
        {
            AsyncOperationHandle downloadOp = default;
            try
            {
                if (assetsNames == null) throw new ArgumentNullException(nameof(assetsNames), "下载资源列表不能为空。");
                if (assetsNames.Count == 0) throw new ArgumentException("下载资源列表不能是空列表。", nameof(assetsNames));
                assetsNames = new List<string>(assetsNames);
                foreach (var assetName in assetsNames)
                {
                    if (string.IsNullOrWhiteSpace(assetName))
                        throw new ArgumentException("下载资源列表中存在空地址。", nameof(assetsNames));
                    var locationsOp = Addressables.LoadResourceLocationsAsync(assetName);
                    try
                    {
                        var locations = await locationsOp.ToUniTask();
                        if (locations == null || locations.Count == 0)
                            throw new InvalidOperationException("找不到需要下载的资源地址：" + assetName);
                    }
                    finally
                    {
                        if (locationsOp.IsValid()) Addressables.Release(locationsOp);
                    }
                }
                Debug.Assert(assetsNames.All(name => !string.IsNullOrWhiteSpace(name)), "下载前必须校验所有资源地址。");
                downloadOp = Addressables.DownloadDependenciesAsync(assetsNames, Addressables.MergeMode.Union, false);
                while (!downloadOp.IsDone)
                {
                    var download = downloadOp.GetDownloadStatus();
                    try { progressCb?.Invoke(download.DownloadedBytes, download.TotalBytes, download.Percent); }
                    catch (Exception callbackException) { Debug.LogException(callbackException); }
                    await UniTask.DelayFrame(10);
                }
                if (downloadOp.Status != AsyncOperationStatus.Succeeded)
                    throw downloadOp.OperationException ?? new InvalidOperationException("下载资源组失败。");
                return (assetsNames, "Succeeded");
            }
            catch (Exception exception)
            {
                Debug.LogError("下载资源组失败：" + exception.Message);
                return (assetsNames, "Download Failed:" + exception.Message);
            }
            finally
            {
                if (downloadOp.IsValid()) Addressables.Release(downloadOp);
            }
        }
    }
}
