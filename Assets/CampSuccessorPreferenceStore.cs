using UnityEngine;

/// <summary>
/// Scene compatibility layer for the camp-marked successor.
/// Source of truth is GameState.markedSuccessorId.
/// IMPORTANT: this object must NOT be DontDestroyOnLoad because it may sit beside UI refs.
/// </summary>
public class CampSuccessorPreferenceStore : MonoBehaviour
{
    public static CampSuccessorPreferenceStore Instance { get; private set; }

    [Header("Debug View - mirrors GameState")]
    public string markedSuccessorId = "";

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
        SyncFromGameState();
    }

    void OnEnable()
    {
        if (Instance == null) Instance = this;
        SyncFromGameState();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static CampSuccessorPreferenceStore GetOrCreate()
    {
        if (Instance != null) return Instance;
        CampSuccessorPreferenceStore found = Object.FindAnyObjectByType<CampSuccessorPreferenceStore>(FindObjectsInactive.Include);
        if (found != null) { Instance = found; return Instance; }
        GameObject obj = new GameObject("CampSuccessorPreferenceStore");
        Instance = obj.AddComponent<CampSuccessorPreferenceStore>();
        return Instance;
    }

    public void MarkSuccessor(GobboUnitSaveData unit) => SetMarkedSuccessor(unit);
    public void MarkSuccessor(string gobboId) => SetMarkedSuccessor(gobboId);

    public void SetMarkedSuccessor(GobboUnitSaveData unit)
    {
        if (unit == null) { ClearSuccessor(); return; }
        unit.EnsureId();
        SetMarkedSuccessor(unit.uniqueId);
    }

    public void SetMarkedSuccessor(string gobboId) => SetMarkedSuccessor(gobboId, true);

    public void SetMarkedSuccessor(string gobboId, bool writeImmediately)
    {
        if (GameState.Instance == null) markedSuccessorId = string.IsNullOrWhiteSpace(gobboId) ? "" : gobboId.Trim();
        else
        {
            GameState.Instance.SetMarkedSuccessorId(gobboId);
            markedSuccessorId = GameState.Instance.GetMarkedSuccessorId();
        }
        Log("Marked successor id: " + (string.IsNullOrWhiteSpace(markedSuccessorId) ? "none" : markedSuccessorId));
        if (writeImmediately) SaveToCurrentSave();
    }

    public void ClearSuccessor() => ClearSuccessor(true);

    public void ClearSuccessor(bool writeImmediately)
    {
        if (GameState.Instance != null) GameState.Instance.SetMarkedSuccessorId("");
        markedSuccessorId = "";
        Log("Cleared successor.");
        if (writeImmediately) SaveToCurrentSave();
    }

    public GobboUnitSaveData GetMarkedSuccessor()
    {
        SyncFromGameState();
        return GameState.Instance != null ? GameState.Instance.GetMarkedSuccessorUnit() : null;
    }

    public GobboUnitSaveData GetMarkedSuccessorUnit() => GetMarkedSuccessor();

    public string GetMarkedSuccessorId()
    {
        SyncFromGameState();
        return markedSuccessorId;
    }

    public bool IsMarked(string gobboId)
    {
        SyncFromGameState();
        return !string.IsNullOrWhiteSpace(gobboId) && gobboId == markedSuccessorId;
    }

    public bool IsMarked(GobboUnitSaveData unit)
    {
        if (unit == null) return false;
        unit.EnsureId();
        return IsMarked(unit.uniqueId);
    }

    public bool HasMarkedSuccessor() => GetMarkedSuccessor() != null;

    public void ValidateAgainstRoster() => ValidateAgainstRoster(true);

    public void ValidateAgainstRoster(bool writeImmediately)
    {
        if (GameState.Instance == null) return;
        string current = GameState.Instance.GetMarkedSuccessorId();
        if (string.IsNullOrWhiteSpace(current)) { markedSuccessorId = ""; return; }
        if (GameState.Instance.GetMarkedSuccessorUnit() == null) ClearSuccessor(writeImmediately);
        else markedSuccessorId = current;
    }

    public void SyncFromGameState()
    {
        if (GameState.Instance == null) return;
        markedSuccessorId = GameState.Instance.GetMarkedSuccessorId();
    }

    public void LoadFromCurrentSave()
    {
        SyncFromGameState();
        ValidateAgainstRoster(false);
    }

    public void SaveToCurrentSave()
    {
        SyncFromGameState();
        if (SporeSaveManager.GetCurrentSlot() > 0) SporeSaveManager.SaveCurrentSlotFromGameState();
    }

    public void LoadFromGameState() => LoadFromCurrentSave();
    public void SaveToGameState() => SaveToCurrentSave();

    void Log(string message)
    {
        if (logDebugMessages) Debug.Log("[CampSuccessorPreferenceStore] " + message);
    }
}
