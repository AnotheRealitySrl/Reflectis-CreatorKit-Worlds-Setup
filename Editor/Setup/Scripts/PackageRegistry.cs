using System;
using System.Collections.Generic;

using UnityEngine;

namespace Reflectis.CreatorKit.Worlds.Setup.Editor
{
    [Serializable]
    [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.Fields)]
    public class PackageRegistry
    {
        [SerializeField] private string reflectisVersion;
        [SerializeField] private string requiredUnityVersion;
        [SerializeField] private PackageDefinition[] packages;
        [SerializeField] private Dictionary<string, string[]> dependencies;

        public string ReflectisVersion => reflectisVersion;
        public string RequiredUnityVersion => requiredUnityVersion;
        public PackageDefinition[] Packages => packages;
        public Dictionary<string, string[]> Dependencies => dependencies;
    }
}
