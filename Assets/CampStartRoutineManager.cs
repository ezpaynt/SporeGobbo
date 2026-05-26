using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CampBedExpansionStep
{
    [Header("Unlock")]
    public string stationId = "beds_1";
    public int requiredOwnedBuddies = 1;

    [Header("Scene Objects")]
    [Tooltip("Object that becomes visible after this expansion unlocks. Usually an empty parent with bed art/anchors inside.")]
    public GameObject hiddenAreaToReveal;
    [Tooltip("Optional dirt/cover/placeholder object that should disappear when the beds are revealed.")]
    public GameObject coverObjectToHideWhenRevealed;
    public Transform digWalkTarget;
    public Transform[] bedAnchors;

    [Header("Popup / Voice")]
    [TextArea(2, 5)] public string popupText = "The little guys need somewhere to sleep.";
    public AudioClip voiceLine;
    [Tooltip("How long the first expansion message stays visible before the digging walk starts.")]
    public float firstMessageSeconds = 1.25f;

    [Header("Timing")]
    [Tooltip("How long the buddies get to walk up to the blocked bed spot before the digging moment starts.")]
    public float maxWaitForBuddiesToArrive = 4f;
    [Tooltip("Even if buddies arrive instantly, wait this long so the walk/dig moment can be seen.")]
    public float minimumWalkTimeBeforeDig = 1.5f;
    [Tooltip("How close most buddies need to get before digging can begin.")]
    public float arriveDistance = 0.85f;
    [Range(0.1f, 1f)] public float percentNeededToArrive = 0.65f;
    [Tooltip("How long they scratch/dig at the spot before the bed area reveals.")]
    public float digDuration = 2f;

    [Header("Digging Message")]
    [TextArea(2, 5)] public string diggingPopupText = "The little guys start clawing at the dirt.";
    public AudioClip diggingVoiceLine;
}

public class CampStartRoutineManager : MonoBehaviour
{
    public static CampStartRoutineManager Instance { get; private set; }

    [Header("Camp Start Routine")]
    public bool runRoutineOnCampOpen = true;
    public bool sendBuddiesToFireOnNormalVisits = true;
    public bool waitForFireRecoveryBeforeFreeWander = true;
    [Tooltip("After a new bed area is revealed, send the buddies back to the fire instead of immediately free wandering.")]
    public bool sendToFireAfterBedExpansion = true;

    [Header("Core Locations")]
    public Transform fireGatherPoint;
    public Transform[] defaultWanderAnchors;

    [Header("Bed Expansions")]
    public List<CampBedExpansionStep> bedExpansionSteps = new List<CampBedExpansionStep>();

    [Header("Messages")]
    [TextArea(2, 5)] public string normalReturnPopup = "The camp is quiet enough. Go sit by the fire when you're ready.";
    [TextArea(2, 5)] public string afterBedRevealPopup = "Beds scratched out. Now everyone is hungry. Sit by the fire and feed the camp.";
    [TextArea(2, 5)] public string afterRecoveryPopup = "Bellies full. Everyone starts wandering again.";
    public AudioSource voiceSource;

    [Header("Movement")]
    [Tooltip("Small delay after camp spawn so spawned buddies exist before the routine checks them.")]
    public float startRoutineDelay = 0.25f;
    public float directedWalkSpeed = 1.5f;
    public float fireWanderRadius = 0.9f;
    public float bedWanderRadius = 1.4f;

