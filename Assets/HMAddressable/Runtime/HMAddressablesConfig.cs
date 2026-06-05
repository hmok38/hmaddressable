using System;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;

namespace HM
{
    [CreateAssetMenu(fileName = "ConfigHMAddressables", menuName = "HMAddressables/创建Config对象")]
    public class HMAddressablesConfig : ScriptableObject
    {
        [Header("AA资源目录")] public string[] AAAssetsPath = new[] { "Assets/Bundles" };
        [Header("尚未分配本地/远程的资源目录")] public AssetGroupLoadPath[] UnassignedAssetsPath = new AssetGroupLoadPath[] { };

        [Header("未分配的资源目录打包到本地?默认为true,打包到本地")]
        public bool UnassignedAssetsBeLocal = true;

        [Header("要包含在APP中的资源目录")] public string[] LocalAseetsPaths = new string[] { "Assets/Bundles" };
        [Header("要远程下载的资源目录")] public string[] RemoteAseetsPaths = new string[] { };
        [Header("要分散打包的资源目录")] public string[] SeparatelyPackAssetsPaths = new string[] { };
        [Header("正式资源服务器分发地址")] public string RemoteLoadPath = "http://[PrivateIpAddress]/[BuildTarget]";
        [Header("测试资源服务器分发地址")] public string TestRemoteLoadPath = "http://[PrivateIpAddress]/Test/[BuildTarget]";


#if UNITY_2021_1_OR_NEWER
        [Space(40), Header("=============加密================"),
         Header("此文件夹(不包含子文件夹)生成的资源组需要加密,采用下面的加密方式"), Header("需要加密的文件夹(资源组)")]
#else
        [Header("需要加密的文件夹(资源组)"), Header("此文件夹(不包含子文件夹)生成的资源组需要加密,采用下面的加密方式"),
         Header("============加密================="), Space(40)]
#endif
        public string[] EncryptAssetsGroup;


#if UNITY_2022_2_OR_NEWER
        [Header("注意:Unity2022.2版本以上暂时无法使用加密功能,会默认采用官方的资源打包方式--注意更新")]
#else
        [Header("上述加密文件夹使用的资源加密类型")]
#endif
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
        [SerializeField, Space(40),
         Header(
             "已弃用,请直接在playerSettings中设置Split Application Binary,同时需要在build Settings 中设置 Build App Bundle (Google Play)")]
#else
        [SerializeField,
         Header(
             "已弃用,请直接在playerSettings中设置Split Application Binary,同时需要在build Settings 中设置 Build App Bundle (Google Play)"),
         Space(40)]
#endif
        private bool useGooglePlayAssetDelivery;

        /// <summary>
        /// 使用谷歌PlayAssetDelivery分包,发布大于150M的包
        /// </summary>
        [Obsolete(
            "已弃用,请直接在playerSettings中设置Split Application Binary,同时需要在build Settings 中设置 Build App Bundle (Google Play)")]
        public bool UseGooglePlayAssetDelivery
        {
            get { return false; }

            set { }
        }


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


        [Obsolete(
            "已弃用,请直接在playerSettings中设置Split Application Binary,同时需要在build Settings 中设置 Build App Bundle (Google Play)")]
        public void OnUseGooglePlayAssetDelivery()
        {
        }

        /// <summary>
        /// 获得配置表提示,用来在打包资源前对配置表配置在日志上进行提醒
        /// </summary>
        public void CheckConfigTips()
        {
            if (BeSkipUpdateCheck)
            {
                Debug.Log($"重要提示:本次资源打包跳过了资源更新检查,在运行时不会进行资源更新检查,如非特意设置,请修改本配置表设置");
            }
        }

#if UNITY_EDITOR

        private Dictionary<string, AssetGroupLoadPath> map = new Dictionary<string, AssetGroupLoadPath>();

