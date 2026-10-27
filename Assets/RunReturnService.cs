using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum RunReturnReason { NormalExit, Retreat }

/// <summary>One outcome/return transaction authority for normal runs and explicitly guarded Camp simulations.</summary>
public static class RunReturnService
{
    static bool returnInProgress,preparedOutcome,campSimulation;
    static UnityEngine.Object returnOwner;
    public static bool DevelopmentPreparationFailed {get;private set;}
    public static bool PreparingSimulatedOutcome {get;private set;}
    public static bool HasPreparedOutcome=>preparedOutcome;
    public static bool HasPreparedCampSimulation=>campSimulation&&preparedOutcome;
    public static string LastFailure {get;private set;}
    public static float retreatSnackLossPercent=0.75f;
    public static RunReturnReason LastReturnReason{get;private set;}=RunReturnReason.NormalExit;
    public static int LastRetreatMushroomsLost{get;private set;}
    public static int LastRetreatShiniesLost{get;private set;}

    // Transactions require an explicitly selected, valid committed campaign before any outcome mutation.
    public static void RequireSelectedCampaign()
    {
        int slot=SporeSaveManager.GetCurrentSlot();
        if(slot<1||slot>SporeSaveManager.SlotCount||SporeSaveManager.InspectSlot(slot)!=SporeSaveManager.SlotStatus.Valid)
            throw new System.IO.InvalidDataException("Return requires a valid selected campaign. Recover the last committed Camp from Main Menu.");
    }

    public static bool ReturnToCamp(string sceneToLoad="CampScene",bool saveRunBeforeLeaving=true,bool saveSlotAfterRunCommit=true,string source="Run return")
        =>ReturnToCamp(sceneToLoad,saveRunBeforeLeaving,saveSlotAfterRunCommit,RunReturnReason.NormalExit,source);

    public static bool ReturnToCamp(string sceneToLoad,bool saveRunBeforeLeaving,bool saveSlotAfterRunCommit,RunReturnReason returnReason,string source)
    {
        sceneToLoad=GameSceneCatalog.Resolve(sceneToLoad);
        if(DevelopmentPreparationFailed){Debug.LogWarning("[Run return] Recover the last committed Camp before retrying failed preparation.");return false;}
        if(!Application.CanStreamedLevelBeLoaded(sceneToLoad)){Debug.LogWarning("[Run return] Destination unavailable; campaign unchanged: "+sceneToLoad);return false;}
        if(campSimulation)
        {
            var camp=UnityEngine.Object.FindAnyObjectByType<CampSceneController>();
            if(!V1RuntimeProfile.DevelopmentToolsEnabled||!SporeGobbo.Safety.Persistence.Isolated||camp==null||!camp.WorldInputReady)
                return false;
            returnOwner=camp;
        }
        else
        {
            returnOwner=UnityEngine.Object.FindAnyObjectByType<RunStartInitializer>();
            if(returnOwner==null||SampleSceneModeController.IsIntroMode){Debug.LogWarning("[Run return] No active NormalRun lifecycle; no effects applied.");return false;}
        }
        if(returnInProgress){Debug.LogWarning("[Run return] Duplicate request ignored from "+source);return false;}
        returnInProgress=true;
        if(!preparedOutcome){LastReturnReason=returnReason;LastRetreatMushroomsLost=LastRetreatShiniesLost=0;}
        var state=EnsureGameState();
        try
        {
            RequireSelectedCampaign();
            if(!preparedOutcome)
            {
                if(returnReason==RunReturnReason.Retreat){RunSnackLootService.FinalizeRetreat(state,retreatSnackLossPercent);ApplyRetreatLootPenalty(state);}
                else RunSnackLootService.FinalizeSuccess(state);
                if(saveRunBeforeLeaving)state.SaveFromRun();
                preparedOutcome=true;CampArrivalContext.SetPending(CampArrivalMode.ReturnedFromRun);SporePauseService.Acquire(returnOwner);
            }
            if(saveSlotAfterRunCommit)SporeSaveManager.SaveCurrentSlotFromGameState();
            PlayerDeathRunStore.Instance?.ClearPendingDeath();
            PlayerDeathWatcher.SuppressDeathHandlingForSceneChange();SporePauseService.ResetAll();
            LastFailure=null;SceneManager.LoadScene(sceneToLoad);return true;
        }
        catch(Exception error)
        {
            returnInProgress=false;if(!preparedOutcome)DevelopmentPreparationFailed=true;
            LastFailure="The return could not be saved. Your last committed Camp is preserved.";
            SporePauseService.Acquire(returnOwner);
            Debug.LogWarning("[Run return] Outcome retained; retry without repeating rewards. "+error.Message);
            RunReturnFailureView.Show(returnOwner);return false;
        }
    }

    public static bool ReturnSimulatedCampOutcome(Action apply)
    {
        var camp=UnityEngine.Object.FindAnyObjectByType<CampSceneController>();
        var state=GameState.Instance;
        if(!V1RuntimeProfile.DevelopmentToolsEnabled||!SporeGobbo.Safety.Persistence.Isolated||camp==null||!camp.WorldInputReady||state==null||state.HasActiveRunSnapshot)
            throw new InvalidOperationException("Camp simulation requires a ready Camp and owned development tools");
        if(campSimulation&&preparedOutcome)return ReturnToCamp("CampScene",false,true,LastReturnReason,"Retry simulated Camp return");
        if(DevelopmentPreparationFailed)throw new InvalidOperationException("Recover the last committed Camp after failed preparation");
        if(apply==null)throw new ArgumentNullException(nameof(apply));
        ResetForNewRun();returnOwner=camp;campSimulation=true;PreparingSimulatedOutcome=true;SporePauseService.Acquire(camp);
        try
        {
            RequireSelectedCampaign();
            apply();preparedOutcome=true;LastReturnReason=RunReturnReason.NormalExit;
            CampArrivalContext.SetPending(CampArrivalMode.ReturnedFromRun);
        }
        catch(Exception error)
        {
            DevelopmentPreparationFailed=true;LastFailure="The simulated return could not be prepared. Recover your last committed Camp from the Main Menu.";
            Debug.LogWarning("[Camp simulation] Preparation stopped without committing: "+error.Message);RunReturnFailureView.Show(camp);return false;
        }
        finally{PreparingSimulatedOutcome=false;}
        return ReturnToCamp("CampScene",false,true,RunReturnReason.NormalExit,"Simulated Camp outcome");
    }
    public static void AcknowledgeCampArrival()
    {
        returnInProgress=preparedOutcome=campSimulation=PreparingSimulatedOutcome=DevelopmentPreparationFailed=false;
        LastFailure=null;SporePauseService.Release(returnOwner);returnOwner=null;
    }
    public static void ResetForNewRun()
    {
        AcknowledgeCampArrival();LastReturnReason=RunReturnReason.NormalExit;LastRetreatMushroomsLost=LastRetreatShiniesLost=0;
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
