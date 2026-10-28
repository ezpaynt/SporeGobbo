#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SporeGobbo.CampLifecycle;
using UnityEditor;
using System.Reflection;

/// <summary>Reviewed real-owner tests; no campaign selection, scene install or native storage.</summary>
public class Stage64BCombatTests
{
    sealed class Grid:IDiggableTerrain
    {
        public bool Wall;public float CellSize=>1;
        public bool IsBlocked(Vector2Int c)=>Wall&&c.x==1;
        public bool IsDiggable(Vector2Int c)=>false;
        public Vector2Int WorldToCell(Vector2 p)=>Vector2Int.FloorToInt(p);
        public Vector2 CellToWorld(Vector2Int c)=>(Vector2)c+Vector2.one*.5f;
        public void DigCircle(Vector2 p,float r){}
        public TerrainDigResult DigCircle(Vector2 p,float r,TerrainDigAuthority a,int s,IReadOnlyCollection<Vector2Int> cells)=>new TerrainDigResult(0,0,0,TerrainDigFailureReason.NoDirtRemoved);
    }
    readonly List<GameObject> owned=new List<GameObject>();
    Grid grid;GameObject leader;EnemyHealth enemy;bool ready;int revision;
    GameObject Make(string name,Vector2 pos)
    {var go=new GameObject(name);owned.Add(go);go.transform.position=pos;return go;}
    void Bind(GameObject go)
    {var b=go.AddComponent<ActorTerrainBinding>();b.Bind(grid);b.Ready=()=>ready;b.ReadRevision=()=>revision;}
    [SetUp]public void Setup()
    {
        grid=new Grid();ready=true;revision=0;
        leader=Make("source",new Vector2(.25f,.5f));leader.AddComponent<GobboController>();Bind(leader);
        var target=Make("real health owner",new Vector2(2.5f,.5f));enemy=target.AddComponent<EnemyHealth>();enemy.health=20;enemy.maxHealth=20;Bind(target);
    }
    [TearDown]public void Cleanup(){for(int i=owned.Count-1;i>=0;i--)if(owned[i]!=null)Object.DestroyImmediate(owned[i]);owned.Clear();}
    GameObject Child(GameObject parent,string name)
    {var go=Make(name,parent.transform.position);go.transform.SetParent(parent.transform,true);go.AddComponent<CircleCollider2D>();return go;}
    [Test]public void MultipleChildHurtboxesResolveOneCanonicalOwnerAndOneHitPerAction()
    {
        var a=Child(enemy.gameObject,"hurtbox a");var b=Child(enemy.gameObject,"hurtbox b");var action=new MeleeAttackExecution(leader);
        var hit=action.Commit(a,3,"real-source-id");var duplicate=action.Commit(b,3,"real-source-id");
        Assert.That(hit.Accepted,Is.True);Assert.That(hit.Target,Is.SameAs(enemy));Assert.That(hit.AppliedDamage,Is.EqualTo(3));
        Assert.That(duplicate.Failure,Is.EqualTo(EnemyDamageFailure.Duplicate));Assert.That(enemy.health,Is.EqualTo(17));
    }
    [Test]public void IndependentAttackersCanHitSameActorInSameFrame()
    {
        var other=Make("second source",new Vector2(.25f,.7f));other.AddComponent<GobboController>();Bind(other);
        Assert.That(new MeleeAttackExecution(leader).Commit(enemy.gameObject,2).Accepted,Is.True);
        Assert.That(new MeleeAttackExecution(other).Commit(enemy.gameObject,2).Accepted,Is.True);Assert.That(enemy.health,Is.EqualTo(16));
    }
    [Test]public void ZeroDamageIsNotAnAcceptedReceipt(){Assert.That(EnemyDamage.TryApply(enemy.gameObject,new EnemyDamageRequest(0)).Accepted,Is.False);Assert.That(enemy.health,Is.EqualTo(20));}
    [Test]public void DeadReceiverRejectsDispatch(){enemy.health=0;Assert.That(EnemyDamage.Apply(enemy.gameObject,3),Is.False);}
    [Test]public void DisabledReceiverRejectsDispatch(){enemy.enabled=false;Assert.That(EnemyDamage.Apply(enemy.gameObject,3),Is.False);}
    [Test]public void CancelledActionDoesNotMutateHealth(){var action=new MeleeAttackExecution(leader);action.Cancel();Assert.That(action.Commit(enemy.gameObject,3).Failure,Is.EqualTo(EnemyDamageFailure.Cancelled));Assert.That(enemy.health,Is.EqualTo(20));}
    [Test]public void CurrentObstructionIsCheckedAtCommit(){grid.Wall=true;Assert.That(new MeleeAttackExecution(leader).Commit(enemy.gameObject,3).Failure,Is.EqualTo(EnemyDamageFailure.Obstructed));grid.Wall=false;revision++;Assert.That(new MeleeAttackExecution(leader).Commit(enemy.gameObject,3).Accepted,Is.True);}
    [Test]public void RegionTeardownRejectsPendingAction(){var action=new MeleeAttackExecution(leader);ready=false;Assert.That(action.Commit(enemy.gameObject,3).Accepted,Is.False);Assert.That(enemy.health,Is.EqualTo(20));}
    [Test]public void SourceDeathCancelsPendingAction(){var action=new MeleeAttackExecution(leader);leader.GetComponent<GobboController>().health=0;Assert.That(action.Commit(enemy.gameObject,3).Accepted,Is.False);}
    [Test]public void SourceChildCanonicalizationPreservesGobboIdentity(){var child=Child(leader,"weapon child");var request=new EnemyDamageRequest(3,"gobbo-identity",child);Assert.That(request.source,Is.SameAs(leader));Assert.That(request.gobboSourceId,Is.EqualTo("gobbo-identity"));}
    [Test]public void DestroyedSourceDoesNotBecomeEnvironmentalDamage(){var request=new EnemyDamageRequest(3,"",leader);Object.DestroyImmediate(leader);Assert.That(EnemyDamage.TryApply(enemy.gameObject,request).Failure,Is.EqualTo(EnemyDamageFailure.Cancelled));}
    [Test]public void OrdinaryPlayerDefenseIsPreserved(){var p=leader.GetComponent<GobboController>();p.defense=2;p.health=100;var hit=p.ReceiveCombatDamage(new EnemyDamageRequest(5));Assert.That(hit.AppliedDamage,Is.EqualTo(3));Assert.That(p.health,Is.EqualTo(97));}
    [Test]public void OrdinaryBuddyDefenseIsPreserved(){var go=Make("real buddy",Vector2.zero);var u=go.AddComponent<BuddyUnit>();u.Initialize(new GobboUnitSaveData{health=100,maxHealth=100,defense=2});var hit=u.ReceiveCombatDamage(new EnemyDamageRequest(5));Assert.That(hit.Accepted,Is.True);Assert.That(hit.AppliedDamage,Is.EqualTo(3));Assert.That(u.unitData.health,Is.EqualTo(97));}
    [Test]public void MissingReceiverCannotProduceFalseHit(){var go=Make("visual-only",Vector2.zero);Assert.That(EnemyDamage.Apply(go,3),Is.False);}
    [Test]public void FriendlyGobboCannotBeDamagedWithoutExplicitMatchPolicy(){var go=Make("ally",Vector2.zero);go.AddComponent<GobboController>();Assert.That(EnemyDamage.TryApply(go,new EnemyDamageRequest(3,"",leader)).Failure,Is.EqualTo(EnemyDamageFailure.Ineligible));}
    [TestCase("Assets/TunnelWeevil.prefab",typeof(TunnelWeevilEnemy))]
    [TestCase("Assets/BlobSpitter.prefab",typeof(BlobSpitterEnemy))]
    public void RealSerializedEnemyPrefabsRetainTheirSingleHealthOwner(string path,System.Type ownerType)
    {var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);Assert.That(prefab,Is.Not.Null);Assert.That(prefab.GetComponent(ownerType),Is.Not.Null);Assert.That(prefab.GetComponents<EnemyHealth>().Length,Is.Zero);}
    [Test]public void WeevilReceiptUsesActualAttackerForKnockback()
    {
        var go=Make("real weevil owner",new Vector2(2.5f,.5f));var w=go.AddComponent<TunnelWeevilEnemy>();w.health=20;w.spriteRenderer=null;Bind(go);
        var decoy=Make("closer wrong attacker",new Vector2(2.7f,.5f));decoy.AddComponent<GobboController>();
        var result=EnemyDamage.TryApply(go,new EnemyDamageRequest(3,"source-id",leader));
        Assert.That(result.Accepted,Is.True);Assert.That(result.Target,Is.SameAs(w));Assert.That(w.health,Is.EqualTo(17));
        var velocity=(Vector2)typeof(TunnelWeevilEnemy).GetField("knockbackVelocity",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(w);
        Assert.That(velocity.x,Is.GreaterThan(0));
    }
    [Test]public void BlobReceiptMutatesItsExistingHealthOnly()
    {var go=Make("real blob owner",Vector2.one);var blob=go.AddComponent<BlobSpitterEnemy>();blob.health=20;blob.spriteRenderer=null;Bind(go);var hit=EnemyDamage.TryApply(go,new EnemyDamageRequest(3,"",leader));Assert.That(hit.Accepted,Is.True);Assert.That(hit.Target,Is.SameAs(blob));Assert.That(blob.health,Is.EqualTo(17));}
    [Test]public void ProjectileRoutesTwoChildCollidersToOnePlayerHealthReceipt()
    {
        var shooter=Make("real blob source",new Vector2(3,.5f));var blob=shooter.AddComponent<BlobSpitterEnemy>();blob.health=20;
        var a=Child(leader,"hurt a");var b=Child(leader,"hurt b");var go=Make("real projectile",Vector2.one);go.AddComponent<CircleCollider2D>();var shot=go.AddComponent<EnemyProjectile>();shot.hitLayers=~0;shot.destroyOnHit=false;shot.damage=12;shot.Launch(Vector2.left,shooter.transform);
        var hit=typeof(EnemyProjectile).GetMethod("HandleHit",BindingFlags.Instance|BindingFlags.NonPublic);
        hit.Invoke(shot,new object[]{a.GetComponent<Collider2D>()});hit.Invoke(shot,new object[]{b.GetComponent<Collider2D>()});
        Assert.That(leader.GetComponent<GobboController>().health,Is.EqualTo(90));
    }
    [Test]public void DestroyedSourceCannotBypassDirectOwnerDispatch()
    {var request=new EnemyDamageRequest(3,"",leader);Object.DestroyImmediate(leader);Assert.That(enemy.ReceiveEnemyDamage(request).Accepted,Is.False);Assert.That(enemy.health,Is.EqualTo(20));}
    [Test]public void AlreadyDestroyedSourceCannotBecomeEnvironmentalDamage()
    {Object.DestroyImmediate(leader);var request=new EnemyDamageRequest(3,"",leader);Assert.That(request.hadSource,Is.True);Assert.That(enemy.ReceiveEnemyDamage(request).Accepted,Is.False);}
    [Test]public void SweptSelectionSkipsFriendlyChildCollidersBeforeValidRecipient()
    {
        var shooter=Make("blob source",new Vector2(-4,10));shooter.AddComponent<BlobSpitterEnemy>();
        var friendly=Make("foreground blob",new Vector2(-2,10));friendly.AddComponent<BlobSpitterEnemy>();Child(friendly,"friendly a");Child(friendly,"friendly b");
        var target=Make("valid player",new Vector2(0,10));target.AddComponent<GobboController>();var hurt=Child(target,"valid hurtbox");
        var go=Make("swept shot",new Vector2(-4,10));go.AddComponent<CircleCollider2D>();var shot=go.AddComponent<EnemyProjectile>();shot.Launch(Vector2.right,shooter.transform);Physics2D.SyncTransforms();
        var hits=(RaycastHit2D[])typeof(EnemyProjectile).GetField("sweepHits",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(shot);
        var filter=new ContactFilter2D();filter.SetLayerMask(~0);filter.useTriggers=true;
        int count=Physics2D.CircleCast(new Vector2(-4,10),.1f,Vector2.right,filter,hits,6);
        object[] args={count,0f};var selected=typeof(EnemyProjectile).GetMethod("SelectSweptRecipient",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(shot,args);
        Assert.That(selected,Is.SameAs(hurt.GetComponent<Collider2D>()));
    }
    [Test]public void ProjectileRejectsCapturedTerrainAfterProviderMismatch()
    {var go=Make("shot",Vector2.zero);go.AddComponent<CircleCollider2D>();var shot=go.AddComponent<EnemyProjectile>();shot.Launch(Vector2.right,leader.transform);Assert.That((bool)typeof(EnemyProjectile).GetMethod("CanContinue",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(shot,null),Is.False);}
    sealed class NonlethalFixture:ICombatSessionRules
    {
        public bool Active=true;
        public bool Allows(GameObject a,GameObject b)=>Active&&a!=null&&a!=b;
        public bool ActionsAllowed(GameObject actor)=>Active;
        public int LimitDamage(GameObject target,EnemyDamageRequest request,int proposed)=>Mathf.Min(proposed,Mathf.Max(0,target.GetComponent<BuddyUnit>().unitData.health-1));
        public void AfterDamage(GameObject target,EnemyDamageRequest request,EnemyDamageResult result){if(target.GetComponent<BuddyUnit>().unitData.health==1)Active=false;}
    }
    [Test]public void ProtectedBuddyThresholdFreezesBeforeSecondQueuedAttack()
    {
        var rules=new NonlethalFixture();var a=Make("fighter a",Vector2.zero);var b=Make("fighter b",Vector2.one);
        var u=a.AddComponent<BuddyUnit>();u.Initialize(new GobboUnitSaveData{health=100,maxHealth=100});var v=b.AddComponent<BuddyUnit>();v.Initialize(new GobboUnitSaveData{health=100,maxHealth=100});
        a.AddComponent<CombatSessionBinding>().Rules=rules;b.AddComponent<CombatSessionBinding>().Rules=rules;
        var first=u.ReceiveCombatDamage(new EnemyDamageRequest(1000,"",b));var queued=v.ReceiveCombatDamage(new EnemyDamageRequest(1000,"",a));
        Assert.That(first.Accepted,Is.True);Assert.That(first.Killed,Is.False);Assert.That(u.unitData.health,Is.EqualTo(1));Assert.That(queued.Accepted,Is.False);Assert.That(v.unitData.health,Is.EqualTo(100));Assert.That(u.unitData.isDead,Is.False);
    }
    [Test]public void DirectUnownedDamageCannotBypassSessionRecipientEligibility()
    {var go=Make("bound real buddy",Vector2.zero);var u=go.AddComponent<BuddyUnit>();u.Initialize(new GobboUnitSaveData{health=100,maxHealth=100});go.AddComponent<CombatSessionBinding>().Rules=new NonlethalFixture();Assert.That(u.ReceiveCombatDamage(new EnemyDamageRequest(1000)).Accepted,Is.False);Assert.That(u.unitData.health,Is.EqualTo(100));}
}
#endif
