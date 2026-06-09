using System.Collections.Generic;
using UnityEngine;

public static class RunSummaryService
{
    public static void BuildRunSummary(
        GameState state,
        GobboUnitSaveData before,
        List<string> beforeIds,
        List<GobboUnitSaveData> beforeGobbos,
        List<string> runStartActiveBuddyIds,
        List<string> deadBuddyNamesThisRun,
        float runStartTime,
        bool survived)
    {
        if (state == null || before == null) return;

        EnsureLastRun(state);
        RunSummaryData lastRun = state.lastRun;

        int trackedXpGained = lastRun.xpGained;
        int trackedSporesGained = lastRun.sporesGained;
        int trackedMushroomsGained = lastRun.mushroomsGained;
        int trackedMoneyGained = lastRun.moneyGained;
        int trackedFoodValueGained = lastRun.foodValueGained;
        int trackedShiniesGained = lastRun.shiniesGained;
        int trackedEnemiesKilled = lastRun.enemiesKilled;
        List<string> trackedUpgrades = new List<string>(lastRun.upgradesChosen);
        List<string> trackedNewBuddyNames = lastRun.newBuddyNames != null ? new List<string>(lastRun.newBuddyNames) : new List<string>();

        GobboUnitSaveData leader = state.leader;
        beforeIds ??= new List<string>();
        beforeGobbos ??= new List<GobboUnitSaveData>();
        deadBuddyNamesThisRun ??= new List<string>();

        lastRun.survived = survived;
        lastRun.timeSpent = runStartTime > 0f ? Time.time - runStartTime : 0f;
        lastRun.runNumber = state.currentRunNumber;
        lastRun.playerLevelStart = before.level;
        lastRun.playerLevelEnd = leader.level;
        lastRun.xpStart = before.xp;
        lastRun.xpEnd = leader.xp;
        lastRun.xpGained = trackedXpGained > 0 ? trackedXpGained : Mathf.Max(0, leader.xp - before.xp);
        lastRun.sporesStart = before.spores;
        lastRun.sporesEnd = leader.spores;
        lastRun.sporesGained = trackedSporesGained > 0 ? trackedSporesGained : Mathf.Max(0, leader.spores - before.spores);
        lastRun.mushroomsStart = before.mushrooms;
        lastRun.mushroomsEnd = leader.mushrooms;
        lastRun.mushroomsGained = trackedMushroomsGained > 0 ? trackedMushroomsGained : Mathf.Max(0, leader.mushrooms - before.mushrooms);
        lastRun.moneyStart = before.money;
        lastRun.moneyEnd = leader.money;
        lastRun.moneyGained = trackedMoneyGained > 0 ? trackedMoneyGained : Mathf.Max(0, leader.money - before.money);
        lastRun.foodValueGained = trackedFoodValueGained;
        lastRun.shiniesStart = before.shinies;
        lastRun.shiniesEnd = leader.shinies;
        lastRun.shiniesGained = trackedShiniesGained > 0 ? trackedShiniesGained : Mathf.Max(0, leader.shinies - before.shinies);
        lastRun.enemiesKilled = trackedEnemiesKilled;
        lastRun.upgradesChosen = trackedUpgrades;
        lastRun.buddiesStart = beforeIds.Count;
        lastRun.buddiesEnd = state.ownedGobbos != null ? state.ownedGobbos.Count : 0;

        lastRun.newBuddyNames.Clear();
        lastRun.deadBuddyNames.Clear();
        lastRun.activeBuddyReports.Clear();
        lastRun.reserveBuddyReports.Clear();
        lastRun.leveledBuddyNames.Clear();

        foreach (string trackedName in trackedNewBuddyNames)
            if (!string.IsNullOrWhiteSpace(trackedName) && !lastRun.newBuddyNames.Contains(trackedName))
                lastRun.newBuddyNames.Add(trackedName);

        if (state.ownedGobbos != null)
        {
            foreach (GobboUnitSaveData unit in state.ownedGobbos)
            {
                if (unit == null) continue;
                unit.EnsureRuntimeDefaults();
                if (!beforeIds.Contains(unit.uniqueId))
                {
                    string label = GetGobboLabel(unit);
                    if (!lastRun.newBuddyNames.Contains(label)) lastRun.newBuddyNames.Add(label);
                }
            }
        }

        foreach (string deadName in deadBuddyNamesThisRun)
            if (!lastRun.deadBuddyNames.Contains(deadName)) lastRun.deadBuddyNames.Add(deadName);

        foreach (GobboUnitSaveData oldUnit in beforeGobbos)
        {
            if (oldUnit == null) continue;
            bool stillOwned = FindOwnedGobboRaw(state, oldUnit.uniqueId) != null;
            if (!stillOwned)
            {
                string label = GetGobboLabel(oldUnit);
                if (!lastRun.deadBuddyNames.Contains(label)) lastRun.deadBuddyNames.Add(label);
            }
        }

        BuildBuddyRunReports(state, beforeGobbos, runStartActiveBuddyIds);

        lastRun.buddiesFound = lastRun.newBuddyNames.Count;
        lastRun.buddiesLost = lastRun.deadBuddyNames.Count;
    }

