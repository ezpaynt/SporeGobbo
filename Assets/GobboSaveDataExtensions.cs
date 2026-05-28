using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Bridge helpers while the project is moving from separate player/buddy save data
/// toward one shared GobboUnitSaveData shape.
///
/// IMPORTANT: this file intentionally does NOT directly access optional fields on
/// GobboSaveData like uniqueId or relationshipIds, because GobboSaveData still lives
/// inside GameState.cs and different local versions may not have every new field yet.
/// Optional fields are read/written by reflection so the project keeps compiling during
/// the transition.
/// </summary>
public static class GobboSaveDataExtensions
{
    private const string DefaultLeaderName = "Gobbo";

    public static GobboUnitSaveData ToUnitData(this GobboSaveData source)
    {
        GobboUnitSaveData unit = new GobboUnitSaveData();
        source.CopyIntoUnit(unit);
        return unit;
    }

    public static GobboUnitSaveData ToGobboUnitSaveData(this GobboSaveData source)
    {
        return source.ToUnitData();
    }

    public static GobboUnitSaveData ToUnit(this GobboSaveData source)
    {
        return source.ToUnitData();
    }

    public static void CopyIntoUnit(this GobboSaveData source, GobboUnitSaveData unit)
    {
        if (unit == null) return;

        if (source == null)
        {
            unit.EnsureRuntimeDefaults();
            return;
        }

        string id = GetOptionalString(source, "gobboId");
        if (string.IsNullOrWhiteSpace(id)) id = GetOptionalString(source, "uniqueId");
        if (string.IsNullOrWhiteSpace(id)) id = "gobbo_" + Guid.NewGuid().ToString("N");

        string displayName = GetOptionalString(source, "displayName");
        if (string.IsNullOrWhiteSpace(displayName)) displayName = DefaultLeaderName;

        unit.uniqueId = id;
        unit.displayName = displayName;
        unit.isLeader = GetOptionalBool(source, "isLeader", true);
        unit.isDead = GetOptionalBool(source, "isDead", false);

        unit.gobboType = source.gobboType;
        unit.ageStage = source.ageStage;
        unit.visualSetId = source.visualSetId;

        unit.level = source.level;
        unit.xp = source.xp;
        unit.xpToNextLevel = source.xpToNextLevel;
        unit.pendingEvolution = source.pendingEvolution;
        unit.evolutionLevelWaiting = source.evolutionLevelWaiting;

        unit.maxHealth = source.maxHealth;
        unit.health = source.health;
        unit.attack = source.attack;
        unit.damage = source.attack;
        unit.defense = source.defense;
        unit.attackRange = source.attackRange;
        unit.attackRadius = source.attackRadius;
        unit.attackCooldown = source.attackCooldown;
        unit.critChance = source.critChance;
        unit.critDamageMultiplier = source.critDamageMultiplier;
        unit.knockbackForce = source.knockbackForce;

        unit.moveSpeed = source.moveSpeed;
        unit.dashSpeed = source.dashSpeed;
        unit.dashDuration = source.dashDuration;
        unit.dashCooldown = source.dashCooldown;

        unit.digPower = source.digPower;
        unit.digRadius = source.digRadius;
        unit.digRange = source.digRange;
        unit.digTickRate = source.digTickRate;

        unit.hasSporeMend = source.hasSporeMend;
        unit.hasDashBite = source.hasDashBite;
        unit.healthControlsSize = source.healthControlsSize;
        unit.healthSizeMultiplier = source.healthSizeMultiplier;

        unit.spores = source.spores;
        unit.mushrooms = source.mushrooms;
        unit.money = source.money;
        unit.shinies = source.shinies;

        unit.unlockedUpgrades = CopyList(source.unlockedUpgrades);
        unit.unlockedAbilities = CopyList(source.unlockedAbilities);
        unit.unlockedCosmetics = CopyList(source.unlockedCosmetics);
        unit.equippedCosmetics = CopyList(source.equippedCosmetics);
        unit.unlockedItems = CopyList(source.unlockedItems);
        unit.chosenCardIds = CopyList(source.chosenCardIds);

        unit.traitIds = GetOptionalList(source, "traitIds");
        unit.abilityIds = GetOptionalList(source, "abilityIds");
        unit.itemIds = GetOptionalList(source, "itemIds");
        unit.relationshipIds = GetOptionalList(source, "relationshipIds");
        unit.evolutionHistoryIds = GetOptionalList(source, "evolutionHistoryIds");
        unit.runsSurvived = GetOptionalInt(source, "runsSurvived", unit.runsSurvived);
        unit.kills = GetOptionalInt(source, "kills", unit.kills);

        unit.EnsureRuntimeDefaults();
    }

