#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using SporeGobbo.Geometry;
using SporeGobbo.CampCandidate;
using UnityEngine;
using SporeGobbo.CampLifecycle;
using SporeGobbo.Geometry.Preview;
public sealed class Stage61LayoutTests
{
 [Test]public void DetachedCampaignCaptureCannotImportStaleSceneHistoryOrMutateItsInputs()
 {
  var host=new GameObject("owned campaign capture input");var mirrorHost=new GameObject("owned stale memorial view");
  try
  {
   var state=host.AddComponent<GameState>();state.deathHistory.Add(new DeadBuddyRecord{gobboId="canonical-death",displayName="Canonical",runNumber=1});
   var mirror=mirrorHost.AddComponent<CampDeathHistoryStore>();mirror.deadBuddyHistory.Add(new DeadBuddyRecord{gobboId="stale-scene-record",displayName="Stale"});
   var prior=SporeSaveSlotData.CreateNew(2,"Original campaign metadata","CampScene");prior.hasSave=true;string priorJson=JsonUtility.ToJson(prior);
   var snapshot=SporeSaveManager.BuildSlotFromCampaign(2,state,prior);
   Assert.That(snapshot.deathHistory.Select(record=>record.gobboId),Is.EqualTo(new[]{"canonical-death"}));
   Assert.That(state.deathHistory.Select(record=>record.gobboId),Is.EqualTo(new[]{"canonical-death"}));Assert.That(JsonUtility.ToJson(prior),Is.EqualTo(priorJson));
   snapshot.deathHistory[0].displayName="Detached mutation";Assert.That(state.deathHistory[0].displayName,Is.EqualTo("Canonical"));
  }
  finally{UnityEngine.Object.DestroyImmediate(mirrorHost);UnityEngine.Object.DestroyImmediate(host);}
 }
 [Test]public void ConcurrentDirectedWalksKeepCollisionLeaseUntilBothDutiesFinish()
 {
  var a=new GameObject("owned Buddy A");var b=new GameObject("owned Buddy B");var target=new GameObject("owned target");
  try
  {
   foreach(var actor in new[]{a,b}){actor.AddComponent<Rigidbody2D>();actor.AddComponent<BoxCollider2D>();actor.AddComponent<BuddyUnit>();actor.AddComponent<CampDirectedWalk>().destroyWhenDone=false;}
   var first=a.GetComponent<CampDirectedWalk>();var second=b.GetComponent<CampDirectedWalk>();var ac=a.GetComponent<Collider2D>();var bc=b.GetComponent<Collider2D>();
   Physics2D.IgnoreCollision(ac,bc,false);first.BeginWalk(target.transform,2);second.BeginWalk(target.transform,2);
   Assert.That(Physics2D.GetIgnoreCollision(ac,bc),Is.True);first.CancelWalk();Assert.That(Physics2D.GetIgnoreCollision(ac,bc),Is.True,"other Buddy still owns the temporary collision policy");
   second.CancelWalk();Assert.That(Physics2D.GetIgnoreCollision(ac,bc),Is.False,"original collision policy restored after last owner");
  }
  finally{UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);UnityEngine.Object.DestroyImmediate(target);}
 }
 [Test]public void BoundDirectedWalkTimeoutAccountsForWindingRouteAndLegacyKeepsCallerTimeout()
 {
  var actor=new GameObject("owned directed-walk fixture");var target=new GameObject("owned route endpoint");
  try
  {
   actor.AddComponent<Rigidbody2D>();var walk=actor.AddComponent<CampDirectedWalk>();target.transform.position=new Vector2(5,0);
   walk.BeginWalk(target.transform,2,.18f,3);Assert.That(walk.TimeoutSeconds,Is.EqualTo(3));
   var binding=actor.AddComponent<ActorTerrainBinding>();binding.ReadRevision=()=>0;binding.FindRoute=(a,b)=>new List<Vector2>{new Vector2(0,5),new Vector2(5,5),new Vector2(5,0)};
   walk.BeginWalk(target.transform,2,.18f,3);Assert.That(walk.PlannedRouteDistance,Is.EqualTo(15));Assert.That(walk.TimeoutSeconds,Is.GreaterThanOrEqualTo(8.75f));
  }
  finally{UnityEngine.Object.DestroyImmediate(actor);UnityEngine.Object.DestroyImmediate(target);}
 }
 [Test]public void AuthoredLayoutIsDeterministicAndCompact()
 {var a=SettlementCampLayout.Create();var b=SettlementCampLayout.Create();Assert.That(a.Fingerprint(),Is.EqualTo(b.Fingerprint()));Assert.That(a.Capacity,Is.EqualTo(10));var beds=a.Bedrooms.SelectMany(r=>r.Beds).ToArray();Assert.That(beds.Max(p=>p.X)-beds.Min(p=>p.X),Is.LessThan(24));Assert.That(beds.Max(p=>p.Y)-beds.Min(p=>p.Y),Is.LessThan(14));Assert.That(a.Bedrooms.All(r=>r.Beds.Count>=1&&r.Beds.Count<=3),Is.True);}
 [Test]public void ExistingStationBackingsRemainProtected()
 {var a=SettlementCampLayout.Create();foreach(var m in a.Markers.Where(m=>m.Id!="arrival.first"&&m.Id!="fire"&&m.Id!="storage"&&m.Id!="fight.reserved"&&m.Id!=SettlementCampLayout.RootId))for(int y=(int)m.Anchor.Y-3;y<=(int)m.Anchor.Y;y++)for(int x=(int)m.Anchor.X-3;x<=(int)m.Anchor.X+3;x++)Assert.That(a.Protected(x,y),Is.True,m.Id+" "+x+","+y);}
 [Test]public void PublicStationsAndRootFrontHaveThreeCellAccess()
 {var a=SettlementCampLayout.Create();var s=new CampSession(a,true);var fire=a.Marker("fire").Interaction;foreach(string id in new[]{"squad","bones","storage","run.departure","arrival.first","shop.reserved"})Assert.That(s.Route(fire,a.Marker(id).Interaction,3).Count,Is.GreaterThan(0),id);Assert.That(s.Route(fire,SettlementCampLayout.RootFront,3).Count,Is.GreaterThan(0),"root front");}
 [Test]public void OptionalRootDoesNotBlockAnyBedOrItsConstructionEntrance()
 {var a=SettlementCampLayout.Create();var terrain=new RegionTerrainState(a.Id,192,192,a);foreach(var room in a.Bedrooms)Assert.That(terrain.Route(a.Marker("fire").Interaction,room.Entrance,3).Count,Is.GreaterThan(0),room.Id);Assert.That(terrain.World.IsSolid(129,101),Is.True);}
 [Test]public void EveryBedHasPotentialThreeCellRouteAfterOrdinaryExcavation()
 {var a=SettlementCampLayout.Create();var s=new RegionTerrainState(a.Id,192,192,a);var domain=new List<DualTile>();for(int y=0;y<192;y++)for(int x=0;x<192;x++)if(a.Category(x,y)==CampCell.Residential||a.Category(x,y)==CampCell.Expansion)domain.Add(new DualTile(x,y));s.Commit(domain,s.Revision,c=>true);s.Commit(SettlementCampLayout.RootCells(),s.Revision,c=>true);foreach(var bed in a.Bedrooms.SelectMany(r=>r.Beds))Assert.That(s.Route(a.Marker("fire").Interaction,bed,3).Count,Is.GreaterThan(0),"bed "+bed.X+","+bed.Y);}
 [Test]public void RegionCommitRejectsStaleOrMixedProtectedBatchWithoutPartialMutation()
 {var a=SettlementCampLayout.Create();var s=new RegionTerrainState(a.Id,192,192,a);var dirt=a.Bedrooms[0].Cells.First(p=>a.Category(p.X,p.Y)==CampCell.Residential);Assert.That(s.Commit(new[]{dirt,new DualTile(0,0)},s.Revision,c=>true),Is.Zero);Assert.That(s.World.IsSolid(dirt.X,dirt.Y),Is.True);Assert.That(s.Commit(new[]{dirt},s.Revision,c=>true),Is.EqualTo(1));Assert.That(s.Commit(SettlementCampLayout.RootCells(),0,c=>true),Is.Zero);Assert.That(s.Revision,Is.EqualTo(1));}
 [Test]public void RegionMutationLeaseCannotBeBypassedOrAcquiredTwice()
 {var a=SettlementCampLayout.Create();var s=new RegionTerrainState(a.Id,192,192,a);var mutate=s.TakeMutationControl();Assert.Throws<InvalidOperationException>(()=>s.TakeMutationControl());Assert.Throws<InvalidOperationException>(()=>s.Commit(SettlementCampLayout.RootCells(),0,c=>true));Assert.Throws<InvalidOperationException>(()=>s.World.Excavate(107,98));Assert.That(mutate(SettlementCampLayout.RootCells(),0,c=>true),Is.EqualTo(SettlementCampLayout.RootCells().Count));}
 sealed class PlanningTerrain:IResidentialPlanTerrain
 {
  public readonly AuthoredCamp Definition=SettlementCampLayout.Create();public readonly RegionTerrainState State;readonly CampResidentialCatalog catalog;readonly LogicalGridTransform grid=new LogicalGridTransform(new Point2(0,0),.6,192);
  public PlanningTerrain(){State=new RegionTerrainState(Definition.Id,192,192,Definition);catalog=SettlementCampLayout.Catalog(Definition);}
  public float CellSize=>.6f;public CampResidentialCatalog GetResidentialCatalog()=>catalog;
  public List<Vector2Int> GetResidentialSlotFootprint(int slot)=>catalog.GetSlot(slot).ExcavationFootprint.Select(p=>new Vector2Int(p.x,p.y)).ToList();
  public List<Vector2Int> GetResidentialConstructionRoute(int slot)=>catalog.GetSlot(slot).ConstructionRoute.Select(p=>new Vector2Int(p.x,p.y)).ToList();
  public CampDigCategory GetSpatialDigCategory(Vector2Int p)=>Definition.Category(p.x,p.y)==CampCell.Residential?CampDigCategory.ResidentialReserved:CampDigCategory.NormalCampDiggable;
  public bool IsBlocked(Vector2Int c)=>State.World.IsSolid(c.x,c.y);
  public Vector2 CellToWorld(Vector2Int c){var p=grid.ToWorld(new Point2(c.x+.5,c.y+.5));return new Vector2((float)p.X,(float)p.Y);}
  public Vector2Int WorldToCell(Vector2 v){var p=grid.ToLogical(new Point2(v.x,v.y));return new Vector2Int((int)Math.Floor(p.X),(int)Math.Floor(p.Y));}
  public bool IsDerivedTerrainCollider(Collider2D c)=>false;
 }
 [TestCase(0)][TestCase(1)][TestCase(2)]public void RealProductionPlannerAcceptsAllTenCompactBeds(int excavation)
 {
  UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
  var t=new PlanningTerrain();for(int slot=1;slot<=10;slot++)
  {
   var fp=t.GetResidentialSlotFootprint(slot);var pre=excavation==0?fp.Take(0):excavation==1?fp.Take(fp.Count/2):fp;
   t.State.Commit(pre.Select(p=>new DualTile(p.x,p.y)),t.State.Revision,c=>true);
   Assert.That(ResidentialConstructionPlan.TryBuild(t,slot,Vector2.one*.43f,.72f,out var plan,out var why),Is.True,"slot "+slot+": "+why);
   if(excavation==2)Assert.That(plan.DigSteps.Sum(step=>step.DigCenters.Count),Is.Zero);
   foreach(var step in plan.DigSteps)foreach(var center in step.DigCenters)
   {var cut=fp.Where(p=>(t.CellToWorld(p)-t.CellToWorld(center)).sqrMagnitude<=.72f*.72f).Select(p=>new DualTile(p.x,p.y));t.State.Commit(cut,t.State.Revision,c=>true);}
   Assert.That(fp.All(p=>!t.IsBlocked(p)),Is.True,"required footprint "+slot);
   var bed=t.GetResidentialCatalog().GetSlot(slot).RestCell;Assert.That(t.State.Route(new Point2(bed.x+.5,bed.y+.5),t.Definition.Marker("fire").Interaction,3).Count,Is.GreaterThan(0),"return "+slot);
  }
 }
 [Test]public void ReusablePresentationReferenceMatchesAcceptedCameraExactly()
 {
  var host=new GameObject("reference camera contract only");try{var old=host.AddComponent<Stage49Preview>();old.Apply(2);old.Live.settings.anchor=.62f;
   foreach(var f in new[]{new Vector2(90.5f,113.5f),new Vector2(104,85)}){var a=old.Map(f);var b=WorldRegionPresentation.ReferenceSnapshot(f,old.Live.settings);for(int y=0;y<192;y+=11)for(int x=0;x<192;x+=11){var p=new Point2(x+.5,y+.5);Assert.That(b.Project(p),Is.EqualTo(a.Project(p)));}}
  }finally{UnityEngine.Object.DestroyImmediate(host);}
 }
}
#endif
