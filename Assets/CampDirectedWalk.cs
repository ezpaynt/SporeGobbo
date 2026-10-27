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
    public float TimeoutSeconds => timeout;
    public float PlannedRouteDistance { get; private set; }
    private float noProgressElapsed;
    private Vector2 lastPhysicsPosition;
    private Collider2D bodyCollider;
    private readonly List<(Collider2D a,Collider2D b)> ignoredBuddyPairs = new List<(Collider2D,Collider2D)>();
    sealed class BuddyCollisionLease
    {
        public bool Previous;
        public readonly HashSet<CampDirectedWalk> Owners=new HashSet<CampDirectedWalk>();
    }
    static readonly Dictionary<(Collider2D a,Collider2D b),BuddyCollisionLease> buddyCollisionLeases=new Dictionary<(Collider2D,Collider2D),BuddyCollisionLease>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetCollisionLeases()=>buddyCollisionLeases.Clear();
    public CampDirectedWalkResult Result { get; private set; } = CampDirectedWalkResult.None;
    public bool IsWalking => Result == CampDirectedWalkResult.Walking;
    public Vector2 PhysicsPosition => rb != null ? rb.position : (Vector2)transform.position;
    public float EffectiveReachDistance => reachDistance;
    public string BlockingColliderDescription { get; private set; } = "none detected";

    void Awake()=>CachePhysicsReferences();
    void CachePhysicsReferences()
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
        if(rb==null)CachePhysicsReferences();
        RestoreBuddyCollisions();
        regionRoute.Reset();
        target = newTarget;
        moveSpeed = Mathf.Max(0.2f, speed);
        if (arrivalDistance >= 0f) reachDistance = Mathf.Max(0.01f, arrivalDistance);
        timeout = Mathf.Max(0.01f, timeoutSeconds);
        PlannedRouteDistance=target!=null?Vector2.Distance(PhysicsPosition,target.position):0;
        var binding=GetComponent<ActorTerrainBinding>();
        if(binding!=null&&target!=null&&binding.TryPath(PhysicsPosition,target.position,out var route))
        {
            float length=0;Vector2 prior=PhysicsPosition;
            foreach(var waypoint in route){length+=Vector2.Distance(prior,waypoint);prior=waypoint;}
            PlannedRouteDistance=length;
            // A winding cave route takes longer than its straight-line displacement.
            timeout=Mathf.Max(timeout,SporeGobbo.CampLifecycle.CampBuddyPhysicalPolicy.GetDirectedWalkTimeout(length,moveSpeed,0));
        }
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

    readonly RegionRouteFollower regionRoute=new RegionRouteFollower();
    void FixedUpdate()
    {
        if (!activeWalk || target == null)
            return;

        var binding=GetComponent<ActorTerrainBinding>();
        if(binding!=null && (binding.Ready!=null&&!binding.Ready() || SporeGobbo.Input.SporeInputReader.Instance==null || SporeGobbo.Input.SporeInputReader.Instance.Context!=SporeGobbo.Input.SporeInputContext.Gameplay)){rb.linearVelocity=Vector2.zero;return;}
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

        if(!regionRoute.Direction(binding,physicsPosition,target.position,out Vector2 moveDir)){Finish(CampDirectedWalkResult.Blocked);return;}
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
    void OnDisable(){if(activeWalk)Finish(CampDirectedWalkResult.Cancelled);else RestoreBuddyCollisions();}

    void IgnoreOtherBuddyBodies()
    {
        if (bodyCollider == null || GetComponent<BuddyUnit>() == null) return;
        BuddyUnit[] buddies = Object.FindObjectsByType<BuddyUnit>();
        foreach (BuddyUnit buddy in buddies)
        {
            if (buddy == null || buddy.gameObject == gameObject) continue;
            Collider2D other = buddy.GetComponent<Collider2D>();
            if (other == null || other == bodyCollider) continue;
            var pair=bodyCollider.GetInstanceID()<other.GetInstanceID()?(bodyCollider,other):(other,bodyCollider);
            if(ignoredBuddyPairs.Contains(pair))continue;
            if(!buddyCollisionLeases.TryGetValue(pair,out var lease))
            {lease=new BuddyCollisionLease{Previous=Physics2D.GetIgnoreCollision(pair.Item1,pair.Item2)};buddyCollisionLeases.Add(pair,lease);}
            lease.Owners.Add(this);ignoredBuddyPairs.Add(pair);
            Physics2D.IgnoreCollision(bodyCollider, other, true);
        }
    }

    void RestoreBuddyCollisions()
    {
        foreach(var pair in ignoredBuddyPairs)
        {
            if(!buddyCollisionLeases.TryGetValue(pair,out var lease))continue;
            lease.Owners.Remove(this);if(lease.Owners.Count>0)continue;
            if(pair.a!=null&&pair.b!=null)Physics2D.IgnoreCollision(pair.a,pair.b,lease.Previous);
            buddyCollisionLeases.Remove(pair);
        }
        ignoredBuddyPairs.Clear();
    }

    string DescribePhysicalBlocker(Vector2 toTarget)
    {
        if (bodyCollider == null) return "missing body collider";
        var binding=GetComponent<ActorTerrainBinding>();var terrain=ActorTerrainBinding.Resolve(gameObject);var extents=TileMover.GetMapClearanceExtents(rb,bodyRadius);
        string context=" position="+rb.position+" velocity="+rb.linearVelocity+" extents="+extents+" radius="+bodyRadius+" next="+regionRoute.CurrentWaypoint+" canOccupy="+TileMover.CanOccupyBox(terrain,rb.position,extents)+" canNext="+TileMover.CanTraverseBox(terrain,rb.position,regionRoute.CurrentWaypoint,extents)+" bindingReady="+(binding?.Ready?.Invoke())+" simulated="+rb.simulated+" constraints="+rb.constraints;
        Vector2 direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.zero;
        RaycastHit2D[] hits = Physics2D.BoxCastAll(bodyCollider.bounds.center,
            bodyCollider.bounds.size * 0.98f, 0f, direction, 0.08f, Physics2D.AllLayers);
        foreach (RaycastHit2D hit in hits)
            if (hit.collider != null && hit.collider != bodyCollider && !hit.collider.isTrigger && !Physics2D.GetIgnoreCollision(bodyCollider,hit.collider))
                return hit.collider.name + " layer=" + LayerMask.LayerToName(hit.collider.gameObject.layer) +
                       " point=" + hit.point + " normal=" + hit.normal + context;
        return "no non-trigger non-ignored collider found by body sweep"+context;
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
