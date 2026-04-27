using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyWander : MonoBehaviour
{
    [Header("Wander")]
    [SerializeField] private float wanderRadius = 12f;
    [SerializeField] private float minPickInterval = 1.5f;
    [SerializeField] private float maxPickInterval = 4f;
    [SerializeField] private float sampleRadius = 2f;
    [SerializeField] private int maxAttempts = 20;

    [Header("Debug")]
    [SerializeField] private bool drawDebugGizmos = true;

    private NavMeshAgent agent;
    private EnemyMovement chaseScript;
    private NavMeshPath testPath;
    private float pickTimer;
    private Vector3 wanderTarget;
    private bool hasWanderTarget;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        chaseScript = GetComponent<EnemyMovement>();
        testPath = new NavMeshPath();
        ScheduleNextPick();
    }

    private void Update()
    {
        // Only wander when the chase script cannot reach the player
        if (PlayerIsReachable()) return;

        pickTimer -= Time.deltaTime;

        // Pick a new point when timer expires or we've arrived at the current one
        bool arrived = hasWanderTarget &&
                       !agent.pathPending &&
                       agent.remainingDistance <= agent.stoppingDistance + 0.05f;

        if (pickTimer <= 0f || arrived)
        {
            if (TryPickWanderPoint(out Vector3 point))
            {
                wanderTarget = point;
                hasWanderTarget = true;
                agent.isStopped = false;
                agent.SetDestination(point);
            }

            ScheduleNextPick();
        }
    }

    private bool PlayerIsReachable()
    {
        if (chaseScript == null || chaseScript.player == null) return false;

        // Mirror the same check EnemyMovement uses — full path required
        return agent.CalculatePath(chaseScript.player.position, testPath) &&
               testPath.status == NavMeshPathStatus.PathComplete &&
               testPath.corners.Length > 0;
    }

    private bool TryPickWanderPoint(out Vector3 result)
    {
        result = transform.position;

        for (int i = 0; i < maxAttempts; i++)
        {
            // Random direction, random distance up to wanderRadius
            Vector2 circle = Random.insideUnitCircle.normalized * (wanderRadius * Random.Range(0.3f, 1f));
            Vector3 candidate = transform.position + new Vector3(circle.x, 0f, circle.y);

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
                continue;

            if (!agent.CalculatePath(hit.position, testPath) ||
                testPath.status != NavMeshPathStatus.PathComplete ||
                testPath.corners.Length == 0)
                continue;

            // Must be far enough to bother walking there
            if (Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                                 new Vector3(hit.position.x, 0, hit.position.z)) <= agent.stoppingDistance + 0.1f)
                continue;

            result = hit.position;
            return true;
        }

        return false;
    }

    private void ScheduleNextPick()
    {
        pickTimer = Random.Range(minPickInterval, maxPickInterval);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos) return;
        Gizmos.color = new Color(0.8f, 0.8f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, wanderRadius);
        if (hasWanderTarget)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(wanderTarget + Vector3.up * 0.2f, 0.12f);
        }
    }
}