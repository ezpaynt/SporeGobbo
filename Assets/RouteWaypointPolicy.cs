using System.Collections.Generic;
using UnityEngine;
/// <summary>Advance a bounded number of navigation nodes without walking back to a stale anchor.
/// Every shortcut uses the same full-body terrain sweep as physical movement.</summary>
public static class RouteWaypointPolicy
{
    // Consumer-owned derived cache, not another goal or movement authority.
    public sealed class RejoinState
    {
        internal IReadOnlyList<Vector2> Path;
        internal long Epoch=-1;
        internal int Revision=-1;
        internal Vector2 Extents;
        internal TileMover.ActorFootprint Footprint;
        public long ConnectionChecks {get;internal set;}
        public void Reset(){Path=null;Epoch=-1;Revision=-1;}
    }
    static readonly Unity.Profiling.ProfilerMarker rejoinProfile=new Unity.Profiling.ProfilerMarker("Spore.Navigation.RouteRejoin");
    public static int Rejoin(IReadOnlyList<Vector2> path,int cursor,Vector2 position,ActorTerrainBinding binding,float radius,RejoinState cache)
    {
        if(cache==null||binding?.Terrain==null||binding.RequestRoute==null||path==null||cursor<0||cursor>=path.Count||binding.Ready!=null&&!binding.Ready())return cursor;
        long epoch=0;if(binding.Movement!=null&&!binding.Movement.TryRouteProgressVersion(binding,out epoch))return cursor;
        var extents=TileMover.GetMapClearanceExtents(binding.GetComponent<Rigidbody2D>(),radius);
        var footprint=TileMover.TerrainFootprint(binding.GetComponent<Rigidbody2D>(),extents);
        if(ReferenceEquals(cache.Path,path)&&cache.Epoch==epoch&&cache.Revision==binding.Revision&&(cache.Extents-extents).sqrMagnitude<.000001f&&cache.Footprint.Same(footprint))return cursor;
        cache.Path=path;cache.Epoch=epoch;cache.Revision=binding.Revision;cache.Extents=extents;cache.Footprint=footprint;
        using var sample=rejoinProfile.Auto();
        // At most two later nodes. Every complete connector is checked, never just its first step.
        for(int next=Mathf.Min(path.Count-1,cursor+2);next>cursor;next--)
        {
            cache.ConnectionChecks++;
            bool clear=binding.Movement!=null?binding.Movement.RouteConnectionClear(binding,path[next]):TileMover.CanTraverseActor(binding.Terrain,binding.GetComponent<Rigidbody2D>(),position,path[next],radius);
            if(!clear)continue;
            binding.Movement?.RouteRejoined(binding);return next;
        }
        return cursor;
    }
    public static int Advance(IReadOnlyList<Vector2> path,int cursor,Vector2 position,ActorTerrainBinding binding,float radius)
    {
        if(binding==null||binding.RequestRoute==null||binding.Terrain==null)return cursor;
        var extents=TileMover.GetMapClearanceExtents(binding.GetComponent<Rigidbody2D>(),radius);
        float tolerance=binding.Terrain.CellSize*.12f;
        for(int n=0;n<2&&cursor<path.Count;n++)
        {
            if((path[cursor]-position).sqrMagnitude<=tolerance*tolerance)
            {
                if(!NextConnectionClear(path,cursor,position,binding,extents))break;
                cursor++;continue;
            }
            if(cursor+1<path.Count&&Vector2.Dot(position-path[cursor],path[cursor+1]-path[cursor])>=0&&TileMover.CanTraverseActor(binding.Terrain,binding.GetComponent<Rigidbody2D>(),position,path[cursor+1],binding.TerrainReserveRadius)){cursor++;continue;}
            break;
        }
        return cursor;
    }

    // Intermediate completion is a terrain decision; peers still belong to the coordinator.
    // Final route-node tolerance does not replace the owning activity's arrival contract.
    static bool NextConnectionClear(IReadOnlyList<Vector2> path,int cursor,Vector2 position,ActorTerrainBinding binding,Vector2 extents)
        =>cursor+1>=path.Count||binding?.Terrain==null||TileMover.CanTraverseActor(binding.Terrain,binding.GetComponent<Rigidbody2D>(),position,path[cursor+1],binding.TerrainReserveRadius);
    public static bool Reached(IReadOnlyList<Vector2> path,int cursor,Vector2 position,ActorTerrainBinding binding,float radius,float tolerance)
    {
        if(path==null||cursor<0||cursor>=path.Count||(path[cursor]-position).sqrMagnitude>tolerance*tolerance)return false;
        return NextConnectionClear(path,cursor,position,binding,TileMover.GetMapClearanceExtents(binding!=null?binding.GetComponent<Rigidbody2D>():null,radius));
    }

    /// <summary>A displaced body may no longer connect to its retained cursor even though
    /// the cached route itself is valid. Reuse the region's normal request/suffix operation.
    /// Only the existing terrain-rejection notification triggers work; peer waits do not.</summary>
    public static bool RecoverTerrainEntry(List<Vector2> path,ref int cursor,ref int revision,
        Vector2 position,Vector2 destination,ActorTerrainBinding binding,float radius,RejoinState cache=null)
    {
        if(binding?.RequestRoute==null||binding.Movement==null||path==null||cursor<0||cursor>=path.Count||
            binding.Ready!=null&&!binding.Ready()||
            binding.Movement.State(binding)!=MovementResolutionState.Blocked||
            binding.Movement.Reason(binding)!="TerrainOrRegionNotReady"||
            !binding.Movement.TryRouteProgressVersion(binding,out _))return true;
        using var sample=rejoinProfile.Auto();
        var extents=TileMover.GetMapClearanceExtents(binding.GetComponent<Rigidbody2D>(),radius);
        if(TileMover.CanTraverseActor(binding.Terrain,binding.GetComponent<Rigidbody2D>(),position,path[cursor],radius))return true;
        Stage64DRouteObservation.Trace("TerrainEntryRejected",binding,0,0,position,destination,extents,revision,path,cursor);
        bool ready=binding.TryPath(position,destination,out var replacement);
        if(!ReferenceEquals(path,replacement)){path.Clear();if(replacement!=null)path.AddRange(replacement);}
        cursor=0;revision=binding.LastRouteRevision;cache?.Reset();
        Stage64DRouteObservation.Trace("TerrainEntryDelivery",binding,0,0,position,destination,extents,revision,path,cursor);
        return ready&&path.Count>0;
    }
}
