using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveCurrentOnCampReturn : MonoBehaviour
{
    [Header("Save Timing")]
    public bool saveOnStart = true;
    public bool saveOnlyIfGameStateExists = true;
    public string campSceneName = "CampScene";

    private void Start()
    {
        if (!saveOnStart) return;
        if (SceneManager.GetActiveScene().name != campSceneName) return;
        if (saveOnlyIfGameStateExists && GameState.Instance == null)
        {
            Debug.LogWarning("[SaveCurrentOnCampReturn] No GameState found. Skipping camp return save.");
            return;
        }
        SporeSaveManager.SaveCurrentSlotFromGameState();
    }
}
