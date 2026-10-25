using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using UnityEngine;
namespace SporeGobbo.Geometry.Preview
{
 [DefaultExecutionOrder(1300),RequireComponent(typeof(Stage49Preview))]
 public sealed class Stage50Preview:MonoBehaviour
 {
  [NonSerialized] public bool EnvironmentalMode;
  public Func<Point2,bool> PresentationAllowed;
  public const int Capacity=32;public static readonly string[] CreatureNames={"Gobbo","Buddy","Weevil","Boss-like"};public readonly LabTuning tuning=new LabTuning();public readonly LabActor[] Actors=new LabActor[Capacity];
  public readonly List<Point2> guidanceRoute=new List<Point2>();readonly List<Point2> scratch=new List<Point2>();readonly List<Point2> eventRoute=new List<Point2>(),visitedRoute=new List<Point2>(),trackedRoute=new List<Point2>();
  public Stage47Preview Live {get;private set;}public Stage49Preview CameraStudy {get;private set;}public LabNavigation Navigation {get;private set;}
  public int encounterCount=8,selected,footprint=1,scaleMode,objective,guidanceMode,location;public bool paused,showPanel=true,showLabels=true,showFootprints=true,showVisualBounds,showPaths,showDetection,showPursuit,showSearch,showAttacks=true,showSafety=true,showSizes,xray,onlySelected,decorations=true;
  public float timeScale=1,safetyMargin=3,bodyScale=1;public double Clock,SimulationMs,VisualMs,NavigationMs,LastDt;public readonly Vector2[] PixelSizes=new Vector2[Capacity];public readonly double[] ProjectedSpeeds=new double[Capacity];public int Active,Dormant,Visible,IndicatorVertices,ActorVertices,NavigationBuilds;public bool automation;
  public string status="";public Point2 LabRoot,Target;public LabVisibility TargetVisibility;public readonly int[] Sizes={1,2,3,5,7,9,11};
  readonly PreviewMeshData[] data=new PreviewMeshData[Capacity+14];readonly MeshFilter[] filters=new MeshFilter[Capacity+14];readonly MeshRenderer[] renderers=new MeshRenderer[Capacity+14];MaterialPropertyBlock block;
  bool initialized;int edits,playerSize;double replanCooldown;Vector2 scroll;readonly Point2[] desired=new Point2[Capacity];readonly string[] approach={"Above","Below","Left","Right","Upper-left","Upper-right","Winding route","Long passage"};
  public int OwnedMeshes=>initialized?filters.Length:0;public long NavigationBytes=>Navigation==null?0:Navigation.PayloadBytes;
  public static readonly string[] ObjectiveNames={"Offscreen event","Visited tunnel","Tracked creature","Around cave corner"};
  void Start(){Initialize();}
  public void Initialize()
  {
   if(initialized)return;initialized=true;block=new MaterialPropertyBlock();Live=GetComponent<Stage47Preview>();CameraStudy=GetComponent<Stage49Preview>();Live.Initialize();Live.Actors.Clear();CameraStudy.freezeActors=true;
   for(int i=0;i<Actors.Length;i++)Actors[i]=new LabActor();
   Color[] colors={Color.white,new Color(.3f,1,.6f),new Color(.3f,.7f,1),new Color(1,.3f,.25f),new Color(1,.85f,.25f),new Color(.7f,.45f,1),new Color(.9f,.9f,.9f)};
   for(int i=0;i<filters.Length;i++)
   {
    data[i]=new PreviewMeshData("Stage50 "+i,PreviewLayer.Gobbo);var go=new GameObject(data[i].Name){hideFlags=HideFlags.DontSave};go.transform.SetParent(transform,false);var filter=go.AddComponent<MeshFilter>();filter.sharedMesh=new Mesh{name=data[i].Name,hideFlags=HideFlags.DontSave};filter.sharedMesh.MarkDynamic();filters[i]=filter;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=Live.materials[(int)PreviewLayer.Gobbo];renderer.sortingOrder=i<Capacity||i>=Capacity+7?11:28+i-Capacity;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;renderers[i]=renderer;go.transform.localPosition=new Vector3(0,0,i<Capacity||i>=Capacity+7?-.01f:0);Tint(i,i<Capacity?Color.white:colors[(i-Capacity)%7]);
   }
   Restart();
  }
  public void Restart()
  {
   Initialize();Clock=0;selected=Mathf.Clamp(selected,0,Math.Max(0,encounterCount-1));LabRoot=new Point2(Live.Position.x,Live.Position.y);Repath(true);status="Reset actors and shared logical routes; terrain and player unchanged.";
  }
  public void SetLocation(int n){Initialize();location=n;CameraStudy.Go(n);Restart();}
  void Repath(bool reset)
  {
   long start=System.Diagnostics.Stopwatch.GetTimestamp();Navigation=new LabNavigation(Live.World,new Point2(Live.Position.x,Live.Position.y));NavigationBuilds++;edits=Live.World.Revision;playerSize=footprint;replanCooldown=2;
   var p=Navigation.Root;Point2[] offsets={new Point2(0,-55),new Point2(0,30),new Point2(-55,0),new Point2(55,0),new Point2(-38,-34),new Point2(38,-34),new Point2(-38,-20),new Point2(90,0)};
   for(int i=0;i<Mathf.Clamp(encounterCount,1,Capacity);i++)
   {
    var a=Actors[i];if(!reset&&(a.State==LabState.Searching||a.State==LabState.Disengaged||a.State==LabState.Pursuing&&!LabNavigation.Contact(Live.World,a.Position,p)))continue;desired[i]=new Point2(p.X+offsets[i%8].X,p.Y+offsets[i%8].Y);if(reset&&i>=8){scratch.Clear();scratch.AddRange(Actors[i%8].Route);}else if(reset||!Navigation.TraceToRoot(a.Position,footprint,scratch))Navigation.Route(reset?desired[i]:a.Position,footprint,scratch);if(reset&&i==6)Navigation.Winding(footprint,scratch);
    if(reset){a.Reset(i,approach[i%8]+" / "+CreatureNames[i%4]+(i%2==0?" · short":" · persistent"),scratch,footprint,i%2!=0,(LabAttack)(i%4));a.Kind=i%4;}
    else{a.Route.Clear();a.Route.AddRange(scratch);a.RouteIndex=0;}
   }
   Navigation.Route(new Point2(LabRoot.X+90,LabRoot.Y),1,eventRoute);Navigation.Route(new Point2(LabRoot.X-40,LabRoot.Y),1,visitedRoute);Navigation.Corner(1,guidanceRoute);NavigationMs=StreamingGeometry.Elapsed(start);
  }
  void Update()
  {
   if(!initialized)Initialize();var keys=UnityEngine.InputSystem.Keyboard.current;if(!automation&&keys!=null){if(keys.tabKey.wasPressedThisFrame)showPanel=Live.showUI;if(keys.f2Key.wasPressedThisFrame)showPanel=!showPanel;}Live.RightPanelWidth=showPanel?390:0;Live.settings.actorMode=scaleMode;if(!automation)Advance(Math.Min(Time.deltaTime,.1));
  }
  public void Advance(double dt)
  {
   Initialize();LastDt=paused?0:dt*timeScale;Live.settings.actorMode=scaleMode;long start=System.Diagnostics.Stopwatch.GetTimestamp();if(!paused){dt*=Math.Max(0,timeScale);Clock+=dt;replanCooldown-=dt;if(replanCooldown<=0&&(playerSize!=footprint||edits!=Live.World.Revision||LabNavigation.Distance(Navigation.Root,new Point2(Live.Position.x,Live.Position.y))>6))Repath(false);for(int i=0;i<encounterCount;i++)Actors[i].Step(dt,new Point2(Live.Position.x,Live.Position.y),tuning,Live.World);}
   Active=Dormant=0;for(int i=0;i<encounterCount;i++){Actors[i].Distance=LabNavigation.Distance(Actors[i].Position,new Point2(Live.Position.x,Live.Position.y));if(Actors[i].Active)Active++;else Dormant++;}SimulationMs=StreamingGeometry.Elapsed(start);
  }
  void LateUpdate(){if(initialized&&Live.Snapshot!=null)Draw();}
  public void Draw()
  {
   long start=System.Diagnostics.Stopwatch.GetTimestamp();Active=Dormant=0;for(int i=0;i<encounterCount;i++){var actor=Actors[i];actor.Distance=LabNavigation.Distance(actor.Position,new Point2(Live.Position.x,Live.Position.y));actor.Active=actor.SpawnValid&&(actor.Distance<=64||actor.State!=LabState.Idle&&actor.State!=LabState.Disengaged);if(actor.Active)Active++;else Dormant++;}ActorVertices=IndicatorVertices=Visible=0;for(int i=0;i<Capacity;i++){PixelSizes[i]=Vector2.zero;ProjectedSpeeds[i]=0;}displayBoxes.Clear();for(int i=Capacity;i<data.Length;i++)data[i].Clear();
   var p=Live.Snapshot;var player=new Point2(Live.Position.x,Live.Position.y);double w=CameraStudy.DisplayWidth,h=w/(16.0/9);
   for(int i=0;i<Capacity;i++)
   {
    var a=Actors[i];bool show=i<encounterCount&&a.SpawnValid&&(!onlySelected||i==selected);var bodyBounds=VisualBounds(a.Position,a.Size*bodyScale,a.Kind,decorations);var projectedBody=LabPresentation.BillboardBounds(p,a.Position,bodyBounds,scaleMode);
    show=show&&(PresentationAllowed==null||PresentationAllowed(a.Position))&&LabPresentation.Intersects(projectedBody,w,h)&&(xray||LabNavigation.Contact(Live.World,player,a.Position));renderers[i].enabled=show;if(!show)continue;Visible++;filters[i].transform.localPosition=new Vector3(0,0,-.01f-(float)(a.Position.Y/Live.World.Height)*.001f);var d=data[i];d.Clear();BuildSilhouette(d,a.Position,a.Size*bodyScale,a.Kind,decorations);d.VisualActorAnchor=a.Position;
    Tint(i,a.State==LabState.Telegraphing?new Color(1,.25f,.2f):a.Persistent?new Color(1,.7f,.25f):new Color(.4f,.85f,1));
    double s=a.Size*bodyScale;var bounds=VisualBounds(a.Position,s,a.Kind,decorations);var localScale=LabPresentation.Scale(p,a.Position,scaleMode);PixelSizes[i]=new Vector2((float)((bounds.Max.X-bounds.Min.X)*localScale.x*1920/w),(float)((bounds.Max.Y-bounds.Min.Y)*localScale.y*1920/w));ProjectedSpeeds[i]=LastDt>0?LabNavigation.Distance(p.Project(a.Previous),p.Project(a.Position))*1920/w/LastDt:0;Live.Gpu.Draw(d,filters[i],bounds,true);ActorVertices+=d.Vertices.Count;
    if(showFootprints)Box(data[Capacity],new ProjectionBounds(new Point2(a.Position.X-a.Size*.5,a.Position.Y-a.Size*.5),new Point2(a.Position.X+a.Size*.5,a.Position.Y+a.Size*.5)),.035);
    if(showVisualBounds){var scale=LabPresentation.Scale(p,a.Position,scaleMode);var anchor=p.Project(a.Position);var b=new ProjectionBounds(new Point2(anchor.X+(bounds.Min.X-a.Position.X)*scale.x,anchor.Y+(bounds.Min.Y-a.Position.Y)*scale.y),new Point2(anchor.X+(bounds.Max.X-a.Position.X)*scale.x,anchor.Y+(bounds.Max.Y-a.Position.Y)*scale.y));DrawDisplayBox(b,Color.magenta);}
   }
   if(encounterCount>0)
   {
    var a=Actors[Mathf.Clamp(selected,0,encounterCount-1)];if(a.SpawnValid){if(showDetection)Circle(data[Capacity+1],a.Position,tuning.detection,.025);if(showPursuit)Circle(data[Capacity+2],a.Position,tuning.pursuit,.025);if(showSearch)Circle(data[Capacity+5],a.Position,tuning.searchDistance,.025);}
   }
   for(int i=0;i<encounterCount;i++)
   {
    var a=Actors[i];if(!a.SpawnValid||onlySelected&&i!=selected||PresentationAllowed!=null&&!PresentationAllowed(a.Position))continue;if(showPaths)for(int j=Math.Max(1,a.RouteIndex);j<a.Route.Count;j++)Stroke(data[Capacity+4],a.Route[j-1],a.Route[j],.035);
    if(showAttacks&&a.State==LabState.Telegraphing)
    {
     double r=tuning.Reach(a.Attack);if(a.Attack==LabAttack.WideArea)Circle(data[Capacity+3],a.Position,r,.08);else{var end=new Point2(a.Position.X+a.AttackDirection.X*r,a.Position.Y+a.AttackDirection.Y*r);Stroke(data[Capacity+3],a.Position,end,a.Attack==LabAttack.Projectile?.08:.3);Circle(data[Capacity+3],end,.25,.07);}
    }
    if(a.Shot!=null&&!a.Shot.Finished){var q=a.Shot.Position;Circle(data[Capacity+3],q,.18,.1);}
   }
   if(showSafety)Circle(data[Capacity+5],player,tuning.Reach(Actors[Mathf.Clamp(selected,0,encounterCount-1)].Attack)+safetyMargin,.04);
   Target=objective==0?(eventRoute.Count>0?eventRoute[0]:LabRoot):objective==1?(visitedRoute.Count>0?visitedRoute[0]:LabRoot):objective==2&&encounterCount>0?Actors[0].Position:guidanceRoute.Count>0?guidanceRoute[0]:LabRoot;
   if(objective==2&&!Actors[0].SpawnValid)Target=LabRoot;
   TargetVisibility=LabPresentation.Visibility(p,player,Target,w,h,Live.World);
   if(guidanceMode>0){if(objective==2)Navigation.TraceToRoot(Actors[0].Position,1,trackedRoute);var route=objective==0?eventRoute:objective==1?visitedRoute:objective==3?guidanceRoute:trackedRoute;for(int j=1;j<route.Count;j++){if(guidanceMode==1)Stroke(data[Capacity+4],route[j-1],route[j],.06);else if(j%3==0&&LabNavigation.Contact(Live.World,player,route[j]))Circle(data[Capacity+1],route[j],.11,.055,8);}}
   for(int n=0;n<7;n++)renderers[Capacity+7+n].enabled=showSizes&&Live.Ready;if(showSizes)DrawSizes();
   var logical=new ProjectionBounds(new Point2(0,0),new Point2(Live.World.Width,Live.World.Height));for(int i=Capacity;i<Capacity+7;i++){renderers[i].enabled=Live.Ready;Live.Gpu.Draw(data[i],filters[i],logical,true);IndicatorVertices+=data[i].Vertices.Count;}
   VisualMs=StreamingGeometry.Elapsed(start);
  }
  public static ProjectionBounds VisualBounds(Point2 p,double size,int kind,bool decor=true){double half=kind==0?.85:kind==1?.65:kind==2?.85:.65,top=kind==0?1.35:kind==1?1.3:kind==2?1.5:1.65,bottom=kind==1?.2:kind==2?.05:0;if(!decor){half=kind==1?.65:.43;top=1.3;bottom=kind==1?.03:0;}return new ProjectionBounds(new Point2(p.X-size*half,p.Y-size*top),new Point2(p.X+size*half,p.Y+size*bottom));}
  public static void BuildSilhouette(PreviewMeshData d,Point2 p,double s,int kind,bool decor=true)
  {
   double x=p.X,y=p.Y;Ellipse(d,new Point2(x,y-s*.65),s*.43,s*.65);
   if(kind==0&&decor){d.Triangle(new Point2(x-s*.25,y-s*.85),new Point2(x-s*.85,y-s*1.35),new Point2(x-s*.35,y-s*1.1));d.Triangle(new Point2(x+s*.25,y-s*.85),new Point2(x+s*.85,y-s*1.35),new Point2(x+s*.35,y-s*1.1));}
   if(kind==1){Ellipse(d,new Point2(x,y-s*.32),s*.65,s*.35);if(decor){d.Rectangle(x-s*.6,y-s*.1,x-s*.38,y+s*.2);d.Rectangle(x+s*.38,y-s*.1,x+s*.6,y+s*.2);}}
   if(kind==2&&decor){for(int k=0;k<3;k++){double yy=y-s*(.25+k*.3);Stroke(d,new Point2(x-s*.25,yy),new Point2(x-s*.8,yy+s*.25),s*.08);Stroke(d,new Point2(x+s*.25,yy),new Point2(x+s*.8,yy+s*.25),s*.08);}d.Rectangle(x-s*.1,y-s*1.5,x+s*.1,y-s*.9);}
   if(kind==3&&decor){d.Triangle(new Point2(x-s*.35,y-s),new Point2(x-s*.65,y-s*1.65),new Point2(x-s*.05,y-s*1.2));d.Triangle(new Point2(x+s*.35,y-s),new Point2(x+s*.65,y-s*1.65),new Point2(x+s*.05,y-s*1.2));}
   // Eye cut-in is a separate tiny geometric accent on the same silhouette, not a production sprite.
  }
  static void Ellipse(PreviewMeshData d,Point2 p,double rx,double ry){for(int i=0;i<20;i++){double a=i*Math.PI*2/20,b=(i+1)*Math.PI*2/20;d.Triangle(p,new Point2(p.X+Math.Cos(a)*rx,p.Y+Math.Sin(a)*ry),new Point2(p.X+Math.Cos(b)*rx,p.Y+Math.Sin(b)*ry));}}
  public static void Stroke(PreviewMeshData d,Point2 a,Point2 b,double width){double x=b.X-a.X,y=b.Y-a.Y,len=Math.Sqrt(x*x+y*y);if(len<1e-8)return;double nx=-y/len*width*.5,ny=x/len*width*.5;d.Quad(new Point2(a.X+nx,a.Y+ny),new Point2(b.X+nx,b.Y+ny),new Point2(b.X-nx,b.Y-ny),new Point2(a.X-nx,a.Y-ny));}
  public static void Circle(PreviewMeshData d,Point2 p,double r,double width,int segments=96){for(int j=0;j<segments;j++)Stroke(d,Stage49Preview.RangePoint(p,r,j*Math.PI*2/segments),Stage49Preview.RangePoint(p,r,(j+1)*Math.PI*2/segments),width);}
  static void Box(PreviewMeshData d,ProjectionBounds b,double width){var a=b.Min;var z=b.Max;Stroke(d,a,new Point2(z.X,a.Y),width);Stroke(d,new Point2(z.X,a.Y),z,width);Stroke(d,z,new Point2(a.X,z.Y),width);Stroke(d,new Point2(a.X,z.Y),a,width);}
  void DrawSizes(){for(int n=0;n<Sizes.Length;n++){int size=Sizes[n];var q=new Point2(LabRoot.X+(n-3)*13,LabRoot.Y);Box(data[Capacity],new ProjectionBounds(new Point2(q.X-size*.5,q.Y-size*.5),new Point2(q.X+size*.5,q.Y+size*.5)),.06);int i=Capacity+7+n;BuildSilhouette(data[i],q,size,n%4,decorations);data[i].VisualActorAnchor=q;Tint(i,Navigation.Fits(q,size)?new Color(.6f,.9f,.8f):new Color(1,.3f,.4f,.55f));Live.Gpu.Draw(data[i],filters[i],VisualBounds(q,size,n%4,decorations),true);ActorVertices+=data[i].Vertices.Count;}}

