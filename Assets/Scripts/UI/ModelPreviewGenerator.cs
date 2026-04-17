using UnityEngine;

/// <summary>
/// Génère des textures preview des prefabs 3D en les instanciant temporairement
/// dans une zone cachée et en les capturant avec une caméra dédiée.
/// </summary>
public static class ModelPreviewGenerator
{
    private const int PreviewLayer = 31;
    private static readonly Vector3 PreviewLocation = new Vector3(10000f, 10000f, 10000f);

    /// <summary>
    /// Génère une Texture2D preview d'un prefab.
    /// </summary>
    public static Texture2D Generate(GameObject prefab, int size = 128)
    {
        if (prefab == null) return null;

        // Instancier dans la zone cachée
        GameObject instance = Object.Instantiate(prefab, PreviewLocation, Quaternion.Euler(0f, -30f, 0f));
        StripExtras(instance);
        SetLayerRecursive(instance, PreviewLayer);

        // Calculer les bounds combinés des meshes enfants
        var renderers = instance.GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length == 0)
        {
            Object.DestroyImmediate(instance);
            return MakePlaceholderTexture(size);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        // Caméra preview
        GameObject camObj = new GameObject("_PreviewCamera");
        camObj.transform.position = PreviewLocation;
        Camera cam = camObj.AddComponent<Camera>();
        cam.cullingMask = 1 << PreviewLayer;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.08f, 0.15f, 1f);
        cam.orthographic = false;
        cam.fieldOfView = 40f;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 100f;

        // Lumière preview
        GameObject lightObj = new GameObject("_PreviewLight");
        lightObj.transform.position = PreviewLocation + new Vector3(1f, 2f, -1f);
        Light previewLight = lightObj.AddComponent<Light>();
        previewLight.type = LightType.Directional;
        previewLight.intensity = 1.3f;
        previewLight.color = Color.white;
        previewLight.cullingMask = 1 << PreviewLayer;
        lightObj.layer = PreviewLayer;

        // Cadrer l'objet
        float maxExtent = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        float camDist = maxExtent * 1.8f + 0.5f;
        cam.transform.position = bounds.center + new Vector3(camDist * 0.7f, camDist * 0.5f, -camDist);
        cam.transform.LookAt(bounds.center);

        // Render vers RenderTexture
        RenderTexture rt = RenderTexture.GetTemporary(size, size, 24);
        cam.targetTexture = rt;
        cam.Render();

        // Copier vers Texture2D
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D result = new Texture2D(size, size, TextureFormat.RGB24, false);
        result.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        result.Apply();
        RenderTexture.active = prev;

        // Nettoyage
        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        Object.DestroyImmediate(instance);
        Object.DestroyImmediate(camObj);
        Object.DestroyImmediate(lightObj);

        return result;
    }

    private static void StripExtras(GameObject obj)
    {
        // Caméras : détruire le GameObject entier (URP ajoute des composants requis)
        foreach (var cam in obj.GetComponentsInChildren<Camera>(true))
        {
            if (cam == null) continue;
            if (cam.gameObject == obj)
            {
                cam.enabled = false;
            }
            else
            {
                Object.DestroyImmediate(cam.gameObject);
            }
        }

        // Lumières : détruire le GameObject entier
        foreach (var l in obj.GetComponentsInChildren<Light>(true))
        {
            if (l == null) continue;
            if (l.gameObject == obj)
            {
                l.enabled = false;
            }
            else
            {
                Object.DestroyImmediate(l.gameObject);
            }
        }

        foreach (var al in obj.GetComponentsInChildren<AudioListener>(true))
        {
            if (al == null) continue;
            al.enabled = false;
            if (al.gameObject != obj)
                Object.DestroyImmediate(al.gameObject);
        }

        // Désactiver les MonoBehaviours pour pas qu'ils tournent
        foreach (var mb in obj.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb != null) mb.enabled = false;
        }
    }

    private static void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    private static Texture2D MakePlaceholderTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        Color c = new Color(0.2f, 0.2f, 0.25f);
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = c;
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
