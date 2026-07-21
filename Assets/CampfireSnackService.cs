using System.Collections.Generic;
using UnityEngine;

public class CampfireSnackResult
{
    public bool success;
    public string message = "";
    public string itemId = "";
    public string targetId = "";
    public ItemTargetType targetType = ItemTargetType.None;
    public SnackEffectPreview preview;

    public static CampfireSnackResult Fail(string message)
    {
        return new CampfireSnackResult { success = false, message = message };
    }
}

public static class CampfireSnackService
{
    private static bool transactionInProgress;

    public static IEnumerable<ItemDefinition> EnumerateOwnedCampfireSnacks(GameState state)
    {
        foreach (ItemDefinition definition in InventoryService.EnumerateOwnedDefinitions(state, ItemCategory.Food, ItemTrait.CampfireSnack))
        {
            if (IsValidCampfireSnack(definition))
                yield return definition;
        }
    }

    public static CampfireSnackResult BuildPreview(GameState state, string itemId, ItemTargetType targetType, string targetId)
    {
        CampfireSnackResult validation = ValidateUse(state, itemId, targetType, targetId);
        if (!validation.success) return validation;

        ItemDefinition definition = ItemDatabase.Get(itemId);
        validation.preview = SnackEffectService.BuildPreview(state, definition, targetType, targetId);
        validation.success = validation.preview != null && validation.preview.canApply;
        validation.message = validation.preview != null ? validation.preview.message : "Could not preview snack.";
        return validation;
    }

    public static CampfireSnackResult CommitFeeding(GameState state, string itemId, ItemTargetType targetType, string targetId)
    {
        if (transactionInProgress)
            return CampfireSnackResult.Fail("Snack use is already in progress.");

        transactionInProgress = true;
        List<InventoryStackSaveData> inventoryBefore = null;
        GobboUnitSaveData targetBefore = null;

        try
        {
            CampfireSnackResult validation = ValidateUse(state, itemId, targetType, targetId);
            if (!validation.success) return validation;

            ItemDefinition definition = ItemDatabase.Get(itemId);
            GobboUnitSaveData target = SnackEffectService.ResolveTargetData(state, targetType, targetId);
            if (target == null) return CampfireSnackResult.Fail("Target could not be resolved.");
            SnackEffectPreview previewBeforeCommit = SnackEffectService.BuildPreview(state, definition, targetType, targetId);

            SyncLeaderBeforeSnapshot(state, targetType);
            target = SnackEffectService.ResolveTargetData(state, targetType, targetId);
            targetBefore = target != null ? target.CloneUnit() : null;
            inventoryBefore = InventoryService.CloneStacks(state.itemStacks);

            string effectMessage;
            if (!SnackEffectService.ApplyEffects(state, definition, targetType, targetId, out effectMessage))
                return CampfireSnackResult.Fail(effectMessage);

            target = SnackEffectService.ResolveTargetData(state, targetType, targetId);
            if (target == null)
            {
                Rollback(state, targetType, targetId, targetBefore, inventoryBefore);
                return CampfireSnackResult.Fail("Target disappeared during snack use.");
            }

            target.lastSnackCampCycle = Mathf.Max(0, state.campCycleNumber);

            if (definition.consumeOnUse && !InventoryService.TryRemove(state, definition.NormalizedId, 1, false))
            {
                Rollback(state, targetType, targetId, targetBefore, inventoryBefore);
                return CampfireSnackResult.Fail("Snack could not be consumed.");
            }

            SporeSaveManager.SaveCurrentSlotFromGameState();
            CampfireSnackResult result = new CampfireSnackResult();
            result.success = true;
            result.message = effectMessage;
            result.itemId = definition.NormalizedId;
            result.targetType = targetType;
            result.targetId = target != null ? target.uniqueId : targetId;
            result.preview = previewBeforeCommit;
            return result;
        }
        finally
        {
            transactionInProgress = false;
        }
    }

