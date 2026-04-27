using UnityEngine;
using System.Collections.Generic;

public class TeleportPoint : MonoBehaviour
{
    [Header("Teleport Pair")]
    public Transform targetPoint; // where player will be teleported

    [Header("Settings")]
    public float cooldown = 0.5f;
    public float yOffset = 0.2f;
    public bool useManualOverlapCheck = true;

    private bool canTeleport = true;
    private Collider triggerCollider;
    private readonly Collider[] overlapResults = new Collider[8];

    // Tracks the last teleport time for each player instance.
    private static readonly Dictionary<int, float> lastTeleportByPlayer = new Dictionary<int, float>();

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider == null) return;

        if (!triggerCollider.isTrigger)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void Update()
    {
        if (!useManualOverlapCheck || triggerCollider == null) return;

        Bounds b = triggerCollider.bounds;
        int hitCount = Physics.OverlapBoxNonAlloc(
            b.center,
            b.extents,
            overlapResults,
            transform.rotation,
            ~0,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider c = overlapResults[i];
            if (c == null || c == triggerCollider) continue;
            TryTeleport(c);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryTeleport(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // Helps when player starts already touching the trigger.
        TryTeleport(other);
    }

    void TryTeleport(Collider other)
    {
        if (!canTeleport) return;

        Transform playerRoot = ResolvePlayerRoot(other);
        if (playerRoot == null) return;

        int playerId = playerRoot.gameObject.GetInstanceID();
        if (lastTeleportByPlayer.TryGetValue(playerId, out float lastTime) && Time.time - lastTime < cooldown) return;

        Teleport(playerRoot);
    }

    Transform ResolvePlayerRoot(Collider other)
    {
        if (other.CompareTag("Player")) return other.transform;

        Transform root = other.transform.root;
        if (root != null && root.CompareTag("Player")) return root;

        CharacterController ccInParent = other.GetComponentInParent<CharacterController>();
        if (ccInParent != null) return ccInParent.transform;

        return null;
    }

    void Teleport(Transform playerRoot)
    {
        if (targetPoint == null) return;

        // Move player
        CharacterController cc = playerRoot.GetComponent<CharacterController>();
        if (cc == null)
        {
            cc = playerRoot.GetComponentInChildren<CharacterController>();
        }

        if (cc != null)
        {
            cc.enabled = false; // disable before moving
            playerRoot.position = targetPoint.position + Vector3.up * yOffset;
            cc.enabled = true;
        }
        else
        {
            playerRoot.position = targetPoint.position + Vector3.up * yOffset;
        }

        int playerId = playerRoot.gameObject.GetInstanceID();
        lastTeleportByPlayer[playerId] = Time.time;

        // Prevent instant back-teleport on this point and the destination point.
        StartCooldown();
        TeleportPoint destinationTeleport = targetPoint.GetComponent<TeleportPoint>();
        if (destinationTeleport != null)
        {
            destinationTeleport.StartCooldown();
        }
    }

    void StartCooldown()
    {
        StartCoroutine(TeleportCooldown());
    }

    System.Collections.IEnumerator TeleportCooldown()
    {
        canTeleport = false;
        yield return new WaitForSeconds(cooldown);
        canTeleport = true;
    }
}

public class RotateCircle : MonoBehaviour
{
    public float speed = 50f;

    void Update()
    {
        transform.Rotate(Vector3.up * speed * Time.deltaTime);
    }
}