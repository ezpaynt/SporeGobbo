using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class SporeSaveManager
{
    public const int SlotCount = 3;
    private const string LastSlotKey = "SporeGobbo_LastPlayedSlot";
    private const string CurrentSlotKey = "SporeGobbo_CurrentSlot";

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
        slotIndex = ClampSlot(slotIndex);
        return Path.Combine(SaveFolder, "slot_" + slotIndex + ".json");
    }

    public static bool HasSave(int slotIndex)
    {
        SporeSaveSlotData data = LoadSlot(slotIndex);
        return data != null && data.hasSave;
    }

    public static int SaveCount()
    {
        int count = 0;
        for (int i = 1; i <= SlotCount; i++)
            if (HasSave(i)) count++;
        return count;
    }

    public static bool CanCreateNewGame()
    {
        return FindFirstEmptySlot() > 0;
    }

    public static int FindFirstEmptySlot()
    {
        for (int i = 1; i <= SlotCount; i++)
            if (!HasSave(i)) return i;
        return 0;
    }

    public static int GetFirstEmptySlotIndex() => FindFirstEmptySlot();

    public static SporeSaveSlotData LoadSlot(int slotIndex)
    {
        slotIndex = ClampSlot(slotIndex);
        string path = GetSlotPath(slotIndex);
        if (!File.Exists(path))
            return new SporeSaveSlotData { slotIndex = slotIndex, hasSave = false, saveId = "slot_" + slotIndex };

        try
        {
            string json = File.ReadAllText(path);
            SporeSaveSlotData data = JsonUtility.FromJson<SporeSaveSlotData>(json);
            if (data == null) data = new SporeSaveSlotData { hasSave = false };
            data.slotIndex = slotIndex;
            data.saveId = "slot_" + slotIndex;
            Normalize(data);
            return data;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SporeSaveManager] Failed to load save slot " + slotIndex + ": " + ex.Message);
            return new SporeSaveSlotData { slotIndex = slotIndex, hasSave = false, saveId = "slot_" + slotIndex };
        }
    }

    public static void SaveSlot(SporeSaveSlotData data)
    {
        if (data == null) return;
        data.slotIndex = ClampSlot(data.slotIndex);
        data.saveId = "slot_" + data.slotIndex;
        data.hasSave = true;
        data.lastPlayedUtcTicks = DateTime.UtcNow.Ticks;
        data.lastPlayedAt = DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        Normalize(data);

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(GetSlotPath(data.slotIndex), json);
        SetCurrentSlot(data.slotIndex);
        SetLastPlayedSlot(data.slotIndex);
        SyncBridgeFromSlot(data);
        Debug.Log("[SporeSaveManager] Saved full slot " + data.slotIndex + " to " + GetSlotPath(data.slotIndex));
    }

    public static SporeSaveSlotData CreateNewGame(string playerName, string firstSceneName)
    {
        int slotIndex = FindFirstEmptySlot();
        if (slotIndex <= 0)
        {
            Debug.LogWarning("[SporeSaveManager] Cannot create new game. All 3 save slots are full.");
            return null;
        }
        return CreateNewGame(slotIndex, playerName, firstSceneName, false);
    }

    public static SporeSaveSlotData CreateNewGame(int slotIndex, string firstSceneName)
    {
        return CreateNewGame(slotIndex, "Gobbo", firstSceneName, false);
    }

    public static SporeSaveSlotData CreateNewGame(int slotIndex, string playerName, string firstSceneName)
    {
        return CreateNewGame(slotIndex, playerName, firstSceneName, false);
    }

    public static SporeSaveSlotData CreateNewGame(int slotIndex, string playerName, string firstSceneName, bool allowOverwrite = false)
    {
        slotIndex = ClampSlot(slotIndex);
        if (HasSave(slotIndex) && !allowOverwrite)
        {
            Debug.LogWarning("[SporeSaveManager] Slot " + slotIndex + " already has a save. Refusing to overwrite.");
            return null;
        }

        SporeSaveSlotData data = SporeSaveSlotData.CreateNew(slotIndex, playerName, firstSceneName);
        ApplySlotToGameState(data);
        SaveSlot(data);
        return data;
    }

    public static SporeSaveSlotData CreateNewGameInFirstEmptySlot(string firstSceneName, string playerName = "Gobbo")
    {
        return CreateNewGame(playerName, firstSceneName);
    }

    // Safe camp autosave. This never creates a new slot.
    public static void SaveCurrentSlotFromGameState()
    {
        int slotIndex = GetCurrentSlot();
        if (slotIndex <= 0) slotIndex = GetLastPlayedSlot();
        if (slotIndex <= 0 || !HasSave(slotIndex))
        {
            Debug.LogWarning("[SporeSaveManager] No current save slot set. Refusing to create a new autosave.");
            return;
        }

        SporeSaveSlotData existing = LoadSlot(slotIndex);
        SporeSaveSlotData data = BuildSlotFromGameState(slotIndex, existing);
        SaveSlot(data);
    }

    public static void SaveCurrentGameToCurrentSlot() => SaveCurrentSlotFromGameState();
    public static void SaveCurrentGameToSlot(int slotIndex)
    {
        slotIndex = ClampSlot(slotIndex);
        if (!HasSave(slotIndex))
        {
            Debug.LogWarning("[SporeSaveManager] Cannot save to empty slot " + slotIndex + " except through New Game.");
            return;
        }
        SaveSlot(BuildSlotFromGameState(slotIndex, LoadSlot(slotIndex)));
    }

    public static SporeSaveSlotData BuildSlotFromGameState(int slotIndex, SporeSaveSlotData existing = null)
    {
        slotIndex = ClampSlot(slotIndex);
        if (existing == null || !existing.hasSave)
        {
            existing = SporeSaveSlotData.CreateNew(slotIndex, "Gobbo", "CampScene");
        }

        GameState gs = GameState.Instance;
        if (gs != null)
        {
            gs.RepairRosterState();
            existing.currentRunNumber = Mathf.Max(1, gs.currentRunNumber);
            existing.runNumber = existing.currentRunNumber;
            existing.maxActiveSquad = Mathf.Max(1, gs.maxActiveSquad);
            existing.campLevel = Mathf.Max(1, gs.campLevel);
            existing.player = CloneGobbo(gs.gobbo);
            existing.ownedBuddies = CloneBuddies(gs.ownedBuddies);
            existing.activeSquadIds = gs.activeSquadIds != null ? new List<string>(gs.activeSquadIds) : new List<string>();
            existing.unlockedStations = gs.unlockedStations != null ? new List<string>(gs.unlockedStations) : new List<string>();
            existing.decorationsUnlocked = gs.decorationsUnlocked != null ? new List<string>(gs.decorationsUnlocked) : new List<string>();
            existing.lastRun = CloneRunSummary(gs.lastRun);
        }

        GameStateSaveBridge bridge = GameStateSaveBridge.GetOrCreate();
        if (bridge != null)
        {
            if (!string.IsNullOrWhiteSpace(bridge.currentPlayerName))
                existing.playerName = bridge.currentPlayerName;
            if (!string.IsNullOrWhiteSpace(bridge.currentSaveName))
                existing.saveName = bridge.currentSaveName;
            existing.markedSuccessorId = bridge.GetMarkedSuccessorId();
        }

        CampSuccessorPreferenceStore successorStore = UnityEngine.Object.FindAnyObjectByType<CampSuccessorPreferenceStore>(FindObjectsInactive.Include);
        if (successorStore != null)
        {
            successorStore.ValidateAgainstRoster();
            existing.markedSuccessorId = successorStore.GetMarkedSuccessorId();
        }

        CampDeathHistoryStore deathStore = UnityEngine.Object.FindAnyObjectByType<CampDeathHistoryStore>(FindObjectsInactive.Include);
        if (deathStore != null && deathStore.deadBuddyHistory != null)
            existing.deathHistory = new List<DeadBuddyRecord>(deathStore.deadBuddyHistory);
        else if (existing.deathHistory == null)
            existing.deathHistory = new List<DeadBuddyRecord>();

        existing.nextSceneName = "CampScene";
        existing.Normalize();
        return existing;
    }

    public static bool ApplySlotToGameState(SporeSaveSlotData data)
    {
        if (data == null || !data.hasSave) return false;
        Normalize(data);
        EnsureGameState();
        GameState gs = GameState.Instance;
        if (gs == null) return false;

        gs.currentRunNumber = Mathf.Max(1, data.currentRunNumber);
        gs.maxActiveSquad = Mathf.Max(1, data.maxActiveSquad);
        gs.campLevel = Mathf.Max(1, data.campLevel);
        gs.gobbo = CloneGobbo(data.player);
        gs.ownedBuddies = CloneBuddies(data.ownedBuddies);
        gs.activeSquadIds = data.activeSquadIds != null ? new List<string>(data.activeSquadIds) : new List<string>();
        gs.unlockedStations = data.unlockedStations != null ? new List<string>(data.unlockedStations) : new List<string>();
        gs.decorationsUnlocked = data.decorationsUnlocked != null ? new List<string>(data.decorationsUnlocked) : new List<string>();
        gs.lastRun = CloneRunSummary(data.lastRun);
        gs.RepairRosterState();

        GameStateSaveBridge bridge = GameStateSaveBridge.GetOrCreate();
        if (bridge != null)
            bridge.SetCurrentSlotWithoutSaving(data.slotIndex, data.playerName, data.saveName, data.markedSuccessorId);

        CampSuccessorPreferenceStore successorStore = CampSuccessorPreferenceStore.GetOrCreate();
        if (successorStore != null)
        {
            if (string.IsNullOrWhiteSpace(data.markedSuccessorId)) successorStore.ClearSuccessor(false);
            else successorStore.SetMarkedSuccessor(data.markedSuccessorId, false);
            successorStore.ValidateAgainstRoster(false);
        }

        CampDeathHistoryStore deathStore = UnityEngine.Object.FindAnyObjectByType<CampDeathHistoryStore>(FindObjectsInactive.Include);
        if (deathStore != null && deathStore.deadBuddyHistory != null)
        {
            deathStore.deadBuddyHistory.Clear();
            if (data.deathHistory != null) deathStore.deadBuddyHistory.AddRange(data.deathHistory);
        }

        SetCurrentSlot(data.slotIndex);
        SetLastPlayedSlot(data.slotIndex);
        Debug.Log("[SporeSaveManager] Applied full slot " + data.slotIndex + " to GameState. Buddies: " + gs.ownedBuddies.Count);
        return true;
    }

    public static bool LoadSlotIntoGameState(int slotIndex)
    {
        SporeSaveSlotData data = LoadSlot(slotIndex);
        if (data == null || !data.hasSave) return false;
        return ApplySlotToGameState(data);
    }

    public static SporeSaveSlotData LoadMostRecentSlot()
    {
        SporeSaveSlotData best = null;
        for (int i = 1; i <= SlotCount; i++)
        {
            SporeSaveSlotData data = LoadSlot(i);
            if (data == null || !data.hasSave) continue;
            if (best == null || data.lastPlayedUtcTicks > best.lastPlayedUtcTicks)
                best = data;
        }
        return best;
    }

    public static SporeSaveSlotData LoadLastPlayedSlot()
    {
        return LoadMostRecentSlot();
    }

    public static bool HasAnySave() => LoadMostRecentSlot() != null;

    public static void DeleteSlot(int slotIndex)
    {
        slotIndex = ClampSlot(slotIndex);
        string path = GetSlotPath(slotIndex);
        if (File.Exists(path)) File.Delete(path);
        if (GetLastPlayedSlot() == slotIndex) PlayerPrefs.DeleteKey(LastSlotKey);
        if (GetCurrentSlot() == slotIndex) PlayerPrefs.DeleteKey(CurrentSlotKey);
        PlayerPrefs.Save();
        Debug.Log("[SporeSaveManager] Deleted slot " + slotIndex);
    }

    public static int GetLastPlayedSlot() => PlayerPrefs.GetInt(LastSlotKey, 0);
    public static void SetLastPlayedSlot(int slotIndex)
    {
        PlayerPrefs.SetInt(LastSlotKey, ClampSlot(slotIndex));
        PlayerPrefs.Save();
    }

    public static int GetCurrentSlot() => PlayerPrefs.GetInt(CurrentSlotKey, 0);
    public static void SetCurrentSlot(int slotIndex)
    {
        PlayerPrefs.SetInt(CurrentSlotKey, ClampSlot(slotIndex));
        PlayerPrefs.Save();
    }

    public static void Normalize(SporeSaveSlotData data)
    {
        if (data == null) return;
        data.Normalize();
        if (data.ownedBuddies != null)
        {
            foreach (BuddyData buddy in data.ownedBuddies)
            {
                if (buddy == null) continue;
                buddy.EnsureId();
                buddy.EnsureRuntimeDefaults();
            }
        }
    }

    private static int ClampSlot(int slotIndex) => Mathf.Clamp(slotIndex, 1, SlotCount);

    private static void EnsureGameState()
    {
        if (GameState.Instance != null) return;
        GameObject obj = new GameObject("GameState");
        obj.AddComponent<GameState>();
    }

    private static void SyncBridgeFromSlot(SporeSaveSlotData data)
    {
        GameStateSaveBridge bridge = GameStateSaveBridge.GetOrCreate();
        if (bridge != null)
            bridge.SetCurrentSlotWithoutSaving(data.slotIndex, data.playerName, data.saveName, data.markedSuccessorId);
    }

    private static GobboSaveData CloneGobbo(GobboSaveData source)
    {
        GobboSaveData copy = new GobboSaveData();
        if (source == null) return copy;
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), copy);
        return copy;
    }

    private static RunSummaryData CloneRunSummary(RunSummaryData source)
    {
        RunSummaryData copy = new RunSummaryData();
        if (source == null) return copy;
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), copy);
        return copy;
    }

    private static List<BuddyData> CloneBuddies(List<BuddyData> source)
    {
        List<BuddyData> result = new List<BuddyData>();
        if (source == null) return result;
        foreach (BuddyData buddy in source)
        {
            if (buddy == null) continue;
            BuddyData copy = new BuddyData();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(buddy), copy);
            copy.EnsureId();
            copy.EnsureRuntimeDefaults();
            result.Add(copy);
        }
        return result;
    }
}
