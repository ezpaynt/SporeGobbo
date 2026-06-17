using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(FoodItem))]
public class FleeingMushroomCritter : MonoBehaviour
{
    private enum CritterState
    {
        Sleeping,
        Spooked,
        Fleeing,
        Calming,
        Idle
    }

    [Header("References")]
    public SpriteRenderer spriteRenderer;
    public FoodItem foodItem;
    public Rigidbody2D rb;
    [Tooltip("Optional. If empty, the critter finds the current GobboController/player at runtime.")]
    public Transform playerTarget;

    [Header("Behavior")]
    public float wakeRange = 3f;
    public float calmRange = 6f;
    public float spookedDuration = 0.25f;
    public float fleeSpeed = 2.2f;
    public float bodyRadius = 0.25f;
    public float turnInterval = 0.35f;
    [Range(0f, 1f)] public float wobbleStrength = 0.35f;
    public float calmDelay = 2f;
    public float idleBeforeSleepDuration = 1.5f;

    [Header("Animation")]
    public Sprite[] sleepFrames;
    public float sleepFrameDuration = 0.45f;
    public Sprite spookedSprite;
    public Sprite[] runFrames;
    public float runFrameDuration = 0.12f;
    public Sprite[] idleFrames;
    public float idleFrameDuration = 0.35f;
    public bool flipSpriteTowardMovement = true;

    [Header("Debug")]
    public bool debugDrawRanges = false;

