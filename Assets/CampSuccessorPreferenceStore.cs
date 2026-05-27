using UnityEngine;

/// <summary>
/// Runtime holder for the camp-marked successor.
/// The durable copy is saved into the current SporeSaveManager slot.
/// </summary>
public class CampSuccessorPreferenceStore : MonoBehaviour
{
    public static CampSuccessorPreferenceStore Instance { get; private set; }

    [Header("Chosen Successor")]
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
        DontDestroyOnLoad(gameObject);
        LoadFromCurrentSave();
    }

    void OnEnable()
    {
        if (Instance == null) Instance = this;
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

    public void MarkSuccessor(BuddyData buddy) => SetMarkedSuccessor(buddy);
    public void MarkSuccessor(string buddyId) => SetMarkedSuccessor(buddyId);

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
        markedSuccessorId = string.IsNullOrWhiteSpace(buddyId) ? "" : buddyId.Trim();
        Log("Marked successor id: " + (string.IsNullOrWhiteSpace(markedSuccessorId) ? "none" : markedSuccessorId));
        if (writeImmediately) SaveToCurrentSave();
    }

    public void ClearSuccessor()
    {
        ClearSuccessor(true);
    }

    public void ClearSuccessor(bool writeImmediately)
    {
        markedSuccessorId = "";
        Log("Cleared successor.");
        if (writeImmediately) SaveToCurrentSave();
    }

    public BuddyData GetMarkedSuccessor()
    {
        if (string.IsNullOrWhiteSpace(markedSuccessorId) || GameState.Instance == null) return null;
        BuddyData buddy = GameState.Instance.FindBuddy(markedSuccessorId);
        if (buddy == null)
        {
            ClearSuccessor();
            return null;
        }
        return buddy;
    }

    public string GetMarkedSuccessorId() => markedSuccessorId;

    public bool IsMarked(string buddyId)
    {
        return !string.IsNullOrWhiteSpace(buddyId) && buddyId == markedSuccessorId;
    }

    public bool IsMarked(BuddyData buddy)
    {
        if (buddy == null) return false;
        buddy.EnsureId();
        return IsMarked(buddy.uniqueId);
    }

    public bool HasMarkedSuccessor() => GetMarkedSuccessor() != null;

    public void ValidateAgainstRoster()
    {
        ValidateAgainstRoster(true);
    }

    public void ValidateAgainstRoster(bool writeImmediately)
    {
        if (string.IsNullOrWhiteSpace(markedSuccessorId)) return;
        if (GameState.Instance == null || GameState.Instance.FindBuddy(markedSuccessorId) == null)
            ClearSuccessor(writeImmediately);
    }

    public void LoadFromCurrentSave()
    {
        int slot = SporeSaveManager.GetCurrentSlot();
        if (slot <= 0) slot = SporeSaveManager.GetLastPlayedSlot();
        if (slot <= 0) return;

        SporeSaveSlotData data = SporeSaveManager.LoadSlot(slot);
        if (data != null && data.hasSave)
        {
            markedSuccessorId = data.markedSuccessorId;
            ValidateAgainstRoster(false);
        }
    }

    public void SaveToCurrentSave()
    {
        int slot = SporeSaveManager.GetCurrentSlot();
        if (slot <= 0) slot = SporeSaveManager.GetLastPlayedSlot();
        if (slot <= 0) return;

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
