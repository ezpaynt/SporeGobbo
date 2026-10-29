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
