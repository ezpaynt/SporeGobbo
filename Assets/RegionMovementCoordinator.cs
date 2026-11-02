using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One region-owned destination/admission policy. Does not own tasks, paths, combat or terrain.</summary>
public sealed partial class RegionMovementCoordinator : IDisposable
{
    sealed class Actor
    {
        public ActorTerrainBinding Binding;
        public Rigidbody2D Body;
        public Collider2D[] Colliders;
        public readonly List<NativeContactFilter> Filters=new List<NativeContactFilter>();
        public ActorMotionIntent Motion;
        public string Key;
        public int Group;
        public bool Human, Camp, IdleYield, Selected, Ready, Live;
        public Vector2 Position, Extents, Preferred, Resolved, YieldGoal, External;
        public bool Yielding, YieldRequested, Access, Granted, Occupied;
        public int AccessKey, Priority;
        public Vector2 AccessGoal;
        public long Ticket;
        public Actor YieldOwner;
        public long YieldOwnerTicket;
        public Vector2 YieldOwnerOrigin, YieldHeading, RejectedYieldGoal;
        public bool HasRejectedYield;
        public bool Waiting, NoPlan;
        public Vector2 SpacePosition, SpaceExtents, WaitOwnDirection, WaitPeerDirection;
        public bool SpaceObserved;
        public long SpaceVersion;
        public long RouteProgressVersion;
        public ulong WaitSpaceStamp;
        public MonoBehaviour WaitPeerTask;

        public Actor WaitPeer;
        public MonoBehaviour WaitTask;
        public Vector2 WaitPeerPosition, WaitGoal, WaitExtents, WaitPeerExtents;
        public int WaitRevision;
        public MovementContactRole WaitRole, WaitPeerRole;

        public double ExternalStamp = double.NegativeInfinity;
        public MovementContactRole Role;
        public MovementResolutionState State;
        public string Reason;
    }
    sealed class Contact
    {
        public Actor A, B; public Collider2D CA, CB; public bool Owned, Previous;
        public void Apply(bool soft,bool required=false,bool preserveExcluded=false)
        {
            if (CA == null || CB == null) return;
            if ((soft||required||preserveExcluded) && CA.isActiveAndEnabled && CB.isActiveAndEnabled)
            {
                if (!Owned) { Previous = Physics2D.GetIgnoreCollision(CA, CB); Owned = true; }
                bool ignore=soft||preserveExcluded;
                if (Physics2D.GetIgnoreCollision(CA, CB)!=ignore) Physics2D.IgnoreCollision(CA, CB, ignore);
            }
            else if (Owned)
            {
                Physics2D.IgnoreCollision(CA, CB, Previous); Owned = false;
            }
        }
    }
    readonly IDiggableTerrain terrain;
    readonly bool camp;
    readonly RegionMovementSlots destinations;
    readonly List<Actor> actors = new List<Actor>();
    readonly Dictionary<ActorTerrainBinding, Actor> byActor = new Dictionary<ActorTerrainBinding, Actor>();
    readonly List<Contact> contacts = new List<Contact>();
    readonly List<Actor> order = new List<Actor>();
    long nextTicket, nextSpaceVersion;
    int contactSignature = int.MinValue;
    double lastTick = double.NegativeInfinity;
    float resolvedDelta;
    bool disposed;
    public bool Applying { get; private set; }
    public int ActorCount => actors.Count;
    public int DestinationCount => destinations.Count;
    public long Resolutions { get; private set; }
    public long Reconsidered { get; private set; }
    public float MaximumCampPenetration { get; private set; }
    public int OwnedContactOverrides { get { int count = 0; foreach (var p in contacts) if (p.Owned) count++; return count; } }
    static readonly Unity.Profiling.ProfilerMarker profile = new Unity.Profiling.ProfilerMarker("Spore.AI.Movement");

    public RegionMovementCoordinator(IDiggableTerrain terrain, bool camp = false)
    { this.terrain = terrain; this.camp = camp; destinations = new RegionMovementSlots(terrain); }

