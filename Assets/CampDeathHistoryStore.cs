using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[Serializable]
public class DeadBuddyRecord
{
    public string gobboId = "";
    public string displayName = "Unknown Gobbo";
    public string gobboType = "Unknown";
    public int level = 1;
    public int runNumber = 1;
    public int nightsSurvived = 0;
    public int kills = 0;
    public string traitId = "";
    public string cause = "Lost in the dirt.";
    public bool wasLeader = false;
    public bool memorialSeen = false;

    public string GetDisplayLine()
    {
        string role = wasLeader ? "Leader" : "Buddy";
        string name = string.IsNullOrWhiteSpace(displayName) ? "Unknown Gobbo" : displayName;
        string type = string.IsNullOrWhiteSpace(gobboType) ? "Gobbo" : gobboType;
        string why = string.IsNullOrWhiteSpace(cause) ? "Lost in the dirt." : cause;
        string history = $"{Mathf.Max(0, nightsSurvived)} nights, {Mathf.Max(0, kills)} kills";
        string trait = string.IsNullOrWhiteSpace(traitId) ? "" : $" | {traitId}";
        return $"{name} — {role} {type}, Lv {Mathf.Max(1, level)}{trait} | {history} | Run {Mathf.Max(1, runNumber)} | {why}";
    }
}

/// <summary>
/// Scene-side access point for the saved death history.
/// Permanent data is copied into/out of SporeSaveSlotData by SporeSaveManager.
/// This object should NOT be DontDestroyOnLoad; only data should persist through saves.
/// </summary>
public class CampDeathHistoryStore : MonoBehaviour
{
    public static CampDeathHistoryStore Instance { get; private set; }

    [Header("Saved Memorial Records")]
    public List<DeadBuddyRecord> deadBuddyHistory = new List<DeadBuddyRecord>();

    [Header("Debug")]
    public bool logDebugMessages = true;

    void Awake()
    {
        Instance = this;
        deadBuddyHistory ??= new List<DeadBuddyRecord>();
        LoadFromCurrentSaveIfEmpty();
    }

