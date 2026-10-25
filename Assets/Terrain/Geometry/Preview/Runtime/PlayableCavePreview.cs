using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SporeGobbo.Geometry.Preview
{
    /// <summary>Isolated Stage 3 driver. No production player, terrain service, physics or save dependencies.</summary>
    [RequireComponent(typeof(CaveGeometryPreview))]
    public sealed class PlayableCavePreview : MonoBehaviour
    {
        public TextAsset fixture;
        public Material[] layerMaterials;
        public float movementSpeed=5,footprint=1,digRadius=2.2f,digInterval=.1f;
        public int patchSize=16;
        public DigBrush brush=DigBrush.ThreeWide;
        public bool followPlayer=true,showPatchBoundaries,showDirtyPatches=true,directMouseEditing,fullRebuildComparison;
        public PreviewEditSession Session {get;private set;}
        public Vector2 Position {get;private set;}
        public Vector2 Aim {get;private set;}=Vector2.up;
        public double LastUpdateMilliseconds {get;private set;}
        public double AverageUpdateMilliseconds=>updates==0?0:totalUpdate/updates;
        public double WorstUpdateMilliseconds {get;private set;}
        public long LastUpdateAllocatedBytes {get;private set;}
        public int RuntimeObjectCount=>objects.Count;
        public int RuntimeMeshCount=>meshes.Count;
        public int ActiveRuntimeMeshCount=>objects.Count(o=>o!=null&&o.GetComponent<MeshRenderer>().enabled&&o.GetComponent<MeshFilter>().sharedMesh.vertexCount>0);
        public int TotalSceneObjectCount=>UnityEngine.Object.FindObjectsByType<Transform>().Length+objects.Count;
        public int ActiveSceneMeshCount=>ActiveRuntimeMeshCount+UnityEngine.Object.FindObjectsByType<MeshRenderer>().Count(r=>r.enabled&&r.GetComponent<MeshFilter>()!=null&&r.GetComponent<MeshFilter>().sharedMesh.vertexCount>0);
        public int VertexCount=>meshes.Sum(m=>m.vertexCount);
        public int TriangleCount=>meshes.Sum(m=>(int)m.GetIndexCount(0)/3);
        readonly Dictionary<DualTile,MeshFilter[]> patches=new Dictionary<DualTile,MeshFilter[]>();
        readonly Dictionary<DualTile,float> rebuiltAt=new Dictionary<DualTile,float>();
        readonly List<GameObject> objects=new List<GameObject>();
        readonly List<Mesh> meshes=new List<Mesh>();
        readonly List<DualTile> predicted=new List<DualTile>();
        readonly List<MeshRenderer> solids=new List<MeshRenderer>(),walls=new List<MeshRenderer>(),lips=new List<MeshRenderer>(),contours=new List<MeshRenderer>();
        CaveGeometryPreview display;
        Stage4CameraPreview projectedView;
        MeshFilter actor,border,boundaries,dirtyOverlay;
        MeshRenderer actorRenderer;
        readonly PreviewMeshData actorData=new PreviewMeshData("Actor",PreviewLayer.Gobbo);
        readonly PreviewMeshData boundaryData=new PreviewMeshData("Patches",PreviewLayer.Grid);
        readonly PreviewMeshData dirtyData=new PreviewMeshData("Dirty",PreviewLayer.Grid);
        readonly PreviewMeshData edgeData=new PreviewMeshData("Edge",PreviewLayer.Solid);
        bool edgeInitialized;
        MaterialPropertyBlock overlayColor;
        Vector2 scroll;
        Vector2 target;
        float cooldown;
        int updates;
        double totalUpdate;
        string status="";
        public IReadOnlyList<DualTile> PredictedCells=>predicted;
        void Start(){Initialize();}
        public void Initialize()
        {
            if(Session!=null)return;
            display=GetComponent<CaveGeometryPreview>();projectedView=GetComponent<Stage4CameraPreview>();
            if(fixture==null)throw new InvalidOperationException("Assign the supplied cave fixture.");
            Session=new PreviewEditSession(fixture.text,patchSize);
            // Baked reference meshes remain available outside Play; runtime owns a fixed mesh pool.
            foreach(var renderer in GetComponentsInChildren<MeshRenderer>())
                if(renderer.name!="Floor"&&renderer.name!="Grid")renderer.enabled=false;
            foreach(var key in Session.Patches.Keys)
            {
                var filters=new MeshFilter[4];
                var data=Session.Patches[key];
                for(int i=0;i<4;i++)filters[i]=Create(data[i].Name,data[i].Layer);
                patches.Add(key,filters);
            }
            border=Create("Live world edge",PreviewLayer.Solid);
            boundaries=Create("Patch boundaries",PreviewLayer.Grid,18);
            dirtyOverlay=Create("Recently rebuilt patches",PreviewLayer.Grid,17);
            actor=Create("Player footprint and dig preview",PreviewLayer.Gobbo,20);actorRenderer=actor.GetComponent<MeshRenderer>();
            display.wallRenderers=walls.ToArray();display.lipRenderers=lips.ToArray();display.contourRenderers=contours.ToArray();
            Upload();Position=PreviewMotion.FindSpawn(Session.Terrain,new Vector2(50.5f,39.5f),footprint);
            Predict(Position+Aim*(footprint*.5f+.55f));DrawOverlay();
        }
        MeshFilter Create(string label,PreviewLayer layer,int order=-1)
        {
            var owner=new GameObject(label){hideFlags=HideFlags.DontSave};owner.transform.SetParent(transform,false);objects.Add(owner);
            var filter=owner.AddComponent<MeshFilter>();var mesh=new Mesh{name=label,hideFlags=HideFlags.DontSave};mesh.MarkDynamic();meshes.Add(mesh);filter.sharedMesh=mesh;
            var renderer=owner.AddComponent<MeshRenderer>();renderer.sharedMaterial=layerMaterials[(int)layer];renderer.sortingOrder=order>=0?order:layer==PreviewLayer.Solid?1:layer==PreviewLayer.Faces?10:layer==PreviewLayer.Lips?11:12;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            if(layer==PreviewLayer.Solid)solids.Add(renderer);if(layer==PreviewLayer.Faces)walls.Add(renderer);if(layer==PreviewLayer.Lips)lips.Add(renderer);if(layer==PreviewLayer.Contours)contours.Add(renderer);
            return filter;
        }
        void Upload()
        {
            foreach(var key in Session.LastDirty)
            {
                for(int i=0;i<4;i++)UploadDisplay(Session.Patches[key][i],patches[key][i]);
                rebuiltAt[key]=Time.realtimeSinceStartup;
            }
            if(NeedsEdgeRefresh())using(GeometryTrace.Measure(GeometryPhase.Edge)) {
            var edge=edgeData;edge.Clear();var t=Session.Terrain;
            for(int x=0;x<t.Width;x++){if(t.IsSolid(x,0))edge.Rectangle(x,0,x+1,.5);if(t.IsSolid(x,t.Height-1))edge.Rectangle(x,t.Height-.5,x+1,t.Height);}
            for(int y=0;y<t.Height;y++){if(t.IsSolid(0,y))edge.Rectangle(0,y,.5,y+1);if(t.IsSolid(t.Width-1,y))edge.Rectangle(t.Width-.5,y,t.Width,y+1);}
            UploadDisplay(edge,border);edgeInitialized=true;}
            display.ApplyVisibility();
        }
        bool NeedsEdgeRefresh()
        {
            if(!edgeInitialized||Session.DigCount==0)return true;
            var cells=Session.Changed;var t=Session.Terrain;
            for(int i=0;i<cells.Count;i++)if(cells[i].X==0||cells[i].Y==0||cells[i].X==t.Width-1||cells[i].Y==t.Height-1)return true;
            return false;
        }
        public void Predict(Vector2 logicalTarget)
        {target=logicalTarget;PreviewDigBrush.Cells(target,brush,digRadius,Session.Terrain,predicted);}
        public bool DigNow()
        {
            using(GeometryTrace.Measure(GeometryPhase.Dig))
            {
            long allocated=GC.GetAllocatedBytesForCurrentThread();var watch=System.Diagnostics.Stopwatch.StartNew();
            if(!Session.Dig(predicted,fullRebuildComparison))return false;
            Upload();watch.Stop();LastUpdateMilliseconds=watch.Elapsed.TotalMilliseconds;LastUpdateAllocatedBytes=allocated==0?-1:GC.GetAllocatedBytesForCurrentThread()-allocated;
            updates++;totalUpdate+=LastUpdateMilliseconds;WorstUpdateMilliseconds=Math.Max(WorstUpdateMilliseconds,LastUpdateMilliseconds);return true;
        
            }
        }
        public void Step(Vector2 direction,Vector2 aim,bool digging,float dt)
        {
            Initialize();if(aim.sqrMagnitude>.0001f)Aim=aim.normalized;
            Predict(Position+Aim*(footprint*.5f+.55f));
            cooldown-=dt;if(digging&&cooldown<=0){DigNow();cooldown=Mathf.Max(.02f,digInterval);}
            if(direction.sqrMagnitude>0)Position=PreviewMotion.Move(Session.Terrain,Position,Vector2.ClampMagnitude(direction,1)*movementSpeed*dt,footprint);
            Predict(Position+Aim*(footprint*.5f+.55f));
            if(followPlayer&&projectedView==null){display.previewCamera.transform.position=new Vector3(Position.x,-Position.y,-10);display.previewCamera.orthographicSize=Mathf.Max(10,footprint*1.6f);}
        }
        void Update()=>TickInput(Time.deltaTime);
        public void TickInput(float dt)
        {
            if(Session==null)return;
            var keyboard=Keyboard.current;var mouse=Mouse.current;Vector2 movement=Vector2.zero;
            if(keyboard!=null&&(projectedView==null||!projectedView.EditingPresetName)){movement.x=(keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed?1:0);movement.y=(keyboard.sKey.isPressed?1:0)-(keyboard.wKey.isPressed?1:0);if(keyboard.rKey.wasPressedThisFrame)ResetFixture();}
            Vector2 aim=movement.sqrMagnitude>0?movement:Aim;bool overUI=false;
            if(mouse!=null)
            {
                Vector2 screen=mouse.position.ReadValue();overUI=(projectedView==null||projectedView.ShowDebug)&&(projectedView!=null&&projectedView.PointerOverPanel(screen)||screen.x<304&&screen.y>Screen.height-300||screen.x>Screen.width-374&&screen.y>Screen.height-660);
                Vector3 world=display.previewCamera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,10));target=projectedView!=null?projectedView.InverseRender(world):new Vector2(world.x,-world.y);if(!overUI)aim=target-Position;
            }
            bool dig=(projectedView==null||!projectedView.EditingPresetName)&&!overUI&&((mouse!=null&&mouse.leftButton.isPressed)||(keyboard!=null&&keyboard.spaceKey.isPressed));
            if(directMouseEditing&&mouse!=null)
            {
                Vector2 mouseTarget=target;Step(movement,aim,false,Mathf.Min(dt,.1f));Predict(mouseTarget);
                if(dig&&cooldown<=0){DigNow();cooldown=digInterval;}
            }
            else Step(movement,aim,dig,Mathf.Min(dt,.1f));
            DrawOverlay();
        }
        public void SetFootprint(float size)
        {
            try{Vector2 spawn=PreviewMotion.FindSpawn(Session.Terrain,Position,size);Position=spawn;footprint=size;status="Footprint changed; moved to the nearest fitting open position if required.";}
            catch(InvalidOperationException ex){status=ex.Message;}
        }
        public void ResetFixture()
        {
            Session.Reset();Upload();Position=PreviewMotion.FindSpawn(Session.Terrain,new Vector2(50.5f,39.5f),footprint);cooldown=0;
            updates=0;totalUpdate=WorstUpdateMilliseconds=LastUpdateMilliseconds=0;LastUpdateAllocatedBytes=0;status="Original fixture restored.";
        }
        public void SetPatchSize(int size)
        {
            if(size==patchSize)return;
            // Explicit diagnostic repartition may replace the pool; ordinary digging never does.
            Release();patchSize=size;Initialize();status="Patch size changed; fixture reset.";
        }
        public void DrawOverlay()
        {
            using(GeometryTrace.Measure(GeometryPhase.Overlay))
            {
            var data=actorData;data.Clear();float half=footprint*.5f;
            Ring(data,Position.x-half,Position.y-half,Position.x+half,Position.y+half,.06);
            data.Rectangle(Position.x-.27,Position.y-.32,Position.x+.27,Position.y+.32);
            data.Triangle(new Point2(Position.x-.2,Position.y-.18),new Point2(Position.x-.55,Position.y-.4),new Point2(Position.x-.22,Position.y+.08));
            data.Triangle(new Point2(Position.x+.2,Position.y-.18),new Point2(Position.x+.55,Position.y-.4),new Point2(Position.x+.22,Position.y+.08));
            foreach(var cell in predicted)Ring(data,cell.X+.08,cell.Y+.08,cell.X+.92,cell.Y+.92,.045);
            UploadDisplay(data,actor);
            if(overlayColor==null)overlayColor=new MaterialPropertyBlock();var color=overlayColor;color.SetColor("_Color",PreviewDigBrush.TooSmall(brush,digRadius,footprint)?new Color(1,.3f,.16f):new Color(1,.78f,.12f));actorRenderer.SetPropertyBlock(color);
            var lines=boundaryData;lines.Clear();
            if(showPatchBoundaries)foreach(var key in Session.Patches.Keys)Ring(lines,key.X+.5,key.Y+.5,Math.Min(key.X+patchSize+.5,Session.Terrain.Width-.5),Math.Min(key.Y+patchSize+.5,Session.Terrain.Height-.5),.045);
            UploadDisplay(lines,boundaries);
            var recent=dirtyData;recent.Clear();
            if(showDirtyPatches)foreach(var entry in rebuiltAt)if(Time.realtimeSinceStartup-entry.Value<.65f)
                Ring(recent,entry.Key.X+.6,entry.Key.Y+.6,Math.Min(entry.Key.X+patchSize+.4,Session.Terrain.Width-.6),Math.Min(entry.Key.Y+patchSize+.4,Session.Terrain.Height-.6),.11);
            UploadDisplay(recent,dirtyOverlay);
        
            }
        }
        static void Ring(PreviewMeshData d,double x0,double y0,double x1,double y1,double w)
        {d.Rectangle(x0,y0,x1,y0+w);d.Rectangle(x0,y1-w,x1,y1);d.Rectangle(x0,y0+w,x0+w,y1-w);d.Rectangle(x1-w,y0+w,x1,y1-w);}
        void UploadDisplay(PreviewMeshData data,MeshFilter filter)
        {if(projectedView!=null&&projectedView.enabled)projectedView.RegisterUpload(data,filter);else data.ApplyTo(filter.sharedMesh);}
        void OnGUI()
        {
            using(GeometryTrace.Measure(GeometryPhase.UI))
            {
            if(Session==null||!Application.isPlaying||projectedView!=null&&!projectedView.ShowDebug)return;
            GUILayout.BeginArea(new Rect(Screen.width-370,12,358,Math.Min(650,Screen.height-24)),GUI.skin.box);scroll=GUILayout.BeginScrollView(scroll);
            GUILayout.Label("STAGE 3 — PLAYABLE EXCAVATION");GUILayout.Label("WASD move · mouse aim · hold LMB / Space dig\nR reset · yellow outline = actual collision body\nSmall cell rings = exact next dig batch");
            GUILayout.Label("Body footprint: "+footprint+" × "+footprint);
            GUILayout.BeginHorizontal();foreach(int size in new[]{1,3,9})if(GUILayout.Button(size+" × "+size))SetFootprint(size);GUILayout.EndHorizontal();
            movementSpeed=Slider("Speed",movementSpeed,1,12);
            GUILayout.Label("Dig brush");brush=(DigBrush)GUILayout.Toolbar((int)brush,new[]{"1 cell","3 wide","Radius"});
            if(brush==DigBrush.Radius)digRadius=Slider("Radius",digRadius,.5f,8);
            if(PreviewDigBrush.TooSmall(brush,digRadius,footprint))GUILayout.Label("ORANGE: brush is smaller than the body. Solid cells still block movement; widen the brush or excavate more.");
            followPlayer=GUILayout.Toggle(followPlayer,projectedView==null?"Follow Gobbo (uniform camera)":"Follow Gobbo (projected camera)");
            directMouseEditing=GUILayout.Toggle(directMouseEditing,"Debug: edit directly under mouse (unlimited reach)");
            showPatchBoundaries=GUILayout.Toggle(showPatchBoundaries,"Patch boundaries");showDirtyPatches=GUILayout.Toggle(showDirtyPatches,"Recently rebuilt patches (0.65 s)");
            GUILayout.BeginHorizontal();if(GUILayout.Button("8 × 8 patches"))SetPatchSize(8);if(GUILayout.Button("16 × 16 patches"))SetPatchSize(16);GUILayout.EndHorizontal();
            fullRebuildComparison=GUILayout.Toggle(fullRebuildComparison,"Comparison: full map rebuild per dig");
            if(GUILayout.Button("Reset original fixture (R)"))ResetFixture();
            GUILayout.Label($"Dig batches {Session.DigCount} · last {Session.Changed.Count} cells / {Session.LastPatchCount} patches\n{(projectedView==null?"CPU build+upload":"CPU edit/build queue")} ms: last {LastUpdateMilliseconds:F2} · avg {AverageUpdateMilliseconds:F2} · worst {WorstUpdateMilliseconds:F2}\nLast managed allocation: {(LastUpdateAllocatedBytes<0?"unavailable (Mono counter)":(LastUpdateAllocatedBytes/1024.0).ToString("F1")+" KiB")}\nLive pool: {RuntimeMeshCount} meshes / {RuntimeObjectCount} objects\n{VertexCount:N0} vertices / {TriangleCount:N0} triangles\nWall sample evaluations: {Session.OrientationEvaluations}");
            GUILayout.Label(status);GUILayout.EndScrollView();GUILayout.EndArea();
        
            }
        }
        static float Slider(string label,float value,float min,float max){GUILayout.Label(label+": "+value.ToString("F1"));return GUILayout.HorizontalSlider(value,min,max);}
        void Release()
        {
            foreach(var owner in objects)if(owner!=null){if(Application.isPlaying)Destroy(owner);else DestroyImmediate(owner);}
            foreach(var mesh in meshes)if(mesh!=null){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}
            objects.Clear();meshes.Clear();patches.Clear();rebuiltAt.Clear();solids.Clear();walls.Clear();lips.Clear();contours.Clear();edgeInitialized=false;Session=null;
        }
        void OnDestroy(){Release();}
    }
}
