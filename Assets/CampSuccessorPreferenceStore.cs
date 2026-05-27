using UnityEngine;

/// <summary>
/// UI-facing successor helper.
/// The actual successor value now lives in GameStateSaveBridge/current save data, not PlayerPrefs.
/// </summary>
public class CampSuccessorPreferenceStore : MonoBehaviour
{
    public static CampSuccessorPreferenceStore Instance { get; private set; }

    [Header("Mirror Only - Actual Value Lives In Save Bridge")]
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
        markedSuccessorId = string.IsNullOrWhiteSpace(buddyId) ? "" : buddyId.Trim();
        GameStateSaveBridge.GetOrCreate().SetMarkedSuccessor(markedSuccessorId, true);
        Log("Marked successor id: " + (string.IsNullOrWhiteSpace(markedSuccessorId) ? "none" : markedSuccessorId));
    }

    public void ClearSuccessor()
    {
        markedSuccessorId = "";
        GameStateSaveBridge.GetOrCreate().ClearMarkedSuccessor(true);
        Log("Cleared successor.");
    }

    public BuddyData GetMarkedSuccessor()
    {
        SyncFromBridge();
        if (string.IsNullOrWhiteSpace(markedSuccessorId) || GameState.Instance == null) return null;

        BuddyData buddy = GameState.Instance.FindBuddy(markedSuccessorId);
        if (buddy == null)
        {
            ClearSuccessor();
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
        if (GameState.Instance == null || GameState.Instance.FindBuddy(markedSuccessorId) == null) ClearSuccessor();
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
