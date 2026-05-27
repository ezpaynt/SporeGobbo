using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class GobboSaveSystem
{
    public const string SaveFolderName = "Saves";
    public const string IndexFileName = "save_index.json";

    public static string SaveFolderPath
    {
        get
        {
            string path = Path.Combine(Application.persistentDataPath, SaveFolderName);
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string IndexPath => Path.Combine(SaveFolderPath, IndexFileName);

    public static string GetSavePath(string saveId)
    {
        saveId = SanitizeSaveId(saveId);
        return Path.Combine(SaveFolderPath, saveId + ".json");
    }

    public static string CreateSaveId(string playerName)
    {
        string safeName = SanitizeFilePart(string.IsNullOrWhiteSpace(playerName) ? "Gobbo" : playerName.Trim());
        string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        string shortGuid = Guid.NewGuid().ToString("N").Substring(0, 8);
        return SanitizeSaveId(safeName + "_" + stamp + "_" + shortGuid);
    }

    public static void WriteSave(SaveSlotData save)
    {
        if (save == null) return;
        if (string.IsNullOrWhiteSpace(save.saveId)) save.saveId = CreateSaveId(save.playerName);
        save.lastPlayedUtcTicks = DateTime.UtcNow.Ticks;

        string json = JsonUtility.ToJson(save, true);
        File.WriteAllText(GetSavePath(save.saveId), json);

        SaveSlotIndexData index = LoadIndex();
        UpsertSummary(index, save);
        index.lastLoadedSaveId = save.saveId;
        WriteIndex(index);

        Debug.Log("[GobboSaveSystem] Wrote save: " + save.saveId + " for " + save.playerName);
    }

    public static SaveSlotData ReadSave(string saveId)
    {
        string path = GetSavePath(saveId);
        if (!File.Exists(path))
        {
            Debug.LogWarning("[GobboSaveSystem] Save not found: " + path);
            return null;
        }

        string json = File.ReadAllText(path);
        SaveSlotData save = JsonUtility.FromJson<SaveSlotData>(json);
        NormalizeSave(save);
        return save;
    }

    public static SaveSlotIndexData LoadIndex()
    {
        if (!File.Exists(IndexPath)) return new SaveSlotIndexData();
        try
        {
            SaveSlotIndexData index = JsonUtility.FromJson<SaveSlotIndexData>(File.ReadAllText(IndexPath));
            if (index == null) index = new SaveSlotIndexData();
            if (index.saves == null) index.saves = new List<SaveSlotSummary>();
            return index;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[GobboSaveSystem] Could not read save index. Starting fresh. " + ex.Message);
            return new SaveSlotIndexData();
        }
    }

    public static void WriteIndex(SaveSlotIndexData index)
    {
        if (index == null) index = new SaveSlotIndexData();
        if (index.saves == null) index.saves = new List<SaveSlotSummary>();
        File.WriteAllText(IndexPath, JsonUtility.ToJson(index, true));
    }

    public static List<SaveSlotSummary> ListSaves()
    {
        return LoadIndex().saves;
    }

    public static SaveSlotData ReadLastLoadedSave()
    {
        SaveSlotIndexData index = LoadIndex();
        if (string.IsNullOrWhiteSpace(index.lastLoadedSaveId)) return null;
        return ReadSave(index.lastLoadedSaveId);
    }

    public static void DeleteSave(string saveId)
    {
        string path = GetSavePath(saveId);
        if (File.Exists(path)) File.Delete(path);

        SaveSlotIndexData index = LoadIndex();
        index.saves.RemoveAll(s => s != null && s.saveId == saveId);
        if (index.lastLoadedSaveId == saveId) index.lastLoadedSaveId = "";
        WriteIndex(index);
    }

    static void UpsertSummary(SaveSlotIndexData index, SaveSlotData save)
    {
        if (index.saves == null) index.saves = new List<SaveSlotSummary>();
        SaveSlotSummary summary = index.saves.Find(s => s != null && s.saveId == save.saveId);
        if (summary == null)
        {
            summary = new SaveSlotSummary();
            index.saves.Add(summary);
        }

        summary.saveId = save.saveId;
        summary.saveName = save.saveName;
        summary.playerName = save.playerName;
        summary.createdUtcTicks = save.createdUtcTicks;
        summary.lastPlayedUtcTicks = save.lastPlayedUtcTicks;
        summary.currentRunNumber = save.currentRunNumber;
        summary.buddyCount = save.ownedBuddies != null ? save.ownedBuddies.Count : 0;

        index.saves.Sort((a, b) => b.lastPlayedUtcTicks.CompareTo(a.lastPlayedUtcTicks));
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

    static string SanitizeSaveId(string raw)
    {
        string id = SanitizeFilePart(raw);
        return string.IsNullOrWhiteSpace(id) ? "save_" + Guid.NewGuid().ToString("N") : id;
    }

    static string SanitizeFilePart(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Gobbo";
        foreach (char c in Path.GetInvalidFileNameChars()) raw = raw.Replace(c, '_');
        raw = raw.Replace(' ', '_').Trim('_');
        return string.IsNullOrWhiteSpace(raw) ? "Gobbo" : raw;
    }
}
