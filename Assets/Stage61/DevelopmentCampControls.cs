#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using SporeGobbo.Geometry;
/// <summary>Camp tools delegate to canonical services; no roster/job/outcome authority.</summary>
public sealed class DevelopmentCampControls : MonoBehaviour
{
 public bool Visible;bool modal;SporeUiCoordinator ui;string message="";int participants,deaths,recruits=1;
 bool CanAct=>(V1RuntimeProfile.DevelopmentToolsEnabled||V1RuntimeProfile.EvidenceEnabled)&&SporeGobbo.Safety.Persistence.Isolated;
 void Start(){ui=SporeUiCoordinator.Instance;}
 public bool Warp(string marker)
 {
  if(!CanAct)return false;var region=UnityEngine.Object.FindAnyObjectByType<SettlementCampRegion>();var player=UnityEngine.Object.FindAnyObjectByType<CampPlayableSpawner>()?.SpawnedPlayer;if(region==null||player==null||!region.Ready)return false;
  Point2 p=marker=="fire.watch"?new Point2(90.5,113.5):region.Definition.Marker(marker).Interaction;var position=region.World(p);var body=player.GetComponent<Rigidbody2D>();
  if(!TileMover.CanOccupyBox(region,position,TileMover.GetMapClearanceExtents(body,.25f)))return false;body.position=position;body.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();return true;
 }
 public void AddBuddy()
 {
  if(!CanAct)throw new InvalidOperationException("Development actions require reviewed isolated tools or evidence");
  var state=GameState.Instance;var region=UnityEngine.Object.FindAnyObjectByType<SettlementCampRegion>();var spawner=UnityEngine.Object.FindAnyObjectByType<CampPlayableSpawner>();if(state==null||region==null||spawner==null)return;
  int ordinal=1;while(state.GetLeader().uniqueId=="development-b-buddy-"+ordinal||state.ownedGobbos.Exists(g=>g!=null&&g.uniqueId=="development-b-buddy-"+ordinal)||state.deathHistory.Exists(record=>record!=null&&record.gobboId=="development-b-buddy-"+ordinal))ordinal++;
  var data=new GobboUnitSaveData{uniqueId="development-b-buddy-"+ordinal,displayName="Burrow Buddy "+ordinal,gobboType=BuddyType.Baby,ageStage=GobboAgeStage.Baby,visualSetId="baby"};state.AddGobbo(data,true);
  spawner.SpawnAdditionalOwnedBuddy(data.uniqueId,region.World(region.Definition.Marker("fire").Interaction));CampStartRoutineManager.Instance.RestartCampVisitForDevelopment();SporeSaveManager.SaveCurrentSlotFromGameState();
 }
 void Act(Action action){try{message="Done through existing services.";action();}catch(Exception error){message=error.Message;Debug.LogWarning("[Camp tools] "+message);}}
 void Update()
 {
  if(!V1RuntimeProfile.DevelopmentToolsEnabled){Visible=false;ReleaseModal();return;}
  if(Keyboard.current!=null&&Keyboard.current.f10Key.wasPressedThisFrame){if(!DevelopmentGameControls.OpenContext())Visible=!Visible;}
  bool hover=!DevelopmentGameControls.HasHub&&Visible&&Mouse.current!=null&&Mouse.current.position.ReadValue().x<340&&Mouse.current.position.ReadValue().y>Screen.height-650;
  if(hover!=modal&&ui!=null){modal=hover;if(hover)ui.PushModal(this,null,false);else ui.PopModal(this);}
 }
 void OnGUI(){if(!V1RuntimeProfile.DevelopmentToolsEnabled||DevelopmentGameControls.HasHub||!Visible)return;GUILayout.BeginArea(new Rect(8,8,330,Mathf.Min(650,Screen.height-30)),GUI.skin.box);DrawDevelopmentControls();GUILayout.EndArea();}
 public void DrawDevelopmentControls()
 {
  if(!V1RuntimeProfile.DevelopmentToolsEnabled)return;var activeLab=UnityEngine.Object.FindAnyObjectByType<CampFightPitLab>();if(activeLab!=null&&activeLab.Active){activeLab.DrawControls();return;}GUILayout.Label("Camp: real roster, resources and construction");
  if(GUILayout.Button("Fire observation point"))Act(()=>Warp("fire.watch"));if(GUILayout.Button("Optional root approach"))Act(()=>Warp(SporeGobbo.CampCandidate.SettlementCampLayout.RootId));
  if(GUILayout.Button("Add a real owned Buddy"))Act(AddBuddy);
  if(GUILayout.Button("Add resources through real inventory"))Act(()=>{CampResourceService.Add(GameState.Instance,CampResourceType.Mushrooms,5);CampResourceService.Add(GameState.Instance,CampResourceType.Shinies,5);});
  if(GUILayout.Button("Restart existing Camp routine"))Act(()=>CampStartRoutineManager.Instance.RestartCampVisitForDevelopment());
  UnityEngine.Object.FindAnyObjectByType<CampFightPitLab>()?.DrawControls();
  GUILayout.Space(10);GUILayout.Label("Fake completed run: explicit synthetic setup");participants=CountField("Existing Buddies who went",participants);deaths=CountField("Existing Buddies who died",deaths);recruits=CountField("New Buddies acquired",recruits);
  var input=new FakeCompletedRunInput(participants,deaths,recruits);bool valid=FakeCompletedRunBuilder.Validate(GameState.Instance,input,out string reason);GUILayout.Label(reason);
  bool previous=GUI.enabled;GUI.enabled=previous&&(valid||RunReturnService.HasPreparedCampSimulation);
  if(GUILayout.Button(RunReturnService.HasPreparedCampSimulation?"Retry prepared return (no duplicate facts)":"Simulate completed run + real Camp return"))Act(()=>{if(!RunReturnService.ReturnSimulatedCampOutcome(()=>FakeCompletedRunBuilder.Apply(GameState.Instance,input)))message="Return held safely. Retry prepared return or recover last committed Camp.";});
  GUI.enabled=previous;GUILayout.Label("Same existing fake builder, then real summary / arrival / save. Not natural expedition evidence.");GUILayout.Label(message);
 }
 static int CountField(string label,int value){GUILayout.BeginHorizontal();GUILayout.Label(label);if(int.TryParse(GUILayout.TextField(value.ToString(),GUILayout.Width(55)),out int number))value=Mathf.Clamp(number,0,10);GUILayout.EndHorizontal();return value;}
 void ReleaseModal(){if(modal&&ui!=null)ui.PopModal(this);modal=false;}
 void OnDisable(){ReleaseModal();}void OnDestroy(){ReleaseModal();}
}
#endif
