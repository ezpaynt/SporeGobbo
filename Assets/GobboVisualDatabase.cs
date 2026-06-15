using System.Collections.Generic;
using UnityEngine;

public class GobboVisualDatabase : MonoBehaviour
{
    public static GobboVisualDatabase Instance { get; private set; }

    [Header("Visual Sets")]
    public List<GobboVisualSet> visualSets = new List<GobboVisualSet>();

    [Header("Diagnostics")]
    public bool logVisualDatabaseDiagnostics = true;

    void Awake()
    {
        Instance = this;
        LogAvailableSets("Awake");
    }

    void OnEnable()
    {
        Instance = this;
        LogAvailableSets("OnEnable");
        RefreshActiveVisualControllers();
    }

    void Start()
    {
        Instance = this;
        LogAvailableSets("Start");
        RefreshActiveVisualControllers();
    }

    public GobboVisualSet GetVisualSet(string visualSetId, BuddyType type, GobboAgeStage ageStage)
    {
        string requestedId = NormalizeVisualId(visualSetId);
        LogLookupStart(visualSetId, requestedId, type, ageStage);

        if (!string.IsNullOrWhiteSpace(requestedId))
        {
            foreach (GobboVisualSet set in visualSets)
            {
                if (set != null && NormalizeVisualId(set.visualSetId) == requestedId)
                {
                    LogLookupResult("id", set);
                    return set;
                }
            }
        }

        foreach (GobboVisualSet set in visualSets)
        {
            if (set != null && set.gobboType == type && set.ageStage == ageStage)
            {
                LogLookupResult("type+stage", set);
                return set;
            }
        }

        foreach (GobboVisualSet set in visualSets)
        {
            if (set != null && set.gobboType == BuddyType.Baby)
            {
                LogLookupResult("baby fallback", set);
                return set;
            }
        }

        GobboVisualSet fallback = visualSets.Count > 0 ? visualSets[0] : null;
        LogLookupResult(fallback != null ? "first visual set fallback" : "no match", fallback);
        return fallback;
    }

    public string GetDefaultVisualId(BuddyType type, GobboAgeStage ageStage)
    {
        GobboVisualSet set = GetVisualSet("", type, ageStage);
        return set != null ? set.visualSetId : (type.ToString().ToLowerInvariant() + "_" + ageStage.ToString().ToLowerInvariant());
    }

    public void RefreshActiveVisualControllers()
    {
        GobboVisualController[] controllers = Object.FindObjectsByType<GobboVisualController>(FindObjectsSortMode.None);
        foreach (GobboVisualController controller in controllers)
        {
            if (controller == null) continue;
            controller.RefreshVisual();
        }
    }

    [ContextMenu("Log Visual Database Contents")]
    public void LogVisualDatabaseContents()
    {
        LogAvailableSets("ContextMenu");
    }

    void LogLookupStart(string rawId, string normalizedId, BuddyType type, GobboAgeStage ageStage)
    {
        if (!logVisualDatabaseDiagnostics)
            return;

        Debug.Log(
            "[GobboVisualDatabase] Lookup requested" +
            " | database=" + name +
            " | rawId=" + rawId +
            " | normalizedId=" + normalizedId +
            " | type=" + type +
            " | ageStage=" + ageStage +
            " | visualSetCount=" + visualSets.Count,
            this);
    }

    void LogLookupResult(string matchMode, GobboVisualSet set)
    {
        if (!logVisualDatabaseDiagnostics)
            return;

        Debug.Log(
            "[GobboVisualDatabase] Lookup result" +
            " | database=" + name +
            " | matchMode=" + matchMode +
            " | resultId=" + (set != null ? set.visualSetId : "NULL") +
            " | resultType=" + (set != null ? set.gobboType.ToString() : "NULL") +
            " | resultStage=" + (set != null ? set.ageStage.ToString() : "NULL"),
            this);
    }

    void LogAvailableSets(string source)
    {
        if (!logVisualDatabaseDiagnostics)
            return;

        Dictionary<string, int> idCounts = new Dictionary<string, int>();
        List<string> setSummaries = new List<string>();

        for (int i = 0; i < visualSets.Count; i++)
        {
            GobboVisualSet set = visualSets[i];
            if (set == null)
            {
                setSummaries.Add(i + ": NULL");
                continue;
            }

            string normalizedId = NormalizeVisualId(set.visualSetId);
            if (!idCounts.ContainsKey(normalizedId))
                idCounts[normalizedId] = 0;
            idCounts[normalizedId]++;

            setSummaries.Add(i + ": id=" + set.visualSetId + " normalized=" + normalizedId + " type=" + set.gobboType + " stage=" + set.ageStage);
        }

        List<string> duplicateIds = new List<string>();
        foreach (KeyValuePair<string, int> pair in idCounts)
        {
            if (pair.Value > 1)
                duplicateIds.Add(pair.Key + " x" + pair.Value);
        }

        Debug.Log(
            "[GobboVisualDatabase] Available visual sets" +
            " | source=" + source +
            " | database=" + name +
            " | activeInstance=" + (Instance != null ? Instance.name : "NULL") +
            " | count=" + visualSets.Count +
            " | duplicateIds=" + (duplicateIds.Count > 0 ? string.Join(", ", duplicateIds) : "none") +
            "\n" + string.Join("\n", setSummaries),
            this);
    }

    static string NormalizeVisualId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant();
    }
}
