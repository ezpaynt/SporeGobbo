#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public readonly struct FakeCompletedRunInput
{
    public readonly int ParticipantCount;
    public readonly int DeathCount;
    public readonly int NewBuddyCount;
    public FakeCompletedRunInput(int participants, int deaths, int newBuddies)
    {
        ParticipantCount = participants; DeathCount = deaths; NewBuddyCount = newBuddies;
    }
}

public sealed class FakeCompletedRunResult
{
    public readonly IReadOnlyList<string> ParticipantIds;
    public readonly IReadOnlyList<string> DeathIds;
    public readonly IReadOnlyList<string> NewBuddyIds;
    public FakeCompletedRunResult(List<string> participants, List<string> deaths, List<string> newBuddies)
    {
        ParticipantIds = participants; DeathIds = deaths; NewBuddyIds = newBuddies;
    }
}

public static class FakeCompletedRunBuilder
{
    public const string TestIdPrefix = "dev_campreturn_";
    static readonly Regex DisplaySequencePattern = new Regex(@"^Run Gobbo (\d+)$", RegexOptions.CultureInvariant);
    static readonly Regex IdSequencePattern = new Regex(@"^dev_campreturn_(\d+)$", RegexOptions.CultureInvariant);

    public static bool Validate(GameState state, FakeCompletedRunInput input, out string reason)
    {
        if (state == null) { reason = "GameState is unavailable."; return false; }
        if (input.ParticipantCount < 0 || input.DeathCount < 0 || input.NewBuddyCount < 0)
        { reason = "Counts cannot be negative."; return false; }
        if (input.DeathCount > input.ParticipantCount)
        { reason = "Existing Buddy deaths cannot exceed participants."; return false; }
        int available = GetLivingExistingBuddies(state).Count;
        if (input.ParticipantCount > available)
        { reason = "Participants cannot exceed the " + available + " living existing Buddies."; return false; }
        reason = "Ready."; return true;
    }

    public static List<GobboUnitSaveData> SelectParticipants(GameState state, int count) =>
        GetLivingExistingBuddies(state).Take(Mathf.Max(0, count)).ToList();

    public static List<GobboUnitSaveData> SelectDeaths(IReadOnlyList<GobboUnitSaveData> participants, int count) =>
        participants == null ? new List<GobboUnitSaveData>() : participants.Take(Mathf.Max(0, count)).ToList();

    public static FakeCompletedRunResult Apply(GameState state, FakeCompletedRunInput input)
    {
        if (!Validate(state, input, out string reason)) throw new InvalidOperationException(reason);
        state.EnsureRuntimeDefaults();
        GobboUnitSaveData leaderBefore = state.GetLeader().CloneUnit();
        List<GobboUnitSaveData> rosterBefore = state.ownedGobbos.Where(unit => unit != null)
            .Select(unit => unit.CloneUnit()).ToList();
        List<string> rosterIdsBefore = rosterBefore.Select(unit => unit.uniqueId).ToList();
        List<GobboUnitSaveData> participants = SelectParticipants(state, input.ParticipantCount);
        List<GobboUnitSaveData> deaths = SelectDeaths(participants, input.DeathCount);
        List<string> participantIds = participants.Select(unit => unit.uniqueId).ToList();
        List<string> deathIds = deaths.Select(unit => unit.uniqueId).ToList();
        List<string> deadNames = new List<string>();

        state.lastRun = CreateEmptySummary(state, leaderBefore, rosterBefore.Count);
        foreach (GobboUnitSaveData dead in deaths)
        {
            dead.causeOfDeath = "Lost during fake completed run.";
            deadNames.Add(dead.displayName + " the " + dead.gobboType);
            state.RegisterGobboDeath(dead);
            state.RemoveGobbo(dead.uniqueId);
        }

        List<string> newBuddyIds = new List<string>();
        for (int i = 0; i < input.NewBuddyCount; i++)
        {
            GobboUnitSaveData buddy = CreateNewBuddy(state, i + 1);
            state.AddGobbo(buddy, false);
            GobboUnitSaveData stored = state.FindOwnedGobbo(buddy.uniqueId);
            state.RegisterGobboFound(stored);
            newBuddyIds.Add(stored.uniqueId);
        }

        foreach (GobboUnitSaveData survivor in participants.Where(unit => !deathIds.Contains(unit.uniqueId)))
        {
            GobboUnitSaveData stored = state.FindOwnedGobbo(survivor.uniqueId);
            if (stored != null) stored.survivedLastRun = true;
        }

        RunSummaryService.BuildRunSummary(state, leaderBefore, rosterIdsBefore, rosterBefore,
            participantIds, deadNames, 0f, true);
        state.currentRunNumber = Mathf.Max(1, state.currentRunNumber + 1);
        state.leader.runsSurvived++;
        return new FakeCompletedRunResult(participantIds, deathIds, newBuddyIds);
    }

