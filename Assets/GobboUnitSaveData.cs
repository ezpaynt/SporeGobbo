using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared persistent data for any gobbo that can exist long-term.
/// Player leaders and buddies should both be representable by this shape.
/// Scene objects are temporary; this data is the saved identity.
/// </summary>
[Serializable]
public class GobboUnitSaveData
{
    [Header("Identity")]
    public string uniqueId = "";
    public string displayName = "Gobbo";
    public bool isLeader = false;
    public bool isDead = false;

    [Header("Type / Growth")]
    public BuddyType gobboType = BuddyType.Baby;
    public GobboAgeStage ageStage = GobboAgeStage.Baby;
    public int level = 1;
    public int xp = 0;
    public int xpToNextLevel = 10;
    public int campLevel = 1;
    public bool pendingEvolution = false;
    public int evolutionLevelWaiting = 0;
    public int runsWaitingForEvolution = 0;

    [Header("Mood / Social")]
    [Range(0, 100)] public int happiness = 100;
    [Range(0, 100)] public int loyalty = 100;

    [Header("Combat Stats")]
    public int maxHealth = 10;
    public int health = 10;
    public int attack = 1;
    public int damage = 1; // compatibility alias used by buddy combat scripts
    public int defense = 0;
    public float moveSpeed = 3.5f;
    public float attackRange = 0.85f;
    public float attackRadius = 0.45f;
    public float attackCooldown = 0.8f;
    public float critChance = 0f;
    public float critDamageMultiplier = 1.5f;
    public float knockbackForce = 6f;

    [Header("Movement / Digging")]
    public float dashSpeed = 12f;
    public float dashDuration = 0.12f;
    public float dashCooldown = 0.7f;
    public int digPower = 1;
    public float digRadius = 0.65f;
    public float digRange = 0.8f;
    public float digTickRate = 0.05f;

    [Header("Abilities")]
    public bool hasSporeMend = false;
    public bool hasDashBite = false;
    public bool healthControlsSize = false;
    public float healthSizeMultiplier = 0f;

    [Header("Behavior")]
    public bool onlyFightsAfterHit = false;
    public bool collectsFood = false;
    public bool hasBeenHit = false;

    [Header("Roster State")]
    public bool isInActiveSquad = false;
    public bool survivedLastRun = true;

    [Header("Visual")]
    public Color bodyColor = Color.green;
    public string visualSetId = "baby";
    public string portraitId = "";
    public string equippedHat = "";

    [Header("Progress / History")]
    public int runsSurvived = 0;
    public int kills = 0;
    public string causeOfDeath = "";

    [Header("Content IDs")]
    public List<string> traitIds = new List<string>();
    public List<string> abilityIds = new List<string>();
    public List<string> itemIds = new List<string>();
    public List<string> relationshipIds = new List<string>();
    public List<string> evolutionHistoryIds = new List<string>();
    public List<string> chosenCardIds = new List<string>();
    public List<string> mutationIds = new List<string>();
    public List<string> upgradeIds = new List<string>();
    public List<string> unlockedUpgrades = new List<string>();
    public List<string> unlockedAbilities = new List<string>();
    public List<string> unlockedCosmetics = new List<string>();
    public List<string> equippedCosmetics = new List<string>();
    public List<string> unlockedItems = new List<string>();

    [Header("Resources - usually leader only")]
    public int spores = 0;
    public int mushrooms = 0;
    public int money = 0;
    public int shinies = 0;

    public virtual void EnsureId()
    {
        if (string.IsNullOrWhiteSpace(uniqueId))
            uniqueId = GobboIdUtility.NewGobboId();
        EnsureRuntimeDefaults();
    }

