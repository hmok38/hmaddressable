using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace HM
{
    /// <summary>
    /// Interface for converting data.  This can be used to provide a custom encryption strategy for asset bundles.
    /// </summary>
    public interface IDataConverter
    {
        /// <summary>
        /// Create a stream for converting raw data into a format to be consumed by the game.  This method will be called by the build process when preparing data.
        /// </summary>
        /// <param name="input">The raw data to convert.</param>
        /// <param name="id">The id of the stream, useful for debugging.</param>
        /// <returns>Stream that converts the input data.</returns>
        Stream CreateWriteStream(Stream input, string id);
        /// <summary>
        /// Create a stream for transforming converted data back into the format that is expected by the player.  This method will be called at runtime.
        /// </summary>
        /// <param name="input">The converted data to transform.</param>
        /// <param name="id">The id of the stream, useful for debugging..</param>
        /// <returns>Stream that transforms the input data.  For best performance, this stream should support seeking.  If not, a full memory copy of the data must be made.</returns>
        Stream CreateReadStream(Stream input, string id);
    }
    
    public class AESStreamProcessor : IDataConverter
    {
        byte[] Key { get { return System.Text.Encoding.ASCII.GetBytes("ABCDEFGHIJKLMNOP"); } }
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
        public Stream CreateReadStream(Stream input, string id)
        {
            Debug.Log("创建加密读数据");
            return new CryptoStream(input, Algorithm.CreateDecryptor(Algorithm.Key, Algorithm.IV), CryptoStreamMode.Read);
        }

        public Stream CreateWriteStream(Stream input, string id)
        {
            Debug.Log("创建加密写数据");
            return new CryptoStream(input, Algorithm.CreateEncryptor(Algorithm.Key, Algorithm.IV), CryptoStreamMode.Write);
        }
    }
}
