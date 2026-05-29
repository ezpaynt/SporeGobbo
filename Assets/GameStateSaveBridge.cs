using UnityEngine;
using UnityEngine.SceneManagement;

public class GameStateSaveBridge : MonoBehaviour
{
    public static GameStateSaveBridge Instance { get; private set; }

    [Header("Current Save")]
    public int currentSlotIndex = 0;
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
        if (found != null) { Instance = found; return Instance; }
        GameObject obj = new GameObject("GameStateSaveBridge");
        Instance = obj.AddComponent<GameStateSaveBridge>();
        return Instance;
    }

    public void CreateNewGameAndLoad(string playerName)
    {
        SporeSaveSlotData data = CreateNewGame(playerName);
        if (data != null) SceneManager.LoadScene(newGameSceneName);
    }

    public SporeSaveSlotData CreateNewGame(string playerName)
    {
        SporeSaveSlotData data = SporeSaveManager.CreateNewGame(playerName, newGameSceneName);
        if (data == null)
        {
            Log("No empty save slots. New game refused.");
            return null;
        }
        SetCurrentSlotWithoutSaving(data.slotIndex, data.playerName, data.saveName, data.markedSuccessorId);
        Log("Created new save slot " + data.slotIndex + " for " + data.playerName);
        return data;
    }

    public bool LoadSaveAndLoadScene(string saveId, string sceneName)
    {
        if (!int.TryParse((saveId ?? "").Replace("slot_", ""), out int slot)) slot = SporeSaveManager.GetLastPlayedSlot();
        bool loaded = LoadSaveSlot(slot);
        if (loaded) SceneManager.LoadScene(string.IsNullOrWhiteSpace(sceneName) ? campSceneName : sceneName);
        return loaded;
    }

    public bool LoadSave(string saveId)
    {
        if (!int.TryParse((saveId ?? "").Replace("slot_", ""), out int slot)) return false;
        return LoadSaveSlot(slot);
    }

    public bool LoadSaveSlot(int slotIndex)
    {
        bool loaded = SporeSaveManager.LoadSlotIntoGameState(slotIndex);
        if (loaded) Log("Loaded slot " + slotIndex);
        return loaded;
    }

    public bool LoadLastSave()
    {
        SporeSaveSlotData data = SporeSaveManager.LoadLastPlayedSlot();
        if (data == null || !data.hasSave) return false;
        bool loaded = SporeSaveManager.ApplySlotToGameState(data);
        if (loaded) Log("Loaded most recent slot " + data.slotIndex + " for " + data.playerName);
        return loaded;
    }

    public void SaveCurrentGame() => SporeSaveManager.SaveCurrentSlotFromGameState();

    public SporeSaveSlotData BuildSaveFromGameState()
    {
        int slot = currentSlotIndex > 0 ? currentSlotIndex : SporeSaveManager.GetLastPlayedSlot();
        if (slot <= 0) slot = 1;
        return SporeSaveManager.BuildSlotFromGameState(slot);
    }

    public void ApplySaveToGameState(SporeSaveSlotData save)
    {
        if (save == null) return;
        SporeSaveManager.ApplySlotToGameState(save);
    }

    public void SetCurrentSlotWithoutSaving(int slotIndex, string playerName, string saveName, string successorId)
    {
        currentSlotIndex = Mathf.Clamp(slotIndex, 1, SporeSaveManager.SlotCount);
        currentSaveId = "slot_" + currentSlotIndex;
        currentPlayerName = string.IsNullOrWhiteSpace(playerName) ? "Gobbo" : playerName;
        currentSaveName = string.IsNullOrWhiteSpace(saveName) ? currentPlayerName + "'s Camp" : saveName;
        markedSuccessorId = string.IsNullOrWhiteSpace(successorId) ? "" : successorId.Trim();
    }

    public void SetMarkedSuccessor(string gobboId, bool writeImmediately = true)
    {
        markedSuccessorId = string.IsNullOrWhiteSpace(gobboId) ? "" : gobboId.Trim();
        Log("Marked successor now: " + (string.IsNullOrWhiteSpace(markedSuccessorId) ? "none" : markedSuccessorId));
        if (writeImmediately) SaveCurrentGame();
    }

    public string GetMarkedSuccessorId() => markedSuccessorId;
    public void ClearMarkedSuccessor(bool writeImmediately = true) => SetMarkedSuccessor("", writeImmediately);

    public void ValidateMarkedSuccessorAgainstRoster(bool writeImmediately = true)
    {
        if (string.IsNullOrWhiteSpace(markedSuccessorId) || GameState.Instance == null) return;
        if (GameState.Instance.FindGobboById(markedSuccessorId) == null)
        {
            Log("Marked successor no longer exists. Clearing: " + markedSuccessorId);
            ClearMarkedSuccessor(writeImmediately);
        }
    }

    void Log(string message)
    {
        if (logDebugMessages) Debug.Log("[GameStateSaveBridge] " + message);
    }
}
