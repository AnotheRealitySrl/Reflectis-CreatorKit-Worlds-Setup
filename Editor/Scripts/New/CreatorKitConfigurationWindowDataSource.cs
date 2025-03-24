using UnityEngine;

namespace Reflectis.CreatorKit.Worlds.Installer.Editor
{
    [CreateAssetMenu(fileName = "CreatorKitConfigurationWindowDataSource", menuName = "Reflectis/Creator-Kit-Installer/CreatorKitConfigurationWindowDataSource")]
    public class CreatorKitConfigurationWindowDataSource : ScriptableObject
    {
        [HideInInspector] public bool isGitInstalled;
        [HideInInspector] public string gitVersion;


        //private bool UnityVersionIsMatching => InternalEditorUtility.GetFullUnityVersion().Split(' ')[0] == allVersionsPackageRegistries[displayedReflectisVersionIndex].RequiredUnityVersion;
        //private bool AllEditorModulesInstalled => !installedModules.Values.Contains(false);

        public bool renderPipelineURP;
        public bool playerSettings;
        public bool maxTextureSizeOverride;


        public PackageRegistry selectedVersionPackageRegistry;

        public string displayedReflectisVersion;

    }
}
