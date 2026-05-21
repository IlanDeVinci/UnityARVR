#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persiste les objets créés en Play mode (paint stamps, tableaux,
/// meubles, boomboxes) après la sortie du Play mode.
///
/// Stratégie : capture en JSON (transforms + matériaux + textures via
/// AssetDatabase path), puis reconstruction explicite en Edit mode pour
/// éviter la corruption des shaders / keywords / _Cull que provoque
/// PrefabUtility.SaveAsPrefabAsset sur des matériaux runtime.
///
/// Le fichier de persistance est stocké dans Library/PlayModePersisted.json
/// (hors Assets → non importé par Unity).
/// </summary>
[InitializeOnLoad]
public static class PlayModePersistence
{
    private const string SavePath = "Library/PlayModePersisted.json";
    private const string StampsRoot = "PaintStamps";
    private const string TableauxRoot = "Tableaux";
    private const string BoomboxesRoot = "Boomboxes";
    private const string FurnitureTag = "SpawnedObject";

    static PlayModePersistence()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode) Capture();
        else if (state == PlayModeStateChange.EnteredEditMode) Restore();
    }

    // ────────────────────────────────────────────────────────────────────
    //  Data
    // ────────────────────────────────────────────────────────────────────

    [Serializable] private class V3 { public float x, y, z; }
    [Serializable] private class Q { public float x, y, z, w; }
    [Serializable] private class C { public float r, g, b, a; }

    [Serializable] private class MeshChildData
    {
        public string name;
        public V3 localPos, localScale;
        public Q localRot;
        public C baseColor;
        public string texturePath;
        public float cull = 2f;
        public C emissionColor;
    }

    [Serializable] private class StampData
    {
        public V3 pos, scale;
        public Q rot;
        public C color;
        public string brushTexturePath;
        public string tag;
    }

    [Serializable] private class TableauData
    {
        public string name;
        public V3 pos, scale;
        public Q rot;
        public List<MeshChildData> children = new List<MeshChildData>();
    }

    [Serializable] private class FurnitureData
    {
        public string name;
        public V3 pos, scale;
        public Q rot;
        public string prefabPath;
    }

    [Serializable] private class MusicData
    {
        public V3 pos, scale;
        public Q rot;
        public string trackName;
        public List<MeshChildData> children = new List<MeshChildData>();
    }

    [Serializable] private class SceneState
    {
        public List<StampData> stamps = new List<StampData>();
        public List<TableauData> tableaux = new List<TableauData>();
        public List<FurnitureData> furniture = new List<FurnitureData>();
        public List<MusicData> music = new List<MusicData>();
    }

    // ────────────────────────────────────────────────────────────────────
    //  Capture (encore en Play mode)
    // ────────────────────────────────────────────────────────────────────

    private static void Capture()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.isLoaded) return;

        var state = new SceneState();
        var roots = scene.GetRootGameObjects();

        // 1. Stamps
        var stampsRoot = Array.Find(roots, r => r.name == StampsRoot);
        if (stampsRoot != null)
        {
            foreach (Transform t in stampsRoot.transform)
            {
                var mr = t.GetComponent<MeshRenderer>();
                if (mr == null || mr.sharedMaterial == null) continue;
                var mat = mr.sharedMaterial;

                var data = new StampData
                {
                    pos = ToV3(t.position),
                    rot = ToQ(t.rotation),
                    scale = ToV3(t.localScale),
                    tag = SafeTag(t.gameObject)
                };

                Color col = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor")
                          : mat.HasProperty("_Color") ? mat.GetColor("_Color")
                          : mat.color;
                data.color = ToC(col);

                Texture tex = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;
                if (tex == null && mat.HasProperty("_MainTex")) tex = mat.GetTexture("_MainTex");
                data.brushTexturePath = tex != null ? AssetDatabase.GetAssetPath(tex) : null;

                state.stamps.Add(data);
            }
        }

        // 2. Tableaux — sous "Tableaux" ET au scène root (cas drag-drop qui détache).
        var tableauxTransforms = new List<Transform>();
        var tableauxRoot = Array.Find(roots, r => r.name == TableauxRoot);
        if (tableauxRoot != null)
            foreach (Transform t in tableauxRoot.transform) tableauxTransforms.Add(t);
        foreach (var r in roots)
            if (r != null && r.name.StartsWith("Tableau_")) tableauxTransforms.Add(r.transform);

        foreach (var t in tableauxTransforms)
        {
            var data = new TableauData
            {
                name = t.name,
                pos = ToV3(t.position),
                rot = ToQ(t.rotation),
                scale = ToV3(t.localScale)
            };
            CaptureChildrenMeshes(t, data.children);
            state.tableaux.Add(data);
        }

        // 3. Furniture (tag SpawnedObject)
        var taggedFurniture = SafeFindWithTag(FurnitureTag);
        if (taggedFurniture != null)
        {
            foreach (var f in taggedFurniture)
            {
                if (f == null) continue;
                var sourcePrefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(f);
                if (sourcePrefab == null) continue;
                string prefabPath = AssetDatabase.GetAssetPath(sourcePrefab);
                if (string.IsNullOrEmpty(prefabPath)) continue;

                state.furniture.Add(new FurnitureData
                {
                    name = f.name,
                    pos = ToV3(f.transform.position),
                    rot = ToQ(f.transform.rotation),
                    scale = ToV3(f.transform.localScale),
                    prefabPath = prefabPath
                });
            }
        }

        // 4. Boomboxes
        var boomboxesRoot = Array.Find(roots, r => r.name == BoomboxesRoot);
        if (boomboxesRoot != null)
        {
            foreach (Transform t in boomboxesRoot.transform)
            {
                var data = new MusicData
                {
                    pos = ToV3(t.position),
                    rot = ToQ(t.rotation),
                    scale = ToV3(t.localScale),
                    trackName = t.name.StartsWith("Boombox_") ? t.name.Substring("Boombox_".Length) : ""
                };
                CaptureChildrenMeshes(t, data.children);
                state.music.Add(data);
            }
        }

        // Sauvegarde JSON
        string json = JsonUtility.ToJson(state, true);
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
        File.WriteAllText(SavePath, json);
        Debug.Log($"[PlayModePersistence] Capture : {state.stamps.Count} stamps, {state.tableaux.Count} tableaux, {state.furniture.Count} meubles, {state.music.Count} boomboxes → {SavePath}");
    }

    private static void CaptureChildrenMeshes(Transform parent, List<MeshChildData> output)
    {
        foreach (Transform child in parent)
        {
            var mr = child.GetComponent<MeshRenderer>();
            if (mr == null || mr.sharedMaterial == null) continue;
            var mat = mr.sharedMaterial;

            var c = new MeshChildData
            {
                name = child.name,
                localPos = ToV3(child.localPosition),
                localRot = ToQ(child.localRotation),
                localScale = ToV3(child.localScale)
            };

            Color color = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor")
                        : mat.HasProperty("_Color") ? mat.GetColor("_Color")
                        : mat.color;
            c.baseColor = ToC(color);

            Texture tex = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;
            if (tex == null && mat.HasProperty("_MainTex")) tex = mat.GetTexture("_MainTex");
            c.texturePath = tex != null ? AssetDatabase.GetAssetPath(tex) : null;

            if (mat.HasProperty("_Cull")) c.cull = mat.GetFloat("_Cull");
            if (mat.HasProperty("_EmissionColor"))
            {
                Color e = mat.GetColor("_EmissionColor");
                if (e.maxColorComponent > 0.001f) c.emissionColor = ToC(e);
            }

            output.Add(c);
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  Restore (Edit mode)
    // ────────────────────────────────────────────────────────────────────

    private static void Restore()
    {
        if (!File.Exists(SavePath)) return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.isLoaded) return;

        SceneState state;
        try
        {
            string json = File.ReadAllText(SavePath);
            state = JsonUtility.FromJson<SceneState>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayModePersistence] Lecture JSON échouée : {e.Message}");
            return;
        }
        if (state == null) return;

        // 1. Stamps
        if (state.stamps.Count > 0)
        {
            var stampsRoot = EnsureRootInScene(scene, StampsRoot);
            foreach (var s in state.stamps)
                BuildStamp(s, stampsRoot.transform);
        }

        // 2. Tableaux
        if (state.tableaux.Count > 0)
        {
            var tableauxRoot = EnsureRootInScene(scene, TableauxRoot);
            foreach (var t in state.tableaux)
                BuildTableau(t, tableauxRoot.transform);
        }

        // 3. Furniture
        foreach (var f in state.furniture)
            BuildFurniture(f, scene);

        // 4. Boomboxes
        if (state.music.Count > 0)
        {
            var boomboxesRoot = EnsureRootInScene(scene, BoomboxesRoot);
            foreach (var b in state.music)
                BuildBoombox(b, boomboxesRoot.transform);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[PlayModePersistence] Restauration : {state.stamps.Count} stamps, {state.tableaux.Count} tableaux, {state.furniture.Count} meubles, {state.music.Count} boomboxes. Sauvegarde la scène (Ctrl+S) pour rendre permanent.");
    }

    private static GameObject EnsureRootInScene(Scene scene, string name)
    {
        foreach (var r in scene.GetRootGameObjects())
            if (r.name == name) return r;
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }

    private static void BuildStamp(StampData s, Transform parent)
    {
        var stamp = GameObject.CreatePrimitive(PrimitiveType.Quad);
        stamp.name = "PaintStamp";
        var col = stamp.GetComponent<Collider>();
        if (col != null) UnityEngine.Object.DestroyImmediate(col);

        if (!string.IsNullOrEmpty(s.tag)) try { stamp.tag = s.tag; } catch { /* tag non défini */ }

        stamp.transform.SetParent(parent, false);
        stamp.transform.position = FromV3(s.pos);
        stamp.transform.rotation = FromQ(s.rot);
        stamp.transform.localScale = FromV3(s.scale);

        var mr = stamp.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        Color color = FromC(s.color);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

        if (!string.IsNullOrEmpty(s.brushTexturePath))
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(s.brushTexturePath);
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }
        }

        // Backface culling off pour gérer le sens de normal du Quad Unity.
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);

        // Transparence (alpha)
        SetMaterialTransparent(mat);

        mr.sharedMaterial = mat;
    }

    private static void BuildTableau(TableauData t, Transform parent)
    {
        var root = new GameObject(t.name);
        root.transform.SetParent(parent, false);
        root.transform.position = FromV3(t.pos);
        root.transform.rotation = FromQ(t.rot);
        root.transform.localScale = FromV3(t.scale);

        foreach (var c in t.children)
            BuildMeshChild(c, root.transform);
    }

    private static void BuildBoombox(MusicData b, Transform parent)
    {
        var root = new GameObject($"Boombox_{b.trackName}");
        root.transform.SetParent(parent, false);
        root.transform.position = FromV3(b.pos);
        root.transform.rotation = FromQ(b.rot);
        root.transform.localScale = FromV3(b.scale);

        foreach (var c in b.children)
            BuildMeshChild(c, root.transform);
    }

    private static void BuildMeshChild(MeshChildData c, Transform parent)
    {
        // On utilise Quad par défaut — paint stamps et picture sont des quads.
        // Pour les cubes (frame, body de boombox), on devine via le nom.
        PrimitiveType type = PrimitiveType.Quad;
        string n = c.name != null ? c.name.ToLowerInvariant() : "";
        if (n.Contains("frame") || n.Contains("body") || n.Contains("speaker") || n.Contains("antenna") || n.Contains("cube"))
            type = PrimitiveType.Cube;

        var go = GameObject.CreatePrimitive(type);
        go.name = c.name;
        var col = go.GetComponent<Collider>();
        if (col != null) UnityEngine.Object.DestroyImmediate(col);

        go.transform.SetParent(parent, false);
        go.transform.localPosition = FromV3(c.localPos);
        go.transform.localRotation = FromQ(c.localRot);
        go.transform.localScale = FromV3(c.localScale);

        var mr = go.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        Color color = FromC(c.baseColor);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

        if (!string.IsNullOrEmpty(c.texturePath))
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(c.texturePath);
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }
        }

        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", c.cull);

        if (c.emissionColor != null)
        {
            Color e = FromC(c.emissionColor);
            if (e.maxColorComponent > 0.001f && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", e);
            }
        }

        mr.sharedMaterial = mat;
    }

    private static void BuildFurniture(FurnitureData f, Scene scene)
    {
        if (string.IsNullOrEmpty(f.prefabPath)) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(f.prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[PlayModePersistence] Prefab introuvable : {f.prefabPath}");
            return;
        }

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        if (inst == null) return;

        inst.transform.position = FromV3(f.pos);
        inst.transform.rotation = FromQ(f.rot);
        inst.transform.localScale = FromV3(f.scale);
        try { inst.tag = FurnitureTag; } catch { /* tag non défini */ }
    }

    private static void SetMaterialTransparent(Material mat)
    {
        // Active le mode Transparent sur URP/Unlit pour préserver l'alpha des paint stamps.
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // 1 = Transparent
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f); // Alpha
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    // ────────────────────────────────────────────────────────────────────
    //  Conversions
    // ────────────────────────────────────────────────────────────────────

    private static V3 ToV3(Vector3 v) => new V3 { x = v.x, y = v.y, z = v.z };
    private static Q ToQ(Quaternion q) => new Q { x = q.x, y = q.y, z = q.z, w = q.w };
    private static C ToC(Color c) => new C { r = c.r, g = c.g, b = c.b, a = c.a };

    private static Vector3 FromV3(V3 v) => v == null ? Vector3.zero : new Vector3(v.x, v.y, v.z);
    private static Quaternion FromQ(Q q) => q == null ? Quaternion.identity : new Quaternion(q.x, q.y, q.z, q.w);
    private static Color FromC(C c) => c == null ? Color.white : new Color(c.r, c.g, c.b, c.a);

    private static GameObject[] SafeFindWithTag(string tag)
    {
        try { return GameObject.FindGameObjectsWithTag(tag); }
        catch { return null; }
    }

    private static string SafeTag(GameObject go)
    {
        try { return go.tag; } catch { return null; }
    }

    [MenuItem("Tools/Play Mode Persistence/Vider le cache")]
    private static void ClearCacheMenu()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
            Debug.Log("[PlayModePersistence] Cache JSON supprimé.");
        }
        // Ancien cache prefab
        const string oldFolder = "Assets/_PersistedPlayMode";
        if (Directory.Exists(oldFolder))
        {
            AssetDatabase.DeleteAsset(oldFolder);
            AssetDatabase.Refresh();
        }
    }

    [MenuItem("Tools/Play Mode Persistence/Restaurer maintenant")]
    private static void RestoreMenu() => Restore();
}
#endif
