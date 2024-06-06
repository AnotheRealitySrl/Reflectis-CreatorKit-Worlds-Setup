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
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Reflectis.SetupEditor
{
    [InitializeOnLoad]
    public class SetupReflectisWindow : EditorWindow
    {
        #region Reflectis JSON data variables
        private string reflectisPrefs = "ReflectisVersion"; //string used to know which reflectis version is installed. Set The first time you press the setup button. 
        private ReflectisJSON reflectisJSON;
        private static string reflectisJSONstring;
        #endregion

        int reflectisSelectedVersion;

        #region booleanValues
        private bool isGitInstalled = false;
        private string gitVersion = "";
        private bool URPInstalled = true;
        private bool renderPipelineURP = false;
        private bool netFramework = false;
        private static string reflectisSetupShown = "EditorWindowAlreadyShown";

        private bool allSettingsFixed = true;
        private bool allPlatformFixed = true;
        private bool allCoreInstalled = true;

        private bool showPlatformSupport = false;
        private bool showProjectSettingss = false;
        #endregion


        #region package and platform lists
        public Dictionary<string, bool> supportedPlatform = new Dictionary<string, bool>
        {
            { "Android", false },
            { "WebGL", false },
            { "Windows", false }
        };
        [SerializeField] private List<ReflectisPackage> corePackageList = new List<ReflectisPackage>();
        [SerializeField] private List<ReflectisPackage> optionalPackageList = new List<ReflectisPackage>();

        private List<string> assemblyFileNames = new List<string>();
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
        GUIContent warningIconContent;
        GUIContent errorIconContent;
        GUIContent confirmedIcon;

        public GUIContent[] tabContents = new GUIContent[]
        {
            new GUIContent(" Core"),
            new GUIContent(" Optional")
        };

        int selectedTab = 0;

        private Vector2 scrollPosition = Vector2.zero;
        #endregion

        private AddRequest addRequest;
        private Action buttonFunction;

        ListRequest listRequest; //used to keep track of the installed packages

        //this variable is set by the other packages (like CK, used to show buttons and other GUI elements).
        public static UnityEvent configurationEvents = new UnityEvent();


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

        private void Awake()
        {
            //Load json data
            if (reflectisJSONstring == null)
            {
                reflectisJSONstring = GetReflectisJSON();
            }

            //EditorCoroutineUtility.StartCoroutine(GetReflectisJSONweb(), this);

            reflectisJSON = JsonUtility.FromJson<ReflectisJSON>(reflectisJSONstring);

            //Get reflectis version and update list of packages
            string reflectisVersion = EditorPrefs.GetString(reflectisPrefs);
            if (reflectisPrefs != "" && reflectisPrefs != null && !System.String.IsNullOrEmpty(reflectisVersion))
            {
                SetPackagesBasedOnVersion(reflectisVersion);
            }
            else
            {
                reflectisSelectedVersion = 0;
                corePackageList = reflectisJSON.reflectisVersions[0].reflectisPackages;
                optionalPackageList = reflectisJSON.reflectisVersions[0].optionalPackages;
            }

            GetAllAssemblyFiles();
            ClientListCall();
            CheckGitInstallation();
            CheckGeneralSetup();

            if (PlayerPrefs.HasKey(reflectisSetupShown))
            {
                EditorApplication.quitting += OnUnityQuit;
            }
        }

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
            allCoreInstalled = true;
            allPlatformFixed = true;
            allSettingsFixed = true;
            CheckGitInstallation();
            ClientListCall(); //call the manifest and retrieve the isntalled packages again. That way the page will be updated.
            CheckGeneralSetup();

            Repaint(); // Request Unity to redraw the window
        }

        #region GUI Functions
        private void OnGUI()
        {
            // Define the GUIStyle
            //----------------------------------------------------
            iconStyle = new(EditorStyles.label);
            titleStyle = new GUIStyle(GUI.skin.label);
            labelStyle = new GUIStyle(GUI.skin.label);
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

            warningIconContent = EditorGUIUtility.IconContent("DotFill");
            errorIconContent = EditorGUIUtility.IconContent("console.erroricon");
            confirmedIcon = EditorGUIUtility.IconContent("d_winbtn_mac_max"); //Assets / Editor Default Resources/ Icons

            toggleStyle = new GUIStyle("Foldout");
            toggleStyle.fixedHeight = 18f;
            toggleStyle.fontStyle = FontStyle.Bold;

            tabContents = new GUIContent[]
            {
                new GUIContent(" Core", allCoreInstalled ? confirmedIcon.image : errorIconContent.image),
                new GUIContent(" Optional")
            };
            // Create a custom GUIStyle for the toolbar buttons with bold font
            boldTabStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                fontStyle = FontStyle.Bold
            };

            GUIStyle lineStyle = lineStyles[0];
            //-----------------------------------------------------

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false);

            // Draw the title text with the specified GUIStyle
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("WELCOME TO REFLECTIS", titleStyle);
            EditorGUILayout.Space();
            Rect lineRect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(lineRect, Color.black);
            GUILayout.Space(20);

            //tabs
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < tabContents.Length; i++)
            {
                if (optionalPackageList.Count != 0)
                {
                    if (GUILayout.Toggle(selectedTab == i, tabContents[i], boldTabStyle))
                    {
                        selectedTab = i;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginVertical();
            GUILayout.Space(20);
            switch (selectedTab)
            {
                case 0:

                    //Github part
                    GUILayout.BeginHorizontal();
                    //EditorGUILayout.LabelField($"{(isGitInstalled ? "<b>[<color=lime>√</color>]</b>" : "<b>[<color=red>X</color>]</b>")}", iconStyle, GUILayout.Width(20));
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
                    GUILayout.EndHorizontal();

                    //Core tab
                    CreateGeneralSetupGUI();
                    GUILayout.Space(20);

                    lineRect = EditorGUILayout.GetControlRect(false, 1);
                    EditorGUI.DrawRect(lineRect, Color.black);
                    GUILayout.Space(15);
                    SetupReflectisVersionGUI();
                    GUILayout.Space(15);
                    lineRect = EditorGUILayout.GetControlRect(false, 1);
                    EditorGUI.DrawRect(lineRect, Color.black);
                    GUILayout.Space(25);

                    //Creator Kit Buttons
                    configurationEvents.Invoke(); //add element to tab too(?)
                    break;
                case 1:
                    //Optional tab
                    GUILayout.Space(20);
                    CreatePackagesSetupGUI(optionalPackageList);
                    break;
                default:
                    break;

            }
            EditorGUILayout.EndVertical();

            //Documentation Link
            EditorGUILayout.BeginVertical();
            GUILayout.Space(20);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Open Creator Kit Documentation", EditorStyles.linkLabel))
            {
                Application.OpenURL("https://reflectis.io/docs/2024.4/CK/intro");
            }

            GUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            GUILayout.EndScrollView();
        }

        private void SetupReflectisVersionGUI()
        {
            string[] reflectisVersions = new string[reflectisJSON.reflectisVersions.Count];
            for (int i = 0; i < reflectisJSON.reflectisVersions.Count; i++)
            {
                reflectisVersions[i] = reflectisJSON.reflectisVersions[i].version;
            }

            GUILayout.BeginVertical(new GUIStyle(lineStyles[1]));
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Choose your preferred Reflectis version", headerStyle, GUILayout.Width(250));

            EditorGUI.BeginChangeCheck();
            reflectisSelectedVersion = EditorGUILayout.Popup(reflectisSelectedVersion, reflectisVersions);
            if (EditorGUI.EndChangeCheck())
            {
                //set allCoreInstalled to true so to make it update the boolean to show correct icons, the boolean is then updated in the different functions.
                allCoreInstalled = true;
                //update the core and optional packages list
                corePackageList = reflectisJSON.reflectisVersions[reflectisSelectedVersion].reflectisPackages;
                optionalPackageList = reflectisJSON.reflectisVersions[reflectisSelectedVersion].optionalPackages;
            }
            GUILayout.EndHorizontal();

            DisplayCorePackageList(corePackageList);
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Setup Reflectis", GUILayout.MinWidth(350)))
            {
                SetupReflectis(corePackageList, reflectisJSON.reflectisVersions[reflectisSelectedVersion].version);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DisplayCorePackageList(List<ReflectisPackage> reflectisPackages)
        {
            foreach (ReflectisPackage rpkg in reflectisPackages)
            {
                EditorGUI.BeginDisabledGroup(true);
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent(CheckReflectisDependencies(rpkg, true) ? confirmedIcon.image : errorIconContent.image), GUILayout.Width(20));
                EditorGUILayout.LabelField(rpkg.displayedName + " " + rpkg.version);
                GUILayout.EndHorizontal();
                EditorGUI.EndDisabledGroup();
            }
        }

        private bool CheckReflectisDependencies(ReflectisPackage rpkg, bool isCore)
        {
            bool installed = PackageExists(rpkg.name, "", rpkg.version);
            if (isCore)
            {
                if (!installed)
                {
                    allCoreInstalled = false;
                }
            }
            return installed;
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
                if (URPInstalled)
                {
                    buttonFunction = new Action(() =>
                    {
                        SetURPRenderPipeline();

                    });

                    CreateSettingFixField("URP as Render Pipeline", renderPipelineURP, "You need to set URP as your render pipeline", currentLineStyleIndex, buttonFunction, errorIconContent, "Fix");
                    currentLineStyleIndex = (currentLineStyleIndex + 1) % lineStyles.Length;
                }

                //Net Framework
                buttonFunction = new Action(() =>
                {
                    SetNetFramework();
                });

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

        private void CreatePackagesSetupGUI(List<ReflectisPackage> packageList)
        {
            GUIContent iconContent = warningIconContent;
            GUILayout.BeginVertical();

            GUILayout.Label("Optional Packages", EditorStyles.boldLabel);
            GUILayout.Space(5);

            int currentLineStyleIndex = 0;
            GUIStyle lineStyle = lineStyles[currentLineStyleIndex];

            //Core Packages
            for (int i = 0; i < packageList.Count; i++)
            {

                buttonFunction = new Action(() =>
                {
                    InstallPackages(packageList[i].name, packageList[i].gitUrl, true);
                });

                CreateSettingFixField(packageList[i].displayedName + " v." + packageList[i].version, CheckReflectisDependencies(packageList[i], false), "You need to install the " + packageList[i].displayedName + " package using the package manager", currentLineStyleIndex, buttonFunction, iconContent, "Install");
                currentLineStyleIndex = (currentLineStyleIndex + 1) % lineStyles.Length;
            }

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Install All", fixAllStyle, GUILayout.Width(80)))
            {
                FixAllPackages(packageList);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void CreateSettingFixField(string name, bool valueToCheck, string buttonDescription, int currentLineStyleIndex, Action buttonFunction, GUIContent errorIcon, string buttonText)
        {
            GUIStyle lineStyle = lineStyles[currentLineStyleIndex];

            GUILayout.BeginHorizontal(lineStyle);

            //EditorGUILayout.LabelField($"{(valueToCheck ? "<b>[<color=lime>√</color>]</b>" : "<b>[<color=red>X</color>]</b>")}", iconStyle, GUILayout.Width(20));
            EditorGUILayout.LabelField(new GUIContent(valueToCheck ? confirmedIcon.image : errorIcon.image), GUILayout.Width(20));
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
            GUILayout.EndHorizontal();
        }
        #endregion

        #region General Checks And Get Functions

        //Get json data from API
        private string GetReflectisJSON()
        {
            //for now load the json via the resource folder. In the future calla an api to retrieve the json
            TextAsset jsonData = Resources.Load<TextAsset>("jsonExample");
            return jsonData.text;
        }

        /*
         * TODO Replace the link with the reflectis json url
         * private IEnumerator GetReflectisJSONweb()
        {
            UnityEngine.Debug.LogError("Entered enumerator");
            UnityWebRequest www = new UnityWebRequest("https://reqbin.com/echo/get/json", UnityWebRequest.kHttpVerbGET);

            www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("User-Agent", "DefaultBrowser");
            www.SetRequestHeader("Cookie", string.Format("DummyCookie"));

            yield return www.Send();
            if (www.isNetworkError || www.isHttpError)
            {
                UnityEngine.Debug.LogError(www.error);
            }
            else
            {
                // Show results as text
                reflectisJSONstring = www.downloadHandler.text;
                UnityEngine.Debug.LogError(reflectisJSONstring);
            }
        }*/

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
                        gitVersion = output;
                        isGitInstalled = true;
                    }
                    else
                    {
                        isGitInstalled = false;
                        allCoreInstalled = false;
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
                CheckBuildTarget(group, BuildTarget.Android, "Android");
                CheckBuildTarget(group, BuildTarget.StandaloneWindows, "Windows");
                CheckBuildTarget(group, BuildTarget.WebGL, "WebGL");
            }

            //update the coreInstalled value
            foreach (var element in supportedPlatform)
            {
                if (element.Value == false)
                {
                    allCoreInstalled = false;
                    allPlatformFixed = false;
                }
            }

            //URP Pipeline
            //#if UNITY_URP_INSTALLED
            if (GraphicsSettings.renderPipelineAsset is UniversalRenderPipelineAsset)
            {
                renderPipelineURP = true;
            }
            else
            {
                renderPipelineURP = false;
                allCoreInstalled = false;
                allSettingsFixed = false;
            }
            //# endif

            //NET Framework
            if (PlayerSettings.GetApiCompatibilityLevel(BuildTargetGroup.Standalone) == ApiCompatibilityLevel.NET_Unity_4_8)
            {
                netFramework = true;
            }
            else
            {
                netFramework = false;
                allCoreInstalled = false;
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

        private bool PackageExists(string packageName, string assemblyGUID, string version)
        {
            //check if package is in project
            if (listRequest.Status == StatusCode.Success)
            {
                foreach (var package in listRequest.Result)
                {
                    if (package.name == packageName)
                    {
                        if (package.version == version)
                        {
                            return true;
                        }
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

        #region ButtonsLogic Functions
        private void InstallPackages(string packageName, string gitUrl, bool isGitPackage)
        {
            if (isGitPackage)
            {
                string manifestFilePath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
                string manifestJson = File.ReadAllText(manifestFilePath);
                JObject manifestObj = JObject.Parse(manifestJson);

                JObject dependencies = (JObject)manifestObj["dependencies"];
                dependencies[packageName] = gitUrl;
                //UnityEngine.Debug.Log($"Git package {packageName} added to manifest.json " + gitUrl);

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

                //#if UNITY_URP_INSTALLED
                var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
                GraphicsSettings.renderPipelineAsset = urpAsset;
                QualitySettings.renderPipeline = urpAsset;
                //#endif
            }

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

        private void FixAllPackages(List<ReflectisPackage> packageList)
        {
            for (int i = 0; i < packageList.Count; i++)
            {
                InstallPackages(packageList[i].name, packageList[i].gitUrl, true);
            }
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

        //install the reflectis core packages
        private void SetupReflectis(List<ReflectisPackage> reflectisDependencies, string version)
        {

            //set the reflectis version in the editor prefs
            EditorPrefs.SetString(reflectisPrefs, version);

            //install the core packages for the selected version
            foreach (ReflectisPackage pkg in reflectisDependencies)
            {
                //Install the package
                UnityEngine.Debug.LogError(pkg.gitUrl);
                InstallPackages(pkg.name, pkg.gitUrl, true);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshWindow();
        }
        #endregion

        private void SetPackagesBasedOnVersion(string reflectisVersion)
        {
            int i = 0;
            foreach (ReflectisVersion rv in reflectisJSON.reflectisVersions)
            {
                if (rv.version == reflectisVersion)
                {
                    reflectisSelectedVersion = i;
                    corePackageList = rv.reflectisPackages;
                    optionalPackageList = rv.optionalPackages;
                }
                i++;
            }

        }


    }
}

