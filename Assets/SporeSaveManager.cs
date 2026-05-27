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
        slotIndex = Mathf.Clamp(slotIndex, 1, SlotCount);
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
        {
            if (HasSave(i)) count++;
        }
        return count;
    }

    public static bool CanCreateNewGame()
    {
        return FindFirstEmptySlot() > 0;
    }

    public static int FindFirstEmptySlot()
    {
        for (int i = 1; i <= SlotCount; i++)
        {
            if (!HasSave(i)) return i;
        }
        return 0;
    }

    public static SporeSaveSlotData LoadSlot(int slotIndex)
    {
        slotIndex = Mathf.Clamp(slotIndex, 1, SlotCount);
        string path = GetSlotPath(slotIndex);
        if (!File.Exists(path)) return new SporeSaveSlotData { slotIndex = slotIndex, hasSave = false, saveId = "slot_" + slotIndex };

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
        data.slotIndex = Mathf.Clamp(data.slotIndex, 1, SlotCount);
        data.saveId = "slot_" + data.slotIndex;
        data.hasSave = true;
        data.lastPlayedUtcTicks = DateTime.UtcNow.Ticks;
        Normalize(data);

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(GetSlotPath(data.slotIndex), json);
        SetCurrentSlot(data.slotIndex);
        SetLastPlayedSlot(data.slotIndex);
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

    public static SporeSaveSlotData CreateNewGame(int slotIndex, string playerName, string firstSceneName, bool allowOverwrite = false)
    {
        slotIndex = Mathf.Clamp(slotIndex, 1, SlotCount);
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

    public static void SaveCurrentSlotFromGameState()
    {
        int slotIndex = GetCurrentSlot();
        if (slotIndex <= 0) slotIndex = GetLastPlayedSlot();
        if (slotIndex <= 0)
        {
            Debug.LogWarning("[SporeSaveManager] No current slot set. Cannot save GameState.");
            return;
        }

        SporeSaveSlotData existing = LoadSlot(slotIndex);
        SporeSaveSlotData data = BuildSlotFromGameState(slotIndex, existing);
        SaveSlot(data);
    }

    public static SporeSaveSlotData BuildSlotFromGameState(int slotIndex, SporeSaveSlotData existing = null)
    {
        slotIndex = Mathf.Clamp(slotIndex, 1, SlotCount);
        if (existing == null || !existing.hasSave) existing = SporeSaveSlotData.CreateNew(slotIndex, "Gobbo", "SampleScene");

        GameState gs = GameState.Instance;
        if (gs != null)
        {
            gs.RepairRosterState();
            existing.currentRunNumber = gs.currentRunNumber;
            existing.runNumber = gs.currentRunNumber;
            existing.maxActiveSquad = gs.maxActiveSquad;
            existing.campLevel = gs.campLevel;
            existing.player = CloneGobbo(gs.gobbo);
            existing.ownedBuddies = CloneBuddies(gs.ownedBuddies);
            existing.activeSquadIds = gs.activeSquadIds != null ? new List<string>(gs.activeSquadIds) : new List<string>();
            existing.unlockedStations = gs.unlockedStations != null ? new List<string>(gs.unlockedStations) : new List<string>();
            existing.decorationsUnlocked = gs.decorationsUnlocked != null ? new List<string>(gs.decorationsUnlocked) : new List<string>();
            existing.lastRun = CloneRunSummary(gs.lastRun);
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
        existing.RefreshDerivedFields();
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

        CampSuccessorPreferenceStore successorStore = CampSuccessorPreferenceStore.GetOrCreate();
        if (successorStore != null)
        {
            if (string.IsNullOrWhiteSpace(data.markedSuccessorId)) successorStore.ClearSuccessor();
            else successorStore.SetMarkedSuccessor(data.markedSuccessorId);
            successorStore.ValidateAgainstRoster();
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
            if (best == null || data.lastPlayedUtcTicks > best.lastPlayedUtcTicks) best = data;
        }
        return best;
    }

    public static SporeSaveSlotData LoadLastPlayedSlot()
    {
        SporeSaveSlotData mostRecent = LoadMostRecentSlot();
        if (mostRecent != null) return mostRecent;

        int slot = GetLastPlayedSlot();
        if (slot <= 0) return null;
        SporeSaveSlotData data = LoadSlot(slot);
        return data != null && data.hasSave ? data : null;
    }

    public static bool HasAnySave()
    {
        return LoadMostRecentSlot() != null;
    }

    public static void DeleteSlot(int slotIndex)
    {
        slotIndex = Mathf.Clamp(slotIndex, 1, SlotCount);
        string path = GetSlotPath(slotIndex);
        if (File.Exists(path)) File.Delete(path);
        if (GetLastPlayedSlot() == slotIndex) PlayerPrefs.DeleteKey(LastSlotKey);
        if (GetCurrentSlot() == slotIndex) PlayerPrefs.DeleteKey(CurrentSlotKey);
        PlayerPrefs.Save();
    }

    public static int GetLastPlayedSlot()
    {
        return PlayerPrefs.GetInt(LastSlotKey, 0);
    }

    public static void SetLastPlayedSlot(int slotIndex)
    {
        slotIndex = Mathf.Clamp(slotIndex, 1, SlotCount);
        PlayerPrefs.SetInt(LastSlotKey, slotIndex);
        PlayerPrefs.Save();
    }

    public static int GetCurrentSlot()
    {
        return PlayerPrefs.GetInt(CurrentSlotKey, 0);
    }

    public static void SetCurrentSlot(int slotIndex)
    {
        slotIndex = Mathf.Clamp(slotIndex, 1, SlotCount);
        PlayerPrefs.SetInt(CurrentSlotKey, slotIndex);
        PlayerPrefs.Save();
    }

    public static void Normalize(SporeSaveSlotData data)
    {
        if (data == null) return;
        data.RefreshDerivedFields();
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

    static void EnsureGameState()
    {
        if (GameState.Instance != null) return;
        GameObject obj = new GameObject("GameState");
        obj.AddComponent<GameState>();
    }

    static GobboSaveData CloneGobbo(GobboSaveData source)
    {
        GobboSaveData copy = new GobboSaveData();
        if (source == null) return copy;
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), copy);
        return copy;
    }

    static RunSummaryData CloneRunSummary(RunSummaryData source)
    {
        RunSummaryData copy = new RunSummaryData();
        if (source == null) return copy;
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), copy);
        return copy;
    }

    static List<BuddyData> CloneBuddies(List<BuddyData> source)
    {
        List<BuddyData> result = new List<BuddyData>();
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
}
