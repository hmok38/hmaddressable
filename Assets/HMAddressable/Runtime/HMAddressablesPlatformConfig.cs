using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HM
{
    [CreateAssetMenu(fileName = "HMAddressablePlatformConfig", menuName = "HMAddressables/创建多平台配置表")]
    /// <summary>
    /// 资源插件多平台管理器,开发者自行添加平台条目并拖入对应配置表,运行时通过CurrentConfig按平台获取
    /// </summary>
    public class HMAddressablePlatformConfig : ScriptableObject
    {
        [SerializeField] [Tooltip("平台配置列表,按条目添加目标平台和对应的配置表")]
        private List<PlatformConfigEntry> _platformConfigs = new List<PlatformConfigEntry>();

        public List<PlatformConfigEntry> PlatformConfigEntries => _platformConfigs;

        /// <summary>
        /// 根据当前运行平台从列表中匹配并返回对应的HMAddressablesConfig
        /// </summary>
        public HMAddressablesConfig CurrentConfig
        {
            get
            {
                var platform = GetCurrentPlatform();
                foreach (var entry in _platformConfigs)
                {
                    if (entry.Config != null && entry.Platform == platform)
                        return entry.Config;
                }

                Debug.LogError(
                    $"HMAddressablePlatformConfig: 未找到平台 {platform} 对应的配置,请在配置表中添加该平台条目");
                return null;
            }
        }

        private static RuntimePlatform GetCurrentPlatform()
        {
#if UNITY_EDITOR
            switch (EditorUserBuildSettings.activeBuildTarget)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return RuntimePlatform.WindowsPlayer;
                case BuildTarget.StandaloneOSX:
                    return RuntimePlatform.OSXPlayer;
                case BuildTarget.StandaloneLinux64:
                    return RuntimePlatform.LinuxPlayer;
                case BuildTarget.Android:
                    return RuntimePlatform.Android;
                case BuildTarget.iOS:
                    return RuntimePlatform.IPhonePlayer;
                case BuildTarget.WebGL:
                    return RuntimePlatform.WebGLPlayer;
                default:
                    return Application.platform;
            }
#else
            return Application.platform;
#endif
        }
    }
}