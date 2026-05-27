using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Save-slot layer around the existing GameState singleton.
/// GameState still owns runtime data during play; this bridge turns that runtime data into named JSON saves.
/// </summary>
public class GameStateSaveBridge : MonoBehaviour
{
    public static GameStateSaveBridge Instance { get; private set; }

    [Header("Current Save")]
    public string currentSaveId = "";
    public string currentPlayerName = "Gobbo";
    public string currentSaveName = "Gobbo's Camp";
    public string markedSuccessorId = "";

    [Header("Scenes")]
    public string newGameSceneName = "SampleScene";
    public string campSceneName = "CampScene";

    [Header("Debug")]
    public bool logDebugMessages = true;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static GameStateSaveBridge GetOrCreate()
    {
        if (Instance != null) return Instance;
        GameStateSaveBridge found = FindAnyObjectByType<GameStateSaveBridge>(FindObjectsInactive.Include);
        if (found != null)
        {
            Instance = found;
            return Instance;
        }

        GameObject obj = new GameObject("GameStateSaveBridge");
        Instance = obj.AddComponent<GameStateSaveBridge>();
        return Instance;
    }

    public void CreateNewGameAndLoad(string playerName)
    {
        CreateNewGame(playerName);
        SceneManager.LoadScene(newGameSceneName);
    }

    public SaveSlotData CreateNewGame(string playerName)
    {
        EnsureGameStateExists();

        playerName = string.IsNullOrWhiteSpace(playerName) ? "Gobbo" : playerName.Trim();
        SaveSlotData save = new SaveSlotData();
        save.playerName = playerName;
        save.saveName = playerName + "'s Camp";
        save.saveId = GobboSaveSystem.CreateSaveId(playerName);
        save.createdUtcTicks = DateTime.UtcNow.Ticks;
        save.lastPlayedUtcTicks = save.createdUtcTicks;
        save.currentRunNumber = 1;
        save.maxActiveSquad = 5;
        save.campLevel = 1;
        save.player = new GobboSaveData();
        save.ownedBuddies = new List<BuddyData>();
        save.activeSquadIds = new List<string>();
        save.markedSuccessorId = "";
        save.lastRun = new RunSummaryData();

        ApplySaveToGameState(save);
        GobboSaveSystem.WriteSave(save);
        Log("Created new save " + save.saveId + " for " + playerName);
        return save;
    }

    public bool LoadSaveAndLoadScene(string saveId, string sceneName)
    {
        bool loaded = LoadSave(saveId);
        if (loaded) SceneManager.LoadScene(string.IsNullOrWhiteSpace(sceneName) ? campSceneName : sceneName);
        return loaded;
    }

    public bool LoadSave(string saveId)
    {
        SaveSlotData save = GobboSaveSystem.ReadSave(saveId);
        if (save == null) return false;
        ApplySaveToGameState(save);
        Log("Loaded save " + save.saveId + " for " + save.playerName);
        return true;
    }

    public bool LoadLastSave()
    {
        SaveSlotData save = GobboSaveSystem.ReadLastLoadedSave();
        if (save == null) return false;
        ApplySaveToGameState(save);
        Log("Loaded last save " + save.saveId + " for " + save.playerName);
        return true;
    }

    public void SaveCurrentGame()
    {
        SaveSlotData save = BuildSaveFromGameState();
        if (save == null) return;
        GobboSaveSystem.WriteSave(save);
    }

    public SaveSlotData BuildSaveFromGameState()
    {
        EnsureGameStateExists();
        GameState gs = GameState.Instance;
        if (gs == null) return null;

        gs.RepairRosterState();

        SaveSlotData save = new SaveSlotData();
        save.saveId = string.IsNullOrWhiteSpace(currentSaveId) ? GobboSaveSystem.CreateSaveId(currentPlayerName) : currentSaveId;
        save.playerName = string.IsNullOrWhiteSpace(currentPlayerName) ? "Gobbo" : currentPlayerName;
        save.saveName = string.IsNullOrWhiteSpace(currentSaveName) ? save.playerName + "'s Camp" : currentSaveName;
        save.createdUtcTicks = GetExistingCreatedTicks(save.saveId);
        save.lastPlayedUtcTicks = DateTime.UtcNow.Ticks;

        save.currentRunNumber = gs.currentRunNumber;
        save.maxActiveSquad = gs.maxActiveSquad;
        save.campLevel = gs.campLevel;
        save.player = CloneGobbo(gs.gobbo);
        save.ownedBuddies = CloneBuddyList(gs.ownedBuddies);
        save.activeSquadIds = new List<string>(gs.activeSquadIds);
        save.unlockedStations = gs.unlockedStations != null ? new List<string>(gs.unlockedStations) : new List<string>();
        save.decorationsUnlocked = gs.decorationsUnlocked != null ? new List<string>(gs.decorationsUnlocked) : new List<string>();
        save.lastRun = gs.lastRun != null ? gs.lastRun : new RunSummaryData();
        save.markedSuccessorId = markedSuccessorId;

        return save;
    }

