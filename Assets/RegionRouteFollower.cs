using System.Collections.Generic;
using UnityEngine;
/// <summary>Waypoints for an existing movement duty. Does not choose goals or own terrain/AI state.</summary>
public sealed class RegionRouteFollower
{
 List<Vector2> path;int cursor,revision=-1;Vector2 goal;
 public Vector2 CurrentWaypoint{get;private set;}
 public bool Direction(ActorTerrainBinding binding,Vector2 position,Vector2 destination,out Vector2 direction)
 {
  direction=Vector2.zero;if(binding==null){direction=(destination-position).normalized;return true;}
  if(binding.Ready!=null&&!binding.Ready())return false;
  if(path==null||!binding.AcceptsRoute(revision)||(destination-goal).sqrMagnitude>.0025f)
  {goal=destination;if(!binding.TryPath(position,destination,out path)){path=null;return false;}revision=binding.LastRouteRevision;cursor=0;}
  while(cursor<path.Count&&(path[cursor]-position).sqrMagnitude<.018f)cursor++;
  CurrentWaypoint=cursor<path.Count?path[cursor]:destination;direction=(CurrentWaypoint-position).normalized;return true;
 }
 public void Reset(){path=null;cursor=0;revision=-1;}
}
