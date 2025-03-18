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
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

using UnityEditorInternal;

using UnityEngine;
using UnityEngine.Events;

namespace Reflectis.CreatorKit.Worlds.Installer.Editor
{
    [InitializeOnLoad]
    public class SetupReflectisWindow : EditorWindow
    {
        #region Reflectis JSON data variables

        private readonly string packageRegistryPath = "https://spacsglobal.dfs.core.windows.net/reflectis2023-public/PackageManager/PackageRegistry.json";
        private readonly string breakingChangesSolverPath = "https://spacsglobal.dfs.core.windows.net/reflectis2023-public/PackageManager/BreakingChangesSolverIndex.json";

        private readonly string playerPrefsVersionKey = "SelectedReflectisVersion"; //string used to know which reflectis version is installed. Set The first time you press the setup button. 
        private readonly string installedPackagesKey = "InstalledPackages"; //string used to know which packages are installed.

        private static PackageRegistry[] allVersionsPackageRegistries;
        private static Dictionary<(string, string), string> breakingChangesSolverDictionary;

        #endregion

        private int displayedReflectisVersionIndex;
        private static string displayedReflectisVersion;

        #region booleanValues

        private bool isGitInstalled = false;
        private string gitVersion = string.Empty;

        private bool allPlatformFixed = true;

        private bool renderPipelineURP = false;
        private bool projectSettings = false;
        private bool maxTextureSizeOverride = false;

        private bool allSettingsFixed = true;

        private bool setupCompleted = false;

        private bool resolveBreakingChangesAutomatically = true;

        #endregion

        #region Package and platform lists

        public Dictionary<string, bool> installedModules = new()
        {
            { "Android", true },
            { "WebGL", true },
            { "Windows", true }
        };

        #endregion

        #region Packages details

        private List<string> availableVersions = new();

        private string currentInstallationVersion;
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

        #endregion

        #region GUIElements

        //GUI Styles
        GUIStyle iconStyle;
        GUIStyle titleStyle;
        GUIStyle labelStyle;
        GUIStyle headerStyle;
        GUIStyle paragraphStyle;
        GUIStyle arrowStyle;
        GUIStyle configureAllStyle;
        GUIStyle[] lineStyles; // Different line styles for alternating colors
        GUIStyle boldTabStyle;
        GUIStyle toggleStyle;
        GUIStyle optionalToggleStyle;
        GUIContent warningIconContent;
        GUIContent errorIconContent;
        GUIContent confirmedIcon;

        private bool showPlatformSupport = false;
        private bool showProjectSettings = false;

        private Vector2 scrollPosition = Vector2.zero;
        private Rect lineRect;

        #endregion

        private AddRequest addRequest;

        //this variable is set by the other packages (like CK, used to show buttons and other GUI elements).
        public static UnityEvent configurationEvents = new();

        [MenuItem("Reflectis/Setup Window")]
        public static void ShowWindow()
        {
            GetWindow(typeof(SetupReflectisWindow));
        }

        #region Unity callbacks

        private void Awake()
        {
            SetupWindowData();
        }

        #endregion

        #region GUI