    public void ApplySaveToGameState(SaveSlotData save)
    {
        if (save == null) return;
        GobboSaveSystem.NormalizeSave(save);
        EnsureGameStateExists();
        GameState gs = GameState.Instance;
        if (gs == null) return;

        currentSaveId = save.saveId;
        currentPlayerName = save.playerName;
        currentSaveName = save.saveName;
        markedSuccessorId = save.markedSuccessorId;

        gs.currentRunNumber = save.currentRunNumber;
        gs.maxActiveSquad = save.maxActiveSquad;
        gs.campLevel = save.campLevel;
        gs.gobbo = CloneGobbo(save.player);
        gs.ownedBuddies = CloneBuddyList(save.ownedBuddies);
        gs.activeSquadIds = save.activeSquadIds != null ? new List<string>(save.activeSquadIds) : new List<string>();
        gs.unlockedStations = save.unlockedStations != null ? new List<string>(save.unlockedStations) : new List<string>();
        gs.decorationsUnlocked = save.decorationsUnlocked != null ? new List<string>(save.decorationsUnlocked) : new List<string>();
        gs.lastRun = save.lastRun != null ? save.lastRun : new RunSummaryData();
        gs.RepairRosterState();
    }

    public void SetMarkedSuccessor(string buddyId, bool writeImmediately = true)
    {
        markedSuccessorId = string.IsNullOrWhiteSpace(buddyId) ? "" : buddyId.Trim();
        Log("Marked successor now: " + (string.IsNullOrWhiteSpace(markedSuccessorId) ? "none" : markedSuccessorId));
        if (writeImmediately && !string.IsNullOrWhiteSpace(currentSaveId)) SaveCurrentGame();
    }

    public string GetMarkedSuccessorId()
    {
        return markedSuccessorId;
    }

    public void ClearMarkedSuccessor(bool writeImmediately = true)
    {
        SetMarkedSuccessor("", writeImmediately);
    }

    public void ValidateMarkedSuccessorAgainstRoster(bool writeImmediately = true)
    {
        if (string.IsNullOrWhiteSpace(markedSuccessorId) || GameState.Instance == null) return;
        if (GameState.Instance.FindBuddy(markedSuccessorId) == null)
        {
            Log("Marked successor no longer exists. Clearing: " + markedSuccessorId);
            ClearMarkedSuccessor(writeImmediately);
        }
    }

    long GetExistingCreatedTicks(string saveId)
    {
        SaveSlotData existing = GobboSaveSystem.ReadSave(saveId);
        if (existing != null && existing.createdUtcTicks > 0) return existing.createdUtcTicks;
        return DateTime.UtcNow.Ticks;
    }

    static void EnsureGameStateExists()
    {
        if (GameState.Instance != null) return;
        GameObject obj = new GameObject("GameState");
        obj.AddComponent<GameState>();
    }

    static List<BuddyData> CloneBuddyList(List<BuddyData> source)
    {
        List<BuddyData> result = new List<BuddyData>();
        if (source == null) return result;
        foreach (BuddyData buddy in source)
        {
            if (buddy == null) continue;
            buddy.EnsureId();
            buddy.EnsureRuntimeDefaults();
            result.Add(buddy.Clone());
        }
        return result;
    }

    static GobboSaveData CloneGobbo(GobboSaveData source)
    {
        GobboSaveData copy = new GobboSaveData();
        if (source == null) return copy;
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), copy);
        return copy;
    }

    void Log(string message)
    {
        if (logDebugMessages) Debug.Log("[GameStateSaveBridge] " + message);
    }
}
