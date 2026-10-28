using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Unity.Profiling;

namespace SporeGobbo.Geometry.Preview
{
    [Serializable] public sealed class Stage4Settings
    {
        public float centerWidth=48,centerHeight=28,coverage=.62f,side=.45f,top=.35f,foreground=1.5f,distance=24,anchor=.58f,smoothing=.15f,wallHeight=2.15f;
        public bool uniform,stage3Comparison;
        public WallHeightPolicy heightPolicy=WallHeightPolicy.ProjectLogicalExtrusion;
        public int actorMode;
        public Stage4Settings Copy()=>JsonUtility.FromJson<Stage4Settings>(JsonUtility.ToJson(this));
    }
    /// <summary>Finite-fixture display experiment only. Never queries terrain during camera reprojection.</summary>
    [DefaultExecutionOrder(1000),RequireComponent(typeof(PlayableCavePreview))]
    public sealed class Stage4CameraPreview : MonoBehaviour
    {
        public Stage4Settings settings=new Stage4Settings();
        public FocusProjection Snapshot {get;private set;}
        public ProjectionBounds VisibleLogical {get;private set;}
        public Vector2 Focus {get;private set;}
        public int ProjectedVertices {get;private set;}
        public int ActivePatches {get;private set;}
        public int Uploads {get;private set;}
        public double ProjectionMs {get;private set;}
        public double UploadMs {get;private set;}
        public int EntryCount=>entries.Count;
        public double DisplayFrameMs {get;private set;}
        Stage45GpuProjection gpu;
        public bool FreezeCamera;
        public bool ShowDebug=true;
        public bool EditingPresetName {get;private set;}
        public Vector2? ExperimentFocus;
        static readonly ProfilerMarker projectionMarker=new ProfilerMarker("SporeGobbo.Stage4.Projection");
        static readonly ProfilerMarker uploadMarker=new ProfilerMarker("SporeGobbo.Stage4.Upload");
        sealed class Entry
        {
            public PreviewMeshData data;public MeshFilter filter;public MeshRenderer renderer;
            public readonly List<Vector3> vertices=new List<Vector3>();public WallDisplayVertex[] walls=Array.Empty<WallDisplayVertex>();
            public readonly List<Vector3> sourceVertices=new List<Vector3>();public readonly List<int> sourceIndices=new List<int>();
            public ProjectionBounds logical;public bool dirty=true,hasBounds;public int version=-1;
        }
        readonly Dictionary<MeshFilter,Entry> byFilter=new Dictionary<MeshFilter,Entry>();
        readonly List<Entry> entries=new List<Entry>();
        readonly List<GameObject> owned=new List<GameObject>();
        readonly List<Mesh> ownedMeshes=new List<Mesh>();
        readonly PreviewMeshData references=new PreviewMeshData("Reference actors",PreviewLayer.Gobbo);
        readonly Point2[] referencePoints={new Point2(55.5,39.5),new Point2(54.5,18.5),new Point2(90.5,39.5),new Point2(46.5,55.5),new Point2(43.5,25.5)};
        public IReadOnlyList<Point2> ReferencePoints=>referencePoints;
        MeshFilter referenceFilter;
        PlayableCavePreview live;CaveGeometryPreview display;Camera renderCamera;
        Stage4Settings previous;Vector2 lastFocus;float lastAspect;bool ready;
        Vector2 scroll;string presetName="My experiment",message="";
        float panelHeight;int version;
        readonly List<MeshRenderer> baked=new List<MeshRenderer>();
        public bool PointerOverPanel(Vector2 p)=>ShowDebug&&p.x<322&&p.y<Screen.height-306;
        public Vector2 InverseRender(Vector3 render)
        {var p=Snapshot==null?new Point2(render.x,-render.y):Snapshot.Unproject(new Point2(render.x,-render.y));return new Vector2((float)p.X,(float)p.Y);}
        public void RegisterUpload(PreviewMeshData data,MeshFilter filter)
        {
            if(!byFilter.TryGetValue(filter,out var entry))
            {entry=new Entry{data=data,filter=filter,renderer=filter.GetComponent<MeshRenderer>()};byFilter.Add(filter,entry);entries.Add(entry);}
            if(data.Layer==PreviewLayer.Gobbo||data.Layer==PreviewLayer.Grid)
            {
                bool same=entry.sourceVertices.Count==data.Vertices.Count&&entry.sourceIndices.Count==data.Indices.Count;
                if(same)for(int i=0;i<data.Vertices.Count;i++)if(entry.sourceVertices[i]!=data.Vertices[i]){same=false;break;}
                if(same)for(int i=0;i<data.Indices.Count;i++)if(entry.sourceIndices[i]!=data.Indices[i]){same=false;break;}
                if(same&&entry.hasBounds)return;
                entry.sourceVertices.Clear();entry.sourceVertices.AddRange(data.Vertices);entry.sourceIndices.Clear();entry.sourceIndices.AddRange(data.Indices);
            }
            entry.data=data;entry.dirty=true;entry.hasBounds=false;
            // Initial pool construction can precede the first camera snapshot.
            if(Snapshot==null)data.ApplyTo(filter.sharedMesh);
        }
        void Initialize()
        {
            if(ready)return;live=GetComponent<PlayableCavePreview>();display=GetComponent<CaveGeometryPreview>();renderCamera=display.previewCamera;
            live.Initialize();Focus=live.Position;gpu=GetComponent<Stage45GpuProjection>();
            foreach(var filter in GetComponentsInChildren<MeshFilter>())
                if(filter.name=="Floor"||filter.name=="Grid"){var renderer=filter.GetComponent<MeshRenderer>();renderer.enabled=false;baked.Add(renderer);}
            var floor=new PreviewMeshData("Projected floor",PreviewLayer.Floor);floor.Rectangle(0,0,live.Session.Terrain.Width,live.Session.Terrain.Height);Create(floor,0);
            var grid=new PreviewMeshData("Projected grid",PreviewLayer.Grid);
            int width=live.Session.Terrain.Width,height=live.Session.Terrain.Height;
            for(int x=0;x<=width;x++)grid.Rectangle(x-.012,0,x+.012,height);
            for(int y=0;y<=height;y++)grid.Rectangle(0,y-.012,width,y+.012);
            Create(grid,14);referenceFilter=Create(references,21);
            ready=true;
        }
        MeshFilter Create(PreviewMeshData data,int order)
        {
            var go=new GameObject(data.Name){hideFlags=HideFlags.DontSave};go.transform.SetParent(transform,false);owned.Add(go);
            var mesh=new Mesh{name=data.Name,hideFlags=HideFlags.DontSave};mesh.MarkDynamic();ownedMeshes.Add(mesh);
            var filter=go.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=live.layerMaterials[(int)data.Layer];renderer.sortingOrder=order;
            RegisterUpload(data,filter);return filter;
        }
        void LateUpdate(){if(Application.isPlaying){if(UnityEngine.InputSystem.Keyboard.current!=null&&UnityEngine.InputSystem.Keyboard.current.tabKey.wasPressedThisFrame)ShowDebug=!ShowDebug;Refresh(Time.deltaTime);}}
        public void Refresh(float dt)
        {
            long displayStart=System.Diagnostics.Stopwatch.GetTimestamp();
            Initialize();ProjectedVertices=ActivePatches=Uploads=0;ProjectionMs=UploadMs=0;
            Vector2 wanted=ExperimentFocus??live.Position;
            if(!FreezeCamera&&live.followPlayer)Focus=Vector2.Lerp(Focus,wanted,settings.smoothing<=0?1:1-Mathf.Exp(-Mathf.Max(0,dt)/settings.smoothing));
            // One isotropic display scale. Coverage is horizontal; vertical coverage is derived from aspect.
            float aspect=16f/9;
            float windowAspect=Screen.height>0?Screen.width/(float)Screen.height:aspect;
            float vw=Mathf.Min(1,aspect/windowAspect),vh=Mathf.Min(1,windowAspect/aspect);
            renderCamera.rect=new Rect((1-vw)*.5f,(1-vh)*.5f,vw,vh);renderCamera.aspect=aspect;
            float displayWidth=settings.stage3Comparison?Mathf.Max(10,live.footprint*1.6f)*2*aspect:Mathf.Max(8,settings.centerWidth)/Mathf.Clamp(settings.coverage,.25f,.85f);
            float displayHeight=displayWidth/aspect;
            bool changed=Snapshot==null||Focus!=lastFocus||lastAspect!=aspect||!Same(previous,settings);
            if(changed)
            {
                version++;
                Snapshot=new FocusProjection(new Point2(Focus.x,Focus.y),Math.Max(4,settings.centerWidth),Math.Max(4,settings.centerHeight),Math.Max(1,settings.distance),Math.Max(.05,settings.side),Math.Max(.05,settings.top),Math.Max(.05,settings.foreground),new Point2(0,settings.stage3Comparison?0:(settings.anchor-.5)*displayHeight),settings.uniform||settings.stage3Comparison);
                renderCamera.transform.position=new Vector3(0,0,-10);renderCamera.orthographicSize=displayHeight*.5f;
                lastFocus=Focus;lastAspect=aspect;previous=CopyValues(settings,previous);
            }
            renderCamera.transform.position=new Vector3(0,0,-10);renderCamera.orthographicSize=displayHeight*.5f;foreach(var renderer in baked)if(renderer!=null)renderer.enabled=false;
            var a=Snapshot.Unproject(new Point2(-displayWidth*.5,-displayHeight*.5));var b=Snapshot.Unproject(new Point2(displayWidth*.5,displayHeight*.5));VisibleLogical=new ProjectionBounds(a,b);
            if(gpu!=null&&gpu.BeginFrame(Snapshot,settings))version++;
            if((gpu==null||gpu.showStationaryReferences)&&(changed||references.Vertices.Count==0)){BuildReferences();RegisterUpload(references,referenceFilter);}
            for(int i=entries.Count-1;i>=0;i--)
            {
                var e=entries[i];if(e.filter==null){byFilter.Remove(e.filter);entries.RemoveAt(i);continue;}
                if(!e.hasBounds)CacheBounds(e);
                bool visible=Visible(e,displayWidth,displayHeight)&&LayerVisible(e.data.Layer)&&(gpu==null||gpu.showStationaryReferences||e.data!=references);
                e.renderer.enabled=visible;
                if(visible)
                {
                    if(e.data.Layer==PreviewLayer.Solid&&e.data.Name!="Edge"&&e.data.Vertices.Count>0)ActivePatches++;
                    if(gpu!=null&&gpu.useGpu){if(gpu.Draw(e.data,e.filter,e.logical,e.dirty))Uploads++;e.dirty=false;e.version=version;}
                    else{bool project=e.version!=version||e.dirty;if(project)Project(e);if(gpu!=null)gpu.CompleteCpu(e.data,e.filter,project);}
                }
            }
            if(gpu!=null&&gpu.useGpu)UploadMs=gpu.UploadMs;
            DisplayFrameMs=(System.Diagnostics.Stopwatch.GetTimestamp()-displayStart)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        }
        static Stage4Settings CopyValues(Stage4Settings s,Stage4Settings p)
        {
            if(p==null)p=new Stage4Settings();p.centerWidth=s.centerWidth;p.centerHeight=s.centerHeight;p.coverage=s.coverage;p.side=s.side;p.top=s.top;p.foreground=s.foreground;p.distance=s.distance;p.anchor=s.anchor;p.smoothing=s.smoothing;p.wallHeight=s.wallHeight;p.uniform=s.uniform;p.stage3Comparison=s.stage3Comparison;p.heightPolicy=s.heightPolicy;p.actorMode=s.actorMode;return p;
        }
        static bool Same(Stage4Settings a,Stage4Settings b)=>a!=null&&a.centerWidth==b.centerWidth&&a.centerHeight==b.centerHeight&&a.coverage==b.coverage&&a.side==b.side&&a.top==b.top&&a.foreground==b.foreground&&a.distance==b.distance&&a.anchor==b.anchor&&a.smoothing==b.smoothing&&a.wallHeight==b.wallHeight&&a.uniform==b.uniform&&a.stage3Comparison==b.stage3Comparison&&a.heightPolicy==b.heightPolicy&&a.actorMode==b.actorMode;
        void CacheBounds(Entry e)
        {
            if(e.data.LogicalVertices.Count==0){e.logical=new ProjectionBounds(new Point2(0,0),new Point2(0,0));e.hasBounds=true;return;}
            double x0=double.MaxValue,y0=x0,x1=double.MinValue,y1=x1;
            foreach(var v in e.data.LogicalVertices){x0=Math.Min(x0,v.Logical.X);y0=Math.Min(y0,v.Logical.Y);x1=Math.Max(x1,v.Logical.X);y1=Math.Max(y1,v.Logical.Y);}
            e.logical=new ProjectionBounds(new Point2(x0,y0),new Point2(x1,y1));e.hasBounds=true;
        }
        bool Visible(Entry e,float width,float height)
        {
            // Inverse viewport determines the logical candidate region, expanded for offscreen wall anchors.
            double wallHeight=settings.stage3Comparison?2.15:Math.Max(0,settings.wallHeight);
            double margin=(wallHeight+.3)*Math.Max(1,settings.foreground)/Math.Max(.05,Math.Min(settings.top,settings.side))+2;
            if(e.logical.Max.X<VisibleLogical.Min.X-margin||e.logical.Min.X>VisibleLogical.Max.X+margin||e.logical.Max.Y<VisibleLogical.Min.Y-margin||e.logical.Min.Y>VisibleLogical.Max.Y+margin)return false;
            var b=ProjectionShaderContract.Bounds(e.data,e.logical,Snapshot,settings);
            return b.Max.X>=-width*.5&&b.Min.X<=width*.5&&b.Max.Y>=-height*.5&&b.Min.Y<=height*.5;
        }
        bool LayerVisible(PreviewLayer layer)=>layer==PreviewLayer.Grid?display.showGrid:layer==PreviewLayer.Faces?display.showWallFaces:layer==PreviewLayer.Lips?display.showForegroundLips:layer==PreviewLayer.Contours?display.showContours:true;
        void Project(Entry e)
        {
            var start=System.Diagnostics.Stopwatch.GetTimestamp();
            using(projectionMarker.Auto())
            {
                e.vertices.Clear();var data=e.data;
                if(e.walls.Length<data.WallSamples.Count)e.walls=new WallDisplayVertex[data.WallSamples.Count];
                var wallSettings=new WallSettings(settings.stage3Comparison?2.15:Math.Max(0,settings.wallHeight),.22);
                for(int i=0;i<data.WallSamples.Count;i++)e.walls[i]=WallGeometry.Project(data.WallSamples[i],Snapshot,wallSettings,settings.stage3Comparison?WallHeightPolicy.ReferenceLocalScale:settings.heightPolicy);
                for(int i=0;i<data.LogicalVertices.Count;i++)
                {
                    var v=data.LogicalVertices[i];Point2 p;
                    if(data.VisualActorAnchor.HasValue){var anchor=data.VisualActorAnchor.Value;var projected=Snapshot.Project(anchor);var scale=ActorScale(Snapshot,anchor,settings.actorMode);p=new Point2(projected.X+(v.Logical.X-anchor.X)*scale.x,projected.Y+(v.Logical.Y-anchor.Y)*scale.y);}
                    else if(v.WallSampleIndex<0)p=Snapshot.Project(v.Logical);
                    else{var wall=e.walls[v.WallSampleIndex];p=v.Role==GeometryVertexRole.WallBottom?wall.Bottom:v.Role==GeometryVertexRole.ForegroundLip?wall.Lip:wall.Top;}
                    e.vertices.Add(PreviewMeshData.RenderPoint(p));
                }
                ProjectedVertices+=e.vertices.Count;
            }
            ProjectionMs+=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;
            start=System.Diagnostics.Stopwatch.GetTimestamp();
            using(uploadMarker.Auto())using(GeometryTrace.Measure(GeometryPhase.MeshUpload))
            {
                var mesh=e.filter.sharedMesh;
                // Reuse topology across camera-only changes; dirty terrain/overlays upload their current indices.
                if(e.dirty){mesh.Clear();mesh.indexFormat=e.vertices.Count>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16;mesh.SetVertices(e.vertices);mesh.SetTriangles(e.data.Indices,0,false);}
                else mesh.SetVertices(e.vertices);
                using(GeometryTrace.Measure(GeometryPhase.Bounds)){mesh.RecalculateBounds();var bounds=mesh.bounds;bounds.Expand(.001f);mesh.bounds=bounds;}
                Uploads++;e.dirty=false;e.version=version;
            }
            UploadMs+=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        }
        public static Vector2 ActorScale(ICartesianProjection projection,Point2 anchor,int mode)
        {float x=(float)projection.ScaleX(anchor.X),y=(float)projection.ScaleY(anchor.Y);return mode==0?Vector2.one*Mathf.Sqrt(x*y):new Vector2(x,y);}
        void BuildReferences()
        {
            references.Clear();foreach(var anchor in referencePoints)
            {
                // Footprint is always a projected logical 3x3 ring; anatomy mode is evaluated in display space.
                Ring(references,anchor.X-1.5,anchor.Y-1.5,anchor.X+1.5,anchor.Y+1.5,.055);
                var scale=ActorScale(Snapshot,anchor,settings.actorMode);
                // Inverse each display-space sprite vertex to retain the shared projection upload path.
                var p=Snapshot.Project(anchor);double w=scale.x*.55,h=scale.y*.8;
                AddDisplayQuad(p.X-w,p.Y-h,p.X+w,p.Y+h);
                AddDisplayQuad(p.X-w*1.5,p.Y-h,p.X-w*.4,p.Y-h*.6);
                AddDisplayQuad(p.X+w*.4,p.Y-h,p.X+w*1.5,p.Y-h*.6);
            }
        }
        void AddDisplayQuad(double x0,double y0,double x1,double y1)
        {references.Quad(Snapshot.Unproject(new Point2(x0,y0)),Snapshot.Unproject(new Point2(x1,y0)),Snapshot.Unproject(new Point2(x1,y1)),Snapshot.Unproject(new Point2(x0,y1)));}
        static void Ring(PreviewMeshData d,double x0,double y0,double x1,double y1,double w)
        {d.Rectangle(x0,y0,x1,y0+w);d.Rectangle(x0,y1-w,x1,y1);d.Rectangle(x0,y0+w,x0+w,y1-w);d.Rectangle(x1-w,y0+w,x1,y1-w);}
        public void Preset(int index)
        {settings.uniform=false;settings.stage3Comparison=false;settings.side=new[]{.7f,.45f,.25f}[index];settings.top=new[]{.6f,.35f,.2f}[index];settings.foreground=new[]{1.2f,1.5f,1.9f}[index];}
        public void SavePreset(string name)
        {string path=PresetPath(name);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonUtility.ToJson(settings,true));}
        public void LoadPreset(string name){settings=JsonUtility.FromJson<Stage4Settings>(File.ReadAllText(PresetPath(name)));}
        static string PresetPath(string name)
        {if(string.IsNullOrWhiteSpace(name)||name.Length>48)throw new ArgumentException("Use a name of 1â€“48 letters, digits, spaces, _ or -.");foreach(char c in name)if(!char.IsLetterOrDigit(c)&&c!=' '&&c!='_'&&c!='-')throw new ArgumentException("Preset name contains unsupported characters.");return SporeGobbo.Safety.Persistence.ExperimentPath("Stage4CameraExperiments",name+".json");}
        void OnGUI()
        {
            if(!ready||!ShowDebug){EditingPresetName=false;return;}panelHeight=Math.Max(100,Screen.height-320);
            GUILayout.BeginArea(new Rect(12,306,306,panelHeight),GUI.skin.box);scroll=GUILayout.BeginScrollView(scroll);
            GUILayout.Label("STAGE 4 â€” FOCUS + CONTEXT");if(gpu!=null)gpu.DrawControls();GUILayout.BeginHorizontal();if(GUILayout.Button("Gentle"))Preset(0);if(GUILayout.Button("Balanced"))Preset(1);if(GUILayout.Button("Strong"))Preset(2);GUILayout.EndHorizontal();
            settings.uniform=GUILayout.Toggle(settings.uniform,"Uniform map (same framing)");settings.stage3Comparison=GUILayout.Toggle(settings.stage3Comparison,"Original Stage 3 framing / walls");
            settings.centerWidth=Slider("Square center width (cells)",settings.centerWidth,12,64);settings.centerHeight=Slider("Square center height (cells)",settings.centerHeight,10,36);
            settings.coverage=Slider("Center screen width fraction",settings.coverage,.35f,.8f);settings.side=Slider("Side edge width",settings.side,.15f,1);settings.top=Slider("Distant top row height",settings.top,.1f,1);settings.foreground=Slider("Foreground height",settings.foreground,1,2.5f);settings.distance=Slider("Smooth transition distance",settings.distance,3,40);
            settings.anchor=Slider("Gobbo anchor (fraction down)",settings.anchor,.45f,.7f);settings.smoothing=Slider("Follow smoothing seconds",settings.smoothing,0,.8f);settings.wallHeight=Slider("Visual wall height",settings.wallHeight,.5f,4);
            settings.heightPolicy=(WallHeightPolicy)GUILayout.Toolbar((int)settings.heightPolicy,new[]{"Reference height","Continuous height"});
            settings.actorMode=GUILayout.Toolbar(settings.actorMode,new[]{"Proportional actors","Grid X/Y actors"});FreezeCamera=GUILayout.Toggle(FreezeCamera,"Freeze camera for dig comparison");
            if(GUILayout.Button("Reset camera defaults")){settings=new Stage4Settings();FreezeCamera=false;ExperimentFocus=null;}
            GUI.SetNextControlName("Stage4PresetName");presetName=GUILayout.TextField(presetName);EditingPresetName=GUI.GetNameOfFocusedControl()=="Stage4PresetName";GUILayout.BeginHorizontal();if(GUILayout.Button("Save named"))TryPreset(true);if(GUILayout.Button("Restore named"))TryPreset(false);GUILayout.EndHorizontal();
            GUILayout.Label(message);GUILayout.Label($"Logical Gobbo {live.Position:F2} Â· focus {Focus:F2}\nVisible logical X {VisibleLogical.Min.X:F1}â€¦{VisibleLogical.Max.X:F1}\nVisible logical Y {VisibleLogical.Min.Y:F1}â€¦{VisibleLogical.Max.Y:F1}\nCenter coverage X {settings.coverage:P0}, Y {settings.centerHeight/(renderCamera.orthographicSize*2):P0}\nProjected {ProjectedVertices:N0} vertices / {ActivePatches} patches\nProjection {ProjectionMs:F2} ms Â· upload {UploadMs:F2} ms ({Uploads})\nReferences: center (55.5,39.5), top (54.5,18.5), side (90.5,39.5), foreground (46.5,55.5), transition (43.5,25.5). 3Ã—3 ground rings; stationary placeholders, no AI.");
            GUILayout.EndScrollView();GUILayout.EndArea();
        }
        void TryPreset(bool save){try{if(save)SavePreset(presetName);else LoadPreset(presetName);message=(save?"Saved ":"Restored ")+presetName;}catch(Exception e){message=e.Message;}}
        static float Slider(string label,float v,float min,float max){GUILayout.Label(label+": "+v.ToString("F2"));return GUILayout.HorizontalSlider(v,min,max);}
        void OnDestroy(){foreach(var go in owned)if(go!=null){if(Application.isPlaying)Destroy(go);else DestroyImmediate(go);}foreach(var mesh in ownedMeshes)if(mesh!=null){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}}
    }
}