    void LoadFromCurrentSaveIfEmpty()
    {
        if (deadBuddyHistory != null && deadBuddyHistory.Count > 0) return;

        if (GameState.Instance != null && GameState.Instance.deathHistory != null && GameState.Instance.deathHistory.Count > 0)
        {
            deadBuddyHistory = GameState.Instance.GetDeathHistoryCopy();
            Log("Loaded " + deadBuddyHistory.Count + " memorial records from GameState.");
            return;
        }

        int slotIndex = SporeSaveManager.GetCurrentSlot();
        if (slotIndex <= 0) slotIndex = SporeSaveManager.GetLastPlayedSlot();
        if (slotIndex <= 0) return;

        SporeSaveSlotData data = SporeSaveManager.LoadSlot(slotIndex);
        if (data == null || !data.hasSave || data.deathHistory == null || data.deathHistory.Count == 0) return;

        deadBuddyHistory = new List<DeadBuddyRecord>(data.deathHistory);
        if (GameState.Instance != null) GameState.Instance.SetDeathHistory(deadBuddyHistory);
        Log("Loaded " + deadBuddyHistory.Count + " memorial records from save slot " + slotIndex + ".");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static CampDeathHistoryStore GetOrCreate()
    {
        if (Instance != null) return Instance;

        CampDeathHistoryStore found = FindAnyObjectByType<CampDeathHistoryStore>();
        if (found != null)
        {
            Instance = found;
            return found;
        }

        Debug.LogWarning("CampDeathHistoryStore.GetOrCreate could not find a CampDeathHistoryStore in the scene. Add one to CampScene.");
        return null;
    }

    public bool HasAnyDeaths()
    {
        return deadBuddyHistory != null && deadBuddyHistory.Count > 0;
    }

    public bool HasUnseenMemorials()
    {
        if (deadBuddyHistory == null) return false;
        foreach (DeadBuddyRecord record in deadBuddyHistory)
        {
            if (record != null && !record.memorialSeen) return true;
        }
        return false;
    }

    public void MarkAllSeen()
    {
        if (deadBuddyHistory == null) return;
        foreach (DeadBuddyRecord record in deadBuddyHistory)
        {
            if (record != null) record.memorialSeen = true;
        }
        if (GameState.Instance != null) GameState.Instance.SetDeathHistory(deadBuddyHistory);
        TrySaveCurrentSlot();
    }

    public DeadBuddyRecord AddFromGobbo(GobboUnitSaveData gobbo, int runNumber, string cause, bool wasLeader)
    {
        if (gobbo == null) return null;
        gobbo.EnsureRuntimeDefaults();
        DeadBuddyRecord record = new DeadBuddyRecord
        {
            gobboId = gobbo.uniqueId,
            displayName = string.IsNullOrWhiteSpace(gobbo.displayName) ? "Unknown Gobbo" : gobbo.displayName,
            gobboType = gobbo.gobboType.ToString(),
            level = Mathf.Max(1, gobbo.level),
            runNumber = Mathf.Max(1, runNumber),
            nightsSurvived = Mathf.Max(0, gobbo.nightsSurvived),
            kills = Mathf.Max(0, gobbo.kills),
            traitId = string.IsNullOrWhiteSpace(gobbo.primaryTraitId) ? "" : gobbo.primaryTraitId,
            cause = string.IsNullOrWhiteSpace(cause) ? "Lost in the dirt." : cause,
            wasLeader = wasLeader,
            memorialSeen = false
        };
        return AddRecord(record);
    }

    public DeadBuddyRecord AddFromLabel(string label, int runNumber, string cause)
    {
        DeadBuddyRecord record = new DeadBuddyRecord
        {
            gobboId = "",
            displayName = string.IsNullOrWhiteSpace(label) ? "Unknown Gobbo" : label,
            gobboType = "Gobbo",
            level = 1,
            runNumber = Mathf.Max(1, runNumber),
            cause = string.IsNullOrWhiteSpace(cause) ? "Lost in the dirt." : cause,
            wasLeader = false,
            memorialSeen = false
        };
        return AddRecord(record);
    }

    public DeadBuddyRecord AddDeadLeaderFromPendingStore()
    {
        PlayerDeathRunStore store = PlayerDeathRunStore.Instance;
        if (store == null || !GetBool(store, "playerDiedThisRun")) return null;
        if (GetBool(store, "deadLeaderMemorialized")) return null;

        string id = GetString(store, "deadLeaderId", GetString(store, "deadPlayerId", ""));
        string name = GetString(store, "deadLeaderName", GetString(store, "deadPlayerName", "Unknown Leader"));
        string type = GetString(store, "deadLeaderType", GetString(store, "deadPlayerType", "Gobbo"));
        int level = GetInt(store, "deadLeaderLevel", GetInt(store, "deadPlayerLevel", 1));
        int runNumber = GetInt(store, "runNumber", GameState.Instance != null ? GameState.Instance.currentRunNumber : 1);
        string cause = GetString(store, "deathCause", "The leader got chewed up in the dirt.");

        DeadBuddyRecord record = new DeadBuddyRecord
        {
            gobboId = id,
            displayName = string.IsNullOrWhiteSpace(name) ? "Unknown Leader" : name,
            gobboType = string.IsNullOrWhiteSpace(type) ? "Gobbo" : type,
            level = Mathf.Max(1, level),
            runNumber = Mathf.Max(1, runNumber),
            cause = string.IsNullOrWhiteSpace(cause) ? "The leader got chewed up in the dirt." : cause,
            wasLeader = true,
            memorialSeen = false
        };

        DeadBuddyRecord added = AddRecord(record);
        SetBool(store, "deadLeaderMemorialized", true);
        SetBool(store, "deadPlayerMemorialized", true);
        return added;
    }

    public DeadBuddyRecord AddRecord(DeadBuddyRecord record)
    {
        if (record == null) return null;
        deadBuddyHistory ??= new List<DeadBuddyRecord>();

        // Prevent duplicate records for the same gobbo/run/role.
        foreach (DeadBuddyRecord existing in deadBuddyHistory)
        {
            if (existing == null) continue;
            bool sameId = !string.IsNullOrWhiteSpace(record.gobboId) && existing.gobboId == record.gobboId;
            bool sameName = string.IsNullOrWhiteSpace(record.gobboId) && existing.displayName == record.displayName;
            if ((sameId || sameName) && existing.runNumber == record.runNumber && existing.wasLeader == record.wasLeader)
                return existing;
        }

        deadBuddyHistory.Add(record);
        if (GameState.Instance != null) GameState.Instance.AddDeathHistoryRecord(record);
        Log("Added memorial: " + record.GetDisplayLine());
        TrySaveCurrentSlot();
        return record;
    }

    public List<DeadBuddyRecord> GetDeadBuddiesFromCurrentRun()
    {
        int run = GameState.Instance != null ? GameState.Instance.currentRunNumber : 1;
        List<DeadBuddyRecord> result = new List<DeadBuddyRecord>();
        if (deadBuddyHistory == null) return result;
        foreach (DeadBuddyRecord record in deadBuddyHistory)
        {
            if (record != null && !record.wasLeader && record.runNumber == run) result.Add(record);
        }
        return result;
    }

    public List<DeadBuddyRecord> GetDeadLeaders()
    {
        List<DeadBuddyRecord> result = new List<DeadBuddyRecord>();
        if (deadBuddyHistory == null) return result;
        foreach (DeadBuddyRecord record in deadBuddyHistory)
        {
            if (record != null && record.wasLeader) result.Add(record);
        }
        return result;
    }

    void TrySaveCurrentSlot()
    {
        try { SporeSaveManager.SaveCurrentSlotFromGameState(); }
        catch { /* compile-safe best effort during scene teardown */ }
    }

    void Log(string message)
    {
        if (logDebugMessages) Debug.Log("[CampDeathHistoryStore] " + message);
    }

    static FieldInfo Field(object obj, string name) => obj != null ? obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) : null;
    static string GetString(object obj, string name, string fallback) { FieldInfo f = Field(obj, name); object v = f != null ? f.GetValue(obj) : null; return v is string s ? s : fallback; }
    static int GetInt(object obj, string name, int fallback) { FieldInfo f = Field(obj, name); object v = f != null ? f.GetValue(obj) : null; return v is int i ? i : fallback; }
    static bool GetBool(object obj, string name) { FieldInfo f = Field(obj, name); object v = f != null ? f.GetValue(obj) : null; return v is bool b && b; }
    static void SetBool(object obj, string name, bool value) { FieldInfo f = Field(obj, name); if (f != null && f.FieldType == typeof(bool)) f.SetValue(obj, value); }
}
