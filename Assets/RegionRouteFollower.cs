using System.Collections.Generic;
using UnityEngine;
/// <summary>Waypoints for an existing movement duty. Does not choose goals or own terrain/AI state.</summary>
public sealed class RegionRouteFollower
{
 List<Vector2> path;int cursor,revision=-1;Vector2 goal;bool requested;
 public Vector2 CurrentWaypoint{get;private set;}
 public int Publication {get;private set;}
 public float PublishedDistance {get;private set;}
 public bool Direction(ActorTerrainBinding binding,Vector2 position,Vector2 destination,out Vector2 direction,float travelPerStep=0)
 {
  direction=Vector2.zero;if(binding==null){direction=(destination-position).normalized;return true;}
  if(binding.Ready!=null&&!binding.Ready())return false;
  // Terrain-clear movement does not need a grid route or shared intermediate anchors.
  // Physical actors still collide; ActorMotionIntent refines AI movement afterwards.
  if(binding.Sight(position,destination))
  {
   if(requested)binding.CancelMovementNavigation();requested=false;path=null;cursor=0;goal=destination;
   CurrentWaypoint=destination;var direct=destination-position;direction=direct.normalized;if(travelPerStep>0)direction*=Mathf.Clamp01(direct.magnitude/travelPerStep);return true;
  }
  if(path==null||!binding.AcceptsMovementRoute(revision)||(destination-goal).sqrMagnitude>.0025f)
  {goal=destination;requested=true;if(!binding.TryPath(position,destination,out path)){path=null;return false;}revision=binding.LastRouteRevision;cursor=0;PublishedDistance=binding.LastRouteDistance;Publication++;}
  cursor=RouteWaypointPolicy.Advance(path,cursor,position,binding,binding.MinimumBodyRadius);
  float tolerance=binding.RequestRoute!=null?binding.Terrain.CellSize*.12f:Mathf.Sqrt(.018f);
  while(cursor<path.Count&&(path[cursor]-position).sqrMagnitude<tolerance*tolerance)cursor++;
  CurrentWaypoint=cursor<path.Count?path[cursor]:destination;var offset=CurrentWaypoint-position;direction=offset.normalized;if(travelPerStep>0)direction*=Mathf.Clamp01(offset.magnitude/travelPerStep);return true;
 }
 public void Reset(){path=null;cursor=0;revision=-1;requested=false;Publication=0;PublishedDistance=0;}
}
