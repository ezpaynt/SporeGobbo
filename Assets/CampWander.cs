using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class CampWander : MonoBehaviour
{
    [Header("Anchor")]
    public Transform anchor;
    public Vector2 anchorPosition;
    public bool useTransformAnchor = true;

    [Header("Wander")]
    public float wanderRadius = 1.5f;
    public float moveSpeed = 1.4f;
    public float bodyRadius = 0.25f;
    public float reachDistance = 0.12f;
    public float idleMinTime = 0.5f;
    public float idleMaxTime = 2.0f;
    public float repickIfStuckTime = 1.25f;

    private Rigidbody2D rb;
    private BuddyDirectionalSprite directionalSprite;
    private GobboVisualController visualController;
    private Vector2 target;
    private float idleTimer;
    private float stuckTimer;
    private Vector2 lastPosition;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;

        directionalSprite = GetComponent<BuddyDirectionalSprite>();
        visualController = GetComponent<GobboVisualController>();
        if (visualController == null)
            visualController = GetComponentInChildren<GobboVisualController>();

        if (anchor == null)
        {
            useTransformAnchor = false;
            anchorPosition = transform.position;
        }

        PickNewTarget();
        lastPosition = rb.position;
    }

    public void SetAnchor(Transform newAnchor, float radius, float speed)
    {
        anchor = newAnchor;
        useTransformAnchor = anchor != null;
        anchorPosition = anchor != null ? (Vector2)anchor.position : (Vector2)transform.position;
        wanderRadius = Mathf.Max(0.1f, radius);
        moveSpeed = Mathf.Max(0f, speed);
        PickNewTarget();
    }

    void FixedUpdate()
    {
        Vector2 anchorPos = GetAnchorPosition();

        if (idleTimer > 0f)
        {
            idleTimer -= Time.fixedDeltaTime;
            rb.linearVelocity = Vector2.zero;
            SetVisualState(GobboAnimationState.Idle, Vector2.zero);
            return;
        }

        Vector2 toTarget = target - rb.position;

        if (toTarget.magnitude <= reachDistance)
        {
            rb.linearVelocity = Vector2.zero;
            idleTimer = Random.Range(idleMinTime, idleMaxTime);
            PickNewTarget();
            SetVisualState(GobboAnimationState.Idle, Vector2.zero);
            return;
        }

        // Keep camp buddies from drifting too far even if something bumps them.
        if (Vector2.Distance(rb.position, anchorPos) > wanderRadius * 1.75f)
            target = anchorPos;

        Vector2 moveDir = (target - rb.position).normalized;
        TileMover.Move(rb, moveDir * moveSpeed, bodyRadius);
        SetVisualState(GobboAnimationState.Walk, moveDir);

        float moved = Vector2.Distance(rb.position, lastPosition);
        if (moved < 0.01f)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer >= repickIfStuckTime)
            {
                stuckTimer = 0f;
                PickNewTarget();
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        lastPosition = rb.position;
    }

    Vector2 GetAnchorPosition()
    {
        if (useTransformAnchor && anchor != null)
            return anchor.position;

        return anchorPosition;
    }

    void PickNewTarget()
    {
        Vector2 anchorPos = GetAnchorPosition();
        target = anchorPos + Random.insideUnitCircle * wanderRadius;
    }

    void SetVisualState(GobboAnimationState state, Vector2 direction)
    {
        if (visualController != null)
        {
            visualController.SetAnimationState(state);
            if (direction.sqrMagnitude > 0.001f)
                visualController.SetDirection(direction);
            return;
        }

        if (directionalSprite != null && direction.sqrMagnitude > 0.001f)
            directionalSprite.SetDirection(direction);
    }
}
