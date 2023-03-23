using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace HM
{
    
    /// <summary>
    /// 资源加密类型
    /// </summary>
    public enum AssetsEncryptType
    {
        /// <summary>
        /// 不加密
        /// </summary>
        AET_None,
        AET_AESStreamProcessor,
    }
    
    /// <summary>
    /// Interface for converting data.  This can be used to provide a custom encryption strategy for asset bundles.
    /// </summary>
    public abstract class DataConverterBase
    {
        /// <summary>
        /// 获取加密类型
        /// </summary>
        /// <returns></returns>
        public static Type GetEncryptType(AssetsEncryptType assetsEncryptType)
        {
            switch (assetsEncryptType)
            {
                case AssetsEncryptType.AET_None:
                    return null;
                case AssetsEncryptType.AET_AESStreamProcessor:
                    return typeof(AESStreamProcessor);
            }

            return null;
        }
       
       public abstract Stream CreateWriteStream(Stream input, string id);
        
       public abstract Stream CreateReadStream(Stream input, string id);
    }
    
    public class AESStreamProcessor : DataConverterBase
    {
        /// <summary>
        /// 必须是16位字符串
        /// </summary>
        byte[] Key { get { return System.Text.Encoding.ASCII.GetBytes("ABCDEFGHABCDEFGH"); } }
        SymmetricAlgorithm m_algorithm;
        SymmetricAlgorithm Algorithm
        {
            get
            {
                if (m_algorithm == null)
                {
                    m_algorithm = new AesManaged();
                    m_algorithm.Padding = PaddingMode.Zeros;
                    var initVector = new byte[m_algorithm.BlockSize / 8];
                    for (int i = 0; i < initVector.Length; i++)
                        initVector[i] = (byte)i;
                    m_algorithm.IV = initVector;
                    m_algorithm.Key = Key;
                    m_algorithm.Mode = CipherMode.ECB;
                }
                return m_algorithm;
            }
        }
        public override Stream CreateReadStream(Stream input, string id)
        {
            //Debug.Log("创建加密读数据");
            return new CryptoStream(input, Algorithm.CreateDecryptor(Algorithm.Key, Algorithm.IV), CryptoStreamMode.Read);
        }

        public override Stream CreateWriteStream(Stream input, string id)
        {
            //Debug.Log("创建加密写数据");
            return new CryptoStream(input, Algorithm.CreateEncryptor(Algorithm.Key, Algorithm.IV), CryptoStreamMode.Write);
        }
    }
}
