using UnityEngine;

/// <summary>
/// Lightweight marker on the XR Origin root.
/// Movement is handled by XR Interaction Toolkit locomotion providers
/// (Continuous Move, Snap Turn, Teleportation) configured on the XR Origin.
/// GameManager uses this to find the player.
/// </summary>
public class PlayerController : MonoBehaviour
{
    public Vector3 Position => transform.position;
}