    private CritterState state = CritterState.Sleeping;
    private float stateTimer;
    private float frameTimer;
    private int frameIndex;
    private float turnTimer;
    private Vector2 fleeDirection = Vector2.right;
    private Vector2 lastMoveDirection = Vector2.right;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (foodItem == null)
            foodItem = GetComponent<FoodItem>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }
    }

    private void Start()
    {
        FindPlayerIfNeeded();
        EnterSleeping();
    }

    private void Update()
    {
        FindPlayerIfNeeded();

        float distanceToPlayer = GetDistanceToPlayer();

        switch (state)
        {
            case CritterState.Sleeping:
                PlayLoop(sleepFrames, sleepFrameDuration);
                if (distanceToPlayer <= wakeRange)
                    EnterSpooked();
                break;

            case CritterState.Spooked:
                if (spookedSprite != null)
                    SetSprite(spookedSprite);

                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                    EnterFleeing();
                break;

            case CritterState.Fleeing:
                PlayLoop(runFrames, runFrameDuration);
                if (distanceToPlayer >= calmRange)
                    EnterCalming();
                break;

            case CritterState.Calming:
                PlayLoop(idleFrames, idleFrameDuration);
                if (distanceToPlayer <= wakeRange)
                {
                    EnterSpooked();
                    break;
                }

                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                    EnterIdle();
                break;

            case CritterState.Idle:
                PlayLoop(idleFrames, idleFrameDuration);
                if (distanceToPlayer <= wakeRange)
                {
                    EnterSpooked();
                    break;
                }

                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                    EnterSleeping();
                break;
        }
    }

    private void FixedUpdate()
    {
        if (rb == null)
            return;

        if (state != CritterState.Fleeing || playerTarget == null)
        {
            rb.linearVelocity = Vector2.zero;
            TileMover.KeepOutOfWalls(rb, bodyRadius);
            return;
        }

        turnTimer -= Time.fixedDeltaTime;
        if (turnTimer <= 0f || !CanMove(fleeDirection))
        {
            fleeDirection = PickFleeDirection();
            turnTimer = Mathf.Max(0.05f, turnInterval);
        }

        Vector2 desiredVelocity = fleeDirection * fleeSpeed;
        if (desiredVelocity.sqrMagnitude > 0.001f)
        {
            lastMoveDirection = desiredVelocity.normalized;
            UpdateSpriteFlip(lastMoveDirection);
        }

        TileMover.Move(rb, desiredVelocity, bodyRadius);
        TileMover.KeepOutOfWalls(rb, bodyRadius);
    }

    private void EnterSleeping()
    {
        state = CritterState.Sleeping;
        ResetAnimation();
        StopMoving();
        PlayLoop(sleepFrames, sleepFrameDuration, true);
    }

    private void EnterSpooked()
    {
        state = CritterState.Spooked;
        stateTimer = Mathf.Max(0.01f, spookedDuration);
        ResetAnimation();
        StopMoving();
        if (spookedSprite != null)
            SetSprite(spookedSprite);
    }

    private void EnterFleeing()
    {
        state = CritterState.Fleeing;
        ResetAnimation();
        turnTimer = 0f;
        fleeDirection = PickFleeDirection();
    }

    private void EnterCalming()
    {
        state = CritterState.Calming;
        stateTimer = Mathf.Max(0f, calmDelay);
        ResetAnimation();
        StopMoving();
    }

    private void EnterIdle()
    {
        state = CritterState.Idle;
        stateTimer = Mathf.Max(0f, idleBeforeSleepDuration);
        ResetAnimation();
        StopMoving();
    }

    private void StopMoving()
    {
        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    private void FindPlayerIfNeeded()
    {
        if (playerTarget != null)
            return;

        GobboController gobbo = UnityEngine.Object.FindAnyObjectByType<GobboController>();
        if (gobbo != null)
            playerTarget = gobbo.transform;
    }

    private float GetDistanceToPlayer()
    {
        if (playerTarget == null)
            return Mathf.Infinity;

        return Vector2.Distance(transform.position, playerTarget.position);
    }

    private Vector2 PickFleeDirection()
    {
        Vector2 away = playerTarget != null
            ? ((Vector2)transform.position - (Vector2)playerTarget.position).normalized
            : lastMoveDirection;

        if (away.sqrMagnitude <= 0.001f)
            away = Random.insideUnitCircle.normalized;

        Vector2 perpendicular = new Vector2(-away.y, away.x);
        float wobble = Random.Range(-wobbleStrength, wobbleStrength);
        Vector2 preferred = (away + perpendicular * wobble).normalized;

        Vector2[] candidates = new Vector2[]
        {
            preferred,
            Rotate(preferred, 35f),
            Rotate(preferred, -35f),
            Rotate(preferred, 70f),
            Rotate(preferred, -70f),
            -perpendicular,
            perpendicular,
            -away
        };

        foreach (Vector2 candidate in candidates)
        {
            if (candidate.sqrMagnitude > 0.001f && CanMove(candidate.normalized))
                return candidate.normalized;
        }

        return Vector2.zero;
    }

    private bool CanMove(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
            return false;

        MapGenerator map = MapGenerator.Instance;
        if (map == null)
            return true;

        float probeDistance = Mathf.Max(bodyRadius * 1.5f, fleeSpeed * Time.fixedDeltaTime * 2f);
        Vector2 probePosition = (Vector2)transform.position + direction.normalized * probeDistance;
        return map.IsWorldPositionClearForBody(probePosition, bodyRadius);
    }

    private Vector2 Rotate(Vector2 value, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(value.x * cos - value.y * sin, value.x * sin + value.y * cos);
    }

    private void PlayLoop(Sprite[] frames, float frameDuration, bool forceFirstFrame = false)
    {
        if (frames == null || frames.Length == 0)
            return;

        if (forceFirstFrame)
        {
            frameIndex = FindNextValidFrame(frames, 0);
            SetSprite(frames[frameIndex]);
            return;
        }

        float safeDuration = Mathf.Max(0.01f, frameDuration);
        frameTimer -= Time.deltaTime;

        if (frameTimer > 0f && spriteRenderer != null && spriteRenderer.sprite != null)
            return;

        frameTimer = safeDuration;
        frameIndex = FindNextValidFrame(frames, frameIndex);
        SetSprite(frames[frameIndex]);
        frameIndex = (frameIndex + 1) % frames.Length;
    }

    private int FindNextValidFrame(Sprite[] frames, int startIndex)
    {
        if (frames == null || frames.Length == 0)
            return 0;

        int safeStart = Mathf.Clamp(startIndex, 0, frames.Length - 1);
        for (int i = 0; i < frames.Length; i++)
        {
            int index = (safeStart + i) % frames.Length;
            if (frames[index] != null)
                return index;
        }

        return safeStart;
    }

    private void ResetAnimation()
    {
        frameTimer = 0f;
        frameIndex = 0;
    }

    private void SetSprite(Sprite sprite)
    {
        if (spriteRenderer != null && sprite != null)
            spriteRenderer.sprite = sprite;
    }

    private void UpdateSpriteFlip(Vector2 direction)
    {
        if (!flipSpriteTowardMovement || spriteRenderer == null || Mathf.Abs(direction.x) <= 0.05f)
            return;

        spriteRenderer.flipX = direction.x < 0f;
    }

    private void OnValidate()
    {
        wakeRange = Mathf.Max(0f, wakeRange);
        calmRange = Mathf.Max(wakeRange, calmRange);
        spookedDuration = Mathf.Max(0.01f, spookedDuration);
        fleeSpeed = Mathf.Max(0f, fleeSpeed);
        bodyRadius = Mathf.Max(0.01f, bodyRadius);
        turnInterval = Mathf.Max(0.05f, turnInterval);
        calmDelay = Mathf.Max(0f, calmDelay);
        idleBeforeSleepDuration = Mathf.Max(0f, idleBeforeSleepDuration);
        sleepFrameDuration = Mathf.Max(0.01f, sleepFrameDuration);
        runFrameDuration = Mathf.Max(0.01f, runFrameDuration);
        idleFrameDuration = Mathf.Max(0.01f, idleFrameDuration);
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugDrawRanges)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, wakeRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, calmRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, bodyRadius);
    }
}
