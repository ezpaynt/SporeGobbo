using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using SporeGobbo.Geometry;

/// <summary>Region-owned navigation work, not a terrain or actor authority.</summary>
public sealed class RegionRouteQueue : IDisposable
{
    sealed class Request
    {
        public ActorTerrainBinding Owner;
        public int OwnerId;
        public Vector2 Start, Goal, Extents;
        public GridRouteSearch Search;
        public GridRouteStatus Status;
        public List<Vector2> Path;
        public readonly HashSet<int> Corridor = new HashSet<int>();
        public int Revision;
        public int Published;
        public Vector2 Prior;
        public float RetryAt;
    }
    readonly RegionTerrainState state;
    readonly IDiggableTerrain terrain;
    readonly Func<Vector2,Point2> logical;
    readonly Func<Point2,Vector2> world;
    readonly Dictionary<int,Request> owners = new Dictionary<int,Request>();
    readonly List<Request> pending = new List<Request>();
    bool disposed;
    public long Requests { get; private set; }
    public long Coalesced { get; private set; }
    public long Work { get; private set; }
    public long Completed { get; private set; }
    public int PendingCount => pending.Count;
    public RegionRouteQueue(RegionTerrainState state,IDiggableTerrain terrain,Func<Vector2,Point2> logical,Func<Point2,Vector2> world)
    {this.state=state;this.terrain=terrain;this.logical=logical;this.world=world;state.Committed+=Changed;}

    public GridRouteStatus RequestPath(ActorTerrainBinding owner,Vector2 from,Vector2 goal,Vector2 extents,out List<Vector2> path)
    {
        path=null;if(disposed||owner==null)return GridRouteStatus.Canceled;
        Requests++;int id=owner.GetInstanceID();
        if(owners.TryGetValue(id,out var r))
        {
            bool same=(r.Goal-goal).sqrMagnitude<=.0025f&&(r.Extents-extents).sqrMagnitude<.000001f;
            if(same&&r.Status==GridRouteStatus.Succeeded)
            {Coalesced++;path=Remaining(r.Path,from,extents);return path.Count>0?GridRouteStatus.Succeeded:Restart(owner,from,goal,extents,out path);}
            if(same&&r.Status==GridRouteStatus.Pending){Coalesced++;return r.Status;}
            // Terrain-only failure is still valid until topology, clearance, goal or start changes.
            if(same&&r.Status==GridRouteStatus.Unreachable&&(Time.unscaledTime<r.RetryAt||(r.Start-from).sqrMagnitude<.09f)){Coalesced++;return r.Status;}
            Cancel(owner);
        }
        return Restart(owner,from,goal,extents,out path);
    }
    GridRouteStatus Restart(ActorTerrainBinding owner,Vector2 from,Vector2 goal,Vector2 extents,out List<Vector2> path)
    {
        path=null;float cell=terrain.CellSize;
        var search=new GridRouteSearch(state,logical(from),logical(goal),extents.x/cell,extents.y/cell);
        var r=new Request{Owner=owner,OwnerId=owner.GetInstanceID(),Start=from,Goal=goal,Extents=extents,Search=search,Status=search.Status,Revision=state.Revision,RetryAt=Time.unscaledTime+.5f};
        owners[owner.GetInstanceID()]=r;if(r.Status==GridRouteStatus.Pending)pending.Add(r);return r.Status;
    }
    List<Vector2> Remaining(List<Vector2> full,Vector2 from,Vector2 extents)
    {
        int best=-1;float distance=float.PositiveInfinity;
        for(int i=0;i<full.Count;i++)
        {
            float d=(full[i]-from).sqrMagnitude;
            if(d<distance){best=i;distance=d;}
        }
        var result=new List<Vector2>();if(best<0||!TileMover.CanTraverseBox(terrain,from,full[best],extents))return result;
        for(int i=best;i<full.Count;i++)result.Add(full[i]);return result;
    }
    public bool Accepts(ActorTerrainBinding owner,int revision)
    =>!disposed&&owner!=null&&owners.TryGetValue(owner.GetInstanceID(),out var r)&&r.Revision==revision&&r.Status==GridRouteStatus.Succeeded;
    public void Cancel(ActorTerrainBinding owner)
    {
        if(owner==null||!owners.TryGetValue(owner.GetInstanceID(),out var r))return;
        r.Search.Cancel();r.Status=GridRouteStatus.Canceled;pending.Remove(r);owners.Remove(owner.GetInstanceID());
    }
    public int RouteRevision(ActorTerrainBinding owner)=>owner!=null&&owners.TryGetValue(owner.GetInstanceID(),out var r)?r.Revision:-1;

