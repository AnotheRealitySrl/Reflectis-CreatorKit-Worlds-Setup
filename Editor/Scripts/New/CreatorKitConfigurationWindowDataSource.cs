using System;

using UnityEngine;

namespace Reflectis.CreatorKit.Worlds.Installer.Editor
{
    [CreateAssetMenu(fileName = "CreatorKitConfigurationWindowDataSource", menuName = "Reflectis/Creator-Kit-Installer/CreatorKitConfigurationWindowDataSource")]
    public class CreatorKitConfigurationWindowDataSource : ScriptableObject
    {
        [HideInInspector] public bool isGitInstalled;
        [HideInInspector] public string gitVersion;


        public bool unityVersionIsMatching;
        public bool allEditorModulesInstalled;

        public bool renderPipelineURP;
        public bool playerSettings;
        public bool maxTextureSizeOverride;


        public PackageRegistry[] allVersionsPackageRegistry;
        public PackageRegistry selectedVersionPackageRegistry;



        public int displayedReflectisVersionIndex;
        public string displayReflectisVersion;

        public string currentInstallationVersion;
        public string displayedReflectisVersion;


        public bool resolveBreakingChangesAutomatically;

        // Not serializable
        public DateTime lastRefreshDateTime;
    }
}
