using UnityEngine;

/// <summary>
/// Optional helper for CampScene.
/// Saves current camp state when camp opens. This should not be used in SampleScene.
/// </summary>
public class SaveCurrentOnCampReturn : MonoBehaviour
{
    public bool saveOnStart = true;

    void Start()
    {
        if (!saveOnStart) return;
        if (GameState.Instance == null) return;
        SporeSaveManager.SaveCurrentGameToCurrentSlot();
    }
}
