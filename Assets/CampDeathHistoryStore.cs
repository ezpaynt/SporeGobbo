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
        return leaderPart + buddyName + " the " + typePart +
               "\nLv " + Mathf.Max(1, level) + "   Run " + Mathf.Max(1, runNumber) +
               "\n" + (string.IsNullOrWhiteSpace(causeOfDeath) ? "Lost in the caves" : causeOfDeath);
    }
}

public class CampDeathHistoryStore : MonoBehaviour
{
    public static CampDeathHistoryStore Instance { get; private set; }

    [Header("Permanent Death History")]
    public List<DeadBuddyRecord> deadBuddyHistory = new List<DeadBuddyRecord>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (deadBuddyHistory == null)
            deadBuddyHistory = new List<DeadBuddyRecord>();
    }

    public static CampDeathHistoryStore GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        CampDeathHistoryStore found = Object.FindAnyObjectByType<CampDeathHistoryStore>(FindObjectsInactive.Include);
        if (found != null)
        {
            Instance = found;
            return Instance;
        }

        GameObject obj = new GameObject("CampDeathHistoryStore");
        return obj.AddComponent<CampDeathHistoryStore>();
    }

    public bool HasAnyDeaths()
    {
        return deadBuddyHistory != null && deadBuddyHistory.Count > 0;
    }

    public bool HasUnseenMemorials()
    {
        if (deadBuddyHistory == null)
            return false;

        foreach (DeadBuddyRecord record in deadBuddyHistory)
        {
            if (record != null && !record.memorialSeen)
                return true;
        }

        return false;
    }

    public void MarkAllSeen()
    {
        if (deadBuddyHistory == null)
            return;

        foreach (DeadBuddyRecord record in deadBuddyHistory)
        {
            if (record != null)
                record.memorialSeen = true;
        }
    }

    public DeadBuddyRecord AddFromLabel(string label)
    {
        return AddFromLabel(label, 1, "Lost in the caves");
    }

    public DeadBuddyRecord AddFromLabel(string label, int runNumber)
    {
        return AddFromLabel(label, runNumber, "Lost in the caves");
    }

    public DeadBuddyRecord AddFromLabel(string label, int runNumber, string causeOfDeath)
    {
        DeadBuddyRecord record = new DeadBuddyRecord();
        record.buddyName = string.IsNullOrWhiteSpace(label) ? "Unknown Gobbo" : label.Trim();
        record.buddyType = "Gobbo";
        record.ageStage = "";
        record.level = 1;
        record.runNumber = Mathf.Max(1, runNumber);
        record.causeOfDeath = string.IsNullOrWhiteSpace(causeOfDeath) ? "Lost in the caves" : causeOfDeath.Trim();
        record.memorialSeen = false;
        record.wasPlayerLeader = false;

        AddRecord(record);
        return record;
    }

    public DeadBuddyRecord AddFromBuddy(BuddyData buddy, int runNumber, string causeOfDeath)
    {
        if (buddy == null)
            return AddFromLabel("Unknown Gobbo", runNumber, causeOfDeath);

        buddy.EnsureRuntimeDefaults();

        DeadBuddyRecord record = new DeadBuddyRecord();
        record.buddyName = string.IsNullOrWhiteSpace(buddy.buddyName) ? "Unknown Gobbo" : buddy.buddyName;
        record.buddyType = buddy.buddyType.ToString();
        record.ageStage = buddy.ageStage.ToString();
        record.level = Mathf.Max(1, buddy.level);
        record.runNumber = Mathf.Max(1, runNumber);
        record.causeOfDeath = string.IsNullOrWhiteSpace(causeOfDeath) ? "Lost in the caves" : causeOfDeath.Trim();
        record.memorialSeen = false;
        record.wasPlayerLeader = false;

        AddRecord(record);
        return record;
    }

    public DeadBuddyRecord AddDeadLeaderFromPendingStore()
    {
        return AddDeadLeaderFromPendingStore(PlayerDeathRunStore.Instance);
    }

    public DeadBuddyRecord AddDeadLeaderFromPendingStore(PlayerDeathRunStore pending)
    {
        if (pending == null)
            return AddDeadLeaderFallback();

        if (pending.memorialAddedToHistory)
            return null;

        pending.SyncCompatibilityFields();

        DeadBuddyRecord record = new DeadBuddyRecord();
        record.buddyName = string.IsNullOrWhiteSpace(pending.deadLeaderName) ? "The old leader" : pending.deadLeaderName;
        record.buddyType = string.IsNullOrWhiteSpace(pending.deadLeaderType) ? "Gobbo" : pending.deadLeaderType;
        record.ageStage = "Leader";
        record.level = Mathf.Max(1, pending.deadLeaderLevel);
        record.runNumber = Mathf.Max(1, pending.deadLeaderRunNumber);
        record.causeOfDeath = string.IsNullOrWhiteSpace(pending.deathCause) ? "Fell leading the horde" : pending.deathCause;
        record.memorialSeen = false;
        record.wasPlayerLeader = true;

        AddRecord(record);
        pending.memorialAddedToHistory = true;
        return record;
    }

    DeadBuddyRecord AddDeadLeaderFallback()
    {
        DeadBuddyRecord record = new DeadBuddyRecord();
        record.buddyName = "The old leader";
        record.buddyType = "Gobbo";
        record.ageStage = "Leader";
        record.level = 1;
        record.runNumber = GetCurrentRunNumber();
        record.causeOfDeath = "Fell leading the horde";
        record.memorialSeen = false;
        record.wasPlayerLeader = true;
        AddRecord(record);
        return record;
    }

    void AddRecord(DeadBuddyRecord record)
    {
        if (deadBuddyHistory == null)
            deadBuddyHistory = new List<DeadBuddyRecord>();

        deadBuddyHistory.Add(record);
    }

    int GetCurrentRunNumber()
    {
        if (GameState.Instance != null)
            return Mathf.Max(1, GameState.Instance.currentRunNumber);

        return 1;
    }
}
