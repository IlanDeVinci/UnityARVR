using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Attachment;

/// <summary>
/// Spawner d'objets. Charge automatiquement tous les modèles depuis
/// Resources/Furniture/ (triés alphabétiquement).
/// </summary>
public class ObjectSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private float spawnDistance = 3f;
    [SerializeField] private LayerMask groundLayer;
    [Tooltip("Échelle par défaut des objets spawnés (0.1 = 10%).")]
    [SerializeField] private float defaultScale = 0.2f;

    [Header("XR References")]
    [SerializeField] private XRRayInteractor rightHandRay;

    [Header("Input")]
    [SerializeField] private InputActionReference spawnAction;
    [SerializeField] private InputActionReference nextItemAction;
    [SerializeField] private InputActionReference prevItemAction;

    [Header("Audio")]
    [SerializeField] private AudioClip spawnSound;

    private AudioSource audioSource;
    private int selectedIndex;
    private SpawnableItem[] spawnableItems;

    public SpawnableItem[] SpawnableItems => spawnableItems;
    public int SelectedIndex => selectedIndex;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        LoadSpawnableItems();
    }

    private void LoadSpawnableItems()
    {
        // Charger tous les prefabs depuis Resources/Furniture
        var prefabs = Resources.LoadAll<GameObject>("Furniture");
        System.Array.Sort(prefabs, (a, b) => string.Compare(a.name, b.name));

        spawnableItems = new SpawnableItem[prefabs.Length];
        for (int i = 0; i < prefabs.Length; i++)
        {
            spawnableItems[i] = new SpawnableItem
            {
                name = FormatName(prefabs[i].name),
                prefab = prefabs[i]
            };
        }

        Debug.Log($"[ObjectSpawner] {spawnableItems.Length} objets chargés");
    }

    /// <summary>
    /// Enlève les préfixes numériques (ex: "01_Chaise_Pikachu" → "Chaise Pikachu").
    /// </summary>
    private static string FormatName(string raw)
    {
        // Enlever préfixe "01_" si présent
        int underscore = raw.IndexOf('_');
        if (underscore > 0 && underscore <= 3 && int.TryParse(raw.Substring(0, underscore), out _))
            raw = raw.Substring(underscore + 1);
        return raw.Replace('_', ' ');
    }

    private void OnEnable()
    {
        if (spawnAction != null && spawnAction.action != null)
        {
            spawnAction.action.Enable();
            spawnAction.action.performed += OnSpawn;
        }
        if (nextItemAction != null && nextItemAction.action != null)
        {
            nextItemAction.action.Enable();
            nextItemAction.action.performed += OnNextItem;
        }
        if (prevItemAction != null && prevItemAction.action != null)
        {
            prevItemAction.action.Enable();
            prevItemAction.action.performed += OnPrevItem;
        }
    }

    private void OnDisable()
    {
        if (spawnAction != null && spawnAction.action != null)
        {
            spawnAction.action.performed -= OnSpawn;
            spawnAction.action.Disable();
        }
        if (nextItemAction != null && nextItemAction.action != null)
        {
            nextItemAction.action.performed -= OnNextItem;
            nextItemAction.action.Disable();
        }
        if (prevItemAction != null && prevItemAction.action != null)
        {
            prevItemAction.action.performed -= OnPrevItem;
            prevItemAction.action.Disable();
        }
    }

    public void SetSelectedIndex(int index)
    {
        if (spawnableItems == null || spawnableItems.Length == 0) return;
        selectedIndex = ((index % spawnableItems.Length) + spawnableItems.Length) % spawnableItems.Length;
    }

    /// <summary>
    /// Spawn immédiatement l'objet sélectionné (appelable depuis un bouton UI).
    /// </summary>
    public void SpawnSelected()
    {
        OnSpawn(default);
    }

    private void OnNextItem(InputAction.CallbackContext ctx) => SetSelectedIndex(selectedIndex + 1);
    private void OnPrevItem(InputAction.CallbackContext ctx) => SetSelectedIndex(selectedIndex - 1);

    private void OnSpawn(InputAction.CallbackContext ctx)
    {
        if (spawnableItems == null || spawnableItems.Length == 0) return;
        SpawnItem(selectedIndex);
    }

    /// <summary>
    /// Spawn un objet par index (appelable depuis le menu UI).
    /// </summary>
    public void SpawnItem(int index)
    {
        if (spawnableItems == null || spawnableItems.Length == 0) return;
        if (index < 0 || index >= spawnableItems.Length) return;

        Vector3 spawnPos = GetSpawnPosition();
        GameObject obj = Instantiate(spawnableItems[index].prefab, spawnPos, Quaternion.identity);
        obj.tag = "SpawnedObject";
        obj.transform.localScale = Vector3.one * defaultScale;

        // Nettoyer les caméras et lumières héritées du modèle 3D
        StripCamerasAndLights(obj);

        MakeGrabbable(obj);

        if (spawnSound != null)
            audioSource.PlayOneShot(spawnSound);
    }

    /// <summary>
    /// Supprime toutes les caméras et lumières du modèle spawné.
    /// Les GLB/FBX importés contiennent souvent les cameras/lights du logiciel d'origine.
    /// </summary>
    private static void StripCamerasAndLights(GameObject obj)
    {
        // URP ajoute des composants requis sur Camera/Light : on détruit le GameObject entier
        foreach (var cam in obj.GetComponentsInChildren<Camera>(true))
        {
            if (cam == null) continue;
            if (cam.gameObject == obj) cam.enabled = false;
            else Destroy(cam.gameObject);
        }

        foreach (var l in obj.GetComponentsInChildren<Light>(true))
        {
            if (l == null) continue;
            if (l.gameObject == obj) l.enabled = false;
            else Destroy(l.gameObject);
        }

        foreach (var al in obj.GetComponentsInChildren<AudioListener>(true))
        {
            if (al == null) continue;
            al.enabled = false;
            if (al.gameObject != obj) Destroy(al.gameObject);
        }
    }

    /// <summary>
    /// Ajoute Rigidbody + Collider + XRGrabInteractable (far-grab blaster).
    /// </summary>
    private static void MakeGrabbable(GameObject obj)
    {
        if (obj.GetComponent<Rigidbody>() == null)
        {
            var rb = obj.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        if (obj.GetComponent<Collider>() == null)
        {
            var renderers = obj.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length > 0)
            {
                Bounds combined = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    combined.Encapsulate(renderers[i].bounds);

                var box = obj.AddComponent<BoxCollider>();
                box.center = obj.transform.InverseTransformPoint(combined.center);
                Vector3 scale = obj.transform.lossyScale;
                box.size = new Vector3(
                    combined.size.x / scale.x,
                    combined.size.y / scale.y,
                    combined.size.z / scale.z);
            }
            else
            {
                obj.AddComponent<BoxCollider>();
            }
        }

        if (obj.GetComponent<XRGrabInteractable>() == null)
        {
            var g = obj.AddComponent<XRGrabInteractable>();
            g.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            g.throwOnDetach = true;
            g.useDynamicAttach = true;
            g.farAttachMode = InteractableFarAttachMode.Near;
            g.smoothPosition = true;
            g.smoothPositionAmount = 12f;
            g.tightenPosition = 0.5f;
            g.smoothRotation = true;
            g.smoothRotationAmount = 12f;
            g.tightenRotation = 0.5f;
            g.attachEaseInTime = 0.1f;
        }
    }

    private Vector3 GetSpawnPosition()
    {
        if (rightHandRay != null && rightHandRay.TryGetCurrent3DRaycastHit(out RaycastHit xrHit))
            return xrHit.point + Vector3.up * 0.2f;

        Camera cam = Camera.main;
        if (cam != null)
        {
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, spawnDistance * 2f, groundLayer))
                return hit.point + Vector3.up * 0.2f;

            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            forward.Normalize();
            return cam.transform.position + forward * spawnDistance;
        }

        return transform.position + Vector3.forward * spawnDistance;
    }
}

[System.Serializable]
public class SpawnableItem
{
    public string name;
    public GameObject prefab;
    public Sprite icon;
}
