using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Editor tool: builds the Pokemon Restaurant scene in one click.
/// Room construction is skipped — use your custom 3D model for the restaurant.
/// Menu: Pokemon Restaurant > Build All
/// </summary>
public class RestaurantBuilder : EditorWindow
{
    // --- Input Action Wiring ---

    /// <summary>
    /// Finds a persistent InputActionReference sub-asset from the .inputactions file.
    /// This is the ONLY correct way to assign InputActionReferences that persist in the scene.
    /// </summary>
    private static InputActionReference FindActionReference(InputActionAsset asset, string mapName, string actionName)
    {
        string assetPath = AssetDatabase.GetAssetPath(asset);
        Object[] allSubAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

        foreach (Object subAsset in allSubAssets)
        {
            if (subAsset is InputActionReference actionRef
                && actionRef.action != null
                && actionRef.action.actionMap.name == mapName
                && actionRef.action.name == actionName)
            {
                return actionRef;
            }
        }

        Debug.LogWarning($"[Pokemon Restaurant] InputActionReference not found: {mapName}/{actionName}");
        return null;
    }

    /// <summary>
    /// Finds the InputActionAsset in the project.
    /// </summary>
    private static InputActionAsset FindInputActionAsset()
    {
        string[] guids = AssetDatabase.FindAssets("InputSystem_Actions t:InputActionAsset");
        if (guids.Length == 0)
        {
            Debug.LogError("[Pokemon Restaurant] InputSystem_Actions.inputactions not found in project!");
            return null;
        }
        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
    }

