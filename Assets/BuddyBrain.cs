using UnityEngine;

[RequireComponent(typeof(BuddyFollow))]
public class BuddyBrain : MonoBehaviour
{
    [Header("Task Priority")]
    public bool allowCombat = true;
    public bool allowScavenging = true;
    public bool allowFollowing = true;

    [Header("Debug")]
    public string currentTask = "Follow";

    private BuddyUnit unit;
    private BuddyFollow follow;
    private BuddyCombat combat;
    private BuddyScavenger scavenger;

    void Awake()
    {
        unit = GetComponent<BuddyUnit>();
        follow = GetComponent<BuddyFollow>();
        combat = GetComponent<BuddyCombat>();
        scavenger = GetComponent<BuddyScavenger>();
    }

    void Update()
    {
        UpdateTaskPermissions();
    }

    void FixedUpdate()
    {
        UpdateTaskPermissions();
    }

    void UpdateTaskPermissions()
    {
        bool canCombat = allowCombat && combat != null && combat.enabled && combat.WantsControl();
        bool canScavenge = allowScavenging && scavenger != null && scavenger.enabled && scavenger.WantsControl();
        bool canFollow = allowFollowing && follow != null && follow.enabled;

        // Priority: Combat > Scavenge > Follow.
        if (canCombat)
        {
            currentTask = "Combat";
            SetMovementPermissions(false, true, false);
            return;
        }

        if (canScavenge)
        {
            currentTask = "Scavenge";
            SetMovementPermissions(false, false, true);
            return;
        }

        if (canFollow)
        {
            currentTask = "Follow";
            SetMovementPermissions(true, false, false);
            return;
        }

        currentTask = "Idle";
        SetMovementPermissions(false, false, false);
    }

    void SetMovementPermissions(bool followCanMove, bool combatCanMove, bool scavengerCanMove)
    {
        if (follow != null)
            follow.brainAllowsMovement = followCanMove;

        if (combat != null)
            combat.brainAllowsMovement = combatCanMove;

        if (scavenger != null)
            scavenger.brainAllowsMovement = scavengerCanMove;
    }
}
