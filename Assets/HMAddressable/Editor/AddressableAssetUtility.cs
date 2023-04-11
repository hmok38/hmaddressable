
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.AddressableAssets;
using UnityEditor.Build.Utilities;
using UnityEditor.VersionControl;


namespace HM.Editor
{
   internal static class AddressableAssetUtility
    {
        private static string isEditorFolder = $"{Path.DirectorySeparatorChar}Editor";
        private static string insideEditorFolder = $"{Path.DirectorySeparatorChar}Editor{Path.DirectorySeparatorChar}";
        static HashSet<string> excludedExtensions = new HashSet<string>(new string[] { ".cs", ".js", ".boo", ".exe", ".dll", ".meta", ".preset", ".asmdef" });
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
        internal static bool IsPathValidForEntry(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            path = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

            if (!path.StartsWith("Assets", StringComparison.Ordinal) && !IsPathValidPackageAsset(path))
                return false;

            string ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext))
            {
                // is folder
                if (path == "Assets")
                    return false;
                if (path.EndsWith(isEditorFolder, StringComparison.Ordinal) || path.Contains(insideEditorFolder))
                    return false;
                if (path == CommonStrings.UnityEditorResourcePath ||
                    path == CommonStrings.UnityDefaultResourcePath ||
                    path == CommonStrings.UnityBuiltInExtraPath)
                    return false;
            }
            else
            {
                // asset type
                if (path.Contains(insideEditorFolder))
                    return false;
                if (excludedExtensions.Contains(ext))
                    return false;
            }

            var settings = AddressableAssetSettingsDefaultObject.SettingsExists ? AddressableAssetSettingsDefaultObject.Settings : null;
            if (settings != null && path.StartsWith(settings.ConfigFolder, StringComparison.Ordinal))
                return false;

            return true;
        }
        internal static bool IsPathValidPackageAsset(string path)
        {
            string[] splitPath = path.ToLower().Split(Path.DirectorySeparatorChar);

            if (splitPath.Length < 3)
                return false;
            if (splitPath[0] != "packages")
                return false;
            if (splitPath[2] == "package.json")
                return false;
            return true;
        }
    }
}
