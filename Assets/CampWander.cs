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
    private BuddyUnit buddy;
    private bool semanticDestinations;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        buddy = GetComponent<BuddyUnit>();
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
        semanticDestinations = false;
        anchor = newAnchor;
        useTransformAnchor = anchor != null;
        anchorPosition = anchor != null ? (Vector2)anchor.position : (Vector2)transform.position;
        wanderRadius = Mathf.Max(0.1f, radius);
        moveSpeed = Mathf.Max(0f, speed);
        PickNewTarget();
    }

    public void SetSemanticDestinations(float speed)
    {
        semanticDestinations = true;
        moveSpeed = Mathf.Max(0f, speed);
        PickNewTarget();
    }

    readonly RegionRouteFollower regionRoute=new RegionRouteFollower();
    void FixedUpdate()
    {
        var binding=GetComponent<ActorTerrainBinding>();
        if(binding!=null && (binding.Ready!=null&&!binding.Ready() || SporeGobbo.Input.SporeInputReader.Instance==null || SporeGobbo.Input.SporeInputReader.Instance.Context!=SporeGobbo.Input.SporeInputContext.Gameplay)){rb.linearVelocity=Vector2.zero;return;}
        Vector2 anchorPos = GetAnchorPosition();

        if (idleTimer > 0f)
        {
            idleTimer -= Time.fixedDeltaTime;
            if(idleTimer<=0)PickNewTarget();
            rb.linearVelocity = Vector2.zero;
            SetVisualState(GobboAnimationState.Idle, Vector2.zero);
            return;
        }

        Vector2 toTarget = target - rb.position;

        if (toTarget.magnitude <= reachDistance&&(binding?.Movement==null||binding.Movement.MarkArrived(binding)))
        {
            rb.linearVelocity = Vector2.zero;
            idleTimer = Random.Range(idleMinTime, idleMaxTime);
            SetVisualState(GobboAnimationState.Idle, Vector2.zero);
            return;
        }

        // Keep camp buddies from drifting too far even if something bumps them.
        if (!semanticDestinations && Vector2.Distance(rb.position, anchorPos) > wanderRadius * 1.75f)
            target = anchorPos;

        if(!regionRoute.Direction(binding,rb.position,target,out Vector2 moveDir,moveSpeed*Time.fixedDeltaTime)){rb.linearVelocity=Vector2.zero;stuckTimer+=Time.fixedDeltaTime;if(stuckTimer>=repickIfStuckTime){stuckTimer=0;PickNewTarget();}return;}
        ActorMotionIntent.Submit(this,rb,moveDir*moveSpeed,bodyRadius,5);
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
        regionRoute.Reset();GetComponent<ActorTerrainBinding>()?.CancelNavigation();
        if (semanticDestinations)
        {
            CampActivityPoint[] points = CampActivityPoint.GetValidFor(buddy != null ? buddy.unitData : null);
            if (points.Length > 0)
            {
                var point=points[Random.Range(0,points.Length)];target=point.transform.position;
                var binding=GetComponent<ActorTerrainBinding>();if(binding!=null)target=binding.MovementGoal(point.GetInstanceID(),target,Vector2.zero);
                return;
            }
        }
        Vector2 anchorPos = GetAnchorPosition();
        target = anchorPos + Random.insideUnitCircle * wanderRadius;
        var bound=GetComponent<ActorTerrainBinding>();if(bound?.Movement!=null)target=bound.MovementGoal(GetInstanceID(),target,Vector2.zero);
    }

    void OnDisable(){ActorMotionIntent.Cancel(gameObject);GetComponent<ActorTerrainBinding>()?.Movement?.Park(GetComponent<ActorTerrainBinding>());}

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
