using System.Collections.Generic;

using UnityEngine;

namespace Reflectis.CreatorKit.Worlds.Installer.Editor
{
    [SerializeField]
    public class PackageRegistry
    {
        [SerializeField] private string reflectisVersion;
        [SerializeField] private string requiredUnityVersion;
        [SerializeField] private List<PackageDefinition> packages;
        [SerializeField] private Dictionary<string, List<string>> dependencies;

        public string ReflectisVersion => reflectisVersion;
        public string RequiredUnityVersion => requiredUnityVersion;
        public List<PackageDefinition> Packages => packages;
        public Dictionary<string, List<string>> Dependencies => dependencies;
    }
}
