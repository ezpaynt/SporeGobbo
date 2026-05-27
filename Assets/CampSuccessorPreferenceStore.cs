using UnityEngine;

/// <summary>
/// Runtime-only holder for the camp-marked successor.
/// No PlayerPrefs. No test auto-picking. No cross-save leakage.
/// The mark only changes when squad select explicitly calls Mark/Set/Clear.
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
    }

    void OnEnable()
    {
        if (Instance == null)
            Instance = this;
    }

    public static CampSuccessorPreferenceStore GetOrCreate()
    {
        if (Instance != null)
            return Instance;

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
        markedSuccessorId = string.IsNullOrWhiteSpace(buddyId) ? "" : buddyId.Trim();
        Log("Marked successor id: " + (string.IsNullOrWhiteSpace(markedSuccessorId) ? "none" : markedSuccessorId));
    }

    public void ClearSuccessor()
    {
        markedSuccessorId = "";
        Log("Cleared successor.");
    }

    public BuddyData GetMarkedSuccessor()
    {
        if (string.IsNullOrWhiteSpace(markedSuccessorId) || GameState.Instance == null)
            return null;

        BuddyData buddy = GameState.Instance.FindBuddy(markedSuccessorId);
        if (buddy == null)
        {
            // Do not auto-pick a replacement. Just clear an invalid choice.
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
        if (buddy == null)
            return false;

        buddy.EnsureId();
        return IsMarked(buddy.uniqueId);
    }

    public bool HasMarkedSuccessor() => GetMarkedSuccessor() != null;

    public void ValidateAgainstRoster()
    {
        if (string.IsNullOrWhiteSpace(markedSuccessorId))
            return;

        if (GameState.Instance == null || GameState.Instance.FindBuddy(markedSuccessorId) == null)
            ClearSuccessor();
    }

    // Backwards-compatible names older scripts may still call.
    public void LoadFromGameState() { }
    public void SaveToGameState() { }

    void Log(string message)
    {
        if (logDebugMessages)
            Debug.Log("[CampSuccessorPreferenceStore] " + message);
    }
}
