using System;
using UnityEngine;

namespace HM
{
    [CreateAssetMenu(fileName = "ConfigHMAddressables", menuName = "HMAddressables/创建Config对象")]
    public class HMAddressablesConfig : ScriptableObject
    {
        
        [Header("要包含在APP中的资源目录")]
        public string[] LocalAseetsPaths=new []{"Assets/Bundles"} ;
        [Header("要远程下载的资源目录")]
        public string[] RemoteAseetsPaths=new string[]{} ;
        [Header("正式资源服务器分发地址")]
        public string RemoteLoadPath = "http://[PrivateIpAddress]/[BuildTarget]";
        [Header("测试资源服务器分发地址")]
        public string TestRemoteLoadPath = "http://[PrivateIpAddress]/Test/[BuildTarget]";
        [Header("及升级组都会默认使用此加密,如某些组不需要加密可去组设置进行调整")]
        [Header("默认资源加密类型,新创建组或重复引用组")]
        [Header("=================================")]
        [Space(20)]
        public EncrypyType MyDefaultAssetsEncryptType = EncrypyType.AESStreamProcessorWithSeek;

        
       
        /// <summary>
        /// 加密类型
        /// </summary>
        /// <param name="typeInt"></param>
        /// <returns></returns>
        public static Type GetEncrypyType(EncrypyType typeEnum)
        {
            return GetEncrypyType((int)typeEnum);
        }
        /// <summary>
        /// 加密类型
        /// </summary>
        /// <param name="typeInt"></param>
        /// <returns></returns>
        public static Type GetEncrypyType(int typeInt)
        {
            switch (typeInt)
            {
                case 0:
                    return null;
                case 1:
                    return typeof(AESStreamProcessor);
                case 2:
                    return typeof(GZipDataStreamProc);
                case 3:
                    return typeof(AESStreamProcessorWithSeek);
            }

            return null;
        }
        
        
    }

    public enum EncrypyType
    {
        None=0,
        AESStreamProcessor=1,
        GZipDataStreamProc=2,
        AESStreamProcessorWithSeek=3
    }
        
    
}


