using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Events;
using UnityEngine.AddressableAssets.ResourceLocators;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

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
        private const string PrefsName = "ADDRESSABLES_NEEDUPDATE";

        static readonly Dictionary<string, Object> ResMap =
            new Dictionary<string, Object>();

        private static readonly Dictionary<string, bool> LoadingMap = new Dictionary<string, bool>();

        private static Dictionary<string, SceneInstance> LoadedSceneMap = new Dictionary<string, SceneInstance>();
        private static Dictionary<string, bool> LoadingSceneMap = new Dictionary<string, bool>();

        /// <summary>
        /// 加载资源 同步加载,尽量不要使用
        /// </summary>
        /// <param name="resName"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T Load<T>(string resName) where T : UnityEngine.Object
        {
            if (ResMap.ContainsKey(resName))
            {
                return ResMap[resName] as T;
            }

            // if (BeOtherDebug)
            // {
            //     Debug.Log($"准备直接加载资源:{resName} ");
            // }

            T obj=null;

#if UNITY_EDITOR
            if (!HasAssets(resName))
            {
                //编辑器下判断有没有,没有就直接读目录,有就从aa走
                obj = AssetDatabase.LoadAssetAtPath<T>(resName);
            }
#endif

            if (obj == null)
            {
                var operation = Addressables.LoadAssetAsync<T>(resName);
                operation.WaitForCompletion();
                obj = operation.Result;
            }
            ResMap.Add(resName, obj);
            RemoveFromLoadingMap(resName);
            return ResMap[resName] as T;
        }

        public static async UniTask<T> LoadAsync<T>(string resName) where T : UnityEngine.Object
        {
            if (ResMap.ContainsKey(resName))
                return ResMap[resName] as T;
            if (LoadingMap.ContainsKey(resName) && LoadingMap[resName])
            {
                await UniTask.WaitUntil(() => !LoadingMap[resName]);

                return ResMap[resName] as T;
            }

            // if (BeOtherDebug)
            // {
            //     Debug.Log($"准备异步加载资源:{resName} ");
            // }
            
            T obj=null;
#if UNITY_EDITOR
            if (!HasAssets(resName))
            {
                //编辑器下判断有没有,没有就直接读目录,有就从aa走
                obj = AssetDatabase.LoadAssetAtPath<T>(resName);
               
            }
#endif
            if (obj == null)
            {
                var operation = Addressables.LoadAssetAsync<T>(resName);
                AddToLoadingMap(resName);
                await operation.Task;
                obj = operation.Result;
            }
            

            ResMap.Add(resName, obj);
            RemoveFromLoadingMap(resName);
            return ResMap[resName] as T;
        }
        /// <summary>
        /// 加载多个资源
        /// </summary>
        /// <param name="resNames"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static async UniTask<List<T>> loadAssetsAsync<T>(List<string> resNames) where T : UnityEngine.Object
        {
            List<UniTask<T>> uniTasks = new List<UniTask<T>>(resNames.Count);
            var lists = new List<T>(resNames.Count);
            for (int i = 0; i < resNames.Count; i++)
            {
                uniTasks.Add(  LoadAsync<T>(resNames[i]));
                lists.Add(null);
            }
            bool beWaite = true;
            //等待加载完毕
            while (beWaite)
            {
                bool allOver = true;
                for (int i = 0; i < uniTasks.Count; i++)
                {
                    if (uniTasks[i].Status == UniTaskStatus.Pending)
                    {
                        allOver = false;
                        break;
                    }
                    else
                    {
                        lists[i] = uniTasks[i].GetAwaiter().GetResult();
                    }
                }

                if (!allOver)
                {
                    await UniTask.Yield();//还在执行就等待一帧
                }
                else
                {
                    beWaite = false;
                }
            }


            return lists;

        }

        /// <summary>
        /// 判断是否有资源
        /// </summary>
        /// <param name="resName"></param>
        /// <returns></returns>
        public static bool HasAssets(string resName)
        {
#if UNITY_EDITOR
            //编辑器下检查是不是有设置,没有就直接返回,有就真的判断是不是存在资源
          var sett=  AssetDatabase.LoadAssetAtPath<Object>("Assets/AddressableAssetsData/AddressableAssetSettings.asset");
          if (sett == null)
          {
              return false;
          }
#endif
            
            var rs = Addressables.LoadResourceLocationsAsync(resName);
            
            rs.WaitForCompletion();
            if (rs.IsDone && rs.IsValid())
            {
                return (rs.Result != null && rs.Result.Count > 0);
            }

            return false;
        }

        /// <summary>
        /// 释放资源,
        /// </summary>
        /// <param name="res"></param>
        /// <returns></returns>
        public static bool ReleaseRes(Object res)
        {
            var item = ResMap.First((item) => item.Value == res);
            if (item.Key == null) return false;
            var operation = ResMap[item.Key];
            ResMap.Remove(item.Key);
            if (BeOtherDebug)
            {
                Debug.Log($"释放资源:{operation.name}");
            }
#if UNITY_EDITOR
            if (!HasAssets(operation.name))
            {
                return true;
            }
#endif
            Addressables.Release(operation);

            return true;
        }

        /// <summary>
        /// 释放资源,
        /// </summary>
        /// <param name="resName"></param>
        /// <returns></returns>
        public static async void ReleaseRes(string resName)
        {
            if (ResMap.ContainsKey(resName))
            {
                var operation = ResMap[resName];
                ResMap.Remove(resName);
                if (operation != null)
                {
                    // if (BeOtherDebug)
                    // {
                    //     Debug.Log($"释放资源:{resName}");
                    // }
#if UNITY_EDITOR
                    if (!HasAssets(resName))
                    {
                        return;
                    }
#endif
                    Addressables.Release(operation);
                }
            }
            //正在加载中,就等加载完毕后再释放
            else if (LoadingMap.ContainsKey(resName) && LoadingMap[resName])
            {
                await UniTask.WaitUntil(() => !LoadingMap[resName]);
                ReleaseRes(resName);
            }
        }
        
        /// <summary>
        /// 释放多个资源
        /// </summary>
        /// <param name="resNames"></param>
        public static  void ReleaseRes(List<string> resNames)
        {
            for (int i = 0; i < resNames.Count; i++)
            {
                ReleaseRes(resNames[i]);
            }
        }
        
        /// <summary>
        /// 异步加载场景,如果要对加载完毕的场景做操作,请用await写
        /// 如果要手动释放,请保留好这个SceneInstance释放的时候需要它
        /// </summary>
        /// <param name="sceneName"></param>
        /// <param name="loadSceneMode"></param>
        /// <param name="activeteOnLoad"></param>
        /// <returns></returns>
        public static async UniTask<Scene> LoadSceneAsync(string sceneName,
            LoadSceneMode loadSceneMode = LoadSceneMode.Single, bool activeteOnLoad = true)
        {
            if (LoadedSceneMap.ContainsKey(sceneName) && LoadedSceneMap[sceneName].Scene.isLoaded)
            {
                return LoadedSceneMap[sceneName].Scene;
            }

            //正在加载中就等待
            if (LoadingSceneMap.ContainsKey(sceneName) && LoadingSceneMap[sceneName])
            {
                await UniTask.WaitUntil(() => !LoadingSceneMap[sceneName] && LoadedSceneMap[sceneName].Scene.isLoaded);
                return LoadedSceneMap[sceneName].Scene;
            }


           
#if UNITY_EDITOR
            if (!HasAssets(sceneName))
            {
              var scene=  EditorSceneManager.LoadSceneInPlayMode(sceneName,new LoadSceneParameters());
              return scene;
            }

#endif
            
            var op = Addressables.LoadSceneAsync(sceneName, loadSceneMode, activeteOnLoad);
            AddToLoadingSceneMap(sceneName);
            await op.Task;
            if (!LoadedSceneMap.ContainsKey(sceneName))
            {
                LoadedSceneMap.Add(sceneName, op.Result);
            }
            else
            {
                LoadedSceneMap[sceneName] = op.Result;
            }

            RemoveFromLoadingSceneMap(sceneName);
            return LoadedSceneMap[sceneName].Scene;
        }

        /// <summary>
        /// 同步加载场景
        /// 如果要手动释放,请保留好这个SceneInstance释放的时候需要它
        /// </summary>
        /// <param name="sceneName"></param>
        /// <param name="loadSceneMode"></param>
        /// <param name="activeteOnLoad"></param>
        /// <returns></returns>
        public static Scene LoadScene(string sceneName, LoadSceneMode loadSceneMode = LoadSceneMode.Single,
            bool activeteOnLoad = true)
        {
            if (LoadedSceneMap.ContainsKey(sceneName) && LoadedSceneMap[sceneName].Scene.isLoaded)
            {
                return LoadedSceneMap[sceneName].Scene;
            }
            
#if UNITY_EDITOR
            if (!HasAssets(sceneName))
            {
                var scene = EditorSceneManager.LoadSceneInPlayMode(sceneName, new LoadSceneParameters(loadSceneMode));
                return scene;
            }
#endif
            


            var op = Addressables.LoadSceneAsync(sceneName, loadSceneMode, activeteOnLoad);
            op.WaitForCompletion();
            if (!LoadedSceneMap.ContainsKey(sceneName))
            {
                LoadedSceneMap.Add(sceneName, op.Result);
            }
            else
            {
                LoadedSceneMap[sceneName] = op.Result;
            }

            RemoveFromLoadingSceneMap(sceneName);
            return LoadedSceneMap[sceneName].Scene;
        }

        /// <summary>
        /// 释放场景
        /// </summary>
        /// <param name="scenePath"></param>
        public static async UniTask UnloadSceneAsync(string scenePath)
        {
#if UNITY_EDITOR
            if (!HasAssets(scenePath))
            {
               
                return;
            }
#endif
            if (LoadedSceneMap.ContainsKey(scenePath) && LoadedSceneMap[scenePath].Scene.isLoaded)
            {
                var op = Addressables.UnloadSceneAsync(LoadedSceneMap[scenePath]);
                await op.Task;
                LoadedSceneMap.Remove(scenePath);
            }
            else if (LoadedSceneMap.ContainsKey(scenePath) && !LoadedSceneMap[scenePath].Scene.isLoaded)
            {
                LoadedSceneMap.Remove(scenePath);
            }
        }

        /// <summary>
        /// 释放场景
        /// </summary>
        /// <param name="scene"></param>
        public static async UniTask UnloadSceneAsync(Scene scene)
        {
#if UNITY_EDITOR
            if (!HasAssets(scene.name))
            {
               
                return;
            }
#endif
            await UnloadSceneAsync(scene.name);
        }

        private static void AddToLoadingMap(string res)
        {
            if (!LoadingMap.ContainsKey(res))
            {
                LoadingMap.Add(res, true);
            }

            LoadingMap[res] = true;
        }

        private static void RemoveFromLoadingMap(string res)
        {
            if (LoadingMap.ContainsKey(res))
            {
                LoadingMap[res] = false;
            }
        }

        private static void AddToLoadingSceneMap(string res)
        {
            if (!LoadingSceneMap.ContainsKey(res))
            {
                LoadingSceneMap.Add(res, true);
            }

            LoadingSceneMap[res] = true;
        }

        private static void RemoveFromLoadingSceneMap(string res)
        {
            if (LoadingSceneMap.ContainsKey(res))
            {
                LoadingSceneMap[res] = false;
            }
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

        /// <summary>
        /// 整体更新的回调
        /// </summary>
        private static UnityAction<AsyncOperationStatus, float, string> _updateCb;


        private static List<string> _needUpdateCatalogs;
        static readonly List<object> NeedUpdateKeys = new List<object>();

        private static AsyncOperationHandle<List<string>> _checkMainCatalogOp;
        private static AsyncOperationHandle<List<IResourceLocator>> _updateCatalogsOp;
        private static AsyncOperationHandle<long> _sizeOpration;
        private static AsyncOperationHandle _downloadOp;

        ///  <summary>
        /// 更新所有资源
        ///  </summary>
        ///  <param name="updateCb"></param>
        public static async UniTask UpdateAddressablesAllAssets(
            UnityAction<AsyncOperationStatus, float, string> updateCb)
        {
            if (_updateStatus == AsyncOperationStatus.None)
            {
                UnityEngine.Debug.LogError("正在更新所有资源,不能重复调用更新");

                return; //正在更新
            }
            
            NeedUpdateKeys.Clear();
            _updateStatus = AsyncOperationStatus.None;
            _updateCb += updateCb;
            _progressValue = 0;
            
#if UNITY_EDITOR
            _updateStatus = AsyncOperationStatus.Succeeded;
            _resultMessage = "不需要更新资源";
            DispatchUpdateCallback();
            return;
            
#endif

          
            await UpdateWork();
        }


        static async UniTask UpdateWork()
        {
            Debug.Log("Addressable.InitializeAsync");

            var initializeAsync = Addressables.InitializeAsync();
            await initializeAsync.Task;
            var cache = Caching.currentCacheForWriting;
            //Debug.Log("下载缓存路径:" + cache.path);
            //Debug.Log("下载覆盖路径" + UnityEngine.Application.persistentDataPath);
            //Debug.Log("UpdateWork");
            await CheckUpdateMainCatalog();
            if (!CheckCanGoOn()) return;

            await CheckNeedUpdateRecourceLocators();
            if (!CheckCanGoOn()) return;

            await CheckNeedDownloadSize();
            if (!CheckCanGoOn()) return;

            await DownLoadAssets();
            if (!CheckCanGoOn()) return;
        }

        static async UniTask CheckUpdateMainCatalog()
        {
            //Debug.Log("CheckUpdateMainCatalog");
            //检查是否需要更新
            try
            {
                _checkMainCatalogOp = Addressables.CheckForCatalogUpdates(false);
            }
            catch
            {
                // ignored
            }


            while (!_checkMainCatalogOp.IsDone)
            {
                _resultMessage = "正在检查资源列表";
                DispatchUpdateCallback();
                await UniTask.Yield();
            }

            //Debug.Log("CheckUpdateMainCatalog = " + _CheckMainCatalogOp.Status);
            if (_checkMainCatalogOp.Status != AsyncOperationStatus.Succeeded)
            {
                _updateStatus = AsyncOperationStatus.Failed;
                _resultMessage = "检查资源列表时发生错误:" + _checkMainCatalogOp.OperationException.Message;
                Addressables.Release(_checkMainCatalogOp);
                return;
            }

            _needUpdateCatalogs = _checkMainCatalogOp.Result;
            var oldListStr = PlayerPrefs.GetString(PrefsName, ""); //取出旧的列表,避免升级中断导致的不再更新
            //Debug.LogFormat("oldListStr = {0}", oldListStr);
            if (!string.IsNullOrEmpty(oldListStr) && oldListStr.Length > 0)
            {
                try
                {
                    var oldList = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(oldListStr);
                    if (oldList != null)
                    {
                        for (var i = 0; i < oldList.Count; i++)
                        {
                            var str = oldList[i];
                            if (!_needUpdateCatalogs.Contains(str)) //没有包含的就一起添加进来
                            {
                                _needUpdateCatalogs.Add(str);
                            }
                        }
                    }
                }
                catch
                {
                    // ignored
                }
            }

            if (_needUpdateCatalogs.Count > 0)
            {
                var listS = Newtonsoft.Json.JsonConvert.SerializeObject(_needUpdateCatalogs);
                //Debug.LogFormat("newListStr = {0}", listS);
                //保存进去,结束后,如果成功就删除,没成功就等待下次重新更新
                PlayerPrefs.SetString(PrefsName, listS);
            }
            else
            {
                PlayerPrefs.SetString(PrefsName, "");
            }


            if (BeOtherDebug)
            {
                Debug.Log($"需要更新的MainCatalog数量:{_needUpdateCatalogs.Count} " +
                          $":{Newtonsoft.Json.JsonConvert.SerializeObject(_needUpdateCatalogs)}");
            }

            if (_needUpdateCatalogs.Count <= 0)
            {
                //Debug.Log("CheckUpdateMainCatalog: 不需要更新");
                _updateStatus = AsyncOperationStatus.Succeeded;
                _resultMessage = "不需要更新资源";
                _progressValue = 1;
                Addressables.Release(_checkMainCatalogOp);
                return;
            }

            Addressables.Release(_checkMainCatalogOp);
        }


        static async UniTask CheckNeedUpdateRecourceLocators()
        {
            //Debug.Log("CheckNeedUpdateRecourceLocators");
            try
            {
                _updateCatalogsOp = Addressables.UpdateCatalogs(_needUpdateCatalogs, false);
            }
            catch
            {
                // ignored
            }

            while (!_updateCatalogsOp.IsDone)
            {
                await UniTask.Yield();
                _resultMessage = "正在核对需要更新的资源内容";
                DispatchUpdateCallback();
            }

            Debug.LogFormat("CheckNeedUpdateRecourceLocators Status = {0}", _updateCatalogsOp.Status);
            if (_updateCatalogsOp.Status != AsyncOperationStatus.Succeeded)
            {
                _updateStatus = AsyncOperationStatus.Failed;
                _resultMessage = "更新资源列表时发生错误:" + _updateCatalogsOp.OperationException.Message;
                Addressables.Release(_updateCatalogsOp);
                return;
            }

            foreach (var item in _updateCatalogsOp.Result)
            {
                NeedUpdateKeys.AddRange(item.Keys);
            }

            if (BeOtherDebug)
            {
                Debug.Log(Newtonsoft.Json.JsonConvert.SerializeObject(NeedUpdateKeys));
                Debug.Log($"需要更新的资源数量:{NeedUpdateKeys.Count}");
            }

            if (NeedUpdateKeys.Count <= 0)
            {
                _updateStatus = AsyncOperationStatus.Succeeded;
                _resultMessage = "不需要更新资源";
                Addressables.Release(_updateCatalogsOp);
                return;
            }

            Addressables.Release(_updateCatalogsOp);
        }

        private static async UniTask CheckNeedDownloadSize()
        {
            //Debug.Log("CheckNeedDownloadSize");
            try
            {
                _sizeOpration = Addressables.GetDownloadSizeAsync(NeedUpdateKeys);
            }
            catch
            {
                // ignored
            }

            while (!_sizeOpration.IsDone)
            {
                await UniTask.Yield();
                _resultMessage = "正在获取资源文件总大小";
                DispatchUpdateCallback();
            }

            Debug.LogFormat("CheckNeedDownloadSize Status = {0}", _sizeOpration.Status);
            if (_sizeOpration.Status == AsyncOperationStatus.Failed)
            {
                _updateStatus = AsyncOperationStatus.Failed;
                _resultMessage = "获取资源文件大小时发生错误:" + _sizeOpration.OperationException.Message;
                Addressables.Release(_sizeOpration);
                return;
            }

            _totalDownloadSize = _sizeOpration.Result;
            if (BeOtherDebug)
            {
                Debug.Log($"需要更新的数据量:{_totalDownloadSize}");
            }

            if (_totalDownloadSize <= 0)
            {
                _updateStatus = AsyncOperationStatus.Succeeded;
                _resultMessage = "检查到需要下载的资源大小为" + _totalDownloadSize;
                Addressables.Release(_sizeOpration);
                return;
            }

            Addressables.Release(_sizeOpration);
        }


        static async UniTask DownLoadAssets()
        {
            //Debug.Log("DownLoadAssets");
            try
            {
                _downloadOp = Addressables.DownloadDependenciesAsync(NeedUpdateKeys, Addressables.MergeMode.Union);
            }
            catch
            {
                // ignored
            }

            Debug.Log("DownLoadAssets TotalSize = " + _downloadOp.GetDownloadStatus().TotalBytes);

            while (_downloadOp.Status == AsyncOperationStatus.None && !_downloadOp.IsDone)
            {
                await UniTask.Yield();
                _progressValue = _downloadOp.GetDownloadStatus().Percent;
                _resultMessage = $"下载资源中:{_downloadOp.GetDownloadStatus().DownloadedBytes}/{_totalDownloadSize}";
                DispatchUpdateCallback();
            }

            Debug.LogFormat("DownLoadAssets Status = {0}", _downloadOp.Status);
            if (BeOtherDebug)
            {
                Debug.Log($"下载资源结束:{_downloadOp.GetDownloadStatus().DownloadedBytes}/{_totalDownloadSize}");
            }

            switch (_downloadOp.Status)
            {
                case AsyncOperationStatus.Failed:
                    _updateStatus = AsyncOperationStatus.Failed;
                    _resultMessage = "下载资源时发生错误:" + _downloadOp.OperationException.Message;
                    break;
                case AsyncOperationStatus.Succeeded:
                    PlayerPrefs.SetString(PrefsName, ""); //更新成功了,清理掉所有需要更新的内容
                    _updateStatus = AsyncOperationStatus.Succeeded;
                    _resultMessage = "下载资源完成!";
                    break;
            }

            Addressables.Release(_downloadOp);
        }

        private static void DispatchUpdateCallback()
        {
            _updateCb?.Invoke(_updateStatus, _progressValue, _resultMessage);
        }


        private static bool CheckCanGoOn()
        {
            switch (_updateStatus)
            {
                case AsyncOperationStatus.Failed:
                    DispatchUpdateCallback();
                    return false;
                case AsyncOperationStatus.Succeeded:
                    DispatchUpdateCallback();
                    return false;
            }

            return true;
        }

        #endregion
    }
}