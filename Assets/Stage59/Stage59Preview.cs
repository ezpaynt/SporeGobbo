using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SporeGobbo.Input;
using SporeGobbo.Geometry;
using SporeGobbo.Geometry.Preview;
using SporeGobbo.CampCandidate.Preview;

[DefaultExecutionOrder(-1500)]
public sealed class Stage59Preview:MonoBehaviour,ISporePauseScreen
{
    public GameObject PlayerPrefab,BuddyPrefab;
    public Shader SpriteShader;
    public List<GobboVisualSet> VisualSets=new List<GobboVisualSet>();
    public Stage57Preview Camp {get;private set;}
    public Stage47Preview Live=>Camp.Live;
    public Stage59Authority Authority {get;private set;}
    public GobboController Player {get;private set;}
    public BuddyFollow Buddy {get;private set;}
    public readonly List<Stage59ActorView> Views=new List<Stage59ActorView>();
    public bool ExternalScenario;
    public bool DiagnosticCamera,ShowPanel=true,ShowOutlines=true,ShowLandmark=true,Automated;
    public double CameraMs,SortingMs,InteractionMs,InitializationMs,InitialCollisionMs,ActorSpawnMs;
    public string Status="Actual Gobbo + Buddy; no campaign state.";
    public Vector2 LastInteractionLogical;
    public int RejectedRoutes;
    SporeUiCoordinator uiCoordinator;bool initialized,modal,paused;float referenceTransition;Vector2 comfortFocus,comfortVelocity;GameObject actorRoot,databaseRoot,landmarkGo;
    Presentation presenter;bool hoverModal;bool rendererDirty=true;MeshRenderer[] terrainRenderers;
    LineRenderer bedroom;Material bedroomMaterial;static readonly Point2[] roomCorners={new Point2(82,127),new Point2(87,127),new Point2(87,132),new Point2(82,132),new Point2(82,127)};readonly Vector3[] bedroomCorners=new Vector3[5];
    Mesh landmarkMesh;Material landmarkMaterial;ElevatedPath landmark;readonly List<Vector3> landmarkVertices=new List<Vector3>();
    public Point2 LandmarkAnchor=>new Point2(84.5,111.5);
    void Start()=>Initialize();
    public void Initialize()
    {
        if(initialized)return;initialized=true;long initial=System.Diagnostics.Stopwatch.GetTimestamp();
        if(GameState.Instance!=null||DiggableTerrainService.Active!=null||MapGenerator.Instance!=null)throw new InvalidOperationException("Stage59 requires an isolated scene without campaign or terrain globals.");
        uiCoordinator=SporeUiCoordinator.Instance;if(!uiCoordinator.TryBindPauseScreen(this))throw new InvalidOperationException("Stage59 cannot replace another pause screen.");
        Camp=GetComponent<Stage57Preview>();Camp.Initialize();Camp.automation=true;Camp.showPanel=false;Camp.enabled=false;Camp.CameraStudy.enabled=false;
        Live.automation=false;Live.ExternalInputBlock=true;Live.previewCamera.tag="MainCamera";Camp.Go("fire");referenceTransition=Camp.CameraStudy.sideTransition;
        Authority=new Stage59Authority(Camp.Session);Authority.AllowEdits=()=>Live.Ready&&Time.timeScale>0&&SporeInputReader.Instance!=null&&SporeInputReader.Instance.Context==SporeInputContext.Gameplay&&!modal;
        long collisionStart=System.Diagnostics.Stopwatch.GetTimestamp();Authority.CreateCollision(transform);InitialCollisionMs=StreamingGeometry.Elapsed(collisionStart);presenter=gameObject.AddComponent<Presentation>();presenter.Owner=this;Live.Stream.Activated+=c=>rendererDirty=true;Live.Stream.Evicted+=c=>rendererDirty=true;
        foreach(var f in Live.GetComponentsInChildren<MeshFilter>())if(f.name=="Gobbo and brush"||f.name=="Active logical actors"||f.name=="Dormant logical actors")f.GetComponent<MeshRenderer>().forceRenderingOff=true;
        databaseRoot=new GameObject("Stage59 copied production visual database");databaseRoot.transform.SetParent(transform,false);var db=databaseRoot.AddComponent<GobboVisualDatabase>();db.visualSets=VisualSets;
        long spawnStart=System.Diagnostics.Stopwatch.GetTimestamp();actorRoot=new GameObject("Stage59 real actor physics roots");actorRoot.transform.SetParent(transform,false);actorRoot.SetActive(false);
        var player=Spawn(PlayerPrefab,"Real player",Authority.ToWorld(new Point2(84.5,117.5)),true);Player=player.GetComponent<GobboController>();
        var buddy=Spawn(BuddyPrefab,"Real following Buddy",Authority.ToWorld(new Point2(88.5,119.5)),false);Buddy=buddy.GetComponent<BuddyFollow>();Buddy.player=player.transform;Buddy.useGobboDance=false;Buddy.teleportDistance=float.PositiveInfinity;Buddy.waypointReachDistance=.15f;Buddy.physicalBodyRadius=.28f;Buddy.followSpeed=3.5f;
        actorRoot.SetActive(true);ActorSpawnMs=StreamingGeometry.Elapsed(spawnStart);comfortFocus=Live.Position;
        landmark=ElevatedShapes.Spike(LandmarkAnchor,3.2,17,LandmarkProfile.BroadShoulder);
        landmarkGo=new GameObject("Diagnostic elevated landmark on protected Fire backing");landmarkGo.transform.SetParent(transform,false);landmarkMesh=new Mesh{name="Existing ElevatedShapes spike diagnostic"};landmarkGo.AddComponent<MeshFilter>().sharedMesh=landmarkMesh;landmarkMaterial=new Material(SpriteShader);landmarkMaterial.color=new Color(.38f,.34f,.24f);var mr=landmarkGo.AddComponent<MeshRenderer>();mr.sharedMaterial=landmarkMaterial;mr.sortingOrder=18;
        int[] triangles=new int[(landmark.Sections.Length-1)*6];for(int i=0;i<landmark.Sections.Length-1;i++){int t=i*6,k=i*2;triangles[t]=k;triangles[t+1]=k+1;triangles[t+2]=k+2;triangles[t+3]=k+1;triangles[t+4]=k+3;triangles[t+5]=k+2;}landmarkMesh.vertices=new Vector3[landmark.Sections.Length*2];landmarkMesh.triangles=triangles;
        var room=new GameObject("Future bedroom diagnostic / ordinary dirt / no progression");room.transform.SetParent(transform,false);bedroom=room.AddComponent<LineRenderer>();bedroomMaterial=new Material(SpriteShader);bedroom.sharedMaterial=bedroomMaterial;bedroom.startColor=bedroom.endColor=new Color(.2f,.8f,1,.65f);bedroom.startWidth=bedroom.endWidth=.055f;bedroom.positionCount=5;bedroom.sortingOrder=81;InitializationMs=StreamingGeometry.Elapsed(initial);
    }
    public GameObject Spawn(GameObject prefab,string label,Vector2 position,bool player)
    {
        var go=Instantiate(prefab,actorRoot.transform);go.name=label;go.transform.position=position;
        // Remove only unrelated campaign/combat/lifecycle components from this instance, never the prefab asset.
        foreach(var component in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            bool keep=component is GobboVisualController||component is BuddyDirectionalSprite||(player?component is GobboController:component is BuddyFollow||component is BuddyDigAbility);
            if(!keep){component.enabled=false;DestroyImmediate(component);}
        }
        var rb=go.GetComponent<Rigidbody2D>();rb.gravityScale=0;rb.freezeRotation=true;rb.collisionDetectionMode=CollisionDetectionMode2D.Continuous;rb.interpolation=RigidbodyInterpolation2D.Interpolate;rb.position=position;rb.linearVelocity=Vector2.zero;rb.angularVelocity=0;Physics2D.SyncTransforms();
        var binding=go.AddComponent<ActorTerrainBinding>();binding.Bind(Authority);binding.MinimumBodyRadius=player ? .4f : .28f;binding.Ready=()=>Live.Ready&&!Authority.IsDisposed;binding.ReadRevision=()=>Authority.Revision;binding.PointerToWorld=InversePointer;
        binding.FindRoute=(from,to)=>{var path=Authority.Route(from,to,TileMover.GetMapClearanceExtents(rb,binding.MinimumBodyRadius));if(path.Count==0)RejectedRoutes++;return path;};
        if(player){var gobbo=go.GetComponent<GobboController>();gobbo.healthControlsSize=false;gobbo.baseSize=1;gobbo.maxSize=3;gobbo.followerCount=0;gobbo.moveSpeed=4;gobbo.hasDashBite=false;}
        else{var dig=go.GetComponent<BuddyDigAbility>();if(dig==null)dig=go.AddComponent<BuddyDigAbility>();dig.BindTerrain(Authority);}
        var view=go.AddComponent<Stage59ActorView>();view.Owner=this;view.Initialize();Views.Add(view);return go;
    }
    public Vector3 Render(Vector2 physical)
    {var p=Live.Snapshot.Project(Authority.ToLogical(physical));return new Vector3((float)p.X,(float)-p.Y,0);}
    public Vector2 InversePointer(Vector2 screen)
    {var p=Live.previewCamera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,10));return Authority.ToWorld(Live.Snapshot.Unproject(new Point2(p.x,-p.y)));}
    public void ResetCameraStudy(){comfortFocus=Live.Position;comfortVelocity=Vector2.zero;Live.SetPreviewFocus(Live.Position);}
    public void SetModal(bool value)
    {if(modal==value)return;modal=value;if(value)SporeUiCoordinator.Instance.PushModal(this,()=>SetModal(false),false);else SporeUiCoordinator.Instance.PopModal(this);}
    public bool IsPauseOpen=>paused;public bool HasPauseSubpage=>false;public UnityEngine.UI.Selectable PauseDefaultSelectable=>null;
    public void OpenPause()=>SetPaused(true);public void ClosePause()=>SetPaused(false);public void BackPauseSubpage(){}
    public void SetPaused(bool value)
    {if(paused==value)return;paused=value;if(value)SporePauseService.Acquire(this);else SporePauseService.Release(this);}
    public void SetSize(float size)
    {Player.baseSize=size;Player.RefreshAfterSaveLoad();Physics2D.SyncTransforms();}
    void Update()
    {
        Initialize();
        bool hover=!Automated&&ShowPanel&&Mouse.current!=null&&new Rect(8,Screen.height-523,310,515).Contains(Mouse.current.position.ReadValue());
        if(hover!=hoverModal){hoverModal=hover;if(hover)SporeUiCoordinator.Instance.PushModal(presenter,null,false);else SporeUiCoordinator.Instance.PopModal(presenter);}
        if(!Automated&&Keyboard.current!=null&&Keyboard.current.tabKey.wasPressedThisFrame)ShowPanel=!ShowPanel;
        // Existing semantic Interact consumes the same inverse mapping; no Camp station/run service is invoked.
        var input=SporeInputReader.Instance;
        if(!ExternalScenario&&input!=null&&input.Context==SporeInputContext.Gameplay&&input.Interact.StartedThisFrame&&!modal&&Time.timeScale>0)
        {using(new ActorTerrainBinding.Scope("SG59.InteractionMapping")){long t=System.Diagnostics.Stopwatch.GetTimestamp();var world=input.ActiveControlScheme==SporeControlScheme.Gamepad?(Vector2)Player.transform.position+Player.CurrentAimDirection*1.2f:InversePointer(input.AimPointer);var p=Authority.ToLogical(world);LastInteractionLogical=new Vector2((float)p.X,(float)p.Y);Status="Logical interaction probe "+LastInteractionLogical+" (no station gameplay)";InteractionMs=StreamingGeometry.Elapsed(t);}}
        if(Player==null)return;
        var logical=Authority.ToLogical(Player.transform.position);Live.TeleportWithoutFocus(new Vector2((float)logical.X,(float)logical.Y));
        using(new ActorTerrainBinding.Scope("SG59.CameraControl"))
        {
            long t=System.Diagnostics.Stopwatch.GetTimestamp();var camera=Camp.CameraStudy;camera.sideTransition=DiagnosticCamera?referenceTransition*1.5f:referenceTransition;camera.freezeFocus=DiagnosticCamera;
            if(DiagnosticCamera)
            {
                Vector2 target=Live.Position;var delta=target-comfortFocus;const float deadband=.6f;
                if(delta.magnitude>deadband)target-=delta.normalized*deadband;else target=comfortFocus;
                comfortFocus=Vector2.SmoothDamp(comfortFocus,target,ref comfortVelocity,.16f,float.PositiveInfinity,Time.deltaTime);Live.SetPreviewFocus(comfortFocus);
            }
            else {comfortFocus=Live.Position;comfortVelocity=Vector2.zero;}
            CameraMs=StreamingGeometry.Elapsed(t);
        }
    }
    // Runs after the streaming host has committed this frame's exact projection snapshot.
    [DefaultExecutionOrder(2600)] public sealed class Presentation:MonoBehaviour
    {
        public Stage59Preview Owner;void LateUpdate()=>Owner.Draw();
    }
    public void Draw()
    {
        if(Authority==null||Live.Snapshot==null)return;
        using(new ActorTerrainBinding.Scope("SG59.Sorting"))
        {
            long t=System.Diagnostics.Stopwatch.GetTimestamp();Views.Sort((a,b)=>b.PhysicalFoot.y.CompareTo(a.PhysicalFoot.y));
            if(rendererDirty){terrainRenderers=Live.GetComponentsInChildren<MeshRenderer>();rendererDirty=false;
            foreach(var r in terrainRenderers)
            {if(r==null)continue;if(r.name.Contains("Faces"))r.sortingOrder=12;if(r.name.Contains("Lips"))r.sortingOrder=60;}}
            SortingMs=StreamingGeometry.Elapsed(t);
        }
        int order=20;foreach(var view in Views){view.Outline=ShowOutlines;view.Draw(order++);}
        for(int i=0;i<5;i++){var q=Live.Snapshot.Project(roomCorners[i]);bedroomCorners[i]=new Vector3((float)q.X,(float)-q.Y);}bedroom.SetPositions(bedroomCorners);bedroom.enabled=!ExternalScenario;
        landmarkGo.SetActive(ShowLandmark);if(ShowLandmark)
        {landmarkVertices.Clear();foreach(var section in landmark.Sections){var a=ElevatedProjection.Project(Live.Snapshot,section.Left);var b=ElevatedProjection.Project(Live.Snapshot,section.Right);landmarkVertices.Add(new Vector3((float)a.X,(float)-a.Y));landmarkVertices.Add(new Vector3((float)b.X,(float)-b.Y));}landmarkMesh.SetVertices(landmarkVertices);landmarkMesh.RecalculateBounds();}
    }
    void OnGUI()
    {
        if(ExternalScenario||!ShowPanel||Authority==null)return;
        GUILayout.BeginArea(new Rect(8,8,310,515),GUI.skin.box);GUILayout.Label("STAGE 5.9 / REAL ACTOR PROOF");GUILayout.Label("WASD move / Shift dig / Space dash\nMouse aim / F interaction probe / Tab panel\nProduction controller bindings remain active.");
        DiagnosticCamera=GUILayout.Toggle(DiagnosticCamera,"Optional comfort comparison");ShowOutlines=GUILayout.Toggle(ShowOutlines,"Projected physical collider outlines");ShowLandmark=GUILayout.Toggle(ShowLandmark,"Elevated landmark diagnostic");
        bool m=GUILayout.Toggle(modal,"Nonpausing UI modal (blocks world input)");if(m!=modal)SetModal(m);bool p=GUILayout.Toggle(paused,"Pause simulation");if(p!=paused)SetPaused(p);
        GUILayout.BeginHorizontal();foreach(float size in new[]{.75f,1f,1.5f})if(GUILayout.Button("Body "+size))SetSize(size);GUILayout.EndHorizontal();
        if(GUILayout.Button("Fire / safe reset position")){Player.GetComponent<Rigidbody2D>().position=Authority.ToWorld(new Point2(84.5,117.5));Buddy.GetComponent<Rigidbody2D>().position=Authority.ToWorld(new Point2(88.5,119.5));}
        GUILayout.Label("Single terrain revision "+Authority.Revision+" / collision "+Authority.PublishedRevision+"\nNav rejects "+RejectedRoutes+" / collider publish "+Authority.ColliderMs.ToString("F2")+" ms\nCamera "+CameraMs.ToString("F3")+" / presentation+sort "+SortingMs.ToString("F3")+" ms");
        GUILayout.Label("Future bedroom test earth: south of Fire.\nNo root-door or residential construction.\nShadows are diagnostic ground ellipses;\nlarge spike uses existing elevated geometry.");GUILayout.Label(Status);GUILayout.EndArea();
    }
    public GameObject AddScenarioBuddy(Vector2 position)
    {
        var go=Spawn(BuddyPrefab,"Real resident Buddy",position,false);var f=go.GetComponent<BuddyFollow>();
        f.player=Player.transform;f.useGobboDance=false;f.teleportDistance=float.PositiveInfinity;f.waypointReachDistance=.15f;f.physicalBodyRadius=.28f;f.followSpeed=3.5f;
        if(Buddy==null)Buddy=f;return go;
    }
    public void RemoveScenarioActor(GameObject go)
    {
        if(go==null)return;var view=go.GetComponent<Stage59ActorView>();Views.Remove(view);if(Buddy!=null&&Buddy.gameObject==go)Buddy=null;go.SetActive(false);Destroy(go);
    }
    public void RebindScenario()
    {
        Authority.Dispose();Authority=new Stage59Authority(Camp.Session);
        Authority.AllowEdits=()=>Live.Ready&&Time.timeScale>0&&SporeInputReader.Instance!=null&&SporeInputReader.Instance.Context==SporeInputContext.Gameplay;
        Authority.CreateCollision(transform);
        foreach(var view in Views){view.GetComponent<ActorTerrainBinding>().Bind(Authority);var dig=view.GetComponent<BuddyDigAbility>();if(dig!=null)dig.BindTerrain(Authority);}
    }
    public void ReleaseActors(){Views.Clear();if(actorRoot!=null)Destroy(actorRoot);Player=null;Buddy=null;}
    void OnDestroy()
    {if(uiCoordinator!=null){if(hoverModal)uiCoordinator.PopModal(presenter);if(modal)uiCoordinator.PopModal(this);uiCoordinator.UnbindPauseScreen(this);}if(paused)SporePauseService.Release(this);Authority?.Dispose();foreach(var o in new UnityEngine.Object[]{actorRoot,databaseRoot,bedroomMaterial,landmarkMesh,landmarkMaterial,landmarkGo})if(o!=null){if(Application.isPlaying)Destroy(o);else DestroyImmediate(o);}}
}
