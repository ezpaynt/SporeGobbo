using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SporeGobbo.Geometry.Preview.Editor;

namespace SporeGobbo.Geometry.Preview.Tests
{
    public sealed class PlayablePreviewTests
    {
        static string Fixture=>AssetDatabase.LoadAssetAtPath<TextAsset>(GeometryPreviewSceneTools.FixturePath).text;
        static string Grid(int w,int h,Func<int,int,bool> solid)=>string.Join("\n",Enumerable.Range(0,h).Select(y=>new string(Enumerable.Range(0,w).Select(x=>solid(x,y)?'1':'0').ToArray())));
        [TestCase(1)][TestCase(3)][TestCase(9)]
        public void FootprintsFitOnlySufficientlyWidePassages(int size)
        {
            var t=new PlayableTerrain(Grid(24,24,(x,y)=>x<10||x>=10+size));
            var p=new Vector2(10+size*.5f,12);
            Assert.That(PreviewMotion.Fits(t,p,size),Is.True);
            Assert.That(PreviewMotion.Fits(t,p,size+.01f),Is.False);
            Assert.That(PreviewMotion.Move(t,p,new Vector2(0,4),size).y,Is.EqualTo(16).Within(.001));
            Assert.That(PreviewMotion.Move(t,p,new Vector2(4,0),size).x,Is.EqualTo(p.x).Within(.001));
        }
        [Test] public void SweptMovementCannotTunnelThroughThinWall()
        {
            var t=new PlayableTerrain(Grid(30,12,(x,y)=>x==12));var p=PreviewMotion.Move(t,new Vector2(5,6),new Vector2(20,0),1);
            Assert.That(p.x,Is.InRange(11.40f,11.5001f));Assert.That(PreviewMotion.Fits(t,p,1),Is.True);
        }
        [Test] public void DiagonalCornerCannotBeCrossed()
        {
            var t=new PlayableTerrain("1111\n1011\n1101\n1111");
            var p=PreviewMotion.Move(t,new Vector2(1.5f,1.5f),new Vector2(1,1),1);
            Assert.That(p,Is.EqualTo(new Vector2(1.5f,1.5f)));
        }
        [Test] public void NewExcavationIsImmediatelyTraversable()
        {
            var t=new PlayableTerrain(Grid(20,12,(x,y)=>x>=10));var p=new Vector2(9.5f,5.5f);
            Assert.That(PreviewMotion.Move(t,p,Vector2.right,1),Is.EqualTo(p));
            t.Excavate(new DualTile(10,5));var moved=PreviewMotion.Move(t,p,Vector2.right,1);
            Assert.That(moved.x,Is.EqualTo(10.5f).Within(.001));
            Assert.That(PreviewMotion.Fits(t,moved,3),Is.False);
        }
        [TestCase(DigBrush.Single,1)][TestCase(DigBrush.ThreeWide,9)][TestCase(DigBrush.Radius,13)]
        public void BrushPreviewExactlyDefinesChangedCells(DigBrush brush,int count)
        {
            var session=new PreviewEditSession(Grid(24,24,(x,y)=>true));var cells=new List<DualTile>();
            PreviewDigBrush.Cells(new Vector2(12.2f,12.7f),brush,2,session.Terrain,cells);
            Assert.That(cells.Count,Is.EqualTo(count));session.Dig(cells);
            CollectionAssert.AreEquivalent(cells,session.Changed);
            Assert.That(session.Dig(cells),Is.False);Assert.That(session.LastDirty.Count,Is.Zero);
        }
        [Test] public void BoundaryBrushClipsToWorldAndHasNoDuplicateCells()
        {
            var t=new PlayableTerrain(Grid(10,10,(x,y)=>true));var cells=new List<DualTile>();
            PreviewDigBrush.Cells(Vector2.zero,DigBrush.ThreeWide,2,t,cells);
            Assert.That(cells.Count,Is.EqualTo(4));Assert.That(cells.Distinct().Count(),Is.EqualTo(cells.Count));
        }
        [Test] public void UndersizedBrushWarningMatchesBodyRelationship()
        {
            Assert.That(PreviewDigBrush.TooSmall(DigBrush.Single,1,3),Is.True);
            Assert.That(PreviewDigBrush.TooSmall(DigBrush.ThreeWide,1,3),Is.False);
            Assert.That(PreviewDigBrush.TooSmall(DigBrush.Radius,2,3),Is.True);
            Assert.That(PreviewDigBrush.TooSmall(DigBrush.Radius,3,3),Is.False);
        }
        [TestCase(8)][TestCase(16)]
        public void CrossingPatchBoundaryRebuildsUniqueLocalPatchesAndMatchesFull(int size)
        {
            var session=new PreviewEditSession(Grid(48,48,(x,y)=>true),size);var cells=new List<DualTile>();
            PreviewDigBrush.Cells(new Vector2(16,16),DigBrush.ThreeWide,1,session.Terrain,cells);session.Dig(cells);
            Assert.That(session.LastDirty.Count,Is.LessThan(session.Patches.Count));Assert.That(session.LastDirty.Distinct().Count(),Is.EqualTo(session.LastDirty.Count));
            Assert.That(session.LastDirty.Any(k=>k.X<16),Is.True);Assert.That(session.LastDirty.Any(k=>k.X>=16),Is.True);
            AssertParity(session);
        }
        [TestCase(8)][TestCase(16)]
        public void RandomExcavationsAndResetMatchFreshFullBuild(int size)
        {
            string fixture=Grid(34,30,(x,y)=>x<3||y<3||x>30||y>26||(x*13+y*7)%5!=0);
            var session=new PreviewEditSession(fixture,size);var random=new System.Random(1843);var cells=new List<DualTile>();
            for(int i=0;i<25;i++)
            {
                PreviewDigBrush.Cells(new Vector2(random.Next(34),random.Next(30)),(DigBrush)(i%3),2.3f,session.Terrain,cells);
                session.Dig(cells);AssertParity(session);
            }
            session.Reset();var original=new PreviewFixture(fixture);
            for(int y=0;y<original.Height;y++)for(int x=0;x<original.Width;x++)Assert.That(session.Terrain.IsSolid(x,y),Is.EqualTo(original.IsSolid(x,y)));
            AssertParity(session);
        }
        static void AssertParity(PreviewEditSession session)
        {
            var fresh=new ContourTerrainCache(session.Terrain);
            foreach(var key in session.Patches.Keys)
            {
                var full=PreviewMeshBuilder.BuildPatch(session.Terrain,fresh,key.X,key.Y,session.PatchSize);
                for(int layer=0;layer<4;layer++)
                {CollectionAssert.AreEqual(full[layer].Vertices,session.Patches[key][layer].Vertices);CollectionAssert.AreEqual(full[layer].Indices,session.Patches[key][layer].Indices);}
            }
        }
        [Test] public void GeometryIsIdenticalAcrossEightAndSixteenCellPartitions()
        {
            var a=new PreviewEditSession(Fixture,8);var b=new PreviewEditSession(Fixture,16);
            var cells=new List<DualTile>();PreviewDigBrush.Cells(new Vector2(48,32),DigBrush.Radius,4,a.Terrain,cells);a.Dig(cells);b.Dig(cells);
            CollectionAssert.AreEqual(Triangles(a),Triangles(b));
        }
        static string[] Triangles(PreviewEditSession s)
        {
            var result=new List<string>();foreach(var patch in s.Patches.Values)foreach(var mesh in patch)
            for(int i=0;i<mesh.Indices.Count;i+=3)
            {var vertices=new string[3];for(int j=0;j<3;j++){var v=mesh.Vertices[mesh.Indices[i+j]];vertices[j]=v.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+v.y.ToString("R",System.Globalization.CultureInfo.InvariantCulture);}result.Add(mesh.Layer+":"+string.Join(";",vertices));}
            result.Sort(StringComparer.Ordinal);return result.ToArray();
        }
        [Test] public void MovingDoesNotEvaluateTopologyOrWallNormals()
        {
            var session=new PreviewEditSession(Fixture);int count=session.OrientationEvaluations;
            Vector2 p=PreviewMotion.FindSpawn(session.Terrain,new Vector2(50,39),1);
            for(int i=0;i<600;i++)p=PreviewMotion.Move(session.Terrain,p,new Vector2(.03f,-.01f),1);
            Assert.That(session.OrientationEvaluations,Is.EqualTo(count));
        }
        [Test] public void RuntimeMeshPoolIsStableAfterDiggingAndReset()
        {
            EditorSceneManager.OpenScene(PlayablePreviewTools.ScenePath);var live=UnityEngine.Object.FindAnyObjectByType<PlayableCavePreview>();live.Initialize();
            int objects=live.RuntimeObjectCount,meshes=live.RuntimeMeshCount;var ids=live.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh.GetInstanceID()).OrderBy(i=>i).ToArray();
            for(int i=0;i<12;i++){live.Predict(new Vector2(40+i,25));live.DigNow();}
            for(int i=0;i<100;i++){live.Step(new Vector2(.2f,0),Vector2.right,false,1f/60);live.DrawOverlay();}
            var next=new List<DualTile>();PreviewDigBrush.Cells(live.Position+live.Aim*(live.footprint*.5f+.55f),live.brush,live.digRadius,live.Session.Terrain,next);
            CollectionAssert.AreEqual(next,live.PredictedCells);
            live.ResetFixture();Assert.That(live.RuntimeObjectCount,Is.EqualTo(objects));Assert.That(live.RuntimeMeshCount,Is.EqualTo(meshes));
            CollectionAssert.AreEqual(ids,live.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh.GetInstanceID()).OrderBy(i=>i).ToArray());
            EditorSceneManager.OpenScene(GeometryPreviewSceneTools.ScenePath);
        }
    }
}
