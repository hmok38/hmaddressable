using System;
using System.ComponentModel;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.Exceptions;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using AsyncOperation = UnityEngine.AsyncOperation;


namespace HM
{
 
    
    [DisplayName("HMAAEncrypt_AssetBundleProvider")]
    public class HMAAEncrypt_AssetBundleProvider : AssetBundleProvider
    {
        public IDataConverter DataStreamProcessor {get;set;}
        public override void Provide(ProvideHandle providerInterface)
        {
#if UNLOAD_BUNDLE_ASYNC
            if (m_UnloadingBundles.TryGetValue(providerInterface.Location.InternalId, out var unloadOp))
            {
                if (unloadOp.isDone)
                    unloadOp = null;
            }
            new AssetBundleResource().Start(providerInterface, unloadOp);
#else
            new HMAA_AssetBundleResource().Start(providerInterface,DataStreamProcessor);
#endif
        }
        
        public override Type GetDefaultType(IResourceLocation location)
        {
            return typeof(IAssetBundleResource);
        }
        
        public override bool Initialize(string id, string data)
        {
            if (!base.Initialize(id, data))
                return false;
            Debug.Log($"HMAAEncrypt_AssetBundleProvider Initialize:{data}");
            if (!string.IsNullOrEmpty(data))
            {
                var dsType = JsonUtility.FromJson<SerializedType>(data);
                if (dsType.Value != null)
                    DataStreamProcessor = Activator.CreateInstance(dsType.Value) as IDataConverter;
            }

            return true;
        }

        /// <summary>
        /// Releases the asset bundle via AssetBundle.Unload(true).
        /// </summary>
        /// <param name="location">The location of the asset to release</param>
        /// <param name="asset">The asset in question</param>
        public override void Release(IResourceLocation location, object asset)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            if (asset == null)
            {
                Debug.LogWarningFormat("Releasing null asset bundle from location {0}.  This is an indication that the bundle failed to load.", location);
                return;
            }

