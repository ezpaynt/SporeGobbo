#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using SporeGobbo.Safety;

// Owned diagnostic reads only. Never creates destinations, requests paths, invokes
// AI decisions, or writes gameplay. Reflection reads fields, not behavior methods.
public sealed class Stage64DReplayEvidence : MonoBehaviour
{
    static Stage64DReplayEvidence combat,buddy;
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static readonly Dictionary<string,FieldInfo> enemyFields=new Dictionary<string,FieldInfo>();
    static T EnemyField<T>(TunnelWeevilEnemy e,string name)
    {if(!enemyFields.TryGetValue(name,out var field)){field=typeof(TunnelWeevilEnemy).GetField(name,Private);enemyFields.Add(name,field);}return (T)field.GetValue(e);}
    [Serializable]struct BuddyRow
    {
        public double time,tick;public int frame,test,stage,revision,cursor,nodes,routeRevision;public string status,task;
        public Vector2 position,leader,destination,waypoint,half,requested,velocity;
        public float gap,destinationDistance,tolerance,stopDistance;public bool pointDestination,reached,ready,occupy,goalConnector,bodyClear,crossedMiddle,usable,leaderFinished;
    }
    [Serializable]struct CombatRow
    {
        public double time,tick;public int frame,id,target,health,leaderHealth,attempts,attemptedDamage,contacts,pathCursor,pathNodes,revision;public string name,state;
        public Vector2 position,leader,velocity,half,targetPosition,closestTarget,goal,waypoint;
        public float centerDistance,closestDistance,biteReach,noticeRange,cooldown,stateTimer,noAttackTime;public bool god,protects,ready,actions,sight,occupy,inBiteRange;
    }
    readonly BuddyRow[] buddies=new BuddyRow[4000];readonly CombatRow[] enemies=new CombatRow[1600];
    readonly ContactPoint2D[] contacts=new ContactPoint2D[32];
    int buddyCount,combatCount,buddyDropped,combatDropped,contactSaturated,actorDropped;float nextSample;bool stopped;
    ExpeditionRegion region;GobboController leader;BuddyUnit unit;BuddyFollow follow;int test;Vector2 destination;float tolerance;bool pointDestination;
    static void Guard()
    {if(!Persistence.Isolated||!V1RuntimeProfile.DevelopmentStorage||!V1RuntimeProfile.DevelopmentToolsEnabled||Environment.GetEnvironmentVariable("SPORE_STAGE61_CAPTURE")!="lifecycle")throw new InvalidOperationException("Owned lifecycle replay diagnostics only");}
    public static void BeginCombat(GameObject owner,ExpeditionRegion region,GobboController leader)
    {Guard();if(combat!=null)throw new InvalidOperationException("Duplicate combat observer");combat=owner.AddComponent<Stage64DReplayEvidence>();combat.region=region;combat.leader=leader;combat.SampleCombat();}
    public static void EndCombat(string directory)
    {if(combat==null)return;combat.SampleCombat();combat.Export(directory,"combat-replay");combat=null;}
    public static void BeginBuddy(GameObject owner,ExpeditionRegion region,GobboController leader,BuddyUnit unit)
    {Guard();if(buddy!=null)throw new InvalidOperationException("Duplicate Buddy observer");buddy=owner.AddComponent<Stage64DReplayEvidence>();buddy.region=region;buddy.leader=leader;buddy.unit=unit;buddy.follow=unit.GetComponent<BuddyFollow>();}
    public static void SetCase(int value){if(buddy!=null)buddy.test=value;}
    public static void FollowDecision(BuddyFollow f,Vector2 target,float reach,bool point)
    {
        var a=buddy;if(a==null||a.stopped||a.follow!=f)return;a.destination=target;a.tolerance=reach;a.pointDestination=point;
        if(Time.realtimeSinceStartup<a.nextSample)return;a.nextSample=Time.realtimeSinceStartup+.1f;a.SampleBuddy(0,false,false,false,false);
    }
    public static void Passage(int test,string stage,bool finished,bool bodyClear,bool crossed,bool usable)
    {if(buddy==null)return;buddy.test=test;buddy.SampleBuddy(stage=="Leader"?1:stage=="Clearance"?2:3,finished,bodyClear,crossed,usable);}
    public static void EndBuddy(string directory){if(buddy==null)return;buddy.Export(directory,"buddy-replay");buddy=null;}
    void SampleBuddy(int stage,bool finished,bool clear,bool crossed,bool usable)
    {
        if(stopped||unit==null||leader==null)return;if(buddyCount>=buddies.Length){buddyDropped++;return;}
        var body=unit.GetComponent<Rigidbody2D>();var binding=unit.GetComponent<ActorTerrainBinding>();var motion=unit.GetComponent<ActorMotionIntent>();var brain=unit.GetComponent<BuddyBrain>();var half=TileMover.GetMapClearanceExtents(body,follow.physicalBodyRadius);
        buddies[buddyCount++]=new BuddyRow{time=Time.realtimeSinceStartupAsDouble,tick=Time.fixedTimeAsDouble,frame=Time.frameCount,test=test,stage=stage,position=body.position,leader=leader.GetComponent<Rigidbody2D>().position,
            destination=destination,waypoint=follow.RouteWaypoint,half=half,requested=motion!=null?motion.RequestedVelocity:Vector2.zero,velocity=body.linearVelocity,gap=Vector2.Distance(body.position,leader.GetComponent<Rigidbody2D>().position),
            destinationDistance=Vector2.Distance(body.position,destination),tolerance=tolerance,stopDistance=follow.stopDistance,pointDestination=pointDestination,reached=Vector2.Distance(body.position,destination)<=tolerance,
            ready=region.LogicalReady,occupy=TileMover.CanOccupyBox(region,body.position,half),goalConnector=TileMover.CanTraverseBox(region,body.position,destination,half),revision=region.Revision,cursor=follow.ObservedCursor,nodes=follow.RouteNodeCount,
            status=binding.RouteStatus.ToString(),routeRevision=binding.LastRouteRevision,task=brain!=null?brain.currentTask:"None",bodyClear=clear,crossedMiddle=crossed,usable=usable,leaderFinished=finished};
    }
    void Update(){if(this!=combat||stopped||Time.realtimeSinceStartup<nextSample)return;nextSample=Time.realtimeSinceStartup+.1f;SampleCombat();}
    void SampleCombat()
    {
        if(stopped||leader==null||region==null)return;
        var found=UnityEngine.Object.FindObjectsByType<TunnelWeevilEnemy>(FindObjectsSortMode.None);int index=0;
        foreach(var enemy in found)
        {
            if(index++>=16){actorDropped++;continue;}if(combatCount>=enemies.Length){combatDropped++;continue;}
            var body=enemy.GetComponent<Rigidbody2D>();var binding=enemy.GetComponent<ActorTerrainBinding>();var target=EnemyField<Transform>(enemy,"currentTarget");var path=EnemyField<List<Vector2>>(enemy,"currentPath");int cursor=EnemyField<int>(enemy,"pathIndex");
            Vector2 closest=target!=null?(Vector2)target.position:body.position;float best=float.PositiveInfinity;
            if(target!=null)foreach(var c in target.GetComponentsInChildren<Collider2D>())if(c!=null&&c.enabled&&c.gameObject.activeInHierarchy){var p=c.ClosestPoint(body.position);float distance=Vector2.Distance(p,body.position);if(distance<best){best=distance;closest=p;}}
            int count=body.GetContacts(contacts);if(count==contacts.Length)contactSaturated++;var half=TileMover.GetMapClearanceExtents(body,enemy.bodyRadius);
            enemies[combatCount++]=new CombatRow{time=Time.realtimeSinceStartupAsDouble,tick=Time.fixedTimeAsDouble,frame=Time.frameCount,id=enemy.GetInstanceID(),name=enemy.name,target=target!=null?target.GetInstanceID():0,state=EnemyField<object>(enemy,"combatState").ToString(),
                position=body.position,leader=leader.GetComponent<Rigidbody2D>().position,velocity=body.linearVelocity,half=half,targetPosition=target!=null?(Vector2)target.position:Vector2.zero,closestTarget=closest,
                goal=binding!=null?binding.CurrentMovementGoal:Vector2.zero,waypoint=cursor>=0&&cursor<path.Count?path[cursor]:body.position,pathCursor=cursor,pathNodes=path.Count,revision=region.Revision,
                health=enemy.health,leaderHealth=leader.health,attempts=ExpeditionDevelopmentControls.DamageAttempts,attemptedDamage=ExpeditionDevelopmentControls.AttemptedDamage,god=ExpeditionDevelopmentControls.GodMode,protects=ExpeditionDevelopmentControls.Protects(leader),
                ready=EnemyDamage.Ready(enemy.gameObject),actions=CombatSessionBinding.AllowsActions(enemy.gameObject),sight=target!=null&&RegionQueries.Sight(enemy.gameObject,body.position,target.position),occupy=TileMover.CanOccupyBox(region,body.position,half),
                centerDistance=Vector2.Distance(body.position,leader.GetComponent<Rigidbody2D>().position),closestDistance=Vector2.Distance(body.position,closest),biteReach=enemy.attackRange+enemy.attackContactPadding,noticeRange=enemy.noticeRange,
                inBiteRange=target!=null&&Vector2.Distance(body.position,closest)<=enemy.attackRange+enemy.attackContactPadding,cooldown=EnemyField<float>(enemy,"attackCooldownTimer"),stateTimer=EnemyField<float>(enemy,"stateTimer"),noAttackTime=EnemyField<float>(enemy,"timeSinceSuccessfulAttack"),contacts=count};
        }
    }
    void Export(string directory,string prefix)
    {
        stopped=true;
        using(var f=new StreamWriter(Path.Combine(directory,prefix+".jsonl"))){if(prefix=="combat-replay")for(int i=0;i<combatCount;i++)f.WriteLine(JsonUtility.ToJson(enemies[i]));else for(int i=0;i<buddyCount;i++)f.WriteLine(JsonUtility.ToJson(buddies[i]));}
        File.WriteAllText(Path.Combine(directory,prefix+"-status.txt"),$"buddyRows={buddyCount}/4000 dropped={buddyDropped}\ncombatRows={combatCount}/1600 dropped={combatDropped}\ncontactSaturated={contactSaturated} actorDropped={actorDropped}; maxEnemiesPerSample=16\n10Hz reads; forced samples at original assertions; no writes to bodies, AI, health, goals, routes, tasks or terrain; no path requests or AI method calls. Buddy stages 0=FollowDecision,1=LeaderAssertion,2=RouteClearanceAssertion,3=BuddyAssertion.\n");
    }
    void OnDestroy(){if(combat==this)combat=null;if(buddy==this)buddy=null;}
}
#endif
