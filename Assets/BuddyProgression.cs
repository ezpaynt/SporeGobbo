using System.Collections.Generic;
using UnityEngine;

public static class BuddyProgression
{
    public const float ActiveFoodShare = 0.70f;
    public const float ReserveFoodShare = 0.30f;

    public const int BuddyBaseXPToNextLevel = 8;
    public const float BuddyXPGrowthRate = 1.35f;
    public const bool UseFastBuddyXPForTesting = false;
    public const float BuddyTestingXPCostDivisor = 4f;

    public static readonly int[] EvolutionLevels = { 2, 6, 12, 24, 48 };

    public static void PrepareNewBaby(GobboUnitSaveData unit)
    {
        if (unit == null) return;
        unit.EnsureId();
        unit.EnsureRuntimeDefaults();
        unit.isLeader = false;
        unit.gobboType = BuddyType.Baby;
        unit.ageStage = GobboAgeStage.Baby;
        unit.level = 1;
        unit.xp = 0;
        unit.xpToNextLevel = GetXPToNextLevel(unit.level);
        unit.pendingGrowthChoiceType = BuddyGrowthChoiceType.None;
        unit.pendingGrowthLevelWaiting = 0;
        unit.pendingEvolution = false;
        unit.evolutionLevelWaiting = 0;
        unit.runsWaitingForEvolution = 0;
        unit.happiness = 100;
        unit.loyalty = 100;
        unit.visualSetId = "baby";
    }

    public static void AddXP(GobboUnitSaveData unit, int amount)
    {
        if (unit == null || amount <= 0) return;
        unit.EnsureId();
        unit.EnsureRuntimeDefaults();
        EnsureBuddyXPThreshold(unit);

        unit.xp += amount;
        while (unit.xp >= unit.xpToNextLevel)
        {
            unit.xp -= unit.xpToNextLevel;
            unit.level++;
            unit.xpToNextLevel = GetXPToNextLevel(unit.level);
            unit.maxHealth += 2;
            unit.health = Mathf.Clamp(unit.health, 1, unit.maxHealth);
            if (unit.level % 3 == 0) unit.damage += 1;
            unit.attack = Mathf.Max(unit.attack, unit.damage);
            MarkPendingGrowthForLevel(unit, unit.level);
        }
    }

    public static int GetXPToNextLevel(int currentLevel)
    {
        int safeLevel = Mathf.Max(1, currentLevel);
        float scaled = BuddyBaseXPToNextLevel * Mathf.Pow(BuddyXPGrowthRate, safeLevel - 1);

        if (UseFastBuddyXPForTesting)
            scaled /= Mathf.Max(1f, BuddyTestingXPCostDivisor);

        return Mathf.Max(1, Mathf.RoundToInt(scaled));
    }

    static void EnsureBuddyXPThreshold(GobboUnitSaveData unit)
    {
        if (unit == null) return;

        int expected = GetXPToNextLevel(unit.level);
        if (unit.xpToNextLevel <= 1 || unit.xpToNextLevel < expected)
            unit.xpToNextLevel = expected;
    }

    public static void DistributeEndRunFoodXP(GameState state, int foodXP)
    {
        if (state == null || foodXP <= 0) return;

        List<GobboUnitSaveData> active = state.GetActiveSquadUnits();
        List<GobboUnitSaveData> reserve = state.GetReserveGobboUnits();

        int activePool = Mathf.RoundToInt(foodXP * ActiveFoodShare);
        int reservePool = Mathf.Max(0, foodXP - activePool);
        int activeEach = active.Count > 0 ? Mathf.Max(1, activePool / active.Count) : 0;
        int reserveEach = reserve.Count > 0 ? Mathf.Max(1, reservePool / reserve.Count) : 0;

        foreach (GobboUnitSaveData unit in active)
        {
            AddXP(unit, activeEach);
            EndRunCareTick(unit, true);
        }

        foreach (GobboUnitSaveData unit in reserve)
        {
            AddXP(unit, reserveEach);
            EndRunCareTick(unit, false);
        }
    }

