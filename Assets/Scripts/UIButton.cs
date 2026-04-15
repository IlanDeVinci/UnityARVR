using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// VR-compatible button with hover/press visual feedback.
/// Works with TrackedDeviceGraphicRaycaster (ray-based) and
/// XRPokeInteractor (finger-poke) via standard EventSystem interfaces.
///
/// Setup: Add to any UI Button child that has an Image component.
/// The parent Canvas must have a TrackedDeviceGraphicRaycaster (added
/// automatically by VRWristMenu, or add it manually).
/// </summary>
/// 
[RequireComponent(typeof(Button))]
public class UIButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Visual")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor  = new Color(0.85f, 0.95f, 1f);
    [SerializeField] private Color pressColor  = new Color(0.5f, 0.75f, 1f);
    [SerializeField] private float pressScaleFactor = 0.93f;


    [Header("Audio")]
    [SerializeField] private AudioClip hoverClip;
    [SerializeField] private AudioClip clickClip;

    private Image _image;
    private Vector3 _baseScale;
    private AudioSource _audio;

    private void Awake()
    {
        _image     = GetComponent<Image>();
        _baseScale = transform.localScale;
        _audio     = GetComponentInParent<AudioSource>();
    }

    public void OnClick() => Debug.Log($"[UIButton] {name} clicked");

    public void OnPointerEnter(PointerEventData e)
    {
        if (_image != null) _image.color = hoverColor;
        Play(hoverClip);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (_image != null) _image.color = normalColor;
        transform.localScale = _baseScale;
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (_image != null) _image.color = pressColor;
        transform.localScale = _baseScale * pressScaleFactor;
        Play(clickClip);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (_image != null) _image.color = hoverColor;
        transform.localScale = _baseScale;
    }

    private void Play(AudioClip clip)
    {
        if (_audio != null && clip != null)
            _audio.PlayOneShot(clip);
    }
}
