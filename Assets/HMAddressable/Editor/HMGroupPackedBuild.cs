using UnityEditor.AddressableAssets.Build.DataBuilders;

namespace HM.Editor
{
    /// <summary>兼容已保存的旧构建器资产，构建过程完全沿用原生 Packed Mode。</summary>
    public sealed class HMGroupPackedBuild : BuildScriptPackedMode
    {
        /// <summary>标识仍引用此脚本的旧配置。</summary>
        public override string Name => "HMAA 旧配置（原生 Packed Mode）";
    }
}
