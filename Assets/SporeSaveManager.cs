using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class SporeSaveManager
{
 static readonly Unity.Profiling.ProfilerMarker stage61SaveProfile = new Unity.Profiling.ProfilerMarker("Spore.Stage61.SaveCommit");
 static readonly Unity.Profiling.ProfilerMarker stage61SnapshotProfile = new Unity.Profiling.ProfilerMarker("Spore.Stage61.SnapshotCapture");

    public const int SlotCount = 3;
    private static string LastSlotKey => V1RuntimeProfile.PreferencePrefix + "LastPlayedSlot";
    private static string CurrentSlotKey => V1RuntimeProfile.PreferencePrefix + "CurrentSlot";
    static string developmentWriteFailure;
    public static void FailNextDevelopmentWrite(string at)
    {
        if (!V1RuntimeProfile.AllowsTestActions || !SporeGobbo.Safety.Persistence.Isolated || (at != "flushed" && at != "validated"))
            throw new InvalidOperationException("Write fault injection requires the owned development profile");
        developmentWriteFailure = at;
    }
    public static void CommitResolvedDevelopmentSuccession() => CommitResolvedSuccession();
    public static void CommitResolvedSuccession()
    {
        var state = GameState.Instance;
        if (state == null || state.HasActiveRunSnapshot ||
            state.GetLeader().isDead || PlayerDeathRunStore.Instance == null || !PlayerDeathRunStore.Instance.playerDiedThisRun)
            throw new InvalidOperationException("No resolved development succession to commit");
        int slot = GetCurrentSlot();
        if (slot < 1 || slot > SlotCount) throw new InvalidDataException("Succession has no selected campaign");
        var previous = LoadSlot(slot);
        if (previous == null || !previous.hasSave) throw new InvalidDataException("Last committed Camp is unavailable");
        if (previous.leader.uniqueId == state.GetLeader().uniqueId) return; // Already durably committed, UI acknowledgement may retry.
        if (!state.deathHistory.Exists(record => record != null && record.wasLeader && record.gobboId == previous.leader.uniqueId))
            throw new InvalidDataException("Succession does not reconcile the previous leader");
        var snapshot = BuildSlotFromGameState(slot, previous);
        snapshot.pendingCampArrivalMode = (int)CampArrivalMode.LoadedSave;
        snapshot.pendingCampArrivalId = CampArrivalContext.PendingId ?? Guid.NewGuid().ToString("N");
        SaveSlot(snapshot);
    }
    public static void CommitReadyDevelopmentArrival(bool completeIntro = false) => CommitReadyArrival(completeIntro);
    public static void CommitReadyArrival(bool completeIntro = false)
    {
        var state = GameState.Instance;
        var spawner = UnityEngine.Object.FindAnyObjectByType<CampPlayableSpawner>();
        if (state == null || state.HasActiveRunSnapshot || state.GetLeader().isDead ||
            spawner == null || spawner.SpawnedPlayer == null || !(DiggableTerrainService.Active is ICampRegionSnapshotSource) ||
            PlayerDeathRunStore.Instance != null && PlayerDeathRunStore.Instance.playerDiedThisRun || CampArrivalContext.HasPending)
            throw new InvalidOperationException("Camp arrival is not ready for durable acknowledgement");
        int slot = GetCurrentSlot();
        if (slot < 1 || slot > SlotCount || !HasSave(slot)) throw new InvalidDataException("Arrival has no validated campaign slot");
        var snapshot = BuildSlotFromGameState(slot, LoadSlot(slot));
        // Stage first-arrival discovery only in the detached snapshot. Publish the real fact/event after commit.
        if(completeIntro)StoryEventService.Complete(snapshot.storyProgress,StoryEventIds.ASporeHatchesCampDiscovered,snapshot.currentRunNumber,snapshot.campCycleNumber);
        SaveSlot(snapshot);
        if(completeIntro)StoryEventService.Complete(state,StoryEventIds.ASporeHatchesCampDiscovered);
    }
    public enum SlotStatus { Missing, Valid, Corrupt, Unsupported, RecoveryAvailable, IncompatibleLegacy }
    public static SlotStatus InspectSlot(int slotIndex)
    {
        string path = GetSlotPath(slotIndex);
        SlotStatus primary = ValidateExisting(path, slotIndex);
        if (primary == SlotStatus.Valid) return primary;
        SlotStatus backup = ValidateExisting(path + ".bak", slotIndex);
        if (backup == SlotStatus.Valid) return SlotStatus.RecoveryAvailable;
        return primary == SlotStatus.Missing && File.Exists(path + ".bak") ? backup : primary;
    }
    static SlotStatus ValidateExisting(string path, int slotIndex)
    {
        if (!File.Exists(path)) return SlotStatus.Missing;
        try { ValidateStored(CampaignSnapshotStorage.Read(path), slotIndex); return SlotStatus.Valid; }
        catch (IncompatibleLegacyCampaignException) { return SlotStatus.IncompatibleLegacy; }
        catch (NotSupportedException) { return SlotStatus.Unsupported; }
        catch (Exception) { return SlotStatus.Corrupt; }
    }
    static void ValidateStored(string text, int slotIndex) => V1CampaignSnapshot.Decode(text, slotIndex, out _);
    public static void RecoverDevelopmentSlot(int slotIndex) => RecoverSlot(slotIndex);
    public static void RecoverSlot(int slotIndex)
    {
        CampaignSnapshotStorage.Recover(GetSlotPath(slotIndex), text => ValidateStored(text, slotIndex));
    }

    public static string SaveFolder
    {
        get
        {
            string folder = SporeGobbo.Safety.Persistence.CampaignFolder;
            return folder;
        }
    }

    public static string GetSlotPath(int slotIndex)
    {
        slotIndex = ClampSlot(slotIndex);
        return SporeGobbo.Safety.Persistence.SlotPath(SaveFolder, "slot_" + slotIndex + ".json");
    }

    public static bool HasSave(int slotIndex)
    {
        SporeSaveSlotData data = LoadSlot(slotIndex);
        return data != null && data.hasSave;
    }

    public static int SaveCount()
    {
        int count = 0;
        for (int i = 1; i <= SlotCount; i++) if (HasSave(i)) count++;
        return count;
    }

    public static bool CanCreateNewGame() => FindFirstEmptySlot() > 0;

    public static int FindFirstEmptySlot()
    {
        for (int i = 1; i <= SlotCount; i++) if (!File.Exists(GetSlotPath(i)) && !File.Exists(GetSlotPath(i) + ".bak")) return i;
        return 0;
    }

    public static int GetFirstEmptySlotIndex() => FindFirstEmptySlot();

    public static SporeSaveSlotData LoadSlot(int slotIndex)
    {
        slotIndex = ClampSlot(slotIndex);
        string path = GetSlotPath(slotIndex);
        if (!File.Exists(path)) return new SporeSaveSlotData { slotIndex = slotIndex, hasSave = false, saveId = "slot_" + slotIndex };
        try
        {
            string json = CampaignSnapshotStorage.Read(path);
            V1CampaignSnapshot.Decode(json, slotIndex, out var loaded);
            return loaded;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SporeSaveManager] Failed to load save slot " + slotIndex + ": " + ex.Message);
            return new SporeSaveSlotData { slotIndex = slotIndex, hasSave = false, saveId = "slot_" + slotIndex };
        }
    }

    public static void SaveSlot(SporeSaveSlotData data)
    {
 using var _stage61Profile = stage61SaveProfile.Auto();

        if (data == null) return;
        if (data.saveVersion != SporeSaveSlotData.CurrentSaveVersion)
            throw new NotSupportedException("Refusing to rewrite unsupported campaign version");
        data = JsonUtility.FromJson<SporeSaveSlotData>(JsonUtility.ToJson(data));
        data.slotIndex = ClampSlot(data.slotIndex);
        data.saveId = "slot_" + data.slotIndex;
        data.hasSave = true;
        data.lastPlayedUtcTicks = DateTime.UtcNow.Ticks;
        data.lastPlayedAt = DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        Normalize(data);
        string path = GetSlotPath(data.slotIndex);
        string json;
        Action<string> validate;
        string campaignId = V1RuntimeProfile.CampaignIdPrefix + Guid.NewGuid().ToString("N"); long generation = 1;
        if (File.Exists(path))
        {
            var previous = V1CampaignSnapshot.Decode(CampaignSnapshotStorage.Read(path), data.slotIndex, out _);
            campaignId = previous.campaignId; generation = checked(previous.generation + 1);
        }
        json = V1CampaignSnapshot.Encode(data, campaignId, generation);
        int slot = data.slotIndex;
        validate = text => V1CampaignSnapshot.Decode(text, slot, out _);
        string failure = V1RuntimeProfile.AllowsTestActions ? developmentWriteFailure : null;
        developmentWriteFailure = null;
        CampaignSnapshotStorage.Commit(path, json, validate, at => { if (at == failure) throw new IOException("Synthetic write interruption at " + at); });
        if (GameState.Instance != null && GameState.Instance.GetLeader().uniqueId == data.leader.uniqueId)
            GameState.Instance.campTerrainState.regionSnapshot = data.campTerrainState.regionSnapshot.Clone();
        SetCurrentSlot(data.slotIndex);
        SetLastPlayedSlot(data.slotIndex);
        SyncBridgeFromSlot(data);
        Debug.Log("[SporeSaveManager] Saved unified slot " + data.slotIndex + " to " + GetSlotPath(data.slotIndex));
    }

    public static SporeSaveSlotData CreateNewGame(string playerName, string firstSceneName)
    {
        int slotIndex = FindFirstEmptySlot();
        if (slotIndex <= 0)
        {
            Debug.LogWarning("[SporeSaveManager] Cannot create new game. All 3 save slots are full.");
            return null;
        }
        return CreateNewGame(slotIndex, playerName, firstSceneName, false);
    }

    public static SporeSaveSlotData CreateNewGame(int slotIndex, string firstSceneName) => CreateNewGame(slotIndex, "Gobbo", firstSceneName, false);
    public static SporeSaveSlotData CreateNewGame(int slotIndex, string playerName, string firstSceneName) => CreateNewGame(slotIndex, playerName, firstSceneName, false);

    public static SporeSaveSlotData CreateNewGame(int slotIndex, string playerName, string firstSceneName, bool allowOverwrite)
    {
        slotIndex = ClampSlot(slotIndex);
        if ((File.Exists(GetSlotPath(slotIndex)) || File.Exists(GetSlotPath(slotIndex) + ".bak")) && !allowOverwrite)
        {
            Debug.LogWarning("[SporeSaveManager] Slot " + slotIndex + " already has a save. Refusing to overwrite.");
            return null;
        }
        SporeSaveSlotData data = SporeSaveSlotData.CreateNew(slotIndex, playerName, firstSceneName);
        data.campTerrainState.regionSnapshot = CampRegionSnapshot.Pristine();
        StoryEventService.Complete(data.storyProgress, StoryEventIds.ASporeHatchesNamed, data.currentRunNumber, data.campCycleNumber);
        SaveSlot(data);
        ApplySlotToGameState(data);
        return data;
    }

    public static SporeSaveSlotData CreateNewGameInFirstEmptySlot(string firstSceneName, string playerName = "Gobbo") => CreateNewGame(playerName, firstSceneName);

    public static void SaveCurrentSlotFromGameState()
    {
        if (DeferCampaignCommit()) return;
        int slotIndex = GetCurrentSlot();
        if (slotIndex <= 0) slotIndex = GetLastPlayedSlot();
        if (slotIndex <= 0 || !HasSave(slotIndex))
        {
            Debug.LogWarning("[SporeSaveManager] No current save slot set. Refusing to create a new autosave.");
            return;
        }
        SporeSaveSlotData existing = LoadSlot(slotIndex);
        SporeSaveSlotData data = BuildSlotFromGameState(slotIndex, existing);
        SaveSlot(data);
    }

    public static void SaveCurrentGameToCurrentSlot() => SaveCurrentSlotFromGameState();

    public static void SaveCurrentGameToSlot(int slotIndex)
    {
        if (DeferCampaignCommit()) return;
        slotIndex = ClampSlot(slotIndex);
        if (!HasSave(slotIndex))
        {
            Debug.LogWarning("[SporeSaveManager] Cannot save to empty slot " + slotIndex + " except through New Game.");
            return;
        }
        SaveSlot(BuildSlotFromGameState(slotIndex, LoadSlot(slotIndex)));
    }

    public static SporeSaveSlotData BuildSlotFromGameState(int slotIndex, SporeSaveSlotData existing = null)
    {
 using var _stage61Profile = stage61SnapshotProfile.Auto();

        var data = BuildSlotFromCampaign(slotIndex, GameState.Instance, existing);
        if (DiggableTerrainService.Active is ICampRegionSnapshotSource region)
            data.campTerrainState.regionSnapshot = region.CaptureRegionSnapshot(GameState.Instance);
        {
            data.pendingCampArrivalMode = CampArrivalContext.HasPending ? (int)CampArrivalContext.PeekOrDefault() : -1;
            data.pendingCampArrivalId = CampArrivalContext.PendingId;
        }
        return data;
    }

    static bool DeferCampaignCommit()
    {
        var state = GameState.Instance;
        if (state == null) return false; // Unselected historical callers remain harmless; actual capture still requires canonical state.
        bool pending = UnityEngine.Object.FindAnyObjectByType<IntroRegion>()!=null || state.HasActiveRunSnapshot || !state.lineageEnded &&
            (state.GetLeader().isDead || PlayerDeathRunStore.Instance != null && PlayerDeathRunStore.Instance.playerDiedThisRun);
        pending |= RunReturnService.DevelopmentPreparationFailed || RunReturnService.PreparingSimulatedOutcome;
        var controller = UnityEngine.Object.FindAnyObjectByType<CampSceneController>();
        pending |= controller != null && !controller.WorldInputReady;
        if (pending) Debug.Log("[Campaign save] Commit deferred; last committed Camp remains recoverable.");
        return pending;
    }

    // One save authority; explicit campaign input makes detached capture testable without a scene singleton.
    public static SporeSaveSlotData BuildSlotFromCampaign(int slotIndex, GameState gs, SporeSaveSlotData existing = null)
    {
        if (gs == null) throw new InvalidOperationException("Save capture requires the canonical campaign");
        if(DiggableTerrainService.Active is ICampaignTransientActivity activity)activity.FinishForCampaignCapture(gs);
        slotIndex = ClampSlot(slotIndex);
        existing = existing != null && existing.hasSave
            ? JsonUtility.FromJson<SporeSaveSlotData>(JsonUtility.ToJson(existing))
            : SporeSaveSlotData.CreateNew(slotIndex, "Gobbo", "CampScene");
        if (gs != null)
        {
            gs.EnsureRuntimeDefaults();
            existing.currentRunNumber = Mathf.Max(1, gs.currentRunNumber);
            existing.runNumber = existing.currentRunNumber;
            existing.campCycleNumber = Mathf.Max(0, gs.campCycleNumber);
            existing.maxActiveSquad = Mathf.Max(1, gs.maxActiveSquad);
            existing.campLevel = Mathf.Max(1, gs.campLevel);
            existing.lineageEnded = gs.lineageEnded;
            existing.leader = gs.GetLeader().CloneUnit();
            existing.ownedGobbos = CloneUnits(gs.ownedGobbos);
            existing.activeSquadIds = gs.activeSquadIds != null ? new List<string>(gs.activeSquadIds) : new List<string>();
            existing.unlockedStations = gs.unlockedStations != null ? new List<string>(gs.unlockedStations) : new List<string>();
            existing.decorationsUnlocked = gs.decorationsUnlocked != null ? new List<string>(gs.decorationsUnlocked) : new List<string>();
            existing.campaignFacts = gs.campaignFacts != null ? gs.campaignFacts.Clone() : new CampaignFacts();
            existing.itemStacks = InventoryService.CloneStacks(gs.itemStacks);
            existing.lastRun = CloneRunSummary(gs.lastRun);
            existing.markedSuccessorId = gs.markedSuccessorId;
            existing.deathHistory = gs.GetDeathHistoryCopy();
            existing.campTerrainState = gs.campTerrainState != null ? gs.campTerrainState.Clone() : new CampTerrainState();
            existing.persistentTestCaveTerrain = gs.persistentTestCaveTerrain != null
                ? gs.persistentTestCaveTerrain.Clone()
                : new PersistentTestCaveTerrainState();
            existing.storyProgress = gs.storyProgress != null ? gs.storyProgress.Clone() : new StoryProgressData();
        }

        // Scene stores and UI bridges mirror campaign facts; they cannot replace them during capture.
        existing.nextSceneName = "CampScene";
        existing.Normalize();
        return JsonUtility.FromJson<SporeSaveSlotData>(JsonUtility.ToJson(existing));
    }

    public static bool ApplySlotToGameState(SporeSaveSlotData data)
    {
        if(GameState.Instance!=null&&DiggableTerrainService.Active is ICampaignTransientActivity activity)activity.FinishForCampaignRestore(GameState.Instance);
        if (data == null || !data.hasSave) return false;
        Normalize(data);
        EnsureGameState();
        GameState gs = GameState.Instance;
        if (gs == null) return false;
        gs.currentRunNumber = Mathf.Max(1, data.currentRunNumber);
        gs.campCycleNumber = Mathf.Max(0, data.campCycleNumber);
        gs.maxActiveSquad = Mathf.Max(1, data.maxActiveSquad);
        gs.campLevel = Mathf.Max(1, data.campLevel);
        gs.lineageEnded = data.lineageEnded;
        gs.SetLeader(data.leader != null ? data.leader.CloneUnit() : new GobboUnitSaveData { isLeader = true, displayName = data.playerName });
        gs.ownedGobbos = CloneUnits(data.ownedGobbos);
        gs.activeSquadIds = data.activeSquadIds != null ? new List<string>(data.activeSquadIds) : new List<string>();
        gs.unlockedStations = data.unlockedStations != null ? new List<string>(data.unlockedStations) : new List<string>();
        gs.decorationsUnlocked = data.decorationsUnlocked != null ? new List<string>(data.decorationsUnlocked) : new List<string>();
        gs.campaignFacts = data.campaignFacts != null ? data.campaignFacts.Clone() : new CampaignFacts();
        gs.itemStacks = InventoryService.CloneStacks(data.itemStacks);
        gs.runSnackStacks = new List<InventoryStackSaveData>();
        gs.runSnackFinalizedThisRun = true;
        gs.lastRun = CloneRunSummary(data.lastRun);
        gs.markedSuccessorId = data.markedSuccessorId;
        gs.SetDeathHistory(data.deathHistory);
        gs.campTerrainState = data.campTerrainState != null ? data.campTerrainState.Clone() : new CampTerrainState();
        gs.persistentTestCaveTerrain = data.persistentTestCaveTerrain != null
            ? data.persistentTestCaveTerrain.Clone()
            : new PersistentTestCaveTerrainState();
        gs.storyProgress = data.storyProgress != null ? data.storyProgress.Clone() : new StoryProgressData();
        gs.RepairRosterState();
        {
            gs.EndRunSnapshot();
            RunReturnService.ResetForNewRun();
            PlayerDeathRunStore.Instance?.ClearPendingDeath();
            if (data.pendingCampArrivalMode >= 0) CampArrivalContext.RestorePending((CampArrivalMode)data.pendingCampArrivalMode, data.pendingCampArrivalId);
            else CampArrivalContext.Clear();
        }

        GameStateSaveBridge bridge = GameStateSaveBridge.GetOrCreate();
        if (bridge != null) bridge.SetCurrentSlotWithoutSaving(data.slotIndex, data.playerName, data.saveName, data.markedSuccessorId);

        CampSuccessorPreferenceStore successorStore = CampSuccessorPreferenceStore.GetOrCreate();
        if (successorStore != null)
        {
            if (string.IsNullOrWhiteSpace(data.markedSuccessorId)) successorStore.ClearSuccessor(false);
            else successorStore.SetMarkedSuccessor(data.markedSuccessorId, false);
            successorStore.ValidateAgainstRoster(false);
        }

        CampDeathHistoryStore deathStore = UnityEngine.Object.FindAnyObjectByType<CampDeathHistoryStore>(FindObjectsInactive.Include);
        if (deathStore != null && deathStore.deadBuddyHistory != null)
        {
            deathStore.deadBuddyHistory.Clear();
            if (gs.deathHistory != null) deathStore.deadBuddyHistory.AddRange(gs.deathHistory);
        }

        SetCurrentSlot(data.slotIndex);
        SetLastPlayedSlot(data.slotIndex);
        Debug.Log("[SporeSaveManager] Applied unified slot " + data.slotIndex + " to GameState. Leader: " + gs.leader.displayName + " / " + gs.leader.uniqueId + ", buddies: " + gs.ownedGobbos.Count);
        return true;
    }

    public static bool LoadSlotIntoGameState(int slotIndex)
    {
        SporeSaveSlotData data = LoadSlot(slotIndex);
        if (data == null || !data.hasSave) return false;
        if (!ApplySlotToGameState(data)) return false;
        return true;
    }

    public static bool ShouldLoadIntro(SporeSaveSlotData data)
    {
        return data != null && data.hasSave &&
               !StoryEventService.IsCompleted(data.storyProgress, StoryEventIds.ASporeHatchesCampDiscovered);
    }

    public static SporeSaveSlotData LoadMostRecentSlot()
    {
        SporeSaveSlotData best = null;
        for (int i = 1; i <= SlotCount; i++)
        {
            SporeSaveSlotData data = LoadSlot(i);
            if (data == null || !data.hasSave) continue;
            if (best == null || data.lastPlayedUtcTicks > best.lastPlayedUtcTicks) best = data;
        }
        return best;
    }

    public static SporeSaveSlotData LoadLastPlayedSlot() => LoadMostRecentSlot();
    public static bool HasAnySave() => LoadMostRecentSlot() != null;

    public static void DeleteSlot(int slotIndex)
    {
        slotIndex = ClampSlot(slotIndex);
        string path = GetSlotPath(slotIndex);
        string archive = SporeGobbo.Safety.Persistence.Isolated ? SporeGobbo.Safety.Persistence.ExperimentPath("RetiredCampaigns") : SporeGobbo.Safety.Persistence.Within(SaveFolder,"RetiredCampaigns");
        SporeGobbo.Safety.Persistence.NoLinks(archive); Directory.CreateDirectory(archive);
        foreach(string source in new[]{path,path+".bak"}) if(File.Exists(source))
        {
            CampaignSnapshotStorage.ValidatePath(source);
            string target=SporeGobbo.Safety.Persistence.Within(archive,Path.GetFileName(source)+"."+Guid.NewGuid().ToString("N"));
            File.Move(source,target);
        }
        if (GetLastPlayedSlot() == slotIndex) SporeGobbo.Safety.Persistence.DeleteKey(LastSlotKey);
        if (GetCurrentSlot() == slotIndex) SporeGobbo.Safety.Persistence.DeleteKey(CurrentSlotKey);
        SporeGobbo.Safety.Persistence.Save();
        Debug.Log("[SporeSaveManager] Deleted slot " + slotIndex);
    }

    public static int GetLastPlayedSlot() => SporeGobbo.Safety.Persistence.GetInt(LastSlotKey, 0);
    public static void SetLastPlayedSlot(int slotIndex) { SporeGobbo.Safety.Persistence.SetInt(LastSlotKey, ClampSlot(slotIndex)); SporeGobbo.Safety.Persistence.Save(); }
    public static int GetCurrentSlot() => SporeGobbo.Safety.Persistence.GetInt(CurrentSlotKey, 0);
    public static void SetCurrentSlot(int slotIndex) { SporeGobbo.Safety.Persistence.SetInt(CurrentSlotKey, ClampSlot(slotIndex)); SporeGobbo.Safety.Persistence.Save(); }

    public static void Normalize(SporeSaveSlotData data)
    {
        if (data == null) return;
        data.Normalize();
        data.itemStacks = InventoryService.NormalizeStacks(data.itemStacks, true);
        if (data.ownedGobbos != null)
        {
            foreach (GobboUnitSaveData unit in data.ownedGobbos)
            {
                if (unit == null) continue;
                unit.EnsureRuntimeDefaults();
            }
        }
    }

    private static int ClampSlot(int slotIndex) => Mathf.Clamp(slotIndex <= 0 ? 1 : slotIndex, 1, SlotCount);

    private static void EnsureGameState()
    {
        if (GameState.Instance != null) return;
        GameObject obj = new GameObject("GameState");
        obj.AddComponent<GameState>();
    }

    private static void SyncBridgeFromSlot(SporeSaveSlotData data)
    {
        GameStateSaveBridge bridge = GameStateSaveBridge.GetOrCreate();
        if (bridge != null) bridge.SetCurrentSlotWithoutSaving(data.slotIndex, data.playerName, data.saveName, data.markedSuccessorId);
    }

    private static RunSummaryData CloneRunSummary(RunSummaryData source)
    {
        RunSummaryData copy = new RunSummaryData();
        if (source == null) return copy;
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), copy);
        return copy;
    }

    private static List<GobboUnitSaveData> CloneUnits(List<GobboUnitSaveData> source)
    {
        List<GobboUnitSaveData> result = new List<GobboUnitSaveData>();
        if (source == null) return result;
        foreach (GobboUnitSaveData unit in source)
        {
            if (unit == null) continue;
            GobboUnitSaveData copy = unit.CloneUnit();
            copy.EnsureRuntimeDefaults();
            result.Add(copy);
        }
        return result;
    }
}
