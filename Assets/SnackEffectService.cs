using System.Collections.Generic;
using UnityEngine;

public class SnackEffectPreview
{
    public bool canApply;
    public string message = "";
    public int healingAmount;
    public int maxHealthIncrease;
    public int attackIncrease;
    public int defenseIncrease;
}

public static class SnackEffectService
{
    public static SnackEffectPreview BuildPreview(GameState state, ItemDefinition item, ItemTargetType targetType, string targetId)
    {
        SnackEffectPreview preview = new SnackEffectPreview();
        GobboUnitSaveData target = ResolveTargetData(state, targetType, targetId);
        if (item == null)
        {
            preview.message = "Missing item.";
            return preview;
        }
        if (target == null)
        {
            preview.message = "Missing target.";
            return preview;
        }

        int currentHealth = target.health;
        int maxHealth = Mathf.Max(1, target.maxHealth);
        GobboController liveLeader = targetType == ItemTargetType.Leader ? FindLiveLeader() : null;
        if (liveLeader != null)
        {
            currentHealth = liveLeader.health;
            maxHealth = Mathf.Max(1, liveLeader.maxHealth);
        }

        foreach (ItemEffectDefinition effect in GetEffects(item))
        {
            int amount = effect.GetSafeAmount();
            switch (effect.effectType)
            {
                case ItemEffectType.HealCurrentHp:
                    preview.healingAmount += Mathf.Max(0, Mathf.Min(amount, maxHealth - currentHealth));
                    break;
                case ItemEffectType.PermanentMaxHp:
                    preview.maxHealthIncrease += amount;
                    preview.healingAmount += amount;
                    maxHealth += amount;
                    currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
                    break;
                case ItemEffectType.PermanentAttack:
                    preview.attackIncrease += amount;
                    break;
                case ItemEffectType.PermanentDefense:
                    preview.defenseIncrease += amount;
                    break;
            }
        }

        preview.canApply = true;
        preview.message = BuildPreviewMessage(preview);
        return preview;
    }

    public static bool ApplyEffects(GameState state, ItemDefinition item, ItemTargetType targetType, string targetId, out string message)
    {
        message = "";
        if (state == null)
        {
            message = "Missing game state.";
            return false;
        }
        if (item == null)
        {
            message = "Missing item.";
            return false;
        }

        GobboUnitSaveData target = ResolveTargetData(state, targetType, targetId);
        if (target == null)
        {
            message = "Missing target.";
            return false;
        }

        if (targetType == ItemTargetType.Leader)
            return ApplyToLeader(state, item, out message);

        bool applied = ApplyToSavedUnit(target, item, out message);
        if (applied) RefreshSpawnedBuddy(target.uniqueId);
        return applied;
    }

    public static GobboUnitSaveData ResolveTargetData(GameState state, ItemTargetType targetType, string targetId)
    {
        if (state == null) return null;
        state.EnsureRuntimeDefaults();
        if (targetType == ItemTargetType.Leader) return state.GetLeader();
        if (targetType == ItemTargetType.Buddy) return state.FindOwnedGobbo(targetId);
        return null;
    }

    static bool ApplyToLeader(GameState state, ItemDefinition item, out string message)
    {
        message = "";
        GobboUnitSaveData leaderData = state.GetLeader();
        GobboController liveLeader = FindLiveLeader();
        if (liveLeader != null)
        {
            foreach (ItemEffectDefinition effect in GetEffects(item))
                ApplyToLiveLeader(liveLeader, leaderData, effect);

            liveLeader.RefreshAfterSaveLoad();
            state.SavePlayer(liveLeader);
            message = "Applied snack to leader.";
            return true;
        }

        bool applied = ApplyToSavedUnit(leaderData, item, out message);
        if (applied) leaderData.isLeader = true;
        return applied;
    }

