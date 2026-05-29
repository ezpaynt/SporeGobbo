using System;
using System.Collections.Generic;

/// <summary>
/// Legacy/simple save-slot DTO kept for older menu/index code.
/// The active save system is SporeSaveSlotData, but this file must compile
/// without GobboSaveData/BuddyData after the unified gobbo purge.
/// </summary>
[Serializable]
public class SaveSlotData
{
    public string saveId = "";
    public string saveName = "";
    public string playerName = "Gobbo";

    public long createdUtcTicks = 0;
    public long lastPlayedUtcTicks = 0;

    public int currentRunNumber = 1;
    public int maxActiveSquad = 5;
    public int campLevel = 1;

    public GobboUnitSaveData player = new GobboUnitSaveData();
    public GobboUnitSaveData leader = new GobboUnitSaveData();
    public List<GobboUnitSaveData> ownedGobbos = new List<GobboUnitSaveData>();

    public List<string> activeSquadIds = new List<string>();
    public string markedSuccessorId = "";

    public List<string> unlockedStations = new List<string>();
    public List<string> decorationsUnlocked = new List<string>();

    public RunSummaryData lastRun = new RunSummaryData();

    public void EnsureDefaults()
    {
        if (leader == null)
            leader = new GobboUnitSaveData();

        leader.EnsureRuntimeDefaults();
        leader.isLeader = true;

        if (player == null)
            player = leader.Clone();

        player.EnsureRuntimeDefaults();
        player.isLeader = true;

        if (ownedGobbos == null)
            ownedGobbos = new List<GobboUnitSaveData>();

        if (activeSquadIds == null)
            activeSquadIds = new List<string>();

        if (unlockedStations == null)
            unlockedStations = new List<string>();

        if (decorationsUnlocked == null)
            decorationsUnlocked = new List<string>();

        if (lastRun == null)
            lastRun = new RunSummaryData();
    }
}

[Serializable]
public class SaveSlotSummary
{
    public string saveId = "";
    public string saveName = "";
    public string playerName = "";
    public long createdUtcTicks = 0;
    public long lastPlayedUtcTicks = 0;
    public int currentRunNumber = 1;
    public int buddyCount = 0;
}

[Serializable]
public class SaveSlotIndexData
{
    public List<SaveSlotSummary> saves = new List<SaveSlotSummary>();
    public string lastLoadedSaveId = "";
}