            var bundle = asset as HMAA_AssetBundleResource;
            if (bundle != null)
            {
#if UNLOAD_BUNDLE_ASYNC
                if (bundle.Unload(out var unloadOp))
                {
                    m_UnloadingBundles.Add(location.InternalId, unloadOp);
                    unloadOp.completed += op => m_UnloadingBundles.Remove(location.InternalId);
                }
#else
                bundle.Unload();
#endif
                return;
            }
        }
      
    }
    
  
     internal class HMAA_AssetBundleResource : IAssetBundleResource, IUpdateReceiver
    {
        internal enum LoadType
        {
            None,
            Local,
            Web,
            LocalDecrypt,
            LocalDecryptCache
        }

        AssetBundle m_AssetBundle;
        DownloadHandler m_downloadHandler;
        AsyncOperation m_RequestOperation;
        WebRequestQueueOperation m_WebRequestQueueOperation;
        internal ProvideHandle m_ProvideHandle;
        internal HMAA_AssetBundleRequestOptions m_Options;
        IDataConverter m_dataProc;
        [NonSerialized]
        bool m_WebRequestCompletedCallbackCalled = false;
        int m_Retries;
        long m_BytesToDownload;
        long m_DownloadedBytes;
        bool m_Completed = false;
        const int k_WaitForWebRequestMainThreadSleep = 1;
        string m_TransformedInternalId;
        AssetBundleRequest m_PreloadRequest;
        bool m_PreloadCompleted = false;
        ulong m_LastDownloadedByteCount = 0;
        float m_TimeoutTimer = 0;
        int m_TimeoutOverFrames = 0;

        private bool HasTimedOut => m_TimeoutTimer >= m_Options.Timeout && m_TimeoutOverFrames > 5;

        String m_InternalId;
        public static String GetEncryptedCachePath() {
            return Path.Combine(Application.persistentDataPath, "eb");
        }
        public static String GetEncryptedAssetLocalPath(String internalId, HMAA_AssetBundleRequestOptions options) {
            return Path.Combine(GetEncryptedCachePath(), internalId.GetHashCode().ToString() + "." + options == null ? "000" : options.Hash);
        }

        internal long BytesToDownload
        {
            get
            {
                if (m_BytesToDownload == -1)
                {
                    if (m_Options != null)
                        m_BytesToDownload = m_Options.ComputeSize(m_ProvideHandle.Location, m_ProvideHandle.ResourceManager);
                    else
                        m_BytesToDownload = 0;
                }
                return m_BytesToDownload;
            }
        }

        internal UnityWebRequest CreateWebRequest(IResourceLocation loc)
        {
            var url = m_ProvideHandle.ResourceManager.TransformInternalId(loc);
            return CreateWebRequest(url);
        }

        internal UnityWebRequest CreateWebRequest(string url)
        {
            UnityWebRequest webRequest = null;

            if (m_dataProc == null) {
                if (m_Options == null)
                    return UnityWebRequestAssetBundle.GetAssetBundle(url);
                
                if (!string.IsNullOrEmpty(m_Options.Hash))
                {
                    CachedAssetBundle cachedBundle = new CachedAssetBundle(m_Options.BundleName, Hash128.Parse(m_Options.Hash));
#if ENABLE_CACHING
                    if (m_Options.UseCrcForCachedBundle || !Caching.IsVersionCached(cachedBundle))
                        webRequest = UnityWebRequestAssetBundle.GetAssetBundle(url, cachedBundle, m_Options.Crc);
                    else
                        webRequest = UnityWebRequestAssetBundle.GetAssetBundle(url, cachedBundle);
#else
                    webRequest = UnityWebRequestAssetBundle.GetAssetBundle(url, cachedBundle, m_Options.Crc);
#endif
                }
                else
                    webRequest = UnityWebRequestAssetBundle.GetAssetBundle(url, m_Options.Crc);
            } else {
                webRequest = new UnityWebRequest(url);
                DownloadHandlerBuffer dH = new DownloadHandlerBuffer();
                webRequest.downloadHandler = dH;
            }

            if (webRequest == null)
                return webRequest;

            if (m_Options != null) {
                if (m_Options.Timeout > 0)
                    webRequest.timeout = m_Options.Timeout;
                if (m_Options.RedirectLimit > 0)
                    webRequest.redirectLimit = m_Options.RedirectLimit;
#if !UNITY_2019_3_OR_NEWER
                webRequest.chunkedTransfer = m_Options.ChunkedTransfer;
#endif
            }
            if (m_ProvideHandle.ResourceManager.CertificateHandlerInstance != null)
            {
                webRequest.certificateHandler = m_ProvideHandle.ResourceManager.CertificateHandlerInstance;
                webRequest.disposeCertificateHandlerOnDispose = false;
            }

            m_ProvideHandle.ResourceManager.WebRequestOverride?.Invoke(webRequest);
            return webRequest;
        }

        internal AssetBundleRequest GetAssetPreloadRequest()
        {
            if (m_PreloadCompleted || GetAssetBundle() == null)
                return null;

            if (m_Options.AssetLoadMode == AssetLoadMode.AllPackedAssetsAndDependencies)
            {
#if !UNITY_2021_1_OR_NEWER
                if (AsyncOperationHandle.IsWaitingForCompletion)
                {
                    m_AssetBundle.LoadAllAssets();
                    m_PreloadCompleted = true;
                    return null;
                }
#endif
                if (m_PreloadRequest == null)
                {
                    m_PreloadRequest = m_AssetBundle.LoadAllAssetsAsync();
                    m_PreloadRequest.completed += operation => m_PreloadCompleted = true;
                }
                return m_PreloadRequest;
            }

            return null;
        }

        float PercentComplete() { return m_RequestOperation != null ? m_RequestOperation.progress : 0.0f; }

        DownloadStatus GetDownloadStatus()
        {
            if (m_Options == null)
                return default;
            var status = new DownloadStatus() { TotalBytes = BytesToDownload, IsDone = PercentComplete() >= 1f };
            if (BytesToDownload > 0)
            {
                if (m_WebRequestQueueOperation != null && string.IsNullOrEmpty(m_WebRequestQueueOperation.WebRequest.error))
                    m_DownloadedBytes = (long)(m_WebRequestQueueOperation.WebRequest.downloadedBytes);
                else if (m_RequestOperation != null && m_RequestOperation is UnityWebRequestAsyncOperation operation && string.IsNullOrEmpty(operation.webRequest.error))
                    m_DownloadedBytes = (long)operation.webRequest.downloadedBytes;
            }

            status.DownloadedBytes = m_DownloadedBytes;
            return status;
        }

        /// <summary>
        /// Get the asset bundle object managed by this resource.  This call may force the bundle to load if not already loaded.
        /// </summary>
        /// <returns>The asset bundle.</returns>
        public AssetBundle GetAssetBundle()
        {
            if (m_AssetBundle == null)
            {
                if (m_downloadHandler != null)
                {
                    if (m_dataProc == null) {
                        m_AssetBundle = (m_downloadHandler as DownloadHandlerAssetBundle).assetBundle;;
                    } else {
                        var crc = m_Options == null ? 0 : m_Options.Crc;
                        var inputStream = new MemoryStream(m_downloadHandler.data, false);
                        String filePath = GetEncryptedAssetLocalPath(m_InternalId, m_Options);
                        saveDownloadBundle(inputStream, filePath);
                        inputStream.Seek(0, SeekOrigin.Begin);
                        var dataStream = m_dataProc.CreateReadStream(inputStream, m_InternalId);
                        if (dataStream.CanSeek)
                        {
                            m_AssetBundle = AssetBundle.LoadFromStream(dataStream, crc);
                        }
                        else
                        {
                            //Slow path needed if stream is not seekable
                            var memStream = new MemoryStream();
                            dataStream.CopyTo(memStream);
                            dataStream.Flush();
                            dataStream.Dispose();
                            inputStream.Dispose();
                            m_AssetBundle = AssetBundle.LoadFromStream(memStream, crc);
                        }
                    }
                    m_downloadHandler.Dispose();
                    m_downloadHandler = null;
                }
                else if (m_RequestOperation is AssetBundleCreateRequest)
                {
                    m_AssetBundle = (m_RequestOperation as AssetBundleCreateRequest).assetBundle;
                }
            }
            return m_AssetBundle;
        }

        void CopyStream(Stream input, Stream output)
        {
            byte[] buffer = new byte[8 * 1024];
            int len;
            while ( (len = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, len);
            }    
        }

        void saveDownloadBundle(Stream stream, string path)
        {
            //Create the Directory if it does not exist
            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }

            try
            {
                using (Stream file = File.Create(path))
                {
                    CopyStream(stream, file);
                    file.Flush();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Failed To Save Data to: " + path.Replace("/", "\\"));
                Debug.LogWarning("Error: " + e.Message);
            }
        }

        internal void Start(ProvideHandle provideHandle, IDataConverter dataProc)
        {
            m_dataProc = dataProc;
            m_Retries = 0;
            m_AssetBundle = null;
            m_downloadHandler = null;
            m_RequestOperation = null;
            m_WebRequestCompletedCallbackCalled = false;
            m_ProvideHandle = provideHandle;
            m_Options = m_ProvideHandle.Location.Data as HMAA_AssetBundleRequestOptions;
            m_BytesToDownload = -1;
            m_ProvideHandle.SetProgressCallback(PercentComplete);
            m_ProvideHandle.SetDownloadProgressCallbacks(GetDownloadStatus);
            m_ProvideHandle.SetWaitForCompletionCallback(WaitForCompletionHandler);
            BeginOperation();
        }

        private void LoadWithDataProc(String path, uint crc) {
            var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            var dataStream = m_dataProc.CreateReadStream(fileStream, m_ProvideHandle.Location.InternalId);
            if (dataStream.CanSeek)
            {
                m_RequestOperation = AssetBundle.LoadFromStreamAsync(dataStream, crc);
            }
            else
            {
                //Slow path needed if stream is not seekable
                var memStream = new MemoryStream();
                dataStream.CopyTo(memStream);
                dataStream.Flush();
                dataStream.Dispose();
                fileStream.Dispose();

                memStream.Position = 0;
                m_RequestOperation = AssetBundle.LoadFromStreamAsync(memStream, crc);
            }
        }

        private bool WaitForCompletionHandler()
        {
            if (m_RequestOperation == null)
                return false;

            //We don't want to wait for request op to complete if it's a LoadFromFileAsync. Only UWR will complete in a tight loop like this.
            if (!(m_RequestOperation is AssetBundleCreateRequest))
                while (!m_RequestOperation.isDone) { System.Threading.Thread.Sleep(k_WaitForWebRequestMainThreadSleep); }

            if (m_RequestOperation is UnityWebRequestAsyncOperation && !m_WebRequestCompletedCallbackCalled)
            {
                WebRequestOperationCompleted(m_RequestOperation);
                m_RequestOperation.completed -= WebRequestOperationCompleted;
            }

            var assetBundle = GetAssetBundle();
            if (!m_Completed && m_RequestOperation.isDone)
            {
                m_ProvideHandle.Complete(this, m_AssetBundle != null, null);
                m_Completed = true;
            }

            return m_Completed;
        }

        void AddCallbackInvokeIfDone(AsyncOperation operation, Action<AsyncOperation> callback)
        {
            if (operation.isDone)
                callback(operation);
            else
                operation.completed += callback;
        }
        
        internal static void GetLoadInfo(ProvideHandle handle, IDataConverter dataProc, out LoadType loadType, out string path)
        {
            GetLoadInfo(handle.Location, handle.ResourceManager, dataProc, out loadType, out path);
        }

        internal static void GetLoadInfo(IResourceLocation location, ResourceManager resourceManager, IDataConverter dataProc, out LoadType loadType, out string path)
        {
            var options = location?.Data as HMAA_AssetBundleRequestOptions;
            if (options == null)
            {
                loadType = LoadType.None;
                path = null;
                return;
            }

            path = resourceManager.TransformInternalId(location);
            if (Application.platform == RuntimePlatform.Android && path.StartsWith("jar:"))
            {
                loadType = options.UseUnityWebRequestForLocalBundles ? LoadType.Web : LoadType.Local;
                if (dataProc != null) {
                    // // if a path starts with jar:file, it is an android embeded resource. The resource is a local file but cannot be accessed by 
                    // FileStream(called in LoadWithDataProc) directly
                    // Need to use webrequest's async call to get the content.
                    loadType = LoadType.Web;
                }
            }
            else if (ResourceManagerConfig.ShouldPathUseWebRequest(path))
                loadType = LoadType.Web;
            else if (options.UseUnityWebRequestForLocalBundles)
            {
                path = "file:///" + Path.GetFullPath(path);
                loadType = LoadType.Web;
            }
            else
                loadType = LoadType.Local;

            if (dataProc != null && File.Exists(path) && loadType == LoadType.Local) {
                loadType = LoadType.LocalDecrypt;
            } else if (dataProc != null && File.Exists(GetEncryptedAssetLocalPath(path, options))) // cached local path
            {
                loadType = LoadType.LocalDecryptCache;
            }
        }

        private void BeginOperation()
        {

            m_DownloadedBytes = 0;
            GetLoadInfo(m_ProvideHandle, m_dataProc, out LoadType loadType, out m_TransformedInternalId);

            if (loadType == LoadType.Local)
            {
#if !UNITY_2021_1_OR_NEWER
                if (AsyncOperationHandle.IsWaitingForCompletion)
                    CompleteBundleLoad(AssetBundle.LoadFromFile(m_TransformedInternalId, m_Options == null ? 0 : m_Options.Crc));
                else
#endif
                {
                    m_RequestOperation = AssetBundle.LoadFromFileAsync(m_TransformedInternalId, m_Options == null ? 0 : m_Options.Crc);
                    AddCallbackInvokeIfDone(m_RequestOperation, LocalRequestOperationCompleted);
                }
            }
            else if (loadType == LoadType.Web)
            {
                m_WebRequestCompletedCallbackCalled = false;
                var req = CreateWebRequest(m_TransformedInternalId);
#if ENABLE_ASYNC_ASSETBUNDLE_UWR
                ((DownloadHandlerAssetBundle)req.downloadHandler).autoLoadAssetBundle = !(m_ProvideHandle.Location is DownloadOnlyLocation);
#endif
                req.disposeDownloadHandlerOnDispose = false;

                m_WebRequestQueueOperation = WebRequestQueue.QueueRequest(req);
                if (m_WebRequestQueueOperation.IsDone)
                    BeginWebRequestOperation(m_WebRequestQueueOperation.Result);
                else
                    m_WebRequestQueueOperation.OnComplete += asyncOp => BeginWebRequestOperation(asyncOp);
            }
            else if (loadType == LoadType.LocalDecrypt) {
                var crc = m_Options == null ? 0 : m_Options.Crc;
                LoadWithDataProc(m_TransformedInternalId, crc);
                AddCallbackInvokeIfDone(m_RequestOperation, LocalRequestOperationCompleted);
            }
            else if (loadType == LoadType.LocalDecryptCache) {
                var crc = m_Options == null ? 0 : m_Options.Crc;
                LoadWithDataProc(GetEncryptedAssetLocalPath(m_TransformedInternalId, m_Options), crc);
                AddCallbackInvokeIfDone(m_RequestOperation, LocalRequestOperationCompleted);
            }
            else
            {
                m_RequestOperation = null;
                m_ProvideHandle.Complete<HMAA_AssetBundleResource>(null, false, new RemoteProviderException(string.Format("Invalid path in AssetBundleProvider: '{0}'.", m_TransformedInternalId), m_ProvideHandle.Location));
                m_Completed = true;
            }
        }

        private void BeginWebRequestOperation(AsyncOperation asyncOp)
        {
            m_TimeoutTimer = 0;
            m_TimeoutOverFrames = 0;
            m_LastDownloadedByteCount = 0;
            m_RequestOperation = asyncOp;
            if (m_RequestOperation == null || m_RequestOperation.isDone)
                WebRequestOperationCompleted(m_RequestOperation);
            else
            {
                if (m_Options.Timeout > 0)
                    m_ProvideHandle.ResourceManager.AddUpdateReceiver(this);
                m_RequestOperation.completed += WebRequestOperationCompleted;
            }
        }
        
        public void Update(float unscaledDeltaTime)
        {
            if (m_RequestOperation != null && m_RequestOperation is UnityWebRequestAsyncOperation operation && !operation.isDone)
            {
                if (m_LastDownloadedByteCount != operation.webRequest.downloadedBytes)
                {
                    m_TimeoutTimer = 0;
                    m_TimeoutOverFrames = 0;
                    m_LastDownloadedByteCount = operation.webRequest.downloadedBytes;
                }
                else
                {
                    m_TimeoutTimer += unscaledDeltaTime;
                    if (HasTimedOut)
                        operation.webRequest.Abort();
                    m_TimeoutOverFrames++;
                }
            }
        }

        private void LocalRequestOperationCompleted(AsyncOperation op)
        {
            CompleteBundleLoad((op as AssetBundleCreateRequest).assetBundle);
        }

        private void CompleteBundleLoad(AssetBundle bundle)
        {
            m_AssetBundle = bundle;
            if (m_AssetBundle != null)
                m_ProvideHandle.Complete(this, true, null);
            else
                m_ProvideHandle.Complete<HMAA_AssetBundleResource>(null, false, new RemoteProviderException(string.Format("Invalid path in AssetBundleProvider: '{0}'.", m_TransformedInternalId), m_ProvideHandle.Location));
            m_Completed = true;
        }

        private void WebRequestOperationCompleted(AsyncOperation op)
        {
            if (m_WebRequestCompletedCallbackCalled)
                return;

            if (m_Options.Timeout > 0)
                m_ProvideHandle.ResourceManager.RemoveUpdateReciever(this);
            
            m_WebRequestCompletedCallbackCalled = true;
            UnityWebRequestAsyncOperation remoteReq = op as UnityWebRequestAsyncOperation;
            var webReq = remoteReq?.webRequest;
            m_downloadHandler = webReq?.downloadHandler as DownloadHandlerAssetBundle;
            UnityWebRequestResult uwrResult = null;
            if (webReq != null && !UnityWebRequestUtilities.RequestHasErrors(webReq, out uwrResult))
            {
                if (m_dataProc != null) {
                    // encrypt bundle, need to cache InternalId before complete
                    m_InternalId = m_ProvideHandle.Location.InternalId;
                }
                if (!m_Completed)
                {
                    m_downloadHandler = webReq.downloadHandler as DownloadHandlerAssetBundle;
                    // encrypted bundle will use DownloadHandlerBuffer
                    if (m_downloadHandler == null)
                    {
                        m_downloadHandler = webReq.downloadHandler as DownloadHandlerBuffer;
                    }
                    m_ProvideHandle.Complete(this, true, null);
                    m_Completed = true;
                }
                if (m_dataProc == null)
                {
                    m_downloadHandler = webReq.downloadHandler as DownloadHandlerAssetBundle;
                }
#if ENABLE_CACHING
                if (!string.IsNullOrEmpty(m_Options.Hash) && m_Options.ClearOtherCachedVersionsWhenLoaded)
                    Caching.ClearOtherCachedVersions(m_Options.BundleName, Hash128.Parse(m_Options.Hash));
#endif
            }
            else
            {
                if (HasTimedOut)
                    uwrResult.Error = "Request timeout";
                webReq = m_WebRequestQueueOperation.WebRequest;
                if (uwrResult == null)
                    uwrResult = new UnityWebRequestResult(m_WebRequestQueueOperation.WebRequest);

                m_downloadHandler = webReq.downloadHandler as DownloadHandlerAssetBundle;
                m_downloadHandler.Dispose();
                m_downloadHandler = null;
                bool forcedRetry = false;
                string message = $"Web request failed, retrying ({m_Retries}/{m_Options.RetryCount})...\n{uwrResult}";
#if ENABLE_CACHING
                if (!string.IsNullOrEmpty(m_Options.Hash))
                {
                    CachedAssetBundle cab = new CachedAssetBundle(m_Options.BundleName, Hash128.Parse(m_Options.Hash));
                    if (Caching.IsVersionCached(cab))
                    {
                        message = $"Web request failed to load from cache. The cached AssetBundle will be cleared from the cache and re-downloaded. Retrying...\n{uwrResult}";
                        Caching.ClearCachedVersion(cab.name, cab.hash);
                        if (m_Options.RetryCount == 0 && m_Retries == 0)
                        {
                            Debug.LogFormat(message);
                            BeginOperation();
                            m_Retries++; //Will prevent us from entering an infinite loop of retrying if retry count is 0
                            forcedRetry = true;
                        }
                    }
                }
#endif
                if (!forcedRetry)
                {
                    if (m_Retries < m_Options.RetryCount && uwrResult.Error != "Request aborted")
                    {
                        m_Retries++;
                        Debug.LogFormat(message);
                        BeginOperation();
                    }
                    else
                    {
                        var exception = new RemoteProviderException($"Unable to load asset bundle from : {webReq.url}", m_ProvideHandle.Location, uwrResult);
                        m_ProvideHandle.Complete<HMAA_AssetBundleResource>(null, false, exception);
                        m_Completed = true;
                    }
                }
            }
            webReq.Dispose();
        }

        /// <summary>
        /// Unloads all resources associated with this asset bundle.
        /// </summary>
        public void Unload()
        {
            if (m_AssetBundle != null)
            {
                m_AssetBundle.Unload(true);
                m_AssetBundle = null;
            }
            if (m_downloadHandler != null)
            {
                m_downloadHandler.Dispose();
                m_downloadHandler = null;
            }
            m_RequestOperation = null;
        }
    }

     /// <summary>
     /// Contains cache information to be used by the AssetBundleProvider
     /// </summary>
     [Serializable]
     public class HMAA_AssetBundleRequestOptions : AssetBundleRequestOptions
     {
         public override long ComputeSize(IResourceLocation location, ResourceManager resourceManager)
         {
             var id = resourceManager == null ? location.InternalId : resourceManager.TransformInternalId(location);
             if (!ResourceManagerConfig.IsPathRemote(id))
                 return 0;
             var locHash = Hash128.Parse(Hash);
#if ENABLE_CACHING
             HMAAEncrypt_AssetBundleProvider assetBundleProvider = resourceManager.GetResourceProvider(null, location) as HMAAEncrypt_AssetBundleProvider;
             if (assetBundleProvider != null & assetBundleProvider.DataStreamProcessor != null)
             {
                 // check encrypted bundle cache
                 if (File.Exists(HMAA_AssetBundleResource.GetEncryptedAssetLocalPath(id, this)))
                 {
                     return 0;
                 }
             }

             if (locHash.isValid) //If we have a hash, ensure that our desired version is cached.
             {
                 if (Caching.IsVersionCached(new CachedAssetBundle(BundleName, locHash)))
                     return 0;
                 return BundleSize;
             }
#endif //ENABLE_CACHING
             return BundleSize;
         }
         
     }
}
