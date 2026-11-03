#if UNITY_EDITOR
using System;using System.IO;using System.Linq;using System.Collections;using System.Collections.Generic;using System.Reflection;
using NUnit.Framework;using UnityEngine;using UnityEngine.SceneManagement;using UnityEngine.TestTools;using UnityEngine.InputSystem;using UnityEngine.InputSystem.LowLevel;
using SporeGobbo.Safety;using SporeGobbo.Input;using SporeGobbo.Geometry;
// Bounded observations over actual expedition actors/AI/physics. No combat tuning or movement writes after setup.
public sealed class Stage64DCooperativeCombatCrowdingTests
{
    Stage64DContractHost host;Stage64DNativeMotionEvidence native;Keyboard keyboard;string directory;int samples;
    readonly List<string> observations=new List<string>();readonly List<GameObject> spawned=new List<GameObject>();
    static T Find<T>()where T:UnityEngine.Object=>UnityEngine.Object.FindAnyObjectByType<T>();
    IEnumerator Wait(Func<bool> f,string label,float seconds){float end=Time.realtimeSinceStartup+seconds;while(!f()&&Time.realtimeSinceStartup<end)yield return null;Assert.That(f(),Is.True,label);}
    void Capture(RegionMovementCoordinator.DecisionTrace d)=>native?.Applied(d);
    static void Place(IDiggableTerrain terrain,GameObject actor,Vector2 p)
    {var body=actor.GetComponent<Rigidbody2D>();Assert.That(TileMover.CanOccupyBox(terrain,p,TileMover.GetMapClearanceExtents(body,.25f)),Is.True,"Owned setup uses executable actual-body clearance");body.position=p;body.linearVelocity=Vector2.zero;actor.transform.position=p;Physics2D.SyncTransforms();}
    void Row(string phase,GameObject actor,string detail)
    {Assert.That(samples++,Is.LessThan(8000),"Bounded observer");var body=actor.GetComponent<Rigidbody2D>();var binding=actor.GetComponent<ActorTerrainBinding>();observations.Add(FormattableString.Invariant($"{phase},{Time.fixedTimeAsDouble:R},{actor.name},{body.position.x:R},{body.position.y:R},{body.linearVelocity.x:R},{body.linearVelocity.y:R},{binding?.RouteStatus},{detail}"));}

