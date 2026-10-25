using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace SporeGobbo.Geometry.Preview
{
 [DefaultExecutionOrder(1350),RequireComponent(typeof(Stage50Preview))]
 public sealed class Stage51Preview:MonoBehaviour
 {
  public EnvironmentState State {get;private set;}public readonly EnvironmentTransfer Transfer=new EnvironmentTransfer();
  public Stage47Preview Live {get;private set;}public Stage50Preview Lab {get;private set;}
  public float testingSpeed=10;
  public bool cutaway=true,gradualCutaway=true,showOccupancy,showProtected,showCoverMask,showDiscovery,showLinks=true,showChunks,showPanel=true,automation;
  public double DiscoveryMs,CoverMs,PreparationMs;public string status="";public int ReadyTransfers,CancelledTransfers;public bool Loading=>Transfer.Phase==TransferPhase.Preparing;
  public bool ChamberVisible=>State!=null&&Live.Snapshot!=null&&State.CurrentlyVisible(Player)&&LabPresentation.Intersects(Live.Snapshot.ConservativeBounds(new ProjectionBounds(new Point2(State.Region.X+33,State.Region.Y-27),new Point2(State.Region.X+51,State.Region.Y-10)),2.15,.22),Lab.CameraStudy.DisplayWidth,Lab.CameraStudy.DisplayWidth/(16.0/9));
  readonly PreviewMeshData[] data=new PreviewMeshData[7];readonly MeshFilter[] filters=new MeshFilter[7];readonly MeshRenderer[] renderers=new MeshRenderer[7];
  readonly Color[] colors={new Color(.73f,.64f,.42f),new Color(.26f,.23f,.18f),new Color(.26f,.23f,.18f,.15f),new Color(.15f,.12f,.1f),new Color(.2f,.9f,.9f),new Color(1,.3f,.7f),new Color(.9f,.8f,.25f)};
  MaterialPropertyBlock tintBlock;
  bool initialized,previousFreeze;int edits;float coverBlend;Vector2 source,scroll;long preparingStart;
  public string StatePath=>Path.Combine(Application.persistentDataPath,"Stage51Experiment","environment.sg51");public int OwnedMeshes=>initialized?filters.Length:0;
  void Start()=>Initialize();
  public void Initialize()
  {
   if(initialized)return;initialized=true;tintBlock=new MaterialPropertyBlock();Live=GetComponent<Stage47Preview>();Lab=GetComponent<Stage50Preview>();Lab.Initialize();
   colors[3]=Live.materials[(int)PreviewLayer.Solid].GetColor("_Color");colors[3].a=1;
   Lab.encounterCount=0;Lab.showPanel=false;Lab.showLabels=false;Lab.showSafety=false;Lab.showAttacks=false;Lab.EnvironmentalMode=true;
   for(int i=0;i<data.Length;i++){data[i]=new PreviewMeshData("Stage51 batch "+i,PreviewLayer.Solid);var go=new GameObject(data[i].Name){hideFlags=HideFlags.DontSave};go.transform.SetParent(transform,false);filters[i]=go.AddComponent<MeshFilter>();filters[i].sharedMesh=new Mesh{name=data[i].Name,hideFlags=HideFlags.DontSave};renderers[i]=go.AddComponent<MeshRenderer>();renderers[i].sharedMaterial=Live.materials[(int)PreviewLayer.Gobbo];renderers[i].sortingOrder=i==0?2:i<=2?40:i==3?50:35;renderers[i].shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;Tint(i,colors[i]);}
   ResetDemo();
  }
  static Vector2 V(Point2 p)=>new Vector2((float)p.X,(float)p.Y);Point2 Player=>new Point2(Live.Position.x,Live.Position.y);
  void Tint(int i,Color c){renderers[i].GetPropertyBlock(tintBlock);tintBlock.SetColor("_Color",c);renderers[i].SetPropertyBlock(tintBlock);}
  public void ResetDemo(){CancelTravel();State=new EnvironmentState(Live.worldSize);Install();Live.footprint=1;Live.Teleport(V(State.Region.Entry));Lab.Restart();status="Deterministic Stage51 fixture reset; production state untouched.";}
  void Install(){Live.ReplacePreviewWorld(State.World);edits=State.World.EditCount;coverBlend=0;BuildLandmark();Lab.PresentationAllowed=p=>State.Discovered||!State.Region.InRoom(p);}
  sealed class ProtectedSource:IOccupancySource{public EnvironmentRegion Region;public int Width{get;set;}public int Height{get;set;}public bool IsSolid(int x,int y)=>Region.Protected(x,y);}
  void BuildLandmark()
  {
   var d=data[0];d.Clear();var s=new ProtectedSource{Region=State.Region,Width=State.World.Width,Height=State.World.Height};var cache=new ContourTerrainCache(s);
   // Canonical fill template; boundary anchors remain exactly those used by terrain, no separate atlas.
   for(int y=State.Region.Y-16;y<State.Region.Y+16;y+=16)for(int x=State.Region.X-32;x<State.Region.X+32;x+=16){var layers=PreviewMeshBuilder.BuildPatch(s,cache,x,y,16);var fill=layers[0];for(int k=0;k<fill.Indices.Count;k+=3)d.Triangle(fill.LogicalVertices[fill.Indices[k]].Logical,fill.LogicalVertices[fill.Indices[k+1]].Logical,fill.LogicalVertices[fill.Indices[k+2]].Logical);}
  }
  public void NotifyExcavation(){long t=System.Diagnostics.Stopwatch.GetTimestamp();State.OnExcavation();DiscoveryMs=StreamingGeometry.Elapsed(t);edits=State.World.EditCount;}
  public bool BeginTravel()
  {
   if(!Transfer.Begin(State.Link,Player,TraversalAccess.Player,p=>PreviewMotion.Fits(State.World,V(p),Live.footprint))){status="Travel refused: step near a marker, enable/unlock the link, and keep arrival clear.";return false;}
   source=Live.Position;previousFreeze=Lab.CameraStudy.freezeFocus;Lab.CameraStudy.freezeFocus=true;Live.ExternalInputBlock=Live.ExternalPresentationBlock=true;Live.SetPreviewFocus(V(Transfer.Arrival));preparingStart=System.Diagnostics.Stopwatch.GetTimestamp();status="Preparing destination viewport; Escape cancels.";return true;
  }
  public void CancelTravel(){if(!Loading)return;Transfer.Cancel();CancelledTransfers++;Live.SetPreviewFocus(source);Live.ExternalInputBlock=Live.ExternalPresentationBlock=false;Lab.CameraStudy.freezeFocus=previousFreeze;status="Preparation cancelled; player remains at source.";}
  public void InteractSwitch(){if(LabNavigation.Distance(Player,State.Link.Source)>3){status="Step near the source doorway to use its access switch.";return;}State.Link.Unlocked=!State.Link.Unlocked;status="Local source access switch toggled.";}
  public void SaveState(){Directory.CreateDirectory(Path.GetDirectoryName(StatePath));using(var s=File.Create(StatePath))State.Save(s);status="Saved isolated fixture identity + excavation and environment state.";}
  public void LoadState(){CancelTravel();EnvironmentState next;using(var s=File.OpenRead(StatePath))next=EnvironmentState.Load(s);State=next;Install();if(!PreviewMotion.Fits(State.World,Live.Position,Live.footprint)){Live.footprint=1;Live.Teleport(V(State.Region.Entry));}Lab.Restart();status="Reloaded isolated state; render caches reconstruct from authority.";}
  void Update()
  {
   Initialize();Live.speed=testingSpeed;if(edits!=State.World.EditCount)NotifyExcavation();State.Step(Time.deltaTime);
   bool underneath=State.Region.UnderCover(Player);coverBlend=gradualCutaway?Mathf.MoveTowards(coverBlend,underneath?1:0,Time.deltaTime/.4f):underneath?1:0;
   if(!automation){var k=Keyboard.current;if(k!=null){if(k.eKey.wasPressedThisFrame&&!Loading)BeginTravel();if(k.rKey.wasPressedThisFrame)InteractSwitch();if(k.escapeKey.wasPressedThisFrame)CancelTravel();if(k.f3Key.wasPressedThisFrame){Lab.showPanel=!Lab.showPanel;showPanel=!Lab.showPanel;}}}
   if(!Lab.showPanel)Live.RightPanelWidth=showPanel&&Live.showUI?390:0;
  }
  void LateUpdate()=>Present();
  public void Present()
  {
   if(!initialized||Live.Snapshot==null)return;
   if(Loading)
   {
    bool ready=Live.Ready&&Math.Abs(Live.Snapshot.Focus.X-(Transfer.Arrival.X+Lab.CameraStudy.horizontalFocusOffset))<.001&&Math.Abs(Live.Snapshot.Focus.Y-Transfer.Arrival.Y)<.001;
    if(Transfer.Commit(Transfer.Ticket,ready,p=>PreviewMotion.Fits(State.World,V(p),Live.footprint),(p,f)=>{Live.Teleport(V(p));Live.Aim=V(f);Lab.Restart();}))
    {PreparationMs=StreamingGeometry.Elapsed(preparingStart);ReadyTransfers++;Live.ExternalInputBlock=Live.ExternalPresentationBlock=false;Lab.CameraStudy.freezeFocus=previousFreeze;status="Arrived once, viewport complete; ordinary streaming resumed.";}
    else if(Transfer.Phase==TransferPhase.Cancelled){Live.SetPreviewFocus(source);Live.ExternalInputBlock=Live.ExternalPresentationBlock=false;Lab.CameraStudy.freezeFocus=previousFreeze;}
   }
   Draw();
  }
  public void Draw()
  {
   long t=System.Diagnostics.Stopwatch.GetTimestamp();for(int i=1;i<data.Length;i++)data[i].Clear();var r=State.Region;
   // Continuous cover surface subdivided only to support a local cutaway. Two batches, no cell objects.
   for(int y=r.Y+19;y<r.Y+28;y++)for(int x=r.X-30;x<r.X+6;x++)
   {float a=EnvironmentState.CoverOpacity(new Point2(x+.5,y+.5),Player,cutaway,coverBlend);data[a<.999f?2:1].Rectangle(x,y,x+1,y+1);if(showCoverMask)Outline(data[4],x,y,x+1,y+1,.025);}
   Tint(2,new Color(colors[2].r,colors[2].g,colors[2].b,1-.85f*coverBlend));
   if(!State.Discovered||State.Reveal<1){data[3].Rectangle(r.X+30.5,r.Y-30,r.X+54,r.Y-7);Tint(3,new Color(colors[3].r,colors[3].g,colors[3].b,(float)(1-State.Reveal)));if(!State.Discovered)Tint(3,colors[3]);}
   else {data[6].Rectangle(r.Chamber.X-1,r.Chamber.Y-1,r.Chamber.X+1,r.Chamber.Y+1);}
   if(showDiscovery)Outline(data[5],r.X+33,r.Y-27,r.X+51,r.Y-10,.08);
   if(showLinks){Stage50Preview.Circle(data[4],State.Link.Source,1.1,.12,24);Stage50Preview.Circle(data[4],State.Link.Destination,1.1,.12,24);}
   if(showProtected||showOccupancy)for(int y=r.Y-30;y<=r.Y+30;y++)for(int x=r.X-45;x<=r.X+54;x++){bool blocked=State.World.IsSolid(x,y);if(showProtected&&r.Protected(x,y))Outline(data[5],x+.03,y+.03,x+.97,y+.97,.04);if(showOccupancy&&blocked)Outline(data[6],x+.15,y+.15,x+.85,y+.85,.02);}
   if(showChunks)foreach(var key in Live.Stream.Visible){var b=StreamingCoordinates.Bounds(key,State.World.Width,State.World.Height);Outline(data[4],b.Min.X,b.Min.Y,b.Max.X,b.Max.Y,.03);}
   var bounds=new ProjectionBounds(new Point2(r.X-50,r.Y-40),new Point2(r.X+60,r.Y+40));
   for(int i=0;i<data.Length;i++){renderers[i].enabled=Live.Ready&&!Loading;var b=i==4?new ProjectionBounds(new Point2(0,0),new Point2(State.World.Width,State.World.Height)):bounds;Live.Gpu.Draw(data[i],filters[i],b,i!=0);}
   CoverMs=StreamingGeometry.Elapsed(t);
  }
  static void Outline(PreviewMeshData d,double x,double y,double z,double w,double t){d.Rectangle(x,y,z,y+t);d.Rectangle(x,w-t,z,w);d.Rectangle(x,y+t,x+t,w-t);d.Rectangle(z-t,y+t,z,w-t);}
  void OnGUI()
  {
   if(!initialized)return;if(Loading){GUI.Box(new Rect(Screen.width/2-210,Screen.height/2-45,420,90),"Preparing special traversal destination\nNo player transfer until the expanded view is complete.\nEscape: cancel");return;}
   if(!showPanel||!Live.showUI||Lab.showPanel)return;
   GUILayout.BeginArea(new Rect(Screen.width-382,8,374,Screen.height-16),GUI.skin.box);scroll=GUILayout.BeginScrollView(scroll);GUILayout.Label("STAGE 5.1 â€” ENVIRONMENT PROOF");GUILayout.Label("WASD / mouse + LMB or Space dig / E traverse\nTab UI / F3 Stage5.0 actor lab / Escape cancel");
   GUILayout.Label("Fossil: protected irregular cells. Beige is material.\nArch: walkable beneath dark overhead.\nChamber: dig east through the 3-cell seal.\nCyan markers: isolated nonadjacent logical link.");
   GUILayout.Label($"Player speed: {testingSpeed:F1} cells/sec (original: 5)");testingSpeed=GUILayout.HorizontalSlider(testingSpeed,1,30);
   if(GUILayout.Button("Go: fossil / large actor route"))Go(r=>r.Entry);
   if(GUILayout.Button("Go: sealed chamber entrance"))Go(r=>r.P(29,-18));
   if(GUILayout.Button("Go: covered passage entrance"))Go(r=>r.CoverEntry);
   if(GUILayout.Button("Go: beneath cover (already open floor)"))Go(r=>r.P(-18,23));
   if(GUILayout.Button("Go: source link"))Go(r=>r.P(-35,3));
   if(GUILayout.Button("Use nearby special connection [E]"))BeginTravel();
   State.Link.Enabled=GUILayout.Toggle(State.Link.Enabled,"Connection enabled");if(GUILayout.Button("Toggle nearby source access switch [R]"))InteractSwitch();GUILayout.Label("Access switch: "+(State.Link.Unlocked?"unlocked":"locked"));State.Link.Bidirectional=GUILayout.Toggle(State.Link.Bidirectional,"Bidirectional (off = source only)");
   cutaway=GUILayout.Toggle(cutaway,"Local cover cutaway (off: obscure player)");gradualCutaway=GUILayout.Toggle(gradualCutaway,"Gradual local cutaway (.4s)");State.Gradual=GUILayout.Toggle(State.Gradual,"Gradual chamber reveal (.65s; collision immediate)");
   showOccupancy=GUILayout.Toggle(showOccupancy,"Debug blocked occupancy");showProtected=GUILayout.Toggle(showProtected,"Debug protected footprint");showCoverMask=GUILayout.Toggle(showCoverMask,"Debug cover cells (no collision)");showDiscovery=GUILayout.Toggle(showDiscovery,"Debug concealed bounds (explicit reveal aid)");showLinks=GUILayout.Toggle(showLinks,"Traversal markers");showChunks=GUILayout.Toggle(showChunks,"Chunk boundaries");Live.showGrid=GUILayout.Toggle(Live.showGrid,"Uniform square grid");
   if(GUILayout.Button("Stage5.0 actors: 7-cell around fossil")){Go(r=>r.Entry);Lab.encounterCount=4;Lab.footprint=7;Lab.showPaths=true;Lab.Restart();}
   if(GUILayout.Button("Stop actor demonstration")){Lab.encounterCount=0;Lab.showPaths=false;}
   if(GUILayout.Button("Save isolated .sg51 state"))SaveState();if(GUILayout.Button("Load isolated state")&&File.Exists(StatePath))LoadState();if(GUILayout.Button("Reset current demonstration only"))ResetDemo();
   GUILayout.Label($"Discovered {State.Discovered}; currently visible {ChamberVisible}; reveal {State.Reveal:F2}\nCover entrance open {State.CoverOpened}\nDiscovery local visits {State.DiscoveryVisits}; {DiscoveryMs:F3}ms\nCover + debug batches {CoverMs:F3}ms\nTransfer {Transfer.Phase}; completed {ReadyTransfers}; cancelled {CancelledTransfers}; prep {PreparationMs:F1}ms\nTerrain residents {Live.Stream.Resident.Count}/{Live.Stream.Capacity}; edits {State.World.EditCount}\nOwned environment meshes {OwnedMeshes}\n{status}");GUILayout.EndScrollView();GUILayout.EndArea();
  }
  public void Go(Func<EnvironmentRegion,Point2> target){CancelTravel();Live.footprint=1;Live.Teleport(V(target(State.Region)));Lab.Restart();}
  void OnDestroy(){if(Live!=null){Live.ExternalInputBlock=Live.ExternalPresentationBlock=false;}if(Lab!=null)Lab.PresentationAllowed=null;foreach(var f in filters)if(f!=null){if(Live!=null&&Live.Gpu!=null)Live.Gpu.Forget(f);if(Application.isPlaying){Destroy(f.sharedMesh);Destroy(f.gameObject);}else{DestroyImmediate(f.sharedMesh);DestroyImmediate(f.gameObject);}}}
 }
}
