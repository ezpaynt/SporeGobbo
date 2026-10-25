using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
namespace SporeGobbo.Geometry.Preview
{
    /// <summary>Isolated input/presentation host; authority, chunk residency and actor rules live outside this MonoBehaviour.</summary>
    [DefaultExecutionOrder(1100)] public sealed class Stage47Preview:MonoBehaviour
    {
        public bool stage48Optimized;public Shader terrainArtShader;public Texture2D[] terrainTextures;
        readonly Stack<Presentation> presentationPool=new Stack<Presentation>();public int PooledPresentations=>presentationPool.Count;
        public Material[] materials;public Camera previewCamera;
        public Stage4Settings settings=new Stage4Settings{coverage=.55f,side=.25f,top=.2f,foreground=1.9f};
        [NonSerialized] public float RightPanelWidth;
        [NonSerialized] public bool ExternalInputBlock,ExternalPresentationBlock;
        IOccupancySource previewPresentation;
        public void ReplacePreviewWorld(StreamingWorld next,IOccupancySource presentation=null){Initialize();if(next.Width!=worldSize||next.Height!=worldSize)throw new ArgumentException("Preview dimensions must match");Stream.Dispose();World=next;previewPresentation=presentation;CreateStream();}
        public void SetPreviewFocus(Vector2 next){focus=next;}
        public int worldSize=1024;public float footprint=1,speed=5,digRadius=2.2f;
        public DigBrush brush=DigBrush.ThreeWide;public bool showGrid,showContours=true,showWalls=true,showLips=true,showUI=true,automation;
        public StreamingWorld World {get;private set;}public StreamingGeometry Stream {get;private set;}
        public Vector2 Position {get;private set;}public Vector2 Aim=Vector2.right;
        public FocusProjection Snapshot {get;private set;}public ProjectionBounds LogicalVisible {get;private set;}
        public readonly List<StreamingActor> Actors=new List<StreamingActor>();
        public int ActiveActors {get;private set;}public int VisibleDormant {get;private set;}public int PresentedActors {get;private set;}
        public int SimulationChunks {get;private set;}
        public double CreationMs,DisplayMs;public int MeshCount=>(presentations.Count+presentationPool.Count)*6+3;public int ObjectCount=>MeshCount;
        public int VertexCount {get;private set;}public bool Ready=>Snapshot!=null&&Stream!=null&&Stream.Visible.Count>0&&Stream.Ready;
        public Stage45GpuProjection Gpu {get;private set;}
        readonly Dictionary<DualTile,Presentation> presentations=new Dictionary<DualTile,Presentation>();
        readonly List<DualTile> brushCells=new List<DualTile>();readonly HashSet<DualTile> simulationChunks=new HashSet<DualTile>();
        sealed class Presentation{public MeshFilter[] Filters=new MeshFilter[6];public PreviewMeshData[] Data=new PreviewMeshData[6];public readonly bool[] Dirty={true,true,true,true,true,true};}
        readonly PreviewMeshData player=new PreviewMeshData("Large-world Gobbo",PreviewLayer.Gobbo),active=new PreviewMeshData("Active actors",PreviewLayer.Gobbo),dormant=new PreviewMeshData("Dormant actors",PreviewLayer.Gobbo);
        MeshFilter playerFilter,activeFilter,dormantFilter;Point2 origin;float cooldown;double displayWidth,displayHeight;Vector2 focus;bool initialized;
        Stage49Preview composition;
        string message="";Vector2 scroll;
        public string SnapshotPath=>Path.Combine(Application.persistentDataPath,"Stage47Experiment","excavation.sg47");
        void Start()=>Initialize();
        public void Initialize()
        {
            if(initialized)return;initialized=true;composition=GetComponent<Stage49Preview>();Gpu=GetComponent<Stage45GpuProjection>();
            var args=Environment.GetCommandLineArgs();int sizeArg=Array.IndexOf(args,"-stage48WorldSize");if(sizeArg>=0)worldSize=int.Parse(args[sizeArg+1]);if(Array.IndexOf(args,"-stage48Reference")>=0)stage48Optimized=false;
            if(stage48Optimized){Gpu.bufferPoolLimit=48;Gpu.cacheMaterialBindings=true;}
            if(terrainArtShader!=null)Gpu.projectionShader=terrainArtShader;
            long start=System.Diagnostics.Stopwatch.GetTimestamp();World=new StreamingWorld(worldSize,worldSize);CreationMs=StreamingGeometry.Elapsed(start);
            origin=new Point2(worldSize*.5,worldSize*.5);Position=focus=new Vector2(worldSize*.5f+.5f,worldSize*.5f+.5f);CreateStream();
            playerFilter=Create("Gobbo and brush",PreviewLayer.Gobbo,25);activeFilter=Create("Active logical actors",PreviewLayer.Gobbo,24);dormantFilter=Create("Dormant logical actors",PreviewLayer.Gobbo,24);
            Tint(playerFilter,new Color(.42f,1,.3f));Tint(activeFilter,new Color(1,.65f,.15f));Tint(dormantFilter,new Color(.35f,.7f,1));
            for(int y=32;y<worldSize;y+=64)for(int x=32;x<worldSize&&(!stage48Optimized||Actors.Count<96);x+=64)if(!World.IsSolid(x,y))Actors.Add(new StreamingActor(new Point2(x+.5,y+.5)));
            // Known visible-but-dormant references along the traversable axis.
            for(int x=16;x<worldSize;x+=stage48Optimized?Math.Max(32,worldSize/32):32)Actors.Add(new StreamingActor(new Point2(x+.5,worldSize*.5+.5)));
        }
        void CreateStream(){Stream=new StreamingGeometry(World,384,stage48Optimized,previewPresentation);Stream.SurfaceCellGrid=terrainArtShader!=null;Stream.Activated=Activate;Stream.Evicted=Evict;Stream.Rebuilt=c=>{AppendRim(c);var p=presentations[c.Key];for(int i=0;i<4;i++)p.Dirty[i]=true;};}
        MeshFilter Create(string name,PreviewLayer layer,int order)
        {
            var go=new GameObject(name){hideFlags=HideFlags.DontSave};go.transform.SetParent(transform,false);
            var filter=go.AddComponent<MeshFilter>();filter.sharedMesh=new Mesh{name=name,hideFlags=HideFlags.DontSave};
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=materials[(int)layer];renderer.sortingOrder=order;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;return filter;
        }
        static void Tint(MeshFilter f,Color color){var block=new MaterialPropertyBlock();block.SetColor("_Color",color);f.GetComponent<MeshRenderer>().SetPropertyBlock(block);}
        void Activate(StreamedChunk c)
        {
            AppendRim(c);var p=stage48Optimized&&presentationPool.Count>0?presentationPool.Pop():new Presentation();for(int i=0;i<6;i++)p.Dirty[i]=true;for(int i=0;i<4;i++){p.Data[i]=c.Layers[i];if(p.Filters[i]==null)p.Filters[i]=Create($"Chunk {c.Key.X},{c.Key.Y} {p.Data[i].Layer}",p.Data[i].Layer,i==0?1:10+i);}
            var bounds=StreamingCoordinates.Bounds(c.Key,World.Width,World.Height);
            p.Data[4]=new PreviewMeshData("Resident floor",PreviewLayer.Floor);if(terrainArtShader!=null){for(double fy=bounds.Min.Y;fy<bounds.Max.Y;fy+=1)for(double fx=bounds.Min.X;fx<bounds.Max.X;fx+=1)p.Data[4].Rectangle(fx,fy,Math.Min(fx+1,bounds.Max.X),Math.Min(fy+1,bounds.Max.Y));}else p.Data[4].Rectangle(bounds.Min.X,bounds.Min.Y,bounds.Max.X,bounds.Max.Y);if(p.Filters[4]==null)p.Filters[4]=Create("Resident floor",PreviewLayer.Floor,0);
            // Half-cell perimeter strip only at actual world edges; interior ownership remains the existing dual grid.
            var dirt=p.Data[0];int x=c.Key.X*16,y=c.Key.Y*16;
            
            p.Data[5]=new PreviewMeshData("Resident grid",PreviewLayer.Grid);
            for(int xx=x;xx<=Math.Min(x+16,World.Width);xx++)p.Data[5].Rectangle(xx-.008,bounds.Min.Y,xx+.008,bounds.Max.Y);
            for(int yy=y;yy<=Math.Min(y+16,World.Height);yy++)p.Data[5].Rectangle(bounds.Min.X,yy-.008,bounds.Max.X,yy+.008);
            if(p.Filters[5]==null)p.Filters[5]=Create("Resident grid",PreviewLayer.Grid,16);foreach(var f in p.Filters)f.gameObject.SetActive(true);presentations.Add(c.Key,p);
        }
        void AppendRim(StreamedChunk c){var b=StreamingCoordinates.Bounds(c.Key,World.Width,World.Height);var d=c.Layers[0];if(c.Key.X==0)d.Rectangle(0,c.Key.Y==0?0:b.Min.Y,.5,b.Max.Y);if(c.Key.Y==0)d.Rectangle(b.Min.X,0,b.Max.X,.5);if(b.Max.X==World.Width)d.Rectangle(World.Width-.5,b.Min.Y,World.Width,b.Max.Y);if(b.Max.Y==World.Height)d.Rectangle(b.Min.X,World.Height-.5,b.Max.X,World.Height);}
        void Release(MeshFilter f){if(f==null)return;Gpu.Forget(f);f.gameObject.SetActive(false);if(Application.isPlaying){Destroy(f.sharedMesh);Destroy(f.gameObject);}else{DestroyImmediate(f.sharedMesh);DestroyImmediate(f.gameObject);}}
        void Evict(StreamedChunk c){if(presentations.TryGetValue(c.Key,out var p)){if(stage48Optimized&&presentationPool.Count<8){foreach(var f in p.Filters){Gpu.Forget(f);f.sharedMesh.Clear();f.gameObject.SetActive(false);}presentationPool.Push(p);}else foreach(var filter in p.Filters)Release(filter);presentations.Remove(c.Key);}}
        public void Teleport(Vector2 target)
        {Initialize();target.x=Mathf.Clamp(target.x,5,World.Width-6);target.y=Mathf.Clamp(target.y,5,World.Height-6);if(!PreviewMotion.Fits(World,target,footprint)){message="Teleport destination is solid; use the open center axes.";return;}Position=focus=target;}
        public int DigAt(Vector2 target){PreviewDigBrush.Cells(target,brush,digRadius,World,brushCells);return Stream.Dig(brushCells);}
        public void Step(Vector2 movement,Vector2 aim,bool dig,float dt)
        {
            if(aim.sqrMagnitude>.001f)Aim=aim.normalized;cooldown-=dt;
            if(dig&&cooldown<=0){DigAt(Position+Aim*(footprint*.5f+.55f));cooldown=.1f;}
            if(movement.sqrMagnitude>0)Position=PreviewMotion.Move(World,Position,Vector2.ClampMagnitude(movement,1)*speed*dt,footprint);
        }
        void Update()
        {
            if(automation||ExternalInputBlock)return;var k=Keyboard.current;var m=Mouse.current;
            if(k!=null&&k.tabKey.wasPressedThisFrame)showUI=!showUI;
            Vector2 movement=Vector2.zero;if(k!=null){movement.x=(k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0);movement.y=(k.sKey.isPressed?1:0)-(k.wKey.isPressed?1:0);}
            bool over=m!=null&&((showUI&&m.position.ReadValue().x<355)||(RightPanelWidth>0&&m.position.ReadValue().x>Screen.width-RightPanelWidth));
            Vector2 aim=Aim;if(m!=null&&Snapshot!=null&&!over){var w=previewCamera.ScreenToWorldPoint(new Vector3(m.position.ReadValue().x,m.position.ReadValue().y,10));var p=Snapshot.Unproject(new Point2(w.x,-w.y));aim=new Vector2((float)p.X,(float)p.Y)-Position;}
            if(Ready)Step(movement,aim,!over&&((m!=null&&m.leftButton.isPressed)||(k!=null&&k.spaceKey.isPressed)),Time.deltaTime);
        }
        void LateUpdate()=>Refresh(automation?1f/60:Time.deltaTime);
        public void Refresh(float dt)
        {
            Initialize();long start=System.Diagnostics.Stopwatch.GetTimestamp();
            if(composition==null||!composition.freezeFocus)focus=Vector2.Lerp(focus,Position,settings.smoothing<=0?1:1-Mathf.Exp(-dt/settings.smoothing));
            displayWidth=Math.Max(8,settings.centerWidth)/Mathf.Clamp(settings.coverage,.35f,.85f);displayHeight=displayWidth/(16.0/9);
            if(composition!=null){composition.Constrain();displayWidth=composition.DisplayWidth;displayHeight=displayWidth/(16.0/9);Snapshot=composition.Map(focus,Snapshot);}
            else if(!stage48Optimized||Snapshot==null||Snapshot.Focus.X!=focus.x||Snapshot.Focus.Y!=focus.y||Snapshot.HalfWidth!=Math.Max(4,settings.centerWidth)*.5||Snapshot.HalfHeight!=Math.Max(4,settings.centerHeight)*.5||Snapshot.Side!=(settings.uniform?1:Math.Max(.15,settings.side))||Snapshot.Top!=(settings.uniform?1:Math.Max(.1,settings.top))||Snapshot.Foreground!=(settings.uniform?1:Math.Max(.1,settings.foreground))||Snapshot.Distance!=Math.Max(1,settings.distance)||Snapshot.Offset.Y!=(settings.anchor-.5)*displayHeight)Snapshot=new FocusProjection(new Point2(focus.x,focus.y),Math.Max(4,settings.centerWidth),Math.Max(4,settings.centerHeight),Math.Max(1,settings.distance),Math.Max(.15,settings.side),Math.Max(.1,settings.top),Math.Max(.1,settings.foreground),new Point2(0,(settings.anchor-.5)*displayHeight),settings.uniform);
            previewCamera.transform.position=new Vector3(0,0,-10);previewCamera.orthographicSize=(float)displayHeight*.5f;previewCamera.aspect=16f/9;
            float aspect=Screen.height>0?(float)Screen.width/Screen.height:16f/9,vw=Mathf.Min(1,(16f/9)/aspect),vh=Mathf.Min(1,aspect/(16f/9));previewCamera.rect=new Rect((1-vw)*.5f,(1-vh)*.5f,vw,vh);
            Stream.Select(Snapshot,displayWidth,displayHeight,settings.wallHeight);Stream.Pump();
            LogicalVisible=StreamingVisibility.LogicalRegion(Snapshot,displayWidth,displayHeight,0);
            Gpu.BeginFrame(Snapshot,settings,origin);VertexCount=0;
            foreach(var pair in presentations)
            {
                var p=pair.Value;bool visible=Stream.Visible.Contains(pair.Key);
                for(int i=0;i<6;i++)
                {
                    bool show=visible&&(i!=1||showWalls)&&(i!=2||showLips)&&(i!=3||showContours)&&(i!=5||showGrid);p.Filters[i].GetComponent<MeshRenderer>().enabled=show;
                    if(show){ApplyArt(p.Data[i],p.Filters[i],p.Dirty[i]);var cellBounds=StreamingCoordinates.Bounds(pair.Key,World.Width,World.Height);var renderBounds=new ProjectionBounds(new Point2(cellBounds.Min.X-.51,cellBounds.Min.Y-.51),new Point2(cellBounds.Max.X+.02,cellBounds.Max.Y+.02));Gpu.Draw(p.Data[i],p.Filters[i],renderBounds,p.Dirty[i]);p.Dirty[i]=false;VertexCount+=p.Data[i].Vertices.Count;}
                }
                // Hidden layers must upload current data when next enabled.
                
            }
            DrawActors(composition!=null&&composition.freezeActors?0:dt);DrawPlayer();previewCamera.cullingMask=Ready&&!ExternalPresentationBlock?-1:0;
            DisplayMs=StreamingGeometry.Elapsed(start);
        }
        void ApplyArt(PreviewMeshData data,MeshFilter filter,bool dirty)
        {
            if(terrainArtShader==null||terrainTextures==null||terrainTextures.Length<5)return;
            int index=data.Layer==PreviewLayer.Floor?0:data.Layer==PreviewLayer.Solid?1:data.Layer==PreviewLayer.Faces?2:data.Layer==PreviewLayer.Contours?3:data.Layer==PreviewLayer.Lips?4:-1;
            if(index<0)return;data.PreviewTexture=terrainTextures[index];
            if(dirty){data.TextureCoordinates.Clear();for(int i=0;i<data.LogicalVertices.Count;i++){var v=data.LogicalVertices[i];var p=v.Logical;double u=p.X/4.0,w=p.Y/4.0;if(v.WallSampleIndex>=0){var sample=data.WallSamples[v.WallSampleIndex];u=(p.X+p.Y)/4.0;w=v.Role==GeometryVertexRole.WallBottom?1:0;if(data.Layer==PreviewLayer.Lips)w=v.Role==GeometryVertexRole.ForegroundLip?1:0;}data.TextureCoordinates.Add(new Vector2((float)u,(float)w));}}
        }
        void DrawActors(double dt)
        {
            active.Clear();dormant.Clear();ActiveActors=VisibleDormant=PresentedActors=0;simulationChunks.Clear();
            foreach(var actor in Actors)
            {
                actor.Step(new Point2(Position.x,Position.y),LogicalVisible,dt,World);
                if(actor.Active){ActiveActors++;simulationChunks.Add(StreamingCoordinates.Owner((int)actor.Position.X,(int)actor.Position.Y));}
                if(!actor.Visible)continue;PresentedActors++;if(!actor.Active)VisibleDormant++;
                // Grounded lightweight placeholder, never a production actor prefab.
                var p=actor.Position;(actor.Active?active:dormant).Rectangle(p.X-.35,p.Y-.6,p.X+.35,p.Y+.1);
            }
            SimulationChunks=simulationChunks.Count;
            var bounds=new ProjectionBounds(new Point2(0,0),new Point2(World.Width,World.Height));Gpu.Draw(active,activeFilter,bounds,true);Gpu.Draw(dormant,dormantFilter,bounds,true);
        }
        void DrawPlayer()
        {
            player.Clear();double x=Position.x,y=Position.y,h=footprint*.5;
            Ring(player,x-h,y-h,x+h,y+h,.045);player.Rectangle(x-.28,y-.55,x+.28,y+.15);
            PreviewDigBrush.Cells(Position+Aim*(footprint*.5f+.55f),brush,digRadius,World,brushCells);
            foreach(var c in brushCells)Ring(player,c.X,c.Y,c.X+1,c.Y+1,.025);
            Gpu.Draw(player,playerFilter,new ProjectionBounds(new Point2(x-16,y-16),new Point2(x+16,y+16)),true);
        }
        static void Ring(PreviewMeshData d,double x,double y,double xx,double yy,double w){d.Rectangle(x,y,xx,y+w);d.Rectangle(x,yy-w,xx,yy);d.Rectangle(x,y+w,x+w,yy-w);d.Rectangle(xx-w,y+w,xx,yy-w);}
        public void LoadOriginalForStage49(){Stream.Dispose();previewPresentation=null;World=new StreamingWorld(worldSize,worldSize);CreateStream();}
        public void Save(){Directory.CreateDirectory(Path.GetDirectoryName(SnapshotPath));using(var s=File.Create(SnapshotPath))World.Save(s);message="Saved isolated excavation delta snapshot";}
        public void Load(){using(var s=File.OpenRead(SnapshotPath)){var next=StreamingWorld.Load(s);Stream.Dispose();World=next;previewPresentation=null;CreateStream();message="Reloaded excavation snapshot";}}
        void OnGUI()
        {
            if(!initialized)return;
            if(!Ready){GUI.Box(new Rect(Screen.width*.5f-240,Screen.height*.5f-50,480,100),Stream.OverBudget?"Viewport exceeds 384-chunk safety cap. Increase compression scales or coverage.":$"Loading actual terrain: {Stream.Resident.Count} resident, {Stream.Pending} queued\nMovement resumes when the expanded viewport is complete.");}
            if(!showUI||composition!=null)return;GUILayout.BeginArea(new Rect(8,8,340,Screen.height-16),GUI.skin.box);scroll=GUILayout.BeginScrollView(scroll);
            GUILayout.Label(terrainArtShader!=null?"STAGE 4.8 — TERRAIN TEXTURE PROOF":stage48Optimized?"STAGE 4.8 — OPTIMIZED WORLD":"STAGE 4.7 — LARGE WORLD (isolated)");GUILayout.Label($"{World.Width}×{World.Height} · seed {World.Seed}\nGobbo {Position.x:F1}, {Position.y:F1} · edits {World.EditCount}\nResident {Stream.Resident.Count}/{Stream.Capacity} · visible {Stream.Visible.Count}\nQueue {Stream.Pending} · meshes/objects {MeshCount}\nCache tiles {Stream.CacheTiles:N0} · normals {Stream.CacheSamples:N0}\nActive actors {ActiveActors} · visible dormant {VisibleDormant}\nPresented {PresentedActors} · unloaded {Actors.Count-PresentedActors}\nSimulation chunks {SimulationChunks}\nBuild {Stream.BuildMs:F2} ms · unload {Stream.UnloadMs:F2} ms\nDig {Stream.DigMs:F2} ms / {Stream.LastDigRebuilds} chunks\nDisplay {DisplayMs:F2} ms · vertices {VertexCount:N0}");
            GUILayout.Label("WASD · mouse aim · LMB/Space dig · Tab UI");
            int body=GUILayout.Toolbar(footprint==1?0:footprint==3?1:2,new[]{"1×1","3×3","9×9"});float nextBody=new[]{1,3,9}[body];if(PreviewMotion.Fits(World,Position,nextBody))footprint=nextBody;
            brush=(DigBrush)GUILayout.Toolbar((int)brush,new[]{"Single","3-wide","Radius"});digRadius=Slider("Brush radius",digRadius,.5f,8);
            settings.uniform=GUILayout.Toggle(settings.uniform,"Uniform comparison");GUILayout.BeginHorizontal();if(GUILayout.Button("Gentle"))Preset(0);if(GUILayout.Button("Balanced"))Preset(1);if(GUILayout.Button("Strong"))Preset(2);GUILayout.EndHorizontal();
            settings.centerWidth=Slider("Center width",settings.centerWidth,24,64);settings.centerHeight=Slider("Center height",settings.centerHeight,12,40);settings.coverage=Slider("Coverage",settings.coverage,.35f,.85f);settings.side=Slider("Side scale",settings.side,.15f,1);settings.top=Slider("Top scale",settings.top,.1f,1);settings.foreground=Slider("Foreground",settings.foreground,.5f,2.5f);settings.distance=Slider("Transition",settings.distance,4,40);settings.anchor=Slider("Anchor",settings.anchor,.3f,.7f);settings.wallHeight=Slider("Wall height",settings.wallHeight,0,4);settings.smoothing=Slider("Follow smoothing",settings.smoothing,0,.5f);
            settings.heightPolicy=(WallHeightPolicy)GUILayout.Toolbar((int)settings.heightPolicy,new[]{"Static wall rule","Continuous walls"});
            showGrid=GUILayout.Toggle(showGrid,"Grid");showContours=GUILayout.Toggle(showContours,"Contours");showWalls=GUILayout.Toggle(showWalls,"Wall faces");showLips=GUILayout.Toggle(showLips,"Foreground lips");
            GUILayout.BeginHorizontal();if(GUILayout.Button("West"))Teleport(new Vector2(24.5f,World.Height/2+.5f));if(GUILayout.Button("Center"))Teleport(new Vector2(World.Width/2+.5f,World.Height/2+.5f));if(GUILayout.Button("East"))Teleport(new Vector2(World.Width-24.5f,World.Height/2+.5f));GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();if(GUILayout.Button("Save delta"))Save();if(GUILayout.Button("Reload delta")&&File.Exists(SnapshotPath))Load();GUILayout.EndHorizontal();GUILayout.Label(message);GUILayout.EndScrollView();GUILayout.EndArea();
        }
        static float Slider(string label,float value,float min,float max){GUILayout.Label($"{label}: {value:F2}");return GUILayout.HorizontalSlider(value,min,max);}
        public void Preset(int i){settings.uniform=false;settings.side=new[]{.7f,.45f,.25f}[i];settings.top=new[]{.6f,.35f,.2f}[i];settings.foreground=new[]{1.2f,1.5f,1.9f}[i];}
        void OnDestroy(){if(Stream!=null)Stream.Dispose();while(presentationPool.Count>0)foreach(var f in presentationPool.Pop().Filters)Release(f);Release(playerFilter);Release(activeFilter);Release(dormantFilter);}
    }
}





