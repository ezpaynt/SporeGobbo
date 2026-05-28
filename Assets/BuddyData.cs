using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Compatibility wrapper around GobboUnitSaveData.
/// Existing scripts still use BuddyData.buddyName / buddyType / damage,
/// but the saved identity/stat shape is now shared with leader gobbos.
/// </summary>
[System.Serializable]
public class BuddyData : GobboUnitSaveData
{
    [Header("Buddy Compatibility Fields")]
    public string buddyName = "Gobbo";
    public BuddyType buddyType = BuddyType.Baby;
    public bool neglectedElder = false;
    public string equippedItem = "";

    public override void EnsureId()
    {
        base.EnsureId();
        SyncLegacyFromUnit();
    }

    public override void EnsureRuntimeDefaults()
    {
        base.EnsureRuntimeDefaults();
        SyncLegacyFromUnit();

        if (xpToNextLevel <= 0) xpToNextLevel = BuddyProgression.TestBabyXPToNextLevel;
        if (maxHealth <= 0) maxHealth = buddyType == BuddyType.Baby ? 8 : 5;
        if (health <= 0 || health > maxHealth) health = maxHealth;
        if (damage <= 0) damage = Mathf.Max(1, attack);
        if (attack <= 0) attack = damage;
        if (moveSpeed <= 0f) moveSpeed = buddyType == BuddyType.Baby ? 3.2f : 3.5f;
        if (attackCooldown <= 0f) attackCooldown = 0.9f;
        if (string.IsNullOrWhiteSpace(visualSetId))
            visualSetId = buddyType == BuddyType.Baby ? "baby" : buddyType.ToString().ToLowerInvariant() + "_" + ageStage.ToString().ToLowerInvariant();
    }

    void SyncLegacyFromUnit()
    {
        if (string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(buddyName)) displayName = buddyName;
        if (string.IsNullOrWhiteSpace(buddyName) && !string.IsNullOrWhiteSpace(displayName)) buddyName = displayName;
        if (string.IsNullOrWhiteSpace(displayName)) displayName = "Gobbo";
        if (string.IsNullOrWhiteSpace(buddyName)) buddyName = displayName;

        // Keep both names/types in lockstep while old scripts still use the old fields.
        displayName = buddyName;
        gobboType = buddyType;

        if (attack <= 0 && damage > 0) attack = damage;
        if (damage <= 0 && attack > 0) damage = attack;
        if (damage <= 0) damage = 1;
        if (attack <= 0) attack = damage;

        if (!string.IsNullOrWhiteSpace(equippedItem) && !itemIds.Contains(equippedItem))
            itemIds.Add(equippedItem);
        if (itemIds != null && itemIds.Count > 0 && string.IsNullOrWhiteSpace(equippedItem))
            equippedItem = itemIds[0];
    }

    public BuddyData Clone()
    {
        EnsureRuntimeDefaults();
        BuddyData copy = new BuddyData();
        CopyInto(copy);
        return copy;
    }

    public void CopyInto(BuddyData copy)
    {
        if (copy == null) return;
        copy.uniqueId = uniqueId;
        copy.displayName = displayName;
        copy.buddyName = buddyName;
        copy.isLeader = isLeader;
        copy.isDead = isDead;
        copy.gobboType = gobboType;
        copy.buddyType = buddyType;
        copy.ageStage = ageStage;
        copy.level = level;
        copy.xp = xp;
        copy.xpToNextLevel = xpToNextLevel;
        copy.campLevel = campLevel;
        copy.pendingEvolution = pendingEvolution;
        copy.evolutionLevelWaiting = evolutionLevelWaiting;
        copy.runsWaitingForEvolution = runsWaitingForEvolution;
        copy.neglectedElder = neglectedElder;
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
        copy.equippedItem = equippedItem;
        copy.spores = spores;
        copy.mushrooms = mushrooms;
        copy.money = money;
        copy.shinies = shinies;
        copy.EnsureRuntimeDefaults();
    }

    public static BuddyData FromUnit(GobboUnitSaveData unit)
    {
        if (unit == null) return null;
        BuddyData buddy = new BuddyData();
        buddy.uniqueId = unit.uniqueId;
        buddy.displayName = unit.displayName;
        buddy.buddyName = unit.displayName;
        buddy.isLeader = unit.isLeader;
        buddy.isDead = unit.isDead;
        buddy.gobboType = unit.gobboType;
        buddy.buddyType = unit.gobboType;
        buddy.ageStage = unit.ageStage;
        buddy.level = unit.level;
        buddy.xp = unit.xp;
        buddy.xpToNextLevel = unit.xpToNextLevel;
        buddy.campLevel = unit.campLevel;
        buddy.pendingEvolution = unit.pendingEvolution;
        buddy.evolutionLevelWaiting = unit.evolutionLevelWaiting;
        buddy.runsWaitingForEvolution = unit.runsWaitingForEvolution;
        buddy.happiness = unit.happiness;
        buddy.loyalty = unit.loyalty;
        buddy.maxHealth = unit.maxHealth;
        buddy.health = unit.health;
        buddy.attack = unit.attack;
        buddy.damage = unit.damage;
        buddy.defense = unit.defense;
        buddy.moveSpeed = unit.moveSpeed;
        buddy.attackRange = unit.attackRange;
        buddy.attackRadius = unit.attackRadius;
        buddy.attackCooldown = unit.attackCooldown;
        buddy.onlyFightsAfterHit = unit.onlyFightsAfterHit;
        buddy.collectsFood = unit.collectsFood;
        buddy.hasBeenHit = unit.hasBeenHit;
        buddy.isInActiveSquad = unit.isInActiveSquad;
        buddy.survivedLastRun = unit.survivedLastRun;
        buddy.bodyColor = unit.bodyColor;
        buddy.visualSetId = unit.visualSetId;
        buddy.portraitId = unit.portraitId;
        buddy.equippedHat = unit.equippedHat;
        buddy.runsSurvived = unit.runsSurvived;
        buddy.kills = unit.kills;
        buddy.causeOfDeath = unit.causeOfDeath;
        buddy.traitIds = unit.traitIds != null ? new List<string>(unit.traitIds) : new List<string>();
        buddy.abilityIds = unit.abilityIds != null ? new List<string>(unit.abilityIds) : new List<string>();
        buddy.itemIds = unit.itemIds != null ? new List<string>(unit.itemIds) : new List<string>();
        buddy.relationshipIds = unit.relationshipIds != null ? new List<string>(unit.relationshipIds) : new List<string>();
        buddy.evolutionHistoryIds = unit.evolutionHistoryIds != null ? new List<string>(unit.evolutionHistoryIds) : new List<string>();
        buddy.chosenCardIds = unit.chosenCardIds != null ? new List<string>(unit.chosenCardIds) : new List<string>();
        buddy.mutationIds = unit.mutationIds != null ? new List<string>(unit.mutationIds) : new List<string>();
        buddy.upgradeIds = unit.upgradeIds != null ? new List<string>(unit.upgradeIds) : new List<string>();
        buddy.equippedItem = buddy.itemIds.Count > 0 ? buddy.itemIds[0] : "";
        buddy.EnsureRuntimeDefaults();
        return buddy;
    }
}
