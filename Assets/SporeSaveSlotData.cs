using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SporeSaveSlotData
{
    public const int CurrentSaveVersion = 3;

    [Header("Slot Metadata")]
    public int saveVersion = CurrentSaveVersion;
    public bool hasSave = false;
    public int slotIndex = 1;
    public string saveId = "slot_1";
    public string saveName = "Gobbo's Camp";
    public string playerName = "Gobbo";
    public long createdUtcTicks = 0;
    public long lastPlayedUtcTicks = 0;
    public string lastPlayedAt = "";
    public string nextSceneName = "CampScene"; // compatibility only; Continue/Load should ignore and open CampScene.

    [Header("Unified Gobbo Data")]
    public GobboUnitSaveData leader = new GobboUnitSaveData { isLeader = true, displayName = "Gobbo" };
    public List<GobboUnitSaveData> ownedGobbos = new List<GobboUnitSaveData>();

    [Header("Compatibility Mirrors")]
    public GobboSaveData player = new GobboSaveData();
    public List<BuddyData> ownedBuddies = new List<BuddyData>();

    public List<string> activeSquadIds = new List<string>();
    public string markedSuccessorId = "";

    [Header("Camp")]
    public int currentRunNumber = 1;
    public int runNumber = 1; // card compatibility
    public int maxActiveSquad = 5;
    public int campLevel = 1;
    public List<string> unlockedStations = new List<string>();
    public List<string> decorationsUnlocked = new List<string>();

    [Header("History")]
    public RunSummaryData lastRun = new RunSummaryData();
    public List<DeadBuddyRecord> deathHistory = new List<DeadBuddyRecord>();

    [Header("Derived Card Data")]
    public int buddyCount = 0;
    public int fallenCount = 0;

    public static SporeSaveSlotData CreateNew(int slotIndex, string playerName, string firstSceneName)
    {
        string cleanName = string.IsNullOrWhiteSpace(playerName) ? "Gobbo" : playerName.Trim();
        DateTime now = DateTime.UtcNow;
        GobboSaveData leaderSave = new GobboSaveData();
        leaderSave.displayName = cleanName;
        leaderSave.EnsureLeaderIdentity(cleanName);

        SporeSaveSlotData data = new SporeSaveSlotData
        {
            saveVersion = CurrentSaveVersion,
            hasSave = true,
            slotIndex = Mathf.Clamp(slotIndex, 1, SporeSaveManager.SlotCount),
            saveId = "slot_" + Mathf.Clamp(slotIndex, 1, SporeSaveManager.SlotCount),
            playerName = cleanName,
            saveName = cleanName + "'s Camp",
            createdUtcTicks = now.Ticks,
            lastPlayedUtcTicks = now.Ticks,
            lastPlayedAt = now.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            nextSceneName = string.IsNullOrWhiteSpace(firstSceneName) ? "SampleScene" : firstSceneName,
            player = leaderSave,
            leader = leaderSave.ToUnitSave(),
            ownedGobbos = new List<GobboUnitSaveData>(),
            ownedBuddies = new List<BuddyData>(),
            activeSquadIds = new List<string>(),
            markedSuccessorId = "",
            currentRunNumber = 1,
            runNumber = 1,
            maxActiveSquad = 5,
            campLevel = 1,
            unlockedStations = new List<string>(),
            decorationsUnlocked = new List<string>(),
            lastRun = new RunSummaryData(),
            deathHistory = new List<DeadBuddyRecord>()
        };

        data.Normalize();
        return data;
    }

    public void Normalize()
    {
        saveVersion = Mathf.Max(saveVersion, CurrentSaveVersion);
        slotIndex = Mathf.Clamp(slotIndex <= 0 ? 1 : slotIndex, 1, SporeSaveManager.SlotCount);
        saveId = "slot_" + slotIndex;
        hasSave = true;

        if (createdUtcTicks <= 0) createdUtcTicks = DateTime.UtcNow.Ticks;
        if (lastPlayedUtcTicks <= 0) lastPlayedUtcTicks = createdUtcTicks;
        if (string.IsNullOrWhiteSpace(lastPlayedAt))
            lastPlayedAt = new DateTime(lastPlayedUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        if (leader == null) leader = player != null ? player.ToUnitSave() : new GobboUnitSaveData { isLeader = true };
        leader.isLeader = true;
        leader.EnsureIdentity(string.IsNullOrWhiteSpace(playerName) ? "Gobbo" : playerName);
        player = leader.ToLeaderSave();

        if (string.IsNullOrWhiteSpace(playerName)) playerName = leader.displayName;
        if (string.IsNullOrWhiteSpace(saveName)) saveName = playerName + "'s Camp";
        if (string.IsNullOrWhiteSpace(nextSceneName)) nextSceneName = "CampScene";

        ownedGobbos ??= new List<GobboUnitSaveData>();
        ownedBuddies ??= new List<BuddyData>();
        activeSquadIds ??= new List<string>();
        unlockedStations ??= new List<string>();
        decorationsUnlocked ??= new List<string>();
        deathHistory ??= new List<DeadBuddyRecord>();
        if (lastRun == null) lastRun = new RunSummaryData();

        // Upgrade old saves/mirrors into the unified list.
        foreach (BuddyData buddy in ownedBuddies)
        {
            if (buddy == null) continue;
            buddy.EnsureId();
            buddy.EnsureRuntimeDefaults();
            if (FindUnit(ownedGobbos, buddy.uniqueId) == null)
            {
                GobboUnitSaveData copy = buddy.CloneUnit();
                copy.isLeader = false;
                ownedGobbos.Add(copy);
            }
        }

        ownedGobbos.RemoveAll(g => g == null);
        Dictionary<string, GobboUnitSaveData> unique = new Dictionary<string, GobboUnitSaveData>();
        List<GobboUnitSaveData> repaired = new List<GobboUnitSaveData>();
        foreach (GobboUnitSaveData unit in ownedGobbos)
        {
            if (unit == null) continue;
            unit.isLeader = false;
            unit.EnsureRuntimeDefaults();
            if (unique.ContainsKey(unit.uniqueId)) continue;
            unique.Add(unit.uniqueId, unit);
            repaired.Add(unit);
        }
        ownedGobbos = repaired;

        HashSet<string> ownedIds = new HashSet<string>();
        foreach (GobboUnitSaveData unit in ownedGobbos)
        {
            ownedIds.Add(unit.uniqueId);
            unit.isInActiveSquad = false;
        }

        List<string> fixedActive = new List<string>();
        foreach (string id in activeSquadIds)
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (!ownedIds.Contains(id)) continue;
            if (fixedActive.Contains(id)) continue;
            if (fixedActive.Count >= Mathf.Max(1, maxActiveSquad)) break;
            fixedActive.Add(id);
        }
        activeSquadIds = fixedActive;

        foreach (GobboUnitSaveData unit in ownedGobbos)
            unit.isInActiveSquad = activeSquadIds.Contains(unit.uniqueId);

        if (!string.IsNullOrWhiteSpace(markedSuccessorId) && !ownedIds.Contains(markedSuccessorId)) markedSuccessorId = "";

        currentRunNumber = Mathf.Max(1, currentRunNumber);
        runNumber = currentRunNumber;
        maxActiveSquad = Mathf.Max(1, maxActiveSquad);
        campLevel = Mathf.Max(1, campLevel);

        RebuildBuddyMirror();
        RefreshDerivedFields();
    }

    public void RefreshDerivedFields()
    {
        buddyCount = ownedGobbos != null ? ownedGobbos.Count : 0;
        fallenCount = deathHistory != null ? deathHistory.Count : 0;
        if (leader != null && !string.IsNullOrWhiteSpace(leader.displayName)) playerName = leader.displayName;
        if (string.IsNullOrWhiteSpace(saveName)) saveName = playerName + "'s Camp";
    }

    public string GetButtonLabel()
    {
        Normalize();
        return saveName + "\nLeader: " + playerName + "\nCamp " + campLevel + " • Run " + currentRunNumber + "\nBuddies: " + buddyCount + "\n" + lastPlayedAt;
    }

    void RebuildBuddyMirror()
    {
        ownedBuddies.Clear();
        foreach (GobboUnitSaveData unit in ownedGobbos)
        {
            BuddyData buddy = unit.AsBuddyData();
            if (buddy != null) ownedBuddies.Add(buddy);
        }
    }

    static GobboUnitSaveData FindUnit(List<GobboUnitSaveData> units, string id)
    {
        if (units == null || string.IsNullOrWhiteSpace(id)) return null;
        foreach (GobboUnitSaveData unit in units)
        {
            if (unit != null && unit.uniqueId == id) return unit;
        }
        return null;
    }
}
