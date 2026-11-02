#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using SporeGobbo.Geometry;
using SporeGobbo.Geometry.Preview;
using SporeGobbo.CampLifecycle;

// Actual Bind/BindVisual, actual spore prefab and native components/clearance.
// The local region supplies only deterministic fixture geography, not campaign or actor authority.
public sealed class Stage64DSpawnBindingTests
{
    sealed class Region:IWorldRegion,IExecutableTerrainClearance,ICompleteTerrainDefinition,IDisposable
    {
        readonly bool blocked;public readonly GameObject Root;public readonly RegionTerrainState State;
        public readonly List<Stage59ActorView> Views=new List<Stage59ActorView>();public RegionTerrainPhysics TerrainPhysics{get;}
        readonly int meshes=Stage59ActorView.OwnedMeshes,materials=Stage59ActorView.OwnedMaterials;
        public Region(bool blocked=false)
        {
            this.blocked=blocked;Root=new GameObject("Owned spawn binding component fixture");
            try
            {
                Assert.That(ActorShader,Is.Not.Null);
                State=new RegionTerrainState("owned spawn binding",32,32,this);TerrainPhysics=new RegionTerrainPhysics(State,Root.transform);
            }
            catch{if(Root!=null)UnityEngine.Object.DestroyImmediate(Root);TerrainPhysics?.Dispose();throw;}
        }
        public float CellSize=>.6f;public bool LogicalReady=>true;public bool PresentationReady=>true;public int Revision=>0;public Vector2 Arrival=>new Vector2(6.3f,6.3f);
        public Transform PresentationRoot=>Root.transform;public Shader ActorShader=>Shader.Find("Sprites/Default");public FocusProjection Projection=>null;public Stage4Settings PresentationSettings=>null;
        public Point2 LogicalPosition(Vector2 p)=>TerrainPhysics.Logical(p);public Vector3 Render(Vector2 p)=>p;public Vector2 World(Point2 p)=>TerrainPhysics.World(p);
        public Vector2Int WorldToCell(Vector2 p){var q=LogicalPosition(p);return new Vector2Int((int)Math.Floor(q.X),(int)Math.Floor(q.Y));}
        public Vector2 CellToWorld(Vector2Int p)=>World(new Point2(p.x+.5,p.y+.5));
        public bool Protected(int x,int y)=>x<=0||y<=0||x>=31||y>=31;public bool PlacementAllowed(int x,int y)=>!Protected(x,y);
        public bool TrySolid(int x,int y,out bool solid){solid=blocked||Protected(x,y);return true;}
        public bool IsBlocked(Vector2Int p)=>State.World.IsSolid(p.x,p.y);public bool IsDiggable(Vector2Int p)=>false;
        public void DigCircle(Vector2 p,float radius)=>throw new InvalidOperationException("Readonly fixture");
        public TerrainDigResult DigCircle(Vector2 p,float radius,TerrainDigAuthority a,int power,IReadOnlyCollection<Vector2Int> cells)=>throw new InvalidOperationException("Readonly fixture");
        public List<Vector2> Route(Vector2 a,Vector2 b,Vector2 extents)=>throw new InvalidOperationException("Spawn binding must not search a route");
        public bool RayClear(Vector2 a,Vector2 b,float radius=0)=>TileMover.CanTraverseBox(this,a,b,Vector2.one*Mathf.Max(.005f,radius));
        public void BindActor(GameObject actor)=>RegionActorViews.Bind(this,null,Views,actor,()=>true);
        public void BindVisual(GameObject visual)=>RegionActorViews.BindVisual(this,Views,visual);
        public GameObject Body(string name,Vector2 position)
        {var go=new GameObject(name);go.transform.SetParent(Root.transform);go.transform.position=position;var rb=go.AddComponent<Rigidbody2D>();rb.gravityScale=0;rb.position=position;go.AddComponent<BoxCollider2D>().size=Vector2.one;go.AddComponent<SpriteRenderer>();return go;}
        public GameObject Spore(Vector2 position)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CollectableSpore.prefab");Assert.That(prefab,Is.Not.Null);
            var go=UnityEngine.Object.Instantiate(prefab,position,Quaternion.identity,Root.transform);BindVisual(go);
            Assert.That(go.TryGetComponent<Rigidbody2D>(out var body),Is.False);Assert.That(body==null,Is.True);
            Assert.That(go.GetComponent<Collider2D>().isTrigger,Is.True,"Pickup remains nonblocking");return go;
        }
        public void Dispose()
        {
            try
            {
                // Initialize was called manually in EditMode, so this fixture owns explicit teardown.
                // Release is idempotent if Unity also invokes OnDestroy.
                foreach(var view in Views)if(view!=null)view.Release();
                if(Root!=null)UnityEngine.Object.DestroyImmediate(Root);TerrainPhysics.Dispose();
            }
            finally{Assert.That(Stage59ActorView.OwnedMeshes,Is.EqualTo(meshes));Assert.That(Stage59ActorView.OwnedMaterials,Is.EqualTo(materials));}
        }
    }
    [Test]public void VisualOnlySporeDoesNotObstructOrMoveValidActorSpawn()
    {
        using var region=new Region();var spore=region.Spore(region.Arrival);var actor=region.Body("Physical actor",region.Arrival);
        Assert.DoesNotThrow(()=>region.BindActor(actor));Assert.That(actor.GetComponent<Rigidbody2D>().position,Is.EqualTo(region.Arrival));
        Assert.That(region.Views.Count,Is.EqualTo(2));Assert.That(spore.TryGetComponent<Rigidbody2D>(out _),Is.False);
        Assert.That(TileMover.CanOccupyBox(region,actor.transform.position,TileMover.GetMapClearanceExtents(actor.GetComponent<Rigidbody2D>(),.22f)),Is.True);
    }
    [Test]public void MixedViewsRetainPhysicalActorSpawnSeparation()
    {
        using var region=new Region();var first=region.Body("First physical actor",region.Arrival);region.BindActor(first);region.Spore(region.Arrival);
        var second=region.Body("Second physical actor",region.Arrival);Assert.DoesNotThrow(()=>region.BindActor(second));
        var a=first.GetComponent<Rigidbody2D>();var b=second.GetComponent<Rigidbody2D>();var ea=TileMover.GetMapClearanceExtents(a,.22f);var eb=TileMover.GetMapClearanceExtents(b,.22f);
        Assert.That((a.position-b.position).sqrMagnitude,Is.GreaterThanOrEqualTo(Mathf.Pow(ea.magnitude+eb.magnitude,2)),"Existing radial spawn separation remains effective");
        Assert.That(TileMover.CanOccupyBox(region,b.position,eb),Is.True);Assert.That(a.position,Is.EqualTo(region.Arrival));Assert.That(region.Views.Count,Is.EqualTo(3));
    }
    [Test]public void SolidTerrainStillRejectsSpawnWithVisualOnlyEntries()
    {
        using var region=new Region(true);region.Spore(region.Arrival);var actor=region.Body("Blocked physical actor",region.Arrival);
        Assert.That(TileMover.CanOccupyBox(region,region.Arrival,TileMover.GetMapClearanceExtents(actor.GetComponent<Rigidbody2D>(),.22f)),Is.False);
        var error=Assert.Throws<InvalidOperationException>(()=>region.BindActor(actor));StringAssert.Contains("No clear local spawn",error.Message);
        Assert.That(region.Views.Count,Is.EqualTo(1));Assert.That(actor.GetComponent<Rigidbody2D>().position,Is.EqualTo(region.Arrival));
    }
}
#endif