    public virtual void EnsureRuntimeDefaults()
    {
        if (string.IsNullOrWhiteSpace(uniqueId)) uniqueId = GobboIdUtility.NewGobboId();
        if (string.IsNullOrWhiteSpace(displayName)) displayName = "Gobbo";
        if (traitIds == null) traitIds = new List<string>();
        if (abilityIds == null) abilityIds = new List<string>();
        if (itemIds == null) itemIds = new List<string>();
        if (relationshipIds == null) relationshipIds = new List<string>();
        if (evolutionHistoryIds == null) evolutionHistoryIds = new List<string>();
        if (chosenCardIds == null) chosenCardIds = new List<string>();
        if (mutationIds == null) mutationIds = new List<string>();
        if (upgradeIds == null) upgradeIds = new List<string>();
        if (unlockedUpgrades == null) unlockedUpgrades = new List<string>();
        if (unlockedAbilities == null) unlockedAbilities = new List<string>();
        if (unlockedCosmetics == null) unlockedCosmetics = new List<string>();
        if (equippedCosmetics == null) equippedCosmetics = new List<string>();
        if (unlockedItems == null) unlockedItems = new List<string>();

        if (level <= 0) level = 1;
        if (campLevel <= 0) campLevel = level;
        if (xp < 0) xp = 0;
        if (xpToNextLevel <= 0) xpToNextLevel = 10;
        if (maxHealth <= 0) maxHealth = 10;
        if (health <= 0 || health > maxHealth) health = maxHealth;
        if (attack <= 0 && damage > 0) attack = damage;
        if (damage <= 0 && attack > 0) damage = attack;
        if (attack <= 0) attack = 1;
        if (damage <= 0) damage = attack;
        if (moveSpeed <= 0f) moveSpeed = 3.5f;
        if (attackRange <= 0f) attackRange = 0.85f;
        if (attackRadius <= 0f) attackRadius = 0.45f;
        if (attackCooldown <= 0f) attackCooldown = 0.8f;
        if (critDamageMultiplier <= 0f) critDamageMultiplier = 1.5f;
        if (knockbackForce <= 0f) knockbackForce = 6f;
        if (dashSpeed <= 0f) dashSpeed = 12f;
        if (dashDuration <= 0f) dashDuration = 0.12f;
        if (dashCooldown <= 0f) dashCooldown = 0.7f;
        if (digPower <= 0) digPower = 1;
        if (digRadius <= 0f) digRadius = 0.65f;
        if (digRange <= 0f) digRange = 0.8f;
        if (digTickRate <= 0f) digTickRate = 0.05f;
        happiness = Mathf.Clamp(happiness <= 0 ? 100 : happiness, 0, 100);
        loyalty = Mathf.Clamp(loyalty <= 0 ? 100 : loyalty, 0, 100);
        if (string.IsNullOrWhiteSpace(visualSetId))
            visualSetId = gobboType == BuddyType.Baby ? "baby" : gobboType.ToString().ToLowerInvariant() + "_" + ageStage.ToString().ToLowerInvariant();
    }

    public GobboUnitSaveData CloneUnit()
    {
        EnsureRuntimeDefaults();
        return new GobboUnitSaveData
        {
            uniqueId = uniqueId,
            displayName = displayName,
            isLeader = isLeader,
            isDead = isDead,
            gobboType = gobboType,
            ageStage = ageStage,
            level = level,
            xp = xp,
            xpToNextLevel = xpToNextLevel,
            campLevel = campLevel,
            pendingEvolution = pendingEvolution,
            evolutionLevelWaiting = evolutionLevelWaiting,
            runsWaitingForEvolution = runsWaitingForEvolution,
            happiness = happiness,
            loyalty = loyalty,
            maxHealth = maxHealth,
            health = health,
            attack = attack,
            damage = damage,
            defense = defense,
            moveSpeed = moveSpeed,
            attackRange = attackRange,
            attackRadius = attackRadius,
            attackCooldown = attackCooldown,
            critChance = critChance,
            critDamageMultiplier = critDamageMultiplier,
            knockbackForce = knockbackForce,
            dashSpeed = dashSpeed,
            dashDuration = dashDuration,
            dashCooldown = dashCooldown,
            digPower = digPower,
            digRadius = digRadius,
            digRange = digRange,
            digTickRate = digTickRate,
            hasSporeMend = hasSporeMend,
            hasDashBite = hasDashBite,
            healthControlsSize = healthControlsSize,
            healthSizeMultiplier = healthSizeMultiplier,
            onlyFightsAfterHit = onlyFightsAfterHit,
            collectsFood = collectsFood,
            hasBeenHit = hasBeenHit,
            isInActiveSquad = isInActiveSquad,
            survivedLastRun = survivedLastRun,
            bodyColor = bodyColor,
            visualSetId = visualSetId,
            portraitId = portraitId,
            equippedHat = equippedHat,
            runsSurvived = runsSurvived,
            kills = kills,
            causeOfDeath = causeOfDeath,
            traitIds = new List<string>(traitIds),
            abilityIds = new List<string>(abilityIds),
            itemIds = new List<string>(itemIds),
            relationshipIds = new List<string>(relationshipIds),
            evolutionHistoryIds = new List<string>(evolutionHistoryIds),
            chosenCardIds = new List<string>(chosenCardIds),
            mutationIds = new List<string>(mutationIds),
            upgradeIds = new List<string>(upgradeIds),
            unlockedUpgrades = new List<string>(unlockedUpgrades),
            unlockedAbilities = new List<string>(unlockedAbilities),
            unlockedCosmetics = new List<string>(unlockedCosmetics),
            equippedCosmetics = new List<string>(equippedCosmetics),
            unlockedItems = new List<string>(unlockedItems),
            spores = spores,
            mushrooms = mushrooms,
            money = money,
            shinies = shinies
        };
    }
}

public static class GobboIdUtility
{
    public static string NewGobboId()
    {
        return "gobbo_" + Guid.NewGuid().ToString("N");
    }
}
