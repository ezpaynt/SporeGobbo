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

    public void MarkSuccessor(BuddyData buddy)
    {
        if (buddy == null)
            return;

        buddy.EnsureId();
        MarkSuccessor(buddy.uniqueId);
    }

    public void MarkSuccessor(string buddyId)
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

    // This signature is for CampSquadSelect, which expects a BuddyData.
    public BuddyData GetMarkedSuccessor()
    {
        LoadFromGameState();

        if (string.IsNullOrWhiteSpace(markedSuccessorId))
            return null;

        if (GameState.Instance == null)
            return null;

        BuddyData buddy = GameState.Instance.FindBuddy(markedSuccessorId);
        if (buddy == null)
        {
            ClearSuccessor();
            return null;
        }

        return buddy;
    }

    // Use this when another script only needs the id.
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

    public bool HasMarkedSuccessor()
    {
        return GetMarkedSuccessor() != null;
    }

    public void LoadFromGameState()
    {
        if (GameState.Instance == null)
            return;

        // Reflection keeps this compatible even if GameState does not have the field yet.
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