    private bool routineStarted = false;
    private bool waitingForRecovery = false;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        ApplyUnlockedAreaVisibility();
    }

    public void BeginCampVisit()
    {
        if (!runRoutineOnCampOpen || routineStarted)
            return;

        routineStarted = true;
        ApplyUnlockedAreaVisibility();
        StartCoroutine(CampOpenRoutine());
    }

    IEnumerator CampOpenRoutine()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, startRoutineDelay));
        yield return WaitForSpawnedBuddiesIfAny();

        CampBedExpansionStep expansion = GetNextNeededExpansion();

        if (expansion != null)
        {
            yield return RunBedExpansion(expansion);
            yield break;
        }

        if (sendBuddiesToFireOnNormalVisits && fireGatherPoint != null)
        {
            CampMessageUI.Show(normalReturnPopup);
            SendAllBuddiesToTemporaryAnchor(fireGatherPoint, fireWanderRadius, true);
            waitingForRecovery = waitForFireRecoveryBeforeFreeWander;
        }
        else
        {
            ReleaseBuddiesToBedsOrDefaultAnchors();
        }
    }

    IEnumerator RunBedExpansion(CampBedExpansionStep step)
    {
        string firstMessage = string.IsNullOrWhiteSpace(step.popupText)
            ? "The little guys need somewhere to sleep."
            : step.popupText;

        CampMessageUI.Show(firstMessage);
        PlayVoice(step.voiceLine);

        if (step.firstMessageSeconds > 0f)
            yield return new WaitForSeconds(step.firstMessageSeconds);

        Transform target = step.digWalkTarget;
        if (target == null)
        {
            Debug.LogWarning("Camp bed expansion has no Dig Walk Target assigned for station: " + step.stationId + ". Assign a real scene object in the Inspector.", this);
            yield break;
        }

        SendAllBuddiesDirected(target);
        yield return WaitForBuddiesToReachTarget(target, step.maxWaitForBuddiesToArrive, step.minimumWalkTimeBeforeDig, step.arriveDistance, step.percentNeededToArrive);

        if (!string.IsNullOrWhiteSpace(step.diggingPopupText))
            CampMessageUI.Show(step.diggingPopupText);

        PlayVoice(step.diggingVoiceLine);
        yield return new WaitForSeconds(Mathf.Max(0f, step.digDuration));

        if (step.hiddenAreaToReveal != null)
            step.hiddenAreaToReveal.SetActive(true);

        if (step.coverObjectToHideWhenRevealed != null)
            step.coverObjectToHideWhenRevealed.SetActive(false);

        UnlockStation(step.stationId);

        if (sendToFireAfterBedExpansion && fireGatherPoint != null)
        {
            CampMessageUI.Show(afterBedRevealPopup);
            SendAllBuddiesToTemporaryAnchor(fireGatherPoint, fireWanderRadius, true);
            waitingForRecovery = waitForFireRecoveryBeforeFreeWander;
        }
        else
        {
            ReleaseBuddiesToBedStep(step);
            waitingForRecovery = false;
        }
    }

    IEnumerator WaitForSpawnedBuddiesIfAny()
    {
        if (GameState.Instance == null || GameState.Instance.ownedBuddies == null || GameState.Instance.ownedBuddies.Count == 0)
            yield break;

        float timer = 0f;
        while (timer < 2f)
        {
            BuddyUnit[] buddies = Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None);
            if (buddies.Length > 0)
                yield break;

            timer += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator WaitForBuddiesToReachTarget(Transform target, float maxWait, float minimumWalkTime, float arriveDistance, float percentNeeded)
    {
        if (target == null)
            yield break;

        float timer = 0f;
        maxWait = Mathf.Max(0.1f, maxWait);
        minimumWalkTime = Mathf.Max(0f, minimumWalkTime);
        arriveDistance = Mathf.Max(0.05f, arriveDistance);
        percentNeeded = Mathf.Clamp01(percentNeeded);

        while (timer < maxWait)
        {
            BuddyUnit[] buddies = Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None);

            if (buddies.Length == 0)
            {
                timer += Time.deltaTime;
                yield return null;
                continue;
            }

            int arrived = 0;
            foreach (BuddyUnit buddy in buddies)
            {
                if (buddy == null)
                {
                    arrived++;
                    continue;
                }

                float distance = Vector2.Distance(buddy.transform.position, target.position);
                if (distance <= arriveDistance)
                    arrived++;
            }

            int needed = Mathf.Max(1, Mathf.CeilToInt(buddies.Length * percentNeeded));

            // This keeps the moment readable: they must at least spend a little time
            // walking/scratching before the bed reveal can fire.
            if (timer >= minimumWalkTime && arrived >= needed)
                yield break;

            timer += Time.deltaTime;
            yield return null;
        }
    }

    public void NotifyFireRecovered()
    {
        if (!waitingForRecovery)
            return;

        waitingForRecovery = false;
        CampMessageUI.Show(afterRecoveryPopup);
        ReleaseBuddiesToBedsOrDefaultAnchors();
    }

    CampBedExpansionStep GetNextNeededExpansion()
    {
        if (GameState.Instance == null)
            return null;

        int buddyCount = GameState.Instance.ownedBuddies != null ? GameState.Instance.ownedBuddies.Count : 0;

        foreach (CampBedExpansionStep step in bedExpansionSteps)
        {
            if (step == null || string.IsNullOrWhiteSpace(step.stationId))
                continue;

            if (buddyCount >= step.requiredOwnedBuddies && !IsStationUnlocked(step.stationId))
                return step;
        }

        return null;
    }

    void ApplyUnlockedAreaVisibility()
    {
        foreach (CampBedExpansionStep step in bedExpansionSteps)
        {
            if (step == null)
                continue;

            bool unlocked = IsStationUnlocked(step.stationId);

            if (step.hiddenAreaToReveal != null)
                step.hiddenAreaToReveal.SetActive(unlocked);

            if (step.coverObjectToHideWhenRevealed != null)
                step.coverObjectToHideWhenRevealed.SetActive(!unlocked);
        }
    }

    void SendAllBuddiesDirected(Transform target)
    {
        if (target == null)
            return;

        BuddyUnit[] buddies = Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None);
        foreach (BuddyUnit buddy in buddies)
        {
            if (buddy == null)
                continue;

            CampWander wander = buddy.GetComponent<CampWander>();
            float speed = directedWalkSpeed;
            if (buddy.data != null)
                speed = Mathf.Max(0.2f, buddy.data.moveSpeed * 0.45f);

            CampDirectedWalk walker = buddy.GetComponent<CampDirectedWalk>();
            if (walker == null)
                walker = buddy.gameObject.AddComponent<CampDirectedWalk>();

            walker.BeginWalk(target, speed);

            if (wander != null)
                wander.enabled = false;
        }
    }

    void SendAllBuddiesToTemporaryAnchor(Transform anchor, float radius, bool disableFreeWanderUntilArrived)
    {
        BuddyUnit[] buddies = Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None);
        foreach (BuddyUnit buddy in buddies)
        {
            if (buddy == null)
                continue;

            CampWander wander = buddy.GetComponent<CampWander>();
            if (wander == null)
                wander = buddy.gameObject.AddComponent<CampWander>();

            float speed = buddy.data != null ? Mathf.Max(0.2f, buddy.data.moveSpeed * 0.45f) : directedWalkSpeed;
            wander.SetAnchor(anchor, radius, speed);
            wander.enabled = !disableFreeWanderUntilArrived;

            CampDirectedWalk walker = buddy.GetComponent<CampDirectedWalk>();
            if (walker == null)
                walker = buddy.gameObject.AddComponent<CampDirectedWalk>();

            walker.BeginWalk(anchor, speed);
        }
    }

    void ReleaseBuddiesToBedsOrDefaultAnchors()
    {
        CampBedExpansionStep bestStep = GetHighestUnlockedBedStepWithAnchors();

        if (bestStep != null)
        {
            ReleaseBuddiesToBedStep(bestStep);
            return;
        }

        ReleaseBuddiesToAnchors(defaultWanderAnchors, bedWanderRadius);
    }

    void ReleaseBuddiesToBedStep(CampBedExpansionStep step)
    {
        if (step != null && step.bedAnchors != null && step.bedAnchors.Length > 0)
            ReleaseBuddiesToAnchors(step.bedAnchors, bedWanderRadius);
        else
            ReleaseBuddiesToAnchors(defaultWanderAnchors, bedWanderRadius);
    }

    void ReleaseBuddiesToAnchors(Transform[] anchors, float radius)
    {
        BuddyUnit[] buddies = Object.FindObjectsByType<BuddyUnit>(FindObjectsSortMode.None);
        for (int i = 0; i < buddies.Length; i++)
        {
            BuddyUnit buddy = buddies[i];
            if (buddy == null)
                continue;

            CampWander wander = buddy.GetComponent<CampWander>();
            if (wander == null)
                wander = buddy.gameObject.AddComponent<CampWander>();

            Transform anchor = GetAnchor(anchors, i);
            float speed = buddy.data != null ? Mathf.Max(0.2f, buddy.data.moveSpeed * 0.45f) : directedWalkSpeed;
            wander.SetAnchor(anchor, radius, speed);
            wander.enabled = true;
        }
    }

    CampBedExpansionStep GetHighestUnlockedBedStepWithAnchors()
    {
        CampBedExpansionStep best = null;

        foreach (CampBedExpansionStep step in bedExpansionSteps)
        {
            if (step == null || step.bedAnchors == null || step.bedAnchors.Length == 0)
                continue;

            if (IsStationUnlocked(step.stationId))
                best = step;
        }

        return best;
    }

    Transform GetAnchor(Transform[] anchors, int index)
    {
        if (anchors == null || anchors.Length == 0)
            return fireGatherPoint != null ? fireGatherPoint : transform;

        int safeIndex = Mathf.Abs(index) % anchors.Length;
        return anchors[safeIndex] != null ? anchors[safeIndex] : transform;
    }

    bool IsStationUnlocked(string id)
    {
        if (GameState.Instance == null || GameState.Instance.unlockedStations == null)
            return false;

        return GameState.Instance.unlockedStations.Contains(id);
    }

    void UnlockStation(string id)
    {
        if (GameState.Instance == null || string.IsNullOrWhiteSpace(id))
            return;

        if (GameState.Instance.unlockedStations == null)
            GameState.Instance.unlockedStations = new List<string>();

        if (!GameState.Instance.unlockedStations.Contains(id))
            GameState.Instance.unlockedStations.Add(id);
    }

    void PlayVoice(AudioClip clip)
    {
        if (clip == null || voiceSource == null)
            return;

        voiceSource.clip = clip;
        voiceSource.Play();
    }
}
