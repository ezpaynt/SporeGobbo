using System;using System.Linq;using System.Collections.Generic;using UnityEngine;using SporeGobbo.Geometry;using SporeGobbo.CampCandidate;using SporeGobbo.Input;
/// <summary>Temporary Camp match orchestration. Existing actors own AI, HP, attacks, damage and campaign identity.</summary>
public sealed class CampFightPitLab:MonoBehaviour,ICombatSessionRules,ICombatCommandSession
{
 public enum MatchPhase{Idle,Preparing,Fighting,Resolved,CleaningUp}
 public MatchPhase Phase{get;private set;}public string Outcome{get;private set;}="";public int AcceptedHits{get;private set;}public int DamageAttempts{get;private set;}public int FriendlyHits{get;private set;}public int OpponentHits{get;private set;}public bool GodMode;
 public bool Watch;public string Opponent="Weevil",Preset="Open";public int OpponentCount=1;public readonly HashSet<string> SelectedBuddies=new HashSet<string>();
 public bool RestoringCommittedCampaign{get;private set;}
 public bool CleanupBlocked=>cleanupFailure!=null;
 public bool Active=>Phase!=MatchPhase.Idle;public bool Fighting=>Phase==MatchPhase.Fighting;
 public IReadOnlyList<GameObject> Fighters=>fighters.AsReadOnly();public IReadOnlyList<GameObject> Spectators=>spectators.AsReadOnly();
 SettlementCampRegion region;GobboController player;GameState campaign;CampStartRoutineManager routines;GameObject cameraAnchor,followAnchor;bool routineLease,terrainLease,cleaning;float started;string cleanupFailure;int oldEnemyMask;bool oldFollowing,oldAggressive;Action restorePlayerCombat;
 readonly List<GameObject> fighters=new List<GameObject>(),spectators=new List<GameObject>(),spawned=new List<GameObject>();readonly List<ActorLease> leases=new List<ActorLease>();readonly Dictionary<GameObject,int> teams=new Dictionary<GameObject,int>();readonly List<BuddyBrain> commanded=new List<BuddyBrain>();
 Mesh rim;Material rimMaterial;GameObject rimObject,interactionObject;Vector3[] rimVertices;int[] rimIndices;readonly List<(Vector2 position,Vector2 extents)> used=new List<(Vector2,Vector2)>();
 sealed class ActorLease
 {
  public GameObject Actor;public Vector2 Position;public bool Simulated;public int Health;public Action CombatRestore;public bool Hit,PlayerEnabled,FaceCursor,FaceMovement;public GobboUnitSaveData Data,LeaderData;public string LeaderId;public CombatSessionBinding Binding;public bool AddedBinding;public LayerMask EnemyLayer;public bool NearPlayer;public bool AllowCombat,AllowFollowing,AllowScavenging;
  public readonly Dictionary<Behaviour,bool> Enabled=new Dictionary<Behaviour,bool>();
 }
 bool Authorized=>V1RuntimeProfile.DevelopmentToolsEnabled&&V1RuntimeProfile.DevelopmentStorage&&SporeGobbo.Safety.Persistence.Isolated;
 void Awake(){region=GetComponent<SettlementCampRegion>();if(!Authorized||region==null){enabled=false;return;}_ = SporeGobbo.Safety.Persistence.ExperimentRoot;CreateRim();interactionObject=new GameObject("Fight Pit development entrance");interactionObject.transform.SetParent(transform,false);interactionObject.transform.position=region.World(CampFightPitLayout.Entry);interactionObject.AddComponent<CircleCollider2D>().isTrigger=true;interactionObject.AddComponent<CampFightPitInteraction>().Initialize(region,this);}
 public bool Allows(GameObject source,GameObject target)=>Fighting&&source!=null&&target!=null&&teams.TryGetValue(source,out int a)&&teams.TryGetValue(target,out int b)&&a>=0&&b>=0&&a!=b;
 public bool ActionsAllowed(GameObject actor)=>Fighting&&teams.TryGetValue(actor,out int team)&&team>=0;
 static int Health(GameObject actor)=>actor.GetComponent<GobboController>() is GobboController p?p.health:actor.GetComponent<BuddyUnit>() is BuddyUnit b?b.unitData.health:actor.GetComponent<TunnelWeevilEnemy>() is TunnelWeevilEnemy w?w.health:actor.GetComponent<BlobSpitterEnemy>() is BlobSpitterEnemy blob?blob.health:0;
 public int LimitDamage(GameObject target,EnemyDamageRequest request,int proposed)
 {DamageAttempts++;if(!Fighting||!teams.ContainsKey(target))return 0;if(Health(target)<=1){Resolve(target.name+" already at protected 1 HP");return 0;}if(GodMode&&target==player?.gameObject)return 0;return Mathf.Clamp(proposed,0,Mathf.Max(0,Health(target)-1));}
 public void AfterDamage(GameObject target,EnemyDamageRequest request,EnemyDamageResult result)
 {if(result.Accepted){AcceptedHits++;if(request.source!=null&&teams.TryGetValue(request.source,out int team)){if(team==0)FriendlyHits++;else if(team==1)OpponentHits++;}}if(Fighting&&Health(target)<=1)Resolve(target.name+" reached protected 1 HP");}
 public IEnumerable<BuddyBrain> Recipients(GobboController leader)=>leader==player?commanded:Array.Empty<BuddyBrain>();
 public bool CanControl(GobboController leader,BuddyBrain brain)
 {var unit=brain!=null?brain.GetComponent<BuddyUnit>():null;return Active&&Phase!=MatchPhase.CleaningUp&&leader==player&&leader!=null&&leader.isActiveAndEnabled&&commanded.Contains(brain)&&unit!=null&&unit.CombatAlive&&campaign!=null&&campaign==GameState.Instance&&campaign.ownedGobbos.Contains(unit.unitData)&&brain.gameObject.scene==gameObject.scene&&object.ReferenceEquals(ActorTerrainBinding.Resolve(brain.gameObject),region)&&object.ReferenceEquals(DiggableTerrainService.Active,region)&&brain.GetComponent<CombatSessionBinding>()?.Rules==this;}
 public void StartSession()=>StartSessionCore(null);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
 // Reviewed composition fixture only. Same coordinator, real AI, bindings and cleanup;
 // no additional menu/game mode or alternate spawn/health authority.
 public void StartEvidenceSession(params string[] opponents)
 {
  string scenario=Environment.GetEnvironmentVariable("SPORE_STAGE61_CAPTURE");
  if(!Authorized||!V1RuntimeProfile.EvidenceEnabled||(scenario!="audit-mixed"&&scenario!="audit-resets"))throw new InvalidOperationException("Owned reviewed audit evidence required");
  if(opponents==null||opponents.Length<1||opponents.Length>3||opponents.Any(id=>id!="Weevil"&&id!="Blob"))throw new ArgumentException("Only bounded existing enemy composition supported");
  StartSessionCore((string[])opponents.Clone());
 }
#endif
 void StartSessionCore(string[] evidenceOpponents)
 {
  using var profile=new Unity.Profiling.ProfilerMarker("Spore.Pit.Prepare").Auto();
  if(!Authorized||Active||!region.Ready||!region.ResidentialEntranceOpen)throw new InvalidOperationException("Open the owned development Pit entrance, then start from ready Camp.");
  if(SporeUiCoordinator.Instance==null||Time.timeScale<=0)throw new InvalidOperationException("Resume Camp before starting a match.");
  UnityEngine.Object permittedHub=null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
  permittedHub=DevelopmentGameControls.Instance;
#endif
  if(SporeUiCoordinator.Instance.HasModalOtherThan(permittedHub)||SporeInputReader.Instance.Context==SporeInputContext.Wheel)throw new InvalidOperationException("Close other Camp interactions and the command wheel before starting a match.");
  _ = SporeGobbo.Safety.Persistence.ExperimentRoot;campaign=GameState.Instance;player=FindAnyObjectByType<CampPlayableSpawner>()?.SpawnedPlayer;routines=CampStartRoutineManager.Instance;
  if(campaign==null||player==null||player.isPoisoned||!player.CombatAlive||routines==null||routines.DeparturePending)throw new InvalidOperationException("Ready living Camp actors without active poison are required.");
  if(evidenceOpponents==null&&(OpponentCount<1||OpponentCount>3))throw new ArgumentException("One to three opponents supported");bool buddyOpponent=evidenceOpponents==null&&Opponent=="Buddy";var planned=evidenceOpponents??Enumerable.Repeat(Opponent,OpponentCount).ToArray();if(planned.Length<1||planned.Length>3)throw new ArgumentException("One to three opponents supported");
  var residents=FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None).Where(b=>b.gameObject.scene==gameObject.scene&&b.CombatAlive&&campaign.ownedGobbos.Contains(b.unitData)).OrderBy(b=>b.unitData.uniqueId,StringComparer.Ordinal).ToArray();
  if(residents.Any(b=>b.IsPoisoned)||residents.Any(b=>b.GetComponent<BuddyBrain>()?.SquadScoped==true))throw new InvalidOperationException("Wait for poison to end; Camp workers must not own expedition commands.");
  var friends=residents.Where(b=>SelectedBuddies.Contains(b.unitData.uniqueId)).ToArray();if(friends.Length!=SelectedBuddies.Count||friends.Length>5||Watch&&friends.Length==0)throw new InvalidOperationException("Select up to five eligible real Buddies; Watch requires at least one.");
  var enemies=buddyOpponent?residents.Where(b=>!SelectedBuddies.Contains(b.unitData.uniqueId)).Take(OpponentCount).ToArray():Array.Empty<BuddyUnit>();if(buddyOpponent&&enemies.Length!=OpponentCount)throw new InvalidOperationException("Not enough unselected eligible Buddies for opponents.");
  if(!Watch&&player.health<=1||friends.Concat(enemies).Any(b=>b.unitData.health<=1))throw new InvalidOperationException("Fighters must start above protected 1 HP");
  if(!buddyOpponent&&planned.Any(id=>CampFightPitCatalog.Get(id).Prefab(region)==null))throw new InvalidOperationException("Missing real enemy prefab");
  Phase=MatchPhase.Preparing;Outcome="Preparing";AcceptedHits=DamageAttempts=FriendlyHits=OpponentHits=0;GodMode=false;started=Time.realtimeSinceStartup;used.Clear();
  try
  {
   oldFollowing=player.followersFollowing;oldAggressive=player.followersAggressive;oldEnemyMask=player.enemyLayers;restorePlayerCombat=player.CaptureTemporaryCombatRestoration();player.CancelTemporaryCombatMotion();
   leases.Add(Capture(player.gameObject));foreach(var buddy in residents)leases.Add(Capture(buddy.gameObject));
   if(!routines.TrySuspendForCombat(this))throw new InvalidOperationException("Camp routines already leased");routineLease=true;
   foreach(var lease in leases)Suspend(lease);
   if(!region.AcquireArenaTerrain(this))throw new InvalidOperationException("Arena terrain already leased");terrainLease=true;
   // Move actors to distinct clear outer points before publishing any solid preset.
   for(int i=0;i<leases.Count;i++)MoveTo(leases[i].Actor,CampFightPitLayout.Spectator(i*Math.PI*2/Math.Max(1,leases.Count)),true);
   region.ApplyArenaTerrain(this,Preset);used.Clear();
   Bind(player.gameObject,Watch?-1:0);player.enemyLayers=~0;player.faceCursor=true;player.faceMovementWhenNotFacingCursor=false;
   if(!Watch){MoveTo(player.gameObject,new Point2(145.5,82.5),false);fighters.Add(player.gameObject);player.SuppressTemporaryInput=false;}
   else {spectators.Add(player.gameObject);player.SuppressTemporaryInput=true;}
   foreach(var buddy in friends){PrepareBuddy(buddy,0,true);MoveTo(buddy.gameObject,new Point2(143.5,77.5+commanded.Count*2),false);}
   if(buddyOpponent)foreach(var buddy in enemies){PrepareBuddy(buddy,1,false);MoveTo(buddy.gameObject,new Point2(160.5,77.5+fighters.Count),false);}
   else for(int i=0;i<planned.Length;i++)
   {
    var prefab=CampFightPitCatalog.Get(planned[i]).Prefab(region);var go=Instantiate(prefab,region.World(new Point2(159.5,77.5+i*3)),Quaternion.identity);spawned.Add(go);go.name="Pit "+planned[i]+" "+(i+1);Bind(go,1);region.BindActor(go);MoveTo(go,new Point2(159.5,77.5+i*3),false);fighters.Add(go);
    var weevil=go.GetComponent<TunnelWeevilEnemy>();if(weevil!=null)weevil.targetLayers=~0;var blob=go.GetComponent<BlobSpitterEnemy>();if(blob!=null)blob.targetLayers=~0;
   }
   foreach(var lease in leases)if(!fighters.Contains(lease.Actor)&&lease.Actor!=player.gameObject){Bind(lease.Actor,-1);spectators.Add(lease.Actor);}
   // Spectators are nonblocking, ineligible, and keep their actual body size.
   used.Clear();for(int i=0;i<spectators.Count;i++){var actor=spectators[i];MoveTo(actor,CampFightPitLayout.Spectator((i+.5)*Math.PI*2/Math.Max(1,spectators.Count)),true);actor.GetComponent<Rigidbody2D>().simulated=false;}
   cameraAnchor=new GameObject("Owned Pit camera focus");cameraAnchor.transform.SetParent(transform,false);cameraAnchor.transform.position=region.World(CampFightPitLayout.Center);
   if(!region.Presentation.TryBeginStory(this,cameraAnchor.transform,Vector3.zero))throw new InvalidOperationException("Existing camera owner cannot be preempted");
   if(Watch)
   {
    // Camera composition is not a navigation destination: a preset may occupy its centre.
    Vector2 rally=default;bool foundRally=false;
    foreach(var offset in Enumerable.Range(-6,13).SelectMany(y=>Enumerable.Range(-6,13).Select(x=>new Vector2Int(x,y))).OrderBy(p=>p.sqrMagnitude))
    {
     var logical=CampFightPitLayout.Center+new Point2(offset.x,offset.y);if(!CampFightPitLayout.InnerCell((int)logical.X,(int)logical.Y))continue;var candidate=region.World(logical);
     if(commanded.Any(brain=>region.Route(brain.transform.position,candidate,TileMover.GetMapClearanceExtents(brain.GetComponent<Rigidbody2D>(),.28f)).Count==0))continue;
     rally=candidate;foundRally=true;break;
    }
    if(!foundRally)throw new InvalidOperationException("No shared rally point fits actual friendly navigation clearance");
    followAnchor=new GameObject("Owned Pit reachable rally");followAnchor.transform.SetParent(transform,false);followAnchor.transform.position=rally;
    foreach(var brain in commanded){brain.GetComponent<BuddyFollow>().SetPlayer(followAnchor.transform);brain.GetComponent<BuddyCombat>().SetPlayer(followAnchor.transform);}
   }
   player.followersFollowing=true;player.followersAggressive=true;Phase=MatchPhase.Fighting;
   foreach(var brain in commanded)brain.BindSquad(player,true,true);Outcome="Fighting — nonlethal, no rewards";
  }
  catch{StopSession("Preparation failed");throw;}
 }
 ActorLease Capture(GameObject actor)
 {
  if(actor.GetComponent<CombatSessionBinding>()?.Rules!=null)throw new InvalidOperationException("Actor already owns a combat session");var body=actor.GetComponent<Rigidbody2D>();var unit=actor.GetComponent<BuddyUnit>();var p=actor.GetComponent<GobboController>();var lease=new ActorLease{Actor=actor,Position=body.position,Simulated=body.simulated,Health=Health(actor),Data=unit?.unitData,LeaderData=p!=null?campaign?.leader:null,LeaderId=p!=null?campaign?.leader?.uniqueId:null,Hit=unit?.unitData?.hasBeenHit??false,PlayerEnabled=p!=null&&p.enabled,FaceCursor=p!=null&&p.faceCursor,FaceMovement=p!=null&&p.faceMovementWhenNotFacingCursor};
  foreach(var b in actor.GetComponents<Behaviour>())if(b is BuddyBrain||b is BuddyFollow||b is BuddyCombat||b is BuddyScavenger||b is CampWander||b is CampDirectedWalk)lease.Enabled.Add(b,b.enabled);
  var combat=actor.GetComponent<BuddyCombat>();if(combat!=null){lease.EnemyLayer=combat.enemyLayer;lease.NearPlayer=combat.onlyFightNearPlayer;}var brain=actor.GetComponent<BuddyBrain>();if(brain!=null){lease.AllowCombat=brain.allowCombat;lease.AllowFollowing=brain.allowFollowing;lease.AllowScavenging=brain.allowScavenging;}
  return lease;
 }
 void Suspend(ActorLease lease){foreach(var pair in lease.Enabled)if(pair.Key!=null)pair.Key.enabled=false;lease.Actor.GetComponent<BuddyCombat>()?.CancelEngagement();lease.Actor.GetComponent<BuddyFollow>()?.ResetCommandMotion();var body=lease.Actor.GetComponent<Rigidbody2D>();body.linearVelocity=Vector2.zero;}
 void Bind(GameObject actor,int team){var binding=actor.GetComponent<CombatSessionBinding>();var lease=leases.FirstOrDefault(l=>l.Actor==actor);bool added=binding==null;if(added)binding=actor.AddComponent<CombatSessionBinding>();binding.Rules=this;binding.Team=team;teams.Add(actor,team);if(lease!=null){lease.Binding=binding;lease.AddedBinding=added;}}
 void PrepareBuddy(BuddyUnit buddy,int team,bool commands)
 {
  Bind(buddy.gameObject,team);fighters.Add(buddy.gameObject);var brain=buddy.GetComponent<BuddyBrain>();var combat=buddy.GetComponent<BuddyCombat>();if(brain==null||combat==null)throw new InvalidOperationException("Real Buddy lacks combat components");brain.allowCombat=true;brain.allowScavenging=false;brain.allowFollowing=commands;combat.enemyLayer=~0;combat.onlyFightNearPlayer=false;leases.Single(l=>l.Actor==buddy.gameObject).CombatRestore=combat.BeginTemporaryCombat();combat.SetPlayer(player.transform);combat.enabled=true;buddy.GetComponent<BuddyFollow>().SetPlayer(player.transform);buddy.GetComponent<BuddyFollow>().enabled=commands;brain.enabled=true;if(commands)commanded.Add(brain);
 }
 void MoveTo(GameObject actor,Point2 desired,bool outer)
 {
  var body=actor.GetComponent<Rigidbody2D>();var ext=TileMover.GetMapClearanceExtents(body,.25f);Vector2 selected=default;float best=float.PositiveInfinity;
  for(int y=-5;y<=5;y++)for(int x=-5;x<=5;x++){var cell=desired+new Point2(x,y);if(outer?CampFightPitLayout.InnerCell((int)cell.X,(int)cell.Y):!CampFightPitLayout.ArenaCell((int)cell.X,(int)cell.Y))continue;var p=region.World(cell);float d=x*x+y*y;if(d>=best||!TileMover.CanOccupyBox(region,p,ext)||used.Any(q=>Math.Abs(q.position.x-p.x)<q.extents.x+ext.x+.1f&&Math.Abs(q.position.y-p.y)<q.extents.y+ext.y+.1f))continue;best=d;selected=p;}
  if(float.IsPositiveInfinity(best))throw new InvalidOperationException("No actual collider clearance for "+actor.name);body.position=selected;body.linearVelocity=Vector2.zero;actor.transform.position=selected;Physics2D.SyncTransforms();used.Add((selected,ext));
 }
 public void Resolve(string outcome)
 {if(!Fighting)return;Phase=MatchPhase.Resolved;Outcome=outcome;foreach(var actor in fighters){if(actor==null)continue;actor.GetComponent<BuddyBrain>()?.SuspendCommandMotion();actor.GetComponent<BuddyCombat>()?.CancelEngagement();var body=actor.GetComponent<Rigidbody2D>();if(body!=null)body.linearVelocity=Vector2.zero;foreach(var ai in actor.GetComponents<Behaviour>())if(ai is TunnelWeevilEnemy||ai is BlobSpitterEnemy)ai.enabled=false;}player.CancelTemporaryCombatMotion();player.SuppressTemporaryInput=true;}
 public void StopSession(string reason="Stopped")
 {
  using var profile=new Unity.Profiling.ProfilerMarker("Spore.Pit.Cleanup").Auto();
  if(!Active||cleaning||cleanupFailure!=null)return;cleaning=true;Phase=MatchPhase.CleaningUp;var failures=new List<string>();bool leaderRestorable=true;
  void Attempt(Action action){try{action();}catch(Exception error){failures.Add(error.Message);}}
  try
  {
   foreach(var actor in fighters)if(actor!=null)Attempt(()=>{actor.GetComponent<BuddyBrain>()?.SuspendCommandMotion();actor.GetComponent<BuddyCombat>()?.CancelEngagement();var body=actor.GetComponent<Rigidbody2D>();if(body!=null)body.linearVelocity=Vector2.zero;});
   Attempt(()=>{foreach(var projectile in FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))if(object.ReferenceEquals(projectile.SessionRules,this)){projectile.gameObject.SetActive(false);Destroy(projectile.gameObject);}});
   foreach(var actor in spawned)if(actor!=null)Attempt(()=>{actor.SetActive(false);Destroy(actor);});spawned.Clear();
   if(terrainLease)Attempt(()=>{region.ReleaseArenaTerrain(this);terrainLease=false;});
   foreach(var lease in leases)
   {
    // Restore only the exact still-owned identity. Removal/death is never undone.
    if(lease.Data!=null)
    {
     var buddy=lease.Actor!=null?lease.Actor.GetComponent<BuddyUnit>():null;
     if(campaign==null||campaign!=GameState.Instance||!campaign.ownedGobbos.Contains(lease.Data)||lease.Data.isDead||buddy==null||!object.ReferenceEquals(buddy.unitData,lease.Data)||!buddy.CombatAlive)
     {
      failures.Add("Buddy died, was removed or changed identity; old state not restored");
      // Only release this activity's capabilities; never heal/recreate the absent identity.
      if(lease.Binding!=null&&object.ReferenceEquals(lease.Binding.Rules,this))
      {
       Attempt(()=>{if(lease.Actor!=null){lease.Actor.GetComponent<BuddyBrain>()?.ReleaseTemporarySquad(player);lease.Actor.GetComponent<BuddyCombat>()?.CancelEngagement();lease.Actor.GetComponent<BuddyFollow>()?.ResetCommandMotion();foreach(var b in lease.Actor.GetComponents<Behaviour>())if(b is BuddyBrain||b is BuddyCombat||b is BuddyFollow||b is BuddyScavenger||b is CampWander||b is CampDirectedWalk)b.enabled=false;var body=lease.Actor.GetComponent<Rigidbody2D>();if(body!=null)body.linearVelocity=Vector2.zero;}});
       lease.Binding.Rules=null;lease.Binding.Team=0;
      }
      continue;
     }
     lease.Data.health=lease.Health;lease.Data.hasBeenHit=lease.Hit;
    }
    if(lease.LeaderData!=null&&(lease.Actor==null||campaign==null||campaign!=GameState.Instance||!object.ReferenceEquals(campaign?.leader,lease.LeaderData)||lease.LeaderData.uniqueId!=lease.LeaderId||lease.LeaderData.isDead||lease.Actor.GetComponent<GobboController>()?.CombatAlive!=true))
    {
     leaderRestorable=false;failures.Add("Leader died or identity changed; old actor health and simulation not restored");
     if(lease.Actor!=null&&lease.Actor.TryGetComponent<GobboController>(out var invalidLeader))invalidLeader.SuppressTemporaryInput=true;
     if(lease.Binding!=null){lease.Binding.Rules=null;lease.Binding.Team=0;}
     continue;
    }
    if(lease.Actor==null)continue;var actor=lease.Actor;
    Attempt(()=>{var unit=actor.GetComponent<BuddyUnit>();if(unit!=null&&unit.unitData!=lease.Data)throw new InvalidOperationException("Actor identity replaced; foreign identity not changed");});
    Attempt(()=>{var unit=actor.GetComponent<BuddyUnit>();if(unit!=null&&unit.unitData==lease.Data&&campaign==GameState.Instance&&campaign.ownedGobbos.Contains(lease.Data)&&unit.CombatAlive)unit.ClearTemporaryCombatFeedback();var p=actor.GetComponent<GobboController>();if(p!=null&&campaign==GameState.Instance&&p.CombatAlive)p.ClearTemporaryCombatFeedback();});
    Attempt(()=>{var brain=actor.GetComponent<BuddyBrain>();brain?.ReleaseTemporarySquad(player);var combat=actor.GetComponent<BuddyCombat>();if(combat!=null){lease.CombatRestore?.Invoke();combat.CancelEngagement();combat.enemyLayer=lease.EnemyLayer;combat.onlyFightNearPlayer=lease.NearPlayer;combat.SetPlayer(player!=null?player.transform:null);}actor.GetComponent<BuddyFollow>()?.SetPlayer(player!=null?player.transform:null);if(brain!=null){brain.allowCombat=lease.AllowCombat;brain.allowFollowing=lease.AllowFollowing;brain.allowScavenging=lease.AllowScavenging;}});
    Attempt(()=>{var body=actor.GetComponent<Rigidbody2D>();body.linearVelocity=Vector2.zero;body.simulated=lease.Simulated;if(!TileMover.CanOccupyBox(region,lease.Position,TileMover.GetMapClearanceExtents(body,.25f)))throw new InvalidOperationException("Owned actor return position blocked; left at current cleared floor");body.position=lease.Position;actor.transform.position=lease.Position;});
    // Keep one inert reusable binding per actor: destroying/readding during a same-frame Reset would lose the new binding at end of frame.
    if(lease.Binding!=null){lease.Binding.Rules=null;lease.Binding.Team=0;}
    foreach(var pair in lease.Enabled)if(pair.Key!=null)Attempt(()=>pair.Key.enabled=pair.Value);
    Attempt(()=>{var p=actor.GetComponent<GobboController>();if(p!=null){if(campaign!=GameState.Instance)throw new InvalidOperationException("Campaign identity changed; leader not overwritten");p.health=lease.Health;p.enabled=lease.PlayerEnabled;p.faceCursor=lease.FaceCursor;p.faceMovementWhenNotFacingCursor=lease.FaceMovement;}});
   }
   Attempt(()=>{if(leaderRestorable)restorePlayerCombat?.Invoke();restorePlayerCombat=null;if(leaderRestorable&&player!=null){player.enemyLayers=oldEnemyMask;player.followersFollowing=oldFollowing;player.followersAggressive=oldAggressive;}});
   Attempt(()=>{if(region!=null&&region.Presentation!=null)region.Presentation.EndStory(this);if(cameraAnchor!=null)Destroy(cameraAnchor);cameraAnchor=null;if(followAnchor!=null)Destroy(followAnchor);followAnchor=null;});
   if(routineLease)Attempt(()=>{if(routines!=null)routines.ResumeAfterCombat(this,region!=null&&region.isActiveAndEnabled&&campaign==GameState.Instance&&failures.Count==0);routineLease=false;});
   Physics2D.SyncTransforms();
  }
  finally{cleaning=false;GodMode=false;}
  if(failures.Count>0){cleanupFailure=string.Join("; ",failures);Outcome="Cleanup blocked: "+cleanupFailure;return;}
  fighters.Clear();spectators.Clear();leases.Clear();teams.Clear();commanded.Clear();used.Clear();Phase=MatchPhase.Idle;Outcome=reason;
 }

 /// Explicit development recovery only. Ordinary save remains blocked after a cleanup fault.
 public bool RecoverCommittedCampaign(int slot)
 {
  if(!Authorized)return false;var detached=SporeSaveManager.LoadSlot(slot);if(detached==null||!detached.hasSave)return false;
  StopSession("Explicit committed recovery");
  if(terrainLease||routineLease||cameraAnchor!=null||followAnchor!=null||spawned.Any(a=>a!=null&&a.activeInHierarchy)||leases.Any(l=>l.Binding!=null&&object.ReferenceEquals(l.Binding.Rules,this))||FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Any(p=>object.ReferenceEquals(p.SessionRules,this)))return false;
  RestoringCommittedCampaign=true;
  try
  {
   // The existing campaign authority applies the authenticated detached commit. Save stays blocked until success.
   if(!SporeSaveManager.ApplySlotToGameState(detached))return false;
   fighters.Clear();spectators.Clear();leases.Clear();teams.Clear();commanded.Clear();used.Clear();cleanupFailure=null;Phase=MatchPhase.Idle;Outcome="Restored last committed campaign; return to Menu";return true;
  }
  finally{RestoringCommittedCampaign=false;}
 }

 void Update()
 {
  if(!Active)return;if(!Authorized||campaign==null||campaign!=GameState.Instance||DiggableTerrainService.Active!=region||player==null){StopSession("Ownership changed");return;}
  if(Fighting&&fighters.Any(a=>a==null||!EnemyDamage.Alive(EnemyDamage.Resolve(a)))){StopSession("Actor removed or disabled");return;}
  if(Fighting&&Time.timeScale>0&&SporeInputReader.Instance?.Context==SporeInputContext.Gameplay)
   foreach(var actor in fighters){var p=region.LogicalPosition(actor.transform.position);if(!CampFightPitLayout.InnerCell((int)p.X,(int)p.Y)){Resolve("Fighter left the inner floor");break;}}
 }
 void LateUpdate()
 {
  using var profile=new Unity.Profiling.ProfilerMarker("Spore.Pit.Presentation").Auto();
  if(rim==null||region.Projection==null)return;for(int i=0;i<96;i++){double t=i*Math.PI*2/96;var a=new Point2(151.5+16*Math.Cos(t),82.5+12*Math.Sin(t));var b=new Point2(151.5+16.22*Math.Cos(t),82.5+12.22*Math.Sin(t));rimVertices[i*2]=region.Presentation.Project(a);rimVertices[i*2+1]=region.Presentation.Project(b);}rim.vertices=rimVertices;rim.RecalculateBounds();
 }
 void CreateRim(){rimObject=new GameObject("Temporary ground-level Pit rim");rimObject.transform.SetParent(transform,false);rim=new Mesh{name="Pit nonphysical ground rim"};rim.MarkDynamic();rimVertices=new Vector3[192];rimIndices=new int[96*6];var colors=Enumerable.Repeat(new Color(.36f,.29f,.18f,1),192).ToArray();for(int i=0;i<96;i++){int a=i*2,b=(i+1)%96*2,k=i*6;rimIndices[k]=a;rimIndices[k+1]=b;rimIndices[k+2]=a+1;rimIndices[k+3]=a+1;rimIndices[k+4]=b;rimIndices[k+5]=b+1;}rim.vertices=rimVertices;rim.colors=colors;rim.triangles=rimIndices;rimObject.AddComponent<MeshFilter>().sharedMesh=rim;rimMaterial=new Material(region.ActorShader);rimObject.AddComponent<MeshRenderer>().sharedMaterial=rimMaterial;rimObject.GetComponent<MeshRenderer>().sortingOrder=8;}
 public void DrawControls()
 {
  if(!Authorized)return;GUILayout.Space(10);GUILayout.Label("FIGHT PIT LAB / "+Phase+" / "+Outcome);GUILayout.Label("Real actors — protected 1 HP; no deaths, XP or rewards.");GUILayout.Label("Hits "+AcceptedHits+" / damage attempts "+DamageAttempts);
  if(Active){if(GUILayout.Button("Stop and restore Camp"))StopSession();if(cleanupFailure==null&&GUILayout.Button("Reset same configuration")){try{StopSession("Reset");if(!Active)StartSession();}catch(Exception e){Outcome=e.Message;Debug.LogWarning("[Pit] "+e.Message);}}if(!Watch)GodMode=GUILayout.Toggle(GodMode,"Player God Mode "+(GodMode?"ON":"OFF"));return;}
  Watch=GUILayout.Toggle(Watch,"WATCH (off = PLAY)");GUILayout.Label("Friendly real Buddies:");foreach(var unit in FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None).Where(b=>b.gameObject.scene==gameObject.scene&&b.CombatAlive)){bool selected=SelectedBuddies.Contains(unit.unitData.uniqueId);bool next=GUILayout.Toggle(selected,unit.unitData.displayName+" / "+unit.unitData.health+" HP");if(next)SelectedBuddies.Add(unit.unitData.uniqueId);else SelectedBuddies.Remove(unit.unitData.uniqueId);}
  GUILayout.Label("Opponent");GUILayout.BeginHorizontal();foreach(var entry in CampFightPitCatalog.Entries)if(GUILayout.Toggle(Opponent==entry.Id,entry.Name,"Button"))Opponent=entry.Id;GUILayout.EndHorizontal();GUILayout.BeginHorizontal();for(int i=1;i<=3;i++)if(GUILayout.Toggle(OpponentCount==i,i.ToString(),"Button"))OpponentCount=i;GUILayout.EndHorizontal();
  GUILayout.Label("Owned arena terrain");GUILayout.BeginHorizontal();foreach(string name in new[]{"Open","Rock","Roots","Dirt"})if(GUILayout.Toggle(Preset==name,name,"Button"))Preset=name;GUILayout.EndHorizontal();
  if(!region.ResidentialEntranceOpen&&GUILayout.Button("DEV: open existing Pit root entrance"))region.OpenPitDevelopmentEntrance();
  if(GUILayout.Button("Start real nonlethal match"))try{StartSession();}catch(Exception e){Outcome=e.Message;Debug.LogWarning("[Pit] "+e.Message);}
  GUILayout.Label("Future catalog entries deferred: Protector, Big Shroom, Captain, Guard, Wild Gobbo, Rats. No Mercy is not implemented.");
 }
 void OnGUI(){if(Authorized&&Active){foreach(var actor in fighters.Concat(spectators)){if(actor==null)continue;var view=actor.GetComponent<Stage59ActorView>();if(view==null||!view.IsPresented)continue;var p=region.RegionCamera.WorldToScreenPoint(view.ProjectedHead);GUI.color=spectators.Contains(actor)?Color.yellow:teams.TryGetValue(actor,out int team)&&team==0?Color.cyan:Color.red;GUI.Label(new Rect(p.x-70,Screen.height-p.y-22,180,25),(spectators.Contains(actor)?"Watching":actor.GetComponent<BuddyUnit>()?.unitData?.displayName??actor.name)+(fighters.Contains(actor)?" / "+Health(actor)+" HP":""));}GUI.color=Color.white;GUI.Label(new Rect(Screen.width-620,Screen.height-50,610,35),"PIT "+(Watch?"WATCH":"PLAY")+" / "+Phase+" / protected 1 HP / GOD "+(GodMode?"ON":"OFF")+" / F9 controls");}}
 void OnDisable(){if(Active)StopSession("Pit disabled");}void OnDestroy(){if(Active)StopSession("Camp teardown");foreach(var o in new UnityEngine.Object[]{rim,rimMaterial,rimObject,interactionObject})if(o!=null)Destroy(o);}
}
