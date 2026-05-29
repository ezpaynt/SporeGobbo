using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deprecated compatibility wrapper. The real save files are SporeSaveManager slot_1/2/3 JSON files.
/// This wrapper now speaks unified GobboUnitSaveData only.
/// </summary>
public static class GobboSaveSystem
{
    public const string SaveFolderName = "Saves";
    public const string IndexFileName = "save_index_unused.json";
    public static string SaveFolderPath => SporeSaveManager.SaveFolder;
    public static string IndexPath => System.IO.Path.Combine(SaveFolderPath, IndexFileName);

    public static string GetSavePath(string saveId)
    {
        int slot = SlotFromSaveId(saveId);
        if (slot <= 0) slot = SporeSaveManager.GetCurrentSlot();
        if (slot <= 0) slot = SporeSaveManager.GetLastPlayedSlot();
        if (slot <= 0) slot = 1;
        return SporeSaveManager.GetSlotPath(slot);
    }

    public static string CreateSaveId(string playerName)
    {
        int slot = SporeSaveManager.FindFirstEmptySlot();
        if (slot <= 0) slot = SporeSaveManager.GetCurrentSlot();
        if (slot <= 0) slot = 1;
        return "slot_" + slot;
    }

    public static void WriteSave(SaveSlotData save)
    {
        if (save == null) return;
        save.Normalize();
        int slot = SlotFromSaveId(save.saveId);
        if (slot <= 0) slot = SporeSaveManager.GetCurrentSlot();
        if (slot <= 0) slot = SporeSaveManager.FindFirstEmptySlot();
        if (slot <= 0)
        {
            Debug.LogWarning("[GobboSaveSystem] Could not write legacy SaveSlotData. No slot available.");
            return;
        }

        SporeSaveSlotData data = new SporeSaveSlotData
        {
            slotIndex = slot,
            hasSave = true,
            saveId = "slot_" + slot,
            saveName = save.saveName,
            playerName = save.playerName,
            createdUtcTicks = save.createdUtcTicks,
            lastPlayedUtcTicks = save.lastPlayedUtcTicks,
            currentRunNumber = save.currentRunNumber,
            maxActiveSquad = save.maxActiveSquad,
            campLevel = save.campLevel,
            leader = save.leader != null ? save.leader.CloneUnit() : new GobboUnitSaveData { isLeader = true, displayName = save.playerName },
            ownedGobbos = CloneUnits(save.ownedGobbos),
            activeSquadIds = save.activeSquadIds != null ? new List<string>(save.activeSquadIds) : new List<string>(),
            markedSuccessorId = save.markedSuccessorId,
            unlockedStations = save.unlockedStations != null ? new List<string>(save.unlockedStations) : new List<string>(),
            decorationsUnlocked = save.decorationsUnlocked != null ? new List<string>(save.decorationsUnlocked) : new List<string>(),
            lastRun = save.lastRun
        };
        SporeSaveManager.SaveSlot(data);
    }

    public static SaveSlotData ReadSave(string saveId)
    {
        int slot = SlotFromSaveId(saveId);
        if (slot <= 0) return null;
        return ToLegacy(SporeSaveManager.LoadSlot(slot));
    }

    public static SaveSlotIndexData LoadIndex()
    {
        SaveSlotIndexData index = new SaveSlotIndexData();
        index.saves = ListSaves();
        SporeSaveSlotData last = SporeSaveManager.LoadLastPlayedSlot();
        index.lastLoadedSaveId = last != null ? last.saveId : "";
        return index;
    }

    public static void WriteIndex(SaveSlotIndexData index) { }

    public static List<SaveSlotSummary> ListSaves()
    {
        List<SaveSlotSummary> summaries = new List<SaveSlotSummary>();
        for (int i = 1; i <= SporeSaveManager.SlotCount; i++)
        {
            SporeSaveSlotData data = SporeSaveManager.LoadSlot(i);
            if (data == null || !data.hasSave) continue;
            data.Normalize();
            summaries.Add(new SaveSlotSummary
            {
                saveId = data.saveId,
                saveName = data.saveName,
                playerName = data.playerName,
                createdUtcTicks = data.createdUtcTicks,
                lastPlayedUtcTicks = data.lastPlayedUtcTicks,
                currentRunNumber = data.currentRunNumber,
                buddyCount = data.ownedGobbos != null ? data.ownedGobbos.Count : 0
            });
        }
        summaries.Sort((a, b) => b.lastPlayedUtcTicks.CompareTo(a.lastPlayedUtcTicks));
        return summaries;
    }

    public static SaveSlotData ReadLastLoadedSave() => ToLegacy(SporeSaveManager.LoadLastPlayedSlot());

    public static void DeleteSave(string saveId)
    {
        int slot = SlotFromSaveId(saveId);
        if (slot > 0) SporeSaveManager.DeleteSlot(slot);
    }

    public static void NormalizeSave(SaveSlotData save) => save?.Normalize();

    static int SlotFromSaveId(string saveId)
    {
        if (string.IsNullOrWhiteSpace(saveId)) return 0;
        if (saveId.StartsWith("slot_"))
        {
            string text = saveId.Substring("slot_".Length);
            if (int.TryParse(text, out int slot)) return Mathf.Clamp(slot, 1, SporeSaveManager.SlotCount);
        }
        return 0;
    }

    static SaveSlotData ToLegacy(SporeSaveSlotData data)
    {
        if (data == null || !data.hasSave) return null;
        data.Normalize();
        SaveSlotData save = new SaveSlotData
        {
            saveId = data.saveId,
            saveName = data.saveName,
            playerName = data.playerName,
            createdUtcTicks = data.createdUtcTicks,
            lastPlayedUtcTicks = data.lastPlayedUtcTicks,
            currentRunNumber = data.currentRunNumber,
            maxActiveSquad = data.maxActiveSquad,
            campLevel = data.campLevel,
            leader = data.leader != null ? data.leader.CloneUnit() : new GobboUnitSaveData { isLeader = true, displayName = data.playerName },
            ownedGobbos = CloneUnits(data.ownedGobbos),
            activeSquadIds = data.activeSquadIds != null ? new List<string>(data.activeSquadIds) : new List<string>(),
            markedSuccessorId = data.markedSuccessorId,
            unlockedStations = data.unlockedStations != null ? new List<string>(data.unlockedStations) : new List<string>(),
            decorationsUnlocked = data.decorationsUnlocked != null ? new List<string>(data.decorationsUnlocked) : new List<string>(),
            lastRun = data.lastRun
        };
        save.Normalize();
        return save;
    }

    static List<GobboUnitSaveData> CloneUnits(List<GobboUnitSaveData> source)
    {
        List<GobboUnitSaveData> result = new List<GobboUnitSaveData>();
        if (source == null) return result;
        foreach (GobboUnitSaveData unit in source)
        {
            if (unit != null) result.Add(unit.CloneUnit());
        }
        return result;
    }
}
