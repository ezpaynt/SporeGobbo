using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BuddyCombat : MonoBehaviour
{
    [Header("Brain")]
    public bool brainAllowsMovement = false;

    [Header("Targeting")]
    public LayerMask enemyLayer;
    public float aggroRange = 6f;
    public float attackRange = 0.75f;
    public bool requireLineOfSightToTarget = true;
    public bool requireReachableTarget = true;

    [Header("Attack")]
    public int damage = 1;
    public float attackCooldown = 0.8f;
    public float chaseSpeed = 4f;
    public float selfKnockbackForce = 2f;
    public float enemyKnockbackForce = 3f;
    public float selfBounceDuration = 0.12f;

    [Header("Behavior")]
    public bool onlyFightNearPlayer = true;
    public float maxDistanceFromPlayerToFight = 7f;

    [Header("Pathing")]
    public float bodyRadius = 0.28f;
    public float repathTime = 0.25f;
    public float repathDistance = 0.8f;
    public float waypointReachDistance = 0.75f;

    private readonly List<Collider2D> contactSource=new List<Collider2D>(),contactTarget=new List<Collider2D>();
    private Rigidbody2D rb;
    private Transform player;
    private Transform currentTarget;
    private float attackTimer = 0f;
    private readonly List<Vector2> currentPath = new List<Vector2>();
    private int pathIndex = 0;
    private int regionPathRevision = -1;
    private float repathTimer = 0f;
    private Vector2 lastPathGoal;
    private float bounceTimer = 0f;
    private Vector2 bounceVelocity;
    private BuddyUnit unit;
    private GobboVisualController visualController;
    private BuddyDirectionalSprite directionalSprite;

    Vector2 reachabilityGoal,reachabilityFrom;int reachabilityRevision=-1;float nextReachability;bool cachedReachability,hasReachability;

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
        using var decisionProfile=new Unity.Profiling.ProfilerMarker("Spore.AI.BuddyDecision").Auto();
        attackTimer -= Time.deltaTime;
        FindPlayerIfMissing();

        if (!AllowedToFight())
        {
            ClearTarget();
            return;
        }

        FindTarget();
    }

    void FixedUpdate()
    {
        using var profile=new Unity.Profiling.ProfilerMarker("Spore.AI.BuddyCombat").Auto();
        if (!brainAllowsMovement) return;
        if (!AllowedToFight() || currentTarget == null) return;

        if (bounceTimer > 0f)
        {
            bounceTimer -= Time.fixedDeltaTime;
            ActorMotionIntent.Submit(this,rb,BoundMotion(bounceVelocity),bodyRadius,100);
            FaceDirection(bounceVelocity, GobboAnimationState.Walk);
            return;
        }

        if (!IsStillValidTarget(currentTarget))
        {
            ClearTarget();
            return;
        }

        if (!IsInAttackRange()) MoveTowardTarget();
        else TryAttack();
    }

    public bool WantsControl()
    {
        return AllowedToFight() && currentTarget != null && IsStillValidTarget(currentTarget);
    }

    bool AllowedToFight()
    {
        var brain=GetComponent<BuddyBrain>();
        if(brain!=null&&!brain.CombatPermitted)return false;
        if(!CombatSessionBinding.AllowsActions(gameObject)||!EnemyDamage.Ready(gameObject))return false;
        if (unit == null || unit.unitData == null) return true;
        if (unit.unitData.onlyFightsAfterHit && !unit.unitData.hasBeenHit) return false;
        return true;
    }

    void FindTarget()
    {
        if (enemyLayer.value == 0) return;
        if (currentTarget != null && IsStillValidTarget(currentTarget)) return;

        currentTarget = null;
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, aggroRange, enemyLayer);
        float closestDistance = Mathf.Infinity;
        Transform closestEnemy = null;

        foreach (Collider2D hit in hits)
        {
            if (!hit.gameObject.activeInHierarchy) continue;

            Component recipient=EnemyDamage.Resolve(hit.gameObject);
            if(!EnemyDamage.Eligible(gameObject,recipient))continue;
            Transform possibleTarget = recipient.transform;
            if (!IsTargetAllowedByPlayerDistance(possibleTarget)) continue;
            if (!CanSeeOrPathTo(possibleTarget.position)) continue;

            float distance = Vector2.Distance(transform.position, possibleTarget.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEnemy = possibleTarget;
            }
        }

        currentTarget = closestEnemy;
        if (currentTarget != null) ClearPath();
    }

    bool IsStillValidTarget(Transform target)
    {
        if (target == null || !EnemyDamage.Eligible(gameObject,EnemyDamage.Resolve(target.gameObject))) return false;
        if (Vector2.Distance(transform.position, target.position) > aggroRange + 1.5f) return false;
        if (!IsTargetAllowedByPlayerDistance(target)) return false;
        return CanSeeOrPathTo(target.position);
    }

    bool IsTargetAllowedByPlayerDistance(Transform target)
    {
        var brain=GetComponent<BuddyBrain>();
        if(brain!=null&&!brain.AllowsDestination(target.position))return false;
        if(brain!=null&&brain.SquadScoped&&!brain.Following)return true;
        if (!onlyFightNearPlayer || player == null) return true;

        float buddyDistanceFromPlayer = Vector2.Distance(transform.position, player.position);
        float targetDistanceFromPlayer = Vector2.Distance(target.position, player.position);
        return buddyDistanceFromPlayer <= maxDistanceFromPlayerToFight &&
               targetDistanceFromPlayer <= maxDistanceFromPlayerToFight + 2f;
    }

    bool CanSeeOrPathTo(Vector2 targetPos)
    {
        var binding=GetComponent<ActorTerrainBinding>();int revision=binding!=null?binding.Revision:RegionQueries.Current?.Revision??-1;
        if(hasReachability&&Time.time<nextReachability&&revision==reachabilityRevision&&Vector2.Distance(targetPos,reachabilityGoal)<repathDistance&&Vector2.Distance(rb.position,reachabilityFrom)<repathDistance)return cachedReachability;
        reachabilityRevision=revision;reachabilityGoal=targetPos;reachabilityFrom=rb.position;nextReachability=Time.time+Mathf.Max(.05f,repathTime);hasReachability=true;
        cachedReachability=RegionQueries.Sight(gameObject,rb.position,targetPos);
        if(!cachedReachability&&!requireReachableTarget)cachedReachability=!requireLineOfSightToTarget;
        if(!cachedReachability&&requireReachableTarget)
        {
            cachedReachability=RegionQueries.Probe(gameObject,rb.position,targetPos,out List<Vector2> route)&&route.Count>0;
            if(binding!=null&&binding.ProbeStatus==SporeGobbo.Geometry.GridRouteStatus.Pending){cachedReachability=true;return true;}
            var brain=GetComponent<BuddyBrain>();
            if(cachedReachability&&brain!=null&&brain.SquadScoped&&!brain.Following)
                foreach(var point in route)if(!brain.AllowsDestination(point)){cachedReachability=false;break;}
        }
        return cachedReachability;
    }

    void MoveTowardTarget()
    {
        if (currentTarget == null) return;

        Vector2 targetPos = currentTarget.position;
        var binding=GetComponent<ActorTerrainBinding>();
        if(binding!=null&&binding.ChooseDestination!=null){var targetBody=EnemyDamage.Resolve(currentTarget.gameObject)?.GetComponent<Rigidbody2D>();targetPos=binding.MovementGoal(currentTarget.GetInstanceID(),targetPos,TileMover.GetMapClearanceExtents(targetBody,.25f),true);}
        Vector2 moveDir = GetPathDirection(targetPos);
        ActorMotionIntent.Submit(this,rb,BoundMotion(moveDir*chaseSpeed),bodyRadius,30);
        FaceDirection(moveDir, GobboAnimationState.Walk);
    }

    Vector2 GetPathDirection(Vector2 targetPos)
    {
        if (RegionQueries.Sight(gameObject,rb.position, targetPos))
        {
            ClearPath();
            return (targetPos - rb.position).normalized;
        }

        var binding=GetComponent<ActorTerrainBinding>();int revision=binding!=null?binding.Revision:RegionQueries.Current?.Revision??-1;
        if(regionPathRevision!=revision && !(binding!=null&&binding.AcceptsMovementRoute(regionPathRevision))){currentPath.Clear();pathIndex=0;regionPathRevision=revision;}
        repathTimer -= Time.fixedDeltaTime;
        bool needsNewPath =
            (currentPath.Count == 0 && repathTimer <= 0f) ||
                            (currentPath.Count > 0 && pathIndex >= currentPath.Count) ||
                            (repathTimer <= 0f && (binding==null||binding.RequestRoute==null||!binding.AcceptsMovementRoute(regionPathRevision))) ||
                            Vector2.Distance(targetPos, lastPathGoal) > repathDistance;

        if (needsNewPath) RebuildPath(targetPos);

        if (currentPath.Count == 0 || pathIndex >= currentPath.Count) return Vector2.zero;

        pathIndex=RouteWaypointPolicy.Advance(currentPath,pathIndex,rb.position,binding,bodyRadius);
        if(pathIndex>=currentPath.Count)return Vector2.zero;
        Vector2 waypoint = currentPath[pathIndex];
        float waypointTolerance=binding!=null&&binding.RequestRoute!=null?Mathf.Min(waypointReachDistance,binding.Terrain.CellSize*.12f):waypointReachDistance;
        if (Vector2.Distance(rb.position, waypoint) <= waypointTolerance)
        {
            pathIndex++;
            if (pathIndex >= currentPath.Count) return Vector2.zero;
            waypoint = currentPath[pathIndex];
        }

        var pathDirection=(waypoint-rb.position).normalized;
        return binding!=null&&binding.RequestRoute!=null?pathDirection*Mathf.Clamp01(Vector2.Distance(waypoint,rb.position)/Mathf.Max(.001f,chaseSpeed*Time.fixedDeltaTime)):pathDirection;
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

    // Preserve nominal centre reach. Larger physical bodies must also be able to bite
    // at contact; sensors, nested actors and detached bodies cannot extend that reach.
    bool IsInAttackRange()
    {
        if(currentTarget==null)return false;
        if(Vector2.Distance(transform.position,currentTarget.position)<=attackRange)return true;
        var owner=EnemyDamage.Resolve(currentTarget.gameObject);if(owner==null)return false;
        GetComponentsInChildren<Collider2D>(false,contactSource);owner.GetComponentsInChildren<Collider2D>(false,contactTarget);
        var targetBody=owner.GetComponent<Rigidbody2D>();float tolerance=Physics2D.defaultContactOffset*2f+.001f;
        foreach(var a in contactSource)
        {
            if(!a.enabled||a.isTrigger||a.attachedRigidbody!=rb||EnemyDamage.Resolve(a.gameObject)?.gameObject!=gameObject)continue;
            foreach(var b in contactTarget)
            {
                if(!b.enabled||b.isTrigger||b.attachedRigidbody!=targetBody||EnemyDamage.Resolve(b.gameObject)!=owner)continue;
                var distance=a.Distance(b);if(distance.isValid&&distance.distance<=tolerance)return true;
            }
        }
        return false;
    }

    void TryAttack()
    {
        rb.linearVelocity = Vector2.zero;
        if (!AllowedToFight()||attackTimer > 0f || currentTarget == null||!IsStillValidTarget(currentTarget)||!IsInAttackRange()) return;
        if (!RegionQueries.Sight(gameObject,rb.position, currentTarget.position)) return;

        attackTimer = attackCooldown;
        Vector2 directionToEnemy = ((Vector2)currentTarget.position - rb.position).normalized;
        FaceDirection(directionToEnemy, GobboAnimationState.Attack);

        string id=unit?.unitData?.uniqueId??"";
        var result=new MeleeAttackExecution(gameObject).Commit(currentTarget.gameObject,damage,id);
        if(result.Accepted&&CombatSessionBinding.AllowsActions(gameObject))
        {
            KnockEnemy(result.Target.gameObject,directionToEnemy);
            StartSelfBounce(-directionToEnemy);
        }
    }

    void KnockEnemy(GameObject enemy, Vector2 direction)
    {
        Rigidbody2D enemyRb = enemy.GetComponent<Rigidbody2D>();
        if (enemyRb != null) enemyRb.AddForce(direction * enemyKnockbackForce, ForceMode2D.Impulse);
    }

    void StartSelfBounce(Vector2 direction)
    {
        bounceVelocity = direction.normalized * selfKnockbackForce;
        bounceTimer = selfBounceDuration;
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

    public System.Action BeginTemporaryCombat()
    {float prior=attackTimer;CancelEngagement();attackTimer=0;return ()=>{CancelEngagement();attackTimer=prior;};}
    public void InvalidateReachability()=>hasReachability=false;
    public void CancelEngagement()
    {ClearTarget();hasReachability=false;bounceTimer=0;bounceVelocity=Vector2.zero;brainAllowsMovement=false;if(rb!=null)rb.linearVelocity=Vector2.zero;}
    Vector2 BoundMotion(Vector2 velocity)
    {var brain=GetComponent<BuddyBrain>();return brain!=null?brain.BoundVelocity(rb.position,velocity,Time.fixedDeltaTime):velocity;}

    void ClearTarget()
    {
        currentTarget = null;
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
        GameObject foundPlayer = GameObject.FindGameObjectWithTag("Player");
        if (foundPlayer != null) player = foundPlayer.transform;
    }

    public void SetPlayer(Transform newPlayer)
    {
        player = newPlayer;
    }

    public void SetTarget(Transform target)
    {
        var receiver=EnemyDamage.Resolve(target!=null?target.gameObject:null);
        currentTarget=AllowedToFight()&&EnemyDamage.Eligible(gameObject,receiver)&&IsTargetAllowedByPlayerDistance(receiver.transform)?receiver.transform:null;
        ClearPath();
    }
}
