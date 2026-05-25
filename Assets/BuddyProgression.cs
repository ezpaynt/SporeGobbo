using System.Collections.Generic;
using UnityEngine;

public static class BuddyProgression
{
    public const float ActiveFoodShare = 0.70f;
    public const float ReserveFoodShare = 0.30f;

    // Test mode: buddies need only 1 XP to hit their first growth choice.
    // Later, raise baby XP to 10+ and tune the curve.
    public const int TestBabyXPToNextLevel = 1;

    public static readonly int[] EvolutionLevels = { 2, 6, 12, 24, 48 };

    public static void PrepareNewBaby(BuddyData buddy)
    {
        if (buddy == null)
            return;

        buddy.EnsureId();
        buddy.EnsureRuntimeDefaults();
        buddy.buddyType = BuddyType.Baby;
        buddy.ageStage = GobboAgeStage.Baby;
        buddy.level = 1;
        buddy.xp = 0;
        buddy.xpToNextLevel = TestBabyXPToNextLevel;
        buddy.pendingEvolution = false;
        buddy.evolutionLevelWaiting = 0;
        buddy.runsWaitingForEvolution = 0;
        buddy.neglectedElder = false;
        buddy.happiness = 100;
        buddy.loyalty = 100;
        buddy.visualSetId = "baby";
    }

    public static void AddXP(BuddyData buddy, int amount)
    {
        if (buddy == null || amount <= 0)
            return;

        buddy.EnsureId();
        buddy.EnsureRuntimeDefaults();

        if (buddy.xpToNextLevel <= 0)
            buddy.xpToNextLevel = TestBabyXPToNextLevel;

        buddy.xp += amount;

        while (buddy.xp >= buddy.xpToNextLevel)
        {
            buddy.xp -= buddy.xpToNextLevel;
            buddy.level++;

            // Keep the very first test level quick. After that, climb normally.
            buddy.xpToNextLevel = Mathf.Max(2, Mathf.RoundToInt(buddy.xpToNextLevel * 1.35f) + 1);

            buddy.maxHealth += 2;
            // Do not full-heal here. The survivor screen should show how hurt they came back.
            buddy.health = Mathf.Clamp(buddy.health, 1, buddy.maxHealth);

            if (buddy.level % 3 == 0)
                buddy.damage += 1;

            if (IsEvolutionLevel(buddy.level))
                MarkPendingEvolution(buddy, buddy.level);
        }
    }

    public static void DistributeEndRunFoodXP(GameState state, int foodXP)
    {
        if (state == null || foodXP <= 0)
            return;

        List<BuddyData> active = state.GetActiveSquad();
        List<BuddyData> reserve = state.GetReserveBuddies();

        int activePool = Mathf.RoundToInt(foodXP * ActiveFoodShare);
        int reservePool = Mathf.Max(0, foodXP - activePool);

        int activeEach = active.Count > 0 ? Mathf.Max(1, activePool / active.Count) : 0;
        int reserveEach = reserve.Count > 0 ? Mathf.Max(1, reservePool / reserve.Count) : 0;

        foreach (BuddyData buddy in active)
        {
            AddXP(buddy, activeEach);
            EndRunCareTick(buddy, true);
        }

        foreach (BuddyData buddy in reserve)
        {
            AddXP(buddy, reserveEach);
            EndRunCareTick(buddy, false);
        }
    }

    static void EndRunCareTick(BuddyData buddy, bool wasActive)
    {
        if (buddy == null)
            return;

        buddy.EnsureRuntimeDefaults();
        // Do not full-heal before the survivor screen. CampSceneController heals
        // everyone only after the player accepts the survivor report and enters camp.
        buddy.health = Mathf.Clamp(buddy.health, 1, buddy.maxHealth);
        buddy.hasBeenHit = false;
        buddy.survivedLastRun = true;

        // We currently block continuing until camp evolutions are handled,
        // so this is mostly future-proofing for the later "ignore them" version.
        if (buddy.pendingEvolution)
        {
            buddy.runsWaitingForEvolution++;

            if (buddy.runsWaitingForEvolution >= 5)
                ForceNeglectedElder(buddy);
            else if (buddy.runsWaitingForEvolution >= 3 && buddy.happiness < 50)
                ForceNeglectedElder(buddy);
        }
    }

    public static bool IsEvolutionLevel(int level)
    {
        foreach (int milestone in EvolutionLevels)
        {
            if (level == milestone)
                return true;
        }

        return false;
    }

    public static void MarkPendingEvolution(BuddyData buddy, int level)
    {
        if (buddy == null)
            return;

        buddy.pendingEvolution = true;
        buddy.evolutionLevelWaiting = level;
        buddy.runsWaitingForEvolution = 0;
    }

    public static GobboAgeStage GetStageForEvolutionLevel(int level)
    {
        if (level <= 1) return GobboAgeStage.Baby;
        if (level == 2) return GobboAgeStage.Young;
        if (level == 6) return GobboAgeStage.Stage1;
        if (level == 12) return GobboAgeStage.Stage2;
        if (level == 24) return GobboAgeStage.Stage3;
        if (level >= 48) return GobboAgeStage.Stage4;
        return GobboAgeStage.Young;
    }

    public static void ApplyEvolutionChoice(BuddyData buddy, BuddyType chosenType, BuddyRoster roster)
    {
        if (buddy == null)
            return;

        buddy.EnsureId();
        buddy.EnsureRuntimeDefaults();

        // First evolution: Baby becomes one of the real gobbo classes.
        if (buddy.buddyType == BuddyType.Baby)
            buddy.buddyType = chosenType;

        // Later evolution milestones keep their existing class unless explicitly changed.
        if (buddy.buddyType == BuddyType.Baby && chosenType != BuddyType.Baby)
            buddy.buddyType = chosenType;

        buddy.ageStage = GetStageForEvolutionLevel(Mathf.Max(2, buddy.evolutionLevelWaiting));
        buddy.pendingEvolution = false;
        buddy.runsWaitingForEvolution = 0;
        buddy.evolutionLevelWaiting = 0;
        buddy.neglectedElder = false;

        int healthBeforeEvolution = buddy.health;

        if (roster != null)
        {
            roster.ApplySetupToBuddy(buddy, roster.GetSetup(buddy.buddyType));
            buddy.health = Mathf.Clamp(healthBeforeEvolution, 1, buddy.maxHealth);
        }
        else
        {
            buddy.visualSetId = buddy.buddyType.ToString().ToLowerInvariant() + "_" + buddy.ageStage.ToString().ToLowerInvariant();
            buddy.health = Mathf.Clamp(healthBeforeEvolution, 1, buddy.maxHealth);
        }

        if (buddy.chosenCardIds != null && !buddy.chosenCardIds.Contains("evolve_" + chosenType.ToString().ToLowerInvariant()))
            buddy.chosenCardIds.Add("evolve_" + chosenType.ToString().ToLowerInvariant());
    }

    public static void ForceNeglectedElder(BuddyData buddy)
    {
        if (buddy == null)
            return;

        buddy.EnsureRuntimeDefaults();
        buddy.ageStage = GobboAgeStage.NeglectedElder;
        buddy.neglectedElder = true;
        buddy.pendingEvolution = false;
        buddy.runsWaitingForEvolution = 0;
        buddy.evolutionLevelWaiting = 0;
        buddy.visualSetId = buddy.buddyType.ToString().ToLowerInvariant() + "_neglectedelder";

        buddy.maxHealth += 5;
        buddy.health = buddy.maxHealth;
        buddy.damage += 1;
        buddy.moveSpeed = Mathf.Max(1f, buddy.moveSpeed - 0.25f);
    }
}