    public void Register(ActorTerrainBinding binding)
    {
        if (disposed || binding == null || byActor.ContainsKey(binding)) return;
        var body = binding.GetComponent<Rigidbody2D>(); if (body == null) return;
        var a = new Actor { Binding = binding, Body = body, Key = binding.GetInstanceID().ToString(), Colliders = binding.GetComponentsInChildren<Collider2D>(true) };
        foreach (var b in actors)
            foreach (var ca in a.Colliders) foreach (var cb in b.Colliders)
                if (!ca.isTrigger && !cb.isTrigger && ca.attachedRigidbody == body && cb.attachedRigidbody == b.Body)
                    contacts.Add(new Contact { A = a, B = b, CA = ca, CB = cb });
        foreach(var c in a.Colliders)if(c!=null&&!c.isTrigger&&c.attachedRigidbody==body)a.Filters.Add(new NativeContactFilter(c));
        actors.Add(a); byActor.Add(binding, a); binding.Movement = this; contactSignature = int.MinValue;
    }
    public void Configure(ActorTerrainBinding binding, string stableKey, int cooperativeGroup, bool ordinaryCamp, bool human = false, bool idleMayYield = false)
    {
        Register(binding); if (!byActor.TryGetValue(binding, out var a)) return;
        a.Key = stableKey; a.Group = cooperativeGroup; a.Camp = camp && ordinaryCamp;
        a.Human = human; a.IdleYield = idleMayYield;
        if (!human && cooperativeGroup != 0) a.Motion = binding.GetComponent<ActorMotionIntent>() ?? binding.gameObject.AddComponent<ActorMotionIntent>();
        contactSignature = int.MinValue;
    }
    public void Observe(ActorTerrainBinding binding, ActorMotionIntent motion)
    { Register(binding); if (byActor.TryGetValue(binding, out var a)) a.Motion = motion; }
    public void ObserveExternal(ActorTerrainBinding binding, Vector2 wanted)
    { if (byActor.TryGetValue(binding, out var a)) { a.External = wanted; a.ExternalStamp = Time.fixedTimeAsDouble; } }
    public MovementResolutionState State(ActorTerrainBinding binding)
        => byActor.TryGetValue(binding, out var a) ? a.State : MovementResolutionState.Blocked;
    public string Reason(ActorTerrainBinding binding)
        => byActor.TryGetValue(binding, out var a) ? a.Reason : "unregistered actor";
    public long Ticket(ActorTerrainBinding binding) => byActor.TryGetValue(binding, out var a) ? a.Ticket : 0;
    // Cheap task notification: no route search, physics query, timer renewal or steering.
    public bool WaitingForPeer(ActorTerrainBinding binding, Vector2 goal)
    {
        if(!byActor.TryGetValue(binding,out var a)||!a.Waiting)return false;
        if((goal-a.WaitGoal).sqrMagnitude>.0025f){EndWait(a,"WaitGoalChanged");return false;}
        return true;
    }
    // Progression reads an epoch; it never changes goals, tickets or yield directives.
    public bool TryRouteProgressVersion(ActorTerrainBinding binding,out long version)
    {version=0;if(!byActor.TryGetValue(binding,out var a)||a.Yielding)return false;version=a.RouteProgressVersion;return true;}
    public bool RouteConnectionClear(ActorTerrainBinding binding,Vector2 end)
    {
        if(!byActor.TryGetValue(binding,out var a)||!Live(a)||binding.Ready!=null&&!binding.Ready())return false;
        var start=a.Body.position;var extents=TileMover.GetMapClearanceExtents(a.Body,binding.MinimumBodyRadius);var delta=end-start;
        if(a.Motion!=null&&(a.Motion.Bound(delta,1)-delta).sqrMagnitude>.000001f)return false;
        if(!TileMover.CanTraverseActor(terrain,a.Body,start,end,a.Binding.TerrainReserveRadius))return false;
        foreach(var peer in actors)if(peer!=a&&Live(peer)&&!PassThrough(a,peer))
        {var pe=TileMover.GetMapClearanceExtents(peer.Body,peer.Binding.MinimumBodyRadius);if(!MovementContactPolicy.PeerSweepClear(start-peer.Body.position,delta,extents,pe,Soft(a,peer),Physics2D.defaultContactOffset))return false;}
        return true;
    }
    public void RouteRejoined(ActorTerrainBinding binding)
    {if(byActor.TryGetValue(binding,out var a)&&!a.Yielding)EndWait(a,"RouteRejoined",false);}
    public MovementContactRole Role(ActorTerrainBinding binding)
    {
        if (!camp || !byActor.TryGetValue(binding, out var a) || !a.Camp) return MovementContactRole.Strict;
        var session = binding.GetComponent<CombatSessionBinding>();
        if (session == null || session.Rules == null) return MovementContactRole.CampResident;
        if (session.Rules is IMovementContactSession roles && !roles.IsMovementParticipant(binding.gameObject)) return MovementContactRole.CampSpectator;
        return MovementContactRole.Strict;
    }
    bool Cooperative(Actor a, Actor b)
    {
        if (a.Group == 0 || a.Group != b.Group) return false;
        var sa = a.Binding.GetComponent<CombatSessionBinding>(); var sb = b.Binding.GetComponent<CombatSessionBinding>();
        if (sa?.Rules == null && sb?.Rules == null) return true;
        return sa?.Rules != null && ReferenceEquals(sa.Rules, sb?.Rules) && sa.Team == sb.Team;
    }
    bool Soft(Actor a, Actor b) => Cooperative(a, b) && (MovementContactPolicy.SoftPair(Role(a.Binding), Role(b.Binding)) || ExpeditionSoft(a,b));
    public void RefreshContacts()
    {
        if (disposed) return;
        int signature=NativeContactSignature();
        if(signature==contactSignature)return;
        ApplyNativeContactLeases();
        contactSignature=NativeContactSignature();
    }
    public bool VerifyStrict(GameObject actor)
    {
        RefreshContacts();var binding=actor.GetComponent<ActorTerrainBinding>();
        if(Role(binding)!=MovementContactRole.Strict)return false;
        var session=actor.GetComponent<CombatSessionBinding>();
        foreach(var p in contacts)
        {
            if(p.A.Binding!=binding&&p.B.Binding!=binding||!Live(p.A)||!Live(p.B))continue;
            // In a match, only opposing live participants require native contact.
            // Same-team/spectator filtering retains its pre-session policy.
            if(session?.Rules is IMovementContactSession&&!OpposingContact(p))continue;
            if(!EffectiveNativeContact(p.CA,p.CB))return false;
        }
        return true;
    }
    static bool Live(Actor a) => a.Binding != null && a.Binding.isActiveAndEnabled && a.Body != null && a.Body.gameObject.activeInHierarchy && a.Body.simulated;
    public Vector2 Choose(ActorTerrainBinding actor, int key, Vector2 center, Vector2 extents, bool contact)
        => destinations.Choose(actor, key, center, extents, contact);
    public bool HasDestination(ActorTerrainBinding actor) => destinations.HasDestination(actor);
    public bool RequestAccess(ActorTerrainBinding binding, int key, Vector2 goal)
    {
        Register(binding); var a = byActor[binding];
        if (!a.Access || a.AccessKey != key || (a.AccessGoal - goal).sqrMagnitude > .0001f)
        {
            destinations.Release(binding); a.Access = true; a.Granted = false; a.Occupied = false;
            a.AccessKey = key; a.AccessGoal = goal; a.Ticket = 0;
        }
        return a.Granted;
    }
    public bool MarkArrived(ActorTerrainBinding binding)
    {
        if (!byActor.TryGetValue(binding, out var a) || !CanSettle(binding)) return false;
        destinations.MarkOccupied(binding); a.Occupied = true; a.State = MovementResolutionState.Arrived; return true;
    }
    public bool CanSettle(ActorTerrainBinding binding)
    {
        if (!byActor.TryGetValue(binding, out var a)) return false;
        var size = TileMover.GetMapClearanceExtents(a.Body, binding.MinimumBodyRadius);
        foreach (var other in actors)
        {
            if (other == a || !Live(other) || !Cooperative(a, other)) continue;
            var sum = size + TileMover.GetMapClearanceExtents(other.Body, other.Binding.MinimumBodyRadius);
            if (MovementContactPolicy.Penetration(a.Body.position - other.Body.position, sum) > .001f) return false;
        }
        return true;
    }
    public void CancelInput(ActorTerrainBinding binding)
    {
        if (!byActor.TryGetValue(binding, out var a)) return;
        EndWait(a,"WaitInputCanceled"); EndYield(a, "InputCanceled"); a.Ticket = a.Access ? a.Ticket : 0;
    }
    public void ReleaseDuty(ActorTerrainBinding binding)
    {
        destinations.Release(binding); if (!byActor.TryGetValue(binding, out var a)) return;
        EndWait(a,"WaitDutyReleased"); EndYield(a, "DutyReleased"); a.Access = a.Granted = a.Occupied = false; a.Ticket = 0;
    }
    public void Park(ActorTerrainBinding binding)
    {
        ReleaseDuty(binding);if(!byActor.TryGetValue(binding,out var a)||!Live(a))return;
        destinations.ClaimExact(binding,0,a.Body.position);destinations.MarkOccupied(binding);a.Occupied=true;
    }
    public void Deactivate(ActorTerrainBinding binding)
    { ReleaseDuty(binding); contactSignature = int.MinValue; RefreshContacts(); }
    public void Unregister(ActorTerrainBinding binding)
    {
        ReleaseDuty(binding); if (!byActor.TryGetValue(binding, out var a)) return;
        for (int i = contacts.Count - 1; i >= 0; i--) if (contacts[i].A == a || contacts[i].B == a) { contacts[i].Apply(false); contacts.RemoveAt(i); }
        foreach(var filter in a.Filters)filter.Restore();
        actors.Remove(a); byActor.Remove(binding); contactSignature = int.MinValue;RefreshContacts();
    }

