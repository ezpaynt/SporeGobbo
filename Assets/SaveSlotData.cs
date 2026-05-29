using System;
using System.Collections.Generic;

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

    public GobboUnitSaveData leader = new GobboUnitSaveData { isLeader = true, displayName = "Gobbo" };
    public List<GobboUnitSaveData> ownedGobbos = new List<GobboUnitSaveData>();
    public List<string> activeSquadIds = new List<string>();
    public string markedSuccessorId = "";
    public List<string> unlockedStations = new List<string>();
    public List<string> decorationsUnlocked = new List<string>();
    public RunSummaryData lastRun = new RunSummaryData();

    public void Normalize()
    {
        if (leader == null) leader = new GobboUnitSaveData { isLeader = true, displayName = string.IsNullOrWhiteSpace(playerName) ? "Gobbo" : playerName };
        leader.isLeader = true;
        leader.EnsureIdentity(string.IsNullOrWhiteSpace(playerName) ? "Gobbo" : playerName);
        if (string.IsNullOrWhiteSpace(playerName)) playerName = leader.displayName;
        if (string.IsNullOrWhiteSpace(saveName)) saveName = playerName + "'s Camp";
        ownedGobbos ??= new List<GobboUnitSaveData>();
        activeSquadIds ??= new List<string>();
        unlockedStations ??= new List<string>();
        decorationsUnlocked ??= new List<string>();
        if (lastRun == null) lastRun = new RunSummaryData();
        if (currentRunNumber <= 0) currentRunNumber = 1;
        if (maxActiveSquad <= 0) maxActiveSquad = 5;
        if (campLevel <= 0) campLevel = 1;
        if (createdUtcTicks <= 0) createdUtcTicks = DateTime.UtcNow.Ticks;
        if (lastPlayedUtcTicks <= 0) lastPlayedUtcTicks = createdUtcTicks;
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
