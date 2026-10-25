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
 public sealed class Stage49Tests
 {
  Stage49Preview Open(){EditorSceneManager.OpenScene(Stage49Tools.ScenePath);var p=UnityEngine.Object.FindAnyObjectByType<Stage49Preview>();p.Live.Initialize();return p;}
  [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)]
  public void ProfilesKeepSquareCenterStraightAxesMonotonicRowsAndInverse(int profile)
  {
   var study=Open();try{study.Apply(profile);var p=study.Map(new Vector2(512.375f,512.8125f));double previousFore=1,previousTop=1;
    for(int i=0;i<=100;i++){double u=i*.5;var fore=Stage49Preview.CellSize(p,new Point2(p.Focus.X,p.Focus.Y+p.HalfHeight+.5+u));var top=Stage49Preview.CellSize(p,new Point2(p.Focus.X,p.Focus.Y-p.HalfHeight-.5-u));Assert.That(fore.Y,Is.GreaterThanOrEqualTo(previousFore-1e-12));Assert.That(top.Y,Is.LessThanOrEqualTo(previousTop+1e-12));previousFore=fore.Y;previousTop=top.Y;}
    foreach(double x in new[]{-100.25,-25.125,0,22.375,120.5})foreach(double y in new[]{-130.25,-18.125,0,19.75,100.125}){var q=new Point2(p.Focus.X+x,p.Focus.Y+y);var projected=p.Project(q);var inverse=p.Unproject(projected);Assert.That(inverse.X,Is.EqualTo(q.X).Within(1e-10));Assert.That(inverse.Y,Is.EqualTo(q.Y).Within(1e-10));Assert.That(p.Project(new Point2(q.X,q.Y+8)).X,Is.EqualTo(projected.X));Assert.That(p.Project(new Point2(q.X+8,q.Y)).Y,Is.EqualTo(projected.Y));}
    var size=Stage49Preview.CellSize(p,p.Focus);Assert.That(size.X,Is.EqualTo(1).Within(1e-12));Assert.That(size.Y,Is.EqualTo(1).Within(1e-12));
    foreach(var y in new[]{p.Focus.Y-p.HalfHeight,p.Focus.Y+p.HalfHeight,p.Focus.Y-p.HalfHeight-p.TopDistance,p.Focus.Y+p.HalfHeight+p.ForegroundDistance}){var a=p.Project(new Point2(p.Focus.X,y-1e-7));var b=p.Project(new Point2(p.Focus.X,y+1e-7));Assert.That(b.Y-a.Y,Is.InRange(0,1e-6));}
   }finally{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);}
  }
  [Test] public void StrongReferenceExactlyRetainsStage48MapAndZoom()
  {
   var study=Open();try{study.Apply(0);var s=study.Live.settings;Assert.That(study.Map(Vector2.zero).CurveBlend,Is.Zero);Assert.That(s.centerWidth,Is.EqualTo(48));Assert.That(s.centerHeight,Is.EqualTo(28));Assert.That(s.coverage,Is.EqualTo(.55f));Assert.That(s.anchor,Is.EqualTo(.58f));Assert.That(s.wallHeight,Is.EqualTo(2.15f));Assert.That(s.heightPolicy,Is.EqualTo(WallHeightPolicy.ProjectLogicalExtrusion));double dw=Math.Max(8,s.centerWidth)/Mathf.Clamp(s.coverage,.35f,.85f);var old=new FocusProjection(new Point2(512.375,512.8125),48,28,24,s.side,s.top,s.foreground,new Point2(0,(s.anchor-.5)*(dw/(16.0/9))));var p=study.Map(new Vector2(512.375f,512.8125f));for(int i=0;i<500;i++){var q=new Point2(350+i*.67,380+i*.53);Assert.That(p.Project(q),Is.EqualTo(old.Project(q)));}for(int n=0;n<4;n++){study.Apply(n);Assert.That(study.DisplayWidth,Is.EqualTo(dw).Within(.00002));}}
   finally{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);}
  }
  [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)]
  public void NormalCombatAndAttackOverlaysShareExactProjectionAndGpuKernel(int profile)
  {
   var study=Open();try{study.Apply(profile);var p=study.Map(new Vector2(512.375f,512.8125f));var b=Stage49Preview.Normal(p);var a=p.Project(b.Min);var z=p.Project(b.Max);Assert.That(z.X-a.X,Is.EqualTo(study.Live.settings.centerWidth).Within(1e-10));Assert.That(z.Y-a.Y,Is.EqualTo(study.Live.settings.centerHeight).Within(1e-10));var anchor=new Point2(515.5,520.5);var samples=new List<Stage45GpuValidation.Sample>();for(int i=0;i<128;i++){var q=Stage49Preview.RangePoint(anchor,6,i*Math.PI*2/128);double dx=q.X-anchor.X,dy=q.Y-anchor.Y;Assert.That(dx*dx+dy*dy,Is.EqualTo(36).Within(1e-10));samples.Add(new Stage45GpuValidation.Sample(q,0,default));}for(int i=0;i<64;i++){var q=new Point2(450+i*2,440+i*2);samples.Add(new Stage45GpuValidation.Sample(q,i%5,new WallSample(q,new Point2(Math.Cos(i),Math.Sin(i)))));}
    if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null){var shader=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Terrain/Geometry/Preview/Runtime/Stage45Parity.compute");var values=Stage45GpuValidation.Evaluate(shader,p,study.Live.settings,samples.ToArray());double max=0;for(int i=0;i<samples.Count;i++){var expected=Stage45GpuValidation.Cpu(p,study.Live.settings,samples[i]);max=Math.Max(max,Math.Max(Math.Abs(values[i].x-expected.X),Math.Abs(values[i].y-expected.Y)));}TestContext.WriteLine("Stage49 CPU/GPU max error "+max);Assert.That(max,Is.LessThan(.0003));}
   }finally{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);}
  }
  [Test] public void CameraSwitchPreservesAuthorityActorsAndRetainedChunkTopology()
  {
   var study=Open();try{var live=study.Live;study.freezeActors=true;for(int n=0;n<1000;n++)live.Refresh(0);var position=live.Position;var actors=live.Actors.Select(a=>a.Position).ToArray();var chunks=live.Stream.Resident.ToDictionary(k=>k.Key,k=>(k.Value,k.Value.Revision,k.Value.Layers.Select(m=>m.Indices.ToArray()).ToArray()));int digs=live.Stream.DigOperations,edits=live.World.EditCount;
    for(int n=0;n<4;n++){study.Apply(n);for(int i=0;i<350;i++)live.Refresh(0);Assert.That(live.Ready,Is.True);Assert.That(live.Stream.OverBudget,Is.False);Assert.That(live.Position,Is.EqualTo(position));CollectionAssert.AreEqual(actors,live.Actors.Select(a=>a.Position).ToArray());foreach(var pair in live.Stream.Resident)if(chunks.TryGetValue(pair.Key,out var prior)&&ReferenceEquals(prior.Item1,pair.Value)){Assert.That(pair.Value.Revision,Is.EqualTo(prior.Item2));for(int j=0;j<4;j++)CollectionAssert.AreEqual(prior.Item3[j],pair.Value.Layers[j].Indices);}}
    Assert.That(live.Stream.DigOperations,Is.EqualTo(digs));Assert.That(live.World.EditCount,Is.EqualTo(edits));
    var p=live.Snapshot;study.freezeFocus=true;live.Step(Vector2.right,Vector2.right,false,.1f);live.Refresh(0);Assert.That(live.Snapshot.Focus,Is.EqualTo(p.Focus));
   }finally{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);}
  }
  [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)] public void ViewportCornersAndWallExpandedSelectionAreCovered(int profile)
  {
   var study=Open();try{study.Apply(profile);var p=study.Map(new Vector2(512.375f,512.8125f));double w=study.DisplayWidth,h=w/(16.0/9);var region=StreamingVisibility.LogicalRegion(p,w,h,2.15);var set=new HashSet<DualTile>();StreamingVisibility.Select(region,1024,1024,set);Assert.That(set.Count,Is.LessThanOrEqualTo(384));foreach(double x in new[]{-w/2,0,w/2})foreach(double y in new[]{-h/2,0,h/2}){var q=p.Unproject(new Point2(x,y));Assert.That(StreamingVisibility.Contains(region,q),Is.True);Assert.That(set.Contains(StreamingCoordinates.Owner((int)q.X,(int)q.Y)),Is.True);}study.Live.settings.centerHeight=90;study.Constrain();Assert.That(study.Live.settings.centerHeight,Is.LessThanOrEqualTo((float)h));}
   finally{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);}
  }
  [Test] public void ComparisonBookmarksAreOpenWithoutEditingAuthority(){var s=Open();try{foreach(var p in s.Bookmarks())Assert.That(PreviewMotion.Fits(s.Live.World,p,1),Is.True);Assert.That(s.Live.World.EditCount,Is.Zero);}finally{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);}}
  [Test] public void OversizedViewportExplicitlyReportsCapacityWithoutChangingOccupancy()
  {
   var s=Open();try{s.Apply(2);s.Live.settings.top=.05f;s.Live.settings.side=.15f;s.Live.settings.coverage=.35f;s.softness=.5f;s.Constrain();var p=s.Map(new Vector2(512.5f,512.5f));s.Live.Stream.Select(p,s.DisplayWidth,s.DisplayWidth/(16.0/9),2.15);Assert.That(s.Live.Stream.OverBudget,Is.True);Assert.That(s.Live.Stream.Visible.Count,Is.GreaterThan(384));Assert.That(s.Live.World.EditCount,Is.Zero);}finally{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);}
  }
  [Test] public void CombatTargetAndFractionalFollowAnchorShareLogicalCells()
  {
   var s=Open();try{s.Apply(2);var p=s.Map(new Vector2(512.375f,512.8125f));var center=new Point2(512.375,512.8125);var b=Stage49Preview.CombatBounds(center,20,12);var a=p.Project(b.Min);var z=p.Project(b.Max);Assert.That(z.X-a.X,Is.EqualTo(20).Within(1e-12));Assert.That(z.Y-a.Y,Is.EqualTo(12).Within(1e-12));s.Live.Refresh(0);var position=s.Live.Position;s.Live.settings.anchor=.58f;s.Live.Refresh(0);var first=s.Live.Snapshot.Project(new Point2(position.x,position.y));s.Live.settings.anchor=.62f;s.Live.Refresh(0);var next=s.Live.Snapshot.Project(new Point2(position.x,position.y));Assert.That(s.Live.Position,Is.EqualTo(position));Assert.That(next.Y-first.Y,Is.EqualTo((.62f-.58f)*s.DisplayWidth/(16.0/9)).Within(1e-6));}finally{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);}
  }

 }
}
