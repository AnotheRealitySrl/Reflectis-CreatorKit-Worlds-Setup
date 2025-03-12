using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

using UnityEngine;
using UnityEngine.Events;

namespace Reflectis.CreatorKit.Worlds.Installer.Editor
{
    [InitializeOnLoad]
    public class SetupReflectisWindow : EditorWindow
    {
        #region Reflectis JSON data variables

        private readonly string playerPrefsVersionKey = "SelectedReflectisVersion"; //string used to know which reflectis version is installed. Set The first time you press the setup button. 
        private readonly string installedPackagesKey = "InstalledPackages"; //string used to know which packages are installed.

        private static PackageRegistry[] allVersionsPackageRegistries;
        private static string packagesTextAsset;

        #endregion

        private int reflectisVersionIndex;
        private static string selectedReflectisVersion;

        #region booleanValues
        private bool isGitInstalled = false;
        private string gitVersion = string.Empty;
        private bool urpConfigured = true;
        private bool renderPipelineURP = false;
        private bool netFramework = false;
        private bool maxTextureSizeOverride = false;

        private bool allSettingsFixed = true;
        private bool allPlatformFixed = true;

        private bool showPlatformSupport = false;
        private bool showProjectSettings = false;

        private bool setupCompleted = false;

        #endregion

        #region Package and platform lists

        public Dictionary<string, bool> supportedPlatform = new()
        {
            { "Android", true },
            { "WebGL", true },
            { "Windows", true }
        };

        private List<string> availableVersions = new();

        #endregion

        #region Packages details

        private static List<PackageDefinition> packageList = new(); //the selected version packages
        private static Dictionary<string, PackageDefinition> packagesDictionary = new();

        private static Dictionary<string, string[]> installedPackages; //the currently installed packages, the values are the dependencies

        private static Dictionary<string, string[]> dependencyList = new();

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

        public GUIContent[] tabContents = new GUIContent[]
        {
            new(" Core"),
            new(" Optional")
        };

        int selectedTab = 0;

        private Vector2 scrollPosition = Vector2.zero;

        private Rect lineRect;

        #endregion

        private AddRequest addRequest;

        private Action buttonFunction;

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

            //Core tab
            //---------------------------------------------------------------
            //---------------------------------------------------------------Platform Build Support

            //General project setup
            int currentLineStyleIndex = 0;
            lineStyle = lineStyles[currentLineStyleIndex];

            #region Editor installations

            showPlatformSupport = GUILayout.Toggle(showPlatformSupport, new GUIContent("Unity Editor configuration", allPlatformFixed ? confirmedIcon.image : errorIconContent.image), new GUIStyle(toggleStyle));

            if (showPlatformSupport)
            {
                GUILayout.Space(5);

                EditorGUILayout.BeginVertical(new GUIStyle { margin = new RectOffset(10, 10, 0, 0) });

                foreach (KeyValuePair<string, bool> element in supportedPlatform)
                {
                    buttonFunction = new Action(() =>
                    {
                        if (EditorUtility.DisplayDialog("Build Support", "You need to install the " + element.Key + " build support from the Unity Hub. If already installed try to close the setup window and reopen it or reopen the project", "Ok"))
                        {

                        }
                    });
                    CreateSettingFixField(element.Key, element.Value, "You have to install the " + element.Key + " build support from Unity Hub", currentLineStyleIndex, buttonFunction, errorIconContent, "Fix");
                    currentLineStyleIndex = (currentLineStyleIndex + 1) % lineStyles.Length;
                }

                EditorGUILayout.EndVertical();
            }

            #endregion

            #region Project settings

            //---------------------------------------------------------------
            //---------------------------------------------------------------Project Settings
            GUILayout.Space(10);
            showProjectSettings = GUILayout.Toggle(showProjectSettings, new GUIContent("Project Settings", allSettingsFixed ? confirmedIcon.image : errorIconContent.image), new GUIStyle(toggleStyle));

