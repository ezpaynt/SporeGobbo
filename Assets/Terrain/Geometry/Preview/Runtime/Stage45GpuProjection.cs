using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;
namespace SporeGobbo.Geometry.Preview
{
    /// <summary>Exports the CPU mathematical contract. No independent shader preset or layout rules.</summary>
    public static class ProjectionShaderContract
    {
        public static readonly int Focus=Shader.PropertyToID("_SG_FocusOffset"),Center=Shader.PropertyToID("_SG_CenterDistance"),Scales=Shader.PropertyToID("_SG_Scales"),Polynomial=Shader.PropertyToID("_SG_Polynomial"),Options=Shader.PropertyToID("_SG_Options"),CurveShape=Shader.PropertyToID("_SG_CurveShape"),AxisDistances=Shader.PropertyToID("_SG_AxisDistances"),Roles=Shader.PropertyToID("_SG_Roles");
        public static void Apply(FocusProjection p,Stage4Settings settings,bool gpu,ComputeShader compute=null,Point2 origin=default)
        {
            Set(Focus,new Vector4((float)(p.Focus.X-origin.X),(float)(p.Focus.Y-origin.Y),(float)p.Offset.X,(float)p.Offset.Y),compute);
            Set(Center,new Vector4((float)p.HalfWidth,(float)p.HalfHeight,(float)p.Distance,(float)(p.Distance*FocusProjection.ForegroundDistanceRatio)),compute);
            Set(CurveShape,new Vector4((float)p.CurveBlend,0,0,0),compute);
            Set(AxisDistances,new Vector4((float)p.SideDistance,(float)p.TopDistance,(float)p.ForegroundDistance,0),compute);
            Set(Scales,new Vector4((float)p.Side,(float)p.Top,(float)p.Foreground,settings.stage3Comparison?2.15f:Mathf.Max(0,settings.wallHeight)),compute);
            Set(Polynomial,new Vector4((float)FocusProjection.SmoothQuadratic,(float)FocusProjection.SmoothCubic,(float)FocusProjection.IntegralCubic,(float)FocusProjection.IntegralQuartic),compute);
            Set(Options,new Vector4(settings.stage3Comparison?0:(int)settings.heightPolicy,settings.actorMode,gpu?1:0,.22f),compute);
            Set(Roles,new Vector4((int)GeometryVertexRole.WallBottom,(int)GeometryVertexRole.ForegroundLip,8,0),compute);
        }
        static void Set(int id,Vector4 value,ComputeShader compute){if(compute!=null)compute.SetVector(id,value);else Shader.SetGlobalVector(id,value);}
        public static ProjectionBounds Bounds(PreviewMeshData data,ProjectionBounds logical,FocusProjection projection,Stage4Settings settings)
        {
            if(!data.VisualActorAnchor.HasValue)return projection.ConservativeBounds(logical,settings.stage3Comparison?2.15:Math.Max(0,settings.wallHeight),.22);
            var anchor=data.VisualActorAnchor.Value;var p=projection.Project(anchor);var scale=Stage4CameraPreview.ActorScale(projection,anchor,settings.actorMode);
            return new ProjectionBounds(new Point2(p.X+(logical.Min.X-anchor.X)*scale.x,p.Y+(logical.Min.Y-anchor.Y)*scale.y),new Point2(p.X+(logical.Max.X-anchor.X)*scale.x,p.Y+(logical.Max.Y-anchor.Y)*scale.y));
        }
    }
    /// <summary>Optional Stage 4.5 backend. Logical vertex streams upload only after terrain/overlay edits or a backend switch.</summary>
    [RequireComponent(typeof(Stage4CameraPreview))]
    public sealed class Stage45GpuProjection:MonoBehaviour
    {
        public Shader projectionShader;
        public ComputeShader parityShader;
        public bool useGpu=true,showStationaryReferences;
        public int VisibleVertices {get;private set;}
        public int TerrainUploads {get;private set;}
        public int TotalUploads {get;private set;}
        public long UploadedVertices {get;private set;}
        public double UploadMs {get;private set;}
        public double ControlMs {get;private set;}
        public int ResourceCount=>materials.Count;
        public int BufferCount=>buffers.Count;
        sealed class Buffer
        {
            public readonly List<Vector3> positions=new List<Vector3>();
            public readonly List<Vector4> walls=new List<Vector4>(),roles=new List<Vector4>();
            public int generation=-1;public bool bound;public Texture texture;public FocusProjection boundsSnapshot;public float boundsHeight;public WallHeightPolicy boundsPolicy;public int boundsActorMode;
        }
        public bool cacheMaterialBindings;
        public int bufferPoolLimit;public int PooledBufferCount=>spareBuffers.Count;
        readonly Stack<Buffer> spareBuffers=new Stack<Buffer>();
        readonly Dictionary<MeshFilter,Buffer> buffers=new Dictionary<MeshFilter,Buffer>();
        readonly Dictionary<Material,Material> materials=new Dictionary<Material,Material>();
        readonly Dictionary<MeshRenderer,Material> originals=new Dictionary<MeshRenderer,Material>();
        MaterialPropertyBlock block;
        readonly List<MeshFilter> deadFilters=new List<MeshFilter>();
        readonly List<MeshRenderer> deadRenderers=new List<MeshRenderer>();
        Point2 renderOrigin;
        int generation;bool lastGpu,initialized;FocusProjection projection;Stage4Settings current;
        static readonly ProfilerMarker marker=new ProfilerMarker("SporeGobbo.Stage45.GpuControl"),uploadMarker=new ProfilerMarker("SporeGobbo.Stage45.LogicalUpload");
        public bool BeginFrame(FocusProjection snapshot,Stage4Settings settings,Point2 origin=default)
        {
            long start=System.Diagnostics.Stopwatch.GetTimestamp();VisibleVertices=TerrainUploads=TotalUploads=0;UploadedVertices=0;UploadMs=ControlMs=0;
            if(projectionShader==null)throw new InvalidOperationException("Assign the Stage 4.5 projection shader.");
            bool changed=!initialized||useGpu!=lastGpu;if(changed){generation++;lastGpu=useGpu;initialized=true;}
            deadFilters.Clear();foreach(var key in buffers.Keys)if(key==null)deadFilters.Add(key);foreach(var key in deadFilters)buffers.Remove(key);
            deadRenderers.Clear();foreach(var key in originals.Keys)if(key==null)deadRenderers.Add(key);foreach(var key in deadRenderers)originals.Remove(key);
            if(renderOrigin.X!=origin.X||renderOrigin.Y!=origin.Y){generation++;changed=true;}
            renderOrigin=origin;projection=snapshot;current=settings;
            if(cacheMaterialBindings){using(marker.Auto())ProjectionShaderContract.Apply(snapshot,settings,useGpu,null,origin);}
            else{
            var shaderProjection=origin.X==0&&origin.Y==0?snapshot:new FocusProjection(new Point2(snapshot.Focus.X-origin.X,snapshot.Focus.Y-origin.Y),snapshot.HalfWidth*2,snapshot.HalfHeight*2,snapshot.Distance,snapshot.Side,snapshot.Top,snapshot.Foreground,snapshot.Offset,false,snapshot.SideDistance,snapshot.TopDistance,snapshot.ForegroundDistance,snapshot.CurveBlend);
            using(marker.Auto())ProjectionShaderContract.Apply(shaderProjection,settings,useGpu);
            }
            ControlMs=Elapsed(start);return changed;
        }
        void MaterialFor(PreviewMeshData data,MeshFilter filter)
        {
            if(block==null)block=new MaterialPropertyBlock();var renderer=filter.GetComponent<MeshRenderer>();
            if(!originals.TryGetValue(renderer,out var original)){original=renderer.sharedMaterial;originals.Add(renderer,original);}
            if(!materials.TryGetValue(original,out var material)){material=new Material(original){shader=projectionShader,name=original.name+" Stage45",hideFlags=HideFlags.DontSave};materials.Add(original,material);}
            if(renderer.sharedMaterial!=material)renderer.sharedMaterial=material;
            renderer.GetPropertyBlock(block);block.SetFloat("_UseTexture",data.PreviewTexture!=null?1:0);
            if(data.PreviewTexture!=null)block.SetTexture("_MainTex",data.PreviewTexture);renderer.SetPropertyBlock(block);
        }
        public bool Draw(PreviewMeshData data,MeshFilter filter,ProjectionBounds logical,bool dirty)
        {
            long controlStart=System.Diagnostics.Stopwatch.GetTimestamp();using(marker.Auto())
            {
                VisibleVertices+=data.Vertices.Count;
                if(!buffers.TryGetValue(filter,out var buffer)){buffer=spareBuffers.Count>0?spareBuffers.Pop():new Buffer();buffer.generation=-1;buffer.bound=false;buffer.boundsSnapshot=null;buffers.Add(filter,buffer);}
                if(!cacheMaterialBindings||!buffer.bound||buffer.texture!=data.PreviewTexture||buffer.generation!=generation){MaterialFor(data,filter);buffer.bound=true;buffer.texture=data.PreviewTexture;}
                bool upload=dirty||buffer.generation!=generation;
                if(upload)
                {
                    long start=System.Diagnostics.Stopwatch.GetTimestamp();using(uploadMarker.Auto())using(GeometryTrace.Measure(GeometryPhase.MeshUpload))
                    {
                        buffer.positions.Clear();buffer.walls.Clear();buffer.roles.Clear();
                        for(int i=0;i<data.LogicalVertices.Count;i++)
                        {
                            var v=data.LogicalVertices[i];Point2 logicalPoint=v.Logical;Vector4 wall=Vector4.zero,role=Vector4.zero;
                            if(data.VisualActorAnchor.HasValue){var anchor=data.VisualActorAnchor.Value;logicalPoint=anchor;role=new Vector4(8,(float)(v.Logical.X-anchor.X),(float)(v.Logical.Y-anchor.Y),0);}
                            else if(v.WallSampleIndex>=0){var sample=data.WallSamples[v.WallSampleIndex];wall=new Vector4((float)sample.SolidNormal.X,(float)sample.SolidNormal.Y,(float)sample.BackWeight,(float)sample.FrontWeight);role.x=(int)v.Role;}
                            buffer.positions.Add(new Vector3((float)(logicalPoint.X-renderOrigin.X),(float)(logicalPoint.Y-renderOrigin.Y),0));buffer.walls.Add(wall);buffer.roles.Add(role);
                        }
                        var mesh=filter.sharedMesh;mesh.Clear();mesh.indexFormat=buffer.positions.Count>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16;
                        mesh.SetVertices(buffer.positions);mesh.SetUVs(0,buffer.walls);mesh.SetUVs(1,buffer.roles);
                        if(data.PreviewTexture!=null)mesh.SetUVs(2,data.TextureCoordinates);
                        mesh.SetTriangles(data.Indices,0,false);buffer.generation=generation;
                        TotalUploads++;UploadedVertices+=buffer.positions.Count;
                        if(data.Layer==PreviewLayer.Solid||data.Layer==PreviewLayer.Faces||data.Layer==PreviewLayer.Lips||data.Layer==PreviewLayer.Contours||data.Layer==PreviewLayer.Floor||data.Name=="Projected grid")TerrainUploads++;
                    }
                    UploadMs+=Elapsed(start);
                }
                if(!cacheMaterialBindings||dirty||!ReferenceEquals(buffer.boundsSnapshot,projection)||buffer.boundsHeight!=current.wallHeight||buffer.boundsPolicy!=current.heightPolicy||buffer.boundsActorMode!=current.actorMode)
                {
                var b=ProjectionShaderContract.Bounds(data,logical,projection,current);
                var bounds=new Bounds(new Vector3((float)((b.Min.X+b.Max.X)*.5),(float)(-(b.Min.Y+b.Max.Y)*.5),0),new Vector3((float)(b.Max.X-b.Min.X),(float)(b.Max.Y-b.Min.Y),.01f));bounds.Expand(.002f);filter.sharedMesh.bounds=bounds;
                    buffer.boundsSnapshot=projection;buffer.boundsHeight=current.wallHeight;buffer.boundsPolicy=current.heightPolicy;buffer.boundsActorMode=current.actorMode;
                }
                ControlMs+=Elapsed(controlStart);return upload;
            }
        }
        public void Forget(MeshFilter filter)
        {
            if(buffers.TryGetValue(filter,out var retained)&&spareBuffers.Count<bufferPoolLimit){retained.bound=false;retained.texture=null;retained.boundsSnapshot=null;retained.positions.Clear();retained.walls.Clear();retained.roles.Clear();spareBuffers.Push(retained);}buffers.Remove(filter);var renderer=filter.GetComponent<MeshRenderer>();
            if(originals.TryGetValue(renderer,out var original)){renderer.sharedMaterial=original;originals.Remove(renderer);}
        }
        public void CompleteCpu(PreviewMeshData data,MeshFilter filter,bool uploaded)
        {
            VisibleVertices+=data.Vertices.Count;
            if(uploaded&&data.PreviewTexture!=null)filter.sharedMesh.SetUVs(2,data.TextureCoordinates);
        }
        static double Elapsed(long start)=>(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        public void DrawControls()
        {
            GUILayout.Label("STAGE 4.5 — GPU + LOGICAL DEMOS");useGpu=GUILayout.Toolbar(useGpu?1:0,new[]{"CPU reference","GPU vertices"})==1;
            showStationaryReferences=GUILayout.Toggle(showStationaryReferences,"Also show Stage 4 stationary references");
            GUILayout.Label($"{VisibleVertices:N0} visible vertices · {TerrainUploads} terrain uploads\nGPU control {ControlMs:F3} ms · logical upload {UploadMs:F3} ms");
            var demo=GetComponent<Stage45MovingDemo>();if(demo!=null)demo.DrawControls();
        }
        void OnDestroy(){foreach(var pair in originals)if(pair.Key!=null)pair.Key.sharedMaterial=pair.Value;foreach(var material in materials.Values)if(material!=null){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}materials.Clear();buffers.Clear();spareBuffers.Clear();originals.Clear();}
    }
}
