using System;
using System.Collections.Generic;
using UnityEngine;

public static class InventoryService
{
    public static event Action InventoryChanged;

    public static void Normalize(GameState state)
    {
        if (state == null) return;
        state.itemStacks = NormalizeStacks(state.itemStacks, true);
    }

    public static int GetQuantity(GameState state, string itemId)
    {
        if (state == null) return 0;
        string normalized = ItemIdUtility.Normalize(itemId);
        if (string.IsNullOrWhiteSpace(normalized)) return 0;

        // A query observes the normalized value without repairing canonical stacks.
        long total = 0;
        if (state.itemStacks != null)
            foreach (InventoryStackSaveData stack in state.itemStacks)
                if (stack != null && ItemIdUtility.Normalize(stack.itemId) == normalized && stack.quantity > 0)
                    total = Math.Min(int.MaxValue, total + stack.quantity);
        ItemDefinition definition = ItemDatabase.Get(normalized);
        int maximum = definition != null ? definition.GetMaxStack() : int.MaxValue;
        return Math.Min((int)total, maximum);
    }

    public static bool Has(GameState state, string itemId, int quantity = 1)
    {
        return GetQuantity(state, itemId) >= Mathf.Max(1, quantity);
    }

    public static bool CanAdd(GameState state, ItemDefinition item, int quantity)
    {
        if (state == null || item == null || quantity <= 0) return false;
        string id = item.NormalizedId;
        if (string.IsNullOrWhiteSpace(id)) return false;
        int current = GetQuantity(state, id);
        int maxStack = item.GetMaxStack();
        return maxStack == int.MaxValue || current <= maxStack - quantity;
    }

    public static bool TryAdd(GameState state, ItemDefinition item, int quantity, bool saveImmediately = false)
    {
        if (!CanAdd(state, item, quantity)) return false;
        return TryAddById(state, item.NormalizedId, quantity, item, saveImmediately);
    }

    public static bool TryAddById(GameState state, string itemId, int quantity, ItemDefinition definition = null, bool saveImmediately = false)
    {
        if (state == null || quantity <= 0) return false;
        string normalized = ItemIdUtility.Normalize(itemId);
        if (string.IsNullOrWhiteSpace(normalized)) return false;

        Normalize(state);
        definition ??= ItemDatabase.Get(normalized);
        int maxStack = definition != null ? definition.GetMaxStack() : int.MaxValue;
        int current = GetQuantity(state, normalized);
        if (current > maxStack - quantity) return false;

        InventoryStackSaveData stack = FindStack(state.itemStacks, normalized);
        if (stack == null)
        {
            stack = new InventoryStackSaveData(normalized, 0);
            state.itemStacks.Add(stack);
        }

        stack.quantity = Mathf.Max(0, stack.quantity + quantity);
        Normalize(state);
        if (saveImmediately) SporeSaveManager.SaveCurrentSlotFromGameState();
        NotifyChanged();
        return true;
    }

    public static bool CanRemove(GameState state, string itemId, int quantity)
    {
        return quantity <= 0 || GetQuantity(state, itemId) >= quantity;
    }

    public static bool TryRemove(GameState state, string itemId, int quantity, bool saveImmediately = false)
    {
        if (quantity <= 0) return true;
        if (state == null) return false;

        string normalized = ItemIdUtility.Normalize(itemId);
        if (string.IsNullOrWhiteSpace(normalized)) return false;

        Normalize(state);
        InventoryStackSaveData stack = FindStack(state.itemStacks, normalized);
        if (stack == null || stack.quantity < quantity) return false;

        stack.quantity -= quantity;
        Normalize(state);
        if (saveImmediately) SporeSaveManager.SaveCurrentSlotFromGameState();
        NotifyChanged();
        return true;
    }

    public static IReadOnlyList<InventoryStackSaveData> GetStacks(GameState state)
    {
        if (state == null) return new List<InventoryStackSaveData>();
        return NormalizeStacks(state.itemStacks, true);
    }

    public static IEnumerable<ItemDefinition> EnumerateOwnedDefinitions(GameState state, ItemCategory? category = null, ItemTrait requiredTraits = ItemTrait.None)
    {
        if (state == null) yield break;
        foreach (InventoryStackSaveData stack in GetStacks(state))
        {
            if (stack == null || stack.quantity <= 0) continue;
            ItemDefinition definition = ItemDatabase.Get(stack.itemId);
            if (definition == null) continue;
            if (category.HasValue && definition.category != category.Value) continue;
            if (requiredTraits != ItemTrait.None && !definition.HasTrait(requiredTraits)) continue;
            yield return definition;
        }
    }

    public static List<InventoryStackSaveData> CloneStacks(List<InventoryStackSaveData> source)
    {
        List<InventoryStackSaveData> result = new List<InventoryStackSaveData>();
        if (source == null) return result;

        foreach (InventoryStackSaveData stack in source)
            if (stack != null) result.Add(stack.Clone());

        return result;
    }

    public static List<InventoryStackSaveData> NormalizeStacks(List<InventoryStackSaveData> source, bool respectKnownMaxStack)
    {
        Dictionary<string, int> merged = new Dictionary<string, int>();
        if (source != null)
        {
            foreach (InventoryStackSaveData stack in source)
            {
                if (stack == null) continue;
                string id = ItemIdUtility.Normalize(stack.itemId);
                int quantity = Mathf.Max(0, stack.quantity);
                if (string.IsNullOrWhiteSpace(id) || quantity <= 0) continue;
                if (!merged.ContainsKey(id)) merged[id] = 0;
                merged[id] = Mathf.Max(0, merged[id] + quantity);
            }
        }

        List<InventoryStackSaveData> result = new List<InventoryStackSaveData>();
        foreach (KeyValuePair<string, int> pair in merged)
        {
            int quantity = pair.Value;
            if (respectKnownMaxStack)
            {
                ItemDefinition definition = ItemDatabase.Get(pair.Key);
                if (definition != null)
                    quantity = Mathf.Min(quantity, definition.GetMaxStack());
            }

            if (quantity > 0)
                result.Add(new InventoryStackSaveData(pair.Key, quantity));
        }

        result.Sort((a, b) => string.CompareOrdinal(a.itemId, b.itemId));
        return result;
    }

    static InventoryStackSaveData FindStack(List<InventoryStackSaveData> stacks, string itemId)
    {
        if (stacks == null) return null;
        foreach (InventoryStackSaveData stack in stacks)
            if (stack != null && stack.itemId == itemId) return stack;
        return null;
    }

    static void NotifyChanged()
    {
        InventoryChanged?.Invoke();
    }
}
