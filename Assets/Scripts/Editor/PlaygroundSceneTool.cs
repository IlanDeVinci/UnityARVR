using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// Nettoie MainScene.unity : supprime tout le décor (cuisine, mobilier, etc.)
/// en gardant seulement l'essentiel (XR Rig, menu, GameManager, EventSystem, lumière)
/// et pose un grand sol blanc 1000x1000.
/// Menu: Tools > Playground > Clean MainScene & Setup Playground
/// </summary>
public static class PlaygroundSceneTool
{
    private const string ScenePath = "Assets/Scenes/MainScene.unity";
    private const string FloorMatPath = "Assets/Material/FloorWhite.mat";
    private const float FloorScale = 100f; // Plane = 10m × FloorScale = 1000m

    [MenuItem("Tools/Playground/Clean MainScene & Setup Playground")]
    public static void RunFromMenu()
    {
        if (!EditorUtility.DisplayDialog(
                "Nettoyer MainScene",
                "Supprimer tous les objets de décor de MainScene et poser un sol blanc 1000x1000 ?\n\n" +
                "Gardés : XR Rig, XR Interaction Manager, EventSystem, GameManager, Volume, Lumière, Menu, ObjectSpawner.",
                "Nettoyer", "Annuler"))
            return;

        Execute();
    }

    /// <summary>
    /// Point d'entrée pour batchmode : Unity.exe -executeMethod PlaygroundSceneTool.Execute
    /// </summary>
    public static void Execute()
    {
        Debug.Log("[PlaygroundSceneTool] Start.");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        int destroyedCount = CleanScene(scene);
        CreateInfiniteFloor();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[PlaygroundSceneTool] Done. {destroyedCount} root objets supprimés. Sol blanc 1000x1000 ajouté.");
    }

    private static int CleanScene(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        int destroyed = 0;
        List<string> keptNames = new List<string>();
        List<string> removedNames = new List<string>();

        foreach (var root in roots)
        {
            if (root == null) continue;
            if (ShouldKeep(root))
            {
                keptNames.Add(root.name);
                continue;
            }
            removedNames.Add(root.name);
            Object.DestroyImmediate(root);
            destroyed++;
        }

        Debug.Log("[PlaygroundSceneTool] Gardés : " + string.Join(", ", keptNames));
        Debug.Log("[PlaygroundSceneTool] Supprimés : " + string.Join(", ", removedNames));
        return destroyed;
    }

    private static bool ShouldKeep(GameObject root)
    {
        // Garder uniquement par composant POSÉ AU NIVEAU DU ROOT (les lumières / caméras
        // dans des prefabs de décor ne doivent pas sauver leur parent).
        if (root.GetComponent<XROrigin>() != null) return true;
        if (root.GetComponent<XRInteractionManager>() != null) return true;
        if (root.GetComponent<EventSystem>() != null) return true;
        if (root.GetComponent<Volume>() != null) return true;
        if (root.GetComponent<Light>() != null) return true;
        if (root.GetComponent<Canvas>() != null) return true;

        if (HasRootComponentNamed(root, "GameManager")) return true;
        if (HasRootComponentNamed(root, "ObjectSpawner")) return true;
        if (HasRootComponentNamed(root, "SpawnMenuUI")) return true;
        if (HasRootComponentNamed(root, "MainMenuUI")) return true;
        if (HasRootComponentNamed(root, "VRWristMenu")) return true;
        if (HasRootComponentNamed(root, "SceneAssetCleaner")) return true;

        // Garder par nom explicite (au cas où la hiérarchie soit différente)
        string n = root.name;
        if (n == "XR Origin (XR Rig)" || n == "XR Interaction Manager"
            || n == "EventSystem" || n == "GameManager"
            || n == "Directional Light" || n == "Global Volume"
            || n == "Canvas" || n == "MainMenu")
            return true;

        return false;
    }

    private static bool HasRootComponentNamed(GameObject root, string typeName)
    {
        foreach (var comp in root.GetComponents<Component>())
        {
            if (comp == null) continue;
            if (comp.GetType().Name == typeName) return true;
        }
        return false;
    }

    private static void CreateInfiniteFloor()
    {
        // Supprimer l'ancien sol si présent
        var existing = GameObject.Find("InfiniteFloor");
        if (existing != null) Object.DestroyImmediate(existing);

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "InfiniteFloor";
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(FloorScale, 1f, FloorScale);
        floor.isStatic = true;

        // Matériau blanc
        Material mat = LoadOrCreateWhiteMaterial();
        var mr = floor.GetComponent<MeshRenderer>();
        if (mr != null) mr.sharedMaterial = mat;

        // Téléportation VR
        var teleportArea = floor.AddComponent<TeleportationArea>();
        teleportArea.matchOrientation = MatchOrientation.WorldSpaceUp;
        teleportArea.teleportTrigger = BaseTeleportationInteractable.TeleportTrigger.OnSelectExited;
    }

    private static Material LoadOrCreateWhiteMaterial()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(FloorMatPath);
        if (mat != null) return mat;

        // Choisir le bon shader (URP si dispo, sinon Standard)
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");

        mat = new Material(shader) { color = Color.white };

        // S'assurer que le dossier existe
        if (!AssetDatabase.IsValidFolder("Assets/Material"))
            AssetDatabase.CreateFolder("Assets", "Material");

        AssetDatabase.CreateAsset(mat, FloorMatPath);
        AssetDatabase.SaveAssets();
        return mat;
    }
}
