using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using SporeGobbo.Geometry.Preview.Editor;
namespace SporeGobbo.Geometry.Preview.Tests
{
 public sealed class Stage50Tests
 {
  sealed class Corridor:IOccupancySource{public int Width=>160;public int Height=>160;public int Clearance=3;public bool IsSolid(int x,int y)=>x<1||y<1||x>=159||y>=159||Math.Abs(y-80)>Clearance/2;}
  [TestCase(1,true)][TestCase(2,true)][TestCase(3,true)][TestCase(5,false)][TestCase(7,false)][TestCase(9,false)][TestCase(11,false)]
  public void ThreeCellClearanceMatchesUnchangedCollision(int size,bool expected){var source=new Corridor();var nav=new LabNavigation(source,new Point2(80.5,80.5));var p=new Point2(80.5,80.5);Assert.That(nav.Fits(p,size),Is.EqualTo(expected));Assert.That(PreviewMotion.Fits(source,new Vector2(80.5f,80.5f),size),Is.EqualTo(expected));}
  [TestCase(1,true)][TestCase(2,true)][TestCase(3,true)][TestCase(5,true)][TestCase(7,true)][TestCase(9,true)][TestCase(11,false)]
  public void NineCellClearanceHasNoSilentAccommodation(int size,bool expected){var source=new Corridor{Clearance=9};var nav=new LabNavigation(source,new Point2(80.5,80.5));Assert.That(nav.Fits(new Point2(80.5,80.5),size),Is.EqualTo(expected));}
  static LabActor Actor(){var a=new LabActor();a.Reset(0,"deterministic",new List<Point2>{new Point2(40.5,80.5),new Point2(80.5,80.5)},1,true,LabAttack.LongMelee);return a;}
  [Test] public void PursuitPlaybackAndLogicalAttacksAreIndependentOfEveryCameraProfile()
  {
   var source=new Corridor{Clearance=9};var tuning=new LabTuning();var baseline=Actor();var candidate=Actor();for(int i=0;i<1800;i++){var player=new Point2(80.5,80.5);baseline.Step(1.0/60,player,tuning,source);var settings=Stage49Preview.Settings(i%4);var projection=new FocusProjection(new Point2(70.375+i*.001,80.8125),settings.centerWidth,settings.centerHeight,settings.distance,settings.side,settings.top,settings.foreground,new Point2(0,3));projection.Project(candidate.Position);candidate.Step(1.0/60,player,tuning,source);Assert.That(candidate.State,Is.EqualTo(baseline.State));Assert.That(candidate.Position,Is.EqualTo(baseline.Position));Assert.That(candidate.Timer,Is.EqualTo(baseline.Timer));Assert.That(candidate.Attacks,Is.EqualTo(baseline.Attacks));}Assert.That(baseline.Attacks,Is.GreaterThan(1));
  }
  [Test] public void SearchTimersAndDistanceRulesHaveExplicitLogicalTriggers()
  {
   var source=new Corridor{Clearance=9};var t=new LabTuning{rule=LabRule.DistanceThreshold};var shortActor=Actor();shortActor.Persistent=false;shortActor.State=LabState.Pursuing;shortActor.Timer=5;var longActor=Actor();longActor.State=LabState.Pursuing;longActor.Timer=15;var target=new Point2(100.5,80.5);shortActor.Step(.1,target,t,source);longActor.Step(.1,target,t,source);Assert.That(shortActor.State,Is.EqualTo(LabState.Searching));Assert.That(longActor.State,Is.EqualTo(LabState.Pursuing));for(int i=0;i<60;i++)shortActor.Step(.1,target,t,source);Assert.That(shortActor.State,Is.EqualTo(LabState.Disengaged));
  }
  [Test] public void CachedRoutesAndMovingTargetBreadcrumbsAreActuallyTraversable()
  {
   var world=new StreamingWorld(256,256);var nav=new LabNavigation(world,new Point2(128.5,128.5));var route=new List<Point2>();Assert.That(nav.Route(new Point2(210.5,128.5),1,route),Is.True);foreach(var p in route)Assert.That(PreviewMotion.Fits(world,new Vector2((float)p.X,(float)p.Y),1),Is.True);for(int i=1;i<route.Count;i++)Assert.That(LabNavigation.Distance(route[i],route[i-1]),Is.EqualTo(1));int trees=nav.TreeCount;Assert.That(nav.TraceToRoot(route[3],1,route),Is.True);Assert.That(nav.TreeCount,Is.EqualTo(trees));nav.Winding(1,route);foreach(var p in route)Assert.That(nav.Fits(p,1),Is.True);Assert.That(world.EditCount,Is.Zero);
  }
  [Test] public void VisualOverhangDoesNotChangeCollisionOrNavigationFootprints()
  {
   var source=new Corridor();var p=new Point2(80.5,80.5);Assert.That(PreviewMotion.Fits(source,new Vector2(80.5f,80.5f),3),Is.True);var b=Stage50Preview.VisualBounds(p,3,3);Assert.That(source.IsSolid((int)Math.Floor(b.Min.X),(int)Math.Floor(b.Min.Y)),Is.True);var d=new PreviewMeshData("boss",PreviewLayer.Gobbo);Stage50Preview.BuildSilhouette(d,p,3,3);foreach(var v in d.LogicalVertices){Assert.That(v.Logical.X,Is.InRange(b.Min.X,b.Max.X));Assert.That(v.Logical.Y,Is.InRange(b.Min.Y,b.Max.Y));}Assert.That(PreviewMotion.Fits(source,new Vector2(80.5f,80.5f),3),Is.True);
  }
  [TestCase(0)][TestCase(1)] public void ActorScaleAndGroundAnchorStayContinuous(int mode)
  {
   var p=new FocusProjection(new Point2(80.375,80.8125),48,28,24,.25,.2,1.9,new Point2(0,3));foreach(double x in new[]{56.375,32.375,104.375,128.375})foreach(double y in new[]{66.8125,42.8125,94.8125,104.8125}){var a=LabPresentation.Scale(p,new Point2(x-1e-6,y-1e-6),mode);var b=LabPresentation.Scale(p,new Point2(x+1e-6,y+1e-6),mode);Assert.That(Vector2.Distance(a,b),Is.LessThan(1e-5));if(mode==0)Assert.That(a.x,Is.EqualTo(a.y));var anchor=p.Project(new Point2(x,y));Assert.That(p.Unproject(anchor).X,Is.EqualTo(x).Within(1e-10));}
  }
  [Test] public void DirectionMarkerUsesProjectedPositionAndVisibilitySeparatesOcclusion()
  {
   var source=new Corridor();var p=new FocusProjection(new Point2(80.5,80.5),48,28,24,.25,.2,1.9,new Point2(0,3));var player=new Point2(80.5,80.5);Assert.That(LabPresentation.Visibility(p,player,new Point2(90.5,80.5),80,45,source),Is.EqualTo(LabVisibility.OnscreenVisible));Assert.That(LabPresentation.Visibility(p,player,new Point2(80.5,70.5),80,45,source),Is.EqualTo(LabVisibility.OnscreenOccluded));Assert.That(LabPresentation.Visibility(p,player,new Point2(150.5,80.5),80,45,source),Is.EqualTo(LabVisibility.OffscreenVisible));var target=new Point2(150.5,10.5);var q=p.Project(target);var edge=LabPresentation.Edge(p,target,80,45);Assert.That(Math.Abs(edge.X*q.Y-edge.Y*q.X),Is.LessThan(1e-8));Assert.That(Math.Abs(edge.X),Is.LessThanOrEqualTo(38.5));Assert.That(Math.Abs(edge.Y),Is.LessThanOrEqualTo(21));Assert.That(LabPresentation.Visibility(p,player,target,80,45,source),Is.EqualTo(LabVisibility.OffscreenOccluded));
  }
  [Test] public void ProjectileTrajectoryAndAllFourRangesRemainLogical()
  {
   var s=new Corridor();var t=new LabTuning();Assert.That(t.Reach(LabAttack.ShortMelee),Is.EqualTo(2));Assert.That(t.Reach(LabAttack.LongMelee),Is.EqualTo(5));Assert.That(t.Reach(LabAttack.Projectile),Is.EqualTo(18));Assert.That(t.Reach(LabAttack.WideArea),Is.EqualTo(7));var p=new PreviewLogicalProjectile(new Point2(80.5,80.5),new Point2(0,-1));p.Advance(1,20,18,s);Assert.That(p.Blocked,Is.True);Assert.That(p.Travelled,Is.LessThan(3));
  }
  [Test] public void DormantIdleActorsAdvanceAtReducedFrequencyWithoutCameraInput(){var a=Actor();a.Position=new Point2(5.5,80.5);a.Home=a.Position;var t=new LabTuning();var s=new Corridor();for(int i=0;i<20;i++)a.Step(.01,new Point2(120.5,80.5),t,s);Assert.That(a.Active,Is.False);Assert.That(a.Clock,Is.Zero);for(int i=0;i<40;i++)a.Step(.01,new Point2(120.5,80.5),t,s);Assert.That(a.Clock,Is.GreaterThan(.49));}
  [Test] public void RepeatedResetAndCameraChangesKeepPoolsAndAuthorityStable()
  {
   EditorSceneManager.OpenScene(Stage50Tools.ScenePath);try{var lab=UnityEngine.Object.FindAnyObjectByType<Stage50Preview>();lab.Initialize();lab.paused=true;var live=lab.Live;for(int n=0;n<700;n++)live.Refresh(0);int count=lab.OwnedMeshes,edits=live.World.EditCount;var position=live.Position;var old=live.Stream.Resident.ToDictionary(p=>p.Key,p=>(p.Value,p.Value.Revision));for(int n=0;n<12;n++){lab.Restart();lab.CameraStudy.Apply(n%4);for(int i=0;i<120;i++)live.Refresh(0);lab.Draw();Assert.That(lab.OwnedMeshes,Is.EqualTo(count));Assert.That(live.Position,Is.EqualTo(position));Assert.That(live.World.EditCount,Is.EqualTo(edits));foreach(var pair in live.Stream.Resident)if(old.TryGetValue(pair.Key,out var v)&&ReferenceEquals(v.Item1,pair.Value))Assert.That(pair.Value.Revision,Is.EqualTo(v.Item2));}Assert.That(lab.Navigation.TreeCount,Is.LessThanOrEqualTo(2));Assert.That(lab.GetComponentsInChildren<MeshFilter>().Count(f=>f.name.StartsWith("Stage50")),Is.EqualTo(count));}
   finally{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);}
  }
  [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)] public void SilhouetteBoundsAndDecorationsStaySeparateFromGroundFootprint(int kind)
  {
   var point=new Point2(80.375,80.8125);foreach(bool decor in new[]{false,true}){var d=new PreviewMeshData("shape",PreviewLayer.Gobbo);Stage50Preview.BuildSilhouette(d,point,7,kind,decor);var b=Stage50Preview.VisualBounds(point,7,kind,decor);foreach(var v in d.LogicalVertices){Assert.That(v.Logical.X,Is.InRange(b.Min.X-1e-10,b.Max.X+1e-10));Assert.That(v.Logical.Y,Is.InRange(b.Min.Y-1e-10,b.Max.Y+1e-10));}}
  }
  [Test] public void CoarseDormantPatrolConsumesFullLogicalDistanceAcrossWaypoints()
  {
   var actor=new LabActor();actor.Reset(0,"dormant",new List<Point2>{new Point2(5.5,80.5),new Point2(6.5,80.5),new Point2(7.5,80.5),new Point2(8.5,80.5)},1,false,LabAttack.ShortMelee);actor.Step(.5,new Point2(140.5,80.5),new LabTuning(),new Corridor());Assert.That(actor.Position.X,Is.EqualTo(7).Within(.0001));
  }

  [Test] public void SearchingUsesLastKnownPositionInsteadOfTrackingUnseenPlayer()
  {
   var a=Actor();a.State=LabState.Searching;a.Timer=5;a.LastKnown=new Point2(35.5,80.5);a.Step(.25,new Point2(100.5,80.5),new LabTuning(),new Corridor{Clearance=9});Assert.That(a.Position.X,Is.LessThan(40.5));Assert.That(a.State,Is.EqualTo(LabState.Searching));Assert.That(a.Timer,Is.EqualTo(4.75));
  }

  [Test] public void CoincidentAnchorsKeepAValidProjectileDirectionAndResetDeterministically()
  {
   var a=Actor();a.Position=new Point2(80.5,80.5);a.State=LabState.Pursuing;a.Attack=LabAttack.Projectile;var source=new Corridor{Clearance=9};var tuning=new LabTuning();Assert.DoesNotThrow(()=>{for(int i=0;i<300;i++)a.Step(1.0/60,new Point2(80.5,80.5),tuning,source);});Assert.That(a.Attacks,Is.GreaterThan(0));Assert.That(a.AttackDirection.X*a.AttackDirection.X+a.AttackDirection.Y*a.AttackDirection.Y,Is.EqualTo(1).Within(1e-9));a.Reset(0,"reset",new List<Point2>{new Point2(40.5,80.5),new Point2(80.5,80.5)},1,true,LabAttack.Projectile);Assert.That(a.AttackDirection,Is.EqualTo(new Point2(1,0)));
  }

  [Test] public void AroundCornerObjectiveIsOnscreenOccludedAndHasARealRoute()
  {
   var world=new StreamingWorld();var root=new Point2(512.5,512.5);var nav=new LabNavigation(world,root);var path=new List<Point2>();nav.Corner(1,path);Assert.That(path.Count,Is.GreaterThan(2));Assert.That(nav.Fits(path[0],1),Is.True);Assert.That(LabNavigation.Contact(world,root,path[0]),Is.False);foreach(var point in path)Assert.That(nav.Fits(point,1),Is.True);var projection=new FocusProjection(root,48,28,24,.25,.2,1.9,new Point2(0,3.927));Assert.That(LabPresentation.Visibility(projection,root,path[0],87.2727,49.0909,world),Is.EqualTo(LabVisibility.OnscreenOccluded));
  }

  [TestCase(0)][TestCase(1)] public void LargeSilhouetteIsPresentedBeforeItsGroundAnchorEntersViewport(int mode)
  {
   var root=new Point2(80.5,80.5);var p=new FocusProjection(root,48,28,24,.25,.2,1.9,new Point2(0,3.927));var anchor=new Point2(80.5,103.5);var logical=Stage50Preview.VisualBounds(anchor,9,3);var projected=LabPresentation.BillboardBounds(p,anchor,logical,mode);Assert.That(p.Project(anchor).Y,Is.GreaterThan(49.0909*.5));Assert.That(LabPresentation.Intersects(projected,87.2727,49.0909),Is.True);Assert.That(LabPresentation.Visibility(p,root,anchor,87.2727,49.0909,new Corridor{Clearance=159}),Is.EqualTo(LabVisibility.OffscreenVisible));
  }

 }
}
