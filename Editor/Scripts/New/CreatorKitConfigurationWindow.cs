using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;

using UnityEditor;
using UnityEditor.Build;

using UnityEditorInternal;

using UnityEngine;
using UnityEngine.UIElements;

namespace Reflectis.CreatorKit.Worlds.Installer.Editor
{
    public class CreatorKitConfigurationWindow : EditorWindow
    {
        [SerializeField]
        private VisualTreeAsset m_VisualTreeAsset = default;

        private VisualElement root;
        private CreatorKitConfigurationWindowDataSource dataSource;

        #region editor window setup

        private static bool isSetupping = false;
        private static bool setupCompleted = false;

        #endregion

        #region Project configuration

        private bool EditorConfigurationOk => dataSource.unityVersionIsMatching && dataSource.allEditorModulesInstalled;
        private bool ProjectSettingsOk => dataSource.renderPipelineURP && dataSource.playerSettings && dataSource.maxTextureSizeOverride;

        public static Dictionary<string, bool> installedModules = new()
        {
            { "Android", true },
            { "WebGL", true },
            { "Windows", true }
        };

        #endregion

        #region Package manager

        private readonly string packageRegistryPath = "https://spacsglobal.dfs.core.windows.net/reflectis2023-public/PackageManager/PackageRegistry.json";
        private readonly string breakingChangesSolverPath = "https://spacsglobal.dfs.core.windows.net/reflectis2023-public/PackageManager/BreakingChangesSolverIndex.json";

        private static Dictionary<(string, string), string> breakingChangesSolverDictionary;

        private int displayedReflectisVersionIndex;
        private static string displayedReflectisVersion;

        private List<string> availableVersions = new();

        private string previousInstallationVersion;

        private static List<PackageDefinition> selectedVersionPackageList = new(); //the selected version packages
        private static Dictionary<string, PackageDefinition> selectedVersionPackageDictionary = new();

        private static Dictionary<string, string[]> selectedVersionDependencies = new();
        private static Dictionary<string, string[]> selectedVersionDependenciesFull = new();
        private static Dictionary<string, List<string>> reverseDependencies = new();

        private static HashSet<PackageDefinition> installedPackages;
        private static Dictionary<string, bool> installationAsDependency = new();

        private static Dictionary<string, bool> packagesDropdown;
        private static Dictionary<string, bool> dependenciesDropdown;

        private readonly string currentInstallationKey = "SelectedReflectisVersion"; //string used to know which reflectis version is installed. Set The first time you press the setup button. 
        private readonly string installedPackagesKey = "InstalledPackages"; //string used to know which packages are installed.

        #endregion


        [MenuItem("Reflectis/Creator Kit configuration window")]
        public static void ShowWindow()
        {
            CreatorKitConfigurationWindow wnd = GetWindow<CreatorKitConfigurationWindow>();
            wnd.titleContent = new GUIContent("Creator Kit configuration window");

            wnd.LoadOrCreateDataSource();
            wnd.AddDataBindings();
        }


        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            root = rootVisualElement;

            // Instantiate UXML
            VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
            root.Add(labelFromUXML);

            SetupWindowData();
        }

        private void LoadOrCreateDataSource()
        {
            string folderPath = "Assets/CreatorKitInstallerData";
            string assetPath = $"{folderPath}/CreatorKitConfigurationWindowDataSource.asset";

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets", "CreatorKitInstallerData");
            }

            dataSource = AssetDatabase.LoadAssetAtPath<CreatorKitConfigurationWindowDataSource>(assetPath);

