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
    public int damage = 1; // buddy compatibility alias
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
        if (string.IsNullOrWhiteSpace(uniqueId)) uniqueId = GobboIdUtility.NewGobboId();
        EnsureRuntimeDefaults();
    }

    public virtual void EnsureIdentity()
    {
        EnsureId();
    }

    public virtual void EnsureIdentity(string preferredName)
    {
        if (!string.IsNullOrWhiteSpace(preferredName) && string.IsNullOrWhiteSpace(displayName)) displayName = preferredName.Trim();
        EnsureId();
    }

    public virtual void EnsureRuntimeDefaults()
    {
        if (string.IsNullOrWhiteSpace(uniqueId)) uniqueId = GobboIdUtility.NewGobboId();
        if (string.IsNullOrWhiteSpace(displayName)) displayName = "Gobbo";

        traitIds ??= new List<string>();
        abilityIds ??= new List<string>();
        itemIds ??= new List<string>();
        relationshipIds ??= new List<string>();
        evolutionHistoryIds ??= new List<string>();
        chosenCardIds ??= new List<string>();
        mutationIds ??= new List<string>();
        upgradeIds ??= new List<string>();
        unlockedUpgrades ??= new List<string>();
        unlockedAbilities ??= new List<string>();
        unlockedCosmetics ??= new List<string>();
        equippedCosmetics ??= new List<string>();
        unlockedItems ??= new List<string>();

        if (level <= 0) level = 1;
        if (campLevel <= 0) campLevel = level;
        if (xp < 0) xp = 0;
        if (xpToNextLevel <= 0) xpToNextLevel = 10;
        if (maxHealth <= 0) maxHealth = isLeader ? 100 : 10;
        if (health <= 0 || health > maxHealth) health = maxHealth;
        if (attack <= 0 && damage > 0) attack = damage;
        if (damage <= 0 && attack > 0) damage = attack;
        if (attack <= 0) attack = 1;
        if (damage <= 0) damage = attack;
        if (moveSpeed <= 0f) moveSpeed = isLeader ? 5f : 3.5f;
        if (attackRange <= 0f) attackRange = 0.85f;
        if (attackRadius <= 0f) attackRadius = 0.45f;
        if (attackCooldown <= 0f) attackCooldown = isLeader ? 0.7f : 0.8f;
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
        if (string.IsNullOrWhiteSpace(visualSetId)) visualSetId = gobboType == BuddyType.Baby ? "baby" : gobboType.ToString().ToLowerInvariant() + "_" + ageStage.ToString().ToLowerInvariant();
        if (shinies == 0 && money > 0) shinies = money;
        money = shinies;
    }

    public virtual GobboUnitSaveData CloneUnit()
    {
        EnsureRuntimeDefaults();
        GobboUnitSaveData copy = new GobboUnitSaveData();
        CopyInto(copy);
        return copy;
    }

    public virtual void CopyInto(GobboUnitSaveData copy)
    {
        if (copy == null) return;
        copy.uniqueId = uniqueId;
        copy.displayName = displayName;
        copy.isLeader = isLeader;
        copy.isDead = isDead;
        copy.gobboType = gobboType;
        copy.ageStage = ageStage;
        copy.level = level;
        copy.xp = xp;
        copy.xpToNextLevel = xpToNextLevel;
        copy.campLevel = campLevel;
        copy.pendingEvolution = pendingEvolution;
        copy.evolutionLevelWaiting = evolutionLevelWaiting;
        copy.runsWaitingForEvolution = runsWaitingForEvolution;
        copy.happiness = happiness;
        copy.loyalty = loyalty;
        copy.maxHealth = maxHealth;
        copy.health = health;
        copy.attack = attack;
        copy.damage = damage;
        copy.defense = defense;
        copy.moveSpeed = moveSpeed;
        copy.attackRange = attackRange;
        copy.attackRadius = attackRadius;
        copy.attackCooldown = attackCooldown;
        copy.critChance = critChance;
        copy.critDamageMultiplier = critDamageMultiplier;
        copy.knockbackForce = knockbackForce;
        copy.dashSpeed = dashSpeed;
        copy.dashDuration = dashDuration;
        copy.dashCooldown = dashCooldown;
        copy.digPower = digPower;
        copy.digRadius = digRadius;
        copy.digRange = digRange;
        copy.digTickRate = digTickRate;
        copy.hasSporeMend = hasSporeMend;
        copy.hasDashBite = hasDashBite;
        copy.healthControlsSize = healthControlsSize;
        copy.healthSizeMultiplier = healthSizeMultiplier;
        copy.onlyFightsAfterHit = onlyFightsAfterHit;
        copy.collectsFood = collectsFood;
        copy.hasBeenHit = hasBeenHit;
        copy.isInActiveSquad = isInActiveSquad;
        copy.survivedLastRun = survivedLastRun;
        copy.bodyColor = bodyColor;
        copy.visualSetId = visualSetId;
        copy.portraitId = portraitId;
        copy.equippedHat = equippedHat;
        copy.runsSurvived = runsSurvived;
        copy.kills = kills;
        copy.causeOfDeath = causeOfDeath;
        copy.traitIds = traitIds != null ? new List<string>(traitIds) : new List<string>();
        copy.abilityIds = abilityIds != null ? new List<string>(abilityIds) : new List<string>();
        copy.itemIds = itemIds != null ? new List<string>(itemIds) : new List<string>();
        copy.relationshipIds = relationshipIds != null ? new List<string>(relationshipIds) : new List<string>();
        copy.evolutionHistoryIds = evolutionHistoryIds != null ? new List<string>(evolutionHistoryIds) : new List<string>();
        copy.chosenCardIds = chosenCardIds != null ? new List<string>(chosenCardIds) : new List<string>();
        copy.mutationIds = mutationIds != null ? new List<string>(mutationIds) : new List<string>();
        copy.upgradeIds = upgradeIds != null ? new List<string>(upgradeIds) : new List<string>();
        copy.unlockedUpgrades = unlockedUpgrades != null ? new List<string>(unlockedUpgrades) : new List<string>();
        copy.unlockedAbilities = unlockedAbilities != null ? new List<string>(unlockedAbilities) : new List<string>();
        copy.unlockedCosmetics = unlockedCosmetics != null ? new List<string>(unlockedCosmetics) : new List<string>();
        copy.equippedCosmetics = equippedCosmetics != null ? new List<string>(equippedCosmetics) : new List<string>();
        copy.unlockedItems = unlockedItems != null ? new List<string>(unlockedItems) : new List<string>();
        copy.spores = spores;
        copy.mushrooms = mushrooms;
        copy.money = money;
        copy.shinies = shinies;
        copy.EnsureRuntimeDefaults();
    }
}

public static class GobboIdUtility
{
    public static string NewGobboId()
    {
        return "gobbo_" + Guid.NewGuid().ToString("N");
    }
}
