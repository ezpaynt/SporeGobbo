#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using SporeGobbo.Input;
using SporeGobbo.CampCandidate;
using SporeGobbo.CampLifecycle;
using SporeGobbo.Geometry;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SporeGobbo.Safety;
/// <summary>Owned real-service smoke evidence. Does not replace campaign or Camp lifecycle.</summary>
public sealed class Stage61ServiceEvidence:MonoBehaviour
{
 readonly List<string> checks=new List<string>(); readonly List<string> errors=new List<string>();
 string directory; bool finished;Keyboard keyboard;Mouse mouse;float watchdog;
 void Awake(){watchdog=Time.realtimeSinceStartup+720;directory=Persistence.ExperimentPath("Evidence");Directory.CreateDirectory(directory);Application.logMessageReceived+=Observe;}
 void Update(){if(!finished&&errors.Count>0){Finish();return;}if(!finished&&Time.realtimeSinceStartup>watchdog){errors.Add("Real-service evidence timed out");Finish();}}
 void Observe(string message,string trace,LogType type){if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)errors.Add(message+"\n"+trace);}
 IEnumerator Start()
 {
  float deadline=Time.realtimeSinceStartup+90;CampSceneController controller=null;SettlementCampRegion region=null;CampPlayableSpawner spawner=null;
  while(Time.realtimeSinceStartup<deadline)
  {controller=UnityEngine.Object.FindAnyObjectByType<CampSceneController>();region=UnityEngine.Object.FindAnyObjectByType<SettlementCampRegion>();spawner=UnityEngine.Object.FindAnyObjectByType<CampPlayableSpawner>();if(controller!=null&&controller.WorldInputReady&&region!=null&&region.Ready&&spawner!=null&&spawner.SpawnedPlayer!=null)break;yield return null;}
  Check("real Camp readiness",controller!=null&&controller.WorldInputReady&&region!=null&&region.Ready&&spawner!=null&&spawner.SpawnedPlayer!=null);
  if(checks[checks.Count-1].StartsWith("FAIL")){Finish();yield break;}
  keyboard=InputSystem.AddDevice<Keyboard>("Stage61 owned virtual keyboard");keyboard.MakeCurrent();mouse=InputSystem.AddDevice<Mouse>("Stage61 owned virtual mouse");mouse.MakeCurrent();
  UnityEngine.Object.FindAnyObjectByType<CampInteractionDetector>().logInteractions=true;
  Check("one real campaign",UnityEngine.Object.FindObjectsByType<GameState>(FindObjectsSortMode.None).Length==1);
  Check("one Camp lifecycle",UnityEngine.Object.FindObjectsByType<CampSceneController>(FindObjectsSortMode.None).Length==1);
  Check("one terrain provider",ReferenceEquals(DiggableTerrainService.Active,region));
  Check("five real Buddy identities",UnityEngine.Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None).Length==5);
  Check("real player movement and projected binding",spawner.SpawnedPlayer.GetComponent<GobboController>()!=null&&spawner.SpawnedPlayer.GetComponent<ActorTerrainBinding>()!=null);
  Check("legacy terrain absent",UnityEngine.Object.FindObjectsByType<HandcraftedCampTerrain>(FindObjectsSortMode.None).Length==0);
  Check("real stations",UnityEngine.Object.FindAnyObjectByType<CampFireRecovery>()!=null&&UnityEngine.Object.FindAnyObjectByType<CampSquadSelect>()!=null&&UnityEngine.Object.FindAnyObjectByType<CampOldBonesWall>()!=null);
  var gridExport=new System.Text.StringBuilder("x,y,category,protected\n");for(int gy=0;gy<192;gy++)for(int gx=0;gx<192;gx++)gridExport.Append(gx).Append(',').Append(gy).Append(',').Append(region.Definition.Category(gx,gy)).Append(',').Append(region.Definition.Protected(gx,gy)?1:0).Append('\n');File.WriteAllText(Path.Combine(directory,"camp-logical-grid.csv"),gridExport.ToString());File.WriteAllText(Path.Combine(directory,"layout-identity.txt"),region.LayoutId+" / "+region.Definition.Fingerprint());
  Check("first-home observation approach is reachable",region.GetComponent<DevelopmentCampControls>().Warp("fire.watch"));
  float cueDeadline=Time.realtimeSinceStartup+60;while(BuddyHomeThought.RenderedCount==0&&Time.realtimeSinceStartup<cueDeadline)yield return null;Check("first-home intention driven by real routine",BuddyHomeThought.ActiveCount>0);Check("first-home cue actually repaints above a presented Buddy",BuddyHomeThought.RenderedCount>0&&region.Ready&&region.RegionCamera.cullingMask!=0);Check("first-home Buddies physically gather near the Fire before cue",UnityEngine.Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None).All(unit=>Vector2.Distance(unit.transform.position,region.World(region.Definition.Marker("fire").Interaction))<3));yield return Capture("first-home-sleepy-cue");
  for(int i=0;i<30;i++)yield return null;
  yield return Capture("real-camp-initial");
  var forms=UnityEngine.Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None);var sizeRows=new List<string>{"id,type,age,scale,halfWidth,halfHeight,projectedFeetError"};bool allFeet=true,allClear=true;
  foreach(var unit in forms){var actor=unit.GetComponent<Stage59ActorView>();var extents=TileMover.GetMapClearanceExtents(unit.GetComponent<Rigidbody2D>(),.25f);float error=Vector2.Distance(actor.ProjectedFoot,region.Render(actor.PhysicalFoot));allFeet&=error<.001f;allClear&=TileMover.CanOccupyBox(region,unit.transform.position,extents);sizeRows.Add(unit.unitData.uniqueId+","+unit.unitData.gobboType+","+unit.unitData.ageStage+","+unit.transform.localScale.x+","+extents.x+","+extents.y+","+error);}
  File.WriteAllLines(Path.Combine(directory,"actor-size-proof.csv"),sizeRows);Check("real age and type produce multiple physical sizes",forms.Max(unit=>unit.transform.localScale.x)>forms.Min(unit=>unit.transform.localScale.x));Check("all real body forms align projected feet",allFeet);Check("all real body forms fit logical arrival terrain",allClear);
  var player=spawner.SpawnedPlayer;var controls=region.GetComponent<DevelopmentCampControls>();var body=player.GetComponent<Rigidbody2D>();var binding=player.GetComponent<ActorTerrainBinding>();var campaign=GameState.Instance;
  controls.Warp("fire.watch");for(int i=0;i<90;i++)yield return null;yield return Capture("fire-before-real-construction");
  float constructionDeadline=Time.realtimeSinceStartup+240;int last=-1;bool digCaptured=false,preparationCancelled=false;
  while(campaign.campTerrainState.residentialSlotsEstablished<5&&Time.realtimeSinceStartup<constructionDeadline&&errors.Count==0)
  {
   var duties=CampStartRoutineManager.Instance;
   if(!preparationCancelled&&duties.CurrentResidentialSlot==2)
   {
    var worker=UnityEngine.Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None).FirstOrDefault(unit=>unit.unitData.uniqueId==duties.CurrentResidentialConstructorId);var ability=worker!=null?worker.GetComponent<BuddyDigAbility>():null;
    if(ability!=null&&(ability.IsPreparingBed||duties.LastResidentialDigRemovedCells>0))
    {
     preparationCancelled=true;int establishedBeforeCancel=campaign.campTerrainState.residentialSlotsEstablished;var acceptedDeltas=region.State.ExportDeltas();ability.enabled=false;
     float cancelDeadline=Time.realtimeSinceStartup+45;while(duties.IsCampVisitRoutineRunning&&Time.realtimeSinceStartup<cancelDeadline)yield return null;
     Check("cancelled real construction cannot establish a bed",!ability.LastBedPreparationCompleted&&campaign.campTerrainState.residentialSlotsEstablished==establishedBeforeCancel&&worker.unitData.campResidentialSlotId==0);
     Check("construction cancellation retains accepted excavation",acceptedDeltas.SequenceEqual(region.State.ExportDeltas()));ability.enabled=true;Check("existing routine can resume cancelled construction",duties.RestartCampVisitForDevelopment());
    }
   }
   if(preparationCancelled&&!digCaptured&&CampStartRoutineManager.Instance.LastResidentialDigRemovedCells>0){digCaptured=true;yield return Capture("fire-during-real-dig");}if(preparationCancelled&&last!=campaign.campTerrainState.residentialSlotsEstablished){last=campaign.campTerrainState.residentialSlotsEstablished;yield return Capture("fire-real-construction-"+last);}yield return null;}
  Check("real construction supports cancellation and recovery",preparationCancelled);
  Check("five real Buddy beds constructed",campaign.campTerrainState.residentialSlotsEstablished==5);
  Check("real assignments agree with established beds",campaign.ownedGobbos.Count(unit=>unit.campResidentialSlotId>0)==5&&campaign.ownedGobbos.Select(unit=>unit.campResidentialSlotId).Distinct().Count()==5);
  yield return Capture("fire-after-real-construction");
  if(campaign.campTerrainState.residentialSlotsEstablished!=5||errors.Count>0){Finish();yield break;}
  Check("first five beds established without any root interaction",!region.ResidentialEntranceOpen&&campaign.campTerrainState.residentialSlotsEstablished==5);
  Stage61RuntimeMetrics.Mark("steady five Buddies optional root intact");for(int frame=0;frame<600;frame++)yield return null;Stage61RuntimeMetrics.Mark("Camp interactions");
  Check("one real input reader",UnityEngine.Object.FindObjectsByType<SporeInputReader>(FindObjectsSortMode.None).Length==1);
  Check("one real UI coordinator",UnityEngine.Object.FindObjectsByType<SporeUiCoordinator>(FindObjectsSortMode.None).Length==1);
  Check("item database available",ItemDatabase.Instance!=null);
  Check("real command wheel retained",player.GetComponent<BuddyCommandWheelController>()!=null);
  Check("test adapter reaches Fire observation point",controls.Warp("fire.watch"));
  for(int i=0;i<90;i++)yield return null;
  Vector2 before=body.position;yield return Press(Key.D,45);
  Check("real keyboard action moves full player",Vector2.Distance(before,body.position)>.2f);
  var pause=UnityEngine.Object.FindAnyObjectByType<PauseMenuController>();pause.OpenPause();yield return null;before=body.position;yield return Press(Key.D,25);
  Check("real pause blocks world motion",pause.IsPauseOpen&&Time.timeScale==0&&Vector2.Distance(before,body.position)<.01f);
  var runView=pause.pausePanel.GetComponent<PauseMenuRunView>();runView.ShowJournal();yield return null;yield return Capture("real-journal");runView.ShowDevState();yield return null;yield return Capture("real-data-tab");
  Check("existing Journal and Data views populated",pause.pausePanel.activeInHierarchy&&pause.pausePanel.GetComponentsInChildren<TMPro.TMP_Text>().Any(text=>text.text.Contains(region.LayoutId))&&DevStateSnapshot.Build(campaign).Any(section=>section.heading=="CURRENT WORLD REGION"));
  pause.ClosePause();yield return null;Check("resume restores gameplay",Time.timeScale>0&&SporeInputReader.Instance.Context==SporeInputContext.Gameplay);
  controls.Warp("fire.watch");for(int i=0;i<60;i++)yield return null;
  double pointerError=0;foreach(var delta in new[]{Vector2.zero,new Vector2(2,3),new Vector2(-4,1)})
  {Vector2 world=body.position+delta;var screen=region.RegionCamera.WorldToScreenPoint(region.Render(world));pointerError=Math.Max(pointerError,Vector2.Distance(world,binding.PointerToWorld(screen)));}
  Check("inverse pointer world roundtrip under 0.001",pointerError<.001);
  var actorView=player.GetComponent<Stage59ActorView>();yield return new WaitForEndOfFrame();Check("projected feet match camera contract",Vector2.Distance(actorView.ProjectedFoot,region.Render(actorView.PhysicalFoot))<.001f);
  yield return CheckPromptAnchors(region,"fire");yield return CheckPromptAnchors(region,"squad");yield return CheckPromptAnchors(region,"run.departure");
  var fire=UnityEngine.Object.FindAnyObjectByType<CampFireRecovery>();Check("real station colliders are interaction triggers",UnityEngine.Object.FindObjectsByType<CampStationMarker>(FindObjectsSortMode.None).All(station=>station.GetComponentsInChildren<Collider2D>(true).All(c=>c.isTrigger)));Check("test adapter reaches Fire approach",controls.Warp("fire"));yield return new WaitForFixedUpdate();for(int i=0;i<4;i++)yield return null;yield return Press(Key.F,2);before=body.position;yield return Press(Key.D,25);
  Check("Fire modal blocks real movement",fire.fireMenuPanel.activeInHierarchy&&SporeInputReader.Instance.Context!=SporeInputContext.Gameplay&&Vector2.Distance(before,body.position)<.01f);
  fire.eatAndRestButton.onClick.Invoke();yield return null;Check("real Fire recovery heals canonical leader",campaign.GetLeader().health==campaign.GetLeader().maxHealth&&player.health==player.maxHealth);
  player.health=Math.Max(1,player.maxHealth-1);campaign.GetLeader().health=player.health;fire.eatAndRestButton.onClick.Invoke();Check("Fire visit limit retained",player.health<player.maxHealth);yield return Capture("real-fire-menu");fire.CloseMenu();yield return null;
  var snack=ItemDatabase.Instance.GetDefinitions().FirstOrDefault(item=>item!=null&&item.category==ItemCategory.Food&&item.persistence==ItemPersistence.Persistent&&item.HasTrait(ItemTrait.CampfireSnack)&&item.CanUseAt(ItemUseLocation.Campfire)&&item.CanTarget(ItemTargetType.Leader)&&item.effects.Count>0);
  Check("existing snack definition present",snack!=null);
  if(snack!=null)
  {InventoryService.TryAdd(campaign,snack,2);int count=InventoryService.GetQuantity(campaign,snack.NormalizedId);var result=CampfireSnackService.CommitFeeding(campaign,snack.NormalizedId,ItemTargetType.Leader,campaign.GetLeader().uniqueId);Check("real snack transaction consumes one item",result.success&&InventoryService.GetQuantity(campaign,snack.NormalizedId)==count-1);Check("real snack cycle limit rejects repeat",!CampfireSnackService.CommitFeeding(campaign,snack.NormalizedId,ItemTargetType.Leader,campaign.GetLeader().uniqueId).success);}
  var squad=UnityEngine.Object.FindAnyObjectByType<CampSquadSelect>();controls.Warp("squad");yield return null;Check("Squad has logical approach",squad.CanInteract(player));yield return Press(Key.F,2);yield return Capture("real-squad-menu");Check("real Squad modal opens",SporeInputReader.Instance.Context!=SporeInputContext.Gameplay);Canvas.ForceUpdateCanvases();Check("Squad names and stats are not clipped at 1080p",squad.activeListParent.GetComponentsInChildren<TMPro.TMP_Text>().All(t=>!t.isTextOverflowing));
  var move=squad.activeListParent.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name=="MoveButton");move.onClick.Invoke();yield return null;Check("real Squad UI moves one identity to reserve",campaign.GetActiveSquadUnits().Count==4&&campaign.GetReserveGobboUnits().Count==1);squad.reserveListParent.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name=="MoveButton").onClick.Invoke();yield return null;Check("real Squad UI restores full active capacity",campaign.GetActiveSquadUnits().Count==5&&campaign.GetReserveGobboUnits().Count==0);
  squad.activeListParent.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name=="SuccessorButton").onClick.Invoke();yield return null;Check("real Squad UI marks canonical successor",!string.IsNullOrEmpty(campaign.markedSuccessorId));squad.activeListParent.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name=="SuccessorButton").onClick.Invoke();yield return null;Check("real Squad UI clears successor without losing ownership",string.IsNullOrEmpty(campaign.markedSuccessorId)&&campaign.ownedGobbos.All(u=>u.campResidentialSlotId>0));player=spawner.SpawnedPlayer;body=player.GetComponent<Rigidbody2D>();binding=player.GetComponent<ActorTerrainBinding>();
  squad.CloseMenu();yield return null;
  Check("Bones first-death lock retained",!UnityEngine.Object.FindAnyObjectByType<CampOldBonesWall>().CanInteract(player));
  yield return CheckPromptAnchors(region,SettlementCampLayout.RootId);
  Check("test adapter reaches root front",controls.Warp(SettlementCampLayout.RootId));yield return null;var root=UnityEngine.Object.FindAnyObjectByType<CampRootEntrance>();yield return Press(Key.F,2);float rootDeadline=Time.realtimeSinceStartup+45;
  while(!region.ResidentialEntranceOpen&&Time.realtimeSinceStartup<rootDeadline){yield return Press(Key.F,2);yield return new WaitForSecondsRealtime(.15f);}
  Check("real physically reachable Buddy opens one root",region.ResidentialEntranceOpen);
  if(!region.ResidentialEntranceOpen)
  {
   var diagnostic=new System.Text.StringBuilder("Root failure diagnostics / existing actors\n");diagnostic.Append("ready=").Append(region.Ready).Append(" inputReady=").Append(controller.WorldInputReady).Append(" keyboardEnabled=").Append(keyboard.enabled).Append(" focused=").Append(Application.isFocused).Append(" input=").Append(SporeInputReader.Instance.Context).Append(" timeScale=").Append(Time.timeScale).Append(" player=").Append(region.LogicalPosition(player.transform.position).X).Append(',').Append(region.LogicalPosition(player.transform.position).Y).Append(" front=").Append(SettlementCampLayout.RootFront.X).Append(',').Append(SettlementCampLayout.RootFront.Y).Append(" rejectedRoutes=").Append(region.RejectedRoutes).AppendLine();
   foreach(var buddy in UnityEngine.Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None))
   {var point=region.LogicalPosition(buddy.transform.position);var ext=TileMover.GetMapClearanceExtents(buddy.GetComponent<Rigidbody2D>(),.25f);diagnostic.Append(buddy.name).Append(" logical=").Append(point.X).Append(',').Append(point.Y).Append(" extents=").Append(ext).Append(" fits1=").Append(region.State.Fits(point,1)).Append(" fits3=").Append(region.State.Fits(point,3)).Append(" route1=").Append(region.State.Route(point,SettlementCampLayout.RootFront,1).Count).Append(" route3=").Append(region.State.Route(point,SettlementCampLayout.RootFront,3).Count).Append(" actorRoute=").Append(region.Route(buddy.transform.position,region.ResidentialEntranceWorld,ext).Count).Append(" walker=").Append(buddy.GetComponent<CampDirectedWalk>()?.Result).AppendLine();}
   diagnostic.AppendLine("Diagnostic direct real root service result: "+region.TryOpenRoot(player));File.WriteAllText(Path.Combine(directory,"root-failure.txt"),diagnostic.ToString());Finish();yield break;
  }
  controls.Warp("fire.watch");for(int i=0;i<90;i++)yield return null;Stage61RuntimeMetrics.Mark("steady five Buddies optional root open");for(int frame=0;frame<600;frame++)yield return null;Stage61RuntimeMetrics.Mark("Camp excavation");
  float idleDeadline=Time.realtimeSinceStartup+45;while(CampStartRoutineManager.Instance.IsCampVisitRoutineRunning&&Time.realtimeSinceStartup<idleDeadline)yield return null;
  Check("first residential duty batch completes",!CampStartRoutineManager.Instance.IsCampVisitRoutineRunning);
  // Positioning is a test adapter; excavation itself uses the real controller's bound Shift action.
  var slotSeven=region.GetResidentialSlot(7);var approach=region.CellToWorld(new Vector2Int(slotSeven.Approach.x,slotSeven.Approach.y));
  Check("player can stand at unopened lower pocket approach",TileMover.CanOccupyBox(region,approach,TileMover.GetMapClearanceExtents(body,.25f)));body.position=approach;body.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();for(int i=0;i<90;i++)yield return null;
  binding.TryPath(body.position,region.World(SettlementCampLayout.RootFront),out var priorRoute);int priorRevision=binding.Revision,priorDigAttempts=binding.DigAttempts;
  InputSystem.QueueStateEvent(mouse,new MouseState{position=region.RegionCamera.WorldToScreenPoint(region.Render(body.position+Vector2.left*.8f))});for(int i=0;i<6;i++)yield return null;
  InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.A,Key.LeftShift));for(int i=0;i<12;i++){InputSystem.QueueStateEvent(mouse,new MouseState{position=region.RegionCamera.WorldToScreenPoint(region.Render(body.position+Vector2.left*.8f))});yield return null;}InputSystem.QueueStateEvent(keyboard,new KeyboardState());for(int i=0;i<6;i++)yield return null;
  var sevenFootprint=region.GetResidentialSlotFootprint(7);int opened=sevenFootprint.Count(cell=>!region.IsBlocked(cell));
  File.WriteAllText(Path.Combine(directory,"dig-input-diagnostic.txt"),"attempts "+priorDigAttempts+" -> "+binding.DigAttempts+" revision "+priorRevision+" -> "+region.Revision+" opened "+opened+" / "+sevenFootprint.Count+" context "+SporeInputReader.Instance.Context+" aim "+player.CurrentAimDirection);
  Check("real Shift digging partially excavates future bed",binding.DigAttempts>priorDigAttempts&&region.Revision>priorRevision&&opened>0&&opened<sevenFootprint.Count);
  Check("real excavation invalidates stale routes",binding.Revision>priorRevision&&!binding.AcceptsRoute(priorRevision)&&binding.Ready());
  controls.Warp("fire.watch");for(int i=0;i<90;i++)yield return null;
  int protectedRevision=region.Revision;var protectedPoint=region.World(region.Definition.Marker("bones").Anchor);var blocked=region.WorldToCell(protectedPoint);var rejected=region.DigCircle(protectedPoint,.12f,TerrainDigAuthority.Player,0,null);
  Check("terrain service rejects protected excavation",!rejected.Changed&&region.Revision==protectedRevision&&region.IsBlocked(blocked));
  var pile=UnityEngine.Object.FindAnyObjectByType<CampResourcePile>();Check("both real resource sprites use projected views",pile!=null&&pile.resourceVisuals.Length==2&&pile.resourceVisuals.All(item=>item.spriteRenderer!=null&&item.spriteRenderer.forceRenderingOff));
  controls.Warp("storage");for(int i=0;i<90;i++)yield return null;yield return Capture("real-resource-pile");
  var caption=pile.popupPanel.GetComponent<RectTransform>();var captionCanvas=caption.GetComponentInParent<Canvas>();Vector2 captionScreen=RectTransformUtility.WorldToScreenPoint(null,caption.position);Vector2 expectedCaption=region.RegionCamera.WorldToScreenPoint(region.Render(region.World(region.Definition.Marker("storage").Anchor)));expectedCaption.y+=55f*Screen.height/1080f;
  Check("resource caption has its own compact card, not the shared message canvas",pile.popupPanel.GetComponent<Canvas>()==null&&caption.rect.width<=260&&caption.rect.height<=80&&pile.popupText.transform.IsChildOf(caption));Check("resource caption follows projected pile contact",Vector2.Distance(captionScreen,expectedCaption)<2&&captionCanvas!=null);
  Check("real resource popup reports campaign balances",pile.popupText!=null&&pile.popupText.text.Contains(CampResourceService.GetAmount(campaign,CampResourceType.Mushrooms).ToString())&&pile.popupText.text.Contains(CampResourceService.GetAmount(campaign,CampResourceType.Shinies).ToString()));controls.Warp("fire.watch");for(int i=0;i<60;i++)yield return null;
  // Batch recruitment is a development adapter into existing canonical services, not run recruitment proof.
  for(int i=0;i<5;i++)controls.AddBuddy();
  Check("squad capacity retained with five real reserves",campaign.GetActiveSquadUnits().Count==5&&campaign.GetReserveGobboUnits().Count==5);
  constructionDeadline=Time.realtimeSinceStartup+240;last=5;
  while(campaign.campTerrainState.residentialSlotsEstablished<10&&Time.realtimeSinceStartup<constructionDeadline&&errors.Count==0)
  {if(last!=campaign.campTerrainState.residentialSlotsEstablished){last=campaign.campTerrainState.residentialSlotsEstablished;yield return Capture("fire-real-construction-"+last);}yield return null;}
  Check("all ten real Buddy beds constructed",campaign.campTerrainState.residentialSlotsEstablished==10);
  Check("ten stable real bed assignments",campaign.ownedGobbos.Count(unit=>unit.campResidentialSlotId>0)==10&&campaign.ownedGobbos.Select(unit=>unit.campResidentialSlotId).Distinct().Count()==10);
  Check("temporary thought cues expire after real activity",BuddyHomeThought.ActiveCount==0);
  yield return Capture("first-ten-beds");
  if(campaign.campTerrainState.residentialSlotsEstablished!=10||errors.Count>0){Finish();yield break;}
  idleDeadline=Time.realtimeSinceStartup+45;while(CampStartRoutineManager.Instance.IsCampVisitRoutineRunning&&Time.realtimeSinceStartup<idleDeadline)yield return null;
  var casualty=UnityEngine.Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None).First(unit=>unit.unitData.uniqueId=="development-b-buddy-3");int vacant=casualty.unitData.campResidentialSlotId;int beforeDeath=campaign.deathHistory.Count;string deadId=casualty.unitData.uniqueId;int beforeVacancyRevision=region.Revision;
  casualty.TakeDamage(1000000);yield return null;yield return null;
  Check("real Buddy death updates canonical roster and history",!campaign.ownedGobbos.Any(unit=>unit.uniqueId==deadId)&&campaign.deathHistory.Count==beforeDeath+1);
  var previousIds=new HashSet<string>(campaign.ownedGobbos.Select(unit=>unit.uniqueId));controls.AddBuddy();var newcomer=campaign.ownedGobbos.First(unit=>!previousIds.Contains(unit.uniqueId));
  Check("development identity never reuses the deceased identity",newcomer.uniqueId!=deadId);
  idleDeadline=Time.realtimeSinceStartup+90;while((newcomer.campResidentialSlotId!=vacant||CampStartRoutineManager.Instance.IsCampVisitRoutineRunning)&&Time.realtimeSinceStartup<idleDeadline&&errors.Count==0)yield return null;
  Check("real vacancy reused without new terrain excavation",newcomer.campResidentialSlotId==vacant&&region.Revision==beforeVacancyRevision&&campaign.campTerrainState.residentialSlotsEstablished==10);
  yield return CheckPromptAnchors(region,"bones");
  var bones=UnityEngine.Object.FindAnyObjectByType<CampOldBonesWall>();Check("test adapter reaches Bones approach",controls.Warp("bones"));for(int i=0;i<90;i++)yield return null;Check("real first death unlocks Bones",bones.CanInteract(player));controls.Warp("bones");yield return new WaitForFixedUpdate();for(int i=0;i<4;i++)yield return null;yield return Capture("real-bones-prompt");yield return Press(Key.F,2);yield return Capture("real-bones-history");Check("real Bones panel opens through input",bones.panel.activeInHierarchy);bones.ClosePanel();yield return null;Check("canonical death survives the development autosave",SporeSaveManager.LoadSlot(2).deathHistory.Any(record=>record.gobboId==deadId));
  SporeSaveManager.SaveCurrentSlotFromGameState();
  var saved=SporeSaveManager.LoadSlot(2);var physical=region.CaptureRegionSnapshot(campaign);
  Check("versioned campaign and region committed together",saved.campTerrainState.regionSnapshot.terrainRevision==physical.terrainRevision&&saved.campTerrainState.regionSnapshot.establishedBeds.Length==10);
  File.WriteAllText(Path.Combine(directory,"established-c-fixture.json"),File.ReadAllText(SporeSaveManager.GetSlotPath(2)));
  string originalRegion=JsonUtility.ToJson(physical);var originalRoster=campaign.ownedGobbos.Select(unit=>unit.uniqueId+":"+unit.campResidentialSlotId).OrderBy(id=>id).ToArray();
  Check("real save read does not write",ReadDoesNotWrite(2));
  CampArrivalContext.SetPending(CampArrivalMode.LoadedSave);
  yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("MainMenu");yield return null;
  Check("terrain unregisters on Camp teardown",DiggableTerrainService.Active==null);
  Check("real campaign reload",SporeSaveManager.LoadSlotIntoGameState(2));
  yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(GameSceneCatalog.Camp);
  deadline=Time.realtimeSinceStartup+90;
  while(Time.realtimeSinceStartup<deadline){controller=UnityEngine.Object.FindAnyObjectByType<CampSceneController>();region=UnityEngine.Object.FindAnyObjectByType<SettlementCampRegion>();if(controller!=null&&controller.WorldInputReady&&region!=null&&region.Ready)break;yield return null;}
  Check("restored Camp becomes ready",controller!=null&&controller.WorldInputReady&&region!=null&&region.Ready);
  Check("physical edits root and ten beds survive scene reload",region!=null&&JsonUtility.ToJson(region.CaptureRegionSnapshot(GameState.Instance))==originalRegion);
  Check("real Gobbo bed ownership survives scene reload",GameState.Instance.ownedGobbos.Select(unit=>unit.uniqueId+":"+unit.campResidentialSlotId).OrderBy(id=>id).SequenceEqual(originalRoster));
  Check("single authorities after reload",UnityEngine.Object.FindObjectsByType<GameState>(FindObjectsSortMode.None).Length==1&&UnityEngine.Object.FindObjectsByType<CampSceneController>(FindObjectsSortMode.None).Length==1&&ReferenceEquals(DiggableTerrainService.Active,region));
  yield return Capture("restored-camp-ten-beds");
  yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("MainMenu");yield return null;
  string savedB=CampaignSnapshotStorage.Hash(File.ReadAllText(SporeSaveManager.GetSlotPath(2)));
  var freshA=SporeSaveManager.CreateNewGame(1,"Development Fresh A","SampleScene",false);
  Check("fresh development A uses real New Game serializer",freshA!=null&&SporeSaveManager.ShouldLoadIntro(SporeSaveManager.LoadSlot(1)));
  var recoveryC=SporeSaveManager.CreateNewGame(3,"Development Recovery C","SampleScene",false);
  StoryEventService.Complete(GameState.Instance,StoryEventIds.ASporeHatchesCampDiscovered);SporeSaveManager.SaveCurrentSlotFromGameState();
  string pathC=SporeSaveManager.GetSlotPath(3);string knownGood=File.ReadAllText(pathC+".bak");
  File.WriteAllText(pathC,"owned synthetic corrupt development C primary");
  Check("corrupt campaign is occupied and reports recovery",SporeSaveManager.InspectSlot(3)==SporeSaveManager.SlotStatus.RecoveryAvailable&&SporeSaveManager.CreateNewGame(3,"Must not overwrite","SampleScene",false)==null);
  SporeSaveManager.RecoverDevelopmentSlot(3);
  Check("explicit C recovery restores known-good backup",File.ReadAllText(pathC)==knownGood&&Directory.GetFiles(Path.GetDirectoryName(pathC),"slot_3.json.failed-*").Length==1);
  var ids=new HashSet<string>();for(int slot=1;slot<=3;slot++){var envelope=DevelopmentCampaignSnapshot.Decode(File.ReadAllText(SporeSaveManager.GetSlotPath(slot)),slot,out _);ids.Add(envelope.campaignId);}
  Check("three distinct owned development campaigns",ids.Count==3&&SporeSaveManager.FindFirstEmptySlot()==0);
  Check("A and C operations preserve established B bytes",CampaignSnapshotStorage.Hash(File.ReadAllText(SporeSaveManager.GetSlotPath(2)))==savedB);
  Check("restore selected real B after fixture work",SporeSaveManager.LoadSlotIntoGameState(2));
  Finish();
 }
 IEnumerator CheckPromptAnchors(SettlementCampRegion region,string markerId)
 {
  var actor=UnityEngine.Object.FindAnyObjectByType<CampPlayableSpawner>().SpawnedPlayer;var body=actor.GetComponent<Rigidbody2D>();var original=body.position;var marker=region.Definition.Marker(markerId);var origin=region.World(marker.Interaction);bool aligned=true,compact=true;
  foreach(var direction in new[]{Vector2.up,Vector2.down,Vector2.left,Vector2.right})
  {
   var target=origin+direction*.35f;if(!TileMover.CanOccupyBox(region,target,TileMover.GetMapClearanceExtents(body,.25f))){aligned=false;continue;}
   body.position=target;body.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();yield return new WaitForFixedUpdate();for(int i=0;i<6;i++)yield return null;yield return new WaitForEndOfFrame();
   var detector=UnityEngine.Object.FindAnyObjectByType<CampInteractionDetector>();var rect=detector.promptPanel.GetComponent<RectTransform>();var canvas=rect.GetComponentInParent<Canvas>();Vector2 actual=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.position);IProjectedInteractionAnchor visible=markerId==SettlementCampLayout.RootId?(IProjectedInteractionAnchor)UnityEngine.Object.FindAnyObjectByType<CampRootEntrance>():UnityEngine.Object.FindObjectsByType<CampStationMarker>(FindObjectsSortMode.None).First(m=>m.markerId==markerId);Vector2 expected=region.RegionCamera.WorldToScreenPoint(visible.ProjectedPromptAnchor);expected.y+=26f*Screen.height/1080f;
   aligned&=detector.promptPanel.activeInHierarchy&&Vector2.Distance(actual,expected)<2;compact&=detector.promptText.fontSize<=24&&rect.rect.width<=300&&rect.rect.height<=45;
  }
  Check("projected prompt tracks visual anchor from four approaches / "+markerId,aligned);Check("compact prompt / "+markerId,compact);yield return Capture("prompt-"+markerId.Replace('.','-'));body.position=original;body.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();
 }
 bool ReadDoesNotWrite(int slot){string path=SporeSaveManager.GetSlotPath(slot);string before=File.ReadAllText(path);var stamp=File.GetLastWriteTimeUtc(path);SporeSaveManager.LoadSlot(slot);return before==File.ReadAllText(path)&&stamp==File.GetLastWriteTimeUtc(path);}
 IEnumerator Press(Key key,int frames){keyboard.MakeCurrent();InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));for(int i=0;i<frames;i++){yield return null;if(i<2)File.AppendAllText(Path.Combine(directory,"input-events.csv"),key+","+Time.frameCount+","+keyboard.enabled+","+Application.isFocused+","+SporeInputReader.Instance.Context+","+SporeInputReader.Instance.Interact.StartedThisFrame+","+SporeInputReader.Instance.Interact.IsHeld+"\n");}InputSystem.QueueStateEvent(keyboard,new KeyboardState());for(int i=0;i<3;i++)yield return null;}
 IEnumerator Capture(string name){Stage61RuntimeMetrics.Mark(name);yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(Path.Combine(directory,name+".png"),image.EncodeToPNG());}finally{Destroy(image);}}
 void Check(string name,bool passed){checks.Add((passed?"PASS ":"FAIL ")+name);File.WriteAllLines(Path.Combine(directory,"checks-in-progress.txt"),checks);}
 [Serializable]sealed class Result{public bool finished;public string error;public string[] checks;public string[] runtimeErrors;}
 void Finish(){if(finished)return;finished=true;if(errors.Count>0){var routine=CampStartRoutineManager.Instance;if(routine!=null)errors.Add("Residential detail: "+routine.LastResidentialFailureReason);}bool ok=errors.Count==0&&checks.TrueForAll(c=>c.StartsWith("PASS "));File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(new Result{finished=true,error=ok?"":"Real-service smoke validation failed",checks=checks.ToArray(),runtimeErrors=errors.ToArray()},true));Application.Quit(ok?0:1);}
 void OnDestroy(){Application.logMessageReceived-=Observe;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);}
}
#endif
