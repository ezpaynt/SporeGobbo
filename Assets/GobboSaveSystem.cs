using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deprecated compatibility wrapper.
/// The real save files are the 3 SporeSaveManager slots: slot_1.json, slot_2.json, slot_3.json.
/// This wrapper prevents older scripts from creating a second save-index system.
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
            player = save.player,
            ownedBuddies = save.ownedBuddies,
            activeSquadIds = save.activeSquadIds,
            markedSuccessorId = save.markedSuccessorId,
            unlockedStations = save.unlockedStations,
            decorationsUnlocked = save.decorationsUnlocked,
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

    public static void WriteIndex(SaveSlotIndexData index)
    {
        // No-op. SporeSaveManager discovers slots from slot files directly.
    }

    public static List<SaveSlotSummary> ListSaves()
    {
        List<SaveSlotSummary> summaries = new List<SaveSlotSummary>();
        for (int i = 1; i <= SporeSaveManager.SlotCount; i++)
        {
            SporeSaveSlotData data = SporeSaveManager.LoadSlot(i);
            if (data == null || !data.hasSave) continue;
            summaries.Add(new SaveSlotSummary
            {
                saveId = data.saveId,
                saveName = data.saveName,
                playerName = data.playerName,
                createdUtcTicks = data.createdUtcTicks,
                lastPlayedUtcTicks = data.lastPlayedUtcTicks,
                currentRunNumber = data.currentRunNumber,
                buddyCount = data.ownedBuddies != null ? data.ownedBuddies.Count : 0
            });
        }
        summaries.Sort((a, b) => b.lastPlayedUtcTicks.CompareTo(a.lastPlayedUtcTicks));
        return summaries;
    }

    public static SaveSlotData ReadLastLoadedSave()
    {
        return ToLegacy(SporeSaveManager.LoadLastPlayedSlot());
    }

    public static void DeleteSave(string saveId)
    {
        int slot = SlotFromSaveId(saveId);
        if (slot > 0) SporeSaveManager.DeleteSlot(slot);
    }

    public static void NormalizeSave(SaveSlotData save)
    {
        if (save == null) return;
        if (string.IsNullOrWhiteSpace(save.playerName)) save.playerName = "Gobbo";
        if (string.IsNullOrWhiteSpace(save.saveName)) save.saveName = save.playerName + "'s Camp";
        if (save.createdUtcTicks <= 0) save.createdUtcTicks = DateTime.UtcNow.Ticks;
        if (save.lastPlayedUtcTicks <= 0) save.lastPlayedUtcTicks = save.createdUtcTicks;
        if (save.player == null) save.player = new GobboSaveData();
        if (save.ownedBuddies == null) save.ownedBuddies = new List<BuddyData>();
        if (save.activeSquadIds == null) save.activeSquadIds = new List<string>();
        if (save.unlockedStations == null) save.unlockedStations = new List<string>();
        if (save.decorationsUnlocked == null) save.decorationsUnlocked = new List<string>();
        if (save.lastRun == null) save.lastRun = new RunSummaryData();
        if (save.maxActiveSquad <= 0) save.maxActiveSquad = 5;
        if (save.currentRunNumber <= 0) save.currentRunNumber = 1;
        if (save.campLevel <= 0) save.campLevel = 1;
    }

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
        data.RefreshDerivedFields();
        SaveSlotData save = new SaveSlotData();
        save.saveId = data.saveId;
        save.saveName = data.saveName;
        save.playerName = data.playerName;
        save.createdUtcTicks = data.createdUtcTicks;
        save.lastPlayedUtcTicks = data.lastPlayedUtcTicks;
        save.currentRunNumber = data.currentRunNumber;
        save.maxActiveSquad = data.maxActiveSquad;
        save.campLevel = data.campLevel;
        save.player = data.player;
        save.ownedBuddies = data.ownedBuddies;
        save.activeSquadIds = data.activeSquadIds;
        save.markedSuccessorId = data.markedSuccessorId;
        save.unlockedStations = data.unlockedStations;
        save.decorationsUnlocked = data.decorationsUnlocked;
        save.lastRun = data.lastRun;
        return save;
    }
}
