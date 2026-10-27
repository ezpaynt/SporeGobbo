using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SporeGobbo.Geometry;
using SporeGobbo.CampCandidate;
using SporeGobbo.CampLifecycle;

/// <summary>Scoped coordinator adapter. Planning, DigRoutine, directed movement and ownership policy are production code.</summary>
public sealed class Stage60Residence
{
    public sealed class Resident
    {
        public PreviewResident Record;public GameObject Actor;public BuddyFollow Follow;public BuddyDigAbility Dig;public CampDirectedWalk Walk;public Transform Target;public readonly List<Collider2D> Ignored=new List<Collider2D>();
    }
    public readonly List<Resident> Residents=new List<Resident>();
    public readonly Stage60Preview Owner;public Stage60ResidenceTerrain Terrain;
    public Resident Worker {get;private set;}public bool Busy=>Worker!=null;
    public string Phase="Idle",Failure="";public int Removed,DigActions,Cancelled,Completed,ZeroDigPreparations;
    public int Speed=0;public bool FinishCurrent,BuildDorm;
    public readonly List<double> PlanSamples=new List<double>();public double LastPlanMs,LastCommitMs;Coroutine routine;
    public Stage60Residence(Stage60Preview owner){Owner=owner;Terrain=new Stage60ResidenceTerrain(owner.Proof.Authority);}
    public Resident Add()
    {
        var record=Owner.Session.AddBuddy();var position=Owner.Proof.Authority.ToWorld(record.Position);
        var go=Owner.Proof.AddScenarioBuddy(position);go.name=record.Id;
        var walk=go.AddComponent<CampDirectedWalk>();walk.destroyWhenDone=false;walk.enableWanderWhenDone=false;walk.enabled=false;walk.bodyRadius=.28f;
        var target=new GameObject(record.Id+" scoped home/walk target");target.transform.SetParent(Owner.transform,false);
        var resident=new Resident{Record=record,Actor=go,Follow=go.GetComponent<BuddyFollow>(),Dig=go.GetComponent<BuddyDigAbility>(),Walk=walk,Target=target.transform};
        // Production directed walks ignore Buddy bodies. Sanitized prefabs omit BuddyUnit; scope that same behavior to these owned actors.
        if(Worker!=null&&Worker.Actor!=null){var collider=go.GetComponent<Collider2D>();Physics2D.IgnoreCollision(Worker.Actor.GetComponent<Collider2D>(),collider,true);Worker.Ignored.Add(collider);}
        Residents.Add(resident);return resident;
    }
    public bool Begin()
    {
        if(Busy||!Owner.RootOpened)return false;
        var worker=Residents.Where(r=>r.Record.Alive&&r.Record.Slot==0&&r.Actor!=null).OrderBy(r=>r.Record.Id,StringComparer.Ordinal).FirstOrDefault();
        if(worker==null||Owner.Session.Established>=10)return false;
        Worker=worker;WorkerCollisions(worker,true);Failure="";routine=Owner.StartCoroutine(Build(worker,Owner.Session.Established+1));return true;
    }
    public void Cancel(string reason)
    {
        if(routine!=null)Owner.StopCoroutine(routine);routine=null;
        if(Worker!=null&&Worker.Actor!=null){Worker.Walk.CancelWalk();Worker.Walk.enabled=false;Worker.Actor.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;Worker.Actor.GetComponent<GobboVisualController>()?.SetAnimationState(GobboAnimationState.Idle);Worker.Follow.enabled=Worker.Record.Alive;}
        if(Worker!=null){WorkerCollisions(Worker,false);Cancelled++;}Worker=null;Phase="Cancelled";Failure=reason;FinishCurrent=false;
    }
    public bool Die()
    {
        var victim=Worker??Residents.FirstOrDefault(r=>r.Record.Alive);if(victim==null)return false;
        if(victim==Worker)Cancel("Worker removed: excavation retained, bed not claimed");
        // The session is the sole local identity/occupancy authority; it vacates and repairs ownership using CampSpatialPolicy.
        Owner.Session.RemoveIsolatedBuddy(victim.Record.Id);Owner.Proof.RemoveScenarioActor(victim.Actor);victim.Actor=null;
        if(victim.Target!=null)UnityEngine.Object.Destroy(victim.Target.gameObject);return true;
    }
    bool Active(Resident r)=>r!=null&&r.Record.Alive&&r.Actor!=null;
    IEnumerator WaitAllowed(Resident r)
    {while(Active(r)&&!Owner.CanSimulate){r.Walk.enabled=false;r.Actor.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;yield return null;}if(Active(r))r.Walk.enabled=true;}
    IEnumerator Move(Resident r,Vector2 destination)
    {
        r.Target.position=destination;r.Walk.enabled=true;r.Walk.BeginWalk(r.Target,3.5f,.08f,30);
        while(Active(r)&&r.Walk.IsWalking){yield return WaitAllowed(r);yield return null;}
        if(Active(r)){r.Walk.enabled=false;if(r.Walk.Result!=CampDirectedWalkResult.Arrived)Failure="Directed walk: "+r.Walk.Result+" / "+r.Walk.BlockingColliderDescription;}
    }
    IEnumerator Build(Resident resident,int slot)
    {
        Phase="Planning bed "+slot;resident.Follow.enabled=false;
        yield return new WaitForFixedUpdate();
        if(!Active(resident)){CompleteRoutine(resident,false);yield break;}
        var definition=Terrain.Catalog.GetSlot(slot);var footprint=Terrain.GetResidentialSlotFootprint(slot);
        long clock=System.Diagnostics.Stopwatch.GetTimestamp();
        // Conservative 3-cell planning clearance; actual Rigidbody scale is unchanged.
        bool planned;ResidentialConstructionPlan plan;string why;
        using(new ActorTerrainBinding.Scope("SG60.ResidentialPlan"))planned=ResidentialConstructionPlan.TryBuild(Terrain,slot,Vector2.one*.43f,resident.Dig.digRadius,out plan,out why,resident.Actor.layer);
        LastPlanMs=SporeGobbo.Geometry.Preview.StreamingGeometry.Elapsed(clock);PlanSamples.Add(LastPlanMs);
        if(!planned){Failure="Production plan: "+why;CompleteRoutine(resident,false);yield break;}
        var approach=Owner.Proof.Authority.Route(resident.Actor.GetComponent<Rigidbody2D>().position,Terrain.CellToWorld(plan.ApproachRoute[0]),Vector2.one*.28f);
        if(approach.Count==0){Failure="No clear route to bedroom staging point";CompleteRoutine(resident,false);yield break;}
        Phase="Approaching bed "+slot;foreach(var point in approach){yield return WaitAllowed(resident);if(!Active(resident))yield break;yield return Move(resident,point);if(Failure!=""){CompleteRoutine(resident,false);yield break;}}
        int required=0,successful=0;
        foreach(var step in plan.DigSteps)
        {
            foreach(var center in step.DigCenters)
            {
                yield return WaitAllowed(resident);if(!Active(resident))yield break;
                // Player may excavate between planning and execution. Open cells are preparation, never a failed/no-op Dig.
                bool remains=false;foreach(var c in footprint)if(Terrain.IsBlocked(c)&&Vector2.Distance(Terrain.CellToWorld(c),Terrain.CellToWorld(center))<=resident.Dig.digRadius+.0001f){remains=true;break;}
                if(!remains)continue;
                Phase="Excavating bed "+slot;required++;TerrainDigResult result=default;
                resident.Dig.digDuration=FinishCurrent?.02f:Speed==2?.025f:Speed==1?.07f:.18f;
                yield return resident.Dig.DigRoutine(Terrain.CellToWorld(center),TerrainDigAuthority.ResidentialProgression,slot,footprint,r=>result=r);
                if(!Active(resident))yield break;
                if(!result.Changed){Failure="Authorized Dig failed: "+result.FailureReason;CompleteRoutine(resident,false);yield break;}
                successful++;Removed+=result.RemovedCells;DigActions++;
            }
            yield return WaitAllowed(resident);if(!Active(resident))yield break;
            Vector2 target=Terrain.CellToWorld(step.AdvanceCell);
            if(!TileMover.CanTraverseBox(Owner.Proof.Authority,resident.Actor.GetComponent<Rigidbody2D>().position,target,Vector2.one*.28f)){Failure="Post-Dig clearance changed";CompleteRoutine(resident,false);yield break;}
            yield return Move(resident,target);if(Failure!=""){CompleteRoutine(resident,false);yield break;}
        }
        if(!Active(resident))yield break;
        int blocked=footprint.Count(Terrain.IsBlocked);
        if(!CampSpatialPolicy.CanCommitResidentialConstruction(required,successful,blocked,true)){Failure="Required footprint is incomplete";CompleteRoutine(resident,false);yield break;}
        // Even fully open rooms use a visible, scaled-time production animation hook before ownership commit.
        if(required==0)ZeroDigPreparations++;
        Phase="Preparing bed "+slot;resident.Actor.GetComponent<GobboVisualController>()?.SetAnimationState(GobboAnimationState.Dig);
        float preparation=0;while(preparation<.8f){yield return WaitAllowed(resident);if(!Active(resident))yield break;preparation+=Time.deltaTime*(FinishCurrent?4:Speed==2?3:Speed==1?2:1);yield return null;}
        resident.Actor.GetComponent<GobboVisualController>()?.SetAnimationState(GobboAnimationState.Idle);
        clock=System.Diagnostics.Stopwatch.GetTimestamp();bool committed;using(new ActorTerrainBinding.Scope("SG60.BedCommit"))committed=Owner.Session.CompleteIsolatedBed(resident.Record.Id,slot,footprint.Select(c=>new DualTile(c.x,c.y)).ToArray(),new Point2(definition.RestCell.x+.5,definition.RestCell.y+.5));LastCommitMs=SporeGobbo.Geometry.Preview.StreamingGeometry.Elapsed(clock);
        if(!committed)Failure="Occupancy or return route refused completion";
        CompleteRoutine(resident,committed);
    }
    void CompleteRoutine(Resident r,bool success)
    {WorkerCollisions(r,false);if(r.Actor!=null){r.Walk.CancelWalk();r.Walk.enabled=false;r.Follow.player=Owner.Proof.Player.transform;if(success){var rest=Terrain.Catalog.GetSlot(r.Record.Slot).RestCell;r.Target.position=Terrain.CellToWorld(new Vector2Int(rest.x,rest.y));r.Follow.player=r.Target;}r.Follow.enabled=r.Record.Alive;}if(success){Completed++;Phase="Bed "+r.Record.Slot+" established";}else Phase="Construction stopped";Worker=null;routine=null;FinishCurrent=false;}
    public void Tick()
    {
        foreach(var r in Residents)if(Active(r))r.Record.Position=Owner.Proof.Authority.ToLogical(r.Actor.GetComponent<Rigidbody2D>().position);
        if(BuildDorm&&Owner.CanSimulate&&!Busy&&Owner.RootOpened)
        {if(Owner.Session.Established>=10){BuildDorm=false;return;}if(Owner.Session.Living<=Owner.Session.Established)Add();if(!Begin())BuildDorm=false;}
    }
    void WorkerCollisions(Resident r,bool ignore)
    {
        if(r.Actor==null)return;var body=r.Actor.GetComponent<Collider2D>();
        if(ignore){foreach(var other in Residents)if(other!=r&&other.Actor!=null){var c=other.Actor.GetComponent<Collider2D>();Physics2D.IgnoreCollision(body,c,true);r.Ignored.Add(c);}}
        else {foreach(var c in r.Ignored)if(c!=null)Physics2D.IgnoreCollision(body,c,false);r.Ignored.Clear();}
    }
    public void CallToFire()
    {foreach(var r in Residents)if(r.Record.Alive&&r.Actor!=null&&r!=Worker){r.Follow.player=Owner.Proof.Player.transform;r.Follow.enabled=true;}}
    public void SendHome(Resident r)
    {if(r==null||r.Actor==null||r.Record.Slot<=0||r==Worker)return;var p=Terrain.Catalog.GetSlot(r.Record.Slot).RestCell;r.Target.position=Terrain.CellToWorld(new Vector2Int(p.x,p.y));r.Follow.player=r.Target;r.Follow.enabled=true;}
    public void PreDigNext(double fraction)
    {
        int slot=Owner.Session.Established+1;if(slot>10||!Owner.RootOpened)return;
        var cells=Terrain.GetResidentialSlotFootprint(slot).Where(Terrain.IsBlocked).ToList();int n=(int)Math.Ceiling(cells.Count*fraction);
        Owner.Session.DigIsolatedResidence(cells.Take(n).Select(c=>new DualTile(c.x,c.y)),TerrainDigAuthority.Player);
    }
    public void Dispose()
    {Cancel("Reset / disposed");foreach(var r in Residents){if(r.Actor!=null)Owner.Proof.RemoveScenarioActor(r.Actor);if(r.Target!=null)UnityEngine.Object.Destroy(r.Target.gameObject);}Residents.Clear();}
}
