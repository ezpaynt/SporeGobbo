using UnityEngine;
using UnityEngine.SceneManagement;

public class ExitTrigger : MonoBehaviour
{
    public string sceneToLoad = "CampScene";
    public bool saveRunBeforeLeaving = true;

    private bool used = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (used) return;
        if (!other.CompareTag("Player")) return;

        used = true;
        Debug.Log("Leaving level through ExitTrigger to " + sceneToLoad + "...");

        if (saveRunBeforeLeaving)
            EnsureGameState().SaveFromRun();

        // This is a clean exit, not player death. Prevent OnDisable during scene unload
        // from creating a false death flow.
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
