using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class SpawnMenuUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private ObjectSpawner objectSpawner;
    [SerializeField] private Transform buttonContainer;
    [SerializeField] private GameObject buttonPrefab; // Button with Image + Text

    [Header("Input")]
    [SerializeField] private InputActionReference menuToggleAction;

    [Header("Visual")]
    [SerializeField] private Color selectedColor = new Color(1f, 0.8f, 0.2f);  // Pokemon yellow
    [SerializeField] private Color normalColor = Color.white;

    private Button[] buttons;

    private void OnEnable()
    {
        menuToggleAction.action.Enable();
        menuToggleAction.action.performed += OnToggleMenu;
    }

    private void OnDisable()
    {
        menuToggleAction.action.performed -= OnToggleMenu;
        menuToggleAction.action.Disable();
    }

    private void Start()
    {
        menuPanel.SetActive(false);
        BuildMenu();
    }

    private void BuildMenu()
    {
        SpawnableItem[] items = objectSpawner.SpawnableItems;
        buttons = new Button[items.Length];

        for (int i = 0; i < items.Length; i++)
        {
            int index = i; // capture for closure
            GameObject btnObj;

            if (buttonPrefab != null)
            {
                btnObj = Instantiate(buttonPrefab, buttonContainer);
            }
            else
            {
                // Fallback: create basic button
                btnObj = new GameObject($"Btn_{items[i].name}", typeof(RectTransform), typeof(Button), typeof(Image));
                btnObj.transform.SetParent(buttonContainer, false);
            }

            // Set up text
            TMP_Text text = btnObj.GetComponentInChildren<TMP_Text>();
            if (text != null)
                text.text = items[i].name;

            // Set up icon if available
            Image icon = btnObj.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null && items[i].icon != null)
                icon.sprite = items[i].icon;

            Button btn = btnObj.GetComponent<Button>();
            btn.onClick.AddListener(() => SelectItem(index));
            buttons[i] = btn;
        }

        UpdateSelection();
    }

    private void SelectItem(int index)
    {
        objectSpawner.SetSelectedIndex(index);
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        if (buttons == null) return;
        for (int i = 0; i < buttons.Length; i++)
        {
            Image img = buttons[i].GetComponent<Image>();
            if (img != null)
                img.color = (i == objectSpawner.SelectedIndex) ? selectedColor : normalColor;
        }
    }

    private void OnToggleMenu(InputAction.CallbackContext ctx)
    {
        bool opening = !menuPanel.activeSelf;
        menuPanel.SetActive(opening);

        if (GameManager.Instance != null)
        {
            if (opening)
                GameManager.Instance.UnlockCursor();
            else
                GameManager.Instance.LockCursor();
        }
    }
}
