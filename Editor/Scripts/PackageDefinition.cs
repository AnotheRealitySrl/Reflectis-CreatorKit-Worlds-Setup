using System;

using Unity.Properties;

using UnityEngine;

namespace Reflectis.CreatorKit.Worlds.Setup.Editor
{
    public enum EPackageVisibility
    {
        Visible,
        Hidden
    }

    [Serializable]
    [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.Fields)]
    public class PackageDefinition : IEquatable<PackageDefinition>
    {
        [SerializeField] private string name;
        [SerializeField] private string displayName;
        [SerializeField] private string description;
        [SerializeField] private string version;
        [SerializeField] private string url;
        [SerializeField] private EPackageVisibility visibility;

        public string Name => name;
        [CreateProperty] public string DisplayName => displayName;
        [CreateProperty] public string Description => description;
        [CreateProperty] public string Version => version;
        [CreateProperty] public string Url => url;
        public EPackageVisibility Visibility => visibility;

        public bool Equals(PackageDefinition other)
        {
            if (other == null) return false;
            return name == other.name && version == other.version;
        }

        public override bool Equals(object obj)
        {
            if (obj is PackageDefinition other)
            {
                return Equals(other);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(name, version);
        }
    }
}
