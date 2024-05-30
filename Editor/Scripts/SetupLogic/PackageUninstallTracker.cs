using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Reflectis.SetupEditor
{
    [InitializeOnLoad]
    public class PackageUninstallTracker
    {
        private static string packageNameBeingUninstalled;
        private static string symbolToRemove = "UNITY_URP_INSTALLED";

        static PackageUninstallTracker()
        {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            Events.registeredPackages += OnRegisteredPackages;
        }
        private static void OnRegisteredPackages(PackageRegistrationEventArgs args)
        {
            // Iterate through the packages that were removed
            foreach (var removedPackage in args.removed)
            {
                // Capture the name of the package being uninstalled
                packageNameBeingUninstalled = removedPackage.name;
                if (packageNameBeingUninstalled == "com.unity.render-pipelines.universal")
                {
                    // Get the current build target group
                    BuildTargetGroup buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;

                    // Get the current scripting define symbols
                    string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);

                    // Split the symbols into a list
                    var defineSymbols = new System.Collections.Generic.List<string>(defines.Split(';'));

                    // Check if the symbol exists and remove it
                    if (defineSymbols.Contains(symbolToRemove))
                    {
                        defineSymbols.Remove(symbolToRemove);

                        // Join the symbols back into a string
                        string newDefines = string.Join(";", defineSymbols.ToArray());

                        // Set the updated scripting define symbols
                        PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, newDefines);

                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                    }
                    else
                    {
                        Debug.LogWarning($"Symbol '{symbolToRemove}' not found.");
                    }
                }
            }
        }

        private static void OnBeforeAssemblyReload()
        {
            if (!string.IsNullOrEmpty(packageNameBeingUninstalled))
            {
                // Handle the package uninstallation before assembly reload
                Debug.Log($"Before assembly reload: Package '{packageNameBeingUninstalled}' was uninstalled.");
            }
        }
    }
}
