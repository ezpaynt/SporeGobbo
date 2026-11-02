#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SporeGobbo.Geometry;
using SporeGobbo.CampLifecycle;

/// <summary>Owned navigation/native geometry fixtures; historical diagnostics are explicit. Real-service collision acceptance is separately reviewed in Stage64DValidationContractTests.</summary>
public sealed class Stage64DNavigationTests
{
    // Real occupancy/commit and motor-clearance code; authorization here is an isolated adapter.
    sealed class DigFixture:ICompleteTerrainDefinition,IDiggableTerrain
    {
        public readonly HashSet<Vector2Int> Walls=new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> ProtectedCells=new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> Resistant=new HashSet<Vector2Int>();
        public RegionTerrainState State;readonly bool inverted;
        Func<IEnumerable<DualTile>,int,Func<DualTile,bool>,int> commit;
        public DigFixture(bool flipped=false){inverted=flipped;}
        public float CellSize=>.6f;
        public bool Protected(int x,int y)=>x<=0||y<=0||x>=127||y>=127||ProtectedCells.Contains(new Vector2Int(x,y));
        public bool PlacementAllowed(int x,int y)=>!Protected(x,y);
        public bool TrySolid(int x,int y,out bool solid){solid=Protected(x,y)||Walls.Contains(new Vector2Int(x,y));return true;}
        public Vector2Int WorldToCell(Vector2 p)=>new Vector2Int(Mathf.FloorToInt(p.x/.6f),Mathf.FloorToInt(inverted?128-p.y/.6f:p.y/.6f));
        public Vector2 CellToWorld(Vector2Int p)=>new Vector2((p.x+.5f)*.6f,(inverted?128-p.y-.5f:p.y+.5f)*.6f);
        public bool IsBlocked(Vector2Int p)=>State.World.IsSolid(p.x,p.y);
        public bool IsDiggable(Vector2Int p)=>IsBlocked(p)&&!Protected(p.x,p.y);
        public void Initialize(){State=new RegionTerrainState("owned digging fixture",128,128,this);commit=State.TakeMutationControl();}
        public TerrainDigResult Apply(PlayerDigFootprint footprint)
        {
            var selected=footprint.Cells(this);var cuts=new List<DualTile>();
            foreach(var p in selected)if(IsDiggable(p)&&!Resistant.Contains(p))cuts.Add(new DualTile(p.x,p.y));
            int n=commit(cuts,State.Revision,c=>true);return new TerrainDigResult(selected.Count,n);
        }
        public void DigCircle(Vector2 p,float r)=>Apply(new PlayerDigFootprint(p,r));
        public TerrainDigResult DigCircle(Vector2 p,float r,TerrainDigAuthority a,int s,IReadOnlyCollection<Vector2Int> cells)=>Apply(new PlayerDigFootprint(p,r));
    }

    [Test]
    public void ExactNativeDiagonalCornerLockUsesCurrentBodySweep()
    {
        var f=new DigFixture(true);var a=new Vector2Int(67,21);var b=new Vector2Int(69,23);
        f.Walls.Add(a);f.Walls.Add(b);f.Initialize();
        Vector2 p=new Vector2(40.8253937f,63.0253944f),half=Vector2.one*.5250015f;
        Vector2 end=p+new Vector2(.707106769f,.707106769f)*.8f;
        Assert.That(TileMover.CanOccupyBox(f,p,half),Is.True);
        Assert.That(TileMover.CanTraverseBox(f,p,p+Vector2.one*.07071068f,half),Is.False);
        // Four original native circle samples miss both corner centres.
        for(int i=0;i<=3;i++)foreach(var cell in new[]{a,b})
            Assert.That((f.CellToWorld(cell)-Vector2.Lerp(p,end,i/3f)).sqrMagnitude,Is.GreaterThan(.848528147f*.848528147f));
        int publications=0;f.State.Committed+=(cells,revision)=>publications++;
        var result=f.Apply(new PlayerDigFootprint(p,end,half,.848528147f));
        Assert.That(result.RemovedCells,Is.EqualTo(2));Assert.That(publications,Is.EqualTo(1));
        Assert.That(TileMover.CanTraverseBox(f,p,end,half),Is.True);
    }

    [TestCase(.22f)][TestCase(.375f)][TestCase(.5250015f)][TestCase(.75f)][TestCase(.9f)]
    public void CurrentBodyExcavatesCardinalDiagonalAndTurningAtSubcellPhases(float size)
    {
        foreach(float phase in new[]{.01f,.27f,.59f})foreach(bool flipped in new[]{false,true})
        {
            var f=new DigFixture(flipped);Vector2 p=new Vector2(36+phase,36+phase),half=new Vector2(size,size*.85f);
            for(int y=48;y<78;y++)for(int x=48;x<78;x++)
            {
                var cell=new Vector2Int(x,y);
                if(!TileMover.BodyOverlapsCell(p,half,f.CellToWorld(cell),f.CellSize))f.Walls.Add(cell);
            }
            f.Initialize();
            foreach(var direction in new[]{Vector2.right,Vector2.one.normalized,Vector2.up,new Vector2(-1,1).normalized,Vector2.left})
            {
                Vector2 next=p+direction*.8f;
                var result=f.Apply(new PlayerDigFootprint(p,next,half,.848528147f));
                Assert.That(result.EvaluatedCells,Is.LessThan(100),"Local stroke work must remain bounded for tested bodies");
                Assert.That(TileMover.CanTraverseBox(f,p,next,half),Is.True,$"size={size}, phase={phase}, flipped={flipped}, direction={direction}");
                p=next;
            }
        }
    }

    [Test]
    public void CoverageDoesNotAuthorizeProtectedResistanceOrClearUnrelatedEarth()
    {
        var f=new DigFixture();Vector2 start=new Vector2(36.3f,36.3f);var permanent=new Vector2Int(62,60);var resistant=new Vector2Int(61,61);var distant=new Vector2Int(65,65);
        f.ProtectedCells.Add(permanent);f.Resistant.Add(resistant);f.Walls.UnionWith(new[]{permanent,resistant,distant});f.Initialize();
        var footprint=new PlayerDigFootprint(start,start+Vector2.right*.8f,Vector2.one*.525f,.848528147f);
        f.Apply(footprint);
        Assert.That(f.IsBlocked(permanent),Is.True);Assert.That(f.IsBlocked(resistant),Is.True);Assert.That(f.IsBlocked(distant),Is.True);
        Assert.That(TileMover.CanTraverseBox(f,start,start+Vector2.right*.8f,Vector2.one*.525f),Is.False);
    }

    [Test]
    public void DigCorridorDoesNotGuaranteeLargerBuddyOrLaterGrowthFits()
    {
        var f=new DigFixture();for(int y=50;y<72;y++)for(int x=50;x<72;x++)f.Walls.Add(new Vector2Int(x,y));f.Initialize();
        Vector2 start=new Vector2(36.3f,36.3f),end=start+Vector2.right*.8f;
        f.Apply(new PlayerDigFootprint(start,end,Vector2.one*.375f,.848528147f));
        Assert.That(TileMover.CanTraverseBox(f,start,end,Vector2.one*.375f),Is.True);
        Assert.That(TileMover.CanTraverseBox(f,start,end,Vector2.one*1.2f),Is.False);
    }

