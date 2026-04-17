using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections;

public class PikachuWander : MonoBehaviour
{
    [Header("Déplacement")]
    public float wanderSpeed = 2f;
    public float fleeSpeed = 5f;
    public float rotationSpeed = 5f;
    public float waitTimeMin = 1f;
    public float waitTimeMax = 3f;

    [Header("Zone de déplacement")]
    public float wanderRadius = 10f;
    public Vector3 centerPoint = Vector3.zero;

    [Header("Détection du joueur")]
    public float detectionRadius = 5f;
    public float safeDistance = 8f;

    [Header("Évitement")]
    public float wallDetectionRange = 2f;
    public float wallAvoidStrength = 3f;
    public float separationRadius = 2f;
    public float separationStrength = 2f;
    public int wallRayCount = 8;

    [Header("Réaction")]
    public AudioClip fleeSound;
    public AudioClip grabSound;
    public AudioClip throwSound;

    private enum State { Wandering, Fleeing }
    private State currentState = State.Wandering;

    private Vector3 targetPosition;
    private bool isWaiting = false;
    private Rigidbody rb;
    private Animator animator;
    private Transform player;
    private AudioSource audioSource;
    private bool hasPlayedFleeSound = false;
    private XRGrabInteractable grabInteractable;
    private bool isGrabbed = false;
    private bool isThrown = false;

    // Force de collision pour sortir des murs
    private Vector3 collisionPush = Vector3.zero;

