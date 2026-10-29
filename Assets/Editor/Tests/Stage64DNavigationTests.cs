#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SporeGobbo.Geometry;
using SporeGobbo.CampLifecycle;

/// <summary>Pure searches and owned query adapters only. No campaign, scene loading, prefs or file I/O.</summary>
public sealed class Stage64DNavigationTests
{
    sealed class Fixture:ICompleteTerrainDefinition,IDiggableTerrain,IBlockingTerrainDefinition
    {
        public readonly HashSet<int> Walls=new HashSet<int>();public RegionTerrainState State;
        public readonly int Size;
        public Fixture(int size=32){Size=size;State=new RegionTerrainState("owned navigation fixture",Size,Size,this);}
        public float CellSize=>.6f;
        public bool Protected(int x,int y)=>x<=0||y<=0||x>=Size-1||y>=Size-1;
        public bool PlacementAllowed(int x,int y)=>!Protected(x,y);
        public bool TrySolid(int x,int y,out bool solid){solid=Protected(x,y)||Walls.Contains(y*Size+x);return true;}
        public bool IsBlocked(Vector2Int p)=>State.World.IsSolid(p.x,p.y);
        public bool IsDiggable(Vector2Int p)=>!Protected(p.x,p.y)&&IsBlocked(p);
        public Vector2Int WorldToCell(Vector2 p)=>new Vector2Int(Mathf.FloorToInt(p.x/.6f),Mathf.FloorToInt(p.y/.6f));
        public Vector2 CellToWorld(Vector2Int p)=>new Vector2((p.x+.5f)*.6f,(p.y+.5f)*.6f);
        public void DigCircle(Vector2 p,float r)=>throw new InvalidOperationException("Not a digging fixture");
        public TerrainDigResult DigCircle(Vector2 p,float r,TerrainDigAuthority a,int s,IReadOnlyCollection<Vector2Int> cells)=>throw new InvalidOperationException("Not a digging fixture");
        public void Rebuild()=>State=new RegionTerrainState("owned navigation fixture",Size,Size,this);
    }
    static void Complete(GridRouteSearch search)
    {
        int n=0;while(search.Status==GridRouteStatus.Pending){long prior=search.Work;search.Step(7);Assert.That(search.Work-prior,Is.LessThanOrEqualTo(7));Assert.That(++n,Is.LessThan(2000));}
    }
    [TestCase(1)][TestCase(7)][TestCase(32)]
    public void SmallFixtureShortestPathsAgreeWithRetainedBreadthFirstOracle(int seed)
    {
        var random=new System.Random(seed);var f=new Fixture();
        for(int y=1;y<31;y++)for(int x=1;x<31;x++)if(random.NextDouble()<.2)f.Walls.Add(y*32+x);
        f.Walls.Remove(66);f.Walls.Remove(29*32+29);f.Rebuild();var a=new Point2(2.5,2.5);var b=new Point2(29.5,29.5);
        var expected=f.State.Route(a,b,1);var search=new GridRouteSearch(f.State,a,b,.499,.499);Complete(search);
        Assert.That(search.Path.Count,Is.EqualTo(expected.Count));
        Assert.That(search.Status,Is.EqualTo(expected.Count>0?GridRouteStatus.Succeeded:GridRouteStatus.Unreachable));
    }
    [Test]public void TwoCellTunnelAdmitsActualBodyWithoutOddCellInflation()
    {
        var f=new Fixture();for(int y=1;y<31;y++)for(int x=1;x<31;x++)if(y!=15&&y!=16)f.Walls.Add(y*32+x);f.Rebuild();
        var fit=new GridRouteSearch(f.State,new Point2(3,16),new Point2(28,16),.8,.8);Complete(fit);Assert.That(fit.Status,Is.EqualTo(GridRouteStatus.Succeeded));
        var large=new GridRouteSearch(f.State,new Point2(3,16),new Point2(28,16),1.01,1.01);Complete(large);Assert.That(large.Status,Is.EqualTo(GridRouteStatus.Unreachable));
    }
    [Test]public void TerrainEditCancelsPendingSearchAndRetryFindsOpening()
    {
        var f=new Fixture();for(int y=1;y<31;y++)f.Walls.Add(y*32+16);f.Rebuild();
        var search=new GridRouteSearch(f.State,new Point2(3.5,15.5),new Point2(28.5,15.5),.49,.49);search.Step(1);
        f.State.Commit(new[]{new DualTile(16,15)},f.State.Revision,_=>true);
        Assert.That(search.Step(1),Is.EqualTo(GridRouteStatus.Canceled));
        var next=new GridRouteSearch(f.State,new Point2(3.5,15.5),new Point2(28.5,15.5),.49,.49);Complete(next);Assert.That(next.Status,Is.EqualTo(GridRouteStatus.Succeeded));
    }
    [Test]public void QueueCoalescesPendingAndDoesNotDiscardSafePathForDistantExcavation()
    {
        var f=new Fixture();f.Walls.Add(25*32+25);f.Rebuild();var go=new GameObject("owned navigation adapter");go.AddComponent<Rigidbody2D>();
        using var queue=new RegionRouteQueue(f.State,f,p=>new Point2(p.x/.6f,p.y/.6f),p=>new Vector2((float)p.X*.6f,(float)p.Y*.6f));
        try
        {
            var binding=go.AddComponent<ActorTerrainBinding>();binding.MinimumBodyRadius=.2f;binding.Bind(f);go.transform.position=new Vector2(2.1f,2.1f);go.GetComponent<Rigidbody2D>().position=go.transform.position;Physics2D.SyncTransforms();
            Vector2 from=go.GetComponent<Rigidbody2D>().position,goal=new Vector2(15.3f,2.1f),extents=Vector2.one*.2f;
            Assert.That(from,Is.EqualTo(new Vector2(2.1f,2.1f)),"fixture physical and logical start must agree");
            Assert.That(queue.RequestPath(binding,from,goal,extents,out _),Is.EqualTo(GridRouteStatus.Pending));
            Assert.That(queue.RequestPath(binding,from,goal,extents,out _),Is.EqualTo(GridRouteStatus.Pending));Assert.That(queue.Coalesced,Is.EqualTo(1));
            int n=0;while(queue.PendingCount>0){queue.Advance(32,100);Assert.That(++n,Is.LessThan(100));}
            Assert.That(queue.RequestPath(binding,from,goal,extents,out var path),Is.EqualTo(GridRouteStatus.Succeeded));Assert.That(path.Count,Is.GreaterThan(0));
            int revision=queue.RouteRevision(binding);f.State.Commit(new[]{new DualTile(25,25)},f.State.Revision,_=>true);
            Assert.That(queue.Accepts(binding,revision),Is.True,"open distant terrain does not make an existing route unsafe");
            binding.MinimumBodyRadius=.25f;Assert.That(queue.Accepts(binding,revision),Is.False,"physical clearance change invalidates cached route");binding.MinimumBodyRadius=.2f;
            queue.Cancel(binding);Assert.That(queue.Accepts(binding,revision),Is.False);Assert.That(queue.PendingCount,Is.Zero);
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }
    [Test]public void ExplicitCancellationNeverPublishesSuccess()
    {var f=new Fixture();var search=new GridRouteSearch(f.State,new Point2(2.5,2.5),new Point2(28.5,28.5),.49,.49);search.Cancel();Assert.That(search.Step(100),Is.EqualTo(GridRouteStatus.Canceled));Assert.That(search.Path.Count,Is.Zero);}
    [Test]public void NonFiniteBodySizeAndEndpointCannotCreateUnsafeRoute()
    {var f=new Fixture();Assert.Throws<ArgumentOutOfRangeException>(()=>new GridRouteSearch(f.State,new Point2(2,2),new Point2(3,3),double.NaN,.5));Assert.That(new GridRouteSearch(f.State,new Point2(double.NaN,2),new Point2(3,3),.5,.5).Status,Is.EqualTo(GridRouteStatus.Unreachable));}
    [Test]public void ReachabilityProbeCannotCancelCurrentMovementRequest()
    {
        var f=new Fixture();var go=new GameObject("owned channel adapter");go.AddComponent<Rigidbody2D>();
        using var queue=new RegionRouteQueue(f.State,f,p=>new Point2(p.x/.6f,p.y/.6f),p=>new Vector2((float)p.X*.6f,(float)p.Y*.6f));
        try
        {
            var binding=go.AddComponent<ActorTerrainBinding>();binding.MinimumBodyRadius=.2f;binding.Bind(f);go.transform.position=new Vector2(2.1f,2.1f);go.GetComponent<Rigidbody2D>().position=go.transform.position;Physics2D.SyncTransforms();
            var from=go.GetComponent<Rigidbody2D>().position;
            queue.RequestPath(binding,from,new Vector2(15.3f,2.1f),Vector2.one*.2f,out _);
            queue.RequestPath(binding,from,new Vector2(2.1f,15.3f),Vector2.one*.2f,out _,7);
            Assert.That(queue.PendingCount,Is.EqualTo(2));queue.Cancel(binding,7);Assert.That(queue.PendingCount,Is.EqualTo(1));
            int ticks=0;while(queue.PendingCount>0){queue.Advance(32,100);Assert.That(++ticks,Is.LessThan(100));}
            Assert.That(queue.Accepts(binding,queue.RouteRevision(binding)),Is.True);queue.CancelAll(binding);Assert.That(queue.Accepts(binding,queue.RouteRevision(binding)),Is.False);
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }
    [Test]public void FiveBodyAwareDestinationsDoNotShareFootprintsAndReleaseCleanly()
    {
        var f=new Fixture();var actors=new List<GameObject>();var points=new List<Vector2>();using var slots=new RegionMovementSlots(f);
        try
        {
            for(int i=0;i<5;i++)
            {
                var go=new GameObject("owned slot adapter "+i);actors.Add(go);go.AddComponent<Rigidbody2D>();go.AddComponent<BoxCollider2D>().size=Vector2.one*.6f;
                go.transform.position=new Vector2(2.1f+i*.8f,2.1f);Physics2D.SyncTransforms();var binding=go.AddComponent<ActorTerrainBinding>();binding.Bind(f);
                var point=slots.Choose(binding,123,new Vector2(9.3f,9.3f),Vector2.one*.4f);
                foreach(var other in points)Assert.That(Mathf.Abs(point.x-other.x)>=.639f||Mathf.Abs(point.y-other.y)>=.639f,Is.True,"reserved body footprints must not overlap");
                points.Add(point);
            }
            Assert.That(slots.Count,Is.EqualTo(5));foreach(var actor in actors)slots.Release(actor.GetComponent<ActorTerrainBinding>());Assert.That(slots.Count,Is.Zero);
        }
        finally{foreach(var actor in actors)UnityEngine.Object.DestroyImmediate(actor);}
    }
    [Test]public void OnlyHighestPriorityIntentionAppliesOncePerPhysicsTick()
    {
        var f=new Fixture();var go=new GameObject("owned movement-intent adapter");
        try
        {
            var body=go.AddComponent<Rigidbody2D>();go.transform.position=new Vector2(3.9f,3.9f);body.position=go.transform.position;
            var binding=go.AddComponent<ActorTerrainBinding>();binding.Bind(f);binding.Ready=()=>true;binding.RequestRoute=(a,b)=>new ActorRouteResult(GridRouteStatus.Pending,null,0);
            var follow=go.AddComponent<BuddyFollow>();var wander=go.AddComponent<CampWander>();
            ActorMotionIntent.Submit(follow,body,Vector2.right,.2f,10);ActorMotionIntent.Submit(wander,body,Vector2.left,.2f,5);
            var motion=go.GetComponent<ActorMotionIntent>();var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(ActorMotionIntent).GetMethod("Awake",flags).Invoke(motion,null);typeof(ActorMotionIntent).GetMethod("FixedUpdate",flags).Invoke(motion,null);typeof(ActorMotionIntent).GetMethod("FixedUpdate",flags).Invoke(motion,null);
            Assert.That(motion.Applications,Is.EqualTo(1));Assert.That(body.linearVelocity.x,Is.GreaterThan(0));Assert.That(motion.ContestedIntents,Is.EqualTo(1));
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }
    [Test]public void CombatApproachReachesRoundedColliderContactWithoutChangingAttackRange()
    {
        var f=new Fixture();var source=new GameObject("owned round approach");var target=new GameObject("owned round opponent");using var slots=new RegionMovementSlots(f);
        try
        {
            var body=source.AddComponent<Rigidbody2D>();var a=source.AddComponent<CircleCollider2D>();a.radius=.5f;
            var other=target.AddComponent<Rigidbody2D>();var b=target.AddComponent<CircleCollider2D>();b.radius=.5f;
            source.transform.position=new Vector2(6,6);body.position=source.transform.position;target.transform.position=new Vector2(9,9);other.position=target.transform.position;Physics2D.SyncTransforms();
            var binding=source.AddComponent<ActorTerrainBinding>();binding.Bind(f);
            var point=slots.Choose(binding,123,other.position,Vector2.one*.5f,true);body.position=point;source.transform.position=point;Physics2D.SyncTransforms();
            var gap=a.Distance(b);Assert.That(gap.isValid,Is.True);Assert.That(gap.distance,Is.InRange(0f,Physics2D.defaultContactOffset*2+.001f),"approach must reach the existing contact tolerance, not a diagonal box corner");
        }
        finally{UnityEngine.Object.DestroyImmediate(source);UnityEngine.Object.DestroyImmediate(target);}
    }
    [Test]public void OpposingApproachReservationsDoNotRepelFightersBeforeContact()
    {
        var f=new Fixture();var a=new GameObject("owned opponent A");var b=new GameObject("owned opponent B");using var slots=new RegionMovementSlots(f);
        try
        {
            var ba=a.AddComponent<Rigidbody2D>();a.AddComponent<BoxCollider2D>();var bb=b.AddComponent<Rigidbody2D>();b.AddComponent<BoxCollider2D>();
            var aa=a.AddComponent<ActorTerrainBinding>();aa.Bind(f);var ab=b.AddComponent<ActorTerrainBinding>();ab.Bind(f);
            a.transform.position=new Vector2(3,6);ba.position=a.transform.position;b.transform.position=new Vector2(7,6);bb.position=b.transform.position;Physics2D.SyncTransforms();
            slots.Choose(aa,bb.GetInstanceID(),bb.position,Vector2.one*.5f,true);slots.Choose(ab,ba.GetInstanceID(),ba.position,Vector2.one*.5f,true);
            a.transform.position=new Vector2(4.5f,6);ba.position=a.transform.position;b.transform.position=new Vector2(5.5f,6);bb.position=b.transform.position;Physics2D.SyncTransforms();
            var nearA=slots.Choose(aa,bb.GetInstanceID(),bb.position,Vector2.one*.5f,true);var nearB=slots.Choose(ab,ba.GetInstanceID(),ba.position,Vector2.one*.5f,true);
            Assert.That(Vector2.Distance(nearA,ba.position),Is.LessThan(.011f));Assert.That(Vector2.Distance(nearB,bb.position),Is.LessThan(.011f),"opposing targets must not force reciprocal orbiting at contact");
        }
        finally{UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);}
    }
    [Test]public void EmptyRallyMarkerHasOneCentreDestinationAndSpacedAdditionalBodies()
    {
        var f=new Fixture();var actors=new List<GameObject>();var points=new List<Vector2>();using var slots=new RegionMovementSlots(f);var center=new Vector2(9.3f,9.3f);
        try
        {
            for(int i=0;i<5;i++)
            {
                var actor=new GameObject("owned point rally "+i);actors.Add(actor);actor.AddComponent<Rigidbody2D>();actor.AddComponent<BoxCollider2D>().size=Vector2.one*.75f;actor.transform.position=new Vector2(3,3+i);Physics2D.SyncTransforms();var binding=actor.AddComponent<ActorTerrainBinding>();binding.Bind(f);
                var point=slots.Choose(binding,123,center,Vector2.zero);
                if(i==0)Assert.That(point,Is.EqualTo(center),"a nonphysical rally must not force the first follower to orbit a phantom body");
                foreach(var other in points)Assert.That(Mathf.Abs(point.x-other.x)>=.789f||Mathf.Abs(point.y-other.y)>=.789f,Is.True);
                points.Add(point);
            }
            Assert.That(slots.Count,Is.EqualTo(5));
        }
        finally{foreach(var actor in actors)UnityEngine.Object.DestroyImmediate(actor);}
    }
    [Test]public void GatheringAvoidsAnUnreservedPhysicalOccupantWithoutIgnoringCollision()
    {
        var f=new Fixture();var actor=new GameObject("owned arriving worker");var occupant=new GameObject("owned unreserved standing body");using var slots=new RegionMovementSlots(f);
        try
        {
            var body=actor.AddComponent<Rigidbody2D>();var a=actor.AddComponent<BoxCollider2D>();a.size=Vector2.one*.75f;
            var other=occupant.AddComponent<Rigidbody2D>();var b=occupant.AddComponent<BoxCollider2D>();b.size=Vector2.one*1.2f;
            actor.transform.position=new Vector2(3.9f,3.9f);body.position=actor.transform.position;occupant.transform.position=new Vector2(9.3f,9.3f);other.position=occupant.transform.position;Physics2D.SyncTransforms();
            var binding=actor.AddComponent<ActorTerrainBinding>();binding.Bind(f);var point=slots.Choose(binding,123,other.position,Vector2.zero);
            Assert.That(slots.HasDestination(binding),Is.True);Assert.That(point,Is.Not.EqualTo(other.position));body.position=point;actor.transform.position=point;Physics2D.SyncTransforms();
            Assert.That(a.Distance(b).distance,Is.GreaterThan(.02f));Assert.That(Physics2D.GetIgnoreCollision(a,b),Is.False);Assert.That(a.size,Is.EqualTo(Vector2.one*.75f));
        }
        finally{UnityEngine.Object.DestroyImmediate(actor);UnityEngine.Object.DestroyImmediate(occupant);}
    }
    [Test]public void UnavailableGatheringDestinationIsNotAnArrivalAtTheActorsCurrentPosition()
    {
        var f=new Fixture();for(int y=12;y<19;y++)for(int x=12;x<19;x++)f.Walls.Add(y*32+x);f.Rebuild();var actor=new GameObject("owned unavailable gathering");using var slots=new RegionMovementSlots(f);
        actor.AddComponent<BoxCollider2D>().size=Vector2.one*.75f;
        try
        {
            var body=actor.AddComponent<Rigidbody2D>();actor.transform.position=new Vector2(3.9f,3.9f);body.position=actor.transform.position;
            var binding=actor.AddComponent<ActorTerrainBinding>();binding.Bind(f);binding.HasMovementDestination=()=>slots.HasDestination(binding);
            slots.Choose(binding,123,new Vector2(9.3f,9.3f),Vector2.zero);Assert.That(binding.MovementDestinationAvailable,Is.False,"caller must wait/fail safely, not report an occupied/blocked marker as reached");
        }
        finally{UnityEngine.Object.DestroyImmediate(actor);}
    }
    [Test]public void ChangingAITaskCancelsDerivedWorkOnceRatherThanEveryTick()
    {
        var actor=new GameObject("owned task transition");
        try
        {
            actor.AddComponent<Rigidbody2D>();actor.AddComponent<BuddyFollow>();var binding=actor.AddComponent<ActorTerrainBinding>();int canceled=0;binding.CancelRoute=()=>canceled++;
            var brain=actor.AddComponent<BuddyBrain>();brain.currentTask="Scavenge";var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(BuddyBrain).GetMethod("Awake",flags).Invoke(brain,null);var update=typeof(BuddyBrain).GetMethod("UpdateTaskPermissions",flags);update.Invoke(brain,null);update.Invoke(brain,null);
            Assert.That(brain.currentTask,Is.EqualTo("Follow"));Assert.That(canceled,Is.EqualTo(1));
        }
        finally{UnityEngine.Object.DestroyImmediate(actor);}
    }
    [Test]public void ClearDirectedMovementDoesNotRequestSharedIntermediateGridAnchors()
    {
        var f=new Fixture();var actor=new GameObject("owned clear directed movement");
        try
        {
            var body=actor.AddComponent<Rigidbody2D>();actor.AddComponent<BoxCollider2D>().size=Vector2.one*.75f;actor.transform.position=new Vector2(3.9f,3.9f);body.position=actor.transform.position;Physics2D.SyncTransforms();
            var binding=actor.AddComponent<ActorTerrainBinding>();binding.Bind(f);binding.Ready=()=>true;int requests=0;binding.RequestRoute=(a,b)=>{requests++;return new ActorRouteResult(GridRouteStatus.Pending,null,0);};
            var route=new RegionRouteFollower();var target=new Vector2(9.3f,8.1f);Assert.That(route.Direction(binding,body.position,target,out var direction,.1f),Is.True);Assert.That(requests,Is.Zero);Assert.That(route.CurrentWaypoint,Is.EqualTo(target));Assert.That(Vector2.Dot(direction.normalized,(target-body.position).normalized),Is.GreaterThan(.999f));
        }
        finally{UnityEngine.Object.DestroyImmediate(actor);}
    }
    [TestCase(false)][TestCase(true)]public void FriendlyBlockerGetsACommittedClearSideWithoutIgnoringCollision(bool counterflow)
    {
        var f=new Fixture();var actor=new GameObject("owned local detour");var standing=new GameObject("owned standing friend");
        try
        {
            var body=actor.AddComponent<Rigidbody2D>();var a=actor.AddComponent<BoxCollider2D>();a.size=Vector2.one*.75f;actor.AddComponent<BuddyUnit>();
            var other=standing.AddComponent<Rigidbody2D>();var b=standing.AddComponent<BoxCollider2D>();b.size=Vector2.one;standing.AddComponent<BuddyUnit>();
            actor.transform.position=new Vector2(3.9f,3.9f);body.position=actor.transform.position;standing.transform.position=new Vector2(3.9f,3);other.position=standing.transform.position;Physics2D.SyncTransforms();
            var binding=actor.AddComponent<ActorTerrainBinding>();binding.Bind(f);var motion=actor.AddComponent<ActorMotionIntent>();var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;typeof(ActorMotionIntent).GetMethod("Awake",flags).Invoke(motion,null);var refine=typeof(ActorMotionIntent).GetMethod("Yield",flags);
            if(counterflow){var otherBinding=standing.AddComponent<ActorTerrainBinding>();otherBinding.Bind(f);otherBinding.RequestRoute=(x,y)=>new ActorRouteResult(GridRouteStatus.Pending,null,0);var task=standing.AddComponent<BuddyFollow>();ActorMotionIntent.Submit(task,other,Vector2.up*1.6f,.25f,10);}
            float side=0;
            for(int i=0;i<12;i++)
            {
                var velocity=(Vector2)refine.Invoke(motion,new object[]{Vector2.down*1.6f});if(i==0){Assert.That(Mathf.Abs(velocity.x),Is.GreaterThan(1));side=Mathf.Sign(velocity.x);}Assert.That(velocity.x*side,Is.GreaterThan(0),"do not reverse the chosen passing side each tick");
                var next=body.position+velocity*Time.fixedDeltaTime;body.position=next;actor.transform.position=next;Physics2D.SyncTransforms();Assert.That(a.Distance(b).distance,Is.GreaterThanOrEqualTo(-.001f));Assert.That(TileMover.CanOccupyBox(f,body.position,Vector2.one*.375f),Is.True);
            }
            Assert.That(Mathf.Abs(body.position.x-3.9f),Is.GreaterThan(.25f));Assert.That(Physics2D.GetIgnoreCollision(a,b),Is.False);ActorMotionIntent.Cancel(actor);Assert.That(typeof(ActorMotionIntent).GetField("detourBody",flags).GetValue(motion),Is.Null);
        }
        finally{UnityEngine.Object.DestroyImmediate(actor);UnityEngine.Object.DestroyImmediate(standing);}
    }
    [Test]public void LocalPassingRejectsASideAlreadyOccupiedByAnotherPhysicalBody()
    {
        var f=new Fixture();var actor=new GameObject("owned side choice");var front=new GameObject("owned front friend");var side=new GameObject("owned occupied side");
        try
        {
            var body=actor.AddComponent<Rigidbody2D>();actor.AddComponent<BoxCollider2D>().size=Vector2.one*.75f;actor.AddComponent<BuddyUnit>();actor.transform.position=new Vector2(3.9f,3.9f);body.position=actor.transform.position;
            var other=front.AddComponent<Rigidbody2D>();front.AddComponent<BoxCollider2D>();front.AddComponent<BuddyUnit>();front.transform.position=new Vector2(3.9f,3);other.position=front.transform.position;
            var binding=actor.AddComponent<ActorTerrainBinding>();binding.Bind(f);var motion=actor.AddComponent<ActorMotionIntent>();var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;typeof(ActorMotionIntent).GetMethod("Awake",flags).Invoke(motion,null);
            int preferred=(motion.GetInstanceID()&1)==0?1:-1;var third=side.AddComponent<Rigidbody2D>();side.AddComponent<BoxCollider2D>().size=Vector2.one*.75f;side.AddComponent<BuddyUnit>();side.transform.position=new Vector2(3.9f+preferred*.935f,3.935f);third.position=side.transform.position;Physics2D.SyncTransforms();
            var velocity=(Vector2)typeof(ActorMotionIntent).GetMethod("Yield",flags).Invoke(motion,new object[]{Vector2.down*1.6f});Assert.That(velocity.x*preferred,Is.LessThan(-.5f),"choose the physically clear alternative, not the occupied detour");
        }
        finally{UnityEngine.Object.DestroyImmediate(actor);UnityEngine.Object.DestroyImmediate(front);UnityEngine.Object.DestroyImmediate(side);}
    }
    [Test]public void ObservedDistantExcavationPreservesInFlightSearchProgress()
    {
        var f=new Fixture();f.Walls.Add(25*32+25);f.Rebuild();var search=new GridRouteSearch(f.State,new Point2(2.5,3.5),new Point2(28.5,3.5),.49,.49);search.Step(3);
        int visited=search.Visited;var cells=new[]{new DualTile(25,25)};f.State.Commit(cells,f.State.Revision,_=>true);
        Assert.That(search.ObserveCommit(cells,f.State.Revision),Is.True);Assert.That(search.Visited,Is.EqualTo(visited));Complete(search);Assert.That(search.Status,Is.EqualTo(GridRouteStatus.Succeeded));
        Assert.That(search.Path.Count,Is.EqualTo(f.State.Route(new Point2(2.5,3.5),new Point2(28.5,3.5),1).Count));
    }
    [Test]public void RelevantExcavationRestartsInFlightSearchRatherThanMissingShortcut()
    {
        var f=new Fixture();f.Walls.Add(3*32+4);f.Rebuild();var search=new GridRouteSearch(f.State,new Point2(2.5,3.5),new Point2(28.5,3.5),.49,.49);search.Step(3);
        var cells=new[]{new DualTile(4,3)};f.State.Commit(cells,f.State.Revision,_=>true);
        Assert.That(search.ObserveCommit(cells,f.State.Revision),Is.False);Assert.That(search.Status,Is.EqualTo(GridRouteStatus.Canceled));
    }
    [Test]public void FollowingMovingFriendDoesNotForceSidewaysMotionAtContactHeadway()
    {
        var f=new Fixture();var go=new GameObject("owned following adapter");var front=new GameObject("owned moving friend");
        try
        {
            var body=go.AddComponent<Rigidbody2D>();go.AddComponent<BoxCollider2D>().size=Vector2.one*.75f;go.AddComponent<BuddyUnit>();
            var ahead=front.AddComponent<Rigidbody2D>();front.AddComponent<BoxCollider2D>().size=Vector2.one*.75f;front.AddComponent<BuddyUnit>();
            go.transform.position=new Vector2(3.9f,3.9f);body.position=go.transform.position;front.transform.position=new Vector2(3.9f,4.66f);ahead.position=front.transform.position;ahead.linearVelocity=Vector2.up;Physics2D.SyncTransforms();
            var binding=go.AddComponent<ActorTerrainBinding>();binding.Bind(f);binding.Ready=()=>true;binding.RequestRoute=(a,b)=>new ActorRouteResult(GridRouteStatus.Pending,null,0);
            var follow=go.AddComponent<BuddyFollow>();ActorMotionIntent.Submit(follow,body,Vector2.up,.28f,10);
            var motion=go.GetComponent<ActorMotionIntent>();var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(ActorMotionIntent).GetMethod("Awake",flags).Invoke(motion,null);typeof(ActorMotionIntent).GetMethod("FixedUpdate",flags).Invoke(motion,null);
            Assert.That(body.linearVelocity.y,Is.GreaterThan(0));Assert.That(Mathf.Abs(body.linearVelocity.x),Is.LessThan(.00001f));Assert.That(motion.WaitingForFriendlySpace,Is.False);
        }
        finally{UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(front);}
    }
    [Test]public void ResumingPastAnchorAdvancesOnlyWhenFullBodySweepIsClear()
    {
        var f=new Fixture();var go=new GameObject("owned waypoint adapter");
        try
        {
            var body=go.AddComponent<Rigidbody2D>();var binding=go.AddComponent<ActorTerrainBinding>();binding.Bind(f);binding.MinimumBodyRadius=.2f;binding.RequestRoute=(a,b)=>new ActorRouteResult(GridRouteStatus.Pending,null,0);
            var path=new List<Vector2>{new Vector2(3.9f,3.9f),new Vector2(4.5f,3.9f),new Vector2(5.1f,3.9f)};
            Assert.That(RouteWaypointPolicy.Advance(path,0,new Vector2(4.08f,3.9f),binding,.2f),Is.EqualTo(1),"do not walk backwards to an already passed anchor");
            f.Walls.Add(6*32+7);f.Rebuild();Assert.That(RouteWaypointPolicy.Advance(path,0,new Vector2(3.99f,3.9f),binding,.2f),Is.Zero,"never shorten through solid dirt");
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }
    [Test]public void NearlyHorizontalFriendlyBlockUsesDetourInsteadOfTinyAxisCreep()
    {
        var f=new Fixture();var go=new GameObject("owned near-horizontal walker");var friend=new GameObject("owned large idle blocker");
        try
        {
            var body=go.AddComponent<Rigidbody2D>();go.AddComponent<BoxCollider2D>().size=Vector2.one*.75f;go.AddComponent<BuddyUnit>();var other=friend.AddComponent<Rigidbody2D>();friend.AddComponent<BoxCollider2D>().size=Vector2.one;friend.AddComponent<BuddyUnit>();
            body.position=new Vector2(5.24f,5.42f);go.transform.position=body.position;other.position=new Vector2(4.3f,5.1f);friend.transform.position=other.position;Physics2D.SyncTransforms();
            var binding=go.AddComponent<ActorTerrainBinding>();binding.Bind(f);binding.Ready=()=>true;binding.RequestRoute=(a,b)=>new ActorRouteResult(GridRouteStatus.Pending,null,0);
            var follow=go.AddComponent<BuddyFollow>();ActorMotionIntent.Submit(follow,body,new Vector2(-3.15f,-.03f),.28f,10);var motion=go.GetComponent<ActorMotionIntent>();var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(ActorMotionIntent).GetMethod("Awake",flags).Invoke(motion,null);typeof(ActorMotionIntent).GetMethod("FixedUpdate",flags).Invoke(motion,null);
            Assert.That(body.linearVelocity.magnitude,Is.GreaterThan(1),"a tiny goal-axis component is not a useful avoidance direction");
        }
        finally{UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(friend);}
    }
    [Test]public void DiagonalFriendlyCornerSlidesAlongClearAxisWithoutCollisionException()
    {
        var f=new Fixture();var go=new GameObject("owned corner walker");var friend=new GameObject("owned corner friend");
        try
        {
            var body=go.AddComponent<Rigidbody2D>();go.AddComponent<BoxCollider2D>().size=Vector2.one*.75f;go.AddComponent<BuddyUnit>();var other=friend.AddComponent<Rigidbody2D>();friend.AddComponent<BoxCollider2D>().size=Vector2.one*.75f;friend.AddComponent<BuddyUnit>();
            body.position=new Vector2(5.03f,5.56f);go.transform.position=body.position;other.position=new Vector2(4.25f,4.97f);friend.transform.position=other.position;Physics2D.SyncTransforms();
            var binding=go.AddComponent<ActorTerrainBinding>();binding.Bind(f);binding.Ready=()=>true;binding.RequestRoute=(a,b)=>new ActorRouteResult(GridRouteStatus.Pending,null,0);
            var follow=go.AddComponent<BuddyFollow>();ActorMotionIntent.Submit(follow,body,new Vector2(-2.2f,2.2f),.28f,10);var motion=go.GetComponent<ActorMotionIntent>();var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(ActorMotionIntent).GetMethod("Awake",flags).Invoke(motion,null);typeof(ActorMotionIntent).GetMethod("FixedUpdate",flags).Invoke(motion,null);
            Assert.That(body.linearVelocity.y,Is.GreaterThan(0));Assert.That(Mathf.Abs(body.linearVelocity.x),Is.LessThan(.0001f));Assert.That(motion.WaitingForFriendlySpace,Is.False);
            Assert.That(Physics2D.GetIgnoreCollision(go.GetComponent<Collider2D>(),friend.GetComponent<Collider2D>()),Is.False);
        }
        finally{UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(friend);}
    }
    [Test]public void FriendlyDelayMeasuresWaitingSlowingAndSidewaysDetours()
    {
        Assert.That(ActorMotionIntent.FriendlyDelayFraction(Vector2.up*2,Vector2.up*2),Is.Zero);
        Assert.That(ActorMotionIntent.FriendlyDelayFraction(Vector2.up*2,Vector2.up),Is.EqualTo(.5f));
        Assert.That(ActorMotionIntent.FriendlyDelayFraction(Vector2.up*2,Vector2.right*2),Is.EqualTo(1));
        Assert.That(ActorMotionIntent.FriendlyDelayFraction(Vector2.up*2,Vector2.down*2),Is.EqualTo(1));
        Assert.That(ActorMotionIntent.FriendlyDelayFraction(Vector2.zero,Vector2.zero),Is.Zero);
    }
    [Test]public void GatheringRejectsStandingFloorAcrossPermanentDivider()
    {
        var f=new Fixture();for(int y=1;y<31;y++)f.Walls.Add(y*32+15);f.Rebuild();var actors=new List<GameObject>();using var slots=new RegionMovementSlots(f);
        try
        {
            for(int i=0;i<10;i++)
            {
                var go=new GameObject("owned divider gathering "+i);actors.Add(go);var body=go.AddComponent<Rigidbody2D>();go.AddComponent<BoxCollider2D>().size=Vector2.one*.75f;body.position=new Vector2(4,9);go.transform.position=body.position;
                var binding=go.AddComponent<ActorTerrainBinding>();binding.Bind(f);binding.MinimumBodyRadius=.2f;var center=new Vector2(8.4f,9);
                var goal=slots.Choose(binding,19,center,Vector2.zero);
                if(slots.HasDestination(binding))Assert.That(TileMover.CanTraverseBox(f,center,goal,Vector2.one*.375f),Is.True,"standing fit alone does not establish a connected gathering area");
            }
        }
        finally{foreach(var go in actors)UnityEngine.Object.DestroyImmediate(go);}
    }
    [Test]public void IdleFriendlyCanYieldForwardWhenTunnelHasNoLateralRoom()
    {
        var f=new Fixture();for(int y=1;y<31;y++){f.Walls.Add(y*32+6);f.Walls.Add(y*32+9);}f.Rebuild();
        var idle=new GameObject("owned narrow idle");var walker=new GameObject("owned narrow requester");
        try
        {
            var b=idle.AddComponent<Rigidbody2D>();idle.AddComponent<BoxCollider2D>().size=Vector2.one*.75f;idle.AddComponent<BuddyUnit>();var request=walker.AddComponent<Rigidbody2D>();walker.AddComponent<BoxCollider2D>().size=Vector2.one*.75f;walker.AddComponent<BuddyUnit>();
            b.position=new Vector2(4.8f,4.5f);idle.transform.position=b.position;request.position=new Vector2(4.8f,3.74f);walker.transform.position=request.position;Physics2D.SyncTransforms();
            var binding=idle.AddComponent<ActorTerrainBinding>();binding.Bind(f);binding.Ready=()=>true;binding.RequestRoute=(a,c)=>new ActorRouteResult(GridRouteStatus.Pending,null,0);
            var motion=idle.AddComponent<ActorMotionIntent>();var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;typeof(ActorMotionIntent).GetMethod("Awake",flags).Invoke(motion,null);
            Assert.That((bool)typeof(ActorMotionIntent).GetMethod("RequestIdleYield",flags).Invoke(motion,new object[]{request,Vector2.up}),Is.True);
            Assert.That((bool)typeof(ActorMotionIntent).GetMethod("ApplyIdleYield",flags).Invoke(motion,null),Is.True);Assert.That(b.linearVelocity.y,Is.GreaterThan(0));Assert.That(Mathf.Abs(b.linearVelocity.x),Is.LessThan(.0001f));
        }
        finally{UnityEngine.Object.DestroyImmediate(idle);UnityEngine.Object.DestroyImmediate(walker);}
    }
    [Test]public void CampGatheringSupportsTenBodiesAndKeepsReservationDuringPassing()
    {
        var f=new Fixture();var actors=new List<GameObject>();using var slots=new RegionMovementSlots(f);
        try
        {
            var goals=new List<Vector2>();
            for(int i=0;i<10;i++)
            {
                var go=new GameObject("owned gathering "+i);actors.Add(go);var body=go.AddComponent<Rigidbody2D>();go.AddComponent<BoxCollider2D>().size=Vector2.one*.75f;body.position=new Vector2(2,2);go.transform.position=body.position;
                var binding=go.AddComponent<ActorTerrainBinding>();binding.Bind(f);binding.MinimumBodyRadius=.2f;
                var goal=slots.Choose(binding,17,new Vector2(9,9),Vector2.zero);Assert.That(slots.HasDestination(binding),Is.True,"all ten need actual terrain-clear standing destinations");
                foreach(var prior in goals)Assert.That(Mathf.Abs(prior.x-goal.x)>=.79f||Mathf.Abs(prior.y-goal.y)>=.79f,Is.True);goals.Add(goal);
            }
            var passer=new GameObject("owned transient passer");actors.Add(passer);var passing=passer.AddComponent<Rigidbody2D>();passer.AddComponent<BoxCollider2D>().size=Vector2.one*.75f;passing.position=goals[0];passer.transform.position=passing.position;Physics2D.SyncTransforms();
            Assert.That(slots.Choose(actors[0].GetComponent<ActorTerrainBinding>(),17,new Vector2(9,9),Vector2.zero),Is.EqualTo(goals[0]),"passing traffic must not churn an established lease");
        }
        finally{foreach(var go in actors)UnityEngine.Object.DestroyImmediate(go);}
    }
    [Test]public void DirectedWalkQueueWaitHasSeparateFiniteDeadline()
    {
        Assert.That(CampDirectedWalk.WalkDeadlineExpired(10.1f,10,0),Is.True,"clear travel retains original deadline");
        Assert.That(CampDirectedWalk.WalkDeadlineExpired(10.1f,10,2),Is.False,"observed friendly queue may clear");
        Assert.That(CampDirectedWalk.WalkDeadlineExpired(12.1f,10,2),Is.True,"waiting is not an unlimited renewal");
        Assert.That(CampDirectedWalk.WalkDeadlineExpired(20.1f,10,100),Is.True,"permanent obstruction still terminates");
    }
    [Test]public void IdleFriendlyYieldsThroughNormalMovementAndCancellationClearsRequest()
    {
        var f=new Fixture();var idle=new GameObject("owned idle friend");var walker=new GameObject("owned exact approach");
        try
        {
            var b=idle.AddComponent<Rigidbody2D>();idle.AddComponent<BoxCollider2D>().size=Vector2.one*.75f;idle.AddComponent<BuddyUnit>();
            var request=walker.AddComponent<Rigidbody2D>();walker.AddComponent<BoxCollider2D>().size=Vector2.one*.75f;walker.AddComponent<BuddyUnit>();
            b.position=new Vector2(4.5f,4.5f);idle.transform.position=b.position;request.position=new Vector2(4.5f,3.74f);walker.transform.position=request.position;Physics2D.SyncTransforms();
            var binding=idle.AddComponent<ActorTerrainBinding>();binding.Bind(f);binding.Ready=()=>true;binding.RequestRoute=(a,c)=>new ActorRouteResult(GridRouteStatus.Pending,null,0);
            var motion=idle.AddComponent<ActorMotionIntent>();var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(ActorMotionIntent).GetMethod("Awake",flags).Invoke(motion,null);
            Assert.That((bool)typeof(ActorMotionIntent).GetMethod("RequestIdleYield",flags).Invoke(motion,new object[]{request,Vector2.up}),Is.True);
            Assert.That((bool)typeof(ActorMotionIntent).GetMethod("ApplyIdleYield",flags).Invoke(motion,null),Is.True);Assert.That(Mathf.Abs(b.linearVelocity.x),Is.GreaterThan(0));
            Assert.That(Physics2D.GetIgnoreCollision(idle.GetComponent<Collider2D>(),walker.GetComponent<Collider2D>()),Is.False);
            ActorMotionIntent.Cancel(idle);Assert.That((bool)typeof(ActorMotionIntent).GetMethod("ApplyIdleYield",flags).Invoke(motion,null),Is.False);
        }
        finally{UnityEngine.Object.DestroyImmediate(idle);UnityEngine.Object.DestroyImmediate(walker);}
    }
    [Test]public void QueuedRoutePublishesTravelBudgetOnlyOnceWhileFollowing()
    {
        var f=new Fixture();var go=new GameObject("owned publication adapter");
        try
        {
            go.AddComponent<Rigidbody2D>();var binding=go.AddComponent<ActorTerrainBinding>();binding.Bind(f);binding.MinimumBodyRadius=.2f;
            f.Walls.Add(6*32+7);f.Rebuild();int calls=0;
            binding.RequestRoute=(a,b)=>{calls++;return calls==1?new ActorRouteResult(GridRouteStatus.Pending,null,0):new ActorRouteResult(GridRouteStatus.Succeeded,new List<Vector2>{a,new Vector2(3.9f,5.1f),b},0,4.8f);};
            binding.AcceptCachedRoute=r=>true;var follower=new RegionRouteFollower();Vector2 from=new Vector2(3.9f,3.9f),to=new Vector2(5.1f,3.9f);
            Assert.That(follower.Direction(binding,from,to,out _),Is.False);Assert.That(follower.Publication,Is.Zero);
            Assert.That(follower.Direction(binding,from,to,out _),Is.True);Assert.That(follower.Publication,Is.EqualTo(1));Assert.That(follower.PublishedDistance,Is.EqualTo(4.8f));
            Assert.That(follower.Direction(binding,from,to,out _),Is.True);Assert.That(follower.Publication,Is.EqualTo(1));Assert.That(calls,Is.EqualTo(2));
            follower.Reset();Assert.That(follower.Publication,Is.Zero);
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }
    [TestCase(256,false)][TestCase(256,true)][TestCase(1024,false)][TestCase(1024,true)]
    public void BoundedLargerSearchReportsMonoWorkAndOracleComparison(int size,bool blocked)
    {
        var f=new Fixture(size);if(blocked){for(int y=1;y<size-1;y++)f.Walls.Add(y*size+size/2);f.Rebuild();}
        var start=new Point2(2.5,size/2+.5);var end=new Point2(size-3.5,size/2+.5);
        var clock=System.Diagnostics.Stopwatch.StartNew();var oracle=f.State.Route(start,end,1);clock.Stop();double bfs=clock.Elapsed.TotalMilliseconds;
        long allocated=GC.GetAllocatedBytesForCurrentThread();var search=new GridRouteSearch(f.State,start,end,.499,.499);double maximumSlice=0;int slices=0;clock.Restart();
        var sliceClock=new System.Diagnostics.Stopwatch();
        while(search.Status==GridRouteStatus.Pending)
        {
            long prior=search.Work;sliceClock.Restart();search.Step(512);sliceClock.Stop();maximumSlice=Math.Max(maximumSlice,sliceClock.Elapsed.TotalMilliseconds);
            Assert.That(search.Work-prior,Is.LessThanOrEqualTo(512));Assert.That(++slices,Is.LessThan(30000));
        }
        clock.Stop();long bytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
        Assert.That(search.Path.Count,Is.EqualTo(oracle.Count));Assert.That(search.Status,Is.EqualTo(blocked?GridRouteStatus.Unreachable:GridRouteStatus.Succeeded));
        TestContext.Out.WriteLine(FormattableString.Invariant($"Stage64DScaling size={size} blocked={blocked} bfsMs={bfs:R} astarMs={clock.Elapsed.TotalMilliseconds:R} maximum512NodeSliceMs={maximumSlice:R} work={search.Work} visited={search.Visited} allocationCounterRaw={bytes} allocationCounterAvailable={bytes>0} slices={slices} path={search.Path.Count}. Unity Editor Mono isolated algorithm fixture, not player frame time or region generation capacity."));
    }
}
#endif
