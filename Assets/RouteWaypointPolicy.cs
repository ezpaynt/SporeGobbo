using System.Collections.Generic;
using UnityEngine;
/// <summary>Advance a bounded number of navigation nodes without walking back to a stale anchor.
/// Every shortcut uses the same full-body terrain sweep as physical movement.</summary>
public static class RouteWaypointPolicy
{
    public static int Advance(IReadOnlyList<Vector2> path,int cursor,Vector2 position,ActorTerrainBinding binding,float radius)
    {
        if(binding==null||binding.RequestRoute==null||binding.Terrain==null)return cursor;
        var extents=TileMover.GetMapClearanceExtents(binding.GetComponent<Rigidbody2D>(),radius);
        float tolerance=binding.Terrain.CellSize*.12f;
        for(int n=0;n<2&&cursor<path.Count;n++)
        {
            if((path[cursor]-position).sqrMagnitude<=tolerance*tolerance){cursor++;continue;}
            if(cursor+1<path.Count&&Vector2.Dot(position-path[cursor],path[cursor+1]-path[cursor])>=0&&TileMover.CanTraverseBox(binding.Terrain,position,path[cursor+1],extents)){cursor++;continue;}
            break;
        }
        return cursor;
    }
}