    static bool ApplyToSavedUnit(GobboUnitSaveData unit, ItemDefinition item, out string message)
    {
        message = "";
        if (unit == null)
        {
            message = "Missing target data.";
            return false;
        }

        unit.EnsureRuntimeDefaults();
        foreach (ItemEffectDefinition effect in GetEffects(item))
        {
            int amount = effect.GetSafeAmount();
            switch (effect.effectType)
            {
                case ItemEffectType.HealCurrentHp:
                    unit.health = Mathf.Min(unit.maxHealth, unit.health + amount);
                    break;
                case ItemEffectType.PermanentMaxHp:
                    unit.snackMaxHealthBonus = Mathf.Max(0, unit.snackMaxHealthBonus + amount);
                    unit.maxHealth = Mathf.Max(1, unit.maxHealth + amount);
                    unit.health = Mathf.Min(unit.maxHealth, unit.health + amount);
                    break;
                case ItemEffectType.PermanentAttack:
                    unit.snackAttackBonus = Mathf.Max(0, unit.snackAttackBonus + amount);
                    unit.attack = Mathf.Max(0, unit.attack + amount);
                    unit.damage = Mathf.Max(0, unit.damage + amount);
                    unit.attack = Mathf.Max(unit.attack, unit.damage);
                    break;
                case ItemEffectType.PermanentDefense:
                    unit.snackDefenseBonus = Mathf.Max(0, unit.snackDefenseBonus + amount);
                    unit.defense = Mathf.Max(0, unit.defense + amount);
                    break;
                default:
                    message = "Unsupported snack effect.";
                    return false;
            }
        }

        unit.health = Mathf.Clamp(unit.health, 1, Mathf.Max(1, unit.maxHealth));
        message = "Applied snack.";
        return true;
    }

    static void ApplyToLiveLeader(GobboController leader, GobboUnitSaveData leaderData, ItemEffectDefinition effect)
    {
        if (leader == null || effect == null) return;
        if (leaderData != null) leaderData.EnsureRuntimeDefaults();
        int amount = effect.GetSafeAmount();
        switch (effect.effectType)
        {
            case ItemEffectType.HealCurrentHp:
                leader.health = Mathf.Min(leader.maxHealth, leader.health + amount);
                break;
            case ItemEffectType.PermanentMaxHp:
                if (leaderData != null)
                    leaderData.snackMaxHealthBonus = Mathf.Max(0, leaderData.snackMaxHealthBonus + amount);
                leader.maxHealth = Mathf.Max(1, leader.maxHealth + amount);
                leader.health = Mathf.Min(leader.maxHealth, leader.health + amount);
                break;
            case ItemEffectType.PermanentAttack:
                if (leaderData != null)
                    leaderData.snackAttackBonus = Mathf.Max(0, leaderData.snackAttackBonus + amount);
                leader.attack = Mathf.Max(0, leader.attack + amount);
                break;
            case ItemEffectType.PermanentDefense:
                if (leaderData != null)
                    leaderData.snackDefenseBonus = Mathf.Max(0, leaderData.snackDefenseBonus + amount);
                leader.defense = Mathf.Max(0, leader.defense + amount);
                break;
        }
    }

    static IEnumerable<ItemEffectDefinition> GetEffects(ItemDefinition item)
    {
        if (item == null || item.effects == null) yield break;
        foreach (ItemEffectDefinition effect in item.effects)
            if (effect != null) yield return effect;
    }

    static GobboController FindLiveLeader()
    {
        GobboController[] controllers = Object.FindObjectsByType<GobboController>(FindObjectsInactive.Exclude);
        foreach (GobboController controller in controllers)
        {
            if (controller != null && controller.CompareTag("Player"))
                return controller;
        }
        return controllers.Length > 0 ? controllers[0] : null;
    }

    public static void RefreshSpawnedBuddy(string buddyId)
    {
        if (string.IsNullOrWhiteSpace(buddyId)) return;
        BuddyUnit[] units = Object.FindObjectsByType<BuddyUnit>(FindObjectsInactive.Exclude);
        foreach (BuddyUnit unit in units)
        {
            if (unit == null || unit.unitData == null) continue;
            if (unit.unitData.uniqueId != buddyId) continue;
            unit.ApplyStats();
            unit.ApplyVisuals();
        }
    }

    static string BuildPreviewMessage(SnackEffectPreview preview)
    {
        List<string> parts = new List<string>();
        if (preview.healingAmount > 0) parts.Add("Heal " + preview.healingAmount + " HP");
        if (preview.maxHealthIncrease > 0) parts.Add("Max HP +" + preview.maxHealthIncrease);
        if (preview.attackIncrease > 0) parts.Add("Attack +" + preview.attackIncrease);
        if (preview.defenseIncrease > 0) parts.Add("Defense +" + preview.defenseIncrease);
        return parts.Count > 0 ? string.Join(", ", parts) : "No stat change";
    }
}
