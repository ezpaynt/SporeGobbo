using UnityEngine;
using UnityEngine.SceneManagement;

public class ExitTrigger : MonoBehaviour
{
    public string sceneToLoad = "CampScene";
    public bool saveRunBeforeLeaving = true;
    public bool saveSlotAfterRunCommit = true;
    private bool used = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (used) return;
        if (!other.CompareTag("Player")) return;
        used = true;

        Debug.Log("Leaving level through ExitTrigger to " + sceneToLoad + "...");

        if (saveRunBeforeLeaving)
        {
            EnsureGameState().SaveFromRun();
        }

        // Successful run exit is one of the only times run progress is committed.
        if (saveSlotAfterRunCommit)
        {
            SporeSaveManager.SaveCurrentGameToCurrentSlot();
        }

        PlayerDeathWatcher.SuppressDeathHandlingForSceneChange();
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneToLoad);
    }

    GameState EnsureGameState()
    {
        if (GameState.Instance != null) return GameState.Instance;
        GameObject stateObject = new GameObject("GameState");
        return stateObject.AddComponent<GameState>();
    }
}
