using System;
using System.IO;
using UnityEngine;

public static class SporeSaveManager
{
    public const int SlotCount = 3;
    private const string LastSlotKey = "SporeGobbo_LastPlayedSlot";

    public static string SaveFolder
    {
        get
        {
            string folder = Path.Combine(Application.persistentDataPath, "Saves");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            return folder;
        }
    }

    public static string GetSlotPath(int slotIndex)
    {
        slotIndex = Mathf.Clamp(slotIndex, 1, SlotCount);
        return Path.Combine(SaveFolder, "slot_" + slotIndex + ".json");
    }

    public static bool HasSave(int slotIndex)
    {
        SporeSaveSlotData data = LoadSlot(slotIndex);
        return data != null && data.hasSave;
    }

    public static SporeSaveSlotData LoadSlot(int slotIndex)
    {
        slotIndex = Mathf.Clamp(slotIndex, 1, SlotCount);
        string path = GetSlotPath(slotIndex);
        if (!File.Exists(path)) return new SporeSaveSlotData { slotIndex = slotIndex, hasSave = false };

        try
        {
            string json = File.ReadAllText(path);
            SporeSaveSlotData data = JsonUtility.FromJson<SporeSaveSlotData>(json);
            if (data == null) data = new SporeSaveSlotData { slotIndex = slotIndex, hasSave = false };
            data.slotIndex = slotIndex;
            data.Normalize();
            return data;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to load save slot " + slotIndex + ": " + ex.Message);
            return new SporeSaveSlotData { slotIndex = slotIndex, hasSave = false };
        }
    }

    public static void SaveSlot(SporeSaveSlotData data)
    {
        if (data == null) return;
        data.slotIndex = Mathf.Clamp(data.slotIndex, 1, SlotCount);
        data.hasSave = true;
        data.saveId = "slot_" + data.slotIndex;
        data.nextSceneName = "CampScene"; // Saves always resume to camp.
        data.lastPlayedUtcTicks = DateTime.UtcNow.Ticks;
        data.lastPlayedAt = DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        data.Normalize();

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(GetSlotPath(data.slotIndex), json);
        SetLastPlayedSlot(data.slotIndex);
        Debug.Log("[SporeSaveManager] Saved full slot " + data.slotIndex + " to " + GetSlotPath(data.slotIndex));
    }

    public static SporeSaveSlotData CreateNewGame(int slotIndex, string firstSceneName)
    {
        return CreateNewGame(slotIndex, firstSceneName, "Gobbo", false);
    }

    public static SporeSaveSlotData CreateNewGame(int slotIndex, string firstSceneName, string playerName, bool allowOverwrite = false)
    {
        slotIndex = Mathf.Clamp(slotIndex, 1, SlotCount);
        if (!allowOverwrite && HasSave(slotIndex))
        {
            Debug.LogWarning("[SporeSaveManager] Slot " + slotIndex + " already has a save. New game refused.");
            return null;
        }

        SporeSaveSlotData data = SporeSaveSlotData.CreateNew(slotIndex, firstSceneName, playerName);
        SaveSlot(data);
        ApplySlotToGameState(data);
        return data;
    }

    public static SporeSaveSlotData CreateNewGameInFirstEmptySlot(string firstSceneName, string playerName)
    {
        int slot = GetFirstEmptySlotIndex();
        if (slot <= 0)
        {
            Debug.LogWarning("[SporeSaveManager] All three save slots are full. New game refused.");
            return null;
        }
        return CreateNewGame(slot, firstSceneName, playerName, false);
    }

    public static int GetFirstEmptySlotIndex()
    {
        for (int i = 1; i <= SlotCount; i++)
        {
            if (!HasSave(i)) return i;
        }
        return 0;
    }

    public static bool HasOpenSlot() => GetFirstEmptySlotIndex() > 0;

    public static void SaveCurrentGameToCurrentSlot()
    {
        int slot = GetLastPlayedSlot();
        if (slot <= 0) slot = GetFirstEmptySlotIndex();
        if (slot <= 0) slot = 1;
        SaveCurrentGameToSlot(slot);
    }

