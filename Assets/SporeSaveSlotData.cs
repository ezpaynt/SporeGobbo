using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SporeSaveSlotData
{
    public const int CurrentSaveVersion = 1;

    [Header("Slot Metadata")]
    public int saveVersion = CurrentSaveVersion;
    public int slotIndex = 1;
    public bool hasSave = false;
    public string saveId = "";
    public string saveName = "";

    // Current leader/player name. This is display data only.
    // The permanent file identity is saveId / slotIndex, not this name.
    public string playerName = "Gobbo";

    public string createdAt = "";
    public string lastPlayedAt = "";
    public long createdUtcTicks = 0;
    public long lastPlayedUtcTicks = 0;

    [Header("Scene Policy")]
    public string nextSceneName = "CampScene"; // kept only for old display/compat. Continue/Load always open CampScene.

    [Header("Run/Camp")]
    public int currentRunNumber = 1;
    public int runNumber = 1; // legacy label alias
    public int maxActiveSquad = 5;
    public int campLevel = 1;

    [Header("Full Runtime Save")]
    public GobboSaveData player = new GobboSaveData();
    public List<BuddyData> ownedBuddies = new List<BuddyData>();
    public List<string> activeSquadIds = new List<string>();
    public string markedSuccessorId = "";

    [Header("Camp Unlocks")]
    public List<string> unlockedStations = new List<string>();
    public List<string> decorationsUnlocked = new List<string>();

    [Header("History")]
    public RunSummaryData lastRun = new RunSummaryData();
    public List<DeadBuddyRecord> deathHistory = new List<DeadBuddyRecord>();

    [Header("Derived Menu Fields")]
    public int buddyCount = 0;
    public int deadBuddyCount = 0;

    public static SporeSaveSlotData CreateNew(int slotIndex, string firstSceneName)
    {
        return CreateNew(slotIndex, "Gobbo", firstSceneName);
    }

    public static SporeSaveSlotData CreateNew(int slotIndex, string playerName, string firstSceneName)
    {
        DateTime now = DateTime.UtcNow;
        playerName = string.IsNullOrWhiteSpace(playerName) ? "Gobbo" : playerName.Trim();

        SporeSaveSlotData data = new SporeSaveSlotData
        {
            saveVersion = CurrentSaveVersion,
            slotIndex = Mathf.Clamp(slotIndex, 1, SporeSaveManager.SlotCount),
            hasSave = true,
            playerName = playerName,
            saveName = playerName + "'s Camp",
            createdUtcTicks = now.Ticks,
            lastPlayedUtcTicks = now.Ticks,
            nextSceneName = "CampScene",
            currentRunNumber = 1,
            runNumber = 1,
            maxActiveSquad = 5,
            campLevel = 1,
            player = new GobboSaveData(),
            ownedBuddies = new List<BuddyData>(),
            activeSquadIds = new List<string>(),
            markedSuccessorId = "",
            unlockedStations = new List<string>(),
            decorationsUnlocked = new List<string>(),
            lastRun = new RunSummaryData(),
            deathHistory = new List<DeadBuddyRecord>()
        };

        data.saveId = "slot_" + data.slotIndex;
        data.createdAt = now.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        data.lastPlayedAt = data.createdAt;
        data.Normalize();
        return data;
    }

    public void Normalize()
    {
        if (saveVersion <= 0) saveVersion = 1;
        slotIndex = Mathf.Clamp(slotIndex <= 0 ? 1 : slotIndex, 1, SporeSaveManager.SlotCount);
        saveId = "slot_" + slotIndex;

        if (string.IsNullOrWhiteSpace(playerName)) playerName = "Gobbo";
        if (string.IsNullOrWhiteSpace(saveName)) saveName = playerName + "'s Camp";

        if (createdUtcTicks <= 0) createdUtcTicks = DateTime.UtcNow.Ticks;
        if (lastPlayedUtcTicks <= 0) lastPlayedUtcTicks = createdUtcTicks;

        if (string.IsNullOrWhiteSpace(createdAt))
            createdAt = new DateTime(createdUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        if (string.IsNullOrWhiteSpace(lastPlayedAt))
            lastPlayedAt = new DateTime(lastPlayedUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        nextSceneName = "CampScene";
        if (currentRunNumber <= 0) currentRunNumber = Mathf.Max(1, runNumber);
        runNumber = currentRunNumber;
        if (maxActiveSquad <= 0) maxActiveSquad = 5;
        if (campLevel <= 0) campLevel = 1;

        if (player == null) player = new GobboSaveData();
        if (ownedBuddies == null) ownedBuddies = new List<BuddyData>();
        if (activeSquadIds == null) activeSquadIds = new List<string>();
        if (unlockedStations == null) unlockedStations = new List<string>();
        if (decorationsUnlocked == null) decorationsUnlocked = new List<string>();
        if (lastRun == null) lastRun = new RunSummaryData();
        if (deathHistory == null) deathHistory = new List<DeadBuddyRecord>();

        markedSuccessorId = string.IsNullOrWhiteSpace(markedSuccessorId) ? "" : markedSuccessorId.Trim();
        RefreshDerivedFields();
    }

    public void RefreshDerivedFields()
    {
        runNumber = currentRunNumber;
        buddyCount = ownedBuddies != null ? ownedBuddies.Count : 0;
        deadBuddyCount = deathHistory != null ? deathHistory.Count : 0;
        saveId = "slot_" + Mathf.Clamp(slotIndex <= 0 ? 1 : slotIndex, 1, SporeSaveManager.SlotCount);
        if (string.IsNullOrWhiteSpace(lastPlayedAt) && lastPlayedUtcTicks > 0)
            lastPlayedAt = new DateTime(lastPlayedUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    }

    public void RefreshCounts()
    {
        RefreshDerivedFields();
    }

    public string GetButtonLabel()
    {
        Normalize();
        return saveName
            + "\nLeader: " + playerName
            + "\nCamp " + campLevel + " • Run " + currentRunNumber
            + "\nBuddies: " + buddyCount + " • Bones: " + deadBuddyCount
            + "\n" + lastPlayedAt;
    }
}
