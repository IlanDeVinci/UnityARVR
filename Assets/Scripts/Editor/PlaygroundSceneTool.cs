using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// Reset complet de MainScene.unity pour le mode "playground street art" :
///  - supprime tout le décor (cuisine, mobilier, etc.)
///  - garde XR Rig, EventSystem, GameManager, Volume, Lumière, Menu, ObjectSpawner
///  - pose un sol blanc 1000x1000
///  - dispose des murs de test
///  - crée la texture + matériau de spray
///  - attache SprayPainter à la main droite du XR Rig
///  - importe le .unitypackage Open Brush SDK si présent dans ThirdParty/
///
/// Menu  : Tools > Playground > Clean MainScene & Setup Playground
/// Batch : Unity.exe -executeMethod PlaygroundSceneTool.Execute
/// </summary>
public static class PlaygroundSceneTool
{
    private const string ScenePath          = "Assets/Scenes/MainScene.unity";
    private const string FloorMatPath       = "Assets/Material/FloorWhite.mat";
    private const string WallMatPath        = "Assets/Material/WallConcrete.mat";
    private const string PaintMatPath       = "Assets/Material/PaintBase.mat";
    private const string SprayTexturePath   = "Assets/Textures/SprayCircle.png";
    private const string OpenBrushPackage   = "ThirdParty/open-brush-toolkit-UnitySDK-v24.0.0.unitypackage";
    private const float  FloorScale         = 100f;

    [MenuItem("Tools/Playground/Clean MainScene & Setup Playground")]
    public static void RunFromMenu()
    {
        if (!EditorUtility.DisplayDialog(
                "Reset Playground",
                "Reset complet de MainScene : suppression du décor, sol blanc 1000x1000, murs de test, spray paint.",
                "OK", "Annuler"))
            return;
        Execute();
    }

    public static void Execute()
    {
        Debug.Log("[PlaygroundSceneTool] Start.");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        int destroyedCount = CleanScene(scene);
        CreateInfiniteFloor();
        SpawnTestWalls();
        CreatePaintAssets();
        AttachSprayPainterToRightHand();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[PlaygroundSceneTool] Done. {destroyedCount} root objets supprimés. Playground prêt.");
    }

    /// <summary>
    /// Import du .unitypackage Open Brush. Asynchrone : on s'abonne aux callbacks
    /// pour appeler EditorApplication.Exit nous-mêmes. À lancer SANS -quit.
    ///   Unity.exe -batchmode -nographics -projectPath X -executeMethod PlaygroundSceneTool.ImportOpenBrushAndExit
    /// </summary>
    public static void ImportOpenBrushAndExit()
    {
        string fullPath = Path.GetFullPath(OpenBrushPackage);
        if (!File.Exists(fullPath))
        {
            Debug.LogError($"[PlaygroundSceneTool] Package introuvable : {fullPath}");
            EditorApplication.Exit(1);
            return;
        }

        if (AssetDatabase.IsValidFolder("Assets/TiltBrush") || AssetDatabase.IsValidFolder("Assets/OpenBrush"))
        {
            Debug.Log("[PlaygroundSceneTool] Open Brush SDK déjà importé, skip.");
            EditorApplication.Exit(0);
            return;
        }

        AssetDatabase.importPackageCompleted  += name =>
        {
            Debug.Log($"[PlaygroundSceneTool] Import terminé : {name}");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorApplication.Exit(0);
        };
        AssetDatabase.importPackageFailed     += (name, err) =>
        {
            Debug.LogError($"[PlaygroundSceneTool] Échec import {name}: {err}");
            EditorApplication.Exit(1);
        };
        AssetDatabase.importPackageCancelled  += name =>
        {
            Debug.LogWarning($"[PlaygroundSceneTool] Import annulé : {name}");
            EditorApplication.Exit(2);
        };

        Debug.Log($"[PlaygroundSceneTool] Lancement import : {fullPath}");
        AssetDatabase.ImportPackage(fullPath, false);
    }

    // ────────────────────────────────────────────────────────────────────
    //  Cleanup
    // ────────────────────────────────────────────────────────────────────

    private static int CleanScene(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        int destroyed = 0;
        var kept = new List<string>();
        var removed = new List<string>();

        foreach (var root in roots)
        {
            if (root == null) continue;
            if (ShouldKeep(root))
            {
                kept.Add(root.name);
                continue;
            }
            removed.Add(root.name);
            Object.DestroyImmediate(root);
            destroyed++;
        }

        Debug.Log("[PlaygroundSceneTool] Gardés : " + string.Join(", ", kept));
        Debug.Log("[PlaygroundSceneTool] Supprimés : " + string.Join(", ", removed));
        return destroyed;
    }

