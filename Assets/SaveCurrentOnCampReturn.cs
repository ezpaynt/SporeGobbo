using UnityEngine;

public class SaveCurrentOnCampReturn : MonoBehaviour
{
    [Header("Save Timing")]
    public bool saveOnStart = true;
    public bool saveOnlyIfGameStateExists = true;

    private void Start()
    {
        if (!saveOnStart)
            return;

        if (saveOnlyIfGameStateExists && GameState.Instance == null)
        {
            Debug.LogWarning("[SaveCurrentOnCampReturn] No GameState found. Skipping camp return save.");
            return;
        }

        // Unified save system: SporeSaveManager pulls the current GameState itself.
        SporeSaveManager.SaveCurrentSlotFromGameState();
    }
}
