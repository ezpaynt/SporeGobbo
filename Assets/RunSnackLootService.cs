using System;
using System.Collections.Generic;
using UnityEngine;

public enum RunSnackFinalizationReason
{
    Success,
    Retreat,
    Death
}

public static class RunSnackLootService
{
    public static event Action RunSnackLootChanged;

    public static bool AddRunSnack(GameState state, ItemDefinition item, int quantity)
    {
        if (state == null || item == null || quantity <= 0) return false;
        if (!IsRunSnack(item)) return false;
        return AddRunSnackById(state, item.NormalizedId, quantity);
    }

    public static bool AddRunSnackById(GameState state, string itemId, int quantity)
    {
        if (state == null || quantity <= 0) return false;
        string normalized = ItemIdUtility.Normalize(itemId);
        if (string.IsNullOrWhiteSpace(normalized)) return false;

        NormalizeRunSnacks(state);
        InventoryStackSaveData stack = FindStack(state.runSnackStacks, normalized);
        if (stack == null)
        {
            stack = new InventoryStackSaveData(normalized, 0);
            state.runSnackStacks.Add(stack);
        }

        stack.quantity = Mathf.Max(0, stack.quantity + quantity);
        NormalizeRunSnacks(state);
        RunSnackLootChanged?.Invoke();
        return true;
    }

    public static IReadOnlyList<InventoryStackSaveData> GetRunStacks(GameState state)
    {
        if (state == null) return new List<InventoryStackSaveData>();
        NormalizeRunSnacks(state);
        return InventoryService.CloneStacks(state.runSnackStacks);
    }

    public static void BeginRun(GameState state)
    {
        if (state == null) return;
        state.runSnackStacks ??= new List<InventoryStackSaveData>();
        state.runSnackStacks.Clear();
        state.runSnackFinalizedThisRun = false;
        ClearSnackSummary(state);
        RunSnackLootChanged?.Invoke();
        Debug.Log("[RunSnackLootService] Run snack loot reset for a new run.");
    }

    public static bool FinalizeSuccess(GameState state)
    {
        return FinalizeRunSnacks(state, RunSnackFinalizationReason.Success, 0f);
    }

    public static bool FinalizeRetreat(GameState state, float retreatSnackLossPercent)
    {
        return FinalizeRunSnacks(state, RunSnackFinalizationReason.Retreat, retreatSnackLossPercent);
    }

    public static bool FinalizeDeath(GameState state)
    {
        return FinalizeRunSnacks(state, RunSnackFinalizationReason.Death, 1f);
    }

    public static bool FinalizeRunSnacks(GameState state, RunSnackFinalizationReason reason, float retreatSnackLossPercent)
    {
        if (state == null) return false;
        state.EnsureRuntimeDefaults();

        if (state.runSnackFinalizedThisRun)
        {
            Debug.Log("[RunSnackLootService] Ignored duplicate snack finalization: " + reason);
            return false;
        }

        state.runSnackFinalizedThisRun = true;
        NormalizeRunSnacks(state);
        ClearSnackSummary(state);

        float lossPercent = reason == RunSnackFinalizationReason.Retreat
            ? Mathf.Clamp01(retreatSnackLossPercent)
            : reason == RunSnackFinalizationReason.Death ? 1f : 0f;

        foreach (InventoryStackSaveData stack in InventoryService.CloneStacks(state.runSnackStacks))
        {
            if (stack == null || stack.quantity <= 0) continue;

            int collected = Mathf.Max(0, stack.quantity);
            int lost = reason == RunSnackFinalizationReason.Success
                ? 0
                : Mathf.Clamp(Mathf.CeilToInt(collected * lossPercent), 0, collected);
            int retained = Mathf.Max(0, collected - lost);

            if (retained > 0)
                InventoryService.TryAddById(state, stack.itemId, retained, ItemDatabase.Get(stack.itemId), false);

            state.lastRun.snackSummaryEntries.Add(new RunSnackSummaryEntry(stack.itemId, collected, lost, retained));
        }

        state.runSnackStacks.Clear();
        AdvanceCampCycle(state);
        RunSnackLootChanged?.Invoke();
        Debug.Log("[RunSnackLootService] Finalized snacks for " + reason + ". Camp cycle is now " + state.campCycleNumber + ".");
        return true;
    }

    public static void NormalizeRunSnacks(GameState state)
    {
        if (state == null) return;
        state.runSnackStacks = InventoryService.NormalizeStacks(state.runSnackStacks, false);
    }

    public static void ClearRunSnacks(GameState state)
    {
        if (state == null) return;
        state.runSnackStacks ??= new List<InventoryStackSaveData>();
        state.runSnackStacks.Clear();
        RunSnackLootChanged?.Invoke();
    }

    public static bool IsRunSnack(ItemDefinition item)
    {
        return item != null &&
               item.persistence == ItemPersistence.Persistent &&
               item.category == ItemCategory.Food &&
               item.HasTrait(ItemTrait.CampfireSnack);
    }

    public static int CalculateRetreatLoss(int collectedQuantity, float retreatSnackLossPercent)
    {
        int collected = Mathf.Max(0, collectedQuantity);
        float percent = Mathf.Clamp01(retreatSnackLossPercent);
        return Mathf.Clamp(Mathf.CeilToInt(collected * percent), 0, collected);
    }

    static void AdvanceCampCycle(GameState state)
    {
        if (state == null) return;
        state.campCycleNumber = Mathf.Max(0, state.campCycleNumber) + 1;
    }

    static void ClearSnackSummary(GameState state)
    {
        if (state == null) return;
        if (state.lastRun == null) state.lastRun = new RunSummaryData();
        state.lastRun.snackSummaryEntries ??= new List<RunSnackSummaryEntry>();
        state.lastRun.snackSummaryEntries.Clear();
    }

    static InventoryStackSaveData FindStack(List<InventoryStackSaveData> stacks, string itemId)
    {
        if (stacks == null) return null;
        foreach (InventoryStackSaveData stack in stacks)
            if (stack != null && stack.itemId == itemId) return stack;
        return null;
    }
}
