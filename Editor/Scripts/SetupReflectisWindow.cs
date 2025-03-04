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
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Reflectis.CreatorKit.Worlds.Installer.Editor
{
    [InitializeOnLoad]
    public class SetupReflectisWindow : EditorWindow
    {
        #region Reflectis JSON data variables

        private readonly string playerPrefsVersionKey = "SelectedReflectisVersion"; //string used to know which reflectis version is installed. Set The first time you press the setup button. 
        private readonly string installedPackagesKey = "InstalledPackages"; //string used to know which packages are installed.

        private PackageRegistry[] allVersionsPackageRegistries;
        private string reflectisJSONstring;

        #endregion

        private int reflectisVersionIndex;
        private static string selectedReflectisVersion;

        #region booleanValues
        private bool isGitInstalled = false;
        private string gitVersion = "";
        private bool urpInstalled = true;
        private bool renderPipelineURP = false;
        private bool netFramework = false;
        private static readonly string reflectisSetupShown = "EditorWindowAlreadyShown";

        private bool allSettingsFixed = true;
        private bool allPlatformFixed = true;

        private bool showPlatformSupport = false;
        private bool showProjectSettingss = false;

        private bool canUpdateReflectis = false;
        #endregion

        #region Package and platform lists
        public Dictionary<string, bool> supportedPlatform = new()
        {
            { "Android", true },
            { "WebGL", true },
            { "Windows", true }
        };


        private List<string> availableVersions = new();

        private static List<PackageDefinition> packageList = new(); //the selected version packages
        private static Dictionary<string, PackageDefinition> packagesDictionary = new();

        private static Dictionary<string, string[]> installedPackages; //the currently installed packages, the values are the dependencies

        private static Dictionary<string, string[]> dependencyList = new();
        private static Dictionary<PackageDefinition, PackageDefinition[]> dependenciesWithData = new();

        #endregion

        #region GuiElements
        //GUI Styles
        GUIStyle iconStyle;
        GUIStyle titleStyle;
        GUIStyle labelStyle;
        GUIStyle headerStyle;
        GUIStyle arrowStyle;
        GUIStyle fixAllStyle;
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

        ListRequest listRequest; //used to keep track of the installed packages

        //this variable is set by the other packages (like CK, used to show buttons and other GUI elements).
        public static UnityEvent configurationEvents = new();

        //delete the player pref so to reshow the window when opening the priject again
        private void OnUnityQuit()
        {
            // Reset the PlayerPrefs flag when Unity is quitting
            PlayerPrefs.DeleteKey(reflectisSetupShown);
            PlayerPrefs.Save(); // Save PlayerPrefs
        }

        static SetupReflectisWindow()
        {
            EditorApplication.delayCall += () =>
            {
                if (!PlayerPrefs.HasKey(reflectisSetupShown))
                {
                    PlayerPrefs.SetFloat(reflectisSetupShown, 1f);
                    PlayerPrefs.Save();
                    ShowWindow();
                }
            };

        }

        [MenuItem("Reflectis/Setup Window")]
        public static void ShowWindow()
        {
            GetWindow(typeof(SetupReflectisWindow));
        }

        #region Unity callbacks

        private void Awake()
        {
            //For now load the json via the resource folder. In the future calla an api to retrieve the json
            reflectisJSONstring ??= Resources.Load<TextAsset>("PackageRegistry").text;

            allVersionsPackageRegistries = JsonConvert.DeserializeObject<PackageRegistry[]>(reflectisJSONstring);
            availableVersions = allVersionsPackageRegistries.Select(x => x.ReflectisVersion).ToList();

            //Get reflectis version and update list of packages
            selectedReflectisVersion = EditorPrefs.GetString(playerPrefsVersionKey);
            installedPackages = JsonConvert.DeserializeObject<Dictionary<string, string[]>>(EditorPrefs.GetString(installedPackagesKey)) ?? new();
            if (string.IsNullOrEmpty(selectedReflectisVersion))
            {
                selectedReflectisVersion = allVersionsPackageRegistries[^1].ReflectisVersion;
            }
            reflectisVersionIndex = availableVersions.IndexOf(selectedReflectisVersion);

            UpdatePackageAndDependencies();

            ClientListCall();
            CheckGitInstallation();
            CheckGeneralSetup();

            if (PlayerPrefs.HasKey(reflectisSetupShown))
            {
                EditorApplication.quitting += OnUnityQuit;
            }
        }

        private void OnGUI()
        {
            // Define the GUIStyle
            //----------------------------------------------------
            iconStyle = new(EditorStyles.label);
            titleStyle = new GUIStyle(GUI.skin.label);
            labelStyle = new(EditorStyles.label)
            {
                richText = true,
            };
            headerStyle = new GUIStyle(GUI.skin.label);
            arrowStyle = new GUIStyle(GUI.skin.label);
            lineStyles = new GUIStyle[] { GUI.skin.box, GUI.skin.textField }; // Different line styles for alternating colors
            fixAllStyle = new GUIStyle(GUI.skin.button) { margin = new RectOffset(0, 7, 0, 0) };
            iconStyle.richText = true;
            titleStyle.fontSize = 20;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.fontSize = 12;
            headerStyle.fontStyle = FontStyle.Bold;
            arrowStyle.fontSize = 16;
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

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, false, false);

            // Draw the title text with the specified GUIStyle
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("WELCOME TO REFLECTIS", titleStyle);
            EditorGUILayout.Space();
            EditorGUI.DrawRect(lineRect, Color.black);
            EditorGUILayout.Space(20);

            EditorGUILayout.BeginVertical();
            EditorGUILayout.Space(20);

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

            //Core tab
            CreateGeneralSetupGUI();
            EditorGUILayout.Space(20);

            lineRect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(lineRect, Color.black);
            EditorGUILayout.Space(15);
            SetupReflectisVersionGUI();
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

            if (GUILayout.Button("Open Creator Kit Documentation", EditorStyles.linkLabel))
            {
                Application.OpenURL("https://reflectis.io/docs/2024.4/CK/intro");
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("test"))
            {
                UnityEngine.Debug.Log(JsonConvert.SerializeObject(installedPackages));
            }
        }

        #endregion

        private void ClientListCall()
        {

            listRequest = Client.List(true);
            while (!listRequest.IsCompleted)
            {
                // Wait for the list request to complete
            }
        }

        private void RefreshWindow()
        {
            allPlatformFixed = true;
            allSettingsFixed = true;
            CheckGitInstallation();
            ClientListCall(); //call the manifest and retrieve the isntalled packages again. That way the page will be updated.
            CheckGeneralSetup();

            Repaint(); // Request Unity to redraw the window
        }

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

        private void CheckGeneralSetup()
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
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset)
            {
                renderPipelineURP = true;
            }
            else
            {
                renderPipelineURP = false;
                allSettingsFixed = false;
            }

            //NET Framework
            if (PlayerSettings.GetApiCompatibilityLevel(NamedBuildTarget.Standalone) == ApiCompatibilityLevel.NET_Unity_4_8)
            {
                netFramework = true;
            }
            else
            {
                netFramework = false;
                allSettingsFixed = false;
            }

        }

        private void CheckBuildTarget(BuildTargetGroup group, BuildTarget target, string platform)
        {
            bool value = BuildPipeline.IsBuildTargetSupported(group, target);
            if (value)
            {
                supportedPlatform[platform] = true;
            }
        }


        private void CreateGeneralSetupGUI()
        {
            //---------------------------------------------------------------
            //---------------------------------------------------------------Platform Build Support

            //General project setup
            int currentLineStyleIndex = 0;
            GUIStyle lineStyle = lineStyles[currentLineStyleIndex];

            showPlatformSupport = GUILayout.Toggle(showPlatformSupport, new GUIContent("Platform build support", allPlatformFixed ? confirmedIcon.image : errorIconContent.image), new GUIStyle(toggleStyle));

            if (showPlatformSupport)
            {

                GUILayout.Space(5);

                foreach (KeyValuePair<string, bool> element in supportedPlatform)
                {
                    buttonFunction = new Action(() =>
                    {
                        if (EditorUtility.DisplayDialog("Build Support", "You need to install the " + element.Key + " build support from the Unity Hub. If already installed try to close the setup window and reopen it or reopen the project", "Ok"))
                        {

                        }
                    });
                    CreateSettingFixField(element.Key, element.Value, "You have to install the " + element.Key + " build support from the Unity Hub", currentLineStyleIndex, buttonFunction, errorIconContent, "Fix");
                    currentLineStyleIndex = (currentLineStyleIndex + 1) % lineStyles.Length;
                }
            }

            //---------------------------------------------------------------
            //---------------------------------------------------------------Project Settings
            GUILayout.Space(10);
            showProjectSettingss = GUILayout.Toggle(showProjectSettingss, new GUIContent("Project Settings", allSettingsFixed ? confirmedIcon.image : errorIconContent.image), new GUIStyle(toggleStyle));

            if (showProjectSettingss)
            {
                GUILayout.Space(6);

                //Graphic Setting
                //if urp package is installed do this, otherwise don't show it
                if (urpInstalled)
                {
                    buttonFunction = new Action(SetURPRenderPipeline);

                    CreateSettingFixField("URP as Render Pipeline", renderPipelineURP, "You need to set URP as your render pipeline", currentLineStyleIndex, buttonFunction, errorIconContent, "Fix");
                    currentLineStyleIndex = (currentLineStyleIndex + 1) % lineStyles.Length;
                }

                //Net Framework
                buttonFunction = new Action(SetNetFramework);

                CreateSettingFixField("Net Framework compability Level", netFramework, "You need to set .NET Framework in the Api Compability Level field", currentLineStyleIndex, buttonFunction, errorIconContent, "Fix");
                currentLineStyleIndex = (currentLineStyleIndex + 1) % lineStyles.Length;
                //---------------------------------------------------------------

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Fix All", fixAllStyle, GUILayout.Width(80)))
                {
                    FixAllProjectSettings();
                }
                GUILayout.EndHorizontal();
            }
        }

        private void SetURPRenderPipeline()
        {
            string[] guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");

            if (guids.Length <= 0)
            {
                //Open window to tell user to setup URP in graphic settings
                if (EditorUtility.DisplayDialog("Graphic Settings", "You need to setup URP in your project, head to Edit/Project Settings/Graphics/URP Global Settings and press the fix button. After that you need to create a render pipeline asset to put in the graphic field!", "Ok"))
                {

                }
            }
            else
            {

                string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
                GraphicsSettings.defaultRenderPipeline = urpAsset;
                QualitySettings.renderPipeline = urpAsset;
            }

        }

        private void SetNetFramework()
        {
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Unity_4_8);
        }

        private void FixAllProjectSettings()
        {
            SetURPRenderPipeline();
            SetNetFramework();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshWindow();
        }

        private void SetupReflectisVersionGUI()
        {
            ShowReflectisUpdate();

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

            DisplayPackageList();

            EditorGUILayout.Space(10);

            EditorGUILayout.EndVertical();
        }

        private void UpdatePackageAndDependencies()
        {
            packageList = allVersionsPackageRegistries.FirstOrDefault(x => x.ReflectisVersion == selectedReflectisVersion).Packages;
            dependencyList = allVersionsPackageRegistries.FirstOrDefault(x => x.ReflectisVersion == selectedReflectisVersion).Dependencies;

            packagesDictionary = packageList.ToDictionary(x => x.Name);

            dependenciesWithData = dependencyList
                .Where(x => packagesDictionary[x.Key].Visibility == EPackageVisibility.Visible)
                .ToDictionary(k => packagesDictionary[k.Key], v => v.Value.Select(x => packagesDictionary[x]).ToArray());
        }


        private void ShowReflectisUpdate()
        {
            //GUIContent updateIcon = EditorGUIUtility.IconContent("d_console.infoicon.sml");
            GUIStyle textStyle = new(GUI.skin.label)
            {
                fontStyle = FontStyle.Italic
            };

            if (canUpdateReflectis)
            {
                //Display message that user can Update reflectis
                EditorGUILayout.BeginHorizontal();
                //EditorGUILayout.LabelField(new GUIContent(updateIcon.image), GUILayout.Width(20));
                EditorGUILayout.LabelField("There is a new Reflectis update", textStyle);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Current Reflectis version " + selectedReflectisVersion, textStyle);
            EditorGUILayout.EndHorizontal();
        }

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

        private void InstallPackageWithDependencies(PackageDefinition package, string dependencyOf = null)
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

        #endregion

        private Dictionary<string, bool> packagesDropdown = packagesDictionary.Where(x => x.Value.Visibility == EPackageVisibility.Visible).ToDictionary(x => x.Value.Name, x => false);
        private Dictionary<string, bool> dependenciesDropdown = packagesDictionary.Where(x => x.Value.Visibility == EPackageVisibility.Visible).ToDictionary(x => x.Value.Name, x => false);
        private void DisplayPackageList()
        {
            foreach (var package in packagesDictionary.Where(x => x.Value.Visibility == EPackageVisibility.Visible))
            {
                EditorGUILayout.BeginVertical();

                EditorGUILayout.BeginHorizontal();

                packagesDropdown[package.Value.Name] = EditorGUILayout.Foldout(packagesDropdown[package.Value.Name], package.Value.DisplayName + " - version: " + package.Value.Version);

                if (!installedPackages.TryGetValue(package.Value.Name, out _))
                {
                    GUI.enabled = !IsInstalledAsDependency(package.Value); // Disable button if condition is true
                    if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Plus"), GUILayout.Width(20)))
                    {
                        InstallPackageWithDependencies(package.Value);
                    }
                    GUI.enabled = true; // Re-enable GUI
                }
                else
                {
                    GUI.enabled = !IsInstalledAsDependency(package.Value);
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
                    EditorGUILayout.LabelField("Description: " + package.Value.Description);
                    EditorGUILayout.LabelField(new GUIContent($"<a href='{package.Value.Url}'><i>{package.Value.Url}</i></a>"), labelStyle);

                    dependenciesDropdown[package.Value.Name] = EditorGUILayout.Foldout(dependenciesDropdown[package.Value.Name], "Show dependencies");

                    if (dependenciesDropdown[package.Value.Name])
                    {
                        EditorGUILayout.BeginVertical();

                        foreach (string dependency in dependencyList[package.Key])
                            EditorGUILayout.LabelField(packagesDictionary[dependency].DisplayName);

                        EditorGUILayout.EndVertical();
                    }
                }

                EditorGUILayout.EndVertical();

                EditorGUI.DrawRect(lineRect, Color.black);
                EditorGUILayout.Space(5);
            }
        }

        private bool IsInstalledAsDependency(PackageDefinition package)
        {
            return installedPackages.SelectMany(x => x.Value).Contains(package.Name);
        }

        private void CreateSettingFixField(string name, bool valueToCheck, string buttonDescription, int currentLineStyleIndex, Action buttonFunction, GUIContent errorIcon, string buttonText)
        {
            GUIStyle lineStyle = lineStyles[currentLineStyleIndex];

            EditorGUILayout.BeginVertical(lineStyle);
            EditorGUILayout.BeginHorizontal();

            //EditorGUILayout.LabelField($"{(valueToCheck ? "<b>[<color=lime>√</color>]</b>" : "<b>[<color=red>X</color>]</b>")}", iconStyle, GUILayout.Width(20));
            EditorGUILayout.LabelField(new GUIContent(valueToCheck ? confirmedIcon.image : errorIcon.image), GUILayout.Width(15));
            GUILayout.Label(name, labelStyle);
            GUILayout.FlexibleSpace();

            if (valueToCheck)
                GUI.enabled = false;

            //GUI.Box(new Rect(5, 35, 110, 75), new GUIContent("Box", "this box has a tooltip"));
            if (GUILayout.Button(new GUIContent(buttonText, buttonDescription), GUILayout.Width(80)))
            {
                try
                {
                    EditorUtility.DisplayProgressBar("Loading", "Applying Changes...", 0.75f);
                    buttonFunction();
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    RefreshWindow();
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    RefreshWindow();
                }

            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }


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

