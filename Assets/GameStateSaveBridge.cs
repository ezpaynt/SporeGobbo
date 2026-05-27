using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Compatibility bridge kept so older scripts/scene objects do not break.
/// The real save source is now SporeSaveManager's 3 full slots.
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
        RefreshFromCurrentSlot();
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
        SporeSaveSlotData created = SporeSaveManager.CreateNewGame(playerName, newGameSceneName);
        if (created == null) return;
        RefreshFromSlot(created);
        SceneManager.LoadScene(newGameSceneName);
    }

    public SaveSlotData CreateNewGame(string playerName)
    {
        SporeSaveSlotData created = SporeSaveManager.CreateNewGame(playerName, newGameSceneName);
        RefreshFromSlot(created);
        return ToLegacySaveSlotData(created);
    }

    public bool LoadSaveAndLoadScene(string saveId, string sceneName)
    {
        int slot = SlotFromSaveId(saveId);
        if (slot <= 0) slot = SporeSaveManager.GetLastPlayedSlot();
        if (slot <= 0) return false;

        bool loaded = SporeSaveManager.LoadSlotIntoGameState(slot);
        if (loaded)
        {
            RefreshFromSlot(SporeSaveManager.LoadSlot(slot));
            SceneManager.LoadScene(string.IsNullOrWhiteSpace(sceneName) ? campSceneName : sceneName);
        }

        return loaded;
    }

    public bool LoadSave(string saveId)
    {
        int slot = SlotFromSaveId(saveId);
        if (slot <= 0) slot = SporeSaveManager.GetLastPlayedSlot();
        if (slot <= 0) return false;
        bool loaded = SporeSaveManager.LoadSlotIntoGameState(slot);
        if (loaded) RefreshFromSlot(SporeSaveManager.LoadSlot(slot));
        return loaded;
    }

    public bool LoadLastSave()
    {
        SporeSaveSlotData last = SporeSaveManager.LoadLastPlayedSlot();
        if (last == null) return false;
        bool loaded = SporeSaveManager.LoadSlotIntoGameState(last.slotIndex);
        if (loaded) RefreshFromSlot(last);
        return loaded;
    }

    public void SaveCurrentGame()
    {
        SporeSaveManager.SaveCurrentSlotFromGameState();
        RefreshFromCurrentSlot();
    }

    public SaveSlotData BuildSaveFromGameState()
    {
        int slot = SporeSaveManager.GetCurrentSlot();
        if (slot <= 0) slot = SporeSaveManager.GetLastPlayedSlot();
        if (slot <= 0) return null;
        SporeSaveSlotData data = SporeSaveManager.BuildSlotFromGameState(slot, SporeSaveManager.LoadSlot(slot));
        return ToLegacySaveSlotData(data);
    }

    public void ApplySaveToGameState(SaveSlotData save)
    {
        if (save == null) return;
        // This method exists for compatibility only. Real loading should use SporeSaveManager.LoadSlotIntoGameState.
        SporeSaveSlotData data = new SporeSaveSlotData
        {
            slotIndex = Mathf.Clamp(SporeSaveManager.GetCurrentSlot(), 1, SporeSaveManager.SlotCount),
            hasSave = true,
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
        SporeSaveManager.ApplySlotToGameState(data);
        RefreshFromSlot(data);
    }

    public void SetMarkedSuccessor(string buddyId, bool writeImmediately = true)
    {
        CampSuccessorPreferenceStore store = CampSuccessorPreferenceStore.GetOrCreate();
        if (store != null) store.SetMarkedSuccessor(buddyId, writeImmediately);
        markedSuccessorId = string.IsNullOrWhiteSpace(buddyId) ? "" : buddyId.Trim();
    }

    public string GetMarkedSuccessorId()
    {
        CampSuccessorPreferenceStore store = CampSuccessorPreferenceStore.Instance;
        if (store != null) markedSuccessorId = store.GetMarkedSuccessorId();
        return markedSuccessorId;
    }

    public void ClearMarkedSuccessor(bool writeImmediately = true)
    {
        SetMarkedSuccessor("", writeImmediately);
    }

    public void ValidateMarkedSuccessorAgainstRoster(bool writeImmediately = true)
    {
        CampSuccessorPreferenceStore store = CampSuccessorPreferenceStore.Instance;
        if (store != null) store.ValidateAgainstRoster(writeImmediately);
    }

    void RefreshFromCurrentSlot()
    {
        int slot = SporeSaveManager.GetCurrentSlot();
        if (slot <= 0) slot = SporeSaveManager.GetLastPlayedSlot();
        if (slot <= 0) return;
        RefreshFromSlot(SporeSaveManager.LoadSlot(slot));
    }

    void RefreshFromSlot(SporeSaveSlotData data)
    {
        if (data == null || !data.hasSave) return;
        currentSaveId = data.saveId;
        currentPlayerName = data.playerName;
        currentSaveName = data.saveName;
        markedSuccessorId = data.markedSuccessorId;
        Log("Current save now slot " + data.slotIndex + ": " + currentSaveName);
    }

    int SlotFromSaveId(string saveId)
    {
        if (string.IsNullOrWhiteSpace(saveId)) return 0;
        if (saveId.StartsWith("slot_"))
        {
            string number = saveId.Substring("slot_".Length);
            if (int.TryParse(number, out int slot)) return Mathf.Clamp(slot, 1, SporeSaveManager.SlotCount);
        }
        return 0;
    }

    SaveSlotData ToLegacySaveSlotData(SporeSaveSlotData data)
    {
        if (data == null) return null;
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

    void Log(string message)
    {
        if (logDebugMessages) Debug.Log("[GameStateSaveBridge] " + message);
    }
}
