using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BuddyCombat : MonoBehaviour
{
    [Header("Brain")]
    public bool brainAllowsMovement = false;

    [Header("Targeting")]
    public LayerMask enemyLayer;
    public float aggroRange = 6f;
    public float attackRange = 0.75f;
    public bool requireLineOfSightToTarget = true;
    public bool requireReachableTarget = true;

    [Header("Attack")]
    public int damage = 1;
    public float attackCooldown = 0.8f;
    public float chaseSpeed = 4f;
    public float selfKnockbackForce = 2f;
    public float enemyKnockbackForce = 3f;
    public float selfBounceDuration = 0.12f;

    [Header("Behavior")]
    public bool onlyFightNearPlayer = true;
    public float maxDistanceFromPlayerToFight = 7f;

    [Header("Pathing")]
    public float bodyRadius = 0.28f;
    public float repathTime = 0.25f;
    public float repathDistance = 0.8f;
    public float waypointReachDistance = 0.75f;

    private Rigidbody2D rb;
    private Transform player;
    private Transform currentTarget;
    private float attackTimer = 0f;
    private readonly List<Vector2> currentPath = new List<Vector2>();
    private int pathIndex = 0;
    private float repathTimer = 0f;
    private Vector2 lastPathGoal;
    private float bounceTimer = 0f;
    private Vector2 bounceVelocity;
    private BuddyUnit unit;
    private GobboVisualController visualController;
    private BuddyDirectionalSprite directionalSprite;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        unit = GetComponent<BuddyUnit>();
        visualController = GetComponent<GobboVisualController>();
        if (visualController == null)
            visualController = GetComponentInChildren<GobboVisualController>();
        directionalSprite = GetComponent<BuddyDirectionalSprite>();
        FindPlayerIfMissing();
    }

    void Update()
    {
        attackTimer -= Time.deltaTime;
        FindPlayerIfMissing();

        if (!AllowedToFight())
        {
            ClearTarget();
            return;
        }

        FindTarget();
    }

    void FixedUpdate()
    {
        if (!brainAllowsMovement) return;
        if (!AllowedToFight() || currentTarget == null) return;

        if (bounceTimer > 0f)
        {
            bounceTimer -= Time.fixedDeltaTime;
            TileMover.Move(rb, bounceVelocity, bodyRadius);
            FaceDirection(bounceVelocity, GobboAnimationState.Walk);
            return;
        }

        if (!IsStillValidTarget(currentTarget))
        {
            ClearTarget();
            return;
        }

        float distanceToTarget = Vector2.Distance(transform.position, currentTarget.position);
        if (distanceToTarget > attackRange) MoveTowardTarget();
        else TryAttack();
    }

    public bool WantsControl()
    {
        return AllowedToFight() && currentTarget != null && IsStillValidTarget(currentTarget);
    }

    bool AllowedToFight()
    {
        if (unit == null || unit.unitData == null) return true;
        if (unit.unitData.onlyFightsAfterHit && !unit.unitData.hasBeenHit) return false;
        return true;
    }

    void FindTarget()
    {
        if (enemyLayer.value == 0) return;
        if (currentTarget != null && IsStillValidTarget(currentTarget)) return;

        currentTarget = null;
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, aggroRange, enemyLayer);
        float closestDistance = Mathf.Infinity;
        Transform closestEnemy = null;

        foreach (Collider2D hit in hits)
        {
            if (!hit.gameObject.activeInHierarchy) continue;

            Transform possibleTarget = hit.transform;
            if (!IsTargetAllowedByPlayerDistance(possibleTarget)) continue;
            if (!CanSeeOrPathTo(possibleTarget.position)) continue;

            float distance = Vector2.Distance(transform.position, possibleTarget.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEnemy = possibleTarget;
            }
        }

        currentTarget = closestEnemy;
        if (currentTarget != null) ClearPath();
    }

    bool IsStillValidTarget(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return false;
        if (Vector2.Distance(transform.position, target.position) > aggroRange + 1.5f) return false;
        if (!IsTargetAllowedByPlayerDistance(target)) return false;
        return CanSeeOrPathTo(target.position);
    }

    bool IsTargetAllowedByPlayerDistance(Transform target)
    {
        if (!onlyFightNearPlayer || player == null) return true;

        float buddyDistanceFromPlayer = Vector2.Distance(transform.position, player.position);
        float targetDistanceFromPlayer = Vector2.Distance(target.position, player.position);
        return buddyDistanceFromPlayer <= maxDistanceFromPlayerToFight &&
               targetDistanceFromPlayer <= maxDistanceFromPlayerToFight + 2f;
    }

    bool CanSeeOrPathTo(Vector2 targetPos)
    {
        bool hasSight = !requireLineOfSightToTarget || MapPathfinder.HasLineOfWalkableSight(rb.position, targetPos);
        if (hasSight) return true;
        if (!requireReachableTarget) return true;

        List<Vector2> testPath;
        return MapPathfinder.TryFindPath(rb.position, targetPos, out testPath) && testPath.Count > 0;
    }

    void MoveTowardTarget()
    {
        if (currentTarget == null) return;

        Vector2 targetPos = currentTarget.position;
        Vector2 moveDir = GetPathDirection(targetPos);
        TileMover.Move(rb, moveDir * chaseSpeed, bodyRadius);
        FaceDirection(moveDir, GobboAnimationState.Walk);
    }

    Vector2 GetPathDirection(Vector2 targetPos)
    {
        if (MapPathfinder.HasLineOfWalkableSight(rb.position, targetPos))
        {
            ClearPath();
            return (targetPos - rb.position).normalized;
        }

        repathTimer -= Time.fixedDeltaTime;
        bool needsNewPath = currentPath.Count == 0 ||
                            pathIndex >= currentPath.Count ||
                            repathTimer <= 0f ||
                            Vector2.Distance(targetPos, lastPathGoal) > repathDistance;

        if (needsNewPath) RebuildPath(targetPos);

        if (currentPath.Count == 0 || pathIndex >= currentPath.Count) return Vector2.zero;

        Vector2 waypoint = currentPath[pathIndex];
        if (Vector2.Distance(rb.position, waypoint) <= waypointReachDistance)
        {
            pathIndex++;
            if (pathIndex >= currentPath.Count) return Vector2.zero;
            waypoint = currentPath[pathIndex];
        }

        return (waypoint - rb.position).normalized;
    }

    void RebuildPath(Vector2 targetPos)
    {
        repathTimer = repathTime;
        lastPathGoal = targetPos;

        if (MapPathfinder.TryFindPath(rb.position, targetPos, out List<Vector2> newPath))
        {
            currentPath.Clear();
            currentPath.AddRange(newPath);
            pathIndex = 0;
        }
        else
        {
            ClearPath();
        }
    }

    void TryAttack()
    {
        rb.linearVelocity = Vector2.zero;
        if (attackTimer > 0f || currentTarget == null) return;
        if (!MapPathfinder.HasLineOfWalkableSight(rb.position, currentTarget.position)) return;

        attackTimer = attackCooldown;
        Vector2 directionToEnemy = ((Vector2)currentTarget.position - rb.position).normalized;
        FaceDirection(directionToEnemy, GobboAnimationState.Attack);

        DamageEnemy(currentTarget.gameObject);
        KnockEnemy(currentTarget.gameObject, directionToEnemy);
        StartSelfBounce(-directionToEnemy);
    }

    void DamageEnemy(GameObject enemy)
    {
        enemy.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
    }

    void KnockEnemy(GameObject enemy, Vector2 direction)
    {
        Rigidbody2D enemyRb = enemy.GetComponent<Rigidbody2D>();
        if (enemyRb != null) enemyRb.AddForce(direction * enemyKnockbackForce, ForceMode2D.Impulse);
    }

    void StartSelfBounce(Vector2 direction)
    {
        bounceVelocity = direction.normalized * selfKnockbackForce;
        bounceTimer = selfBounceDuration;
    }

    void FaceDirection(Vector2 direction, GobboAnimationState state)
    {
        if (direction.sqrMagnitude <= 0.001f)
            return;

        if (visualController != null)
        {
            visualController.SetAnimationState(state);
            visualController.SetDirection(direction);
            return;
        }

        if (directionalSprite != null)
            directionalSprite.SetDirection(direction);
    }

    void ClearTarget()
    {
        currentTarget = null;
        ClearPath();
    }

    void ClearPath()
    {
        currentPath.Clear();
        pathIndex = 0;
    }

    void FindPlayerIfMissing()
    {
        if (player != null) return;
        GameObject foundPlayer = GameObject.FindGameObjectWithTag("Player");
        if (foundPlayer != null) player = foundPlayer.transform;
    }

    public void SetPlayer(Transform newPlayer)
    {
        player = newPlayer;
    }

    public void SetTarget(Transform target)
    {
        currentTarget = target;
        ClearPath();
    }
}