    static void BuildBuddyRunReports(GameState state, List<GobboUnitSaveData> beforeGobbos, List<string> runStartActiveBuddyIds)
    {
        EnsureLastRun(state);
        RunSummaryData lastRun = state.lastRun;

        List<string> activeRunIds = new List<string>();
        if (runStartActiveBuddyIds != null)
        {
            foreach (string id in runStartActiveBuddyIds)
                if (!string.IsNullOrWhiteSpace(id) && !activeRunIds.Contains(id)) activeRunIds.Add(id);
        }
        if (state.activeSquadIds != null)
        {
            foreach (string id in state.activeSquadIds)
                if (!string.IsNullOrWhiteSpace(id) && !activeRunIds.Contains(id)) activeRunIds.Add(id);
        }

        foreach (string id in activeRunIds)
        {
            BuddyRunReport report = MakeBuddyRunReport(state, id, true, beforeGobbos);
            if (report != null) lastRun.activeBuddyReports.Add(report);
        }

        if (state.ownedGobbos != null)
        {
            foreach (GobboUnitSaveData unit in state.ownedGobbos)
            {
                if (unit == null) continue;
                unit.EnsureRuntimeDefaults();
                if (activeRunIds.Contains(unit.uniqueId)) continue;

                BuddyRunReport report = MakeBuddyRunReport(state, unit.uniqueId, false, beforeGobbos);
                if (report != null) lastRun.reserveBuddyReports.Add(report);
            }
        }

        foreach (BuddyRunReport report in lastRun.activeBuddyReports)
            RegisterLeveledBuddyReport(lastRun, report);
        foreach (BuddyRunReport report in lastRun.reserveBuddyReports)
            RegisterLeveledBuddyReport(lastRun, report);
    }

    static BuddyRunReport MakeBuddyRunReport(GameState state, string buddyId, bool wasActive, List<GobboUnitSaveData> beforeGobbos)
    {
        if (string.IsNullOrWhiteSpace(buddyId)) return null;

        GobboUnitSaveData beforeUnit = FindSnapshotGobbo(beforeGobbos, buddyId);
        GobboUnitSaveData afterUnit = FindOwnedGobboRaw(state, buddyId);
        GobboUnitSaveData source = afterUnit != null ? afterUnit : beforeUnit;
        if (source == null) return null;

        source.EnsureRuntimeDefaults();
        if (beforeUnit != null) beforeUnit.EnsureRuntimeDefaults();
        if (afterUnit != null) afterUnit.EnsureRuntimeDefaults();

        BuddyRunReport report = new BuddyRunReport();
        report.buddyId = buddyId;
        report.displayName = GetGobboLabel(source);
        report.role = wasActive ? "Run Squad" : "Camp";
        report.wasActive = wasActive;
        report.died = afterUnit == null || source.isDead;
        report.survived = !report.died;

        report.levelStart = beforeUnit != null ? beforeUnit.level : 1;
        report.levelEnd = afterUnit != null ? afterUnit.level : source.level;
        report.xpStart = beforeUnit != null ? beforeUnit.xp : 0;
        report.xpEnd = afterUnit != null ? afterUnit.xp : source.xp;
        report.xpGained = CalculateXpDelta(beforeUnit, afterUnit);

        report.killsStart = beforeUnit != null ? beforeUnit.kills : 0;
        report.killsEnd = afterUnit != null ? afterUnit.kills : source.kills;
        report.killsGained = Mathf.Max(0, report.killsEnd - report.killsStart);

        report.nightsStart = beforeUnit != null ? beforeUnit.runsSurvived : 0;
        report.nightsEnd = afterUnit != null ? afterUnit.runsSurvived : source.runsSurvived;
        report.nightsGained = Mathf.Max(0, report.nightsEnd - report.nightsStart);

        report.happinessStart = beforeUnit != null ? beforeUnit.happiness : source.happiness;
        report.happinessEnd = afterUnit != null ? afterUnit.happiness : source.happiness;
        report.leveledUp = report.levelEnd > report.levelStart;
        report.readyToGrow = afterUnit != null && afterUnit.pendingEvolution;
        report.traitLabel = GetPrimaryTraitLabel(source);
        return report;
    }

