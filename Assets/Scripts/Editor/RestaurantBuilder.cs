using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Editor tool: builds the entire Pokemon Restaurant scene in one click.
/// Menu: Pokemon Restaurant > Build All
/// </summary>
public class RestaurantBuilder : EditorWindow
{
    [MenuItem("Pokemon Restaurant/1 - Build Entire Scene")]
    public static void BuildAll()
    {
        BuildRoom();
        BuildPlayer();
        BuildGameManager();
        BuildUI();
        Debug.Log("[Pokemon Restaurant] Scene built successfully! Assign InputActionReferences in the Inspector.");
    }

    [MenuItem("Pokemon Restaurant/2 - Build Room Only")]
    public static void BuildRoom()
    {
        // Clean up existing room
        GameObject existing = GameObject.Find("Restaurant");
        if (existing != null) Object.DestroyImmediate(existing);

        GameObject restaurant = new GameObject("Restaurant");

        // Floor
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.parent = restaurant.transform;
        floor.transform.localScale = new Vector3(2f, 1f, 2f);
        floor.transform.position = Vector3.zero;
        floor.layer = LayerMask.NameToLayer("Ground");

        // Ceiling
        GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ceiling.name = "Ceiling";
        ceiling.transform.parent = restaurant.transform;
        ceiling.transform.localScale = new Vector3(2f, 1f, 2f);
        ceiling.transform.position = new Vector3(0f, 4f, 0f);
        ceiling.transform.rotation = Quaternion.Euler(180f, 0f, 0f);

        // Walls
        CreateWall("Wall_North", restaurant.transform, new Vector3(0f, 2f, 10f), new Vector3(20f, 4f, 0.2f));
        CreateWall("Wall_South", restaurant.transform, new Vector3(0f, 2f, -10f), new Vector3(20f, 4f, 0.2f));
        CreateWall("Wall_East", restaurant.transform, new Vector3(10f, 2f, 0f), new Vector3(0.2f, 4f, 20f));

        // West wall with door gap
        CreateWall("Wall_West_Top", restaurant.transform, new Vector3(-10f, 3.25f, 0f), new Vector3(0.2f, 1.5f, 20f));
        CreateWall("Wall_West_Left", restaurant.transform, new Vector3(-10f, 1.25f, 5f), new Vector3(0.2f, 2.5f, 10f));
        CreateWall("Wall_West_Right", restaurant.transform, new Vector3(-10f, 1.25f, -5f), new Vector3(0.2f, 2.5f, 10f));

        // Door (simple cube acting as door)
        GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
        door.name = "Door";
        door.transform.parent = restaurant.transform;
        door.transform.position = new Vector3(-10f, 1.25f, 0.5f);
        door.transform.localScale = new Vector3(0.1f, 2.5f, 1.0f);
        door.layer = LayerMask.NameToLayer("Interactable");
        door.AddComponent<DoorController>();

        // Light switch (small cube on wall)
        GameObject lightSwitch = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lightSwitch.name = "LightSwitch";
        lightSwitch.transform.parent = restaurant.transform;
        lightSwitch.transform.position = new Vector3(-9.85f, 1.5f, -1.5f);
        lightSwitch.transform.localScale = new Vector3(0.1f, 0.15f, 0.1f);
        lightSwitch.layer = LayerMask.NameToLayer("Interactable");
        LightSwitchController switchCtrl = lightSwitch.AddComponent<LightSwitchController>();

        // Lights (4 point lights at ceiling + 1 spot for counter area)
        Light[] roomLights = new Light[5];
        Vector3[] lightPositions = {
            new Vector3(-5f, 3.8f, -5f),
            new Vector3(5f, 3.8f, -5f),
            new Vector3(-5f, 3.8f, 5f),
            new Vector3(5f, 3.8f, 5f),
            new Vector3(0f, 3.8f, 0f)
        };

        for (int i = 0; i < 5; i++)
        {
            GameObject lightObj = new GameObject($"RoomLight_{i}");
            lightObj.transform.parent = restaurant.transform;
            lightObj.transform.position = lightPositions[i];
            Light light = lightObj.AddComponent<Light>();
            light.type = (i < 4) ? LightType.Point : LightType.Spot;
            light.intensity = 1.5f;
            light.range = 12f;
            light.color = new Color(1f, 0.95f, 0.85f); // warm light
            light.enabled = false; // off by default, switch turns them on
            roomLights[i] = light;
        }

        // Assign lights to switch controller via SerializedObject
        SerializedObject so = new SerializedObject(switchCtrl);
        SerializedProperty lightsProp = so.FindProperty("lights");
        lightsProp.arraySize = roomLights.Length;
        for (int i = 0; i < roomLights.Length; i++)
        {
            lightsProp.GetArrayElementAtIndex(i).objectReferenceValue = roomLights[i];
        }
        so.ApplyModifiedProperties();

        // Ambient directional light (always on, dim)
        GameObject ambientLight = GameObject.Find("Directional Light");
        if (ambientLight == null)
        {
            ambientLight = new GameObject("Directional Light");
            Light dirLight = ambientLight.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.intensity = 0.3f;
            dirLight.color = new Color(0.8f, 0.85f, 1f);
            ambientLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // Counter/bar area (simple cube)
        GameObject counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
        counter.name = "Counter";
        counter.transform.parent = restaurant.transform;
        counter.transform.position = new Vector3(5f, 0.5f, 7f);
        counter.transform.localScale = new Vector3(6f, 1f, 1.5f);

        Debug.Log("[Pokemon Restaurant] Room built: floor, walls, door, lights, switch, counter.");
    }

    [MenuItem("Pokemon Restaurant/3 - Build Player Only")]
    public static void BuildPlayer()
    {
        GameObject existing = GameObject.Find("Player");
        if (existing != null) Object.DestroyImmediate(existing);

        // Player
        GameObject player = new GameObject("Player");
        player.transform.position = new Vector3(-8f, 1f, 0f); // near door entrance

        CharacterController cc = player.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.5f;
        cc.center = new Vector3(0f, 0f, 0f);

        player.AddComponent<PlayerController>();
        player.AddComponent<PlayerInteraction>();
        player.AddComponent<ObjectSpawner>();
        player.AddComponent<ObjectManipulator>();

        // Camera
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            mainCam = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
        }

        mainCam.transform.parent = player.transform;
        mainCam.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        mainCam.transform.localRotation = Quaternion.identity;

        CameraController camCtrl = mainCam.gameObject.GetComponent<CameraController>();
        if (camCtrl == null)
            camCtrl = mainCam.gameObject.AddComponent<CameraController>();

        // Wire CameraController reference in PlayerInteraction
        PlayerInteraction interaction = player.GetComponent<PlayerInteraction>();
        SerializedObject so = new SerializedObject(interaction);
        SerializedProperty camProp = so.FindProperty("cameraController");
        if (camProp != null)
        {
            camProp.objectReferenceValue = camCtrl;
            so.ApplyModifiedProperties();
        }

        // Wire CameraController in HUDController if exists
        // (will be done when UI is built)

        Debug.Log("[Pokemon Restaurant] Player built with camera, controllers, spawner, manipulator.");
    }

