#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;using System.Globalization;using System.IO;using System.Linq;using System.Collections;using System.Collections.Generic;using UnityEngine;using UnityEngine.SceneManagement;using UnityEngine.InputSystem;using UnityEngine.InputSystem.LowLevel;using SporeGobbo.Safety;using SporeGobbo.Input;
// Bounded real-service audit. Fixture import and automated player input are labeled adapters.
// Real AI, damage owners, protected resolution and coordinator are never replaced.
public sealed class Stage64BPostAuditEvidence:MonoBehaviour
{
 readonly List<string> checks=new List<string>(),errors=new List<string>(),rounds=new List<string>();
 struct ReceiptRow{public int round,damage;public float seconds;public string source,target;public bool accepted;public EnemyDamageFailure failure;}
 readonly ReceiptRow[] receipts=new ReceiptRow[20000];int receiptCount;
 readonly Dictionary<int,Counts> counts=new Dictionary<int,Counts>();
 sealed class Counts{public string name,kind;public int attempts,hits,damage;public float first=-1,acquired=-1;public Vector2 start;public Component ai;public System.Reflection.FieldInfo target;public int lastTarget,targetTransitions;}
 string directory,scenario;bool ended;float deadline,roundStart;int round=-1;CampFightPitLab lab;SettlementCampRegion region;GobboController player;Keyboard keyboard;Mouse mouse;Gamepad pad;GameObject[] actors;Vector2[] last;float[] idle;int receiptOverflow;
 static T Find<T>()where T:UnityEngine.Object=>UnityEngine.Object.FindAnyObjectByType<T>();
 static bool Camp()=>Find<CampSceneController>()?.WorldInputReady==true&&Find<SettlementCampRegion>()?.Ready==true;
 static string N(float v)=>v.ToString("R",CultureInfo.InvariantCulture);
 static string Csv(string s)=>"\""+(s??"").Replace("\"","\"\"")+"\"";
 static string Kind(GameObject a)=>a.GetComponent<GobboController>()!=null?"Player":a.GetComponent<BuddyUnit>()!=null?"Buddy":a.GetComponent<BlobSpitterEnemy>()!=null?"Blob":"Weevil";
 void Awake(){scenario=Environment.GetEnvironmentVariable("SPORE_STAGE61_CAPTURE");if(scenario!="audit-singles"&&scenario!="audit-mixed"&&scenario!="audit-resets"&&scenario!="audit-idle")throw new InvalidOperationException("Unreviewed audit scenario");directory=Persistence.ExperimentPath("Evidence");Directory.CreateDirectory(directory);deadline=Time.realtimeSinceStartup+1150;Application.logMessageReceived+=Observe;EnemyDamage.EvidenceReceipt+=Receipt;rounds.Add("round,watch,preset,opponents,activeSeconds,terminal,acceptedHits,friendlyHits,opponentHits,projectileLaunches,firstAcceptedWallSeconds,actorDetails");}
 void Observe(string m,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(m+"\n"+trace);}
 void Update(){if(!ended&&(errors.Count>0||Time.realtimeSinceStartup>deadline))Finish();}
 void Receipt(EnemyDamageRequest request,EnemyDamageResult result)
 {
  if(round<0||request?.source==null||!counts.TryGetValue(request.source.GetInstanceID(),out var c))return;
  c.attempts++;if(result.Accepted){c.hits++;c.damage+=result.AppliedDamage;if(c.first<0)c.first=Time.realtimeSinceStartup-roundStart;}
  if(receiptCount<receipts.Length){string target=counts.TryGetValue(result.Target!=null?result.Target.gameObject.GetInstanceID():0,out var t)?t.name:"unregistered";receipts[receiptCount++]=new ReceiptRow{round=round,seconds=Time.realtimeSinceStartup-roundStart,source=c.name,target=target,accepted=result.Accepted,damage=result.AppliedDamage,failure=result.Failure};}else receiptOverflow++;
 }
 void Check(string name,bool pass){Stage61RuntimeMetrics.Category("validation");checks.Add((pass?"PASS ":"FAIL ")+name);File.WriteAllLines(Path.Combine(directory,"checks-in-progress.txt"),checks);}
 IEnumerator Frames(int n){for(int i=0;i<n&&!ended;i++)yield return null;}
 IEnumerator Wait(Func<bool> condition,string name,float seconds=60){float end=Time.realtimeSinceStartup+seconds;while(!ended&&!condition()&&Time.realtimeSinceStartup<end)yield return null;Check(name,!ended&&condition());}
 IEnumerator Release(){InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.QueueStateEvent(pad,new GamepadState());yield return Frames(3);}
 static BuddyUnit[] Residents()=>UnityEngine.Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None).Where(b=>b.CombatAlive&&GameState.Instance.ownedGobbos.Contains(b.unitData)).OrderBy(b=>b.unitData.uniqueId,StringComparer.Ordinal).ToArray();
 void Select(int friends,bool watch,string opponent,int count,string preset){lab.SelectedBuddies.Clear();foreach(var b in Residents().Take(friends))lab.SelectedBuddies.Add(b.unitData.uniqueId);lab.Watch=watch;lab.Opponent=opponent;lab.OpponentCount=count;lab.Preset=preset;}
 void BeginRound()
 {
  actors=lab.Fighters.ToArray();last=new Vector2[actors.Length];idle=new float[actors.Length];counts.Clear();
  for(int i=0;i<actors.Length;i++){last[i]=actors[i].transform.position;var ai=(Component)actors[i].GetComponent<BuddyCombat>()??(Component)actors[i].GetComponent<BlobSpitterEnemy>()??actors[i].GetComponent<TunnelWeevilEnemy>();counts.Add(actors[i].GetInstanceID(),new Counts{name=actors[i].name,kind=Kind(actors[i]),start=last[i],ai=ai,target=ai?.GetType().GetField("currentTarget",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)});}
  roundStart=Time.realtimeSinceStartup;
 }
 void DrivePlayer(bool attack=true)
 {
  GameObject target=null;float nearest=float.PositiveInfinity;
  foreach(var a in actors){if(a==null||a==player.gameObject||a.GetComponent<CombatSessionBinding>()?.Team!=1)continue;float d=((Vector2)a.transform.position-(Vector2)player.transform.position).sqrMagnitude;if(d<nearest){nearest=d;target=a;}}
  if(target==null)return;Vector2 delta=target.transform.position-player.transform.position;Vector2 screen=region.RegionCamera.WorldToScreenPoint(region.Render(target.transform.position));
  InputSystem.QueueStateEvent(mouse,new MouseState{position=screen,buttons=(ushort)(attack?1:0)});
  // Actual semantic keyboard movement and attack input; no direct damage or attack invocation.
  Key move=Mathf.Abs(delta.x)>Mathf.Abs(delta.y)?(delta.x>0?Key.D:Key.A):(delta.y>0?Key.W:Key.S);
  InputSystem.QueueStateEvent(keyboard,delta.magnitude>Mathf.Max(.65f,player.attackRange)?new KeyboardState(move):new KeyboardState());
 }
 string Facts()=>JsonUtility.ToJson(GameState.Instance);
 IEnumerator Shot(string name){Stage61RuntimeMetrics.Category("capture");Stage61RuntimeMetrics.Exclude("Post-audit screenshot");yield return new WaitForEndOfFrame();var t=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(Path.Combine(directory,name+".png"),t.EncodeToPNG());}finally{Destroy(t);}}
 IEnumerator RunRounds(bool mixed,bool resets)
 {
  float familyActive=0;int natural=0;bool blobHit=false,weevilHit=false,buddyHit=false;
  for(int n=0;!ended&&(n<20||mixed&&familyActive<60&&n<28);n++)
  {
   round=n;Stage61RuntimeMetrics.Category("setup");Stage61RuntimeMetrics.Mark(scenario+" round "+n);
   bool watch=resets?n%2==0:mixed?n%3!=0:n<12;
   string opponent=n<4?"Buddy":n<8?"Weevil":n<12?"Blob":n<16?"Weevil":"Blob";
   string preset=resets?new[]{"Open","Rock","Roots","Dirt"}[n%4]:mixed?new[]{"Open","Rock","Roots"}[n%3]:"Open";
   int friends=mixed?(n%4==3?5:2):watch?1:0;
   Select(friends,watch,opponent,1,preset);
   if(!mixed&&!resets&&opponent=="Blob"&&watch&&n%2==1){var grown=Residents().OrderByDescending(b=>b.transform.localScale.x).First();lab.SelectedBuddies.Clear();lab.SelectedBuddies.Add(grown.unitData.uniqueId);}
   string facts=Facts(),physical=JsonUtility.ToJson(region.CaptureRegionSnapshot(GameState.Instance));int hp=player.health;
   var residents=Residents();var data=residents.Select(b=>b.unitData).ToArray();var health=data.Select(d=>d.health).ToArray();var positions=residents.Select(b=>(Vector2)b.transform.position).ToArray();
   if(mixed||resets)lab.StartEvidenceSession("Weevil","Blob");else lab.StartSession();
   if(!mixed&&!resets)
   {
    var axis=n%4==0?Vector2.right:n%4==1?Vector2.left:n%4==2?Vector2.up:Vector2.down;
    if(watch&&opponent=="Blob"&&n%2==1)
    {
     var buddy=lab.Fighters.First(a=>a.GetComponent<BuddyUnit>()!=null);var blob=lab.Fighters.First(a=>a.GetComponent<BlobSpitterEnemy>()!=null);var a=buddy.GetComponent<Collider2D>().bounds;var b=blob.GetComponent<Collider2D>().bounds;
     float gap=(n%4<2?a.extents.x+b.extents.x:a.extents.y+b.extents.y)+.005f;Vector2 position=(Vector2)b.center+axis*gap-((Vector2)a.center-(Vector2)buddy.transform.position);var body=buddy.GetComponent<Rigidbody2D>();
     var cell=region.LogicalPosition(position);bool fits=SporeGobbo.CampCandidate.CampFightPitLayout.InnerCell((int)cell.X,(int)cell.Y)&&TileMover.CanOccupyBox(region,position,TileMover.GetMapClearanceExtents(body,.25f));Check("owned real-body close-range setup / "+n,fits);if(!fits){errors.Add("Invalid contact fixture placement");yield break;}body.position=position;body.linearVelocity=Vector2.zero;buddy.transform.position=position;Physics2D.SyncTransforms();var contact=buddy.GetComponent<Collider2D>().Distance(blob.GetComponent<Collider2D>());for(int adjustment=0;adjustment<3&&contact.isValid&&Mathf.Abs(contact.distance-.005f)>.0005f;adjustment++){position+=axis*(.005f-contact.distance);body.position=position;buddy.transform.position=position;Physics2D.SyncTransforms();contact=buddy.GetComponent<Collider2D>().Distance(blob.GetComponent<Collider2D>());}Check("corrected Pit contact has terrain clearance / "+n,TileMover.CanOccupyBox(region,position,TileMover.GetMapClearanceExtents(body,.25f)));Check("real collider contact without overlap / "+n,contact.isValid&&contact.distance>=-.001f&&contact.distance<=Physics2D.defaultContactOffset*2+.001f);
    }
    else if(n%4!=0)
    {
     var group=lab.Fighters.ToArray();var center=region.World(SporeGobbo.CampCandidate.CampFightPitLayout.Center);var next=new Vector2[group.Length];bool fits=true;
     for(int i=0;i<group.Length;i++){Vector2 d=(Vector2)group[i].transform.position-center;Vector2 r=n%4==1?-d:n%4==2?new Vector2(-d.y,d.x):new Vector2(d.y,-d.x);if(!watch&&opponent=="Blob")r*=.7f;next[i]=center+r;var logical=region.LogicalPosition(next[i]);fits&=SporeGobbo.CampCandidate.CampFightPitLayout.InnerCell((int)logical.X,(int)logical.Y)&&TileMover.CanOccupyBox(region,next[i],TileMover.GetMapClearanceExtents(group[i].GetComponent<Rigidbody2D>(),.25f));}
     Check("collider-validated orientation setup / "+n,fits);if(!fits){errors.Add("Invalid rotated fixture placement");yield break;}for(int i=0;i<group.Length;i++){group[i].GetComponent<Rigidbody2D>().position=next[i];group[i].transform.position=next[i];}Physics2D.SyncTransforms();
    }
   }
   BeginRound();int launches=EnemyProjectile.LaunchCount;float active=0,limit=resets?2.5f+n%3:40;bool paused=false;float nextTargetObservation=0;
   while(!ended&&lab.Fighting&&Time.realtimeSinceStartup-roundStart<limit)
   {
    Stage61RuntimeMetrics.Category("ordinary");if(!watch)DrivePlayer();
    if(Time.realtimeSinceStartup>=nextTargetObservation){nextTargetObservation=Time.realtimeSinceStartup+.25f;foreach(var c in counts.Values){var target=c.target?.GetValue(c.ai) as Transform;int id=target!=null?target.GetInstanceID():0;if(id!=c.lastTarget){c.targetTransitions++;c.lastTarget=id;}if(id!=0&&c.acquired<0)c.acquired=Time.realtimeSinceStartup-roundStart;}}
if(Time.timeScale>0){active+=Time.unscaledDeltaTime;for(int i=0;i<actors.Length;i++){Vector2 p=actors[i].transform.position;if(Vector2.Distance(last[i],p)<.002f)idle[i]+=Time.unscaledDeltaTime;last[i]=p;}}
    if(resets&&n%4==0&&!paused&&active>.5f)
    {paused=true;Stage61RuntimeMetrics.Category("pause-transition");InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.Start));yield return Frames(3);Check("pause during actual match / "+n,Time.timeScale==0);yield return Release();InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.Start));yield return Frames(3);yield return Release();Check("resume during actual match / "+n,Time.timeScale>0);}
    yield return null;
   }
   Stage61RuntimeMetrics.Category("resolved");familyActive+=active;bool resolved=lab.Phase==CampFightPitLab.MatchPhase.Resolved;bool threshold=resolved&&lab.Outcome.Contains("protected 1 HP");if(threshold)natural++;
   float first=-1;var detail=new List<string>();foreach(var a in actors){var c=counts[a.GetInstanceID()];if(c.hits>0){if(c.kind=="Blob")blobHit=true;if(c.kind=="Weevil")weevilHit=true;if(c.kind=="Buddy")buddyHit=true;if(first<0||c.first<first)first=c.first;}int i=Array.IndexOf(actors,a);detail.Add(c.kind+":"+c.attempts+"damageDispatches/"+c.hits+"hits/"+c.damage+"damage/first="+N(c.first)+"/stationarySeconds="+N(idle[i])+"/scale="+N(a.transform.localScale.x)+"/start="+N(c.start.x)+":"+N(c.start.y)+"/end="+N(a.transform.position.x)+":"+N(a.transform.position.y)+"/firstTarget="+N(c.acquired)+"/targetTransitions="+c.targetTransitions+"/identity="+a.GetComponent<BuddyUnit>()?.unitData?.uniqueId+"/age="+a.GetComponent<BuddyUnit>()?.unitData?.ageStage+"/type="+a.GetComponent<BuddyUnit>()?.unitData?.gobboType);}
   rounds.Add(n+","+watch+","+preset+","+Csv(mixed||resets?"Weevil+Blob":opponent)+","+N(active)+","+Csv(lab.Outcome+(lab.Fighting?(resets?" / bounded cancellation":" / TIMEOUT"):""))+","+lab.AcceptedHits+","+lab.FriendlyHits+","+lab.OpponentHits+","+(EnemyProjectile.LaunchCount-launches)+","+N(first)+","+Csv(string.Join(";",detail)));
   Check((resets?"real configured actors before cancellation / ":"real configured actors and combat / ")+n,actors.Length==friends+(watch?0:1)+(mixed||resets?2:1)&&(resets||lab.AcceptedHits>0));
   if(!resets)Check("natural protected threshold resolution / "+n,threshold);if(!mixed&&!resets&&watch&&opponent=="Blob"&&n%2==1)Check("real Buddy-origin bite at validated contact / "+n,lab.FriendlyHits>0);
   if(n==0||n==7||n==19)yield return Shot(scenario+"-"+n);
   yield return Release();Stage61RuntimeMetrics.Category("reset");lab.StopSession("bounded post-audit round "+n);
   Check("exact player health and canonical facts restored / "+n,!lab.Active&&!lab.CleanupBlocked&&player.health==hp&&Facts()==facts);
   Check("same roster data HP and return positions / "+n,Enumerable.Range(0,residents.Length).All(i=>residents[i]!=null&&object.ReferenceEquals(residents[i].unitData,data[i])&&data[i].health==health[i]&&Vector2.Distance(residents[i].transform.position,positions[i])<.001f));
   Check("only owned temporary terrain released / "+n,JsonUtility.ToJson(region.CaptureRegionSnapshot(GameState.Instance))==physical);
   round=-1;yield return Frames(3);
   Check("session object and camera cleanup / "+n,!UnityEngine.Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Any(p=>object.ReferenceEquals(p.SessionRules,lab))&&UnityEngine.Object.FindObjectsByType<CombatSessionBinding>(FindObjectsSortMode.None).All(b=>b.Rules==null)&&region.Presentation.CanFrameStory&&!Find<CampStartRoutineManager>().CombatActivitySuspended);
   if(lab.CleanupBlocked||lab.Active){errors.Add("Stop failed to release activity; further rounds refused");yield break;}
  }
  Check("at least twenty bounded completed round records",rounds.Count>=21&&rounds.Count<=(mixed?29:21));Check("bounded receipt buffer has no overflow",receiptOverflow==0);
  if(!resets){Check("all bounded natural rounds resolved at protected threshold",natural==rounds.Count-1);Check("at least sixty actual active fighting seconds",familyActive>=60);Check("real Buddy-origin accepted damage",buddyHit);if(mixed)Check("both existing enemy types commit actual damage",blobHit&&weevilHit);}
  Stage61RuntimeMetrics.Category("validation");File.WriteAllText(Path.Combine(directory,"active-duration.txt"),"Active Fighting seconds="+N(familyActive)+"; natural protected resolutions="+natural+"; Blob receipts="+blobHit+"; Weevil receipts="+weevilHit+"; Buddy receipts="+buddyHit+". No healing/relabeling of resolved rounds.");
 }
 bool FaultLeasesReleased(string label)
 {
  var view=region.Presentation;var ownerField=view!=null?view.GetType().GetField("storyOwner",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic):null;
  bool camera=view!=null&&ownerField!=null&&ownerField.GetValue(view)==null;
  bool anchors=!UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t=>t.name=="Owned Pit camera focus"||t.name=="Owned Pit reachable rally");
  bool routines=!Find<CampStartRoutineManager>().CombatActivitySuspended;bool bindings=UnityEngine.Object.FindObjectsByType<CombatSessionBinding>(FindObjectsSortMode.None).All(b=>b.Rules==null);
  File.AppendAllText(Path.Combine(directory,"fault-release-state.csv"),Csv(label)+","+camera+","+anchors+","+routines+","+bindings+","+(view!=null&&view.CanFrameStory)+"\n");
  return camera&&anchors&&routines&&bindings;
 }
 IEnumerator ModalAndIdentityBoundaries()
 {
  Stage61RuntimeMetrics.Category("validation");Select(1,true,"Weevil",1,"Open");var marker=new GameObject("Owned foreign nonpausing modal");var ui=SporeUiCoordinator.Instance;ui.PushModal(marker,null,false);yield return Frames(2);bool refused=false;try{lab.StartSession();}catch(InvalidOperationException){refused=true;}Check("foreign nonpausing modal refuses before session movement",refused&&!lab.Active);ui.PopModal(marker);Destroy(marker);yield return Frames(2);
  SporeSaveManager.SaveCurrentSlotFromGameState();lab.StartSession();var leader=GameState.Instance.leader;GameState.Instance.leader=leader.CloneUnit();player.health=Mathf.Max(2,player.health-3);player.enabled=false;int hp=player.health;lab.StopSession("owned canonical leader replacement fault");yield return Frames(3);
  Check("same campaign changed leader blocks restoration and save",lab.CleanupBlocked&&player.health==hp&&!player.enabled&&lab.Active);bool blocked=false;try{SporeSaveManager.SaveCurrentSlotFromGameState();}catch(InvalidOperationException){blocked=true;}Check("leader identity fault refuses ordinary save",blocked);GameState.Instance.leader=leader;
  Check("explicit existing committed recovery releases failed session",lab.RecoverCommittedCampaign(2)&&!lab.Active);SceneManager.LoadScene(GameSceneCatalog.MainMenu);yield return Wait(()=>Find<MainMenuController>()!=null,"Menu after owned identity fault recovery");yield return Frames(4);Find<MainMenuController>().LoadGame(2);yield return Wait(Camp,"Continue after identity fault recovery");region=Find<SettlementCampRegion>();lab=Find<CampFightPitLab>();player=Find<CampPlayableSpawner>().SpawnedPlayer;
  // Deliberate owned boundary faults, not natural deaths or execution gameplay.
  foreach(bool removeComponent in new[]{false,true})
  {
   SporeSaveManager.SaveCurrentSlotFromGameState();string beforeFaultCommit=CampaignSnapshotStorage.Read(SporeSaveManager.GetSlotPath(2));Select(1,true,"Weevil",1,"Open");lab.StartSession();var watcher=player.GetComponent<PlayerDeathWatcher>();if(watcher!=null)watcher.enabled=false; // Owned boundary adapter: disable watcher while healthy; real death is tested separately.
   if(removeComponent)Destroy(player);
   else{player.health=0;typeof(GobboController).GetField("isDead",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(player,true);player.enabled=false;GameState.Instance.leader.isDead=true;}
   yield return Frames(3);lab.StopSession("owned dead/removed leader boundary fixture");yield return Frames(3);
   Check("dead or removed controller is never restored / "+removeComponent,lab.CleanupBlocked&&(removeComponent?player==null:player.IsDead&&player.health==0&&!player.enabled&&GameState.Instance.leader.isDead));
   Check("fault releases owned camera routines and bindings / "+removeComponent,FaultLeasesReleased("owned fault release"));
   blocked=false;try{SporeSaveManager.SaveCurrentSlotFromGameState();}catch(InvalidOperationException){blocked=true;}Check("dead or removed controller cannot commit ordinary save / "+removeComponent,(blocked||!removeComponent&&GameState.Instance.leader.isDead)&&lab.CleanupBlocked&&CampaignSnapshotStorage.Read(SporeSaveManager.GetSlotPath(2))==beforeFaultCommit);
   Check("existing committed recovery from dead component fixture / "+removeComponent,lab.RecoverCommittedCampaign(2)&&!lab.Active);SceneManager.LoadScene(GameSceneCatalog.MainMenu);yield return Wait(()=>Find<MainMenuController>()!=null,"real Menu after leader boundary fixture / "+removeComponent);yield return Frames(4);Find<MainMenuController>().LoadGame(2);yield return Wait(Camp,"real Continue after leader boundary fixture / "+removeComponent);region=Find<SettlementCampRegion>();lab=Find<CampFightPitLab>();player=Find<CampPlayableSpawner>().SpawnedPlayer;
  }
  foreach(string fault in new[]{"removed","dead","foreign"})
  {
   Stage61RuntimeMetrics.Category("validation");SporeSaveManager.SaveCurrentSlotFromGameState();string committed=CampaignSnapshotStorage.Read(SporeSaveManager.GetSlotPath(2));Select(1,true,"Weevil",1,"Open");lab.StartSession();var buddy=lab.Fighters.Select(a=>a.GetComponent<BuddyUnit>()).First(b=>b!=null);var canonical=buddy.unitData;var body=buddy.GetComponent<Rigidbody2D>();body.simulated=false;
   if(fault=="removed")GameState.Instance.RemoveGobbo(canonical.uniqueId);
   else if(fault=="dead"){canonical.health=0;canonical.isDead=true;}
   else{buddy.unitData=canonical.CloneUnit();buddy.unitData.health=Mathf.Max(1,canonical.health-2);}
   var actualData=buddy.unitData;int actualHealth=actualData.health;lab.StopSession("owned Buddy "+fault+" boundary fixture");yield return Frames(3);
   Check("invalid Buddy state is never restored / "+fault,lab.CleanupBlocked&&!body.simulated&&object.ReferenceEquals(buddy.unitData,actualData)&&actualData.health==actualHealth&&(!buddy.GetComponent<BuddyBrain>().enabled&&!buddy.GetComponent<BuddyFollow>().enabled&&!buddy.GetComponent<BuddyCombat>().enabled));
   Check("invalid Buddy preserves actual roster fact / "+fault,fault!="removed"||!GameState.Instance.ownedGobbos.Contains(canonical));
   bool saveRefused=false;try{SporeSaveManager.SaveCurrentSlotFromGameState();}catch(InvalidOperationException){saveRefused=true;}Check("invalid Buddy cannot commit contradictory snapshot / "+fault,saveRefused&&CampaignSnapshotStorage.Read(SporeSaveManager.GetSlotPath(2))==committed);
   Check("Buddy fault releases own camera routines and bindings / "+fault,FaultLeasesReleased("owned fault release"));
   Check("actual committed recovery repairs owned Buddy fixture / "+fault,lab.RecoverCommittedCampaign(2)&&!lab.Active);SceneManager.LoadScene(GameSceneCatalog.MainMenu);yield return Wait(()=>Find<MainMenuController>()!=null,"Menu after Buddy boundary fixture / "+fault);yield return Frames(4);Find<MainMenuController>().LoadGame(2);yield return Wait(Camp,"Continue after Buddy boundary fixture / "+fault);region=Find<SettlementCampRegion>();lab=Find<CampFightPitLab>();player=Find<CampPlayableSpawner>().SpawnedPlayer;
  }
 }
 IEnumerator GroupMatrix()
 {
  var rows=new List<string>{"friendlyBuddies,weevils,activeSeconds,terminal,friendlyHits,opponentHits"};
  foreach(var configuration in new[]{(2,1),(5,3)})
  {
   Stage61RuntimeMetrics.Category("setup");Select(configuration.Item1,true,"Weevil",configuration.Item2,"Open");string facts=Facts(),physical=JsonUtility.ToJson(region.CaptureRegionSnapshot(GameState.Instance));lab.StartSession();round=100+configuration.Item1;BeginRound();float end=Time.realtimeSinceStartup+30;
   while(!ended&&lab.Fighting&&Time.realtimeSinceStartup<end){Stage61RuntimeMetrics.Category("ordinary");yield return null;}
   Stage61RuntimeMetrics.Category("validation");Check("natural homogeneous group outcome / "+configuration,lab.Phase==CampFightPitLab.MatchPhase.Resolved&&lab.Outcome.Contains("protected 1 HP")&&lab.FriendlyHits>0&&lab.OpponentHits>0);rows.Add(configuration.Item1+","+configuration.Item2+","+N(Time.realtimeSinceStartup-roundStart)+","+Csv(lab.Outcome)+","+lab.FriendlyHits+","+lab.OpponentHits);yield return Shot("group-"+configuration.Item1+"-vs-"+configuration.Item2);lab.StopSession("homogeneous group proof");round=-1;Check("homogeneous group restores real Camp / "+configuration,!lab.Active&&Facts()==facts&&JsonUtility.ToJson(region.CaptureRegionSnapshot(GameState.Instance))==physical);yield return Frames(3);if(lab.Active){errors.Add("Group cleanup failed");yield break;}
  }
  Stage61RuntimeMetrics.Category("validation");File.WriteAllLines(Path.Combine(directory,"homogeneous-groups.csv"),rows);
 }
 IEnumerator SynchronizedLifetimeBoundaries()
 {
  foreach(string phase in new[]{"AttackWindup","Knockback","ProjectileFlight"})
  {
   Stage61RuntimeMetrics.Category("validation");Select(0,false,phase=="ProjectileFlight"?"Blob":"Weevil",1,"Open");string facts=Facts();lab.StartSession();lab.GodMode=true;actors=lab.Fighters.ToArray();var weevil=Find<TunnelWeevilEnemy>();var state=weevil!=null?weevil.GetType().GetField("combatState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic):null;Check("actual phase source available / "+phase,phase=="ProjectileFlight"||weevil!=null&&state!=null);
   bool observed=false;float end=Time.realtimeSinceStartup+15;
   while(!ended&&lab.Fighting&&Time.realtimeSinceStartup<end)
   {
    observed=phase=="ProjectileFlight"?UnityEngine.Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Any(p=>object.ReferenceEquals(p.SessionRules,lab)):state?.GetValue(weevil)?.ToString()==phase;if(observed)break;
    DrivePlayer(phase!="AttackWindup");yield return null;
   }
   Check("actual synchronized active phase observed / "+phase,observed);lab.StopSession("synchronized "+phase+" cancellation");yield return Release();yield return Frames(3);
   Check("synchronized cancellation restores campaign and releases leases / "+phase,!lab.Active&&Facts()==facts&&FaultLeasesReleased(phase)&&!UnityEngine.Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Any(p=>object.ReferenceEquals(p.SessionRules,lab)));
  }
  // Explicit real-projectile fixtures. Position/speed/lifetime and source HP faults are adapters,
  // not claims of natural AI firing or a lethal campaign event. Damage/physics stay real.
  foreach(string mode in new[]{"impact","multi-child-impact","expiry","dead-source"})
  {
   Stage61RuntimeMetrics.Category("validation");Select(0,false,"Blob",1,"Open");string facts=Facts();lab.StartSession();yield return Frames(2);var blob=lab.Fighters.Select(a=>a.GetComponent<BlobSpitterEnemy>()).First(b=>b!=null);int before=player.health,hits=0;GameObject child=null;int launchesBefore=EnemyProjectile.LaunchCount;Check("projectile fixture begins without another owned shot / "+mode,!UnityEngine.Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Any(p=>object.ReferenceEquals(p.SessionRules,lab)));
   if(mode=="multi-child-impact"){child=new GameObject("Owned projectile child collider fixture");child.layer=player.gameObject.layer;child.transform.SetParent(player.transform,false);var c=child.AddComponent<CircleCollider2D>();c.isTrigger=true;c.radius=.65f;}
   var source=blob.gameObject;Action<EnemyDamageRequest,EnemyDamageResult> receipt=(request,result)=>{if(request?.source==source&&result.Target==player&&result.Accepted)hits++;};EnemyDamage.EvidenceReceipt+=receipt;
   var origin=(Vector2)player.transform.position+Vector2.right*1.6f;var shot=Instantiate(blob.projectilePrefab,origin,Quaternion.identity);if(mode=="expiry"){shot.speed=0;shot.lifetime=.2f;}else shot.speed=mode=="dead-source"?2:30;shot.Launch(Vector2.left,blob.transform);
   float launchedAt=Time.realtimeSinceStartup;if(mode=="dead-source"){blob.health=0;var canContinue=typeof(EnemyProjectile).GetMethod("CanContinue",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Check("actual projectile dead-source guard rejects before coordinator update",canContinue!=null&&!(bool)canContinue.Invoke(shot,null));}
   try{yield return Wait(()=>shot==null,"real projectile is destroyed / "+mode,3);yield return Frames(2);Check("manual shot attribution has no concurrent AI launch / "+mode,EnemyProjectile.LaunchCount==launchesBefore+1);if(mode=="expiry")Check("actual lifetime expiry retains live ready source and session",Time.realtimeSinceStartup-launchedAt>=.15f&&lab.Fighting&&EnemyDamage.Alive(blob)&&EnemyDamage.Ready(blob.gameObject));Check("projectile accepted health and lifetime result / "+mode,mode.Contains("impact")?hits==1&&player.health<before:hits==0&&player.health==before);}finally{EnemyDamage.EvidenceReceipt-=receipt;if(child!=null)Destroy(child);}
   lab.StopSession("projectile lifetime fixture "+mode);yield return Frames(3);Check("projectile fixture restores real Camp / "+mode,!lab.Active&&Facts()==facts&&FaultLeasesReleased(mode));
  }
  foreach(string preset in new[]{"Rock","Roots","Dirt"})
  {
   Stage61RuntimeMetrics.Category("validation");Select(0,false,"Blob",1,preset);string facts=Facts();lab.StartSession();var blob=lab.Fighters.Select(a=>a.GetComponent<BlobSpitterEnemy>()).First(b=>b!=null);float y=preset=="Rock"?86.5f:82.5f;var origin=region.World(new SporeGobbo.Geometry.Point2(159.5,y));var wall=region.World(new SporeGobbo.Geometry.Point2(preset=="Rock"?155.5:152.5,y));Check("real preset obstructs projectile fixture / "+preset,!RegionQueries.ProjectileClear(region,origin,wall,.03f));int impacts=EnemyProjectile.TerrainImpacts;var shot=Instantiate(blob.projectilePrefab,origin,Quaternion.identity);shot.speed=30;shot.Launch(Vector2.left,blob.transform);yield return Wait(()=>shot==null,"fast real projectile destroyed at preset / "+preset,3);Check("actual terrain impact while source is living / "+preset,EnemyDamage.Alive(blob)&&EnemyProjectile.TerrainImpacts>impacts);lab.StopSession("preset projectile fixture");yield return Frames(3);Check("preset projectile restores Camp / "+preset,!lab.Active&&Facts()==facts&&FaultLeasesReleased(preset));
  }
 }
 IEnumerator ActualExpeditionBodies()
 {
  int completed=0;float babyScale=0;var trace=new List<string>{"case,identity,type,age,scale,axis,centreDistance,bodyGap,acceptedBuddyHits,blobHealthBefore,blobHealthAfter,hitCentreDistance,hitBodyGap,physicsContactOffset"};
  for(int pass=0;pass<4&&!ended;pass++)
  {
   Stage61RuntimeMetrics.Category("setup");Stage61RuntimeMetrics.Mark("actual expedition prefab contact "+pass);round=-1;
   var state=GameState.Instance;var residents=Residents();var chosen=pass<2?residents.First(b=>b.unitData.gobboType==BuddyType.Baby):residents.OrderByDescending(b=>b.transform.localScale.x).First();string identity=chosen.unitData.uniqueId;
   foreach(string id in state.activeSquadIds.ToArray())BuddyRosterService.MoveBuddyToReserve(state,id);Check("real roster chooses one actual run Buddy / "+pass,BuddyRosterService.MoveBuddyToActiveSquad(state,identity)&&state.activeSquadIds.Count==1&&state.maxActiveSquad==5);
   string physical=JsonUtility.ToJson(region.CaptureRegionSnapshot(state));var portal=Find<CampRunPortal>();portal.Interact(player);yield return null;portal.goButton.onClick.Invoke();
   yield return Wait(()=>Find<ExpeditionRegion>()?.Player!=null&&Find<ExpeditionRegion>().PresentationReady&&UnityEngine.Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None).Any(u=>u.unitData?.uniqueId==identity&&Find<ExpeditionRegion>().Player.CanControlBuddy(u.GetComponent<BuddyBrain>())),"existing portal reaches actual registered run body proof / "+pass);var run=Find<ExpeditionRegion>();if(run==null||!run.PresentationReady){errors.Add("Actual run not ready");yield break;}
   var leader=run.Player;ExpeditionDevelopmentControls.SetGodMode(true);leader.IssueBuddyCommand(BuddyCommand.Passive);leader.IssueBuddyCommand(BuddyCommand.Stay);
   var staging=run.World(run.Definition.Anchors["blob"]);var leaderBody=leader.GetComponent<Rigidbody2D>();bool clear=TileMover.CanOccupyBox(run,staging,TileMover.GetMapClearanceExtents(leaderBody,.25f));Check("owned existing Blob anchor has leader clearance / "+pass,clear);if(!clear){errors.Add("Blocked run staging anchor");yield break;}leaderBody.position=staging;leaderBody.linearVelocity=Vector2.zero;leader.transform.position=staging;Physics2D.SyncTransforms();
   yield return Wait(()=>Find<BlobSpitterEnemy>()!=null,"actual authored expedition Blob spawns / "+pass,30);var blob=Find<BlobSpitterEnemy>();if(blob==null){errors.Add("Authored Blob unavailable");yield break;}var unit=UnityEngine.Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None).Single(b=>b.unitData.uniqueId==identity);var brain=unit.GetComponent<BuddyBrain>();var body=unit.GetComponent<Rigidbody2D>();var a=unit.GetComponent<Collider2D>();var b=blob.GetComponent<Collider2D>();
   if(pass==0)babyScale=unit.transform.localScale.x;Check("actual Baby versus larger run-body scale / "+pass,pass<2?Mathf.Abs(unit.transform.localScale.x-.75f)<.001f:unit.transform.localScale.x>babyScale);
   Check("real expedition prefab and canonical registration / "+pass,EnemyDamage.Ready(unit.gameObject)&&EnemyDamage.Ready(blob.gameObject)&&object.ReferenceEquals(DiggableTerrainService.Active,run)&&unit.CombatAlive&&unit.GetComponent<BoxCollider2D>()?.size.x==1&&object.ReferenceEquals(unit.unitData,state.ownedGobbos.Single(d=>d.uniqueId==identity))&&leader.CanControlBuddy(brain)&&object.ReferenceEquals(ActorTerrainBinding.Resolve(unit.gameObject),run));
   Vector2 axis=pass==0?Vector2.right:pass==1?Vector2.left:pass==2?Vector2.up:Vector2.down;float separation=(pass<2?a.bounds.extents.x+b.bounds.extents.x:a.bounds.extents.y+b.bounds.extents.y)+.005f;Vector2 contact=(Vector2)b.bounds.center+axis*separation-((Vector2)a.bounds.center-(Vector2)unit.transform.position);
   clear=TileMover.CanOccupyBox(run,contact,TileMover.GetMapClearanceExtents(body,.28f));Check("actual run physical contact fixture has terrain clearance / "+pass,clear);if(!clear){errors.Add("Blocked actual run contact fixture");yield break;}body.position=contact;body.linearVelocity=Vector2.zero;unit.transform.position=contact;Physics2D.SyncTransforms();var distance=a.Distance(b);for(int adjustment=0;adjustment<3&&distance.isValid&&Mathf.Abs(distance.distance-.005f)>.0005f;adjustment++){contact+=axis*(.005f-distance.distance);body.position=contact;unit.transform.position=contact;Physics2D.SyncTransforms();distance=a.Distance(b);}Check("shape-corrected contact retains terrain clearance / "+pass,TileMover.CanOccupyBox(run,contact,TileMover.GetMapClearanceExtents(body,.28f)));float centers=Vector2.Distance(unit.transform.position,blob.transform.position);
   Check("actual run bodies touch beyond nominal centre reach / "+pass,distance.isValid&&distance.distance>=-.001f&&distance.distance<=Physics2D.defaultContactOffset*2+.001f&&centers>unit.GetComponent<BuddyCombat>().attackRange);
   int hits=0,before=blob.health;float atHitCenter=-1,atHitGap=-1;Action<EnemyDamageRequest,EnemyDamageResult> observe=(request,result)=>{if(request?.source==unit.gameObject&&result.Target==blob&&result.Accepted){if(hits==0){atHitCenter=Vector2.Distance(unit.transform.position,blob.transform.position);var actual=a.Distance(b);atHitGap=actual.isValid?actual.distance:float.PositiveInfinity;}hits++;}};EnemyDamage.EvidenceReceipt+=observe;
   try{leader.IssueBuddyCommand(BuddyCommand.Follow);leader.IssueBuddyCommand(BuddyCommand.Aggressive);yield return Wait(()=>hits>0,"natural actual run Buddy commits bite at physical contact / "+pass,3);}finally{EnemyDamage.EvidenceReceipt-=observe;}
   leader.IssueBuddyCommand(BuddyCommand.Passive);trace.Add(pass+","+identity+","+unit.unitData.gobboType+","+unit.unitData.ageStage+","+N(unit.transform.localScale.x)+","+Csv(axis.ToString())+","+N(centers)+","+N(distance.distance)+","+hits+","+before+","+blob.health+","+N(atHitCenter)+","+N(atHitGap)+","+N(Physics2D.defaultContactOffset));Check("contact fallback was required at actual accepted receipt / "+pass,atHitCenter>unit.GetComponent<BuddyCombat>().attackRange&&atHitGap<=Physics2D.defaultContactOffset*2+.001f&&!float.IsNaN(atHitGap)&&!float.IsInfinity(atHitGap));Check("actual run damage owner agrees and Buddy survives / "+pass,hits>0&&blob.health<before&&unit.CombatAlive);if(hits>0)completed++;
   yield return Shot("actual-run-body-"+pass);ExpeditionDevelopmentControls.SetGodMode(false);Check("actual contact proof retreats through real return service / "+pass,RunReturnService.ReturnToCamp(GameSceneCatalog.Camp,true,true,RunReturnReason.Retreat,"post-audit actual run body contact"));
   float end=Time.realtimeSinceStartup+60;while(!ended&&!Camp()&&Time.realtimeSinceStartup<end){var controller=Find<CampSceneController>();if(controller?.continueToSurvivorsButton!=null&&controller.continueToSurvivorsButton.gameObject.activeInHierarchy)controller.continueToSurvivorsButton.onClick.Invoke();else if(controller?.continueToCampButton!=null&&controller.continueToCampButton.gameObject.activeInHierarchy)controller.continueToCampButton.onClick.Invoke();yield return null;}
   Check("actual run proof returns same Camp and stable Buddy / "+pass,Camp()&&state.ownedGobbos.Any(d=>d.uniqueId==identity&&!d.isDead)&&JsonUtility.ToJson(Find<SettlementCampRegion>().CaptureRegionSnapshot(state))==physical);if(!Camp()){errors.Add("Camp return unavailable");yield break;}region=Find<SettlementCampRegion>();lab=Find<CampFightPitLab>();player=Find<CampPlayableSpawner>().SpawnedPlayer;
  }
  Stage61RuntimeMetrics.Category("validation");File.WriteAllLines(Path.Combine(directory,"actual-run-body-contact.csv"),trace);Check("four actual expedition body contact proofs",completed==4);
 }
 IEnumerator Start()
 {
  yield return Frames(2);Check("owned reviewed post-audit evidence",Persistence.Isolated&&V1RuntimeProfile.DevelopmentToolsEnabled&&V1RuntimeProfile.DevelopmentStorage&&V1RuntimeProfile.EvidenceEnabled);if(ended)yield break;
  keyboard=InputSystem.AddDevice<Keyboard>("Post-audit keyboard");mouse=InputSystem.AddDevice<Mouse>("Post-audit mouse");pad=InputSystem.AddDevice<Gamepad>("Post-audit pad");keyboard.MakeCurrent();mouse.MakeCurrent();
  DevelopmentCampaignFixtures.Import(2);Find<MainMenuController>().LoadGame(2);yield return Wait(Camp,"real established owned Camp ready");if(!Camp()){Finish();yield break;}region=Find<SettlementCampRegion>();lab=Find<CampFightPitLab>();player=Find<CampPlayableSpawner>().SpawnedPlayer;Check("owned entrance transaction",region.OpenPitDevelopmentEntrance());yield return Frames(30);SporeSaveManager.SaveCurrentSlotFromGameState();
  if(scenario=="audit-idle"){Stage61RuntimeMetrics.Mark("closed hub idle warmup");Stage61RuntimeMetrics.Category("warmup");yield return Frames(120);Stage61RuntimeMetrics.Mark("closed hub sixty-second ordinary Camp idle");float end=Time.realtimeSinceStartup+60;while(!ended&&Time.realtimeSinceStartup<end){Stage61RuntimeMetrics.Category("ordinary");yield return null;}Check("sixty-second idle ends without session",!lab.Active);}
  else{if(scenario=="audit-resets"){yield return ModalAndIdentityBoundaries();yield return SynchronizedLifetimeBoundaries();}yield return RunRounds(scenario=="audit-mixed",scenario=="audit-resets");if(scenario=="audit-resets"&&!ended&&errors.Count==0){yield return GroupMatrix();if(errors.Count==0)yield return ActualExpeditionBodies();}}
  if(!ended){Stage61RuntimeMetrics.Category("teardown");lab.StopSession("final audit teardown");Find<PauseMenuController>().OpenPause();Find<PauseMenuController>().QuitToMainMenu();yield return Wait(()=>Find<MainMenuController>()!=null,"real Menu terminal audit teardown");yield return Frames(5);Check("terminal actor terrain and binding resources empty",DiggableTerrainService.Active==null&&Stage59ActorView.OwnedMeshes==0&&UnityEngine.Object.FindObjectsByType<CombatSessionBinding>(FindObjectsSortMode.None).Length==0);Finish();}
 }
 void Finish()
 {
  if(ended)return;ended=true;ExpeditionDevelopmentControls.SetGodMode(false);if(Time.realtimeSinceStartup>deadline)errors.Add("Post-audit watchdog1150seconds");Stage61RuntimeMetrics.Category("validation");Find<Stage61RuntimeMetrics>()?.Export();File.WriteAllLines(Path.Combine(directory,"audit-rounds.csv"),rounds);using(var f=new StreamWriter(Path.Combine(directory,"audit-receipts.csv"))){f.WriteLine("round,seconds,source,target,accepted,damage,failure");for(int i=0;i<receiptCount;i++){var r=receipts[i];f.WriteLine(r.round+","+N(r.seconds)+","+Csv(r.source)+","+Csv(r.target)+","+r.accepted+","+r.damage+","+r.failure);}}
  File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(new Result{finished=true,error=errors.Count>0?"Post-audit runtime failed":"",checks=checks.ToArray(),runtimeErrors=errors.ToArray(),scenario=scenario,method="Real Camp, actors, AI, protected damage and actual input devices. Established synthetic campaign import and automated input are adapters. No direct damage is used for natural rounds. Fault boundary uses deliberately replaced canonical leader reference, not natural combat death. Leader God Mode protects only the staging leader during actual-run contact observation, never the Buddy; disabled before return and on finish. Dead-component faults isolate the owned death watcher while healthy, and are not natural death history tests. No human feel acceptance."},true));Application.Quit(errors.Count==0&&checks.All(c=>c.StartsWith("PASS "))?0:1);
 }
 [Serializable]sealed class Result{public bool finished;public string error,scenario,method;public string[] checks,runtimeErrors;}
 void OnDestroy(){Application.logMessageReceived-=Observe;EnemyDamage.EvidenceReceipt-=Receipt;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);if(pad!=null)InputSystem.RemoveDevice(pad);}
}
#endif

