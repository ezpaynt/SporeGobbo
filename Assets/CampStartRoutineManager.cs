using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CampBedExpansionStep
{
    [Header("Unlock")]
    public string stationId = "beds_1";
    public int requiredOwnedBuddies = 1;

    [Header("Required Scene Objects")]
    [Tooltip("Scene object that becomes visible after this expansion unlocks. Usually a parent with bed art/anchors inside.")]
    public GameObject hiddenAreaToReveal;
    [Tooltip("Scene object that disappears when this expansion unlocks. Usually dirt/cover art.")]
    public GameObject coverObjectToHideWhenRevealed;
    [Tooltip("Real scene object buddies walk to before the beds reveal. This is never auto-created.")]
    public Transform digWalkTarget;
    [Tooltip("Real scene anchors buddies wander around after recovery.")]
    public Transform[] bedAnchors;

    [Header("Popup / Voice")]
    [TextArea(2, 5)] public string popupText = "The little guys need somewhere to sleep.";
    public AudioClip voiceLine;
    [Tooltip("Seconds to let the first popup breathe before the digging message can replace it.")]
    public float firstPopupSeconds = 1.5f;

    [Header("Timing")]
    [Tooltip("How long buddies get to walk up to the blocked bed spot before the digging moment starts.")]
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
    [Tooltip("After a new bed area is revealed, send buddies to the fire instead of immediately free wandering.")]
    public bool sendToFireAfterBedExpansion = true;

    [Header("Required Scene Locations")]
    [Tooltip("Real scene object used as the temporary fire gather anchor.")]
    public Transform fireGatherPoint;
    [Tooltip("Real scene anchors used if no bed anchors are available yet.")]
    public Transform[] defaultWanderAnchors;

    [Header("Bed Expansions")]
    public List<CampBedExpansionStep> bedExpansionSteps = new List<CampBedExpansionStep>();

    [Header("Messages")]
    [TextArea(2, 5)] public string normalReturnPopup = "The camp is quiet enough. Go sit by the fire when you're ready.";
    [TextArea(2, 5)] public string afterBedRevealPopup = "Beds scratched out. Now everyone is hungry. Sit by the fire and feed the camp.";
    [TextArea(2, 5)] public string afterRecoveryPopup = "Bellies full. Everyone starts wandering again.";
    public AudioSource voiceSource;

    [Header("Movement")]
    public float startRoutineDelay = 0.25f;
    public float directedWalkSpeed = 1.5f;
    public float fireWanderRadius = 0.9f;
    public float bedWanderRadius = 1.4f;

    private bool routineStarted;
    private bool waitingForRecovery;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        ApplyUnlockedAreaVisibility();
        ValidateSceneReferences();
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
        if (!ValidateBedStep(step))
        {
            CampMessageUI.Show("Camp bed expansion is missing scene references.");
            yield break;
        }

        CampMessageUI.Show(step.popupText);
        PlayVoice(step.voiceLine);

        SendAllBuddiesDirected(step.digWalkTarget);

        float firstPopupSeconds = Mathf.Max(0f, step.firstPopupSeconds);
        if (firstPopupSeconds > 0f)
            yield return new WaitForSeconds(firstPopupSeconds);

        yield return WaitForBuddiesToReachTarget(
            step.digWalkTarget,
            step.maxWaitForBuddiesToArrive,
            step.minimumWalkTimeBeforeDig,
            step.arriveDistance,
            step.percentNeededToArrive
        );

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

    bool ValidateBedStep(CampBedExpansionStep step)
    {
        if (step == null)
            return false;

        bool ok = true;
        if (string.IsNullOrWhiteSpace(step.stationId))
        {
            Debug.LogWarning("CampStartRoutineManager bed expansion has no Station Id.", this);
            ok = false;
        }

        if (step.digWalkTarget == null)
        {
            Debug.LogWarning("CampStartRoutineManager bed expansion '" + step.stationId + "' has no Dig Walk Target. Place a real scene object and assign it.", this);
            ok = false;
        }

        if (step.hiddenAreaToReveal == null)
            Debug.LogWarning("CampStartRoutineManager bed expansion '" + step.stationId + "' has no Hidden Area To Reveal assigned.", this);

        if (step.coverObjectToHideWhenRevealed == null)
            Debug.LogWarning("CampStartRoutineManager bed expansion '" + step.stationId + "' has no Cover Object To Hide assigned.", this);

        return ok;
    }

    void ValidateSceneReferences()
    {
        if (fireGatherPoint == null)
            Debug.LogWarning("CampStartRoutineManager needs a real Fire Gather Point assigned. It will not auto-create one.", this);

        foreach (CampBedExpansionStep step in bedExpansionSteps)
            ValidateBedStep(step);
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

                if (Vector2.Distance(buddy.transform.position, target.position) <= arriveDistance)
                    arrived++;
            }

            int needed = Mathf.Max(1, Mathf.CeilToInt(buddies.Length * percentNeeded));
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

        GameState.Instance.RepairRosterState();
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
            float speed = buddy.data != null ? Mathf.Max(0.2f, buddy.data.moveSpeed * 0.45f) : directedWalkSpeed;

            CampDirectedWalk walker = buddy.GetComponent<CampDirectedWalk>();
            if (walker == null)
                walker = buddy.gameObject.AddComponent<CampDirectedWalk>();

            walker.enabled = true;
            walker.BeginWalk(target, speed);

            if (wander != null)
                wander.enabled = false;
        }
    }

    void SendAllBuddiesToTemporaryAnchor(Transform anchor, float radius, bool disableFreeWanderUntilArrived)
    {
        if (anchor == null)
            return;

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

            walker.enabled = true;
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

        voiceSource.PlayOneShot(clip);
    }
}
