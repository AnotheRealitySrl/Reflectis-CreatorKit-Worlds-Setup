using System.Collections.Generic;
using UnityEngine;

namespace Reflectis.SetupEditor
{
    [System.Serializable]
    public class ReflectisVersion
    {
        public string version;
        public List<ReflectisPackage> reflectisPackages;
        public List<ReflectisOptionalPackage> optionalPackages;

        public void Print()
        {
            Debug.LogError("Reflectis Version: " + version);
            foreach (ReflectisPackage pkg in reflectisPackages)
            {
                pkg.Print();
            }

            Debug.LogError("OPTIONAL PACKAGES " + version);
            foreach (ReflectisOptionalPackage pkg in optionalPackages)
            {
                pkg.Print();
            }
        }
    }
}
