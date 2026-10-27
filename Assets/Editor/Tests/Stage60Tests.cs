#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SporeGobbo.Geometry;
using SporeGobbo.CampCandidate;
using SporeGobbo.CampLifecycle;
public sealed class Stage60Tests
{
 [SetUp]public void EmptyDisposableScene(){UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);}
 CampSession New()=>new CampSession(Stage60Layout.Create(),true);
 [Test]public void SpawnedActorViewInitializesBeforeSortingAndOnlyOnce()
 {
  int meshes=Stage59ActorView.OwnedMeshes,materials=Stage59ActorView.OwnedMaterials;
  var owner=new GameObject("scoped presentation owner");var actor=new GameObject("newly spawned resident");
  try {var preview=owner.AddComponent<Stage59Preview>();preview.SpriteShader=Shader.Find("Sprites/Default");actor.AddComponent<SpriteRenderer>();var collider=actor.AddComponent<BoxCollider2D>();var view=actor.AddComponent<Stage59ActorView>();view.Owner=preview;view.Initialize();view.Initialize();Assert.That(view.PhysicalFoot.y,Is.EqualTo(collider.bounds.min.y));Assert.That(Stage59ActorView.OwnedMeshes,Is.EqualTo(meshes+2));Assert.That(Stage59ActorView.OwnedMaterials,Is.EqualTo(materials+3));}
  finally {var view=actor.GetComponent<Stage59ActorView>();if(view!=null){view.Release();view.Release();}UnityEngine.Object.DestroyImmediate(actor);UnityEngine.Object.DestroyImmediate(owner);}
  Assert.That(Stage59ActorView.OwnedMeshes,Is.EqualTo(meshes));Assert.That(Stage59ActorView.OwnedMaterials,Is.EqualTo(materials));
 }
 [Test]public void StructuralCollarHasNoDiggableBypass(){var d=Stage60Layout.Create();Assert.That(Stage60Layout.PotentialBypass(d),Is.False);Assert.That(d.Capacity,Is.EqualTo(10));Assert.That(d.Marker("fire").Anchor,Is.EqualTo(Stage57Layout.Create().Marker("fire").Anchor));}
 [Test]public void RootRequiresLivingBuddyAndOpensExactlyOnce(){var s=New();Assert.That(s.OpenIsolatedRoot(),Is.False);s.AddBuddy();Assert.That(Stage60Layout.RootCells().All(c=>s.Terrain.IsSolid(c.X,c.Y)),Is.True);int batches=0;s.Committed+=b=>{batches++;Assert.That(b.Reason,Does.Contain(Stage60Layout.RootId));Assert.That(b.Cells.Count,Is.EqualTo(15));};Assert.That(s.OpenIsolatedRoot(),Is.True);Assert.That(s.OpenIsolatedRoot(),Is.False);Assert.That(s.Revision,Is.EqualTo(1));Assert.That(batches,Is.EqualTo(1));Assert.That(s.Discovered,Is.True);}
 [Test]public void DeadBuddyCannotAuthorizeRoot(){var s=New();var r=s.AddBuddy();s.RemoveIsolatedBuddy(r.Id);Assert.That(s.OpenIsolatedRoot(),Is.False);}
 [Test]public void PlayerCanExcavateResidenceButNotRootOrPermanent(){var s=New();var a=s.Definition.Bedrooms[0].Cells.First(c=>s.Terrain.IsSolid(c.X,c.Y)&&s.Definition.Category(c.X,c.Y)==CampCell.Residential);var cells=new[]{a,new DualTile(105,95),new DualTile(0,0)};Assert.That(s.DigIsolatedResidence(cells,TerrainDigAuthority.Player),Is.EqualTo(1));Assert.That(s.Terrain.IsSolid(105,95),Is.True);Assert.That(s.Terrain.IsSolid(0,0),Is.True);}
 [Test]public void OrdinaryPreviewCannotUseNewEditPowers(){var s=new CampSession(Stage57Layout.Create());Assert.That(s.DigIsolatedResidence(s.Definition.Bedrooms[0].Cells.ToArray(),TerrainDigAuthority.Player),Is.Zero);Assert.That(s.OpenIsolatedRoot(),Is.False);}
 [Test]public void MissingResidentialAuthorizationRemovesNothing(){var s=New();Assert.That(s.DigIsolatedResidence(s.Definition.Bedrooms[0].Cells.ToArray(),TerrainDigAuthority.ResidentialProgression),Is.Zero);}
 [TestCase(0)] [TestCase(1)] [TestCase(2)]
 public void ProductionPlannerSupportsUntouchedPartialAndFullyOpen(int condition)
 {
  var s=New();s.AddBuddy();s.OpenIsolatedRoot();using(var authority=new Stage59Authority(s))
  {
   var terrain=new Stage60ResidenceTerrain(authority);var footprint=terrain.GetResidentialSlotFootprint(1);var selected=condition==0?Array.Empty<Vector2Int>():condition==1?footprint.Take(footprint.Count/2).ToArray():footprint.ToArray();s.DigIsolatedResidence(selected.Select(c=>new DualTile(c.x,c.y)),TerrainDigAuthority.Player);
   Assert.That(ResidentialConstructionPlan.TryBuild(terrain,1,Vector2.one*.43f,.72f,out var plan,out var why),Is.True,why);
   int cuts=plan.DigSteps.Sum(p=>p.DigCenters.Count);if(condition==2)Assert.That(cuts,Is.Zero);else Assert.That(cuts,Is.GreaterThan(0));
  }
 }
 [Test]public void EveryBedPlansAndCommitsWithProductionOccupancyPolicy()
 {
  var s=New();s.AddBuddy();s.OpenIsolatedRoot();using(var authority=new Stage59Authority(s))
  {var terrain=new Stage60ResidenceTerrain(authority);for(int slot=1;slot<=10;slot++)
   {
    if(slot>1)s.AddBuddy();var worker=s.Residents.First(r=>r.Alive&&r.Slot==0);
    Assert.That(ResidentialConstructionPlan.TryBuild(terrain,slot,Vector2.one*.43f,.72f,out var plan,out var why),Is.True,"slot "+slot+": "+why);
    foreach(var step in plan.DigSteps)foreach(var center in step.DigCenters){var fp=terrain.GetResidentialSlotFootprint(slot);var result=terrain.Dig(terrain.CellToWorld(center),.72f,TerrainDigAuthority.ResidentialProgression,slot,fp);Assert.That(result.Changed,Is.True);}
    var point=terrain.Catalog.GetSlot(slot).RestCell;Assert.That(s.CompleteIsolatedBed(worker.Id,slot,terrain.GetResidentialSlotFootprint(slot).Select(c=>new DualTile(c.x,c.y)).ToArray(),new Point2(point.x+.5,point.y+.5)),Is.True,"bed "+slot+" no return clearance");
   }Assert.That(s.Residents.Select(r=>r.Slot).Distinct().Count(),Is.EqualTo(10));
   string first=s.Residents[0].Id;s.RemoveIsolatedBuddy(first);var replacement=s.AddBuddy();Assert.That(replacement.Slot,Is.EqualTo(1));Assert.That(s.Residents[0].Slot,Is.Zero);
  }
 }
 [Test]public void RevisionBatchesContainExactlyRemovedCells(){var s=New();s.AddBuddy();s.OpenIsolatedRoot();int rev=s.Revision;var cells=s.Definition.Bedrooms[0].Cells.ToArray();int changed=0;s.Committed+=b=>{Assert.That(b.Revision,Is.EqualTo(++rev));foreach(var p in b.Cells)Assert.That(s.Terrain.IsSolid(p.X,p.Y),Is.False);changed+=b.Cells.Count;};int removed=s.DigIsolatedResidence(cells,TerrainDigAuthority.Player);Assert.That(changed,Is.EqualTo(removed));}
 [Test]public void RepeatedResetUsesIndependentOwnedSessionState(){var d=Stage60Layout.Create();for(int i=0;i<8;i++){var s=new CampSession(d,true);s.AddBuddy();s.OpenIsolatedRoot();s.DigIsolatedResidence(d.Bedrooms[0].Cells.ToArray(),TerrainDigAuthority.Player);var fresh=new CampSession(d,true);Assert.That(fresh.Revision,Is.Zero);Assert.That(fresh.Living,Is.Zero);Assert.That(Stage60Layout.RootCells().All(c=>fresh.Terrain.IsSolid(c.X,c.Y)),Is.True);}}
 [Test]public void ProductionServicesAreNotRegisteredByLogicalScenario(){var gs=GameState.Instance;var terrain=DiggableTerrainService.Active;var map=MapGenerator.Instance;var s=New();s.AddBuddy();s.OpenIsolatedRoot();Assert.That(GameState.Instance,Is.SameAs(gs));Assert.That(DiggableTerrainService.Active,Is.SameAs(terrain));Assert.That(MapGenerator.Instance,Is.SameAs(map));}
 [Test]public void RootAndResidentialTransactionsMatchFreshFullMeshBuilds()
 {
  var s=New();var cache=new ContourTerrainCache(s.Terrain);
  var patches=new[]{new DualTile(96,80),new DualTile(96,96),new DualTile(112,80)};
  foreach(var p in patches)SporeGobbo.Geometry.Preview.PreviewMeshBuilder.BuildPatch(s.Terrain,cache,p.X,p.Y,16);
  s.Committed+=b=>cache.InvalidateCells(b.Cells);s.AddBuddy();s.OpenIsolatedRoot();s.DigIsolatedResidence(s.Definition.Bedrooms[0].Cells.ToArray(),TerrainDigAuthority.Player);
  var fresh=new ContourTerrainCache(s.Terrain);foreach(var p in patches)
  {
   var a=SporeGobbo.Geometry.Preview.PreviewMeshBuilder.BuildPatch(s.Terrain,cache,p.X,p.Y,16);var b=SporeGobbo.Geometry.Preview.PreviewMeshBuilder.BuildPatch(s.Terrain,fresh,p.X,p.Y,16);
   for(int i=0;i<4;i++){CollectionAssert.AreEqual(a[i].Vertices,b[i].Vertices);CollectionAssert.AreEqual(a[i].Indices,b[i].Indices);}
  }
 }
 [Test]public void StructuralChangesRemainLocalAndStationBackingsStayProtected()
 {
  var original=Stage57Layout.Create();var changed=Stage60Layout.Create();
  for(int y=0;y<192;y++)for(int x=0;x<192;x++)if(x<89||x>142||y<53||y>103)Assert.That(changed.Category(x,y),Is.EqualTo(original.Category(x,y)));
  foreach(var marker in original.Markers.Where(m=>m.Id!="arrival.first"))for(int y=(int)marker.Anchor.Y-2;y<=(int)marker.Anchor.Y+2;y++)for(int x=(int)marker.Anchor.X-2;x<=(int)marker.Anchor.X+2;x++)if(original.Protected(x,y))Assert.That(changed.Protected(x,y),Is.True,marker.Id+" backing "+x+","+y);
  var s=new CampSession(changed,true);Assert.That(s.Route(changed.Marker("fire").Interaction,changed.Marker("squad").Interaction,3).Count,Is.GreaterThan(0));
 }
 [Test]public void FireObservationPointHasConservativeClearanceAndPublicRoute(){var s=New();Assert.That(s.Fits(Stage60Preview.FireWatch,3),Is.True);Assert.That(s.Route(s.Definition.Marker("fire").Interaction,Stage60Preview.FireWatch,3).Count,Is.GreaterThan(0));}
 [Test]public void StaleOrForeignResidentialAuthorizationIsRejected()
 {
  var s=New();s.AddBuddy();s.OpenIsolatedRoot();using(var a=new Stage59Authority(s)){var terrain=new Stage60ResidenceTerrain(a);var fp=terrain.GetResidentialSlotFootprint(1);int revision=s.Revision;
   Assert.That(terrain.Dig(terrain.CellToWorld(fp[0]),.72f,TerrainDigAuthority.ResidentialProgression,2,fp).Changed,Is.False);
   var foreign=new List<Vector2Int>(fp){new Vector2Int(128,87)};Assert.That(terrain.Dig(terrain.CellToWorld(fp[0]),.72f,TerrainDigAuthority.ResidentialProgression,1,foreign).Changed,Is.False);Assert.That(s.Revision,Is.EqualTo(revision));}
 }
}
#endif
