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
        string safeName = string.IsNullOrWhiteSpace(buddyName) ? "Unknown Gobbo" : buddyName;
        string safeType = string.IsNullOrWhiteSpace(buddyType) ? "Gobbo" : buddyType;
        string typePart = string.IsNullOrWhiteSpace(ageStage) ? safeType : safeType + " / " + ageStage;
        string leaderPart = wasPlayerLeader ? "Leader " : "";
        string cause = string.IsNullOrWhiteSpace(causeOfDeath) ? "Lost in the caves" : causeOfDeath;

        return leaderPart + safeName + " the " + typePart +
               "\nLv " + Mathf.Max(1, level) + "   Run " + Mathf.Max(1, runNumber) +
               "\n" + cause;
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
        return AddFromLabel(label, GetCurrentRunNumber(), "Lost in the caves");
    }

    public DeadBuddyRecord AddFromLabel(string label, int runNumber)
    {
        return AddFromLabel(label, runNumber, "Lost in the caves");
    }

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

        AddRecordIfNotDuplicate(record);
        return record;
    }

    public DeadBuddyRecord AddFromBuddy(BuddyData buddy, int runNumber, string causeOfDeath)
    {
        if (buddy == null)
            return AddFromLabel("Unknown Gobbo", runNumber, causeOfDeath);

        buddy.EnsureRuntimeDefaults();

        DeadBuddyRecord record = new DeadBuddyRecord
        {
            buddyName = string.IsNullOrWhiteSpace(buddy.buddyName) ? "Unknown Gobbo" : buddy.buddyName,
            buddyType = buddy.buddyType.ToString(),
            ageStage = buddy.ageStage.ToString(),
            level = Mathf.Max(1, buddy.level),
            runNumber = Mathf.Max(1, runNumber),
            causeOfDeath = string.IsNullOrWhiteSpace(causeOfDeath) ? "Lost in the caves" : causeOfDeath.Trim(),
            memorialSeen = false,
            wasPlayerLeader = false
        };

        AddRecordIfNotDuplicate(record);
        return record;
    }

    public DeadBuddyRecord AddDeadLeaderFromPendingStore()
    {
        return AddDeadLeaderFromPendingStore(PlayerDeathRunStore.Instance);
    }

    // Compatibility overload for CampSuccessionUI versions that pass the pending store in.
    public DeadBuddyRecord AddDeadLeaderFromPendingStore(PlayerDeathRunStore pending)
    {
        if (pending == null)
            pending = PlayerDeathRunStore.GetOrCreate();

        if (pending == null)
            return AddLeaderRecord("The old leader", "Gobbo", 1, GetCurrentRunNumber(), "Fell leading the horde");

        // Keep either naming style working.
        string name = !string.IsNullOrWhiteSpace(pending.deadLeaderName) ? pending.deadLeaderName : pending.deadPlayerName;
        string type = !string.IsNullOrWhiteSpace(pending.deadLeaderType) ? pending.deadLeaderType : pending.deadPlayerType;
        int level = Mathf.Max(pending.deadLeaderLevel, pending.deadPlayerLevel);
        int run = Mathf.Max(pending.deadLeaderRunNumber, pending.runNumber);
        string cause = pending.deathCause;

        return AddLeaderRecord(name, type, level, run, cause);
    }

    DeadBuddyRecord AddLeaderRecord(string name, string type, int level, int runNumber, string causeOfDeath)
    {
        DeadBuddyRecord record = new DeadBuddyRecord
        {
            buddyName = string.IsNullOrWhiteSpace(name) ? "The old leader" : name.Trim(),
            buddyType = string.IsNullOrWhiteSpace(type) ? "Gobbo" : type.Trim(),
            ageStage = "Leader",
            level = Mathf.Max(1, level),
            runNumber = Mathf.Max(1, runNumber),
            causeOfDeath = string.IsNullOrWhiteSpace(causeOfDeath) ? "Fell leading the horde" : causeOfDeath.Trim(),
            memorialSeen = false,
            wasPlayerLeader = true
        };

        AddRecordIfNotDuplicate(record);
        return record;
    }

    void AddRecordIfNotDuplicate(DeadBuddyRecord record)
    {
        if (record == null)
            return;

        if (deadBuddyHistory == null)
            deadBuddyHistory = new List<DeadBuddyRecord>();

        foreach (DeadBuddyRecord existing in deadBuddyHistory)
        {
            if (existing == null)
                continue;

            bool sameName = existing.buddyName == record.buddyName;
            bool sameRun = existing.runNumber == record.runNumber;
            bool sameLeaderState = existing.wasPlayerLeader == record.wasPlayerLeader;

            if (sameName && sameRun && sameLeaderState)
                return;
        }

        deadBuddyHistory.Add(record);
    }

    int GetCurrentRunNumber()
    {
        if (GameState.Instance != null)
            return Mathf.Max(1, GameState.Instance.currentRunNumber);

        return 1;
    }
}
