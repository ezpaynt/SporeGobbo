using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveCurrentOnCampReturn : MonoBehaviour
{
    [Header("Save Timing")]
    public bool saveOnStart = true;
    public bool saveOnlyIfGameStateExists = true;
    public string campSceneName = "CampScene";

    private System.Collections.IEnumerator Start()
    {
        if (!saveOnStart) yield break;
        if (!GameSceneCatalog.IsCamp(SceneManager.GetActiveScene())) yield break;
        CampSceneController controller = FindAnyObjectByType<CampSceneController>();
        while (controller != null && !controller.WorldInputReady) yield return null;
        if (saveOnlyIfGameStateExists && GameState.Instance == null)
        {
            Debug.LogWarning("[SaveCurrentOnCampReturn] No GameState found. Skipping camp return save.");
            yield break;
        }
        SporeSaveManager.SaveCurrentSlotFromGameState();
    }
}
