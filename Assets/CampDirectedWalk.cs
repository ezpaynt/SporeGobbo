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

            if (visualController != null)
                visualController.SetAnimationState(GobboAnimationState.Idle);

            if (wander != null)
                wander.enabled = enableWanderWhenDone;

            if (destroyWhenDone)
                Destroy(this);

            return;
        }

        Vector2 moveDir = toTarget.normalized;
        TileMover.Move(rb, moveDir * moveSpeed, bodyRadius);

        if (visualController != null)
        {
            visualController.SetAnimationState(GobboAnimationState.Walk);
            visualController.SetDirection(moveDir);
        }

        if (directionalSprite != null)
            directionalSprite.SetDirection(moveDir);
    }
}
