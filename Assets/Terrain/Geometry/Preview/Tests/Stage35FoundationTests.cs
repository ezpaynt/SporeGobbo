using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using SporeGobbo.Geometry.Preview.Editor;

namespace SporeGobbo.Geometry.Preview.Tests
{
    public sealed class Stage35FoundationTests
    {
        sealed class Field : IOccupancySource
        {
            public int Width=>2048;
            public int Height=>2048;
            public int Queries;
            public bool IsSolid(int x,int y){Queries++;return (x/3+y/4)%2==0;}
        }
        static string Fixture=>UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(GeometryPreviewSceneTools.FixturePath).text;

        [Test] public void Stage3PositionsIndicesAndPatchOwnershipRemainByteIdentical()
        {
            string path=Path.Combine(Application.dataPath,"Terrain/Geometry/Preview/Tests/Reference~/Stage3Golden.json");
            var expected=JsonUtility.FromJson<Stage35ProfilingTests.Golden>(File.ReadAllText(path));
            var actual=Stage35ProfilingTests.BuildGoldens();
            CollectionAssert.AreEqual(expected.names,actual.names);
            for(int i=0;i<expected.names.Count;i++)Assert.That(actual.hashes[i],Is.EqualTo(expected.hashes[i]),expected.names[i]);
        }
        [Test] public void RemoteEditsVisitNoDistantSamplesInLargeCache()
        {
            var source=new Field();var cache=new WallOrientationCache(source);
            for(int y=0;y<100;y++)for(int x=0;x<100;x++)cache.Get(new Point2(x+.5,y+.5));
            int evaluations=cache.Evaluations,queries=source.Queries;
            var cells=new List<DualTile>{new DualTile(1500,1500),new DualTile(1501,1501)};
            for(int i=0;i<25;i++)cache.InvalidateCells(cells);
            Assert.That(cache.CachedCount,Is.EqualTo(10000));Assert.That(cache.LastInvalidationVisitedSamples,Is.Zero);
            Assert.That(cache.Evaluations,Is.EqualTo(evaluations));Assert.That(source.Queries,Is.EqualTo(queries));
        }
        [Test] public void SpatialInvalidationMatchesExactReferenceDependencyAcrossBucketEdges()
        {
            var source=new Field();var cache=new WallOrientationCache(source);var points=new List<Point2>();
            for(int y=0;y<=48;y++)for(int x=0;x<=48;x++){var p=new Point2(x*.25,y*.25);points.Add(p);cache.Get(p);}
            var cells=new List<DualTile>{new DualTile(3,3),new DualTile(4,4),new DualTile(8,8)};
            int before=cache.Evaluations;
            int affected=points.Count(p=>cells.Any(c=>Math.Abs(p.X-(c.X+.5))<=4&&Math.Abs(p.Y-(c.Y+.5))<=4));
            cache.InvalidateCells(cells);
            foreach(var point in points)cache.Get(point);
            Assert.That(cache.Evaluations-before,Is.EqualTo(affected));
            Assert.That(cache.CachedCount,Is.EqualTo(points.Count));
        }
        [Test] public void SingleAndBatchInvalidationRetainIdenticalSamplesAfterRepeatedEdits()
        {
            var a=new WallOrientationCache(new Field());var b=new WallOrientationCache(new Field());
            var cells=new List<DualTile>{new DualTile(0,0),new DualTile(15,16),new DualTile(16,16),new DualTile(31,31)};
            var points=new List<Point2>();for(int y=0;y<33;y++)for(int x=0;x<33;x++)points.Add(new Point2(x+.5,y+.5));
            for(int repeat=0;repeat<8;repeat++)
            {
                foreach(var p in points){a.Get(p);b.Get(p);}
                for(int i=0;i<cells.Count;i++)a.InvalidateCell(cells[i].X,cells[i].Y);b.InvalidateCells(cells);
                foreach(var p in points)Assert.That(a.Get(p).SolidNormal,Is.EqualTo(b.Get(p).SolidNormal));
            }
        }
        [TestCase(8)][TestCase(16)][TestCase(32)]
        public void RebuildsReuseBuffersAndResetLogicalVertexRecords(int size)
        {
            var session=new PreviewEditSession(Fixture,size);var before=session.Patches.ToDictionary(p=>p.Key,p=>p.Value);
            var cells=new List<DualTile>();
            for(int i=0;i<20;i++)
            {
                PreviewDigBrush.Cells(new Vector2(48+i%5,26+i%7),DigBrush.Radius,5,session.Terrain,cells);session.Dig(cells);
                foreach(var key in session.Patches.Keys)
                {
                    Assert.That(session.Patches[key],Is.SameAs(before[key]));
                    foreach(var layer in session.Patches[key])Assert.That(layer.LogicalVertices.Count,Is.EqualTo(layer.Vertices.Count));
                }
            }
            session.Reset();foreach(var key in session.Patches.Keys)Assert.That(session.Patches[key],Is.SameAs(before[key]));
        }
        [Test] public void LogicalRecordsResolveReferenceVerticesWithoutTopologyOrTerrainQueries()
        {
            var source=new Field();var cache=new ContourTerrainCache(source);var data=PreviewMeshBuilder.BuildPatch(source,cache,8,8,8);
            int queries=source.Queries,evaluations=cache.Orientation.Evaluations;
            var roles=new HashSet<GeometryVertexRole>();
            foreach(var mesh in data)
            {
                var indices=mesh.Indices.ToArray();
                for(int i=0;i<mesh.Vertices.Count;i++)
                {
                    roles.Add(mesh.LogicalVertices[i].Role);
                    Assert.That(PreviewMeshData.RenderPoint(mesh.ResolveVertex(i,UniformGridProjection.Unit)),Is.EqualTo(mesh.Vertices[i]));
                    var scaled=mesh.ResolveVertex(i,new UniformGridProjection(2));
                    Assert.That(scaled.X,Is.EqualTo(mesh.Vertices[i].x*2).Within(.00001));
                    Assert.That(scaled.Y,Is.EqualTo(-mesh.Vertices[i].y*2).Within(.00001));
                }
                CollectionAssert.AreEqual(indices,mesh.Indices);
            }
            Assert.That(roles,Does.Contain(GeometryVertexRole.WallBottom));Assert.That(roles,Does.Contain(GeometryVertexRole.ForegroundLip));
            Assert.That(source.Queries,Is.EqualTo(queries));Assert.That(cache.Orientation.Evaluations,Is.EqualTo(evaluations));
            Assert.That(System.Runtime.InteropServices.Marshal.SizeOf<LogicalMeshVertex>(),Is.EqualTo(24));
        }
        [Test] public void ProjectionForwardInverseRoundTripsAndBoundsContainBothHeightPolicies()
        {
            var projection=new StaticGridProjection(new[]{.5,1.0,1.0,.5},new[]{.3,1.0,1.0,2.0});
            foreach(var point in new[]{new Point2(-3,-2),new Point2(0,0),new Point2(.3,1.7),new Point2(4,4),new Point2(8,9)})
            {
                var restored=projection.Unproject(projection.Project(point));
                Assert.That(restored.X,Is.EqualTo(point.X).Within(1e-12));Assert.That(restored.Y,Is.EqualTo(point.Y).Within(1e-12));
            }
            var bounds=projection.ConservativeBounds(new ProjectionBounds(new Point2(0,0),new Point2(4,4)),2.15,.22);
            foreach(WallHeightPolicy policy in Enum.GetValues(typeof(WallHeightPolicy)))
            for(int y=0;y<=16;y++)for(int x=0;x<=16;x++)
            {
                var wall=WallGeometry.Project(new WallSample(new Point2(x*.25,y*.25),new Point2(.6,-.8)),projection,WallSettings.Reference,policy);
                Assert.That(bounds.Contains(wall.Top)&&bounds.Contains(wall.Bottom)&&bounds.Contains(wall.Lip),Is.True);
            }
            var framing=new ProjectionFraming(1280,800,20);Assert.That(framing.DisplayWidth,Is.EqualTo(64));Assert.That(framing.DisplayHeight,Is.EqualTo(40));
            var layout=ProjectionLayout.Reference;Assert.That(layout.CenterColumns,Is.EqualTo(48));Assert.That(layout.CenterRows,Is.EqualTo(28));
        }
        [Test] public void ContinuousHeightAlternativeRemovesRowBoundaryJumpWithoutChangingReference()
        {
            var projection=new StaticGridProjection(new[]{1.0},new[]{1.0,1.0,1.1,1.2});
            var left=new WallSample(new Point2(.5,2-1e-7),new Point2(0,-1));
            var right=new WallSample(new Point2(.5,2+1e-7),new Point2(0,-1));
            var a=WallGeometry.Project(left,projection,WallSettings.Reference);var b=WallGeometry.Project(right,projection,WallSettings.Reference);
            Assert.That((b.Bottom.Y-b.Top.Y)-(a.Bottom.Y-a.Top.Y),Is.EqualTo(.215).Within(1e-8));
            var ca=WallGeometry.Project(left,projection,WallSettings.Reference,WallHeightPolicy.ProjectLogicalExtrusion);
            var cb=WallGeometry.Project(right,projection,WallSettings.Reference,WallHeightPolicy.ProjectLogicalExtrusion);
            Assert.That(Math.Abs(cb.Bottom.Y-ca.Bottom.Y),Is.LessThan(1e-6));
            Assert.That(WallSettings.Reference.HeightCells,Is.EqualTo(2.15));
        }
        [Test] public void DenseThirtyTwoCellPatchUsesValidUInt32MeshAndFiniteBounds()
        {
            string text=string.Join("\n",Enumerable.Range(0,33).Select(y=>new string(Enumerable.Range(0,33).Select(x=>(x+y)%2==0?'1':'0').ToArray())));
            var source=new PlayableTerrain(text);var data=PreviewMeshBuilder.BuildPatch(source,new ContourTerrainCache(source),0,0,32);
            Assert.That(data.Any(m=>m.Vertices.Count>65535),Is.True);
            foreach(var layer in data)
            {
                var mesh=layer.CreateMesh();
                try
                {
                    Assert.That(mesh.indexFormat,Is.EqualTo(layer.Vertices.Count>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16));
                    foreach(var v in layer.Vertices)Assert.That(mesh.bounds.Contains(v),Is.True);
                    for(int i=0;i<layer.Indices.Count;i+=3)
                        Assert.That(Vector3.Cross(layer.Vertices[layer.Indices[i+1]]-layer.Vertices[layer.Indices[i]],layer.Vertices[layer.Indices[i+2]]-layer.Vertices[layer.Indices[i]]).z,Is.LessThan(0));
                }
                finally{UnityEngine.Object.DestroyImmediate(mesh);}
            }
        }
    }
}
