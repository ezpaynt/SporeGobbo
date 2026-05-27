using UnityEngine;

public class CampSuccessorPreferenceStore : MonoBehaviour
{
    public static CampSuccessorPreferenceStore Instance { get; private set; }

    [Header("Chosen Successor")]
    public string markedSuccessorId = "";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadFromGameState();
    }

    void OnEnable()
    {
        if (Instance == null)
            Instance = this;

        LoadFromGameState();
    }

    public static CampSuccessorPreferenceStore GetOrCreate()
    {
        if (Instance != null)
        {
            Instance.LoadFromGameState();
            return Instance;
        }

        CampSuccessorPreferenceStore found = Object.FindAnyObjectByType<CampSuccessorPreferenceStore>(FindObjectsInactive.Include);
        if (found != null)
        {
            Instance = found;
            Instance.LoadFromGameState();
            return Instance;
        }

        GameObject obj = new GameObject("CampSuccessorPreferenceStore");
        Instance = obj.AddComponent<CampSuccessorPreferenceStore>();
        return Instance;
    }

    public void MarkSuccessor(BuddyData buddy)
    {
        if (buddy == null)
            return;

        buddy.EnsureId();
        MarkSuccessor(buddy.uniqueId);
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
        SaveToGameState();
        Debug.Log("[CampSuccessorPreferenceStore] Marked successor id: " + (string.IsNullOrWhiteSpace(markedSuccessorId) ? "none" : markedSuccessorId));
    }

    public void ClearSuccessor()
    {
        markedSuccessorId = "";
        SaveToGameState();
        Debug.Log("[CampSuccessorPreferenceStore] Cleared successor.");
    }

    public BuddyData GetMarkedSuccessor()
    {
        LoadFromGameState();

        if (string.IsNullOrWhiteSpace(markedSuccessorId) || GameState.Instance == null)
            return null;

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
        LoadFromGameState();
        return markedSuccessorId;
    }

    public bool IsMarked(string buddyId)
    {
        LoadFromGameState();
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
        LoadFromGameState();

        if (string.IsNullOrWhiteSpace(markedSuccessorId))
            return;

        if (GameState.Instance == null || GameState.Instance.FindBuddy(markedSuccessorId) == null)
            ClearSuccessor();
    }

    public void LoadFromGameState()
    {
        if (GameState.Instance == null)
            return;

        System.Reflection.FieldInfo field = typeof(GameState).GetField("markedSuccessorId");
        if (field != null)
        {
            string value = field.GetValue(GameState.Instance) as string;
            if (!string.IsNullOrWhiteSpace(value))
                markedSuccessorId = value;
        }
    }

    public void SaveToGameState()
    {
        if (GameState.Instance == null)
            return;

        System.Reflection.FieldInfo field = typeof(GameState).GetField("markedSuccessorId");
        if (field != null)
            field.SetValue(GameState.Instance, markedSuccessorId);
    }
}