    public static void SaveCurrentGameToSlot(int slotIndex)
    {
        SporeSaveSlotData data = BuildSlotFromGameState(slotIndex);
        if (data != null) SaveSlot(data);
    }

    public static SporeSaveSlotData BuildSlotFromGameState(int slotIndex)
    {
        GameState gs = EnsureGameState();
        if (gs == null) return null;

        SporeSaveSlotData existing = LoadSlot(slotIndex);
        SporeSaveSlotData data = new SporeSaveSlotData();
        data.slotIndex = Mathf.Clamp(slotIndex, 1, SlotCount);
        data.hasSave = true;
        data.saveId = "slot_" + data.slotIndex;
        data.playerName = !string.IsNullOrWhiteSpace(existing.playerName) ? existing.playerName : "Gobbo";
        data.saveName = !string.IsNullOrWhiteSpace(existing.saveName) ? existing.saveName : data.playerName + "'s Camp";
        data.createdUtcTicks = existing.createdUtcTicks > 0 ? existing.createdUtcTicks : DateTime.UtcNow.Ticks;
        data.createdAt = !string.IsNullOrWhiteSpace(existing.createdAt) ? existing.createdAt : DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        data.nextSceneName = "CampScene";

        gs.RepairRosterState();
        data.currentRunNumber = gs.currentRunNumber;
        data.maxActiveSquad = gs.maxActiveSquad;
        data.campLevel = gs.campLevel;
        data.player = CloneGobbo(gs.gobbo);
        data.ownedBuddies = CloneBuddyList(gs.ownedBuddies);
        data.activeSquadIds = gs.activeSquadIds != null ? new System.Collections.Generic.List<string>(gs.activeSquadIds) : new System.Collections.Generic.List<string>();
        data.unlockedStations = gs.unlockedStations != null ? new System.Collections.Generic.List<string>(gs.unlockedStations) : new System.Collections.Generic.List<string>();
        data.decorationsUnlocked = gs.decorationsUnlocked != null ? new System.Collections.Generic.List<string>(gs.decorationsUnlocked) : new System.Collections.Generic.List<string>();
        data.lastRun = gs.lastRun != null ? CloneRunSummary(gs.lastRun) : new RunSummaryData();

        GameStateSaveBridge bridge = GameStateSaveBridge.Instance;
        if (bridge != null && !string.IsNullOrWhiteSpace(bridge.markedSuccessorId)) data.markedSuccessorId = bridge.markedSuccessorId;
        else
        {
            CampSuccessorPreferenceStore pref = CampSuccessorPreferenceStore.Instance;
            data.markedSuccessorId = pref != null ? pref.GetMarkedSuccessorId() : existing.markedSuccessorId;
        }

        CampDeathHistoryStore history = CampDeathHistoryStore.Instance;
        data.deathHistory = history != null && history.deadBuddyHistory != null
            ? CloneDeathHistory(history.deadBuddyHistory)
            : (existing.deathHistory != null ? existing.deathHistory : new System.Collections.Generic.List<DeadBuddyRecord>());

        data.Normalize();
        return data;
    }

