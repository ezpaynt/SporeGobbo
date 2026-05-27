using UnityEngine;

public class CampSuccessorPreferenceStore : MonoBehaviour
{
    public static CampSuccessorPreferenceStore Instance { get; private set; }

    private const string PlayerPrefsKey = "SporeGobbo.MarkedSuccessorId";

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
        LoadSavedSuccessorId();
    }

    void OnEnable()
    {
        if (Instance == null)
            Instance = this;

        LoadSavedSuccessorId();
    }

    public static CampSuccessorPreferenceStore GetOrCreate()
    {
        if (Instance != null)
        {
            Instance.LoadSavedSuccessorId();
            return Instance;
        }

        CampSuccessorPreferenceStore found = Object.FindAnyObjectByType<CampSuccessorPreferenceStore>(FindObjectsInactive.Include);
        if (found != null)
        {
            Instance = found;
            Instance.LoadSavedSuccessorId();
            return Instance;
        }

        GameObject obj = new GameObject("CampSuccessorPreferenceStore");
        Instance = obj.AddComponent<CampSuccessorPreferenceStore>();
        return Instance;
    }

    public void MarkSuccessor(BuddyData buddy)
    {
        SetMarkedSuccessor(buddy);
    }

    public void MarkSuccessor(string buddyId)
    {
        SetMarkedSuccessor(buddyId);
    }

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
        SaveSuccessorId();

        if (logDebugMessages)
            Debug.Log("[CampSuccessorPreferenceStore] Marked successor id: " +
                      (string.IsNullOrWhiteSpace(markedSuccessorId) ? "none" : markedSuccessorId));
    }

    public void ClearSuccessor()
    {
        markedSuccessorId = "";
        SaveSuccessorId();

        if (logDebugMessages)
            Debug.Log("[CampSuccessorPreferenceStore] Cleared successor.");
    }

    public BuddyData GetMarkedSuccessor()
    {
        LoadSavedSuccessorId();

        if (string.IsNullOrWhiteSpace(markedSuccessorId) || GameState.Instance == null)
            return null;

        BuddyData buddy = GameState.Instance.FindBuddy(markedSuccessorId);
        if (buddy == null)
            return null;

        return buddy;
    }

    public string GetMarkedSuccessorId()
    {
        LoadSavedSuccessorId();
        return markedSuccessorId;
    }

    public bool IsMarked(string buddyId)
    {
        LoadSavedSuccessorId();
        return !string.IsNullOrWhiteSpace(buddyId) && buddyId == markedSuccessorId;
    }

    public bool IsMarked(BuddyData buddy)
    {
        if (buddy == null)
            return false;

        buddy.EnsureId();
        return IsMarked(buddy.uniqueId);
    }

    public bool HasMarkedSuccessor()
    {
        return GetMarkedSuccessor() != null;
    }

    public void ValidateAgainstRoster()
    {
        LoadSavedSuccessorId();

        if (string.IsNullOrWhiteSpace(markedSuccessorId))
            return;

        if (GameState.Instance == null || GameState.Instance.FindBuddy(markedSuccessorId) == null)
        {
            // Important: only clear invalid ids. Never auto-pick a replacement here.
            ClearSuccessor();
        }
    }

    // Backwards-compatible names older scripts may still call.
    public void LoadFromGameState()
    {
        LoadSavedSuccessorId();
    }

    public void SaveToGameState()
    {
        SaveSuccessorId();
    }

    void LoadSavedSuccessorId()
    {
        string saved = PlayerPrefs.GetString(PlayerPrefsKey, "");
        markedSuccessorId = string.IsNullOrWhiteSpace(saved) ? "" : saved.Trim();
    }

    void SaveSuccessorId()
    {
        PlayerPrefs.SetString(PlayerPrefsKey, markedSuccessorId ?? "");
        PlayerPrefs.Save();
    }
}
