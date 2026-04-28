using UnityEngine;
using UnityEngine.AI;

public class EnemyMovement : MonoBehaviour
{
    public Transform player;

    [Header("Chase")]
    public float chaseSpeed = 3.5f;
    [SerializeField] private float destinationRefreshRate = 0.1f;
    [SerializeField] private float playerSampleRadius = 1.0f;
    [SerializeField] private float playerFallbackSampleRadius = 2.5f;
    [SerializeField] private float offNavMeshRecoveryDistance = 1.5f;

    [Header("Stuck Recovery")]
    [SerializeField] private float stuckVelocityThreshold = 0.05f;
    [SerializeField] private float stuckRemainingDistanceThreshold = 0.75f;
    [SerializeField] private float stuckTimeToRecover = 0.6f;

    [Header("Debug Gizmos")]
    [SerializeField] private bool drawDebugGizmos = true;
    [SerializeField] private float debugMarkerSize = 0.2f;

    private NavMeshAgent navMeshAgent;
    private NavMeshPath chasePath;
    private float refreshTimer;
    private float stuckTimer;
    private bool isChasingPlayer;
    private Vector3 lastRequestedDestination;

    private void Start()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        chasePath = new NavMeshPath();
        navMeshAgent.autoRepath = true;
        navMeshAgent.speed = Mathf.Max(0f, chaseSpeed);
    }

    private void Update()
    {
        if (player == null)
        {
            isChasingPlayer = false;
            return;
        }

        if (!EnsureAgentOnNavMesh())
        {
            isChasingPlayer = false;
            return;
        }

        refreshTimer -= Time.deltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = Mathf.Max(0.02f, destinationRefreshRate);
            SetDestination();
        }

        UpdateStuckRecovery();
    }

    private void OnValidate()
    {
        if (navMeshAgent == null) navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent != null)
        {
            navMeshAgent.speed = Mathf.Max(0f, chaseSpeed);
        }
    }

    private void SetDestination()
    {
        if (TryGetReachablePlayerPoint(out Vector3 point))
        {
            navMeshAgent.isStopped = false;
            navMeshAgent.SetDestination(point);
            lastRequestedDestination = point;
            isChasingPlayer = true;
            return;
        }

        isChasingPlayer = false;
        // player unreachable — EnemyWander handles movement independently
    }

    private bool TryGetReachablePlayerPoint(out Vector3 point)
    {
        point = player.position;

        if (navMeshAgent.CalculatePath(player.position, chasePath) &&
            chasePath.status == NavMeshPathStatus.PathComplete &&
            chasePath.corners.Length > 0) return true;

        if (NavMesh.SamplePosition(player.position, out NavMeshHit hit, playerSampleRadius, NavMesh.AllAreas) &&
            navMeshAgent.CalculatePath(hit.position, chasePath) &&
            chasePath.status == NavMeshPathStatus.PathComplete &&
            chasePath.corners.Length > 0)
        {
            point = hit.position;
            return true;
        }

        if (NavMesh.SamplePosition(player.position, out hit, playerFallbackSampleRadius, NavMesh.AllAreas) &&
            navMeshAgent.CalculatePath(hit.position, chasePath) &&
            chasePath.status == NavMeshPathStatus.PathComplete &&
            chasePath.corners.Length > 0)
        {
            point = hit.position;
            return true;
        }

        return false;
    }

    private bool EnsureAgentOnNavMesh()
    {
        if (navMeshAgent.isOnNavMesh) return true;
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit,
                offNavMeshRecoveryDistance, NavMesh.AllAreas)) return false;
        navMeshAgent.Warp(hit.position);
        return navMeshAgent.isOnNavMesh;
    }

    private void UpdateStuckRecovery()
    {
        if (navMeshAgent.pathPending) { stuckTimer = 0f; return; }

        bool hasMeaningfulPath = navMeshAgent.hasPath && !float.IsInfinity(navMeshAgent.remainingDistance);
        if (!hasMeaningfulPath)
        {
            stuckTimer += Time.deltaTime;
        }
        else
        {
            bool looksStuck =
                navMeshAgent.remainingDistance > stuckRemainingDistanceThreshold &&
                navMeshAgent.velocity.sqrMagnitude <= stuckVelocityThreshold * stuckVelocityThreshold;
            stuckTimer = looksStuck ? stuckTimer + Time.deltaTime : 0f;
        }

        if (stuckTimer < stuckTimeToRecover) return;
        stuckTimer = 0f;
        navMeshAgent.ResetPath();
        SetDestination();
    }

    private void OnDrawGizmos()
    {
        if (!drawDebugGizmos) return;

        NavMeshAgent agent = navMeshAgent != null ? navMeshAgent : GetComponent<NavMeshAgent>();
        if (agent == null) return;

        Color stateColor = isChasingPlayer ? Color.red : Color.yellow;
        float markerSize = Mathf.Max(0.05f, debugMarkerSize);
        Vector3 enemyPos = transform.position;

        // Enemy mode indicator (red = following player, yellow = patrol/next point).
        Gizmos.color = stateColor;
        Gizmos.DrawWireSphere(enemyPos + Vector3.up * 1.5f, markerSize * 1.4f);

        if (player != null)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.65f);
            Gizmos.DrawLine(enemyPos + Vector3.up * 0.2f, player.position + Vector3.up * 0.2f);
            Gizmos.DrawWireSphere(player.position, playerSampleRadius);
            Gizmos.color = new Color(1f, 0.55f, 0f, 0.45f);
            Gizmos.DrawWireSphere(player.position, playerFallbackSampleRadius);
        }

        bool hasPath = agent.path != null && agent.path.corners != null && agent.path.corners.Length > 0;
        if (hasPath)
        {
            Gizmos.color = stateColor;
            Vector3[] corners = agent.path.corners;
            for (int i = 0; i < corners.Length - 1; i++)
            {
                Gizmos.DrawLine(corners[i] + Vector3.up * 0.06f, corners[i + 1] + Vector3.up * 0.06f);
            }

            Vector3 destination = agent.destination;
            Gizmos.DrawSphere(destination + Vector3.up * 0.1f, markerSize);
        }
        else if (lastRequestedDestination != Vector3.zero)
        {
            Gizmos.color = new Color(stateColor.r, stateColor.g, stateColor.b, 0.5f);
            Gizmos.DrawWireSphere(lastRequestedDestination + Vector3.up * 0.1f, markerSize);
        }
    }
}