    public void Resolve(double tick, float delta)
    {
        if (disposed || lastTick == tick || delta <= 0) return; lastTick = tick; resolvedDelta=delta;
        using var sample = profile.Auto(); RefreshContacts(); order.Clear();
        foreach (var a in actors)
        {
            a.Live = Live(a); if (!a.Live) continue;
            a.Ready = (a.Binding.Ready == null || a.Binding.Ready())&&(!camp||SporeGobbo.Input.SporeInputReader.Instance==null||SporeGobbo.Input.SporeInputReader.Instance.Context==SporeGobbo.Input.SporeInputContext.Gameplay); a.Position = a.Body.position;
            a.Extents = TileMover.GetMapClearanceExtents(a.Body, a.Binding.MinimumBodyRadius);
            if(!a.SpaceObserved||(a.Position-a.SpacePosition).sqrMagnitude>.0016f||(a.Extents-a.SpaceExtents).sqrMagnitude>.000001f)
            {a.SpaceObserved=true;a.SpacePosition=a.Position;a.SpaceExtents=a.Extents;a.SpaceVersion=++nextSpaceVersion;}
            a.Selected = a.Motion != null && a.Motion.ReadSelected(tick, out _, out _, out _);
            a.Preferred = Vector2.zero; a.Priority = 0;
            if (a.Selected) { a.Motion.ReadSelected(tick, out a.Preferred, out _, out a.Priority); a.Preferred = a.Motion.Bound(a.Preferred, delta); }
            if (a.Human) a.Preferred = a.ExternalStamp == tick ? a.External : a.Body.linearVelocity;
            a.Resolved = a.Human || a.Motion == null ? a.Body.linearVelocity : a.Preferred;
            if (!a.Ready) a.Resolved = a.Preferred = Vector2.zero;
            if(!a.Access&&!a.Yielding&&!a.Waiting&&a.Preferred.sqrMagnitude<.0001f)a.Ticket=0;
            a.State = a.Access && !a.Granted ? MovementResolutionState.QueuedForAccess : a.Preferred.sqrMagnitude > .0001f ? MovementResolutionState.Moving : MovementResolutionState.Idle;
            a.Reason = null; a.YieldRequested = false;

            Diagnose("Snapshot", a);

            order.Add(a);
        }
        order.Sort((a, b) => { int c = (a.Ticket == 0 ? long.MaxValue : a.Ticket).CompareTo(b.Ticket == 0 ? long.MaxValue : b.Ticket); return c != 0 ? c : string.CompareOrdinal(a.Key, b.Key); });
        foreach(var a in order)if(a.Ticket==0&&(a.Access||a.Preferred.sqrMagnitude>.0001f))a.Ticket=++nextTicket;
        foreach(var a in order) Diagnose("TicketOrder",a);
        foreach (var a in order) if (a.Ready && a.Access && !a.Granted) ResolveAccess(a);
        foreach(var a in order)if(a.Waiting)
        {
            var peer=a.WaitPeer;
            if(!a.Ready||a.WaitTask!=null&&!a.Selected||peer==null||!Live(peer)||!peer.Ready||!Cooperative(a,peer)||PassThrough(a,peer)||
               a.Motion?.SelectedTask!=a.WaitTask||a.Role!=a.WaitRole||peer.Role!=a.WaitPeerRole||a.Binding.Revision!=a.WaitRevision||
               (a.Binding.CurrentMovementGoal-a.WaitGoal).sqrMagnitude>.0025f||
               (peer.Position-a.WaitPeerPosition).sqrMagnitude>.0016f||
               (a.Extents-a.WaitExtents).sqrMagnitude>.000001f||
               (peer.Extents-a.WaitPeerExtents).sqrMagnitude>.000001f||
               (!a.NoPlan&&peer.Preferred.sqrMagnitude>.0001f)||
               (a.NoPlan&&(a.WaitSpaceStamp!=LocalSpaceStamp(a,peer)||peer.Motion?.SelectedTask!=a.WaitPeerTask||
                  DirectionChanged(a.Preferred,a.WaitOwnDirection)||DirectionChanged(peer.Preferred,a.WaitPeerDirection))))
                EndWait(a,"WaitContextChanged");
            else {a.Resolved=Vector2.zero;ApplyWaitState(a);}
        }
        // Revalidate region-owned maneuvers before considering new conflicts. A quiet tick
        // does not transfer ownership back to the task.
        foreach (var a in order) if (a.Yielding)
        {
            if(a.Binding.HasMovementGoal && (a.YieldGoal-a.Position).magnitude<.04f && a.YieldOwner!=null &&
               a.YieldOwner.Preferred.sqrMagnitude<.0001f && a.Preferred.sqrMagnitude>.0001f &&
               !DutyClearOfPeer(a,a.YieldOwner))
            { BeginWait(a,a.YieldOwner); continue; }
            if (!MayYield(a) || a.YieldOwner == null || !Live(a.YieldOwner) || !a.YieldOwner.Ready ||
                !Cooperative(a,a.YieldOwner) || a.YieldOwner.Ticket != a.YieldOwnerTicket || YieldOwnerClear(a,delta))
                EndYield(a,"YieldOwnerReleased");
            else if (!YieldGoalClear(a,a.YieldOwner,a.YieldGoal)) RejectYield(a,"RetainedYieldRejected");
            else a.Resolved=YieldVelocity(a,delta);
        }
        // A single explicit yield directive is selected by the region, never by a neighboring task.
        for (int i = 0; i < order.Count; i++) for (int j = i + 1; j < order.Count; j++)
        {
            var a = order[i]; var b = order[j]; if (!Cooperative(a, b)||PassThrough(a,b)) continue;
            bool conflict = !PairClear(a, a.Resolved, b, b.Resolved, delta);
            bool overlap = MovementContactPolicy.Penetration(a.Position - b.Position, a.Extents + b.Extents) > .001f;
            bool settling=overlap&&a.Preferred.sqrMagnitude<.0001f&&b.Preferred.sqrMagnitude<.0001f;
            if (!conflict && !settling) continue;
            if(a.Waiting&&a.WaitPeer!=b)EndWait(a,"WaitContactChanged");
            if(b.Waiting&&b.WaitPeer!=a)EndWait(b,"WaitContactChanged");
            if(a.Waiting||b.Waiting)continue; // safety pass still enforces physical contact below
            var winner = !MayYield(a) && MayYield(b) ? a : !MayYield(b) && MayYield(a) ? b :
                a.Preferred.sqrMagnitude>.0001f&&b.Preferred.sqrMagnitude<.0001f?a:
                b.Preferred.sqrMagnitude>.0001f&&a.Preferred.sqrMagnitude<.0001f?b:a;
            var loser = winner == a ? b : a;
            Diagnose("ConflictLoser",loser,winner);
            if (!TryYield(loser, winner)) { winner.Reason = loser.Reason = "NoYieldSpace"; }
        }
        foreach (var a in order)
        {
            if (a.Human || a.Motion == null) continue;
            if (!a.YieldRequested && !a.Yielding) Diagnose("YieldNotRequested",a);
            if (a.Yielding)
            {
                var offset = a.YieldGoal - a.Position;
                a.Resolved = YieldVelocity(a,delta);
                if(a.Yielding) a.State = a.Selected ? MovementResolutionState.Yielding : MovementResolutionState.Settling;
                if(offset.magnitude < .04f) Diagnose("YieldHolding",a,a.YieldOwner);
            }
            if(a.Waiting){a.Resolved=Vector2.zero;ApplyWaitState(a);}
            a.Resolved = a.Motion.Bound(a.Resolved, delta);
            if (!a.Ready || !TileMover.CanTraverseActor(terrain,a.Body,a.Position,a.Position+a.Resolved*delta,a.Binding.TerrainReserveRadius))
            { if(a.Yielding)RejectYield(a,"YieldTerrainOrRegionRejected"); a.Resolved = Vector2.zero; a.State = MovementResolutionState.Blocked; a.Reason = "TerrainOrRegionNotReady"; }
            if (a.Access && !a.Granted && !a.Yielding) { a.Resolved = Vector2.zero; a.State = MovementResolutionState.QueuedForAccess; }
            Diagnose("BeforeSafety",a);
        }
        // Removing one motion can invalidate a follower behind it. Recheck to a fixed point;
        // candidates are only removed here, never replaced by another escape heuristic.
        for (int pass = 0; pass <= order.Count; pass++)
        {
            bool changed = false;
            for (int i = 0; i < order.Count; i++) for (int j = i + 1; j < order.Count; j++)
            {
                var a = order[i]; var b = order[j]; if (!Cooperative(a, b) || PairClear(a, a.Resolved, b, b.Resolved, delta)) continue;
                Diagnose("SafetyConflict",a,b,pass); Diagnose("SafetyConflict",b,a,pass);
                if (!a.Human && PairClear(a, Vector2.zero, b, b.Resolved, delta)) { Diagnose("SafetyStopOne",a,b,pass); changed |= Stop(a); }
                else if (!b.Human && PairClear(a, a.Resolved, b, Vector2.zero, delta)) { Diagnose("SafetyStopOne",b,a,pass); changed |= Stop(b); }
                else { if (!a.Human) { Diagnose("SafetyStopBoth",a,b,pass); changed |= Stop(a); } if (!b.Human) { Diagnose("SafetyStopBoth",b,a,pass); changed |= Stop(b); } }
            }
            if (!changed) break;
        }
        Applying = true;
        try
        {
            foreach (var a in order)
            {
                Diagnose("Resolved",a);
                if (!a.Human && a.Motion != null) a.Motion.ApplyResolved(tick, a.Resolved, a.Preferred, a.State);
                Diagnose("Applied",a);
                foreach (var b in order) if (a != b && Soft(a, b)) MaximumCampPenetration = Mathf.Max(MaximumCampPenetration, MovementContactPolicy.Penetration(a.Position - b.Position, a.Extents + b.Extents));
            }
        }
        finally { Applying = false; }
        Resolutions++;
    }
    bool Stop(Actor a)
    {
        bool changed = a.Resolved.sqrMagnitude > .0000001f; if(a.Yielding&&changed)RejectYield(a,"YieldExecutionRejected"); a.Resolved = Vector2.zero;
        a.State = MovementResolutionState.QueuedForAccess; a.Reason = a.Reason ?? "PhysicalPeerContact"; Reconsidered++; return changed;
    }
    bool PairClear(Actor a, Vector2 av, Actor b, Vector2 bv, float delta)
        => PeerSweepClear(a,(av-bv)*delta,b);
    bool PeerSweepClear(Actor a,Vector2 relativeDelta,Actor b)
        => PassThrough(a,b)||MovementContactPolicy.PeerSweepClear(a.Position-b.Position,relativeDelta,a.Extents,b.Extents,Soft(a,b),Physics2D.defaultContactOffset);
    Vector2 YieldVelocity(Actor a,float delta)
    {
        var offset=a.YieldGoal-a.Position;
        // Arrival tolerance alone cannot establish the requester's physical clearance.
        // Finish the retained connector when its last centimetres still matter; the
        // ordinary full-body terrain, task and peer safety passes remain authoritative.
        if(offset.magnitude<.04f && RequesterAdvanceClear(a,a.YieldOwner,a.Position)) return Vector2.zero;
        if(offset.sqrMagnitude==0) { RejectYield(a,"YieldEndpointNoClearance"); return Vector2.zero; }
        return offset.normalized*Mathf.Min(Mathf.Max(1.6f,a.Preferred.magnitude),offset.magnitude/delta);
    }
    void EndYield(Actor a,string reason)
    {
        if(a.Yielding){a.RouteProgressVersion++;Diagnose(reason,a,a.YieldOwner);}
        a.Yielding=false;a.YieldOwner=null;a.YieldOwnerTicket=0;a.HasRejectedYield=false;
    }
    void RejectYield(Actor a,string reason)
    {
        if(a.Yielding)a.RouteProgressVersion++;
        Diagnose(reason,a,a.YieldOwner);a.RejectedYieldGoal=a.YieldGoal;a.HasRejectedYield=true;
        a.Yielding=false;a.Resolved=Vector2.zero;a.State=MovementResolutionState.Blocked;a.Reason=reason;
    }
    bool DutyClearOfPeer(Actor a,Actor peer)
    {
        float span=Mathf.Max(a.Extents.x+peer.Extents.x,a.Extents.y+peer.Extents.y)+Physics2D.defaultContactOffset*2;
        if(!a.Binding.HasMovementGoal)return PeerSweepClear(a,a.Preferred*Time.fixedDeltaTime,peer);
        float distance=Mathf.Min(span*2,Vector2.Distance(a.Binding.CurrentMovementGoal,a.Position));
        return PeerSweepClear(a,a.Preferred.normalized*distance,peer);
    }
    void ApplyWaitState(Actor a)
    {a.State=a.NoPlan?MovementResolutionState.NoLocalYieldPlan:MovementResolutionState.WaitingForPeer;a.Reason=a.NoPlan?"NoUsefulLocalYieldPlan":"UnchangedPeerObstruction";}
    static bool DirectionChanged(Vector2 velocity,Vector2 previous)
        =>velocity.sqrMagnitude>.0001f&&(velocity.normalized-previous).sqrMagnitude>.01f;
    ulong LocalSpaceStamp(Actor a,Actor peer)
    {
        // Only changes in this finite candidate neighbourhood invalidate a no-plan result.
        // Far-away moving actors cannot cause repeated local candidate enumeration.
        float reach=Mathf.Max(a.Extents.x+peer.Extents.x,a.Extents.y+peer.Extents.y)+.06f+terrain.CellSize;
        ulong hash=1469598103934665603UL;
        unchecked{foreach(var other in actors)
        {
            float radius=reach+Mathf.Max(other.Extents.x,other.Extents.y);
            if(!other.Live||Mathf.Min((other.Position-a.Position).sqrMagnitude,(other.Position-peer.Position).sqrMagnitude)>radius*radius)continue;
            hash=(hash^(ulong)other.Binding.GetInstanceID())*1099511628211UL;
            hash=(hash^(ulong)other.SpaceVersion)*1099511628211UL;
            hash=(hash^(ulong)other.Group^(ulong)other.Role^(other.Ready?1UL:0UL))*1099511628211UL;
        }}return hash;
    }
    bool RequesterAdvanceClear(Actor yielding,Actor requester,Vector2 holding)
    {
        Vector2 wanted=requester.Access?requester.AccessGoal-requester.Position:requester.Preferred;
        // An unmoving peer needs separation, not a fictional request to advance.
        if(wanted.sqrMagnitude<.0001f)return true;
        // Validate the full movement the motor will actually execute. Looking beyond a
        // route waypoint can falsely turn a valid corner approach into no-plan.
        float distance=requester.Access?terrain.CellSize*.5f:wanted.magnitude*resolvedDelta;
        if(requester.Access)distance=Mathf.Min(distance,wanted.magnitude);
        else if(requester.Binding.HasMovementGoal)distance=Mathf.Min(distance,Vector2.Distance(requester.Position,requester.Binding.CurrentMovementGoal));
        if(distance<.0001f)return true;
        var step=wanted.normalized*distance;
        if(requester.Motion!=null&&(requester.Motion.Bound(step,1)-step).sqrMagnitude>.000001f){Diagnose("RequesterProgressBoundsRejected",requester,yielding,-1,holding);return false;}
        if(!TileMover.CanTraverseActor(terrain,requester.Body,requester.Position,requester.Position+step,requester.Binding.TerrainReserveRadius)){Diagnose("RequesterProgressTerrainRejected",requester,yielding,-1,holding);return false;}
        foreach(var other in order)
        {
            if(other==requester||!other.Live||PassThrough(requester,other))continue;
            var position=other==yielding?holding:other.Position;
            if(!MovementContactPolicy.PeerSweepClear(requester.Position-position,step,requester.Extents,other.Extents,Soft(requester,other),Physics2D.defaultContactOffset)){Diagnose("RequesterProgressPeerRejected",requester,other,yielding.Binding.GetInstanceID(),holding);return false;}
        }
        return true;
    }
    void BeginWait(Actor a,Actor peer,bool noPlan=false)
    {
        if(!a.Waiting)a.RouteProgressVersion++;
        a.Waiting=true;a.NoPlan=noPlan;a.WaitPeer=peer;a.WaitPeerPosition=peer.Position;
        a.WaitPeerTask=peer.Motion?.SelectedTask;a.WaitOwnDirection=a.Preferred.normalized;a.WaitPeerDirection=peer.Preferred.normalized;
        a.WaitSpaceStamp=LocalSpaceStamp(a,peer);
        a.WaitTask=a.Motion?.SelectedTask;a.WaitGoal=a.Binding.CurrentMovementGoal;
        a.WaitRevision=a.Binding.Revision;a.WaitRole=a.Role;a.WaitPeerRole=peer.Role;a.WaitExtents=a.Extents;a.WaitPeerExtents=peer.Extents;
        EndYield(a,"YieldDisplacementComplete");a.Resolved=Vector2.zero;
        ApplyWaitState(a);
        Diagnose(noPlan?"NoLocalYieldPlan":"WaitStarted",a,peer);
    }
    void EndWait(Actor a,string reason,bool progressChanged=true)
    {
        if(a.Waiting&&progressChanged)a.RouteProgressVersion++;
        if(a.Waiting)Diagnose(reason,a,a.WaitPeer);
        a.Waiting=false;a.NoPlan=false;a.WaitPeer=null;a.WaitTask=null;a.WaitPeerTask=null;
    }
    bool YieldOwnerClear(Actor a,float delta)
    {
        var owner=a.YieldOwner;
        // Finish only after physical passage or substantial withdrawal, never because
        // the current fixed-step velocity happens to be clear.
        var progress=owner.Position-a.YieldOwnerOrigin;
        float span=Mathf.Max(a.Extents.x+owner.Extents.x,a.Extents.y+owner.Extents.y)+Physics2D.defaultContactOffset*2;
        bool passed=Vector2.Dot(progress,a.YieldHeading)>span;
        bool withdrawn=progress.magnitude>span*2;
        // A stationary blocker cannot be required to pass us. Once the authored
        // yield displacement is actually complete, hand back only a safe task step.
        bool reached=(a.YieldGoal-a.Position).magnitude<.04f;
        if(reached&&a.Preferred.sqrMagnitude>.0001f&&PeerSweepClear(a,a.Preferred*delta,owner)&&RequesterAdvanceClear(a,owner,a.Position)&&PairClear(a,a.Preferred,owner,owner.Preferred,delta))return true;
        if(!passed&&!withdrawn)return false;
        Vector2 next=owner.Preferred.sqrMagnitude>.0001f?owner.Preferred.normalized*span:Vector2.zero;
        return PeerSweepClear(owner,next,a);
    }
    bool YieldGoalClear(Actor a,Actor requester,Vector2 goal,bool requireAdvance=true)
    {
        if(!TileMover.CanTraverseActor(terrain,a.Body,a.Position,goal,a.Binding.TerrainReserveRadius)||!destinations.CanPlace(a.Binding,goal,a.Extents))return false;
        var brain=a.Binding.GetComponent<BuddyBrain>();if(brain!=null&&!brain.AllowsDestination(goal))return false;
        foreach(var other in order)
        {
            if(other==a)continue;
            if(!PeerSweepClear(a,goal-a.Position,other))return false;
            if(MovementContactPolicy.Penetration(goal-other.Position,a.Extents+other.Extents+Vector2.one*Physics2D.defaultContactOffset*2)>0)return false;
        }
        if(requester.Access&&MovementContactPolicy.Penetration(goal-requester.AccessGoal,a.Extents+requester.Extents+Vector2.one*Physics2D.defaultContactOffset*2)>0)return false;
        return !requireAdvance||RequesterAdvanceClear(a,requester,goal);
    }
    bool MayYield(Actor a)
        => a.Ready && !a.Human && a.Motion != null && a.Motion.isActiveAndEnabled && (!a.Access || !a.Occupied) &&
           (a.Selected ? a.Motion.MayYield : a.IdleYield && a.Role != MovementContactRole.Strict);
    void ResolveAccess(Actor a)
    {
        Reconsidered++;
        foreach (var other in order)
        {
            if (other == a || !other.Access || other.Ticket >= a.Ticket) continue;
            if (MovementContactPolicy.Penetration(a.AccessGoal - other.AccessGoal, a.Extents + other.Extents) > 0) { a.Reason = "OlderAccessClaim"; Diagnose("AccessOlderOwner",a,other); return; }
        }
        bool clear = true;
        foreach (var other in order)
        {
            if (other == a || !Cooperative(a, other)) continue;
            if (MovementContactPolicy.Penetration(a.AccessGoal - other.Position, a.Extents + other.Extents + Vector2.one * .02f) <= 0) continue;
            clear = false; Diagnose("AccessPhysicalOccupant",a,other); TryYield(other, a);
        }
        if (clear && destinations.ClaimExact(a.Binding, a.AccessKey, a.AccessGoal)) { a.Granted = true; a.Reason = null; a.YieldRequested = false; }
        else a.Reason = "OccupiedWorkApproach";
        Diagnose("AccessResult",a);
    }
    bool TryYield(Actor a, Actor requester)
    {
        if(a.Waiting&&a.NoPlan&&ReferenceEquals(a.WaitPeer,requester)&&a.WaitSpaceStamp==LocalSpaceStamp(a,requester))
        {a.Reason="NoUsefulLocalYieldPlan";return false;}
        if (!MayYield(a)) { Diagnose("YieldIneligible",a,requester); return false; } a.YieldRequested = true;
        if (a.Yielding)
        {
            if(!ReferenceEquals(a.YieldOwner,requester))return true; // existing owner wins until released
            if(YieldGoalClear(a,requester,a.YieldGoal)){Diagnose("YieldRetained",a,requester);return true;}
            RejectYield(a,"RetainedYieldRejected");
        }
        if(!ReferenceEquals(a.YieldOwner,requester)||a.YieldOwnerTicket!=requester.Ticket)a.HasRejectedYield=false;
        Vector2 heading = requester.Access ? requester.AccessGoal - requester.Position : requester.Preferred;
        if (heading.sqrMagnitude < .0001f) heading = a.Position - requester.Position;
        heading.Normalize(); if (heading.sqrMagnitude < .1f) heading = Vector2.right;
        Vector2 side = new Vector2(heading.y, -heading.x);
        float distance = Mathf.Max(a.Extents.x + requester.Extents.x, a.Extents.y + requester.Extents.y) + .06f;
        float best = float.PositiveInfinity; Vector2 selected = default;
        // One candidate basis, not an actor-specific steering fallback: retain heading
        // directions and include grid-axis tangents for axis-aligned terrain/contact.
        for (int option = 0; option < 24; option++)
        {
            float angle = (option%8) * Mathf.PI * .25f;
            Vector2 direction = option<16?side*Mathf.Cos(angle)+heading*Mathf.Sin(angle):
                option%4==0?Vector2.right:option%4==1?Vector2.up:option%4==2?Vector2.left:Vector2.down;
            bool shortStep=option<8||option>=16&&option<20;
            var goal = a.Position + direction * distance*(shortStep?.5f:1);
            if (!TileMover.CanTraverseActor(terrain,a.Body,a.Position,goal,a.Binding.TerrainReserveRadius)) { Diagnose("CandidateTerrain",a,requester,option,goal); continue; }
            if (!destinations.CanPlace(a.Binding, goal, a.Extents)) { Diagnose("CandidateLease",a,requester,option,goal); continue; }
            var brain = a.Binding.GetComponent<BuddyBrain>(); if (brain != null && !brain.AllowsDestination(goal)) { Diagnose("CandidateTaskBounds",a,requester,option,goal); continue; }
            if(a.HasRejectedYield&&(goal-a.RejectedYieldGoal).sqrMagnitude<.0016f){Diagnose("CandidatePreviouslyRejected",a,requester,option,goal);continue;}
            if(!YieldGoalClear(a,requester,goal,false)){Diagnose("CandidateSharedClearance",a,requester,option,goal);continue;}
            if(!RequesterAdvanceClear(a,requester,goal)){Diagnose("CandidateRequesterProgress",a,requester,option,goal);continue;}
            float clearance = requester.Access ? Vector2.Distance(goal, requester.AccessGoal) : Mathf.Abs(Vector2.Dot(goal - requester.Position, side));
            float cost = Vector2.Distance(a.Position,goal) - clearance * .25f + option * .0001f;
            if (cost < best) { best = cost; selected = goal; }
        }
        if (float.IsPositiveInfinity(best))
        {
            Diagnose("YieldNoUsefulCandidate",a,requester);BeginWait(a,requester,true);
            if(!requester.Human&&requester.Motion!=null)BeginWait(requester,a,true);
            return false;
        }
        if (a.Camp && !a.Selected && !destinations.RelocateStanding(a.Binding, selected)){Diagnose("YieldStandingClaimRejected",a,requester,-1,selected);return false;}
        a.YieldGoal = selected; a.YieldOwner=requester;a.YieldOwnerTicket=requester.Ticket;a.YieldOwnerOrigin=requester.Position;a.YieldHeading=heading;a.Yielding = true; if(a.Ticket==0)a.Ticket=++nextTicket; Diagnose("YieldSelected",a,requester,-1,selected); return true;
    }
    // The human motor stays authoritative. It may not rely on a promised future NPC yield.
    public Vector2 ConstrainExternal(ActorTerrainBinding binding, Vector2 wanted, float radius)
    {
        ObserveExternal(binding, wanted);
        if (Applying || !byActor.TryGetValue(binding, out var a)) return wanted;
        // Human expedition input is never arbitrated against its own friendly squad.
        // Native pair filtering is explicitly leased by this same region; terrain/hostiles stay solid.
        if(!a.Camp||Role(binding)==MovementContactRole.Strict)return wanted;
        Vector2 size=TileMover.GetMapClearanceExtents(a.Body,radius);
        foreach(var other in actors)
        {
            if(other==a||!Live(other)||!Soft(a,other))continue;
            var otherSize=TileMover.GetMapClearanceExtents(other.Body,other.Binding.MinimumBodyRadius);
            var sum=size+otherSize;
            if(!MovementContactPolicy.SweepClear(a.Body.position-other.Body.position,wanted*Time.fixedDeltaTime,sum,
                MovementContactPolicy.Allowance(size,otherSize,true)))return Vector2.zero;
        }
        return wanted;
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        foreach (var pair in contacts) pair.Apply(false);
        foreach(var actor in actors)foreach(var filter in actor.Filters)filter.Restore();
        foreach (var a in actors) if (a.Binding != null && ReferenceEquals(a.Binding.Movement, this)) a.Binding.Movement = null;
        destinations.Dispose(); contacts.Clear(); actors.Clear(); byActor.Clear(); order.Clear();
    }
}