    public static bool CanTargetEat(GobboUnitSaveData target, GameState state)
    {
        if (target == null || state == null) return false;
        return target.lastSnackCampCycle != Mathf.Max(0, state.campCycleNumber);
    }

    static CampfireSnackResult ValidateUse(GameState state, string itemId, ItemTargetType targetType, string targetId)
    {
        if (state == null) return CampfireSnackResult.Fail("Missing game state.");
        state.EnsureRuntimeDefaults();

        ItemDefinition definition = ItemDatabase.Get(itemId);
        if (definition == null) return CampfireSnackResult.Fail("Snack definition is missing.");
        if (!IsValidCampfireSnack(definition)) return CampfireSnackResult.Fail("Item is not a campfire snack.");
        if (!InventoryService.Has(state, definition.NormalizedId, 1)) return CampfireSnackResult.Fail("No snack available.");
        if (!definition.CanTarget(targetType)) return CampfireSnackResult.Fail("Snack cannot target that character.");

        GobboUnitSaveData target = SnackEffectService.ResolveTargetData(state, targetType, targetId);
        if (target == null) return CampfireSnackResult.Fail("Target is missing.");
        if (targetType == ItemTargetType.Buddy && !IsActiveBuddy(state, target.uniqueId))
            return CampfireSnackResult.Fail("Reserve buddies cannot eat campfire snacks yet.");
        if (!CanTargetEat(target, state))
            return CampfireSnackResult.Fail("That character already ate this camp cycle.");

        return new CampfireSnackResult
        {
            success = true,
            message = "Snack can be used.",
            itemId = definition.NormalizedId,
            targetType = targetType,
            targetId = target.uniqueId
        };
    }

    static bool IsValidCampfireSnack(ItemDefinition definition)
    {
        return definition != null &&
               definition.category == ItemCategory.Food &&
               definition.persistence == ItemPersistence.Persistent &&
               definition.HasTrait(ItemTrait.CampfireSnack) &&
               definition.CanUseAt(ItemUseLocation.Campfire) &&
               definition.effects != null &&
               definition.effects.Count > 0;
    }

    static bool IsActiveBuddy(GameState state, string buddyId)
    {
        if (state == null || string.IsNullOrWhiteSpace(buddyId)) return false;
        List<GobboUnitSaveData> active = state.GetActiveSquadUnits();
        foreach (GobboUnitSaveData buddy in active)
            if (buddy != null && buddy.uniqueId == buddyId) return true;
        return false;
    }

    static void SyncLeaderBeforeSnapshot(GameState state, ItemTargetType targetType)
    {
        if (state == null || targetType != ItemTargetType.Leader) return;
        GobboController[] controllers = Object.FindObjectsByType<GobboController>(FindObjectsInactive.Exclude);
        foreach (GobboController controller in controllers)
        {
            if (controller != null && controller.CompareTag("Player"))
            {
                state.SavePlayer(controller);
                return;
            }
        }
    }

    static void Rollback(GameState state, ItemTargetType targetType, string targetId, GobboUnitSaveData targetBefore, List<InventoryStackSaveData> inventoryBefore)
    {
        if (state == null) return;
        if (inventoryBefore != null) state.itemStacks = InventoryService.CloneStacks(inventoryBefore);

        GobboUnitSaveData target = SnackEffectService.ResolveTargetData(state, targetType, targetId);
        if (target != null && targetBefore != null)
            targetBefore.CopyInto(target);

        if (targetType == ItemTargetType.Leader)
        {
            GobboController[] controllers = Object.FindObjectsByType<GobboController>(FindObjectsInactive.Exclude);
            foreach (GobboController controller in controllers)
            {
                if (controller != null && controller.CompareTag("Player"))
                {
                    state.ApplyToPlayer(controller);
                    break;
                }
            }
        }
        else if (targetBefore != null)
        {
            SnackEffectService.RefreshSpawnedBuddy(targetBefore.uniqueId);
        }
    }
}
