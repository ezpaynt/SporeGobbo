using System;
using UnityEngine;

[Serializable]
public class InventoryStackSaveData
{
    public string itemId = "";
    public int quantity = 0;

    public InventoryStackSaveData() { }

    public InventoryStackSaveData(string itemId, int quantity)
    {
        this.itemId = ItemIdUtility.Normalize(itemId);
        this.quantity = Mathf.Max(0, quantity);
    }

    public InventoryStackSaveData Clone()
    {
        return new InventoryStackSaveData(itemId, quantity);
    }
}

public static class ItemIdUtility
{
    public static string Normalize(string itemId)
    {
        return string.IsNullOrWhiteSpace(itemId) ? "" : itemId.Trim().ToLowerInvariant();
    }
}
