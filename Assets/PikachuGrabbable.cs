using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(PikachuWander))]
public class PikachuGrabbable : MonoBehaviour
{
    private XRGrabInteractable grab;
    private PikachuWander wander;
    private Rigidbody rb;
    private Animator animator;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        wander = GetComponent<PikachuWander>();
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        // Kinematic = XR déplace l'objet directement, pas de physique résiduelle
        grab.movementType = XRBaseInteractable.MovementType.Kinematic;
        grab.throwOnDetach = false;

        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);
    }

    void OnDestroy()
    {
        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnReleased);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        // 1. Couper l'IA
        wander.enabled = false;
        wander.StopAllCoroutines();

        // 2. Geler le Rigidbody
        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // 3. Couper l'Animator (stop root motion + animations de marche)
        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.enabled = false;
        }
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        // Remettre la physique pour qu'il retombe
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Remettre l'Animator
        if (animator != null)
            animator.enabled = true;

        StartCoroutine(ResumeAfterLanding());
    }

    private System.Collections.IEnumerator ResumeAfterLanding()
    {
        // Attendre qu'il touche le sol
        yield return new WaitForSeconds(0.1f);

        while (!IsGrounded())
        {
            yield return new WaitForFixedUpdate();
        }

        // Atterri : reprendre l'IA depuis cette position
        wander.centerPoint = transform.position;
        wander.enabled = true;
    }

    private bool IsGrounded()
    {
        return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.3f);
    }
}