        private void OnGUI()
        {
            if (!setupCompleted)
                return;

            #region GUI Styles definition

            // Define the GUIStyle
            //----------------------------------------------------
            iconStyle = new(EditorStyles.label);
            titleStyle = new GUIStyle(GUI.skin.label);
            paragraphStyle = new GUIStyle(GUI.skin.label);
            labelStyle = new(EditorStyles.label)
            {
                richText = true,
            };
            headerStyle = new GUIStyle(GUI.skin.label);
            arrowStyle = new GUIStyle(GUI.skin.label);
            lineStyles = new GUIStyle[] { GUI.skin.box, GUI.skin.textField }; // Different line styles for alternating colors
            configureAllStyle = new GUIStyle(GUI.skin.button) { margin = new RectOffset(0, 7, 0, 0) };
            iconStyle.richText = true;
            titleStyle.fontSize = 20;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.fontSize = 12;
            headerStyle.fontStyle = FontStyle.Bold;
            arrowStyle.fontSize = 16;
            paragraphStyle.fontSize = 14;
            paragraphStyle.fontStyle = FontStyle.Bold;
            lineRect = EditorGUILayout.GetControlRect(false, 1);

            warningIconContent = EditorGUIUtility.IconContent("DotFill");
            errorIconContent = EditorGUIUtility.IconContent("console.erroricon");
            confirmedIcon = EditorGUIUtility.IconContent("Installed@2x"); //Assets / Editor Default Resources/ Icons

            toggleStyle = new GUIStyle("Foldout")
            {
                fixedHeight = 18f,
                fontStyle = FontStyle.Bold
            };

            optionalToggleStyle = new GUIStyle("Foldout")
            {
                fixedHeight = 16f,
                fontStyle = FontStyle.Italic,
                margin = new RectOffset(15, 0, 0, 0)
            };

            // Create a custom GUIStyle for the toolbar buttons with bold font
            boldTabStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                fontStyle = FontStyle.Bold
            };

            GUIStyle lineStyle = lineStyles[0];
            //-----------------------------------------------------

            #endregion

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, false, false);

            // Draw the title text with the specified GUIStyle
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Creator Kit configuration window".ToUpper(), titleStyle);
            EditorGUILayout.Space();
            EditorGUI.DrawRect(lineRect, Color.black);
            EditorGUILayout.Space(20);

            EditorGUILayout.BeginVertical();
            EditorGUILayout.Space(20);

            #region Project configuration

            EditorGUILayout.LabelField("Project configuration", paragraphStyle);
            EditorGUILayout.Space(10);

            #region Git installation

