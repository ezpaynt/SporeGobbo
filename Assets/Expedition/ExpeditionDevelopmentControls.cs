using UnityEngine;using UnityEngine.InputSystem;using SporeGobbo.Input;
/// <summary>Ephemeral development switches, never campaign facts or a second damage authority.</summary>
public sealed class ExpeditionDevelopmentControls:MonoBehaviour
{
 public static bool GodMode{get;private set;}public static int DamageAttempts{get;private set;}public static int AttemptedDamage{get;private set;}
 ExpeditionRegion region;bool visible,hovered;string message="";
 public static bool Protects(GobboController player)
 {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
  return GodMode&&V1RuntimeProfile.DevelopmentToolsEnabled&&SporeGobbo.Safety.Persistence.Isolated&&RegionQueries.Current is ExpeditionRegion&&player!=null;
#else
  return false;
#endif
 }
 public static void Observe(int damage){DamageAttempts++;AttemptedDamage+=damage;}
 public static void SetGodMode(bool enabled)
 {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
  GodMode=enabled&&V1RuntimeProfile.DevelopmentToolsEnabled&&SporeGobbo.Safety.Persistence.Isolated&&RegionQueries.Current is ExpeditionRegion;
#else
  GodMode=false;
#endif
 }
 void Awake(){region=GetComponent<ExpeditionRegion>();GodMode=false;DamageAttempts=AttemptedDamage=0;}
 void Update()
 {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
  if(!V1RuntimeProfile.DevelopmentToolsEnabled){visible=false;GodMode=false;ReleaseModal();return;}
  if(Keyboard.current!=null&&Keyboard.current.f8Key.wasPressedThisFrame){if(!DevelopmentGameControls.OpenContext())visible=!visible;}
  bool hover=!DevelopmentGameControls.HasHub&&visible&&Mouse.current!=null&&Mouse.current.position.ReadValue().x>Screen.width-375&&Mouse.current.position.ReadValue().y>Screen.height-550;
  if(hover!=hovered&&SporeUiCoordinator.Instance!=null){hovered=hover;if(hover)SporeUiCoordinator.Instance.PushModal(this,null,false);else SporeUiCoordinator.Instance.PopModal(this);}
#endif
 }
 void Act(System.Action action){try{message="Done through existing services";action();}catch(System.Exception error){message=error.Message;Debug.LogWarning(error);}}
 void OnGUI()
 {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
  if(!V1RuntimeProfile.DevelopmentToolsEnabled||region==null)return;
  GUI.Label(new Rect(Screen.width-440,Screen.height-80,435,30),"EXPEDITION / F9 tools / GOD "+(GodMode?"ON":"OFF")+" / fixture power "+region.TestDigPower);
  if(region.Projection!=null)for(int i=0;i<3;i++){var p=region.RegionCamera.WorldToScreenPoint(region.Render(region.World(new SporeGobbo.Geometry.Point2(81.5+i*5,104.5))));if(p.x>0&&p.x<Screen.width&&p.y>0&&p.y<Screen.height)GUI.Label(new Rect(p.x-55,Screen.height-p.y-10,140,30),"DEV DIRT L"+(i+1));}
  if(DevelopmentGameControls.HasHub||!visible)return;
  GUILayout.BeginArea(new Rect(Screen.width-370,8,360,540),GUI.skin.box);DrawDevelopmentControls();GUILayout.EndArea();
#endif
 }
 public void DrawDevelopmentControls()
 {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
  if(!V1RuntimeProfile.DevelopmentToolsEnabled||region==null)return;
  GUILayout.Label("Expedition tools / real combat services");if(GUILayout.Button("God Mode: "+(GodMode?"ON":"OFF")))SetGodMode(!GodMode);
  GUILayout.Label("Damage attempts "+DamageAttempts+" / attempted HP "+AttemptedDamage);
  GUILayout.Label("Temporary fixture power, not Claws progression");for(int i=1;i<=3;i++){int power=i;if(GUILayout.Button("Fixture power "+i))region.TestDigPower=power;}
  if(region.Player!=null)
  {
   if(GUILayout.Button("Grant one real spore / Q plants"))Act(()=>region.Player.GetComponent<SporeInventory>().AddSpore(1));
   if(GUILayout.Button("Normal return"))Act(()=>RunReturnService.ReturnToCamp());
   if(GUILayout.Button("Retreat"))Act(()=>RunReturnService.ReturnToCamp("CampScene",true,true,RunReturnReason.Retreat,"Expedition tools"));
   if(GUILayout.Button("Disable God and apply fatal damage"))Act(()=>{SetGodMode(false);region.Player.TakeDamage(1000000);});
   foreach(string id in new[]{"arrival","nursery","weevil","blob","fossil","quiet","exit"})if(GUILayout.Button("Development warp: "+id)){string target=id;Act(()=>{var p=region.World(region.Definition.Anchors[target]);if(!TileMover.CanOccupyBox(region,p,TileMover.GetMapClearanceExtents(region.Player.GetComponent<Rigidbody2D>(),.22f)))throw new System.InvalidOperationException("Anchor blocked for this body");var body=region.Player.GetComponent<Rigidbody2D>();body.position=p;body.linearVelocity=Vector2.zero;region.Player.transform.position=p;});}
  }
  GUILayout.Label(message);
#endif
 }
 void ReleaseModal(){if(hovered&&SporeUiCoordinator.Instance!=null)SporeUiCoordinator.Instance.PopModal(this);hovered=false;}
 void OnDisable(){GodMode=false;ReleaseModal();}void OnDestroy(){GodMode=false;ReleaseModal();}
}