    private static bool ShouldKeep(GameObject root)
    {
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

        string n = root.name;
        if (n == "XR Origin (XR Rig)" || n == "XR Interaction Manager"
            || n == "EventSystem" || n == "GameManager"
            || n == "Directional Light" || n == "Global Volume"
            || n == "Canvas" || n == "MainMenu"
            || n == "InfiniteFloor" || n == "TestWalls" || n == "PaintStamps")
            return true;

        return false;
    }

    private static bool HasRootComponentNamed(GameObject root, string typeName)
    {
        foreach (var comp in root.GetComponents<Component>())
            if (comp != null && comp.GetType().Name == typeName) return true;
        return false;
    }

    // ────────────────────────────────────────────────────────────────────
    //  Sol
    // ────────────────────────────────────────────────────────────────────

    private static void CreateInfiniteFloor()
    {
        var existing = GameObject.Find("InfiniteFloor");
        if (existing != null) Object.DestroyImmediate(existing);

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "InfiniteFloor";
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(FloorScale, 1f, FloorScale);
        floor.isStatic = true;

        Material mat = LoadOrCreateMaterial(FloorMatPath, Color.white);
        floor.GetComponent<MeshRenderer>().sharedMaterial = mat;

        var teleportArea = floor.AddComponent<TeleportationArea>();
        teleportArea.matchOrientation = MatchOrientation.WorldSpaceUp;
        teleportArea.teleportTrigger = BaseTeleportationInteractable.TeleportTrigger.OnSelectExited;
    }

    // ────────────────────────────────────────────────────────────────────
    //  Murs de test
    // ────────────────────────────────────────────────────────────────────

    private static readonly (Vector3 pos, Vector3 size, float rotY)[] TestWalls =
    {
        (new Vector3( 4f, 1.5f,  3f), new Vector3(3f, 3f, 0.3f),  10f),
        (new Vector3(-4f, 1.5f,  4f), new Vector3(3f, 3f, 0.3f), -25f),
        (new Vector3( 0f, 1.5f,  6f), new Vector3(4f, 3f, 0.3f),   0f),
        (new Vector3( 7f, 1.5f, -3f), new Vector3(3f, 3f, 0.3f),  60f),
        (new Vector3(-6f, 1.5f, -2f), new Vector3(3f, 3f, 0.3f), -55f),
        (new Vector3( 2f, 1.5f, -6f), new Vector3(4f, 3f, 0.3f), 110f),
    };

    private static void SpawnTestWalls()
    {
        var oldRoot = GameObject.Find("TestWalls");
        if (oldRoot != null) Object.DestroyImmediate(oldRoot);

        var root = new GameObject("TestWalls");
        Material wallMat = LoadOrCreateMaterial(WallMatPath, new Color(0.72f, 0.72f, 0.74f));

        for (int i = 0; i < TestWalls.Length; i++)
        {
            var (pos, size, rotY) = TestWalls[i];
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = $"Wall_{i + 1}";
            wall.transform.SetParent(root.transform, false);
            wall.transform.position = pos;
            wall.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            wall.transform.localScale = size;
            wall.isStatic = true;
            wall.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  Assets de peinture (texture + matériau)
    // ────────────────────────────────────────────────────────────────────

    private static void CreatePaintAssets()
    {
        EnsureFolder("Assets/Textures");
        EnsureFolder("Assets/Material");

        // Texture PNG (cercle doux radial)
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(SprayTexturePath) == null)
            GenerateSprayTexture(SprayTexturePath);

        ConfigureSprayTextureImport(SprayTexturePath);

        // Matériau de base
        if (AssetDatabase.LoadAssetAtPath<Material>(PaintMatPath) == null)
            CreatePaintBaseMaterial();
    }

    private static void GenerateSprayTexture(string path)
    {
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 c = new Vector2(size / 2f, size / 2f);
        float maxR = size / 2f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), c) / maxR; // 0 au centre, 1 au bord
            d = Mathf.Clamp01(d);
            // Profile spray : plein au centre, atténuation rapide vers le bord
            float a = Mathf.Pow(1f - d, 1.8f);
            // Bruit léger pour casser la régularité
            a *= 0.92f + 0.08f * Mathf.PerlinNoise(x * 0.15f, y * 0.15f);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        File.WriteAllBytes(path, png);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    private static void ConfigureSprayTextureImport(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
    }