    public static void ApplySlotToGameState(SporeSaveSlotData data)
    {
        if (data == null || !data.hasSave) return;
        data.Normalize();
        GameState gs = EnsureGameState();
        if (gs == null) return;

        gs.currentRunNumber = data.currentRunNumber;
        gs.maxActiveSquad = data.maxActiveSquad;
        gs.campLevel = data.campLevel;
        gs.gobbo = CloneGobbo(data.player);
        gs.ownedBuddies = CloneBuddyList(data.ownedBuddies);
        gs.activeSquadIds = data.activeSquadIds != null ? new System.Collections.Generic.List<string>(data.activeSquadIds) : new System.Collections.Generic.List<string>();
        gs.unlockedStations = data.unlockedStations != null ? new System.Collections.Generic.List<string>(data.unlockedStations) : new System.Collections.Generic.List<string>();
        gs.decorationsUnlocked = data.decorationsUnlocked != null ? new System.Collections.Generic.List<string>(data.decorationsUnlocked) : new System.Collections.Generic.List<string>();
        gs.lastRun = data.lastRun != null ? CloneRunSummary(data.lastRun) : new RunSummaryData();
        gs.RepairRosterState();

        GameStateSaveBridge bridge = GameStateSaveBridge.GetOrCreate();
        bridge.SetCurrentSlotWithoutSaving(data.slotIndex, data.playerName, data.saveName, data.markedSuccessorId);

        CampSuccessorPreferenceStore pref = CampSuccessorPreferenceStore.GetOrCreate();
        pref.SetMarkedSuccessor(data.markedSuccessorId, false);

        CampDeathHistoryStore history = CampDeathHistoryStore.GetOrCreate();
        history.deadBuddyHistory = data.deathHistory != null ? CloneDeathHistory(data.deathHistory) : new System.Collections.Generic.List<DeadBuddyRecord>();

        SetLastPlayedSlot(data.slotIndex);
        Debug.Log("[SporeSaveManager] Applied slot " + data.slotIndex + " to GameState. Buddies: " + gs.ownedBuddies.Count);
    }

    public static void DeleteSlot(int slotIndex)
    {
        string path = GetSlotPath(slotIndex);
        if (File.Exists(path)) File.Delete(path);
        if (GetLastPlayedSlot() == slotIndex) PlayerPrefs.DeleteKey(LastSlotKey);
        PlayerPrefs.Save();
    }

    public static int GetLastPlayedSlot() => PlayerPrefs.GetInt(LastSlotKey, 0);

    public static void SetLastPlayedSlot(int slotIndex)
    {
        slotIndex = Mathf.Clamp(slotIndex, 1, SlotCount);
        PlayerPrefs.SetInt(LastSlotKey, slotIndex);
        PlayerPrefs.Save();
    }

    public static SporeSaveSlotData LoadLastPlayedSlot()
    {
        SporeSaveSlotData mostRecent = null;
        for (int i = 1; i <= SlotCount; i++)
        {
            SporeSaveSlotData data = LoadSlot(i);
            if (data == null || !data.hasSave) continue;
            if (mostRecent == null || data.lastPlayedUtcTicks > mostRecent.lastPlayedUtcTicks) mostRecent = data;
        }
        if (mostRecent != null) SetLastPlayedSlot(mostRecent.slotIndex);
        return mostRecent;
    }

    public static bool HasAnySave()
    {
        for (int i = 1; i <= SlotCount; i++) if (HasSave(i)) return true;
        return false;
    }

    static GameState EnsureGameState()
    {
        if (GameState.Instance != null) return GameState.Instance;
        GameObject stateObject = new GameObject("GameState");
        return stateObject.AddComponent<GameState>();
    }

    static GobboSaveData CloneGobbo(GobboSaveData source)
    {
        GobboSaveData copy = new GobboSaveData();
        if (source != null) JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), copy);
        return copy;
    }

    static RunSummaryData CloneRunSummary(RunSummaryData source)
    {
        RunSummaryData copy = new RunSummaryData();
        if (source != null) JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), copy);
        return copy;
    }

    static System.Collections.Generic.List<BuddyData> CloneBuddyList(System.Collections.Generic.List<BuddyData> source)
    {
        System.Collections.Generic.List<BuddyData> result = new System.Collections.Generic.List<BuddyData>();
        if (source == null) return result;
        foreach (BuddyData buddy in source)
        {
            if (buddy == null) continue;
            buddy.EnsureId();
            buddy.EnsureRuntimeDefaults();
            result.Add(buddy.Clone());
        }
        return result;
    }

    static System.Collections.Generic.List<DeadBuddyRecord> CloneDeathHistory(System.Collections.Generic.List<DeadBuddyRecord> source)
    {
        System.Collections.Generic.List<DeadBuddyRecord> result = new System.Collections.Generic.List<DeadBuddyRecord>();
        if (source == null) return result;
        foreach (DeadBuddyRecord record in source)
        {
            if (record == null) continue;
            DeadBuddyRecord copy = new DeadBuddyRecord();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(record), copy);
            result.Add(copy);
        }
        return result;
    }
}