    /// <summary>One region budget per rendered frame. Slice size bounds late budget checks.</summary>
    public void Advance(int budget=512,double milliseconds=1.5)
    {
        if(disposed||pending.Count==0)return;
        using var sample=new Unity.Profiling.ProfilerMarker("Spore.Navigation.Queue").Auto();
        var clock=Stopwatch.StartNew();int used=0;
        while(pending.Count>0&&used<budget&&clock.Elapsed.TotalMilliseconds<milliseconds)
        {
            var r=pending[0];pending.RemoveAt(0);
            if(r.Owner==null||!r.Owner.isActiveAndEnabled){r.Search.Cancel();r.Status=GridRouteStatus.Canceled;owners.Remove(r.OwnerId);continue;}
            int slice=Math.Min(32,budget-used);long before=r.Search.Work;
            if(r.Search.Status==GridRouteStatus.Pending)
            {
                r.Status=r.Search.Step(slice);int work=(int)(r.Search.Work-before);used+=Math.Max(1,work);Work+=work;
                if(r.Status==GridRouteStatus.Succeeded){r.Status=GridRouteStatus.Pending;pending.Add(r);continue;}
            }
            else used+=slice;
            if(r.Search.Status==GridRouteStatus.Succeeded)
            {
                if(r.Path==null){r.Path=new List<Vector2>();r.Prior=r.Owner.GetComponent<Rigidbody2D>().position;}
                int end=Math.Min(r.Search.Path.Count,r.Published+slice);r.Status=GridRouteStatus.Pending;
                while(r.Published<end)
                {
                    Vector2 next=world(r.Search.Path[r.Published++]);
                    if(!TileMover.CanTraverseBox(terrain,r.Prior,next,r.Extents)){r.Status=GridRouteStatus.Unreachable;break;}
                    AddCorridor(r,r.Prior,next);r.Path.Add(next);r.Prior=next;
                }
                if(r.Status==GridRouteStatus.Pending&&r.Published==r.Search.Path.Count)
                {
                    if(TileMover.CanTraverseBox(terrain,r.Prior,r.Goal,r.Extents))
                    {AddCorridor(r,r.Prior,r.Goal);r.Path.Add(r.Goal);r.Status=GridRouteStatus.Succeeded;Completed++;}
                    else r.Status=GridRouteStatus.Unreachable;
                }
            }
            if(r.Status==GridRouteStatus.Pending){pending.Add(r);continue;}
            r.RetryAt=Time.unscaledTime+.5f;
        }
    }
    void AddCorridor(Request r,Vector2 from,Vector2 to)
    {
            Point2 prior=logical(from),p=logical(to);double hx=r.Extents.x/terrain.CellSize,hy=r.Extents.y/terrain.CellSize;
            for(int y=(int)Math.Floor(Math.Min(prior.Y,p.Y)-hy);y<=(int)Math.Floor(Math.Max(prior.Y,p.Y)+hy);y++)
                for(int x=(int)Math.Floor(Math.Min(prior.X,p.X)-hx);x<=(int)Math.Floor(Math.Max(prior.X,p.X)+hx);x++)
                    if(state.World.Contains(x,y))r.Corridor.Add(y*state.World.Width+x);
    }
    void Changed(IReadOnlyList<DualTile> cells,int revision)
    {
        foreach(var r in owners.Values)
        {
            if(r.Status==GridRouteStatus.Pending){r.Search.Cancel();r.Status=GridRouteStatus.Canceled;pending.Remove(r);}
            else if(r.Status==GridRouteStatus.Unreachable){r.Status=GridRouteStatus.Canceled;r.RetryAt=0;}
            else if(r.Status==GridRouteStatus.Succeeded)
                foreach(var c in cells)if(state.World.IsSolid(c.X,c.Y)&&r.Corridor.Contains(c.Y*state.World.Width+c.X)){r.Status=GridRouteStatus.Canceled;break;}
        }
    }
    public void Dispose(){if(disposed)return;disposed=true;state.Committed-=Changed;foreach(var r in owners.Values)r.Search.Cancel();owners.Clear();pending.Clear();}
}