            if (dataSource == null)
            {
                dataSource = CreateInstance<CreatorKitConfigurationWindowDataSource>();
                AssetDatabase.CreateAsset(dataSource, assetPath);
                AssetDatabase.SaveAssets();
            }
        }

        private void AddDataBindings()
        {
            root.dataSource = dataSource;

            Label gitVersionLabel = root.Q<Label>("GitVersionLabel");
            dataSource.gitVersion = gitVersionLabel.text;
            gitVersionLabel.SetBinding("text", new DataBinding() { dataSourcePath = Unity.Properties.PropertyPath.FromName(nameof(CreatorKitConfigurationWindowDataSource.gitVersion)) });

            Button gitDownloadButton = root.Q<Button>("git-download-button");
            DataBinding buttonBinding = new() { dataSourcePath = Unity.Properties.PropertyPath.FromName(nameof(CreatorKitConfigurationWindowDataSource.isGitInstalled)) };
            buttonBinding.sourceToUiConverters.AddConverter((ref bool value) => !value);
            gitDownloadButton.SetBinding("visible", buttonBinding);

            Label currentReflectisVersionLabel = root.Q<Label>("current-reflectis-version-value");
            currentReflectisVersionLabel.SetBinding("text", new DataBinding() { dataSourcePath = Unity.Properties.PropertyPath.FromName(nameof(CreatorKitConfigurationWindowDataSource.currentInstallationVersion)) });

            //VisualElement projectSettingsURPCheck = root.Q<VisualElement>("project-settings-urp-check");
            //projectSettingsURPCheck.SetBinding("visible", new DataBinding() { dataSourcePath = Unity.Properties.PropertyPath.FromName(nameof(CreatorKitConfigurationWindowDataSource.renderPipelineURP)) });

            //VisualElement projectSettingsConfiguration = root.Q<VisualElement>("project-settings-configuration-check");
            //projectSettingsConfiguration.SetBinding("visible", new DataBinding() { dataSourcePath = Unity.Properties.PropertyPath.FromName(nameof(CreatorKitConfigurationWindowDataSource.playerSettings)) });

            //VisualElement projectSettingsMaxTextureSize = root.Q<VisualElement>("project-settings-max-texture-size-check");
            //projectSettingsMaxTextureSize.SetBinding("visible", new DataBinding() { dataSourcePath = Unity.Properties.PropertyPath.FromName(nameof(CreatorKitConfigurationWindowDataSource.maxTextureSizeOverride)) });

            VisualElement projectSettingsItemIcon = root.Q<VisualElement>("project-settings-configuration-check");
            DataBinding styleBinding = new() { dataSourcePath = Unity.Properties.PropertyPath.FromName(nameof(CreatorKitConfigurationWindowDataSource.maxTextureSizeOverride)) };
            styleBinding.sourceToUiConverters.AddConverter((ref bool value) =>
            {
                projectSettingsItemIcon.RemoveFromClassList("settings-item-green-icon");
                projectSettingsItemIcon.RemoveFromClassList("settings-item-red-icon");
                projectSettingsItemIcon.AddToClassList(value ? "settings-item-green-icon" : "settings-item-red-icon");
                return true;
            });
            projectSettingsItemIcon.SetBinding("classList", styleBinding);




            Label lastRefreshDateTimeLabel = root.Q<Label>("last-refresh-date-time");
            DataBinding lastRefreshDateTimeDataBinding = new() { dataSourcePath = Unity.Properties.PropertyPath.FromName(nameof(CreatorKitConfigurationWindowDataSource.lastRefreshDateTime)) };
            lastRefreshDateTimeDataBinding.sourceToUiConverters.AddConverter((ref string value) => $"{value}MMM dd, HH:mm");
            lastRefreshDateTimeLabel.SetBinding("text", lastRefreshDateTimeDataBinding);

            Button refreshPackagesButton = root.Q<Button>("refresh-packages-button");
            refreshPackagesButton.clicked += SetupWindowData;

            Toggle resolveBreakingChangesAutomatically = root.Q<Toggle>("resolve-breaking-changes-automatically");
            currentReflectisVersionLabel.SetBinding("text", new DataBinding()
            {
                dataSourcePath = Unity.Properties.PropertyPath.FromName(nameof(CreatorKitConfigurationWindowDataSource.currentInstallationVersion)),
                bindingMode = BindingMode.ToSource
            });
        }

        private async void SetupWindowData()
        {
            isSetupping = true;

            using HttpClient client = new();
            HttpResponseMessage response = await client.GetAsync(packageRegistryPath);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            dataSource.allVersionsPackageRegistry = JsonConvert.DeserializeObject<PackageRegistry[]>(responseBody);

            HttpResponseMessage routineResponse = await client.GetAsync(breakingChangesSolverPath);
            routineResponse.EnsureSuccessStatusCode();
            string routineResponseBody = await routineResponse.Content.ReadAsStringAsync();

            // Deserialize into a Dictionary<string, string>
            var dictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(routineResponseBody);
            // Convert the keys into tuples
            breakingChangesSolverDictionary = new Dictionary<(string, string), string>();
            foreach (var kvp in dictionary)
            {
                // Parse the key into a tuple
                var key = kvp.Key.Trim('(', ')').Split(", ");
                var tupleKey = (key[0].Trim('"'), key[1].Trim('"'));
                breakingChangesSolverDictionary[tupleKey] = kvp.Value;
            }

            availableVersions = dataSource.allVersionsPackageRegistry.Select(x => x.ReflectisVersion).ToList();

            //Get reflectis version and update list of packages
            dataSource.currentInstallationVersion = EditorPrefs.GetString(currentInstallationKey);
            previousInstallationVersion = dataSource.currentInstallationVersion;
            displayedReflectisVersion = dataSource.currentInstallationVersion;
            installedPackages = JsonConvert.DeserializeObject<HashSet<PackageDefinition>>(EditorPrefs.GetString(installedPackagesKey)) ?? new();
            if (string.IsNullOrEmpty(displayedReflectisVersion))
            {
                displayedReflectisVersion = dataSource.allVersionsPackageRegistry[^1].ReflectisVersion;
            }
            displayedReflectisVersionIndex = availableVersions.IndexOf(displayedReflectisVersion);


            packagesDropdown = selectedVersionPackageDictionary.Where(x => x.Value.Visibility == EPackageVisibility.Visible).ToDictionary(x => x.Value.Name, x => false);
            dependenciesDropdown = selectedVersionPackageDictionary.Where(x => x.Value.Visibility == EPackageVisibility.Visible).ToDictionary(x => x.Value.Name, x => false);

            dataSource.unityVersionIsMatching = InternalEditorUtility.GetFullUnityVersion().Split(' ')[0] == dataSource.allVersionsPackageRegistry[displayedReflectisVersionIndex].RequiredUnityVersion;
            dataSource.lastRefreshDateTime = DateTime.Now;

            UpdateDisplayedPacakgesAndDependencies();

            CheckGitInstallation();
            CheckEditorModulesInstallation();
            CheckProjectSettings();

            EditorUtility.SetDirty(dataSource);
            AssetDatabase.SaveAssetIfDirty(dataSource);

            setupCompleted = true;
            isSetupping = false;
        }

        private void RefreshWindow()
        {
            Repaint(); // Request Unity to redraw the window

            UpdateDisplayedPacakgesAndDependencies();

            CheckGitInstallation();
            CheckEditorModulesInstallation();
            CheckProjectSettings();
        }

        #region Project settings

        private void CheckGitInstallation()
        {
            try
            {
                ProcessStartInfo startInfo = new()
                {
                    FileName = "git",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using Process process = new() { StartInfo = startInfo };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    dataSource.gitVersion = string.Format(dataSource.gitVersion, output);
                    dataSource.isGitInstalled = true;
                }
                else
                {
                    dataSource.isGitInstalled = false;
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Git Check", "An error occurred while checking for Git:\n" + ex.Message, "OK");
            }
        }

        private void CheckEditorModulesInstallation()
        {
            //Check supported platforms
            BuildTargetGroup[] buildTargetGroups = (BuildTargetGroup[])Enum.GetValues(typeof(BuildTargetGroup));
            foreach (BuildTargetGroup group in buildTargetGroups)
            {
                installedModules["Android"] = BuildPipeline.IsBuildTargetSupported(group, BuildTarget.Android);
                installedModules["Windows"] = BuildPipeline.IsBuildTargetSupported(group, BuildTarget.StandaloneWindows);
                installedModules["WebGL"] = BuildPipeline.IsBuildTargetSupported(group, BuildTarget.WebGL);
            }
            dataSource.allEditorModulesInstalled = !installedModules.Values.Contains(false);
        }

        private void CheckProjectSettings()
        {
            dataSource.renderPipelineURP = GetURPConfigurationStatus();
            dataSource.playerSettings = GetProjectSettingsStatus();
            dataSource.maxTextureSizeOverride = GetMaxTextureSizeOverride();
        }

        private bool GetURPConfigurationStatus()
        {
            string[] guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineGlobalSettings");
            return guids.Length == 1 && guids[0] == "edf6e41e487713f45862ce6ae2f5dffd";
        }

        private void SetURPConfiguration()
        {
            string[] guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineGlobalSettings");

            if (guids.Length > 1)
            {
                foreach (string guid in guids.Where(x => x != "edf6e41e487713f45862ce6ae2f5dffd"))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    AssetDatabase.DeleteAsset(path);
                }
            }
        }

        private bool GetProjectSettingsStatus()
        {
            return PlayerSettings.GetApiCompatibilityLevel(NamedBuildTarget.Standalone) == ApiCompatibilityLevel.NET_Unity_4_8;
        }

        private void SetProjectSettings()
        {
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Unity_4_8);
        }

        private bool GetMaxTextureSizeOverride()
        {
            return EditorUserBuildSettings.overrideMaxTextureSize == 1024;
        }

        private void SetMaxTextureSizeOverride()
        {
            EditorUserBuildSettings.overrideMaxTextureSize = 1024;
            AssetDatabase.Refresh();
        }

        #endregion

        #region Package management

        private List<string> FindAllDependencies(PackageDefinition package, List<string> dependencies)
        {
            if (selectedVersionDependencies.TryGetValue(package.Name, out string[] packageDependencies))
            {
                foreach (string dependency in packageDependencies)
                {
                    FindAllDependencies(selectedVersionPackageDictionary[dependency], dependencies);
                }
                dependencies.AddRange(packageDependencies);
            }

            return dependencies;
        }

        private void UpdateDisplayedPacakgesAndDependencies()
        {
            selectedVersionPackageList = dataSource.allVersionsPackageRegistry.FirstOrDefault(x => x.ReflectisVersion == displayedReflectisVersion).Packages;
            selectedVersionDependencies = dataSource.allVersionsPackageRegistry.FirstOrDefault(x => x.ReflectisVersion == displayedReflectisVersion).Dependencies;

            selectedVersionPackageDictionary = selectedVersionPackageList.ToDictionary(x => x.Name);
            selectedVersionDependenciesFull = selectedVersionDependencies.ToDictionary(
                kvp => kvp.Key,
                kvp => FindAllDependencies(selectedVersionPackageDictionary[kvp.Key], new List<string>()).ToArray()
            );
            reverseDependencies = InvertDictionary(selectedVersionDependenciesFull);

            packagesDropdown = selectedVersionPackageDictionary.Where(x => x.Value.Visibility == EPackageVisibility.Visible).ToDictionary(x => x.Value.Name, x => false);
            dependenciesDropdown = selectedVersionPackageDictionary.Where(x => x.Value.Visibility == EPackageVisibility.Visible).ToDictionary(x => x.Value.Name, x => false);
        }

        private void InstallPackageWithDependencies(PackageDefinition package)
        {
            List<string> dependenciesToInstall = FindAllDependencies(package, new());

            installedPackages.UnionWith(dependenciesToInstall.Select(x => selectedVersionPackageDictionary[x]).Append(package));
            EditorPrefs.SetString(installedPackagesKey, JsonConvert.SerializeObject(installedPackages));

            InstallPackages(dependenciesToInstall.Append(package.Name).Select(x => selectedVersionPackageDictionary[x]).ToList());

            installationAsDependency = installedPackages.ToDictionary(x => x.Name, y => IsPackageInstalledAsDependency(y));
        }

        private void InstallPackages(List<PackageDefinition> toInstall)
        {
            string manifestFilePath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
            string manifestJson = File.ReadAllText(manifestFilePath);
            JObject manifestObj = JObject.Parse(manifestJson);

            JObject dependencies = (JObject)manifestObj["dependencies"];
            foreach (PackageDefinition p in toInstall)
            {
                dependencies[p.Name] = $"{p.Url}#{p.Version}";
            }

            File.WriteAllText(manifestFilePath, manifestObj.ToString());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshWindow();
        }

        private void UninstallPackageWithDependencies(PackageDefinition toUninstall)
        {
            installedPackages.Remove(installedPackages.FirstOrDefault(x => x.Name == toUninstall.Name));

            List<PackageDefinition> packagesToRemove = new() { toUninstall };

            foreach (var hiddenPackage in installedPackages.Where(x => x.Visibility == EPackageVisibility.Hidden))
            {
                if (reverseDependencies[hiddenPackage.Name].Intersect(installedPackages.Select(x => x.Name)).Count() == 0)
                {
                    packagesToRemove.Add(selectedVersionPackageDictionary[hiddenPackage.Name]);
                    installedPackages.Remove(selectedVersionPackageDictionary[hiddenPackage.Name]);
                }
            }

            EditorPrefs.SetString(installedPackagesKey, JsonConvert.SerializeObject(installedPackages));

            UninstallPackages(packagesToRemove.Select(x => x.Name).ToList());
        }

        private void UninstallPackages(List<string> toRemove)
        {
            string manifestFilePath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
            string manifestJson = File.ReadAllText(manifestFilePath);
            JObject manifestObj = JObject.Parse(manifestJson);

            JObject dependencies = (JObject)manifestObj["dependencies"];
            foreach (string pName in toRemove)
            {
                dependencies.Remove(pName);
            }

            File.WriteAllText(manifestFilePath, manifestObj.ToString());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshWindow();
        }

        public static Dictionary<string, List<string>> InvertDictionary(Dictionary<string, string[]> dictionary)
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

        private bool IsPackageInstalledAsDependency(PackageDefinition package)
        {
            bool isDependency = false;

            if (reverseDependencies.TryGetValue(package.Name, out List<string> deps))
            {
                foreach (string dep in deps)
                {
                    if (installedPackages.Select(x => x.Name).Contains(dep))
                    {
                        isDependency = true;
                        break;
                    }
                }
            }
            else
            {
                isDependency = false;
            }

            return isDependency;
        }

        private void UpdatePackagesToSelectedVersion()
        {
            if (dataSource.currentInstallationVersion != displayedReflectisVersion)
            {
                previousInstallationVersion = dataSource.currentInstallationVersion;
                dataSource.currentInstallationVersion = displayedReflectisVersion;
                EditorPrefs.SetString(currentInstallationKey, dataSource.currentInstallationVersion);

                Dictionary<string, PackageDefinition> packages = dataSource.allVersionsPackageRegistry
                    .FirstOrDefault(x => x.ReflectisVersion == dataSource.currentInstallationVersion).Packages
                    .ToDictionary(x => x.Name, y => y);

                List<PackageDefinition> installedPackagesCopy = new(installedPackages);
                foreach (PackageDefinition package in installedPackagesCopy)
                {
                    if (package.Version != selectedVersionPackageDictionary[package.Name].Version)
                    {
                        installedPackages.Remove(package);
                        UninstallPackages(new() { package.Name });

                        installedPackages.Add(selectedVersionPackageDictionary[package.Name]);
                        InstallPackages(new() { selectedVersionPackageDictionary[package.Name] });
                    }
                }

                EditorPrefs.SetString(installedPackagesKey, JsonConvert.SerializeObject(installedPackages));

                if (dataSource.resolveBreakingChangesAutomatically)
                {
                    ResolveBreakingChanges();
                }
            }
        }

        private async void ResolveBreakingChanges()
        {
            if (!AssetDatabase.IsValidFolder("Assets/CKBreakingChangesSolvers"))
                AssetDatabase.CreateFolder("Assets", "CKBreakingChangesSolvers");

            string prev = FilterPatch(previousInstallationVersion);
            string cur = FilterPatch(dataSource.currentInstallationVersion);
            (string, string) routineKey = (prev, cur);

            string routinePath = breakingChangesSolverDictionary[routineKey];

            using HttpClient client = new();
            HttpResponseMessage response = await client.GetAsync(routinePath);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();

            string assetPath = $"Assets/CKBreakingChangesSolvers/{routinePath.Split('/').Last()}";
            StreamWriter writer = new(assetPath, false);
            writer.WriteLine(responseBody);
            writer.Close();
            AssetDatabase.ImportAsset(assetPath);
        }

        public static void ResolveBreakingChangesCallback(string type)
        {
            Type myClassType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(assembly => assembly.GetTypes())
                    .FirstOrDefault(t => t.Name == type);

            try
            {
                myClassType.GetMethod("SolveBreakingChanges", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            }
            catch
            {
                UnityEngine.Debug.LogError($"Failed to load breaking changes solver: {myClassType}");
            }
        }

        private string FilterPatch(string input)
        {
            int index = input.LastIndexOf('.');
            if (index != -1)
                return input[..index];

            return input;
        }

        #endregion

        private Dictionary<string, string[]> TraverseGraph(Dictionary<string, PackageDefinition> graph)
        {
            HashSet<string> visited = new();
            Dictionary<string, string[]> outgoingEdges = new();

            foreach (var node in graph.Keys)
            {
                if (!visited.Contains(node))
                {
                    TraverseNode(node, graph, visited, outgoingEdges);
                }
            }

            return outgoingEdges;
        }

        private void TraverseNode(string node, Dictionary<string, PackageDefinition> graph, HashSet<string> visited, Dictionary<string, string[]> outgoingEdges)
        {
            if (visited.Contains(node))
            {
                return;
            }

            visited.Add(node);
            UnityEngine.Debug.Log($"Visiting node: {node}");

            if (selectedVersionDependencies.TryGetValue(node, out string[] dependencies))
            {
                outgoingEdges[node] = dependencies;
                foreach (var dependency in dependencies)
                {
                    TraverseNode(dependency, graph, visited, outgoingEdges);
                }
            }
            else
            {
                outgoingEdges[node] = Array.Empty<string>();
            }
        }
    }

    public class CustomAssetPostprocessor : AssetPostprocessor
    {
        // This method is called when importing any number of assets is completed
        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (var importedAsset in importedAssets)
            {
                if (!importedAsset.Contains("CKBreakingChangesSolvers"))
                    return;

                UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath(importedAsset, typeof(MonoScript));
                if (obj != null)
                {
                    SetupReflectisWindow.ResolveBreakingChangesCallback(importedAsset.Split('/').Last()[..^3]);
                }
            }
        }
    }
}