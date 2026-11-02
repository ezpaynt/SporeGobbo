#if UNITY_EDITOR
using System;using System.IO;using System.Collections;using System.Collections.Generic;using System.Linq;using System.Reflection;
using SporeGobbo.CampCandidate;using NUnit.Framework;using UnityEngine;using UnityEngine.SceneManagement;using UnityEngine.TestTools;using SporeGobbo.Safety;

// Actual prefab, Camp/Pit protected health owners, automatic native physics and real Weevil AI.
// Placement/task suppression are fixture adapters. No collider/physics/combat tuning overrides.
public sealed class Stage64DWeevilBiteTests
{
    Stage64DContractHost host;SettlementCampRegion region;CampFightPitLab lab;string directory;
    readonly List<string> rows=new List<string>();
    static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Find<T>()where T:UnityEngine.Object=>UnityEngine.Object.FindAnyObjectByType<T>();
    static object Call(object instance,string method,params object[] args)=>instance.GetType().GetMethod(method,Hidden).Invoke(instance,args);
    static int Health(GameObject actor)=>actor.GetComponent<GobboController>() is GobboController p?p.health:actor.GetComponent<BuddyUnit>().unitData.health;
    IEnumerator Wait(Func<bool> predicate,string label,float seconds){float end=Time.realtimeSinceStartup+seconds;while(!predicate()&&Time.realtimeSinceStartup<end)yield return null;Assert.That(predicate(),Is.True,label);}
    IEnumerator Host(IEnumerator routine)
    {
        Assert.That(Persistence.Isolated&&V1RuntimeProfile.DevelopmentStorage&&V1RuntimeProfile.DevelopmentToolsEnabled,Is.True);
        yield return new EnterPlayMode();var owner=new GameObject("Owned directional bite host");UnityEngine.Object.DontDestroyOnLoad(owner);host=owner.AddComponent<Stage64DContractHost>();host.Begin(routine);
        float end=Time.realtimeSinceStartup+120;while(!host.Complete&&Time.realtimeSinceStartup<end)yield return null;
        Assert.That(host.Complete,Is.True,"Existing real-service120-second host deadline");if(host.Error!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(host.Error).Throw();
    }
    IEnumerator Setup(string label)
    {
        directory=Persistence.ExperimentPath("Evidence/weevil-bite-"+label);Directory.CreateDirectory(directory);rows.Add("target,direction,tick,enemyX,enemyY,targetX,targetY,state,bite,attempts,hits,health,nativeGap,legacyDistance");
        SceneManager.LoadScene(GameSceneCatalog.MainMenu);yield return Wait(()=>Find<MainMenuController>()!=null,"Actual Menu",30);yield return null;DevelopmentCampaignFixtures.Import(2);Find<MainMenuController>().LoadGame(2);
        yield return Wait(()=>Find<CampSceneController>()?.WorldInputReady==true&&Find<SettlementCampRegion>()?.Ready==true,"Actual Camp",60);region=Find<SettlementCampRegion>();lab=Find<CampFightPitLab>();Assert.That(region.OpenPitDevelopmentEntrance(),Is.True);for(int i=0;i<3;i++)yield return null;
    }
    IEnumerator StartRound(bool buddy,Vector2 direction)
    {
        yield return Wait(()=>region.Ready&&!lab.Active,"Real Pit readiness",10);lab.Watch=buddy;lab.Opponent="Weevil";lab.OpponentCount=1;lab.Preset="Open";lab.GodMode=false;lab.SelectedBuddies.Clear();
        if(buddy)lab.SelectedBuddies.Add(UnityEngine.Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None).Where(b=>b.CombatAlive).OrderBy(b=>b.unitData.uniqueId,StringComparer.Ordinal).First().unitData.uniqueId);
        lab.StartSession();Assert.That(lab.Fighting,Is.True);var target=lab.Fighters.Single(a=>a.GetComponent<CombatSessionBinding>().Team==0);var enemy=lab.Fighters.Single(a=>a.GetComponent<CombatSessionBinding>().Team==1);
        // Keep target health owner/body live, suppress only its competing attacks/movement.
        if(target.TryGetComponent<GobboController>(out var player))player.SuppressTemporaryInput=true;
        else foreach(var task in target.GetComponents<Behaviour>().Where(b=>b is BuddyBrain||b is BuddyFollow||b is BuddyCombat||b is BuddyScavenger||b is CampWander||b is CampDirectedWalk))task.enabled=false;
        var center=region.World(CampFightPitLayout.Center);Place(target,center);Place(enemy,center-direction.normalized*2.2f);yield return null;yield return null;
        Assert.That(RegionMovementCoordinator.EffectiveNativeContact(target.GetComponent<Collider2D>(),enemy.GetComponent<Collider2D>()),Is.True,"Real hostile contact enabled");
    }
    void Place(GameObject actor,Vector2 p){var body=actor.GetComponent<Rigidbody2D>();Assert.That(TileMover.CanOccupyBox(region,p,TileMover.GetMapClearanceExtents(body,.25f)),Is.True,"Actual executable setup clearance");body.position=p;body.linearVelocity=Vector2.zero;actor.transform.position=p;Physics2D.SyncTransforms();}
    void Observe(string kind,int index,TunnelWeevilEnemy enemy,GameObject target)
    {
        Assert.That(rows.Count,Is.LessThan(10000),"Bounded evidence");var a=enemy.GetComponent<Rigidbody2D>();var b=target.GetComponent<Rigidbody2D>();var gap=enemy.GetComponent<Collider2D>().Distance(target.GetComponent<Collider2D>());var point=(Vector2)Call(enemy,"GetClosestPointOnCurrentTarget");
        rows.Add(FormattableString.Invariant($"{kind},{index},{Time.fixedTimeAsDouble:R},{a.position.x:R},{a.position.y:R},{b.position.x:R},{b.position.y:R},{typeof(TunnelWeevilEnemy).GetField("combatState",Hidden).GetValue(enemy)},{Call(enemy,"IsCurrentTargetInBiteRange")},{lab.DamageAttempts},{lab.OpponentHits},{Health(target)},{gap.distance:R},{Vector2.Distance(enemy.transform.position,point):R}"));
    }
    [UnityTest,Timeout(240000)]public IEnumerator HistoricalOriginMismatchAndForcedWindupDiagnostic()=>Host(Baseline());
    IEnumerator Baseline()
    {
        yield return Setup("original");yield return StartRound(false,new Vector2(1,.4f));var enemy=lab.Fighters.Single(a=>a.GetComponent<TunnelWeevilEnemy>()!=null).GetComponent<TunnelWeevilEnemy>();var target=lab.Fighters.Single(a=>a.GetComponent<GobboController>()!=null);int hp=Health(target);
        try
        {
            float end=Time.realtimeSinceStartup+5;while(lab.Fighting&&Time.realtimeSinceStartup<end){yield return new WaitForFixedUpdate();Observe("baseline",0,enemy,target);}
            var closest=(Vector2)Call(enemy,"GetClosestPointOnCurrentTarget");var direction=(closest-(Vector2)enemy.transform.position).normalized;var center=(Vector2)enemy.transform.position+direction*enemy.meleeHitForwardOffset;
            bool footprint=Call(enemy,"FindMeleeHitTarget",center) is Collider2D hit&&EnemyDamage.Resolve(hit.gameObject)?.gameObject==target;
            Assert.That(lab.OpponentHits,Is.Zero,"Historical native contact cannot initiate bite");Assert.That((bool)Call(enemy,"IsCurrentTargetInBiteRange"),Is.False);
            Assert.That(footprint&&EnemyDamage.Eligible(enemy.gameObject,EnemyDamage.Resolve(target))&&EnemyDamage.AttackClear(enemy.gameObject,EnemyDamage.Resolve(target)),Is.True,"Unchanged actual hit shape/eligibility/obstruction can reach this target");
            Debug.Log($"BITE_BASELINE range={enemy.attackRange:R} padding={enemy.attackContactPadding:R} originDistance={Vector2.Distance(enemy.transform.position,closest):R} nativeGap={enemy.GetComponent<Collider2D>().Distance(target.GetComponent<Collider2D>()).distance:R} footprint={footprint} offset={enemy.GetComponent<Collider2D>().offset.ToString("R")}");
            // Diagnostic-only counterfactual: bypass only initiation, then ordinary windup/real damage.
            Call(enemy,"FaceTargetDirection",closest);Call(enemy,"BeginAttackWindup");end=Time.realtimeSinceStartup+1;
            while(lab.Fighting&&lab.OpponentHits==0&&Time.realtimeSinceStartup<end){yield return new WaitForFixedUpdate();Observe("forced-windup",0,enemy,target);}
            Assert.That(lab.OpponentHits,Is.GreaterThan(0));Assert.That(Health(target),Is.LessThan(hp),"Real unchanged damage executes after forced initiation; not a natural AI pass");Debug.Log($"BITE_BASELINE_FORCED actualDamage={hp-Health(target)} hits={lab.OpponentHits}");
        }
        finally{File.WriteAllLines(Path.Combine(directory,"observations.csv"),rows);if(lab.Active)lab.StopSession("historical diagnostic cleanup");}
    }
    [UnityTest,Timeout(240000)]public IEnumerator NativeDirectionalBitesAndUnchangedCooldown()=>Host(Directions());
    IEnumerator Directions()
    {
        yield return Setup("directions");var directions=new[]{Vector2.right,Vector2.up,Vector2.left,Vector2.down,new Vector2(1,1),new Vector2(-1,1),new Vector2(-1,-1),new Vector2(1,-1)};
        try
        {
            foreach(bool buddy in new[]{false,true})for(int i=0;i<directions.Length;i++)
            {
                yield return StartRound(buddy,directions[i]);var enemy=lab.Fighters.Single(a=>a.GetComponent<TunnelWeevilEnemy>()!=null).GetComponent<TunnelWeevilEnemy>();var target=lab.Fighters.Single(a=>a.GetComponent<CombatSessionBinding>().Team==0);int hp=Health(target);
                Assert.That((bool)Call(enemy,"IsCurrentTargetInBiteRange"),Is.False,"Outside nominal reach and physical contact must not initiate");
                if(!buddy&&i==0)
                {
                    var original=enemy.GetComponent<Rigidbody2D>().position;Place(enemy.gameObject,(Vector2)target.transform.position-Vector2.right*1.2f);
                    var closest=(Vector2)Call(enemy,"GetClosestPointOnCurrentTarget");var hypothetical=(Vector2)enemy.transform.position+(closest-(Vector2)enemy.transform.position).normalized*enemy.meleeHitForwardOffset;
                    Assert.That(Call(enemy,"FindMeleeHitTarget",hypothetical),Is.Not.Null,"Actual hit circle can overlap outside intended initiation distance");
                    Assert.That((bool)Call(enemy,"IsCurrentTargetInBiteRange"),Is.False,"Hit-circle reach alone must not globally extend bite initiation");Place(enemy.gameObject,original);
                }
                float end=Time.realtimeSinceStartup+5;bool windup=false;int contacts=0;var buffer=new ContactPoint2D[32];
                while(lab.Fighting&&lab.OpponentHits==0&&Time.realtimeSinceStartup<end)
                {yield return new WaitForFixedUpdate();Observe(buddy?"Buddy":"Player",i,enemy,target);windup|=typeof(TunnelWeevilEnemy).GetField("combatState",Hidden).GetValue(enemy).ToString()=="AttackWindup";int count=enemy.GetComponent<Rigidbody2D>().GetContacts(buffer);Assert.That(count,Is.LessThan(buffer.Length));for(int j=0;j<count;j++)if(buffer[j].collider==target.GetComponent<Collider2D>()||buffer[j].otherCollider==target.GetComponent<Collider2D>())contacts++;}
                Debug.Log($"BITE_DIRECTION target={(buddy?"Buddy":"Player")} index={i} windup={windup} hits={lab.OpponentHits} damage={hp-Health(target)} contacts={contacts} range={enemy.attackRange:R} padding={enemy.attackContactPadding:R} windupSeconds={enemy.attackWindup:R} cooldown={enemy.attackCooldown:R}");
                Assert.That(windup&&lab.OpponentHits>0&&Health(target)<hp,Is.True,"Actual AI windup and accepted damage in direction"+i);Assert.That(Health(target),Is.GreaterThanOrEqualTo(1));
                if(lab.Active)lab.StopSession("directional round cleanup");yield return null;yield return null;
            }
            // Existing real terrain preset: no new occupancy or obstacle authority.
            yield return Wait(()=>region.Ready&&!lab.Active,"Rock round readiness",10);lab.Watch=false;lab.SelectedBuddies.Clear();lab.Preset="Rock";lab.StartSession();yield return null;yield return null;
            var wallEnemy=lab.Fighters.Single(a=>a.GetComponent<TunnelWeevilEnemy>()!=null);var wallTarget=lab.Fighters.Single(a=>a.GetComponent<GobboController>()!=null);wallTarget.GetComponent<GobboController>().SuppressTemporaryInput=true;
            Place(wallEnemy,region.World(new SporeGobbo.Geometry.Point2(159.5,86.5)));Place(wallTarget,region.World(new SporeGobbo.Geometry.Point2(152.5,86.5)));int wallHealth=Health(wallTarget);
            Assert.That(EnemyDamage.AttackClear(wallEnemy,EnemyDamage.Resolve(wallTarget)),Is.False,"Actual permanent preset blocks melee visibility");
            var blocked=new MeleeAttackExecution(wallEnemy).Commit(wallTarget,wallEnemy.GetComponent<TunnelWeevilEnemy>().attackDamage);
            Assert.That(blocked.Failure,Is.EqualTo(EnemyDamageFailure.Obstructed));Assert.That(Health(wallTarget),Is.EqualTo(wallHealth),"No damage through terrain");lab.StopSession("real rock obstruction cleanup");yield return null;yield return null;
            yield return StartRound(false,Vector2.right);lab.GodMode=true;var clockEnemy=lab.Fighters.Single(a=>a.GetComponent<TunnelWeevilEnemy>()!=null).GetComponent<TunnelWeevilEnemy>();var clockTarget=lab.Fighters.Single(a=>a.GetComponent<GobboController>()!=null);var attempts=new List<float>();int prior=lab.DamageAttempts;float until=Time.realtimeSinceStartup+5;
            while(lab.Fighting&&attempts.Count<2&&Time.realtimeSinceStartup<until){yield return new WaitForFixedUpdate();Observe("cooldown-protected",0,clockEnemy,clockTarget);if(lab.DamageAttempts!=prior){attempts.Add(Time.time);prior=lab.DamageAttempts;}}
            Assert.That(attempts.Count,Is.EqualTo(2),"Real repeated damage attempts under existing player-only GodMode");Assert.That(attempts[1]-attempts[0],Is.GreaterThanOrEqualTo(clockEnemy.attackCooldown+clockEnemy.attackWindup-Time.fixedDeltaTime*2),"Unchanged cooldown and next windup prevent rapid hits");Debug.Log($"BITE_COOLDOWN interval={attempts[1]-attempts[0]:R} minimum={clockEnemy.attackCooldown+clockEnemy.attackWindup-Time.fixedDeltaTime*2:R}");lab.StopSession("cooldown cleanup");
        }
        finally{File.WriteAllLines(Path.Combine(directory,"observations.csv"),rows);if(lab!=null&&lab.Active)lab.StopSession("directional fixture cleanup");}
    }
    [Test]public void SharedContactUsesRealBodiesNotSensorsOrDetachedChildren()
    {
        var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TunnelWeevil.prefab");Assert.That(prefab,Is.Not.Null);GameObject a=null,b=null;
        try
        {
            a=UnityEngine.Object.Instantiate(prefab);b=UnityEngine.Object.Instantiate(prefab);var ab=a.GetComponent<Rigidbody2D>();var bb=b.GetComponent<Rigidbody2D>();a.transform.position=Vector2.zero;b.transform.position=Vector2.right*3;ab.position=Vector2.zero;bb.position=Vector2.right*3;
            var sensors=new GameObject("Owned trigger sensor");sensors.transform.SetParent(a.transform,false);sensors.AddComponent<BoxCollider2D>().size=Vector2.one*20;sensors.GetComponent<Collider2D>().isTrigger=true;
            var detached=new GameObject("Owned detached body");detached.transform.SetParent(a.transform,false);detached.transform.position=bb.position;detached.AddComponent<Rigidbody2D>().position=bb.position;detached.AddComponent<BoxCollider2D>().size=Vector2.one;
            Physics2D.SyncTransforms();var sources=new List<Collider2D>();var targets=new List<Collider2D>();var owner=EnemyDamage.Resolve(b);
            Assert.That(detached.GetComponent<Collider2D>().Distance(b.GetComponent<Collider2D>()).distance,Is.LessThanOrEqualTo(Physics2D.defaultContactOffset*2+.001f),"Detached collider is actually touching, so ownership exclusion is exercised");
            Assert.That(MeleeAttackExecution.AtBodyContact(a,ab,owner,sources,targets),Is.False,"Sensors and child bodies cannot extend source contact reach");
            bb.position=Vector2.right*.51f;b.transform.position=bb.position;Physics2D.SyncTransforms();Assert.That(MeleeAttackExecution.AtBodyContact(a,ab,owner,sources,targets),Is.True,"Actual prefab body native contact envelope");
            a.GetComponent<Collider2D>().enabled=false;Assert.That(MeleeAttackExecution.AtBodyContact(a,ab,owner,sources,targets),Is.False,"Disabled actual body cannot be replaced by detached/sensor colliders");
            var c=b.GetComponent<Collider2D>();Assert.That(MeleeAttackExecution.TargetInsideCircle(owner,c.bounds.center,.01f,0,targets),Is.True);Assert.That(MeleeAttackExecution.TargetInsideCircle(owner,c.bounds.center,.01f,1<<2,targets),Is.False,"Hit mask is preserved");Assert.That(MeleeAttackExecution.TargetInsideCircle(owner,c.bounds.center+Vector3.right*2,.1f,0,targets),Is.False,"No hit outside actual circle");
        }
        finally{if(a!=null)UnityEngine.Object.DestroyImmediate(a);if(b!=null)UnityEngine.Object.DestroyImmediate(b);}
    }
    [UnityTearDown]public IEnumerator Cleanup(){if(host!=null)host.StopAllCoroutines();if(lab!=null&&lab.Active)lab.StopSession("exception cleanup");if(host!=null)UnityEngine.Object.Destroy(host.gameObject);if(Application.isPlaying)yield return new ExitPlayMode();}
}
#endif

