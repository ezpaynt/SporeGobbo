using System;
using UnityEngine;

public sealed partial class RegionMovementCoordinator
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Read-only final-snapshot audit. Uses the same body, task, terrain and peer predicates.
    // Never calls Resolve, TryYield, RequestPath, setters or task ownership methods.
    public void ObserveJoin(ActorTerrainBinding binding,Vector2 end,bool fixedStep,out bool terrainClear,out bool boundsClear,out string blockedPeers)
    {
        terrainClear=boundsClear=false;blockedPeers="";
        if(!byActor.TryGetValue(binding,out var a)||!Live(a)){blockedPeers="unregistered";return;}
        Vector2 start=a.Body.position,extents=TileMover.GetMapClearanceExtents(a.Body,binding.MinimumBodyRadius),delta=end-start;
        if(fixedStep)delta=Vector2.ClampMagnitude(delta,binding.GetComponent<BuddyFollow>().followSpeed*Time.fixedDeltaTime);
        terrainClear=(binding.Ready==null||binding.Ready())&&TileMover.CanTraverseBox(terrain,start,start+delta,extents);
        boundsClear=a.Motion==null||(a.Motion.Bound(delta,1)-delta).sqrMagnitude<=.000001f;
        foreach(var peer in actors)if(peer!=a&&Live(peer)&&!PassThrough(a,peer))
        {
            var pe=TileMover.GetMapClearanceExtents(peer.Body,peer.Binding.MinimumBodyRadius);
            if(!MovementContactPolicy.PeerSweepClear(start-peer.Body.position,delta,extents,pe,Soft(a,peer),Physics2D.defaultContactOffset))blockedPeers+=(blockedPeers.Length==0?"":"|")+peer.Key;
        }
    }
#endif
    // Observer only. The owned movement capture stores structs in a fixed buffer, without I/O
    // or writes back into tasks, actors, terrain, tickets, claims, velocities or this coordinator.
    public struct DecisionTrace
    {
        public double Tick;
        public int Region, Actor, Peer, Detail, Group, AccessKey, LeaseKey;
        public long Ticket, PeerTicket, YieldOwnerTicket;
        public int YieldOwner;
        public bool HasRejectedYield;
        public Vector2 YieldOwnerOrigin, YieldHeading, RejectedYieldGoal;
        public string Stage, Key, PeerKey, Reason, Task;
        public SporeGobbo.Geometry.GridRouteStatus RouteStatus;
        public Vector2 Position, Extents, Preferred, Resolved, Actual, YieldGoal, AccessGoal, LeaseGoal, Goal, Candidate;
        public bool Camp, Human, Ready, Selected, Yielding, YieldRequested, Access, Granted, Occupied, HasLease, LeaseOccupied;
        public MovementContactRole Role;
        public MovementResolutionState State;
    }
    public static Action<DecisionTrace> DiagnosticObserver;
    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    void Diagnose(string stage, Actor a, Actor peer = null, int detail = -1, Vector2 candidate = default)
    {
        var observer = DiagnosticObserver;
        if (observer == null) return;
        bool hasLease = destinations.Inspect(a.Binding, out int leaseKey, out Vector2 leaseGoal, out bool leaseOccupied);
        observer(new DecisionTrace {
            Tick=lastTick, Region=GetHashCode(), Actor=a.Binding.GetInstanceID(), Peer=peer?.Binding.GetInstanceID()??0,
            Detail=detail, Group=a.Group, AccessKey=a.AccessKey, LeaseKey=leaseKey, Ticket=a.Ticket, PeerTicket=peer?.Ticket??0,
            YieldOwner=a.YieldOwner?.Binding.GetInstanceID()??0,YieldOwnerTicket=a.YieldOwnerTicket,HasRejectedYield=a.HasRejectedYield,YieldOwnerOrigin=a.YieldOwnerOrigin,YieldHeading=a.YieldHeading,RejectedYieldGoal=a.RejectedYieldGoal,
            Stage=stage, Key=a.Key, PeerKey=peer?.Key??"", Reason=a.Reason??"", Task=a.Motion?.DiagnosticTaskName??"None", RouteStatus=a.Binding.RouteStatus,
            Position=a.Position, Extents=a.Extents, Preferred=a.Preferred, Resolved=a.Resolved, Actual=a.Body.linearVelocity,
            YieldGoal=a.YieldGoal, AccessGoal=a.AccessGoal, LeaseGoal=leaseGoal, Goal=a.Binding.CurrentMovementGoal, Candidate=candidate,
            Camp=camp, Human=a.Human, Ready=a.Ready, Selected=a.Selected, Yielding=a.Yielding, YieldRequested=a.YieldRequested,
            Access=a.Access, Granted=a.Granted, Occupied=a.Occupied, HasLease=hasLease, LeaseOccupied=leaseOccupied, Role=a.Role, State=a.State
        });
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ObserveReciprocal(stage,a,peer);
#endif
    }
}