    // Cache des autres Pikachus (partagé entre toutes les instances)
    private static PikachuWander[] allPikachus;
    private static float lastCacheTime = -1f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        grabInteractable = GetComponent<XRGrabInteractable>();
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);
        }

        FindPlayer();
        centerPoint = transform.position;
        PickNewTarget();
    }

    void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        isGrabbed = true;
        StopAllCoroutines();
        isWaiting = false;

        if (grabSound != null && audioSource != null)
            audioSource.PlayOneShot(grabSound);

        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.enabled = false;
        }
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        isGrabbed = false;
        isThrown = true;

        rb.useGravity = true;

        if (throwSound != null && audioSource != null)
            audioSource.PlayOneShot(throwSound);

        StartCoroutine(ResumeAfterLanding());
    }

    private IEnumerator ResumeAfterLanding()
    {
        // Laisser le lancer se faire sans interférence
        yield return new WaitForSeconds(0.1f);

        while (!IsGrounded())
            yield return new WaitForFixedUpdate();

        // Atterri : reprendre l'IA
        isThrown = false;

        if (animator != null)
            animator.enabled = true;

        centerPoint = transform.position;
        PickNewTarget();
    }

    void FindPlayer()
    {
        // Essayer par tag "Player"
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            return;
        }

        // Sinon, utiliser la caméra principale (souvent le casque VR)
        if (Camera.main != null)
        {
            player = Camera.main.transform;
        }
    }

    bool IsGrounded()
    {
        return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.3f);
    }

    void FixedUpdate()
    {
        // Si attrapé ou lancé, ne pas interférer avec la physique
        if (isGrabbed || isThrown)
            return;

        // Si pas au sol, ne rien faire (en l'air ou en train de tomber)
        if (!IsGrounded())
        {
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            if (animator != null)
                animator.SetFloat("Speed", 0f);
            return;
        }

        // Rafraîchir le cache des Pikachus toutes les secondes
        if (Time.time - lastCacheTime > 1f)
        {
            allPikachus = FindObjectsByType<PikachuWander>(FindObjectsSortMode.None);
            lastCacheTime = Time.time;
        }

        // Re-chercher le joueur s'il n'a pas été trouvé
        if (player == null)
            FindPlayer();

        // Déterminer l'état
        if (player != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);

            if (distanceToPlayer <= detectionRadius)
            {
                currentState = State.Fleeing;
            }
            else if (distanceToPlayer >= safeDistance)
            {
                if (currentState == State.Fleeing)
                {
                    hasPlayedFleeSound = false;
                    StartCoroutine(WaitAndPickNewTarget());
                }
                currentState = State.Wandering;
            }
        }
        else
        {
            currentState = State.Wandering;
        }

        // Agir
        switch (currentState)
        {
            case State.Wandering:
                HandleWander();
                break;
            case State.Fleeing:
                HandleFlee();
                break;
        }

        // Reset la force de collision après usage
        collisionPush = Vector3.zero;
    }

    // ─── COLLISIONS (sortir des murs) ────────────────────────────
    void OnCollisionStay(Collision collision)
    {
        // Accumuler une force pour se pousser hors des murs/obstacles
        foreach (ContactPoint contact in collision.contacts)
        {
            Vector3 push = contact.normal;
            push.y = 0;
            collisionPush += push;
        }
    }

    // ─── WANDERING ───────────────────────────────────────────────
    void HandleWander()
    {
        if (isWaiting)
        {
            // Même en pause, se pousser si coincé
            ApplyPushForces();
            return;
        }

        // Direction de base vers la cible
        Vector3 moveDir = (targetPosition - transform.position);
        moveDir.y = 0;

        if (moveDir.magnitude < 0.6f)
        {
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            StartCoroutine(WaitAndPickNewTarget());
            return;
        }

        moveDir.Normalize();

        // Combiner toutes les forces de steering
        Vector3 wallAvoid = GetWallAvoidanceForce();
        Vector3 separation = GetSeparationForce();
        Vector3 push = collisionPush.normalized;

        Vector3 finalDir = moveDir
            + wallAvoid * wallAvoidStrength
            + separation * separationStrength
            + push * wallAvoidStrength;
        finalDir.y = 0;
        finalDir.Normalize();

        ApplyMovement(finalDir, wanderSpeed);
    }

    // ─── FLEEING ─────────────────────────────────────────────────
    void HandleFlee()
    {
        isWaiting = false;

        if (!hasPlayedFleeSound && audioSource != null && fleeSound != null)
        {
            audioSource.PlayOneShot(fleeSound);
            hasPlayedFleeSound = true;
        }

        // Direction opposée au joueur
        Vector3 fleeDir = (transform.position - player.position);
        fleeDir.y = 0;
        fleeDir.Normalize();

        // Combiner avec évitement de murs et séparation
        Vector3 wallAvoid = GetWallAvoidanceForce();
        Vector3 separation = GetSeparationForce();
        Vector3 push = collisionPush.normalized;

        Vector3 finalDir = fleeDir
            + wallAvoid * (wallAvoidStrength * 2f)
            + separation * separationStrength
            + push * (wallAvoidStrength * 2f);
        finalDir.y = 0;
        finalDir.Normalize();

        ApplyMovement(finalDir, fleeSpeed);
    }

    // ─── STEERING FORCES ─────────────────────────────────────────

    /// Raycasts en éventail — détecte TOUS les colliders (pas besoin de layer)
    Vector3 GetWallAvoidanceForce()
    {
        Vector3 avoidance = Vector3.zero;
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        float angleStep = 360f / wallRayCount;

        for (int i = 0; i < wallRayCount; i++)
        {
            float angle = i * angleStep;
            Vector3 dir = Quaternion.Euler(0, angle, 0) * Vector3.forward;

            if (Physics.Raycast(origin, dir, out RaycastHit hit, wallDetectionRange))
            {
                // Ignorer les autres Pikachus (gérés par la séparation)
                if (hit.collider.GetComponent<PikachuWander>() != null) continue;
                // Ignorer le joueur (géré par la fuite)
                if (hit.collider.CompareTag("Player")) continue;

                float weight = 1f - (hit.distance / wallDetectionRange);
                avoidance -= dir * weight;
            }
        }

        avoidance.y = 0;
        return avoidance;
    }

    /// Force de séparation entre Pikachus
    Vector3 GetSeparationForce()
    {
        Vector3 separation = Vector3.zero;

        if (allPikachus == null) return separation;

        foreach (PikachuWander other in allPikachus)
        {
            if (other == this || other == null) continue;

            Vector3 offset = transform.position - other.transform.position;
            offset.y = 0;
            float dist = offset.magnitude;

            if (dist < separationRadius && dist > 0.01f)
            {
                separation += offset.normalized * (1f - dist / separationRadius);
            }
        }

        return separation;
    }

    // ─── HELPERS ─────────────────────────────────────────────────

    void ApplyPushForces()
    {
        Vector3 separation = GetSeparationForce();
        Vector3 push = collisionPush.normalized;
        Vector3 force = separation * separationStrength + push * wallAvoidStrength;
        force.y = 0;

        if (force.sqrMagnitude > 0.01f)
        {
            Vector3 v = force.normalized * wanderSpeed * 0.5f;
            v.y = rb.linearVelocity.y;
            rb.linearVelocity = v;
        }
    }

    void ApplyMovement(Vector3 direction, float speed)
    {
        Vector3 newVelocity = direction * speed;
        newVelocity.y = rb.linearVelocity.y;
        rb.linearVelocity = newVelocity;

        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime
            );
        }

        if (animator != null)
            animator.SetFloat("Speed", speed);
    }

    void PickNewTarget()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * wanderRadius;
            Vector3 candidate = centerPoint + new Vector3(randomCircle.x, 0, randomCircle.y);

            // Vérifier qu'il n'y a pas de mur au point d'arrivée
            if (!Physics.CheckSphere(candidate, 0.5f))
            {
                // Vérifier aussi qu'on peut y aller en ligne droite
                Vector3 dir = candidate - transform.position;
                if (!Physics.Raycast(transform.position + Vector3.up * 0.5f, dir.normalized, dir.magnitude))
                {
                    targetPosition = candidate;
                    return;
                }
            }
        }
        // Fallback : rester au centre
        targetPosition = centerPoint;
    }

    IEnumerator WaitAndPickNewTarget()
    {
        isWaiting = true;

        if (animator != null)
            animator.SetFloat("Speed", 0f);

        yield return new WaitForSeconds(Random.Range(waitTimeMin, waitTimeMax));

        PickNewTarget();
        isWaiting = false;
    }

    // ─── GIZMOS ──────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, safeDistance);

        Gizmos.color = Color.yellow;
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        float angleStep = 360f / wallRayCount;
        for (int i = 0; i < wallRayCount; i++)
        {
            Vector3 dir = Quaternion.Euler(0, i * angleStep, 0) * Vector3.forward;
            Gizmos.DrawRay(origin, dir * wallDetectionRange);
        }

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, separationRadius);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(centerPoint, wanderRadius);
    }
}
