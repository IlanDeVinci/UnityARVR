using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Réinitialisation totale de la scène (peintures, tableaux, meubles,
/// boomboxes) sur double-press du bouton A, avec popup de confirmation VR.
///
/// Flow :
///   A pressé une fois  → arme le timer (cyan)
///   A pressé une 2ème fois dans la fenêtre → popup s'affiche
///   Confirmer / Annuler dans la popup
/// </summary>
public class ResetManager : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference resetAction;

    [Tooltip("Touche clavier de fallback pour tester la popup (utile dans l'éditeur sans casque).")]
    [SerializeField] private Key keyboardFallback = Key.R;

    [Tooltip("Active la binding directe sur le bouton A (RightHand PrimaryButton) sans avoir besoin d'assigner une InputActionReference.")]
    [SerializeField] private bool autoBindRightPrimaryButton = true;

    [Tooltip("Fenêtre de temps (s) pour le 2ème appui sur A.")]
    [SerializeField] private float doublePressWindow = 0.4f;

    [Header("Diagnostics")]
    [SerializeField] private bool verbose = true;

    private InputAction directAButtonAction;

    [Header("Conteneurs à vider")]
    [SerializeField] private string[] containerNames = { "PaintStamps", "Tableaux", "Boomboxes" };

    [Tooltip("Tag des meubles placés (créés par ObjectSpawner).")]
    [SerializeField] private string spawnedObjectTag = "SpawnedObject";

    [Header("Popup")]
    [Tooltip("Distance de la popup devant la caméra (m).")]
    [SerializeField] private float popupDistance = 1.5f;
    [SerializeField] private Vector2 popupSize = new Vector2(560f, 320f);
    [SerializeField] private float popupWorldScale = 0.0025f;

    private float lastPressTime = -10f;
    private GameObject popupRoot;

    private void OnEnable()
    {
        if (resetAction != null && resetAction.action != null)
        {
            resetAction.action.Enable();
            resetAction.action.performed += OnResetPressed;
            if (verbose) Debug.Log($"[ResetManager] Reset Action wired : {resetAction.action.name}", this);
        }
        else if (verbose)
        {
            Debug.LogWarning("[ResetManager] resetAction non assigné (OK si autoBindRightPrimaryButton=true).", this);
        }

        if (autoBindRightPrimaryButton)
        {
            directAButtonAction = new InputAction("ResetDoublePress_A", InputActionType.Button,
                "<XRController>{RightHand}/primaryButton");
            directAButtonAction.performed += OnResetPressed;
            directAButtonAction.Enable();
            if (verbose) Debug.Log("[ResetManager] Auto-binding sur A (RightHand PrimaryButton).", this);
        }
    }

    private void OnDisable()
    {
        if (resetAction != null && resetAction.action != null)
        {
            resetAction.action.performed -= OnResetPressed;
            resetAction.action.Disable();
        }
        if (directAButtonAction != null)
        {
            directAButtonAction.performed -= OnResetPressed;
            directAButtonAction.Disable();
            directAButtonAction.Dispose();
            directAButtonAction = null;
        }
    }

    private void Update()
    {
        // Fallback clavier pour test rapide dans l'éditeur sans casque.
        var kb = Keyboard.current;
        if (kb != null && kb[keyboardFallback].wasPressedThisFrame)
        {
            if (verbose) Debug.Log($"[ResetManager] Touche fallback {keyboardFallback} détectée.");
            OnResetPressed(default);
        }
    }

    private void OnResetPressed(InputAction.CallbackContext ctx)
    {
        // Si la popup est déjà ouverte, on ignore.
        if (popupRoot != null && popupRoot.activeSelf) return;

        float now = Time.unscaledTime;
        float delta = now - lastPressTime;
        if (delta <= doublePressWindow)
        {
            if (verbose) Debug.Log($"[ResetManager] Double-press détecté (Δ={delta:F2}s) → popup.");
            ShowPopup();
            lastPressTime = -10f;
        }
        else
        {
            if (verbose) Debug.Log($"[ResetManager] 1er appui (armement). Appuie à nouveau dans {doublePressWindow:F2}s.");
            lastPressTime = now;
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  Reset action
    // ────────────────────────────────────────────────────────────────────

    private void DoResetAll()
    {
        int destroyed = 0;
        foreach (var name in containerNames)
        {
            var container = GameObject.Find(name);
            if (container == null) continue;
            for (int i = container.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(container.transform.GetChild(i).gameObject);
                destroyed++;
            }
        }

        // Meubles taggés
        if (!string.IsNullOrEmpty(spawnedObjectTag))
        {
            GameObject[] tagged = null;
            try { tagged = GameObject.FindGameObjectsWithTag(spawnedObjectTag); }
            catch { /* tag non défini */ }
            if (tagged != null)
            {
                foreach (var t in tagged)
                {
                    if (t == null) continue;
                    Destroy(t);
                    destroyed++;
                }
            }
        }

        // Tableaux orphelins au scène root (cas drag-drop dont le parent serait null).
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        foreach (var r in roots)
        {
            if (r != null && r.name.StartsWith("Tableau_"))
            {
                Destroy(r);
                destroyed++;
            }
        }

        Debug.Log($"[ResetManager] {destroyed} objet(s) détruit(s).");
    }

    // ────────────────────────────────────────────────────────────────────
    //  Popup VR
    // ────────────────────────────────────────────────────────────────────

    private void ShowPopup()
    {
        if (popupRoot != null)
        {
            popupRoot.SetActive(true);
            PositionPopupInFrontOfCamera();
            return;
        }
        BuildPopup();
        PositionPopupInFrontOfCamera();
    }

    private void HidePopup()
    {
        if (popupRoot != null) popupRoot.SetActive(false);
    }

    private void PositionPopupInFrontOfCamera()
    {
        if (popupRoot == null) return;
        var cam = Camera.main;
        if (cam == null)
        {
            // Fallback : prendre la première Camera active de la scène (XR rig sans tag MainCamera).
            var cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            if (cams != null && cams.Length > 0) cam = cams[0];
        }
        if (cam == null)
        {
            Debug.LogWarning("[ResetManager] Aucune caméra trouvée — popup placée à l'origine.", this);
            popupRoot.transform.position = Vector3.zero;
            popupRoot.transform.rotation = Quaternion.identity;
            return;
        }
        popupRoot.transform.position = cam.transform.position + cam.transform.forward * popupDistance;
        popupRoot.transform.rotation = Quaternion.LookRotation(popupRoot.transform.position - cam.transform.position, Vector3.up);
    }

    private void BuildPopup()
    {
        popupRoot = new GameObject("ResetConfirmPopup");
        var canvas = popupRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        popupRoot.AddComponent<CanvasScaler>();
        popupRoot.AddComponent<TrackedDeviceGraphicRaycaster>();

        var rt = popupRoot.GetComponent<RectTransform>();
        rt.sizeDelta = popupSize;
        rt.localScale = Vector3.one * popupWorldScale;

        // Background panel
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(popupRoot.transform, false);
        var pRT = panel.GetComponent<RectTransform>();
        pRT.anchorMin = Vector2.zero;
        pRT.anchorMax = Vector2.one;
        pRT.offsetMin = pRT.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.06f, 0.04f, 0.10f, 0.97f);
        var outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0.95f, 0.30f, 0.30f, 0.9f);
        outline.effectDistance = new Vector2(3f, -3f);

        // Title
        CreateText("Title", panel.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -42f), new Vector2(0f, 50f),
            "RÉINITIALISER LA SCÈNE", 28,
            new Color(1f, 0.45f, 0.30f),
            TextAlignmentOptions.Center, FontStyles.Bold);

        // Message
        CreateText("Msg", panel.transform,
            new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(0f, 30f), new Vector2(0f, 80f),
            "Toutes les peintures, tableaux, objets\net musiques placés seront supprimés.\n\nContinuer ?",
            18, new Color(0.85f, 0.85f, 0.90f),
            TextAlignmentOptions.Center, FontStyles.Normal);

        // Boutons
        BuildButton("Cancel", panel.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(-90f, 60f), new Vector2(160f, 70f),
            "ANNULER", new Color(0.30f, 0.30f, 0.30f),
            () => HidePopup());

        BuildButton("Confirm", panel.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(90f, 60f), new Vector2(160f, 70f),
            "RÉINITIALISER", new Color(0.78f, 0.18f, 0.18f),
            () => { DoResetAll(); HidePopup(); });
    }

    private static void BuildButton(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 size,
        string label, Color bg, System.Action onClick)
    {
        var btn = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btn.transform.SetParent(parent, false);
        var rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        btn.GetComponent<Image>().color = bg;

        CreateText("Lbl", btn.transform,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            label, 20, Color.white,
            TextAlignmentOptions.Center, FontStyles.Bold)
            .GetComponent<RectTransform>().offsetMin = Vector2.zero;

        var b = btn.GetComponent<Button>();
        b.targetGraphic = btn.GetComponent<Image>();
        b.onClick.AddListener(() => onClick?.Invoke());
    }

    private static GameObject CreateText(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta,
        string text, float fontSize, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.Center,
        FontStyles style = FontStyles.Normal)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.raycastTarget = false;
        return obj;
    }
}