            //Github part
            GUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(new GUIContent(isGitInstalled ? confirmedIcon.image : errorIconContent.image), GUILayout.Width(20));
            GUILayout.Label("Git executable " + gitVersion, labelStyle);
            GUILayout.FlexibleSpace();
            if (isGitInstalled)
                GUI.enabled = false;
            if (GUILayout.Button(new GUIContent("Install", "You have to install github in order to install packages"), GUILayout.Width(80)))
            {
                string gitDownloadUrl = "https://git-scm.com/downloads";
                Application.OpenURL(gitDownloadUrl);
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            #endregion

            #region Project settings configuration

            #region Editor installations

            showPlatformSupport = GUILayout.Toggle(showPlatformSupport, new GUIContent("Unity Editor configuration", allPlatformFixed ? confirmedIcon.image : errorIconContent.image), new GUIStyle(toggleStyle));

            if (showPlatformSupport)
            {
                GUILayout.Space(5);

                EditorGUILayout.BeginVertical(new GUIStyle { margin = new RectOffset(10, 10, 0, 0) });

                string text = $"All editor modules installed properly";
                string alternativeText = $"[{string.Join(", ", installedModules.Where(kv => !kv.Value).Select(kv => kv.Key))}] editor modules are not installed";
                CreateSettingEntry(text, !installedModules.Values.Contains(false), errorIconContent, alternativeText);

                string shortUnityVersion = InternalEditorUtility.GetUnityVersion().ToString();
                string alternateText = $"You have to install Unity version {allVersionsPackageRegistries[displayedReflectisVersionIndex].RequiredUnityVersion}";
                CreateSettingEntry($"Unity version: {InternalEditorUtility.GetFullUnityVersion()}", shortUnityVersion == allVersionsPackageRegistries[displayedReflectisVersionIndex].RequiredUnityVersion, errorIconContent, alternateText);

                EditorGUILayout.EndVertical();
            }

            #endregion

            #region Project settings

            //---------------------------------------------------------------
            //---------------------------------------------------------------Project Settings
            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            showProjectSettings = GUILayout.Toggle(showProjectSettings, new GUIContent("Project Settings", allSettingsFixed ? confirmedIcon.image : errorIconContent.image), new GUIStyle(toggleStyle));

            if (GUILayout.Button("Configure all", configureAllStyle, GUILayout.Width(100)))
            {
                SetURPConfiguration();
                SetProjectSettings();
                SetMaxTextureSizeOverride();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                RefreshWindow();
            }

            EditorGUILayout.EndHorizontal();

            if (showProjectSettings)
            {
                EditorGUILayout.BeginVertical(new GUIStyle { margin = new RectOffset(10, 10, 0, 0) });

                GUILayout.Space(6);

                CreateSettingEntry("Configure URP as render pipeline", renderPipelineURP, errorIconContent);
                CreateSettingEntry("Project settings configuration", projectSettings, errorIconContent);
                CreateSettingEntry("Configure max textures size", maxTextureSizeOverride, errorIconContent);

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(20);

            #endregion

            #endregion

            #endregion

            lineRect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(lineRect, Color.black);
            EditorGUILayout.Space(15);

            #region Package manager

            EditorGUILayout.LabelField("Package manager", paragraphStyle);
            EditorGUILayout.Space(10);

            //GUIContent updateIcon = EditorGUIUtility.IconContent("d_console.infoicon.sml");
            GUIStyle textStyle = new(GUI.skin.label)
            {
                fontStyle = FontStyle.Italic
            };

            if (GUILayout.Button("Refresh"))
            {
                SetupWindowData();
            }
            if (GUILayout.Button("Log"))
            {
                UnityEngine.Debug.LogWarning(EditorPrefs.GetString(installedPackagesKey));
            }
            if (GUILayout.Button("Reset"))
            {
                EditorPrefs.SetString(installedPackagesKey, string.Empty);
            }
            if (GUILayout.Button("Test"))
            {
                ResolveBreakingChanges();
            }

            EditorGUILayout.BeginVertical(new GUIStyle(lineStyles[1]));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Choose Reflectis version", headerStyle, GUILayout.Width(250));

            EditorGUI.BeginChangeCheck();
            displayedReflectisVersionIndex = EditorGUILayout.Popup(displayedReflectisVersionIndex, availableVersions.ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                displayedReflectisVersion = availableVersions[displayedReflectisVersionIndex];
                if (installedPackages.Count == 0)
                {
                    EditorPrefs.SetString(playerPrefsVersionKey, currentInstallationVersion);
                    currentInstallationVersion = displayedReflectisVersion;
                }

                EditorPrefs.SetString(playerPrefsVersionKey, displayedReflectisVersion);
                UpdateDisplayedPacakgesAndDependencies();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            if (packagesDropdown == null || packagesDropdown.Count == 0)
                packagesDropdown = selectedVersionPackageDictionary.Where(x => x.Value.Visibility == EPackageVisibility.Visible).ToDictionary(x => x.Value.Name, x => false);

            if (dependenciesDropdown == null || dependenciesDropdown.Count == 0)
                dependenciesDropdown = selectedVersionPackageDictionary.Where(x => x.Value.Visibility == EPackageVisibility.Visible).ToDictionary(x => x.Value.Name, x => false);

            foreach (var package in selectedVersionPackageDictionary.Where(x => x.Value.Visibility == EPackageVisibility.Visible))
            {
                EditorGUILayout.BeginVertical();

                EditorGUILayout.BeginHorizontal();

                packagesDropdown[package.Value.Name] = EditorGUILayout.Foldout(packagesDropdown[package.Value.Name], package.Value.DisplayName + " - version: " + package.Value.Version);

                GUI.enabled = !installationAsDependency.TryGetValue(package.Key, out bool value) || !value; // Disable button if condition is true
                //TODO: change to a dictionary?
                bool installed = installedPackages.Select(x => x.Name).Contains(package.Value.Name);
                if (installed)
                {
                    if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Minus"), GUILayout.Width(20)))
                    {
                        UninstallPackageWithDependencies(package.Value);
                    }
                }
                else
                {
                    if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Plus"), GUILayout.Width(20)))
                    {
                        InstallPackageWithDependencies(package.Value);
                    }
                }
                GUI.enabled = true; // Re-enable GUI


                EditorGUILayout.EndHorizontal();

                // Dropdown for package details
                if (packagesDropdown[package.Value.Name])
                {
                    EditorGUILayout.BeginVertical(new GUIStyle { margin = new RectOffset(20, 0, 0, 0) });

                    EditorGUILayout.LabelField(package.Value.Description);
                    if (GUILayout.Button($"{package.Value.Url}", EditorStyles.linkLabel))
                    {
                        Application.OpenURL(package.Value.Url);
                    }

                    dependenciesDropdown[package.Value.Name] = EditorGUILayout.Foldout(dependenciesDropdown[package.Value.Name], "Show dependencies");

                    if (dependenciesDropdown[package.Value.Name])
                    {
                        EditorGUILayout.BeginVertical(new GUIStyle { margin = new RectOffset(20, 0, 0, 0) });

                        foreach (string dependency in selectedVersionDependenciesFull[package.Key])
                        {
                            EditorGUILayout.LabelField($"{selectedVersionPackageDictionary[dependency].DisplayName} - {selectedVersionPackageDictionary[dependency].Version}");
                        }

                        EditorGUILayout.EndVertical();
                    }

                    EditorGUILayout.EndVertical();
                }

                CreateSeparator();

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(10);

            GUI.enabled = currentInstallationVersion != displayedReflectisVersion;
            if (GUILayout.Button("Update packages to selected version"))
            {
                if (EditorUtility.DisplayDialog("Warning", "Do you want to update all the packages to the selected Reflectis version?", "Yes", "Cancel"))
                {
                    UpdatePackagesToSelectedVersion();
                }
            }
            GUI.enabled = true;

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(15);
            lineRect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(lineRect, Color.black);
            EditorGUILayout.Space(25);

            //Creator Kit Buttons
            configurationEvents.Invoke(); //add element to tab too(?)

            EditorGUILayout.EndVertical();

            //Documentation Link
            EditorGUILayout.BeginVertical();
            EditorGUILayout.Space(20);
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            #endregion

            #region Footer

            if (GUILayout.Button("Open Creator Kit Documentation", EditorStyles.linkLabel))
            {
                Application.OpenURL("https://reflectis.io/docs/2024.4/CK/intro");
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            #endregion

            EditorGUILayout.EndScrollView();
        }

        private void CreateSettingEntry(string name, bool valueToCheck, GUIContent errorIcon, string errorText = null)
        {
            GUIStyle lineStyle = lineStyles[0];

            EditorGUILayout.BeginVertical(lineStyle);
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(new GUIContent(valueToCheck ? confirmedIcon.image : errorIcon.image), GUILayout.Width(15));
            EditorGUILayout.TextField(valueToCheck ? name : errorText, labelStyle);
            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void CreateSeparator()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            rect.height = 1;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
        }

        #endregion

        private async void SetupWindowData()
        {
            using HttpClient client = new();
            HttpResponseMessage response = await client.GetAsync(packageRegistryPath);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            allVersionsPackageRegistries = JsonConvert.DeserializeObject<PackageRegistry[]>(responseBody);

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

            availableVersions = allVersionsPackageRegistries.Select(x => x.ReflectisVersion).ToList();

            //Get reflectis version and update list of packages
            currentInstallationVersion = EditorPrefs.GetString(playerPrefsVersionKey);
            previousInstallationVersion = currentInstallationVersion;
            displayedReflectisVersion = currentInstallationVersion;
            installedPackages = JsonConvert.DeserializeObject<HashSet<PackageDefinition>>(EditorPrefs.GetString(installedPackagesKey)) ?? new();
            if (string.IsNullOrEmpty(displayedReflectisVersion))
            {
                displayedReflectisVersion = allVersionsPackageRegistries[^1].ReflectisVersion;
            }
            displayedReflectisVersionIndex = availableVersions.IndexOf(displayedReflectisVersion);


            packagesDropdown = selectedVersionPackageDictionary.Where(x => x.Value.Visibility == EPackageVisibility.Visible).ToDictionary(x => x.Value.Name, x => false);
            dependenciesDropdown = selectedVersionPackageDictionary.Where(x => x.Value.Visibility == EPackageVisibility.Visible).ToDictionary(x => x.Value.Name, x => false);

            UpdateDisplayedPacakgesAndDependencies();

            CheckGitInstallation();
            CheckEditorModulesInstallation();
            CheckProjectSettings();

            setupCompleted = true;
        }

        private void RefreshWindow()
        {
            Repaint(); // Request Unity to redraw the window

            allPlatformFixed = true;
            allSettingsFixed = true;

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
                    gitVersion = output;
                    isGitInstalled = true;
                }
                else
                {
                    isGitInstalled = false;
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Git Check", "An error occurred while checking for Git:\n" + ex.Message, "OK");
            }
        }

        private void CheckEditorModulesInstallation()
        {
            //Check supported platform
            BuildTargetGroup[] buildTargetGroups = (BuildTargetGroup[])System.Enum.GetValues(typeof(BuildTargetGroup));
            foreach (BuildTargetGroup group in buildTargetGroups)
            {
                CheckBuildTarget(group, BuildTarget.Android, "Android");
                CheckBuildTarget(group, BuildTarget.StandaloneWindows, "Windows");
                CheckBuildTarget(group, BuildTarget.WebGL, "WebGL");
            }

            //update the coreInstalled value
            foreach (var element in installedModules)
            {
                if (element.Value == false)
                {
                    allPlatformFixed = false;
                }
            }
        }

        private void CheckProjectSettings()
        {
            renderPipelineURP = GetURPConfigurationStatus();
            projectSettings = GetProjectSettingsStatus();
            maxTextureSizeOverride = GetMaxTextureSizeOverride();

            allSettingsFixed = renderPipelineURP && projectSettings && maxTextureSizeOverride;
        }

        private void CheckBuildTarget(BuildTargetGroup group, BuildTarget target, string platform)
        {
            bool value = BuildPipeline.IsBuildTargetSupported(group, target);
            if (value)
            {
                installedModules[platform] = true;
            }
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
            selectedVersionPackageList = allVersionsPackageRegistries.FirstOrDefault(x => x.ReflectisVersion == displayedReflectisVersion).Packages;
            selectedVersionDependencies = allVersionsPackageRegistries.FirstOrDefault(x => x.ReflectisVersion == displayedReflectisVersion).Dependencies;

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
            if (currentInstallationVersion != displayedReflectisVersion)
            {
                previousInstallationVersion = currentInstallationVersion;
                currentInstallationVersion = displayedReflectisVersion;
                EditorPrefs.SetString(playerPrefsVersionKey, currentInstallationVersion);

                Dictionary<string, PackageDefinition> packages = allVersionsPackageRegistries
                    .FirstOrDefault(x => x.ReflectisVersion == currentInstallationVersion).Packages
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

                if (resolveBreakingChangesAutomatically)
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
            string cur = FilterPatch(currentInstallationVersion);
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

        private void Progress()
        {
            if (addRequest.IsCompleted)
            {
                if (addRequest.Status == StatusCode.Success)
                {
                    UnityEngine.Debug.Log("Git package added successfully.");
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    RefreshWindow();
                }
                else if (addRequest.Status >= StatusCode.Failure)
                {
                    UnityEngine.Debug.LogError("Failed to add Git package: " + addRequest.Error.message);
                }

                EditorApplication.update -= Progress;
            }
        }

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
                UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath(importedAsset, typeof(MonoScript));
                if (obj != null)
                {
                    SetupReflectisWindow.ResolveBreakingChangesCallback(importedAsset.Split('/').Last()[..^3]);
                }
            }
        }
    }
}

