#if REFLECTIS_CREATOR_KIT_WORLDS_PLACEHOLDERS
using Reflectis.CreatorKit.Worlds.Placeholders;
#endif
#if REFLECTIS_CREATOR_KIT_WORLDS_TASKS
using Reflectis.CreatorKit.Worlds.Tasks;
#endif
#if REFLECTIS_CREATOR_KIT_WORLDS_VISUAL_SCRIPTING
using Reflectis.CreatorKit.Worlds.VisualScripting;
#endif
using Reflectis.SDK.Core.Utilities;
using Unity.VisualScripting;
using UnityEditor;

using UnityEngine;

namespace Reflectis.CreatorKit.Worlds.Installer.Editor
{
    public static class BreakingChangesSolver2025_4
    {
        [MenuItem("Reflectis Worlds/Creator Kit update routines/v2025.3 -> v2025.4")]
        public static void SolveBreakingChanges()
        {
#if REFLECTIS_CREATOR_KIT_WORLDS_PLACEHOLDERS
            ReplaceInteractablePlaceholder();
#endif
        }
        private static void ReplaceInteractablePlaceholder()
        {
            string activeScenePath = "" + UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;

            string[] prefabPaths = AssetDatabase.FindAssets("t:Prefab");

            foreach (string prefabPathGuid in prefabPaths)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabPathGuid);
                GameObject prefab = PrefabUtility.LoadPrefabContents(prefabPath);

                if (ReplaceComponentsInPrefab(prefab))
                {
                    PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
                }
            }

