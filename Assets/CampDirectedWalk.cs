using UnityEngine;

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

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        directionalSprite = GetComponent<BuddyDirectionalSprite>();
        visualController = GetComponent<GobboVisualController>();
        if (visualController == null)
            visualController = GetComponentInChildren<GobboVisualController>();
        wander = GetComponent<CampWander>();
    }

    public void BeginWalk(Transform newTarget, float speed)
    {
        target = newTarget;
        moveSpeed = Mathf.Max(0.2f, speed);
        activeWalk = target != null;

        if (wander != null)
            wander.enabled = false;
    }

    void FixedUpdate()
    {
        if (!activeWalk || target == null)
            return;

        Vector2 toTarget = (Vector2)target.position - rb.position;

        if (toTarget.magnitude <= reachDistance)
        {
            rb.linearVelocity = Vector2.zero;
            activeWalk = false;

            SetVisualState(GobboAnimationState.Idle, Vector2.zero);

            if (wander != null)
                wander.enabled = enableWanderWhenDone;

            if (destroyWhenDone)
                Destroy(this);

            return;
        }

        Vector2 moveDir = toTarget.normalized;
        TileMover.Move(rb, moveDir * moveSpeed, bodyRadius);

        SetVisualState(GobboAnimationState.Walk, moveDir);
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
