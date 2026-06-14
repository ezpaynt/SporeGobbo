using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class TunnelWeevilEnemy : MonoBehaviour
{
    public enum EnemyKind
    {
        Weevil,
        Scorpion
    }

    private enum CombatState
    {
        Chasing,
        AttackWindup,
        AttackRecovery,
        Knockback
    }

    [Header("Enemy Identity")]
    public string enemyName = "Tunnel Weevil";
    public EnemyKind enemyKind = EnemyKind.Weevil;

    [Header("Health")]
    public int maxHealth = 16;
    public int health = 16;

    [Header("Run Scaling")]
    public bool scaleWithRunNumber = true;
    public int healthPerRun = 2;
    public int damagePerEveryXRuns = 3;
    public int xpPerRun = 1;

    [Header("Drops")]
    public GameObject foodDropPrefab;
    public int xpDropValue = 3;

    [Header("Targeting")]
    [Tooltip("Set this to Player + Buddy.")]
    public LayerMask targetLayers;
    public float noticeRange = 7f;
    public float loseTargetRangeBuffer = 3f;
    public float giveUpRange = 12f;
    public float stopDistance = 0.8f;
    public bool requireLineOfSightToNotice = true;
    public bool requireReachableTarget = true;
    public float targetRefreshTime = 0.35f;
    public float targetSwitchCloserBy = 1.2f;
    public float playerPreferenceBonus = 0.35f;

    [Header("Movement")]
    public float moveSpeed = 1.2f;
    public float chaseSpeed = 1.8f;
    public float bodyRadius = 0.38f;

    [Header("Idle Wander")]
    public float idlePointRadius = 3.5f;
    public float idlePointReachDistance = 0.45f;
    public float idleWaitMin = 0.25f;
    public float idleWaitMax = 1.0f;

    [Header("Chase Patience")]
    public float slowChaseAfterNoAttackTime = 5f;
    public float giveUpAfterNoAttackTime = 10f;
    public float tiredChaseSpeedMultiplier = 0.45f;

    [Header("Pathing")]
    public float repathTime = 0.35f;
    public float repathDistance = 0.8f;
    public float waypointReachDistance = 0.75f;

    [Header("Attack")]
    public int attackDamage = 23;
    public float attackRange = 0.75f;
    public float attackRadius = 0.4f;
    public float attackCooldown = 1.3f;
    public float attackWindup = 0.35f;
    public float attackRecovery = 0.35f;
    public float windupSpeedMultiplier = 0f;
    public GameObject attackDebugPrefab;

    [Header("Hit Response")]
    public float stunDuration = 0.12f;
    public float hitKnockbackForce = 3f;
    [Range(0f, 1f)] public float knockbackResistance = 0.25f;

    [Header("Scorpion Poison")]
    public bool applyPoison = false;
    public int poisonDamagePerTick = 2;
    public float poisonDuration = 3f;
    public float poisonTickRate = 1f;

    [Header("Visual")]
    public bool flipSpriteToFaceTarget = true;
    public SpriteRenderer spriteRenderer;
    public Color hitColor = Color.red;
    public float flashTime = 0.08f;
    public GameObject splatPrefab;

    private Rigidbody2D rb;
    private Transform currentTarget;
    private Vector2 aimDirection = Vector2.left;
    private Vector2 spawnPosition;
    private Vector2 idleGoal;
    private float idleWaitTimer = 0f;
    private float attackCooldownTimer = 0f;
    private float timeSinceSuccessfulAttack = 0f;
    private float targetRefreshTimer = 0f;
    private float stateTimer = 0f;
    private bool didWindupDamage = false;
    private bool isDead = false;
    private Color originalColor;
    private CombatState combatState = CombatState.Chasing;
    private Vector2 knockbackVelocity;

    private readonly List<Vector2> currentPath = new List<Vector2>();
    private int pathIndex = 0;
    private float repathTimer = 0f;
    private Vector2 lastPathGoal;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        spawnPosition = transform.position;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;

        gameObject.tag = "Enemy";
    }

    void Start()
    {
        ApplyKindDefaults();
        ApplyRunScaling();
        health = maxHealth;
        PickIdleGoal();
    }

    void ApplyKindDefaults()
    {
        if (enemyKind == EnemyKind.Scorpion)
        {
            if (enemyName == "Tunnel Weevil")
                enemyName = "Tunnel Scorpion";

            applyPoison = true;

            // Scorpions are a bit less tanky/direct, but poison makes them scary.
            maxHealth = Mathf.Max(maxHealth, 12);
            attackDamage = Mathf.Max(attackDamage, 5);
            chaseSpeed = Mathf.Max(chaseSpeed, 2.8f);
            noticeRange = Mathf.Max(noticeRange, 11f);
        }
        else
        {
            applyPoison = false;
            maxHealth = Mathf.Max(maxHealth, 16);
            attackDamage = Mathf.Max(attackDamage, 23);
        }
    }

    void ApplyRunScaling()
    {
        if (!scaleWithRunNumber || GameState.Instance == null)
            return;

        int extraRuns = Mathf.Max(0, GameState.Instance.currentRunNumber - 1);

        maxHealth += extraRuns * healthPerRun;
        attackDamage += extraRuns / Mathf.Max(1, damagePerEveryXRuns);
        xpDropValue += extraRuns * xpPerRun;
    }

    void Update()
    {
        if (isDead)
            return;

        attackCooldownTimer -= Time.deltaTime;
        targetRefreshTimer -= Time.deltaTime;
        stateTimer -= Time.deltaTime;

        if (combatState == CombatState.Knockback)
            return;

        if (currentTarget == null)
            FindBestTarget();
        else
        {
            if (!IsTargetStillValid(currentTarget))
                ClearTarget();
            else if (combatState == CombatState.Chasing && targetRefreshTimer <= 0f)
                RefreshTargetChoice();
        }

        if (currentTarget != null)
        {
            timeSinceSuccessfulAttack += Time.deltaTime;
            UpdateAimDirection(currentTarget.position);
            FaceTarget();
        }
    }

    void FixedUpdate()
    {
        if (isDead)
            return;

        if (combatState == CombatState.Knockback)
        {
            TickKnockback();
            return;
        }

        if (currentTarget == null)
        {
            combatState = CombatState.Chasing;
            IdleWander();
            return;
        }

        if (ShouldGiveUpTarget())
        {
            ClearTarget();
            return;
        }

        switch (combatState)
        {
            case CombatState.AttackWindup:
                TickAttackWindup();
                break;
            case CombatState.AttackRecovery:
                TickAttackRecovery();
                break;
            default:
                TickChase();
                break;
        }
    }

    bool ShouldGiveUpTarget()
    {
        if (currentTarget == null)
            return true;

        if (Vector2.Distance(transform.position, currentTarget.position) > giveUpRange)
            return true;

        return timeSinceSuccessfulAttack >= giveUpAfterNoAttackTime;
    }

    void TickChase()
    {
        float distance = Vector2.Distance(transform.position, currentTarget.position);

        if (distance <= attackRange)
        {
            BeginAttackWindup();
            return;
        }

        MoveTowardTarget(chaseSpeed);
    }

    void BeginAttackWindup()
    {
        if (attackCooldownTimer > 0f || currentTarget == null)
            return;

        if (!MapPathfinder.HasLineOfWalkableSight(transform.position, currentTarget.position))
            return;

        combatState = CombatState.AttackWindup;
        stateTimer = Mathf.Max(0.01f, attackWindup);
        didWindupDamage = false;
        rb.linearVelocity = Vector2.zero;
        ClearPath();
        UpdateAimDirection(currentTarget.position);
        FaceTarget();
    }

    void TickAttackWindup()
    {
        if (currentTarget == null)
        {
            BeginRecovery();
            return;
        }

        UpdateAimDirection(currentTarget.position);
        FaceTarget();

        if (windupSpeedMultiplier > 0f)
            MoveTowardTarget(chaseSpeed * windupSpeedMultiplier);
        else
            rb.linearVelocity = Vector2.zero;

        if (stateTimer <= 0f && !didWindupDamage)
        {
            didWindupDamage = true;
            DoAttackDamageMoment();
            BeginRecovery();
        }
    }

    void BeginRecovery()
    {
        combatState = CombatState.AttackRecovery;
        stateTimer = Mathf.Max(0.01f, attackRecovery);
        attackCooldownTimer = attackCooldown;
        rb.linearVelocity = Vector2.zero;
    }

    void TickAttackRecovery()
    {
        rb.linearVelocity = Vector2.zero;

        if (stateTimer <= 0f)
            combatState = CombatState.Chasing;
    }

    void TickKnockback()
    {
        TileMover.Move(rb, knockbackVelocity, bodyRadius);
        TileMover.KeepOutOfWalls(rb, bodyRadius);

        if (stateTimer <= 0f)
        {
            combatState = CombatState.Chasing;
            rb.linearVelocity = Vector2.zero;
        }
    }

    void FindBestTarget()
    {
        targetRefreshTimer = targetRefreshTime;

        if (targetLayers.value == 0)
            return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, noticeRange, targetLayers);

        Transform best = null;
        float bestScore = Mathf.Infinity;

        foreach (Collider2D hit in hits)
        {
            if (hit == null || !hit.gameObject.activeInHierarchy)
                continue;

            Transform candidate = hit.transform;

            if (!CanNoticeTarget(candidate))
                continue;

            float distance = Vector2.Distance(transform.position, candidate.position);
            float score = distance;

            if (candidate.CompareTag("Player"))
                score -= playerPreferenceBonus;

            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (best != null)
        {
            currentTarget = best;
            timeSinceSuccessfulAttack = 0f;
            combatState = CombatState.Chasing;
            ClearPath();
        }
    }

    void RefreshTargetChoice()
    {
        Transform oldTarget = currentTarget;
        float oldDistance = oldTarget != null ? Vector2.Distance(transform.position, oldTarget.position) : Mathf.Infinity;

        currentTarget = null;
        FindBestTarget();

        if (currentTarget == null)
        {
            currentTarget = oldTarget;
            return;
        }

        float newDistance = Vector2.Distance(transform.position, currentTarget.position);

        if (oldTarget != null && oldTarget != currentTarget && newDistance > oldDistance - targetSwitchCloserBy)
            currentTarget = oldTarget;
    }

    bool IsTargetStillValid(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy)
            return false;

        if (Vector2.Distance(transform.position, target.position) > giveUpRange)
            return false;

        return CanNoticeTarget(target);
    }

    bool CanNoticeTarget(Transform target)
    {
        if (target == null)
            return false;

        bool hasSight = !requireLineOfSightToNotice ||
                        MapPathfinder.HasLineOfWalkableSight(transform.position, target.position);

        if (hasSight)
            return true;

        if (!requireReachableTarget)
            return true;

        List<Vector2> testPath;
        return MapPathfinder.TryFindPath(transform.position, target.position, out testPath) && testPath.Count > 0;
    }

    void MoveTowardTarget(float speed)
    {
        if (currentTarget == null)
            return;

        Vector2 targetPos = currentTarget.position;
        Vector2 direction = GetPathDirection(targetPos);

        if (timeSinceSuccessfulAttack >= slowChaseAfterNoAttackTime)
            speed *= tiredChaseSpeedMultiplier;

        TileMover.Move(rb, direction * speed, bodyRadius);
        TileMover.KeepOutOfWalls(rb, bodyRadius);
    }

    Vector2 GetPathDirection(Vector2 targetPos)
    {
        if (MapPathfinder.HasLineOfWalkableSight(rb.position, targetPos))
        {
            ClearPath();
            return (targetPos - rb.position).normalized;
        }

        repathTimer -= Time.fixedDeltaTime;

        bool needsNewPath =
            currentPath.Count == 0 ||
            pathIndex >= currentPath.Count ||
            repathTimer <= 0f ||
            Vector2.Distance(targetPos, lastPathGoal) > repathDistance;

        if (needsNewPath)
            RebuildPath(targetPos);

        if (currentPath.Count == 0 || pathIndex >= currentPath.Count)
            return Vector2.zero;

        Vector2 waypoint = currentPath[pathIndex];

        if (Vector2.Distance(rb.position, waypoint) <= waypointReachDistance)
        {
            pathIndex++;

            if (pathIndex >= currentPath.Count)
                return Vector2.zero;

            waypoint = currentPath[pathIndex];
        }

        return (waypoint - rb.position).normalized;
    }

    void RebuildPath(Vector2 targetPos)
    {
        repathTimer = repathTime;
        lastPathGoal = targetPos;

        if (MapPathfinder.TryFindPath(rb.position, targetPos, out List<Vector2> path))
        {
            currentPath.Clear();
            currentPath.AddRange(path);
            pathIndex = 0;
        }
        else
        {
            ClearPath();
        }
    }

    void DoAttackDamageMoment()
    {
        if (currentTarget == null)
            return;

        if (!MapPathfinder.HasLineOfWalkableSight(transform.position, currentTarget.position))
            return;

        Vector2 targetPos = currentTarget.position;
        Vector2 attackPoint = Vector2.Lerp(transform.position, targetPos, 0.65f);

        if (attackDebugPrefab != null)
        {
            GameObject marker = Instantiate(attackDebugPrefab, attackPoint, Quaternion.identity);
            marker.transform.localScale = Vector3.one * attackRadius * 2f;
            Destroy(marker, 0.15f);
        }

        Collider2D hit = targetLayers.value == 0
            ? Physics2D.OverlapCircle(attackPoint, attackRadius)
            : Physics2D.OverlapCircle(attackPoint, attackRadius, targetLayers);

        if (hit == null || !hit.gameObject.activeInHierarchy)
            return;

        if (hit.transform != currentTarget && !IsCurrentTargetCollider(hit))
            return;

        hit.SendMessage("TakeDamage", attackDamage, SendMessageOptions.DontRequireReceiver);

        if (applyPoison)
            ApplyPoisonToTarget(hit.gameObject);

        timeSinceSuccessfulAttack = 0f;
    }

    bool IsCurrentTargetCollider(Collider2D hit)
    {
        if (hit == null || currentTarget == null)
            return false;

        return hit.transform == currentTarget || hit.transform.IsChildOf(currentTarget) || currentTarget.IsChildOf(hit.transform);
    }

    void ApplyPoisonToTarget(GameObject target)
    {
        if (target == null)
            return;

        GobboController gobbo = target.GetComponent<GobboController>();
        if (gobbo != null)
        {
            gobbo.ApplyPoison(poisonDamagePerTick, poisonDuration, poisonTickRate);
            return;
        }

        BuddyUnit buddy = target.GetComponent<BuddyUnit>();
        if (buddy != null)
        {
            buddy.ApplyPoison(poisonDamagePerTick, poisonDuration, poisonTickRate);
            return;
        }
    }

    void IdleWander()
    {
        if (idleWaitTimer > 0f)
        {
            idleWaitTimer -= Time.fixedDeltaTime;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (!IsWorldPointUsable(idleGoal) || Vector2.Distance(transform.position, idleGoal) <= idlePointReachDistance)
            PickIdleGoal();

        Vector2 dir = (idleGoal - rb.position).normalized;

        TileMover.Move(rb, dir * moveSpeed, bodyRadius);
        TileMover.KeepOutOfWalls(rb, bodyRadius);
    }

    void PickIdleGoal()
    {
        for (int i = 0; i < 20; i++)
        {
            Vector2 candidate = spawnPosition + Random.insideUnitCircle * idlePointRadius;

            if (IsWorldPointUsable(candidate))
            {
                idleGoal = candidate;
                idleWaitTimer = Random.Range(idleWaitMin, idleWaitMax);
                return;
            }
        }

        idleGoal = spawnPosition;
        idleWaitTimer = Random.Range(idleWaitMin, idleWaitMax);
    }

    bool IsWorldPointUsable(Vector2 point)
    {
        return MapGenerator.Instance == null ||
               MapGenerator.Instance.IsWorldPositionClearForBody(point, bodyRadius);
    }

    void UpdateAimDirection(Vector2 targetPosition)
    {
        Vector2 direction = targetPosition - (Vector2)transform.position;

        if (direction.sqrMagnitude > 0.001f)
            aimDirection = direction.normalized;
    }

    void FaceTarget()
    {
        if (!flipSpriteToFaceTarget || spriteRenderer == null)
            return;

        spriteRenderer.flipX = aimDirection.x > 0f;
    }

    public void TakeDamage(int amount)
    {
        if (isDead)
            return;

        health -= Mathf.Max(1, amount);

        if (spriteRenderer != null)
        {
            StopCoroutine(nameof(FlashRoutine));
            StartCoroutine(FlashRoutine());
        }

        if (health <= 0)
            Die();
    }

    public void ApplyKnockback(Vector2 direction)
    {
        if (isDead || direction.sqrMagnitude <= 0.001f)
            return;

        float resistedForce = hitKnockbackForce * Mathf.Clamp01(1f - knockbackResistance);
        knockbackVelocity = direction.normalized * resistedForce;
        stateTimer = Mathf.Max(0.01f, stunDuration);
        combatState = CombatState.Knockback;
        didWindupDamage = false;
        ClearPath();
    }

    System.Collections.IEnumerator FlashRoutine()
    {
        Color before = spriteRenderer.color;
        spriteRenderer.color = hitColor;
        yield return new WaitForSeconds(flashTime);

        if (spriteRenderer != null)
            spriteRenderer.color = originalColor == default ? before : originalColor;
    }

    void Die()
    {
        if (isDead)
            return;

        isDead = true;

        if (GameState.Instance != null)
            GameState.Instance.RegisterEnemyKilled();

        if (splatPrefab != null)
        {
            GameObject splat = Instantiate(splatPrefab, transform.position, Quaternion.identity);
            splat.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        }

        SpawnFoodDrop();

        Destroy(gameObject);
    }

    void SpawnFoodDrop()
    {
        if (foodDropPrefab == null)
            return;

        Vector2 dropOffset = Random.insideUnitCircle * 0.25f;
        GameObject food = Instantiate(foodDropPrefab, (Vector2)transform.position + dropOffset, Quaternion.identity);

        FoodItem foodItem = food.GetComponent<FoodItem>();

        if (foodItem != null)
        {
            foodItem.xpValue = xpDropValue;
            foodItem.foodName = enemyKind == EnemyKind.Scorpion ? "Scorpion Meat" : "Weevil Meat";
        }
    }

    void ClearTarget()
    {
        currentTarget = null;
        combatState = CombatState.Chasing;
        timeSinceSuccessfulAttack = 0f;
        rb.linearVelocity = Vector2.zero;
        ClearPath();
        PickIdleGoal();
    }

    void ClearPath()
    {
        currentPath.Clear();
        pathIndex = 0;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, noticeRange);

        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(transform.position, giveUpRange);

        Gizmos.color = Color.red;
        Vector2 dir = aimDirection == Vector2.zero ? Vector2.left : aimDirection.normalized;
        Vector2 attackPoint = (Vector2)transform.position + dir * attackRange * 0.65f;
        Gizmos.DrawWireSphere(attackPoint, attackRadius);
    }
}
