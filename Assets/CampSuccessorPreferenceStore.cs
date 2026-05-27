using UnityEngine;

/// <summary>
/// UI-facing successor helper.
/// Actual truth is the current save slot/bridge; this script is only a scene-friendly wrapper.
/// </summary>
public class CampSuccessorPreferenceStore : MonoBehaviour
{
    public static CampSuccessorPreferenceStore Instance { get; private set; }

    [Header("Mirror Only")]
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
        SyncFromBridge();
    }

    void OnEnable()
    {
        if (Instance == null) Instance = this;
        SyncFromBridge();
    }

    public static CampSuccessorPreferenceStore GetOrCreate()
    {
        if (Instance != null) return Instance;

        CampSuccessorPreferenceStore found = FindAnyObjectByType<CampSuccessorPreferenceStore>(FindObjectsInactive.Include);
        if (found != null)
        {
            Instance = found;
            found.SyncFromBridge();
            return found;
        }

        GameObject obj = new GameObject("CampSuccessorPreferenceStore");
        Instance = obj.AddComponent<CampSuccessorPreferenceStore>();
        return Instance;
    }

    public void MarkSuccessor(BuddyData buddy) => SetMarkedSuccessor(buddy, true);
    public void MarkSuccessor(string buddyId) => SetMarkedSuccessor(buddyId, true);

    public void SetMarkedSuccessor(BuddyData buddy) => SetMarkedSuccessor(buddy, true);

    public void SetMarkedSuccessor(BuddyData buddy, bool writeImmediately)
    {
        if (buddy == null)
        {
            ClearSuccessor(writeImmediately);
            return;
        }
        buddy.EnsureId();
        SetMarkedSuccessor(buddy.uniqueId, writeImmediately);
    }

    public void SetMarkedSuccessor(string buddyId) => SetMarkedSuccessor(buddyId, true);

    public void SetMarkedSuccessor(string buddyId, bool writeImmediately)
    {
        markedSuccessorId = string.IsNullOrWhiteSpace(buddyId) ? "" : buddyId.Trim();
        GameStateSaveBridge.GetOrCreate().SetMarkedSuccessor(markedSuccessorId, writeImmediately);
        Log("Marked successor id: " + (string.IsNullOrWhiteSpace(markedSuccessorId) ? "none" : markedSuccessorId));
    }

    public void ClearSuccessor() => ClearSuccessor(true);

    public void ClearSuccessor(bool writeImmediately)
    {
        markedSuccessorId = "";
        GameStateSaveBridge.GetOrCreate().ClearMarkedSuccessor(writeImmediately);
        Log("Cleared successor.");
    }

    public BuddyData GetMarkedSuccessor()
    {
        SyncFromBridge();
        if (string.IsNullOrWhiteSpace(markedSuccessorId) || GameState.Instance == null) return null;

        BuddyData buddy = GameState.Instance.FindBuddy(markedSuccessorId);
        if (buddy == null)
        {
            ClearSuccessor(true);
            return null;
        }
        return buddy;
    }

    public string GetMarkedSuccessorId()
    {
        SyncFromBridge();
        return markedSuccessorId;
    }

    public bool IsMarked(string buddyId)
    {
        SyncFromBridge();
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
        SyncFromBridge();
        if (string.IsNullOrWhiteSpace(markedSuccessorId)) return;
        if (GameState.Instance == null || GameState.Instance.FindBuddy(markedSuccessorId) == null) ClearSuccessor(true);
    }

    public void LoadFromGameState() => SyncFromBridge();
    public void SaveToGameState() => GameStateSaveBridge.GetOrCreate().SetMarkedSuccessor(markedSuccessorId, true);

    void SyncFromBridge()
    {
        GameStateSaveBridge bridge = GameStateSaveBridge.GetOrCreate();
        markedSuccessorId = bridge.GetMarkedSuccessorId();
    }

    void Log(string message)
    {
        if (logDebugMessages) Debug.Log("[CampSuccessorPreferenceStore] " + message);
    }
}
