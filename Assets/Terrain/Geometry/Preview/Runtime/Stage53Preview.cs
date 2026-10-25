using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace SporeGobbo.Geometry.Preview
{
 public static class StructureMesh
 {
  public static void Build(StructurePart part,PreviewMeshData data,int first=0,int last=int.MaxValue,bool shade=false)
  {data.Clear();for(int i=first;i<Math.Min(last,part.Sections.Length-1);i++){var a=part.Sections[i];var b=part.Sections[i+1];var aa=shade?(a.Left+a.Right)*.5:a.Left;var bb=shade?(b.Left+b.Right)*.5:b.Left;var ar=shade?a.Right:(a.Left+a.Right)*.5;var br=shade?b.Right:(b.Left+b.Right)*.5;data.Quad(aa,bb,br,ar);}}
  public static ProjectionBounds Bounds(PreviewMeshData data){double x0=double.MaxValue,y0=x0,x1=double.MinValue,y1=x1;foreach(var v in data.LogicalVertices){x0=Math.Min(x0,v.Logical.X);y0=Math.Min(y0,v.Logical.Y);x1=Math.Max(x1,v.Logical.X);y1=Math.Max(y1,v.Logical.Y);}return new ProjectionBounds(new Point2(x0,y0),new Point2(x1,y1));}
  public static bool Occludes(ProjectionBounds projected,FocusProjection map,Point2 actor,ProjectionBounds body,int mode)=>StructuralPrimitives.Intersects(projected,LabPresentation.BillboardBounds(map,actor,body,mode));
  public static bool GroundVisible(DimensionalLandmark l,CaveDiscovery d)
  {foreach(var c in l.Contacts)for(int y=c.Y-7;y<=c.Y+7;y++)for(int x=c.X-7;x<=c.X+7;x++)if(!d.World.IsSolid(x,y)&&d.Shown(x,y))return true;return false;}
 }
 [DefaultExecutionOrder(1400),RequireComponent(typeof(Stage50Preview))]
 public sealed class Stage53Preview:MonoBehaviour
 {
  public LandmarkExperiment State {get;private set;}public Stage47Preview Live {get;private set;}public Stage49Preview CameraStudy {get;private set;}public Stage50Preview Lab {get;private set;}
  public int anchor,location;public BreakthroughStyle reveal=BreakthroughStyle.Hybrid;public bool debug,advanced,showPanel=true,automation,cutaway=true;public float testingSpeed=10,spikeHeight=24;
  public double InitializationMs,MeshBuildMs,CullingMs,DiscoveryMs,FrontierMs,CutawayMs,NavigationMs;public int StructureBuilds,StructureUploads,VisibleParts,FrontierTiles;public string status="";
  public readonly List<Point2> Route=new List<Point2>();
  readonly PreviewMeshData[] data=new PreviewMeshData[10];readonly MeshFilter[] filters=new MeshFilter[10];readonly MeshRenderer[] renderers=new MeshRenderer[10];readonly bool[] dirty=new bool[10];
  readonly ProjectionBounds[] bounds=new ProjectionBounds[10];readonly HashSet<DualTile> frontierCandidates=new HashSet<DualTile>();readonly List<DualTile> changes=new List<DualTile>();readonly List<DualTile> remove=new List<DualTile>();
  MaterialPropertyBlock block;bool initialized;int lastX=int.MinValue,lastY=int.MinValue,revision=-1;BreakthroughStyle applied;bool lowerShown;Point2 player;Vector2 scroll;
  public int FrontierVertices=>data[8]==null?0:data[8].Vertices.Count;
  public int OwnedMeshes=>initialized?data.Length:0;public int LandmarkChunkCoverage {get;private set;}
  public static readonly float[] Anchors={.62f,.57f,.50f};
  public void Initialize()
  {
   if(initialized)return;initialized=true;Live=GetComponent<Stage47Preview>();Lab=GetComponent<Stage50Preview>();CameraStudy=GetComponent<Stage49Preview>();Lab.Initialize();Lab.EnvironmentalMode=true;Lab.encounterCount=0;Lab.showPanel=Lab.showLabels=Lab.showSafety=Lab.showAttacks=Lab.showFootprints=false;Lab.automation=true;Live.ExternalInputBlock=true;Live.showUI=false;CameraStudy.normalOverlay=CameraStudy.combatOverlay=CameraStudy.rangeOverlay=CameraStudy.cellOverlay=false;CameraStudy.Apply(2);Live.settings.smoothing=.10f;CameraStudy.freezeActors=true;block=new MaterialPropertyBlock();
   for(int i=0;i<data.Length;i++){data[i]=new PreviewMeshData("Stage53 structure batch "+i,PreviewLayer.Gobbo);var go=new GameObject(data[i].Name){hideFlags=HideFlags.DontSave};go.transform.SetParent(transform,false);filters[i]=go.AddComponent<MeshFilter>();filters[i].sharedMesh=new Mesh{name=go.name,hideFlags=HideFlags.DontSave};renderers[i]=go.AddComponent<MeshRenderer>();renderers[i].sharedMaterial=Live.materials[(int)PreviewLayer.Gobbo];renderers[i].sortingOrder=i==8?32:i==9?30:i<4?18:40;renderers[i].shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderers[i].receiveShadows=false;}
   ResetDemo();
  }
  void Start()=>Initialize();
  public void ResetDemo()
  {
   long t=System.Diagnostics.Stopwatch.GetTimestamp();var next=new LandmarkExperiment(Live.worldSize,spikeHeight);var old=State;State=next;Live.ReplacePreviewWorld(next.World,next.Presentation);old?.Dispose();Lab.PresentationAllowed=Allowed;Lab.encounterCount=0;Lab.Restart();frontierCandidates.Clear();data[8].Clear();data[9].Clear();dirty[8]=dirty[9]=true;FrontierTiles=0;lastX=lastY=int.MinValue;revision=-1;lowerShown=false;applied=reveal;State.Presentation.Style=reveal;State.Discovery.ImmediateDistance=reveal==BreakthroughStyle.Hybrid?5:0;BuildStructures();Go(location);InitializationMs=StreamingGeometry.Elapsed(t);status="Isolated world reset. Hidden floor is physical; appearance never changes collision.";
  }
  public void BuildStructures()
  {
   long t=System.Diagnostics.Stopwatch.GetTimestamp();var spike=State.Fixture.Landmarks[0].Parts[0];for(int side=0;side<2;side++){StructureMesh.Build(spike,data[side],0,18,side==1);StructureMesh.Build(spike,data[2+side],18,24,side==1);bounds[side]=bounds[2+side]=spike.Bounds;}
   var root=State.Fixture.Landmarks[1];for(int p=0;p<2;p++)for(int side=0;side<2;side++){int i=4+p*2+side;StructureMesh.Build(root.Parts[p],data[i],0,int.MaxValue,side==1);bounds[i]=root.Parts[p].Bounds;}
   for(int i=0;i<8;i++){bounds[i]=StructureMesh.Bounds(data[i]);dirty[i]=true;}StructureBuilds++;MeshBuildMs=StreamingGeometry.Elapsed(t);var chunks=new HashSet<DualTile>();foreach(var l in State.Fixture.Landmarks)for(int y=l.MinChunk.Y;y<=l.MaxChunk.Y;y++)for(int x=l.MinChunk.X;x<=l.MaxChunk.X;x++)chunks.Add(new DualTile(x,y));LandmarkChunkCoverage=chunks.Count;
  }
  public void SetAnchor(int n){anchor=Mathf.Clamp(n,0,2);Live.settings.anchor=Anchors[anchor];}
  public void Go(int n)
  {location=Mathf.Clamp(n,0,2);var p=State.Fixture.P(location==0?-45:location==1?44:86,location==1?14:24);Live.footprint=1;Live.Teleport(V(p));Live.SetPreviewFocus(V(p));lastX=lastY=int.MinValue;Lab.encounterCount=0;Lab.Restart();CameraStudy.bookmark="Stage 5.3 / "+new[]{"Spike","Roots","Hidden Tunnels"}[location];SetAnchor(anchor);}
  public bool Allowed(Point2 p)=>State.Discovery.ContentAllowed(player,p)&&Live.Stream.IsPresentationCurrent(StreamingCoordinates.Owner((int)p.X,(int)p.Y));
  public void SetReveal(BreakthroughStyle next)
  {
   if(next==applied)return;reveal=applied=next;State.Presentation.Style=next;State.Discovery.ImmediateDistance=next==BreakthroughStyle.Hybrid?5:0;
   changes.Clear();foreach(var key in frontierCandidates)changes.Add(key);Live.Stream.QueuePresentation(changes);BuildFrontier();status="Reveal style applies to future waves. Reset compares identical geography.";
  }
  public void Process(double dt,bool explore=true)
  {
   long t=System.Diagnostics.Stopwatch.GetTimestamp();FrontierMs=0;player=new Point2(Live.Position.x,Live.Position.y);var d=State.Discovery;d.ProcessChanges(player);int x=(int)Math.Floor(player.X),y=(int)Math.Floor(player.Y);if(explore&&(x!=lastX||y!=lastY)){d.Explore(player);lastX=x;lastY=y;}int count=d.Tick(dt);if(count>0){Live.Stream.QueuePresentation(d.Released);UpdateFrontier(d.Released);}
   if(revision!=State.World.Revision){revision=State.World.Revision;changes.Clear();foreach(var cell in frontierCandidates)changes.Add(cell);UpdateFrontier(changes);}
   DiscoveryMs=StreamingGeometry.Elapsed(t);
  }
  void UpdateFrontier(IReadOnlyList<DualTile> cells)
  {
   foreach(var c in cells)for(int y=c.Y-1;y<=c.Y;y++)for(int x=c.X-1;x<=c.X;x++)frontierCandidates.Add(new DualTile(x,y));
   // Remove stale candidates; retain only mixed shown/hidden, physically open tiles.
   remove.Clear();foreach(var c in frontierCandidates)if(!IsFrontier(c.X,c.Y))remove.Add(c);foreach(var c in remove)frontierCandidates.Remove(c);BuildFrontier();
  }
  bool IsFrontier(int x,int y){bool a=false,b=false;for(int j=0;j<2;j++)for(int i=0;i<2;i++){if(State.World.IsSolid(x+i,y+j))return false;if(State.Discovery.Shown(x+i,y+j))a=true;else b=true;}return a&&b;}
  void BuildFrontier()
  {
   long t=System.Diagnostics.Stopwatch.GetTimestamp();data[8].Clear();FrontierTiles=frontierCandidates.Count;
   var caches=new Dictionary<DualTile,ContourTerrainCache>();if(reveal==BreakthroughStyle.Soft)foreach(var c in frontierCandidates){var key=StreamingCoordinates.Owner(c.X,c.Y);if(!caches.TryGetValue(key,out var cache)){cache=new ContourTerrainCache(new ChunkReadWindow(State.Presentation,key));caches.Add(key,cache);}foreach(var strip in cache.GetTile(c.X,c.Y))for(int j=0;j<strip.Samples.Count-1;j++){var a=strip.Samples[j].Logical;var b=strip.Samples[j+1].Logical;Stage50Preview.Stroke(data[8],a,b,.28);Stage50Preview.Stroke(data[8],new Point2(a.X,a.Y-.18),new Point2(b.X,b.Y-.18),.12);}}
   bounds[8]=new ProjectionBounds(new Point2(0,0),new Point2(State.World.Width,State.World.Height));dirty[8]=true;FrontierMs=StreamingGeometry.Elapsed(t);
  }
  public bool TestRoute(int size,bool narrow=true,bool animate=false)
  {
   var f=State.Fixture;var from=f.P(narrow?26:45,14);var to=f.P(narrow?26:45,-14);long t=System.Diagnostics.Stopwatch.GetTimestamp();var nav=new LabNavigation(State.World,from);bool fits=nav.Fits(to,size);bool connected=nav.TraceToRoot(to,size,Route);NavigationMs=StreamingGeometry.Elapsed(t);status=$"Body {size}: destination fits {fits}; full route {connected}; {Route.Count} nodes; {NavigationMs:F2}ms.";
   if(animate&&connected){Live.Teleport(V(from));Lab.encounterCount=1;Lab.footprint=size;Lab.Actors[0].Reset(0,"Root passage / physical route",Route,size,true,LabAttack.ShortMelee);Lab.Actors[0].Kind=size==1?0:1;Lab.Actors[0].State=LabState.Idle;Lab.paused=false;}
   return connected;
  }
  void Update()
  {
   Initialize();SetAnchor(anchor);Live.speed=testingSpeed;Live.showUI=false;Lab.showPanel=false;Live.RightPanelWidth=0;if(automation)return;
   var k=Keyboard.current;var m=Mouse.current;if(k!=null&&k.tabKey.wasPressedThisFrame)showPanel=!showPanel;
   var move=Vector2.zero;if(k!=null)move=new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.sKey.isPressed?1:0)-(k.wKey.isPressed?1:0));bool over=showPanel&&m!=null&&m.position.ReadValue().x<330&&m.position.ReadValue().y>Screen.height-(advanced?730:250);var aim=Live.Aim;
   if(m!=null&&Live.Snapshot!=null&&!over){var q=Live.previewCamera.ScreenToWorldPoint(new Vector3(m.position.ReadValue().x,m.position.ReadValue().y,10));var p=Live.Snapshot.Unproject(new Point2(q.x,-q.y));aim=V(p)-Live.Position;}
   if(Live.Ready)Live.Step(move,aim,!over&&((m!=null&&m.leftButton.isPressed)||(k!=null&&k.spaceKey.isPressed)),Time.deltaTime);SetReveal(reveal);Process(Time.deltaTime);if(Lab.encounterCount>0&&!Lab.paused)Lab.Advance(Math.Min(Time.deltaTime,.1));
  }
  void LateUpdate()
  {
   if(!initialized||Live.Snapshot==null)return;long t=System.Diagnostics.Stopwatch.GetTimestamp();VisibleParts=StructureUploads=0;lowerShown=lowerShown||StructureMesh.GroundVisible(State.Fixture.Landmarks[0],State.Discovery);double w=CameraStudy.DisplayWidth,h=w/(16.0/9);var view=new ProjectionBounds(new Point2(-w*.5,-h*.5),new Point2(w*.5,h*.5));
   for(int i=0;i<9;i++)
   {bool show=Live.Ready&&data[i].Indices.Count>0&&(i>=2||lowerShown);var projected=Live.Snapshot.Project(bounds[i].Min);var max=Live.Snapshot.Project(bounds[i].Max);show&=StructuralPrimitives.Intersects(new ProjectionBounds(projected,max),view);renderers[i].enabled=show;if(!show)continue;if(i<8)VisibleParts++;if(Live.Gpu.Draw(data[i],filters[i],bounds[i],dirty[i])&&i<8)StructureUploads++;dirty[i]=false;
    long ct=System.Diagnostics.Stopwatch.GetTimestamp();float alpha=1;if(i>=4&&i<8&&cutaway){var b=new ProjectionBounds(projected,max);var actor=new Point2(Live.Position.x,Live.Position.y);if(StructureMesh.Occludes(b,Live.Snapshot,actor,Stage50Preview.VisualBounds(actor,1,0),Live.settings.actorMode))alpha=.22f;for(int a=0;a<Lab.encounterCount&&alpha==1;a++){var creature=Lab.Actors[a];if(creature.SpawnValid&&Allowed(creature.Position)&&StructureMesh.Occludes(b,Live.Snapshot,creature.Position,Stage50Preview.VisualBounds(creature.Position,creature.Size*Lab.bodyScale,creature.Kind),Live.settings.actorMode))alpha=.22f;}}
    Tint(i,i==8?new Color(.65f,.80f,.95f,.32f):i%2==0?new Color(.64f,.53f,.35f,alpha):new Color(.38f,.29f,.19f,alpha));CutawayMs=StreamingGeometry.Elapsed(ct);
   }
   CullingMs=StreamingGeometry.Elapsed(t);DrawDebug();
  }
  void DrawDebug()
  {renderers[9].enabled=debug&&Live.Ready;if(!debug)return;data[9].Clear();foreach(var l in State.Fixture.Landmarks)foreach(var c in l.Footprint)Stage50Preview.Stroke(data[9],new Point2(c.X,c.Y+.5),new Point2(c.X+1,c.Y+.5),.1);bounds[9]=new ProjectionBounds(new Point2(0,0),new Point2(State.World.Width,State.World.Height));Live.Gpu.Draw(data[9],filters[9],bounds[9],true);Tint(9,new Color(1,.25f,.6f,.7f));}
  void Tint(int i,Color c){renderers[i].GetPropertyBlock(block);block.SetColor("_Color",c);renderers[i].SetPropertyBlock(block);}
  public static Vector2 V(Point2 p)=>new Vector2((float)p.X,(float)p.Y);
  void OnGUI()
  {
   if(!initialized||!showPanel)return;GUILayout.BeginArea(new Rect(8,8,320,advanced?720:240),GUI.skin.box);scroll=GUILayout.BeginScrollView(scroll);GUILayout.Label("STAGE 5.3 · GEOMETRY & EXPLORATION");GUILayout.Label("WASD · mouse aim · LMB / Space dig · Tab UI");GUILayout.Label("Camera");anchor=GUILayout.Toolbar(anchor,new[]{"Current 62%","57%","Centered"});GUILayout.Label("Location");int next=GUILayout.Toolbar(location,new[]{"Spike","Roots","Hidden Tunnels"});if(next!=location)Go(next);GUILayout.Label("Reveal");reveal=(BreakthroughStyle)GUILayout.Toolbar((int)reveal,new[]{"Original","Hybrid","Soft"});GUILayout.BeginHorizontal();debug=GUILayout.Toggle(debug,"Debug");advanced=GUILayout.Toggle(advanced,"Advanced");if(GUILayout.Button("Reset demo"))ResetDemo();GUILayout.EndHorizontal();
   if(advanced){GUILayout.Label("Test speed: "+testingSpeed.ToString("F0"));testingSpeed=GUILayout.HorizontalSlider(testingSpeed,1,30);GUILayout.Label("Spike height / ordinary wall: "+(spikeHeight/2.15f).ToString("F1")+"x (reset applies)");spikeHeight=GUILayout.HorizontalSlider(spikeHeight,6,40);Live.brush=(DigBrush)GUILayout.Toolbar((int)Live.brush,new[]{"Single","3-wide","Radius"});cutaway=GUILayout.Toggle(cutaway,"Transparent root cutaway near player");Live.showGrid=GUILayout.Toggle(Live.showGrid,"Square logical grid");GUILayout.Label("Physical passage demonstrations (existing actor model)");foreach(int size in new[]{1,3,5,7,9}){GUILayout.BeginHorizontal();if(GUILayout.Button(size+" narrow"))TestRoute(size,true,true);if(GUILayout.Button(size+" wide"))TestRoute(size,false,true);GUILayout.EndHorizontal();}GUILayout.Label(status);GUILayout.Label(CameraStudy.Diagnostics());GUILayout.Label($"Structures {VisibleParts}/8 halves · {LandmarkChunkCoverage} logical chunks\nResident {Live.Stream.Resident.Count}/384 · meshes {OwnedMeshes}\nBuild {MeshBuildMs:F3}ms · cull/draw {CullingMs:F3}ms\nDiscovery {DiscoveryMs:F3}ms · local publication {Live.Stream.PresentationMs:F3}ms\nKnown {State.Discovery.Remembered.Count} · frontier {FrontierTiles}");}GUILayout.EndScrollView();GUILayout.EndArea();
  }
  void OnDestroy(){State?.Dispose();if(Lab!=null)Lab.PresentationAllowed=null;foreach(var f in filters)if(f!=null){if(Live!=null&&Live.Gpu!=null)Live.Gpu.Forget(f);if(Application.isPlaying){Destroy(f.sharedMesh);Destroy(f.gameObject);}else{DestroyImmediate(f.sharedMesh);DestroyImmediate(f.gameObject);}}}
 }
}