    static void EndRunCareTick(GobboUnitSaveData unit, bool wasActive)
    {
        if (unit == null) return;
        unit.EnsureRuntimeDefaults();
        unit.health = Mathf.Clamp(unit.health, 1, unit.maxHealth);
        unit.hasBeenHit = false;
        unit.survivedLastRun = true;

        if (BuddyGrowthService.HasPendingGrowth(unit))
        {
            unit.runsWaitingForEvolution++;
            if (unit.runsWaitingForEvolution >= 5) ForceNeglectedElder(unit);
            else if (unit.runsWaitingForEvolution >= 3 && unit.happiness < 50) ForceNeglectedElder(unit);
        }
    }

    public static bool IsEvolutionLevel(int level)
    {
        foreach (int milestone in EvolutionLevels)
            if (level == milestone) return true;
        return false;
    }

    static void MarkPendingGrowthForLevel(GobboUnitSaveData unit, int level)
    {
        if (unit == null) return;
        unit.EnsureRuntimeDefaults();
        if (BuddyGrowthService.HasPendingGrowth(unit)) return;

        if (IsEvolutionLevel(level))
            MarkPendingEvolution(unit, level);
        else
            MarkPendingStatCard(unit, level);
    }

    public static void MarkPendingEvolution(GobboUnitSaveData unit, int level)
    {
        if (unit == null) return;
        unit.pendingGrowthChoiceType = BuddyGrowthChoiceType.Evolution;
        unit.pendingGrowthLevelWaiting = level;
        unit.pendingEvolution = true;
        unit.evolutionLevelWaiting = level;
        unit.runsWaitingForEvolution = 0;
    }

    public static void MarkPendingStatCard(GobboUnitSaveData unit, int level)
    {
        if (unit == null) return;
        unit.EnsureRuntimeDefaults();
        if (BuddyGrowthService.HasPendingGrowth(unit)) return;

        unit.pendingGrowthChoiceType = BuddyGrowthChoiceType.StatCard;
        unit.pendingGrowthLevelWaiting = level;
        unit.runsWaitingForEvolution = 0;
    }

    public static List<GobboCard> GetStatCardChoices(GobboUnitSaveData unit, int count, List<string> excludeIds = null)
    {
        List<GobboCard> choices = new List<GobboCard>();
        if (unit == null || count <= 0) return choices;

        unit.EnsureRuntimeDefaults();
        if (GobboCardDatabase.Instance != null)
            choices = GobboCardDatabase.Instance.GetChoicesForBuddy(unit, GobboCardContext.BuddyStatGrowth, count, excludeIds);

        if (choices.Count > 0) return choices;
        return GetFallbackStatCardChoices(unit, count, excludeIds);
    }

    public static bool ApplyStatCardChoice(GobboUnitSaveData unit, GobboCard card)
    {
        if (unit == null || card == null) return false;
        unit.EnsureId();
        unit.EnsureRuntimeDefaults();

        if (card.isEvolutionCard || card.isTypeChoiceCard)
        {
            Debug.LogWarning("Tried to apply an evolution card as buddy stat growth: " + card.cardName);
            return false;
        }

        card.ApplyToBuddy(unit);

        unit.chosenCardIds ??= new List<string>();
        if (!string.IsNullOrWhiteSpace(card.cardId) && !unit.chosenCardIds.Contains(card.cardId))
            unit.chosenCardIds.Add(card.cardId);

        if (unit.pendingGrowthChoiceType == BuddyGrowthChoiceType.StatCard)
        {
            unit.pendingGrowthChoiceType = BuddyGrowthChoiceType.None;
            unit.pendingGrowthLevelWaiting = 0;
        }

        unit.EnsureRuntimeDefaults();
        return true;
    }