    [MenuItem("Pokemon Restaurant/4 - Build Game Manager Only")]
    public static void BuildGameManager()
    {
        GameObject existing = GameObject.Find("GameManager");
        if (existing != null) Object.DestroyImmediate(existing);

        GameObject gm = new GameObject("GameManager");
        gm.AddComponent<GameManager>();
        gm.AddComponent<SceneResetManager>();
        gm.AddComponent<AudioManager>();

        // Wire references in GameManager
        GameManager gmComp = gm.GetComponent<GameManager>();
        SerializedObject so = new SerializedObject(gmComp);

        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            SerializedProperty playerProp = so.FindProperty("player");
            if (playerProp != null)
                playerProp.objectReferenceValue = player.GetComponent<PlayerController>();

            SerializedProperty camProp = so.FindProperty("cameraController");
            Camera mainCam = Camera.main;
            if (camProp != null && mainCam != null)
                camProp.objectReferenceValue = mainCam.GetComponent<CameraController>();

            SerializedProperty spawnerProp = so.FindProperty("objectSpawner");
            if (spawnerProp != null)
                spawnerProp.objectReferenceValue = player.GetComponent<ObjectSpawner>();
        }

        so.ApplyModifiedProperties();

        Debug.Log("[Pokemon Restaurant] GameManager built with AudioManager and SceneResetManager.");
    }

    [MenuItem("Pokemon Restaurant/5 - Build UI Only")]
    public static void BuildUI()
    {
        // Clean up
        GameObject existingCanvas = GameObject.Find("GameCanvas");
        if (existingCanvas != null) Object.DestroyImmediate(existingCanvas);

        // Canvas
        GameObject canvas = new GameObject("GameCanvas");
        Canvas c = canvas.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 10;
        canvas.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Event System (if missing)
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // --- HUD ---
        GameObject hud = new GameObject("HUD");
        hud.transform.SetParent(canvas.transform, false);

        // Crosshair
        GameObject crosshair = new GameObject("Crosshair", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        crosshair.transform.SetParent(hud.transform, false);
        RectTransform crossRT = crosshair.GetComponent<RectTransform>();
        crossRT.sizeDelta = new Vector2(8f, 8f);
        crossRT.anchoredPosition = Vector2.zero;
        crossRT.anchorMin = new Vector2(0.5f, 0.5f);
        crossRT.anchorMax = new Vector2(0.5f, 0.5f);
        UnityEngine.UI.Image crossImg = crosshair.GetComponent<UnityEngine.UI.Image>();
        crossImg.color = Color.white;

        // Interact text (bottom center)
        GameObject interactText = CreateTMPText("InteractText", hud.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(500f, 40f),
            "", 18, TextAlignmentOptions.Center);

        // Selected object text (bottom left)
        GameObject selectedText = CreateTMPText("SelectedObjectText", hud.transform,
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 20f), new Vector2(400f, 30f),
            "Objet: Table (F - Placer)", 16, TextAlignmentOptions.Left);

        // Hint text (top center)
        GameObject hintText = CreateTMPText("HintText", hud.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(400f, 30f),
            "Tab - Menu de spawn | E - Interagir", 14, TextAlignmentOptions.Center);

        // Add HUDController
        HUDController hudCtrl = hud.AddComponent<HUDController>();
        SerializedObject hudSO = new SerializedObject(hudCtrl);
        SetProp(hudSO, "crosshair", crosshair);
        SetProp(hudSO, "interactText", interactText.GetComponent<TMPro.TMP_Text>());
        SetProp(hudSO, "selectedObjectText", selectedText.GetComponent<TMPro.TMP_Text>());
        SetProp(hudSO, "hintText", hintText.GetComponent<TMPro.TMP_Text>());

        // Wire references
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

        // --- Spawn Menu ---
        GameObject menuPanel = new GameObject("SpawnMenuPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        menuPanel.transform.SetParent(canvas.transform, false);
        RectTransform menuRT = menuPanel.GetComponent<RectTransform>();
        menuRT.anchorMin = new Vector2(0.2f, 0.2f);
        menuRT.anchorMax = new Vector2(0.8f, 0.8f);
        menuRT.offsetMin = Vector2.zero;
        menuRT.offsetMax = Vector2.zero;
        UnityEngine.UI.Image menuBg = menuPanel.GetComponent<UnityEngine.UI.Image>();
        menuBg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

        // Title
        CreateTMPText("MenuTitle", menuPanel.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(300f, 40f),
            "MENU SPAWN - Restaurant Pokemon", 22, TextAlignmentOptions.Center);

        // Grid layout for buttons
        GameObject grid = new GameObject("ButtonGrid", typeof(RectTransform));
        grid.transform.SetParent(menuPanel.transform, false);
        RectTransform gridRT = grid.GetComponent<RectTransform>();
        gridRT.anchorMin = new Vector2(0.05f, 0.1f);
        gridRT.anchorMax = new Vector2(0.95f, 0.85f);
        gridRT.offsetMin = Vector2.zero;
        gridRT.offsetMax = Vector2.zero;
        UnityEngine.UI.GridLayoutGroup glg = grid.AddComponent<UnityEngine.UI.GridLayoutGroup>();
        glg.cellSize = new Vector2(120f, 80f);
        glg.spacing = new Vector2(15f, 15f);
        glg.childAlignment = TextAnchor.MiddleCenter;

        // SpawnMenuUI component
        SpawnMenuUI spawnMenu = canvas.AddComponent<SpawnMenuUI>();
        SerializedObject menuSO = new SerializedObject(spawnMenu);
        SetProp(menuSO, "menuPanel", menuPanel);
        SetProp(menuSO, "buttonContainer", grid.transform);
        if (player != null)
            SetProp(menuSO, "objectSpawner", player.GetComponent<ObjectSpawner>());
        menuSO.ApplyModifiedProperties();

        // Wire SpawnMenuUI in GameManager
        GameObject gmObj = GameObject.Find("GameManager");
        if (gmObj != null)
        {
            GameManager gm = gmObj.GetComponent<GameManager>();
            SerializedObject gmSO = new SerializedObject(gm);
            SetProp(gmSO, "spawnMenuUI", spawnMenu);
            gmSO.ApplyModifiedProperties();
        }

        menuPanel.SetActive(false);

        Debug.Log("[Pokemon Restaurant] UI built: HUD (crosshair, texts) + Spawn Menu panel.");
    }

    // --- Helpers ---

    private static void CreateWall(string name, Transform parent, Vector3 pos, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.parent = parent;
        wall.transform.position = pos;
        wall.transform.localScale = scale;
    }

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

        TMPro.TextMeshProUGUI tmp = obj.AddComponent<TMPro.TextMeshProUGUI>();
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
