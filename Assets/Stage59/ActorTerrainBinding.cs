using System;
using System.Collections.Generic;
using UnityEngine;
using SporeGobbo.Input;
using SporeGobbo.Geometry;

public readonly struct ActorRouteResult
{public readonly GridRouteStatus Status;public readonly List<Vector2> Path;public readonly int Revision;public readonly float Distance;public ActorRouteResult(GridRouteStatus status,List<Vector2> path,int revision,float distance=0){Status=status;Path=path;Revision=revision;Distance=distance;}}

/// <summary>Explicit per-actor dependency. No registry, singleton or production scene installation.</summary>
public sealed class ActorTerrainBinding : MonoBehaviour
{
    public IDiggableTerrain Terrain { get; private set; }
    public float MinimumBodyRadius;
    // Historical isolated actor proofs opt out; real regions retain campaign controls.
    public bool SuppressCampaignBootstrap = true;
    public Func<bool> Ready;
    public Func<int> ReadRevision;
    public Func<Vector2,Vector2,List<Vector2>> FindRoute;
    public Func<Vector2,Vector2,ActorRouteResult> RequestRoute;
    public Func<int,bool> AcceptCachedRoute;
    public Func<int,Vector2,Vector2,ActorRouteResult> RequestProbe;
    public Func<int,Vector2,Vector2,bool,Vector2> ChooseDestination;
    public Func<bool> HasMovementDestination;
    public bool MovementDestinationAvailable=>HasMovementDestination?.Invoke()??true;
    public GridRouteStatus ProbeStatus {get;private set;}=GridRouteStatus.Canceled;
    public Vector2 CurrentMovementGoal {get;private set;}
    public Vector2 MovementGoal(int key,Vector2 center,Vector2 targetExtents,bool contact=false){CurrentMovementGoal=ChooseDestination?.Invoke(key,center,targetExtents,contact)??center;return CurrentMovementGoal;}
    public bool TryProbe(Vector2 from,Vector2 goal,out List<Vector2> path)
    {
        if(RequestProbe==null)return TryPath(from,goal,out path);
        if(Terrain==null||Ready!=null&&!Ready()){path=new List<Vector2>();ProbeStatus=GridRouteStatus.Canceled;return false;}
        int key=Terrain.WorldToCell(goal).GetHashCode()|1;
        var answer=RequestProbe(key,from,goal);ProbeStatus=answer.Status;path=answer.Path??new List<Vector2>();return answer.Status==GridRouteStatus.Succeeded&&path.Count>0;
    }

    public Action CancelRoute;
    public Action CancelMovementRoute;
    public GridRouteStatus RouteStatus {get;private set;}=GridRouteStatus.Canceled;
    public float LastRouteDistance {get;private set;}
    public bool AcceptsMovementRoute(int revision)=>Terrain!=null&&(Ready==null||Ready())&&(AcceptCachedRoute?.Invoke(revision)??revision==Revision);
    public void CancelNavigation(){CancelRoute?.Invoke();RouteStatus=ProbeStatus=GridRouteStatus.Canceled;}
    public void CancelMovementNavigation(){CancelMovementRoute?.Invoke();RouteStatus=GridRouteStatus.Canceled;}

    public Func<Vector2,Vector2> PointerToWorld;
    public int DigAttempts {get;private set;}
    public void ObserveDig()=>DigAttempts++;
    public int Revision => ReadRevision?.Invoke() ?? -1;
    public int LastRouteRevision { get; private set; } = -1;
    public void Bind(IDiggableTerrain terrain) { Terrain=terrain; }
    public static IDiggableTerrain Resolve(GameObject actor)
    {
        var binding=actor.GetComponent<ActorTerrainBinding>();
        return binding != null ? binding.Terrain : DiggableTerrainService.Active;
    }
    public bool Sight(Vector2 a,Vector2 b)
    {
        using(new Scope("SG59.NavigationLOS"))
        return Terrain!=null && (Ready==null||Ready()) && TileMover.CanTraverseBox(Terrain,a,b,TileMover.GetMapClearanceExtents(GetComponent<Rigidbody2D>(),MinimumBodyRadius));
    }
    public bool InteractionSight(Vector2 a,Vector2 b)=>Terrain!=null&&(Ready==null||Ready())&&(Terrain is IWorldRegion region?region.RayClear(a,b):Sight(a,b));
    public static bool HasSight(GameObject actor,Vector2 a,Vector2 b)
    {var binding=actor.GetComponent<ActorTerrainBinding>();return binding!=null?binding.Sight(a,b):MapPathfinder.HasLineOfWalkableSight(a,b);}
    public bool TryPath(Vector2 a,Vector2 b,out List<Vector2> path)
    {
        using(new Scope("SG59.Navigation"))
        {
            if(RequestRoute!=null&&(Terrain==null||Ready!=null&&!Ready())){path=new List<Vector2>();CancelNavigation();return false;}
            if(RequestRoute!=null){var answer=RequestRoute(a,b);RouteStatus=answer.Status;LastRouteRevision=answer.Revision;LastRouteDistance=answer.Distance;path=answer.Path??new List<Vector2>();return answer.Status==GridRouteStatus.Succeeded&&path.Count>0;}
            int revision=Revision;path=FindRoute?.Invoke(a,b)??new List<Vector2>();
            RouteStatus=path.Count>0?GridRouteStatus.Succeeded:GridRouteStatus.Unreachable;
            if(revision!=Revision){path.Clear();return false;}
            LastRouteRevision=revision;return path.Count>0;
        }
    }
    public bool AcceptsRoute(int revision)=>Terrain!=null && (Ready==null||Ready()) && revision==Revision;
    public static bool TryPointer(GameObject actor,SporeInputReader input,Camera camera,out Vector2 world)
    {
        var binding=actor.GetComponent<ActorTerrainBinding>();
        if(binding==null)return input.TryGetPointerWorldPosition(camera,out world);
        if(binding.Ready!=null&&!binding.Ready()){world=default;return false;}
        using(new Scope("SG59.InversePointer"))
        {world=binding.PointerToWorld!=null?binding.PointerToWorld(input.AimPointer):default;return binding.PointerToWorld!=null;}
    }
    public readonly struct Scope:IDisposable
    {
        readonly bool active;
        public Scope(string name,bool active=true){this.active=active;if(active)UnityEngine.Profiling.Profiler.BeginSample(name);}
        public void Dispose(){if(active)UnityEngine.Profiling.Profiler.EndSample();}
    }
    void OnDisable()=>CancelNavigation();
    void OnDestroy(){CancelNavigation();RequestRoute=null;RequestProbe=null;ChooseDestination=null;HasMovementDestination=null;AcceptCachedRoute=null;CancelRoute=null;CancelMovementRoute=null;Terrain=null;Ready=null;ReadRevision=null;FindRoute=null;PointerToWorld=null;}
}
