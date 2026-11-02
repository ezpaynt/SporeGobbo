using System.Collections.Generic;
using UnityEngine;
using System.Runtime.CompilerServices;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;using System.IO;using SporeGobbo.Safety;
#endif
// Observer-only storage. No searches, cursor writes, task calls or motion publication.
public static class Stage64DRouteObservation
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
 struct Event {public string stage,key;public float time;public double tick;public int queue,request,path,revision,terrainRevision,cursor,start,count;public Vector2 position,goal,extents;}
 static Event[] events;static Vector2[] nodes;static int count,nodeCount,dropped,nodeDropped;static bool active;
 public static void Begin(){if(!Persistence.Isolated)throw new InvalidOperationException("Route observer isolation");events=new Event[30000];nodes=new Vector2[300000];count=nodeCount=dropped=nodeDropped=0;active=true;}
 public static int Id(object value)=>value==null?0:RuntimeHelpers.GetHashCode(value);
#else
 public static int Id(object value)=>0;
#endif
 [System.Diagnostics.Conditional("UNITY_EDITOR"),System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
 public static void Trace(string stage,ActorTerrainBinding actor,int queue,int request,Vector2 position,Vector2 goal,Vector2 extents,int revision,IReadOnlyList<Vector2> path,int cursor=-1)
 {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
  if(!active||actor==null)return;var unit=actor.GetComponent<BuddyUnit>();string key=unit?.unitData?.uniqueId;
  if(key!="development-b-buddy-1"&&key!="development-b-buddy-2"&&key!="development-b-buddy-4"&&key!="development-b-buddy-5")return;
  if(count>=events.Length){dropped++;return;}int n=path?.Count??0;if(nodeCount+n>nodes.Length){nodeDropped+=n;dropped++;return;}
  events[count++]=new Event{stage=stage,key=key,time=Time.realtimeSinceStartup,tick=Time.fixedTimeAsDouble,queue=queue,request=request,path=Id(path),revision=revision,terrainRevision=actor.Revision,cursor=cursor,start=nodeCount,count=n,position=position,goal=goal,extents=extents};
  for(int i=0;i<n;i++)nodes[nodeCount++]=path[i];
#endif
 }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
 public static void Export(string directory)
 {
  if(!active)return;
  foreach(var follow in UnityEngine.Object.FindObjectsByType<BuddyFollow>(FindObjectsSortMode.None)){var a=follow.GetComponent<ActorTerrainBinding>();if(a!=null)Trace("FinalFollower",a,0,0,follow.GetComponent<Rigidbody2D>().position,a.CurrentMovementGoal,TileMover.GetMapClearanceExtents(follow.GetComponent<Rigidbody2D>(),a.MinimumBodyRadius),a.LastRouteRevision,follow.ObservedPath,follow.ObservedCursor);}
  active=false;
  using(var f=new StreamWriter(Path.Combine(directory,"route-events.csv"))){f.WriteLine("event,time,tick,stage,key,queue,request,path,revision,terrainRevision,cursor,nodeStart,nodeCount,x,y,goalX,goalY,halfX,halfY");for(int i=0;i<count;i++){var r=events[i];f.WriteLine(FormattableString.Invariant($"{i},{r.time:R},{r.tick:R},{r.stage},{r.key},{r.queue},{r.request},{r.path},{r.revision},{r.terrainRevision},{r.cursor},{r.start},{r.count},{r.position.x:R},{r.position.y:R},{r.goal.x:R},{r.goal.y:R},{r.extents.x:R},{r.extents.y:R}"));}}
  using(var f=new StreamWriter(Path.Combine(directory,"route-nodes.csv"))){f.WriteLine("index,x,y");for(int i=0;i<nodeCount;i++)f.WriteLine(FormattableString.Invariant($"{i},{nodes[i].x:R},{nodes[i].y:R}"));}
  bool auditComplete=true;int audits=0;
  using(var f=new StreamWriter(Path.Combine(directory,"route-forward-joins.csv")))
  {f.WriteLine("key,cursor,index,x,y,revision,terrainRevision,terrainClear,boundsClear,fullConnectorPeersClear,stepTerrainClear,stepBoundsClear,stepPeersClear,blockedPeers,stepBlockedPeers");
   foreach(var follow in UnityEngine.Object.FindObjectsByType<BuddyFollow>(FindObjectsSortMode.None))
   {
    var a=follow.GetComponent<ActorTerrainBinding>();var u=follow.GetComponent<BuddyUnit>();if(a?.Movement==null||u?.unitData?.uniqueId!="development-b-buddy-2")continue;
    var path=follow.ObservedPath;int cursor=follow.ObservedCursor,revision=a.Revision;audits++;
    if(path.Count==0||path.Count>128){auditComplete=false;continue;}
    for(int i=Mathf.Max(0,cursor);i<path.Count;i++)
    {
     var end=path[i];a.Movement.ObserveJoin(a,end,false,out bool tc,out bool bc,out string peers);
     a.Movement.ObserveJoin(a,end,true,out bool st,out bool sb,out string sp);
     f.WriteLine(FormattableString.Invariant($"{u.unitData.uniqueId},{cursor},{i},{end.x:R},{end.y:R},{a.LastRouteRevision},{revision},{tc},{bc},{peers.Length==0},{st},{sb},{sp.Length==0},{peers},{sp}"));
    }
    if(revision!=a.Revision)auditComplete=false;
   }
  }
  var region=UnityEngine.Object.FindAnyObjectByType<ExpeditionRegion>();
  using(var f=new StreamWriter(Path.Combine(directory,"route-final-actors.csv"))){f.WriteLine("actor,key,x,y,halfX,halfY,vx,vy,ready,revision");foreach(var a in UnityEngine.Object.FindObjectsByType<ActorTerrainBinding>(FindObjectsSortMode.None)){var body=a.GetComponent<Rigidbody2D>();if(body==null)continue;var e=TileMover.GetMapClearanceExtents(body,a.MinimumBodyRadius);f.WriteLine(FormattableString.Invariant($"{a.GetInstanceID()},{a.GetComponent<BuddyUnit>()?.unitData?.uniqueId??a.name},{body.position.x:R},{body.position.y:R},{e.x:R},{e.y:R},{body.linearVelocity.x:R},{body.linearVelocity.y:R},{a.Ready==null||a.Ready()},{a.Revision}"));}}
  if(region!=null){using(var f=new StreamWriter(Path.Combine(directory,"route-terrain.csv"))){f.WriteLine("x,y,solid");for(int y=0;y<128;y++)for(int x=0;x<144;x++)f.WriteLine($"{x},{y},{region.State.World.IsSolid(x,y)}");}}
  File.WriteAllText(Path.Combine(directory,"route-observer-status.txt"),$"events={count}\nnodes={nodeCount}\ndropped={dropped}\nnodeDropped={nodeDropped}\nauditComplete={auditComplete}\nauditActors={audits}\nready={region?.LogicalReady}\nrevision={region?.Revision}\ncontactOffset={Physics2D.defaultContactOffset:R}\nObserver only. Final connector audit uses frozen peers; not a replacement route or a simultaneous maneuver. No performance claim.\n");
  events=null;nodes=null;
 }
#endif
}
