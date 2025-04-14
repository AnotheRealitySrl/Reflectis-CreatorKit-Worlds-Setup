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
        // TODO: needed here?
        public PackageRegistry[] AllVersionsPackageRegistry { get; set; }
        public PackageDefinition[] SelectedVersionPackageList { get; set; }

        [CreateProperty] public PackageDefinition[] SelectedVersionPackageListFiltered => SelectedVersionPackageList.Where(x => x.Visibility == EPackageVisibility.Visible).ToArray();

        [CreateProperty]
        public List<string> AvailableVersions => AllVersionsPackageRegistry
                .Where(x => ShowPrereleases || x.ReflectisVersion != "develop")
                .Select(x => x.ReflectisVersion)
                .ToList();

        public List<string[]> SelectedVersionDependenciesFullOrdered => SelectedVersionPackageList.Select(x => SelectedVersionDependenciesFull[x.Name]).ToList();

        public Dictionary<string, string[]> SelectedVersionDependenciesFull { get; set; }


        [SerializeField] private List<PackageDefinition> installedPackages = new();
        [CreateProperty] public List<PackageDefinition> InstalledPackages { get => installedPackages; set => installedPackages = value; }


        [SerializeField] private string currentInstallationVersion;
        [CreateProperty] public string CurrentInstallationVersion { get => currentInstallationVersion; set => currentInstallationVersion = value; }
        [CreateProperty] public string DisplayedReflectisVersion { get; set; }
        [CreateProperty] public bool DisplayedAndInstalledVersionsAreDifferent => CurrentInstallationVersion != DisplayedReflectisVersion;


        [SerializeField] private bool resolveBreakingChangesAutomatically;
        [CreateProperty] public bool ResolveBreakingChangesAutomatically { get => resolveBreakingChangesAutomatically; set => resolveBreakingChangesAutomatically = value; }

        [SerializeField] private bool showPrereleases;
        [CreateProperty] public bool ShowPrereleases { get => showPrereleases; set => showPrereleases = value; }

        [CreateProperty] public DateTime LastRefreshTime { get; set; }
    }

}
