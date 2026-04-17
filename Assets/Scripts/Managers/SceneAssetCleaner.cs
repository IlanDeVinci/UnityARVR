using UnityEngine;

/// <summary>
/// Nettoie la scène au démarrage : supprime toutes les caméras et lumières
/// héritées des modèles GLB/FBX importés (sauf la Main Camera et la Directional Light).
/// </summary>
[DefaultExecutionOrder(-500)]
public class SceneAssetCleaner : MonoBehaviour
{
    private void Awake()
    {
        // Caméras : garder uniquement la caméra principale (celle taguée MainCamera)
        var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int camerasKept = 0;
        int camerasRemoved = 0;
        foreach (var cam in cameras)
        {
            if (cam.CompareTag("MainCamera") || cam.name.Contains("Main") || cam.name.Contains("XR"))
            {
                camerasKept++;
                continue;
            }
            cam.enabled = false;
            Destroy(cam.gameObject);
            camerasRemoved++;
        }

        // Lumières : garder uniquement la Directional Light principale
        var lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int lightsKept = 0;
        int lightsRemoved = 0;
        foreach (var l in lights)
        {
            if (l.type == LightType.Directional)
            {
                lightsKept++;
                continue;
            }
            if (l.gameObject.name.ToLower().Contains("room") || l.gameObject.name.ToLower().Contains("lamp"))
            {
                lightsKept++;
                continue;
            }
            l.enabled = false;
            Destroy(l.gameObject);
            lightsRemoved++;
        }

        // AudioListener : un seul doit rester dans la scène
        var listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        bool kept = false;
        int listenersRemoved = 0;
        foreach (var al in listeners)
        {
            // Garder celui sur la caméra principale
            if (!kept && (al.GetComponent<Camera>() != null || al.CompareTag("MainCamera")))
            {
                kept = true;
                continue;
            }
            if (!kept)
            {
                kept = true;
                continue;
            }
            Destroy(al);
            listenersRemoved++;
        }

        Debug.Log($"[SceneAssetCleaner] Caméras: {camerasKept} gardées, {camerasRemoved} supprimées. " +
                  $"Lumières: {lightsKept} gardées, {lightsRemoved} supprimées. " +
                  $"AudioListeners supprimés: {listenersRemoved}");
    }
}
