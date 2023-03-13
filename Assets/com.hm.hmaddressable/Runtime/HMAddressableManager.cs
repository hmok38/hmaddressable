using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Events;
using UnityEngine.AddressableAssets.ResourceLocators;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace HM
{
    /// <summary>
    /// HMAddresablesAsset资源管理系统的运行时代码,可以通过它进行资源升级和加载
    /// </summary>
    public static class HMAddressableManager
    {
        private const string PrefsName = "ADDRESSABLES_NEEDUPDATE";

        static readonly Dictionary<string, AsyncOperationHandle> ResMap =
            new Dictionary<string, AsyncOperationHandle>();

        /// <summary>
        ///通过预制体名字实例化GameObject,注意:销毁时,请使用 DestroyGameObject()接口销毁
        /// </summary>
        /// <param name="prefabName"></param>
        /// <returns></returns>
        public static GameObject InstantiateGameObject(string prefabName)
        {
            var operation = Addressables.InstantiateAsync(prefabName);
            return operation.WaitForCompletion();
        }

        /// <summary>
        /// 销毁被Addressables管理系统实例化的GameObject,计数器会自动计数
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static bool DestroyGameObject(GameObject obj)
        {
            return Addressables.ReleaseInstance(obj);
        }

        /// <summary>
        /// 加载资源
        /// </summary>
        /// <param name="resName"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T Load<T>(string resName) where T : UnityEngine.Object
        {
            if (typeof(T) == typeof(Sprite))
            {
                return LoadSprite(resName) as T;
            }

            if (typeof(T) == typeof(Texture2D))
            {
                return LoadTexture2d(resName) as T;
            }

            if (ResMap.ContainsKey(resName)) return ResMap[resName].Result as T;
            var operation = Addressables.LoadAssetAsync<T>(resName);
            operation.WaitForCompletion();
            ResMap.Add(resName, operation);

            return ResMap[resName].Result as T;
        }

        /// <summary>
        /// 判断是否有资源
        /// </summary>
        /// <param name="resName"></param>
        /// <returns></returns>
        public static bool HasAssets(string resName)
        {
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
        public static bool ReleaseRes(object res)
        {
            if (res is Sprite || res is Texture2D)
            {
                return ReleaseTexture(res);
            }

            var item = ResMap.First((item) => item.Value.Result == res);
            if (item.Key == null) return false;
            var operation = ResMap[item.Key];
            ResMap.Remove(item.Key);
            Addressables.Release(operation);
            return true;
        }

        static bool ReleaseTexture(object res)
        {
            var name = "";
            if (res is Sprite spriteObj)
            {
                name = spriteObj.texture.name;
            }
            else if (res is Texture2D)
            {
                name = (res as Texture2D).name;
            }

            if (SpritesInTextureMap.ContainsKey(name))
            {
                SpritesInTextureMap.Remove(name);
            }

            if (!SpriteTextureOprationMap.ContainsKey(name)) return false;
            Addressables.Release(SpriteTextureOprationMap[name]);
            return true;
        }

        /// <summary>
        /// 因为Sprite是子资源,所以特殊处理
        /// </summary>
        /// <param name="resName"></param>
        /// <returns></returns>
        public static Sprite LoadSprite(string resName)
        {
            var nameBaoIndex = resName.IndexOf("[", StringComparison.Ordinal);
            var textureName = resName.Substring(0, nameBaoIndex);
            var spriteName = resName.Substring(nameBaoIndex + 1);
            spriteName = spriteName.Replace("]", "");
            var sprites = LoadAllSpriteByTexture(textureName);
            if (sprites != null && sprites.Length > 0)
            {
                return sprites.First(x => x.name == spriteName);
            }

            return null;
        }

        private static readonly Dictionary<string, AsyncOperationHandle<IList<Sprite>>>
            SpriteTextureOprationMap =
                new Dictionary<string,
                    AsyncOperationHandle<IList<Sprite>>>();

        static readonly Dictionary<string, Sprite[]> SpritesInTextureMap = new Dictionary<string, Sprite[]>();

        public static Sprite[] LoadAllSpriteByTexture(string texture2DName)
        {
            if (SpritesInTextureMap.ContainsKey(texture2DName))
            {
                return SpritesInTextureMap[texture2DName];
            }

            if (!SpriteTextureOprationMap.ContainsKey(texture2DName))
            {
                var a = Addressables.LoadAssetAsync<IList<Sprite>>(texture2DName);
                var res = a.WaitForCompletion();

                SpriteTextureOprationMap.Add(texture2DName, a);
            }

            if (SpriteTextureOprationMap[texture2DName].Result == null) return null;
            SpritesInTextureMap.Add(texture2DName, SpriteTextureOprationMap[texture2DName].Result.ToArray<Sprite>());
            return SpritesInTextureMap[texture2DName];
        }

        /// <summary>
        /// 加载贴图
        /// </summary>
        /// <param name="texture2DName"></param>
        /// <returns></returns>
        public static Texture2D LoadTexture2d(string texture2DName)
        {
            var sprites = LoadAllSpriteByTexture(texture2DName);
            if (sprites != null && sprites.Length > 0)
            {
                return sprites[0].texture;
            }

            return null;
        }

        public static void LoadSceneAsync(string sceneName)
        {
            Addressables.LoadSceneAsync(sceneName);
        }

        public static void UnloadSceneAsync(AsyncOperationHandle handle)
        {
            Addressables.UnloadSceneAsync(handle);
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
        public static void UpdateAddressablesAllAssets(UnityAction<AsyncOperationStatus, float, string> updateCb)
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
            UpdateWork();
        }


        static async void UpdateWork()
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