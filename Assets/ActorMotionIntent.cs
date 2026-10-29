using UnityEngine;

/// <summary>One late AI movement application. Existing tasks choose intent; TileMover owns clearance.</summary>
[DefaultExecutionOrder(2000)]
public sealed class ActorMotionIntent : MonoBehaviour
{
    Rigidbody2D body;
    ActorTerrainBinding binding;
    GobboVisualController visual;
    MonoBehaviour owner;
    Vector2 velocity;
    float radius;
    double stamp=double.NegativeInfinity,lastApplied=double.NegativeInfinity;
    int priority;
    readonly Collider2D[] neighbours=new Collider2D[32];
    static readonly Unity.Profiling.ProfilerMarker movementProfile=new Unity.Profiling.ProfilerMarker("Spore.AI.Movement");
    public long Applications {get;private set;}
    public long ContestedIntents {get;private set;}
    public bool WaitingForFriendlySpace {get;private set;}
    public Vector2 RequestedVelocity=>owner!=null&&stamp==Time.fixedTimeAsDouble?velocity:Vector2.zero;
    public string ActiveTaskName=>owner!=null&&stamp==Time.fixedTimeAsDouble?owner.GetType().Name:"None";
    public static void Cancel(GameObject actor)
    {var motion=actor.GetComponent<ActorMotionIntent>();if(motion!=null){motion.owner=null;motion.stamp=double.NegativeInfinity;motion.WaitingForFriendlySpace=false;}}
    public static void Submit(MonoBehaviour task,Rigidbody2D body,Vector2 velocity,float radius,int priority)
    {
        var binding=body.GetComponent<ActorTerrainBinding>();
        if(binding==null||binding.RequestRoute==null){TileMover.Move(body,velocity,radius);return;}
        var intent=body.GetComponent<ActorMotionIntent>()??body.gameObject.AddComponent<ActorMotionIntent>();
        if(intent.stamp!=Time.fixedTimeAsDouble){intent.stamp=Time.fixedTimeAsDouble;intent.owner=null;intent.priority=int.MinValue;}
        if(intent.owner!=null&&intent.owner!=task)intent.ContestedIntents++;
        if(intent.owner!=null&&(priority<intent.priority||priority==intent.priority&&task.GetInstanceID()>intent.owner.GetInstanceID()))return;
        intent.owner=task;intent.velocity=velocity;intent.radius=radius;intent.priority=priority;
    }
    void Awake(){body=GetComponent<Rigidbody2D>();binding=GetComponent<ActorTerrainBinding>();visual=GetComponent<GobboVisualController>()??GetComponentInChildren<GobboVisualController>();}
    void FixedUpdate()
    {
        using var movementSample=movementProfile.Auto();
        var player=GetComponent<GobboController>();if(player!=null&&player.enabled)return;
        if(lastApplied==Time.fixedTimeAsDouble)return;lastApplied=Time.fixedTimeAsDouble;
        if(body==null)return;
        if(stamp!=Time.fixedTimeAsDouble||owner==null||!owner.isActiveAndEnabled||binding==null||binding.Ready!=null&&!binding.Ready())
        {body.linearVelocity=Vector2.zero;return;}
        Vector2 refined=priority>=100?velocity:Yield(velocity);
        var brain=GetComponent<BuddyBrain>();if(brain!=null)refined=brain.BoundVelocity(body.position,refined,Time.fixedDeltaTime);
        TileMover.Move(body,refined,radius);Applications++;
        if(priority<100&&refined.sqrMagnitude>.0001f)visual?.SetDirection(refined);
    }
    bool Friendly(GameObject other)
    {
        if(other==gameObject||other.GetComponent<BuddyUnit>()==null&&other.GetComponent<GobboController>()==null)return false;
        var a=GetComponent<CombatSessionBinding>();var b=other.GetComponent<CombatSessionBinding>();
        if(a!=null&&a.Rules!=null||b!=null&&b.Rules!=null)return a!=null&&b!=null&&ReferenceEquals(a.Rules,b.Rules)&&a.Team==b.Team;
        return true;
    }
    Vector2 Yield(Vector2 desired)
    {
        WaitingForFriendlySpace=false;
        float speed=desired.magnitude;if(speed<.001f)return desired;
        Vector2 size=TileMover.GetMapClearanceExtents(body,radius),away=Vector2.zero;bool followingQueue=false;
        int count=Physics2D.OverlapCircleNonAlloc(body.position,size.magnitude+1.5f,neighbours);
        for(int i=0;i<count;i++)
        {
            var collider=neighbours[i];if(collider==null||collider.isTrigger)continue;
            var other=collider.attachedRigidbody;if(other==null||!Friendly(other.gameObject))continue;
            Vector2 delta=body.position-other.position;
            Vector2 otherSize=TileMover.GetMapClearanceExtents(other,.1f),sum=size+otherSize+Vector2.one*.08f;
            float scaled=Mathf.Max(Mathf.Abs(delta.x)/sum.x,Mathf.Abs(delta.y)/sum.y);
            if(scaled>=1.3f)continue;
            var otherMotion=other.GetComponent<ActorMotionIntent>();Vector2 otherIntent=otherMotion!=null?otherMotion.RequestedVelocity:other.linearVelocity;
            if(Vector2.Dot(delta,desired)<0&&otherIntent.sqrMagnitude>.01f&&Vector2.Dot(otherIntent.normalized,desired.normalized)>.5f)followingQueue=true;
            Vector2 direction=delta.sqrMagnitude>.0001f?delta.normalized:(GetInstanceID()<other.GetInstanceID()?Vector2.left:Vector2.right);
            away+=direction*Mathf.Clamp01((1.3f-scaled)/.5f);
        }
        Vector2 candidate=Vector2.ClampMagnitude(desired+away*speed*.7f,speed);
        if(CanMove(candidate,size,count))return candidate;
        if(CanMove(desired*.25f,size,count))return desired*.25f;
        if(followingQueue){WaitingForFriendlySpace=true;return Vector2.zero;}
        Vector2 right=new Vector2(desired.y,-desired.x).normalized*speed*.65f;
        if(CanMove(right,size,count))return right;
        if(CanMove(-right,size,count))return -right;
        WaitingForFriendlySpace=true;return Vector2.zero;
    }
    bool CanMove(Vector2 proposed,Vector2 size,int count)
    {
        Vector2 next=body.position+proposed*Time.fixedDeltaTime;
        if(!TileMover.CanTraverseBox(binding.Terrain,body.position,next,size))return false;
        for(int i=0;i<count;i++)
        {
            var c=neighbours[i];var other=c!=null&&!c.isTrigger?c.attachedRigidbody:null;
            if(other==null||!Friendly(other.gameObject))continue;
            var sum=size+TileMover.GetMapClearanceExtents(other,.1f)+Vector2.one*.01f;
            Vector2 old=body.position-other.position,delta=next-(other.position+other.linearVelocity*Time.fixedDeltaTime);
            float oldGap=Mathf.Max(Mathf.Abs(old.x)/sum.x,Mathf.Abs(old.y)/sum.y);
            float newGap=Mathf.Max(Mathf.Abs(delta.x)/sum.x,Mathf.Abs(delta.y)/sum.y);
            if(newGap>=1)continue;
            float oldArea=Mathf.Max(0,sum.x-Mathf.Abs(old.x))*Mathf.Max(0,sum.y-Mathf.Abs(old.y));
            float nextArea=Mathf.Max(0,sum.x-Mathf.Abs(delta.x))*Mathf.Max(0,sum.y-Mathf.Abs(delta.y));
            if(nextArea<oldArea-.00000001f)continue;
            float depth=Mathf.Min(sum.x-Mathf.Abs(old.x),sum.y-Mathf.Abs(old.y));
            bool shallowContact=oldGap<1&&depth<=Physics2D.defaultContactOffset*2+.01f;
            if(shallowContact&&nextArea<=oldArea+.00000001f&&Vector2.Dot(proposed-other.linearVelocity,old)>=-.000001f)continue;
            return false;
        }
        return true;
    }
    void OnDisable(){owner=null;binding?.CancelNavigation();}
}
