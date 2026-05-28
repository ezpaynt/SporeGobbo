using System.Collections.Generic;
using UnityEngine;

public static class GobboSaveDataExtensions
{
    public static void EnsureLeaderIdentity(this GobboSaveData data, string fallbackName = "Gobbo")
    {
        if (data == null) return;
        if (string.IsNullOrWhiteSpace(data.uniqueId)) data.uniqueId = GobboIdUtility.NewGobboId();
        if (string.IsNullOrWhiteSpace(data.displayName)) data.displayName = string.IsNullOrWhiteSpace(fallbackName) ? "Gobbo" : fallbackName.Trim();
        data.isLeader = true;
        if (data.level <= 0) data.level = 1;
        if (data.xpToNextLevel <= 0) data.xpToNextLevel = 10;
        if (data.maxHealth <= 0) data.maxHealth = 100;
        if (data.health <= 0 || data.health > data.maxHealth) data.health = data.maxHealth;
        if (data.attack <= 0) data.attack = 5;
        if (data.defense < 0) data.defense = 0;
        if (data.moveSpeed <= 0f) data.moveSpeed = 5f;
        if (data.attackRange <= 0f) data.attackRange = 0.85f;
        if (data.attackRadius <= 0f) data.attackRadius = 0.45f;
        if (data.attackCooldown <= 0f) data.attackCooldown = 0.7f;
        if (data.visualSetId == null || data.visualSetId.Trim().Length == 0) data.visualSetId = "baby";
        if (data.traitIds == null) data.traitIds = new List<string>();
        if (data.abilityIds == null) data.abilityIds = new List<string>();
        if (data.itemIds == null) data.itemIds = new List<string>();
        if (data.relationshipIds == null) data.relationshipIds = new List<string>();
        if (data.evolutionHistoryIds == null) data.evolutionHistoryIds = new List<string>();
    }

    public static GobboUnitSaveData ToUnitData(this GobboSaveData data)
    {
        if (data == null) return null;
        data.EnsureLeaderIdentity();
        GobboUnitSaveData unit = new GobboUnitSaveData();
        unit.uniqueId = data.uniqueId;
        unit.displayName = data.displayName;
        unit.isLeader = true;
        unit.gobboType = data.gobboType;
        unit.ageStage = data.ageStage;
        unit.level = data.level;
        unit.xp = data.xp;
        unit.xpToNextLevel = data.xpToNextLevel;
        unit.maxHealth = data.maxHealth;
        unit.health = data.health;
        unit.attack = data.attack;
        unit.damage = data.attack;
        unit.defense = data.defense;
        unit.moveSpeed = data.moveSpeed;
        unit.attackRange = data.attackRange;
        unit.attackRadius = data.attackRadius;
        unit.attackCooldown = data.attackCooldown;
        unit.critChance = data.critChance;
        unit.critDamageMultiplier = data.critDamageMultiplier;
        unit.knockbackForce = data.knockbackForce;
        unit.dashSpeed = data.dashSpeed;
        unit.dashDuration = data.dashDuration;
        unit.dashCooldown = data.dashCooldown;
        unit.digPower = data.digPower;
        unit.digRadius = data.digRadius;
        unit.digRange = data.digRange;
        unit.digTickRate = data.digTickRate;
        unit.hasSporeMend = data.hasSporeMend;
        unit.hasDashBite = data.hasDashBite;
        unit.healthControlsSize = data.healthControlsSize;
        unit.healthSizeMultiplier = data.healthSizeMultiplier;
        unit.visualSetId = data.visualSetId;
        unit.unlockedUpgrades = data.unlockedUpgrades != null ? new List<string>(data.unlockedUpgrades) : new List<string>();
        unit.unlockedAbilities = data.unlockedAbilities != null ? new List<string>(data.unlockedAbilities) : new List<string>();
        unit.unlockedCosmetics = data.unlockedCosmetics != null ? new List<string>(data.unlockedCosmetics) : new List<string>();
        unit.equippedCosmetics = data.equippedCosmetics != null ? new List<string>(data.equippedCosmetics) : new List<string>();
        unit.unlockedItems = data.unlockedItems != null ? new List<string>(data.unlockedItems) : new List<string>();
        unit.chosenCardIds = data.chosenCardIds != null ? new List<string>(data.chosenCardIds) : new List<string>();
        unit.traitIds = data.traitIds != null ? new List<string>(data.traitIds) : new List<string>();
        unit.abilityIds = data.abilityIds != null ? new List<string>(data.abilityIds) : new List<string>();
        unit.itemIds = data.itemIds != null ? new List<string>(data.itemIds) : new List<string>();
        unit.relationshipIds = data.relationshipIds != null ? new List<string>(data.relationshipIds) : new List<string>();
        unit.evolutionHistoryIds = data.evolutionHistoryIds != null ? new List<string>(data.evolutionHistoryIds) : new List<string>();
        unit.spores = data.spores;
        unit.mushrooms = data.mushrooms;
        unit.money = data.money;
        unit.shinies = data.shinies;
        unit.EnsureRuntimeDefaults();
        return unit;
    }

