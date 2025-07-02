#if REFLECTIS_CREATOR_KIT_WORLDS_PLACEHOLDERS
using Reflectis.CreatorKit.Worlds.Placeholders;
#endif
#if REFLECTIS_CREATOR_KIT_WORLDS_TASKS
using Reflectis.CreatorKit.Worlds.Tasks;
#endif
#if REFLECTIS_CREATOR_KIT_WORLDS_VISUAL_SCRIPTING
using Reflectis.CreatorKit.Worlds.VisualScripting;
#endif
using Reflectis.SDK.Core.Utilities; // Ensure this GetOrAddComponent utility is defined and works correctly
using UnityEditor;
using UnityEngine;

namespace Reflectis.CreatorKit.Worlds.Installer.Editor
{
    /// <summary>
    /// Editor script to solve breaking changes from v2025.3 to v2025.4 by updating components in prefabs and scenes.
    /// This script handles the proper way to modify prefab assets in Editor mode.
    /// </summary>
    public static class BreakingChangesSolver2025_4
    {
        /// <summary>
        /// Adds a menu item to the Unity Editor to run the update routine.
        /// </summary>
        [MenuItem("Reflectis Worlds/Creator Kit update routines/v2025.3 -> v2025.4")]
        public static void SolveBreakingChanges()
        {
#if REFLECTIS_CREATOR_KIT_WORLDS_PLACEHOLDERS
            ReplaceInteractablePlaceholderInAssetsAndScenes();
#endif
        }

        /// <summary>
        /// Main routine to iterate through all prefabs and scenes to replace and fix components.
        /// </summary>
        private static void ReplaceInteractablePlaceholderInAssetsAndScenes()
        {
            // Store the path of the currently active scene to restore it later.
            string activeScenePath = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;

            // --- PHASE 1: Replace components in Prefabs ---
            string[] prefabPathsGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (string prefabPathGuid in prefabPathsGuids)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabPathGuid);

                // IMPORTANT: Load the prefab contents into a temporary, modifiable GameObject instance.
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

                try
                {
                    // Attempt to replace components recursively within this temporary prefab instance.
                    bool modified = ReplaceComponentsRecursiveInPrefab(prefabRoot);
                    if (modified)
                    {
                        // If modifications were made, save the changes back to the original prefab asset.
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                        Debug.Log($"Components replaced in prefab: {prefabPath}");
                    }
                }
                finally
                {
                    // CRUCIAL: Unload the temporary prefab contents to release resources and avoid memory leaks.
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            // --- PHASE 2: Fix Detectors in Prefabs ---
            foreach (string prefabPathGuid in prefabPathsGuids)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabPathGuid);
                // Reload the prefab contents as the previous operation unloaded it.
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

                try
                {
                    // Attempt to fix detectors within this temporary prefab instance.
                    if (FixDetector(prefabRoot))
                    {
                        // If fixes were made, save the changes back to the original prefab asset.
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                        Debug.Log($"Detectors fixed in prefab: {prefabPath}");
                    }
                }
                finally
                {
                    // Unload the temporary prefab contents.
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            // --- PHASE 3: Replace components in Scenes ---
            string[] scenePathsGuids = AssetDatabase.FindAssets("t:Scene");
            foreach (string scenePathGuid in scenePathsGuids)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(scenePathGuid);
                // Skip package scenes to avoid modifying external assets.
                if (!scenePath.StartsWith("Packages/"))
                {
                    // Open each scene individually.
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

                    // Replace components within the currently open scene.
                    if (ReplaceComponentsInScene())
                    {
                        // Save the modified scene.
                        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
                        Debug.Log($"Components replaced in scene: {scenePath}");
                    }
                }
            }

            // --- PHASE 4: Fix Detectors in Scenes ---
            foreach (string scenePathGuid in scenePathsGuids)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(scenePathGuid);
                if (!scenePath.StartsWith("Packages/"))
                {
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

                    if (FixDetectorsInScene())
                    {
                        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
                        Debug.Log($"Detectors fixed in scene: {scenePath}");
                    }
                }
            }

