using UnityEngine;
using TMPro;

public class HUDController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject crosshair;
    [SerializeField] private TMP_Text interactText;       // "E - Ouvrir la porte"
    [SerializeField] private TMP_Text selectedObjectText;  // "Objet: Table"
    [SerializeField] private TMP_Text hintText;            // "Tab - Menu de spawn"

    [Header("References")]
    [SerializeField] private CameraController cameraController;
    [SerializeField] private ObjectSpawner objectSpawner;
    [SerializeField] private ObjectManipulator objectManipulator;

    private void Update()
    {
        UpdateInteractText();
        UpdateSelectedText();
        UpdateCrosshairVisibility();
    }

    private void UpdateCrosshairVisibility()
    {
        if (crosshair != null)
        {
            bool menuOpen = GameManager.Instance != null && GameManager.Instance.IsMenuOpen;
            crosshair.SetActive(!menuOpen);
        }
    }

    private void UpdateInteractText()
    {
        if (interactText == null) return;

        if (cameraController != null && cameraController.HasTarget)
        {
            GameObject target = cameraController.CurrentTarget;

            if (target.GetComponent<DoorController>() != null)
            {
                interactText.text = "E - Ouvrir/Fermer la porte";
                interactText.gameObject.SetActive(true);
                return;
            }

            if (target.GetComponent<LightSwitchController>() != null)
            {
                interactText.text = "E - Allumer/Eteindre la lumiere";
                interactText.gameObject.SetActive(true);
                return;
            }
        }

        // Check if hovering a spawned object
        if (objectManipulator != null && objectManipulator.HoveredObject != null)
        {
            interactText.text = "Clic - Deplacer  |  R - Tourner  |  X - Supprimer";
            interactText.gameObject.SetActive(true);
            return;
        }

        interactText.gameObject.SetActive(false);
    }

    private void UpdateSelectedText()
    {
        if (selectedObjectText == null || objectSpawner == null) return;

        SpawnableItem[] items = objectSpawner.SpawnableItems;
        if (items.Length > 0)
        {
            selectedObjectText.text = $"Objet: {items[objectSpawner.SelectedIndex].name}  (F - Placer)";
        }
    }
}
