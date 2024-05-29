using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditorInternal;
using UnityEngine;

#if UNITY_URP_INSTALLED
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace Reflectis.SetupEditor
{
    [InitializeOnLoad]
    public class SetupReflectisWindow : EditorWindow
    {
        #region booleanValues
        private bool isGitInstalled = false;
        private string gitVersion = "";
        private bool URPInstalled = false;
        private bool renderPipelineURP = false;
        private bool netFramework = false;
        #endregion

        #region package and platform lists
        public Dictionary<string, bool> supportedPlatform = new Dictionary<string, bool>
        {
            { "Android", false },
            { "WebGL", false },
            { "Windows", false }
        };
        [SerializeField] private List<PackageSetupScriptable> packageList = new List<PackageSetupScriptable>();
        #endregion

        #region GuiElements
        //GUI Styles
        GUIStyle iconStyle;
        GUIStyle titleStyle;
        GUIStyle labelStyle;
        GUIStyle arrowStyle;
        GUIStyle fixAllStyle;
        GUIStyle[] lineStyles; // Different line styles for alternating colors
        #endregion

        private AddRequest addRequest;

        private Action buttonFunction;

        ListRequest listRequest; //used to keep track of the installed packages
        private List<string> assemblyFileNames = new List<string>();

        static SetupReflectisWindow()
        {
            EditorApplication.delayCall += () =>
            {
                ShowWindow();
            };

        }

        [MenuItem("Reflectis/Setup Window")]
        public static void ShowWindow()
        {
            //Show existing window instance. If one doesn't exist, make one.
            GetWindow(typeof(SetupReflectisWindow));
        }

        private void Awake()
        {
            PackagesDetails myScriptableObject = Resources.Load<PackagesDetails>("PackagesSetup");

            if (myScriptableObject != null)
            {
                packageList = myScriptableObject.packageDetailsList;

            }

            GetAllAssemblyFiles();
            CheckGitInstallation();
            CheckGeneralSetup();
            InitializePackages();
        }

        #region General Checks Functions
        private void GetAllAssemblyFiles()
        {
            string[] asmdefs = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset");
            foreach (string guid in asmdefs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asmdefAsset = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(path);
                if (asmdefAsset != null)
                {
                    //assemblyFileNames.Add(asmdefAsset.name);
                    assemblyFileNames.Add(guid);
                    //UnityEngine.Debug.LogError(asmdefAsset.name + "----" + guid);
                }
            }
        }

        private void InitializePackages()
        {
            //init the list of packages in the project
            listRequest = Client.List(true);
            while (!listRequest.IsCompleted)
            {
                // Wait for the list request to complete
            }

            // Initialize the package list with some sample data
            foreach (PackageSetupScriptable packageScriptable in packageList)
            {
                packageScriptable.installed = CheckPackageInstallation(packageScriptable.packageName, packageScriptable.assemblyGUID);
                if (packageScriptable.packageName == "com.unity.render-pipelines.universal")
                {
                    if (packageScriptable.installed)
                    {
                        //add UNITY_URP_INSTALLED to player settings
                        var buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
                        var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);
                        if (!symbols.Contains("UNITY_URP_INSTALLED"))
                        {
                            symbols += ";" + "UNITY_URP_INSTALLED";
                            PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, symbols);
                        }
                        URPInstalled = true;
                    }
                }
            }
        }

        private bool CheckPackageInstallation(string packageName, string assemblyName)
        {
            string manifestFilePath = Path.Combine(Application.dataPath, "../Packages/manifest.json");

            if (!File.Exists(manifestFilePath))
            {
                UnityEngine.Debug.LogError("manifest.json file not found!");
                return false;
            }

            string manifestJson = File.ReadAllText(manifestFilePath);

            if (!PackageExists(packageName, assemblyName))
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private void CheckGitInstallation()
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        UnityEngine.Debug.LogError("Git is installed with version " + output);
                        gitVersion = output;
                        isGitInstalled = true;
                    }
                    else
                    {
                        isGitInstalled = false;
                        UnityEngine.Debug.LogError("Git not installed");
                    }
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
                bool value = BuildPipeline.IsBuildTargetSupported(group, BuildTarget.Android);
                if (value) { supportedPlatform["Android"] = true; }
                value = BuildPipeline.IsBuildTargetSupported(group, BuildTarget.StandaloneWindows);
                if (value) { supportedPlatform["Windows"] = true; }
                value = BuildPipeline.IsBuildTargetSupported(group, BuildTarget.WebGL);
                if (value) { supportedPlatform["WebGL"] = true; }
            }

            //URP Pipeline
