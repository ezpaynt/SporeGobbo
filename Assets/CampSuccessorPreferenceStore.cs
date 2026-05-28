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
        // Scene-only singleton. Do not DontDestroyOnLoad this object.
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
        if (found != null)
        {
            Instance = found;
            return Instance;
        }

        GameObject obj = new GameObject("CampSuccessorPreferenceStore");
        Instance = obj.AddComponent<CampSuccessorPreferenceStore>();
        return Instance;
    }

    public void MarkSuccessor(BuddyData buddy) { SetMarkedSuccessor(buddy); }
    public void MarkSuccessor(string buddyId) { SetMarkedSuccessor(buddyId); }

    public void SetMarkedSuccessor(BuddyData buddy)
    {
        if (buddy == null)
        {
            ClearSuccessor();
            return;
        }

        buddy.EnsureId();
        SetMarkedSuccessor(buddy.uniqueId);
    }

    public void SetMarkedSuccessor(string buddyId)
    {
        SetMarkedSuccessor(buddyId, true);
    }

    public void SetMarkedSuccessor(string buddyId, bool writeImmediately)
    {
        if (GameState.Instance == null)
        {
            markedSuccessorId = string.IsNullOrWhiteSpace(buddyId) ? "" : buddyId.Trim();
        }
        else
        {
            GameState.Instance.SetMarkedSuccessorId(buddyId);
            markedSuccessorId = GameState.Instance.GetMarkedSuccessorId();
        }

        Log("Marked successor id: " + (string.IsNullOrWhiteSpace(markedSuccessorId) ? "none" : markedSuccessorId));

        if (writeImmediately) SaveToCurrentSave();
    }

    public void ClearSuccessor()
    {
        ClearSuccessor(true);
    }

    public void ClearSuccessor(bool writeImmediately)
    {
        if (GameState.Instance != null) GameState.Instance.SetMarkedSuccessorId("");
        markedSuccessorId = "";

        Log("Cleared successor.");

        if (writeImmediately) SaveToCurrentSave();
    }

    public BuddyData GetMarkedSuccessor()
    {
        SyncFromGameState();
        return GameState.Instance != null ? GameState.Instance.GetMarkedSuccessor() : null;
    }

    public GobboUnitSaveData GetMarkedSuccessorUnit()
    {
        SyncFromGameState();
        return GameState.Instance != null ? GameState.Instance.GetMarkedSuccessorUnit() : null;
    }

    public string GetMarkedSuccessorId()
    {
        SyncFromGameState();
        return markedSuccessorId;
    }

    public bool IsMarked(string buddyId)
    {
        SyncFromGameState();
        return !string.IsNullOrWhiteSpace(buddyId) && buddyId == markedSuccessorId;
    }

    public bool IsMarked(BuddyData buddy)
    {
        if (buddy == null) return false;
        buddy.EnsureId();
        return IsMarked(buddy.uniqueId);
    }

    public bool HasMarkedSuccessor()
    {
        return GetMarkedSuccessor() != null;
    }

    public void ValidateAgainstRoster()
    {
        ValidateAgainstRoster(true);
    }

    public void ValidateAgainstRoster(bool writeImmediately)
    {
        if (GameState.Instance == null) return;

        string current = GameState.Instance.GetMarkedSuccessorId();
        if (string.IsNullOrWhiteSpace(current))
        {
            markedSuccessorId = "";
            return;
        }

        if (GameState.Instance.GetMarkedSuccessorUnit() == null)
            ClearSuccessor(writeImmediately);
        else
            markedSuccessorId = current;
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
        if (SporeSaveManager.GetCurrentSlot() > 0)
            SporeSaveManager.SaveCurrentSlotFromGameState();
    }

    // Backwards-compatible names older scripts may still call.
    public void LoadFromGameState() { LoadFromCurrentSave(); }
    public void SaveToGameState() { SaveToCurrentSave(); }

    void Log(string message)
    {
        if (logDebugMessages) Debug.Log("[CampSuccessorPreferenceStore] " + message);
    }
}
