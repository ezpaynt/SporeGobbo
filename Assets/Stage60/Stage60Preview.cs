using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using SporeGobbo.Input;
using SporeGobbo.Geometry;
using SporeGobbo.Geometry.Preview;
using SporeGobbo.CampCandidate;
using SporeGobbo.CampCandidate.Preview;

[DefaultExecutionOrder(-1700)]
public sealed class Stage60Preview:MonoBehaviour
{
    public Stage59Preview Proof {get;private set;}public CampSession Session=>Proof.Camp.Session;
    public Stage60Residence Residence {get;private set;}public bool Automated,ShowPanel=true,Advanced,ShowGuide=false;
    public static readonly Point2 FireWatch=new Point2(90.5,113.5);
    public string Status="Explore the roots above Squad Select. Test state only; no campaign.";
    public int RootActivations,Resets,FakeReturns;public double RootMs,ResetMs;
    AuthoredCamp definition;bool initialized,hover;Mesh rootMesh,bedMesh,crackMesh,stationMesh;Material rootMaterial,bedMaterial,crackMaterial,stationMaterial;MeshRenderer rootRenderer;GameObject overlay;
    readonly List<Vector3> vertices=new List<Vector3>();readonly List<int> triangles=new List<int>();readonly List<Color> colors=new List<Color>();
    public bool RootOpened=>initialized&&Stage60Layout.RootCells().All(c=>!Session.Terrain.IsSolid(c.X,c.Y));
    public bool CanSimulate=>initialized&&Proof.Live.Ready&&Time.timeScale>0&&SporeInputReader.Instance!=null&&SporeInputReader.Instance.Context==SporeInputContext.Gameplay;
    void Awake()
    {
        definition=Stage60Layout.Create();var camp=GetComponent<Stage57Preview>();camp.DefinitionOverride=()=>definition;camp.SessionFactory=d=>new CampSession(d,true);
        Proof=GetComponent<Stage59Preview>();Proof.ExternalScenario=true;Proof.ShowPanel=false;Proof.ShowLandmark=false;Proof.ShowOutlines=false;
    }
    void Start()=>Initialize();
    public void Initialize()
    {
        if(initialized)return;
        // Verify storage before any scenario initialization; never allow a manual unowned launch.
        SporeGobbo.Safety.Persistence.ExperimentPath("Stage60");
        Proof.Initialize();initialized=true;
        if(Proof.Buddy!=null)Proof.RemoveScenarioActor(Proof.Buddy.gameObject);
        Bind();Residence=new Stage60Residence(this);
        overlay=new GameObject("Stage60 batched root / bed placeholders");overlay.transform.SetParent(transform,false);
        rootMesh=MakeMesh("Attached Palette V2 root",out rootMaterial,out rootRenderer,62);
        bedMesh=MakeMesh("Established bed placeholders",out bedMaterial,out _,19);
        crackMesh=MakeMesh("Temporary dotted fracture",out crackMaterial,out _,63);
        stationMesh=MakeMesh("Existing Future Camp station placeholders",out stationMaterial,out _,27);
        gameObject.AddComponent<Stage60Draw>().Owner=this;
    }
    void Bind()
    {
        var terrain=new Stage60ResidenceTerrain(Proof.Authority);
        Proof.Authority.ScenarioDig=terrain.Dig;
        Proof.Authority.ScenarioDiggable=c=>{var k=Session.Definition.Category(c.x,c.y);return k==CampCell.Expansion||k==CampCell.Residential;};
    }
    public bool EligibleBuddy()
    {
        if(!CanSimulate||RootOpened)return false;
        var player=Proof.Player.GetComponent<Rigidbody2D>().position;var front=Proof.Authority.ToWorld(Stage60Layout.RootFront);
        if(Vector2.Distance(player,front)>1.8f||!TileMover.CanTraverseBox(Proof.Authority,player,front,Vector2.one*.5f))return false;
        foreach(var r in Residence.Residents)
        {
            if(!r.Record.Alive||r.Actor==null)continue;var p=r.Actor.GetComponent<Rigidbody2D>().position;
            if(Vector2.Distance(p,player)<=2.5f&&TileMover.CanTraverseBox(Proof.Authority,p,player,Vector2.one*.28f))return true;
        }
        return false;
    }
    public bool TryInteractRoot()
    {
        if(!EligibleBuddy())return false;
        long t=System.Diagnostics.Stopwatch.GetTimestamp();bool opened;using(new ActorTerrainBinding.Scope("SG60.RootOpening"))opened=Session.OpenIsolatedRoot();RootMs=StreamingGeometry.Elapsed(t);
        if(opened){RootActivations++;Status="The roots break. Earth beyond is ordinary diggable dirt. Buddies can start making beds.";}return opened;
    }
    public void AddBuddy(){if(!initialized)return;Residence.Add();Status="A real Buddy has joined this test Camp. No campaign roster changed.";}
    public void FakeReturn(){FakeReturns++;Session.FakeReturn(0,0,0);AddBuddy();Status="Fake Return added one real test Buddy; no run, inventory or save service.";}
    public void ResetTest()
    {
        Initialize();long t=System.Diagnostics.Stopwatch.GetTimestamp();Residence.Dispose();Proof.SetModal(false);Proof.SetPaused(false);
        Proof.Camp.ResetTest(0);Proof.RebindScenario();Bind();Residence=new Stage60Residence(this);
        Proof.Player.GetComponent<Rigidbody2D>().position=Proof.Authority.ToWorld(Session.Definition.Marker("fire").Interaction);Proof.ResetCameraStudy();
        Resets++;RootActivations=0;Status="New test Camp: intact root, no Buddies, no beds.";ResetMs=StreamingGeometry.Elapsed(t);
    }
    public void ReturnToFire()
    {
        var fire=Proof.Authority.ToWorld(Session.Definition.Marker("fire").Interaction);Proof.Player.GetComponent<Rigidbody2D>().position=fire;
        foreach(var r in Residence.Residents)if(r.Actor!=null&&!Residence.Busy){r.Follow.player=Proof.Player.transform;r.Follow.enabled=true;}
    }
    void Update()
    {
        Initialize();Proof.Automated=Automated;
        bool over=!Automated&&ShowPanel&&Mouse.current!=null&&new Rect(8,Screen.height-520,300,512).Contains(Mouse.current.position.ReadValue());
        if(over!=hover){hover=over;if(hover)SporeUiCoordinator.Instance.PushModal(this,null,false);else SporeUiCoordinator.Instance.PopModal(this);}
        if(!Automated&&Keyboard.current!=null&&Keyboard.current.tabKey.wasPressedThisFrame)ShowPanel=!ShowPanel;
        var input=SporeInputReader.Instance;if(input!=null&&input.Context==SporeInputContext.Gameplay&&input.Interact.StartedThisFrame&&Time.timeScale>0){if(!TryInteractRoot()&&RootOpened&&Vector2.Distance(Proof.Player.transform.position,Proof.Authority.ToWorld(Session.Definition.Marker("fire").Interaction))<1.8f){Session.Recover();Residence.CallToFire();Status="Called test Buddies back to the Fire.";}}
        Residence.Tick();
    }
    Mesh MakeMesh(string label,out Material material,out MeshRenderer renderer,int order)
    {
        var go=new GameObject(label);go.transform.SetParent(overlay.transform,false);var mesh=new Mesh{name=label};go.AddComponent<MeshFilter>().sharedMesh=mesh;
        material=new Material(Proof.SpriteShader);renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=order;return mesh;
    }
    void BeginMesh(){vertices.Clear();triangles.Clear();colors.Clear();}
    void Quad(double x,double y,double width,double height,Color color)
    {
        int n=vertices.Count;foreach(var p in new[]{new Point2(x,y),new Point2(x+width,y),new Point2(x+width,y+height),new Point2(x,y+height)}){vertices.Add(Proof.Render(Proof.Authority.ToWorld(p)));colors.Add(QualitySettings.activeColorSpace==ColorSpace.Linear?color.linear:color);}
        triangles.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});
    }
    void RootRibbon(double y,double phase,double width,Color color)
    {
        for(int i=0;i<12;i++)
        {
            double a=i/12.0,b=(i+1)/12.0,x0=101.7+a*7.6,x1=101.7+b*7.6;
            double y0=y+Math.Sin(a*Math.PI*2+phase)*.28,y1=y+Math.Sin(b*Math.PI*2+phase)*.28;
            int n=vertices.Count;foreach(var p in new[]{new Point2(x0,y0-width*.5),new Point2(x1,y1-width*.5),new Point2(x1,y1+width*.5),new Point2(x0,y0+width*.5)}){vertices.Add(Proof.Render(Proof.Authority.ToWorld(p)));colors.Add(QualitySettings.activeColorSpace==ColorSpace.Linear?color.linear:color);}triangles.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});
        }
    }
    void Publish(Mesh mesh){mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();}
    public void Draw()
    {
        if(!initialized||Proof.Live.Snapshot==null||overlay==null)return;
        var palette=Proof.Camp.palette;
        BeginMesh();foreach(var marker in Session.Definition.Markers)if(marker.Id!=Stage60Layout.RootId){var p=marker.Anchor;Quad(p.X-1.4,p.Y-.7,2.8,1.4,palette.Color(Session.Active(marker.Gate)?"BoneLimestone":"RootLight"));var q=marker.Interaction;Quad(q.X-.25,q.Y-.25,.5,.5,palette.Color("FloorDust"));}Publish(stationMesh);
        BeginMesh();if(!RootOpened)
        {
            // Interlocked rooted bands meet the permanent shoulders; terrain underneath owns collision.
            Quad(102.5,94,6,3,palette.Color("RootShadow"));
            for(int i=0;i<6;i++){RootRibbon(94.2+i*.54,i*.9,.68,palette.Color("RootShadow"));RootRibbon(94.15+i*.54,i*.9,.43,palette.Color("RootBase"));RootRibbon(94.05+i*.54,i*.9,.10,palette.Color("RootLight"));}
        }Publish(rootMesh);rootRenderer.enabled=Proof.Live.Ready;
        BeginMesh();if(!RootOpened&&EligibleBuddy())for(int i=0;i<12;i++)Quad(103+i*.36,96.7-Math.Sin(i*.8)*.25,.16,.13,palette.Color("BoneLimestone"));Publish(crackMesh);
        BeginMesh();for(int slot=1;slot<=Session.Established;slot++)
        {var room=Session.Definition.RoomForSlot(slot);var bed=room.Beds[slot-room.FirstSlot];Quad(bed.X-1.1,bed.Y-.7,2.2,1.5,palette.Color("RootShadow"));Quad(bed.X-.95,bed.Y-.55,1.9,1.2,palette.Color("MossStain"));Quad(bed.X-.8,bed.Y-.4,.55,.85,palette.Color("BoneLimestone"));}Publish(bedMesh);
    }
    void OnGUI()
    {
        if(!initialized||Proof.Live.Snapshot==null)return;
        foreach(var marker in Session.Definition.Markers)if(marker.Id!=Stage60Layout.RootId){var label=Proof.Camp.ScreenPoint(marker.Anchor);if(label.x>320&&label.x<Screen.width-120&&label.y>30&&label.y<Screen.height-50)GUI.Box(new Rect(label.x-80,label.y-38,170,25),marker.Id=="fire"?"Campfire":marker.Name);}
        var root=Proof.Camp.ScreenPoint(Stage60Layout.RootFront);bool nearby=Vector2.Distance(Proof.Player.transform.position,Proof.Authority.ToWorld(Stage60Layout.RootFront))<3;
        if(nearby&&!RootOpened)GUI.Box(new Rect(root.x-115,root.y-30,230,28),EligibleBuddy()?SporeInputReader.Instance.GetInteractBindingDisplay()+" — break roots together":"Ancient roots");
        if(RootOpened)foreach(var room in Session.Definition.Bedrooms){var entry=Proof.Camp.ScreenPoint(room.Entrance);if(entry.x>320&&entry.x<Screen.width-180&&entry.y>40&&entry.y<Screen.height-60)GUI.Label(new Rect(entry.x-65,entry.y+20,180,24),room.Id.Replace("dorm.","")+" bedroom pocket");}
        if(Residence.Busy&&Residence.Worker?.Actor!=null)
        {var p=Proof.Camp.ScreenPoint(Proof.Authority.ToLogical(Residence.Worker.Actor.transform.position));GUI.Box(new Rect(p.x-80,p.y-50,160,25),Residence.Phase);}
        // Temporary owned-bed tokens share the actor presentation band, beneath actors and foreground lips.
        // This is not a height-aware furnishing occlusion solution.
        if(RootOpened)for(int slot=1;slot<=Session.Established;slot++)
        {var room=Session.Definition.RoomForSlot(slot);var bed=room.Beds[slot-room.FirstSlot];if(Vector2.Distance(Proof.Player.transform.position,Proof.Authority.ToWorld(bed))>12)continue;var p=Proof.Camp.ScreenPoint(bed);if(p.x>320&&p.x<Screen.width-100&&p.y>50&&p.y<Screen.height-50)GUI.Label(new Rect(p.x-22,p.y+12,70,22),"Bed "+slot);}
        if(!ShowPanel)return;
        GUILayout.BeginArea(new Rect(8,8,300,512),GUI.skin.box);GUILayout.Label("STAGE 6.0 — FIRST BUDDY CAMP");GUILayout.Label("WASD / stick: move   Shift: dig\nF / Interact: nearby roots   Tab: panel");GUILayout.Label("Root Entrance: "+(RootOpened?"Opened":"Intact")+"\nBuddies "+Session.Living+"   Beds "+Session.Established+" / 10");
        GUILayout.BeginHorizontal();if(GUILayout.Button("New Camp"))ResetTest();if(GUILayout.Button("Fake Return"))FakeReturn();GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();if(GUILayout.Button("Add Buddy"))AddBuddy();if(GUILayout.Button("Buddy Died"))Residence.Die();GUILayout.EndHorizontal();
        if(GUILayout.Button("Dig Bedrooms")){if(!Residence.Begin())Status=RootOpened?"A living Buddy needs an unclaimed bed.":"The entrance is still blocked.";}
        GUILayout.Label("Construction Speed");Residence.Speed=GUILayout.Toolbar(Residence.Speed,new[]{"Normal","Fast","Very Fast"});
        if(GUILayout.Button("Finish Current Construction"))Residence.FinishCurrent=true;
        GUILayout.BeginHorizontal();if(GUILayout.Button("Build First Dorm"))Residence.BuildDorm=true;if(GUILayout.Button("Reset Test"))ResetTest();GUILayout.EndHorizontal();
        ShowGuide=GUILayout.Toggle(ShowGuide,"What to Test");Advanced=GUILayout.Toggle(Advanced,"Engineering controls");
        if(Advanced){if(GUILayout.Button("Pre-dig next bed: partial (test control)"))Residence.PreDigNext(.5);if(GUILayout.Button("Pre-dig next bed: fully (test control)"))Residence.PreDigNext(1);if(GUILayout.Button("Cancel current construction"))Residence.Cancel("Test cancellation");if(GUILayout.Button("Fire test position"))ReturnToFire();if(GUILayout.Button("Call Buddies to Fire"))Residence.CallToFire();if(GUILayout.Button("Send housed Buddies home"))foreach(var r in Residence.Residents)Residence.SendHome(r);Proof.ShowOutlines=GUILayout.Toggle(Proof.ShowOutlines,"Projected collision outlines");}
        GUILayout.Label(Residence.Busy?Residence.Phase:Residence.Failure!=""?Residence.Failure:Status);GUILayout.EndArea();
        if(ShowGuide)
        {
            GUI.Box(new Rect(8,530,300,390),"What to Test");GUI.Label(new Rect(20,558,275,350),
             "1. Alone: approach roots above Squad. They stay solid. Failure: pass through.\n\n2. Add Buddy; let it follow you. Interact together: one opening. Failure: opens from afar.\n\n3. Dig Bedrooms: watch travel, Dig, then a bed. Failure: stuck or instant claim.\n\n4. Partly dig next pocket; Buddy finishes remaining dirt.\n\n5. Fully dig next footprint; Buddy still prepares a bed.\n\n6. At Fire, Interact to call Buddies through the entrance.\n\n7. Buddy Died, then Add Buddy: reuse its bed, no duplicate owner.\n\n8. Reset: roots intact, no beds or stale actors.\n\nPause must stop excavation and interaction.");
        }
    }
    void OnDestroy()
    {
        if(hover&&SporeUiCoordinator.Instance!=null)SporeUiCoordinator.Instance.PopModal(this);
        Residence?.Dispose();foreach(var item in new UnityEngine.Object[]{rootMesh,bedMesh,crackMesh,stationMesh,rootMaterial,bedMaterial,crackMaterial,stationMaterial,overlay})if(item!=null)Destroy(item);
    }
}
[DefaultExecutionOrder(2700)]public sealed class Stage60Draw:MonoBehaviour {public Stage60Preview Owner;void LateUpdate()=>Owner.Draw();}
