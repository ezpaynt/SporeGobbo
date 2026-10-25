using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
namespace SporeGobbo.Geometry.Preview.Tests
{
    public sealed class Stage47Tests
    {
        static FocusProjection Map(double x,double y,bool uniform=false)=>new FocusProjection(new Point2(x,y),24,16,12,.25,.2,1.9,new Point2(0,2),uniform);
        static void Drain(StreamingGeometry stream){int guard=0;while(stream.Pending>0&&stream.Resident.Count<stream.Capacity&&guard++<1000)stream.Pump(20,10000);Assert.That(guard,Is.LessThan(1000));}
        [Test] public void DeterministicAuthorityAndSeedAndCompactMemory()
        {
            StreamingWorld a=new StreamingWorld(128,128),b=new StreamingWorld(128,128),c=new StreamingWorld(128,128,33);int differences=0;
            for(int y=0;y<128;y++)for(int x=0;x<128;x++){Assert.That(a.IsSolid(x,y),Is.EqualTo(b.IsSolid(x,y)));if(a.IsSolid(x,y)!=c.IsSolid(x,y))differences++;}
            Assert.That(differences,Is.GreaterThan(100));Assert.That(new StreamingWorld().OccupancyBytes,Is.EqualTo(131072));
            Assert.That(a.Excavate(0,30),Is.False);Assert.That(a.IsSolid(-1,50),Is.True);
        }
        [TestCase(-1,-1)] [TestCase(0,0)] [TestCase(15,0)] [TestCase(16,1)] [TestCase(1023,63)]
        public void ChunkConversionUsesFloorAndHalfOpenOwnership(int cell,int owner){Assert.That(StreamingCoordinates.Owner(cell,cell).X,Is.EqualTo(owner));}
        [Test] public void InverseVisibilityIncludesCompressedDistanceAndWallsAndWorldEdges()
        {
            var selected=new HashSet<DualTile>();var map=Map(512,512);var bounds=StreamingVisibility.LogicalRegion(map,90,50,4,24);StreamingVisibility.Select(bounds,1024,1024,selected);
            var uniform=StreamingVisibility.LogicalRegion(Map(512,512,true),90,50,4,24);Assert.That(bounds.Max.X-bounds.Min.X,Is.GreaterThan(uniform.Max.X-uniform.Min.X));
            for(int y=0;y<1024;y+=4)for(int x=0;x<1024;x+=4)
            {var p=new Point2(x+.5,y+.5);var projected=map.Project(p);if(Math.Abs(projected.X)<45&&Math.Abs(projected.Y)<25)Assert.That(selected.Contains(StreamingCoordinates.Owner(x,y)),Is.True);}
            StreamingVisibility.Select(StreamingVisibility.LogicalRegion(Map(0,0),90,50,4,24),1024,1024,selected);Assert.That(selected.All(k=>k.X>=0&&k.Y>=0&&k.X<64&&k.Y<64),Is.True);
        }
        [Test] public void IndependentChunkCachesProduceExactSharedWallNormalsAndVertices()
        {
            var world=new StreamingWorld(128,128);ContourTerrainCache a=new ContourTerrainCache(world),b=new ContourTerrainCache(world);int checkedCount=0;
            for(int y=0;y<127;y++)foreach(var strip in a.GetTile(15,y))foreach(var sample in strip.Samples)
            {
                var other=b.Orientation.Get(sample.Logical);Assert.That(other.SolidNormal.X,Is.EqualTo(sample.SolidNormal.X));Assert.That(other.SolidNormal.Y,Is.EqualTo(sample.SolidNormal.Y));
                var map=Map(64,64);WallDisplayVertex wa=WallGeometry.Project(sample,map,WallSettings.Reference),wb=WallGeometry.Project(other,map,WallSettings.Reference);Assert.That(wa.Bottom.X,Is.EqualTo(wb.Bottom.X));Assert.That(wa.Bottom.Y,Is.EqualTo(wb.Bottom.Y));checkedCount++;
            }
            Assert.That(checkedCount,Is.GreaterThan(50));
            int joined=0;var endpoints=new Dictionary<Point2,WallSample>();
            for(int y=0;y<127;y++)foreach(var strip in a.GetTile(15,y))foreach(var sample in strip.Samples)if(sample.Logical.X==16.5)endpoints[sample.Logical]=sample;
            for(int y=0;y<127;y++)foreach(var strip in b.GetTile(16,y))foreach(var sample in strip.Samples)if(sample.Logical.X==16.5&&endpoints.TryGetValue(sample.Logical,out var adjacent))
            {Assert.That(sample.SolidNormal,Is.EqualTo(adjacent.SolidNormal));Assert.That(sample.BackWeight,Is.EqualTo(adjacent.BackWeight));Assert.That(sample.FrontWeight,Is.EqualTo(adjacent.FrontWeight));joined++;}
            Assert.That(joined,Is.GreaterThan(2));
            var left=PreviewMeshBuilder.BuildPatch(world,a,0,0,16);var right=PreviewMeshBuilder.BuildPatch(world,b,16,0,16);
            foreach(var layer in left.Concat(right))foreach(var vertex in layer.LogicalVertices)Assert.That(double.IsNaN(vertex.Logical.X),Is.False);
        }
        [Test] public void DormancyRetainsClockAndPositionDespiteVisiblePresentation()
        {
            var world=new StreamingWorld(128,128);var actor=new StreamingActor(new Point2(90.5,64.5));var bounds=new ProjectionBounds(new Point2(0,0),new Point2(128,128));
            for(int i=0;i<100;i++)actor.Step(new Point2(10,64),bounds,.016,world,20);
            Assert.That(actor.Visible,Is.True);Assert.That(actor.Active,Is.False);Assert.That(actor.Updates,Is.Zero);Assert.That(actor.Clock,Is.Zero);Assert.That(actor.Position.X,Is.EqualTo(90.5));
            actor.Step(new Point2(90,64),bounds,1,world,20);Assert.That(actor.Updates,Is.EqualTo(1));var position=actor.Position;
            actor.Step(new Point2(10,64),new ProjectionBounds(new Point2(0,0),new Point2(20,20)),10,world,20);Assert.That(actor.Visible,Is.False);Assert.That(actor.Position.X,Is.EqualTo(position.X));Assert.That(actor.Clock,Is.EqualTo(1));
            actor.Step(new Point2(90,64),bounds,1,world,20);Assert.That(actor.Clock,Is.EqualTo(2));Assert.That(actor.Updates,Is.EqualTo(2));
        }
        [Test] public void ExcavationSurvivesEvictionAndIsolatedDeltaReload()
        {
            var world=new StreamingWorld(256,256);using(var stream=new StreamingGeometry(world,100))
            {
                stream.Select(Map(64,64),30,20,2,0);Drain(stream);var cell=new DualTile(60,60);while(!world.IsSolid(cell.X,cell.Y))cell=new DualTile(cell.X+1,cell.Y);
                Assert.That(stream.Dig(new[]{cell}),Is.EqualTo(1));stream.Select(Map(220,220),30,20,2,0);Drain(stream);Assert.That(stream.Resident.ContainsKey(StreamingCoordinates.Owner(cell.X,cell.Y)),Is.False);
                stream.Select(Map(64,64),30,20,2,0);Drain(stream);Assert.That(world.IsSolid(cell.X,cell.Y),Is.False);
                using(var memory=new MemoryStream()){world.Save(memory);var saved=memory.ToArray();memory.Position=0;var restored=StreamingWorld.Load(memory);Assert.That(restored.IsSolid(cell.X,cell.Y),Is.False);Assert.That(restored.EditCount,Is.EqualTo(1));using(var second=new MemoryStream()){restored.Save(second);CollectionAssert.AreEqual(saved,second.ToArray());}}
            }
        }
        [Test] public void ResidencyCachesAndCleanupRemainBoundedAcrossRepeatedLifecycle()
        {
            var world=new StreamingWorld(256,256);int evictions=0;var stream=new StreamingGeometry(world,64);var released=new List<StreamedChunk>();stream.Evicted=c=>{released.Add(c);evictions++;};
            for(int i=0;i<24;i++)
            {stream.Select(Map(i%2==0?24:232,i%3==0?24:232),24,16,2,0);Drain(stream);Assert.That(stream.Resident.Count,Is.LessThanOrEqualTo(64));Assert.That(stream.CacheTiles,Is.LessThanOrEqualTo(64*256));foreach(var c in released){Assert.That(c.Cache.CachedTileCount,Is.Zero);Assert.That(c.Cache.Orientation.CachedCount,Is.Zero);Assert.That(c.Layers,Is.Null);}released.Clear();}
            stream.Dispose();Assert.That(stream.Resident.Count,Is.Zero);Assert.That(stream.CacheSamples,Is.Zero);Assert.That(evictions,Is.GreaterThan(50));
        }
        [Test] public void LocalDigRebuildsOnlyResidentDependencyOwnersAndPresetsNeverChangeAuthority()
        {
            var world=new StreamingWorld(128,128);using(var stream=new StreamingGeometry(world,128))
            {
                stream.Select(Map(64,64),50,35,2,0);Drain(stream);var before=stream.Resident.ToDictionary(k=>k.Key,k=>k.Value.Revision);
                stream.Dig(new[]{new DualTile(47,47),new DualTile(48,47),new DualTile(47,48),new DualTile(48,48)});Assert.That(stream.LastDigRebuilds,Is.LessThanOrEqualTo(4));Assert.That(stream.LastDigRebuilds,Is.GreaterThan(0));
                int changed=stream.Resident.Count(k=>k.Value.Revision!=before[k.Key]);Assert.That(changed,Is.EqualTo(stream.LastDigRebuilds));int edits=world.EditCount;
                for(int i=0;i<6;i++){stream.Select(Map(64,64,i%2==0),50,35,2,0);Drain(stream);Assert.That(world.EditCount,Is.EqualTo(edits));}
            }
        }
        [Test] public void ActivationBudgetAndOverflowAreExplicitAndBounded()
        {
            using(var stream=new StreamingGeometry(new StreamingWorld(256,256),8)){stream.Select(Map(128,128),200,120,4);Assert.That(stream.OverBudget,Is.True);Assert.That(stream.Pump(1,1000),Is.EqualTo(1));Assert.That(stream.Resident.Count,Is.EqualTo(1));Drain(stream);Assert.That(stream.Resident.Count,Is.EqualTo(8));Assert.That(stream.Ready,Is.False);}
        }
        [Test] public void VisibleChunksTakeCapacityBackFromPrefetchAfterCameraChanges()
        {
            using(var stream=new StreamingGeometry(new StreamingWorld(256,256),12))
            {
                foreach(int focus in new[]{64,96,80,128,64})
                {
                    for(int i=0;i<40;i++){stream.Select(Map(focus,64),12,8,0,64);stream.Pump(2,1000);}
                    Assert.That(stream.Visible.Count,Is.LessThanOrEqualTo(12));Assert.That(stream.Ready,Is.True);Assert.That(stream.Resident.Count,Is.LessThanOrEqualTo(12));
                }
            }
        }
        [Test] public void OriginRelativeGpuPrecisionMatchesDoubleAtWorldExtremes()
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)Assert.Ignore("Graphics-capable validation required");
            var shader=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Terrain/Geometry/Preview/Runtime/Stage45Parity.compute");var settings=new Stage4Settings{side=.15f,top=.1f,foreground=2.5f,wallHeight=4};double worst=0;
            foreach(double camera in new[]{8.125,512.375,1015.75})
            {
                var global=new FocusProjection(new Point2(camera,camera),48,28,7,.15,.1,2.5,new Point2(0,3.25));var local=new FocusProjection(new Point2(camera-512,camera-512),48,28,7,.15,.1,2.5,new Point2(0,3.25));var samples=new Stage45GpuValidation.Sample[1024];
                for(int i=0;i<1024;i++){var p=new Point2(i+.125,(i*37)%1024+.375);var relative=new Point2(p.X-512,p.Y-512);samples[i]=new Stage45GpuValidation.Sample(relative,i%5,new WallSample(relative,new Point2(Math.Cos(i*.7),Math.Sin(i*.7))));}
                var output=Stage45GpuValidation.Evaluate(shader,local,settings,samples);
                for(int i=0;i<1024;i++){var original=samples[i];original.logicalRole.x+=512;original.logicalRole.y+=512;var expected=Stage45GpuValidation.Cpu(global,settings,original);worst=Math.Max(worst,Math.Max(Math.Abs(output[i].x-expected.X),Math.Abs(output[i].y-expected.Y)));}
            }
            TestContext.WriteLine("Origin-relative GPU maximum error: "+worst);Assert.That(worst,Is.LessThan(.0005));
        }
        [Test] public void RenderResourceLifecycleDisposesBuffersMeshesAndObjects()
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)Assert.Ignore("Graphics-capable validation required");
            Editor.Stage47PreviewTools.Install();
            try
            {
                var live=UnityEngine.Object.FindAnyObjectByType<Stage47Preview>();live.Initialize();live.settings.smoothing=0;
                for(int i=0;i<300;i++){live.Refresh(1f/60);if(live.Ready&&live.Stream.Pending==0)break;}
                Assert.That(live.Ready,Is.True);Assert.That(live.Gpu.TerrainUploads,Is.LessThanOrEqualTo(12));
                live.Refresh(1f/60);Assert.That(live.Gpu.TerrainUploads,Is.Zero,"A camera frame must not reupload static terrain");
                VerifyGpuBounds(live);
                for(int i=0;i<4;i++)
                {
                    live.Teleport(new Vector2(i%2==0?24.5f:1000.5f,512.5f));
                    for(int n=0;n<300;n++){live.Refresh(1f/60);if(live.Ready&&live.Stream.Pending==0)break;}
                    Assert.That(live.Ready,Is.True);Assert.That(live.GetComponentsInChildren<MeshFilter>().Length,Is.EqualTo(live.MeshCount));Assert.That(live.Gpu.BufferCount,Is.LessThanOrEqualTo(live.MeshCount));
                    VerifyGpuBounds(live);
                }
                live.Stream.Dispose();Assert.That(live.GetComponentsInChildren<MeshFilter>().Length,Is.EqualTo(3));Assert.That(live.Gpu.BufferCount,Is.EqualTo(3));Assert.That(live.Stream.CacheSamples,Is.Zero);
            }
            finally{UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Editor.PlayablePreviewTools.ScenePath);}
        }
        static void VerifyGpuBounds(Stage47Preview live)
        {
            var p=live.Snapshot;var local=new FocusProjection(new Point2(p.Focus.X-512,p.Focus.Y-512),p.HalfWidth*2,p.HalfHeight*2,p.Distance,p.Side,p.Top,p.Foreground,p.Offset);
            foreach(var filter in live.GetComponentsInChildren<MeshFilter>())
            {
                if(!filter.GetComponent<MeshRenderer>().enabled)continue;var mesh=filter.sharedMesh;var vertices=mesh.vertices;var walls=new List<Vector4>();var roles=new List<Vector4>();mesh.GetUVs(0,walls);mesh.GetUVs(1,roles);
                for(int i=0;i<vertices.Length;i+=19)
                {
                    var sample=new Stage45GpuValidation.Sample{logicalRole=new Vector4(vertices[i].x,vertices[i].y,roles[i].x,0),wall=walls[i],offset=Vector4.zero};var expected=Stage45GpuValidation.Cpu(local,live.settings,sample);
                    Assert.That(mesh.bounds.Contains(new Vector3((float)expected.X,(float)-expected.Y,0)),Is.True,filter.name);
                }
            }
        }
    }
}


