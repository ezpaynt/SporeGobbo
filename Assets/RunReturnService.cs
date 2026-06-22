using UnityEngine;
using UnityEngine.SceneManagement;

public static class RunReturnService
{
    private static bool returnInProgress;

    public static bool ReturnToCamp(
        string sceneToLoad = "CampScene",
        bool saveRunBeforeLeaving = true,
        bool saveSlotAfterRunCommit = true,
        string source = "Run return")
    {
        if (returnInProgress)
        {
            Debug.LogWarning("[RunReturnService] Ignored duplicate return request from " + source + ".");
            return false;
        }

        returnInProgress = true;
        Debug.Log("[RunReturnService] Returning to " + sceneToLoad + " from " + source + ".");

        GameState state = EnsureGameState();

        if (saveRunBeforeLeaving)
            state.SaveFromRun();

        if (saveSlotAfterRunCommit)
            SporeSaveManager.SaveCurrentSlotFromGameState();

        PlayerDeathRunStore deathStore = PlayerDeathRunStore.Instance;
        if (deathStore != null)
            deathStore.ClearPendingDeath();

        PlayerDeathWatcher.SuppressDeathHandlingForSceneChange();
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneToLoad);
        return true;
    }

    public static void ResetForNewRun()
    {
        returnInProgress = false;
    }

    static GameState EnsureGameState()
    {
        if (GameState.Instance != null)
            return GameState.Instance;

        GameObject stateObject = new GameObject("GameState");
        return stateObject.AddComponent<GameState>();
    }
}
