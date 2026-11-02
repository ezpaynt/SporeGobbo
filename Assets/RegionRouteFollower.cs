using System.Collections.Generic;
using UnityEngine;
/// <summary>Waypoints for an existing movement duty. Does not choose goals or own terrain/AI state.</summary>
public sealed class RegionRouteFollower
{
 List<Vector2> path;int cursor,revision=-1;Vector2 goal;bool requested;
 readonly RouteWaypointPolicy.RejoinState rejoin=new RouteWaypointPolicy.RejoinState();
 public Vector2 CurrentWaypoint{get;private set;}
 public int Publication {get;private set;}
 public float PublishedDistance {get;private set;}
 public bool Direction(ActorTerrainBinding binding,Vector2 position,Vector2 destination,out Vector2 direction,float travelPerStep=0)
 {
  direction=Vector2.zero;if(binding==null){direction=(destination-position).normalized;return true;}
  if(binding.Ready!=null&&!binding.Ready())return false;
  if(path!=null&&binding.AcceptsMovementRoute(revision))cursor=RouteWaypointPolicy.Rejoin(path,cursor,position,binding,binding.MinimumBodyRadius,rejoin);
  if(binding.Movement?.WaitingForPeer(binding,destination)==true)return true;
  // Terrain-clear movement does not need a grid route or shared intermediate anchors.
  // Physical actors still collide; ActorMotionIntent refines AI movement afterwards.
  if(binding.Sight(position,destination))
  {
   if(requested)binding.CancelMovementNavigation();requested=false;path=null;cursor=0;goal=destination;
   CurrentWaypoint=destination;var direct=destination-position;direction=direct.normalized;if(travelPerStep>0)direction*=Mathf.Clamp01(direct.magnitude/travelPerStep);return true;
  }
  if(path==null||!binding.AcceptsMovementRoute(revision)||(destination-goal).sqrMagnitude>.0025f)
  {bool newDuty=Publication==0||(destination-goal).sqrMagnitude>.0025f;goal=destination;requested=true;if(!binding.TryPath(position,destination,out path)){path=null;return false;}revision=binding.LastRouteRevision;cursor=0;PublishedDistance=binding.LastRouteDistance;if(newDuty)Publication++;}
  cursor=RouteWaypointPolicy.Rejoin(path,cursor,position,binding,binding.MinimumBodyRadius,rejoin);
  cursor=RouteWaypointPolicy.Advance(path,cursor,position,binding,binding.MinimumBodyRadius);
  // Same duty and deadline: recovery does not create a new activity-budget publication.
  if(!RouteWaypointPolicy.RecoverTerrainEntry(path,ref cursor,ref revision,position,destination,binding,binding.MinimumBodyRadius,rejoin))return false;
  float tolerance=binding.RequestRoute!=null?binding.Terrain.CellSize*.12f:Mathf.Sqrt(.018f);
  // Advance owns regional completion; a recovered entry is retained until its next checked advance.
  if(binding.RequestRoute==null)
   while(cursor<path.Count&&(path[cursor]-position).sqrMagnitude<tolerance*tolerance&&RouteWaypointPolicy.Reached(path,cursor,position,binding,binding.MinimumBodyRadius,tolerance))cursor++;
  CurrentWaypoint=cursor<path.Count?path[cursor]:destination;var offset=CurrentWaypoint-position;direction=offset.normalized;if(travelPerStep>0)direction*=Mathf.Clamp01(offset.magnitude/travelPerStep);return true;
 }
 public void Reset(){path=null;cursor=0;revision=-1;requested=false;Publication=0;PublishedDistance=0;rejoin.Reset();}
}
