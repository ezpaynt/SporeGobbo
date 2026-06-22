using UnityEngine;
using UnityEngine.SceneManagement;

public enum RunReturnReason
{
    NormalExit,
    Retreat
}

public static class RunReturnService
{
    private static bool returnInProgress;

    public static RunReturnReason LastReturnReason { get; private set; } = RunReturnReason.NormalExit;
    public static int LastRetreatMushroomsLost { get; private set; }
    public static int LastRetreatShiniesLost { get; private set; }

    public static bool ReturnToCamp(
        string sceneToLoad = "CampScene",
        bool saveRunBeforeLeaving = true,
        bool saveSlotAfterRunCommit = true,
        string source = "Run return")
    {
        return ReturnToCamp(
            sceneToLoad,
            saveRunBeforeLeaving,
            saveSlotAfterRunCommit,
            RunReturnReason.NormalExit,
            source);
    }

    public static bool ReturnToCamp(
        string sceneToLoad,
        bool saveRunBeforeLeaving,
        bool saveSlotAfterRunCommit,
        RunReturnReason returnReason,
        string source)
    {
        if (returnInProgress)
        {
            Debug.LogWarning("[RunReturnService] Ignored duplicate return request from " + source + ".");
            return false;
        }

        returnInProgress = true;
        LastReturnReason = returnReason;
        LastRetreatMushroomsLost = 0;
        LastRetreatShiniesLost = 0;

        Debug.Log("[RunReturnService] Returning to " + sceneToLoad + " from " + source + " | reason=" + returnReason + ".");

        GameState state = EnsureGameState();

        if (returnReason == RunReturnReason.Retreat)
            ApplyRetreatLootPenalty(state);

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
        LastReturnReason = RunReturnReason.NormalExit;
        LastRetreatMushroomsLost = 0;
        LastRetreatShiniesLost = 0;
    }

    static void ApplyRetreatLootPenalty(GameState state)
    {
        if (state == null || state.lastRun == null)
        {
            Debug.LogWarning("[RunReturnService] Retreat loot penalty skipped because run tracking data is missing.");
            return;
        }

        state.EnsureRuntimeDefaults();
        GobboUnitSaveData leader = state.GetLeader();
        RunSummaryData run = state.lastRun;
        if (leader == null)
            return;

        int mushroomStart = Mathf.Max(0, run.mushroomsStart);
        int shinyStart = Mathf.Max(0, run.shiniesStart);

        int mushroomsGained = GetTrackedRunGain(run.mushroomsGained, mushroomStart, leader.mushrooms);
        int shiniesGained = GetTrackedRunGain(run.shiniesGained, shinyStart, leader.shinies);

        LastRetreatMushroomsLost = ApplyResourcePenalty(
            ref leader.mushrooms,
            mushroomStart,
            mushroomsGained);

        LastRetreatShiniesLost = ApplyResourcePenalty(
            ref leader.shinies,
            shinyStart,
            shiniesGained);

        leader.money = leader.shinies;

        Debug.Log(
            "[RunReturnService] Retreat loot penalty applied" +
            " | mushrooms gained=" + mushroomsGained +
            " lost=" + LastRetreatMushroomsLost +
            " final=" + leader.mushrooms +
            " | shinies gained=" + shiniesGained +
            " lost=" + LastRetreatShiniesLost +
            " final=" + leader.shinies + ".");
    }

    static int GetTrackedRunGain(int trackedGain, int startAmount, int currentAmount)
    {
        if (trackedGain > 0)
            return trackedGain;

        return Mathf.Max(0, currentAmount - startAmount);
    }

    static int ApplyResourcePenalty(ref int currentAmount, int runStartAmount, int gainedThisRun)
    {
        currentAmount = Mathf.Max(0, currentAmount);
        runStartAmount = Mathf.Max(0, runStartAmount);
        gainedThisRun = Mathf.Max(0, gainedThisRun);

        int kept = Mathf.CeilToInt(gainedThisRun * 0.25f);
        int wantedLoss = Mathf.Max(0, gainedThisRun - kept);
        int beforePenalty = currentAmount;

        currentAmount = Mathf.Max(runStartAmount, currentAmount - wantedLoss);
        currentAmount = Mathf.Max(0, currentAmount);

        return Mathf.Max(0, beforePenalty - currentAmount);
    }

    static GameState EnsureGameState()
    {
        if (GameState.Instance != null)
            return GameState.Instance;

        GameObject stateObject = new GameObject("GameState");
        return stateObject.AddComponent<GameState>();
    }
}
