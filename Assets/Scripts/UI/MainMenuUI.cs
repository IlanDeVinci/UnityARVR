using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Menu principal VR affiché au lancement.
/// Un Canvas World Space avec titre + bouton Play.
/// Quand on appuie sur Play, le menu disparaît.
/// Le jeu reste actif pendant l'affichage du menu (le joueur peut regarder autour).
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip clickSound;

    private Canvas canvas;
    private AudioSource audioSource;

    private void Awake()
    {
        // Sécurité : s'assurer que le temps tourne (au cas où il aurait été figé)
        Time.timeScale = 1f;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;

        BuildMenu();
    }

    private void BuildMenu()
    {
        // --- Canvas World Space ---
        canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        if (canvas.worldCamera == null)
            canvas.worldCamera = Camera.main;

        // Taille du canvas
        RectTransform canvasRT = GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(800f, 600f);
        canvasRT.localScale = Vector3.one * 0.002f; // 1.6m x 1.2m en monde

        // Positionner devant le joueur
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            forward.Normalize();
            transform.position = cam.transform.position + forward * 2.5f + Vector3.up * 1.5f;
            transform.rotation = Quaternion.LookRotation(forward);
        }
        else
        {
            transform.position = new Vector3(0f, 1.5f, 2.5f);
            transform.rotation = Quaternion.identity;
        }

        // Raycaster VR
        if (GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        var oldRaycaster = GetComponent<GraphicRaycaster>();
        if (oldRaycaster != null)
            Destroy(oldRaycaster);

        // CanvasScaler
        if (GetComponent<CanvasScaler>() == null)
            gameObject.AddComponent<CanvasScaler>();

        // --- Fond semi-transparent ---
        GameObject bgObj = CreateUIElement("Background", transform);
        RectTransform bgRT = bgObj.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.05f, 0.02f, 0.08f, 0.85f);

        // --- Titre ---
        GameObject titleObj = CreateUIElement("Title", bgObj.transform);
        RectTransform titleRT = titleObj.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 0.7f);
        titleRT.anchorMax = new Vector2(0.5f, 0.7f);
        titleRT.anchoredPosition = Vector2.zero;
        titleRT.sizeDelta = new Vector2(700f, 100f);
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "Pokemon Restaurant";
        titleText.fontSize = 64;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(1f, 0.85f, 0.3f);
        titleText.fontStyle = FontStyles.Bold;

        // --- Sous-titre ---
        GameObject subObj = CreateUIElement("Subtitle", bgObj.transform);
        RectTransform subRT = subObj.GetComponent<RectTransform>();
        subRT.anchorMin = new Vector2(0.5f, 0.58f);
        subRT.anchorMax = new Vector2(0.5f, 0.58f);
        subRT.anchoredPosition = Vector2.zero;
        subRT.sizeDelta = new Vector2(600f, 50f);
        TextMeshProUGUI subText = subObj.AddComponent<TextMeshProUGUI>();
        subText.text = "Restaurant Japonais VR";
        subText.fontSize = 28;
        subText.alignment = TextAlignmentOptions.Center;
        subText.color = new Color(0.8f, 0.8f, 0.8f);

        // --- Bouton Play ---
        GameObject btnObj = CreateUIElement("PlayButton", bgObj.transform);
        RectTransform btnRT = btnObj.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0.5f, 0.35f);
        btnRT.anchorMax = new Vector2(0.5f, 0.35f);
        btnRT.anchoredPosition = Vector2.zero;
        btnRT.sizeDelta = new Vector2(300f, 80f);

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.9f, 0.3f, 0.2f);

        Button btn = btnObj.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = new Color(0.9f, 0.3f, 0.2f);
        colors.highlightedColor = new Color(1f, 0.45f, 0.3f);
        colors.pressedColor = new Color(0.7f, 0.2f, 0.15f);
        btn.colors = colors;
        btn.onClick.AddListener(OnPlayClicked);

        // Texte du bouton
        GameObject btnTextObj = CreateUIElement("Text", btnObj.transform);
        RectTransform btnTextRT = btnTextObj.GetComponent<RectTransform>();
        btnTextRT.anchorMin = Vector2.zero;
        btnTextRT.anchorMax = Vector2.one;
        btnTextRT.offsetMin = btnTextRT.offsetMax = Vector2.zero;
        TextMeshProUGUI btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
        btnText.text = "JOUER";
        btnText.fontSize = 42;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.color = Color.white;
        btnText.fontStyle = FontStyles.Bold;

        // --- Instructions ---
        GameObject hintObj = CreateUIElement("Hint", bgObj.transform);
        RectTransform hintRT = hintObj.GetComponent<RectTransform>();
        hintRT.anchorMin = new Vector2(0.5f, 0.15f);
        hintRT.anchorMax = new Vector2(0.5f, 0.15f);
        hintRT.anchoredPosition = Vector2.zero;
        hintRT.sizeDelta = new Vector2(600f, 80f);
        TextMeshProUGUI hintText = hintObj.AddComponent<TextMeshProUGUI>();
        hintText.text = "Pointez et cliquez sur JOUER pour commencer";
        hintText.fontSize = 20;
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.color = new Color(0.6f, 0.6f, 0.6f);
    }

    private void OnPlayClicked()
    {
        if (clickSound != null)
            audioSource.PlayOneShot(clickSound);

        // Détruire le menu
        Destroy(gameObject);
    }

    private static GameObject CreateUIElement(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }
}
