using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SporeSaveSlotData
{
    [Header("Slot")]
    public int slotIndex = 1;
    public bool hasSave = false;
    public string saveId = "";
    public string saveName = "New Gobbo Camp";
    public string playerName = "Gobbo";
    public string createdAt = "";
    public string lastPlayedAt = "";
    public long createdUtcTicks = 0;
    public long lastPlayedUtcTicks = 0;

    [Header("Progress")]
    public int currentRunNumber = 1;
    public int runNumber = 1; // compatibility label used by old UI
    public int maxActiveSquad = 5;
    public int campLevel = 1;

    [Header("Player")]
    public GobboSaveData player = new GobboSaveData();

    [Header("Roster")]
    public List<BuddyData> ownedBuddies = new List<BuddyData>();
    public List<string> activeSquadIds = new List<string>();
    public int ownedBuddyCount = 0; // compatibility label used by old UI

    [Header("Camp")]
    public string markedSuccessorId = "";
    public List<string> unlockedStations = new List<string>();
    public List<string> decorationsUnlocked = new List<string>();

    [Header("History")]
    public List<DeadBuddyRecord> deathHistory = new List<DeadBuddyRecord>();
    public int deadBuddyCount = 0; // compatibility label used by old UI

    [Header("Last Run")]
    public RunSummaryData lastRun = new RunSummaryData();

    [Header("Legacy Scene Fields")]
    public string lastSceneName = "";
    public string nextSceneName = "CampScene"; // kept only so old inspectors do not break; load ignores this.

    public static SporeSaveSlotData CreateNew(int slotIndex, string firstSceneName)
    {
        return CreateNew(slotIndex, "Gobbo", firstSceneName);
    }

    public static SporeSaveSlotData CreateNew(int slotIndex, string playerName, string firstSceneName)
    {
        string cleanName = string.IsNullOrWhiteSpace(playerName) ? "Gobbo" : playerName.Trim();
        DateTime now = DateTime.UtcNow;

        SporeSaveSlotData data = new SporeSaveSlotData
        {
            slotIndex = Mathf.Clamp(slotIndex, 1, SporeSaveManager.SlotCount),
            hasSave = true,
            saveId = "slot_" + Mathf.Clamp(slotIndex, 1, SporeSaveManager.SlotCount),
            saveName = cleanName + "'s Camp",
            playerName = cleanName,
            createdUtcTicks = now.Ticks,
            lastPlayedUtcTicks = now.Ticks,
            createdAt = now.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            lastPlayedAt = now.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
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
            deathHistory = new List<DeadBuddyRecord>(),
            lastRun = new RunSummaryData(),
            lastSceneName = "",
            nextSceneName = "CampScene"
        };

        data.RefreshDerivedFields();
        return data;
    }

    public void RefreshDerivedFields()
    {
        hasSave = true;
        slotIndex = Mathf.Clamp(slotIndex, 1, SporeSaveManager.SlotCount);
        if (string.IsNullOrWhiteSpace(saveId)) saveId = "slot_" + slotIndex;
        if (string.IsNullOrWhiteSpace(playerName)) playerName = "Gobbo";
        if (string.IsNullOrWhiteSpace(saveName)) saveName = playerName + "'s Camp";
        if (createdUtcTicks <= 0) createdUtcTicks = DateTime.UtcNow.Ticks;
        if (lastPlayedUtcTicks <= 0) lastPlayedUtcTicks = createdUtcTicks;
        createdAt = new DateTime(createdUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        lastPlayedAt = new DateTime(lastPlayedUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        if (currentRunNumber <= 0) currentRunNumber = Mathf.Max(1, runNumber);
        runNumber = currentRunNumber;
        if (maxActiveSquad <= 0) maxActiveSquad = 5;
        if (campLevel <= 0) campLevel = 1;
        if (player == null) player = new GobboSaveData();
        if (ownedBuddies == null) ownedBuddies = new List<BuddyData>();
        if (activeSquadIds == null) activeSquadIds = new List<string>();
        if (unlockedStations == null) unlockedStations = new List<string>();
        if (decorationsUnlocked == null) decorationsUnlocked = new List<string>();
        if (deathHistory == null) deathHistory = new List<DeadBuddyRecord>();
        if (lastRun == null) lastRun = new RunSummaryData();
        ownedBuddyCount = ownedBuddies.Count;
        deadBuddyCount = deathHistory.Count;
        nextSceneName = "CampScene";
    }

    public string GetButtonLabel()
    {
        if (!hasSave) return "Slot " + slotIndex + " — Empty";
        RefreshDerivedFields();
        return saveName + "\nRun " + currentRunNumber + " — Gobbos: " + ownedBuddyCount + "\n" + lastPlayedAt;
    }
}
