using UnityEngine;

public class CampSuccessorStore : MonoBehaviour
{
    public static CampSuccessorStore Instance { get; private set; }

    [Header("Marked Camp Successor")]
    public string markedSuccessorId = "";
    public string markedSuccessorName = "";

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

    public static CampSuccessorStore GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        CampSuccessorStore found = Object.FindAnyObjectByType<CampSuccessorStore>(FindObjectsInactive.Include);
        if (found != null)
        {
            Instance = found;
            return Instance;
        }

        GameObject obj = new GameObject("CampSuccessorStore");
        return obj.AddComponent<CampSuccessorStore>();
    }

    public bool HasMarkedSuccessor()
    {
        return !string.IsNullOrWhiteSpace(markedSuccessorId);
    }

    public void MarkSuccessor(BuddyData buddy)
    {
        if (buddy == null)
        {
            ClearMarkedSuccessor();
            return;
        }

        buddy.EnsureId();
        buddy.EnsureRuntimeDefaults();

        markedSuccessorId = buddy.uniqueId;
        markedSuccessorName = buddy.buddyName;
    }

    public void ClearMarkedSuccessor()
    {
        markedSuccessorId = "";
        markedSuccessorName = "";
    }

    public bool IsMarked(string buddyId)
    {
        return !string.IsNullOrWhiteSpace(buddyId) && buddyId == markedSuccessorId;
    }
}
