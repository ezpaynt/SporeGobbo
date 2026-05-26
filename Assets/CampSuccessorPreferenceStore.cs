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
        return obj.AddComponent<CampSuccessorPreferenceStore>();
    }

    public void MarkSuccessor(string buddyId)
    {
        markedSuccessorId = string.IsNullOrWhiteSpace(buddyId) ? "" : buddyId.Trim();
    }

    public void ClearSuccessor()
    {
        markedSuccessorId = "";
    }

    public BuddyData GetMarkedSuccessor()
    {
        if (GameState.Instance == null || string.IsNullOrWhiteSpace(markedSuccessorId))
            return null;

        return GameState.Instance.FindBuddy(markedSuccessorId);
    }

    public bool HasValidMarkedSuccessor()
    {
        return GetMarkedSuccessor() != null;
    }
}
