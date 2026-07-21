using System;
using System.Collections.Generic;
using UnityEngine;

public enum ItemCategory
{
    Food,
    Consumable,
    Resource,
    Equipment,
    KeyItem
}

[Flags]
public enum ItemTrait
{
    None = 0,
    CampfireSnack = 1 << 0,
    Healing = 1 << 1,
    PermanentStat = 1 << 2
}

[Flags]
public enum ItemUseLocation
{
    None = 0,
    Campfire = 1 << 0,
    Run = 1 << 1,
    Camp = 1 << 2
}

[Flags]
public enum ItemTargetType
{
    None = 0,
    Leader = 1 << 0,
    Buddy = 1 << 1
}

public enum ItemPersistence
{
    Immediate,
    RunOnly,
    Persistent
}

public enum ItemEffectType
{
    HealCurrentHp,
    PermanentMaxHp,
    PermanentAttack,
    PermanentDefense
}

[Serializable]
public class ItemEffectDefinition
{
    public ItemEffectType effectType = ItemEffectType.HealCurrentHp;
    public int amount = 1;

    public int GetSafeAmount()
    {
        return Mathf.Max(0, amount);
    }
}

[CreateAssetMenu(fileName = "ItemDefinition", menuName = "Spore Gobbo/Items/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    [Header("Identity")]
    public string itemId = "";
    public string displayName = "New Item";
    [TextArea(2, 5)] public string description = "";
    public Sprite icon;

    [Header("Classification")]
    public ItemCategory category = ItemCategory.Food;
    public ItemTrait traits = ItemTrait.CampfireSnack;
    public ItemUseLocation useLocations = ItemUseLocation.Campfire;
    public ItemTargetType validTargets = ItemTargetType.Leader | ItemTargetType.Buddy;
    public ItemPersistence persistence = ItemPersistence.Persistent;

    [Header("Stacking / Use")]
    public bool stackable = true;
    public int maxStack = 99;
    public bool confirmationRequired = true;
    public bool consumeOnUse = true;

    [Header("Effects")]
    public List<ItemEffectDefinition> effects = new List<ItemEffectDefinition>();

    public string NormalizedId => ItemIdUtility.Normalize(itemId);

    public bool HasTrait(ItemTrait trait)
    {
        return (traits & trait) == trait;
    }

    public bool CanUseAt(ItemUseLocation location)
    {
        return (useLocations & location) != 0;
    }

    public bool CanTarget(ItemTargetType targetType)
    {
        return (validTargets & targetType) != 0;
    }

    public int GetMaxStack()
    {
        if (!stackable) return 1;
        return maxStack <= 0 ? int.MaxValue : maxStack;
    }

    public string GetDisplayName()
    {
        return string.IsNullOrWhiteSpace(displayName) ? NormalizedId : displayName.Trim();
    }

    void OnValidate()
    {
        itemId = ItemIdUtility.Normalize(itemId);
        if (maxStack < 1) maxStack = 1;
        if (effects == null) effects = new List<ItemEffectDefinition>();
    }
}
