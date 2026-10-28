using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
namespace SporeGobbo.Geometry.Preview
{
 [DefaultExecutionOrder(1350),RequireComponent(typeof(Stage50Preview))]
 public sealed class Stage52Preview:MonoBehaviour
 {
  public DiscoveryExperiment State {get;private set;}public Stage47Preview Live {get;private set;}public Stage50Preview Lab {get;private set;}
  public float testingSpeed=10;public bool showPanel=true,showClues=true,showRegions,showMemory,showCurrentSight,showChunks,showRoof=true,automation;
  public double InitializationMs,DiscoveryMs,PresentationMs;public int DiscoveredUpdates,VisibleActors,DormantActors;public string status="";
  public readonly List<StreamingActor> HiddenActors=new List<StreamingActor>();
  readonly PreviewMeshData[] data=new PreviewMeshData[13];readonly MeshFilter[] filters=new MeshFilter[13];readonly MeshRenderer[] renderers=new MeshRenderer[13];
  readonly HashSet<DiscoveryRegion> visibleRegions=new HashSet<DiscoveryRegion>();readonly List<Point2> route=new List<Point2>();readonly List<DualTile> rubble=new List<DualTile>();
  MaterialPropertyBlock block;bool initialized,labPanel;int observerX=int.MinValue,observerY=int.MinValue;Vector2 scroll;
  public int OwnedMeshes=>initialized?filters.Length:0;public string SavePath=>SporeGobbo.Safety.Persistence.ExperimentPath("Stage52Experiment","discovery.sg52");
  Point2 Player=>new Point2(Live.Position.x,Live.Position.y);static Vector2 V(Point2 p)=>new Vector2((float)p.X,(float)p.Y);
  void Start()=>Initialize();
  public void Initialize()
  {
   if(initialized)return;initialized=true;block=new MaterialPropertyBlock();Live=GetComponent<Stage47Preview>();Lab=GetComponent<Stage50Preview>();Lab.Initialize();Lab.EnvironmentalMode=true;Lab.encounterCount=0;Lab.showPanel=false;Lab.showLabels=false;Lab.showSafety=false;Lab.showAttacks=false;
   for(int i=0;i<data.Length;i++){data[i]=new PreviewMeshData("Stage52 batch "+i,PreviewLayer.Gobbo);var go=new GameObject(data[i].Name){hideFlags=HideFlags.DontSave};go.transform.SetParent(transform,false);filters[i]=go.AddComponent<MeshFilter>();filters[i].sharedMesh=new Mesh{name=go.name,hideFlags=HideFlags.DontSave};renderers[i]=go.AddComponent<MeshRenderer>();renderers[i].sharedMaterial=Live.materials[(int)PreviewLayer.Gobbo];renderers[i].sortingOrder=i<8?11:i==12?40:30;renderers[i].shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderers[i].receiveShadows=false;Tint(i,i<8?new Color(.45f,.85f,1):i==8?new Color(.6f,.46f,.31f):i==9?new Color(1,.4f,.75f):i==10?new Color(.3f,1,.6f):i==12?new Color(.25f,.22f,.17f):new Color(.3f,.9f,1));}
   ResetDemo();
  }
  void Install(DiscoveryExperiment next)
  {
   var old=State;State=next;Live.ReplacePreviewWorld(next.World,next.Presentation);old?.Dispose();HiddenActors.Clear();foreach(int i in new[]{1,2,3,5}){var p=State.Fixture.Centers[i];if(i==5)p=new Point2(p.X+5,p.Y-4);HiddenActors.Add(new StreamingActor(p));}
   Lab.PresentationAllowed=Allowed;observerX=observerY=int.MinValue;Lab.encounterCount=0;Lab.Restart();
  }
  public void ResetDemo()
  {
   long t=System.Diagnostics.Stopwatch.GetTimestamp();var next=new DiscoveryExperiment(Live.worldSize);next.Discovery.SeedKnown(next.Fixture.InitialKnown);double duration=next.Discovery.Duration;next.Discovery.Duration=0;next.Discovery.Explore(next.Fixture.Entry);while(next.Discovery.PendingReleases>0)next.Discovery.Tick(0);next.Discovery.Duration=duration;Install(next);Live.footprint=1;Live.Teleport(V(next.Fixture.Entry));InitializationMs=StreamingGeometry.Elapsed(t);status="Rebuilt deterministic terrain and discovery index; isolated history reset.";
  }
  public void Go(int n){Live.footprint=1;Live.Teleport(V(State.Fixture.Approaches[n]));observerX=observerY=int.MinValue;Lab.Restart();status="Inspection bookmark: "+State.Fixture.Names[n]+". Dig into the nearby seal; this does not reveal the room.";}
  public void Process(double dt,bool explore=true)
  {
   long t=System.Diagnostics.Stopwatch.GetTimestamp();var d=State.Discovery;d.ProcessChanges(Player);int x=(int)Math.Floor(Player.X),y=(int)Math.Floor(Player.Y);
   if(explore&&(x!=observerX||y!=observerY)){d.Explore(Player);observerX=x;observerY=y;}
   if(d.Tick(dt)>0){Live.Stream.QueuePresentation(d.Released);DiscoveredUpdates++;}DiscoveryMs=StreamingGeometry.Elapsed(t);
  }
  public bool Allowed(Point2 p)
  {return State!=null&&State.Discovery.ContentAllowed(Player,p)&&Live.Stream.IsPresentationCurrent(StreamingCoordinates.Owner((int)Math.Floor(p.X),(int)Math.Floor(p.Y)));}
  public int RefillRubble()
  {
   rubble.Clear();rubble.AddRange(State.Fixture.Rubble);foreach(var cell in rubble){var center=new Vector2(cell.X+.5f,cell.Y+.5f);if(Mathf.Abs(Live.Position.x-center.x)<Live.footprint*.5f+.5f&&Mathf.Abs(Live.Position.y-center.y)<Live.footprint*.5f+.5f){status="Move off the designated rubble seal before refilling it.";return 0;}}
   int n=Live.Stream.Refill(rubble);status="Refilled "+n+" designated cells; remembered geography remains.";return n;
  }
  public void SaveState(){Directory.CreateDirectory(Path.GetDirectoryName(SavePath));using(var s=File.Create(SavePath))State.Save(s);status="Saved isolated fixture, edits, rubble state and region-local memory bits.";}
  public void LoadState(){DiscoveryExperiment next;using(var s=File.OpenRead(SavePath))next=DiscoveryExperiment.Load(s);Install(next);if(!PreviewMotion.Fits(State.World,Live.Position,Live.footprint)){Live.footprint=1;Live.Teleport(V(State.Fixture.Entry));}status="Restored discovery memory independently of resident presentation.";}
  public void StartRememberedApproach()
  {
   var center=State.Fixture.Centers[1];var target=new Point2(center.X+8,center.Y);var nav=new LabNavigation(State.World,Player);if(!nav.TraceToRoot(target,1,route)){status="Open the medium chamber entrance before testing this approach.";return;}Lab.encounterCount=1;Lab.footprint=1;Lab.paused=false;Lab.Actors[0].Reset(0,"Existing pooled actor approaching from remembered room",route,1,true,LabAttack.ShortMelee);Lab.Actors[0].State=LabState.Idle;status="Actor follows physical floor independently of discovery; only its visible presentation is gated.";
  }
  void Update()
  {
   Initialize();Live.speed=testingSpeed;if(!automation){var k=Keyboard.current;if(k!=null&&k.f3Key.wasPressedThisFrame)labPanel=!labPanel;Process(Time.deltaTime);}
   Lab.showPanel=labPanel&&Live.showUI;Live.RightPanelWidth=Live.showUI&&(showPanel||labPanel)?390:0;
   DormantActors=0;for(int i=0;i<HiddenActors.Count;i++){HiddenActors[i].Step(Player,Live.LogicalVisible,automation?0:Math.Min(Time.deltaTime,.1),State.World);if(!HiddenActors[i].Active)DormantActors++;}
  }
  void LateUpdate(){if(initialized&&Live.Snapshot!=null)Draw();}
  public void Draw()
  {
   long t=System.Diagnostics.Stopwatch.GetTimestamp();VisibleActors=0;for(int i=0;i<data.Length;i++)data[i].Clear();var r=State.Fixture;double width=Lab.CameraStudy.DisplayWidth,height=width/(16.0/9);
   for(int i=0;i<8;i++)
   {
    if(i>=HiddenActors.Count){renderers[i].enabled=false;continue;}var a=HiddenActors[i];var bounds=Stage50Preview.VisualBounds(a.Position,1,i%3);bool show=Live.Ready&&Allowed(a.Position)&&LabPresentation.Intersects(LabPresentation.BillboardBounds(Live.Snapshot,a.Position,bounds,Live.settings.actorMode),width,height);renderers[i].enabled=show;if(!show)continue;VisibleActors++;Stage50Preview.BuildSilhouette(data[i],a.Position,1,i%3);data[i].VisualActorAnchor=a.Position;Live.Gpu.Draw(data[i],filters[i],bounds,true);
   }
   if(showClues)for(int i=0;i<r.Clues.Count;i++)
   {
    var clue=r.Clues[i];var c=clue.Position;if(LabNavigation.Distance(Player,c)>40)continue;bool nearbyKnown=false;for(int y=(int)c.Y-1;y<=(int)c.Y+1;y++)for(int x=(int)c.X-1;x<=(int)c.X+1;x++)if(!State.World.IsSolid(x,y)&&State.Discovery.Shown(x,y))nearbyKnown=true;if(!nearbyKnown)continue;
    if(clue.Kind==TerrainClueKind.Tint||clue.Kind==TerrainClueKind.Disturbed)data[8].Rectangle(c.X-.7,c.Y-.4,c.X+.7,c.Y+.4);else if(clue.Kind==TerrainClueKind.Crack){Stage50Preview.Stroke(data[8],new Point2(c.X-.5,c.Y-.3),new Point2(c.X+.3,c.Y+.2),.05);Stage50Preview.Stroke(data[8],new Point2(c.X+.3,c.Y+.2),new Point2(c.X+.6,c.Y-.2),.04);}else Stage50Preview.Circle(data[8],c,.45,.035,8);
   }
   visibleRegions.Clear();if(showRegions||showMemory)foreach(var key in Live.Stream.Visible)if(State.Index.ByChunk.TryGetValue(key,out var regions))foreach(var region in regions)visibleRegions.Add(region);
   foreach(var region in visibleRegions)foreach(int cell in region.Cells){int x=cell%State.World.Width,y=cell/State.World.Width;if(!Live.Stream.Visible.Contains(StreamingCoordinates.Owner(x,y)))continue;if(showRegions)Outline(data[9],x+.05,y+.05,x+.95,y+.95,.025);if(showMemory&&State.Discovery.Known(x,y))Outline(data[10],x+.18,y+.18,x+.82,y+.82,.025);}
   if(showChunks)foreach(var key in Live.Stream.Visible){var b=StreamingCoordinates.Bounds(key,State.World.Width,State.World.Height);Outline(data[11],b.Min.X,b.Min.Y,b.Max.X,b.Max.Y,.025);}
   if(showCurrentSight)Stage50Preview.Circle(data[11],Player,32,.04);
   // Overhead visuals cannot disclose hidden floor. This root patch appears only after its supporting geography is shown.
   if(showRoof&&State.Discovery.Shown(r.X+70,r.Y-24))data[12].Rectangle(r.X+64,r.Y-26,r.X+77,r.Y-21);
   var all=new ProjectionBounds(new Point2(0,0),new Point2(State.World.Width,State.World.Height));for(int i=8;i<data.Length;i++){renderers[i].enabled=Live.Ready;Live.Gpu.Draw(data[i],filters[i],all,true);}PresentationMs=StreamingGeometry.Elapsed(t);
  }
  static void Outline(PreviewMeshData d,double x,double y,double z,double w,double t){d.Rectangle(x,y,z,y+t);d.Rectangle(x,w-t,z,w);d.Rectangle(x,y+t,x+t,w-t);d.Rectangle(z-t,y+t,z,w-t);}
  void Tint(int i,Color color){renderers[i].GetPropertyBlock(block);block.SetColor("_Color",color);renderers[i].SetPropertyBlock(block);}
  void OnGUI()
  {
   if(!initialized||!Live.showUI||!showPanel||labPanel)return;var d=State.Discovery;GUILayout.BeginArea(new Rect(Screen.width-382,8,374,Screen.height-16),GUI.skin.box);scroll=GUILayout.BeginScrollView(scroll);GUILayout.Label("STAGE 5.2 - PERSISTENT CAVE DISCOVERY");GUILayout.Label("WASD / mouse + LMB or Space dig / Tab UI / F3 actor lab");GUILayout.Label($"Player speed {testingSpeed:F1} cells/sec");testingSpeed=GUILayout.HorizontalSlider(testingSpeed,1,30);
   for(int n=0;n<State.Fixture.Names.Length;n++)if(GUILayout.Button("Go: "+State.Fixture.Names[n]))Go(n);
   GUILayout.Label("Dig upward for the first three and far-network seals; downward for the others. Single brush demonstrates narrow access. False clues can lead nowhere.");GUILayout.Label("Reveal tuning applies to new discoveries; reset to compare styles.");Live.brush=(DigBrush)GUILayout.Toolbar((int)Live.brush,new[]{"Single","3-wide","Radius"});
   int policy=GUILayout.Toolbar(d.PolicyOverride+1,new[]{"Fixture rules","Immediate","Gradual","Bounded"})-1;d.PolicyOverride=policy;GUILayout.Label("Reveal path distance: "+d.RevealDistance);d.RevealDistance=Mathf.RoundToInt(GUILayout.HorizontalSlider(d.RevealDistance,4,48));GUILayout.Label($"Reveal duration {d.Duration:F2}s");d.Duration=GUILayout.HorizontalSlider((float)d.Duration,0,3);GUILayout.Label("Geometry distance grouping: "+d.Grouping);d.Grouping=Mathf.RoundToInt(GUILayout.HorizontalSlider(d.Grouping,1,12));
   showClues=GUILayout.Toggle(showClues,"Optional terrain clues (no resistance/reward rule)");showRegions=GUILayout.Toggle(showRegions,"DEBUG hidden unit cells (spoils geography)");showMemory=GUILayout.Toggle(showMemory,"DEBUG permanent memory cells");showCurrentSight=GUILayout.Toggle(showCurrentSight,"DEBUG current sensing range (LOS also required)");showChunks=GUILayout.Toggle(showChunks,"Chunk boundaries");showRoof=GUILayout.Toggle(showRoof,"Overhead root demonstration");Live.showGrid=GUILayout.Toggle(Live.showGrid,"Square gameplay grid");
   if(GUILayout.Button("Refill designated rubble entrance"))RefillRubble();if(GUILayout.Button("Existing actor approaches from medium chamber"))StartRememberedApproach();if(GUILayout.Button("Save isolated discovery state"))SaveState();if(GUILayout.Button("Load isolated discovery state")&&File.Exists(SavePath))LoadState();if(GUILayout.Button("Reset current demonstration"))ResetDemo();
   GUILayout.Label($"Initial index {State.Index.Regions.Count:N0} units / {State.Index.CellCount:N0} floor cells\nRemembered {d.Remembered.Count:N0}; visual gates {d.Visual.Count:N0}\nDiscovered stable IDs {d.DiscoveredRegions.Count}; first discoveries {d.DiscoveryEvents}\nQueued visual releases {d.PendingReleases}; local visits {d.LastVisits}\nDiscovery init {InitializationMs:F1}ms; update {DiscoveryMs:F3}ms\nPresentation queue {Live.Stream.PendingPresentation}; rebuild {Live.Stream.PresentationMs:F3}ms\nActors exist {HiddenActors.Count}; dormant {DormantActors}; currently presented {VisibleActors}\nPermanent edits {State.World.EditCount}; open rubble {State.World.TemporaryOpenCells.Count}\nResident {Live.Stream.Resident.Count}/384; owned overlay/actor meshes {OwnedMeshes}\n{status}");GUILayout.EndScrollView();GUILayout.EndArea();
  }
  void OnDestroy(){State?.Dispose();if(Lab!=null)Lab.PresentationAllowed=null;foreach(var f in filters)if(f!=null){if(Live!=null&&Live.Gpu!=null)Live.Gpu.Forget(f);if(Application.isPlaying){Destroy(f.sharedMesh);Destroy(f.gameObject);}else{DestroyImmediate(f.sharedMesh);DestroyImmediate(f.gameObject);}}}
 }
}
