using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor.SceneManagement;
using SporeGobbo.Geometry.Preview.Editor;
namespace SporeGobbo.Geometry.Preview.Tests
{
    public sealed class Stage4ProjectionTests
    {
        static FocusProjection Map(double side=.25,double top=.2,double front=1.9,Point2? focus=null)=>new FocusProjection(focus??new Point2(50.25,39.75),48,28,24,side,top,front,new Point2(0,3));
        [TestCase(.7,.6,1.2)] [TestCase(.45,.35,1.5)] [TestCase(.25,.2,1.9)]
        public void MonotoneSquareStraightAndRoundTrips(double side,double top,double front)
        {
            var p=Map(side,top,front);
            for(int i=-200;i<400;i++)
            {
                var a=new Point2(i*.37,i*.31);var b=new Point2(a.X+.01,a.Y+.01);
                var pa=p.Project(a);var pb=p.Project(b);var restored=p.Unproject(pa);
                Assert.That(pb.X,Is.GreaterThan(pa.X));Assert.That(pb.Y,Is.GreaterThan(pa.Y));
                Assert.That(restored.X,Is.EqualTo(a.X).Within(1e-10));Assert.That(restored.Y,Is.EqualTo(a.Y).Within(1e-10));
                Assert.That(p.Project(new Point2(a.X,0)).X,Is.EqualTo(pa.X));Assert.That(p.Project(new Point2(0,a.Y)).Y,Is.EqualTo(pa.Y));
            }
            for(int x=-20;x<20;x++)for(int y=-12;y<12;y++)
            {var a=p.Project(new Point2(p.Focus.X+x,p.Focus.Y+y));var b=p.Project(new Point2(p.Focus.X+x+1,p.Focus.Y+y+1));Assert.That(b.X-a.X,Is.EqualTo(1).Within(1e-12));Assert.That(b.Y-a.Y,Is.EqualTo(1).Within(1e-12));}
        }
        [Test] public void FractionalFocusAndTransitionDerivativesAreContinuous()
        {
            const double eps=1e-6;var p=Map();
            foreach(double x in new[]{p.Focus.X-48,p.Focus.X-24,p.Focus.X+24,p.Focus.X+48})
            {Assert.That(Math.Abs(p.ScaleX(x-eps)-p.ScaleX(x+eps)),Is.LessThan(1e-6));Assert.That(Math.Abs(p.Project(new Point2(x+eps,0)).X-p.Project(new Point2(x-eps,0)).X),Is.LessThan(3e-6));}
            var a=Map(focus:new Point2(50.25,39.75));var b=Map(focus:new Point2(50.25+eps,39.75+eps));
            var anchor=new Point2(90,5);Assert.That(Math.Abs(a.Project(anchor).X-b.Project(anchor).X),Is.LessThan(2e-6));
            foreach(double y in new[]{1.75,25.75,53.75,77.75})
            {var left=WallGeometry.Project(new WallSample(new Point2(50,y-eps),new Point2(0,-1)),p,WallSettings.Reference,WallHeightPolicy.ProjectLogicalExtrusion);var right=WallGeometry.Project(new WallSample(new Point2(50,y+eps),new Point2(0,-1)),p,WallSettings.Reference,WallHeightPolicy.ProjectLogicalExtrusion);Assert.That(Math.Abs(left.Bottom.Y-right.Bottom.Y),Is.LessThan(5e-6));}
        }
        [Test] public void SharedPatchAnchorsResolveIdenticallyAndBoundsContainAllRoles()
        {
            var source=new PlayableTerrain(string.Join("\n",Enumerable.Range(0,34).Select(y=>new string(Enumerable.Range(0,34).Select(x=>(x+y)%3==0?'1':'0').ToArray()))));
            var cache=new ContourTerrainCache(source);var p=Map(focus:new Point2(12.75,18.25));var shared=new Dictionary<string,Tuple<int,Vector3>>();int matches=0;
            foreach(int px in new[]{0,16})foreach(var data in PreviewMeshBuilder.BuildPatch(source,cache,px,0,16))
            {
                var bounds=p.ConservativeBounds(new ProjectionBounds(new Point2(px,0),new Point2(px+17,17)),2.15,.22);
                for(int i=0;i<data.LogicalVertices.Count;i++)
                {
                    var v=data.LogicalVertices[i];var resolved=data.ResolveVertex(i,p,WallHeightPolicy.ProjectLogicalExtrusion);
                    Assert.That(bounds.Contains(resolved),Is.True);
                    if(v.WallSampleIndex>=0){var wall=data.WallSamples[v.WallSampleIndex];string key=wall.Logical.X.ToString("R")+","+wall.Logical.Y.ToString("R")+":"+v.Role;var render=PreviewMeshData.RenderPoint(resolved);if(shared.TryGetValue(key,out var prior)){Assert.That(render,Is.EqualTo(prior.Item2));if(prior.Item1!=px)matches++;}else shared[key]=Tuple.Create(px,render);}
                }
            }
            Assert.That(matches,Is.GreaterThan(2));
        }
        [Test] public void ActorScalingAndAnchorsAreContinuousInBothModes()
        {
            var p=Map();foreach(int mode in new[]{0,1})for(int i=0;i<120;i++)
            {var a=new Point2(i,20);var b=new Point2(i+1e-5,20+1e-5);var sa=Stage4CameraPreview.ActorScale(p,a,mode);var sb=Stage4CameraPreview.ActorScale(p,b,mode);Assert.That(Vector2.Distance(sa,sb),Is.LessThan(1e-5));if(mode==0)Assert.That(sa.x,Is.EqualTo(sa.y));}
        }
        [Test] public void CameraOnlyRefreshDoesNotReadTerrainOrRebuildAndPoolIsStable()
        {
            EditorSceneManager.OpenScene(Stage4PreviewTools.ScenePath);
            try
            {
                var view=UnityEngine.Object.FindAnyObjectByType<Stage4CameraPreview>();view.Refresh(1);var live=view.GetComponent<PlayableCavePreview>();view.settings.smoothing=0;
                long queries=live.Session.Terrain.OccupancyQueries;int filters=live.Session.OrientationEvaluations,count=view.EntryCount,meshes=live.RuntimeMeshCount;
                var position=live.Position;var indices=live.Session.Patches.Values.SelectMany(a=>a).Select(a=>a.Indices.ToArray()).ToArray();
                for(int i=0;i<40;i++){view.ExperimentFocus=position+new Vector2(Mathf.Sin(i*.4f)*20,Mathf.Cos(i*.4f)*20);view.Preset(i%3);view.settings.wallHeight=.5f+(i%4);view.settings.stage3Comparison=i%4==0;view.Refresh(.016f);
                    foreach(var filter in view.GetComponentsInChildren<MeshFilter>()){if(!filter.GetComponent<MeshRenderer>().enabled)continue;var mesh=filter.sharedMesh;var vertices=mesh.vertices;for(int v=0;v<vertices.Length;v+=173)Assert.That(mesh.bounds.Contains(vertices[v]),Is.True);}}
                Assert.That(live.Position,Is.EqualTo(position));Assert.That(live.Session.Terrain.OccupancyQueries,Is.EqualTo(queries));Assert.That(live.Session.OrientationEvaluations,Is.EqualTo(filters));Assert.That(view.EntryCount,Is.EqualTo(count));Assert.That(live.RuntimeMeshCount,Is.EqualTo(meshes));
                int j=0;foreach(var a in live.Session.Patches.Values.SelectMany(a=>a))CollectionAssert.AreEqual(indices[j++],a.Indices);
                // Inverse target yields exactly the logical brush cells and occupancy changes.
                var target=new Point2(20.5,20.5);var render=PreviewMeshData.RenderPoint(view.Snapshot.Project(target));var recovered=view.InverseRender(render);live.Predict(recovered);var cells=live.PredictedCells.ToArray();live.DigNow();foreach(var cell in cells)Assert.That(live.Session.Terrain.IsSolid(cell.X,cell.Y),Is.False);
                Assert.That(PreviewMotion.Fits(live.Session.Terrain,position,live.footprint),Is.True);
            }
            finally{EditorSceneManager.OpenScene(PlayablePreviewTools.ScenePath);}
        }
    }
}
