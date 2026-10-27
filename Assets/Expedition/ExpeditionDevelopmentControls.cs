using UnityEngine;using UnityEngine.InputSystem;using SporeGobbo.Input;
/// <summary>Ephemeral development switches, never campaign facts or a second damage service.</summary>
public sealed class ExpeditionDevelopmentControls:MonoBehaviour
{
 public static bool GodMode{get;private set;}public static int DamageAttempts{get;private set;}public static int AttemptedDamage{get;private set;}
 ExpeditionRegion region;bool visible,hovered;string message="";
 public static bool Protects(GobboController player)
 {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
  return GodMode&&GameSceneCatalog.DevelopmentCandidate&&SporeGobbo.Safety.Persistence.Isolated&&RegionQueries.Current!=null&&player!=null;
#else
  return false;
#endif
 }
 public static void Observe(int damage){DamageAttempts++;AttemptedDamage+=damage;}
 public static void SetGodMode(bool enabled)
 {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
  GodMode=enabled&&GameSceneCatalog.DevelopmentCandidate&&SporeGobbo.Safety.Persistence.Isolated&&RegionQueries.Current!=null;
#endif
 }
 void Awake(){region=GetComponent<ExpeditionRegion>();GodMode=false;DamageAttempts=AttemptedDamage=0;}
 void Update(){if(Keyboard.current!=null&&Keyboard.current.f8Key.wasPressedThisFrame)visible=!visible;bool hover=visible&&Mouse.current!=null&&Mouse.current.position.ReadValue().x>Screen.width-375&&Mouse.current.position.ReadValue().y>Screen.height-550;if(hover!=hovered&&SporeUiCoordinator.Instance!=null){hovered=hover;if(hover)SporeUiCoordinator.Instance.PushModal(this,null,false);else SporeUiCoordinator.Instance.PopModal(this);}}
 void Act(System.Action action){try{action();message="Done through existing services";}catch(System.Exception e){message=e.Message;Debug.LogWarning(e);}}
 void OnGUI()
 {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
  GUI.Label(new Rect(Screen.width-420,Screen.height-80,415,30),"EXPEDITION / F8 tools / GOD "+(GodMode?"ON":"OFF")+" / dig power "+region.TestDigPower);
  if(region.Projection!=null)for(int i=0;i<3;i++){var position=region.RegionCamera.WorldToScreenPoint(region.Render(region.World(new SporeGobbo.Geometry.Point2(81.5+i*5,104.5))));if(position.x>0&&position.x<Screen.width&&position.y>0&&position.y<Screen.height)GUI.Label(new Rect(position.x-55,Screen.height-position.y-10,140,30),"DEV DIRT L"+(i+1));}
  if(!visible)return;GUILayout.BeginArea(new Rect(Screen.width-370,8,360,540),GUI.skin.box);GUILayout.Label("Isolated expedition development tools");if(GUILayout.Button("God Mode: "+(GodMode?"ON":"OFF")))SetGodMode(!GodMode);GUILayout.Label("Damage attempts "+DamageAttempts+" / attempted HP "+AttemptedDamage);
  GUILayout.Label("Temporary fixture power (not Claws progression)");for(int i=1;i<=3;i++){int power=i;if(GUILayout.Button("Fixture power "+i))region.TestDigPower=power;}
  if(region.Player!=null){if(GUILayout.Button("Grant one real spore — Q plants"))Act(()=>region.Player.GetComponent<SporeInventory>().AddSpore(1));if(GUILayout.Button("Normal return"))Act(()=>RunReturnService.ReturnToCamp());if(GUILayout.Button("Retreat"))Act(()=>RunReturnService.ReturnToCamp("CampScene",true,true,RunReturnReason.Retreat,"Expedition tools"));if(GUILayout.Button("Disable God Mode and apply fatal damage"))Act(()=>{SetGodMode(false);region.Player.TakeDamage(1000000);});
  foreach(string id in new[]{"arrival","nursery","weevil","blob","fossil","quiet","exit"})if(GUILayout.Button("Development warp: "+id)){string target=id;Act(()=>{var p=region.World(region.Definition.Anchors[target]);if(!TileMover.CanOccupyBox(region,p,TileMover.GetMapClearanceExtents(region.Player.GetComponent<Rigidbody2D>(),.22f)))throw new System.InvalidOperationException("Anchor blocked for this body");region.Player.GetComponent<Rigidbody2D>().position=p;region.Player.transform.position=p;});}}
  GUILayout.Label(message);GUILayout.EndArea();
#endif
 }
 void OnDestroy(){GodMode=false;if(hovered&&SporeUiCoordinator.Instance!=null)SporeUiCoordinator.Instance.PopModal(this);}
}
