using System.Collections.Generic;
using UnityEngine;

public class GobboVisualDatabase : MonoBehaviour
{
    public static GobboVisualDatabase Instance { get; private set; }

    [Header("Visual Sets")]
    public List<GobboVisualSet> visualSets = new List<GobboVisualSet>();

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        Instance = this;
        RefreshActiveVisualControllers();
    }

    public GobboVisualSet GetVisualSet(string visualSetId, BuddyType type, GobboAgeStage ageStage)
    {
        string requestedId = NormalizeVisualId(visualSetId);

        if (!string.IsNullOrWhiteSpace(requestedId))
        {
            foreach (GobboVisualSet set in visualSets)
            {
                if (set != null && NormalizeVisualId(set.visualSetId) == requestedId)
                    return set;
            }
        }

        foreach (GobboVisualSet set in visualSets)
        {
            if (set != null && set.gobboType == type && set.ageStage == ageStage)
                return set;
        }

        foreach (GobboVisualSet set in visualSets)
        {
            if (set != null && set.gobboType == BuddyType.Baby)
                return set;
        }

        return visualSets.Count > 0 ? visualSets[0] : null;
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

    static string NormalizeVisualId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant();
    }
}