#if UNITY_URP_INSTALLED
            if (GraphicsSettings.renderPipelineAsset is UniversalRenderPipelineAsset)
            {
                renderPipelineURP = true;
            }
            else
            {
                renderPipelineURP = false;
            }
# endif

            //NET Framework
            if (PlayerSettings.GetApiCompatibilityLevel(BuildTargetGroup.Standalone) == ApiCompatibilityLevel.NET_Unity_4_8)
            {
                netFramework = true;
            }
            else
            {
                netFramework = false;
            }

        }

        private bool PackageExists(string packageName, string assemblyGUID)
        {
            //check if package is in project
            if (listRequest.Status == StatusCode.Success)
            {
                foreach (var package in listRequest.Result)
                {
                    if (package.name == packageName)
                    {
                        return true;
                    }
                }
            }
            else if (listRequest.Status >= StatusCode.Failure)
            {
                UnityEngine.Debug.LogError("Failed to list packages: " + listRequest.Error.message);
            }

            //check if assembly is there
            if (assemblyFileNames.Contains(assemblyGUID))
            {
                return true;
            }

            return false;
        }
        #endregion

        #region GUI Functions
        private void OnGUI()
        {
            // Define the GUIStyle
            //----------------------------------------------------
            iconStyle = new(EditorStyles.label);
            titleStyle = new GUIStyle(GUI.skin.label);
            labelStyle = new GUIStyle(GUI.skin.label);
            arrowStyle = new GUIStyle(GUI.skin.label);
            lineStyles = new GUIStyle[] { GUI.skin.box, GUI.skin.textField }; // Different line styles for alternating colors
            fixAllStyle = new GUIStyle(GUI.skin.button) { margin = new RectOffset(0, 7, 0, 0) };
            iconStyle.richText = true;
            titleStyle.fontSize = 20;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.fontSize = 12;
            arrowStyle.fontSize = 16;
            //-----------------------------------------------------


            // Draw the title text with the specified GUIStyle
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("WELCOME TO REFLECTIS", titleStyle);

            // Draw a horizontal line beneath the title
            EditorGUILayout.Space();
            Rect lineRect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(lineRect, Color.black);
            GUILayout.Space(10);

            CreateGeneralSetupGUI();

            GUILayout.Space(10);
            lineRect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUILayout.Space();
            EditorGUI.DrawRect(lineRect, Color.black);

            CreatePackagesSetupGUI();

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Open Creator Kit Documentation", EditorStyles.linkLabel))
            {
                Application.OpenURL("https://reflectis.io/docs/2024.4/CK/intro");
            }
            GUILayout.EndHorizontal();
        }

        private void CreateGeneralSetupGUI()
        {
            //---------------------------------------------------------------
            //---------------------------------------------------------------Platform Build Support
            GUILayout.Label("Platform build support", EditorStyles.boldLabel);
            GUILayout.Space(5);

            //General project setup
            int currentLineStyleIndex = 0;
            GUIStyle lineStyle = lineStyles[currentLineStyleIndex];

            foreach (KeyValuePair<string, bool> element in supportedPlatform)
            {
                buttonFunction = new Action(() =>
                {
                    if (EditorUtility.DisplayDialog("Build Support", "You need to install the " + element.Key + " build support from the Unity Hub", "Ok"))
                    {

                    }
                });
                CreateSettingFixField(element.Key, element.Value, "You have to install the " + element.Key + " build support from the Unity Hub", currentLineStyleIndex, buttonFunction);
                currentLineStyleIndex = (currentLineStyleIndex + 1) % lineStyles.Length;
            }

            //---------------------------------------------------------------
            //---------------------------------------------------------------Project Settings
            GUILayout.Space(10);
            GUILayout.Label("Project settings", EditorStyles.boldLabel);
            GUILayout.Space(6);

            //Graphic Setting

            //if urp package is installed do this, otherwise don't show it
            if (URPInstalled)
            {
                buttonFunction = new Action(() =>
                {
                    SetURPRenderPipeline();

                });

                CreateSettingFixField("URP as Render Pipeline", renderPipelineURP, "You need to set URP as your render pipeline", currentLineStyleIndex, buttonFunction);
                currentLineStyleIndex = (currentLineStyleIndex + 1) % lineStyles.Length;
            }

            //Net Framework
            buttonFunction = new Action(() =>
            {
                SetNetFramework();
            });

            CreateSettingFixField("Net Framework compability Level", netFramework, "You need to set .NET Framework in the Api Compability Level field", currentLineStyleIndex, buttonFunction);
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

        private void CreatePackagesSetupGUI()
        {
            GUILayout.BeginVertical();

            GUILayout.Label("Packages", EditorStyles.boldLabel);
            GUILayout.Space(5);
            //Github part
            GUILayout.BeginHorizontal(lineStyles[1]);
            EditorGUILayout.LabelField($"{(isGitInstalled ? "<b>[<color=lime>√</color>]</b>" : "<b>[<color=red>X</color>]</b>")}", iconStyle, GUILayout.Width(20));
            GUILayout.Label("Github v. " + gitVersion, labelStyle);
            GUILayout.FlexibleSpace();
            if (isGitInstalled)
                GUI.enabled = false;
            if (GUILayout.Button(new GUIContent("Fix", "You have to install github in order to install packages"), GUILayout.Width(80)))
            {
                string gitDownloadUrl = "https://git-scm.com/downloads";
                Application.OpenURL(gitDownloadUrl);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            int currentLineStyleIndex = 0;
            GUIStyle lineStyle = lineStyles[currentLineStyleIndex];
            //Packages
            for (int i = 0; i < packageList.Count; i++)
            {

                buttonFunction = new Action(() =>
                {
                    InstallPackages(packageList[i].packageName, packageList[i].gitURL, packageList[i].isGitPackage);
                });

                CreateSettingFixField(packageList[i].displayedName, packageList[i].installed, "You need to install the " + packageList[i].displayedName + " package using the package manager", currentLineStyleIndex, buttonFunction);
                currentLineStyleIndex = (currentLineStyleIndex + 1) % lineStyles.Length;
            }

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Fix All", fixAllStyle, GUILayout.Width(80)))
            {
                FixAllPackages();
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void CreateSettingFixField(string name, bool valueToCheck, string buttonDescription, int currentLineStyleIndex, Action buttonFunction)
        {
            GUIStyle lineStyle = lineStyles[currentLineStyleIndex];

            GUILayout.BeginHorizontal(lineStyle);
            EditorGUILayout.LabelField($"{(valueToCheck ? "<b>[<color=lime>√</color>]</b>" : "<b>[<color=red>X</color>]</b>")}", iconStyle, GUILayout.Width(20));
            GUILayout.Label(name, labelStyle);
            GUILayout.FlexibleSpace();

            if (valueToCheck)
                GUI.enabled = false;

            //GUI.Box(new Rect(5, 35, 110, 75), new GUIContent("Box", "this box has a tooltip"));
            if (GUILayout.Button(new GUIContent("Fix", buttonDescription), GUILayout.Width(80)))
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
                }

            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
        #endregion

        private void InstallPackages(string packageName, string gitUrl, bool isGitPackage)
        {
            if (isGitPackage)
            {
                string manifestFilePath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
                string manifestJson = File.ReadAllText(manifestFilePath);
                JObject manifestObj = JObject.Parse(manifestJson);

                JObject dependencies = (JObject)manifestObj["dependencies"];
                dependencies[packageName] = gitUrl;
                UnityEngine.Debug.Log($"Git package {packageName} added to manifest.json");

                File.WriteAllText(manifestFilePath, manifestObj.ToString());
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                RefreshWindow();
            }
            else
            {
                addRequest = Client.Add(packageName);
                EditorApplication.update += Progress;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RefreshWindow();

        }

        private void SetURPRenderPipeline()
        {
            string[] guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);

#if UNITY_URP_INSTALLED
            var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            GraphicsSettings.renderPipelineAsset = urpAsset;
            QualitySettings.renderPipeline = urpAsset;
#endif

        }

        private void SetNetFramework()
        {
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone, ApiCompatibilityLevel.NET_Unity_4_8);
        }

        private void FixAllProjectSettings()
        {
            SetURPRenderPipeline();
            SetNetFramework();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshWindow();
        }

        private void FixAllPackages()
        {
            for (int i = 0; i < packageList.Count; i++)
            {
                InstallPackages(packageList[i].packageName, packageList[i].gitURL, packageList[i].isGitPackage);
            }
        }

        /*public bool URPPackageInstalled()
        {
            for (int i = 0; i < packageList.Count; i++)
            {
                if (packageList[i].packageName == "com.unity.render-pipelines.universal")
                {
                    if (packageList[i].installed)
                    {
                        return true;
                    }
                }
            }
            return false;
        }*/


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

        private void RefreshWindow()
        {
            InitializePackages(); // Re-initialize packages to reflect current status
            CheckGitInstallation();
            CheckGeneralSetup();
            Repaint(); // Request Unity to redraw the window
        }
    }
}