    /// <summary>
    /// Assigns an InputActionReference to a serialized field on a MonoBehaviour.
    /// </summary>
    private static void AssignInputAction(MonoBehaviour target, string fieldName, InputActionAsset asset, string mapName, string actionName)
    {
        InputActionReference actionRef = FindActionReference(asset, mapName, actionName);
        if (actionRef == null) return;

        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop != null)
        {
            prop.objectReferenceValue = actionRef;
            so.ApplyModifiedProperties();
        }
        else
        {
            Debug.LogWarning($"[Pokemon Restaurant] Field '{fieldName}' not found on {target.GetType().Name}");
        }
    }

    // --- Build Menu Items ---

    [MenuItem("Pokemon Restaurant/1 - Build All (Player + Manager + UI + Wire Inputs)")]
    public static void BuildAll()
    {
        BuildPlayer();
        BuildGameManager();
        BuildUI();
        WireAllInputActions();
        Debug.Log("[Pokemon Restaurant] Done! Place your custom 3D restaurant model in the scene, assign layer 'Ground' to walkable surfaces and 'Interactable' to doors/switches.");
    }

    [MenuItem("Pokemon Restaurant/2 - Build Player")]
    public static void BuildPlayer()
    {
        GameObject existing = GameObject.Find("Player");
        if (existing != null) Object.DestroyImmediate(existing);

        // Player root
        GameObject player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 1f, 0f);

        CharacterController cc = player.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.5f;
        cc.center = Vector3.zero;

        player.AddComponent<PlayerController>();
        player.AddComponent<PlayerInteraction>();
        player.AddComponent<ObjectSpawner>();
        player.AddComponent<ObjectManipulator>();

        // Camera as child
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            mainCam = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
        }

        mainCam.transform.SetParent(player.transform);
        mainCam.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        mainCam.transform.localRotation = Quaternion.identity;

        CameraController camCtrl = mainCam.GetComponent<CameraController>();
        if (camCtrl == null)
            camCtrl = mainCam.gameObject.AddComponent<CameraController>();

        // Wire CameraController ref in PlayerInteraction
        SetProp(new SerializedObject(player.GetComponent<PlayerInteraction>()), "cameraController", camCtrl);

        Debug.Log("[Pokemon Restaurant] Player built.");
    }

    [MenuItem("Pokemon Restaurant/3 - Build Game Manager")]
    public static void BuildGameManager()
    {
        GameObject existing = GameObject.Find("GameManager");
        if (existing != null) Object.DestroyImmediate(existing);

        GameObject gm = new GameObject("GameManager");
        gm.AddComponent<GameManager>();
        gm.AddComponent<SceneResetManager>();
        gm.AddComponent<AudioManager>();

        // Wire references
        SerializedObject so = new SerializedObject(gm.GetComponent<GameManager>());

        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            SetProp(so, "player", player.GetComponent<PlayerController>());
            SetProp(so, "objectSpawner", player.GetComponent<ObjectSpawner>());

            Camera mainCam = Camera.main;
            if (mainCam != null)
                SetProp(so, "cameraController", mainCam.GetComponent<CameraController>());
        }
        so.ApplyModifiedProperties();

        Debug.Log("[Pokemon Restaurant] GameManager built.");
    }

    [MenuItem("Pokemon Restaurant/4 - Build UI")]
    public static void BuildUI()
    {
        GameObject existingCanvas = GameObject.Find("GameCanvas");
        if (existingCanvas != null) Object.DestroyImmediate(existingCanvas);

        // --- Canvas ---
        GameObject canvas = new GameObject("GameCanvas");
        Canvas c = canvas.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 10;

        var scaler = canvas.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // EventSystem
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // --- HUD ---
        GameObject hud = new GameObject("HUD");
        hud.AddComponent<RectTransform>();
        hud.transform.SetParent(canvas.transform, false);

        // Crosshair (small dot)
        GameObject crosshair = new GameObject("Crosshair", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        crosshair.transform.SetParent(hud.transform, false);
        RectTransform crossRT = crosshair.GetComponent<RectTransform>();
        crossRT.sizeDelta = new Vector2(6f, 6f);
        crossRT.anchoredPosition = Vector2.zero;
        crossRT.anchorMin = crossRT.anchorMax = new Vector2(0.5f, 0.5f);
        crosshair.GetComponent<UnityEngine.UI.Image>().color = Color.white;

        // Interact text (bottom center)
        GameObject interactText = CreateTMPText("InteractText", hud.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(600f, 40f),
            "", 20, TextAlignmentOptions.Center);

        // Selected object text (bottom left)
        GameObject selectedText = CreateTMPText("SelectedObjectText", hud.transform,
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 20f), new Vector2(400f, 30f),
            "", 16, TextAlignmentOptions.Left);

        // Hint text (top center)
        GameObject hintText = CreateTMPText("HintText", hud.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(500f, 30f),
            "Tab - Menu  |  E - Interagir  |  F - Placer", 14, TextAlignmentOptions.Center);

        // HUDController
        HUDController hudCtrl = hud.AddComponent<HUDController>();
        SerializedObject hudSO = new SerializedObject(hudCtrl);
        SetProp(hudSO, "crosshair", crosshair);
        SetProp(hudSO, "interactText", interactText.GetComponent<TMP_Text>());
        SetProp(hudSO, "selectedObjectText", selectedText.GetComponent<TMP_Text>());
        SetProp(hudSO, "hintText", hintText.GetComponent<TMP_Text>());

        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
                SetProp(hudSO, "cameraController", mainCam.GetComponent<CameraController>());
            SetProp(hudSO, "objectSpawner", player.GetComponent<ObjectSpawner>());
            SetProp(hudSO, "objectManipulator", player.GetComponent<ObjectManipulator>());
        }
        hudSO.ApplyModifiedProperties();

        // --- Spawn Menu Panel ---
        GameObject menuPanel = new GameObject("SpawnMenuPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        menuPanel.transform.SetParent(canvas.transform, false);
        RectTransform menuRT = menuPanel.GetComponent<RectTransform>();
        menuRT.anchorMin = new Vector2(0.2f, 0.15f);
        menuRT.anchorMax = new Vector2(0.8f, 0.85f);
        menuRT.offsetMin = menuRT.offsetMax = Vector2.zero;
        menuPanel.GetComponent<UnityEngine.UI.Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.92f);

        // Menu title
        CreateTMPText("MenuTitle", menuPanel.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(400f, 45f),
            "MENU SPAWN - Restaurant Pokemon", 24, TextAlignmentOptions.Center);

        // Button grid
        GameObject grid = new GameObject("ButtonGrid", typeof(RectTransform));
        grid.transform.SetParent(menuPanel.transform, false);
        RectTransform gridRT = grid.GetComponent<RectTransform>();
        gridRT.anchorMin = new Vector2(0.05f, 0.05f);
        gridRT.anchorMax = new Vector2(0.95f, 0.85f);
        gridRT.offsetMin = gridRT.offsetMax = Vector2.zero;

        var glg = grid.AddComponent<UnityEngine.UI.GridLayoutGroup>();
        glg.cellSize = new Vector2(130f, 90f);
        glg.spacing = new Vector2(15f, 15f);
        glg.childAlignment = TextAnchor.UpperCenter;
        glg.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 4;

        // SpawnMenuUI
        SpawnMenuUI spawnMenu = canvas.AddComponent<SpawnMenuUI>();
        SerializedObject menuSO = new SerializedObject(spawnMenu);
        SetProp(menuSO, "menuPanel", menuPanel);
        SetProp(menuSO, "buttonContainer", grid.transform);
        if (player != null)
            SetProp(menuSO, "objectSpawner", player.GetComponent<ObjectSpawner>());
        menuSO.ApplyModifiedProperties();

        // Wire into GameManager
        GameObject gmObj = GameObject.Find("GameManager");
        if (gmObj != null)
        {
            SerializedObject gmSO = new SerializedObject(gmObj.GetComponent<GameManager>());
            SetProp(gmSO, "spawnMenuUI", spawnMenu);
            gmSO.ApplyModifiedProperties();
        }

        menuPanel.SetActive(false);

        Debug.Log("[Pokemon Restaurant] UI built.");
    }

    [MenuItem("Pokemon Restaurant/5 - Wire All Input Actions")]
    public static void WireAllInputActions()
    {
        InputActionAsset asset = FindInputActionAsset();
        if (asset == null) return;

        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("[Pokemon Restaurant] Player not found! Run Build Player first.");
            return;
        }

        Camera mainCam = Camera.main;

        // PlayerController: Move, Sprint
        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null)
        {
            AssignInputAction(pc, "moveAction", asset, "Player", "Move");
            AssignInputAction(pc, "sprintAction", asset, "Player", "Sprint");
        }

        // CameraController: Look
        if (mainCam != null)
        {
            CameraController cc = mainCam.GetComponent<CameraController>();
            if (cc != null)
                AssignInputAction(cc, "lookAction", asset, "Player", "Look");
        }

        // PlayerInteraction: Interact
        PlayerInteraction pi = player.GetComponent<PlayerInteraction>();
        if (pi != null)
            AssignInputAction(pi, "interactAction", asset, "Player", "Interact");

        // ObjectSpawner: Spawn
        ObjectSpawner os = player.GetComponent<ObjectSpawner>();
        if (os != null)
            AssignInputAction(os, "spawnAction", asset, "Player", "Spawn");

        // ObjectManipulator: Attack (click), Rotate, Delete, Look
        ObjectManipulator om = player.GetComponent<ObjectManipulator>();
        if (om != null)
        {
            AssignInputAction(om, "clickAction", asset, "Player", "Attack");
            AssignInputAction(om, "rotateAction", asset, "Player", "Rotate");
            AssignInputAction(om, "deleteAction", asset, "Player", "Delete");
            AssignInputAction(om, "lookAction", asset, "Player", "Look");
        }

        // SpawnMenuUI: OpenMenu
        SpawnMenuUI smu = Object.FindAnyObjectByType<SpawnMenuUI>();
        if (smu != null)
            AssignInputAction(smu, "menuToggleAction", asset, "Player", "OpenMenu");

        // SceneResetManager: Reset
        SceneResetManager srm = Object.FindAnyObjectByType<SceneResetManager>();
        if (srm != null)
            AssignInputAction(srm, "resetAction", asset, "Player", "Reset");

        Debug.Log("[Pokemon Restaurant] All InputActionReferences wired successfully!");
    }

    // --- Helpers ---

    private static GameObject CreateTMPText(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta,
        string text, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;

        return obj;
    }

    private static void SetProp(SerializedObject so, string propName, Object value)
    {
        SerializedProperty prop = so.FindProperty(propName);
        if (prop != null)
        {
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }
    }
}
