using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BlobSpitterEnemy : MonoBehaviour
{
    private enum BlobState
    {
        Idle,
        Approaching,
        Windup,
        FireMoment,
        Recovery
    }

    [Header("Health")]
    public int maxHealth = 14;
    public int health = 14;

    [Header("Targeting")]
    [Tooltip("Defaults to Player + Buddy if left empty.")]
    public LayerMask targetLayers;
    public float detectionRange = 8f;
    public float attackRange = 4.5f;
    public bool requireLineOfSight = true;

    [Header("Movement")]
    public float moveSpeed = 0.9f;
    public float bodyRadius = 0.38f;
    public float repathTime = 0.35f;
    public float repathDistance = 0.8f;
    public float waypointReachDistance = 0.75f;

    [Header("Placeholder Attack")]
    public float windupDuration = 0.6f;
    public float recoveryDuration = 0.8f;

    [Header("Projectile")]
    public EnemyProjectile projectilePrefab;
    public Transform projectileSpawnPoint;

    [Header("Sprites")]
    public Sprite frontSprite;
    public Sprite backSprite;
    public Sprite leftSprite;
    public Sprite rightSprite;
    public Sprite attackReadySprite;
    public Sprite attackFireSprite;

    [Header("Visual")]
    public SpriteRenderer spriteRenderer;
    public Color hitColor = Color.red;
    public float hitFlashTime = 0.08f;
    public GameObject splatPrefab;

    private Rigidbody2D rb;
    private Transform currentTarget;
    private BlobState state = BlobState.Idle;
    private Vector2 facingDirection = Vector2.down;
    private float stateTimer = 0f;
    private bool isDead = false;
    private Color originalColor;
    private Sprite fallbackSprite;

    private readonly List<Vector2> currentPath = new List<Vector2>();
    private int pathIndex = 0;
    private float repathTimer = 0f;
    private Vector2 lastPathGoal;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
            fallbackSprite = spriteRenderer.sprite;
        }

        if (targetLayers.value == 0)
        {
            int layers = 0;
            int playerLayer = LayerMask.NameToLayer("Player");
            int buddyLayer = LayerMask.NameToLayer("Buddy");

            if (playerLayer >= 0)
                layers |= 1 << playerLayer;

            if (buddyLayer >= 0)
                layers |= 1 << buddyLayer;

            targetLayers.value = layers;
        }

        gameObject.tag = "Enemy";
    }

    void Start()
    {
        health = maxHealth;
        ApplyDirectionalSprite();
    }

    void Update()
    {
        if (isDead)
            return;

        stateTimer -= Time.deltaTime;

        if (currentTarget == null || !IsTargetValid(currentTarget))
            FindTarget();

        if (currentTarget == null)
        {
            EnterIdle();
            return;
        }

        FaceTarget();

        if (state == BlobState.Windup && stateTimer <= 0f)
        {
            BeginFireMoment();
            return;
        }

        if (state == BlobState.FireMoment && stateTimer <= 0f)
        {
            BeginRecovery();
            return;
        }

        if (state == BlobState.Recovery && stateTimer <= 0f)
            state = BlobState.Approaching;
    }

    void FixedUpdate()
    {
        if (isDead)
            return;

        if (currentTarget == null)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (state == BlobState.Windup || state == BlobState.FireMoment || state == BlobState.Recovery)
        {
            rb.linearVelocity = Vector2.zero;
            TileMover.KeepOutOfWalls(rb, bodyRadius);
            return;
        }

        if (IsInAttackRange())
        {
            BeginWindup();
            return;
        }

        state = BlobState.Approaching;
        MoveTowardTarget();
    }

    void EnterIdle()
    {
        if (state == BlobState.Idle)
            return;

        state = BlobState.Idle;
        rb.linearVelocity = Vector2.zero;
        ClearPath();
        ApplyDirectionalSprite();
    }

    void BeginWindup()
    {
        if (state == BlobState.Windup || state == BlobState.FireMoment || state == BlobState.Recovery)
            return;

        state = BlobState.Windup;
        stateTimer = Mathf.Max(0.01f, windupDuration);
        rb.linearVelocity = Vector2.zero;
        ClearPath();
        SetSpriteOrFallback(attackReadySprite);
    }

    void BeginFireMoment()
    {
        state = BlobState.FireMoment;
        stateTimer = 0.08f;
        rb.linearVelocity = Vector2.zero;
        SetSpriteOrFallback(attackFireSprite != null ? attackFireSprite : attackReadySprite);
        FireProjectile();
    }

    void FireProjectile()
    {
        if (projectilePrefab == null || currentTarget == null)
            return;

        Vector2 spawnPosition = projectileSpawnPoint != null
            ? projectileSpawnPoint.position
            : transform.position;

        Vector2 targetPosition = currentTarget.position;
        Vector2 direction = targetPosition - spawnPosition;

        if (direction.sqrMagnitude <= 0.001f)
            direction = facingDirection.sqrMagnitude > 0.001f ? facingDirection : Vector2.down;

        EnemyProjectile projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
        projectile.Launch(direction.normalized, transform);
    }

    void BeginRecovery()
    {
        state = BlobState.Recovery;
        stateTimer = Mathf.Max(0.01f, recoveryDuration);
        rb.linearVelocity = Vector2.zero;
        ApplyDirectionalSprite();
    }

    void FindTarget()
    {
        currentTarget = null;

        if (targetLayers.value == 0)
            return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRange, targetLayers);
        float bestDistance = Mathf.Infinity;

        foreach (Collider2D hit in hits)
        {
            if (hit == null || !hit.gameObject.activeInHierarchy)
                continue;

            Transform candidate = hit.transform;
            if (!CanTarget(candidate))
                continue;

            float distance = Vector2.Distance(transform.position, candidate.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                currentTarget = candidate;
            }
        }

        if (currentTarget != null)
        {
            state = BlobState.Approaching;
            ClearPath();
        }
    }

    bool IsTargetValid(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy)
            return false;

        if (Vector2.Distance(transform.position, target.position) > detectionRange + 3f)
            return false;

        return CanTarget(target);
    }

    bool CanTarget(Transform target)
    {
        if (target == null)
            return false;

        if (!requireLineOfSight)
            return true;

        return MapPathfinder.HasLineOfWalkableSight(transform.position, target.position) ||
               MapPathfinder.TryFindPath(transform.position, target.position, out List<Vector2> path) && path.Count > 0;
    }

    bool IsInAttackRange()
    {
        if (currentTarget == null)
            return false;

        if (Vector2.Distance(transform.position, currentTarget.position) > attackRange)
            return false;

        return !requireLineOfSight || MapPathfinder.HasLineOfWalkableSight(transform.position, currentTarget.position);
    }

    void MoveTowardTarget()
    {
        if (currentTarget == null)
            return;

        Vector2 direction = GetPathDirection(currentTarget.position);

        if (direction.sqrMagnitude > 0.001f)
            SetFacingDirection(direction);

        TileMover.Move(rb, direction * moveSpeed, bodyRadius);
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

        bool needsNewPath = currentPath.Count == 0 ||
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

    void ClearPath()
    {
        currentPath.Clear();
        pathIndex = 0;
    }

    void FaceTarget()
    {
        if (currentTarget == null)
            return;

        SetFacingDirection((Vector2)currentTarget.position - rb.position);
    }

    void SetFacingDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
            return;

        facingDirection = direction.normalized;

        if (state == BlobState.Windup || state == BlobState.FireMoment)
            return;

        ApplyDirectionalSprite();
    }

    void ApplyDirectionalSprite()
    {
        if (spriteRenderer == null)
            return;

        Vector2 dir = facingDirection.sqrMagnitude > 0.001f ? facingDirection.normalized : Vector2.down;
        Sprite sprite;

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            sprite = dir.x < 0f ? leftSprite : rightSprite;
        else
            sprite = dir.y > 0f ? backSprite : frontSprite;

        SetSpriteOrFallback(sprite);
    }

    void SetSpriteOrFallback(Sprite sprite)
    {
        if (spriteRenderer == null)
            return;

        if (sprite != null)
            spriteRenderer.sprite = sprite;
        else if (fallbackSprite != null)
            spriteRenderer.sprite = fallbackSprite;
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

    System.Collections.IEnumerator FlashRoutine()
    {
        Color before = spriteRenderer.color;
        spriteRenderer.color = hitColor;
        yield return new WaitForSeconds(hitFlashTime);

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
            Instantiate(splatPrefab, transform.position, Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));

        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