            if (showProjectSettings)
            {
                EditorGUILayout.BeginVertical(new GUIStyle { margin = new RectOffset(10, 10, 0, 0) });

                GUILayout.Space(6);

                //Graphic Setting
                //if urp package is installed do this, otherwise don't show it
                if (urpConfigured)
                {
                    buttonFunction = new Action(SetURPConfiguration);

                    CreateSettingFixField("Configure URP as render pipeline", renderPipelineURP, "You need to set URP as your render pipeline", currentLineStyleIndex, buttonFunction, errorIconContent, "Configure");
                    currentLineStyleIndex = (currentLineStyleIndex + 1) % lineStyles.Length;
                }

                //Net Framework
                buttonFunction = new Action(SetProjectSettings);

                CreateSettingFixField("Net Framework compability Level", netFramework, "You need to set .NET Framework in the Api Compability Level field", currentLineStyleIndex, buttonFunction, errorIconContent, "Configure");
                currentLineStyleIndex = (currentLineStyleIndex + 1) % lineStyles.Length;
                //---------------------------------------------------------------

                //Max texture size override
                buttonFunction = new Action(SetMaxTextureSizeOverride);

                CreateSettingFixField("Configure max textures size", maxTextureSizeOverride, "You need to set URP as your render pipeline", currentLineStyleIndex, buttonFunction, errorIconContent, "Configure");
                currentLineStyleIndex = (currentLineStyleIndex + 1) % lineStyles.Length;

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

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
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginVertical(new GUIStyle(lineStyles[1]));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Choose Reflectis version", headerStyle, GUILayout.Width(250));

            EditorGUI.BeginChangeCheck();
            selectedReflectisVersion = availableVersions[reflectisVersionIndex];
            reflectisVersionIndex = EditorGUILayout.Popup(reflectisVersionIndex, availableVersions.ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(playerPrefsVersionKey, selectedReflectisVersion);
                UpdatePackageAndDependencies();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            if (packagesDropdown == null || packagesDropdown.Count == 0)
                packagesDropdown = packagesDictionary.Where(x => x.Value.Visibility == EPackageVisibility.Visible).ToDictionary(x => x.Value.Name, x => false);

            if (dependenciesDropdown == null || dependenciesDropdown.Count == 0)
                dependenciesDropdown = packagesDictionary.Where(x => x.Value.Visibility == EPackageVisibility.Visible).ToDictionary(x => x.Value.Name, x => false);

            foreach (var package in packagesDictionary.Where(x => x.Value.Visibility == EPackageVisibility.Visible))
            {
                EditorGUILayout.BeginVertical();

                EditorGUILayout.BeginHorizontal();

                packagesDropdown[package.Value.Name] = EditorGUILayout.Foldout(packagesDropdown[package.Value.Name], package.Value.DisplayName + " - version: " + package.Value.Version);

                if (!installedPackages.TryGetValue(package.Value.Name, out _))
                {
                    GUI.enabled = !IsPackageInstalledAsDependency(package.Value); // Disable button if condition is true
                    if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Plus"), GUILayout.Width(20)))
                    {
                        InstallPackageWithDependencies(package.Value);
                    }
                    GUI.enabled = true; // Re-enable GUI
                }
                else
                {
                    GUI.enabled = !IsPackageInstalledAsDependency(package.Value);
                    if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Minus"), GUILayout.Width(20)))
                    {
                        UninstallPackageWithDependencies(package.Value);
                    }
                    GUI.enabled = true; // Re-enable GUI
                }

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

                        foreach (string dependency in dependencyList[package.Key])
                        {
                            EditorGUILayout.LabelField($"{packagesDictionary[dependency].DisplayName} - {packagesDictionary[dependency].Version}");
                        }

                        EditorGUILayout.EndVertical();
                    }

                    EditorGUILayout.EndVertical();
                }

                CreateSeparator();

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(10);

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

        private void CreateSettingFixField(string name, bool valueToCheck, string buttonDescription, int currentLineStyleIndex, Action buttonFunction, GUIContent errorIcon, string buttonText)
        {
            GUIStyle lineStyle = lineStyles[currentLineStyleIndex];

            EditorGUILayout.BeginVertical(lineStyle);
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(new GUIContent(valueToCheck ? confirmedIcon.image : errorIcon.image), GUILayout.Width(15));
            GUILayout.Label(name, labelStyle);
            GUILayout.FlexibleSpace();

            if (valueToCheck)
                GUI.enabled = false;

            if (GUILayout.Button(new GUIContent(buttonText, buttonDescription), GUILayout.Width(80)))
            {
                try
                {
                    EditorUtility.DisplayProgressBar("Loading", "Applying Changes...", 0.75f);
                    buttonFunction();
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                RefreshWindow();

            }
            GUI.enabled = true;
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

        private void SetupWindowData()
        {
            //For now load the json via the resource folder. In the future calla an api to retrieve the json
            packagesTextAsset ??= Resources.Load<TextAsset>("PackageRegistry").text;

            allVersionsPackageRegistries = JsonConvert.DeserializeObject<PackageRegistry[]>(packagesTextAsset);
            availableVersions = allVersionsPackageRegistries.Select(x => x.ReflectisVersion).ToList();

            //Get reflectis version and update list of packages
            selectedReflectisVersion = EditorPrefs.GetString(playerPrefsVersionKey);
            installedPackages = JsonConvert.DeserializeObject<Dictionary<string, string[]>>(EditorPrefs.GetString(installedPackagesKey)) ?? new();
            if (string.IsNullOrEmpty(selectedReflectisVersion))
            {
                selectedReflectisVersion = allVersionsPackageRegistries[^1].ReflectisVersion;
            }
            reflectisVersionIndex = availableVersions.IndexOf(selectedReflectisVersion);

            packagesDropdown = packagesDictionary.Where(x => x.Value.Visibility == EPackageVisibility.Visible).ToDictionary(x => x.Value.Name, x => false);
            dependenciesDropdown = packagesDictionary.Where(x => x.Value.Visibility == EPackageVisibility.Visible).ToDictionary(x => x.Value.Name, x => false);

            UpdatePackageAndDependencies();

            CheckGitInstallation();
            CheckEditorModulesInstallation();

            setupCompleted = true;
        }

        private void RefreshWindow()
        {
            Repaint(); // Request Unity to redraw the window

            allPlatformFixed = true;
            allSettingsFixed = true;

            UpdatePackageAndDependencies();

            CheckGitInstallation();
            CheckEditorModulesInstallation();
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
            foreach (var element in supportedPlatform)
            {
                if (element.Value == false)
                {
                    allPlatformFixed = false;
                }
            }

            //URP Pipeline
            renderPipelineURP = GetURPConfigurationStatus();
            //NET Framework
            netFramework = GetProjectSettingsStatus();
            //Max texture override
            maxTextureSizeOverride = GetMaxTextureSizeOverride();

            allSettingsFixed = renderPipelineURP && netFramework && maxTextureSizeOverride;
        }

        private void CheckBuildTarget(BuildTargetGroup group, BuildTarget target, string platform)
        {
            bool value = BuildPipeline.IsBuildTargetSupported(group, target);
            if (value)
            {
                supportedPlatform[platform] = true;
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

            urpConfigured = true;
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
            if (dependencyList.TryGetValue(package.Name, out string[] packageDependencies))
            {
                foreach (string dependency in packageDependencies)
                {
                    FindAllDependencies(packagesDictionary[dependency], dependencies);
                }
                dependencies.AddRange(packageDependencies);
            }

            return dependencies;
        }

        private void UpdatePackageAndDependencies()
        {
            packageList = allVersionsPackageRegistries.FirstOrDefault(x => x.ReflectisVersion == selectedReflectisVersion).Packages;
            dependencyList = allVersionsPackageRegistries.FirstOrDefault(x => x.ReflectisVersion == selectedReflectisVersion).Dependencies;

            packagesDropdown = packagesDictionary.Where(x => x.Value.Visibility == EPackageVisibility.Visible).ToDictionary(x => x.Value.Name, x => false);
            dependenciesDropdown = packagesDictionary.Where(x => x.Value.Visibility == EPackageVisibility.Visible).ToDictionary(x => x.Value.Name, x => false);

            packagesDictionary = packageList.ToDictionary(x => x.Name);
        }

        private void InstallPackageWithDependencies(PackageDefinition package)
        {
            List<string> dependenciesToInstall = FindAllDependencies(package, new());

            installedPackages.Add(package.Name, dependenciesToInstall.ToArray());

            foreach (string depToInstall in dependenciesToInstall.Where(x => packagesDictionary[package.Name].Visibility == EPackageVisibility.Visible))
                installedPackages.TryAdd(depToInstall, FindAllDependencies(packagesDictionary[depToInstall], new()).ToArray());

            EditorPrefs.SetString(installedPackagesKey, JsonConvert.SerializeObject(installedPackages));

            InstallPackages(dependenciesToInstall.Append(package.Name).Select(x => packagesDictionary[x]).ToList());
        }

        private void InstallPackages(List<PackageDefinition> toInstall)
        {
            string manifestFilePath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
            string manifestJson = File.ReadAllText(manifestFilePath);
            JObject manifestObj = JObject.Parse(manifestJson);

            JObject dependencies = (JObject)manifestObj["dependencies"];
            foreach (PackageDefinition p in toInstall)
            {
                dependencies[p.Name] = p.Url;
            }

            File.WriteAllText(manifestFilePath, manifestObj.ToString());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshWindow();
        }

        private void UninstallPackageWithDependencies(PackageDefinition toUninstall)
        {
            installedPackages.Remove(toUninstall.Name);

            List<string> hiddenPackages = installedPackages.Where(x => packagesDictionary[x.Key].Visibility == EPackageVisibility.Hidden).Select(x => x.Key).ToList();
            Dictionary<string, List<string>> inverted = InvertDictionary(installedPackages);

            List<string> dependenciesToUninstall = new();
            List<string> allValues = installedPackages.Select(x => x.Value).SelectMany(innerList => innerList).ToList();
            foreach (string hidden in hiddenPackages)
            {
                if (!allValues.Contains(hidden))
                {
                    dependenciesToUninstall.Add(hidden);
                    installedPackages.Remove(hidden);
                }
            }

            EditorPrefs.SetString(installedPackagesKey, JsonConvert.SerializeObject(installedPackages));

            UninstallPackage(dependenciesToUninstall.Select(x => packagesDictionary[x]).Append(toUninstall).ToList());
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

        private void UninstallPackage(List<PackageDefinition> toRemove)
        {
            string manifestFilePath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
            string manifestJson = File.ReadAllText(manifestFilePath);
            JObject manifestObj = JObject.Parse(manifestJson);

            JObject dependencies = (JObject)manifestObj["dependencies"];
            foreach (PackageDefinition p in toRemove)
            {
                dependencies.Remove(p.Name);
            }

            File.WriteAllText(manifestFilePath, manifestObj.ToString());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshWindow();
        }

        private bool IsPackageInstalledAsDependency(PackageDefinition package)
        {
            return installedPackages.SelectMany(x => x.Value).Contains(package.Name);
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
    }
}

