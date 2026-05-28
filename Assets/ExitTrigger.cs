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

        if (saveSlotAfterRunCommit)
        {
            SporeSaveManager.SaveCurrentSlotFromGameState();
        }

        // A clean exit through the portal is NOT a death. Clear stale pending death data
        // before CampScene loads, or CampSceneController will open the death panel after
        // a normal successful run.
        PlayerDeathRunStore deathStore = PlayerDeathRunStore.Instance;
        if (deathStore != null)
        {
            deathStore.ClearPendingDeath();
            Debug.Log("[ExitTrigger] Cleared pending death flow for normal run exit.");
        }

        PlayerDeathWatcher.SuppressDeathHandlingForSceneChange();
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneToLoad);
    }

    private GameState EnsureGameState()
    {
        if (GameState.Instance != null) return GameState.Instance;
        GameObject stateObject = new GameObject("GameState");
        return stateObject.AddComponent<GameState>();
    }
}
