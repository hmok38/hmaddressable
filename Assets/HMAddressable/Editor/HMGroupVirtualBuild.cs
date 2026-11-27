#if HMAA_HAS_VIRTUAL_BUILD
using UnityEditor.AddressableAssets.Build.DataBuilders;

namespace HM.Editor
{
    /// <summary>兼容已保存的旧播放模式资产，直接使用原生 Simulate Groups。</summary>
    public sealed class HMGroupVirtualBuild : BuildScriptVirtualMode
    {
    }
}
#endif
