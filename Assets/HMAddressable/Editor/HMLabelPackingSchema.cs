using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace HM.Editor
{
    /// <summary>随母包资源组保存分片所属的平台，供后续热更识别工具管理的组。</summary>
    public sealed class HMLabelPackingSchema : AddressableAssetGroupSchema
    {
        [SerializeField, HideInInspector]
        private BuildTarget _buildTarget;

        internal BuildTarget BuildTarget => _buildTarget;

        internal void RecordBuildTarget(BuildTarget target)
        {
            if (_buildTarget != target)
            {
                _buildTarget = target;
                SetDirty(true);
            }
        }
    }
}