  void Tint(int i,Color c){renderers[i].GetPropertyBlock(block);block.SetColor("_Color",c);renderers[i].SetPropertyBlock(block);}
  Vector2 ScreenPoint(Point2 q){var d=Live.Snapshot.Project(q);return DisplayPoint(d);}
  Vector2 DisplayPoint(Point2 q){var p=Live.previewCamera.WorldToScreenPoint(new Vector3((float)q.X,-(float)q.Y));return new Vector2(p.x,Screen.height-p.y);}
  readonly List<(ProjectionBounds,Color)> displayBoxes=new List<(ProjectionBounds,Color)>();
  void DrawDisplayBox(ProjectionBounds b,Color c){displayBoxes.Add((b,c));}
  void OnGUI()
  {
   if(!initialized||Live.Snapshot==null||EnvironmentalMode&&!showPanel)return;foreach(var item in displayBoxes){var a=DisplayPoint(item.Item1.Min);var b=DisplayPoint(item.Item1.Max);GUI.color=item.Item2;GUI.DrawTexture(new Rect(a.x,a.y,b.x-a.x,1),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(a.x,b.y,b.x-a.x,1),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(a.x,a.y,1,b.y-a.y),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(b.x,a.y,1,b.y-a.y),Texture2D.whiteTexture);}GUI.color=Color.white;
   if(showLabels)for(int i=0;i<encounterCount;i++){var a=Actors[i];if(!a.SpawnValid||PresentationAllowed!=null&&!PresentationAllowed(a.Position)||onlySelected&&i!=selected||!xray&&!LabNavigation.Contact(Live.World,new Point2(Live.Position.x,Live.Position.y),a.Position))continue;var q=ScreenPoint(a.Position);if(q.x<0||q.x>Screen.width||q.y<0||q.y>Screen.height)continue;GUI.Label(new Rect(q.x+8,q.y-45,230,45),$"{i} {a.State} · {a.Size}×{a.Size}\n{a.Distance:F1} cells / {Math.Max(0,a.Timer):F1}s" );}
   var edge=LabPresentation.Edge(Live.Snapshot,Target,CameraStudy.DisplayWidth,CameraStudy.DisplayWidth/(16.0/9),5);var ep=DisplayPoint(edge);var projectedTarget=Live.Snapshot.Project(Target);var matrix=GUI.matrix;GUIUtility.RotateAroundPivot((float)(Math.Atan2(projectedTarget.Y,projectedTarget.X)*180/Math.PI),ep);GUI.Label(new Rect(ep.x-12,ep.y-12,25,25),"▶");GUI.matrix=matrix;GUI.Box(new Rect(ep.x-90,ep.y+10,180,40),$"{ObjectiveNames[objective]}{(objective==2&&!Actors[0].SpawnValid?" (no fitting actor)":"")}\n{TargetVisibility}");
   if(showSizes)for(int n=0;n<Sizes.Length;n++){var q=new Point2(LabRoot.X+(n-3)*13,LabRoot.Y);var sp=ScreenPoint(q);bool fits=Navigation.Fits(q,Sizes[n]);GUI.Label(new Rect(sp.x-60,sp.y+10,220,45),$"{Sizes[n]}×{Sizes[n]} probe: {(fits?"fits":"BLOCKED — ghost")}\nvisual overhang ≠ collision");}
   if(CameraStudy.captureLabel)GUI.Box(new Rect(365,145,Mathf.Max(350,Screen.width-760),50),$"STAGE 5.0 · clock {Clock:F2}s · actors {encounterCount} · footprint {footprint} · {(scaleMode==0?"proportional":"grid X/Y")} · {tuning.rule}\n{ObjectiveNames[objective]} / {TargetVisibility} · guidance {guidanceMode} · frozen {CameraStudy.freezeFocus} · XRAY {xray}");
   if(!showPanel)return;GUILayout.BeginArea(new Rect(Screen.width-382,8,374,Screen.height-16),GUI.skin.box);scroll=GUILayout.BeginScrollView(scroll);GUILayout.Label("STAGE 5.0 — ENCOUNTER LAB");GUILayout.Label("Preview-only state/attack/path demonstrations. No damage.");GUILayout.Label("Blue: short persistence · amber: long · red: telegraph. Tab UI / F2 lab panel.");paused=GUILayout.Toggle(paused,"Pause encounter clocks (player remains playable)");timeScale=Slider("Encounter time scale",timeScale,.1f,1);CameraStudy.freezeFocus=GUILayout.Toggle(CameraStudy.freezeFocus,"Freeze camera focus");if(GUILayout.Button("Restart actors at current player position"))Restart();
   int count=Mathf.RoundToInt(Slider("Simultaneous actors",encounterCount,1,Capacity));if(count!=encounterCount){encounterCount=count;Restart();}selected=Mathf.Clamp(Mathf.RoundToInt(Slider("Selected actor",selected,0,encounterCount-1)),0,encounterCount-1);int next=GUILayout.Toolbar(Array.IndexOf(Sizes,footprint),new[]{"1","2","3","5","7","9","11"});if(next>=0&&footprint!=Sizes[next]){footprint=Sizes[next];Restart();}scaleMode=GUILayout.Toolbar(scaleMode,new[]{"Proportional bodies","Grid X/Y bodies"});bodyScale=Slider("Visible body scale (not footprint)",bodyScale,.4f,1.6f);decorations=GUILayout.Toggle(decorations,"Decorative ears / horns / limbs (no collision)");
   tuning.rule=(LabRule)GUILayout.Toolbar((int)tuning.rule,new[]{"Distance cutoff","Loss of contact"});tuning.detection=Slider("Detection cells",(float)tuning.detection,2,30);tuning.pursuit=Slider("Preferred pursuit cells",(float)tuning.pursuit,5,50);tuning.shortSearch=Slider("Short search seconds",(float)tuning.shortSearch,1,15);tuning.longSearch=Slider("Long persistence seconds",(float)tuning.longSearch,5,40);tuning.searchDistance=Slider("Short search distance limit",(float)tuning.searchDistance,10,70);tuning.speed=Slider("Logical actor speed",(float)tuning.speed,.5f,6);tuning.shortReach=Slider("Short melee cells",(float)tuning.shortReach,1,5);tuning.longReach=Slider("Long melee cells",(float)tuning.longReach,2,10);tuning.projectileReach=Slider("Projectile cells",(float)tuning.projectileReach,5,35);tuning.areaReach=Slider("Wide-area radius",(float)tuning.areaReach,3,14);safetyMargin=Slider("Attack safety margin",safetyMargin,0,10);
   onlySelected=GUILayout.Toggle(onlySelected,"Isolate selected actor (presentation only)");showFootprints=GUILayout.Toggle(showFootprints,"Actual collision footprint outlines");showVisualBounds=GUILayout.Toggle(showVisualBounds,"Separate billboard visual bounds");showSizes=GUILayout.Toggle(showSizes,"Seven static size probes (invalid ones are ghosts)");showLabels=GUILayout.Toggle(showLabels,"Actor state/distance/timer labels");showPaths=GUILayout.Toggle(showPaths,"Expected traversable approach routes");showDetection=GUILayout.Toggle(showDetection,"Selected detection circle");showPursuit=GUILayout.Toggle(showPursuit,"Selected pursuit circle");showSearch=GUILayout.Toggle(showSearch,"Selected search-distance circle");showAttacks=GUILayout.Toggle(showAttacks,"Telegraphs and wall-stopped projectiles");showSafety=GUILayout.Toggle(showSafety,"Selected attack-area + safety margin around player");xray=GUILayout.Toggle(xray,"Debug X-ray silhouettes (explicit)");
   objective=GUILayout.Toolbar(objective,new[]{"Event","Visited","Tracked","Corner"});guidanceMode=GUILayout.Toolbar(guidanceMode,new[]{"Direction","Route","Sensory trail"});for(int n=0;n<Stage49Preview.Places.Length;n++)if(GUILayout.Button(Stage49Preview.Places[n]))SetLocation(n);
   GUILayout.Label($"Clock {Clock:F1}s · active {Active} / dormant {Dormant} / presented {Visible}\nSim {SimulationMs:F3}ms · visuals {VisualMs:F3}ms\nNav {NavigationMs:F2}ms / {NavigationBuilds} rebuilds / {Navigation.TreeCount} shared trees\nNavigation payload {NavigationBytes/1024:N0} KiB\nActor vertices {ActorVertices:N0} · indicator {IndicatorVertices:N0}\nOwned pooled meshes {OwnedMeshes} · terrain residents {Live.Stream.Resident.Count}");GUILayout.Label(status);if(GUILayout.Button("Capture lab PNG + camera + actor JSON"))StartCoroutine(ManualCapture());
   for(int i=0;i<encounterCount;i++){var a=Actors[i];GUILayout.Label($"{i} {a.Name} / {(a.SpawnValid?a.State.ToString():"NO FIT")}\nlogical d={a.Distance:F1}; timer={Math.Max(0,a.Timer):F1}; speed={a.LogicalSpeed:F2}; visual={PixelSizes[i].x:F1}×{PixelSizes[i].y:F1}px, apparent={ProjectedSpeeds[i]:F1}px/lab-s; blocked={a.Blocked}; attacks={a.Attacks}");}
   GUILayout.EndScrollView();GUILayout.EndArea();
  }
  IEnumerator ManualCapture(){if(CameraStudy.CapturePending)yield break;string folder=Path.Combine(Application.persistentDataPath,"Stage50Captures"),name=DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");CameraStudy.Capture(folder,name);yield return new WaitForEndOfFrame();File.WriteAllText(Path.Combine(folder,name+".lab.json"),JsonUtility.ToJson(Stage50Benchmark.SnapshotFor(this,status),true));status="Captured lab to "+folder;}
  float Slider(string text,float value,float min,float max){GUILayout.Label(text+": "+value.ToString("F2"));return GUILayout.HorizontalSlider(value,min,max);}
  void OnDestroy(){if(Live!=null)Live.RightPanelWidth=0;for(int i=0;i<filters.Length;i++)if(filters[i]!=null){if(Live!=null&&Live.Gpu!=null)Live.Gpu.Forget(filters[i]);if(Application.isPlaying){Destroy(filters[i].sharedMesh);Destroy(filters[i].gameObject);}else{DestroyImmediate(filters[i].sharedMesh);DestroyImmediate(filters[i].gameObject);}}}
 }
}
