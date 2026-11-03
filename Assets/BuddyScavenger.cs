using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BuddyScavenger : MonoBehaviour, ITerrainMovementReserve
{
    [Header("Brain")]
    public bool brainAllowsMovement = false;
    public float scanRange = 4f;
    public float pickupRange = 0.45f;

    [Header("Loyalty")]
    public float maxDistanceFromPlayer = 5f;
    public float abandonFoodDistance = 6f;

    [Header("Layers")]
    public LayerMask foodLayer;

    [Header("Pathing")]
    public float bodyRadius = 0.28f;
    public float TerrainMovementReserve => bodyRadius;
    public float repathTime = 0.35f;
    public float repathDistance = 0.8f;
    public float waypointReachDistance = 0.75f;

    private Rigidbody2D rb;
    private BuddyUnit unit;
    private GobboVisualController visualController;
    private BuddyDirectionalSprite directionalSprite;
    private Transform player;
    private Transform targetFood;
    private readonly List<Vector2> currentPath = new List<Vector2>();
    private int pathIndex = 0;
    private int regionPathRevision = -1;
    private float repathTimer = 0f;
    private Vector2 lastPathGoal;

    Vector2 reachabilityGoal,reachabilityFrom;int reachabilityRevision=-1;float nextReachability;bool cachedReachability,hasReachability;
    Vector2 ActivityOrigin {get {var brain=GetComponent<BuddyBrain>();return brain!=null&&brain.SquadScoped&&!brain.Following?brain.StayAnchor:player!=null?(Vector2)player.position:(Vector2)transform.position;}}

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        unit = GetComponent<BuddyUnit>();
        visualController = GetComponent<GobboVisualController>();
        if (visualController == null)
            visualController = GetComponentInChildren<GobboVisualController>();
        directionalSprite = GetComponent<BuddyDirectionalSprite>();
        FindPlayerIfMissing();
    }

    void Update()
    {
        if (!CanScavenge())
        {
            DropFoodTarget();
            return;
        }

        FindPlayerIfMissing();
        if (player == null) return;

        float distanceFromPlayer = Vector2.Distance(transform.position, ActivityOrigin);
        if (distanceFromPlayer > abandonFoodDistance)
        {
            DropFoodTarget();
            return;
        }

        if (targetFood == null && distanceFromPlayer <= maxDistanceFromPlayer)
            FindFood();

        if (targetFood != null && !IsFoodStillValid(targetFood))
            DropFoodTarget();
    }

    void FixedUpdate()
    {
        using var profile=new Unity.Profiling.ProfilerMarker("Spore.AI.BuddyScavenger").Auto();
        if (!brainAllowsMovement) return;
        if (!CanScavenge() || targetFood == null || player == null) return;
        if(!IsFoodStillValid(targetFood)){DropFoodTarget();return;}

        float distanceFromPlayer = Vector2.Distance(transform.position, ActivityOrigin);
        if (distanceFromPlayer > abandonFoodDistance)
        {
            DropFoodTarget();
            return;
        }

        float distanceToFood = Vector2.Distance(transform.position, targetFood.position);
        if (distanceToFood > pickupRange)
        {
            Vector2 moveDir = GetPathDirection(targetFood.position);
            var brain=GetComponent<BuddyBrain>();var velocity=moveDir*unit.unitData.moveSpeed;
            ActorMotionIntent.Submit(this,rb,brain!=null?brain.BoundVelocity(rb.position,velocity,Time.fixedDeltaTime):velocity,bodyRadius,20);
            FaceDirection(moveDir, GobboAnimationState.Walk);
        }
        else
        {
            PickUpFood();
        }
    }

    public bool WantsControl()
    {
        return CanScavenge() && targetFood != null && IsFoodStillValid(targetFood);
    }

    bool CanScavenge()
    {
        var brain=GetComponent<BuddyBrain>();
        return EnemyDamage.Ready(gameObject)&&(brain==null||brain.SquadAuthorized)&&CombatSessionBinding.AllowsActions(gameObject)&&unit != null && unit.unitData != null && unit.unitData.collectsFood;
    }

    void FindFood()
    {
        if (foodLayer.value == 0) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, scanRange, foodLayer);
        float closest = Mathf.Infinity;
        Transform closestFood = null;

        foreach (Collider2D hit in hits)
        {
            FoodItem food = hit.GetComponent<FoodItem>();
            if (food == null) continue;
            if (!CanReachFood(hit.transform)) continue;

            float dist = Vector2.Distance(transform.position, hit.transform.position);
            if (dist < closest)
            {
                closest = dist;
                closestFood = hit.transform;
            }
        }

        targetFood = closestFood;
        ClearPath();
    }

    bool IsFoodStillValid(Transform food)
    {
        if (food == null || !food.gameObject.activeInHierarchy) return false;
        if (player != null && Vector2.Distance(food.position, ActivityOrigin) > abandonFoodDistance + 2f) return false;
        return CanReachFood(food);
    }

    bool CanReachFood(Transform food)
    {
        if (food == null) return false;
        var brain=GetComponent<BuddyBrain>();if(brain!=null&&!brain.AllowsDestination(food.position))return false;
        var binding=GetComponent<ActorTerrainBinding>();int revision=binding!=null?binding.Revision:RegionQueries.Current?.Revision??-1;
        if(hasReachability&&Time.time<nextReachability&&revision==reachabilityRevision&&Vector2.Distance(food.position,reachabilityGoal)<repathDistance&&Vector2.Distance(rb.position,reachabilityFrom)<repathDistance)return cachedReachability;
        reachabilityRevision=revision;reachabilityGoal=food.position;reachabilityFrom=rb.position;nextReachability=Time.time+Mathf.Max(.05f,repathTime);hasReachability=true;
        cachedReachability=RegionQueries.Sight(gameObject,rb.position,food.position);
        if(!cachedReachability)
        {
            cachedReachability=RegionQueries.Probe(gameObject,rb.position,food.position,out List<Vector2> route)&&route.Count>0;
            if(cachedReachability&&brain!=null&&brain.SquadScoped&&!brain.Following)
                foreach(var point in route)if(!brain.AllowsDestination(point)){cachedReachability=false;break;}
        }
        return cachedReachability;
    }

    Vector2 GetPathDirection(Vector2 targetPos)
    {
        if (RegionQueries.Sight(gameObject,rb.position, targetPos))
        {
            ClearPath();
            return (targetPos - rb.position).normalized;
        }

        var binding=GetComponent<ActorTerrainBinding>();int revision=binding!=null?binding.Revision:RegionQueries.Current?.Revision??-1;
        if(regionPathRevision!=revision&&!(binding!=null&&binding.AcceptsMovementRoute(regionPathRevision))){currentPath.Clear();pathIndex=0;regionPathRevision=revision;}
        repathTimer -= Time.fixedDeltaTime;
        bool needsNewPath = (currentPath.Count == 0 && repathTimer <= 0f) ||
                            (currentPath.Count>0&&pathIndex>=currentPath.Count) ||
                            (repathTimer <= 0f && (binding==null||binding.RequestRoute==null||!binding.AcceptsMovementRoute(regionPathRevision))) ||
                            Vector2.Distance(targetPos, lastPathGoal) > repathDistance;

        if (needsNewPath) RebuildPath(targetPos);

        if (currentPath.Count == 0 || pathIndex >= currentPath.Count) return Vector2.zero;

        pathIndex=RouteWaypointPolicy.Advance(currentPath,pathIndex,rb.position,binding,bodyRadius);
        if(!RouteWaypointPolicy.RecoverTerrainEntry(currentPath,ref pathIndex,ref regionPathRevision,rb.position,targetPos,binding,bodyRadius))return Vector2.zero;
        if(pathIndex>=currentPath.Count)return Vector2.zero;
        Vector2 waypoint = currentPath[pathIndex];
        float waypointTolerance=binding!=null&&binding.RequestRoute!=null?Mathf.Min(waypointReachDistance,binding.Terrain.CellSize*.12f):waypointReachDistance;
        // Regional progression is owned by Advance; never discard a just-recovered entry here.
        if ((binding==null||binding.RequestRoute==null)&&RouteWaypointPolicy.Reached(currentPath,pathIndex,rb.position,binding,bodyRadius,waypointTolerance))
        {
            pathIndex++;
            if (pathIndex >= currentPath.Count) return Vector2.zero;
            waypoint = currentPath[pathIndex];
        }

        var pathDirection=(waypoint-rb.position).normalized;
        return binding!=null&&binding.RequestRoute!=null?pathDirection*Mathf.Clamp01(Vector2.Distance(waypoint,rb.position)/Mathf.Max(.001f,unit.unitData.moveSpeed*Time.fixedDeltaTime)):pathDirection;
    }

    void RebuildPath(Vector2 targetPos)
    {
        repathTimer = repathTime;
        lastPathGoal = targetPos;

        if (RegionQueries.Path(gameObject,rb.position, targetPos, out List<Vector2> newPath))
        {
            currentPath.Clear();
            currentPath.AddRange(newPath);
            pathIndex = 0;
        }
        else
        {
            ClearPath();
        }
    }

    void PickUpFood()
    {
        if (targetFood == null) return;

        FaceDirection((Vector2)targetFood.position - rb.position, GobboAnimationState.Grab);

        FoodItem food = targetFood.GetComponent<FoodItem>();
        var brain=GetComponent<BuddyBrain>();
        GobboController playerGobbo = brain!=null&&brain.SquadScoped?brain.SquadLeader:player!=null?player.GetComponent<GobboController>():null;
        if (food != null && playerGobbo != null)
            food.Eat(playerGobbo);

        DropFoodTarget();
    }

    void FaceDirection(Vector2 direction, GobboAnimationState state)
    {
        if (direction.sqrMagnitude <= 0.001f)
            return;

        if (visualController != null)
        {
            visualController.SetAnimationState(state);
            visualController.SetDirection(direction);
            return;
        }

        if (directionalSprite != null)
            directionalSprite.SetDirection(direction);
    }

    public void InvalidateReachability()=>hasReachability=false;
    void DropFoodTarget()
    {
        targetFood = null;
        ClearPath();
    }

    void ClearPath()
    {
        currentPath.Clear();
        pathIndex = 0;
    }

    void FindPlayerIfMissing()
    {
        if (player != null) return;
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null) player = playerObject.transform;
    }
}