    [TestCase("Assets/CollectableSpore.prefab")]
    [TestCase("Assets/Mushroom.prefab")]
    public void AuthoredLooseResourceCollidersAreNonblocking(string path)
    {
        var asset=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(asset,Is.Not.Null);
        var colliders=asset.GetComponentsInChildren<Collider2D>(true);
        Assert.That(colliders.Length,Is.GreaterThan(0));
        foreach(var collider in colliders)Assert.That(collider.isTrigger,Is.True,path);
    }
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
    [Test]
    public void PickupTraversalFixtureRejectsMissingLaneWithoutChangingItsRequirement()
    {
        var f=new Fixture();var target=f.CellToWorld(new Vector2Int(16,16));
        Assert.That(Stage62LifecycleEvidence.PickupTraversalDirection(f,target,Vector2.one*.525f),Is.EqualTo(Vector2.right));
        foreach(var cell in new[]{new Vector2Int(18,16),new Vector2Int(14,16),new Vector2Int(16,18),new Vector2Int(16,14)})f.Walls.Add(cell.y*f.Size+cell.x);
        f.Rebuild();
        Assert.That(Stage62LifecycleEvidence.PickupTraversalDirection(f,target,Vector2.one*.525f),Is.EqualTo(Vector2.zero));
    }
    // Owned geometry/intent adapters. Manual integration is not a real player physics proof.
    sealed class MovementFixture:IDisposable
    {
        public readonly Fixture Terrain;
        public readonly RegionMovementCoordinator Coordinator;
        public readonly List<ActorTerrainBinding> Actors=new List<ActorTerrainBinding>();
        public MovementFixture(bool camp=false,Fixture terrain=null){Terrain=terrain??new Fixture();Coordinator=new RegionMovementCoordinator(Terrain,camp);
            // Preserve the historical strict-contact contract fixtures as a legacy reference.
            typeof(RegionMovementCoordinator).GetField("expeditionCooperativeContact",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(Coordinator,false);
        }
        public ActorTerrainBinding Add(string key,Vector2 position,float half=.375f,bool camp=false,bool human=false,int group=1)
        {
            var go=new GameObject("owned coordinated "+key);var body=go.AddComponent<Rigidbody2D>();body.gravityScale=0;body.freezeRotation=true;
            go.AddComponent<BoxCollider2D>().size=Vector2.one*half*2;body.position=position;go.transform.position=position;
            var b=go.AddComponent<ActorTerrainBinding>();b.Bind(Terrain);b.Ready=()=>true;b.MinimumBodyRadius=.1f;b.RequestRoute=(a,c)=>new ActorRouteResult(GridRouteStatus.Pending,null,0);
            Coordinator.Configure(b,key,group,camp,human,camp&&!human);Actors.Add(b);Physics2D.SyncTransforms();return b;
        }
        public void Intent(ActorTerrainBinding actor,Vector2 velocity,double tick,int priority=10)
        {
            var motion=actor.GetComponent<ActorMotionIntent>()??actor.gameObject.AddComponent<ActorMotionIntent>();
            ActorMotionIntent.Submit(motion,actor.GetComponent<Rigidbody2D>(),velocity,.1f,priority);
            typeof(ActorMotionIntent).GetField("stamp",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(motion,tick);
        }
        public void Tick(double tick,bool integrate=false)
        {
            Coordinator.Resolve(tick,.02f);
            if(integrate)
            {
                var next=new Vector2[Actors.Count];for(int i=0;i<Actors.Count;i++){var body=Actors[i].GetComponent<Rigidbody2D>();next[i]=body.position+body.linearVelocity*.02f;}
                for(int i=0;i<Actors.Count;i++){Actors[i].GetComponent<Rigidbody2D>().position=next[i];Actors[i].transform.position=next[i];}Physics2D.SyncTransforms();
            }
        }
        public void Dispose(){Coordinator.Dispose();foreach(var actor in Actors)if(actor!=null)UnityEngine.Object.DestroyImmediate(actor.gameObject);}
    }
    sealed class MovementSession:ICombatSessionRules,IMovementContactSession
    {
        public readonly HashSet<GameObject> Fighters=new HashSet<GameObject>();
        public bool IsMovementParticipant(GameObject a)=>Fighters.Contains(a);
        public bool Allows(GameObject a,GameObject b)=>false;
        public int LimitDamage(GameObject target,EnemyDamageRequest request,int proposedDamage)=>proposedDamage;
        public void AfterDamage(GameObject target,EnemyDamageRequest request,EnemyDamageResult result){}
        public bool ActionsAllowed(GameObject actor)=>false;
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
        using var f=new MovementFixture();var b=f.Add("intent",new Vector2(3.9f,3.9f));var body=b.GetComponent<Rigidbody2D>();
        var high=b.GetComponent<ActorMotionIntent>();var low=b.gameObject.AddComponent<CampWander>();
        ActorMotionIntent.Submit(high,body,Vector2.right,.2f,10);ActorMotionIntent.Submit(low,body,Vector2.left,.2f,5);
        f.Tick(Time.fixedTimeAsDouble);f.Tick(Time.fixedTimeAsDouble);
        Assert.That(high.Applications,Is.EqualTo(1));Assert.That(body.linearVelocity.x,Is.GreaterThan(0));Assert.That(high.ContestedIntents,Is.EqualTo(1));
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
    [TestCase(false)][TestCase(true)]public void CoordinatedStrictPassingRetainsFullClearanceAndStableDirection(bool counterflow)
    {
        using var f=new MovementFixture();var a=f.Add("moving",new Vector2(3.9f,3.9f));var b=f.Add("blocking",new Vector2(3.9f,3),.5f);
        float side=0;
        for(int tick=1;tick<=12;tick++)
        {
            f.Intent(a,Vector2.down*1.6f,tick);if(counterflow)f.Intent(b,Vector2.up*1.6f,tick);f.Tick(tick,true);
            var v=a.GetComponent<Rigidbody2D>().linearVelocity;if(tick==1)side=Mathf.Sign(v.x);
            Assert.That(v.x*side,Is.GreaterThanOrEqualTo(0),"one committed region directive must not oscillate passing sides");
            Assert.That(a.GetComponent<Collider2D>().Distance(b.GetComponent<Collider2D>()).distance,Is.GreaterThanOrEqualTo(-.001f));
            Assert.That(TileMover.CanOccupyBox(f.Terrain,a.transform.position,Vector2.one*.375f),Is.True);
        }
        Assert.That(Physics2D.GetIgnoreCollision(a.GetComponent<Collider2D>(),b.GetComponent<Collider2D>()),Is.False);
    }
    [Test]public void CoordinatedPassingRejectsOccupiedYieldSpace()
    {
        using var f=new MovementFixture();var a=f.Add("moving",new Vector2(3.9f,3.9f));var b=f.Add("front",new Vector2(3.9f,3),.5f);var side=f.Add("uncooperative obstacle",new Vector2(4.835f,3.935f),.375f,false,false,0);
        f.Intent(a,Vector2.down*1.6f,1);f.Tick(1,true);
        Assert.That(a.GetComponent<Collider2D>().Distance(side.GetComponent<Collider2D>()).distance,Is.GreaterThanOrEqualTo(-.001f));
        Assert.That(a.GetComponent<Rigidbody2D>().linearVelocity.magnitude,Is.GreaterThan(.1f));
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
        using var f=new MovementFixture();var a=f.Add("following",new Vector2(3.9f,3.9f));var b=f.Add("ahead",new Vector2(3.9f,4.66f));
        f.Intent(a,Vector2.up,1);f.Intent(b,Vector2.up,1);f.Tick(1);
        Assert.That(a.GetComponent<Rigidbody2D>().linearVelocity.y,Is.GreaterThan(0));Assert.That(Mathf.Abs(a.GetComponent<Rigidbody2D>().linearVelocity.x),Is.LessThan(.00001f));
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
            var near=new Vector2(3.9f,4.38f);var corner=new List<Vector2>{new Vector2(3.9f,4.44f),new Vector2(4.5f,4.44f)};
            Assert.That(Vector2.Distance(near,corner[0]),Is.LessThan(f.CellSize*.12f));
            Assert.That(TileMover.CanTraverseBox(f,near,corner[0],new Vector2(.2f,.2f)),Is.True);
            Assert.That(TileMover.CanTraverseBox(f,corner[0],corner[1],new Vector2(.2f,.2f)),Is.True);
            Assert.That(TileMover.CanTraverseBox(f,near,corner[1],new Vector2(.2f,.2f)),Is.False);
            Assert.That(RouteWaypointPolicy.Advance(corner,0,near,binding,.2f),Is.Zero,"near intermediate corner cannot discard its required connector");
            Assert.That(RouteWaypointPolicy.Reached(corner,0,near,binding,.2f,f.CellSize*.12f),Is.False);
            Assert.That(RouteWaypointPolicy.Advance(corner,0,corner[0],binding,.2f),Is.EqualTo(1));
            Assert.That(RouteWaypointPolicy.Reached(corner,1,corner[1]+Vector2.up*.06f,binding,.2f,f.CellSize*.12f),Is.True,"final route arrival tolerance remains separate");

        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }
    sealed class CapturedCampTerrain:IDiggableTerrain
    {
        public readonly RegionTerrainState State=new RegionTerrainState(SporeGobbo.CampCandidate.SettlementCampLayout.LayoutId,192,192,SporeGobbo.CampCandidate.SettlementCampLayout.Shared);
        public float CellSize=>.6f;
        readonly LogicalGridTransform grid=new LogicalGridTransform(default,.6,192);
        public Vector2Int WorldToCell(Vector2 p){var q=grid.ToLogical(new Point2(p.x,p.y));return new Vector2Int((int)Math.Floor(q.X),(int)Math.Floor(q.Y));}
        public Vector2 CellToWorld(Vector2Int p){var q=grid.ToWorld(new Point2(p.x+.5,p.y+.5));return new Vector2((float)q.X,(float)q.Y);}
        public bool IsBlocked(Vector2Int p)=>State.World.IsSolid(p.x,p.y);
        public bool IsDiggable(Vector2Int p)=>false;
        public void DigCircle(Vector2 p,float r)=>throw new InvalidOperationException("Read-only captured Camp");
        public TerrainDigResult DigCircle(Vector2 p,float r,TerrainDigAuthority a,int strength,IReadOnlyCollection<Vector2Int> cells)=>throw new InvalidOperationException("Read-only captured Camp");
    }
    sealed class ExecutableCampTerrain:IDiggableTerrain,IExecutableTerrainClearance
    {
        readonly CapturedCampTerrain source;public RegionTerrainPhysics TerrainPhysics{get;}
        public ExecutableCampTerrain(CapturedCampTerrain source,RegionTerrainPhysics physics){this.source=source;TerrainPhysics=physics;}
        public float CellSize=>source.CellSize;public Vector2Int WorldToCell(Vector2 p)=>source.WorldToCell(p);public Vector2 CellToWorld(Vector2Int p)=>source.CellToWorld(p);
        public bool IsBlocked(Vector2Int p)=>source.IsBlocked(p);public bool IsDiggable(Vector2Int p)=>false;
        public void DigCircle(Vector2 p,float r)=>throw new InvalidOperationException("read-only native fixture");
        public TerrainDigResult DigCircle(Vector2 p,float r,TerrainDigAuthority a,int strength,IReadOnlyCollection<Vector2Int> cells)=>throw new InvalidOperationException("read-only native fixture");
    }
    [Test]public void SharedExecutableContractRoutesCapturedCampCornerUnderNativePhysics()
    {
        Assert.That(UnityEngine.Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None),Is.Empty);
        var previous=Physics2D.simulationMode;GameObject root=null;RegionTerrainPhysics geometry=null;RegionRouteQueue queue=null;
        try
        {
            Physics2D.simulationMode=SimulationMode2D.Script;root=new GameObject("owned executable Camp corner");var source=new CapturedCampTerrain();geometry=new RegionTerrainPhysics(source.State,root.transform);var terrain=new ExecutableCampTerrain(source,geometry);
            var actor=new GameObject("owned captured Buddy2 native traversal");actor.transform.SetParent(root.transform);actor.layer=6;actor.transform.localScale=Vector3.one*1.2f;
            var body=actor.AddComponent<Rigidbody2D>();body.gravityScale=0;body.freezeRotation=true;actor.AddComponent<BoxCollider2D>().size=Vector2.one;
            var start=new Vector2(52.1599731f,72.0149841f);var entry=new Vector2(52.2f,72);var oldNext=new Vector2(52.2f,71.4f);var goal=new Vector2(51.4f,49.6051941f);
            body.position=start;actor.transform.position=start;Physics2D.SyncTransforms();var half=TileMover.GetMapClearanceExtents(body,.1f);
            Assert.That(half.x,Is.EqualTo(.5999985f).Within(.000004f));Assert.That(half.y,Is.EqualTo(.5999985f).Within(.000004f));
            Assert.That(TileMover.CanTraverseBox(source,start,entry,half),Is.True,"historical nominal false positive retained as evidence");
            Assert.That(TileMover.CanOccupyBox(terrain,entry,half),Is.False,"native-overlapping entry must not be advertised as free");
            Assert.That(TileMover.CanTraverseBox(terrain,start,entry,half),Is.False,"do not push deeper into native contact");
            Assert.That(TileMover.CanTraverseBox(terrain,entry,oldNext,half),Is.False);
            Assert.That(TileMover.CanTraverseBox(terrain,start,start+Vector2.up*.3f,half),Is.True,"resting contact may safely separate");
            var binding=actor.AddComponent<ActorTerrainBinding>();binding.Bind(terrain);binding.Ready=()=>true;binding.MinimumBodyRadius=.1f;
            queue=new RegionRouteQueue(source.State,terrain,geometry.Logical,geometry.World);
            var status=queue.RequestPath(binding,start,goal,half,out var route);int slices=0;
            while(status==GridRouteStatus.Pending){queue.Advance(128,100);Assert.That(++slices,Is.LessThan(1000));status=queue.RequestPath(binding,start,goal,half,out route);}
            TestContext.WriteLine($"CONTRACT_ROUTE status={status} slices={slices} nodes={route?.Count??0} actualHalf={NativeVector(half)} planningHalf={NativeVector(geometry.TerrainPlanningExtents(half))}");
            Assert.That(status,Is.EqualTo(GridRouteStatus.Succeeded));Assert.That(route,Is.Not.Empty);
            var prior=start;for(int i=0;i<route.Count;i++){TestContext.WriteLine($"CONTRACT_NODE index={i} p={NativeVector(route[i])}");Assert.That(TileMover.CanTraverseBox(terrain,prior,route[i],half),Is.True);Assert.That(TileMover.CanOccupyBox(terrain,route[i],half),Is.True);prior=route[i];}
            binding.RequestRoute=(from,to)=>new ActorRouteResult(queue.RequestPath(binding,from,to,TileMover.GetMapClearanceExtents(body,.1f),out var supplied),supplied,source.State.Revision);
            binding.ReadRevision=()=>source.State.Revision;binding.AcceptCachedRoute=r=>r==source.State.Revision;
            VerifyNativeWaypointConsumers(terrain,binding,body);
            body.position=start;actor.transform.position=start;Physics2D.SyncTransforms();
            var contacts=new ContactPoint2D[64];bool terrainContact=false;int cursor=0,firstClear=-1;
            for(int n=-5;n<150;n++)
            {
                var p=body.position;half=TileMover.GetMapClearanceExtents(body,.1f);Vector2 velocity=Vector2.zero;
                if(n>=0)
                {
                    cursor=RouteWaypointPolicy.Advance(route,cursor,p,binding,.1f);
                    if(cursor>=route.Count)break;
                    var delta=route[cursor]-p;velocity=delta.normalized*Mathf.Min(1.575f,delta.magnitude/.02f);
                    Assert.That(TileMover.CanTraverseBox(terrain,p,p+velocity*.02f,half),Is.True,"published direction must remain executable");
                    if(body.position.y<71.6f&&firstClear<0)firstClear=n;
                }
                body.linearVelocity=velocity;Physics2D.Simulate(.02f);int count=body.GetContacts(contacts);Assert.That(count,Is.LessThan(contacts.Length));
                for(int c=0;c<count;c++)if(contacts[c].collider?.name=="Terrain boundary patch 5,4"||contacts[c].otherCollider?.name=="Terrain boundary patch 5,4")terrainContact=true;
                TestContext.WriteLine($"CONTRACT_NATIVE step={n} cursor={cursor} from={NativeVector(p)} requested={NativeVector(velocity)} after={NativeVector(body.position)} post={NativeVector(body.linearVelocity)} contacts={count}");
            }
            TestContext.WriteLine($"CONTRACT_RESULT firstClear={firstClear} end={NativeVector(body.position)} terrainContacts={terrainContact} queries={geometry.ClearanceQueries} rectangles={geometry.ClearanceRectangles}");
            Assert.That(terrainContact,Is.True,"recorded native contact must actually occur");
            Assert.That(firstClear,Is.GreaterThanOrEqualTo(0),"native contact must permit reaching an actually clear following connector");
            Assert.That(body.position.y,Is.LessThan(71.6f),"body must physically traverse the corner, not merely retain its entry");
        }
        finally{try{queue?.Dispose();if(root!=null)UnityEngine.Object.DestroyImmediate(root);geometry?.Dispose();}finally{Physics2D.simulationMode=previous;}}
    }
    static void VerifyNativeWaypointConsumers(ExecutableCampTerrain terrain,ActorTerrainBinding binding,Rigidbody2D body)
    {
        var half=TileMover.GetMapClearanceExtents(body,.1f);Vector2 near=default,entry=default,next=default;bool found=false;
        // Bounded fixture sampling at the captured authored corner, not a runtime route search.
        // The old overlapping entry is already rejected above; use a valid contact-aware entry here.
        for(int i=0;i<24&&!found;i++)
        {
            entry=new Vector2(52.20f+i*.005f,72.3f);near=entry-Vector2.right*.06f;next=entry-Vector2.up*.6f;
            found=TileMover.CanOccupyBox(terrain,entry,half)&&TileMover.CanOccupyBox(terrain,next,half)&&
                TileMover.CanTraverseBox(terrain,near,entry,half)&&TileMover.CanTraverseBox(terrain,entry,next,half)&&
                !TileMover.CanTraverseBox(terrain,near,next,half);
        }
        Assert.That(found,Is.True,"authored native corner must expose the nearby-entry/blocked-next contract");
        TestContext.WriteLine($"WAYPOINT_NATIVE near={NativeVector(near)} entry={NativeVector(entry)} next={NativeVector(next)} half={NativeVector(half)}");
        body.position=near;body.transform.position=near;Physics2D.SyncTransforms();
        var route=new List<Vector2>{entry,next};
        Assert.That(RouteWaypointPolicy.Advance(route,0,near,binding,.1f),Is.Zero);
        Assert.That(RouteWaypointPolicy.Reached(route,0,near,binding,.1f,.072f),Is.False);
        var follower=new RegionRouteFollower();
        void Seed(object target,string name,object value)=>target.GetType().GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(target,value);
        Seed(follower,"path",new List<Vector2>(route));Seed(follower,"goal",next);Seed(follower,"revision",binding.Revision);
        Assert.That(follower.Direction(binding,near,next,out var direction),Is.True);
        Assert.That(follower.CurrentWaypoint,Is.EqualTo(entry));Assert.That(direction.x,Is.GreaterThan(0));
        var unit=body.gameObject.AddComponent<BuddyUnit>();unit.unitData=new GobboUnitSaveData{moveSpeed=1.575f};
        try
        {
            foreach(var type in new[]{typeof(BuddyFollow),typeof(BuddyCombat),typeof(BuddyScavenger)})
            {
                var consumer=(MonoBehaviour)body.gameObject.AddComponent(type);consumer.enabled=false;
                try
                {
                    Seed(consumer,"rb",body);if(type!=typeof(BuddyFollow))Seed(consumer,"unit",unit);
                    var field=type.GetField("currentPath",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                    var owned=(List<Vector2>)field.GetValue(consumer);owned.Clear();owned.AddRange(route);
                    Seed(consumer,"pathIndex",0);Seed(consumer,"repathTimer",1f);Seed(consumer,"lastPathGoal",next);
                    Seed(consumer,type==typeof(BuddyFollow)?"boundRouteRevision":"regionPathRevision",binding.Revision);
                    string method=type==typeof(BuddyFollow)?"GetFollowDirection":"GetPathDirection";
                    var result=(Vector2)type.GetMethod(method,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(consumer,new object[]{next});
                    Assert.That((int)type.GetField("pathIndex",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(consumer),Is.Zero,type.Name+" must retain recovered intermediate entry");
                    Assert.That(result.x,Is.GreaterThan(0),type.Name+" must aim toward executable entry");
                }
                finally{UnityEngine.Object.DestroyImmediate(consumer);}
            }
        }
        finally{UnityEngine.Object.DestroyImmediate(unit);}
        int cursor=0,stalls=0;
        for(int tick=0;tick<100;tick++)
        {
            var before=body.position;cursor=RouteWaypointPolicy.Advance(route,cursor,before,binding,.1f);
            if(cursor>=route.Count)break;
            var offset=route[cursor]-before;var velocity=offset.normalized*Mathf.Min(1.575f,offset.magnitude/.02f);
            Assert.That(TileMover.CanTraverseBox(terrain,before,before+velocity*.02f,half),Is.True);
            body.linearVelocity=velocity;Physics2D.Simulate(.02f);
            if((body.position-before).sqrMagnitude<.00000001f)stalls++;
        }
        Assert.That(cursor,Is.EqualTo(route.Count),"real native body must traverse retained corner under shared completion");
        Assert.That(stalls,Is.Zero);Assert.That(body.position.y,Is.LessThan(71.8f));
        TestContext.WriteLine($"WAYPOINT_NATIVE_RESULT final={NativeVector(body.position)} cursor={cursor} stalls={stalls}");
    }
    sealed class NativeTerrainFixture:ICompleteTerrainDefinition,IDiggableTerrain,IExecutableTerrainClearance,IDisposable
    {
        public readonly HashSet<Vector2Int> Walls=new HashSet<Vector2Int>();public readonly GameObject Root;public readonly RegionTerrainState State;
        public RegionTerrainPhysics TerrainPhysics{get;}public float CellSize=>.6f;readonly LogicalGridTransform grid=new LogicalGridTransform(default,.6,32);
        public NativeTerrainFixture(bool narrow=false,bool plug=false)
        {
            if(narrow){for(int y=1;y<31;y++)for(int x=1;x<31;x++)if(y!=15&&y!=16)Walls.Add(new Vector2Int(x,y));if(plug){Walls.Add(new Vector2Int(16,15));Walls.Add(new Vector2Int(16,16));}}
            else{for(int y=12;y<=16;y++)for(int x=12;x<=16;x++)Walls.Add(new Vector2Int(x,y));for(int y=5;y<=24;y++)if(y<14||y>18)Walls.Add(new Vector2Int(20,y));}
            State=new RegionTerrainState("owned native-clearance fixture",32,32,this);Root=new GameObject("owned native-clearance fixture");TerrainPhysics=new RegionTerrainPhysics(State,Root.transform);State.Committed+=Publish;
        }
        void Publish(IReadOnlyList<DualTile> cells,int revision)=>TerrainPhysics.Publish(cells);
        public bool Protected(int x,int y)=>x<=0||y<=0||x>=31||y>=31;public bool PlacementAllowed(int x,int y)=>!Protected(x,y);
        public bool TrySolid(int x,int y,out bool solid){solid=Protected(x,y)||Walls.Contains(new Vector2Int(x,y));return true;}
        public bool IsBlocked(Vector2Int p)=>State.World.IsSolid(p.x,p.y);public bool IsDiggable(Vector2Int p)=>!Protected(p.x,p.y)&&IsBlocked(p);
        public Vector2Int WorldToCell(Vector2 p){var q=grid.ToLogical(new Point2(p.x,p.y));return new Vector2Int((int)Math.Floor(q.X),(int)Math.Floor(q.Y));}
        public Vector2 CellToWorld(Vector2Int c)=>World(new Point2(c.x+.5,c.y+.5));public Point2 Logical(Vector2 p)=>grid.ToLogical(new Point2(p.x,p.y));public Vector2 World(Point2 p){var q=grid.ToWorld(p);return new Vector2((float)q.X,(float)q.Y);}
        public void DigCircle(Vector2 p,float r)=>throw new InvalidOperationException("explicit synthetic commit only");public TerrainDigResult DigCircle(Vector2 p,float r,TerrainDigAuthority a,int strength,IReadOnlyCollection<Vector2Int> cells)=>throw new InvalidOperationException("explicit synthetic commit only");
        public ActorTerrainBinding Body(Vector2 p,float half,int layer=6)
        {var go=new GameObject("owned real native body");go.transform.SetParent(Root.transform);go.layer=layer;var rb=go.AddComponent<Rigidbody2D>();rb.gravityScale=0;rb.freezeRotation=true;go.AddComponent<BoxCollider2D>().size=Vector2.one*half*2;go.transform.position=p;rb.position=p;var b=go.AddComponent<ActorTerrainBinding>();b.Bind(this);b.Ready=()=>true;b.MinimumBodyRadius=.1f;Physics2D.SyncTransforms();return b;}
        public void Dispose(){State.Committed-=Publish;UnityEngine.Object.DestroyImmediate(Root);TerrainPhysics.Dispose();}
    }
    static List<Vector2> PublishedNativeRoute(NativeTerrainFixture f,ActorTerrainBinding actor,Vector2 goal,RegionRouteQueue queue)
    {
        var rb=actor.GetComponent<Rigidbody2D>();var half=TileMover.GetMapClearanceExtents(rb,.1f);var status=queue.RequestPath(actor,rb.position,goal,half,out var route);int slices=0;
        while(status==GridRouteStatus.Pending){queue.Advance(128,100);Assert.That(++slices,Is.LessThan(1000));status=queue.RequestPath(actor,rb.position,goal,half,out route);}
        Assert.That(status,Is.EqualTo(GridRouteStatus.Succeeded));
        actor.RequestRoute=(from,to)=>new ActorRouteResult(queue.RequestPath(actor,from,to,TileMover.GetMapClearanceExtents(rb,.1f),out var supplied),supplied,f.State.Revision);
        return route;
    }
    static int PhysicallyConsumePublishedNativeRoute(NativeTerrainFixture f,ActorTerrainBinding actor,List<Vector2> route,int limit=500)
    {
        var rb=actor.GetComponent<Rigidbody2D>();var box=actor.GetComponent<BoxCollider2D>();var half=TileMover.GetMapClearanceExtents(rb,.1f);var prior=rb.position;int cursor=0;
        foreach(var node in route)
        {
            Assert.That(TileMover.CanOccupyBox(f,node,half),Is.True);Assert.That(TileMover.CanTraverseBox(f,prior,node,half),Is.True);
            foreach(var collider in f.Root.GetComponentsInChildren<PolygonCollider2D>())if(collider.enabled){var distance=box.Distance(node,0,collider,collider.attachedRigidbody.position,0);Assert.That(distance.isValid,Is.True);Assert.That(distance.distance,Is.GreaterThanOrEqualTo(0),"published waypoint must be clear in native geometry");}
            prior=node;
        }
        int blocked=0;
        for(int n=0;n<limit;n++)
        {
            var p=rb.position;half=TileMover.GetMapClearanceExtents(rb,.1f);cursor=RouteWaypointPolicy.Advance(route,cursor,p,actor,.1f);
            if(cursor==route.Count)return n;var delta=route[cursor]-p;var velocity=delta.normalized*Mathf.Min(3.5f,delta.magnitude/.02f);
            Assert.That(TileMover.CanTraverseBox(f,p,p+velocity*.02f,half),Is.True,"full native connector must remain clear during motion");
            rb.linearVelocity=velocity;Physics2D.Simulate(.02f);if((rb.position-p).sqrMagnitude<.0000000001f&&delta.magnitude>.001f)blocked++;else blocked=0;Assert.That(blocked,Is.LessThan(6),"accepted route must not repeatedly produce zero realized motion");
        }
        Assert.Fail("native route did not complete within unchanged bounded fixture steps");return limit;
    }
    [TestCase(.22f)][TestCase(.375f)][TestCase(.525f)][TestCase(.6f)][TestCase(.75f)][TestCase(.9f)]
    public void NativeCardinalDiagonalAndTurningRoutesPreserveActualBody(float half)
    {
        var mode=Physics2D.simulationMode;try
        {
            Physics2D.simulationMode=SimulationMode2D.Script;
            foreach(float phase in new[]{.01f,.27f,.59f})
            {
                using var f=new NativeTerrainFixture();var actor=f.Body(new Vector2(6.3f+phase,6.3f+phase),half);var goal=new Vector2(14.1f+phase,14.1f+phase);
                using var queue=new RegionRouteQueue(f.State,f,f.Logical,f.World);var route=PublishedNativeRoute(f,actor,goal,queue);
                int steps=PhysicallyConsumePublishedNativeRoute(f,actor,route);TestContext.WriteLine($"NATIVE_BODY half={half:R} phase={phase:R} nodes={route.Count} steps={steps} queries={f.TerrainPhysics.ClearanceQueries}");
                Assert.That(actor.GetComponent<BoxCollider2D>().size,Is.EqualTo(Vector2.one*half*2));
            }
        }
        finally{Physics2D.simulationMode=mode;}
    }
    [TestCase(.45f,true)][TestCase(.525f,true)][TestCase(.6f,false)]
    public void NativeNarrowPassageDistinguishesPhysicalFitFromNominalTouch(float half,bool fits)
    {
        var mode=Physics2D.simulationMode;try
        {
            Physics2D.simulationMode=SimulationMode2D.Script;using var f=new NativeTerrainFixture(true);var actor=f.Body(f.World(new Point2(5,16)),half);var body=actor.GetComponent<Rigidbody2D>();var extents=TileMover.GetMapClearanceExtents(body,.1f);var goal=f.World(new Point2(27,16));
            bool nativeClear=true;foreach(var c in f.Root.GetComponentsInChildren<PolygonCollider2D>())if(c.enabled&&actor.GetComponent<BoxCollider2D>().Distance(c).distance<0)nativeClear=false;
            Assert.That(nativeClear,Is.EqualTo(fits),"fit classification comes from unchanged native body/terrain");Assert.That(TileMover.CanOccupyBox(f,body.position,extents),Is.EqualTo(fits));
            using var queue=new RegionRouteQueue(f.State,f,f.Logical,f.World);
            if(fits){var route=PublishedNativeRoute(f,actor,goal,queue);int steps=PhysicallyConsumePublishedNativeRoute(f,actor,route);TestContext.WriteLine($"NARROW half={half:R} nativeFits={nativeClear} steps={steps}");}
            else{var status=queue.RequestPath(actor,body.position,goal,extents,out _);Assert.That(status,Is.EqualTo(GridRouteStatus.Unreachable));}
        }
        finally{Physics2D.simulationMode=mode;}
    }
    [Test]public void NativeExcavationRebuildReopensExistingRouteWithoutStaleCollision()
    {
        var mode=Physics2D.simulationMode;try
        {
            Physics2D.simulationMode=SimulationMode2D.Script;using var f=new NativeTerrainFixture(true,true);var actor=f.Body(f.World(new Point2(5,16)),.525f);var body=actor.GetComponent<Rigidbody2D>();var extents=TileMover.GetMapClearanceExtents(body,.1f);var goal=f.World(new Point2(27,16));using var queue=new RegionRouteQueue(f.State,f,f.Logical,f.World);
            var status=queue.RequestPath(actor,body.position,goal,extents,out _);int slices=0;while(status==GridRouteStatus.Pending){queue.Advance(128,100);Assert.That(++slices,Is.LessThan(1000));status=queue.RequestPath(actor,body.position,goal,extents,out _);}Assert.That(status,Is.EqualTo(GridRouteStatus.Unreachable));
            Assert.That(f.State.Commit(new[]{new DualTile(16,15),new DualTile(16,16)},f.State.Revision,_=>true),Is.EqualTo(2));
            var route=PublishedNativeRoute(f,actor,goal,queue);int steps=PhysicallyConsumePublishedNativeRoute(f,actor,route);Assert.That(queue.RouteRevision(actor),Is.EqualTo(f.State.Revision));TestContext.WriteLine($"NATIVE_REBUILD revision={f.State.Revision} nativeSteps={steps}");
        }
        finally{Physics2D.simulationMode=mode;}
    }
    [TestCase(6,6)][TestCase(6,7)][TestCase(3,7)]
    [Category("Stage64DHistoricalDiagnostic")]
    [Ignore("Historical raw-velocity/zero-nominal-overlap diagnostic; not production collision acceptance. Original failures archived in TerrainClearanceContract-20261030.")]
    public void NativeCreatureAndPitBodyContactsRemainFirm(int layerA,int layerB)
    {
        var mode=Physics2D.simulationMode;try
        {
            Physics2D.simulationMode=SimulationMode2D.Script;using var f=new NativeTerrainFixture();var a=f.Body(new Vector2(4,4),.375f,layerA);var b=f.Body(new Vector2(6,4),.6f,layerB);var ca=a.GetComponent<Collider2D>();var cb=b.GetComponent<Collider2D>();
            if(layerA==6&&layerB==6)
            {
                using var coordinator=new RegionMovementCoordinator(f,true);coordinator.Configure(a,"owned Pit fighter a",1,true,false,true);coordinator.Configure(b,"owned Pit fighter b",1,true,false,true);
                var rules=new MovementSession();rules.Fighters.UnionWith(new[]{a.gameObject,b.gameObject});a.gameObject.AddComponent<CombatSessionBinding>().Rules=rules;b.gameObject.AddComponent<CombatSessionBinding>().Rules=rules;coordinator.RefreshContacts();Assert.That(coordinator.Role(a),Is.EqualTo(MovementContactRole.Strict));Assert.That(Physics2D.GetIgnoreCollision(ca,cb),Is.False);
            }
            var ra=a.GetComponent<Rigidbody2D>();var rb=b.GetComponent<Rigidbody2D>();
            for(int n=0;n<40;n++){ra.linearVelocity=Vector2.right*2;rb.linearVelocity=Vector2.left*2;Physics2D.Simulate(.02f);Assert.That(rb.position.x-ra.position.x,Is.GreaterThanOrEqualTo(.975f),"actual core bodies may not overlap or pass through");}
            Assert.That(ca.Distance(cb).distance,Is.LessThan(.01f),"actual contact must occur");TestContext.WriteLine($"NATIVE_PAIR layers={layerA}/{layerB} separation={rb.position.x-ra.position.x:R} queryProxyNotObstacle=True");
        }
        finally{Physics2D.simulationMode=mode;}
    }
    static (float minimum,float final,int contacts) RawNativePairComparison(int layerA,int layerB,bool derivedTerrain)
    {
        var root=new GameObject("owned before-after native pair comparison");RegionTerrainPhysics geometry=null;
        try
        {
            if(derivedTerrain)geometry=new RegionTerrainPhysics(new CapturedCampTerrain().State,root.transform);
            Rigidbody2D Make(string name,Vector2 p,float half,int layer)
            {var go=new GameObject(name);go.transform.SetParent(root.transform);go.layer=layer;var rb=go.AddComponent<Rigidbody2D>();rb.gravityScale=0;rb.freezeRotation=true;go.AddComponent<BoxCollider2D>().size=Vector2.one*half*2;go.transform.position=p;rb.position=p;return rb;}
            var a=Make("owned pair left",new Vector2(4,4),.375f,layerA);var b=Make("owned pair right",new Vector2(6,4),.6f,layerB);Physics2D.SyncTransforms();
            var nominal=new Fixture();var ba=a.gameObject.AddComponent<ActorTerrainBinding>();ba.Bind(nominal);ba.Ready=()=>true;var bb=b.gameObject.AddComponent<ActorTerrainBinding>();bb.Bind(nominal);bb.Ready=()=>true;
            if(layerA==6&&layerB==6)
            {
                using var coordinator=new RegionMovementCoordinator(nominal,true);coordinator.Configure(ba,"owned Pit fighter a",1,true,false,true);coordinator.Configure(bb,"owned Pit fighter b",1,true,false,true);
                var rules=new MovementSession();rules.Fighters.UnionWith(new[]{a.gameObject,b.gameObject});a.gameObject.AddComponent<CombatSessionBinding>().Rules=rules;b.gameObject.AddComponent<CombatSessionBinding>().Rules=rules;coordinator.RefreshContacts();Assert.That(coordinator.Role(ba),Is.EqualTo(MovementContactRole.Strict));Assert.That(Physics2D.GetIgnoreCollision(a.GetComponent<Collider2D>(),b.GetComponent<Collider2D>()),Is.False);
            }
            TestContext.WriteLine($"RAW_PAIR_SETUP layers={layerA}/{layerB} derived={derivedTerrain} layerIgnored={Physics2D.GetIgnoreLayerCollision(layerA,layerB)} pairIgnored={Physics2D.GetIgnoreCollision(a.GetComponent<Collider2D>(),b.GetComponent<Collider2D>())}");
            float minimum=float.PositiveInfinity;int count=0;var buffer=new ContactPoint2D[16];
            for(int n=0;n<40;n++){a.linearVelocity=Vector2.right*2;b.linearVelocity=Vector2.left*2;Physics2D.Simulate(.02f);minimum=Mathf.Min(minimum,b.position.x-a.position.x);count+=a.GetContacts(buffer);}
            return (minimum,b.position.x-a.position.x,count);
        }
        finally{UnityEngine.Object.DestroyImmediate(root);geometry?.Dispose();}
    }
    [TestCase(6,6)][TestCase(6,7)][TestCase(3,7)]
    public void RawNativePeerFailureIsComparedWithoutWeakeningItsStrictAssertion(int layerA,int layerB)
    {
        var mode=Physics2D.simulationMode;try
        {
            Physics2D.simulationMode=SimulationMode2D.Script;
            var baseline=RawNativePairComparison(layerA,layerB,false);var derived=RawNativePairComparison(layerA,layerB,true);
            TestContext.WriteLine($"RAW_PAIR_BASELINE layers={layerA}/{layerB} withoutTerrainMinimum={baseline.minimum:R} withTerrainMinimum={derived.minimum:R} withoutTerrainFinal={baseline.final:R} withTerrainFinal={derived.final:R} contacts={baseline.contacts}/{derived.contacts} directVelocityBypassesMovementCoordinator=True");
            Assert.That(derived.minimum,Is.EqualTo(baseline.minimum));Assert.That(derived.final,Is.EqualTo(baseline.final));Assert.That(derived.contacts,Is.EqualTo(baseline.contacts));
            // The separate strict body-pair regression is retained unchanged. This only establishes attribution.
        }
        finally{Physics2D.simulationMode=mode;}
    }
    [Test]public void SharedClearanceBoundedQueryCostIsReportedSeparatelyFromGameplay()
    {
        var root=new GameObject("owned clearance microbenchmark");RegionTerrainPhysics geometry=null;
        try
        {
            var source=new CapturedCampTerrain();geometry=new RegionTerrainPhysics(source.State,root.transform);var native=new ExecutableCampTerrain(source,geometry);
            var a=new Vector2(52.5f,72.3f);var b=new Vector2(52.5f,70.5f);var half=Vector2.one*.525f;bool clear=true;
            for(int n=0;n<100;n++){TileMover.CanTraverseBox(source,a,b,half);TileMover.CanTraverseBox(native,a,b,half);}
            const int count=2000;var clock=new System.Diagnostics.Stopwatch();long bytes=GC.GetAllocatedBytesForCurrentThread();clock.Start();
            for(int n=0;n<count;n++)clear&=TileMover.CanTraverseBox(source,a,b,half);clock.Stop();double nominal=clock.Elapsed.TotalMilliseconds;long nominalAlloc=GC.GetAllocatedBytesForCurrentThread()-bytes;
            bytes=GC.GetAllocatedBytesForCurrentThread();clock.Restart();for(int n=0;n<count;n++)clear&=TileMover.CanTraverseBox(native,a,b,half);clock.Stop();double shared=clock.Elapsed.TotalMilliseconds;long sharedAlloc=GC.GetAllocatedBytesForCurrentThread()-bytes;
            bytes=GC.GetAllocatedBytesForCurrentThread();clock.Restart();for(int n=0;n<count;n++)clear&=TileMover.CanTraverseBox(native,a,b,Vector2.one*(n%2==0?.375f:.525f));clock.Stop();double mixed=clock.Elapsed.TotalMilliseconds;long mixedAlloc=GC.GetAllocatedBytesForCurrentThread()-bytes;
            bytes=GC.GetAllocatedBytesForCurrentThread();clock.Restart();for(int n=0;n<count;n++)clear&=TileMover.CanTraverseBox(native,new Vector2(52.1599731f,72.0149841f),new Vector2(52.1599731f,72.3149841f),Vector2.one*.5999985f);clock.Stop();double resting=clock.Elapsed.TotalMilliseconds;long restingAlloc=GC.GetAllocatedBytesForCurrentThread()-bytes;
            Assert.That(restingAlloc,Is.Zero,"resting-contact feasibility must not allocate per rectangle");
            Assert.That(clear,Is.True);Assert.That(sharedAlloc,Is.Zero,"steady-size terrain query must not add managed allocations");Assert.That(mixedAlloc,Is.Zero,"changing supported box size must not add managed allocations");
            TestContext.WriteLine($"CLEARANCE_COST queries={count} nominalMs={nominal:R} nominalAllocated={nominalAlloc} sharedMs={shared:R} sharedAllocated={sharedAlloc} mixedSizeMs={mixed:R} mixedAllocated={mixedAlloc} restingContactMs={resting:R} restingAllocated={restingAlloc} source=OwnedEditModeMicrobenchmarkNotGameplayCertification");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);geometry?.Dispose();}
    }
    [Test]public void NativeClearanceQueryCapabilityDoesNotAddAnObstacle()
    {
        var root=new GameObject("owned native query capability");RegionTerrainPhysics geometry=null;
        try
        {
            var t=new CapturedCampTerrain();geometry=new RegionTerrainPhysics(t.State,root.transform);
            PolygonCollider2D patch=null;foreach(var c in root.GetComponentsInChildren<PolygonCollider2D>())if(c.name=="Terrain boundary patch 5,4")patch=c;
            var go=new GameObject("owned unsimulated query shape");go.transform.SetParent(root.transform);var rb=go.AddComponent<Rigidbody2D>();rb.simulated=false;rb.gravityScale=0;
            var box=go.AddComponent<BoxCollider2D>();box.size=Vector2.one*1.2f;rb.position=new Vector2(52.1599731f,72.0149841f);go.transform.position=rb.position;Physics2D.SyncTransforms();
            var d=box.Distance(rb.position,0,patch,patch.attachedRigidbody.position,0);var shapes=new PhysicsShapeGroup2D();box.GetShapes(shapes);
            bool visible=false;foreach(var c in Physics2D.OverlapBoxAll(rb.position,Vector2.one*1.2f,0))if(c==box)visible=true;
            TestContext.WriteLine($"QUERY_CAPABILITY valid={d.isValid} distance={d.distance:R} worldVisible={visible} shapes={shapes.shapeCount} bodyClosest={NativeVector(box.ClosestPoint(new Vector2(54,72)))} terrainClosest={NativeVector(patch.ClosestPoint(new Vector2(52,71.1f)))}");
            Assert.That(d.isValid,Is.True,"direct geometric distance must work without a simulated obstacle");Assert.That(visible,Is.False,"query proxy must not become another world obstacle");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);geometry?.Dispose();}
    }
    sealed class NativeCornerResult
    {
        public int FirstClear=-1;public Vector2 End;public float Travel,NormalImpulse,TangentImpulse;public int ContactSamples;
    }
    static string NativeVector(Vector2 p)=>"("+p.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+p.y.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+")";
    static string NativeCollider(Collider2D c)=>c==null?"null":$"{c.GetInstanceID()}:{c.name}:body={c.attachedRigidbody?.GetInstanceID()??0}:layer={c.gameObject.layer}:trigger={c.isTrigger}";
    static void CornerMaterial(string condition,Collider2D c)
    {
        TestContext.WriteLine($"MATERIAL condition={condition} collider={NativeCollider(c)} material={c.sharedMaterial?.name??"null(default)"} friction={c.friction:R} frictionCombine={c.frictionCombine} bounce={c.bounciness:R} bounceCombine={c.bounceCombine}");
    }
    static void CornerContacts(string condition,string phase,int step,Rigidbody2D body,ContactPoint2D[] contacts,NativeCornerResult result)
    {
        int count=body.GetContacts(contacts);TestContext.WriteLine($"MANIFOLD condition={condition} phase={phase} step={step} count={count} capacity={contacts.Length}");
        Assert.That(count,Is.LessThan(contacts.Length),"bounded manifold must not silently truncate");
        for(int i=0;i<count;i++)
        {
            var c=contacts[i];
            TestContext.WriteLine($"CONTACT condition={condition} phase={phase} step={step} index={i} a={NativeCollider(c.collider)} b={NativeCollider(c.otherCollider)} point={NativeVector(c.point)} normal={NativeVector(c.normal)} separation={c.separation:R} normalImpulse={c.normalImpulse:R} tangentImpulse={c.tangentImpulse:R} friction={c.friction:R} relativeVelocity={NativeVector(c.relativeVelocity)} enabled={c.enabled}");
            if(phase=="AfterPhysics"){result.NormalImpulse+=Mathf.Abs(c.normalImpulse);result.TangentImpulse+=Mathf.Abs(c.tangentImpulse);result.ContactSamples++;}
        }
    }
    static void CornerNativeSeparation(string condition,string label,Vector2 pose,BoxCollider2D box,PolygonCollider2D patch,CapturedCampTerrain terrain,Vector2 half)
    {
        var d=box.Distance(pose,0,patch,patch.attachedRigidbody.position,patch.attachedRigidbody.rotation);
        TestContext.WriteLine($"NATIVE_DISTANCE condition={condition} label={label} pose={NativeVector(pose)} valid={d.isValid} overlap={d.isOverlapped} distance={d.distance:R} normal={NativeVector(d.normal)} a={NativeVector(d.pointA)} b={NativeVector(d.pointB)} logicalOccupy={TileMover.CanOccupyBox(terrain,pose,half)}");
    }
    static void CornerNativeShapes(string condition,Collider2D c)
    {
        var group=new PhysicsShapeGroup2D();c.GetShapes(group);TestContext.WriteLine($"SHAPES condition={condition} collider={NativeCollider(c)} count={group.shapeCount}");
        Assert.That(group.shapeCount,Is.LessThan(128),"owned shape export must stay bounded");
        for(int n=0;n<group.shapeCount;n++)
        {
            var shape=group.GetShape(n);TestContext.WriteLine($"SHAPE condition={condition} collider={c.GetInstanceID()} index={n} type={shape.shapeType} radius={shape.radius:R} vertices={shape.vertexCount}");
            Assert.That(shape.vertexCount,Is.LessThan(32));
            for(int k=0;k<shape.vertexCount;k++)TestContext.WriteLine($"SHAPE_VERTEX condition={condition} collider={c.GetInstanceID()} shape={n} vertex={k} world={NativeVector(group.localToWorldMatrix.MultiplyPoint3x4(group.GetShapeVertex(n,k)))}");
        }
    }
    static void CornerGeometry(string condition,string label,Vector2 pose,Vector2 half,CapturedCampTerrain terrain,PolygonCollider2D patch,BoxCollider2D box)
    {
        var entry=new Vector2(52.2f,72f);var following=new Vector2(52.2f,71.4f);
        TestContext.WriteLine($"FOOTPRINT condition={condition} label={label} position={NativeVector(pose)} half={NativeVector(half)} min={NativeVector(pose-half)} max={NativeVector(pose+half)} occupy={TileMover.CanOccupyBox(terrain,pose,half)} entryConnector={TileMover.CanTraverseBox(terrain,pose,entry,half)} followingConnector={TileMover.CanTraverseBox(terrain,pose,following,half)}");
        CornerNativeSeparation(condition,label,pose,box,patch,terrain,half);
        var connector=following-pose;var hits=new List<RaycastHit2D>();int nativeHits=box.Cast(pose,0,connector.normalized,new ContactFilter2D().NoFilter(),hits,connector.magnitude,true);
        Assert.That(nativeHits,Is.LessThan(64),"bounded native cast export");
        TestContext.WriteLine($"NATIVE_CAST condition={condition} label={label} from={NativeVector(pose)} to={NativeVector(following)} hits={nativeHits} logicalConnector={TileMover.CanTraverseBox(terrain,pose,following,half)}");
        foreach(var hit in hits)TestContext.WriteLine($"NATIVE_CAST_HIT condition={condition} label={label} collider={NativeCollider(hit.collider)} distance={hit.distance:R} fraction={hit.fraction:R} point={NativeVector(hit.point)} normal={NativeVector(hit.normal)}");
        // Read-only native overlap query at the hypothetical footprint, not a body teleport.
        foreach(var overlap in Physics2D.OverlapBoxAll(pose,box.size*(Vector2)box.transform.lossyScale,0))
            if(overlap!=box)TestContext.WriteLine($"NATIVE_OVERLAP condition={condition} label={label} other={NativeCollider(overlap)}");
        if(label!="initial")return;
        CornerNativeShapes(condition,box);CornerNativeShapes(condition,patch);
        foreach(float dx in new[]{0f,.005f,.01f,.015f,.02f,.025f})foreach(float y in new[]{72f,71.4f})CornerNativeSeparation(condition,"measurement-only-offset-"+dx,new Vector2(entry.x+dx,y),box,patch,terrain,half);
        TestContext.WriteLine($"BODY condition={condition} size={NativeVector(box.size)} offset={NativeVector(box.offset)} scale={box.transform.lossyScale.ToString("F8")} edgeRadius={box.edgeRadius:R} mass={box.attachedRigidbody.mass:R} damping={box.attachedRigidbody.linearDamping:R} constraints={box.attachedRigidbody.constraints} collisionMode={box.attachedRigidbody.collisionDetectionMode}");
        TestContext.WriteLine($"PATCH condition={condition} collider={NativeCollider(patch)} paths={patch.pathCount} min={NativeVector(patch.bounds.min)} max={NativeVector(patch.bounds.max)}");
        for(int n=0;n<patch.pathCount;n++)
        {
            var vertices=patch.GetPath(n);for(int k=0;k<vertices.Length;k++)TestContext.WriteLine($"VERTEX condition={condition} path={n} vertex={k} world={NativeVector(patch.transform.TransformPoint(vertices[k]))}");
        }
        for(int y=69;y<=76;y++)for(int x=84;x<=90;x++)
        {
            var c=new Vector2Int(x,y);var center=terrain.CellToWorld(c);TestContext.WriteLine($"CELL condition={condition} x={x} y={y} solid={terrain.IsBlocked(c)} center={NativeVector(center)} min={NativeVector(center-Vector2.one*terrain.CellSize*.5f)} max={NativeVector(center+Vector2.one*terrain.CellSize*.5f)}");
        }
    }
    static NativeCornerResult ObserveCapturedNativeCorner(string condition,bool zeroFriction)
    {
        Assert.That(UnityEngine.Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None),Is.Empty,"manual physics requires no unrelated scene bodies");
        var priorSimulation=Physics2D.simulationMode;GameObject root=null;RegionTerrainPhysics geometry=null;PhysicsMaterial2D diagnosticMaterial=null;
        var changedMaterials=new Dictionary<Collider2D,PhysicsMaterial2D>();var result=new NativeCornerResult();
        try
        {
            // Every setting change and all setup are inside exception-safe cleanup.
            Physics2D.simulationMode=SimulationMode2D.Script;root=new GameObject("owned authored Camp collision");
            var terrain=new CapturedCampTerrain();geometry=new RegionTerrainPhysics(terrain.State,root.transform);
            var actor=new GameObject("owned captured Buddy2 body");actor.transform.SetParent(root.transform);actor.layer=6;actor.transform.localScale=Vector3.one*1.2f;
            var body=actor.AddComponent<Rigidbody2D>();body.gravityScale=0;body.freezeRotation=true;var box=actor.AddComponent<BoxCollider2D>();box.size=Vector2.one;
            var position=new Vector2(52.1599731f,72.0149841f);var entry=new Vector2(52.2f,72f);var following=new Vector2(52.2f,71.4f);
            body.position=position;actor.transform.position=position;Physics2D.SyncTransforms();
            var half=TileMover.GetMapClearanceExtents(body,.1f);PolygonCollider2D patch=null;
            foreach(var c in root.GetComponentsInChildren<PolygonCollider2D>())if(c.name=="Terrain boundary patch 5,4")patch=c;
            Assert.That(patch,Is.Not.Null);
            TestContext.WriteLine($"CONDITION name={condition} nonproduction={zeroFriction} dt=.02 steps=150 settle=5 velocityIterations={Physics2D.velocityIterations} positionIterations={Physics2D.positionIterations} contactOffset={Physics2D.defaultContactOffset:R} maxCorrection={Physics2D.maxLinearCorrection:R}");
            CornerMaterial(condition+"-original",box);CornerMaterial(condition+"-original",patch);
            if(zeroFriction)
            {
                // A causal comparison only. Both contact surfaces change friction, nothing else.
                diagnosticMaterial=new PhysicsMaterial2D("NONPRODUCTION owned zero-friction diagnostic"){friction=0,bounciness=box.bounciness,frictionCombine=box.frictionCombine,bounceCombine=box.bounceCombine};
                foreach(var c in root.GetComponentsInChildren<Collider2D>()){changedMaterials.Add(c,c.sharedMaterial);c.sharedMaterial=diagnosticMaterial;}
                CornerMaterial(condition+"-diagnostic",box);CornerMaterial(condition+"-diagnostic",patch);
            }
            CornerGeometry(condition,"initial",position,half,terrain,patch,box);CornerGeometry(condition,"entry",entry,half,terrain,patch,box);CornerGeometry(condition,"following",following,half,terrain,patch,box);
            Assert.That(half.x,Is.EqualTo(.5999985f).Within(.000004f));Assert.That(half.y,Is.EqualTo(.5999985f).Within(.000004f));
            Assert.That(Vector2.Distance(position,entry),Is.LessThan(terrain.CellSize*.12f));
            Assert.That(TileMover.CanTraverseBox(terrain,position,entry,half),Is.True,"captured recovered entry");
            Assert.That(TileMover.CanTraverseBox(terrain,position,following,half),Is.False,"captured next connector");
            var physics=Physics2D.defaultPhysicsScene;var contacts=new ContactPoint2D[64];bool recordedPatch=false;
            for(int n=-5;n<150;n++)
            {
                bool settling=n<0;var p=body.position;var pre=body.linearVelocity;half=TileMover.GetMapClearanceExtents(body,.1f);
                bool occupy=TileMover.CanOccupyBox(terrain,p,half),entryClear=TileMover.CanTraverseBox(terrain,p,entry,half),clear=TileMover.CanTraverseBox(terrain,p,following,half);
                if(!settling&&clear&&result.FirstClear<0)result.FirstClear=n;
                var target=clear?following:entry;var delta=target-p;var requested=settling?Vector2.zero:delta.normalized*Mathf.Min(1.575f,delta.magnitude/.02f);
                bool accepted=settling||TileMover.CanTraverseBox(terrain,p,p+requested*.02f,half);
                CornerContacts(condition,"BeforePhysics",n,body,contacts,result);
                body.linearVelocity=accepted?requested:Vector2.zero;var assigned=body.linearVelocity;physics.Simulate(.02f);var moved=body.position-p;
                TestContext.WriteLine($"MOTION condition={condition} step={n} settling={settling} p={NativeVector(p)} half={NativeVector(half)} target={NativeVector(target)} requested={NativeVector(requested)} beforeAssignment={NativeVector(pre)} presolver={NativeVector(assigned)} postsolver={NativeVector(body.linearVelocity)} realized={NativeVector(moved)} after={NativeVector(body.position)} occupy={occupy} entryClear={entryClear} nextClear={clear} stepClear={accepted}");
                CornerContacts(condition,"AfterPhysics",n,body,contacts,result);
                int count=body.GetContacts(contacts);for(int c=0;c<count;c++)if(contacts[c].collider?.name=="Terrain boundary patch 5,4"||contacts[c].otherCollider?.name=="Terrain boundary patch 5,4")recordedPatch=true;
                result.Travel=Mathf.Max(result.Travel,Vector2.Distance(position,body.position));
            }
            Assert.That(recordedPatch,Is.True,"must reproduce recorded native contact");result.End=body.position;
            CornerGeometry(condition,"stopping-or-final",body.position,TileMover.GetMapClearanceExtents(body,.1f),terrain,patch,box);
            TestContext.WriteLine($"RESULT condition={condition} firstClear={result.FirstClear} travel={result.Travel:R} end={NativeVector(result.End)} normalImpulse={result.NormalImpulse:R} tangentImpulse={result.TangentImpulse:R} contacts={result.ContactSamples}");return result;
        }
        finally
        {
            try
            {
                foreach(var item in changedMaterials)if(item.Key!=null)item.Key.sharedMaterial=item.Value;
                if(root!=null)UnityEngine.Object.DestroyImmediate(root);geometry?.Dispose();
            }
            finally
            {
                try{if(diagnosticMaterial!=null)UnityEngine.Object.DestroyImmediate(diagnosticMaterial);}
                finally{Physics2D.simulationMode=priorSimulation;}
            }
        }
    }
    [Category("Stage64DHistoricalDiagnostic")]
    [Test, Ignore("Historical nominal-only failed corner diagnostic. Current strict acceptance uses SharedExecutableContractRoutesCapturedCampCornerUnderNativePhysics; original assertions retained.")]public void CapturedCampBuddy2EntryAndNativeCornerContactReproduction()
    {
        var baseline=ObserveCapturedNativeCorner("BASELINE_A",false);
        // Only compare friction if the unchanged baseline supplies positive friction-impulse evidence.
        if(baseline.FirstClear<0&&baseline.TangentImpulse>.00001f)
        {
            var comparison=ObserveCapturedNativeCorner("NONPRODUCTION_ZERO_FRICTION_B",true);
            var restored=ObserveCapturedNativeCorner("BASELINE_RESTORED_A",false);
            TestContext.WriteLine($"COMPARISON baselineClear={baseline.FirstClear} diagnosticClear={comparison.FirstClear} restoredClear={restored.FirstClear} endDifference={Vector2.Distance(baseline.End,restored.End):R}");
            Assert.That(Vector2.Distance(baseline.End,restored.End),Is.LessThan(.0001f),"fresh baseline repeat must restore original physical condition");
        }
        // Keep the original strict physical-traversal regression failing; diagnostic B is not production success.
        Assert.That(baseline.FirstClear,Is.GreaterThanOrEqualTo(0),"native contact must permit reaching an actually clear following connector");
        Assert.That(baseline.End.y,Is.LessThan(71.6f),"body must physically traverse the corner, not merely retain its entry");
    }

    static Fixture CapturedRejoinTerrain()
    {
        var terrain=new Fixture(80);var definition=new ExpeditionLayout();
        for(int y=0;y<80;y++)for(int x=0;x<80;x++){definition.TrySolid(x,127-y,out bool solid);if(solid)terrain.Walls.Add(y*80+x);}
        terrain.Rebuild();return terrain;
    }
    static List<Vector2> CapturedBuddy4Route()=>new List<Vector2>{
            new Vector2(20.4000015f,21.6f),
            new Vector2(20.4000015f,22.2f),
            new Vector2(20.4000015f,22.8000011f),
            new Vector2(20.4000015f,23.4000015f),
            new Vector2(21f,23.4000015f),
            new Vector2(21f,24f),
            new Vector2(21.6f,24f),
            new Vector2(21.6f,24.6f),
            new Vector2(21.6f,25.2f),
            new Vector2(22.2f,25.2f),
            new Vector2(22.2f,25.8000011f),
            new Vector2(22.8000011f,25.8000011f),
            new Vector2(22.8000011f,26.4000015f),
            new Vector2(22.8000011f,27.0000019f),
            new Vector2(22.8000011f,27.6f),
            new Vector2(22.8000011f,28.2f),
            new Vector2(22.8000011f,28.8000011f),
            new Vector2(22.8000011f,29.4000015f),
            new Vector2(22.8000011f,30.0000019f),
            new Vector2(22.2f,30.0000019f),
            new Vector2(22.2f,30.6f),
            new Vector2(22.2f,31.2f),
            new Vector2(21.6f,31.2f),
            new Vector2(21.6f,31.8000011f),
            new Vector2(21.6f,32.4f),
            new Vector2(21.6f,33f),
            new Vector2(21f,33f),
            new Vector2(21f,33.6000023f),
            new Vector2(21f,34.2f),
            new Vector2(21f,34.8000031f),
            new Vector2(21f,35.4f),
            new Vector2(21f,36f),
            new Vector2(21f,36.6000023f),
            new Vector2(21f,37.2f),
            new Vector2(21f,37.8000031f),
            new Vector2(21f,38.4f),
            new Vector2(21f,39f),
            new Vector2(21f,39.6000023f),
            new Vector2(20.4000015f,39.6000023f),
            new Vector2(19.8000011f,39.6000023f),
            new Vector2(19.2f,39.6000023f),
            new Vector2(19.1600246f,39.39993f)
    };
    [Test]public void CapturedBuddy4CachedSuffixReconnectsAfterYieldUsingNativeClearance()
    {
        using var f=new MovementFixture(false,CapturedRejoinTerrain());
        var before=new Vector2(21.5572567f,23.9964352f);var after=new Vector2(20.99165f,24.1121864f);
        var a=f.Add("captured route entry",before,.450000763f);var body=a.GetComponent<Rigidbody2D>();
        var path=CapturedBuddy4Route();const int cursor=7;
        var extents=TileMover.GetMapClearanceExtents(body,.1f);
        Assert.That(TileMover.CanTraverseBox(f.Terrain,before,path[cursor],extents),Is.True);
        body.position=after;a.transform.position=after;Physics2D.SyncTransforms();
        Assert.That(TileMover.CanOccupyBox(f.Terrain,after,extents),Is.True);
        Assert.That(TileMover.CanTraverseBox(f.Terrain,after,after+new Vector2(2.73055744f,2.18953323f)*.02f,extents),Is.False);
        Assert.That(TileMover.CanTraverseBox(f.Terrain,after,path[cursor],extents),Is.False);
        Assert.That(TileMover.CanTraverseBox(f.Terrain,after,path[cursor-1],extents),Is.True);
        Assert.That(RouteWaypointPolicy.Rejoin(path,cursor,after,a,.1f,new RouteWaypointPolicy.RejoinState()),Is.EqualTo(cursor));
        using var queue=new RegionRouteQueue(f.Terrain.State,f.Terrain,p=>new Point2(p.x/.6f,p.y/.6f),p=>new Vector2((float)p.X*.6f,(float)p.Y*.6f));
        var operation=typeof(RegionRouteQueue).GetMethod("Remaining",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        var suffix=(List<Vector2>)operation.Invoke(queue,new object[]{path,after,TileMover.TerrainFootprint(body,extents)});
        Assert.That(suffix.Count,Is.GreaterThan(0));Assert.That(suffix[0],Is.EqualTo(path[5]));
        Assert.That(queue.Searches,Is.Zero,"existing suffix operation needs no replacement search");
        var previous=after;foreach(var node in suffix){Assert.That(TileMover.CanTraverseBox(f.Terrain,previous,node,extents),Is.True);previous=node;}
        Assert.That(suffix[suffix.Count-1],Is.EqualTo(path[path.Count-1]));
    }

    [Test]public void SharedTerrainEntryRecoveryReusesPublishedCacheAndPreservesDuty()
    {
        using var f=new MovementFixture(false,CapturedRejoinTerrain());var path=CapturedBuddy4Route();
        var a=f.Add("generic route consumer",path[0],.450000763f);var body=a.GetComponent<Rigidbody2D>();
        var extents=TileMover.GetMapClearanceExtents(body,.1f);var goal=path[path.Count-1];
        using var queue=new RegionRouteQueue(f.Terrain.State,f.Terrain,p=>new Point2(p.x/.6f,p.y/.6f),p=>new Vector2((float)p.X*.6f,(float)p.Y*.6f));
        queue.RequestPath(a,body.position,goal,extents,out _);int slices=0;
        while(queue.PendingCount>0){queue.Advance(128,100);Assert.That(++slices,Is.LessThan(1000));}
        Assert.That(queue.RequestPath(a,body.position,goal,extents,out _),Is.EqualTo(GridRouteStatus.Succeeded));
        // Replay the actual captured published cache, not a replacement pathfinder or fake route.
        var requests=(System.Collections.IDictionary)typeof(RegionRouteQueue).GetField("owners",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(queue);
        var request=requests[(a.GetInstanceID(),0)];request.GetType().GetField("Path").SetValue(request,new List<Vector2>(path));
        int calls=0;
        a.RequestRoute=(from,to)=>{calls++;var status=queue.RequestPath(a,from,to,TileMover.GetMapClearanceExtents(body,.1f),out var delivered);return new ActorRouteResult(status,delivered,queue.RouteRevision(a));};
        a.AcceptCachedRoute=r=>queue.Accepts(a,r);a.ReadRevision=()=>f.Terrain.State.Revision;
        body.position=new Vector2(20.99165f,24.1121864f);a.transform.position=body.position;Physics2D.SyncTransforms();
        a.MovementGoal(91,goal,Vector2.zero);var dutyGoal=a.CurrentMovementGoal;
        f.Intent(a,new Vector2(2.73055744f,2.18953323f),1);f.Tick(1);
        Assert.That(f.Coordinator.Reason(a),Is.EqualTo("TerrainOrRegionNotReady"));
        var task=a.GetComponent<ActorMotionIntent>().SelectedTask;long ticket=f.Coordinator.Ticket(a),searches=queue.Searches,coalesced=queue.Coalesced;
        int cursor=7,revision=0;
        Assert.That(RouteWaypointPolicy.RecoverTerrainEntry(path,ref cursor,ref revision,body.position,goal,a,.1f),Is.True);
        Assert.That(path[0],Is.EqualTo(CapturedBuddy4Route()[5]));Assert.That(cursor,Is.Zero);
        Assert.That(calls,Is.EqualTo(1));Assert.That(queue.Searches,Is.EqualTo(searches));Assert.That(queue.Coalesced,Is.EqualTo(coalesced+1));
        Assert.That(a.CurrentMovementGoal,Is.EqualTo(dutyGoal));Assert.That(f.Coordinator.Ticket(a),Is.EqualTo(ticket));Assert.That(a.GetComponent<ActorMotionIntent>().SelectedTask,Is.SameAs(task));
        Assert.That(TileMover.CanTraverseBox(f.Terrain,body.position,path[0],extents),Is.True);
        f.Intent(a,(path[0]-body.position).normalized*3.5f,2);f.Tick(2);
        Assert.That(body.linearVelocity.sqrMagnitude,Is.GreaterThan(0));
        for(int n=0;n<100;n++)Assert.That(RouteWaypointPolicy.RecoverTerrainEntry(path,ref cursor,ref revision,body.position,goal,a,.1f),Is.True);
        Assert.That(calls,Is.EqualTo(1),"healthy frames do not repeat recovery requests");
    }

    [Test]public void TerrainEntryRecoveryLeavesPeerWaitsAndClearConnectorsAlone()
    {
        using var f=new MovementFixture(false,CapturedRejoinTerrain());var p=new Vector2(20.99165f,24.1121864f);
        var a=f.Add("generic entry",p,.450000763f);var peer=f.Add("peer",new Vector2(22.8f,25.8f),.375f);
        int calls=0;a.RequestRoute=(from,to)=>{calls++;return new ActorRouteResult(GridRouteStatus.Pending,null,0);};
        var path=CapturedBuddy4Route();int cursor=6,revision=0;var goal=path[path.Count-1];a.MovementGoal(91,goal,Vector2.zero);
        f.Intent(a,new Vector2(2.73055744f,2.18953323f),1);f.Tick(1);
        Assert.That(f.Coordinator.Reason(a),Is.EqualTo("TerrainOrRegionNotReady"));
        Assert.That(RouteWaypointPolicy.RecoverTerrainEntry(path,ref cursor,ref revision,p,goal,a,.1f),Is.True);
        Assert.That(calls,Is.Zero,"a prior rejected movement does not invalidate a currently clear connector");
        typeof(RegionMovementCoordinator).GetMethod("BeginWait",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(f.Coordinator,new object[]{CoordinationActor(f.Coordinator,a),CoordinationActor(f.Coordinator,peer),false});
        cursor=7;Assert.That(f.Coordinator.WaitingForPeer(a,goal),Is.True);
        Assert.That(RouteWaypointPolicy.RecoverTerrainEntry(path,ref cursor,ref revision,p,goal,a,.1f),Is.True);
        Assert.That(calls,Is.Zero);Assert.That(f.Coordinator.WaitingForPeer(a,goal),Is.True);Assert.That(cursor,Is.EqualTo(7));
    }

    [Test]public void PendingEntryRecoveryStopsFollowingTheInvalidConnector()
    {
        using var f=new MovementFixture(false,CapturedRejoinTerrain());var p=new Vector2(20.99165f,24.1121864f);
        var a=f.Add("generic pending entry",p,.450000763f);var path=CapturedBuddy4Route();int cursor=7,revision=0,calls=0;
        a.RequestRoute=(from,to)=>{calls++;return new ActorRouteResult(GridRouteStatus.Pending,null,0);};
        var goal=path[path.Count-1];a.MovementGoal(91,goal,Vector2.zero);var originalGoal=a.CurrentMovementGoal;
        f.Intent(a,new Vector2(2.73055744f,2.18953323f),1);f.Tick(1);
        Assert.That(RouteWaypointPolicy.RecoverTerrainEntry(path,ref cursor,ref revision,p,goal,a,.1f),Is.False);
        Assert.That(path,Is.Empty);Assert.That(cursor,Is.Zero);Assert.That(a.RouteStatus,Is.EqualTo(GridRouteStatus.Pending));
        Assert.That(calls,Is.EqualTo(1));Assert.That(a.CurrentMovementGoal,Is.EqualTo(originalGoal));
    }

    [Test]public void SameDutyRouteRedeliveryIncludingPendingDoesNotRenewActivityBudget()
    {
        using var f=new MovementFixture(false,CapturedRejoinTerrain());var path=CapturedBuddy4Route();
        var a=f.Add("generic finite route duty",path[0],.450000763f);int mode=0;
        a.AcceptCachedRoute=r=>mode==0;
        a.RequestRoute=(from,to)=>new ActorRouteResult(mode==1?GridRouteStatus.Pending:GridRouteStatus.Succeeded,mode==1?null:new List<Vector2>(path),0,20);
        var follower=new RegionRouteFollower();var goal=path[path.Count-1];
        Assert.That(a.Sight(path[0],goal),Is.False);
        Assert.That(follower.Direction(a,path[0],goal,out _),Is.True);Assert.That(follower.Publication,Is.EqualTo(1));
        mode=1;Assert.That(follower.Direction(a,path[0],goal,out _),Is.False);Assert.That(follower.Publication,Is.EqualTo(1));
        mode=2;Assert.That(follower.Direction(a,path[0],goal,out _),Is.True);Assert.That(follower.Publication,Is.EqualTo(1),"same-goal recovery is not a fresh timeout allowance");
        path.RemoveAt(path.Count-1);goal=path[path.Count-1];
        Assert.That(follower.Direction(a,path[0],goal,out _),Is.True);Assert.That(follower.Publication,Is.EqualTo(2),"a genuinely different destination retains its existing publication contract");
    }

    static List<Vector2> CapturedRejoinPrefix()=>new List<Vector2>{new Vector2(21.3000011f,22.5f),new Vector2(21.3000011f,23.1f),new Vector2(21.3000011f,23.7f),new Vector2(21.9000015f,23.7f),new Vector2(21.9000015f,24.3000011f),new Vector2(21.9000015f,24.9000015f),new Vector2(22.5f,24.9000015f),new Vector2(22.5f,25.5000019f),new Vector2(23.1f,25.5000019f)};
    [Test]public void CapturedBuddy2Buddy11RejoinsClearExistingNodeWithoutReclaimingBlockedAnchor()
    {
        using var f=new MovementFixture(false,CapturedRejoinTerrain());
        var a=f.Add("development-b-buddy-2",new Vector2(22.8713856f,24.4901237f),.6000004f);
        f.Add("development-b-buddy-11",new Vector2(21.83614f,24.02785f));
        var path=CapturedRejoinPrefix();var p=a.GetComponent<Rigidbody2D>().position;
        a.MovementGoal(12,new Vector2(17.1900215f,40.309948f),Vector2.zero);var goal=a.CurrentMovementGoal;
        Assert.That(Vector2.Dot(p-path[6],path[7]-path[6]),Is.LessThan(0));
        Assert.That(RouteWaypointPolicy.Advance(path,6,p,a,.6f),Is.EqualTo(6),"retained passed-plane rule reproduces the restrictive cursor");
        Assert.That(f.Coordinator.RouteConnectionClear(a,path[6]),Is.False);
        Assert.That(f.Coordinator.RouteConnectionClear(a,path[7]),Is.False,"first clear step is not a complete connector");
        Assert.That(f.Coordinator.RouteConnectionClear(a,path[8]),Is.True);
        var cache=new RouteWaypointPolicy.RejoinState();
        Assert.That(RouteWaypointPolicy.Rejoin(path,6,p,a,.6f,cache),Is.EqualTo(8));
        Assert.That(a.CurrentMovementGoal,Is.EqualTo(goal));Assert.That(path.Count,Is.EqualTo(9));
        long checks=cache.ConnectionChecks;for(int i=0;i<100;i++)Assert.That(RouteWaypointPolicy.Rejoin(path,8,p,a,.6f,cache),Is.EqualTo(8));
        Assert.That(cache.ConnectionChecks,Is.EqualTo(checks),"unchanged context cannot chain skips or repeat connector sweeps");
    }
    [Test]public void ForwardRejoinRejectsPeerBeyondClearFirstStepAndSolidCorner()
    {
        using var f=new MovementFixture(false,CapturedRejoinTerrain());var p=new Vector2(22.8713856f,24.4901237f);
        var a=f.Add("rejoining",p,.6000004f);var path=CapturedRejoinPrefix();
        f.Add("beyond-first-step",new Vector2(23.12f,25.59f),.1f);
        Vector2 first=p+(path[8]-p).normalized*.07f;
        Assert.That(f.Coordinator.RouteConnectionClear(a,first),Is.True);
        Assert.That(f.Coordinator.RouteConnectionClear(a,path[8]),Is.False);
        Assert.That(RouteWaypointPolicy.Rejoin(path,6,p,a,.6f,new RouteWaypointPolicy.RejoinState()),Is.EqualTo(6));
        f.Terrain.Walls.Add(43*80+38);f.Terrain.Rebuild();
        Assert.That(TileMover.CanOccupyBox(f.Terrain,p,Vector2.one*.6000004f),Is.True);
        Assert.That(TileMover.CanTraverseBox(f.Terrain,p,path[8],Vector2.one*.6000004f),Is.False);
        Assert.That(RouteWaypointPolicy.Rejoin(path,6,p,a,.6f,new RouteWaypointPolicy.RejoinState()),Is.EqualTo(6));
    }
    [Test]public void SafeRouteRejoinReleasesOwnedWaitWithoutChangingTaskOrDestination()
    {
        using var f=new MovementFixture(false,CapturedRejoinTerrain());var p=new Vector2(22.8713856f,24.4901237f);
        var a=f.Add("rejoining",p,.6000004f);var b=f.Add("waiting-peer",new Vector2(21.83614f,24.02785f));
        a.MovementGoal(12,new Vector2(17.1900215f,40.309948f),Vector2.zero);var goal=a.CurrentMovementGoal;
        f.Intent(a,Vector2.up,1);var task=a.GetComponent<ActorMotionIntent>().SelectedTask;
        typeof(RegionMovementCoordinator).GetMethod("BeginWait",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(f.Coordinator,new object[]{CoordinationActor(f.Coordinator,a),CoordinationActor(f.Coordinator,b),true});
        Assert.That(f.Coordinator.WaitingForPeer(a,goal),Is.True);
        Assert.That(RouteWaypointPolicy.Rejoin(CapturedRejoinPrefix(),6,p,a,.6f,new RouteWaypointPolicy.RejoinState()),Is.EqualTo(8));
        Assert.That(f.Coordinator.WaitingForPeer(a,goal),Is.False);Assert.That(a.CurrentMovementGoal,Is.EqualTo(goal));
        Assert.That(a.GetComponent<ActorMotionIntent>().SelectedTask,Is.SameAs(task));
    }
    [Test]public void NearHorizontalRecordedFailureDoesNotBecomeTinyAxisCreep()
    {
        using var f=new MovementFixture();var a=f.Add("near-horizontal",new Vector2(5.24f,5.42f));var b=f.Add("large standing",new Vector2(4.3f,5.1f),.5f);
        for(int tick=1;tick<=8;tick++){f.Intent(a,new Vector2(-3.15f,-.03f),tick);f.Tick(tick,true);Assert.That(a.GetComponent<Collider2D>().Distance(b.GetComponent<Collider2D>()).distance,Is.GreaterThanOrEqualTo(-.001f));}
        Assert.That(a.GetComponent<Rigidbody2D>().linearVelocity.magnitude,Is.GreaterThan(.1f));
    }
    [Test]public void RecordedCornerUsesOneCoordinatedDirectiveWithoutCollisionException()
    {
        using var f=new MovementFixture();var a=f.Add("corner",new Vector2(5.03f,5.56f));var b=f.Add("standing",new Vector2(4.25f,4.97f));
        for(int tick=1;tick<=8;tick++){f.Intent(a,new Vector2(-2.2f,2.2f),tick);f.Tick(tick,true);Assert.That(a.GetComponent<Collider2D>().Distance(b.GetComponent<Collider2D>()).distance,Is.GreaterThanOrEqualTo(-.001f));}
        Assert.That(Physics2D.GetIgnoreCollision(a.GetComponent<Collider2D>(),b.GetComponent<Collider2D>()),Is.False);
        Assert.That(a.GetComponent<Rigidbody2D>().linearVelocity.magnitude,Is.GreaterThan(.1f));
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
    [Test]public void WorkAdmissionDirectsIdleYieldIntoClearTunnelSpace()
    {
        var terrain=new Fixture();for(int y=1;y<31;y++){terrain.Walls.Add(y*32+6);terrain.Walls.Add(y*32+9);}terrain.Rebuild();
        using var f=new MovementFixture(true,terrain);var a=f.Add("constructor",new Vector2(4.8f,3.74f),.375f,true);var b=f.Add("idle",new Vector2(4.8f,4.5f),.375f,true);
        Assert.That(f.Coordinator.RequestAccess(a,1,new Vector2(4.8f,4.5f)),Is.False);f.Tick(1);
        Assert.That(b.GetComponent<Rigidbody2D>().linearVelocity.y,Is.GreaterThan(0));Assert.That(Mathf.Abs(b.GetComponent<Rigidbody2D>().linearVelocity.x),Is.LessThan(.0001f));
        Assert.That(TileMover.CanTraverseBox(terrain,b.transform.position,(Vector2)b.transform.position+b.GetComponent<Rigidbody2D>().linearVelocity*.02f,Vector2.one*.375f),Is.True);
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
    [Test]public void DirectedWalkQueueingNeverRenewsFiniteDeadline()
    {
        Assert.That(CampDirectedWalk.WalkDeadlineExpired(9.9f,10),Is.False);
        Assert.That(CampDirectedWalk.WalkDeadlineExpired(10.1f,10),Is.True,"queueing does not reset or lengthen the finite end-to-end deadline");
    }
    [Test]public void RegionYieldCancellationDoesNotResumeAStaleDirective()
    {
        using var f=new MovementFixture(true);var a=f.Add("requester",new Vector2(4.5f,3.74f),.375f,true);var b=f.Add("idle",new Vector2(4.5f,4.5f),.375f,true);
        f.Coordinator.RequestAccess(a,1,new Vector2(4.5f,4.5f));f.Tick(1);
        Assert.That(b.GetComponent<Rigidbody2D>().linearVelocity.magnitude,Is.GreaterThan(.1f));Assert.That(b.GetComponent<BoxCollider2D>().size,Is.EqualTo(Vector2.one*.75f));
        f.Coordinator.ReleaseDuty(a);ActorMotionIntent.Cancel(b.gameObject);f.Tick(2);
        Assert.That(f.Coordinator.State(b),Is.Not.EqualTo(MovementResolutionState.Yielding));
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
    [Test]public void CampAllowanceCannotPermitSweptFullBodyPhasing()
    {
        var sum=Vector2.one*.75f;float tolerance=MovementContactPolicy.Allowance(Vector2.one*.375f,Vector2.one*.375f,true);
        Assert.That(tolerance,Is.EqualTo(.06f).Within(.00001f));
        Assert.That(MovementContactPolicy.SweepClear(new Vector2(1,0),new Vector2(-2,0),sum,tolerance),Is.False,"clear endpoints cannot bypass another body");
        Assert.That(MovementContactPolicy.SweepClear(new Vector2(.71f,0),Vector2.up*.02f,sum,tolerance),Is.True,"small tangential Camp contact is permitted");
        Assert.That(MovementContactPolicy.SweepClear(new Vector2(.71f,0),Vector2.left*.03f,sum,tolerance),Is.False,"the overlap envelope is a bound, not phasing");
    }
    [Test]public void ArrivedStandingClaimSurvivesRouteCompletionUntilActivityRelease()
    {
        using var f=new MovementFixture(true);var a=f.Add("A",new Vector2(3,3),.375f,true);var goal=new Vector2(6,6);
        f.Coordinator.Choose(a,17,goal,Vector2.zero,false);a.CancelRouteWork=()=>{};a.GetComponent<Rigidbody2D>().position=goal;a.transform.position=goal;Physics2D.SyncTransforms();
        Assert.That(f.Coordinator.MarkArrived(a),Is.True);a.StopRouteWork();Assert.That(f.Coordinator.HasDestination(a),Is.True);
        a.ReleaseActivitySpace();Assert.That(f.Coordinator.HasDestination(a),Is.False);
    }
    [Test]public void ExactAccessUsesStableOldestTicketAndDoesNotPromiseOccupiedWork()
    {
        using var f=new MovementFixture(true);var a=f.Add("A",new Vector2(3,3),.375f,true);var b=f.Add("B",new Vector2(3,5),.375f,true);var goal=new Vector2(8,8);
        f.Coordinator.RequestAccess(b,2,goal);f.Coordinator.RequestAccess(a,1,goal);f.Tick(1);
        Assert.That(f.Coordinator.Ticket(a),Is.LessThan(f.Coordinator.Ticket(b)));Assert.That(f.Coordinator.RequestAccess(a,1,goal),Is.True);Assert.That(f.Coordinator.RequestAccess(b,2,goal),Is.False);
        a.GetComponent<Rigidbody2D>().position=goal;a.transform.position=goal;Physics2D.SyncTransforms();f.Coordinator.MarkArrived(a);f.Coordinator.ReleaseDuty(a);f.Tick(2);
        Assert.That(f.Coordinator.RequestAccess(b,2,goal),Is.False,"logical release cannot make a physical worker vanish");
    }
    [Test]public void CampContactNeverLeaksIntoParticipantOrExpeditionRoles()
    {
        using var f=new MovementFixture(true);var a=f.Add("residentA",new Vector2(3,3),.375f,true);var b=f.Add("residentB",new Vector2(5,3),.375f,true);
        f.Coordinator.RefreshContacts();var ca=a.GetComponent<Collider2D>();var cb=b.GetComponent<Collider2D>();Assert.That(Physics2D.GetIgnoreCollision(ca,cb),Is.True);
        var session=new MovementSession();session.Fighters.Add(a.gameObject);a.gameObject.AddComponent<CombatSessionBinding>().Rules=session;
        f.Coordinator.RefreshContacts();Assert.That(f.Coordinator.Role(a),Is.EqualTo(MovementContactRole.Strict));Assert.That(Physics2D.GetIgnoreCollision(ca,cb),Is.False);Assert.That(f.Coordinator.VerifyStrict(a.gameObject),Is.True);
        session.Fighters.Clear();f.Coordinator.RefreshContacts();Assert.That(f.Coordinator.Role(a),Is.EqualTo(MovementContactRole.CampSpectator));
        using var expedition=new MovementFixture();var c=expedition.Add("expeditionA",new Vector2(10,10),.375f,true);var d=expedition.Add("expeditionB",new Vector2(12,10),.375f,true);
        expedition.Coordinator.RefreshContacts();Assert.That(expedition.Coordinator.Role(c),Is.EqualTo(MovementContactRole.Strict));Assert.That(Physics2D.GetIgnoreCollision(c.GetComponent<Collider2D>(),d.GetComponent<Collider2D>()),Is.False);
    }
    [Test]public void YieldDirectiveExpiresWhenItsPhysicalConflictHasGone()
    {
        using var f=new MovementFixture();var a=f.Add("mover",new Vector2(3,3));var b=f.Add("standing",new Vector2(3.78f,3));
        f.Intent(a,Vector2.right*3,1);f.Tick(1);
        Assert.That(a.GetComponent<ActorMotionIntent>().State,Is.EqualTo(MovementResolutionState.Yielding));
        b.GetComponent<Rigidbody2D>().position=new Vector2(9,9);b.transform.position=new Vector2(9,9);Physics2D.SyncTransforms();
        f.Intent(a,Vector2.right*3,2);f.Tick(2);
        Assert.That(a.GetComponent<ActorMotionIntent>().State,Is.EqualTo(MovementResolutionState.Moving));
        Assert.That(a.GetComponent<Rigidbody2D>().linearVelocity,Is.EqualTo(Vector2.right*3),"an unneeded old yield cannot hold a clear route hostage");
    }
    [Test]public void SharedPeerSweepRejectsTheRecordedMissingMarginCandidate()
    {
        Vector2 relative=new Vector2(21.6592636f-22.4334278f,23.19611f-23.9528618f);
        Vector2 delta=new Vector2(21.88329f-21.6592636f,22.8587112f-23.19611f);
        var half=Vector2.one*.375f;
        Assert.That(MovementContactPolicy.SweepClear(relative,delta,half+half,0),Is.True,"reproduces the old weaker candidate predicate");
        Assert.That(MovementContactPolicy.PeerSweepClear(relative,delta,half,half,false,.01f),Is.False,"candidate and execution now reject the same swept contact");
    }
    static object CoordinationActor(RegionMovementCoordinator coordinator,ActorTerrainBinding binding)
    {
        var field=typeof(RegionMovementCoordinator).GetField("byActor",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        return ((System.Collections.IDictionary)field.GetValue(coordinator))[binding];
    }
    static T ActorField<T>(object actor,string field)=>(T)actor.GetType().GetField(field).GetValue(actor);
    [Test]public void OwnedYieldSurvivesOneQuietTickAndCannotReverseBackToTask()
    {
        using var f=new MovementFixture();var a=f.Add("mover",new Vector2(3,3));var b=f.Add("standing",new Vector2(3.78f,3));
        f.Intent(a,Vector2.right*3,1);f.Tick(1);
        var actor=CoordinationActor(f.Coordinator,a);Assert.That(ActorField<bool>(actor,"Yielding"),Is.True);
        var goal=ActorField<Vector2>(actor,"YieldGoal");var owner=ActorField<object>(actor,"YieldOwner");
        // Move the blocker a little out of this tick's conflict, not past the maneuver.
        b.GetComponent<Rigidbody2D>().position+=Vector2.right*.15f;b.transform.position=b.GetComponent<Rigidbody2D>().position;Physics2D.SyncTransforms();
        f.Intent(a,Vector2.right*3,2);f.Tick(2);
        Assert.That(ActorField<bool>(actor,"Yielding"),Is.True);Assert.That(ActorField<Vector2>(actor,"YieldGoal"),Is.EqualTo(goal));
        Assert.That(ActorField<object>(actor,"YieldOwner"),Is.SameAs(owner));
        Assert.That(Vector2.Dot(a.GetComponent<Rigidbody2D>().linearVelocity,goal-a.GetComponent<Rigidbody2D>().position),Is.GreaterThan(0));
    }
    [Test]public void CompletedYieldWaitsCalmlyForStationaryPlayerAndResumesWhenTheyMove()
    {
        using var f=new MovementFixture(true);
        var a=f.Add("resident",new Vector2(3.18f,6.2f),.375f,true);
        var player=f.Add("player",new Vector2(3.1f,5.3f),.375f,true,true);
        a.MovementGoal(1,new Vector2(2.7f,1.5f),Vector2.zero);
        int selected=0,started=0;Action<RegionMovementCoordinator.DecisionTrace> observe=r=>{if(r.Key!="resident")return;if(r.Stage=="YieldSelected")selected++;if(r.Stage=="WaitStarted")started++;};
        RegionMovementCoordinator.DiagnosticObserver+=observe;
        try
        {
            for(int tick=1;tick<=200;tick++){f.Intent(a,(a.CurrentMovementGoal-a.GetComponent<Rigidbody2D>().position).normalized*1.6f,tick);f.Tick(tick,true);}
            Assert.That(f.Coordinator.State(a),Is.EqualTo(MovementResolutionState.WaitingForPeer));
            Assert.That(started,Is.EqualTo(1));Assert.That(selected,Is.EqualTo(1));
            var position=a.GetComponent<Rigidbody2D>().position;
            for(int tick=201;tick<=250;tick++){f.Intent(a,Vector2.down*1.6f,tick);f.Tick(tick,true);}
            Assert.That(a.GetComponent<Rigidbody2D>().position,Is.EqualTo(position));
            Assert.That(selected,Is.EqualTo(1),"no repeated maneuver while unchanged");
            player.GetComponent<Rigidbody2D>().position+=Vector2.right*3;player.transform.position=player.GetComponent<Rigidbody2D>().position;Physics2D.SyncTransforms();
            f.Intent(a,Vector2.down*1.6f,251);f.Tick(251,true);
            Assert.That(f.Coordinator.State(a),Is.Not.EqualTo(MovementResolutionState.WaitingForPeer));
            Assert.That(a.GetComponent<Rigidbody2D>().linearVelocity.y,Is.LessThan(-1));
        }
        finally{RegionMovementCoordinator.DiagnosticObserver-=observe;}
    }
    [TestCase("goal")][TestCase("terrain")][TestCase("cancel")]
    public void CalmWaitInvalidatesOnMeaningfulDutyOrTerrainChange(string change)
    {
        using var f=new MovementFixture(true);var a=f.Add("resident",new Vector2(3.18f,6.2f),.375f,true);
        f.Add("player",new Vector2(3.1f,5.3f),.375f,true,true);
        int revision=1;a.ReadRevision=()=>revision;a.MovementGoal(1,new Vector2(2.7f,1.5f),Vector2.zero);
        for(int tick=1;tick<=200;tick++){f.Intent(a,(a.CurrentMovementGoal-a.GetComponent<Rigidbody2D>().position).normalized*1.6f,tick);f.Tick(tick,true);}
        Assert.That(f.Coordinator.State(a),Is.EqualTo(MovementResolutionState.WaitingForPeer));
        if(change=="goal")a.MovementGoal(2,new Vector2(8,8),Vector2.zero);
        if(change=="terrain")revision++;
        if(change=="cancel")ActorMotionIntent.Cancel(a.gameObject);
        f.Intent(a,Vector2.right*1.6f,201);f.Tick(201);
        Assert.That(f.Coordinator.State(a),Is.Not.EqualTo(MovementResolutionState.WaitingForPeer));
        Assert.That(a.GetComponent<Rigidbody2D>().linearVelocity.x,Is.GreaterThan(1));
    }
    [Test]public void ExecutionRejectedYieldIsInvalidatedAndNotRetainedIndefinitely()
    {
        using var f=new MovementFixture();var a=f.Add("mover",new Vector2(3,3));f.Add("standing",new Vector2(3.78f,3));
        f.Intent(a,Vector2.right*3,1);f.Tick(1);var actor=CoordinationActor(f.Coordinator,a);
        Assert.That(ActorField<bool>(actor,"Yielding"),Is.True);var goal=ActorField<Vector2>(actor,"YieldGoal");
        typeof(RegionMovementCoordinator).GetMethod("Stop",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(f.Coordinator,new[]{actor});
        Assert.That(ActorField<bool>(actor,"Yielding"),Is.False);Assert.That(ActorField<bool>(actor,"HasRejectedYield"),Is.True);
        Assert.That(ActorField<Vector2>(actor,"RejectedYieldGoal"),Is.EqualTo(goal));
        f.Intent(a,Vector2.right*3,2);f.Tick(2);
        Assert.That(!ActorField<bool>(actor,"Yielding")||(ActorField<Vector2>(actor,"YieldGoal")-goal).sqrMagnitude>=.0016f,Is.True,"choose a legitimate different goal or report blocked");
    }
    [Test]public void RetainedYieldIsRecheckedAgainstNewPhysicalPeerOccupancy()
    {
        using var f=new MovementFixture();var a=f.Add("mover",new Vector2(3,3));f.Add("standing",new Vector2(3.78f,3));
        f.Intent(a,Vector2.right*3,1);f.Tick(1);var actor=CoordinationActor(f.Coordinator,a);
        Assert.That(ActorField<bool>(actor,"Yielding"),Is.True);var goal=ActorField<Vector2>(actor,"YieldGoal");
        f.Add("new physical blocker",goal,.2f,false,false,0);
        f.Intent(a,Vector2.right*3,2);f.Tick(2);
        Assert.That(!ActorField<bool>(actor,"Yielding")||(ActorField<Vector2>(actor,"YieldGoal")-goal).sqrMagnitude>=.0016f,Is.True,"terrain-clear cannot retain a peer-blocked holding goal");
    }
    [Test]public void RecordedMixedSizeWallContactHasAnAdmissibleGridTangentYield()
    {
        var terrain=new Fixture(64);var definition=new ExpeditionLayout();
        // Same authored occupancy, with the production logical-to-world row inversion.
        for(int y=0;y<64;y++)for(int x=0;x<64;x++){definition.TrySolid(x,127-y,out bool solid);if(solid)terrain.Walls.Add(y*64+x);}
        terrain.Rebuild();using var f=new MovementFixture(false,terrain);
        var first=f.Add("11",new Vector2(20.39147f,22.6853142f),.375f);
        var second=f.Add("4",new Vector2(20.7394543f,23.5320187f),.450000763f);
        f.Intent(first,new Vector2(.2596478f,3.49035573f),1);f.Intent(second,new Vector2(3.12208986f,-1.58194637f),1);f.Tick(1,true);
        Assert.That(second.GetComponent<Rigidbody2D>().linearVelocity.x,Is.GreaterThan(.1f),"an axis tangent is a valid owned yield, not NoYieldSpace");
        Assert.That(first.GetComponent<Rigidbody2D>().linearVelocity.magnitude,Is.LessThan(.00001f),"requester waits while joint movement is unsafe");
        Assert.That(second.GetComponent<Collider2D>().Distance(first.GetComponent<Collider2D>()).distance,Is.GreaterThanOrEqualTo(-.001f));
        Assert.That(Physics2D.GetIgnoreCollision(first.GetComponent<Collider2D>(),second.GetComponent<Collider2D>()),Is.False);
        Assert.That(TileMover.CanOccupyBox(terrain,second.transform.position,Vector2.one*.450000763f),Is.True);
    }
    [Test]public void RecordedBuddy11Buddy5EndpointDoesNotCreateRequesterClearance()
    {
        Vector2 owner=new Vector2(22.0434818f,29.00719f),yielding=new Vector2(22.8423576f,29.1585674f);
        Vector2 ownerVelocity=new Vector2(3.10623169f,1.61286223f),yieldVelocity=new Vector2(-2.12379432f,2.7819953f);
        var half=Vector2.one*.375f;
        Assert.That(Physics2D.defaultContactOffset,Is.EqualTo(.01f));
        Assert.That(MovementContactPolicy.PeerSweepClear(owner-yielding,ownerVelocity*.02f,half,half,false,Physics2D.defaultContactOffset),Is.False);
        Assert.That(MovementContactPolicy.PeerSweepClear(yielding-owner,yieldVelocity*.02f,half,half,false,Physics2D.defaultContactOffset),Is.False);
        // Baseline reproduction is retained: a safe-to-occupy goal must not be confused with a useful plan.
        Assert.That(MovementContactPolicy.Penetration(owner-yielding,half*2+Vector2.one*.02f),Is.Zero);
    }
    [Test]public void RecordedBuddy11Buddy5RetainedEndpointMustNotHoldBothActorsForever()
    {
        var terrain=new Fixture(64);var definition=new ExpeditionLayout();
        for(int y=0;y<64;y++)for(int x=0;x<64;x++){definition.TrySolid(x,127-y,out bool solid);if(solid)terrain.Walls.Add(y*64+x);}
        terrain.Rebuild();using var f=new MovementFixture(false,terrain);
        var owner=f.Add("development-b-buddy-11",new Vector2(22.0434818f,29.00719f));
        var yielding=f.Add("development-b-buddy-5",new Vector2(22.8423576f,29.1585674f));
        owner.MovementGoal(1,new Vector2(19.08502f,39.3999367f),Vector2.zero);
        yielding.MovementGoal(1,new Vector2(19.08502f,40.2849236f),Vector2.zero);
        f.Tick(0);var a=CoordinationActor(f.Coordinator,yielding);var requester=CoordinationActor(f.Coordinator,owner);
        void Set(object actor,string field,object value)=>actor.GetType().GetField(field).SetValue(actor,value);
        Set(requester,"Ticket",31L);Set(a,"Ticket",33L);Set(a,"Yielding",true);Set(a,"YieldGoal",yielding.GetComponent<Rigidbody2D>().position);
        Set(a,"YieldOwner",requester);Set(a,"YieldOwnerTicket",31L);Set(a,"YieldOwnerOrigin",new Vector2(21.7949848f,28.8781624f));
        Set(a,"YieldHeading",new Vector2(3.10623169f,1.61286223f).normalized);
        int selections=0;Action<RegionMovementCoordinator.DecisionTrace> observe=r=>{if(r.Stage=="YieldSelected")selections++;};
        RegionMovementCoordinator.DiagnosticObserver+=observe;
        try
        {
            for(int tick=1;tick<=100;tick++)
            {f.Intent(owner,new Vector2(3.10623169f,1.61286223f),tick);f.Intent(yielding,new Vector2(-2.12379432f,2.7819953f),tick);f.Tick(tick,true);}
            bool progress=Vector2.Distance(owner.GetComponent<Rigidbody2D>().position,new Vector2(22.0434818f,29.00719f))>.15f;
            bool noPlan=f.Coordinator.Reason(yielding)=="NoUsefulLocalYieldPlan"||f.Coordinator.Reason(owner)=="NoUsefulLocalYieldPlan";
            Assert.That(progress||noPlan,Is.True,"requester must advance safely or receive explicit bounded no-plan, never mutual holding");
            Assert.That(selections,Is.LessThan(10),"unchanged no-plan context must not repeatedly select unusable goals");
            Assert.That(owner.GetComponent<Collider2D>().Distance(yielding.GetComponent<Collider2D>()).distance,Is.GreaterThanOrEqualTo(-.001f));
        }
        finally{RegionMovementCoordinator.DiagnosticObserver-=observe;}
    }
    [Test]public void ImpossibleLocalYieldIsExplicitAndCachedUntilSpaceChanges()
    {
        var terrain=new Fixture();
        for(int y=1;y<31;y++)for(int x=1;x<31;x++)if(x<4||x>6||y<4||y>5)terrain.Walls.Add(y*32+x);
        terrain.Rebuild();using var f=new MovementFixture(false,terrain);
        var a=f.Add("first",new Vector2(3,3));var b=f.Add("second",new Vector2(3.78f,3));
        a.MovementGoal(1,b.transform.position,Vector2.zero);b.MovementGoal(2,a.transform.position,Vector2.zero);
        int enumerations=0;Action<RegionMovementCoordinator.DecisionTrace> observe=r=>{if(r.Stage=="YieldNoUsefulCandidate")enumerations++;};
        RegionMovementCoordinator.DiagnosticObserver+=observe;
        try
        {
            for(int tick=1;tick<=100;tick++){f.Intent(a,Vector2.right*3,tick);f.Intent(b,Vector2.left*3,tick);f.Tick(tick);}
            Assert.That(f.Coordinator.State(a),Is.EqualTo(MovementResolutionState.NoLocalYieldPlan));
            Assert.That(f.Coordinator.State(b),Is.EqualTo(MovementResolutionState.NoLocalYieldPlan));
            Assert.That(f.Coordinator.Reason(a),Is.EqualTo("NoUsefulLocalYieldPlan"));
            Assert.That(enumerations,Is.EqualTo(1),"one bounded candidate enumeration, no unchanged retries");
            Assert.That(a.GetComponent<Rigidbody2D>().linearVelocity,Is.EqualTo(Vector2.zero));
            Assert.That(b.GetComponent<Rigidbody2D>().linearVelocity,Is.EqualTo(Vector2.zero));
            a.GetComponent<Rigidbody2D>().position+=Vector2.left*.05f;a.transform.position=a.GetComponent<Rigidbody2D>().position;Physics2D.SyncTransforms();
            f.Intent(a,Vector2.right*3,101);f.Intent(b,Vector2.left*3,101);f.Tick(101);
            Assert.That(enumerations,Is.GreaterThan(1),"changed physical space permits reconsideration");
        }
        finally{RegionMovementCoordinator.DiagnosticObserver-=observe;}
    }
    [Test]public void RecordedCampMixedSizeNoPlanProbeReportsExecutableTerrainHorizon()
    {
        var terrain=new Fixture(192);var definition=SporeGobbo.CampCandidate.SettlementCampLayout.Shared;
        for(int y=0;y<192;y++)for(int x=0;x<192;x++){definition.TrySolid(x,191-y,out bool solid);if(solid)terrain.Walls.Add(y*192+x);}
        terrain.Rebuild();var from=new Vector2(51.6000061f,72.7983856f);var half=new Vector2(.5999985f,.5999985f);
        var velocity=new Vector2(-3.028613E-05f,-1.57499993f);
        bool current=TileMover.CanOccupyBox(terrain,from,half);
        bool step=TileMover.CanTraverseBox(terrain,from,from+velocity*.02f,half);
        bool halfCell=TileMover.CanTraverseBox(terrain,from,from+velocity.normalized*.3f,half);
        TestContext.WriteLine("Recorded Camp probe: current="+current+" executableStep="+step+" halfCell="+halfCell);
        Assert.That(current,Is.True,"the recorded body starts on physically valid logical floor");
        Assert.That(step,Is.True,"recorded actual intended fixed step is terrain executable");
        Assert.That(halfCell,Is.False,"a longer straight probe crosses a bend that execution has not reached");
        using var f=new MovementFixture(true,terrain);
        var owner=f.Add("recorded Camp requester",from,.5999985f,true);
        var yielding=f.Add("recorded Camp yielder",new Vector2(52.3937225f,71.7958755f),.450000763f,true);
        f.Add("front resident",new Vector2(52.2547f,69.67996f),.375f,true);
        owner.MovementGoal(1,new Vector2(51.4f,49.45656f),Vector2.zero);
        f.Intent(owner,velocity,1);f.Intent(yielding,new Vector2(.029199142f,-1.5747292f),1);f.Tick(1);
        var method=typeof(RegionMovementCoordinator).GetMethod("RequesterAdvanceClear",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        Assert.That((bool)method.Invoke(f.Coordinator,new[]{CoordinationActor(f.Coordinator,yielding),CoordinationActor(f.Coordinator,owner),(object)new Vector2(52.39371f,71.2408752f)}),Is.True,"the safe real step must not be rejected by a future straight-line terrain probe");
    }
    static object InvokeCoordinator(RegionMovementCoordinator coordinator,string method,params object[] args)
        =>typeof(RegionMovementCoordinator).GetMethod(method,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(coordinator,args);
    static void SetActor(object actor,string field,object value)=>actor.GetType().GetField(field).SetValue(actor,value);
    static void SeedShortfall(MovementFixture f,out ActorTerrainBinding yielder,out ActorTerrainBinding requester,out object a,out object owner)
    {
        yielder=f.Add("shortfall Buddy 2",new Vector2(21.3534164f,31.2489414f),.6000004f);
        requester=f.Add("shortfall Buddy 1",new Vector2(22.2152042f,30.18563f),.375f);
        f.Intent(yielder,new Vector2(-.7283529f,3.42337561f),1);
        f.Intent(requester,new Vector2(-.128330812f,3.49764633f),1);f.Tick(1);
        a=CoordinationActor(f.Coordinator,yielder);owner=CoordinationActor(f.Coordinator,requester);
        SetActor(owner,"Ticket",31L);SetActor(a,"Yielding",true);
        SetActor(a,"YieldGoal",new Vector2(21.3345222f,31.2689247f));
        SetActor(a,"YieldOwner",owner);SetActor(a,"YieldOwnerTicket",31L);
        SetActor(a,"YieldOwnerOrigin",ActorField<Vector2>(owner,"Position"));
        SetActor(a,"YieldHeading",ActorField<Vector2>(owner,"Preferred").normalized);
    }
    [Test]public void RecordedTwoPointSevenFiveCentimetreShortfallFinishesActualClearance()
    {
        using var f=new MovementFixture(false,CapturedRejoinTerrain());
        SeedShortfall(f,out var yielder,out var requester,out var a,out var owner);
        var start=ActorField<Vector2>(a,"Position");var goal=ActorField<Vector2>(a,"YieldGoal");
        Assert.That(Vector2.Distance(start,goal),Is.EqualTo(.02750133f).Within(.000003f));
        Assert.That((bool)InvokeCoordinator(f.Coordinator,"RequesterAdvanceClear",a,owner,start),Is.False);
        Assert.That((bool)InvokeCoordinator(f.Coordinator,"YieldGoalClear",a,owner,goal,true),Is.True);
        Assert.That((bool)InvokeCoordinator(f.Coordinator,"YieldOwnerClear",a,.02f),Is.False);
        var velocity=(Vector2)InvokeCoordinator(f.Coordinator,"YieldVelocity",a,.02f);
        Assert.That(velocity.magnitude,Is.GreaterThan(1f));
        Assert.That(Vector2.Distance(start+velocity*.02f,goal),Is.LessThan(.000003f));
        f.Intent(yielder,new Vector2(-.7283529f,3.42337561f),2);
        f.Intent(requester,new Vector2(-.128330812f,3.49764633f),2);f.Tick(2,true);
        Assert.That(Vector2.Distance(yielder.GetComponent<Rigidbody2D>().position,goal),Is.LessThan(.000003f));
        SetActor(a,"Position",yielder.GetComponent<Rigidbody2D>().position);
        Assert.That((bool)InvokeCoordinator(f.Coordinator,"RequesterAdvanceClear",a,owner,ActorField<Vector2>(a,"Position")),Is.True);
        Assert.That(yielder.GetComponent<Collider2D>().Distance(requester.GetComponent<Collider2D>()).distance,Is.GreaterThanOrEqualTo(-.001f));
    }
    [Test]public void FinalYieldConnectorBlockedByNewPeerCannotRemainActiveAtZeroVelocity()
    {
        using var f=new MovementFixture(false,CapturedRejoinTerrain());
        SeedShortfall(f,out var yielder,out var requester,out var a,out var owner);
        var goal=ActorField<Vector2>(a,"YieldGoal");f.Add("new connector obstruction",goal,.1f,false,false,0);
        f.Intent(yielder,new Vector2(-.7283529f,3.42337561f),2);
        f.Intent(requester,new Vector2(-.128330812f,3.49764633f),2);f.Tick(2);
        Assert.That(!ActorField<bool>(a,"Yielding")||(ActorField<Vector2>(a,"YieldGoal")-goal).sqrMagnitude>=.0016f,Is.True);
    }
    [Test]public void YieldCanHandOffWhenActuallyClearBeforeExactEndpoint()
    {
        using var f=new MovementFixture(false,CapturedRejoinTerrain());
        SeedShortfall(f,out var yielder,out var requester,out var a,out var owner);
        var goal=ActorField<Vector2>(a,"YieldGoal");SetActor(a,"Position",goal+Vector2.up*.005f);
        Assert.That((bool)InvokeCoordinator(f.Coordinator,"RequesterAdvanceClear",a,owner,ActorField<Vector2>(a,"Position")),Is.True);
        Assert.That((bool)InvokeCoordinator(f.Coordinator,"YieldOwnerClear",a,.02f),Is.True);
    }
    [TestCase(false)][TestCase(true)]public void ShortfallYieldReleasesWhenRequesterWithdrawsOrChangesDuty(bool changesDuty)
    {
        using var f=new MovementFixture(false,CapturedRejoinTerrain());
        SeedShortfall(f,out var yielder,out var requester,out var a,out var owner);
        if(changesDuty)f.Coordinator.ReleaseDuty(requester);
        else {requester.GetComponent<Rigidbody2D>().position+=Vector2.right*3;Physics2D.SyncTransforms();}
        f.Intent(yielder,new Vector2(-.7283529f,3.42337561f),2);f.Intent(requester,Vector2.zero,2);f.Tick(2);
        Assert.That(ActorField<bool>(a,"Yielding"),Is.False);
    }
    [Test]public void NativeMotionObservationDoesNotWritePhysicalOrMovementState()
    {
        using var f=new MovementFixture();var a=f.Add("observed",new Vector2(3,3));var b=f.Add("peer",new Vector2(5,3));
        var go=new GameObject("owned native contact observer");
        try
        {
            var observer=go.AddComponent<Stage64DNativeMotionEvidence>();var body=a.GetComponent<Rigidbody2D>();body.linearVelocity=Vector2.right;
            Vector2 position=body.position,velocity=body.linearVelocity;var collider=a.GetComponent<Collider2D>();
            bool ignored=Physics2D.GetIgnoreCollision(collider,b.GetComponent<Collider2D>());var constraints=body.constraints;var mode=Physics2D.simulationMode;
            observer.Applied(new RegionMovementCoordinator.DecisionTrace{Stage="Applied",Actor=a.GetInstanceID(),Key="observed",Tick=Time.fixedTimeAsDouble,Resolved=velocity});
            observer.CaptureBeforePhysics();observer.CaptureAfterPhysics();
            Assert.That(observer.MotionCount,Is.EqualTo(2));Assert.That(body.position,Is.EqualTo(position));Assert.That(body.linearVelocity,Is.EqualTo(velocity));
            Assert.That(body.constraints,Is.EqualTo(constraints));Assert.That(Physics2D.simulationMode,Is.EqualTo(mode));Assert.That(collider.enabled,Is.True);Assert.That(collider.isTrigger,Is.False);
            Assert.That(Physics2D.GetIgnoreCollision(collider,b.GetComponent<Collider2D>()),Is.EqualTo(ignored));Assert.That(f.Coordinator.DestinationCount,Is.Zero);
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }
    [Test]public void DiagnosticObserverDoesNotChangeCoordinatedMovementOrClaims()
    {
        var without=new List<Vector2>();var with=new List<Vector2>();int records=0;
        var prior=RegionMovementCoordinator.DiagnosticObserver;
        try
        {
            for(int observed=0;observed<2;observed++)
            {
                RegionMovementCoordinator.DiagnosticObserver=observed==0?null:(Action<RegionMovementCoordinator.DecisionTrace>)(r=>records++);
                using var f=new MovementFixture();var a=f.Add("A",new Vector2(3,3));var b=f.Add("B",new Vector2(4,3));
                var output=observed==0?without:with;
                for(int tick=1;tick<=40;tick++)
                {
                    f.Intent(a,Vector2.right*2,tick);f.Intent(b,Vector2.left*2,tick);f.Tick(tick,true);
                    output.Add(a.GetComponent<Rigidbody2D>().position);output.Add(b.GetComponent<Rigidbody2D>().position);
                    output.Add(a.GetComponent<Rigidbody2D>().linearVelocity);output.Add(b.GetComponent<Rigidbody2D>().linearVelocity);
                    Assert.That(f.Coordinator.DestinationCount,Is.Zero,"observation cannot create a claim");
                }
            }
            Assert.That(records,Is.GreaterThan(100));Assert.That(with,Is.EqualTo(without),"same fixed-step physical decisions with/without an observer");
        }
        finally{RegionMovementCoordinator.DiagnosticObserver=prior;}
    }
    [Test]public void DisableAndTeardownReleaseEveryOwnedPairOverrideAndClaim()
    {
        using var f=new MovementFixture(true);var a=f.Add("A",new Vector2(3,3),.375f,true);var b=f.Add("B",new Vector2(5,3),.375f,true);
        f.Coordinator.Choose(a,1,new Vector2(8,8),Vector2.zero,false);f.Coordinator.RefreshContacts();Assert.That(f.Coordinator.OwnedContactOverrides,Is.EqualTo(1));
        a.gameObject.SetActive(false);Assert.That(a.Movement,Is.SameAs(f.Coordinator));typeof(ActorTerrainBinding).GetMethod("OnDisable",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(a,null);Assert.That(f.Coordinator.HasDestination(a),Is.False,"activity claim released by binding disable");Assert.That(f.Coordinator.OwnedContactOverrides,Is.Zero);
        a.gameObject.SetActive(true);f.Coordinator.RefreshContacts();Assert.That(f.Coordinator.OwnedContactOverrides,Is.EqualTo(1));
        f.Coordinator.Dispose();Assert.That(f.Coordinator.ActorCount,Is.Zero);Assert.That(f.Coordinator.OwnedContactOverrides,Is.Zero);Assert.That(Physics2D.GetIgnoreCollision(a.GetComponent<Collider2D>(),b.GetComponent<Collider2D>()),Is.False);
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



