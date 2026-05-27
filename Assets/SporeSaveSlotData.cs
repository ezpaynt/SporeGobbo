using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SporeSaveSlotData
{
    [Header("Slot Metadata")]
    public int slotIndex = 1;
    public bool hasSave = false;
    public string saveId = "";
    public string saveName = "";
    public string playerName = "Gobbo";
    public string createdAt = "";
    public string lastPlayedAt = "";
    public long createdUtcTicks = 0;
    public long lastPlayedUtcTicks = 0;

    [Header("Scene Policy")]
    public string nextSceneName = "CampScene"; // kept for old UI display, but Continue/Load should always go to CampScene.

    [Header("Run/Camp")]
    public int currentRunNumber = 1;
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

    // Legacy menu fields so older UI code keeps compiling/labeling.
    public int buddyCount = 0;
    public int deadBuddyCount = 0;

    public static SporeSaveSlotData CreateNew(int slotIndex, string firstSceneName, string playerName = "Gobbo")
    {
        DateTime now = DateTime.UtcNow;
        playerName = string.IsNullOrWhiteSpace(playerName) ? "Gobbo" : playerName.Trim();

        SporeSaveSlotData data = new SporeSaveSlotData();
        data.slotIndex = Mathf.Clamp(slotIndex, 1, SporeSaveManager.SlotCount);
        data.hasSave = true;
        data.saveId = "slot_" + data.slotIndex;
        data.playerName = playerName;
        data.saveName = playerName + "'s Camp";
        data.createdUtcTicks = now.Ticks;
        data.lastPlayedUtcTicks = now.Ticks;
        data.createdAt = now.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        data.lastPlayedAt = data.createdAt;
        data.nextSceneName = "CampScene";
        data.currentRunNumber = 1;
        data.maxActiveSquad = 5;
        data.campLevel = 1;
        data.player = new GobboSaveData();
        data.ownedBuddies = new List<BuddyData>();
        data.activeSquadIds = new List<string>();
        data.markedSuccessorId = "";
        data.unlockedStations = new List<string>();
        data.decorationsUnlocked = new List<string>();
        data.lastRun = new RunSummaryData();
        data.deathHistory = new List<DeadBuddyRecord>();
        data.RefreshCounts();
        return data;
    }

    public void Normalize()
    {
        slotIndex = Mathf.Clamp(slotIndex <= 0 ? 1 : slotIndex, 1, SporeSaveManager.SlotCount);
        if (string.IsNullOrWhiteSpace(saveId)) saveId = "slot_" + slotIndex;
        if (string.IsNullOrWhiteSpace(playerName)) playerName = "Gobbo";
        if (string.IsNullOrWhiteSpace(saveName)) saveName = playerName + "'s Camp";
        if (createdUtcTicks <= 0) createdUtcTicks = DateTime.UtcNow.Ticks;
        if (lastPlayedUtcTicks <= 0) lastPlayedUtcTicks = createdUtcTicks;
        if (string.IsNullOrWhiteSpace(createdAt)) createdAt = new DateTime(createdUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        if (string.IsNullOrWhiteSpace(lastPlayedAt)) lastPlayedAt = new DateTime(lastPlayedUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        if (string.IsNullOrWhiteSpace(nextSceneName)) nextSceneName = "CampScene";
        if (currentRunNumber <= 0) currentRunNumber = 1;
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
        RefreshCounts();
    }

    public void RefreshCounts()
    {
        buddyCount = ownedBuddies != null ? ownedBuddies.Count : 0;
        deadBuddyCount = deathHistory != null ? deathHistory.Count : 0;
    }

    public string GetButtonLabel()
    {
        Normalize();
        return saveName + "\nRun " + currentRunNumber + " • Buddies " + buddyCount + "\n" + lastPlayedAt;
    }
}
