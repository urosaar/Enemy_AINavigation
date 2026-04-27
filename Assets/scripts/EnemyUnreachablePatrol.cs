using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyUnreachablePatrol : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float targetSampleRadius = 0.75f;
    [SerializeField] private float edgeClearance = 0.25f;

    [Header("Unreachable Patrol Ring")]
    [SerializeField] private float patrolRingRadius = 3f;
    [SerializeField] private int patrolSamples = 16;
    [SerializeField] private float patrolPointRefreshRate = 0.25f;
    [SerializeField] private float patrolPauseDuration = 0.6f;

    [Header("Recovery")]
    [SerializeField] private float offNavMeshRecoveryDistance = 2f;
    [SerializeField] private float minRepathDistance = 0.4f;

    [Header("Debug")]
    [SerializeField] private bool drawDebugGizmos = true;

    private NavMeshAgent agent;
    private NavMeshPath workingPath;
    private float refreshTimer;
    private float pauseTimer;
    private Vector3 lastPlayerProbePosition;
    private Vector3 currentDestination;
    private Vector3 lastReachablePoint;
    private bool hasDestination;
    private bool hasLastReachablePoint;
    private bool isPatrollingUnreachable;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        workingPath = new NavMeshPath();
    }

    private void OnValidate()
    {
        targetSampleRadius = Mathf.Max(0.1f, targetSampleRadius);
        edgeClearance = Mathf.Max(0.05f, edgeClearance);
        patrolRingRadius = Mathf.Max(0.5f, patrolRingRadius);
        patrolSamples = Mathf.Max(4, patrolSamples);
        patrolPointRefreshRate = Mathf.Max(0.05f, patrolPointRefreshRate);
        patrolPauseDuration = Mathf.Max(0f, patrolPauseDuration);
        offNavMeshRecoveryDistance = Mathf.Max(0.5f, offNavMeshRecoveryDistance);
        minRepathDistance = Mathf.Max(0.05f, minRepathDistance);
    }

    public bool TryHandleUnreachableTarget(Vector3 playerPosition)
    {
        lastPlayerProbePosition = playerPosition;

        if (!EnsureAgentOnNavMesh())
        {
            isPatrollingUnreachable = true;
            return true;
        }

        if (TryGetReachablePoint(playerPosition, out Vector3 reachableTarget))
        {
            isPatrollingUnreachable = false;
            hasLastReachablePoint = true;
            lastReachablePoint = reachableTarget;
            hasDestination = false;
            return false;
        }

        isPatrollingUnreachable = true;
        refreshTimer -= Time.deltaTime;
        if (refreshTimer > 0f)
        {
            return true;
        }

        refreshTimer = patrolPointRefreshRate;

        if (pauseTimer > 0f)
        {
            pauseTimer -= patrolPointRefreshRate;
            return true;
        }

        if (TryGetRingPatrolPoint(out Vector3 patrolPoint))
        {
            MoveTo(patrolPoint);
            pauseTimer = patrolPauseDuration;
            return true;
        }

        if (hasLastReachablePoint)
        {
            MoveTo(lastReachablePoint);
            pauseTimer = patrolPauseDuration;
            return true;
        }

        return true;
    }

    private bool TryGetRingPatrolPoint(out Vector3 patrolPoint)
    {
        patrolPoint = transform.position;
        float startAngle = Random.Range(0f, Mathf.PI * 2f);

        for (int i = 0; i < patrolSamples; i++)
        {
            float angle = startAngle + i * (Mathf.PI * 2f / patrolSamples);
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * patrolRingRadius;
            Vector3 candidate = lastPlayerProbePosition + offset;

            if (!TryGetReachablePoint(candidate, out Vector3 safePoint))
            {
                continue;
            }

            if (GetPlanarDistance(transform.position, safePoint) <= agent.stoppingDistance + 0.1f)
            {
                continue;
            }

            patrolPoint = safePoint;
            return true;
        }

        return false;
    }

    private bool TryGetReachablePoint(Vector3 worldPoint, out Vector3 reachablePoint)
    {
        reachablePoint = worldPoint;

        if (!NavMesh.SamplePosition(worldPoint, out NavMeshHit sampledPoint, targetSampleRadius, NavMesh.AllAreas))
        {
            return false;
        }

        if (!HasEnoughEdgeClearance(sampledPoint.position))
        {
            return false;
        }

        if (!agent.CalculatePath(sampledPoint.position, workingPath) ||
            workingPath.status != NavMeshPathStatus.PathComplete ||
            workingPath.corners.Length == 0)
        {
            return false;
        }

        reachablePoint = sampledPoint.position;
        return true;
    }

    private bool HasEnoughEdgeClearance(Vector3 point)
    {
        if (!NavMesh.FindClosestEdge(point, out NavMeshHit edgeHit, NavMesh.AllAreas))
        {
            return true;
        }

        return edgeHit.distance >= edgeClearance;
    }

    private bool EnsureAgentOnNavMesh()
    {
        if (agent.isOnNavMesh)
        {
            return true;
        }

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit nearest, offNavMeshRecoveryDistance, NavMesh.AllAreas))
        {
            return false;
        }

        agent.Warp(nearest.position);
        return agent.isOnNavMesh;
    }

    private void MoveTo(Vector3 destination)
    {
        if (hasDestination && GetPlanarDistance(currentDestination, destination) <= minRepathDistance)
        {
            return;
        }

        agent.isStopped = false;
        agent.SetDestination(destination);
        currentDestination = destination;
        hasDestination = true;
    }

    private float GetPlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
        {
            return;
        }

        if (isPatrollingUnreachable)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(lastPlayerProbePosition + Vector3.up * 0.1f, patrolRingRadius);
        }

        if (hasLastReachablePoint)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(lastReachablePoint + Vector3.up * 0.2f, 0.15f);
        }

        if (hasDestination)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(currentDestination + Vector3.up * 0.2f, 0.12f);
        }
    }
}
