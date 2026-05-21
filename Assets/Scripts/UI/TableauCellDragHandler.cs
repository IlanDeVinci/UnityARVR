using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attaché à chaque vignette de l'onglet TABLEAUX. Capte la pression et le
/// relâchement de la gâchette VR pour piloter le drag-and-drop géré par
/// PaintMenuUI.
///
/// Implémente aussi IDragHandler / IEndDragHandler pour que le relâchement
/// soit reçu même si le contrôleur a quitté la vignette pendant le drag
/// (cas typique : l'utilisateur vise un mur loin du menu).
/// </summary>
public class TableauCellDragHandler : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler,
    IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private PaintMenuUI menu;
    private int cellIndex;

    public void Init(PaintMenuUI owner, int index)
    {
        menu = owner;
        cellIndex = index;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (menu != null) menu.BeginTableauDrag(cellIndex, eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (menu != null) menu.EndTableauDrag(cellIndex, eventData);
    }

    // Annule le seuil de pixels pour que IBeginDrag soit immédiat.
    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        eventData.useDragThreshold = false;
    }

    public void OnBeginDrag(PointerEventData eventData) { /* placeholder pour activer la chaîne drag */ }
    public void OnDrag(PointerEventData eventData) { /* tracking fait dans PaintMenuUI.Update */ }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (menu != null) menu.EndTableauDrag(cellIndex, eventData);
    }
}