        /// <summary>
        /// 整理资源路径,用来在打包资源前对资源路径进行整理,比如去除路径中的重复项,或者检查路径的合法性等,目前暂时没有实现任何功能,后续根据需要进行实现
        /// </summary>
        public void OrganizeAssetsPaths()
        {
            var allSubFolders = new List<string>();
            for (int i = 0; i < AAAssetsPath.Length; i++)
            {
                GetAllSubFolders(AAAssetsPath[i], allSubFolders);
            }

            var allSub = allSubFolders.ToHashSet();
            Debug.Log($"共 {allSubFolders.Count} 个资源组");
            bool needSave = false;
            if (UnassignedAssetsPath.Length != 0)
            {
                for (int i = 0; i < UnassignedAssetsPath.Length; i++)
                {
                    if (allSub.Contains(UnassignedAssetsPath[i].GroupName))
                    {
                        //合法的groupName,继续检查路径是否合法
                        if (UnassignedAssetsPath[i].BeLocal) //设置了本地
                        {
                            LocalAseetsPaths = LocalAseetsPaths.Append(UnassignedAssetsPath[i].GroupName).ToArray();
                            needSave = true;
                        }

                        if (UnassignedAssetsPath[i].BeRemote) //设置了远程
                        {
                            RemoteAseetsPaths = RemoteAseetsPaths.Append(UnassignedAssetsPath[i].GroupName).ToArray();
                            needSave = true;
                        }
                    }
                }
            }

            List<int> needRemoveIndex = new List<int>();
            needRemoveIndex.Clear();
            //检查LocalAseetsPaths中的路径是否合法,比如是否存在,是否是文件夹等,目前暂时没有实现任何功能,后续根据需要进行实现
            for (int i = LocalAseetsPaths.Length - 1; i >= 0; i--)
            {
                var localAssets = LocalAseetsPaths[i];
                if (!allSub.Contains(localAssets))
                {
                    Debug.LogError($"LocalAseetsPaths 资源组中存在不合法的路径:{localAssets},已经移除");
                    needRemoveIndex.Add(i);
                }
                else
                {
                    allSub.Remove(localAssets);
                }
            }

            if (needRemoveIndex.Count > 0)
            {
                LocalAseetsPaths = LocalAseetsPaths.Where((x, index) => !needRemoveIndex.Contains(index)).ToArray();
                needSave = true;
            }


            needRemoveIndex.Clear();
            for (int i = RemoteAseetsPaths.Length - 1; i >= 0; i--)
            {
                var remoteAseetsPath = RemoteAseetsPaths[i];
                if (!allSub.Contains(remoteAseetsPath))
                {
                    Debug.LogError($"RemoteAseetsPaths 资源组中存在不合法的路径:{remoteAseetsPath},已经移除");
                    needRemoveIndex.Add(i);
                }
                else
                {
                    allSub.Remove(remoteAseetsPath);
                }
            }

            if (needRemoveIndex.Count > 0)
            {
                RemoteAseetsPaths = RemoteAseetsPaths.Where((path, index) => !needRemoveIndex.Contains(index))
                    .ToArray();
                needSave = true;
            }


            //还有一些路径没有被LocalAseetsPaths和RemoteAseetsPaths包含,说明这些路径没有被分配到资源组,需要进行提醒
            if (allSub.Count > 0)
            {
                needSave = true;
                var list = allSub.ToList();
                list.Sort();
                UnassignedAssetsPath = new AssetGroupLoadPath[list.Count];
                for (int i = 0; i < list.Count; i++)
                {
                    var path = list[i];
                    var assetGroupLoadPath = new AssetGroupLoadPath();
                    assetGroupLoadPath.GroupName = path;
                    UnassignedAssetsPath[i] = assetGroupLoadPath;
                }
            }
            else
            {
                if (UnassignedAssetsPath.Length > 0)
                {
                    UnassignedAssetsPath = Array.Empty<AssetGroupLoadPath>();
                    needSave = true;
                }
            }

            if (needSave)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.AssetDatabase.SaveAssets();
                UnityEditor.AssetDatabase.Refresh();
            }
        }

        private void GetAllSubFolders(string folder, List<string> allSubFolders)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return;

            var allAssetsGuids = AssetDatabase.FindAssets("", new[] { folder });
            var folderDirInfo = new System.IO.DirectoryInfo(folder);
            var hasNotFolderAssets = false; //是否有不在任何子文件夹中的资源
            for (int i = 0; i < allAssetsGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(allAssetsGuids[i]);
                if (AssetDatabase.IsValidFolder(path)) continue;
                var dic = new System.IO.DirectoryInfo(path);
                if (dic.Parent.FullName == folderDirInfo.FullName)
                {
                    hasNotFolderAssets = true;
                    break;
                }
            }

            if (hasNotFolderAssets)
            {
                allSubFolders.Add(folder);
            }


            var subFolders = AssetDatabase.GetSubFolders(folder);
            if (subFolders == null || subFolders.Length <= 0) return;
            foreach (var subFolder in subFolders)
            {
                GetAllSubFolders(subFolder, allSubFolders);
            }
        }

        public bool CheckAllAssetsPathIsInList()
        {
            var allSubFolders = new List<string>();
            for (int i = 0; i < AAAssetsPath.Length; i++)
            {
                GetAllSubFolders(AAAssetsPath[i], allSubFolders);
            }

            var allSub = allSubFolders.ToHashSet();
            Debug.Log($"共 {allSub.Count} 个资源组");
            if (LocalAseetsPaths.Length + RemoteAseetsPaths.Length + UnassignedAssetsPath.Length != allSub.Count)
            {
                Debug.LogError("请选择资源模块配置表,点击 整理资源目录 按钮");
                EditorGUIUtility.PingObject(this);
                Debug.LogError(
                    $"资源组数量不匹配,总数: allSubFolders:{allSubFolders.Count} 其中应该:LocalAseetsPaths:{LocalAseetsPaths.Length}  RemoteAseetsPaths:{RemoteAseetsPaths.Length} UnassignedAssetsPath:{UnassignedAssetsPath.Length} ");
                return false;
            }

            return true;
        }
#endif
    }

    public enum EncrypyType
    {
        None = 0,
        AESStreamProcessor = 1,

        // GZipDataStreamProc=2,
        AESStreamProcessorWithSeek = 3
    }

    /// <summary>
    /// 资源组本地化设置,用来在打包资源前对资源组进行本地化设置,如果资源组的路径包含了GroupName,则会按照BeLocal的设置将该资源组打入本地包或者远程包
    /// </summary>
    [Serializable]
    public class AssetGroupLoadPath
    {
        [Header("资源组名")] public string GroupName;
        [Header("打入本地")] public bool BeLocal;
        [Header("打入远程")] public bool BeRemote;
    }
}