    static List<GobboCard> GetFallbackStatCardChoices(GobboUnitSaveData unit, int count, List<string> excludeIds)
    {
        List<GobboCard> pool = new List<GobboCard>
        {
            new GobboCard { cardId = "buddy_growth_max_health", cardName = "Tougher Hide", description = "+8 Max Health.", playerAllowed = false, buddyAllowed = true, maxHealthBonus = 8 },
            new GobboCard { cardId = "buddy_growth_attack", cardName = "Sharper Bite", description = "+2 Attack.", playerAllowed = false, buddyAllowed = true, attackBonus = 2 },
            new GobboCard { cardId = "buddy_growth_defense", cardName = "Hard Knuckles", description = "+1 Defense.", playerAllowed = false, buddyAllowed = true, defenseBonus = 1 },
            new GobboCard { cardId = "buddy_growth_speed", cardName = "Quicker Feet", description = "+0.35 Move Speed.", playerAllowed = false, buddyAllowed = true, moveSpeedBonus = 0.35f },
            new GobboCard { cardId = "buddy_growth_attack_speed", cardName = "Snappier Swings", description = "Faster attacks.", playerAllowed = false, buddyAllowed = true, attackCooldownBonus = -0.08f }
        };

        for (int i = pool.Count - 1; i >= 0; i--)
        {
            GobboCard card = pool[i];
            if (excludeIds != null && excludeIds.Contains(card.cardId))
            {
                pool.RemoveAt(i);
                continue;
            }

            if (!card.CanAppearForBuddy(unit, GobboCardContext.BuddyStatGrowth))
                pool.RemoveAt(i);
        }

        List<GobboCard> result = new List<GobboCard>();
        while (result.Count < count && pool.Count > 0)
        {
            int index = Random.Range(0, pool.Count);
            result.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return result;
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

    public static void ApplyEvolutionChoice(GobboUnitSaveData unit, BuddyType chosenType, BuddyRoster roster)
    {
        if (unit == null) return;
        unit.EnsureId();
        unit.EnsureRuntimeDefaults();

        if (unit.gobboType == BuddyType.Baby && chosenType != BuddyType.Baby)
            unit.gobboType = chosenType;

        unit.ageStage = GetStageForEvolutionLevel(Mathf.Max(2, unit.evolutionLevelWaiting));
        ClearPendingGrowth(unit);

        int healthBeforeEvolution = unit.health;
        BuddyTypeSetup setup = roster != null ? roster.GetSetup(unit.gobboType) : null;
        if (setup != null)
        {
            unit.maxHealth = setup.maxHealth;
            unit.damage = setup.damage;
            unit.attack = setup.damage;
            unit.defense = setup.defense;
            unit.moveSpeed = setup.moveSpeed;
            unit.attackCooldown = setup.attackCooldown;
            unit.bodyColor = setup.bodyColor;
            unit.onlyFightsAfterHit = setup.onlyFightsAfterHit;
            unit.collectsFood = setup.collectsFood;
            unit.visualSetId = !string.IsNullOrWhiteSpace(setup.defaultVisualSetId)
                ? setup.defaultVisualSetId
                : unit.gobboType.ToString().ToLowerInvariant() + "_" + unit.ageStage.ToString().ToLowerInvariant();
        }
        else
        {
            unit.visualSetId = unit.gobboType.ToString().ToLowerInvariant() + "_" + unit.ageStage.ToString().ToLowerInvariant();
        }

        unit.health = Mathf.Clamp(healthBeforeEvolution, 1, unit.maxHealth);
        string cardId = "evolve_" + chosenType.ToString().ToLowerInvariant();
        unit.chosenCardIds ??= new List<string>();
        if (!unit.chosenCardIds.Contains(cardId)) unit.chosenCardIds.Add(cardId);
        unit.evolutionHistoryIds ??= new List<string>();
        if (!unit.evolutionHistoryIds.Contains(cardId)) unit.evolutionHistoryIds.Add(cardId);
        unit.EnsureRuntimeDefaults();
    }

    public static void ForceNeglectedElder(GobboUnitSaveData unit)
    {
        if (unit == null) return;
        unit.EnsureRuntimeDefaults();
        unit.ageStage = GobboAgeStage.NeglectedElder;
        ClearPendingGrowth(unit);
        unit.visualSetId = unit.gobboType.ToString().ToLowerInvariant() + "_neglectedelder";
        unit.maxHealth += 5;
        unit.health = unit.maxHealth;
        unit.damage += 1;
        unit.attack = Mathf.Max(unit.attack, unit.damage);
        unit.moveSpeed = Mathf.Max(1f, unit.moveSpeed - 0.25f);
    }

    static void ClearPendingGrowth(GobboUnitSaveData unit)
    {
        if (unit == null) return;
        unit.pendingGrowthChoiceType = BuddyGrowthChoiceType.None;
        unit.pendingGrowthLevelWaiting = 0;
        unit.pendingEvolution = false;
        unit.runsWaitingForEvolution = 0;
        unit.evolutionLevelWaiting = 0;
    }
}
