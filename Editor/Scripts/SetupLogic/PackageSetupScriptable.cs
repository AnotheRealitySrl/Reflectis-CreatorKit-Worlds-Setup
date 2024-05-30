using UnityEngine;

namespace Reflectis.SetupEditor
{
    [CreateAssetMenu(fileName = "PackageSetup", menuName = "Reflectis/Packages/SinglePackageSetup")]
    public class PackageSetupScriptable : ScriptableObject
    {
        public string packageName;
        public string displayedName;
        public string gitURL;
        public string assemblyGUID;

        public string version;

        public bool isGitPackage = true;
        public bool installed = false;
        public bool isCore = true;
    }
}
