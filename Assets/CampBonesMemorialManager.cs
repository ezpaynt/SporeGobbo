using System.Collections;
using UnityEngine;

/// <summary>
/// Camp ritual controller for the Old Bones Wall.
/// On camp entry, it pulls lastRun.deadBuddyNames into permanent CampDeathHistoryStore.
/// If there are unseen deaths, it reveals the wall, shows memorial messages, briefly freezes the player,
/// then marks those memorials as seen.
/// </summary>
public class CampBonesMemorialManager : MonoBehaviour
{
    [Header("Bones Wall")]
    public GameObject bonesWallObject;
    public CampOldBonesWall bonesWall;
    public bool hideWallUntilFirstDeath = true;

    [Header("Ritual Timing")]
    public bool runOnStart = true;
    public float startDelay = 0.45f;
    public float freezeSeconds = 2.0f;
    public bool freezePlayerDuringRitual = true;

    [Header("Messages")]
    [TextArea(2, 4)] public string firstDeathMessage = "Someone should remember them.";
    [TextArea(2, 4)] public string addedBoneMessage = "You press a little bone into the wall.";
    [TextArea(2, 4)] public string repeatDeathMessage = "The camp grows quiet. Another bone belongs on the wall.";
    [TextArea(2, 4)] public string repeatAddedBoneMessage = "Another little bone joins the others.";

    [Header("Optional Attention Marker")]
    public GameObject attentionMarker;
    public float markerSeconds = 4f;

    private bool ritualRunning = false;

    void Start()
    {
        AutoFillReferences();
        SyncWallVisibility();

        if (runOnStart)
            StartCoroutine(StartAfterDelay());
    }

    IEnumerator StartAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, startDelay));
        RunMemorialCheck();
    }

    [ContextMenu("Run Memorial Check")]
    public void RunMemorialCheck()
    {
        if (ritualRunning)
            return;

        AutoFillReferences();

        CampDeathHistoryStore store = CampDeathHistoryStore.GetOrCreate();
        if (store == null)
            return;

        ImportLastRunDeaths(store);
        SyncWallVisibility();

        if (store.HasUnseenMemorials())
            StartCoroutine(MemorialRoutine(store));
    }

    void AutoFillReferences()
    {
        if (bonesWall == null && bonesWallObject != null)
            bonesWall = bonesWallObject.GetComponent<CampOldBonesWall>();

        if (bonesWall == null)
            bonesWall = Object.FindAnyObjectByType<CampOldBonesWall>(FindObjectsInactive.Include);

        if (bonesWallObject == null && bonesWall != null)
            bonesWallObject = bonesWall.gameObject;

        if (attentionMarker != null)
            attentionMarker.SetActive(false);
    }

    void ImportLastRunDeaths(CampDeathHistoryStore store)
    {
        if (GameState.Instance == null || GameState.Instance.lastRun == null)
            return;

        RunSummaryData run = GameState.Instance.lastRun;
        int runNumber = Mathf.Max(1, run.runNumber);

        if (run.deadBuddyNames == null || run.deadBuddyNames.Count == 0)
            return;

        foreach (string deadName in run.deadBuddyNames)
            store.AddFromLabel(deadName, runNumber, "Lost in the caves");
    }

    void SyncWallVisibility()
    {
        CampDeathHistoryStore store = CampDeathHistoryStore.GetOrCreate();
        bool hasDeaths = store != null && store.HasAnyDeaths();

        if (bonesWallObject != null && hideWallUntilFirstDeath)
            bonesWallObject.SetActive(hasDeaths);

        if (bonesWall != null)
            bonesWall.RefreshWallVisibility();
    }

    IEnumerator MemorialRoutine(CampDeathHistoryStore store)
    {
        ritualRunning = true;

        bool hadExistingSeenDeath = false;
        foreach (DeadBuddyRecord record in store.deadBuddyHistory)
        {
            if (record != null && record.memorialSeen)
            {
                hadExistingSeenDeath = true;
                break;
            }
        }

        if (bonesWallObject != null)
            bonesWallObject.SetActive(true);

        if (bonesWall != null)
            bonesWall.ForceShowWall();

        GobboController player = Object.FindAnyObjectByType<GobboController>();
        bool oldPlayerEnabled = true;
        Rigidbody2D rb = null;

        if (freezePlayerDuringRitual && player != null)
        {
            oldPlayerEnabled = player.enabled;
            player.enabled = false;

            rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
        }

        if (attentionMarker != null)
            attentionMarker.SetActive(true);

        CampMessageUI.Show(hadExistingSeenDeath ? repeatDeathMessage : firstDeathMessage);

        yield return new WaitForSeconds(Mathf.Max(0.1f, freezeSeconds * 0.55f));

        CampMessageUI.Show(hadExistingSeenDeath ? repeatAddedBoneMessage : addedBoneMessage);

        yield return new WaitForSeconds(Mathf.Max(0.1f, freezeSeconds * 0.45f));

        store.MarkAllSeen();

        if (attentionMarker != null)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, markerSeconds));
            attentionMarker.SetActive(false);
        }

        if (freezePlayerDuringRitual && player != null)
        {
            player.enabled = oldPlayerEnabled;

            if (rb != null)
                rb.linearVelocity = Vector2.zero;
        }

        ritualRunning = false;
    }
}
