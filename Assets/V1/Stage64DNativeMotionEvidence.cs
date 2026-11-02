#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.LowLevel;

// Owned capture observer only. No physics simulation, sync, body/collider setter,
// gameplay callback, task, goal, path or velocity write occurs in this component.
[DefaultExecutionOrder(32000)]
public sealed class Stage64DNativeMotionEvidence : MonoBehaviour
{
    sealed class Tracked
    {
        public Rigidbody2D Body;public string Key;public double Tick;
        public Vector2 AppliedPosition,AppliedVelocity,Resolved,Preferred,PrePosition,PreVelocity;
        public bool HasPre;
    }
    struct MotionRow
    {
        public int Actor,Frame,Contacts,Constraints,BodyType;public double Tick;
        public string Key,Stage;public Vector2 AppliedPosition,AppliedVelocity,Resolved,Preferred,PrePosition,PreVelocity,Position,Velocity;
        public float Mass,Damping;public bool Simulated,Sleeping;
    }
    struct ContactRow
    {
        public int Actor,A,B,Frame;public double Tick;public Vector2 Point,Normal,RelativeVelocity;
        public float Separation,NormalImpulse,TangentImpulse;public bool Enabled,Ignored,LayerIgnored;
    }
    readonly Dictionary<int,Tracked> tracked=new Dictionary<int,Tracked>();
    readonly Dictionary<int,string> colliders=new Dictionary<int,string>();
    readonly ContactPoint2D[] contactBuffer=new ContactPoint2D[64];
    readonly MotionRow[] motions=new MotionRow[100000];readonly ContactRow[] contacts=new ContactRow[200000];
    int motionCount,contactCount,motionDropped,contactDropped,contactSaturated,actorDropped,metadataDropped;
    bool stopped;
    public int MotionCount=>motionCount;
    void Start(){StartCoroutine(AfterPhysics());}
    public void Applied(RegionMovementCoordinator.DecisionTrace trace)
    {
        if(stopped||trace.Stage!="Applied")return;
        if(!tracked.TryGetValue(trace.Actor,out var actor))
        {
            if(tracked.Count>=128){actorDropped++;return;}
            ActorTerrainBinding binding=null;
            foreach(var candidate in UnityEngine.Object.FindObjectsByType<ActorTerrainBinding>(FindObjectsSortMode.None))
                if(candidate.GetInstanceID()==trace.Actor){binding=candidate;break;}
            if(binding==null)return;var body=binding.GetComponent<Rigidbody2D>();if(body==null)return;
            actor=new Tracked{Body=body,Key=trace.Key};tracked.Add(trace.Actor,actor);
            foreach(var c in body.GetComponentsInChildren<Collider2D>(true))Describe(c);
        }
        actor.Tick=trace.Tick;actor.AppliedPosition=actor.Body.position;actor.AppliedVelocity=actor.Body.linearVelocity;
        actor.Resolved=trace.Resolved;actor.Preferred=trace.Preferred;actor.HasPre=false;
    }
    void FixedUpdate()=>CaptureBeforePhysics();
    public void CaptureBeforePhysics()
    {
        if(stopped)return;
        foreach(var pair in tracked)
        {
            var a=pair.Value;if(a.Body==null||a.Tick!=Time.fixedTimeAsDouble)continue;
            a.PrePosition=a.Body.position;a.PreVelocity=a.Body.linearVelocity;a.HasPre=true;
            AddMotion(pair.Key,a,"LateFixedBeforePhysics",0);
        }
    }
    IEnumerator AfterPhysics()
    {
        var wait=new WaitForFixedUpdate();
        while(!stopped){yield return wait;CaptureAfterPhysics();}
    }
    public void CaptureAfterPhysics()
    {
        if(stopped)return;
        foreach(var pair in tracked)
        {
            var a=pair.Value;if(a.Body==null||!a.HasPre||a.Tick!=Time.fixedTimeAsDouble)continue;
            int count=a.Body.GetContacts(contactBuffer);if(count==contactBuffer.Length)contactSaturated++;
            AddMotion(pair.Key,a,"AfterPhysics",count);
            for(int i=0;i<count;i++)
            {
                var c=contactBuffer[i];Describe(c.collider);Describe(c.otherCollider);
                var row=new ContactRow{Actor=pair.Key,Frame=Time.frameCount,Tick=a.Tick,A=c.collider!=null?c.collider.GetInstanceID():0,B=c.otherCollider!=null?c.otherCollider.GetInstanceID():0,
                    Point=c.point,Normal=c.normal,RelativeVelocity=c.relativeVelocity,Separation=c.separation,NormalImpulse=c.normalImpulse,TangentImpulse=c.tangentImpulse,Enabled=c.enabled,
                    Ignored=c.collider!=null&&c.otherCollider!=null&&Physics2D.GetIgnoreCollision(c.collider,c.otherCollider),
                    LayerIgnored=c.collider!=null&&c.otherCollider!=null&&Physics2D.GetIgnoreLayerCollision(c.collider.gameObject.layer,c.otherCollider.gameObject.layer)};
                if(contactCount<contacts.Length)contacts[contactCount++]=row;else contactDropped++;
            }
        }
    }
    void AddMotion(int id,Tracked a,string stage,int count)
    {
        var b=a.Body;var row=new MotionRow{Actor=id,Frame=Time.frameCount,Tick=a.Tick,Key=a.Key,Stage=stage,Contacts=count,
            AppliedPosition=a.AppliedPosition,AppliedVelocity=a.AppliedVelocity,Resolved=a.Resolved,Preferred=a.Preferred,PrePosition=a.PrePosition,PreVelocity=a.PreVelocity,
            Position=b.position,Velocity=b.linearVelocity,Simulated=b.simulated,Sleeping=b.IsSleeping(),Mass=b.mass,Damping=b.linearDamping,Constraints=(int)b.constraints,BodyType=(int)b.bodyType};
        if(motionCount<motions.Length)motions[motionCount++]=row;else motionDropped++;
    }
    static string Q(string value)=>"\""+(value??"").Replace("\"","\"\"")+"\"";
    void Describe(Collider2D c)
    {
        if(c==null||colliders.ContainsKey(c.GetInstanceID()))return;
        if(colliders.Count>=2048){metadataDropped++;return;}
        var b=c.attachedRigidbody;var bounds=c.bounds;var scale=c.transform.lossyScale;var offset=c.offset;
        string shape=c is CircleCollider2D circle?FormattableString.Invariant($"radius={circle.radius:R}"):
            c is BoxCollider2D box?FormattableString.Invariant($"size={box.size.x:R}/{box.size.y:R};edge={box.edgeRadius:R}"):
            c is CapsuleCollider2D capsule?FormattableString.Invariant($"size={capsule.size.x:R}/{capsule.size.y:R};direction={capsule.direction}"):
            c is PolygonCollider2D polygon?"paths="+polygon.pathCount:c.GetType().Name;
        string path=c.name;for(var p=c.transform.parent;p!=null;p=p.parent)path=p.name+"/"+path;
        bool actor=b!=null&&b.GetComponent<ActorTerrainBinding>()!=null;
        colliders[c.GetInstanceID()]=FormattableString.Invariant($"{c.GetInstanceID()},{Q(path)},{Q(c.gameObject.scene.name)},{Q(c.GetType().Name)},{c.gameObject.layer},{Q(LayerMask.LayerToName(c.gameObject.layer))},{c.isTrigger},{c.enabled},{c.gameObject.activeInHierarchy},{(b!=null?b.GetInstanceID():0)},{(b!=null?(int)b.bodyType:-1)},{actor},{offset.x:R},{offset.y:R},{scale.x:R},{scale.y:R},{bounds.min.x:R},{bounds.min.y:R},{bounds.max.x:R},{bounds.max.y:R},{Q(shape)}");
    }
    public void Export(string directory)
    {
        stopped=true;
        using(var f=new StreamWriter(Path.Combine(directory,"native-motion.csv")))
        {
            f.WriteLine("tick,frame,actor,key,stage,appliedX,appliedY,appliedVx,appliedVy,resolvedX,resolvedY,preferredX,preferredY,preX,preY,preVx,preVy,x,y,vx,vy,contacts,simulated,sleeping,mass,damping,constraints,bodyType");
            for(int i=0;i<motionCount;i++){var r=motions[i];f.WriteLine(FormattableString.Invariant($"{r.Tick:R},{r.Frame},{r.Actor},{r.Key},{r.Stage},{r.AppliedPosition.x:R},{r.AppliedPosition.y:R},{r.AppliedVelocity.x:R},{r.AppliedVelocity.y:R},{r.Resolved.x:R},{r.Resolved.y:R},{r.Preferred.x:R},{r.Preferred.y:R},{r.PrePosition.x:R},{r.PrePosition.y:R},{r.PreVelocity.x:R},{r.PreVelocity.y:R},{r.Position.x:R},{r.Position.y:R},{r.Velocity.x:R},{r.Velocity.y:R},{r.Contacts},{r.Simulated},{r.Sleeping},{r.Mass:R},{r.Damping:R},{r.Constraints},{r.BodyType}"));}
        }
        using(var f=new StreamWriter(Path.Combine(directory,"native-contacts.csv")))
        {
            f.WriteLine("tick,frame,actor,collider,otherCollider,pointX,pointY,normalX,normalY,relativeVx,relativeVy,separation,normalImpulse,tangentImpulse,enabled,ignored,layerIgnored");
            for(int i=0;i<contactCount;i++){var r=contacts[i];f.WriteLine(FormattableString.Invariant($"{r.Tick:R},{r.Frame},{r.Actor},{r.A},{r.B},{r.Point.x:R},{r.Point.y:R},{r.Normal.x:R},{r.Normal.y:R},{r.RelativeVelocity.x:R},{r.RelativeVelocity.y:R},{r.Separation:R},{r.NormalImpulse:R},{r.TangentImpulse:R},{r.Enabled},{r.Ignored},{r.LayerIgnored}"));}
        }
        File.WriteAllLines(Path.Combine(directory,"native-colliders.csv"),ColliderLines());
        File.WriteAllText(Path.Combine(directory,"native-status.json"),JsonUtility.ToJson(new Status{motionCount=motionCount,contactCount=contactCount,motionDropped=motionDropped,contactDropped=contactDropped,contactSaturated=contactSaturated,actorDropped=actorDropped,metadataDropped=metadataDropped,colliders=colliders.Count,simulationMode=Physics2D.simulationMode.ToString(),method="Read only: coordinator Applied, execution-order32000 late FixedUpdate, WaitForFixedUpdate after physics. GetContacts includes native physical contacts; no collision/movement/physics settings or gameplay writes. Fixed buffers; diagnostic overhead is not performance evidence."},true));
        var loop=new List<string>();DescribeLoop(PlayerLoop.GetCurrentPlayerLoop(),loop,0);File.WriteAllLines(Path.Combine(directory,"native-player-loop.txt"),loop);
    }
    IEnumerable<string> ColliderLines(){yield return "collider,path,scene,type,layer,layerName,trigger,enabled,active,body,bodyType,registeredActor,offsetX,offsetY,scaleX,scaleY,minX,minY,maxX,maxY,shape";foreach(var row in colliders.Values)yield return row;}
    static void DescribeLoop(PlayerLoopSystem node,List<string> lines,int depth){lines.Add(new string(' ',depth)+(node.type?.FullName??"root"));if(node.subSystemList!=null)foreach(var child in node.subSystemList)DescribeLoop(child,lines,depth+1);}
    [Serializable]sealed class Status{public int motionCount,contactCount,motionDropped,contactDropped,contactSaturated,actorDropped,metadataDropped,colliders;public string simulationMode,method;}
}
#endif
