using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DeadBuddyRecord
{
    public string buddyName = "Unknown Gobbo";
    public string buddyType = "Gobbo";
    public string ageStage = "";
    public int level = 1;
    public int runNumber = 1;
    public string causeOfDeath = "Lost in the caves";
    public bool memorialSeen = false;
    public bool wasPlayerLeader = false;

    public string GetDisplayLine()
    {
        string typePart = string.IsNullOrWhiteSpace(ageStage) ? buddyType : buddyType + " / " + ageStage;
        string leaderPart = wasPlayerLeader ? "Leader " : "";
        return leaderPart + buddyName + " the " + typePart + "\nLv " + Mathf.Max(1, level) + " Run " + Mathf.Max(1, runNumber) + "\n" + (string.IsNullOrWhiteSpace(causeOfDeath) ? "Lost in the caves" : causeOfDeath);
    }
}

public class CampDeathHistoryStore : MonoBehaviour
{
    public static CampDeathHistoryStore Instance { get; private set; }

    [Header("Permanent Death History")]
    public List<DeadBuddyRecord> deadBuddyHistory = new List<DeadBuddyRecord>();

    private void Awake()
    {
        if (deadBuddyHistory == null) deadBuddyHistory = new List<DeadBuddyRecord>();
        if (Instance != null && Instance != this)
        {
            if (IsMixedSceneObject()) { enabled = false; return; }
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private bool IsMixedSceneObject()
    {
        if (GetComponent<CampSuccessionUI>() != null) return true;
        if (GetComponent<CampSuccessorPreferenceStore>() != null) return true;
        return false;
    }

    public static CampDeathHistoryStore GetOrCreate()
    {
        if (Instance != null) return Instance;
        CampDeathHistoryStore found = Object.FindAnyObjectByType<CampDeathHistoryStore>(FindObjectsInactive.Include);
        if (found != null) { Instance = found; return Instance; }
        GameObject obj = new GameObject("CampDeathHistoryStore");
        Instance = obj.AddComponent<CampDeathHistoryStore>();
        return Instance;
    }

    public bool HasAnyDeaths() => deadBuddyHistory != null && deadBuddyHistory.Count > 0;

    public bool HasUnseenMemorials()
    {
        if (deadBuddyHistory == null) return false;
        foreach (DeadBuddyRecord record in deadBuddyHistory)
            if (record != null && !record.memorialSeen) return true;
        return false;
    }

    public void MarkAllSeen()
    {
        if (deadBuddyHistory == null) return;
        foreach (DeadBuddyRecord record in deadBuddyHistory)
            if (record != null) record.memorialSeen = true;
    }

    public DeadBuddyRecord AddFromLabel(string label) => AddFromLabel(label, 1, "Lost in the caves");
    public DeadBuddyRecord AddFromLabel(string label, int runNumber) => AddFromLabel(label, runNumber, "Lost in the caves");

    public DeadBuddyRecord AddFromLabel(string label, int runNumber, string causeOfDeath)
    {
        DeadBuddyRecord record = new DeadBuddyRecord
        {
            buddyName = string.IsNullOrWhiteSpace(label) ? "Unknown Gobbo" : label.Trim(),
            buddyType = "Gobbo",
            ageStage = "",
            level = 1,
            runNumber = Mathf.Max(1, runNumber),
            causeOfDeath = string.IsNullOrWhiteSpace(causeOfDeath) ? "Lost in the caves" : causeOfDeath.Trim(),
            memorialSeen = false,
            wasPlayerLeader = false
        };
        AddRecord(record);
        return record;
    }

    public DeadBuddyRecord AddFromBuddy(GobboUnitSaveData buddy, int runNumber, string causeOfDeath)
    {
        if (buddy == null) return AddFromLabel("Unknown Gobbo", runNumber, causeOfDeath);
        buddy.EnsureRuntimeDefaults();
        DeadBuddyRecord record = new DeadBuddyRecord
        {
            buddyName = string.IsNullOrWhiteSpace(buddy.displayName) ? "Unknown Gobbo" : buddy.displayName,
            buddyType = buddy.gobboType.ToString(),
            ageStage = buddy.ageStage.ToString(),
            level = Mathf.Max(1, buddy.level),
            runNumber = Mathf.Max(1, runNumber),
            causeOfDeath = string.IsNullOrWhiteSpace(causeOfDeath) ? "Lost in the caves" : causeOfDeath.Trim(),
            memorialSeen = false,
            wasPlayerLeader = false
        };
        AddRecord(record);
        return record;
    }

    public DeadBuddyRecord AddDeadLeaderFromPendingStore() => AddDeadLeaderFromPendingStore(PlayerDeathRunStore.Instance);

    public DeadBuddyRecord AddDeadLeaderFromPendingStore(PlayerDeathRunStore pending)
    {
        if (pending == null) return AddDeadLeaderFallback();
        if (pending.memorialAddedToHistory) return null;
        pending.SyncCompatibilityFields();
        DeadBuddyRecord record = new DeadBuddyRecord
        {
            buddyName = string.IsNullOrWhiteSpace(pending.deadLeaderName) ? "The old leader" : pending.deadLeaderName,
            buddyType = string.IsNullOrWhiteSpace(pending.deadLeaderType) ? "Gobbo" : pending.deadLeaderType,
            ageStage = "Leader",
            level = Mathf.Max(1, pending.deadLeaderLevel),
            runNumber = Mathf.Max(1, pending.deadLeaderRunNumber),
            causeOfDeath = string.IsNullOrWhiteSpace(pending.deathCause) ? "Fell leading the horde" : pending.deathCause,
            memorialSeen = false,
            wasPlayerLeader = true
        };
        AddRecord(record);
        pending.memorialAddedToHistory = true;
        return record;
    }

    private DeadBuddyRecord AddDeadLeaderFallback()
    {
        DeadBuddyRecord record = new DeadBuddyRecord
        {
            buddyName = "The old leader",
            buddyType = "Gobbo",
            ageStage = "Leader",
            level = 1,
            runNumber = GetCurrentRunNumber(),
            causeOfDeath = "Fell leading the horde",
            memorialSeen = false,
            wasPlayerLeader = true
        };
        AddRecord(record);
        return record;
    }

    private void AddRecord(DeadBuddyRecord record)
    {
        if (deadBuddyHistory == null) deadBuddyHistory = new List<DeadBuddyRecord>();
        deadBuddyHistory.Add(record);
    }

    private int GetCurrentRunNumber()
    {
        if (GameState.Instance != null) return Mathf.Max(1, GameState.Instance.currentRunNumber);
        return 1;
    }
}
