using System;using System.Collections.Generic;using UnityEngine;using UnityEngine.InputSystem;using SporeGobbo.Input;using SporeGobbo.Geometry;
/// <summary>Small deterministic authored schedule. Campaign and terrain remain in their existing owners.</summary>
public sealed class IntroSequence:MonoBehaviour
{
 public IntroRegion Region{get;private set;}public bool ControlReleased{get;private set;}public bool Arriving{get;private set;}public float BirthTime{get;private set;}public float QuietTime{get;private set;}public float TutorialSeconds{get;private set;}public bool PromptVisible{get;private set;}public bool Forgiven{get;private set;}public bool PlugCleared{get;private set;}public bool ShakeEnabled=true;
 public int CompletedCollapses{get;private set;}public float ActiveSeconds{get;private set;}public bool ClockActive=>Time.timeScale>0&&Focused&&(ControlReleased?SporeInputReader.Instance!=null&&SporeInputReader.Instance.Context==SporeInputContext.Gameplay:true);
 bool Focused=>Application.isFocused||!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SPORE_STAGE61_CAPTURE"));
 GobboController player;SpriteRenderer source;Stage59ActorView view;bool modal,rumble,firstRock,secondRock;Texture2D disc;float shake,arrivalFade;int nextZone,lastPlugRevision=-1;FallingRockHazard collapsing;readonly HashSet<int> done=new HashSet<int>();string notice="";bool tools;
 public void Initialize(IntroRegion region){Region=region;disc=new Texture2D(32,32,TextureFormat.RGBA32,false);var pixels=new Color[1024];for(int y=0;y<32;y++)for(int x=0;x<32;x++)pixels[y*32+x]=(new Vector2(x-15.5f,y-15.5f).sqrMagnitude<225)?Color.white:Color.clear;disc.SetPixels(pixels);disc.Apply();}
 public void BindPlayer(GobboController actor){player=actor;source=actor.GetComponentInChildren<SpriteRenderer>();view=actor.GetComponent<Stage59ActorView>();source.enabled=false;actor.enabled=false;SporeUiCoordinator.Instance.PushStoryModal(this,null,null);modal=true;Region.Cue("spore.fall",Region.Arrival);}
 void Release(){if(modal&&SporeUiCoordinator.Instance!=null)SporeUiCoordinator.Instance.PopModal(this);modal=false;ControlReleased=true;player.enabled=true;source.enabled=true;Region.Cue("hatch.control",player.transform.position);}
 public string Binding(string action){var input=SporeInputReader.Instance;return input?.Bindings!=null?input.Bindings.GetDisplay("Gameplay",action,input.ActiveControlScheme==SporeControlScheme.Gamepad?BindingScheme.Gamepad:BindingScheme.KeyboardMouse):"…";}
 void Update()
 {
  using var profile=new Unity.Profiling.ProfilerMarker("Spore.Intro.Sequence").Auto();if(player==null||!Region.PresentationReady)return;if(Arriving){arrivalFade=Mathf.Clamp01(arrivalFade+Time.deltaTime/.7f);return;}
  if(!ControlReleased){if(ClockActive)BirthTime+=Time.deltaTime;if(BirthTime>=2.4f&&BirthTime-Time.deltaTime<2.4f)Region.Cue("spore.impact",Region.Arrival);if(BirthTime>=4.1f){source.enabled=true;view.PresentationLift=-.12f*Mathf.Clamp01(1-(BirthTime-4.1f)/1.6f);if(BirthTime-Time.deltaTime<4.1f)Region.Cue("spore.hatch",Region.Arrival);}if(BirthTime>=6.2f)Release();return;}
  if(Keyboard.current!=null&&Keyboard.current.f7Key.wasPressedThisFrame)tools=!tools;
  bool hover=tools&&Mouse.current!=null&&Mouse.current.position.ReadValue().x>Screen.width-365&&Mouse.current.position.ReadValue().y>Screen.height-250;if(hover&&!modal){SporeUiCoordinator.Instance.PushModal(this,null,false);modal=true;}else if(!hover&&modal){SporeUiCoordinator.Instance.PopModal(this);modal=false;}
  if(!ClockActive){Region.Presentation.VisualOffset=Vector2.zero;return;}ActiveSeconds+=Time.deltaTime;QuietTime+=Time.deltaTime;var p=Region.LogicalPosition(player.transform.position);
  if(QuietTime>=3&&!rumble){rumble=true;shake=.45f;Region.Cue("cave.first-rumble",player.transform.position);}
  if(shake>0){shake-=Time.deltaTime;Region.Presentation.VisualOffset=ShakeEnabled?new Vector2(Mathf.Sin(ActiveSeconds*24)*.035f,Mathf.Sin(ActiveSeconds*19)*.025f)*Mathf.Clamp01(shake/.45f):Vector2.zero;}else Region.Presentation.VisualOffset=Vector2.zero;
  if(!PlugCleared&&lastPlugRevision!=Region.Revision){lastPlugRevision=Region.Revision;PlugCleared=Region.State.Route(new Point2(95.5,47.5),new Point2(105.5,47.5),3).Count>0;}
  var anchor=Region.RegionCamera.WorldToViewportPoint(Region.Render(Region.World(new Point2(99.5,47.5))));bool onScreen=anchor.x>.08f&&anchor.x<.92f&&anchor.y>.12f&&anchor.y<.9f;
  PromptVisible=!PlugCleared&&p.X>=90&&p.X<99&&Math.Abs(p.Y-47.5)<7&&onScreen;
  if(PromptVisible){TutorialSeconds+=Time.deltaTime;if(TutorialSeconds>=12&&!Forgiven){Forgiven=true;notice="Easy, little one. Dig a way through.";player.TakeDamage(1);Region.Cue("tutorial.hold",player.transform.position);}}
  if(PlugCleared){PromptVisible=false;notice="";}
  if(rumble&&!firstRock&&p.X>35&&p.X<55){firstRock=true;FallingRockHazard.Create(Region,Region.World(new Point2(44.5,44.5)),1,()=>ClockActive,null,Region.Cue);Region.Cue("cave.distant-movement",player.transform.position);}
  if(rumble&&!secondRock&&p.X>72&&p.X<89){secondRock=true;FallingRockHazard.Create(Region,Region.World(new Point2(81.5,52.5)),1,()=>ClockActive,null,Region.Cue);}
  // Authored trailing thresholds, never a wall tied to the current player position.
  if(rumble&&QuietTime>7&&nextZone<Region.Definition.Thresholds.Length&&collapsing==null&&p.X>=Region.Definition.Thresholds[nextZone]&&p.X<132)
  {int zone=nextZone++;if(!done.Contains(zone)){var ground=Region.World(Region.Definition.CollapseCenters[zone]);collapsing=FallingRockHazard.Create(Region,ground,0,()=>ClockActive,()=>{var result=Region.Place("intro.collapse."+zone,Region.Definition.CollapseMasks[zone],Region.Revision);if(result.Accepted){done.Add(zone);CompletedCollapses++;shake=.3f;Region.Cue("cave.collapse",ground);}else notice="The loose earth settles behind you.";},Region.Cue);if(collapsing!=null)collapsing.SetCollapseFootprint(Region.Definition.CollapseMasks[zone]);}}
  if(p.X>=132){Region.Presentation.VisualOffset=Vector2.zero;foreach(var rock in Region.GetComponentsInChildren<FallingRockHazard>())rock.Cancel();if(!done.Contains(99)){done.Add(99);Region.Cue("cave.final-settle",player.transform.position);}}
  if(Vector2.Distance(player.transform.position,Region.World(Region.Definition.Exit))<1.3f&&PlugCleared)Region.ArrivalTransition.BeginArrival(player);
 }
 public void StopForArrival(){if(Arriving)return;Arriving=true;Region.Presentation.VisualOffset=Vector2.zero;if(player!=null){player.enabled=false;player.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;}if(modal&&SporeUiCoordinator.Instance!=null)SporeUiCoordinator.Instance.PopModal(this);modal=false;}
 public FallingRockHazard DevelopmentRock(){if(!GameSceneCatalog.DevelopmentCandidate||!SporeGobbo.Safety.Persistence.Isolated||!ControlReleased)return null;return FallingRockHazard.Create(Region,player.transform.position,1,()=>ClockActive,null,Region.Cue);}
 void OnGUI()
 {
  if(Region?.Projection==null)return;if(Arriving){GUI.color=new Color(.025f,.04f,.033f,arrivalFade);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;return;}
  if(!ControlReleased){GUI.color=new Color(0.025f,.04f,.033f,1-Mathf.SmoothStep(0,1,(BirthTime-2.4f)/2.5f));GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;var point=Region.RegionCamera.WorldToScreenPoint(Region.Render(view!=null?view.PhysicalFoot:Region.Arrival));float t=Mathf.Clamp01(BirthTime/2.4f);float pixels=Screen.height/(Region.RegionCamera.orthographicSize*2);float height=((Screen.height-point.y+30)/pixels)*(1-t*t);float squash=BirthTime>=2.4f&&BirthTime<2.7f?.55f:1;float alpha=1-Mathf.Clamp01((BirthTime-3.8f)/.7f);GUI.color=new Color(.69f,.66f,.42f,alpha);GUI.DrawTexture(new Rect(point.x-7/squash,Screen.height-point.y-height*pixels-13*squash,14/squash,18*squash),disc);if(BirthTime>=2.4f&&BirthTime<3.3f){float puff=(BirthTime-2.4f)/.9f;GUI.color=new Color(.48f,.46f,.31f,(1-puff)*.5f);for(int i=0;i<4;i++)GUI.DrawTexture(new Rect(point.x+(i-1.5f)*puff*16-3,Screen.height-point.y-puff*(8+i*2),6,4),disc);}
  if(BirthTime>3.1f&&BirthTime<4.1f){GUI.color=new Color(.1f,.12f,.09f,alpha);GUI.DrawTexture(new Rect(point.x-1,Screen.height-point.y-12,2,10),Texture2D.whiteTexture);}GUI.color=Color.white;return;}
  if(PromptVisible){var point=Region.RegionCamera.WorldToScreenPoint(Region.Render(Region.World(new Point2(99.5,47.5))));float x=Mathf.Clamp(point.x-125,15,Screen.width-265),y=Mathf.Clamp(Screen.height-point.y-80,45,Screen.height-150);GUI.Box(new Rect(x,y,250,Forgiven?80:54),"Hold "+Binding("Dig")+" and aim at the dirt\nMake a little way through."+(Forgiven?"\nThe cave waits. You have time.":""));}
  var logical=Region.LogicalPosition(player.transform.position);if(logical.X>110&&logical.X<127&&!PromptVisible)GUI.Box(new Rect(Screen.width*.5f-140,80,280,35),Binding("Dash")+" — a quick dash, if you like");
  if(logical.X<31&&QuietTime<7)GUI.Box(new Rect(Screen.width*.5f-135,80,270,35),"Move — "+Binding("Move"));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
  GUI.Label(new Rect(Screen.width-400,Screen.height-80,390,25),"INTRO / F7 tools / shake "+(ShakeEnabled?"ON":"OFF"));if(tools){GUILayout.BeginArea(new Rect(Screen.width-360,10,350,230),GUI.skin.box);GUILayout.Label("Isolated Intro tools");if(GUILayout.Button("Camera shake: "+(ShakeEnabled?"ON":"OFF")))ShakeEnabled=!ShakeEnabled;if(GUILayout.Button("Warn, then drop one real rock here"))DevelopmentRock();GUILayout.Label("Rock impacts are nonlethal in this Intro.");GUILayout.Label("Replay: F9 → Main Menu → Reset owned Fresh A → Continue.");GUILayout.Label("Tutorial active seconds: "+TutorialSeconds.ToString("F1"));GUILayout.EndArea();}
#endif
 }
 void OnDestroy(){if(modal&&SporeUiCoordinator.Instance!=null)SporeUiCoordinator.Instance.PopModal(this);if(Region?.Presentation!=null)Region.Presentation.VisualOffset=Vector2.zero;if(disc!=null)Destroy(disc);}
}