    public static GobboUnitSaveData CreateNewBuddy(GameState state, int ordinal)
    {
        int sequence = GetNextTestSequence(state);
        string id = TestIdPrefix + sequence.ToString("D6");
        var buddy = new GobboUnitSaveData
        {
            uniqueId = id, displayName = "Run Gobbo " + sequence.ToString("D2"),
            isLeader = false, isDead = false, gobboType = BuddyType.Baby, ageStage = GobboAgeStage.Baby,
            health = 10, maxHealth = 10, isInActiveSquad = false, campResidentialSlotId = 0,
            survivedLastRun = true
        };
        buddy.EnsureRuntimeDefaults();
        return buddy;
    }

    public static int GetNextTestSequence(GameState state)
    {
        int maximum = 0;
        if (state?.ownedGobbos != null)
            foreach (GobboUnitSaveData buddy in state.ownedGobbos)
                maximum = Mathf.Max(maximum, GetIssuedSequence(buddy?.uniqueId, buddy?.displayName));
        if (state?.deathHistory != null)
            foreach (DeadBuddyRecord dead in state.deathHistory)
                maximum = Mathf.Max(maximum, GetIssuedSequence(dead?.gobboId, dead?.displayName));
        return maximum + 1;
    }

    static int GetIssuedSequence(string id, string displayName)
    {
        Match idMatch = IdSequencePattern.Match(id ?? "");
        if (idMatch.Success && int.TryParse(idMatch.Groups[1].Value, out int idSequence)) return idSequence;
        Match nameMatch = DisplaySequencePattern.Match(displayName ?? "");
        return nameMatch.Success && int.TryParse(nameMatch.Groups[1].Value, out int nameSequence)
            ? nameSequence : 0;
    }

    static List<GobboUnitSaveData> GetLivingExistingBuddies(GameState state)
    {
        if (state == null) return new List<GobboUnitSaveData>();
        return (state.ownedGobbos ?? new List<GobboUnitSaveData>()).Where(CampResidentialOccupancyResolver.IsLivingBuddy)
            .OrderBy(unit => unit.uniqueId, StringComparer.Ordinal).ToList();
    }

    static RunSummaryData CreateEmptySummary(GameState state, GobboUnitSaveData leader, int buddyCount) =>
        new RunSummaryData
        {
            survived = true, runNumber = Mathf.Max(1, state.currentRunNumber),
            playerLevelStart = leader.level, playerLevelEnd = leader.level,
            xpStart = leader.xp, xpEnd = leader.xp, sporesStart = leader.spores, sporesEnd = leader.spores,
            mushroomsStart = leader.mushrooms, mushroomsEnd = leader.mushrooms,
            moneyStart = leader.money, moneyEnd = leader.money, shiniesStart = leader.shinies,
            shiniesEnd = leader.shinies, buddiesStart = buddyCount
        };
}

#endif
