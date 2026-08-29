using UnityEngine;
using System.Collections.Generic;

public enum CampDirectedWalkResult
{
    None,
    Walking,
    Arrived,
    Blocked,
    Cancelled,
    TimedOut,
    InvalidTarget
}

[RequireComponent(typeof(Rigidbody2D))]
public class CampDirectedWalk : MonoBehaviour
{
    [Header("Directed Camp Walk")]
    public Transform target;
    public float moveSpeed = 1.6f;
    public float bodyRadius = 0.25f;
    public float reachDistance = 0.18f;
    public bool destroyWhenDone = true;
    public bool enableWanderWhenDone = true;

    private Rigidbody2D rb;
    private BuddyDirectionalSprite directionalSprite;
    private GobboVisualController visualController;
    private CampWander wander;
    private bool activeWalk = false;
    private float elapsed;
    private float timeout;
    private float noProgressElapsed;
    private Vector2 lastPhysicsPosition;
    private Collider2D bodyCollider;
    private readonly List<Collider2D> ignoredBuddyColliders = new List<Collider2D>();
    public CampDirectedWalkResult Result { get; private set; } = CampDirectedWalkResult.None;
    public bool IsWalking => Result == CampDirectedWalkResult.Walking;
    public Vector2 PhysicsPosition => rb != null ? rb.position : (Vector2)transform.position;
    public float EffectiveReachDistance => reachDistance;
    public string BlockingColliderDescription { get; private set; } = "none detected";

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        directionalSprite = GetComponent<BuddyDirectionalSprite>();
        visualController = GetComponent<GobboVisualController>();
        if (visualController == null)
            visualController = GetComponentInChildren<GobboVisualController>();
        wander = GetComponent<CampWander>();
        bodyCollider = GetComponent<Collider2D>();
    }

    public void BeginWalk(Transform newTarget, float speed, float arrivalDistance = -1f,
        float timeoutSeconds = float.PositiveInfinity)
    {
        RestoreBuddyCollisions();
        target = newTarget;
        moveSpeed = Mathf.Max(0.2f, speed);
        if (arrivalDistance >= 0f) reachDistance = Mathf.Max(0.01f, arrivalDistance);
        timeout = Mathf.Max(0.01f, timeoutSeconds);
        elapsed = 0f;
        noProgressElapsed = 0f;
        BlockingColliderDescription = "none detected";
        lastPhysicsPosition = PhysicsPosition;
        activeWalk = target != null;
        Result = activeWalk ? CampDirectedWalkResult.Walking : CampDirectedWalkResult.InvalidTarget;

        if (wander != null)
            wander.enabled = false;
        if (activeWalk) IgnoreOtherBuddyBodies();
        else if (wander != null) wander.enabled = enableWanderWhenDone;
    }

    public void CancelWalk()
    {
        if (!activeWalk) return;
        Finish(CampDirectedWalkResult.Cancelled);
    }

    void FixedUpdate()
    {
        if (!activeWalk || target == null)
            return;

        elapsed += Time.fixedDeltaTime;
        Vector2 physicsPosition = rb.position;
        Vector2 toTarget = (Vector2)target.position - physicsPosition;
        if (toTarget.magnitude <= EffectiveReachDistance)
        {
            Finish(CampDirectedWalkResult.Arrived);
            return;
        }
        if (Vector2.Distance(physicsPosition, lastPhysicsPosition) > 0.001f)
            noProgressElapsed = 0f;
        else
            noProgressElapsed += Time.fixedDeltaTime;
        lastPhysicsPosition = physicsPosition;
        if (elapsed >= timeout)
        {
            Finish(CampDirectedWalkResult.TimedOut);
            return;
        }
        if (noProgressElapsed >= 0.5f)
        {
            BlockingColliderDescription = DescribePhysicalBlocker(toTarget);
            Finish(CampDirectedWalkResult.Blocked);
            return;
        }

        Vector2 moveDir = toTarget.normalized;
        TileMover.Move(rb, moveDir * moveSpeed, bodyRadius);

        SetVisualState(GobboAnimationState.Walk, moveDir);
    }

    void Finish(CampDirectedWalkResult result)
    {
        if (rb != null) rb.linearVelocity = Vector2.zero;
        activeWalk = false;
        Result = result;
        RestoreBuddyCollisions();
        SetVisualState(GobboAnimationState.Idle, Vector2.zero);
        if (wander != null) wander.enabled = enableWanderWhenDone;
        if (destroyWhenDone) Destroy(this);
    }

    void OnDestroy() => RestoreBuddyCollisions();

    void IgnoreOtherBuddyBodies()
    {
        if (bodyCollider == null || GetComponent<BuddyUnit>() == null) return;
        BuddyUnit[] buddies = Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None);
        foreach (BuddyUnit buddy in buddies)
        {
            if (buddy == null || buddy.gameObject == gameObject) continue;
            Collider2D other = buddy.GetComponent<Collider2D>();
            if (other == null || other == bodyCollider || ignoredBuddyColliders.Contains(other)) continue;
            Physics2D.IgnoreCollision(bodyCollider, other, true);
            ignoredBuddyColliders.Add(other);
        }
    }

    void RestoreBuddyCollisions()
    {
        if (bodyCollider != null)
            foreach (Collider2D other in ignoredBuddyColliders)
                if (other != null) Physics2D.IgnoreCollision(bodyCollider, other, false);
        ignoredBuddyColliders.Clear();
    }

    string DescribePhysicalBlocker(Vector2 toTarget)
    {
        if (bodyCollider == null) return "missing body collider";
        Vector2 direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.zero;
        RaycastHit2D[] hits = Physics2D.BoxCastAll(bodyCollider.bounds.center,
            bodyCollider.bounds.size * 0.98f, 0f, direction, 0.08f, Physics2D.AllLayers);
        foreach (RaycastHit2D hit in hits)
            if (hit.collider != null && hit.collider != bodyCollider && !hit.collider.isTrigger)
                return hit.collider.name + " layer=" + LayerMask.LayerToName(hit.collider.gameObject.layer) +
                       " point=" + hit.point + " normal=" + hit.normal;
        return "no non-trigger collider found by body sweep";
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
