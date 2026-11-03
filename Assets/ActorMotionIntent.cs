using UnityEngine;

/// <summary>Per-actor intent buffer and one application path. Traffic is decided by the region.</summary>
public sealed class ActorMotionIntent : MonoBehaviour
{
    Rigidbody2D body;
    ActorTerrainBinding binding;
    GobboVisualController visual;
    MonoBehaviour owner;
    Vector2 velocity;
    float radius;
    int priority;
    bool mayYield;
    double stamp = double.NegativeInfinity, lastApplied = double.NegativeInfinity;
    public long Applications { get; private set; }
    public long ContestedIntents { get; private set; }
    public double FriendlyDelaySeconds { get; private set; }
    public bool WaitingForFriendlySpace { get; private set; }
    public MovementResolutionState State { get; private set; }
    public Vector2 RequestedVelocity { get; private set; }
    public string ActiveTaskName { get; private set; } = "None";
    public MonoBehaviour SelectedTask => owner;
    public bool MayYield => mayYield && priority < 100;
    public string DiagnosticTaskName => owner != null && stamp == Time.fixedTimeAsDouble ? owner.GetType().Name : "None";

    void Awake()
    {
        body = GetComponent<Rigidbody2D>(); binding = GetComponent<ActorTerrainBinding>();
        visual = GetComponent<GobboVisualController>() ?? GetComponentInChildren<GobboVisualController>();
    }
    public static void Cancel(GameObject actor)
    {
        var motion = actor.GetComponent<ActorMotionIntent>(); if (motion == null) return;
        motion.owner = null; motion.stamp = double.NegativeInfinity;
        motion.WaitingForFriendlySpace = false; motion.RequestedVelocity = Vector2.zero;
        motion.ActiveTaskName = "None"; motion.State = MovementResolutionState.Idle;
        motion.binding?.Movement?.CancelInput(motion.binding);
    }
    public static void Submit(MonoBehaviour task, Rigidbody2D body, Vector2 velocity, float radius, int priority, bool mayYield = true)
    {
        var binding = body.GetComponent<ActorTerrainBinding>();
        if (binding == null || binding.RequestRoute == null) { TileMover.Move(body, velocity, radius); return; }
        if (binding.Movement == null) { body.linearVelocity = Vector2.zero; return; }
        var intent = body.GetComponent<ActorMotionIntent>() ?? body.gameObject.AddComponent<ActorMotionIntent>();
        if (intent.stamp != Time.fixedTimeAsDouble) { intent.stamp = Time.fixedTimeAsDouble; intent.owner = null; intent.priority = int.MinValue; }
        if (intent.owner != null && intent.owner != task) intent.ContestedIntents++;
        if (intent.owner != null && (priority < intent.priority || priority == intent.priority && task.GetInstanceID() > intent.owner.GetInstanceID())) return;
        intent.owner = task; intent.velocity = velocity; intent.radius = radius; intent.priority = priority; intent.mayYield = mayYield;
        binding.Movement.Observe(binding, intent);
    }
    public bool ReadSelected(double tick, out Vector2 preferred, out float bodyRadius, out int taskPriority)
    {
        if (body == null) Awake();
        bool valid = isActiveAndEnabled && owner != null && owner.isActiveAndEnabled && stamp == tick;
        preferred = valid ? velocity : Vector2.zero; bodyRadius = radius; taskPriority = valid ? priority : 0;
        return valid;
    }
    public Vector2 Bound(Vector2 wanted, float delta)
    {
        var brain = GetComponent<BuddyBrain>(); return brain != null ? brain.BoundVelocity(body.position, wanted, delta) : wanted;
    }
    public void ApplyResolved(double tick, Vector2 wanted, Vector2 preferred, MovementResolutionState state)
    {
        if (lastApplied == tick || !isActiveAndEnabled) return;
        if (body == null) Awake(); lastApplied = tick;
        RequestedVelocity = preferred; ActiveTaskName = owner != null && stamp == tick ? owner.GetType().Name : "None";
        State = state; WaitingForFriendlySpace = state == MovementResolutionState.QueuedForAccess || state == MovementResolutionState.Yielding || state == MovementResolutionState.Settling || state == MovementResolutionState.WaitingForPeer || state == MovementResolutionState.NoLocalYieldPlan;
        if (preferred.sqrMagnitude > .000001f) FriendlyDelaySeconds += FriendlyDelayFraction(preferred, wanted) * Time.fixedDeltaTime;
        TileMover.Move(body, wanted, radius); Applications++;
        if(state==MovementResolutionState.WaitingForPeer||state==MovementResolutionState.NoLocalYieldPlan)visual?.SetAnimationState(GobboAnimationState.Idle);
        if (wanted.sqrMagnitude > .0001f) visual?.SetDirection(wanted);
    }
    public static float FriendlyDelayFraction(Vector2 requested, Vector2 applied)
        => requested.sqrMagnitude <= .000001f ? 0 : Mathf.Clamp01(1 - Vector2.Dot(applied, requested) / requested.sqrMagnitude);
    void OnDisable() { owner = null; stamp = double.NegativeInfinity; binding?.Movement?.CancelInput(binding); }
}