    public static void ApplyUnitData(this GobboSaveData data, GobboUnitSaveData unit)
    {
        if (data == null || unit == null) return;
        unit.EnsureRuntimeDefaults();
        data.uniqueId = unit.uniqueId;
        data.displayName = unit.displayName;
        data.isLeader = true;
        data.gobboType = unit.gobboType;
        data.ageStage = unit.ageStage;
        data.level = unit.level;
        data.xp = unit.xp;
        data.xpToNextLevel = unit.xpToNextLevel;
        data.maxHealth = unit.maxHealth;
        data.health = unit.health;
        data.attack = Mathf.Max(1, unit.attack > 0 ? unit.attack : unit.damage);
        data.defense = unit.defense;
        data.moveSpeed = unit.moveSpeed;
        data.attackRange = unit.attackRange;
        data.attackRadius = unit.attackRadius;
        data.attackCooldown = unit.attackCooldown;
        data.critChance = unit.critChance;
        data.critDamageMultiplier = unit.critDamageMultiplier;
        data.knockbackForce = unit.knockbackForce;
        data.dashSpeed = unit.dashSpeed;
        data.dashDuration = unit.dashDuration;
        data.dashCooldown = unit.dashCooldown;
        data.digPower = unit.digPower;
        data.digRadius = unit.digRadius;
        data.digRange = unit.digRange;
        data.digTickRate = unit.digTickRate;
        data.hasSporeMend = unit.hasSporeMend;
        data.hasDashBite = unit.hasDashBite;
        data.healthControlsSize = unit.healthControlsSize;
        data.healthSizeMultiplier = unit.healthSizeMultiplier;
        data.visualSetId = unit.visualSetId;
        data.unlockedUpgrades = unit.unlockedUpgrades != null ? new List<string>(unit.unlockedUpgrades) : new List<string>();
        data.unlockedAbilities = unit.unlockedAbilities != null ? new List<string>(unit.unlockedAbilities) : new List<string>();
        data.unlockedCosmetics = unit.unlockedCosmetics != null ? new List<string>(unit.unlockedCosmetics) : new List<string>();
        data.equippedCosmetics = unit.equippedCosmetics != null ? new List<string>(unit.equippedCosmetics) : new List<string>();
        data.unlockedItems = unit.unlockedItems != null ? new List<string>(unit.unlockedItems) : new List<string>();
        data.chosenCardIds = unit.chosenCardIds != null ? new List<string>(unit.chosenCardIds) : new List<string>();
        data.traitIds = unit.traitIds != null ? new List<string>(unit.traitIds) : new List<string>();
        data.abilityIds = unit.abilityIds != null ? new List<string>(unit.abilityIds) : new List<string>();
        data.itemIds = unit.itemIds != null ? new List<string>(unit.itemIds) : new List<string>();
        data.relationshipIds = unit.relationshipIds != null ? new List<string>(unit.relationshipIds) : new List<string>();
        data.evolutionHistoryIds = unit.evolutionHistoryIds != null ? new List<string>(unit.evolutionHistoryIds) : new List<string>();
        data.spores = unit.spores;
        data.mushrooms = unit.mushrooms;
        data.money = unit.money;
        data.shinies = unit.shinies;
        data.EnsureLeaderIdentity();
    }
}
