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
    public sealed class Stage45Tests
    {
        sealed class WorkCounter:IGeometryTrace
        {
            public readonly int[] Calls=new int[(int)GeometryPhase.Count];readonly IGeometryTrace previous;
            public WorkCounter(IGeometryTrace previous){this.previous=previous;}
            public void Begin(GeometryPhase phase){Calls[(int)phase]++;previous?.Begin(phase);}
            public void End(GeometryPhase phase){previous?.End(phase);}
        }
        static FocusProjection Map()=>new FocusProjection(new Point2(49.375,38.8125),48,28,7,.15,.1,2.5,new Point2(0,3.25));
        static PlayableTerrain Fixture()=>new PlayableTerrain(AssetDatabase.LoadAssetAtPath<TextAsset>(GeometryPreviewSceneTools.FixturePath).text);
        [Test] public void AuthoredRoutesHaveLogicalClearanceAndContinuousSpeedIndependentOfView()
        {
            var terrain=Fixture();var projection=Map();foreach(var route in Stage45MovingDemo.AuthoredRoutes())
            {
                var previous=route.AtDistance(0);for(double d=0;d<route.Length*2;d+=.137)
                {
                    var p=route.AtDistance(d);Assert.That(PreviewMotion.Fits(terrain,new Vector2((float)p.X,(float)p.Y),(float)Stage45MovingDemo.LogicalFootprint),Is.True,$"route {p.X},{p.Y}");
                    double dx=p.X-previous.X,dy=p.Y-previous.Y;Assert.That(Math.Sqrt(dx*dx+dy*dy),Is.LessThanOrEqualTo(.13700001));
                    var scale=Stage4CameraPreview.ActorScale(projection,p,0);Assert.That(scale.x,Is.EqualTo(scale.y));
                    var a=projection.Project(p);var b=projection.Unproject(a);Assert.That(b.X,Is.EqualTo(p.X).Within(1e-10));previous=p;
                }
            }
        }
        [TestCase(0)] [TestCase(1)] public void ActorScaleAndAnchorRemainContinuousAcrossStrongTransitions(int mode)
        {
            var p=Map();foreach(double x in new[]{18.375,25.375,73.375,80.375})foreach(double y in new[]{17.8125,24.8125,52.8125,55.7291666667})
            {var a=new Point2(x-1e-6,y-1e-6);var b=new Point2(x+1e-6,y+1e-6);Assert.That(Vector2.Distance(Stage4CameraPreview.ActorScale(p,a,mode),Stage4CameraPreview.ActorScale(p,b,mode)),Is.LessThan(1e-5));var pa=p.Project(a);var pb=p.Project(b);Assert.That(Math.Abs(pb.X-pa.X)+Math.Abs(pb.Y-pa.Y),Is.LessThan(1e-5));}
        }
        [Test] public void AttackRangeRemainsFiveLogicalCellsAcrossCompressedRegions()
        {
            var p=Map();foreach(var anchor in new[]{new Point2(10,10),new Point2(50,39),new Point2(85,50)})for(int i=0;i<96;i++)
            {var point=Stage45MovingDemo.RangePoint(anchor,5,i*2*Math.PI/96);double x=point.X-anchor.X,y=point.Y-anchor.Y;Assert.That(x*x+y*y,Is.EqualTo(25).Within(1e-10));var inverse=p.Unproject(p.Project(point));Assert.That(inverse.X,Is.EqualTo(point.X).Within(1e-10));Assert.That(inverse.Y,Is.EqualTo(point.Y).Within(1e-10));}
        }
        [Test] public void ProjectileLogicalTrajectoryAndCollisionAreCameraIndependentAndSwept()
        {
            var a=new PreviewLogicalProjectile(new Point2(65.5,39.5),new Point2(1,0));var b=new PreviewLogicalProjectile(a.Origin,a.Direction);var terrain=Fixture();
            for(int i=0;i<600;i++){a.Advance(1.0/60,8,30,terrain);var projection=new FocusProjection(new Point2(40+i*.1,30+i*.01),48,28,7,.15,.1,2.5,new Point2(0,3));projection.Project(b.Position);b.Advance(1.0/60,8,30,terrain);Assert.That(a.Position,Is.EqualTo(b.Position));Assert.That(a.Blocked,Is.EqualTo(b.Blocked));Assert.That(a.Travelled,Is.EqualTo(b.Travelled));}
            var wall=new PlayableTerrain("11111\n10001\n11111");var fast=new PreviewLogicalProjectile(new Point2(1.5,1.5),new Point2(1,0));fast.Advance(1,100,30,wall);Assert.That(fast.Blocked,Is.True);Assert.That(fast.HitCell,Is.EqualTo(new DualTile(4,1)));Assert.That(fast.Position.X,Is.LessThan(4));
        }
        [Test] public void CpuGpuSwitchingAndCameraOnlyMotionPreserveTerrainAndResources()
        {
            EditorSceneManager.OpenScene(Stage45PreviewTools.ScenePath);var originalTrace=GeometryTrace.Sink;
            try
            {
                var view=UnityEngine.Object.FindAnyObjectByType<Stage4CameraPreview>();var gpu=view.GetComponent<Stage45GpuProjection>();var demo=view.GetComponent<Stage45MovingDemo>();var live=view.GetComponent<PlayableCavePreview>();
                view.settings.smoothing=0;demo.paused=true;demo.Initialize();view.Refresh(0);int meshCount=live.RuntimeMeshCount+demo.MeshCount,entries=view.EntryCount;
                long queries=live.Session.Terrain.OccupancyQueries;int filters=live.Session.OrientationEvaluations;var position=live.Position;
                var indices=live.Session.Patches.Values.SelectMany(a=>a).Select(a=>a.Indices.ToArray()).ToArray();
                var work=new WorkCounter(originalTrace);GeometryTrace.Sink=work;
                for(int i=0;i<24;i++){gpu.useGpu=i%2==0;view.ExperimentFocus=new Vector2(49.375f+Mathf.Sin(i)*18,38.8125f+Mathf.Cos(i)*18);view.Preset(i%3);view.Refresh(.016f);}
                foreach(var phase in new[]{GeometryPhase.Contour,GeometryPhase.WallFilter,GeometryPhase.MeshBuild,GeometryPhase.Triangulation,GeometryPhase.CacheInvalidation})Assert.That(work.Calls[(int)phase],Is.Zero,phase.ToString());
                GeometryTrace.Sink=originalTrace;
                Assert.That(live.Session.Terrain.OccupancyQueries,Is.EqualTo(queries));Assert.That(live.Session.OrientationEvaluations,Is.EqualTo(filters));Assert.That(live.Position,Is.EqualTo(position));Assert.That(live.RuntimeMeshCount+demo.MeshCount,Is.EqualTo(meshCount));Assert.That(view.EntryCount,Is.EqualTo(entries));Assert.That(gpu.ResourceCount,Is.LessThanOrEqualTo(7));Assert.That(gpu.BufferCount,Is.LessThanOrEqualTo(entries));
                int j=0;foreach(var data in live.Session.Patches.Values.SelectMany(a=>a))CollectionAssert.AreEqual(indices[j++],data.Indices);
                gpu.useGpu=true;view.Refresh(0);gpu.useGpu=true;view.Refresh(0);Assert.That(gpu.TerrainUploads,Is.Zero);
                var target=new Point2(20.5,20.5);var inverse=view.InverseRender(PreviewMeshData.RenderPoint(view.Snapshot.Project(target)));live.Predict(inverse);var requested=live.PredictedCells.ToArray();live.DigNow();foreach(var cell in requested)Assert.That(live.Session.Terrain.IsSolid(cell.X,cell.Y),Is.False);
            }
            finally{GeometryTrace.Sink=originalTrace;EditorSceneManager.OpenScene(PlayablePreviewTools.ScenePath);}
        }
        [Test] public void GpuKernelAgreesWithCpuContractWithinPoint0002Units()
        {
            if(!SystemInfo.supportsComputeShaders||SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)Assert.Ignore("Needs a graphics-capable Unity run.");
            var shader=AssetDatabase.LoadAssetAtPath<ComputeShader>(GeometryPreviewSceneTools.Root+"/Runtime/Stage45Parity.compute");Assert.That(Stage45GpuValidation.Probe(shader),Is.LessThanOrEqualTo(.0002));
        }
        [Test] public void GpuDisplacedVerticesStayInsideTheirProjectedBoundsAndShareSeams()
        {
            if(!SystemInfo.supportsComputeShaders||SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)Assert.Ignore("Needs a graphics-capable Unity run.");
            EditorSceneManager.OpenScene(Stage45PreviewTools.ScenePath);
            try
            {
                var view=UnityEngine.Object.FindAnyObjectByType<Stage4CameraPreview>();var gpu=view.GetComponent<Stage45GpuProjection>();view.settings.smoothing=0;view.Preset(2);view.settings.wallHeight=4;view.ExperimentFocus=new Vector2(49.375f,38.8125f);view.Refresh(0);var shared=new Dictionary<(Vector4,Vector4,Vector4),Vector2>();int checkedVertices=0,seams=0;
                foreach(var filter in view.GetComponentsInChildren<MeshFilter>())
                {
                    if(!filter.GetComponent<MeshRenderer>().enabled)continue;var mesh=filter.sharedMesh;var vertices=mesh.vertices;var wall=new List<Vector4>();var roles=new List<Vector4>();mesh.GetUVs(0,wall);mesh.GetUVs(1,roles);if(wall.Count!=vertices.Length||vertices.Length==0)continue;
                    var samples=new List<Stage45GpuValidation.Sample>();for(int i=0;i<vertices.Length;i+=7)samples.Add(new Stage45GpuValidation.Sample{logicalRole=new Vector4(vertices[i].x,vertices[i].y,roles[i].x,0),wall=wall[i],offset=new Vector4(roles[i].y,roles[i].z,0,0)});
                    var output=Stage45GpuValidation.Evaluate(gpu.parityShader,view.Snapshot,view.settings,samples.ToArray());
                    for(int i=0;i<output.Length;i++){Assert.That(mesh.bounds.Contains(new Vector3(output[i].x,-output[i].y,0)),Is.True,filter.name);checkedVertices++;var key=(samples[i].logicalRole,samples[i].wall,samples[i].offset);if(shared.TryGetValue(key,out var prior)){Assert.That(output[i],Is.EqualTo(prior));seams++;}else shared[key]=output[i];}
                }
                Assert.That(checkedVertices,Is.GreaterThan(1000));Assert.That(seams,Is.GreaterThan(100));
            }
            finally{EditorSceneManager.OpenScene(PlayablePreviewTools.ScenePath);}
        }
        [Test] public void CpuGpuRenderedImagesAgreeIncludingTexturedActors()
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)Assert.Ignore("Needs a graphics-capable Unity run.");
            EditorSceneManager.OpenScene(Stage45PreviewTools.ScenePath);
            try
            {
                var view=UnityEngine.Object.FindAnyObjectByType<Stage4CameraPreview>();var gpu=view.GetComponent<Stage45GpuProjection>();var demo=view.GetComponent<Stage45MovingDemo>();demo.Initialize();demo.Advance(10);demo.paused=true;view.settings.smoothing=0;view.Preset(2);view.ExperimentFocus=new Vector2(49.375f,38.8125f);var camera=view.GetComponent<CaveGeometryPreview>().previewCamera;
                Assert.That(gpu.projectionShader.isSupported,Is.True);gpu.useGpu=false;view.Refresh(0);var cpu=Pixels(camera);gpu.useGpu=true;view.Refresh(0);var rendered=Pixels(camera);
                long difference=0;int large=0;for(int i=0;i<cpu.Length;i++){int delta=Math.Abs(cpu[i].r-rendered[i].r)+Math.Abs(cpu[i].g-rendered[i].g)+Math.Abs(cpu[i].b-rendered[i].b);difference+=delta;if(delta>24)large++;}
                Assert.That(difference/(double)(cpu.Length*3),Is.LessThan(1));Assert.That(large/(double)cpu.Length,Is.LessThan(.015));
            }
            finally{EditorSceneManager.OpenScene(PlayablePreviewTools.ScenePath);}
        }
        static Color32[] Pixels(Camera camera)
        {
            var rt=new RenderTexture(640,360,24);var image=new Texture2D(640,360,TextureFormat.RGB24,false);var old=camera.targetTexture;var active=RenderTexture.active;var rect=camera.rect;
            try{camera.targetTexture=rt;camera.rect=new Rect(0,0,1,1);camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,640,360),0,0);image.Apply();return image.GetPixels32();}
            finally{camera.targetTexture=old;camera.rect=rect;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
