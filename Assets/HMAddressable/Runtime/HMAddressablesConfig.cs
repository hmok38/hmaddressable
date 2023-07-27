using System;
using System.Collections.Generic;
using UnityEngine;

namespace HM
{
    [CreateAssetMenu(fileName = "ConfigHMAddressables", menuName = "HMAddressables/创建Config对象")]
    public class HMAddressablesConfig : ScriptableObject
    {
        [Header("要包含在APP中的资源目录")] public string[] LocalAseetsPaths = new[] {"Assets/Bundles"};
        [Header("要远程下载的资源目录")] public string[] RemoteAseetsPaths = new string[] { };
        [Header("正式资源服务器分发地址")] public string RemoteLoadPath = "http://[PrivateIpAddress]/[BuildTarget]";
        [Header("测试资源服务器分发地址")] public string TestRemoteLoadPath = "http://[PrivateIpAddress]/Test/[BuildTarget]";


#if UNITY_2021_1_OR_NEWER
        [Space(40), Header("=============加密================"),
         Header("及升级组都会默认使用此加密,如某些组不需要加密可去组设置进行调整"), Header("需要加密的文件夹名后缀")]
#else
        [Header("需要加密的文件夹(资源组)"), Header("此文件夹(不包含子文件夹)生成的资源组需要加密,采用下面的加密方式"),
         Header("============加密================="), Space(40)]
#endif
        public string[] EncryptAssetsGroup;


        [Header("上述加密文件夹使用的资源加密类型")]
        public EncrypyType MyDefaultAssetsEncryptType = EncrypyType.AESStreamProcessorWithSeek;


#if UNITY_2021_1_OR_NEWER
         [Space(20), Header("=========重复依赖组=============="), Header("重复依赖组放到远端?默认为false,放在本地")]
#else
        [Header("重复依赖组放到远端?默认为false,放在本地"), Header("===========重复依赖组=============="), Space(40)]
#endif
        public bool DuplicateDependenciesGroupBeRemote;


#if UNITY_2021_1_OR_NEWER
        [Space(40), Header("==========Debug测试整包==============="), Header("强制将远程资源全部打入本地包(Debug测试整包)")]
#else
        [Header("强制将远程资源全部打入本地包(Debug测试整包)"), Header("=============Debug测试整包============="), Space(40)]
#endif
        public bool ForceRemoteAssetsToLocal;


#if UNITY_2021_1_OR_NEWER
        [Space(40), Header("===========GPAD================="),
         Header("是否使用谷歌PlayAssetDelivery分包 在谷歌发布大于150M以上的安装包时"),
         Header("会将设置为Remote的资源组作为分包打入AAB中,在做资源检查时,将这些资源复制安装到AA的持久化目录"),
         SerializeField]
#else
        [SerializeField, Header("会将设置为Remote的资源组作为分包打入AAB中,在做资源检查时,将这些资源复制安装到AA的持久化目录"),
         Header("是否使用谷歌PlayAssetDelivery分包 在谷歌发布大于150M以上的安装包时"), Header("=============GPAD=============="), Space(40)]
#endif
        private bool useGooglePlayAssetDelivery;

        /// <summary>
        /// 使用谷歌PlayAssetDelivery分包,发布大于150M的包
        /// </summary>
        public bool UseGooglePlayAssetDelivery
        {
            get
            {
#if !UNITY_ANDROID
                return false;
#endif
                return useGooglePlayAssetDelivery;
            }

            set
            {
                if (value == useGooglePlayAssetDelivery) return;
                useGooglePlayAssetDelivery = value;
                if (value)
                {
                    OnUseGooglePlayAssetDelivery();
                }
            }
        }

        /// <summary>
        /// 使用谷歌playAssetDelivery分包功能的编译符
        /// </summary>
        [HideInInspector, NonSerialized]
        public const string UseGooglePlayAssetDeliveryDefineStr = "HMAAUSEGOOGLEPLAYASSETDELIVERY";

