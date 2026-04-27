using UnityEngine;
using UnityEngine.AI;

public class EnemyMovement : MonoBehaviour
{
    // Tutorial script: drag the Player Transform here in Inspector.
    public Transform player;

    private NavMeshAgent navMeshAgent;
    private NavMeshPath chasePath;

    [Header("Stuck Recovery")]
    [SerializeField] private float destinationRefreshRate = 0.1f;
    [SerializeField] private float playerSampleRadius = 1.0f;
    [SerializeField] private float playerMaxSampleRadius = 6.0f;
    [SerializeField] private int playerSampleSteps = 4;
    [SerializeField] private float stuckVelocityThreshold = 0.05f;
    [SerializeField] private float stuckRemainingDistanceThreshold = 0.75f;
    [SerializeField] private float stuckTimeToRecover = 0.6f;
    [SerializeField] private float offNavMeshRecoveryDistance = 1.5f;

    private float refreshTimer;
    private float stuckTimer;
    private Vector3 lastKnownReachablePlayerPoint;
    private bool hasLastKnownReachablePlayerPoint;

    private void Start()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        chasePath = new NavMeshPath();
        navMeshAgent.autoRepath = true;
    }

    private void Update()
    {
        if (player == null)
        {
            return;
        }

        if (!EnsureAgentOnNavMesh())
        {
            return;
        }

        refreshTimer -= Time.deltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = Mathf.Max(0.02f, destinationRefreshRate);
            SetSafeDestinationNearPlayer();
        }

        UpdateStuckRecovery();
    }

    private bool EnsureAgentOnNavMesh()
    {
        if (navMeshAgent.isOnNavMesh)
        {
            return true;
        }

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit nearest, offNavMeshRecoveryDistance, NavMesh.AllAreas))
        {
            return false;
        }

        navMeshAgent.Warp(nearest.position);
        return navMeshAgent.isOnNavMesh;
    }

    private void SetSafeDestinationNearPlayer()
    {
        Vector3 desired = player.position;

        if (TryGetReachablePointNearPlayer(desired, out Vector3 reachablePlayerPoint))
        {
            hasLastKnownReachablePlayerPoint = true;
            lastKnownReachablePlayerPoint = reachablePlayerPoint;
            navMeshAgent.isStopped = false;
            navMeshAgent.SetDestination(reachablePlayerPoint);
            return;
        }

        // Player currently unreachable. Keep chasing the last known reachable point,
        // but keep re-sampling every refresh so we instantly reacquire when reachable again.
        if (hasLastKnownReachablePlayerPoint)
        {
            navMeshAgent.isStopped = false;
            navMeshAgent.SetDestination(lastKnownReachablePlayerPoint);
            return;
        }

        navMeshAgent.isStopped = false;
        navMeshAgent.SetDestination(desired);
    }

    private bool TryGetReachablePointNearPlayer(Vector3 playerWorldPosition, out Vector3 reachablePoint)
    {
        reachablePoint = playerWorldPosition;

        float minRadius = Mathf.Max(0.1f, playerSampleRadius);
        float maxRadius = Mathf.Max(minRadius, playerMaxSampleRadius);
        int steps = Mathf.Max(1, playerSampleSteps);

        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 0f : (i / (float)steps);
            float radius = Mathf.Lerp(minRadius, maxRadius, t);

            if (!NavMesh.SamplePosition(playerWorldPosition, out NavMeshHit sampledPlayer, radius, NavMesh.AllAreas))
            {
                continue;
            }

            if (!navMeshAgent.CalculatePath(sampledPlayer.position, chasePath) ||
                chasePath.status != NavMeshPathStatus.PathComplete ||
                chasePath.corners.Length == 0)
            {
                continue;
            }

            reachablePoint = sampledPlayer.position;
            return true;
        }

        return false;
    }

    private void UpdateStuckRecovery()
    {
        if (navMeshAgent.pathPending)
        {
            stuckTimer = 0f;
            return;
        }

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

            stuckTimer = looksStuck ? (stuckTimer + Time.deltaTime) : 0f;
        }

        if (stuckTimer < stuckTimeToRecover)
        {
            return;
        }

        stuckTimer = 0f;
        navMeshAgent.ResetPath();
        SetSafeDestinationNearPlayer();
        navMeshAgent.isStopped = false;
        navMeshAgent.SetDestination(navMeshAgent.destination);
    }
}