    private static void CreatePaintBaseMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");

        var mat = new Material(shader) { name = "PaintBase" };

        // URP Unlit transparent
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // 1 = Transparent
        if (mat.HasProperty("_Blend"))   mat.SetFloat("_Blend", 0f);   // 0 = Alpha
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite"))   mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.renderQueue = 3000;

        // Albedo = la texture circulaire
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(SprayTexturePath);
        if (tex != null)
        {
            if (mat.HasProperty("_BaseMap"))   mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex"))   mat.SetTexture("_MainTex", tex);
        }
        mat.color = Color.white;

        AssetDatabase.CreateAsset(mat, PaintMatPath);
        AssetDatabase.SaveAssets();
    }

    // ────────────────────────────────────────────────────────────────────
    //  SprayPainter sur la main droite
    // ────────────────────────────────────────────────────────────────────

    private static void AttachSprayPainterToRightHand()
    {
        XROrigin origin = Object.FindAnyObjectByType<XROrigin>();
        if (origin == null)
        {
            Debug.LogWarning("[PlaygroundSceneTool] XR Origin introuvable, SprayPainter non attaché.");
            return;
        }

        Transform hand = FindByNameRecursive(origin.transform, "Right Controller")
                      ?? FindByNameRecursive(origin.transform, "RightHand Controller")
                      ?? FindByNameRecursive(origin.transform, "RightHand")
                      ?? FindByNameRecursive(origin.transform, "Right Hand");

        if (hand == null)
        {
            Debug.LogWarning("[PlaygroundSceneTool] Main droite introuvable dans le XR Rig.");
            return;
        }

        var painterType = System.Type.GetType("SprayPainter, Assembly-CSharp");
        if (painterType == null)
        {
            Debug.LogWarning("[PlaygroundSceneTool] Type SprayPainter introuvable (compilation requise).");
            return;
        }

        var painter = hand.gameObject.GetComponent(painterType);
        if (painter == null) painter = hand.gameObject.AddComponent(painterType);

        // Câbler les InputActionReference si on les trouve
        AssignSprayInputs(painter);

        // Câbler le matériau
        AssignPaintMaterial(painter);
    }

    private static void AssignSprayInputs(Component painter)
    {
        InputActionReference spray = FindActionReference("XRI Right Interaction", "Activate Value")
                                  ?? FindActionReference("XRI Right Interaction", "Activate")
                                  ?? FindActionReference("XRI RightHand Interaction", "Activate Value")
                                  ?? FindActionReference("XRI RightHand Interaction", "Activate");

        InputActionReference cycle = FindActionReference("XRI Right", "Primary Button")
                                  ?? FindActionReference("XRI RightHand", "Primary Button");

        SerializedObject so = new SerializedObject(painter);
        if (spray != null)
        {
            var p = so.FindProperty("sprayAction");
            if (p != null) p.objectReferenceValue = spray;
        }
        if (cycle != null)
        {
            var p = so.FindProperty("cycleColorAction");
            if (p != null) p.objectReferenceValue = cycle;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignPaintMaterial(Component painter)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(PaintMatPath);
        if (mat == null) return;
        SerializedObject so = new SerializedObject(painter);
        var p = so.FindProperty("paintBaseMaterial");
        if (p != null) p.objectReferenceValue = mat;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static InputActionReference FindActionReference(string mapName, string actionName)
    {
        // Cherche dans tous les InputActionAsset du projet
        string[] guids = AssetDatabase.FindAssets("t:InputActionAsset");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var subs = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var sub in subs)
            {
                if (sub is InputActionReference r
                    && r.action != null
                    && r.action.actionMap != null
                    && r.action.actionMap.name == mapName
                    && r.action.name == actionName)
                {
                    return r;
                }
            }
        }
        return null;
    }

    private static Transform FindByNameRecursive(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindByNameRecursive(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    // ────────────────────────────────────────────────────────────────────
    //  Helpers
    // ────────────────────────────────────────────────────────────────────

    private static Material LoadOrCreateMaterial(string assetPath, Color color)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (mat != null) return mat;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");

        mat = new Material(shader) { color = color };
        EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));
        AssetDatabase.CreateAsset(mat, assetPath);
        AssetDatabase.SaveAssets();
        return mat;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        string leaf = Path.GetFileName(folder);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
