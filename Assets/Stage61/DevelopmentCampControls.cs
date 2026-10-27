#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using SporeGobbo.Geometry;
/// <summary>Reach test states through real services. No independent roster, currency, jobs or save state.</summary>
public sealed class DevelopmentCampControls:MonoBehaviour
{
 public bool Visible;bool modal;SporeUiCoordinator ui;
 void Start(){if(!GameSceneCatalog.DevelopmentCandidate)throw new InvalidOperationException("Development controls require the candidate profile");ui=SporeUiCoordinator.Instance;}
 public bool Warp(string marker)
 {
  var region=UnityEngine.Object.FindAnyObjectByType<SettlementCampRegion>();var player=UnityEngine.Object.FindAnyObjectByType<CampPlayableSpawner>()?.SpawnedPlayer;if(region==null||player==null||!region.Ready)return false;
  Point2 p=marker=="fire.watch"?new Point2(90.5,113.5):region.Definition.Marker(marker).Interaction;var position=region.World(p);var body=player.GetComponent<Rigidbody2D>();
  if(!TileMover.CanOccupyBox(region,position,TileMover.GetMapClearanceExtents(body,.25f)))return false;body.position=position;body.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();return true;
 }
 public void AddBuddy()
 {
  var state=GameState.Instance;var region=UnityEngine.Object.FindAnyObjectByType<SettlementCampRegion>();var spawner=UnityEngine.Object.FindAnyObjectByType<CampPlayableSpawner>();if(state==null||region==null||spawner==null)return;
  int ordinal=1;while(state.GetLeader().uniqueId=="development-b-buddy-"+ordinal||state.ownedGobbos.Exists(g=>g!=null&&g.uniqueId=="development-b-buddy-"+ordinal)||state.deathHistory.Exists(record=>record!=null&&record.gobboId=="development-b-buddy-"+ordinal))ordinal++;
  var data=new GobboUnitSaveData{uniqueId="development-b-buddy-"+ordinal,displayName="Burrow Buddy "+ordinal,gobboType=BuddyType.Baby,ageStage=GobboAgeStage.Baby,visualSetId="baby"};state.AddGobbo(data,true);
  spawner.SpawnAdditionalOwnedBuddy(data.uniqueId,region.World(region.Definition.Marker("fire").Interaction));CampStartRoutineManager.Instance.RestartCampVisitForDevelopment();SporeSaveManager.SaveCurrentSlotFromGameState();
 }
 void Update(){if(Keyboard.current!=null&&Keyboard.current.f10Key.wasPressedThisFrame)Visible=!Visible;bool hover=Visible&&Mouse.current!=null&&Mouse.current.position.ReadValue().x<310&&Mouse.current.position.ReadValue().y>Screen.height-250;if(hover!=modal){modal=hover;if(hover)ui.PushModal(this,null,false);else ui.PopModal(this);}}
 void OnGUI()
 {
  GUI.Label(new Rect(12,Screen.height-30,Screen.width-24,25),"DEVELOPMENT CAMPAIGNS — ISOLATED STORAGE / F10 test controls");if(!Visible)return;
  GUILayout.BeginArea(new Rect(8,8,300,240),GUI.skin.box);GUILayout.Label("Development campaign controls");if(GUILayout.Button("Fire observation point"))Warp("fire.watch");if(GUILayout.Button("Root approach"))Warp(SporeGobbo.CampCandidate.SettlementCampLayout.RootId);if(GUILayout.Button("Add a real owned Buddy"))AddBuddy();if(GUILayout.Button("Add resources (real service)")){CampResourceService.Add(GameState.Instance,CampResourceType.Mushrooms,5);CampResourceService.Add(GameState.Instance,CampResourceType.Shinies,5);}if(GUILayout.Button("Restart existing Camp routine"))CampStartRoutineManager.Instance.RestartCampVisitForDevelopment();GUILayout.EndArea();
 }
 void OnDestroy(){if(modal&&ui!=null)ui.PopModal(this);}
}

#endif
