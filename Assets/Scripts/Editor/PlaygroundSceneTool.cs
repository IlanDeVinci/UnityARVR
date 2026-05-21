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
    private const string ScenePath            = "Assets/Scenes/MainScene.unity";
    private const string FloorMatPath         = "Assets/Material/FloorWhite.mat";
    private const string WallMatPath          = "Assets/Material/WallConcrete.mat";
    private const string PaintMatPath         = "Assets/Material/PaintBase.mat";
    private const string FrameMatPath         = "Assets/Material/PictureFrameBlack.mat";
    private const string PictureBaseMatPath   = "Assets/Material/PictureBase.mat";
    private const string FrameMatBlackPath    = "Assets/Material/Frame_Black.mat";
    private const string FrameMatWhitePath    = "Assets/Material/Frame_White.mat";
    private const string FrameMatGoldPath     = "Assets/Material/Frame_Gold.mat";
    private const string FrameMatWoodPath     = "Assets/Material/Frame_Wood.mat";
    private const string FrameMatNonePath     = "Assets/Material/Frame_None.mat";
    private const string VideosResourceDir    = "Assets/Resources/Videos";
    private const string MusicResourceDir     = "Assets/Resources/Music";
    private const string SprayTexturePath     = "Assets/Textures/SprayCircle.png";
    private const string BrushesDir           = "Assets/Textures/Brushes";
    private const string PicturesResourceDir  = "Assets/Resources/Pictures";
    private const string OpenBrushPackage     = "ThirdParty/open-brush-toolkit-UnitySDK-v24.0.0.unitypackage";
    private const float  FloorScale           = 100f;

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
        CreateBrushAssets();
        CreatePictureAssets();
        CreateFrameMaterials();
        CreateMediaFolders();
        EnsureCityInScene();
        AttachSprayPainterToRightHand();
        AttachPicturePlacerToLeftHand();
        AttachMusicSpawner();
        SetupPaintMenu();

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
            || n == "InfiniteFloor" || n == "TestWalls" || n == "PaintStamps"
            || n == "Tableaux" || n == "MusicManager" || n == "Boomboxes"
            || n.StartsWith("city-iimmersive") || n.StartsWith("City"))
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

        // Câbler le tableau de pinceaux
        AssignBrushes(painter);
    }

    private static void AssignSprayInputs(Component painter)
    {
        InputActionReference spray = FindActionReference("XRI Right Interaction", "Activate Value")
                                  ?? FindActionReference("XRI Right Interaction", "Activate")
                                  ?? FindActionReference("XRI RightHand Interaction", "Activate Value")
                                  ?? FindActionReference("XRI RightHand Interaction", "Activate");

        InputActionReference cycle = FindActionReference("XRI Right", "Secondary Button")
                                  ?? FindActionReference("XRI RightHand", "Secondary Button")
                                  ?? FindActionReference("XRI Right", "Primary Button")
                                  ?? FindActionReference("XRI RightHand", "Primary Button");

        SerializedObject so = new SerializedObject(painter);
        if (spray != null)
        {
            var p = so.FindProperty("sprayAction");
            if (p != null) p.objectReferenceValue = spray;
        }
        if (cycle != null)
        {
            var p = so.FindProperty("cycleBrushAction");
            if (p != null) p.objectReferenceValue = cycle;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignBrushes(Component painter)
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { BrushesDir });
        if (guids == null || guids.Length == 0) return;

        // Tri par nom pour ordre stable
        System.Array.Sort(guids, (a, b) =>
            string.Compare(AssetDatabase.GUIDToAssetPath(a), AssetDatabase.GUIDToAssetPath(b)));

        SerializedObject so = new SerializedObject(painter);
        var arr = so.FindProperty("brushes");
        if (arr == null) return;

        arr.arraySize = guids.Length;
        for (int i = 0; i < guids.Length; i++)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guids[i]));
            arr.GetArrayElementAtIndex(i).objectReferenceValue = tex;
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

    // ────────────────────────────────────────────────────────────────────
    //  Assets tableaux (cadre noir + matériau image + placeholders)
    // ────────────────────────────────────────────────────────────────────

    private static void CreatePictureAssets()
    {
        EnsureFolder("Assets/Material");
        EnsureFolder("Assets/Resources");
        EnsureFolder(PicturesResourceDir);

        // Matériau cadre (noir mat Lit)
        if (AssetDatabase.LoadAssetAtPath<Material>(FrameMatPath) == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var frame = new Material(shader) { name = "PictureFrameBlack", color = new Color(0.05f, 0.05f, 0.05f) };
            if (frame.HasProperty("_BaseColor")) frame.SetColor("_BaseColor", new Color(0.05f, 0.05f, 0.05f));
            if (frame.HasProperty("_Smoothness")) frame.SetFloat("_Smoothness", 0.25f);
            if (frame.HasProperty("_Metallic"))   frame.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(frame, FrameMatPath);
        }

        // Matériau image (URP Unlit opaque)
        if (AssetDatabase.LoadAssetAtPath<Material>(PictureBaseMatPath) == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            var pic = new Material(shader) { name = "PictureBase", color = Color.white };
            AssetDatabase.CreateAsset(pic, PictureBaseMatPath);
        }

        // 4 placeholders procéduraux (uniquement si Resources/Pictures est vide)
        if (CountPicturesInFolder() == 0)
        {
            GeneratePicturePlaceholder("Pic_01_Sunset",   new Color(1f, 0.45f, 0.15f), new Color(0.85f, 0.10f, 0.55f), PicturePattern.LinearGradient);
            GeneratePicturePlaceholder("Pic_02_Ocean",    new Color(0.05f, 0.25f, 0.55f), new Color(0.15f, 0.85f, 0.95f), PicturePattern.RadialGradient);
            GeneratePicturePlaceholder("Pic_03_Forest",   new Color(0.05f, 0.30f, 0.10f), new Color(0.65f, 0.95f, 0.30f), PicturePattern.Stripes);
            GeneratePicturePlaceholder("Pic_04_Abstract", new Color(0.95f, 0.85f, 0.10f), new Color(0.45f, 0.10f, 0.85f), PicturePattern.Checker);
            AssetDatabase.Refresh();
        }
    }

    private enum PicturePattern { LinearGradient, RadialGradient, Stripes, Checker }

    private static int CountPicturesInFolder()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { PicturesResourceDir });
        return guids != null ? guids.Length : 0;
    }

    private static void GeneratePicturePlaceholder(string name, Color a, Color b, PicturePattern pattern)
    {
        const int size = 512;
        string path = $"{PicturesResourceDir}/{name}.png";
        if (File.Exists(path)) return;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = x / (float)(size - 1);
            float v = y / (float)(size - 1);
            float t;
            switch (pattern)
            {
                case PicturePattern.LinearGradient:
                    t = v;
                    break;
                case PicturePattern.RadialGradient:
                    t = Mathf.Clamp01(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 1.6f);
                    break;
                case PicturePattern.Stripes:
                    t = (Mathf.Sin(v * Mathf.PI * 8f) > 0f) ? 1f : 0f;
                    break;
                case PicturePattern.Checker:
                    int gx = Mathf.FloorToInt(u * 6f), gy = Mathf.FloorToInt(v * 6f);
                    t = ((gx + gy) % 2 == 0) ? 0f : 1f;
                    break;
                default: t = u; break;
            }
            // Petit bruit pour casser l'aspect "trop CG"
            t = Mathf.Clamp01(t + (Mathf.PerlinNoise(x * 0.03f, y * 0.03f) - 0.5f) * 0.06f);
            tex.SetPixel(x, y, Color.Lerp(a, b, t));
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  PicturePlacer sur la main gauche
    // ────────────────────────────────────────────────────────────────────

    private static void AttachPicturePlacerToLeftHand()
    {
        XROrigin origin = Object.FindAnyObjectByType<XROrigin>();
        if (origin == null)
        {
            Debug.LogWarning("[PlaygroundSceneTool] XR Origin introuvable, PicturePlacer non attaché.");
            return;
        }

        Transform hand = FindByNameRecursive(origin.transform, "Left Controller")
                      ?? FindByNameRecursive(origin.transform, "LeftHand Controller")
                      ?? FindByNameRecursive(origin.transform, "LeftHand")
                      ?? FindByNameRecursive(origin.transform, "Left Hand");

        if (hand == null)
        {
            Debug.LogWarning("[PlaygroundSceneTool] Main gauche introuvable dans le XR Rig.");
            return;
        }

        var placerType = System.Type.GetType("PicturePlacer, Assembly-CSharp");
        if (placerType == null)
        {
            Debug.LogWarning("[PlaygroundSceneTool] Type PicturePlacer introuvable (compilation requise).");
            return;
        }

        var placer = hand.gameObject.GetComponent(placerType);
        if (placer == null) placer = hand.gameObject.AddComponent(placerType);

        AssignPictureInputs(placer);
        AssignPictureMaterials(placer);
    }

    private static void AssignPictureInputs(Component placer)
    {
        // On utilise une action "press" — Activate (button) plutôt qu'Activate Value (float)
        InputActionReference place = FindActionReference("XRI Left Interaction", "Activate")
                                  ?? FindActionReference("XRI LeftHand Interaction", "Activate")
                                  ?? FindActionReference("XRI Left Interaction", "Activate Value")
                                  ?? FindActionReference("XRI LeftHand Interaction", "Activate Value");

        InputActionReference cycle = FindActionReference("XRI Left", "Primary Button")
                                  ?? FindActionReference("XRI LeftHand", "Primary Button");

        SerializedObject so = new SerializedObject(placer);
        if (place != null)
        {
            var p = so.FindProperty("placeAction");
            if (p != null) p.objectReferenceValue = place;
        }
        if (cycle != null)
        {
            var p = so.FindProperty("cyclePictureAction");
            if (p != null) p.objectReferenceValue = cycle;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignPictureMaterials(Component placer)
    {
        SerializedObject so = new SerializedObject(placer);
        var pic   = AssetDatabase.LoadAssetAtPath<Material>(PictureBaseMatPath);
        if (pic != null)
        {
            var p = so.FindProperty("pictureBaseMaterial");
            if (p != null) p.objectReferenceValue = pic;
        }

        // Array frameMaterials (5 cadres : Black, White, Gold, Wood, None)
        var arr = so.FindProperty("frameMaterials");
        if (arr != null)
        {
            string[] paths = { FrameMatBlackPath, FrameMatWhitePath, FrameMatGoldPath, FrameMatWoodPath, FrameMatNonePath };
            arr.arraySize = paths.Length;
            for (int i = 0; i < paths.Length; i++)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(paths[i]);
                arr.GetArrayElementAtIndex(i).objectReferenceValue = m;
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ────────────────────────────────────────────────────────────────────
    //  Cadres (5 matériaux)
    // ────────────────────────────────────────────────────────────────────

    private static void CreateFrameMaterials()
    {
        EnsureFolder("Assets/Material");

        CreateLitMaterial(FrameMatBlackPath, "Frame_Black", new Color(0.05f, 0.05f, 0.05f), 0.25f, 0f);
        CreateLitMaterial(FrameMatWhitePath, "Frame_White", new Color(0.95f, 0.95f, 0.95f), 0.40f, 0f);
        CreateLitMaterial(FrameMatGoldPath,  "Frame_Gold",  new Color(0.92f, 0.74f, 0.22f), 0.85f, 0.85f);
        CreateLitMaterial(FrameMatWoodPath,  "Frame_Wood",  new Color(0.45f, 0.30f, 0.18f), 0.20f, 0f);

        // "None" = un matériau totalement transparent (cadre invisible)
        if (AssetDatabase.LoadAssetAtPath<Material>(FrameMatNonePath) == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent");
            var mat = new Material(shader) { name = "Frame_None", color = new Color(0f, 0f, 0f, 0f) };
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend"))   mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite"))   mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            AssetDatabase.CreateAsset(mat, FrameMatNonePath);
        }
    }

    private static void CreateLitMaterial(string path, string name, Color color, float smoothness, float metallic)
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader) { name = name, color = color };
        if (mat.HasProperty("_BaseColor"))   mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Smoothness"))  mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Metallic"))    mat.SetFloat("_Metallic", metallic);
        AssetDatabase.CreateAsset(mat, path);
    }

    // ────────────────────────────────────────────────────────────────────
    //  Dossiers Resources/Videos & Resources/Music
    // ────────────────────────────────────────────────────────────────────

    private static void CreateMediaFolders()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder(VideosResourceDir);
        EnsureFolder(MusicResourceDir);
    }

    // ────────────────────────────────────────────────────────────────────
    //  MusicSpawner sur le GameObject hôte du menu
    // ────────────────────────────────────────────────────────────────────

    // ────────────────────────────────────────────────────────────────────
    //  City (FBX importé) : auto-instanciation si absent
    // ────────────────────────────────────────────────────────────────────

    private const string CityFbxPath = "Assets/Models/city-iimmersive-readytodev.fbx";

    private static void EnsureCityInScene()
    {
        // Vérifie s'il y a déjà une instance dans la scène
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var r in roots)
        {
            if (r != null && r.name.StartsWith("city-iimmersive"))
            {
                Debug.Log($"[PlaygroundSceneTool] City déjà présent : {r.name}");
                return;
            }
        }

        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CityFbxPath);
        if (fbx == null)
        {
            Debug.LogWarning($"[PlaygroundSceneTool] {CityFbxPath} introuvable, city non instancié.");
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        if (instance == null)
        {
            Debug.LogWarning("[PlaygroundSceneTool] InstantiatePrefab a échoué pour le city.");
            return;
        }
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.identity;
        Debug.Log($"[PlaygroundSceneTool] City instancié : {instance.name}");
    }

    private static void AttachMusicSpawner()
    {
        var spawnerType = System.Type.GetType("MusicSpawner, Assembly-CSharp");
        if (spawnerType == null)
        {
            Debug.LogWarning("[PlaygroundSceneTool] Type MusicSpawner introuvable (compilation requise).");
            return;
        }

        // Crée un GameObject racine "MusicManager" pour héberger le MusicSpawner
        GameObject host = GameObject.Find("MusicManager");
        if (host == null)
        {
            host = new GameObject("MusicManager");
            host.transform.position = Vector3.zero;
        }

        var existing = host.GetComponent(spawnerType);
        if (existing == null) host.AddComponent(spawnerType);
    }

    // ────────────────────────────────────────────────────────────────────
    //  Génération des pinceaux (5 textures procédurales)
    // ────────────────────────────────────────────────────────────────────

    private static void CreateBrushAssets()
    {
        EnsureFolder(BrushesDir);

        TryGenerateBrush(BrushesDir + "/Brush_01_Soft.png",       BrushKind.Soft);
        TryGenerateBrush(BrushesDir + "/Brush_02_Hard.png",       BrushKind.Hard);
        TryGenerateBrush(BrushesDir + "/Brush_03_Splatter.png",   BrushKind.Splatter);
        TryGenerateBrush(BrushesDir + "/Brush_04_Square.png",     BrushKind.Square);
        TryGenerateBrush(BrushesDir + "/Brush_05_Line.png",       BrushKind.Line);
    }

    private enum BrushKind { Soft, Hard, Splatter, Square, Line }

    private static void TryGenerateBrush(string path, BrushKind kind)
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null) return;

        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 c = new Vector2(size / 2f, size / 2f);
        float maxR = size / 2f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = x / (float)(size - 1);
            float v = y / (float)(size - 1);
            float a = 0f;

            switch (kind)
            {
                case BrushKind.Soft:
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / maxR;
                    a = Mathf.Pow(Mathf.Clamp01(1f - d), 1.8f);
                    a *= 0.92f + 0.08f * Mathf.PerlinNoise(x * 0.15f, y * 0.15f);
                    break;
                }
                case BrushKind.Hard:
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / maxR;
                    a = d < 0.45f ? 1f : Mathf.Clamp01((0.55f - d) / 0.10f);
                    a *= 0.95f + 0.05f * Mathf.PerlinNoise(x * 0.2f, y * 0.2f);
                    break;
                }
                case BrushKind.Splatter:
                {
                    float total = 0f;
                    // 6 dots aléatoires deterministes via seed
                    Random.State prev = Random.state;
                    Random.InitState(42);
                    for (int k = 0; k < 12; k++)
                    {
                        float cx = Random.Range(0.15f, 0.85f);
                        float cy = Random.Range(0.15f, 0.85f);
                        float r  = Random.Range(0.06f, 0.16f);
                        float dd = Vector2.Distance(new Vector2(u, v), new Vector2(cx, cy)) / r;
                        total += Mathf.Pow(Mathf.Clamp01(1f - dd), 2.2f);
                    }
                    Random.state = prev;
                    a = Mathf.Clamp01(total);
                    break;
                }
                case BrushKind.Square:
                {
                    // Carré arrondi soft
                    float dx = Mathf.Abs(u - 0.5f) * 2f;
                    float dy = Mathf.Abs(v - 0.5f) * 2f;
                    float d  = Mathf.Pow(Mathf.Pow(dx, 4f) + Mathf.Pow(dy, 4f), 0.25f);
                    a = Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f);
                    break;
                }
                case BrushKind.Line:
                {
                    // Bande horizontale soft
                    float dy = Mathf.Abs(v - 0.5f) * 2f;
                    float dx = Mathf.Abs(u - 0.5f) * 2f;
                    float falloffX = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Pow(dx, 8f)), 0.5f);
                    float falloffY = Mathf.Pow(Mathf.Clamp01(1f - dy), 2.5f);
                    a = falloffX * falloffY;
                    break;
                }
            }

            tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
        }

        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  Menu peinture : remplace SpawnMenuUI par PaintMenuUI
    // ────────────────────────────────────────────────────────────────────

    private static void SetupPaintMenu()
    {
        var paintMenuType = System.Type.GetType("PaintMenuUI, Assembly-CSharp");
        if (paintMenuType == null)
        {
            Debug.LogWarning("[PlaygroundSceneTool] Type PaintMenuUI introuvable (compilation requise).");
            return;
        }

        var spawnMenuType = System.Type.GetType("SpawnMenuUI, Assembly-CSharp");

        // 1) Localiser le GameObject qui héberge SpawnMenuUI dans la scène
        GameObject host = null;
        InputActionReference savedToggle = null;
        GameObject savedPanel = null;

        if (spawnMenuType != null)
        {
            var existing = Object.FindObjectsByType(spawnMenuType, FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (existing != null && existing.Length > 0)
            {
                var comp = existing[0] as Component;
                host = comp.gameObject;

                var so = new SerializedObject(comp);
                var toggle = so.FindProperty("menuToggleAction");
                if (toggle != null && toggle.objectReferenceValue is InputActionReference r)
                    savedToggle = r;
                var panel = so.FindProperty("menuPanel");
                if (panel != null && panel.objectReferenceValue is GameObject g)
                    savedPanel = g;

                // Détruire toutes les instances de SpawnMenuUI (peut y en avoir plusieurs)
                for (int i = existing.Length - 1; i >= 0; i--)
                {
                    if (existing[i] != null) Object.DestroyImmediate(existing[i], true);
                }
            }
        }

        // Nettoyer les PaintMenuUI mal placés (ex : ancien essai sur MainMenu).
        // Ne supprimer QUE si host est connu — sinon on garde tout, on choisira ensuite.
        if (host != null)
        {
            var stale = Object.FindObjectsByType(paintMenuType, FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (stale != null)
            {
                foreach (var s in stale)
                {
                    if (s == null) continue;
                    if (((Component)s).gameObject != host)
                        Object.DestroyImmediate(s, true);
                }
            }
        }

        if (host == null)
        {
            // Pas de SpawnMenuUI (déjà nettoyé). Cherche un PaintMenuUI existant à re-câbler.
            var existingPaint = Object.FindObjectsByType(paintMenuType, FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (existingPaint != null && existingPaint.Length > 0)
            {
                host = ((Component)existingPaint[0]).gameObject;
                var pmSoExisting = new SerializedObject(existingPaint[0]);
                var pp = pmSoExisting.FindProperty("menuPanel");
                if (pp != null && pp.objectReferenceValue is GameObject g) savedPanel = g;
                var tt = pmSoExisting.FindProperty("menuToggleAction");
                if (tt != null && tt.objectReferenceValue is InputActionReference rr) savedToggle = rr;
                Debug.Log($"[PlaygroundSceneTool] PaintMenuUI existant trouvé sur '{host.name}', re-câblage.");
            }
        }

        if (host == null)
        {
            // Dernier recours : attache au GameManager
            host = GameObject.Find("GameManager");
            if (host == null)
            {
                Debug.LogWarning("[PlaygroundSceneTool] Aucun host trouvé pour PaintMenuUI.");
                return;
            }
            Debug.LogWarning("[PlaygroundSceneTool] Aucun SpawnMenuUI/PaintMenuUI trouvé. Attache au GameManager.");
        }

        // Fallback toggle si l'ancien n'en avait pas
        if (savedToggle == null)
            savedToggle = FindActionReference("Player", "OpenMenu")
                       ?? FindActionReference("XRI Left", "Menu")
                       ?? FindActionReference("XRI LeftHand", "Menu")
                       ?? FindActionReference("XRI Left", "Primary Button")
                       ?? FindActionReference("XRI LeftHand", "Primary Button");

        // Fallback panel : cherche un GameObject "Panel" enfant d'un Canvas dans la scène
        if (savedPanel == null) savedPanel = FindMenuPanelInScene();

        // 2) Ajouter PaintMenuUI sur le même GameObject
        var paintMenu = host.GetComponent(paintMenuType);
        if (paintMenu == null) paintMenu = host.AddComponent(paintMenuType);

        SerializedObject pmSo = new SerializedObject(paintMenu);
        if (savedPanel != null)
        {
            var p = pmSo.FindProperty("menuPanel");
            if (p != null) p.objectReferenceValue = savedPanel;
        }
        if (savedToggle != null)
        {
            var p = pmSo.FindProperty("menuToggleAction");
            if (p != null) p.objectReferenceValue = savedToggle;
        }

        // Lier les 4 références d'outils
        WireFirstOfType(pmSo, "sprayPainter",  "SprayPainter, Assembly-CSharp");
        WireFirstOfType(pmSo, "objectSpawner", "ObjectSpawner, Assembly-CSharp");
        WireFirstOfType(pmSo, "picturePlacer", "PicturePlacer, Assembly-CSharp");
        WireFirstOfType(pmSo, "musicSpawner",  "MusicSpawner, Assembly-CSharp");

        pmSo.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log($"[PlaygroundSceneTool] PaintMenuUI installé sur '{host.name}'. " +
                  $"menuPanel={(savedPanel != null ? savedPanel.name : "<null>")}, " +
                  $"toggle={(savedToggle != null ? savedToggle.name : "<null>")}.");
    }

    private static GameObject FindMenuPanelInScene()
    {
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var r in roots)
        {
            var canvas = r.GetComponentInChildren<Canvas>(true);
            if (canvas == null) continue;
            // Cherche un enfant nommé "Panel"
            var t = FindByNameRecursive(canvas.transform, "Panel");
            if (t != null) return t.gameObject;
        }
        return null;
    }

    private static void WireFirstOfType(SerializedObject so, string fieldName, string typeQualifiedName)
    {
        var t = System.Type.GetType(typeQualifiedName);
        if (t == null) return;
        var found = Object.FindObjectsByType(t, FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (found == null || found.Length == 0) return;
        var p = so.FindProperty(fieldName);
        if (p != null) p.objectReferenceValue = found[0] as Object;
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
