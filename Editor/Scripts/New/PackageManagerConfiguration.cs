using System;
using System.Collections.Generic;
using System.Linq;

using Unity.Properties;

using UnityEngine;

namespace Reflectis.CreatorKit.Worlds.Installer.Editor
{

    [CreateAssetMenu(fileName = "CreatorKitConfigurationWindowDataSource", menuName = "Reflectis/Creator-Kit-Installer/CreatorKitConfigurationWindowDataSource")]
    public class PackageManagerConfiguration : ScriptableObject
    {
        [CreateProperty] public PackageDefinition[] SelectedVersionPackageList { get; set; }
        [CreateProperty] public PackageDefinition[] SelectedVersionPackageListFiltered => SelectedVersionPackageList.Where(x => x.Visibility == EPackageVisibility.Visible).ToArray();

        [SerializeField] private List<PackageDefinition> installedPackages = new();
        [CreateProperty]
        public HashSet<PackageDefinition> InstalledPackages
        {
            get => installedPackages.ToHashSet();
            set => installedPackages = value.ToList();
        }

        [field: SerializeField] public string DisplayedReflectisVersion { get; set; }

        [SerializeField] private string currentInstallationVersion;
        [CreateProperty]
        public string CurrentInstallationVersion
        {
            get => currentInstallationVersion;
            set => currentInstallationVersion = value;
        }

        [SerializeField] private bool resolveBreakingChangesAutomatically;
        [CreateProperty]
        public bool ResolveBreakingChangesAutomatically
        {
            get => resolveBreakingChangesAutomatically;
            set => resolveBreakingChangesAutomatically = value;
        }

        [SerializeField] private bool showPrereleases;
        [CreateProperty]
        public bool ShowPrereleases
        {
            get => showPrereleases;
            set => showPrereleases = value;
        }

        [CreateProperty] public DateTime LastRefreshTime { get; set; }
    }

}
