
using UnityEditor.VersionControl;


namespace HM.Editor
{
   internal static class AddressableAssetUtility
    {
       
        internal static bool IsVCAssetOpenForEdit(string path)
        {
            AssetList VCAssets = GetVCAssets(path);
            foreach (Asset vcAsset in VCAssets)
            {
                if (vcAsset.path == path)
                    return Provider.IsOpenForEdit(vcAsset);
            }

            return false;
        }

        internal static AssetList GetVCAssets(string path)
        {
            UnityEditor.VersionControl.Task op = Provider.Status(path);
            op.Wait();
            return op.assetList;
        }
    }
}