    static int CalculateXpDelta(GobboUnitSaveData beforeUnit, GobboUnitSaveData afterUnit)
    {
        if (afterUnit == null) return 0;
        if (beforeUnit == null) return Mathf.Max(0, afterUnit.xp);
        if (afterUnit.level == beforeUnit.level) return Mathf.Max(0, afterUnit.xp - beforeUnit.xp);

        // XP can wrap when leveling up. This gives a readable "at least this much" number
        // for the roll-call without needing a full XP transaction log yet.
        int rough = Mathf.Max(0, beforeUnit.xpToNextLevel - beforeUnit.xp) + Mathf.Max(0, afterUnit.xp);
        return Mathf.Max(rough, afterUnit.level - beforeUnit.level);
    }

    static GobboUnitSaveData FindSnapshotGobbo(List<GobboUnitSaveData> snapshots, string buddyId)
    {
        if (snapshots == null || string.IsNullOrWhiteSpace(buddyId)) return null;
        foreach (GobboUnitSaveData unit in snapshots)
            if (unit != null && unit.uniqueId == buddyId) return unit;
        return null;
    }

    static GobboUnitSaveData FindOwnedGobboRaw(GameState state, string gobboId)
    {
        if (state == null || string.IsNullOrWhiteSpace(gobboId) || state.ownedGobbos == null) return null;
        foreach (GobboUnitSaveData unit in state.ownedGobbos)
        {
            if (unit == null) continue;
            unit.EnsureRuntimeDefaults();
            if (unit.uniqueId == gobboId) return unit;
        }
        return null;
    }

    static void RegisterLeveledBuddyReport(RunSummaryData lastRun, BuddyRunReport report)
    {
        if (lastRun == null || report == null || !report.leveledUp) return;
        string label = report.displayName + " Lv " + report.levelStart + "  " + report.levelEnd;
        if (!lastRun.leveledBuddyNames.Contains(label)) lastRun.leveledBuddyNames.Add(label);
    }

    static string GetPrimaryTraitLabel(GobboUnitSaveData unit)
    {
        if (unit == null || unit.traitIds == null || unit.traitIds.Count == 0) return "None";
        string trait = unit.traitIds[0];
        if (string.IsNullOrWhiteSpace(trait)) return "None";
        return trait;
    }

    static string GetGobboLabel(GobboUnitSaveData unit)
    {
        if (unit == null) return "Unknown Gobbo";
        unit.EnsureRuntimeDefaults();
        return unit.displayName + " the " + unit.gobboType;
    }

    static void EnsureLastRun(GameState state)
    {
        if (state.lastRun == null) state.lastRun = new RunSummaryData();
        if (state.lastRun.runNumber <= 0) state.lastRun.runNumber = state.currentRunNumber;
        state.lastRun.newBuddyNames ??= new List<string>();
        state.lastRun.deadBuddyNames ??= new List<string>();
        state.lastRun.upgradesChosen ??= new List<string>();
        state.lastRun.activeBuddyReports ??= new List<BuddyRunReport>();
        state.lastRun.reserveBuddyReports ??= new List<BuddyRunReport>();
        state.lastRun.leveledBuddyNames ??= new List<string>();
    }
}
