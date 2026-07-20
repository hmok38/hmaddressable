using System;
using UnityEngine;

namespace HM
{
    /// <summary>
    /// 平台与配置表的映射条目,开发者可在Inspector中自行添加
    /// </summary>
    [Serializable]
    public class PlatformConfigEntry
    {
        [Tooltip("目标平台")] public RuntimePlatform Platform;

        [Tooltip("该平台对应的HMAddressables配置表")] public HMAddressablesConfig Config;
    }
}