        /// <summary>
        /// 使用谷歌资源分发的分组列表,每次打包都会重置和重新记录
        /// </summary>
        [HideInInspector] public List<string> GooglePlayAssetDeliveryBundleNames = new List<string>();

        /// <summary>
        /// 使用谷歌资源分发的远程文件信息,包含文件名和hash值,用来在持久化目录创建"catalog_2023.07.25.07.25.08.hash"和"catalog_2023.07.25.07.25.08.json",建立
        /// </summary>
        [HideInInspector] public string remoteInfo;

#if UNITY_2021_1_OR_NEWER
        [Space(40), Header("===========跳过更新================="),
         Header("跳过检查更新,在运行时不进行更新操作,方便测试")]
#else
        [Header("跳过检查更新,在运行时不进行更新操作,方便测试"),
         Header("=============跳过更新================"), Space(40)]
#endif
        public bool BeSkipUpdateCheck;

        /// <summary>
        /// 获得默认的资源供应器类型
        /// </summary>
        /// <returns></returns>
        public Type GetMyDefaultAssetBundleProvider()
        {
            switch (MyDefaultAssetsEncryptType)
            {
                case EncrypyType.AESStreamProcessor: return typeof(HMAAEncrypt_AssetBundleProvider_AES);
                // case EncrypyType.GZipDataStreamProc: return typeof(HMAAEncrypt_AssetBundleProvider_GZip);
                case EncrypyType.AESStreamProcessorWithSeek: return typeof(HMAAEncrypt_AssetBundleProvider_AESWithSeek);
                default: return typeof(HMAAEncrypt_AssetBundleProvider);
            }
        }
        
        /// <summary>
        /// 获得不加密的资源供应器类型
        /// </summary>
        /// <returns></returns>
        public Type GetNonEncryptAssetBundleProvider()
        {
            return typeof(HMAAEncrypt_AssetBundleProvider);
        }

        /// <summary>
        /// 根据资源供应器确定加密类
        /// </summary>
        /// <param name="typeInt"></param>
        /// <returns></returns>
        public static Type GetEncrypyType(Type assetBundleProviderType)
        {
            if (assetBundleProviderType == typeof(HMAAEncrypt_AssetBundleProvider_AES))
            {
                return typeof(AESStreamProcessor);
            }

            // if (assetBundleProviderType == typeof(HMAAEncrypt_AssetBundleProvider_GZip))
            // {
            //     return typeof( GZipDataStreamProc);
            // }
            if (assetBundleProviderType == typeof(HMAAEncrypt_AssetBundleProvider_AESWithSeek))
            {
                return typeof(AESStreamProcessorWithSeek);
            }

            return null;
        }

        private void OnValidate()
        {
            this.OnUseGooglePlayAssetDelivery();
        }

        public void OnUseGooglePlayAssetDelivery()
        {
#if UNITY_EDITOR && UNITY_ANDROID
            if (useGooglePlayAssetDelivery)
            {
                Debug.Log(
                    "要使用谷歌资源分包 需要在打包或者导出安卓工程时 在<BuildSettings>设置BuildAppBundle(GooglePlay) 或 Export for App Bundle");
            }
#endif
        }

        /// <summary>
        /// 获得配置表提示,用来在打包资源前对配置表配置在日志上进行提醒
        /// </summary>
        public void CheckConfigTips()
        {
#if UNITY_ANDROID
            if (UseGooglePlayAssetDelivery)
            {
                OnUseGooglePlayAssetDelivery();
            }
#endif
            if (BeSkipUpdateCheck)
            {
                Debug.Log($"重要提示:本次资源打包跳过了资源更新检查,在运行时不会进行资源更新检查,如非特意设置,请修改本配置表设置");
            }
        }
    }

    public enum EncrypyType
    {
        None = 0,
        AESStreamProcessor = 1,

        // GZipDataStreamProc=2,
        AESStreamProcessorWithSeek = 3
    }
}