    public static void ApplyFromUnit(this GobboSaveData target, GobboUnitSaveData unit)
    {
        if (target == null || unit == null) return;
        unit.EnsureRuntimeDefaults();

        SetOptionalString(target, "gobboId", unit.uniqueId);
        SetOptionalString(target, "uniqueId", unit.uniqueId);
        SetOptionalString(target, "displayName", string.IsNullOrWhiteSpace(unit.displayName) ? DefaultLeaderName : unit.displayName);
        SetOptionalBool(target, "isLeader", true);
        SetOptionalBool(target, "isDead", unit.isDead);

        target.gobboType = unit.gobboType;
        target.ageStage = unit.ageStage;
        target.visualSetId = unit.visualSetId;

        target.level = unit.level;
        target.xp = unit.xp;
        target.xpToNextLevel = unit.xpToNextLevel;
        target.pendingEvolution = unit.pendingEvolution;
        target.evolutionLevelWaiting = unit.evolutionLevelWaiting;

        target.maxHealth = unit.maxHealth;
        target.health = unit.health;
        target.attack = unit.attack > 0 ? unit.attack : unit.damage;
        target.defense = unit.defense;
        target.attackRange = unit.attackRange;
        target.attackRadius = unit.attackRadius;
        target.attackCooldown = unit.attackCooldown;
        target.critChance = unit.critChance;
        target.critDamageMultiplier = unit.critDamageMultiplier;
        target.knockbackForce = unit.knockbackForce;

        target.moveSpeed = unit.moveSpeed;
        target.dashSpeed = unit.dashSpeed;
        target.dashDuration = unit.dashDuration;
        target.dashCooldown = unit.dashCooldown;

        target.digPower = unit.digPower;
        target.digRadius = unit.digRadius;
        target.digRange = unit.digRange;
        target.digTickRate = unit.digTickRate;

        target.hasSporeMend = unit.hasSporeMend;
        target.hasDashBite = unit.hasDashBite;
        target.healthControlsSize = unit.healthControlsSize;
        target.healthSizeMultiplier = unit.healthSizeMultiplier;

        target.spores = unit.spores;
        target.mushrooms = unit.mushrooms;
        target.money = unit.money;
        target.shinies = unit.shinies;

        target.unlockedUpgrades = CopyList(unit.unlockedUpgrades);
        target.unlockedAbilities = CopyList(unit.unlockedAbilities);
        target.unlockedCosmetics = CopyList(unit.unlockedCosmetics);
        target.equippedCosmetics = CopyList(unit.equippedCosmetics);
        target.unlockedItems = CopyList(unit.unlockedItems);
        target.chosenCardIds = CopyList(unit.chosenCardIds);

        SetOptionalList(target, "traitIds", unit.traitIds);
        SetOptionalList(target, "abilityIds", unit.abilityIds);
        SetOptionalList(target, "itemIds", unit.itemIds);
        SetOptionalList(target, "relationshipIds", unit.relationshipIds);
        SetOptionalList(target, "evolutionHistoryIds", unit.evolutionHistoryIds);
        SetOptionalInt(target, "runsSurvived", unit.runsSurvived);
        SetOptionalInt(target, "kills", unit.kills);
    }

    public static void CopyFromUnit(this GobboSaveData target, GobboUnitSaveData unit)
    {
        target.ApplyFromUnit(unit);
    }

    public static void EnsureLeaderIdentity(this GobboSaveData data)
    {
        if (data == null) return;

        string id = GetGobboId(data);
        if (string.IsNullOrWhiteSpace(id))
        {
            id = "gobbo_" + Guid.NewGuid().ToString("N");
            SetGobboId(data, id);
        }

        string name = GetDisplayName(data);
        if (string.IsNullOrWhiteSpace(name)) SetDisplayName(data, DefaultLeaderName);
        SetOptionalBool(data, "isLeader", true);
    }

