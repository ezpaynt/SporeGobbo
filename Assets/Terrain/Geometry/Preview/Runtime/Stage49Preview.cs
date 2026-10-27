using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
namespace SporeGobbo.Geometry.Preview
{
 /// <summary>Reversible composition-only controls on the retained Stage 4.8 streaming host.</summary>
 [DefaultExecutionOrder(1200),RequireComponent(typeof(Stage47Preview))]
 public sealed class Stage49Preview:MonoBehaviour
 {
  public bool freezeActors;public int profile;public bool freezeFocus,normalOverlay=true,combatOverlay,rangeOverlay,cellOverlay;
  public float curveSoftness;public float horizontalFocusOffset,softness=1,sideTransition=1,topTransition=1,foregroundTransition=1;
  public float combatWidth=20,combatHeight=12,attackRadius=6;
  public string bookmark="Junction";public string message="";public bool captureLabel;
  public Stage47Preview Live=>GetComponent<Stage47Preview>();
  public double DisplayWidth=>Math.Max(8,Live.settings.centerWidth)/Mathf.Clamp(Live.settings.coverage,.35f,.85f);
  Vector2 scroll;Texture2D pixel;readonly StringBuilder text=new StringBuilder(2048);
  public static readonly string[] Names={"A Current Strong","B Foreground Study","C Depth-Oriented","D Wider Gameplay Center"};
  public static Stage4Settings Settings(int n)
  {
   var s=new Stage4Settings{coverage=.55f,side=.25f,top=.2f,foreground=1.9f};
   if(n==1){s.side=.6f;s.distance=30;s.anchor=.6f;}
   if(n==2){s.centerWidth=52;s.centerHeight=28;s.coverage=52f/(48f/.55f);s.side=.55f;s.top=.12f;s.distance=30;s.anchor=.62f;}
   if(n==3){s.centerWidth=60;s.centerHeight=32;s.coverage=60f/(48f/.55f);s.side=.4f;s.top=.15f;s.distance=24;s.anchor=.6f;}
   return s;
  }
  public void Apply(int n){profile=n;curveSoftness=n==0?0:n==2?.75f:.5f;Live.settings=Settings(n);horizontalFocusOffset=0;softness=sideTransition=topTransition=foregroundTransition=1;if(n==1){foregroundTransition=.8f;topTransition=.8f;}if(n==2){sideTransition=1.5f;topTransition=.8f;horizontalFocusOffset=3;}Constrain();}
  // Coverage is horizontal. Height is constrained to the isotropic viewport; no axis stretching.
  public void Constrain(){var s=Live.settings;s.centerWidth=Mathf.Clamp(s.centerWidth,16,80);s.coverage=Mathf.Clamp(s.coverage,.35f,.85f);s.centerHeight=Mathf.Clamp(s.centerHeight,4,(float)(DisplayWidth/(16.0/9)));curveSoftness=Mathf.Clamp01(curveSoftness);s.foreground=Mathf.Max(1,s.foreground);s.side=Mathf.Clamp(s.side,.05f,1);s.top=Mathf.Clamp(s.top,.01f,1);s.distance=Mathf.Max(1,s.distance);softness=Mathf.Max(.1f,softness);sideTransition=Mathf.Max(.1f,sideTransition);topTransition=Mathf.Max(.1f,topTransition);foregroundTransition=Mathf.Max(.1f,foregroundTransition);}
  public FocusProjection Map(Vector2 follow,FocusProjection previous=null)
  {
   var s=Live.settings;double w=DisplayWidth,h=w/(16.0/9);var f=new Point2(follow.x+horizontalFocusOffset,follow.y);var o=new Point2(0,(s.anchor-.5)*h);
   double d=Math.Max(1,s.distance),sd=d*softness*sideTransition,td=d*softness*topTransition,fd=d*FocusProjection.ForegroundDistanceRatio*softness*foregroundTransition;
   double side=s.uniform?1:s.side,top=s.uniform?1:s.top,fore=s.uniform?1:s.foreground;
   if(previous!=null&&previous.Focus.X==f.X&&previous.Focus.Y==f.Y&&previous.Offset.Y==o.Y&&previous.HalfWidth==s.centerWidth*.5&&previous.HalfHeight==s.centerHeight*.5&&previous.Side==side&&previous.Top==top&&previous.Foreground==fore&&previous.Distance==d&&previous.SideDistance==sd&&previous.TopDistance==td&&previous.ForegroundDistance==fd&&previous.CurveBlend==curveSoftness)return previous;
   return new FocusProjection(f,s.centerWidth,s.centerHeight,d,side,top,fore,o,false,sd,td,fd,curveSoftness);
  }
  public static ProjectionBounds Normal(FocusProjection p)=>new ProjectionBounds(new Point2(p.Focus.X-p.HalfWidth,p.Focus.Y-p.HalfHeight),new Point2(p.Focus.X+p.HalfWidth,p.Focus.Y+p.HalfHeight));
  public static ProjectionBounds CombatBounds(Point2 player,double width,double height)=>new ProjectionBounds(new Point2(player.X-width*.5,player.Y-height*.5),new Point2(player.X+width*.5,player.Y+height*.5));
  public static Point2 RangePoint(Point2 player,double radius,double angle)=>new Point2(player.X+radius*Math.Cos(angle),player.Y+radius*Math.Sin(angle));
  public static Point2 CellSize(FocusProjection p,Point2 center)=>new Point2(p.Project(new Point2(center.X+.5,center.Y)).X-p.Project(new Point2(center.X-.5,center.Y)).X,p.Project(new Point2(center.X,center.Y+.5)).Y-p.Project(new Point2(center.X,center.Y-.5)).Y);
  public Vector2[] Bookmarks()
  {
   var w=Live.World;int bx=w.Width/2/64+1,by=w.Height/2/64-1;uint hash=unchecked((uint)(bx*73856093^by*19349663^w.Seed));
   float cx=bx*64+20+hash%25,cy=by*64+20+(hash>>8)%25;
   double phase=(w.Seed%997)*.01;int x=w.Width/2+53;float y=(float)(Math.Round((w.Height/2-90)/96.0)*96-10*Math.Sin(x*.035+phase));
   return new[]{FindOpen(new Vector2(cx+6.5f,cy+.5f)),FindOpen(new Vector2(x+.5f,y+.5f)),FindOpen(new Vector2(x+80.5f,(float)(Math.Round((w.Height/2-90)/96.0)*96-10*Math.Sin((x+80)*.035+phase))+.5f)),FindOpen(new Vector2(w.Width/2+.5f,w.Height/4+35.5f)),FindOpen(new Vector2(cx+.5f,cy-5.5f)),new Vector2(w.Width/2+.5f,w.Height/2+.5f)};
  }
  Vector2 FindOpen(Vector2 desired){for(int r=0;r<20;r++)for(int dy=-r;dy<=r;dy++)for(int dx=-r;dx<=r;dx++){var p=new Vector2(Mathf.Floor(desired.x)+dx+.5f,Mathf.Floor(desired.y)+dy+.5f);if(PreviewMotion.Fits(Live.World,p,Live.footprint))return p;}return new Vector2(Live.World.Width/2+.5f,Live.World.Height/2+.5f);}
  public static readonly string[] Places={"Large chamber","Winding 3-cell tunnel","Oblique route context","Narrow upper route","Foreground pillar","Junction"};
  public void Go(int n){Live.Initialize();Live.Teleport(Bookmarks()[n]);bookmark=Places[n];}
  public string Diagnostics()
  {
   var l=Live;var p=l.Snapshot;if(p==null)return "Initializing";double dw=DisplayWidth,dh=dw/(16.0/9),px=1920/dw;
   var a=p.Unproject(new Point2(-dw*.5,-dh*.5));var b=p.Unproject(new Point2(dw*.5,dh*.5));
   text.Clear();text.AppendFormat("Visible logical [{0:F1},{1:F1}]â€“[{2:F1},{3:F1}]\nNormal {4:F1} Ã— {5:F1}; horizontal coverage {6:P0}; vertical {7:P0}\nAbove {8:F1} / below {9:F1} / left {10:F1} / right {11:F1} cells\nSelected {12}; resident {13}/{14}; desired {15}; vertices {16:N0}\n",a.X,a.Y,b.X,b.Y,p.HalfWidth*2,p.HalfHeight*2,l.settings.coverage,p.HalfHeight*2/dh,l.Position.y-a.Y,b.Y-l.Position.y,l.Position.x-a.X,b.X-l.Position.x,l.Stream.Visible.Count,l.Stream.Resident.Count,l.Stream.Capacity,l.Stream.Desired.Count,l.VertexCount);
   foreach(var v in new[]{new Vector2(.5f,.5f),new Vector2(.1f,.1f),new Vector2(.9f,.1f),new Vector2(.1f,.9f),new Vector2(.5f,.9f),new Vector2(.9f,.9f),new Vector2(.01f,.99f),new Vector2(.99f,.99f)}){var q=p.Unproject(new Point2((v.x-.5)*dw,(v.y-.5)*dh));var size=CellSize(p,q);text.AppendFormat("({0:F1},{1:F1}) cell {2:F1}Ã—{3:F1} px; W/H {4:F2}\n",v.x,v.y,size.X*px,size.Y*px,size.X/size.Y);}
   if(l.Stream.OverBudget)text.Append("WARNING: visible selection exceeds 384-chunk cap; rendering pauses.\n");else if(l.Stream.Desired.Count>l.Stream.Capacity)text.Append("Prefetch exceeds cap; visible chunks take priority.\n");
   if(p.Offset.Y-p.HalfHeight < -dh*.5 || p.Offset.Y+p.HalfHeight > dh*.5)text.Append("Normal region clipped by this anchor; square cells retained.\n");
   return text.ToString();
  }
  public string Numeric()=>JsonUtility.ToJson(new CaptureData{profile=Names[profile],position=Live.Position,settings=Live.settings,horizontalFocusOffset=horizontalFocusOffset,curveSoftness=curveSoftness,softness=softness,sideTransition=sideTransition,topTransition=topTransition,foregroundTransition=foregroundTransition,diagnostics=Diagnostics(),bookmark=bookmark,focus=new Vector2((float)Live.Snapshot.Focus.X,(float)Live.Snapshot.Focus.Y)},true);
  [Serializable] public class CaptureData{public string profile,bookmark,diagnostics;public Vector2 position,focus;public Stage4Settings settings;public float horizontalFocusOffset,curveSoftness,softness,sideTransition,topTransition,foregroundTransition;}
  public bool CapturePending {get;private set;}
  public void Capture(string directory,string name){if(CapturePending)return;captureLabel=true;CapturePending=true;StartCoroutine(CaptureFrame(directory,name));}
  IEnumerator CaptureFrame(string directory,string name)
  {
   yield return new WaitForEndOfFrame();Directory.CreateDirectory(directory);var prior=RenderTexture.active;Texture2D texture=null;
   try{RenderTexture.active=null;texture=ScreenCapture.CaptureScreenshotAsTexture();if(texture==null||texture.width!=Screen.width||texture.height!=Screen.height)throw new InvalidOperationException("Capture dimensions differ from viewport");File.WriteAllBytes(Path.Combine(directory,name+".png"),texture.EncodeToPNG());File.WriteAllText(Path.Combine(directory,name+".json"),Numeric());message="Captured to "+directory;}
   finally{RenderTexture.active=prior;if(texture!=null)Destroy(texture);CapturePending=false;}
  }

