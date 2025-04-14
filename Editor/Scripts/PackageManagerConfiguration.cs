using System;
using System.Collections.Generic;
using System.Linq;

using Unity.Properties;

using UnityEngine;
using UnityEngine.Events;

namespace Reflectis.CreatorKit.Worlds.Setup.Editor
{
    [CreateAssetMenu(fileName = "CreatorKitSetupConfiguration", menuName = "Reflectis Worlds/Creator Kit/Setup/CreatorKitSetupConfiguration")]
    public class PackageManagerConfiguration : ScriptableObject
    {
        public PackageRegistry[] AllVersionsPackageRegistry { get; set; } = new PackageRegistry[0];

        public PackageDefinition[] SelectedVersionPackageList => AllVersionsPackageRegistry.FirstOrDefault(x => x.ReflectisVersion == DisplayedReflectisVersion).Packages;

        public Dictionary<string, PackageDefinition> SelectedVersionPackageDictionary => SelectedVersionPackageList.ToDictionary(x => x.Name);


        [CreateProperty] public PackageDefinition[] SelectedVersionPackageListFiltered => SelectedVersionPackageList.Where(x => x.Visibility == EPackageVisibility.Visible).ToArray();

        [CreateProperty]
        public List<string> AvailableVersions => AllVersionsPackageRegistry
                .Where(x => ShowPrereleases || x.ReflectisVersion != "develop")
                .Select(x => x.ReflectisVersion)
                .ToList();

        [CreateProperty] public List<PackageDefinition[]> SelectedVersionDependenciesFullOrdered => SelectedVersionDependenciesFull.Select(x => x.Value.Select(x => SelectedVersionPackageDictionary[x]).ToArray()).ToList();

        public Dictionary<string, string[]> SelectedVersionDependencies => AllVersionsPackageRegistry.FirstOrDefault(x => x.ReflectisVersion == DisplayedReflectisVersion).Dependencies;
        public Dictionary<string, string[]> SelectedVersionDependenciesFull => SelectedVersionDependencies.ToDictionary(
                kvp => kvp.Key,
                kvp => FindAllDependencies(SelectedVersionPackageDictionary[kvp.Key], new List<string>()).ToArray()
            );

        public Dictionary<string, List<string>> ReverseDependencies => InvertDictionary(SelectedVersionDependenciesFull);


        [SerializeField] private List<PackageDefinition> installedPackages = new();
        [CreateProperty] public List<PackageDefinition> InstalledPackages { get => installedPackages; set => installedPackages = value; }


        [SerializeField] private string currentInstallationVersion;
        [CreateProperty] public string CurrentInstallationVersion { get => currentInstallationVersion; set => currentInstallationVersion = value; }

        public UnityEvent OnDisplayedVersionChanged { get; } = new();

        private string displayedReflectisVersion;
        [CreateProperty]
        public string DisplayedReflectisVersion
        {
            get => displayedReflectisVersion;
            set
            {
                displayedReflectisVersion = value;
                OnDisplayedVersionChanged.Invoke();
            }
        }

        [CreateProperty] public bool DisplayedAndInstalledVersionsAreDifferent => CurrentInstallationVersion != DisplayedReflectisVersion;


        [SerializeField] private bool resolveBreakingChangesAutomatically;
        [CreateProperty] public bool ResolveBreakingChangesAutomatically { get => resolveBreakingChangesAutomatically; set => resolveBreakingChangesAutomatically = value; }

        [SerializeField] private bool showPrereleases;
        [CreateProperty] public bool ShowPrereleases { get => showPrereleases; set => showPrereleases = value; }

        [CreateProperty] public DateTime LastRefreshTime { get; set; }

        private List<string> FindAllDependencies(PackageDefinition package, List<string> dependencies)
        {
            if (SelectedVersionDependencies.TryGetValue(package.Name, out string[] packageDependencies))
            {
                foreach (string dependency in packageDependencies)
                {
                    FindAllDependencies(SelectedVersionPackageDictionary[dependency], dependencies);
                }
                dependencies.AddRange(packageDependencies);
            }

            return dependencies;
        }


        private Dictionary<string, List<string>> InvertDictionary(Dictionary<string, string[]> dictionary)
        {
            var invertedDictionary = new Dictionary<string, List<string>>();

            foreach (var kvp in dictionary)
            {
                foreach (var value in kvp.Value)
                {
                    if (!invertedDictionary.ContainsKey(value))
                    {
                        invertedDictionary[value] = new List<string>();
                    }
                    invertedDictionary[value].Add(kvp.Key);
                }
            }

            return invertedDictionary;
        }


    }

}
