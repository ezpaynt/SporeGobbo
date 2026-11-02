#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using SporeGobbo.Geometry;
using SporeGobbo.CampLifecycle;

public sealed class Stage64DCooperativeContactComparisonTests
{
    sealed class Terrain:IDiggableTerrain,IExecutableTerrainClearance
    {
        public readonly ExpeditionLayout Layout=new ExpeditionLayout();
        public readonly RegionTerrainState State;
        public RegionTerrainPhysics TerrainPhysics{get;set;}
        public Terrain(){State=new RegionTerrainState("expedition",ExpeditionLayout.Width,ExpeditionLayout.Height,Layout,ExpeditionLayout.Seed);}
        public float CellSize=>.6f;
        public Vector2Int WorldToCell(Vector2 p){var q=TerrainPhysics.Logical(p);return new Vector2Int((int)Math.Floor(q.X),(int)Math.Floor(q.Y));}
        public Vector2 CellToWorld(Vector2Int c)=>TerrainPhysics.World(new Point2(c.x+.5,c.y+.5));
        public bool IsBlocked(Vector2Int p)=>State.World.IsSolid(p.x,p.y);
        public bool IsDiggable(Vector2Int p)=>false;
        public void DigCircle(Vector2 p,float radius)=>throw new InvalidOperationException("Read-only comparison");
        public TerrainDigResult DigCircle(Vector2 p,float radius,TerrainDigAuthority a,int strength,IReadOnlyCollection<Vector2Int> cells)=>throw new InvalidOperationException("Read-only comparison");
    }
    static GameObject Body(GameObject root,string key,Vector2 position,float half)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GobboBuddy.prefab");Assert.That(prefab,Is.Not.Null);
        var go=UnityEngine.Object.Instantiate(prefab,position,Quaternion.identity,root.transform);go.name=key;
        // Original body sizes reconstructed from the recorded physical extents, not smaller substitutes.
        go.transform.localScale=Vector3.one*(half*2);var box=go.GetComponent<BoxCollider2D>();Assert.That(box,Is.Not.Null);
        Assert.That(box.size,Is.EqualTo(Vector2.one));var rb=go.GetComponent<Rigidbody2D>();rb.position=position;
        return go;
    }
    [TestCase(false,0)][TestCase(false,1)][TestCase(false,4)]
    [TestCase(true,0)][TestCase(true,1)][TestCase(true,4)]
    public void ReconstructedCornerNativeComparison(bool cooperative,int held)
    {
        Assert.That(SporeGobbo.Safety.Persistence.Isolated&&V1RuntimeProfile.DevelopmentToolsEnabled,Is.True);
        Assert.That(UnityEngine.Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None),Is.Empty);
        var prior=Physics2D.simulationMode;GameObject root=null;RegionTerrainPhysics physics=null;RegionMovementCoordinator coordinator=null;
        Collider2D ca=null,cb=null;bool ignored=false;int includeA=0,includeB=0,excludeA=0,excludeB=0;
        try
        {
            Physics2D.simulationMode=SimulationMode2D.Script;root=new GameObject("owned reconstructed cooperative comparison");
            var terrain=new Terrain();physics=new RegionTerrainPhysics(terrain.State,root.transform);terrain.TerrainPhysics=physics;
            coordinator=new RegionMovementCoordinator(terrain);
            var a=Body(root,"development-b-buddy-4",new Vector2(22.8000011f,29.0853958f),.450000763f);
            var b=Body(root,"development-b-buddy-1",new Vector2(23.3939114f,29.9704037f),.375f);
            var ra=a.GetComponent<Rigidbody2D>();var rb=b.GetComponent<Rigidbody2D>();ca=a.GetComponent<Collider2D>();cb=b.GetComponent<Collider2D>();
            Physics2D.SyncTransforms();ignored=Physics2D.GetIgnoreCollision(ca,cb);includeA=ca.includeLayers;includeB=cb.includeLayers;excludeA=ca.excludeLayers;excludeB=cb.excludeLayers;
            var ba=a.GetComponent<ActorTerrainBinding>()??a.AddComponent<ActorTerrainBinding>();var bb=b.GetComponent<ActorTerrainBinding>()??b.AddComponent<ActorTerrainBinding>();
            foreach(var binding in new[]{ba,bb}){binding.Bind(terrain);binding.Ready=()=>true;binding.RequestRoute=(x,y)=>throw new InvalidOperationException("Historical routes must not be generated");}
            coordinator.Configure(ba,a.name,1,false);coordinator.Configure(bb,b.name,1,false);coordinator.SetDiagnosticCooperativeContact(cooperative);
            var ma=a.GetComponent<ActorMotionIntent>();var mb=b.GetComponent<ActorMotionIntent>();
            var stamp=typeof(ActorMotionIntent).GetField("stamp",BindingFlags.Instance|BindingFlags.NonPublic);
            var requests=new[]{held==4?Vector2.zero:new Vector2(0,3.5f),held==1?Vector2.zero:new Vector2(-3.498925f,.08674184f)};
            var bodies=new[]{ra,rb};var motions=new[]{ma,mb};var bindings=new[]{ba,bb};
            var contacts=new ContactPoint2D[32];float maxOverlap=0;double tick=27.039990419501134;
            TestContext.WriteLine("RECONSTRUCTED assumptions=fresh coordinator/tickets/task ownership; prefab material/filter defaults; pair-only peers; no historical leases/yield memory; recorded body poses/intents; original authored terrain. NOT historical reproduction or complete passage proof.");
            for(int i=0;i<2;i++)Assert.That(TileMover.GetMapClearanceExtents(bodies[i],.1f).x,Is.EqualTo(i==0?.450000763f:.375f).Within(.00001f));
            // One original requested step only. Extending constant intents would invent route progression.
            var startA=ra.position;var startB=rb.position;
            for(int i=0;i<2;i++){ActorMotionIntent.Submit(motions[i],bodies[i],requests[i],.1f,10,held!=(i==0?4:1));stamp.SetValue(motions[i],tick);}
            var ea=TileMover.GetMapClearanceExtents(ra,.1f);var eb=TileMover.GetMapClearanceExtents(rb,.1f);
            for(int i=0;i<2;i++)TestContext.WriteLine($"REQUEST actor={i} terrain={TileMover.CanTraverseBox(terrain,bodies[i].position,bodies[i].position+requests[i]*.02f,i==0?ea:eb)} velocity={requests[i].ToString("R")}");
            coordinator.Resolve(tick,.02f);var appliedA=ra.linearVelocity;var appliedB=rb.linearVelocity;
            Physics2D.Simulate(.02f);
            maxOverlap=MovementContactPolicy.Penetration(ra.position-rb.position,ea+eb);
            int countA=ra.GetContacts(contacts);Assert.That(countA,Is.LessThan(contacts.Length));
            for(int i=0;i<countA;i++)TestContext.WriteLine($"CONTACT point={contacts[i].point} normal={contacts[i].normal} separation={contacts[i].separation} collider={contacts[i].collider?.name} other={contacts[i].otherCollider?.name}");
            TestContext.WriteLine($"OUTCOME cooperative={cooperative} held={held} appliedA={appliedA.ToString("R")} appliedB={appliedB.ToString("R")} displacementA={(ra.position-startA).ToString("R")} displacementB={(rb.position-startB).ToString("R")} overlap={maxOverlap:R} stateA={coordinator.State(ba)}/{coordinator.Reason(ba)} stateB={coordinator.State(bb)}/{coordinator.Reason(bb)} native={RegionMovementCoordinator.EffectiveNativeContact(ca,cb)}");
            Assert.That(maxOverlap,Is.LessThanOrEqualTo(cooperative?MovementContactPolicy.Allowance(ea,eb,true)+.00001f:.00001f),"Separate policy expectation, strict baseline not weakened");
            // This fixture reports admission, not an assumed successful route or reconstructed history.
            coordinator.SetDiagnosticCooperativeContact(false);coordinator.Dispose();coordinator=null;
            Assert.That(Physics2D.GetIgnoreCollision(ca,cb),Is.EqualTo(ignored));Assert.That(ca.includeLayers.value,Is.EqualTo(includeA));Assert.That(cb.includeLayers.value,Is.EqualTo(includeB));Assert.That(ca.excludeLayers.value,Is.EqualTo(excludeA));Assert.That(cb.excludeLayers.value,Is.EqualTo(excludeB));
        }
        finally{try{coordinator?.Dispose();if(root!=null)UnityEngine.Object.DestroyImmediate(root);physics?.Dispose();}finally{Physics2D.simulationMode=prior;}}
    }
}
#endif
