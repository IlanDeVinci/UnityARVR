using UnityEngine;
using TMPro;

/// <summary>
/// VR HUD - attach to a World Space Canvas that follows the player's view
/// or is fixed to the left wrist.
/// Shows what object is selected for spawning.
/// </summary>
public class HUDController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_Text selectedObjectText;
    [SerializeField] private TMP_Text hintText;

    [Header("References")]
    [SerializeField] private ObjectSpawner objectSpawner;

    private void Update()
    {
        UpdateSelectedText();
    }

    private void UpdateSelectedText()
    {
        if (selectedObjectText == null || objectSpawner == null) return;

        SpawnableItem[] items = objectSpawner.SpawnableItems;
        if (items != null && items.Length > 0)
        {
            selectedObjectText.text = $"Objet: {items[objectSpawner.SelectedIndex].name}";
        }
    }
}
