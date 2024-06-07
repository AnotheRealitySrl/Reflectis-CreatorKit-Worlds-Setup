using System.Collections.Generic;
using UnityEngine;

namespace Reflectis.SetupEditor
{
    [System.Serializable]
    public class ReflectisPackage
    {
        public string gitUrl; //The URL to github for the package
        public string name; //name of the package, contained in the package.json
        public string version; //version of the package
        public string displayedName; //name shown in the setup window
        public List<ReflectisPackage> subpackages;

        public void Print()
        {
            Debug.LogError("------ PACKAGE " + displayedName + " " + version + "-------");
            Debug.LogError("URL: " + gitUrl);
            Debug.LogError("--------------------------");

        }
    }
}
