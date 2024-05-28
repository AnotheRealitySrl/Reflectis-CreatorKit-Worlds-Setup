using System.Collections.Generic;
using UnityEngine;

namespace Reflectis.SetupEditor
{
    [CreateAssetMenu(fileName = "PackagesDetails", menuName = "Reflectis/Packages/PackagesDetails")]
    public class PackagesDetails : ScriptableObject
    {
        public List<PackageSetupScriptable> packageDetailsList;
    }
}

