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
    sealed class Fixture:ICompleteTerrainDefinition,IDiggableTerrain
    {
        public readonly HashSet<int> Walls=new HashSet<int>();public RegionTerrainState State;
        public Fixture(){State=new RegionTerrainState("owned navigation fixture",32,32,this);}
        public float CellSize=>.6f;
        public bool Protected(int x,int y)=>x<=0||y<=0||x>=31||y>=31;
        public bool TrySolid(int x,int y,out bool solid){solid=Protected(x,y)||Walls.Contains(y*32+x);return true;}
        public bool IsBlocked(Vector2Int p)=>State.World.IsSolid(p.x,p.y);
        public bool IsDiggable(Vector2Int p)=>!Protected(p.x,p.y)&&IsBlocked(p);
        public Vector2Int WorldToCell(Vector2 p)=>new Vector2Int(Mathf.FloorToInt(p.x/.6f),Mathf.FloorToInt(p.y/.6f));
        public Vector2 CellToWorld(Vector2Int p)=>new Vector2((p.x+.5f)*.6f,(p.y+.5f)*.6f);
        public void DigCircle(Vector2 p,float r)=>throw new InvalidOperationException("Not a digging fixture");
        public TerrainDigResult DigCircle(Vector2 p,float r,TerrainDigAuthority a,int s,IReadOnlyCollection<Vector2Int> cells)=>throw new InvalidOperationException("Not a digging fixture");
        public void Rebuild()=>State=new RegionTerrainState("owned navigation fixture",32,32,this);
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
            var binding=go.AddComponent<ActorTerrainBinding>();binding.Bind(f);go.transform.position=new Vector2(2.1f,2.1f);go.GetComponent<Rigidbody2D>().position=go.transform.position;Physics2D.SyncTransforms();
            Vector2 from=go.GetComponent<Rigidbody2D>().position,goal=new Vector2(15.3f,2.1f),extents=Vector2.one*.2f;
            Assert.That(from,Is.EqualTo(new Vector2(2.1f,2.1f)),"fixture physical and logical start must agree");
            Assert.That(queue.RequestPath(binding,from,goal,extents,out _),Is.EqualTo(GridRouteStatus.Pending));
            Assert.That(queue.RequestPath(binding,from,goal,extents,out _),Is.EqualTo(GridRouteStatus.Pending));Assert.That(queue.Coalesced,Is.EqualTo(1));
            int n=0;while(queue.PendingCount>0){queue.Advance(32,100);Assert.That(++n,Is.LessThan(100));}
            Assert.That(queue.RequestPath(binding,from,goal,extents,out var path),Is.EqualTo(GridRouteStatus.Succeeded));Assert.That(path.Count,Is.GreaterThan(0));
            int revision=queue.RouteRevision(binding);f.State.Commit(new[]{new DualTile(25,25)},f.State.Revision,_=>true);
            Assert.That(queue.Accepts(binding,revision),Is.True,"open distant terrain does not make an existing route unsafe");
            queue.Cancel(binding);Assert.That(queue.Accepts(binding,revision),Is.False);Assert.That(queue.PendingCount,Is.Zero);
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }
    [Test]public void ExplicitCancellationNeverPublishesSuccess()
    {var f=new Fixture();var search=new GridRouteSearch(f.State,new Point2(2.5,2.5),new Point2(28.5,28.5),.49,.49);search.Cancel();Assert.That(search.Step(100),Is.EqualTo(GridRouteStatus.Canceled));Assert.That(search.Path.Count,Is.Zero);}
    [Test]public void NonFiniteBodySizeAndEndpointCannotCreateUnsafeRoute()
    {var f=new Fixture();Assert.Throws<ArgumentOutOfRangeException>(()=>new GridRouteSearch(f.State,new Point2(2,2),new Point2(3,3),double.NaN,.5));Assert.That(new GridRouteSearch(f.State,new Point2(double.NaN,2),new Point2(3,3),.5,.5).Status,Is.EqualTo(GridRouteStatus.Unreachable));}
}
#endif
