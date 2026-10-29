using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One region-owned destination/admission policy. Does not own tasks, paths, combat or terrain.</summary>
public sealed class RegionMovementCoordinator : IDisposable
{
    sealed class Actor
    {
        public ActorTerrainBinding Binding;
        public Rigidbody2D Body;
        public Collider2D[] Colliders;
        public ActorMotionIntent Motion;
        public string Key;
        public int Group;
        public bool Human, Camp, IdleYield, Selected, Ready, Live;
        public Vector2 Position, Extents, Preferred, Resolved, YieldGoal, External;
        public bool Yielding, YieldRequested, Access, Granted, Occupied;
        public int AccessKey, Priority;
        public Vector2 AccessGoal;
        public long Ticket;
        public double ExternalStamp = double.NegativeInfinity;
        public MovementContactRole Role;
        public MovementResolutionState State;
        public string Reason;
    }
    sealed class Contact
    {
        public Actor A, B; public Collider2D CA, CB; public bool Owned, Previous;
        public void Apply(bool soft)
        {
            if (CA == null || CB == null) return;
            if (soft && CA.isActiveAndEnabled && CB.isActiveAndEnabled)
            {
                if (!Owned) { Previous = Physics2D.GetIgnoreCollision(CA, CB); Owned = true; }
                if (!Physics2D.GetIgnoreCollision(CA, CB)) Physics2D.IgnoreCollision(CA, CB, true);
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
    long nextTicket;
    int contactSignature = int.MinValue;
    double lastTick = double.NegativeInfinity;
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
    bool Soft(Actor a, Actor b) => Cooperative(a, b) && MovementContactPolicy.SoftPair(Role(a.Binding), Role(b.Binding));
    public void RefreshContacts()
    {
        if (disposed) return;
        int signature = actors.Count;
        foreach (var a in actors)
        {
            a.Role = Role(a.Binding);
            unchecked { signature = signature * 31 + (int)a.Role; signature = signature * 31 + a.Group; var session = a.Binding.GetComponent<CombatSessionBinding>(); signature = signature * 31 + (session?.Rules?.GetHashCode() ?? 0); signature = signature * 31 + (session != null ? session.Team : 0); signature = signature * 31 + (Live(a) ? 1 : 0); }
            foreach (var c in a.Colliders) unchecked { signature = signature * 31 + (c != null && c.isActiveAndEnabled ? 1 : 0); }
        }
        if (signature == contactSignature) return;
        foreach (var p in contacts) p.Apply(Live(p.A) && Live(p.B) && Soft(p.A, p.B));
        contactSignature = signature;
    }
    public bool VerifyStrict(GameObject actor)
    {
        RefreshContacts(); var binding = actor.GetComponent<ActorTerrainBinding>();
        if (Role(binding) != MovementContactRole.Strict) return false;
        foreach (var p in contacts) if ((p.A.Binding == binding || p.B.Binding == binding) && p.CA != null && p.CB != null && Physics2D.GetIgnoreCollision(p.CA, p.CB)) return false;
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
        a.Yielding = false; a.Ticket = a.Access ? a.Ticket : 0;
    }
    public void ReleaseDuty(ActorTerrainBinding binding)
    {
        destinations.Release(binding); if (!byActor.TryGetValue(binding, out var a)) return;
        a.Access = a.Granted = a.Occupied = a.Yielding = false; a.Ticket = 0;
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
        actors.Remove(a); byActor.Remove(binding); contactSignature = int.MinValue;
    }

    public void Resolve(double tick, float delta)
    {
        if (disposed || lastTick == tick || delta <= 0) return; lastTick = tick;
        using var sample = profile.Auto(); RefreshContacts(); order.Clear();
        foreach (var a in actors)
        {
            a.Live = Live(a); if (!a.Live) continue;
            a.Ready = (a.Binding.Ready == null || a.Binding.Ready())&&(!camp||SporeGobbo.Input.SporeInputReader.Instance==null||SporeGobbo.Input.SporeInputReader.Instance.Context==SporeGobbo.Input.SporeInputContext.Gameplay); a.Position = a.Body.position;
            a.Extents = TileMover.GetMapClearanceExtents(a.Body, a.Binding.MinimumBodyRadius);
            a.Selected = a.Motion != null && a.Motion.ReadSelected(tick, out _, out _, out _);
            a.Preferred = Vector2.zero; a.Priority = 0;
            if (a.Selected) { a.Motion.ReadSelected(tick, out a.Preferred, out _, out a.Priority); a.Preferred = a.Motion.Bound(a.Preferred, delta); }
            if (a.Human) a.Preferred = a.ExternalStamp == tick ? a.External : a.Body.linearVelocity;
            a.Resolved = a.Human || a.Motion == null ? a.Body.linearVelocity : a.Preferred;
            if (!a.Ready) a.Resolved = a.Preferred = Vector2.zero;
            if(!a.Access&&!a.Yielding&&a.Preferred.sqrMagnitude<.0001f)a.Ticket=0;
            a.State = a.Access && !a.Granted ? MovementResolutionState.QueuedForAccess : a.Preferred.sqrMagnitude > .0001f ? MovementResolutionState.Moving : MovementResolutionState.Idle;
            a.Reason = null; a.YieldRequested = false;

            order.Add(a);
        }
        order.Sort((a, b) => { int c = (a.Ticket == 0 ? long.MaxValue : a.Ticket).CompareTo(b.Ticket == 0 ? long.MaxValue : b.Ticket); return c != 0 ? c : string.CompareOrdinal(a.Key, b.Key); });
        foreach(var a in order)if(a.Ticket==0&&(a.Access||a.Preferred.sqrMagnitude>.0001f))a.Ticket=++nextTicket;
        foreach (var a in order) if (a.Ready && a.Access && !a.Granted) ResolveAccess(a);
        // A single explicit yield directive is selected by the region, never by a neighboring task.
        for (int i = 0; i < order.Count; i++) for (int j = i + 1; j < order.Count; j++)
        {
            var a = order[i]; var b = order[j]; if (!Cooperative(a, b)) continue;
            bool conflict = !PairClear(a, a.Resolved, b, b.Resolved, delta);
            bool overlap = MovementContactPolicy.Penetration(a.Position - b.Position, a.Extents + b.Extents) > .001f;
            bool settling=overlap&&a.Preferred.sqrMagnitude<.0001f&&b.Preferred.sqrMagnitude<.0001f;
            if (!conflict && !settling) continue;
            var winner = !MayYield(a) && MayYield(b) ? a : !MayYield(b) && MayYield(a) ? b :
                a.Preferred.sqrMagnitude>.0001f&&b.Preferred.sqrMagnitude<.0001f?a:
                b.Preferred.sqrMagnitude>.0001f&&a.Preferred.sqrMagnitude<.0001f?b:a;
            var loser = winner == a ? b : a;
            if (!TryYield(loser, winner)) { winner.Reason = loser.Reason = "NoYieldSpace"; }
        }
        foreach (var a in order)
        {
            if (a.Human || a.Motion == null) continue;
            if (!a.YieldRequested) a.Yielding = false;
            if (a.Yielding)
            {
                var offset = a.YieldGoal - a.Position;
                if (offset.magnitude < .04f) { a.Yielding = false; a.Ticket = a.Access ? a.Ticket : 0; }
                else { a.Resolved = offset.normalized * Mathf.Min(Mathf.Max(1.6f, a.Preferred.magnitude), offset.magnitude / delta); a.State = a.Selected ? MovementResolutionState.Yielding : MovementResolutionState.Settling; }
            }
            a.Resolved = a.Motion.Bound(a.Resolved, delta);
            if (!a.Ready || !TileMover.CanTraverseBox(terrain, a.Position, a.Position + a.Resolved * delta, a.Extents))
            { a.Resolved = Vector2.zero; a.State = MovementResolutionState.Blocked; a.Reason = "TerrainOrRegionNotReady"; }
            if (a.Access && !a.Granted && !a.Yielding) { a.Resolved = Vector2.zero; a.State = MovementResolutionState.QueuedForAccess; }
        }
        // Removing one motion can invalidate a follower behind it. Recheck to a fixed point;
        // candidates are only removed here, never replaced by another escape heuristic.
        for (int pass = 0; pass <= order.Count; pass++)
        {
            bool changed = false;
            for (int i = 0; i < order.Count; i++) for (int j = i + 1; j < order.Count; j++)
            {
                var a = order[i]; var b = order[j]; if (!Cooperative(a, b) || PairClear(a, a.Resolved, b, b.Resolved, delta)) continue;
                if (!a.Human && PairClear(a, Vector2.zero, b, b.Resolved, delta)) { changed |= Stop(a); }
                else if (!b.Human && PairClear(a, a.Resolved, b, Vector2.zero, delta)) { changed |= Stop(b); }
                else { if (!a.Human) changed |= Stop(a); if (!b.Human) changed |= Stop(b); }
            }
            if (!changed) break;
        }
        Applying = true;
        try
        {
            foreach (var a in order)
            {
                if (!a.Human && a.Motion != null) a.Motion.ApplyResolved(tick, a.Resolved, a.Preferred, a.State);
                foreach (var b in order) if (a != b && Soft(a, b)) MaximumCampPenetration = Mathf.Max(MaximumCampPenetration, MovementContactPolicy.Penetration(a.Position - b.Position, a.Extents + b.Extents));
            }
        }
        finally { Applying = false; }
        Resolutions++;
    }
    bool Stop(Actor a)
    {
        bool changed = a.Resolved.sqrMagnitude > .0000001f; a.Resolved = Vector2.zero;
        a.State = MovementResolutionState.QueuedForAccess; a.Reason = a.Reason ?? "PhysicalPeerContact"; Reconsidered++; return changed;
    }
    bool PairClear(Actor a, Vector2 av, Actor b, Vector2 bv, float delta)
        => MovementContactPolicy.SweepClear(a.Position - b.Position, (av - bv) * delta, a.Extents + b.Extents + (Vector2.one * Physics2D.defaultContactOffset * 2), MovementContactPolicy.Allowance(a.Extents, b.Extents, Soft(a, b)));
    bool MayYield(Actor a)
        => a.Ready && !a.Human && a.Motion != null && a.Motion.isActiveAndEnabled && (!a.Access || !a.Occupied) &&
           (a.Selected ? a.Motion.MayYield : a.IdleYield && a.Role != MovementContactRole.Strict);
    void ResolveAccess(Actor a)
    {
        Reconsidered++;
        foreach (var other in order)
        {
            if (other == a || !other.Access || other.Ticket >= a.Ticket) continue;
            if (MovementContactPolicy.Penetration(a.AccessGoal - other.AccessGoal, a.Extents + other.Extents) > 0) { a.Reason = "OlderAccessClaim"; return; }
        }
        bool clear = true;
        foreach (var other in order)
        {
            if (other == a || !Cooperative(a, other)) continue;
            if (MovementContactPolicy.Penetration(a.AccessGoal - other.Position, a.Extents + other.Extents + Vector2.one * .02f) <= 0) continue;
            clear = false; TryYield(other, a);
        }
        if (clear && destinations.ClaimExact(a.Binding, a.AccessKey, a.AccessGoal)) { a.Granted = true; a.Reason = null; a.YieldRequested = false; }
        else a.Reason = "OccupiedWorkApproach";
    }
    bool TryYield(Actor a, Actor requester)
    {
        if (!MayYield(a)) return false; a.YieldRequested = true;
        if (a.Yielding && TileMover.CanTraverseBox(terrain, a.Position, a.YieldGoal, a.Extents)) return true;
        a.Yielding = false;
        Vector2 heading = requester.Access ? requester.AccessGoal - requester.Position : requester.Preferred;
        if (heading.sqrMagnitude < .0001f) heading = a.Position - requester.Position;
        heading.Normalize(); if (heading.sqrMagnitude < .1f) heading = Vector2.right;
        Vector2 side = new Vector2(heading.y, -heading.x);
        float distance = Mathf.Max(a.Extents.x + requester.Extents.x, a.Extents.y + requester.Extents.y) + .06f;
        float best = float.PositiveInfinity; Vector2 selected = default;
        for (int option = 0; option < 16; option++)
        {
            float angle = (option%8) * Mathf.PI * .25f; Vector2 direction = side * Mathf.Cos(angle) + heading * Mathf.Sin(angle);
            var goal = a.Position + direction * distance*(option<8?.5f:1);
            if (!TileMover.CanTraverseBox(terrain, a.Position, goal, a.Extents) || !destinations.CanPlace(a.Binding, goal, a.Extents)) continue;
            var brain = a.Binding.GetComponent<BuddyBrain>(); if (brain != null && !brain.AllowsDestination(goal)) continue;
            bool free = true;
            foreach (var other in order)
            {
                if (other == a) continue;
                if (!MovementContactPolicy.SweepClear(a.Position - other.Position, goal - a.Position, a.Extents + other.Extents, MovementContactPolicy.Allowance(a.Extents, other.Extents, Soft(a, other)))) { free = false; break; }
                if (MovementContactPolicy.Penetration(goal - other.Position, a.Extents + other.Extents + Vector2.one * .02f) > 0) { free = false; break; }
            }
            if (!free) continue;
            if(requester.Access&&MovementContactPolicy.Penetration(goal-requester.AccessGoal,a.Extents+requester.Extents+Vector2.one*.02f)>0)continue;
            float clearance = requester.Access ? Vector2.Distance(goal, requester.AccessGoal) : Mathf.Abs(Vector2.Dot(goal - requester.Position, side));
            float cost = Vector2.Distance(a.Position,goal) - clearance * .25f + option * .0001f;
            if (cost < best) { best = cost; selected = goal; }
        }
        if (float.IsPositiveInfinity(best)) return false;
        if (a.Camp && !a.Selected && !destinations.RelocateStanding(a.Binding, selected))return false;
        a.YieldGoal = selected; a.Yielding = true; if(a.Ticket==0)a.Ticket=++nextTicket; return true;
    }
    // The human motor stays authoritative. It may not rely on a promised future NPC yield.
    public Vector2 ConstrainExternal(ActorTerrainBinding binding, Vector2 wanted, float radius)
    {
        ObserveExternal(binding, wanted);
        if (Applying || !byActor.TryGetValue(binding, out var a) || !a.Camp || Role(binding) == MovementContactRole.Strict) return wanted;
        Vector2 size = TileMover.GetMapClearanceExtents(a.Body, radius);
        foreach (var other in actors)
        {
            if (other == a || !Live(other) || !Soft(a, other)) continue;
            var sum = size + TileMover.GetMapClearanceExtents(other.Body, other.Binding.MinimumBodyRadius);
            if (!MovementContactPolicy.SweepClear(a.Body.position - other.Body.position, wanted * Time.fixedDeltaTime, sum,
                MovementContactPolicy.Allowance(size, TileMover.GetMapClearanceExtents(other.Body, other.Binding.MinimumBodyRadius), true))) return Vector2.zero;
        }
        return wanted;
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        foreach (var pair in contacts) pair.Apply(false);
        foreach (var a in actors) if (a.Binding != null && ReferenceEquals(a.Binding.Movement, this)) a.Binding.Movement = null;
        destinations.Dispose(); contacts.Clear(); actors.Clear(); byActor.Clear(); order.Clear();
    }
}



