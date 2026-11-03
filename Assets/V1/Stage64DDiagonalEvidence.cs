#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using SporeGobbo.Input;
using SporeGobbo.Safety;
using SporeGobbo.Geometry;

// Observer for the unchanged owned lifecycle fixture. No input, physics, terrain,
// task, clock, route or gameplay writes. Buffers are exported only after the case.
public sealed class Stage64DDiagonalEvidence : MonoBehaviour
{
    public static Stage64DDiagonalEvidence Active {get;private set;}
    struct Row
    {
        public long Sequence,Query; public double Time,Tick; public int Frame,Case,Cursor,Stage,Keys,RawKeys,Context,Scheme,Revision,Attempts,A,B,C,Reason;
        public Vector2 Position,Velocity,Move,Target,Pointer,Inverse,Aim,DigTarget,Half,Buddy,Extra;
        public float Distance,Radius,Reach,Scale,Timer; public bool DigHeld,DigStarted,Suppressed,Ready,PointerOK,Occupy,Traverse,XClear,YClear;
    }
    struct CellRow {public long Sequence,Query;public double Time,Tick;public int Frame,Case,Stage,X,Y,Revision,Resistance;public bool Solid,Protected,Diggable;public Vector2 Sample;}
    readonly Row[] rows=new Row[24000]; readonly CellRow[] cells=new CellRow[100000];
    int count,cellCount,dropped,cellDropped,expired;long sequence,query,cpuTicks;double begin;
    ExpeditionRegion region;GobboController leader;Rigidbody2D body,buddyBody;Keyboard keyboard;Mouse mouse;
    int leaderBinding,buddyBinding,caseIndex,cursor,keys;Vector2 target,queuedPointer;
    Stage64DNativeMotionEvidence native;bool stopped;
    public static Stage64DDiagonalEvidence Begin(GameObject owner,ExpeditionRegion r,GobboController player,BuddyUnit buddy,Keyboard k,Mouse m)
    {
        if(!Persistence.Isolated||!V1RuntimeProfile.DevelopmentStorage||!V1RuntimeProfile.DevelopmentToolsEnabled||Environment.GetEnvironmentVariable("SPORE_STAGE61_CAPTURE")!="lifecycle")
            throw new InvalidOperationException("Owned lifecycle diagonal observer only");
        if(Active!=null)throw new InvalidOperationException("Duplicate diagonal observer");
        var a=owner.AddComponent<Stage64DDiagonalEvidence>();a.region=r;a.leader=player;a.body=player.GetComponent<Rigidbody2D>();a.buddyBody=buddy.GetComponent<Rigidbody2D>();
        a.leaderBinding=player.GetComponent<ActorTerrainBinding>().GetInstanceID();a.buddyBinding=buddy.GetComponent<ActorTerrainBinding>().GetInstanceID();a.keyboard=k;a.mouse=m;
        a.begin=Time.realtimeSinceStartupAsDouble;a.native=owner.AddComponent<Stage64DNativeMotionEvidence>();
        Active=a;r.State.Committed+=a.Committed;RegionMovementCoordinator.DiagnosticObserver+=a.Decision;return a;
    }
    bool Live=>!stopped&&region!=null&&leader!=null&&body!=null;
    bool Allowed()
    {if(!Live)return false;if(Time.realtimeSinceStartupAsDouble-begin<=150)return true;expired++;return false;}
    Row New(int stage)
    {
        var input=SporeInputReader.Instance;
        return new Row{Sequence=++sequence,Query=query,Time=Time.realtimeSinceStartupAsDouble,Tick=Time.fixedTimeAsDouble,Frame=Time.frameCount,Case=caseIndex,Cursor=cursor,Stage=stage,
            Keys=keys,RawKeys=RawKeys(),Position=body.position,Velocity=body.linearVelocity,Target=target,Distance=Vector2.Distance(body.position,target),Pointer=queuedPointer,
            Context=input!=null?(int)input.Context:-1,Scheme=input!=null?(int)input.ActiveControlScheme:-1,Revision=region.Revision,Attempts=leader.GetComponent<ActorTerrainBinding>().DigAttempts,
            Ready=region.LogicalReady,Scale=Time.timeScale,Buddy=buddyBody!=null?buddyBody.position:Vector2.zero};
    }
    int RawKeys()=>keyboard==null?0:(keyboard.wKey.isPressed?1:0)|(keyboard.aKey.isPressed?2:0)|(keyboard.sKey.isPressed?4:0)|(keyboard.dKey.isPressed?8:0)|(keyboard.leftShiftKey.isPressed?16:0);
    void Add(Row r){if(count<rows.Length)rows[count++]=r;else dropped++;}
    void Cell(int stage,long id,int x,int y,Vector2 sample)
    {
        if(cellCount>=cells.Length){cellDropped++;return;}
        var c=new Vector2Int(x,y);cells[cellCount++]=new CellRow{Sequence=++sequence,Query=id,Time=Time.realtimeSinceStartupAsDouble,Tick=Time.fixedTimeAsDouble,Frame=Time.frameCount,Case=caseIndex,Stage=stage,X=x,Y=y,Sample=sample,
            Revision=region.Revision,Solid=region.State.World.IsSolid(x,y),Protected=region.Definition.Protected(x,y),Diggable=region.IsDiggable(c),Resistance=region.Definition.Resistance(x,y)};
    }
    public void Queued(int test,int waypoint,Vector2 goal,List<Key> queued,Vector2 pointer)
    {
        if(!Allowed())return;long start=Stopwatch.GetTimestamp();caseIndex=test;cursor=waypoint;target=goal;queuedPointer=pointer;keys=0;
        foreach(var key in queued)keys|=key==Key.W?1:key==Key.A?2:key==Key.S?4:key==Key.D?8:key==Key.LeftShift?16:0;
        Add(New(0));cpuTicks+=Stopwatch.GetTimestamp()-start;
    }
    public static void Consumed(GobboController player,Vector2 move,Vector2 aim,SporeInputReader input,bool suppress,float timer)
    {
        var a=Active;if(a==null||a.leader!=player||!a.Allowed())return;long start=Stopwatch.GetTimestamp();var r=a.New(1);r.Move=move;r.Aim=aim;r.Suppressed=suppress;
        r.DigHeld=input!=null&&input.Dig.IsHeld;r.DigStarted=input!=null&&input.Dig.StartedThisFrame;r.Pointer=input!=null?input.AimPointer:Vector2.zero;
        r.PointerOK=input!=null&&ActorTerrainBinding.TryPointer(player.gameObject,input,Camera.main,out r.Inverse);
        r.Extra=input!=null?input.Move:Vector2.zero;r.Timer=timer;r.Radius=player.GetCurrentEffectiveDigRadius();r.Reach=player.digRange;r.DigTarget=r.Position+aim.normalized*r.Reach;
        a.Add(r);a.cpuTicks+=Stopwatch.GetTimestamp()-start;
    }
    public static void DigStarted(GobboController player,Vector2 origin,Vector2 end,float radius)
    {
        var a=Active;if(a==null||a.leader!=player||!a.Allowed())return;long start=Stopwatch.GetTimestamp();var r=a.New(2);r.Position=origin;r.DigTarget=end;r.Radius=radius;r.Reach=player.digRange;r.Aim=player.CurrentAimDirection;a.Add(r);a.cpuTicks+=Stopwatch.GetTimestamp()-start;
    }
    public static long DigQuery(ExpeditionRegion region,Vector2 p,float radius)
        =>DigQuery(region,new PlayerDigFootprint(p,radius));
    public static long DigQuery(ExpeditionRegion region,PlayerDigFootprint footprint)
    {
        var a=Active;if(a==null||a.region!=region||!a.Allowed())return 0;long start=Stopwatch.GetTimestamp();a.query=++a.sequence;var r=a.New(3);r.Query=a.query;r.Position=footprint.Start;r.DigTarget=footprint.End;r.Half=footprint.HalfExtents;r.Radius=footprint.Radius;r.Reach=Vector2.Distance(footprint.Start,footprint.End);a.Add(r);
        if(footprint.Valid&&footprint.Radius<=4&&footprint.HalfExtents.x<=4&&footprint.HalfExtents.y<=4&&r.Reach<=4)
        {foreach(var cell in footprint.Cells(region))a.Cell(0,a.query,cell.x,cell.y,footprint.Start);}
        else a.cellDropped++;
        a.cpuTicks+=Stopwatch.GetTimestamp()-start;return a.query;
    }
    public static void DigResult(ExpeditionRegion region,long id,TerrainDigResult result)
    {
        var a=Active;if(id==0||a==null||a.region!=region||!a.Allowed())return;long start=Stopwatch.GetTimestamp();var r=a.New(4);r.Query=id;r.A=result.EvaluatedCells;r.B=result.EligibleCells;r.C=result.RemovedCells;r.Reason=(int)result.FailureReason;a.Add(r);a.cpuTicks+=Stopwatch.GetTimestamp()-start;
    }
    void Committed(IReadOnlyList<DualTile> affected,int revision)
    {if(!Allowed())return;long start=Stopwatch.GetTimestamp();foreach(var c in affected)Cell(1,query,c.X,c.Y,Vector2.zero);cpuTicks+=Stopwatch.GetTimestamp()-start;}
    public readonly struct MotorScope:IDisposable
    {
        readonly Stage64DDiagonalEvidence observer;readonly int stage;readonly long start;
        public MotorScope(Rigidbody2D rb,Vector2 wanted,float fallback,bool keepOut=false)
        {
            observer=Active;stage=keepOut?7:5;start=0;
            if(observer==null||observer.body!=rb||!observer.Allowed()){observer=null;return;}
            start=Stopwatch.GetTimestamp();var r=observer.New(stage);r.Move=wanted;r.Half=TileMover.GetMapClearanceExtents(rb,fallback);r.Radius=fallback;
            r.Occupy=TileMover.CanOccupyBox(observer.region,rb.position,r.Half);r.Traverse=TileMover.CanTraverseBox(observer.region,rb.position,rb.position+wanted*Time.fixedDeltaTime,r.Half);
            r.XClear=TileMover.CanTraverseBox(observer.region,rb.position,rb.position+Vector2.right*wanted.x*Time.fixedDeltaTime,r.Half);
            r.YClear=TileMover.CanTraverseBox(observer.region,rb.position,rb.position+Vector2.up*wanted.y*Time.fixedDeltaTime,r.Half);observer.Add(r);
            if(!keepOut&&!r.Traverse)observer.BlockedCells(r.Sequence,rb.position,rb.position+wanted*Time.fixedDeltaTime,r.Half);
            observer.cpuTicks+=Stopwatch.GetTimestamp()-start;
        }
        public void Dispose(){if(observer==null||!observer.Live)return;long t=Stopwatch.GetTimestamp();observer.Add(observer.New(stage+1));observer.cpuTicks+=Stopwatch.GetTimestamp()-t;}
    }
    void BlockedCells(long id,Vector2 from,Vector2 to,Vector2 half)
    {
        float spacing=Mathf.Max(.01f,Mathf.Min(region.CellSize*.125f,Mathf.Max(.01f,Mathf.Min(half.x,half.y))*.5f));int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(from,to)/spacing));
        for(int n=0;n<=steps;n++){Vector2 p=Vector2.Lerp(from,to,n/(float)steps);var center=region.WorldToCell(p);int rx=Mathf.CeilToInt((half.x+region.CellSize*.5f)/region.CellSize)+1,ry=Mathf.CeilToInt((half.y+region.CellSize*.5f)/region.CellSize)+1;
            for(int y=center.y-ry;y<=center.y+ry;y++)for(int x=center.x-rx;x<=center.x+rx;x++){var c=new Vector2Int(x,y);var q=region.CellToWorld(c);if(region.IsBlocked(c)&&Mathf.Abs(p.x-q.x)<=half.x+region.CellSize*.5f&&Mathf.Abs(p.y-q.y)<=half.y+region.CellSize*.5f)Cell(2,id,x,y,p);}}
    }
    void Decision(RegionMovementCoordinator.DecisionTrace trace)
    {if(!Allowed()||(trace.Actor!=leaderBinding&&trace.Actor!=buddyBinding))return;native.Applied(trace);}
    public void Export(string directory)
    {
        if(stopped)return;stopped=true;region.State.Committed-=Committed;RegionMovementCoordinator.DiagnosticObserver-=Decision;if(Active==this)Active=null;
        native.Export(directory);
        using(var f=new StreamWriter(Path.Combine(directory,"diagonal-observations.csv")))
        {
            f.WriteLine("sequence,query,time,tick,frame,case,cursor,stage,queuedKeys,rawKeys,context,scheme,revision,attempts,evaluated,eligible,removed,reason,x,y,vx,vy,moveX,moveY,targetX,targetY,pointerX,pointerY,inverseX,inverseY,aimX,aimY,digX,digY,halfX,halfY,buddyX,buddyY,inputMoveX,inputMoveY,distance,radius,reach,timeScale,digTimer,digHeld,digStarted,suppressed,ready,pointerOK,occupy,traverse,xClear,yClear");
            for(int i=0;i<count;i++){var r=rows[i];f.WriteLine(FormattableString.Invariant($"{r.Sequence},{r.Query},{r.Time:R},{r.Tick:R},{r.Frame},{r.Case},{r.Cursor},{r.Stage},{r.Keys},{r.RawKeys},{r.Context},{r.Scheme},{r.Revision},{r.Attempts},{r.A},{r.B},{r.C},{r.Reason},{r.Position.x:R},{r.Position.y:R},{r.Velocity.x:R},{r.Velocity.y:R},{r.Move.x:R},{r.Move.y:R},{r.Target.x:R},{r.Target.y:R},{r.Pointer.x:R},{r.Pointer.y:R},{r.Inverse.x:R},{r.Inverse.y:R},{r.Aim.x:R},{r.Aim.y:R},{r.DigTarget.x:R},{r.DigTarget.y:R},{r.Half.x:R},{r.Half.y:R},{r.Buddy.x:R},{r.Buddy.y:R},{r.Extra.x:R},{r.Extra.y:R},{r.Distance:R},{r.Radius:R},{r.Reach:R},{r.Scale:R},{r.Timer:R},{r.DigHeld},{r.DigStarted},{r.Suppressed},{r.Ready},{r.PointerOK},{r.Occupy},{r.Traverse},{r.XClear},{r.YClear}"));}
        }
        using(var f=new StreamWriter(Path.Combine(directory,"diagonal-cells.csv")))
        {f.WriteLine("sequence,query,time,tick,frame,case,stage,x,y,revision,resistance,solid,protected,diggable,sampleX,sampleY");for(int i=0;i<cellCount;i++){var r=cells[i];f.WriteLine(FormattableString.Invariant($"{r.Sequence},{r.Query},{r.Time:R},{r.Tick:R},{r.Frame},{r.Case},{r.Stage},{r.X},{r.Y},{r.Revision},{r.Resistance},{r.Solid},{r.Protected},{r.Diggable},{r.Sample.x:R},{r.Sample.y:R}"));}}
        File.WriteAllText(Path.Combine(directory,"diagonal-observer-status.txt"),$"rows={count}/24000 dropped={dropped}\ncells={cellCount}/100000 dropped={cellDropped}\nexpiredCalls={expired}; captureLimitSeconds=150\nobserverCpuMs={cpuTicks*1000d/Stopwatch.Frequency:R} (native contact observer excluded; not a performance gate)\nstages: 0=Queued(after original queue calls),1=Consumed(after Gobbo Update actions),2=DigExecuted,3=RegionDigQueryBefore,4=RegionDigResultAfter,5=MotorBefore,6=MotorAfter,7=KeepOutBefore,8=KeepOutAfter\ncellStages: 0=EvaluatedBeforeDig,1=ActualCommittedAfter,2=BlockingBodySweep\nkeyBits: W=1 A=2 S=4 D=8 LeftShift=16\ncontext: {string.Join(",",Enum.GetNames(typeof(SporeInputContext)))}\nscheme: {string.Join(",",Enum.GetNames(typeof(SporeControlScheme)))}\nNo input, body, terrain, routes, coordination, physics settings or task writes. No extra yields; fixed bounded buffers; export after workload. Native fixed-tick observer tracks leader and recruited Buddy only.\n");
    }
    void OnDestroy(){if(region!=null)region.State.Committed-=Committed;RegionMovementCoordinator.DiagnosticObserver-=Decision;if(Active==this)Active=null;}
}
#endif
