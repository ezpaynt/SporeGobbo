using UnityEngine;

[RequireComponent(typeof(BuddyFollow))]
[DefaultExecutionOrder(-500)]
public class BuddyBrain : MonoBehaviour
{
    [Header("Task Priority")]
    public bool allowCombat = true;
    public bool allowScavenging = true;
    public bool allowFollowing = true;

    [Header("Debug")]
    public string currentTask = "Follow";

    private BuddyUnit unit;
    private BuddyFollow follow;
    private BuddyCombat combat;
    private BuddyScavenger scavenger;

    // Runtime command recipient only; canonical roster/region remain authoritative elsewhere.
    public GobboController SquadLeader {get;private set;}
    public bool SquadScoped {get;private set;}
    public bool Following {get;private set;}=true;
    public bool Aggressive {get;private set;}=true;
    public Vector2 StayAnchor {get;private set;}
    Rigidbody2D commandBody;
    public float StayRadius {get {var size=TileMover.GetMapClearanceExtents(commandBody,follow!=null?follow.physicalBodyRadius:.28f);return Mathf.Max(1.8f,Mathf.Max(size.x,size.y)*2f);}}
    public bool SquadAuthorized=>!SquadScoped||SquadLeader!=null&&SquadLeader.CanControlBuddy(this);
    public bool CombatPermitted=>allowCombat&&SquadAuthorized&&(!SquadScoped||Aggressive);
    public Vector2 ActivityOrigin=>SquadScoped&&!Following?StayAnchor:SquadLeader!=null?(Vector2)SquadLeader.transform.position:(Vector2)transform.position;
    public void BindSquad(GobboController leader,bool following,bool aggressive)
    {bool fresh=!SquadScoped;SquadLeader=leader;SquadScoped=true;if(fresh)Following=true;ApplyModes(following,aggressive);}
    public void ApplyModes(bool following,bool aggressive)
    {
        if(Following&&!following)StayAnchor=transform.position;
        bool changed=Following!=following;
        bool modesChanged=changed||Aggressive!=aggressive;
        Following=following;Aggressive=aggressive;
        if(changed){follow?.ResetCommandMotion();combat?.InvalidateReachability();scavenger?.InvalidateReachability();}
        if(!aggressive)combat?.CancelEngagement();
        if(modesChanged){GetComponent<ActorTerrainBinding>()?.CancelNavigation();ActorMotionIntent.Cancel(gameObject);}
        UpdateTaskPermissions();
    }
    public bool AllowsDestination(Vector2 position)=>SquadAuthorized&&(!SquadScoped||Following||Vector2.Distance(position,StayAnchor)<=StayRadius);
    public Vector2 BoundVelocity(Vector2 position,Vector2 velocity,float delta)
    {
        if(!SquadAuthorized)return Vector2.zero;
        if(!SquadScoped||Following||delta<=0)return velocity;
        Vector2 offset=position-StayAnchor;
        if(offset.magnitude>StayRadius)
        {Vector2 inward=-offset.normalized;return inward*Mathf.Max(0,Vector2.Dot(velocity,inward));}
        Vector2 next=position+velocity*delta-StayAnchor;
        return (Vector2.ClampMagnitude(next,StayRadius)+StayAnchor-position)/delta;
    }
    public void ReleaseTemporarySquad(GobboController owner)
    {if(SquadLeader!=owner)return;SquadScoped=false;SquadLeader=null;Following=true;Aggressive=true;SuspendCommandMotion();}
    public void SuspendCommandMotion(){GetComponent<ActorTerrainBinding>()?.CancelNavigation();ActorMotionIntent.Cancel(gameObject);combat?.CancelEngagement();follow?.ResetCommandMotion();SetMovementPermissions(false,false,false);}

    void Awake()
    {
        commandBody=GetComponent<Rigidbody2D>();
        unit = GetComponent<BuddyUnit>();
        follow = GetComponent<BuddyFollow>();
        combat = GetComponent<BuddyCombat>();
        scavenger = GetComponent<BuddyScavenger>();
    }

    void Update()
    {
        UpdateTaskPermissions();
    }

    void FixedUpdate()
    {
        UpdateTaskPermissions();
    }

    void UpdateTaskPermissions()
    {
        if(!SquadAuthorized||!CombatSessionBinding.AllowsActions(gameObject)){SuspendCommandMotion();currentTask="Idle";return;}
        bool canCombat = CombatPermitted && combat != null && combat.enabled && combat.WantsControl();
        bool canScavenge = allowScavenging && scavenger != null && scavenger.enabled && scavenger.WantsControl();
        bool canFollow = allowFollowing && follow != null && follow.enabled;

        // Priority: Combat > Scavenge > Follow.
        if (canCombat)
        {
            currentTask = "Combat";
            SetMovementPermissions(false, true, false);
            return;
        }

        if (canScavenge)
        {
            currentTask = "Scavenge";
            SetMovementPermissions(false, false, true);
            return;
        }

        if (canFollow)
        {
            currentTask = "Follow";
            SetMovementPermissions(true, false, false);
            return;
        }

        currentTask = "Idle";
        SetMovementPermissions(false, false, false);
    }

    void SetMovementPermissions(bool followCanMove, bool combatCanMove, bool scavengerCanMove)
    {
        if (follow != null)
            follow.brainAllowsMovement = followCanMove;

        if (combat != null)
            combat.brainAllowsMovement = combatCanMove;

        if (scavenger != null)
            scavenger.brainAllowsMovement = scavengerCanMove;
    }
}
