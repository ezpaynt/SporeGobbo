using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Compatibility bridge while the project finishes moving from GobboSaveData to GobboUnitSaveData.
/// Long-term goal: delete this after every system talks directly to GobboUnitSaveData/GameState API.
/// </summary>
public static class GobboSaveDataExtensions
{
    public static void EnsureLeaderIdentity(this GobboSaveData data)
    {
        data.EnsureLeaderIdentity("Gobbo");
    }

    public static void EnsureLeaderIdentity(this GobboSaveData data, string fallbackName)
    {
        if (data == null) return;

        if (string.IsNullOrWhiteSpace(data.displayName))
            data.displayName = string.IsNullOrWhiteSpace(fallbackName) ? "Gobbo" : fallbackName.Trim();

        data.isLeader = true;
        data.EnsureRuntimeDefaults();

        if (string.IsNullOrWhiteSpace(data.uniqueId))
            data.uniqueId = GobboIdUtility.NewGobboId();

        if (data.maxHealth <= 0) data.maxHealth = 100;
        if (data.health <= 0 || data.health > data.maxHealth) data.health = data.maxHealth;
        if (data.attack <= 0) data.attack = data.damage > 0 ? data.damage : 5;
        if (data.damage <= 0) data.damage = data.attack;
        if (data.defense < 0) data.defense = 0;
        if (data.moveSpeed <= 0f) data.moveSpeed = 5f;
        if (data.attackCooldown <= 0f) data.attackCooldown = 0.7f;
    }

    public static GobboUnitSaveData ToUnitSave(this GobboSaveData data)
    {
        return data.ToUnitData();
    }

    public static GobboUnitSaveData ToUnitData(this GobboSaveData data)
    {
        if (data == null)
            return new GobboUnitSaveData { isLeader = true, displayName = "Gobbo" };

        data.EnsureLeaderIdentity(data.displayName);

        GobboUnitSaveData unit = new GobboUnitSaveData();
        data.CopyInto(unit);
        unit.isLeader = true;
        unit.EnsureRuntimeDefaults();
        return unit;
    }

    public static GobboSaveData ToLeaderSave(this GobboUnitSaveData unit)
    {
        GobboSaveData save = new GobboSaveData();
        save.ApplyUnitData(unit);
        save.isLeader = true;
        save.EnsureLeaderIdentity(save.displayName);
        return save;
    }

    public static GobboSaveData CloneLeader(this GobboSaveData source)
    {
        if (source == null)
            return new GobboSaveData();

        GobboSaveData copy = new GobboSaveData();
        copy.ApplyUnitData(source.ToUnitSave());
        copy.isLeader = true;
        copy.EnsureLeaderIdentity(copy.displayName);
        return copy;
    }

    public static void ApplyUnitData(this GobboSaveData data, GobboUnitSaveData unit)
    {
        if (data == null || unit == null) return;

        unit.EnsureRuntimeDefaults();
        unit.CopyInto(data);

        data.isLeader = true;
        if (string.IsNullOrWhiteSpace(data.displayName))
            data.displayName = string.IsNullOrWhiteSpace(unit.displayName) ? "Gobbo" : unit.displayName;

        data.EnsureLeaderIdentity(data.displayName);
    }
}
