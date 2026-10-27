using System;
using System.Collections.Generic;
using UnityEngine;
namespace SporeGobbo.Geometry.Preview
{
 /// <summary>Reusable region rendering only. No input, actors, campaign, simulation or persistence.</summary>
 [DefaultExecutionOrder(1100)] public sealed class WorldRegionPresentation:MonoBehaviour
 {
 static readonly Unity.Profiling.ProfilerMarker stage61RenderProfile = new Unity.Profiling.ProfilerMarker("Spore.Stage61.RenderRefresh");

  public Vector2 VisualOffset; // Render-only event displacement; pointer inversion uses the same Camera.
  public bool AllowPartialView;
  public bool ViewAvailable=>Snapshot!=null&&Stream!=null&&Stream.Visible.Count>0&&Stream.Resident.Count>0;
  public int ForegroundSortingOrder=60;
  public Material[] materials;public Camera previewCamera;public Shader projectionShader;
  public Stage4Settings settings;public bool showGrid,showContours=true,showWalls=true,showLips=true;
  public StreamingWorld World{get;private set;}public StreamingGeometry Stream{get;private set;}
  public FocusProjection Snapshot{get;private set;}public ProjectionBounds LogicalVisible{get;private set;}
  public Stage45GpuProjection Gpu{get;private set;}public int VertexCount{get;private set;}
  public double DisplayMs;public bool Ready=>Snapshot!=null&&Stream!=null&&Stream.Ready&&Stream.Visible.Count>0;
  UnityEngine.Object storyOwner;Transform storyTarget;Vector3 storyOffset;
  public Func<Vector3,Vector2> PhysicalToLogical;
  public float StoryZoom{get;private set;}=1;
  public bool CanFrameStory=>(AllowPartialView?ViewAvailable:Ready)&&PhysicalToLogical!=null&&storyOwner==null;
  public bool TryBeginStory(UnityEngine.Object owner,Transform target,Vector3 offset){if(owner==null||target==null||!CanFrameStory)return false;storyOwner=owner;storyTarget=target;storyOffset=offset;return true;}
  public void SetStoryZoom(UnityEngine.Object owner,float zoom){if(ReferenceEquals(storyOwner,owner))StoryZoom=Mathf.Clamp(zoom,.5f,1);}
  public void EndStory(UnityEngine.Object owner){if(!ReferenceEquals(storyOwner,owner))return;storyOwner=null;storyTarget=null;storyOffset=default;StoryZoom=1;}
  MeshFilter exteriorFilter;PreviewMeshData exteriorData;bool exteriorDirty=true;
  Point2 origin;Vector2 focus;double displayWidth,displayHeight;
  sealed class Presentation{public MeshFilter[] Filters=new MeshFilter[6];public PreviewMeshData[] Data=new PreviewMeshData[6];public readonly bool[] Dirty={true,true,true,true,true,true};}
  readonly Dictionary<DualTile,Presentation> presentations=new Dictionary<DualTile,Presentation>();
  readonly Stack<Presentation> presentationPool=new Stack<Presentation>();
  public void Initialize(StreamingWorld world,Material[] layers,Shader shader,Camera camera,Vector2 start,IOccupancySource presentationSource=null,bool fillOutside=false)
  {
   if(World!=null)throw new InvalidOperationException("Region renderer already bound");
   World=world;materials=layers;projectionShader=shader;previewCamera=camera;focus=start;
   settings=Stage49Preview.Settings(2);settings.anchor=.62f;
   origin=new Point2(world.Width*.5,world.Height*.5);Gpu=gameObject.AddComponent<Stage45GpuProjection>();
   Gpu.projectionShader=shader;Gpu.bufferPoolLimit=48;Gpu.cacheMaterialBindings=true;
   if(fillOutside){exteriorData=new PreviewMeshData("Protected exterior",PreviewLayer.Solid);double pad=512;exteriorData.Rectangle(-pad,-pad,world.Width+pad,0);exteriorData.Rectangle(-pad,world.Height,world.Width+pad,world.Height+pad);exteriorData.Rectangle(-pad,0,0,world.Height);exteriorData.Rectangle(world.Width,0,world.Width+pad,world.Height);exteriorFilter=Create("Protected exterior",PreviewLayer.Solid,1);}
   Stream=new StreamingGeometry(World,384,true,presentationSource);Stream.Activated=Activate;Stream.Evicted=Evict;
   Stream.Rebuilt=c=>{AppendRim(c);var p=presentations[c.Key];for(int i=0;i<4;i++)p.Dirty[i]=true;};
  }
  public static FocusProjection ReferenceSnapshot(Vector2 focus,Stage4Settings settings)
  {
   double width=settings.centerWidth/settings.coverage,height=width/(16.0/9),d=settings.distance;
   return new FocusProjection(new Point2(focus.x+3,focus.y),settings.centerWidth,settings.centerHeight,d,settings.side,settings.top,settings.foreground,new Point2(0,(settings.anchor-.5)*height),false,d*1.5f,d*.8f,d*FocusProjection.ForegroundDistanceRatio,.75f);
  }
  public void Refresh(Vector2 follow,float deltaTime)
  {
 using var _stage61Profile = stage61RenderProfile.Auto();

   if(World==null)return;long start=System.Diagnostics.Stopwatch.GetTimestamp();
   if(storyTarget!=null&&PhysicalToLogical!=null)follow=PhysicalToLogical(storyTarget.position+new Vector3(storyOffset.x,storyOffset.y,0));
   focus=Vector2.Lerp(focus,follow,settings.smoothing<=0?1:1-Mathf.Exp(-deltaTime/settings.smoothing));
   displayWidth=settings.centerWidth/settings.coverage;displayHeight=displayWidth/(16.0/9);Snapshot=ReferenceSnapshot(focus,settings);
   previewCamera.transform.position=new Vector3(VisualOffset.x,VisualOffset.y,-10);previewCamera.orthographicSize=(float)displayHeight*.5f*StoryZoom;previewCamera.aspect=16f/9;
   float aspect=Screen.height>0?(float)Screen.width/Screen.height:16f/9,vw=Mathf.Min(1,(16f/9)/aspect),vh=Mathf.Min(1,aspect/(16f/9));previewCamera.rect=new Rect((1-vw)*.5f,(1-vh)*.5f,vw,vh);
   Stream.Select(Snapshot,displayWidth,displayHeight,settings.wallHeight);Stream.Pump();LogicalVisible=StreamingVisibility.LogicalRegion(Snapshot,displayWidth,displayHeight,0);
   Gpu.BeginFrame(Snapshot,settings,origin);VertexCount=0;
   if(exteriorFilter!=null){exteriorFilter.GetComponent<MeshRenderer>().enabled=AllowPartialView?ViewAvailable:Ready;Gpu.Draw(exteriorData,exteriorFilter,new ProjectionBounds(new Point2(-512,-512),new Point2(World.Width+512,World.Height+512)),exteriorDirty);exteriorDirty=false;}
   foreach(var pair in presentations){var p=pair.Value;bool visible=Stream.Visible.Contains(pair.Key);for(int i=0;i<6;i++){
    bool show=visible&&(i!=1||showWalls)&&(i!=2||showLips)&&(i!=3||showContours)&&(i!=5||showGrid);p.Filters[i].GetComponent<MeshRenderer>().enabled=show;
    if(show){var bounds=StreamingCoordinates.Bounds(pair.Key,World.Width,World.Height);Gpu.Draw(p.Data[i],p.Filters[i],new ProjectionBounds(new Point2(bounds.Min.X-.51,bounds.Min.Y-.51),new Point2(bounds.Max.X+.02,bounds.Max.Y+.02)),p.Dirty[i]);p.Dirty[i]=false;VertexCount+=p.Data[i].Vertices.Count;}}}
   previewCamera.cullingMask=(AllowPartialView?ViewAvailable:Ready)?-1:0;DisplayMs=StreamingGeometry.Elapsed(start);
  }
  public Vector3 Project(Point2 logical){var p=Snapshot.Project(logical);return new Vector3((float)p.X,(float)-p.Y,0);}
MeshFilter Create(string name,PreviewLayer layer,int order)
        {
            var go=new GameObject(name){hideFlags=HideFlags.DontSave};go.transform.SetParent(transform,false);
            var filter=go.AddComponent<MeshFilter>();filter.sharedMesh=new Mesh{name=name,hideFlags=HideFlags.DontSave};
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=materials[(int)layer];renderer.sortingOrder=order;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;return filter;
        }
static void Tint(MeshFilter f,Color color){var block=new MaterialPropertyBlock();block.SetColor("_Color",color);f.GetComponent<MeshRenderer>().SetPropertyBlock(block);}
void Activate(StreamedChunk c)
        {
            AppendRim(c);var p=presentationPool.Count>0?presentationPool.Pop():new Presentation();for(int i=0;i<6;i++)p.Dirty[i]=true;for(int i=0;i<4;i++){p.Data[i]=c.Layers[i];if(p.Filters[i]==null)p.Filters[i]=Create($"Chunk {c.Key.X},{c.Key.Y} {p.Data[i].Layer}",p.Data[i].Layer,i==0?1:i==1?12:i==2?ForegroundSortingOrder:14);}
            var bounds=StreamingCoordinates.Bounds(c.Key,World.Width,World.Height);
            p.Data[4]=new PreviewMeshData("Resident floor",PreviewLayer.Floor);if(false){for(double fy=bounds.Min.Y;fy<bounds.Max.Y;fy+=1)for(double fx=bounds.Min.X;fx<bounds.Max.X;fx+=1)p.Data[4].Rectangle(fx,fy,Math.Min(fx+1,bounds.Max.X),Math.Min(fy+1,bounds.Max.Y));}else p.Data[4].Rectangle(bounds.Min.X,bounds.Min.Y,bounds.Max.X,bounds.Max.Y);if(p.Filters[4]==null)p.Filters[4]=Create("Resident floor",PreviewLayer.Floor,0);
            // Half-cell perimeter strip only at actual world edges; interior ownership remains the existing dual grid.
            var dirt=p.Data[0];int x=c.Key.X*16,y=c.Key.Y*16;
            
            p.Data[5]=new PreviewMeshData("Resident grid",PreviewLayer.Grid);
            for(int xx=x;xx<=Math.Min(x+16,World.Width);xx++)p.Data[5].Rectangle(xx-.008,bounds.Min.Y,xx+.008,bounds.Max.Y);
            for(int yy=y;yy<=Math.Min(y+16,World.Height);yy++)p.Data[5].Rectangle(bounds.Min.X,yy-.008,bounds.Max.X,yy+.008);
            if(p.Filters[5]==null)p.Filters[5]=Create("Resident grid",PreviewLayer.Grid,16);foreach(var f in p.Filters)f.gameObject.SetActive(true);presentations.Add(c.Key,p);
        }
void AppendRim(StreamedChunk c){var b=StreamingCoordinates.Bounds(c.Key,World.Width,World.Height);var d=c.Layers[0];if(c.Key.X==0)d.Rectangle(0,c.Key.Y==0?0:b.Min.Y,.5,b.Max.Y);if(c.Key.Y==0)d.Rectangle(b.Min.X,0,b.Max.X,.5);if(b.Max.X==World.Width)d.Rectangle(World.Width-.5,b.Min.Y,World.Width,b.Max.Y);if(b.Max.Y==World.Height)d.Rectangle(b.Min.X,World.Height-.5,b.Max.X,World.Height);}
void Release(MeshFilter f){if(f==null)return;Gpu.Forget(f);f.gameObject.SetActive(false);if(Application.isPlaying){Destroy(f.sharedMesh);Destroy(f.gameObject);}else{DestroyImmediate(f.sharedMesh);DestroyImmediate(f.gameObject);}}
void Evict(StreamedChunk c){if(presentations.TryGetValue(c.Key,out var p)){if(presentationPool.Count<8){foreach(var f in p.Filters){Gpu.Forget(f);f.sharedMesh.Clear();f.gameObject.SetActive(false);}presentationPool.Push(p);}else foreach(var filter in p.Filters)Release(filter);presentations.Remove(c.Key);}}void OnDestroy(){if(exteriorFilter!=null)Release(exteriorFilter);Stream?.Dispose();while(presentationPool.Count>0)foreach(var f in presentationPool.Pop().Filters)Release(f);}
 }
}