  void Awake(){Apply(0);}
  void Start(){Live.Initialize();foreach(double dx in new[]{24.0,29.0,30.0})Live.Actors.Add(new StreamingActor(new Point2(Live.World.Width*.5+.5+dx,Live.World.Height*.5+.5)));}
  void OnGUI()
  {
   var l=Live;if(l.Snapshot==null)return;
   if(normalOverlay)Outline(Normal(l.Snapshot),Color.cyan);
   var pos=new Point2(l.Position.x,l.Position.y);
   if(combatOverlay)Outline(CombatBounds(pos,combatWidth,combatHeight),Color.yellow);
   if(rangeOverlay){Point2 last=RangePoint(pos,attackRadius,0);for(int i=1;i<=128;i++){var next=RangePoint(pos,attackRadius,i*Math.PI*2/128);Line(ScreenPoint(last),ScreenPoint(next),Color.magenta);last=next;}}
   if(cellOverlay){for(int y=1;y<=9;y+=2)for(int x=1;x<=9;x+=2){double dw=DisplayWidth,dh=dw/(16.0/9);var q=l.Snapshot.Unproject(new Point2((x*.1-.5)*dw,(y*.1-.5)*dh));var size=CellSize(l.Snapshot,q);var pt=ScreenPoint(q);GUI.color=new Color(0,0,0,.85f);GUI.DrawTexture(new Rect(pt.x-50,pt.y-20,120,38),Texture2D.whiteTexture);GUI.color=Color.white;GUI.Label(new Rect(pt.x-48,pt.y-20,120,38),$"{size.X*1920/dw:F1}Ã—{size.Y*1920/dw:F1}px\nW/H {size.X/size.Y:F2}");}}
   if(captureLabel){GUI.Box(new Rect(365,8,Screen.width-380,130),Names[profile]+" | "+bookmark+" | "+Screen.width+"Ã—"+Screen.height+"\n"+$"center {l.settings.centerWidth}x{l.settings.centerHeight}, coverage {l.settings.coverage:F3}, side/top/front {l.settings.side:F2}/{l.settings.top:F2}/{l.settings.foreground:F2}, D {l.settings.distance}, anchor {l.settings.anchor:F2}, H {l.settings.wallHeight:F2} ({l.settings.heightPolicy})"+$"\nX focus offset {horizontalFocusOffset}; length multiplier {softness}; curve softness {curveSoftness}; transitions side/top/front {sideTransition}/{topTransition}/{foregroundTransition}");}
   if(!l.showUI)return;
   GUILayout.BeginArea(new Rect(8,8,340,Screen.height-16),GUI.skin.box);scroll=GUILayout.BeginScrollView(scroll);GUILayout.Label("STAGE 4.9 â€” CAMERA COMPOSITION");GUILayout.Label("WASD / mouse dig Â· Tab hide UI");
   for(int i=0;i<4;i++)if(GUILayout.Button(Names[i]))Apply(i);
   freezeFocus=GUILayout.Toggle(freezeFocus,"Freeze camera focus (profile switch retains position)");freezeActors=GUILayout.Toggle(freezeActors,"Freeze reference actor clocks");
   var s=l.settings;s.uniform=GUILayout.Toggle(s.uniform,"Uniform comparison");s.centerWidth=Slide("Normal width cells",s.centerWidth,16,80);s.centerHeight=Slide("Normal height cells",s.centerHeight,4,48);s.coverage=Slide("Horizontal center coverage",s.coverage,.35f,.85f);GUILayout.Label("Height constrained to isotropic viewport; vertical coverage derived.");
   horizontalFocusOffset=Slide("Horizontal focus offset (cells)",horizontalFocusOffset,-16,16);s.anchor=Slide("Player anchor (fraction down)",s.anchor,.35f,.75f);s.side=Slide("Side scale",s.side,.15f,1);s.top=Slide("Top scale",s.top,.05f,1);s.foreground=Slide("Foreground scale (minimum 1)",s.foreground,1,3);s.distance=Slide("Transition distance",s.distance,4,48);curveSoftness=Slide("Transition softness (cubic to quintic)",curveSoftness,0,1);softness=Slide("Global transition length multiplier",softness,.4f,2);sideTransition=Slide("Side transition multiplier",sideTransition,.4f,2);topTransition=Slide("Top transition multiplier",topTransition,.4f,2);foregroundTransition=Slide("Foreground transition multiplier",foregroundTransition,.4f,2);s.smoothing=Slide("Follow smoothing seconds",s.smoothing,0,.6f);s.wallHeight=Slide("Wall height",s.wallHeight,0,4);s.heightPolicy=(WallHeightPolicy)GUILayout.Toolbar((int)s.heightPolicy,new[]{"Static reference","Continuous"});
   normalOverlay=GUILayout.Toggle(normalOverlay,"Cyan: actual normal square region");combatOverlay=GUILayout.Toggle(combatOverlay,"Yellow: combat readability target");rangeOverlay=GUILayout.Toggle(rangeOverlay,"Magenta: logical attack range");cellOverlay=GUILayout.Toggle(cellOverlay,"Cell sizes / W:H samples (1920 reference px)");combatWidth=Slide("Combat target width",combatWidth,4,40);combatHeight=Slide("Combat target height",combatHeight,4,28);attackRadius=Slide("Logical attack radius",attackRadius,1,30);
   l.showGrid=GUILayout.Toggle(l.showGrid,"Grid");l.showWalls=GUILayout.Toggle(l.showWalls,"Walls");l.showLips=GUILayout.Toggle(l.showLips,"Lips");l.showContours=GUILayout.Toggle(l.showContours,"Contours");
   for(int i=0;i<Places.Length;i++)if(GUILayout.Button(Places[i]))Go(i);
   captureLabel=GUILayout.Toggle(captureLabel,"Comparison screenshot label");if(GUILayout.Button("Capture PNG + numeric JSON"))Capture(SporeGobbo.Safety.Persistence.ExperimentPath("Stage49Captures"),DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));GUILayout.Label(message);GUILayout.Label(Diagnostics());GUILayout.EndScrollView();GUILayout.EndArea();
  }
  float Slide(string label,float value,float min,float max){GUILayout.Label(label+": "+value.ToString("F2"));return GUILayout.HorizontalSlider(value,min,max);}
  Vector2 ScreenPoint(Point2 p){var q=Live.Snapshot.Project(p);var v=Live.previewCamera.WorldToScreenPoint(new Vector3((float)q.X,-(float)q.Y,0));return new Vector2(v.x,Screen.height-v.y);}
  void Outline(ProjectionBounds b,Color c){var a=ScreenPoint(b.Min);var d=ScreenPoint(b.Max);Line(a,new Vector2(d.x,a.y),c);Line(new Vector2(d.x,a.y),d,c);Line(d,new Vector2(a.x,d.y),c);Line(new Vector2(a.x,d.y),a,c);}
  void Line(Vector2 a,Vector2 b,Color c){if(pixel==null){pixel=new Texture2D(1,1);pixel.SetPixel(0,0,Color.white);pixel.Apply();}var old=GUI.matrix;GUI.color=c;GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg,a);GUI.DrawTexture(new Rect(a.x,a.y,(b-a).magnitude,2),pixel);GUI.matrix=old;GUI.color=Color.white;}
  void OnDestroy(){if(pixel!=null)Destroy(pixel);}
 }
}
