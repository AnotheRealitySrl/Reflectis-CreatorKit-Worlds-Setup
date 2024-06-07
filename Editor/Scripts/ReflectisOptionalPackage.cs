using System.Collections.Generic;
using UnityEngine;

namespace Reflectis.SetupEditor
{
    [System.Serializable]
    public class ReflectisOptionalPackage
    {
        public string displayedName; //name shown in the setup window
        public List<ReflectisPackage> subpackages;

        public void Print()
        {
            Debug.LogError("------ PACKAGE " + displayedName);
            Debug.LogError("--------------------------");

        }
    }
}
