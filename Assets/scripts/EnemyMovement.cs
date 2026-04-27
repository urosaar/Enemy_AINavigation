using UnityEngine;
using UnityEngine.AI;

public class EnemyMovement : MonoBehaviour
{
    public Transform player;

    [Header("Chase")]
    [SerializeField] private float destinationRefreshRate = 0.1f;
    [SerializeField] private float playerSampleRadius = 1.0f;
    [SerializeField] private float playerFallbackSampleRadius = 2.5f;
    [SerializeField] private float offNavMeshRecoveryDistance = 1.5f;

    [Header("Stuck Recovery")]
    [SerializeField] private float stuckVelocityThreshold = 0.05f;
    [SerializeField] private float stuckRemainingDistanceThreshold = 0.75f;
    [SerializeField] private float stuckTimeToRecover = 0.6f;

    private NavMeshAgent navMeshAgent;
    private NavMeshPath chasePath;
    private float refreshTimer;
    private float stuckTimer;

    private void Start()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        chasePath = new NavMeshPath();
        navMeshAgent.autoRepath = true;
    }

    private void Update()
    {
        if (player == null) return;
        if (!EnsureAgentOnNavMesh()) return;

        refreshTimer -= Time.deltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = Mathf.Max(0.02f, destinationRefreshRate);
            SetDestination();
        }

        UpdateStuckRecovery();
    }

    private void SetDestination()
    {
        if (TryGetReachablePlayerPoint(out Vector3 point))
        {
            navMeshAgent.isStopped = false;
            navMeshAgent.SetDestination(point);
        }
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
}