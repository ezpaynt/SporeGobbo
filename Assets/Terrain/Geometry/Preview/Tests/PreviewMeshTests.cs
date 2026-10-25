using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SporeGobbo.Geometry.Preview;
using SporeGobbo.Geometry.Preview.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SporeGobbo.Geometry.PreviewTests
{
    public sealed class PreviewMeshTests
    {
        static PreviewFixture Fixture()=>new PreviewFixture(AssetDatabase.LoadAssetAtPath<TextAsset>(GeometryPreviewSceneTools.FixturePath).text);
        static double Area(PreviewMeshData mesh)
        {
            double area=0;for(int i=0;i<mesh.Indices.Count;i+=3)
            {var a=mesh.Vertices[mesh.Indices[i]];var b=mesh.Vertices[mesh.Indices[i+1]];var c=mesh.Vertices[mesh.Indices[i+2]];area+=Math.Abs((b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x))*.5;}
            return area;
        }
        [TestCaseSource(nameof(Cases))]
        public void EveryOccupancyCaseProducesCorrectRoundedFillArea(int c)
        {
            string text=((c&1)!=0?"1":"0")+((c&2)!=0?"1":"0")+"\n"+((c&8)!=0?"1":"0")+((c&4)!=0?"1":"0");
            var meshes=PreviewMeshBuilder.Build(new PreviewFixture(text));
            double actual=meshes.Where(m=>m.Name.StartsWith("Solid_")).Sum(Area);
            int bits=Enumerable.Range(0,4).Count(i=>(c&(1<<i))!=0);
            double corner=0;var curve=ContourGeometry.Samples(ContourTemplate.ArcTL);
            for(int i=0;i<curve.Count-1;i++)corner+=(curve[i].X*curve[i+1].Y-curve[i+1].X*curve[i].Y)*.5;
            double expected=bits==0?0:bits==4?1:bits==1?corner:bits==3?1-corner:c==5||c==10?1-2*corner:.5;
            Assert.That(actual,Is.EqualTo(expected).Within(2e-6),"Case "+c);
        }
        static IEnumerable<int> Cases=>Enumerable.Range(0,16);

        [Test]
        public void SuppliedFixtureIsOriginalAndMeshOutputIsDeterministic()
        {
            var fixture=Fixture();Assert.That(fixture.Width,Is.EqualTo(96));Assert.That(fixture.Height,Is.EqualTo(62));
            string original=System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath,"Terrain/Geometry/Tests/Reference~/occupancy.txt"));
            Assert.That(AssetDatabase.LoadAssetAtPath<TextAsset>(GeometryPreviewSceneTools.FixturePath).text.Replace("\r",""),Is.EqualTo(original.Replace("\r","")));
            var first=PreviewMeshBuilder.Build(fixture);var second=PreviewMeshBuilder.Build(fixture);Assert.That(first.Count,Is.EqualTo(second.Count));
            for(int i=0;i<first.Count;i++){Assert.That(first[i].Name,Is.EqualTo(second[i].Name));CollectionAssert.AreEqual(first[i].Vertices,second[i].Vertices);CollectionAssert.AreEqual(first[i].Indices,second[i].Indices);}
        }

        [Test]
        public void AllTrianglesHaveValidIndicesClockwiseWindingAndFiniteBounds()
        {
            var data=PreviewMeshBuilder.Build(Fixture());int triangles=0;
            foreach(var patch in data)
            {
                Assert.That(patch.Indices.Count%3,Is.Zero);
                for(int i=0;i<patch.Indices.Count;i+=3)
                {
                    for(int j=0;j<3;j++)Assert.That(patch.Indices[i+j],Is.InRange(0,patch.Vertices.Count-1));
                    var a=patch.Vertices[patch.Indices[i]];var b=patch.Vertices[patch.Indices[i+1]];var c=patch.Vertices[patch.Indices[i+2]];
                    Assert.That(Vector3.Cross(b-a,c-a).z,Is.LessThan(-1e-10),patch.Name);triangles++;
                }
                Mesh mesh=patch.CreateMesh();
                try
                {
                    foreach(Vector3 v in mesh.vertices)
                    {Assert.That(float.IsNaN(v.x)||float.IsInfinity(v.x)||float.IsNaN(v.y)||float.IsInfinity(v.y),Is.False);Assert.That(mesh.bounds.Contains(v),Is.True,patch.Name);}
                }
                finally{UnityEngine.Object.DestroyImmediate(mesh);}
            }
            Assert.That(data.Count,Is.LessThan(110));Assert.That(triangles,Is.GreaterThan(10000));
            Debug.Log("Stage 2 mesh validation: "+data.Count+" batched meshes, "+triangles+" nondegenerate clockwise triangles.");
        }

        [Test]
        public void FacesRetainReferenceDepthAndNoColliderGeometryIsIntroduced()
        {
            var fixture=Fixture();var data=PreviewMeshBuilder.Build(fixture);
            var points=new HashSet<Vector3>(data.Where(m=>m.Layer==PreviewLayer.Faces).SelectMany(m=>m.Vertices));
            var cache=new ContourTerrainCache(fixture);var projection=StaticGridProjection.Uniform(fixture.Width,fixture.Height);int tested=0;
            for(int y=0;y<fixture.Height-1;y++)for(int x=0;x<fixture.Width-1;x++)foreach(var strip in cache.GetTile(x,y))
            for(int i=0;i<strip.Samples.Count;i++)
            {
                var sample=strip.Samples[i];if(sample.BackWeight<.9)continue;
                // An exactly vertical reference strip projects its downward face edge-on: no area,
                // hence no rendered triangle. Test endpoints belonging to nonzero-width faces.
                bool hasWidth=(i>0&&Math.Abs(strip.Samples[i-1].Logical.X-sample.Logical.X)>1e-9)||
                    (i+1<strip.Samples.Count&&Math.Abs(strip.Samples[i+1].Logical.X-sample.Logical.X)>1e-9);
                if(!hasWidth)continue;
                var v=WallGeometry.Project(sample,projection,WallSettings.Reference);
                Assert.That(points.Contains(PreviewMeshData.RenderPoint(v.Top)),Is.True);
                Assert.That(points.Contains(PreviewMeshData.RenderPoint(v.Bottom)),Is.True);
                Assert.That(v.Bottom.Y-v.Top.Y,Is.GreaterThan(1.9));tested++;
            }
            Assert.That(tested,Is.GreaterThan(50));
        }

        [Test]
        public void SavedPreviewOpensWithBakedMeshesAndIndependentLayerToggles()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(GeometryPreviewSceneTools.ScenePath),Is.Not.Null);
            var scene=EditorSceneManager.OpenScene(GeometryPreviewSceneTools.ScenePath);
            try
            {
                var preview=UnityEngine.Object.FindAnyObjectByType<CaveGeometryPreview>();Assert.That(preview,Is.Not.Null);
                Assert.That(preview.previewCamera.orthographic,Is.True);Assert.That(preview.wallRenderers.Length,Is.GreaterThan(0));
                Assert.That(UnityEngine.Object.FindObjectsByType<Collider2D>().Length,Is.Zero);
                Assert.That(UnityEngine.Object.FindObjectsByType<Rigidbody2D>().Length,Is.Zero);
                foreach(var root in scene.GetRootGameObjects())foreach(var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))Assert.That(behaviour,Is.TypeOf<CaveGeometryPreview>());
                foreach(var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>())
                {Assert.That(renderer.sharedMaterial,Is.Not.Null);Assert.That(renderer.sharedMaterial.shader.name,Is.EqualTo("SporeGobbo/GeometryPreview/Flat"));Assert.That(renderer.GetComponent<MeshFilter>().sharedMesh,Is.Not.Null);}
                preview.showContours=false;preview.showGrid=true;preview.showWallFaces=false;preview.showForegroundLips=true;preview.ApplyVisibility();
                Assert.That(preview.contourRenderers.All(r=>!r.enabled),Is.True);Assert.That(preview.wallRenderers.All(r=>!r.enabled),Is.True);
                Assert.That(preview.gridRenderers.All(r=>r.enabled),Is.True);Assert.That(preview.lipRenderers.All(r=>r.enabled),Is.True);
                preview.showContours=true;preview.showGrid=false;preview.showWallFaces=true;preview.ApplyVisibility();
            }
            finally{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);}
        }
    }
}
