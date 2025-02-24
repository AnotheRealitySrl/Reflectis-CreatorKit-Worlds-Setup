using System;

using UnityEngine;

namespace Reflectis.CreatorKit.Worlds.Installer.Editor
{
    public enum EPackageVisibility
    {
        Visible,
        Hidden
    }

    [Serializable]
    [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.Fields)]
    public class PackageDefinition
    {
        [SerializeField] private string name;
        [SerializeField] private string displayName;
        [SerializeField] private string description;
        [SerializeField] private string version;
        [SerializeField] private string url;
        [SerializeField] private EPackageVisibility visibility;

        public string Name => name;
        public string DisplayName => displayName;
        public string Description => description;
        public string Version => version;
        public string Url => url;
        public EPackageVisibility Visibility => visibility;
    }
}