    public static void EnsureRuntimeDefaults(this GobboSaveData data)
    {
        if (data == null) return;
        data.EnsureLeaderIdentity();
        if (data.level <= 0) data.level = 1;
        if (data.xpToNextLevel <= 0) data.xpToNextLevel = 10;
        if (data.maxHealth <= 0) data.maxHealth = 100;
        if (data.health <= 0 || data.health > data.maxHealth) data.health = data.maxHealth;
        if (data.attack <= 0) data.attack = 5;
        if (data.defense <= 0) data.defense = 2;
        if (data.moveSpeed <= 0f) data.moveSpeed = 5f;
        if (data.attackCooldown <= 0f) data.attackCooldown = 0.7f;
        if (string.IsNullOrWhiteSpace(data.visualSetId)) data.visualSetId = "baby";
    }

    public static string GetGobboId(this GobboSaveData data)
    {
        if (data == null) return "";
        string id = GetOptionalString(data, "gobboId");
        if (string.IsNullOrWhiteSpace(id)) id = GetOptionalString(data, "uniqueId");
        return id ?? "";
    }

    public static void SetGobboId(this GobboSaveData data, string id)
    {
        if (data == null) return;
        if (string.IsNullOrWhiteSpace(id)) id = "gobbo_" + Guid.NewGuid().ToString("N");
        SetOptionalString(data, "gobboId", id);
        SetOptionalString(data, "uniqueId", id);
    }

    public static string GetDisplayName(this GobboSaveData data)
    {
        if (data == null) return "";
        string name = GetOptionalString(data, "displayName");
        return string.IsNullOrWhiteSpace(name) ? DefaultLeaderName : name;
    }

    public static void SetDisplayName(this GobboSaveData data, string name)
    {
        if (data == null) return;
        SetOptionalString(data, "displayName", string.IsNullOrWhiteSpace(name) ? DefaultLeaderName : name.Trim());
    }

    private static List<string> CopyList(List<string> source)
    {
        return source != null ? new List<string>(source) : new List<string>();
    }

    private static FieldInfo Field(object obj, string fieldName)
    {
        if (obj == null || string.IsNullOrWhiteSpace(fieldName)) return null;
        return obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }

    private static string GetOptionalString(object obj, string fieldName)
    {
        FieldInfo f = Field(obj, fieldName);
        return f != null && f.FieldType == typeof(string) ? (string)f.GetValue(obj) : "";
    }

    private static void SetOptionalString(object obj, string fieldName, string value)
    {
        FieldInfo f = Field(obj, fieldName);
        if (f != null && f.FieldType == typeof(string)) f.SetValue(obj, value ?? "");
    }

    private static bool GetOptionalBool(object obj, string fieldName, bool fallback)
    {
        FieldInfo f = Field(obj, fieldName);
        return f != null && f.FieldType == typeof(bool) ? (bool)f.GetValue(obj) : fallback;
    }

    private static void SetOptionalBool(object obj, string fieldName, bool value)
    {
        FieldInfo f = Field(obj, fieldName);
        if (f != null && f.FieldType == typeof(bool)) f.SetValue(obj, value);
    }

    private static int GetOptionalInt(object obj, string fieldName, int fallback)
    {
        FieldInfo f = Field(obj, fieldName);
        return f != null && f.FieldType == typeof(int) ? (int)f.GetValue(obj) : fallback;
    }

    private static void SetOptionalInt(object obj, string fieldName, int value)
    {
        FieldInfo f = Field(obj, fieldName);
        if (f != null && f.FieldType == typeof(int)) f.SetValue(obj, value);
    }

    private static List<string> GetOptionalList(object obj, string fieldName)
    {
        FieldInfo f = Field(obj, fieldName);
        if (f == null || f.FieldType != typeof(List<string>)) return new List<string>();
        List<string> list = (List<string>)f.GetValue(obj);
        return list != null ? new List<string>(list) : new List<string>();
    }

    private static void SetOptionalList(object obj, string fieldName, List<string> value)
    {
        FieldInfo f = Field(obj, fieldName);
        if (f != null && f.FieldType == typeof(List<string>)) f.SetValue(obj, value != null ? new List<string>(value) : new List<string>());
    }
}
