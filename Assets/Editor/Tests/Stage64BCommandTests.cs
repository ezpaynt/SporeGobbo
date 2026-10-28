#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SporeGobbo.Input;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

/// <summary>Owned inactive region identity adapter, real actor/brain/mode components. No scene bootstrap, save selection, or terrain renderer.</summary>
public sealed class Stage64BCommandTests
{
    readonly List<GameObject> owned=new List<GameObject>();Scene scene,previousScene;ExpeditionRegion region;GameState state;GobboController leader;int serial;
    static readonly BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    GameObject Make(string name)
    {var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,scene);owned.Add(go);return go;}
    void Bind(GameObject go){var binding=go.AddComponent<ActorTerrainBinding>();binding.Bind(region);binding.Ready=()=>true;binding.ReadRevision=()=>0;binding.FindRoute=(a,b)=>new List<Vector2>{a,b};}
    static void InitializeComponent(MonoBehaviour component)=>component.GetType().GetMethod("Awake",Hidden)?.Invoke(component,null);
    [SetUp]public void Setup()
    {
        Assert.That(GameState.Instance==null,Is.True,"No live campaign authority may exist");Assert.That(DiggableTerrainService.Active,Is.Null);Assert.That(SceneManager.GetSceneByName(GameSceneCatalog.CandidateExpedition).IsValid(),Is.False);
        previousScene=SceneManager.GetActiveScene();scene=EditorSceneManager.NewPreviewScene();scene.name=GameSceneCatalog.CandidateExpedition;
        var host=Make("inactive identity adapter, never bootstrap");host.SetActive(false);region=host.AddComponent<ExpeditionRegion>();
        state=Make("owned synthetic state, no slot").AddComponent<GameState>();state.EnsureRuntimeDefaults();
        typeof(GameState).GetField("<Instance>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,state);
        leader=Make("actual leader component").AddComponent<GobboController>();InitializeComponent(leader);Bind(leader.gameObject);
        typeof(ExpeditionRegion).GetField("<Player>k__BackingField",Hidden).SetValue(region,leader);
        DiggableTerrainService.Register(region);
    }
    [TearDown]public void Cleanup()
    {
        try{DiggableTerrainService.Unregister(region);for(int i=owned.Count-1;i>=0;i--)if(owned[i]!=null)Object.DestroyImmediate(owned[i]);}
        finally{owned.Clear();if(scene.IsValid()&&EditorSceneManager.IsPreviewScene(scene))EditorSceneManager.ClosePreviewScene(scene);if(object.ReferenceEquals(GameState.Instance,state))typeof(GameState).GetField("<Instance>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,null);}
    }
    BuddyUnit Buddy(bool active=true,GobboUnitSaveData data=null)
    {
        data??=new GobboUnitSaveData{uniqueId="owned-command-"+(++serial),health=100,maxHealth=100,collectsFood=true};
        if(!state.ownedGobbos.Contains(data))state.ownedGobbos.Add(data);if(active&&!state.activeSquadIds.Contains(data.uniqueId))state.activeSquadIds.Add(data.uniqueId);
        var go=Make("actual Buddy");go.AddComponent<BuddyFollow>();go.AddComponent<BuddyCombat>();go.AddComponent<BuddyScavenger>();var unit=go.AddComponent<BuddyUnit>();InitializeComponent(unit);unit.Initialize(data);var brain=go.AddComponent<BuddyBrain>();InitializeComponent(go.GetComponent<BuddyFollow>());InitializeComponent(go.GetComponent<BuddyCombat>());InitializeComponent(go.GetComponent<BuddyScavenger>());InitializeComponent(brain);Bind(go);return unit;
    }
    [TestCase(true,true)][TestCase(true,false)][TestCase(false,true)][TestCase(false,false)]
    public void FourOrthogonalModesPreserveCanonicalStateAndDoNotGrantImmunity(bool following,bool aggressive)
    {
        var unit=Buddy();Assert.That(leader.RegisterSquadBuddy(unit),Is.True);string before=JsonUtility.ToJson(state);
        leader.IssueBuddyCommand(following?BuddyCommand.Follow:BuddyCommand.Stay);leader.IssueBuddyCommand(aggressive?BuddyCommand.Aggressive:BuddyCommand.Passive);
        var brain=unit.GetComponent<BuddyBrain>();Assert.That(brain.Following,Is.EqualTo(following));Assert.That(brain.CombatPermitted,Is.EqualTo(aggressive));Assert.That(JsonUtility.ToJson(state),Is.EqualTo(before));
        Assert.That(unit.ReceiveCombatDamage(new EnemyDamageRequest(3)).Accepted,Is.True);Assert.That(unit.unitData.health,Is.LessThan(100));
    }
    [Test]public void StayAnchorSurvivesAggressionChangesAndRepeatedRegistration()
    {
        var unit=Buddy();unit.transform.position=new Vector2(3,4);leader.RegisterSquadBuddy(unit);leader.IssueBuddyCommand(BuddyCommand.Stay);var brain=unit.GetComponent<BuddyBrain>();var anchor=brain.StayAnchor;
        unit.transform.position=new Vector2(4,4);leader.IssueBuddyCommand(BuddyCommand.Passive);leader.IssueBuddyCommand(BuddyCommand.Aggressive);leader.IssueBuddyCommand(BuddyCommand.Stay);leader.RegisterSquadBuddy(unit);Assert.That(brain.StayAnchor,Is.EqualTo(anchor));
        leader.IssueBuddyCommand(BuddyCommand.Follow);Assert.That(brain.Following,Is.True);
    }
    [Test]public void PassiveImmediatelyClearsTargetBounceAndQueuedSharedCommit()
    {
        var unit=Buddy();leader.RegisterSquadBuddy(unit);var combat=unit.GetComponent<BuddyCombat>();var enemy=Make("real target health").AddComponent<EnemyHealth>();enemy.health=20;
        typeof(BuddyCombat).GetField("currentTarget",Hidden).SetValue(combat,enemy.transform);typeof(BuddyCombat).GetField("bounceTimer",Hidden).SetValue(combat,.2f);typeof(BuddyCombat).GetField("bounceVelocity",Hidden).SetValue(combat,Vector2.one);
        leader.IssueBuddyCommand(BuddyCommand.Passive);Assert.That(typeof(BuddyCombat).GetField("currentTarget",Hidden).GetValue(combat),Is.Null);Assert.That((float)typeof(BuddyCombat).GetField("bounceTimer",Hidden).GetValue(combat),Is.Zero);
        Assert.That(EnemyDamage.TryApply(enemy.gameObject,new EnemyDamageRequest(3,"",unit.gameObject)).Accepted,Is.False);Assert.That(enemy.health,Is.EqualTo(20));
    }
    [Test]public void ReserveCloneDuplicateAndUnrelatedLeaderCannotRegister()
    {
        var reserve=Buddy(false);Assert.That(leader.RegisterSquadBuddy(reserve),Is.False);
        var canonical=Buddy();Assert.That(leader.RegisterSquadBuddy(canonical),Is.True);var duplicate=Buddy(true,canonical.unitData);Assert.That(leader.RegisterSquadBuddy(duplicate),Is.False);
        var clone=Buddy();var canonicalData=clone.unitData;clone.unitData=canonicalData.CloneUnit();Assert.That(leader.RegisterSquadBuddy(clone),Is.False);
        var other=Make("unrelated Gobbo").AddComponent<GobboController>();Bind(other.gameObject);Assert.That(other.RegisterSquadBuddy(canonical),Is.False);
    }
    [Test]public void CurrentMaximumRemainsLeaderPlusFiveAndSixthIsRejected()
    {
        Assert.That(state.maxActiveSquad,Is.EqualTo(5));for(int i=0;i<5;i++)Assert.That(leader.RegisterSquadBuddy(Buddy()),Is.True);Assert.That(leader.RegisterSquadBuddy(Buddy()),Is.False);
        leader.IssueBuddyCommand(BuddyCommand.Stay);Assert.That(owned.FindAll(g=>g!=null&&g.GetComponent<BuddyBrain>()?.SquadLeader==leader).Count,Is.EqualTo(5));
    }
    [Test]public void RemovedDisabledOrReboundRecipientLosesPermissionWithoutRosterRepair()
    {
        var unit=Buddy();leader.RegisterSquadBuddy(unit);var brain=unit.GetComponent<BuddyBrain>();unit.enabled=false;Assert.That(brain.SquadAuthorized,Is.False);unit.enabled=true;
        state.activeSquadIds.Remove(unit.unitData.uniqueId);Assert.That(brain.CombatPermitted,Is.False);state.activeSquadIds.Add(unit.unitData.uniqueId);
        unit.GetComponent<ActorTerrainBinding>().Bind(null);Assert.That(brain.SquadAuthorized,Is.False);
    }
    [Test]public void StayBoundsFinalMotionAndAllowsInwardRecoveryOnly()
    {
        var unit=Buddy();leader.RegisterSquadBuddy(unit);leader.IssueBuddyCommand(BuddyCommand.Stay);var brain=unit.GetComponent<BuddyBrain>();float radius=brain.StayRadius;
        Vector2 atEdge=brain.StayAnchor+Vector2.right*(radius-.01f);var velocity=brain.BoundVelocity(atEdge,Vector2.right*10,.02f);Assert.That(Vector2.Distance(atEdge+velocity*.02f,brain.StayAnchor),Is.LessThanOrEqualTo(radius+.001f));
        var outside=brain.StayAnchor+Vector2.right*(radius+1);Assert.That(brain.BoundVelocity(outside,Vector2.right,.02f),Is.EqualTo(Vector2.zero));Assert.That(brain.BoundVelocity(outside,Vector2.left,.02f).x,Is.LessThan(0));
        Assert.That(brain.AllowsDestination(brain.StayAnchor+Vector2.right*(radius+.1f)),Is.False);
    }
    [Test]public void StayEnvelopeUsesActualLargeColliderClearance()
    {var unit=Buddy();var collider=unit.gameObject.AddComponent<BoxCollider2D>();collider.size=new Vector2(3,3);Physics2D.SyncTransforms();leader.RegisterSquadBuddy(unit);float actualDiameter=Mathf.Max(collider.bounds.extents.x,collider.bounds.extents.y)*2;Assert.That(actualDiameter,Is.GreaterThan(1.8f));Assert.That(unit.GetComponent<BuddyBrain>().StayRadius,Is.GreaterThanOrEqualTo(actualDiameter));}
    [Test]public void CommandDispatchLeavesUnregisteredCampStyleWorkerUntouched()
    {
        var worker=Buddy(false);var brain=worker.GetComponent<BuddyBrain>();brain.enabled=false;worker.GetComponent<BuddyFollow>().enabled=false;worker.GetComponent<BuddyCombat>().enabled=false;
        leader.IssueBuddyCommand(BuddyCommand.Passive);leader.IssueBuddyCommand(BuddyCommand.Stay);Assert.That(brain.SquadScoped,Is.False);Assert.That(brain.enabled,Is.False);Assert.That(worker.GetComponent<BuddyFollow>().enabled,Is.False);Assert.That(worker.GetComponent<BuddyCombat>().enabled,Is.False);
    }
    [Test]public void NewRegisteredRecruitImmediatelyReceivesCurrentModes()
    {leader.IssueBuddyCommand(BuddyCommand.Stay);leader.IssueBuddyCommand(BuddyCommand.Passive);var recruit=Buddy();Assert.That(leader.RegisterSquadBuddy(recruit),Is.True);Assert.That(recruit.GetComponent<BuddyBrain>().Following,Is.False);Assert.That(recruit.GetComponent<BuddyBrain>().CombatPermitted,Is.False);}
}
#endif
