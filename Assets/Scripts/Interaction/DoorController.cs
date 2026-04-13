using UnityEngine;

public class DoorController : MonoBehaviour
{
    [Header("Door Settings")]
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float openSpeed = 3f;
    [SerializeField] private Transform pivot; // assign door pivot point

    [Header("Audio")]
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;

    private AudioSource audioSource;
    private bool isOpen;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    private float t;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        Transform target = pivot != null ? pivot : transform;
        closedRotation = target.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
    }

    private void Update()
    {
        Transform target = pivot != null ? pivot : transform;
        float targetT = isOpen ? 1f : 0f;
        t = Mathf.MoveTowards(t, targetT, openSpeed * Time.deltaTime);
        target.localRotation = Quaternion.Slerp(closedRotation, openRotation, t);
    }

    /// <summary>
    /// Called by the player interaction system when clicking the door.
    /// </summary>
    public void ToggleDoor()
    {
        isOpen = !isOpen;
        AudioClip clip = isOpen ? openSound : closeSound;
        if (clip != null)
            audioSource.PlayOneShot(clip);
    }
}