    // Observer-only: existing service queries, no body/terrain/settings writes. Bounded history.
    readonly Queue<string> terrainHistory=new Queue<string>();
    readonly Dictionary<int,Vector2> terrainPrevious=new Dictionary<int,Vector2>();
    int terrainSamples,terrainEvicted;bool terrainDetail;
    static readonly MethodInfo logicalBox=typeof(TileMover).GetMethod("IsTerrainPositionClearForBox",BindingFlags.Static|BindingFlags.NonPublic);
    void ObserveTerrain(ExpeditionRegion r,TunnelWeevilEnemy e)
    {
        if(terrainSamples++>=4000)return;
        var b=e.GetComponent<Rigidbody2D>();var c=e.GetComponent<BoxCollider2D>();var p=b.position;
        var half=TileMover.GetMapClearanceExtents(b,e.bodyRadius);
        bool logical=(bool)logicalBox.Invoke(null,new object[]{r,p,half});
        bool nativeClear=r.TerrainPhysics!=null&&r.TerrainPhysics.CanOccupyTerrainBox(p,half);
        bool composite=TileMover.CanOccupyBox(r,p,half);
        var previous=terrainPrevious.TryGetValue(b.GetInstanceID(),out var prior)?prior:p;terrainPrevious[b.GetInstanceID()]=p;
        var delta=p-previous;
        var published=typeof(ExpeditionRegion).GetField("published",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(r);
        string row=FormattableString.Invariant($"tick={Time.fixedTimeAsDouble:R} actor={e.name} p={p.ToString("R")} velocity={b.linearVelocity.ToString("R")} previousPost={previous.ToString("R")} logical={logical} native={nativeClear} composite={composite} ready={r.LogicalReady} revision={r.Revision} published={published} half={half.ToString("R")} offset={c.offset.ToString("R")} boundsMin={c.bounds.min.ToString("R")} boundsMax={c.bounds.max.ToString("R")} connector={TileMover.CanTraverseBox(r,previous,p,half)} bindingMinimum={e.GetComponent<ActorTerrainBinding>().MinimumBodyRadius:R}");
        if(terrainHistory.Count==256){terrainHistory.Dequeue();terrainEvicted++;}terrainHistory.Enqueue(row);
        if(composite||terrainDetail)return;terrainDetail=true;
        var detail=new List<string>{row,"ACTUAL COLLIDER DISTANCES: signed native separations; negative means overlap, not proxy rejection"};
        var root=(GameObject)typeof(RegionTerrainPhysics).GetField("root",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(r.TerrainPhysics);
        float minimum=float.PositiveInfinity;int count=0;
        foreach(var terrain in root.GetComponentsInChildren<PolygonCollider2D>())
        {
            if(!terrain.enabled||!r.TerrainPhysics.Owns(terrain))continue;
            var d=c.Distance(terrain);count++;if(d.isValid)minimum=Mathf.Min(minimum,d.distance);
            if(!d.isValid||d.distance<2)
            {
                detail.Add(FormattableString.Invariant($"terrain={terrain.name} id={terrain.GetInstanceID()} valid={d.isValid} overlapped={d.isOverlapped} distance={d.distance:R} pointA={d.pointA.ToString("R")} pointB={d.pointB.ToString("R")} normal={d.normal.ToString("R")} material={terrain.sharedMaterial?.name} friction={terrain.friction:R} restitution={terrain.bounciness:R}"));
                for(int path=0;path<terrain.pathCount;path++)detail.Add("path="+path+" "+string.Join(";",terrain.GetPath(path).Select(v=>terrain.transform.TransformPoint(v).ToString("R"))));
            }
        }
        detail.Add(FormattableString.Invariant($"nativeDistanceColliderCount={count} minimumDistance={minimum:R} actualMaterial={c.sharedMaterial?.name} friction={c.friction:R} restitution={c.bounciness:R} rotation={b.rotation:R}"));
        var center=r.WorldToCell(p);for(int x=center.x-3;x<=center.x+3;x++)for(int y=center.y-3;y<=center.y+3;y++){var cell=new Vector2Int(x,y);if(r.IsBlocked(cell))detail.Add($"blockedCell={cell} center={r.CellToWorld(cell).ToString("R")} proxyOverlap={TileMover.BodyOverlapsCell(p,half,r.CellToWorld(cell),r.CellSize)}");}
        var hits=new RaycastHit2D[64];int n=delta.sqrMagnitude>0?c.Cast(delta.normalized,hits,delta.magnitude):0;detail.Add("actualShapeCastFromCurrentPose count="+n+" saturated="+(n==hits.Length)+" direction="+delta.ToString("R"));for(int i=0;i<n;i++)if(r.TerrainPhysics.Owns(hits[i].collider))detail.Add("terrainCast="+hits[i].collider.name+" distance="+hits[i].distance.ToString("R"));
        File.WriteAllLines(Path.Combine(directory,"terrain-rejection.txt"),detail);
    }
    void ExportTerrain()
    {File.WriteAllLines(Path.Combine(directory,"terrain-history.txt"),terrainHistory);File.WriteAllText(Path.Combine(directory,"terrain-observer-status.txt"),$"samples={terrainSamples} cap=4000 overflow={Math.Max(0,terrainSamples-4000)} ringEvicted={terrainEvicted} ringCapacity=256 rejectionCaptured={terrainDetail}; native motion/contact history exported separately; no physics settings or simulation writes");}

    [UnityTest,Timeout(360000)]public IEnumerator RealSharedEnemyCombatRemovalReturnAndThreeWeevilCorner()
    {
        Assert.That(Persistence.Isolated&&V1RuntimeProfile.DevelopmentStorage&&V1RuntimeProfile.DevelopmentToolsEnabled,Is.True);
        yield return new EnterPlayMode();var go=new GameObject("Owned cooperative combat/crowding host");UnityEngine.Object.DontDestroyOnLoad(go);host=go.AddComponent<Stage64DContractHost>();host.Begin(Exercise());float end=Time.realtimeSinceStartup+300;while(!host.Complete&&Time.realtimeSinceStartup<end)yield return null;
        Assert.That(host.Complete,Is.True,"Finite bounded multi-scenario host");if(host.Error!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(host.Error).Throw();
    }
    IEnumerator EnterExpedition()
    {var portal=Find<CampRunPortal>();portal.Interact(Find<CampPlayableSpawner>().SpawnedPlayer);yield return null;portal.goButton.onClick.Invoke();yield return Wait(()=>Find<ExpeditionRegion>()?.PresentationReady==true&&Find<ExpeditionRegion>()?.Player!=null,"Real expedition",60);for(int i=0;i<20;i++)yield return null;}
    IEnumerator Exercise()
    {
        directory=Persistence.ExperimentPath("Evidence/combat-crowding");Directory.CreateDirectory(directory);observations.Add("phase,tick,actor,x,y,vx,vy,route,detail");
        SceneManager.LoadScene(GameSceneCatalog.MainMenu);yield return Wait(()=>Find<MainMenuController>()!=null,"Menu",30);yield return null;DevelopmentCampaignFixtures.Import(2);Find<MainMenuController>().LoadGame(2);
        yield return Wait(()=>Find<CampSceneController>()?.WorldInputReady==true&&Find<SettlementCampRegion>()?.Ready==true,"Camp",60);
        var state=GameState.Instance;foreach(var id in state.activeSquadIds.ToArray())Assert.That(BuddyRosterService.MoveBuddyToReserve(state,id),Is.True);
        var names=new[]{"development-b-buddy-1","development-b-buddy-4","development-b-buddy-2"};foreach(var id in names)Assert.That(BuddyRosterService.MoveBuddyToActiveSquad(state,id),Is.True);
        yield return EnterExpedition();var r=Find<ExpeditionRegion>();var player=r.Player;
        var buddies=names.Select(n=>UnityEngine.Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None).Single(b=>b.unitData.uniqueId==n)).ToArray();
        var coordinator=player.GetComponent<ActorTerrainBinding>().Movement;native=host.gameObject.AddComponent<Stage64DNativeMotionEvidence>();RegionMovementCoordinator.DiagnosticObserver+=Capture;
        try
        {
            var center=r.World(r.Definition.Anchors["arrival"]);Place(r,player.gameObject,center);
            var offsets=new[]{new Vector2(-1.8f,-1.6f),new Vector2(-2,0),new Vector2(-1.8f,1.6f)};
            for(int i=0;i<3;i++){Place(r,buddies[i].gameObject,center+offsets[i]);buddies[i].GetComponent<BuddyFollow>().ResetCommandMotion();}
            var enemy=UnityEngine.Object.Instantiate(r.weevilPrefab,center+Vector2.right*.95f,Quaternion.identity);spawned.Add(enemy);enemy.name="owned.shared-weevil";r.BindActor(enemy);
            Assert.That(RegionMovementCoordinator.EffectiveNativeContact(player.GetComponent<Collider2D>(),enemy.GetComponent<Collider2D>()),Is.True,"Actual hostile filtering remains solid");
            player.IssueBuddyCommand(BuddyCommand.Follow);player.IssueBuddyCommand(BuddyCommand.Aggressive);ExpeditionDevelopmentControls.SetGodMode(true);
            yield return null;var weevil=enemy.GetComponent<TunnelWeevilEnemy>();int initialHealth=weevil.health,minimumHealth=initialHealth;int attempts=ExpeditionDevelopmentControls.DamageAttempts;bool knockback=false;int contacts=0;var contactBuffer=new ContactPoint2D[32];var stateField=typeof(TunnelWeevilEnemy).GetField("combatState",BindingFlags.Instance|BindingFlags.NonPublic);
            float end=Time.realtimeSinceStartup+5;
            while(enemy!=null&&Time.realtimeSinceStartup<end)
            {
                yield return new WaitForFixedUpdate();if(enemy==null)break;minimumHealth=Math.Min(minimumHealth,weevil.health);knockback|=stateField?.GetValue(weevil)?.ToString()=="Knockback";
                Row("shared-enemy",enemy,"health="+weevil.health);int count=enemy.GetComponent<Rigidbody2D>().GetContacts(contactBuffer);Assert.That(count,Is.LessThan(contactBuffer.Length),"Contact observation capacity");contacts+=count;
                for(int i=0;i<3;i++)if(buddies[i]!=null)
                {
                    Row("shared-buddy",buddies[i].gameObject,"health="+buddies[i].unitData.health);var bi=buddies[i].GetComponent<Rigidbody2D>();var ei=TileMover.GetMapClearanceExtents(bi,.25f);Assert.That(TileMover.CanOccupyBox(r,bi.position,ei),Is.True,"Combat terrain clearance");
                    for(int j=i+1;j<3;j++)if(buddies[j]!=null){var bj=buddies[j].GetComponent<Rigidbody2D>();var ej=TileMover.GetMapClearanceExtents(bj,.25f);Assert.That(MovementContactPolicy.Penetration(bi.position-bj.position,ei+ej),Is.LessThanOrEqualTo(MovementContactPolicy.Allowance(ei,ej,true)+.0001f),"No deep friendly combat stacking");}
                }
            }
            Debug.Log($"COOPERATIVE_SHARED_COMBAT hp={initialHealth}/{minimumHealth} enemyAlive={enemy!=null} nativeContacts={contacts} knockback={knockback} leaderDamageAttempts={ExpeditionDevelopmentControls.DamageAttempts-attempts} survivingBuddies={buddies.Count(b=>b!=null&&b.CombatAlive)}");
            Assert.That(enemy==null||minimumHealth<initialHealth,Is.True,"Real autonomous Buddies damage the real enemy; player never attacks");Assert.That(contacts,Is.GreaterThan(0),"Actual hostile native contacts");Assert.That(knockback,Is.True,"Actual existing enemy hit reaction observed");
            if(enemy!=null)UnityEngine.Object.Destroy(enemy);yield return null;ExpeditionDevelopmentControls.SetGodMode(false);player.IssueBuddyCommand(BuddyCommand.Passive);player.IssueBuddyCommand(BuddyCommand.Stay);yield return null;
            var survivor=buddies.FirstOrDefault(b=>b!=null&&b.CombatAlive);Assert.That(survivor,Is.Not.Null,"A surviving identity required for scoped removal/death cleanup");var binding=survivor.GetComponent<ActorTerrainBinding>();var pc=player.GetComponent<Collider2D>();var bc=survivor.GetComponent<Collider2D>();
            coordinator.Unregister(binding);Assert.That(Physics2D.GetIgnoreCollision(pc,bc),Is.False,"Removal releases owned friendly contact override");coordinator.Configure(binding,survivor.unitData.uniqueId,1,false);coordinator.RefreshContacts();Assert.That(Physics2D.GetIgnoreCollision(pc,bc),Is.True,"Re-registration owns friendly pair again");
            string deadId=survivor.unitData.uniqueId;var deadData=survivor.unitData;int before=coordinator.ActorCount;survivor.TakeDamage(1000000);yield return null;yield return null;
            Assert.That(deadData.isDead,Is.True,"Real Buddy death authority");Assert.That(state.ownedGobbos.Any(b=>b.uniqueId==deadId),Is.False);Assert.That(state.activeSquadIds.Contains(deadId),Is.False);Assert.That(coordinator.ActorCount,Is.LessThan(before),"Death unregisters native contact owner");
            Assert.That(RunReturnService.ReturnToCamp(GameSceneCatalog.Camp,true,true,RunReturnReason.Retreat,"Owned cooperative cleanup proof"),Is.True);yield return Wait(()=>Find<CampSceneController>()!=null,"Camp return coordinator",60);
            end=Time.realtimeSinceStartup+60;
            while(Find<CampSceneController>()?.WorldInputReady!=true&&Time.realtimeSinceStartup<end)
            {var c=Find<CampSceneController>();if(c.continueToSurvivorsButton!=null&&c.continueToSurvivorsButton.gameObject.activeInHierarchy)c.continueToSurvivorsButton.onClick.Invoke();else if(c.continueToCampButton!=null&&c.continueToCampButton.gameObject.activeInHierarchy)c.continueToCampButton.onClick.Invoke();yield return null;}
            Assert.That(Find<CampSceneController>()?.WorldInputReady==true,Is.True,"Real return completes");Assert.That(coordinator.ActorCount,Is.Zero);Assert.That(coordinator.OwnedContactOverrides,Is.Zero);Assert.That(coordinator.OwnedNativeFilterLeases,Is.Zero);
            state=GameState.Instance;Assert.That(state.ownedGobbos.Any(b=>b.uniqueId==deadId),Is.False,"Return preserves authoritative death");foreach(var id in state.activeSquadIds.ToArray())Assert.That(BuddyRosterService.MoveBuddyToReserve(state,id),Is.True);
            yield return EnterExpedition();r=Find<ExpeditionRegion>();player=r.Player;Assert.That(UnityEngine.Object.FindObjectsByType<BuddyBrain>(FindObjectsSortMode.None).Count(b=>b.SquadScoped),Is.Zero,"Enemy-only pursuit baseline uses no competing Buddy targets");
            var enemies=new List<TunnelWeevilEnemy>();for(int i=0;i<3;i++){var go=UnityEngine.Object.Instantiate(r.weevilPrefab,r.World(r.Definition.Anchors["arrival"])+Vector2.right*(i+2),Quaternion.identity);spawned.Add(go);go.name="owned.crowding-weevil-"+i;r.BindActor(go);enemies.Add(go.GetComponent<TunnelWeevilEnemy>());}
            yield return null;var half=TileMover.GetMapClearanceExtents(enemies[0].GetComponent<Rigidbody2D>(),enemies[0].bodyRadius);var route=r.Route(r.World(r.Definition.Anchors["arrival"]),r.World(r.Definition.Anchors["nursery"]),half);
            int bend=-1;for(int i=10;i<route.Count-12;i++)if(Vector2.Dot((route[i]-route[i-1]).normalized,(route[i+1]-route[i]).normalized)<.8f&&Vector2.Distance(route[i-9],route[i])>3){bend=i;break;}
            Assert.That(bend,Is.GreaterThanOrEqualTo(10),"Existing authored route supplies a real corner and approach");
            Place(r,player.gameObject,route[bend]);for(int i=0;i<3;i++)Place(r,enemies[i].gameObject,route[bend-3-i*3]);ExpeditionDevelopmentControls.SetGodMode(true);
            keyboard=InputSystem.AddDevice<Keyboard>("Owned enemy-corner retreat keyboard");keyboard.MakeCurrent();var targetField=typeof(TunnelWeevilEnemy).GetField("currentTarget",BindingFlags.Instance|BindingFlags.NonPublic);var starts=enemies.Select(e=>e.GetComponent<Rigidbody2D>().position).ToArray();var acquired=new bool[3];var moved=new float[3];var stopCounts=new int[3];int cursor=bend+1;end=Time.realtimeSinceStartup+20;
            while(Time.realtimeSinceStartup<end)
            {
                var pb=player.GetComponent<Rigidbody2D>();if(cursor<Math.Min(route.Count,bend+10))
                {var delta=route[cursor]-pb.position;if(delta.magnitude<.14f){cursor++;InputSystem.QueueStateEvent(keyboard,new KeyboardState());}else{var k=Mathf.Abs(delta.x)>Mathf.Abs(delta.y)?delta.x>0?Key.D:Key.A:delta.y>0?Key.W:Key.S;InputSystem.QueueStateEvent(keyboard,new KeyboardState(k));}}
                else InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                yield return new WaitForFixedUpdate();
                for(int i=0;i<3;i++)
                {var e=enemies[i];Assert.That(e,Is.Not.Null,"Enemies remain alive; no fixture damage");var eb=e.GetComponent<Rigidbody2D>();acquired[i]|=targetField?.GetValue(e)!=null;moved[i]=Mathf.Max(moved[i],Vector2.Distance(starts[i],eb.position));if(eb.linearVelocity.sqrMagnitude<.0001f)stopCounts[i]++;ObserveTerrain(r,e);Assert.That(TileMover.CanOccupyBox(r,eb.position,TileMover.GetMapClearanceExtents(eb,e.bodyRadius)),Is.True,"Real enemy stays within executable terrain clearance");Row("three-weevils",e.gameObject,"target="+(targetField?.GetValue(e) as Transform)?.name);}
            }
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());Debug.Log($"COOPERATIVE_ENEMY_CROWD bend={bend} playerCursor={cursor} acquired={string.Join("/",acquired)} maxProgress={string.Join("/",moved)} stoppedSamples={string.Join("/",stopCounts)} leaderAttempts={ExpeditionDevelopmentControls.DamageAttempts}");
            for(int i=0;i<3;i++)Debug.Log($"COOPERATIVE_ENEMY_FINAL index={i} start={starts[i].ToString("R")} final={enemies[i].GetComponent<Rigidbody2D>().position.ToString("R")} player={player.GetComponent<Rigidbody2D>().position.ToString("R")} target={(targetField?.GetValue(enemies[i]) as Transform)?.name} route={enemies[i].GetComponent<ActorTerrainBinding>().RouteStatus}");
            // This is an observation baseline, not a requirement that every rear enemy can attack through a front enemy.
            Assert.That(acquired.Any(v=>v),Is.True,"Actual AI acquired the leader");Assert.That(moved.Max(),Is.GreaterThan(.5f),"Actual native pursuit rather than a static placement");
            ExpeditionDevelopmentControls.SetGodMode(false);var exiting=player.GetComponent<ActorTerrainBinding>().Movement;Find<PauseMenuController>().OpenPause();Find<PauseMenuController>().QuitToMainMenu();yield return Wait(()=>Find<MainMenuController>()!=null,"Menu",30);for(int i=0;i<5;i++)yield return null;
            Assert.That(exiting.ActorCount,Is.Zero);Assert.That(exiting.OwnedContactOverrides,Is.Zero);Assert.That(exiting.OwnedNativeFilterLeases,Is.Zero);Assert.That(DiggableTerrainService.Active,Is.Null);
        }
        finally{RegionMovementCoordinator.DiagnosticObserver-=Capture;ExpeditionDevelopmentControls.SetGodMode(false);if(keyboard!=null)InputSystem.QueueStateEvent(keyboard,new KeyboardState());ExportTerrain();native.Export(directory);File.WriteAllLines(Path.Combine(directory,"combat-crowding.csv"),observations);}
    }
    [UnityTearDown]public IEnumerator Cleanup()
    {RegionMovementCoordinator.DiagnosticObserver-=Capture;ExpeditionDevelopmentControls.SetGodMode(false);if(host!=null)host.StopAllCoroutines();if(keyboard!=null){InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.RemoveDevice(keyboard);keyboard=null;}foreach(var go in spawned)if(go!=null)UnityEngine.Object.Destroy(go);if(host!=null)UnityEngine.Object.Destroy(host.gameObject);if(Application.isPlaying)yield return new ExitPlayMode();}
}
#endif
