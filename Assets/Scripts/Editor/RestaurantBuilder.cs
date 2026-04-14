using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Editor tool: sets up the Pokemon Restaurant VR scene.
/// Uses XR Interaction Toolkit's XR Origin from Starter Assets.
/// Menu: Pokemon Restaurant > Build All
///
/// PREREQUISITES:
/// 1. Import "Starter Assets" sample from XR Interaction Toolkit in Package Manager
/// 2. Place your custom 3D restaurant model in the scene
/// </summary>
public class RestaurantBuilder : EditorWindow
{
    // --- Input Action Wiring ---

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

    private static InputActionAsset FindInputActionAsset()
    {
        // Try XRI default actions first
        string[] guids = AssetDatabase.FindAssets("XRI Default Input Actions t:InputActionAsset");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        }

        // Fallback to our custom one
        guids = AssetDatabase.FindAssets("InputSystem_Actions t:InputActionAsset");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        }

        Debug.LogError("[Pokemon Restaurant] No InputActionAsset found!");
        return null;
    }

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
    }

    // --- Build Menu Items ---

    [MenuItem("Pokemon Restaurant/1 - Setup Game Systems (Manager + Scripts on XR Origin)")]
    public static void SetupGameSystems()
    {
        SetupXROriginScripts();
        BuildGameManager();
        Debug.Log("[Pokemon Restaurant] Game systems setup complete!");
    }

    [MenuItem("Pokemon Restaurant/2 - Setup XR Origin Scripts")]
    public static void SetupXROriginScripts()
    {
        // Find XR Origin in scene
        var xrOrigin = Object.FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (xrOrigin == null)
        {
            Debug.LogError("[Pokemon Restaurant] XR Origin not found in scene!\n" +
                "1. Open Package Manager > XR Interaction Toolkit > Samples\n" +
                "2. Import 'Starter Assets'\n" +
                "3. Drag the 'XR Origin (XR Rig)' prefab from Samples into your scene\n" +
                "4. Run this menu again.");
            return;
        }

        GameObject xrRoot = xrOrigin.gameObject;

        // Add our scripts to XR Origin root
        if (xrRoot.GetComponent<PlayerController>() == null)
            xrRoot.AddComponent<PlayerController>();
        if (xrRoot.GetComponent<PlayerInteraction>() == null)
            xrRoot.AddComponent<PlayerInteraction>();
        if (xrRoot.GetComponent<ObjectSpawner>() == null)
            xrRoot.AddComponent<ObjectSpawner>();
        if (xrRoot.GetComponent<ObjectManipulator>() == null)
            xrRoot.AddComponent<ObjectManipulator>();

        // Add CameraController to the XR camera
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            if (mainCam.GetComponent<CameraController>() == null)
                mainCam.gameObject.AddComponent<CameraController>();
        }

        // Wire ObjectManipulator -> CameraController
        ObjectManipulator om = xrRoot.GetComponent<ObjectManipulator>();
        if (om != null && mainCam != null)
        {
            SerializedObject omSO = new SerializedObject(om);
            SetProp(omSO, "cameraController", mainCam.GetComponent<CameraController>());
            omSO.ApplyModifiedProperties();
        }

        Debug.Log("[Pokemon Restaurant] Scripts added to XR Origin.");
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
        var xrOrigin = Object.FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (xrOrigin != null)
        {
            GameObject xrRoot = xrOrigin.gameObject;
            SerializedObject so = new SerializedObject(gm.GetComponent<GameManager>());
            SetProp(so, "player", xrRoot.GetComponent<PlayerController>());
            SetProp(so, "objectSpawner", xrRoot.GetComponent<ObjectSpawner>());
            Camera mainCam = Camera.main;
            if (mainCam != null)
                SetProp(so, "cameraController", mainCam.GetComponent<CameraController>());
            so.ApplyModifiedProperties();
        }

        Debug.Log("[Pokemon Restaurant] GameManager built.");
    }

    [MenuItem("Pokemon Restaurant/4 - Build VR Spawn Menu (World Space Canvas)")]
    public static void BuildVRSpawnMenu()
    {
        // Clean up
        GameObject existingCanvas = GameObject.Find("VRSpawnMenuCanvas");
        if (existingCanvas != null) Object.DestroyImmediate(existingCanvas);

        // World Space Canvas (for VR)
        GameObject canvas = new GameObject("VRSpawnMenuCanvas");
        Canvas c = canvas.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;

        canvas.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Size and position the canvas
        RectTransform canvasRT = canvas.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(600f, 400f);
        canvasRT.localScale = Vector3.one * 0.001f; // 1mm per pixel, so 60cm x 40cm
        canvas.transform.position = new Vector3(0f, 1.2f, 1.5f); // in front of player at start

        // Optionally parent to left hand for wrist menu
        // (user can reparent this to LeftHand Controller in the hierarchy)

        // Menu panel
        GameObject menuPanel = new GameObject("SpawnMenuPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        menuPanel.transform.SetParent(canvas.transform, false);
        RectTransform menuRT = menuPanel.GetComponent<RectTransform>();
        menuRT.anchorMin = Vector2.zero;
        menuRT.anchorMax = Vector2.one;
        menuRT.offsetMin = menuRT.offsetMax = Vector2.zero;
        menuPanel.GetComponent<UnityEngine.UI.Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

        // Title
        CreateTMPText("MenuTitle", menuPanel.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -25f), new Vector2(500f, 40f),
            "MENU SPAWN - Restaurant Pokemon", 28, TextAlignmentOptions.Center);

        // Selected item text
        GameObject selectedText = CreateTMPText("SelectedText", menuPanel.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 25f), new Vector2(400f, 30f),
            "", 20, TextAlignmentOptions.Center);

        // Button grid
        GameObject grid = new GameObject("ButtonGrid", typeof(RectTransform));
        grid.transform.SetParent(menuPanel.transform, false);
        RectTransform gridRT = grid.GetComponent<RectTransform>();
        gridRT.anchorMin = new Vector2(0.05f, 0.1f);
        gridRT.anchorMax = new Vector2(0.95f, 0.85f);
        gridRT.offsetMin = gridRT.offsetMax = Vector2.zero;

        var glg = grid.AddComponent<UnityEngine.UI.GridLayoutGroup>();
        glg.cellSize = new Vector2(130f, 80f);
        glg.spacing = new Vector2(10f, 10f);
        glg.childAlignment = TextAnchor.UpperCenter;
        glg.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 4;

        // SpawnMenuUI
        SpawnMenuUI spawnMenu = canvas.AddComponent<SpawnMenuUI>();
        SerializedObject menuSO = new SerializedObject(spawnMenu);
        SetProp(menuSO, "menuPanel", menuPanel);
        SetProp(menuSO, "buttonContainer", grid.transform);

        var xrOrigin = Object.FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (xrOrigin != null)
        {
            SetProp(menuSO, "objectSpawner", xrOrigin.GetComponent<ObjectSpawner>());
        }
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

        // Add TrackedDevice Graphic Raycaster for VR UI interaction
        // (requires XRI's TrackedDeviceGraphicRaycaster)
        var existingRaycaster = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
        if (existingRaycaster != null)
            Object.DestroyImmediate(existingRaycaster);
        canvas.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();

        Debug.Log("[Pokemon Restaurant] VR Spawn Menu built (World Space Canvas). Parented to scene root - you can reparent to Left Controller for wrist menu.");
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
