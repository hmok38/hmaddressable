using System;
using System.ComponentModel;
using System.Diagnostics;
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
using Debug = UnityEngine.Debug;

namespace HM
{
    [DisplayName("不加密")]
    public class HMAAEncrypt_AssetBundleProvider: AssetBundleProvider
    {
        public DataConverterBase DataStreamProcessor { get; set; }

        public override void Provide(ProvideHandle providerInterface)
        {
#if UNLOAD_BUNDLE_ASYNC
            if (m_UnloadingBundles.TryGetValue(providerInterface.Location.InternalId, out var unloadOp))
            {
                if (unloadOp.isDone)
                    unloadOp = null;
            }
            new AssetBundleResource().Start(providerInterface, unloadOp,DataStreamProcessor);
#else
            new HMAA_AssetBundleResource().Start(providerInterface, DataStreamProcessor);
#endif
        }

        public override Type GetDefaultType(IResourceLocation location)
        {
            return typeof (IAssetBundleResource);
        }

        public override bool Initialize(string id, string data)
        {
            if (!base.Initialize(id, data))
                return false;
            Debug.Log($"{this.GetType().Name} Initialized");

            var encrypyType = HMAddressablesConfig.GetEncrypyType(this.GetType());
            if (encrypyType!=null)
            {
                DataStreamProcessor=Activator.CreateInstance(encrypyType) as DataConverterBase;
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
                Debug.LogWarningFormat("Releasing null asset bundle from location {0}.  This is an indication that the bundle failed to load.",
                    location);
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

    internal class HMAA_AssetBundleResource: IAssetBundleResource, IUpdateReceiver
    {
        /// <summary>
        /// Options for where an AssetBundle can be loaded from.
        /// </summary>
        public enum LoadType
        {
            /// <summary>
            /// Cannot determine where the AssetBundle is located.
            /// </summary>
            None,

            /// <summary>
            /// Load the AssetBundle from a local file location.
            /// </summary>
            Local,

            /// <summary>
            /// Download the AssetBundle from a web server.
            /// </summary>
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

        DataConverterBase m_dataProc;

        [NonSerialized]
        bool m_WebRequestCompletedCallbackCalled = false;

        int m_Retries;
        bool m_IsLoadingFromCache;
        long m_BytesToDownload;
        long m_DownloadedBytes;
        bool m_Completed = false;
#if UNLOAD_BUNDLE_ASYNC
        AssetBundleUnloadOperation m_UnloadOperation;
#endif
        const int k_WaitForWebRequestMainThreadSleep = 1;
        string m_TransformedInternalId;
        AssetBundleRequest m_PreloadRequest;
        bool m_PreloadCompleted = false;
        ulong m_LastDownloadedByteCount = 0;
        float m_TimeoutTimer = 0;
        int m_TimeoutOverFrames = 0;

        private bool HasTimedOut => m_TimeoutTimer >= m_Options.Timeout && m_TimeoutOverFrames > 5;

        String m_InternalId;

        public static String GetEncryptedAssetLocalPath(String internalId, HMAA_AssetBundleRequestOptions options)
        {
            return Path.Combine(GetEncryptedCachePath(), internalId.GetHashCode().ToString() + "." + options == null? "000" : options.Hash);
        }

        public static String GetEncryptedCachePath()
        {
            return Path.Combine(Application.persistentDataPath, "eb");
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

            if (m_dataProc == null)
            {
                if (m_Options == null)
                    return UnityWebRequestAssetBundle.GetAssetBundle(url);
                if (!string.IsNullOrEmpty(m_Options.Hash))
                {
                    CachedAssetBundle cachedBundle = new CachedAssetBundle(m_Options.BundleName, Hash128.Parse(m_Options.Hash));
#if ENABLE_CACHING
                    m_IsLoadingFromCache = Caching.IsVersionCached(cachedBundle);
                    if (m_Options.UseCrcForCachedBundle || !m_IsLoadingFromCache)
                        webRequest = UnityWebRequestAssetBundle.GetAssetBundle(url, cachedBundle, m_Options.Crc);
                    else
                        webRequest = UnityWebRequestAssetBundle.GetAssetBundle(url, cachedBundle);
#else
                webRequest = UnityWebRequestAssetBundle.GetAssetBundle(url, cachedBundle, m_Options.Crc);
#endif
                }
                else
                {
                    m_IsLoadingFromCache = false;
                    webRequest = UnityWebRequestAssetBundle.GetAssetBundle(url, m_Options.Crc);
                }
            }
            else
            {
                webRequest = new UnityWebRequest(url);
                DownloadHandlerBuffer dH = new DownloadHandlerBuffer();
                webRequest.downloadHandler = dH;
            }

            if (webRequest == null)
                return webRequest;

            if (m_Options != null)
            {
                if (m_Options.Timeout > 0)
                    webRequest.timeout = m_Options.Timeout;
                if (m_Options.RedirectLimit > 0)
                    webRequest.redirectLimit = m_Options.RedirectLimit;
            }

            if (m_ProvideHandle.ResourceManager.CertificateHandlerInstance != null)
            {
                webRequest.certificateHandler = m_ProvideHandle.ResourceManager.CertificateHandlerInstance;
                webRequest.disposeCertificateHandlerOnDispose = false;
            }

            m_ProvideHandle.ResourceManager.WebRequestOverride?.Invoke(webRequest);
            return webRequest;
        }

        /// <summary>
        /// Creates a request for loading all assets from an AssetBundle.
        /// </summary>
        /// <returns>Returns the request.</returns>
        public AssetBundleRequest GetAssetPreloadRequest()
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

        float PercentComplete()
        {
            return m_RequestOperation != null? m_RequestOperation.progress : 0.0f;
        }

        DownloadStatus GetDownloadStatus()
        {
            if (m_Options == null)
                return default;
            var status = new DownloadStatus() { TotalBytes = BytesToDownload, IsDone = PercentComplete() >= 1f };
            if (BytesToDownload > 0)
            {
                if (m_WebRequestQueueOperation != null && string.IsNullOrEmpty(m_WebRequestQueueOperation.WebRequest.error))
                    m_DownloadedBytes = (long) (m_WebRequestQueueOperation.WebRequest.downloadedBytes);
                else if (m_RequestOperation != null && m_RequestOperation is UnityWebRequestAsyncOperation operation &&
                         string.IsNullOrEmpty(operation.webRequest.error))
                    m_DownloadedBytes = (long) operation.webRequest.downloadedBytes;
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
                    //------------------------------------------------------------------------
                    if (m_dataProc == null)
                    {
                        m_AssetBundle = (m_downloadHandler as DownloadHandlerAssetBundle).assetBundle;
                        ;
                    }
                    else
                    {
                        var crc = m_Options == null? 0 : m_Options.Crc;
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

#if UNLOAD_BUNDLE_ASYNC
        void OnUnloadOperationComplete(AsyncOperation op)
        {
            m_UnloadOperation = null;
            BeginOperation();
        }

#endif

#if UNLOAD_BUNDLE_ASYNC
        /// <summary>
        /// Stores AssetBundle loading information, starts loading the bundle.
        /// </summary>
        /// <param name="provideHandle">The container for AssetBundle loading information.</param>
        /// <param name="unloadOp">The async operation for unloading the AssetBundle.</param>
        public void Start(ProvideHandle provideHandle, AssetBundleUnloadOperation unloadOp,DataConverterBase dataProc)
#else
        /// <summary>
        /// Stores AssetBundle loading information, starts loading the bundle.
        /// </summary>
        /// <param name="provideHandle">The container for information regarding loading the AssetBundle.</param>
        public void Start(ProvideHandle provideHandle, DataConverterBase dataProc)
#endif
        {
            m_dataProc = dataProc;
            //Debug.Log($"m_dataProc={m_dataProc!=null}");
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
#if UNLOAD_BUNDLE_ASYNC
            m_UnloadOperation = unloadOp;
            if (m_UnloadOperation != null && !m_UnloadOperation.isDone)
                m_UnloadOperation.completed += OnUnloadOperationComplete;
            else
#endif
            BeginOperation();
        }

        private bool WaitForCompletionHandler()
        {
#if UNLOAD_BUNDLE_ASYNC
            if (m_UnloadOperation != null && !m_UnloadOperation.isDone)
            {
                m_UnloadOperation.completed -= OnUnloadOperationComplete;
                m_UnloadOperation.WaitForCompletion();
                m_UnloadOperation = null;
                BeginOperation();
            }
#endif

            if (m_RequestOperation == null)
            {
                if (m_WebRequestQueueOperation == null)
                    return false;
                else
                    WebRequestQueue.WaitForRequestToBeActive(m_WebRequestQueueOperation, k_WaitForWebRequestMainThreadSleep);
            }

            //We don't want to wait for request op to complete if it's a LoadFromFileAsync. Only UWR will complete in a tight loop like this.
            if (m_RequestOperation is UnityWebRequestAsyncOperation op)
            {
                while (!UnityWebRequestUtilities.IsAssetBundleDownloaded(op))
                    System.Threading.Thread.Sleep(k_WaitForWebRequestMainThreadSleep);
            }

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

        /// <summary>
        /// Determines where an AssetBundle can be loaded from.
        /// </summary>
        /// <param name="handle">The container for AssetBundle loading information.</param>
        /// <param name="loadType">Specifies where an AssetBundle can be loaded from.</param>
        /// <param name="path">The file path or url where the AssetBundle is located.</param>
        public static void GetLoadInfo(ProvideHandle handle, DataConverterBase dataProc, out LoadType loadType, out string path)
        {
            GetLoadInfo(handle.Location, handle.ResourceManager, dataProc, out loadType, out path);
        }

        internal static void GetLoadInfo(IResourceLocation location, ResourceManager resourceManager, DataConverterBase dataProc,
        out LoadType loadType, out string path)
        {
            var options = location?.Data as HMAA_AssetBundleRequestOptions;
            if (options == null)
            {
                loadType = LoadType.None;
                path = null;
                return;
            }

            path = resourceManager.TransformInternalId(location);
            // Debug.Log($"location={(location!=null?location.PrimaryKey:"空")} Application.platform={Application.platform} path={path} dataProc={dataProc!=null} ");
            if (Application.platform == RuntimePlatform.Android && path.StartsWith("jar:"))
            {
                loadType = options.UseUnityWebRequestForLocalBundles? LoadType.Web : LoadType.Local;
                if (dataProc != null)
                {
                    // // if a path starts with jar:file, it is an android embeded resource. The resource is a local file but cannot be accessed by 
                    // FileStream(called in LoadWithDataProc) directly
                    // Need to use webrequest's async call to get the content.
                    loadType = LoadType.Web;
                    // Debug.Log($"安卓只能从这里走{loadType}");
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

            if (dataProc != null && File.Exists(path) && loadType == LoadType.Local)
            {
                //Debug.Log($"安卓变了{loadType}==>LoadType.LocalDecrypt");
                loadType = LoadType.LocalDecrypt;
            }
            else if (dataProc != null && File.Exists(GetEncryptedAssetLocalPath(path, options))) // cached local path
            {
                // Debug.Log($"安卓变了{loadType}==>LoadType.LocalDecryptCache");
                loadType = LoadType.LocalDecryptCache;
            }
        }

        private void BeginOperation()
        {
            m_DownloadedBytes = 0;
            //Debug.Log($"获得加载结果为 m_dataProc={m_dataProc!=null}");
            GetLoadInfo(m_ProvideHandle, m_dataProc, out LoadType loadType, out m_TransformedInternalId);
            // Debug.Log($"获得加载结果为{loadType} m_dataProc={m_dataProc!=null}");
            if (loadType == LoadType.Local)
            {
#if !UNITY_2021_1_OR_NEWER
                if (AsyncOperationHandle.IsWaitingForCompletion)
                    CompleteBundleLoad(AssetBundle.LoadFromFile(m_TransformedInternalId, m_Options == null? 0 : m_Options.Crc));
                else
#endif
                {
                    m_RequestOperation = AssetBundle.LoadFromFileAsync(m_TransformedInternalId, m_Options == null? 0 : m_Options.Crc);
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
            else if (loadType == LoadType.LocalDecrypt)
            {
                //Debug.Log($"走的{loadType}");
                var crc = m_Options == null? 0 : m_Options.Crc;
                LoadWithDataProc(m_TransformedInternalId, crc);
                AddCallbackInvokeIfDone(m_RequestOperation, LocalRequestOperationCompleted);
            }
            else if (loadType == LoadType.LocalDecryptCache)
            {
                //Debug.Log($"走的{loadType}");
                var crc = m_Options == null? 0 : m_Options.Crc;
                LoadWithDataProc(GetEncryptedAssetLocalPath(m_TransformedInternalId, m_Options), crc);
                AddCallbackInvokeIfDone(m_RequestOperation, LocalRequestOperationCompleted);
            }
            else
            {
                Debug.Log(
                    $"location={(m_ProvideHandle.Location != null? m_ProvideHandle.Location.PrimaryKey : "空")} Application.platform={Application.platform} path={m_TransformedInternalId} dataProc={m_dataProc != null} ");
                m_RequestOperation = null;
                m_ProvideHandle.Complete<AssetBundleResource>(null, false,
                    new RemoteProviderException(string.Format("Invalid path in AssetBundleProvider: '{0}'.", m_TransformedInternalId),
                        m_ProvideHandle.Location));
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

        /// <inheritdoc/>
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
            // Debug.Log($"资源加载完毕:{bundle.name}+{DateTime.Now.Ticks / 10000l }");
            m_AssetBundle = bundle;
            if (m_AssetBundle != null)
                m_ProvideHandle.Complete(this, true, null);
            else
                m_ProvideHandle.Complete<AssetBundleResource>(null, false,
                    new RemoteProviderException(string.Format("Invalid path in AssetBundleProvider: '{0}'.", m_TransformedInternalId),
                        m_ProvideHandle.Location));
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
                //-----------------------------------
                if (m_dataProc != null)
                {
                    // encrypt bundle, need to cache InternalId before complete
                    m_InternalId = m_ProvideHandle.Location.InternalId;
                }

                if (!m_Completed)
                {
                    //----------------------------------------------------------------------
                    m_downloadHandler = webReq.downloadHandler as DownloadHandlerAssetBundle;
                    // encrypted bundle will use DownloadHandlerBuffer
                    if (m_downloadHandler == null)
                    {
                        m_downloadHandler = webReq.downloadHandler as UnityEngine.Networking.DownloadHandlerBuffer;
                    }

                    m_ProvideHandle.Complete(this, true, null);
                    m_Completed = true;
                }

                //-----------------------------------
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
                    if (m_IsLoadingFromCache)
                    {
                        message =
                                $"Web request failed to load from cache. The cached AssetBundle will be cleared from the cache and re-downloaded. Retrying...\n{uwrResult}";
                        Caching.ClearCachedVersion(m_Options.BundleName, Hash128.Parse(m_Options.Hash));
                        // When attempted to load from cache we always retry on first attempt and failed
                        if (m_Retries == 0)
                        {
                            Debug.LogFormat(message);
                            //Debug.Log($"WebRequestOperationCompleted  m_dataProc={m_dataProc!=null}");
                            BeginOperation();
                            m_Retries++; //Will prevent us from entering an infinite loop of retrying if retry count is 0
                            forcedRetry = true;
                        }
                    }
                }
#endif
                if (!forcedRetry)
                {
                    if (m_Retries < m_Options.RetryCount && uwrResult.ShouldRetryDownloadError())
                    {
                        m_Retries++;
                        Debug.LogFormat(message);
                        //Debug.Log($"WebRequestOperationCompleted  forcedRetry m_dataProc={m_dataProc!=null}");
                        BeginOperation();
                    }
                    else
                    {
                        var exception = new RemoteProviderException($"Unable to load asset bundle from : {webReq.url}", m_ProvideHandle.Location,
                            uwrResult);
                        m_ProvideHandle.Complete<AssetBundleResource>(null, false, exception);
                        m_Completed = true;
                    }
                }
            }

            webReq.Dispose();
        }

#if UNLOAD_BUNDLE_ASYNC
        /// <summary>
        /// Starts an async operation that unloads all resources associated with the AssetBundle.
        /// </summary>
        /// <param name="unloadOp">The async operation.</param>
        /// <returns>Returns true if the async operation object is valid.</returns>
        public bool Unload(out AssetBundleUnloadOperation unloadOp)
#else
        /// <summary>
        /// Unloads all resources associated with the AssetBundle.
        /// </summary>
        public void Unload()
#endif
        {
#if UNLOAD_BUNDLE_ASYNC
            unloadOp = null;
            if (m_AssetBundle != null)
            {
                unloadOp = m_AssetBundle.UnloadAsync(true);
                m_AssetBundle = null;
            }
#else
            if (m_AssetBundle != null)
            {
                m_AssetBundle.Unload(true);
                m_AssetBundle = null;
            }
#endif
            if (m_downloadHandler != null)
            {
                m_downloadHandler.Dispose();
                m_downloadHandler = null;
            }

            m_RequestOperation = null;
#if UNLOAD_BUNDLE_ASYNC
            return unloadOp != null;
#endif
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

        void CopyStream(Stream input, Stream output)
        {
            byte[] buffer = new byte[8 * 1024];
            int len;
            while ((len = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, len);
            }
        }

        private void LoadWithDataProc(String path, uint crc)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            //Debug.Log("开始走LoadWithDataProc");
            var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            //Debug.Log($"path={path}   LoadWithDataProc FileStream 耗时 ={stopwatch.ElapsedMilliseconds}");
            var dataStream = m_dataProc.CreateReadStream(fileStream, m_ProvideHandle.Location.InternalId);
            //Debug.Log($"path={path} LoadWithDataProc CreateReadStream 耗时 ={stopwatch.ElapsedMilliseconds}");
            if (dataStream.CanSeek)
            {
                //Debug.Log($"资源开始从内存中加载CanSeek:{path}+{ DateTime.Now.Ticks/10000L} dataStream.size{dataStream.Length}");
                m_RequestOperation = AssetBundle.LoadFromStreamAsync(dataStream, crc);
            }
            else
            {
                // Debug.Log("开始走LoadWithDataProc->这里");
                //Slow path needed if stream is not seekable

                var memStream = new MemoryStream();
                dataStream.CopyTo(memStream);
                //Debug.Log($"path={path} LoadWithDataProc copy数据操作耗时 ={stopwatch.ElapsedMilliseconds}");
                dataStream.Flush();
                dataStream.Dispose();
                fileStream.Dispose();
                stopwatch.Stop();
                //Debug.Log($"path={path} LoadWithDataProc 刷新数据操作耗时 ={stopwatch.ElapsedMilliseconds}");
                memStream.Position = 0;
                //Debug.Log($"资源开始从内存中加载CanSeek:{path}+{ DateTime.Now.Ticks/10000L} ");
                m_RequestOperation = AssetBundle.LoadFromStreamAsync(memStream, crc);
            }
        }
    }

    /// <summary>
    /// Contains cache information to be used by the AssetBundleProvider
    /// </summary>
    [Serializable]
    public class HMAA_AssetBundleRequestOptions: AssetBundleRequestOptions
    {
        public override long ComputeSize(IResourceLocation location, ResourceManager resourceManager)
        {
            var id = resourceManager == null? location.InternalId : resourceManager.TransformInternalId(location);
            if (!ResourceManagerConfig.IsPathRemote(id))
                return 0;
            var locHash = Hash128.Parse(Hash);
#if ENABLE_CACHING
            HMAAEncrypt_AssetBundleProvider assetBundleProvider =
                    resourceManager.GetResourceProvider(null, location) as HMAAEncrypt_AssetBundleProvider;
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