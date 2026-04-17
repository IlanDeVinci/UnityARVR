using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

/// <summary>
/// Joue des sons de pas quand le joueur se déplace en VR (continuous move).
/// Attacher sur le XR Origin.
/// </summary>
public class FootstepPlayer : MonoBehaviour
{
    [Header("Sons de pas")]
    [SerializeField] private AudioClip[] footstepClips;

    [Header("Réglages")]
    [SerializeField] private float stepInterval = 0.45f;
    [SerializeField] [Range(0f, 1f)] private float volume = 0.8f;

    private AudioSource audioSource;
    private CharacterController characterController;
    private float stepTimer;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f; // 2D car c'est le joueur lui-même
        audioSource.volume = volume;
        audioSource.playOnAwake = false;

        characterController = GetComponentInChildren<CharacterController>();
    }

    private void Update()
    {
        if (!IsMoving())
        {
            stepTimer = 0f;
            return;
        }

        stepTimer += Time.deltaTime;
        if (stepTimer >= stepInterval)
        {
            stepTimer = 0f;
            PlayRandomFootstep();
        }
    }

    private bool IsMoving()
    {
        if (characterController != null)
            return characterController.velocity.sqrMagnitude > 0.5f;

        // Fallback : vérifier le déplacement du transform
        return false;
    }

    private void PlayRandomFootstep()
    {
        if (footstepClips == null || footstepClips.Length == 0) return;
        var clip = footstepClips[Random.Range(0, footstepClips.Length)];
        audioSource.PlayOneShot(clip, volume);
    }
}
