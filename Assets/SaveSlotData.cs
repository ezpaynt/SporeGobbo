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

    public GobboSaveData player = new GobboSaveData();
    public List<BuddyData> ownedBuddies = new List<BuddyData>();
    public List<string> activeSquadIds = new List<string>();

    public string markedSuccessorId = "";

    public List<string> unlockedStations = new List<string>();
    public List<string> decorationsUnlocked = new List<string>();
    public RunSummaryData lastRun = new RunSummaryData();
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
