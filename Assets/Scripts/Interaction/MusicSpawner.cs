using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Attachment;

/// <summary>
/// Charge des AudioClip depuis Resources/Music/ et spawne une boombox 3D
/// grabable devant le joueur, jouant la piste sélectionnée en boucle avec
/// son spatial (audio 3D).
/// </summary>
public class MusicSpawner : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private float spawnDistance = 1.5f;
    [SerializeField] private float spawnHeight = 1.2f;

    [Header("Boombox visuel")]
    [SerializeField] private Vector3 boomboxSize = new Vector3(0.35f, 0.20f, 0.18f);
    [SerializeField] private Color boomboxColor = new Color(0.10f, 0.10f, 0.12f);
    [SerializeField] private Color speakerColor = new Color(0.50f, 0.35f, 0.20f);

    [Header("Audio 3D")]
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float maxDistance = 18f;
    [SerializeField, Range(0f, 1f)] private float volume = 0.9f;

    private AudioClip[] tracks;
    private int trackIndex;
    private Transform boomboxesParent;

    public AudioClip[] Tracks => tracks;
    public int TrackIndex => trackIndex;

    private void Awake()
    {
        LoadTracks();
        var existing = GameObject.Find("Boomboxes");
        boomboxesParent = existing != null ? existing.transform : new GameObject("Boomboxes").transform;
    }

    private void LoadTracks()
    {
        tracks = Resources.LoadAll<AudioClip>("Music");
        System.Array.Sort(tracks, (a, b) => string.Compare(a.name, b.name));
        Debug.Log($"[MusicSpawner] {tracks.Length} piste(s) chargée(s) depuis Resources/Music/");
    }

    public void SetTrackIndex(int index)
    {
        if (tracks == null || tracks.Length == 0) return;
        trackIndex = ((index % tracks.Length) + tracks.Length) % tracks.Length;
        Debug.Log($"[MusicSpawner] Piste : {tracks[trackIndex].name}");
    }

    /// <summary>Spawn la boombox avec la piste courante devant le joueur.</summary>
    public void SpawnSelected()
    {
        if (tracks == null || tracks.Length == 0)
        {
            Debug.LogWarning("[MusicSpawner] Aucune piste dans Resources/Music/");
            return;
        }
        SpawnTrack(trackIndex);
    }

    public void SpawnTrack(int index)
    {
        if (tracks == null || tracks.Length == 0) return;
        if (index < 0 || index >= tracks.Length) return;

        Vector3 pos = GetSpawnPosition();
        GameObject boombox = BuildBoombox(tracks[index].name);
        boombox.transform.SetParent(boomboxesParent, true);
        boombox.transform.position = pos;
        boombox.transform.rotation = Quaternion.Euler(0f, FacingYaw(), 0f);

        var src = boombox.AddComponent<AudioSource>();
        src.clip = tracks[index];
        src.loop = true;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = minDistance;
        src.maxDistance = maxDistance;
        src.volume = volume;
        src.playOnAwake = true;
        src.Play();

        // Grabable
        var rb = boombox.AddComponent<Rigidbody>();
        rb.mass = 1.5f;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // BoxCollider unique englobant — sur le root
        var col = boombox.AddComponent<BoxCollider>();
        col.size = boomboxSize;
        col.center = Vector3.zero;

        var grab = boombox.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        grab.throwOnDetach = true;
        grab.useDynamicAttach = true;
        grab.farAttachMode = InteractableFarAttachMode.Near;
        grab.smoothPosition = true;
        grab.smoothRotation = true;
    }

    private GameObject BuildBoombox(string trackName)
    {
        var root = new GameObject($"Boombox_{trackName}");

        // Corps
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        Destroy(body.GetComponent<BoxCollider>());
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = boomboxSize;
        body.GetComponent<MeshRenderer>().sharedMaterial = MakeUrpLit(boomboxColor);

        // Speakers (2 cônes/cylindres sur la face avant)
        float spkRadius = boomboxSize.y * 0.30f;
        float zFront = boomboxSize.z * 0.5f + 0.001f;
        float xOffset = boomboxSize.x * 0.25f;

        for (int i = 0; i < 2; i++)
        {
            var spk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spk.name = $"Speaker_{i}";
            Destroy(spk.GetComponent<Collider>());
            spk.transform.SetParent(root.transform, false);
            spk.transform.localScale = new Vector3(spkRadius * 2f, 0.01f, spkRadius * 2f);
            spk.transform.localPosition = new Vector3(i == 0 ? -xOffset : xOffset, 0f, zFront);
            spk.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            spk.GetComponent<MeshRenderer>().sharedMaterial = MakeUrpLit(speakerColor);
        }

        // Antenne
        var ant = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ant.name = "Antenna";
        Destroy(ant.GetComponent<Collider>());
        ant.transform.SetParent(root.transform, false);
        ant.transform.localScale = new Vector3(0.008f, 0.10f, 0.008f);
        ant.transform.localPosition = new Vector3(boomboxSize.x * 0.4f, boomboxSize.y * 0.55f, -boomboxSize.z * 0.3f);
        ant.transform.localRotation = Quaternion.Euler(15f, 0f, -10f);
        ant.GetComponent<MeshRenderer>().sharedMaterial = MakeUrpLit(new Color(0.4f, 0.4f, 0.4f));

        return root;
    }

    private static Material MakeUrpLit(Color c)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(shader) { color = c };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        return m;
    }

    private Vector3 GetSpawnPosition()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 fwd = cam.transform.forward;
            fwd.y = 0f; fwd.Normalize();
            return cam.transform.position + fwd * spawnDistance
                 - new Vector3(0f, cam.transform.position.y - spawnHeight, 0f);
        }
        return transform.position + Vector3.forward * spawnDistance + Vector3.up * spawnHeight;
    }

    private float FacingYaw()
    {
        Camera cam = Camera.main;
        if (cam == null) return 0f;
        // La boombox regarde vers le joueur (rotation 180° par rapport à son forward)
        Vector3 toCam = cam.transform.position - GetSpawnPosition();
        toCam.y = 0f;
        if (toCam.sqrMagnitude < 0.001f) return 0f;
        return Mathf.Atan2(toCam.x, toCam.z) * Mathf.Rad2Deg;
    }

    public void Reload() => LoadTracks();
}