            foreach (string prefabPathGuid in prefabPaths)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabPathGuid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (FixDetector(prefab))
                {
                    PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
                }
            }

            string[] scenePaths = AssetDatabase.FindAssets("t:Scene");

            foreach (string scenePathGuid in scenePaths)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(scenePathGuid);
                if (!scenePath.StartsWith("Packages/"))
                {
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

                    // Trova e sostituisci i componenti nella scena
                    if (ReplaceComponentsInScene())
                    {
                        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
                    }
                }
            }

            foreach (string scenePathGuid in scenePaths)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(scenePathGuid);
                if (!scenePath.StartsWith("Packages/"))
                {
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

                    if (FixDetectorsInScene())
                    {
                        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
                    }
                }
            }

            foreach (string prefabPathGuid in prefabPaths)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabPathGuid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (DestroyOldComponentInPrefab(prefab))
                {
                    PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
                }
            }

            foreach (string scenePathGuid in scenePaths)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(scenePathGuid);
                if (!scenePath.StartsWith("Packages/"))
                {
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

                    // Trova e sostituisci i componenti nella scena
                    if (DestroyOldComponentInScene())
                    {
                        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
                        Debug.LogWarning("Removed old components in scene: " + scenePath);
                    }
                }
            }
            if (!string.IsNullOrEmpty(activeScenePath))
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(activeScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            }
            EditorUtility.DisplayDialog("Success", "Creator kit update completed!", "OK");
        }

        private static bool FixDetector(GameObject prefab)
        {
#if REFLECTIS_CREATOR_KIT_WORLDS_TASKS
            var grabs = prefab.GetComponentsInChildren<ManipulableGrabberDetector>(true);
            var hovers = prefab.GetComponentsInChildren<VisualScriptingInteractableHoverDetector>(true);
            foreach (var grab in grabs)
            {
                if (grab.interactablePlaceholder != null)
                {
                    grab.manipulablePlaceholder = grab.interactablePlaceholder.GetComponentInChildren<ManipulablePlaceholder>(true);
                    EditorUtility.SetDirty(grab);
                    EditorUtility.SetDirty(grab.gameObject);
                }
            }
            foreach (var hover in hovers)
            {
                if (hover.interactablePlaceholder != null)
                {
                    hover.visualscriptingPlaceholder = hover.interactablePlaceholder.GetComponentInChildren<VisualScriptingInteractablePlaceholder>(true);
                    EditorUtility.SetDirty(hover);
                    EditorUtility.SetDirty(hover.gameObject);
                }
            }
            return grabs.Length > 0 || hovers.Length > 0;
#else
            return false;
#endif
        }

        private static bool FixDetectorsInScene()
        {
#if REFLECTIS_CREATOR_KIT_WORLDS_TASKS
            GameObject[] gameObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            bool change = false;
            foreach (GameObject gameObject in gameObjects)
            {
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


        private static bool ReplaceComponentsInScene()
        {
            GameObject[] gameObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            bool modified = false;
            foreach (GameObject gameObject in gameObjects)
            {
                RemoveMissingScripts(gameObject);

                var isModified = ReplaceComponentRecursive(gameObject);
                modified = modified || isModified;
            }
            return modified;
        }

        private static bool ReplaceComponentsInPrefab(GameObject prefab)
        {
            RemoveMissingScripts(prefab);

            var replaced = ReplaceComponentRecursive(prefab);

            return replaced;
        }

        private static bool ReplaceComponentRecursive(GameObject gameObject)
        {

            InteractablePlaceholderObsolete[] interactables = gameObject.GetComponents<InteractablePlaceholderObsolete>();
            bool modified = false;
            foreach (var interactable in interactables)
            {
                if (interactable != null)
                {
                    //If it is part of a prefab we first modify the sorce prefab and then replace the overrides in the instance.
                    if (PrefabUtility.IsPartOfPrefabInstance(interactable.gameObject))
                    {
                        GameObject sourcePrefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
                        if (sourcePrefabAsset != null)
                        {
                            string sourcePrefabPath = AssetDatabase.GetAssetPath(sourcePrefabAsset);
                            GameObject loadedSourcePrefab = PrefabUtility.LoadPrefabContents(sourcePrefabPath);
                            // Recursively process the loaded source prefab to ensure its components are replaced
                            bool sourceModified = ReplaceComponentRecursive(loadedSourcePrefab);
                            if (sourceModified)
                            {
                                PrefabUtility.SaveAsPrefabAsset(loadedSourcePrefab, sourcePrefabPath);
                                modified = true;
                            }
                            PrefabUtility.UnloadPrefabContents(loadedSourcePrefab);
                        }
                    }
                }
                ReplaceInteractablePlaceholder(interactable);
                modified = true;
            }
            foreach (Transform child in gameObject.transform)
            {
                var isModified = ReplaceComponentRecursive(child.gameObject);
                modified = modified || isModified;
            }
            return modified;
        }
        private static void ReplaceInteractablePlaceholder(InteractablePlaceholderObsolete interactable)
        {
            Debug.Log($"Replacing component in {interactable.gameObject.name} in scene {interactable.gameObject.scene.name}", interactable.gameObject);

            UnityEditor.Undo.RecordObject(interactable.gameObject, "Replace Component");

            InteractablePlaceholder interactionPlaceholder = interactable.gameObject.GetOrAddComponent<InteractablePlaceholder>();

            interactionPlaceholder.LockHoverDuringInteraction = interactable.LockHoverDuringInteraction;
            interactionPlaceholder.InteractionColliders = interactable.InteractionColliders;
            interactionPlaceholder.IsNetworked = interactable.IsNetworked;

            EditorUtility.SetDirty(interactionPlaceholder);

            if (interactable.InteractionModes.HasFlag(Core.Interaction.IInteractable.EInteractableType.ContextualMenuInteractable))
            {
                ContextualMenuPlaceholder contextualMenuPlaceholder = interactable.gameObject.GetOrAddComponent<ContextualMenuPlaceholder>();
                contextualMenuPlaceholder.ContextualMenuOptions = interactable.ContextualMenuOptions;
                EditorUtility.SetDirty(contextualMenuPlaceholder);
            }
            else
            {
                if (interactionPlaceholder.TryGetComponent<ContextualMenuPlaceholder>(out var cmp))
                {
                    UnityEngine.Object.DestroyImmediate(cmp, true);
                }
            }

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
            EditorUtility.SetDirty(interactionPlaceholder.gameObject);

        }

        private static bool DestroyOldComponentInScene()
        {
            GameObject[] gameObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            bool modified = false;
            foreach (GameObject gameObject in gameObjects)
            {
                var isModified = DestroyComponentRecursive(gameObject);
                modified = modified || isModified;
            }
            return modified;
        }

        private static bool DestroyOldComponentInPrefab(GameObject prefab)
        {
            var replaced = DestroyComponentRecursive(prefab);
            return replaced;
        }

        private static bool DestroyComponentRecursive(GameObject gameObject)
        {
            InteractablePlaceholderObsolete[] interactables = gameObject.GetComponents<InteractablePlaceholderObsolete>();
            bool modified = false;
            foreach (var interactable in interactables)
            {
                if (interactable != null)
                {
                    UnityEngine.Object.DestroyImmediate(interactable, true);
                    EditorUtility.SetDirty(gameObject);
                }
                modified = true;
            }
            foreach (Transform child in gameObject.transform)
            {
                var isModified = DestroyComponentRecursive(child.gameObject);
                modified = modified || isModified;
            }
            return modified;
        }

        /// <summary>
        /// Removes all "Missing Script" components from a GameObject and its children.
        /// This method must be called from an Editor context (e.g., a custom Editor window or menu item).
        /// </summary>
        /// <param name="targetGameObject">The GameObject to clean.</param>

        public static void RemoveMissingScripts(GameObject targetGameObject)
        {
            if (targetGameObject == null)
            {
                Debug.LogWarning("Target GameObject is null. Cannot remove missing scripts.");
                return;
            }

            foreach (var component in targetGameObject.GetComponentsInChildren<Transform>(true))
            {
                var missingCount = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(component.gameObject);

                if (missingCount > 0)
                {
                    Debug.Log($"Removed {missingCount} missing scripts from {component.name} in {targetGameObject.name}", component);
                }
            }

        }

    }

}

