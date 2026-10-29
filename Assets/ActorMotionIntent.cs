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
    int priority;bool friendlyConstraint;
    Vector2 yieldGoal;float yieldRemaining;Rigidbody2D yieldRequester;
    readonly Collider2D[] neighbours=new Collider2D[32];
    readonly RaycastHit2D[] detourHits=new RaycastHit2D[32];
    Rigidbody2D lastFriendlyBlocker,detourBody;
    Vector2 detourOrigin,detourNear,detourFar,detourHeading,detourSize;
    int detourLeg;float detourElapsed;
    static readonly Unity.Profiling.ProfilerMarker movementProfile=new Unity.Profiling.ProfilerMarker("Spore.AI.Movement");
    public long Applications {get;private set;}
    public double FriendlyDelaySeconds {get;private set;}
    public long ContestedIntents {get;private set;}
    public bool WaitingForFriendlySpace {get;private set;}
    public Vector2 RequestedVelocity=>owner!=null&&stamp==Time.fixedTimeAsDouble?velocity:Vector2.zero;
    public string ActiveTaskName=>owner!=null&&stamp==Time.fixedTimeAsDouble?owner.GetType().Name:"None";
    public static void Cancel(GameObject actor)
    {var motion=actor.GetComponent<ActorMotionIntent>();if(motion!=null){motion.owner=null;motion.stamp=double.NegativeInfinity;motion.WaitingForFriendlySpace=false;motion.ClearDetour();motion.yieldRemaining=0;motion.yieldRequester=null;}}
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
        bool hasIntent=stamp==Time.fixedTimeAsDouble&&owner!=null&&owner.isActiveAndEnabled;
        if(!hasIntent&&binding!=null&&(binding.Ready==null||binding.Ready())&&ApplyIdleYield())return;
        if(stamp!=Time.fixedTimeAsDouble||owner==null||!owner.isActiveAndEnabled||binding==null||binding.Ready!=null&&!binding.Ready())
        {body.linearVelocity=Vector2.zero;WaitingForFriendlySpace=false;ClearDetour();return;}
        if(priority>=100){ClearDetour();WaitingForFriendlySpace=false;}
        friendlyConstraint=false;
        Vector2 refined=priority>=100?velocity:Yield(velocity);
        if(friendlyConstraint&&velocity.sqrMagnitude>.000001f)FriendlyDelaySeconds+=FriendlyDelayFraction(velocity,refined)*Time.fixedDeltaTime;
        var brain=GetComponent<BuddyBrain>();if(brain!=null)refined=brain.BoundVelocity(body.position,refined,Time.fixedDeltaTime);
        TileMover.Move(body,refined,radius);Applications++;
        if(priority<100&&refined.sqrMagnitude>.0001f)visual?.SetDirection(refined);
    }
    public static float FriendlyDelayFraction(Vector2 requested,Vector2 applied)
        =>requested.sqrMagnitude<=.000001f?0:Mathf.Clamp01(1-Vector2.Dot(applied,requested)/requested.sqrMagnitude);
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
        float speed=desired.magnitude;if(speed<.001f){ClearDetour();return desired;}
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
            friendlyConstraint=true;
            var otherMotion=other.GetComponent<ActorMotionIntent>();Vector2 otherIntent=otherMotion!=null?otherMotion.RequestedVelocity:other.linearVelocity;
            if(Vector2.Dot(delta,desired)<0&&otherIntent.sqrMagnitude>.01f&&Vector2.Dot(otherIntent.normalized,desired.normalized)>.5f)followingQueue=true;
            Vector2 direction=delta.sqrMagnitude>.0001f?delta.normalized:(GetInstanceID()<other.GetInstanceID()?Vector2.left:Vector2.right);
            away+=direction*Mathf.Clamp01((1.3f-scaled)/.5f);
        }
        // Preserve TileMover's axis sliding when the diagonal alone clips a
        // friendly corner. Larger detours are for cases where neither axis fits.
        if(!CanMove(desired,size,count)&&lastFriendlyBlocker!=null)
        {
            Vector2 a=new Vector2(desired.x,0),b=new Vector2(0,desired.y);
            if(b.sqrMagnitude>a.sqrMagnitude){var swap=a;a=b;b=swap;}
            if(a.sqrMagnitude>=desired.sqrMagnitude*.0625f&&CanMove(a,size,count)){ClearDetour();return a;}
            if(b.sqrMagnitude>=desired.sqrMagnitude*.0625f&&CanMove(b,size,count)){ClearDetour();return b;}
        }
        if(detourBody!=null)friendlyConstraint=true;
        if(detourBody!=null&&Detour(desired,size,count,out var detour))return detour;
        if(!CanMove(desired,size,count)&&lastFriendlyBlocker!=null&&priority>=50)
        {
            var blocker=lastFriendlyBlocker;
            var idle=blocker.GetComponent<ActorMotionIntent>()??blocker.gameObject.AddComponent<ActorMotionIntent>();
            idle.RequestIdleYield(body,desired);
        }
        if(!CanMove(desired,size,count)&&lastFriendlyBlocker!=null)
            for(int i=0;i<count;i++)
            {
                var c=neighbours[i];var other=c!=null&&!c.isTrigger?c.attachedRigidbody:null;
                if(other==null||!Friendly(other.gameObject))continue;
                var sum=size+TileMover.GetMapClearanceExtents(other,.1f)+Vector2.one*.01f;
                var delta=body.position+desired*Time.fixedDeltaTime-other.position;
                if(Mathf.Abs(delta.x)>=sum.x||Mathf.Abs(delta.y)>=sum.y||!BeginDetour(other,desired,size))continue;
                if(Detour(desired,size,count,out detour))return detour;
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
        lastFriendlyBlocker=null;
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
            lastFriendlyBlocker=other;return false;
        }
        return true;
    }
    // A stationary friendly can clear an exact construction approach without a
    // collision exception or a second movement writer. Moving duties keep priority.
    bool RequestIdleYield(Rigidbody2D requester,Vector2 approach)
    {
        if(yieldRemaining>0)return yieldRequester==requester;
        if(body==null)Awake();
        if(body==null||binding==null||binding.RequestRoute==null||!Friendly(requester.gameObject)||GetComponent<BuddyUnit>()==null)return false;
        if(GetComponent<GobboController>()?.enabled==true||GetComponent<CampDirectedWalk>()?.IsWalking==true||GetComponent<CombatSessionBinding>()?.Rules!=null)return false;
        if(RequestedVelocity.sqrMagnitude>.001f||body.linearVelocity.sqrMagnitude>.04f)return false;
        var size=TileMover.GetMapClearanceExtents(body,.1f);var requesterSize=TileMover.GetMapClearanceExtents(requester,.1f);
        Vector2 right=new Vector2(approach.y,-approach.x).normalized;
        // Share the passing clearance: the approaching actor also refines its path.
        float distance=.5f*Mathf.Max(size.x+requesterSize.x,size.y+requesterSize.y)+.06f;
        for(int option=0;option<3;option++)
        {
            Vector2 step=option==0?right:option==1?-right:approach.normalized;
            var target=body.position+step*distance;
            if(!TileMover.CanTraverseBox(binding.Terrain,body.position,target,size)||!DetourLegClear(body.position,target,size,requester))continue;
            yieldGoal=target;yieldRemaining=1.5f;yieldRequester=requester;return true;
        }
        return false;
    }
    bool ApplyIdleYield()
    {
        if(yieldRemaining<=0||yieldRequester==null)return false;
        var input=SporeGobbo.Input.SporeInputReader.Instance;
        if(input!=null&&input.Context!=SporeGobbo.Input.SporeInputContext.Gameplay)return false;
        yieldRemaining-=Time.fixedDeltaTime;
        var offset=yieldGoal-body.position;
        if(offset.magnitude<.04f){yieldRemaining=0;yieldRequester=null;return false;}
        var size=TileMover.GetMapClearanceExtents(body,.1f);
        int count=Physics2D.OverlapCircleNonAlloc(body.position,size.magnitude+1.5f,neighbours);
        var proposed=offset.normalized*Mathf.Min(1.6f,offset.magnitude/Time.fixedDeltaTime);
        if(!CanMove(proposed,size,count)){body.linearVelocity=Vector2.zero;return true;}
        TileMover.Move(body,proposed,.1f);Applications++;visual?.SetDirection(proposed);return true;
    }
    void ClearDetour(){detourBody=null;detourLeg=0;detourElapsed=0;}
    bool BeginDetour(Rigidbody2D other,Vector2 desired,Vector2 size)
    {
        var intent=other.GetComponent<ActorMotionIntent>();var wanted=intent!=null?intent.RequestedVelocity:other.linearVelocity;
        bool counterflow=wanted.sqrMagnitude>.04f&&Vector2.Dot(wanted.normalized,desired.normalized)<-.25f;
        if((other.linearVelocity.sqrMagnitude>.04f||wanted.sqrMagnitude>.04f)&&!counterflow||Vector2.Dot(other.position-body.position,desired)<=0)return false;
        var sum=size+TileMover.GetMapClearanceExtents(other,.1f)+Vector2.one*.06f;
        bool horizontal=Mathf.Abs(desired.x)>=Mathf.Abs(desired.y);float facing=horizontal?Mathf.Sign(body.position.x-other.position.x):Mathf.Sign(body.position.y-other.position.y);
        if(facing==0)facing=horizontal?-Mathf.Sign(desired.x):-Mathf.Sign(desired.y);
        float best=float.PositiveInfinity;Vector2 near=default,far=default;
        int first=(GetInstanceID()&1)==0?1:-1;
        if(counterflow)first=horizontal?(desired.x<0?1:-1):(desired.y>0?1:-1);
        for(int n=0;n<2;n++)
        {
            int side=n==0?first:-first;
            Vector2 a=other.position+(horizontal?new Vector2(facing*sum.x,side*sum.y):new Vector2(side*sum.x,facing*sum.y));
            Vector2 b=other.position+(horizontal?new Vector2(-facing*sum.x,side*sum.y):new Vector2(side*sum.x,-facing*sum.y));
            if(!TileMover.CanTraverseBox(binding.Terrain,body.position,a,size)||!TileMover.CanTraverseBox(binding.Terrain,a,b,size))continue;
            if(!DetourLegClear(body.position,a,size,other)||!DetourLegClear(a,b,size,other))continue;
            float distance=Vector2.Distance(body.position,a)+Vector2.Distance(a,b);if(distance>=best)continue;best=distance;near=a;far=b;
            if(counterflow)break; // Consistent passing side, rather than reciprocal side switching.
        }
        if(float.IsPositiveInfinity(best))return false;
        detourBody=other;detourOrigin=other.position;detourNear=near;detourFar=far;detourHeading=desired.normalized;detourSize=size;detourLeg=0;detourElapsed=0;return true;
    }
    bool DetourLegClear(Vector2 from,Vector2 to,Vector2 size,Rigidbody2D around)
    {
        var offset=to-from;int count=Physics2D.BoxCastNonAlloc(from,size*2,0,offset.normalized,detourHits,offset.magnitude);
        if(count==detourHits.Length)return false;
        for(int i=0;i<count;i++)
        {
            var c=detourHits[i].collider;var other=c!=null&&!c.isTrigger?c.attachedRigidbody:null;
            if(other!=null&&other!=body&&other!=around&&other.bodyType!=RigidbodyType2D.Static)return false;
        }
        return true;
    }
    bool Detour(Vector2 desired,Vector2 size,int count,out Vector2 result)
    {
        result=Vector2.zero;detourElapsed+=Time.fixedDeltaTime;
        if(detourBody==null||!detourBody.gameObject.activeInHierarchy||!Friendly(detourBody.gameObject)||(detourBody.position-detourOrigin).sqrMagnitude>.0625f||(size-detourSize).sqrMagnitude>.000001f||Vector2.Dot(desired.normalized,detourHeading)<.4f||detourElapsed>3)
        {ClearDetour();return false;}
        var target=detourLeg==0?detourNear:detourFar;
        if(Vector2.Distance(body.position,target)<.045f){if(detourLeg==0){detourLeg=1;target=detourFar;}else{ClearDetour();return false;}}
        Vector2 offset=target-body.position;var proposed=offset.normalized*Mathf.Min(desired.magnitude,offset.magnitude/Time.fixedDeltaTime);
        if(CanMove(proposed,size,count)){result=proposed;return true;}
        WaitingForFriendlySpace=true;return true;
    }
    void OnDisable(){owner=null;yieldRemaining=0;yieldRequester=null;ClearDetour();binding?.CancelNavigation();}
}
