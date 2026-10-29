using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BuddyFollow : MonoBehaviour
{
    [Header("Brain")]
    public bool brainAllowsMovement = true;

    [Header("Follow Target")]
    public Transform player;

    [Header("Follow Settings")]
    public float followSpeed = 3.5f;
    public float stopDistance = 0.45f;
    public float teleportDistance = 18f;

    [Header("Formation")]
    public Vector2 formationOffset;
    public float offsetStrength = 0f;

    [Header("Pathing")]
    public float repathTime = 0.25f;
    public float repathDistance = 0.8f;
    public float waypointReachDistance = 0.85f;
    public bool usePathfinding = true;
    public float physicalBodyRadius = 0.28f;

    [Header("Gobbo Dance / Unstuck")]
    public bool useGobboDance = true;
    public float randomDanceMinTime = 1.0f;
    public float randomDanceMaxTime = 2.4f;
    public float randomDanceDuration = 0.12f;
    public float stuckCheckTime = 0.55f;
    public float stuckMoveThreshold = 0.045f;
    public float stuckDanceDuration = 0.35f;
    public float stuckBackstepDuration = 0.18f;
    public float danceStrength = 0.85f;

    [Header("Separation")]
    public float separationRadius = 0.75f;
    public float separationForce = 0.08f;
    public LayerMask buddyLayer;

    private int boundRouteRevision = -1;
    private Rigidbody2D rb;
    private GobboVisualController visualController;
    private BuddyDirectionalSprite directionalSprite;

    private List<Vector2> currentPath = new List<Vector2>();
    private int pathIndex = 0;
    public int RouteNodeCount=>currentPath.Count;
    public Vector2 RouteWaypoint=>pathIndex>=0&&pathIndex<currentPath.Count?currentPath[pathIndex]:rb!=null?rb.position:(Vector2)transform.position;
    private float repathTimer = 0f;
    private Vector2 lastPathGoal;

    private Vector2 lastPosition;
    private float stuckTimer;

    private float randomDanceTimer;
    private float danceTimer;
    private float backstepTimer;
    private int danceSide = 1;

    private bool currentlyUsingPath = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;

        visualController = GetComponent<GobboVisualController>();
        if (visualController == null)
            visualController = GetComponentInChildren<GobboVisualController>();

        directionalSprite = GetComponent<BuddyDirectionalSprite>();
        lastPosition = rb.position;

        ResetRandomDanceTimer();
        FindPlayerIfMissing();
    }

    void FixedUpdate()
    {
        using var decisionProfile=new Unity.Profiling.ProfilerMarker("Spore.AI.BuddyFollow").Auto();
        using (new ActorTerrainBinding.Scope("SG59.BuddyFollow.FixedUpdate", gameObject.GetComponent<ActorTerrainBinding>() != null))
        {
        FindPlayerIfMissing();
        var binding = GetComponent<ActorTerrainBinding>();
        if (binding != null && boundRouteRevision != binding.Revision && !binding.AcceptsMovementRoute(boundRouteRevision)) { ClearPath(); boundRouteRevision = binding.Revision; }

        if (!brainAllowsMovement)
        {
            ClearPath();
            SetVisualState(GobboAnimationState.Idle);
            return;
        }

        if (player == null)
            return;

        float distanceToPlayer = Vector2.Distance(rb.position, player.position);

        if (distanceToPlayer > teleportDistance && GetComponent<ActorTerrainBinding>() == null)
        {
            transform.position = player.position + (Vector3)Random.insideUnitCircle;
            rb.linearVelocity = Vector2.zero;
            ClearPath();
            SetVisualState(GobboAnimationState.Idle);
            return;
        }

        var brain=GetComponent<BuddyBrain>();
        Vector2 targetPosition = brain!=null&&brain.SquadScoped&&!brain.Following?brain.StayAnchor:(Vector2)player.position + formationOffset * offsetStrength;

        var terrainBinding=GetComponent<ActorTerrainBinding>();
        bool pointDestination=false;
        if(terrainBinding!=null&&terrainBinding.ChooseDestination!=null)
        {bool staying=brain!=null&&brain.SquadScoped&&!brain.Following;var targetBody=staying?null:player.GetComponent<Rigidbody2D>();pointDestination=targetBody==null;targetPosition=terrainBinding.MovementGoal(staying?0:player.GetInstanceID(),targetPosition,pointDestination?Vector2.zero:TileMover.GetMapClearanceExtents(targetBody,.25f));}
        float destinationTolerance=pointDestination?Mathf.Min(stopDistance,terrainBinding.Terrain.CellSize*.12f):stopDistance;
        if (Vector2.Distance(rb.position, targetPosition) <= destinationTolerance)
        {
            rb.linearVelocity = Vector2.zero;
            ClearPath();
            SetVisualState(GobboAnimationState.Idle);
            return;
        }

        CheckIfStuck(distanceToPlayer);

        Vector2 moveDir = GetFollowDirection(targetPosition);
        if(binding==null||binding.RequestRoute==null)moveDir = ApplyGobboDance(moveDir, targetPosition);

        Vector2 velocity=moveDir*followSpeed;
        if(brain!=null)velocity=brain.BoundVelocity(rb.position,velocity,Time.fixedDeltaTime);
        ActorMotionIntent.Submit(this,rb,velocity,physicalBodyRadius,10);
        FaceDirection(moveDir);
    
        }
}

    public void ResetCommandMotion()
    {ClearPath();danceTimer=backstepTimer=stuckTimer=0;if(rb!=null)rb.linearVelocity=Vector2.zero;}

    Vector2 GetFollowDirection(Vector2 targetPosition)
    {
        currentlyUsingPath = false;

        Vector2 directDir = targetPosition - rb.position;

        if (directDir.sqrMagnitude < 0.001f)
            return Vector2.zero;

        var binding = GetComponent<ActorTerrainBinding>();
        bool directClear = binding != null ? binding.Sight(rb.position, targetPosition) : MapPathfinder.HasLineOfWalkableSight(rb.position, targetPosition);

        if (!usePathfinding || (currentPath.Count == 0 && directClear))
        {
            ClearPath();
            currentlyUsingPath = false;
            return directDir.normalized;
        }

        repathTimer -= Time.fixedDeltaTime;

        bool needsNewPath =
            (currentPath.Count == 0 && repathTimer <= 0f) ||
            (currentPath.Count > 0 && pathIndex >= currentPath.Count) ||
            (repathTimer <= 0f && (binding==null||binding.RequestRoute==null||!binding.AcceptsMovementRoute(boundRouteRevision))) ||
            Vector2.Distance(targetPosition, lastPathGoal) > repathDistance;

        if (needsNewPath)
            RebuildPath(targetPosition);

        if (currentPath.Count == 0 || pathIndex >= currentPath.Count)
            return Vector2.zero;

        pathIndex=RouteWaypointPolicy.Advance(currentPath,pathIndex,rb.position,binding,physicalBodyRadius);
        if(pathIndex>=currentPath.Count)return Vector2.zero;
        Vector2 waypoint = currentPath[pathIndex];

        float waypointTolerance=binding!=null&&binding.RequestRoute!=null?Mathf.Min(waypointReachDistance,binding.Terrain.CellSize*.12f):waypointReachDistance;
        if (Vector2.Distance(rb.position, waypoint) <= waypointTolerance)
        {
            pathIndex++;

            if (pathIndex >= currentPath.Count)
                return Vector2.zero;

            waypoint = currentPath[pathIndex];
        }

        currentlyUsingPath = true;

        Vector2 pathDir = (waypoint - rb.position).normalized;
        return binding!=null&&binding.RequestRoute!=null?pathDir*Mathf.Clamp01(Vector2.Distance(waypoint,rb.position)/Mathf.Max(.001f,followSpeed*Time.fixedDeltaTime)):(pathDir+GetSeparation()*.2f).normalized;
    }

    void RebuildPath(Vector2 targetPosition)
    {
        repathTimer = repathTime;
        lastPathGoal = targetPosition;

        var binding = GetComponent<ActorTerrainBinding>();
        if (binding != null ? binding.TryPath(rb.position, targetPosition, out currentPath) : MapPathfinder.TryFindPath(rb.position, targetPosition, out currentPath))
            pathIndex = 0;
        else
            ClearPath();
    }

    Vector2 ApplyGobboDance(Vector2 moveDir, Vector2 targetPosition)
    {
        if (!useGobboDance)
            return moveDir;

        if (!currentlyUsingPath && danceTimer <= 0f && backstepTimer <= 0f)
            return moveDir;

        Vector2 toTarget = targetPosition - rb.position;

        if (toTarget.sqrMagnitude < 0.001f)
            return moveDir;

        toTarget.Normalize();

        if (backstepTimer > 0f)
        {
            backstepTimer -= Time.fixedDeltaTime;
            return -toTarget;
        }

        if (danceTimer > 0f)
        {
            danceTimer -= Time.fixedDeltaTime;
            Vector2 side = Vector2.Perpendicular(toTarget) * danceSide;
            return (moveDir + side * danceStrength).normalized;
        }

        randomDanceTimer -= Time.fixedDeltaTime;

        if (currentlyUsingPath && randomDanceTimer <= 0f && moveDir.sqrMagnitude > 0.01f)
        {
            danceSide *= -1;
            danceTimer = randomDanceDuration;
            ResetRandomDanceTimer();
        }

        return moveDir;
    }

    void StartStuckDance()
    {
        danceSide *= -1;
        danceTimer = stuckDanceDuration;
        backstepTimer = stuckBackstepDuration;

        ClearPath();
        repathTimer = 0f;
    }

    void CheckIfStuck(float distanceToPlayer)
    {
        float moved = Vector2.Distance(rb.position, lastPosition);

        if (distanceToPlayer > stopDistance + 0.4f && moved < stuckMoveThreshold)
        {
            stuckTimer += Time.fixedDeltaTime;

            if (stuckTimer >= stuckCheckTime)
            {
                stuckTimer = 0f;
                StartStuckDance();
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        lastPosition = rb.position;
    }

    Vector2 GetSeparation()
    {
        if (buddyLayer.value == 0)
            return Vector2.zero;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, separationRadius, buddyLayer);
        Vector2 push = Vector2.zero;

        foreach (Collider2D hit in hits)
        {
            if (hit.gameObject == gameObject)
                continue;

            Vector2 away = (Vector2)transform.position - (Vector2)hit.transform.position;

            if (away.sqrMagnitude > 0.001f)
                push += away.normalized / away.magnitude;
        }

        return push * separationForce;
    }

    void FaceDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
        {
            SetVisualState(GobboAnimationState.Idle);
            return;
        }

        if (visualController != null)
        {
            visualController.SetAnimationState(GobboAnimationState.Walk);
            visualController.SetDirection(direction);
            return;
        }

        if (directionalSprite != null)
            directionalSprite.SetDirection(direction);
    }

    void SetVisualState(GobboAnimationState state)
    {
        if (visualController != null)
            visualController.SetAnimationState(state);
    }

    void ResetRandomDanceTimer()
    {
        randomDanceTimer = Random.Range(randomDanceMinTime, randomDanceMaxTime);
    }

    void ClearPath()
    {
        currentPath.Clear();
        pathIndex = 0;
    }

    public void SetPlayer(Transform newPlayer)
    {
        player = newPlayer;
        ClearPath();
    }

    public void SetFormationOffset(Vector2 offset)
    {
        formationOffset = offset;
        ClearPath();
    }

    void FindPlayerIfMissing()
    {
        if (player != null)
            return;

        GameObject foundPlayer = GameObject.FindGameObjectWithTag("Player");

        if (foundPlayer != null)
            player = foundPlayer.transform;
    }
}