            // --- PHASE 5: Destroy old components in Prefabs ---
            foreach (string prefabPathGuid in prefabPathsGuids)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabPathGuid);
                // Reload prefab contents for this operation.
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

                try
                {
                    // Destroy old components recursively within the temporary prefab instance.
                    if (DestroyOldComponentRecursiveInPrefab(prefabRoot))
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                        Debug.Log($"Old components destroyed in prefab: {prefabPath}");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            // --- PHASE 6: Destroy old components in Scenes ---
            foreach (string scenePathGuid in scenePathsGuids)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(scenePathGuid);
                if (!scenePath.StartsWith("Packages/"))
                {
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

                    if (DestroyOldComponentInScene())
                    {
                        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
                        Debug.Log($"Old components destroyed in scene: {scenePath}");
                    }
                }
            }

            // Re-open the original scene after all modifications are done.
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(activeScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            AssetDatabase.Refresh(); // Ensure Unity reloads all modified assets in the editor.

            EditorUtility.DisplayDialog("Success", "Creator kit update completed!", "OK");
        }

        /// <summary>
        /// Recursively replaces obsolete components with new ones within a prefab's hierarchy.
        /// Operates on the modifiable prefab instance loaded by PrefabUtility.LoadPrefabContents.
        /// </summary>
        /// <param name="rootGameObject">The root GameObject of the prefab instance.</param>
        /// <returns>True if any components were replaced, false otherwise.</returns>
        private static bool ReplaceComponentsRecursiveInPrefab(GameObject rootGameObject)
        {
            bool modified = false;
            // GetComponentsInChildren(true) finds components on the root and all children, including inactive ones.
            InteractablePlaceholderObsolete[] interactables = rootGameObject.GetComponentsInChildren<InteractablePlaceholderObsolete>(true);

            foreach (var interactable in interactables)
            {
                if (interactable != null)
                {
                    ReplaceInteractablePlaceholder(interactable);
                    modified = true;
                }
            }
            return modified;
        }

        /// <summary>
        /// Recursively destroys obsolete components within a prefab's hierarchy.
        /// Operates on the modifiable prefab instance loaded by PrefabUtility.LoadPrefabContents.
        /// </summary>
        /// <param name="rootGameObject">The root GameObject of the prefab instance.</param>
        /// <returns>True if any components were destroyed, false otherwise.</returns>
        private static bool DestroyOldComponentRecursiveInPrefab(GameObject rootGameObject)
        {
            bool modified = false;
            // Get all obsolete components. Convert to list to safely remove items during iteration.
            InteractablePlaceholderObsolete[] interactables = rootGameObject.GetComponentsInChildren<InteractablePlaceholderObsolete>(true);

            foreach (var interactable in interactables)
            {
                if (interactable != null)
                {
                    // Mark the GameObject as dirty before destroying its component, so Unity registers the change.
                    EditorUtility.SetDirty(interactable.gameObject);
                    // DestroyImmediate is used in Editor scripts for immediate removal. 'true' means allow destroy for assets.
                    UnityEngine.Object.DestroyImmediate(interactable, true);
                    modified = true;
                    Debug.Log($"Destroyed old component in {interactable.gameObject.name}");
                }
            }
            return modified;
        }


        // *********************************************************************************************************************
        // The following functions do not require substantial changes as they operate on scene instances
        // or on the temporary GameObject loaded by PrefabUtility.LoadPrefabContents, both of which are modifiable.
        // I have renamed ReplaceComponentsInPrefab and DestroyOldComponentInPrefab for clarity with the new recursive methods.
        // *********************************************************************************************************************

        /// <summary>
        /// Fixes detector references within the given GameObject's hierarchy (used for both prefabs and scenes).
        /// </summary>
        /// <param name="rootGameObject">The root GameObject to start searching from.</param>
        /// <returns>True if any detectors were fixed, false otherwise.</returns>
        private static bool FixDetector(GameObject rootGameObject)
        {
#if REFLECTIS_CREATOR_KIT_WORLDS_TASKS
            // Find all relevant detector components in children, including inactive ones.
            var grabs = rootGameObject.GetComponentsInChildren<ManipulableGrabberDetector>(true);
#if REFLECTIS_CREATOR_KIT_WORLDS_VISUAL_SCRIPTING
            var hovers = rootGameObject.GetComponentsInChildren<VisualScriptingInteractableHoverDetector>(true);
#else
            var hovers = new VisualScriptingInteractableHoverDetector[0]; // Empty array if VS is not enabled
#endif
            bool changed = false;
            foreach (var grab in grabs)
            {
                if (grab.interactablePlaceholder != null)
                {
                    grab.manipulablePlaceholder = grab.interactablePlaceholder.GetComponentInChildren<ManipulablePlaceholder>(true);
                    EditorUtility.SetDirty(grab); // Mark the component as dirty to save changes.
                    changed = true;
                }
            }
            foreach (var hover in hovers)
            {
                if (hover.interactablePlaceholder != null)
                {
                    hover.visualscriptingPlaceholder = hover.interactablePlaceholder.GetComponentInChildren<VisualScriptingInteractablePlaceholder>(true);
                    EditorUtility.SetDirty(hover); // Mark the component as dirty to save changes.
                    changed = true;
                }
            }
            return changed;
#else
            return false;
#endif
        }

        /// <summary>
        /// Applies the detector fix to all root GameObjects in the active scene.
        /// </summary>
        /// <returns>True if any detectors were fixed in the scene, false otherwise.</returns>
        private static bool FixDetectorsInScene()
        {
#if REFLECTIS_CREATOR_KIT_WORLDS_TASKS
            GameObject[] gameObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            bool change = false;
            foreach (GameObject gameObject in gameObjects)
            {
                // Apply FixDetector to each root GameObject in the scene.
                if (FixDetector(gameObject))
                {
                    change = true;
                    Debug.LogError("Found detectors in " + gameObject.name + " in scene " + gameObject.scene.name, gameObject);
                }
            }
            return change;
#else
            return false;
#endif
        }

        /// <summary>
        /// Replaces obsolete components with new ones for all GameObjects in the active scene.
        /// </summary>
        /// <returns>True if any components were replaced in the scene, false otherwise.</returns>
        private static bool ReplaceComponentsInScene()
        {
            GameObject[] gameObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            bool modified = false;
            foreach (GameObject gameObject in gameObjects)
            {
                // Use GetComponentsInChildren to find all instances of the obsolete component within the GameObject's hierarchy.
                InteractablePlaceholderObsolete[] interactables = gameObject.GetComponentsInChildren<InteractablePlaceholderObsolete>(true);
                foreach (var interactable in interactables)
                {
                    if (interactable != null)
                    {
                        ReplaceInteractablePlaceholder(interactable);
                        modified = true;
                    }
                }
            }
            return modified;
        }

        /// <summary>
        /// Replaces a single InteractablePlaceholderObsolete component with the new InteractablePlaceholder
        /// and potentially adds other related placeholders based on its InteractionModes.
        /// </summary>
        /// <param name="interactable">The obsolete InteractablePlaceholderObsolete component to replace.</param>
        private static void ReplaceInteractablePlaceholder(InteractablePlaceholderObsolete interactable)
        {
            Debug.Log($"Replacing component in {interactable.gameObject.name} in scene {interactable.gameObject.scene.name}", interactable.gameObject);

            // Record the GameObject for Undo functionality in the Editor.
            UnityEditor.Undo.RecordObject(interactable.gameObject, "Replace Component");

            // Get or add the new InteractablePlaceholder component.
            InteractablePlaceholder interactionPlaceholder = interactable.gameObject.GetOrAddComponent<InteractablePlaceholder>();

            // Copy relevant properties from the old component to the new one.
            interactionPlaceholder.LockHoverDuringInteraction = interactable.LockHoverDuringInteraction;
            interactionPlaceholder.InteractionColliders = interactable.InteractionColliders;
            interactionPlaceholder.IsNetworked = interactable.IsNetworked;

            // Mark the new component as dirty so its changes are saved.
            EditorUtility.SetDirty(interactionPlaceholder);

            // Handle ContextualMenuPlaceholder based on old component's flags.
            if (interactable.InteractionModes.HasFlag(Core.Interaction.IInteractable.EInteractableType.ContextualMenuInteractable))
            {
                ContextualMenuPlaceholder contextualMenuPlaceholder = interactable.gameObject.GetOrAddComponent<ContextualMenuPlaceholder>();
                contextualMenuPlaceholder.ContextualMenuOptions = interactable.ContextualMenuOptions;
                EditorUtility.SetDirty(contextualMenuPlaceholder);
            }
            else
            {
                // If the flag is not set, ensure the component is removed if it exists.
                if (interactionPlaceholder.TryGetComponent<ContextualMenuPlaceholder>(out var cmp))
                {
                    UnityEngine.Object.DestroyImmediate(cmp, true);
                }
            }

            // Handle ManipulablePlaceholder based on old component's flags.
            if (interactable.InteractionModes.HasFlag(Core.Interaction.IInteractable.EInteractableType.Manipulable))
            {
                ManipulablePlaceholder manipulablePlaceholder = interactable.gameObject.GetOrAddComponent<ManipulablePlaceholder>();
                manipulablePlaceholder.ManipulationMode = interactable.ManipulationMode;
                manipulablePlaceholder.DynamicAttach = interactable.DynamicAttach;
                manipulablePlaceholder.AdjustRotationOnRelease = interactable.AdjustRotationOnRelease;
                manipulablePlaceholder.MouseLookAtCamera = interactable.MouseLookAtCamera;
                manipulablePlaceholder.RealignAxisX = interactable.RealignAxisX;
                manipulablePlaceholder.RealignAxisY = interactable.RealignAxisY;
                manipulablePlaceholder.RealignAxisZ = interactable.RealignAxisZ;
                manipulablePlaceholder.RealignDurationTimeInSeconds = interactable.RealignDurationTimeInSeconds;
                manipulablePlaceholder.VrInteraction = interactable.VRInteraction;
                manipulablePlaceholder.AttachTransform = interactable.AttachTransform;

                EditorUtility.SetDirty(manipulablePlaceholder);
            }
            else
            {
                if (interactionPlaceholder.TryGetComponent<ManipulablePlaceholder>(out var cmp))
                {
                    UnityEngine.Object.DestroyImmediate(cmp, true);
                }
            }

#if REFLECTIS_CREATOR_KIT_WORLDS_VISUAL_SCRIPTING
            // Handle VisualScriptingInteractablePlaceholder based on old component's flags.
            if (interactable.InteractionModes.HasFlag(Core.Interaction.IInteractable.EInteractableType.VisualScriptingInteractable))
            {
                VisualScriptingInteractablePlaceholder vsPlaceholder = interactable.gameObject.GetOrAddComponent<VisualScriptingInteractablePlaceholder>();
                vsPlaceholder.DesktopAllowedStates = interactable.DesktopAllowedStates;
                vsPlaceholder.VRAllowedStates = interactable.VRAllowedStates;
                vsPlaceholder.InteractionsScriptMachine = interactable.InteractionsScriptMachine;
                vsPlaceholder.VrVisualScriptingInteraction = interactable.VrVisualScriptingInteraction;

                EditorUtility.SetDirty(vsPlaceholder);
            }
            else
            {
                if (interactionPlaceholder.TryGetComponent<VisualScriptingInteractablePlaceholder>(out var cmp))
                {
                    UnityEngine.Object.DestroyImmediate(cmp, true);
                }
            }
#endif
            // Mark the GameObject itself as dirty if its components have changed.
            EditorUtility.SetDirty(interactionPlaceholder.gameObject);
        }

        /// <summary>
        /// Destroys obsolete components for all GameObjects in the active scene.
        /// </summary>
        /// <returns>True if any components were destroyed in the scene, false otherwise.</returns>
        private static bool DestroyOldComponentInScene()
        {
            GameObject[] gameObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            bool modified = false;
            foreach (GameObject gameObject in gameObjects)
            {
                // Use GetComponentsInChildren to ensure all obsolete components in the hierarchy are found.
                InteractablePlaceholderObsolete[] interactables = gameObject.GetComponentsInChildren<InteractablePlaceholderObsolete>(true);
                foreach (var interactable in interactables)
                {
                    if (interactable != null)
                    {
                        UnityEngine.Object.DestroyImmediate(interactable, true);
                        EditorUtility.SetDirty(gameObject); // Mark the GameObject as dirty after component destruction.
                        modified = true;
                    }
                }
            }
            return modified;
        }
    }
}

// Ensure you have this extension method or an equivalent for GetOrAddComponent.
// It might be in Reflectis.SDK.Core.Utilities or you might need to define it.
/*
namespace Reflectis.SDK.Core.Utilities
{
    public static class GameObjectExtensions
    {
        /// <summary>
        /// Gets a component of type T, or adds it if it doesn't exist on the GameObject.
        /// </summary>
        /// <typeparam name="T">The type of the component to get or add.</typeparam>
        /// <param name="obj">The GameObject to operate on.</param>
        /// <returns>The existing or newly added component.</returns>
        public static T GetOrAddComponent<T>(this GameObject obj) where T : Component
        {
            T component = obj.GetComponent<T>();
            if (component == null)
            {
                component = obj.AddComponent<T>();
            }
            return component;
        }
    }
}
*/