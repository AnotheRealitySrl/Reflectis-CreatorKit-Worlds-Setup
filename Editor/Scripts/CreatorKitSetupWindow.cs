using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

using Unity.Properties;

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;

using UnityEditorInternal;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Reflectis.CreatorKit.Worlds.Setup.Editor
{
    public class CreatorKitSetupWindow : EditorWindow
    {
        [Serializable]
        public class ProjectConfiguration
        {
            [CreateProperty] public bool IsGitInstalled { get; set; }
            [CreateProperty] public string GitVersion { get; set; }

            [CreateProperty] public bool EditorConfigurationOk => UnityVersionIsMatching && AllEditorModulesInstalled;
            [CreateProperty] public bool UnityVersionIsMatching { get; set; }
            [CreateProperty] public bool AllEditorModulesInstalled { get; set; }

            [CreateProperty]
            public Dictionary<string, bool> InstalledModules = new()
            {
                { "Android", true },
                { "WebGL", true },
                { "Windows", true }
            };


            [CreateProperty] public bool ProjectSettingsOk => RenderPipelineURP && PlayerSettings && MaxTextureSizeOverride;
            [CreateProperty] public bool RenderPipelineURP { get; set; }
            [CreateProperty] public bool PlayerSettings { get; set; }
            [CreateProperty] public bool MaxTextureSizeOverride { get; set; }
        }

        [SerializeField] private VisualTreeAsset m_VisualTreeAsset = default;
        [SerializeField] private VisualTreeAsset packageItemAsset = default;
        [SerializeField] private VisualTreeAsset packageDependencyAsset = default;
        [SerializeField] private VisualTreeAsset dialogAsset = default;

        [SerializeField] private RenderPipelineGlobalSettings renderPipelineGlobalSettings;
        [SerializeField] private RenderPipelineAsset renderPipelineAsset;

        private VisualElement root;

        private readonly ProjectConfiguration projectConfig = new();
        private PackageManagerConfiguration packageManagerConfig;

        private const string utilities_folder_path = "Assets/CreatorKit/Editor/Scripts";
        private const string settings_folder_path = "Assets/CreatorKit/Editor/Settings";
        private const string setup_configuration_path = "CreatorKitSetupConfiguration.asset";

        #region Editor window setup

        private static bool isSetupping = false;
        private static bool setupCompleted = false;

        #endregion

        #region Project configuration

        private string UnityVersion => InternalEditorUtility.GetFullUnityVersion().Split(' ')[0];

        #endregion

        #region Package manager

        private const string package_registry_path = "https://spacsglobal.dfs.core.windows.net/reflectis2023-public/PackageManager/PackageRegistry.json";
        private const string breaking_changes_solver_path = "https://spacsglobal.dfs.core.windows.net/reflectis2023-public/PackageManager/BreakingChangesSolverIndex.json";

        private static Dictionary<(string, string), string> breakingChangesSolverDictionary;

        private string previousInstallationVersion;

        #endregion

        [MenuItem("Reflectis Worlds/Creator Kit/Setup/Setup project")]
        public static void ShowWindow()
        {
            CreatorKitSetupWindow wnd = GetWindow<CreatorKitSetupWindow>();
            wnd.titleContent = new GUIContent("Setup project");
        }

        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            root = rootVisualElement;

            // Instantiate UXML
            VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
            root.Add(labelFromUXML);

            InitializeWindow();
        }

        private void OnApplicationQuit()
        {
            SaveAsset(packageManagerConfig);
        }

        private void OnDestroy()
        {
            SaveAsset(packageManagerConfig);
        }

        private async void InitializeWindow()
        {
            await LoadData();
            AddDataBindings();
        }

        private async Task LoadData()
        {
            isSetupping = true;

            string packageManagerAssetGuid = AssetDatabase.FindAssets("t:" + typeof(PackageManagerConfiguration).Name).ToList().FirstOrDefault();
            packageManagerConfig = AssetDatabase.LoadAssetAtPath<PackageManagerConfiguration>(AssetDatabase.GUIDToAssetPath(packageManagerAssetGuid));

            if (packageManagerConfig == null)
            {
                EnsureFolderExists(settings_folder_path);

                packageManagerConfig = CreateInstance<PackageManagerConfiguration>();
                string settingsAssetPath = $"{settings_folder_path}/{setup_configuration_path}";
                AssetDatabase.CreateAsset(packageManagerConfig, settingsAssetPath);
                AssetDatabase.SaveAssets();
            }

            using HttpClient client = new();
            HttpResponseMessage response = await client.GetAsync(package_registry_path);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            packageManagerConfig.AllVersionsPackageRegistry = JsonConvert.DeserializeObject<PackageRegistry[]>(responseBody);

            HttpResponseMessage routineResponse = await client.GetAsync(breaking_changes_solver_path);
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

            packageManagerConfig.OnDisplayedVersionChanged.AddListener(InstantiatePackagesInPackageList);

            UpdateAvailableVersions();

            //Get reflectis version and update list of packages
            if (string.IsNullOrEmpty(packageManagerConfig.CurrentInstallationVersion))
            {
                packageManagerConfig.CurrentInstallationVersion = packageManagerConfig.AvailableVersions[^1];
            }
            if (string.IsNullOrEmpty(packageManagerConfig.DisplayedReflectisVersion))
            {
                packageManagerConfig.DisplayedReflectisVersion = !string.IsNullOrEmpty(packageManagerConfig.CurrentInstallationVersion) ? packageManagerConfig.CurrentInstallationVersion : packageManagerConfig.AvailableVersions[^1];
            }
            previousInstallationVersion = packageManagerConfig.CurrentInstallationVersion;


            projectConfig.UnityVersionIsMatching = UnityVersion == packageManagerConfig.AllVersionsPackageRegistry.FirstOrDefault(x => x.ReflectisVersion == packageManagerConfig.CurrentInstallationVersion).RequiredUnityVersion;
            packageManagerConfig.LastRefreshTime = DateTime.Now;

            CheckGitInstallation();
            CheckEditorModulesInstallation();
            CheckProjectSettings();

            SaveAsset(packageManagerConfig);

            setupCompleted = true;
            isSetupping = false;
        }


        private void AddDataBindings()
        {
            #region Project settings section

            VisualElement projectSettingsSection = root.Q<VisualElement>("project-settings");
            projectSettingsSection.dataSource = projectConfig;

            List<(string, string)> settingIcons = new()
            {
                { ("project-settings-git-version-check", nameof(projectConfig.IsGitInstalled)) },
                { ("project-settings-unity-version-check", nameof(projectConfig.UnityVersionIsMatching)) },
                { ("project-settings-editor-modules-check", nameof(projectConfig.AllEditorModulesInstalled)) },
                { ("project-settings-urp-check", nameof(projectConfig.RenderPipelineURP)) },
                { ("project-settings-configuration-check", nameof(projectConfig.PlayerSettings)) },
                { ("project-settings-max-texture-size-check", nameof(projectConfig.MaxTextureSizeOverride)) },
            };
            foreach (var entry in settingIcons)
            {
                VisualElement projectSettingsItemIcon = projectSettingsSection.Q<VisualElement>(entry.Item1);
                DataBinding styleBinding = new() { dataSourcePath = PropertyPath.FromName(entry.Item2) };
                styleBinding.sourceToUiConverters.AddConverter((ref bool value) =>
                {
                    projectSettingsItemIcon.RemoveFromClassList("settings-item-green-icon");
                    projectSettingsItemIcon.RemoveFromClassList("settings-item-red-icon");
                    projectSettingsItemIcon.AddToClassList(value ? "settings-item-green-icon" : "settings-item-red-icon");
                    return true;
                });
                // Find the binding that changes directly the class
                projectSettingsItemIcon.SetBinding(nameof(projectSettingsItemIcon.visible), styleBinding);
            }

            List<(string, string)> warningIcons = new()
            {
                { ("git-installation-warning", nameof(projectConfig.IsGitInstalled)) },
                { ("editor-configuration-warning", nameof(projectConfig.EditorConfigurationOk)) },
                { ("project-settings-warning", nameof(projectConfig.ProjectSettingsOk)) }
            };
            foreach (var entry in warningIcons)
            {
                VisualElement warningIcon = projectSettingsSection.Q<VisualElement>(entry.Item1);
                DataBinding warningIconVisibilityBinding = new()
                {
                    dataSourcePath = PropertyPath.FromName(entry.Item2),
                    bindingMode = BindingMode.ToTarget
                };
                warningIconVisibilityBinding.sourceToUiConverters.AddConverter((ref bool value) => false);
                warningIcon.SetBinding(nameof(warningIcon.visible), warningIconVisibilityBinding);
            }


            Label gitVersionLabel = projectSettingsSection.Q<Label>("git-version-label");
            DataBinding gitVersionLabelBinding = new() { dataSourcePath = PropertyPath.FromName(nameof(projectConfig.IsGitInstalled)), bindingMode = BindingMode.ToTarget };
            gitVersionLabelBinding.sourceToUiConverters.AddConverter((ref bool value) =>
                value ?
                    "Installed Git version: " :
                    "Git is not installed! Click \"Download\" button to download it from the official website.");
            gitVersionLabel.SetBinding(nameof(gitVersionLabel.text), gitVersionLabelBinding);

            Label gitVersionLabelValue = projectSettingsSection.Q<Label>("git-version-label-value");
            gitVersionLabelValue.SetBinding(nameof(gitVersionLabelValue.text), new DataBinding() { dataSourcePath = PropertyPath.FromName(nameof(projectConfig.GitVersion)) });

            Button gitDownloadButton = projectSettingsSection.Q<Button>("git-download-button");
            gitDownloadButton.clicked += () => Application.OpenURL("https://git-scm.com/downloads");
            DataBinding gitDownloadBinding = new() { dataSourcePath = PropertyPath.FromName(nameof(projectConfig.IsGitInstalled)) };
            gitDownloadBinding.sourceToUiConverters.AddConverter((ref bool value) => !value);
            gitDownloadButton.SetBinding(nameof(gitDownloadButton.enabledSelf), gitDownloadBinding);

            Label currentUnityVersionValue = projectSettingsSection.Q<Label>("editor-settings-unity-version-value");
            currentUnityVersionValue.text = UnityVersion;

            Label installedModules = projectSettingsSection.Q<Label>("installed-modules-label");
            DataBinding installedModulesBinding = new() { dataSourcePath = PropertyPath.FromName(nameof(projectConfig.InstalledModules)) };
            installedModulesBinding.sourceToUiConverters.AddConverter((ref Dictionary<string, bool> value) =>
                !value.Values.Contains(false) ?
                    "All editor modules are installed properly" :
                    $"The following modules are missing: {string.Join(", ", value.Where(x => !x.Value).Select(x => x.Key))}. Install them from Unity Hub."
            );
            installedModules.SetBinding(nameof(installedModules.text), installedModulesBinding);

            Button configureProjectSettingsButton = projectSettingsSection.Q<Button>("configure-project-settings-button");
            configureProjectSettingsButton.clicked += ConfigureProjectSettings;
            DataBinding configureProjectSettingsButtonBinding = new() { dataSourcePath = PropertyPath.FromName(nameof(projectConfig.ProjectSettingsOk)) };
            configureProjectSettingsButtonBinding.sourceToUiConverters.AddConverter((ref bool value) => !value);
            configureProjectSettingsButton.SetBinding(nameof(gitDownloadButton.enabledSelf), configureProjectSettingsButtonBinding);

            #endregion

            #region Package manager section

            VisualElement packageManagerSection = root.Q<VisualElement>("package-manager");
            packageManagerSection.dataSource = packageManagerConfig;

            Label lastRefreshDateTimeLabel = packageManagerSection.Q<Label>("last-refresh-date-time");
            DataBinding lastRefreshDateTimeDataBinding = new()
            {
                dataSourcePath = PropertyPath.FromName(nameof(packageManagerConfig.LastRefreshTime)),
                bindingMode = BindingMode.ToTarget
            };
            lastRefreshDateTimeDataBinding.sourceToUiConverters.AddConverter((ref DateTime value) => value.ToString("MMM dd, HH:mm", CultureInfo.InvariantCulture));
            lastRefreshDateTimeLabel.SetBinding(nameof(lastRefreshDateTimeLabel.text), lastRefreshDateTimeDataBinding);

            Label currentReflectisVersionValue = packageManagerSection.Q<Label>("current-reflectis-version-value");
            currentReflectisVersionValue.SetBinding(nameof(currentReflectisVersionValue.text), new DataBinding()
            {
                dataSourcePath = PropertyPath.FromName(nameof(packageManagerConfig.CurrentInstallationVersion)),
                bindingMode = BindingMode.ToTarget
            });

            Button refreshPackagesButton = packageManagerSection.Q<Button>("refresh-packages-button");
            refreshPackagesButton.clicked += SetupWindowData;

            InstantiatePackagesInPackageList();

            Button updatePackagesButton = packageManagerSection.Q<Button>("update-packages-button");
            updatePackagesButton.SetBinding(nameof(updatePackagesButton.enabledSelf), new DataBinding()
            {
                dataSourcePath = PropertyPath.FromName(nameof(packageManagerConfig.DisplayedAndInstalledVersionsAreDifferent)),
                bindingMode = BindingMode.TwoWay
            });
            updatePackagesButton.clicked += () => ShowAlertDialog("Warning", "Packages will be updated to the desired version", UpdatePackagesToSelectedVersion);

            DropdownField dropdown = packageManagerSection.Q<DropdownField>("reflectis-version-dropdown");
            dropdown.SetBinding(nameof(dropdown.choices), new DataBinding()
            {
                dataSourcePath = PropertyPath.FromName(nameof(packageManagerConfig.AvailableVersions)),
                bindingMode = BindingMode.TwoWay
            });
            dropdown.SetBinding(nameof(dropdown.value), new DataBinding()
            {
                dataSourcePath = PropertyPath.FromName(nameof(packageManagerConfig.DisplayedReflectisVersion)),
                bindingMode = BindingMode.TwoWay
            });

            Toggle showPrereleaseToggle = packageManagerSection.Q<Toggle>("show-prereleases-toggle");
            showPrereleaseToggle.SetBinding(nameof(showPrereleaseToggle.value), new DataBinding()
            {
                dataSourcePath = PropertyPath.FromName(nameof(packageManagerConfig.ShowPrereleases)),
                bindingMode = BindingMode.TwoWay
            });
            showPrereleaseToggle.RegisterValueChangedCallback(evt => UpdateAvailableVersions());

            Toggle resolveBreakingChangesAutomatically = packageManagerSection.Q<Toggle>("resolve-breaking-changes-toggle");
            resolveBreakingChangesAutomatically.SetBinding(nameof(resolveBreakingChangesAutomatically.value), new DataBinding()
            {
                dataSourcePath = PropertyPath.FromName(nameof(packageManagerConfig.ResolveBreakingChangesAutomatically)),
                bindingMode = BindingMode.TwoWay
            });

            VisualElement resolveBreakingChangesAutomaticallyWarning = packageManagerSection.Q<VisualElement>("resolve-breaking-changes-warning");
            resolveBreakingChangesAutomaticallyWarning.SetBinding(nameof(resolveBreakingChangesAutomaticallyWarning.visible), new DataBinding()
            {
                dataSourcePath = PropertyPath.FromName(nameof(packageManagerConfig.ResolveBreakingChangesAutomatically)),
                bindingMode = BindingMode.ToTarget
            });

            #endregion
        }

        private void InstantiatePackagesInPackageList()
        {
            ScrollView packagesListScroll = root.Q<ListView>("packages-list-view").Q<ScrollView>();
            packagesListScroll.Clear();

            for (int i = 0; i < packageManagerConfig.SelectedVersionPackageListFiltered.Count(); i++)
            {
                VisualElement packageItem = packageItemAsset.Instantiate();
                packagesListScroll.Add(packageItem);
                packagesListScroll[i].dataSourcePath = PropertyPath.FromIndex(i);

                Foldout packageName = packagesListScroll[i].Q<Foldout>("package-item");
                packageName.text = $"<b>{packageManagerConfig.SelectedVersionPackageListFiltered[i].DisplayName}</b> - {packageManagerConfig.SelectedVersionPackageListFiltered[i].Version}";

                Label packageDescription = packagesListScroll[i].Q<Label>("package-description");
                packageDescription.text = packageManagerConfig.SelectedVersionPackageListFiltered[i].Description;

                Label packageVersion = packagesListScroll[i].Q<Label>("package-url");
                packageVersion.text = $"<i><a href=\"{packageManagerConfig.SelectedVersionPackageListFiltered[i].Url}\">{packageManagerConfig.SelectedVersionPackageListFiltered[i].Url}</a></i>";
                packageVersion.RegisterCallback<ClickEvent>(evt => Application.OpenURL(packageManagerConfig.SelectedVersionPackageListFiltered[i].Url));


                VisualElement dependenciesList = packagesListScroll[i].Q<VisualElement>("package-dependencies");
                dependenciesList.dataSource = packageManagerConfig.SelectedVersionDependenciesFullOrdered[i];

                for (int j = 0; j < packageManagerConfig.SelectedVersionDependenciesFullOrdered[i].Count(); j++)
                {
                    VisualElement packageDependency = packageDependencyAsset.Instantiate();
                    dependenciesList.Add(packageDependency);
                    packageDependency.dataSourcePath = PropertyPath.FromIndex(j);

                    Label dependencyText = packageDependency.Q<Label>("package-dependency-label");
                    dependencyText.SetBinding(nameof(dependencyText.text), new DataBinding()
                    {
                        dataSourcePath = PropertyPath.FromName(nameof(PackageDefinition.DisplayName)),
                        bindingMode = BindingMode.ToTarget
                    });

                    Label dependencyVersion = packageDependency.Q<Label>("package-dependency-version");
                    dependencyVersion.SetBinding(nameof(dependencyVersion.text), new DataBinding()
                    {
                        dataSourcePath = PropertyPath.FromName(nameof(PackageDefinition.Version)),
                        bindingMode = BindingMode.ToTarget
                    });
                }

                Button installPackageButton = packagesListScroll[i].Q<Button>("install-package-button");

                DataBinding installPackageButtonBinding = new() { bindingMode = BindingMode.ToTarget };
                installPackageButtonBinding.sourceToUiConverters.AddConverter((ref PackageDefinition package) => packageManagerConfig.InstalledPackages.Select(x => x.Name).Contains(package.Name) ? "Uninstall" : "Install");
                installPackageButton.SetBinding(nameof(installPackageButton.text), installPackageButtonBinding);

                DataBinding installPackageButtonVisibilityBinding = new() { bindingMode = BindingMode.ToTarget };
                installPackageButtonVisibilityBinding.sourceToUiConverters.AddConverter((ref PackageDefinition package) => !IsPackageInstalledAsDependency(package) && !packageManagerConfig.DisplayedAndInstalledVersionsAreDifferent);
                installPackageButton.SetBinding(nameof(installPackageButton.enabledSelf), installPackageButtonVisibilityBinding);

                PackageDefinition package = packageManagerConfig.SelectedVersionPackageListFiltered[i];
                installPackageButton.clicked += () =>
                {
                    if (packageManagerConfig.InstalledPackages.Select(x => x.Name).Contains(package.Name))
                        UninstallPackageWithDependencies(package);
                    else
                        InstallPackageWithDependencies(package);
                };
            }
        }

        private async void SetupWindowData() => await LoadData();

        private void UpdateAvailableVersions()
        {
            if (packageManagerConfig.DisplayedReflectisVersion == "develop" && !packageManagerConfig.ShowPrereleases)
            {
                packageManagerConfig.DisplayedReflectisVersion = packageManagerConfig.AvailableVersions[^1];
            }
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
                    projectConfig.GitVersion = output;
                    projectConfig.IsGitInstalled = true;
                }
                else
                {
                    projectConfig.IsGitInstalled = false;
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
                projectConfig.InstalledModules["Android"] = BuildPipeline.IsBuildTargetSupported(group, BuildTarget.Android);
                projectConfig.InstalledModules["Windows"] = BuildPipeline.IsBuildTargetSupported(group, BuildTarget.StandaloneWindows);
                projectConfig.InstalledModules["WebGL"] = BuildPipeline.IsBuildTargetSupported(group, BuildTarget.WebGL);
            }
            projectConfig.AllEditorModulesInstalled = !projectConfig.InstalledModules.Values.Contains(false);
        }

        private void CheckProjectSettings()
        {
            projectConfig.RenderPipelineURP = GetURPConfigurationStatus();
            projectConfig.PlayerSettings = GetProjectSettingsStatus();
            projectConfig.MaxTextureSizeOverride = GetMaxTextureSizeOverride();
        }

        private bool GetURPConfigurationStatus()
        {
            return GraphicsSettings.defaultRenderPipeline == renderPipelineAsset && QualitySettings.renderPipeline == renderPipelineAsset;
        }

        private bool GetProjectSettingsStatus()
        {
            return PlayerSettings.GetApiCompatibilityLevel(NamedBuildTarget.Standalone) == ApiCompatibilityLevel.NET_Unity_4_8;
        }

        private bool GetMaxTextureSizeOverride()
        {
            return EditorUserBuildSettings.overrideMaxTextureSize == 1024;
        }

        private void ConfigureProjectSettings()
        {
            // URP configuration
            GraphicsSettings.defaultRenderPipeline = renderPipelineAsset;
            QualitySettings.renderPipeline = renderPipelineAsset;

            // Project settings configuration
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Unity_4_8);

            // Max texture size override
            EditorUserBuildSettings.overrideMaxTextureSize = 1024;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            CheckProjectSettings();
        }

        #endregion

        #region Package management

        private void InstallPackageWithDependencies(PackageDefinition package)
        {
            string[] dependenciesToInstall = packageManagerConfig.SelectedVersionDependenciesFull[package.Name];

            foreach (var dependency in dependenciesToInstall.Select(x => packageManagerConfig.SelectedVersionPackageDictionary[x]).Append(package))
            {
                if (!packageManagerConfig.InstalledPackages.Contains(dependency))
                {
                    packageManagerConfig.InstalledPackages.Add(dependency);
                }
            }

            EditorUtility.SetDirty(packageManagerConfig);
            AssetDatabase.SaveAssetIfDirty(packageManagerConfig);

            InstallPackages(dependenciesToInstall.Append(package.Name).Select(x => packageManagerConfig.SelectedVersionPackageDictionary[x]).ToList());

            Client.Resolve();
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
        }

        private void UninstallPackageWithDependencies(PackageDefinition toUninstall)
        {
            packageManagerConfig.InstalledPackages.Remove(packageManagerConfig.InstalledPackages.FirstOrDefault(x => x.Name == toUninstall.Name));
            EditorUtility.SetDirty(packageManagerConfig);
            AssetDatabase.SaveAssetIfDirty(packageManagerConfig);

            List<PackageDefinition> packagesToRemove = new() { toUninstall };

            foreach (var hiddenPackage in packageManagerConfig.InstalledPackages.Where(x => x.Visibility == EPackageVisibility.Hidden))
            {
                if (packageManagerConfig.ReverseDependencies[hiddenPackage.Name].Intersect(packageManagerConfig.InstalledPackages.Select(x => x.Name)).Count() == 0)
                {
                    packagesToRemove.Add(packageManagerConfig.SelectedVersionPackageDictionary[hiddenPackage.Name]);
                }
            }

            foreach (PackageDefinition package in packagesToRemove)
            {
                packageManagerConfig.InstalledPackages.Remove(package);
            }

            UninstallPackages(packagesToRemove.Select(x => x.Name).ToList());

            Client.Resolve();
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
        }

        private bool IsPackageInstalledAsDependency(PackageDefinition package)
        {
            bool isDependency = false;

            if (packageManagerConfig.ReverseDependencies.TryGetValue(package.Name, out List<string> deps))
            {
                foreach (string dep in deps)
                {
                    if (packageManagerConfig.InstalledPackages.Select(x => x.Name).Contains(dep))
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
            if (packageManagerConfig.CurrentInstallationVersion != packageManagerConfig.DisplayedReflectisVersion)
            {
                previousInstallationVersion = packageManagerConfig.CurrentInstallationVersion;
                packageManagerConfig.CurrentInstallationVersion = packageManagerConfig.DisplayedReflectisVersion;

                Dictionary<string, PackageDefinition> packages = packageManagerConfig.AllVersionsPackageRegistry
                    .FirstOrDefault(x => x.ReflectisVersion == packageManagerConfig.CurrentInstallationVersion).Packages
                    .ToDictionary(x => x.Name, y => y);

                List<PackageDefinition> installedPackagesCopy = new(packageManagerConfig.InstalledPackages);
                foreach (PackageDefinition package in installedPackagesCopy)
                {
                    if (package.Version != packageManagerConfig.SelectedVersionPackageDictionary[package.Name].Version)
                    {
                        packageManagerConfig.InstalledPackages.Remove(package);
                        UninstallPackages(new() { package.Name });

                        packageManagerConfig.InstalledPackages.Add(packageManagerConfig.SelectedVersionPackageDictionary[package.Name]);
                        InstallPackages(new() { packageManagerConfig.SelectedVersionPackageDictionary[package.Name] });
                    }
                }

                if (packageManagerConfig.ResolveBreakingChangesAutomatically)
                {
                    ResolveBreakingChanges();
                }
            }
        }

        private async void ResolveBreakingChanges()
        {
            EnsureFolderExists(utilities_folder_path);

            string prev = FilterPatch(previousInstallationVersion);
            string cur = FilterPatch(packageManagerConfig.CurrentInstallationVersion);
            (string, string) routineKey = (prev, cur);

            string routinePath = breakingChangesSolverDictionary[routineKey];

            using HttpClient client = new();
            HttpResponseMessage response = await client.GetAsync(routinePath);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();

            string assetPath = $"{utilities_folder_path}/{routinePath.Split('/').Last()}";
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

        private void SaveAsset(UnityEngine.Object asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        #endregion

        private void ShowAlertDialog(string title, string message, UnityAction callback)
        {
            var dialog = dialogAsset.CloneTree();

            var titleLabel = dialog.Q<Label>("dialog-title");
            titleLabel.text = title;

            var messageLabel = dialog.Q<Label>("dialog-message");
            messageLabel.text = message;

            var confirmButton = dialog.Q<Button>("dialog-confirm-button");
            confirmButton.clicked += () =>
            {
                root.Remove(dialog);
                callback?.Invoke();
            };

            var backButton = dialog.Q<Button>("dialog-back-button");
            backButton.clicked += () => root.Remove(dialog);

            //// Set dialog position to absolute and center it
            dialog.style.position = Position.Absolute;
            //dialog.style.left = Length.Percent(50);
            //dialog.style.top = Length.Percent(50);

            root.Add(dialog);
        }

        private void EnsureFolderExists(string folderPath)
        {
            string[] folders = folderPath.Split('/');
            string currentPath = "";

            foreach (string folder in folders)
            {
                currentPath = Path.Combine(currentPath, folder);
                if (!AssetDatabase.IsValidFolder(currentPath))
                {
                    AssetDatabase.CreateFolder(Path.GetDirectoryName(currentPath), Path.GetFileName(currentPath));
                }
            }
        }
    }

}