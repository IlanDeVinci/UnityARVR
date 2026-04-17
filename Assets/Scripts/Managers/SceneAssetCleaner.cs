using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Nettoie uniquement les caméras et AudioListeners parasites au démarrage.
/// Les lumières de la scène sont PRÉSERVÉES (les LightSwitch en ont besoin).
/// Les lumières embed dans les objets spawnés sont gérées par ObjectSpawner/PikachuSpawner
/// directement au moment du spawn.
/// </summary>
[DefaultExecutionOrder(-500)]
public class SceneAssetCleaner : MonoBehaviour
{
    private void Awake()
    {
        // Caméras : garder uniquement celles protégées (XR, Main)
        var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int camerasKept = 0;
        int camerasRemoved = 0;
        foreach (var cam in cameras)
        {
            if (IsProtectedCamera(cam))
            {
                camerasKept++;
                continue;
            }
            cam.enabled = false;
            Destroy(cam.gameObject);
            camerasRemoved++;
        }

        // AudioListener : un seul doit rester dans la scène
        var listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        bool kept = false;
        int listenersRemoved = 0;
        foreach (var al in listeners)
        {
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

        // Les lumières de scène sont PRÉSERVÉES (contrôlées par LightSwitchSetup)
        Debug.Log($"[SceneAssetCleaner] Caméras: {camerasKept} gardées, {camerasRemoved} supprimées. " +
                  $"AudioListeners supprimés: {listenersRemoved}. Lumières: préservées.");
    }

    private static bool IsProtectedCamera(Camera cam)
    {
        if (cam == null) return true;
        if (cam.CompareTag("MainCamera")) return true;

        string n = cam.name.ToLower();
        if (n.Contains("main") || n.Contains("xr") || n.Contains("preview"))
            return true;

        // Caméra sous une hiérarchie XR → la garder
        if (cam.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>() != null)
            return true;

        return false;
    }
}
