using System;

using Unity.Properties;

using UnityEngine;

namespace Reflectis.CreatorKit.Worlds.Installer.Editor
{

    [CreateAssetMenu(fileName = "CreatorKitConfigurationWindowDataSource", menuName = "Reflectis/Creator-Kit-Installer/CreatorKitConfigurationWindowDataSource")]
    public class PackageManagerConfiguration : ScriptableObject
    {
        [CreateProperty] public PackageDefinition[] SelectedVersionPackageList { get; set; }

        [CreateProperty] public int DisplayedReflectisVersionIndex { get; set; }
        [CreateProperty] public string DisplayedReflectisVersion { get; set; }
        [CreateProperty] public string CurrentInstallationVersion { get; set; }

        [CreateProperty] public bool ResolveBreakingChangesAutomatically { get; set; }
        [CreateProperty] public bool ShowPrereleases { get; set; }

        [CreateProperty] public DateTime LastRefreshTime { get; set; }
    }

}
