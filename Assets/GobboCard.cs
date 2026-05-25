using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GobboCard
{
    [Header("Identity")]
    public string cardId = "";
    public string cardName = "Unnamed Card";
    [TextArea(2, 5)] public string description = "";

    [Header("Filtering")]
    public bool playerAllowed = true;
    public bool buddyAllowed = true;
    public bool isEvolutionCard = false;
    public bool isTypeChoiceCard = false;
    public bool campShopAllowed = false;
    public bool fightPitRewardAllowed = false;
    public bool shadyDealAllowed = false;
    public int minLevel = 1;
    public BuddyType requiredType = BuddyType.Baby;
    public bool requiresSpecificType = false;
    public GobboAgeStage requiredAgeStage = GobboAgeStage.Baby;
    public bool requiresSpecificAgeStage = false;

    [Header("Evolution / Visual")]
    public BuddyType setType = BuddyType.Baby;
    public bool changesType = false;
    public GobboAgeStage setAgeStage = GobboAgeStage.Baby;
    public bool changesAgeStage = false;
    public string setVisualSetId = "";

    [Header("Basic Stats")]
    public int maxHealthBonus = 0;
    public int healthBonus = 0;
    public int attackBonus = 0;
    public int defenseBonus = 0;
    public int digPowerBonus = 0;

    public float moveSpeedBonus = 0f;
    public float attackCooldownBonus = 0f;
    public float attackRangeBonus = 0f;
    public float attackRadiusBonus = 0f;
    public float dashCooldownBonus = 0f;
    public float dashSpeedBonus = 0f;
    public float dashDurationBonus = 0f;
    public float critChanceBonus = 0f;
    public float critDamageBonus = 0f;
    public float knockbackBonus = 0f;

    [Header("Buddy Behavior")]
    public bool setCollectsFood = false;
    public bool collectsFoodValue = false;
    public bool setOnlyFightsAfterHit = false;
    public bool onlyFightsAfterHitValue = false;

    [Header("Unlocks")]
    public bool unlockSporeMend = false;
    public bool unlockDashBite = false;
    public string unlockCosmeticId = "";
    public string unlockItemId = "";

    public bool CanAppearForPlayer(GobboController gobbo, GobboCardContext context)
    {
        if (!playerAllowed || gobbo == null)
            return false;

        if (gobbo.level < minLevel)
            return false;

        if (!ContextAllowed(context))
            return false;

        if (requiresSpecificType && gobbo.gobboType != requiredType)
            return false;

        if (requiresSpecificAgeStage && gobbo.ageStage != requiredAgeStage)
            return false;

        if (unlockSporeMend && gobbo.hasSporeMend)
            return false;

        if (unlockDashBite && gobbo.hasDashBite)
            return false;

        if (gobbo.chosenCardIds != null && gobbo.chosenCardIds.Contains(cardId))
            return false;

        return true;
    }

    public bool CanAppearForBuddy(BuddyData buddy, GobboCardContext context)
    {
        if (!buddyAllowed || buddy == null)
            return false;

        if (buddy.level < minLevel)
            return false;

        if (!ContextAllowed(context))
            return false;

        if (requiresSpecificType && buddy.buddyType != requiredType)
            return false;

        if (requiresSpecificAgeStage && buddy.ageStage != requiredAgeStage)
            return false;

        if (buddy.chosenCardIds != null && buddy.chosenCardIds.Contains(cardId))
            return false;

        return true;
    }

    bool ContextAllowed(GobboCardContext context)
    {
        switch (context)
        {
            case GobboCardContext.RunLevelUp:
                return !isEvolutionCard && !campShopAllowed && !fightPitRewardAllowed && !shadyDealAllowed;
            case GobboCardContext.EvolutionChoice:
                return isEvolutionCard || isTypeChoiceCard;
            case GobboCardContext.CampShop:
                return campShopAllowed;
            case GobboCardContext.FightPitReward:
                return fightPitRewardAllowed;
            case GobboCardContext.ShadyDeal:
                return shadyDealAllowed;
        }

        return true;
    }

    public void ApplyToPlayer(GobboController gobbo)
    {
        if (gobbo == null)
            return;

        if (changesType)
            gobbo.gobboType = setType;

        if (changesAgeStage)
            gobbo.ageStage = setAgeStage;

        if (!string.IsNullOrWhiteSpace(setVisualSetId))
            gobbo.visualSetId = setVisualSetId;

        gobbo.maxHealth += maxHealthBonus;
        gobbo.health = Mathf.Min(gobbo.maxHealth, gobbo.health + Mathf.Max(healthBonus, maxHealthBonus));
        gobbo.attack += attackBonus;
        gobbo.defense += defenseBonus;
        gobbo.digPower += digPowerBonus;

        gobbo.moveSpeed = Mathf.Max(1f, gobbo.moveSpeed + moveSpeedBonus);
        gobbo.attackCooldown = Mathf.Max(0.15f, gobbo.attackCooldown + attackCooldownBonus);
        gobbo.attackRange += attackRangeBonus;
        gobbo.attackRadius += attackRadiusBonus;
        gobbo.dashCooldown = Mathf.Max(0.15f, gobbo.dashCooldown + dashCooldownBonus);
        gobbo.dashSpeed = Mathf.Max(1f, gobbo.dashSpeed + dashSpeedBonus);
        gobbo.dashDuration = Mathf.Max(0.04f, gobbo.dashDuration + dashDurationBonus);
        gobbo.critChance = Mathf.Clamp01(gobbo.critChance + critChanceBonus);
        gobbo.critDamageMultiplier = Mathf.Max(1f, gobbo.critDamageMultiplier + critDamageBonus);
        gobbo.knockbackForce = Mathf.Max(0f, gobbo.knockbackForce + knockbackBonus);

        if (unlockSporeMend)
            gobbo.hasSporeMend = true;

        if (unlockDashBite)
            gobbo.hasDashBite = true;

        if (gobbo.chosenCardIds != null && !string.IsNullOrWhiteSpace(cardId) && !gobbo.chosenCardIds.Contains(cardId))
            gobbo.chosenCardIds.Add(cardId);

        if (isEvolutionCard || isTypeChoiceCard)
            gobbo.ClearPendingEvolutionIfCurrentLevelHandled();

        if (GameState.Instance != null)
        {
            GameState.Instance.RegisterUpgradeChosen(cardName);

            if (!string.IsNullOrWhiteSpace(unlockCosmeticId))
                GameState.Instance.RegisterCosmeticUnlocked(unlockCosmeticId);

            if (!string.IsNullOrWhiteSpace(unlockItemId))
                GameState.Instance.RegisterItemUnlocked(unlockItemId);
        }

        gobbo.RefreshAfterSaveLoad();
    }

    public void ApplyToBuddy(BuddyData buddy)
    {
        if (buddy == null)
            return;

        if (changesType)
            buddy.buddyType = setType;

        if (changesAgeStage)
            buddy.ageStage = setAgeStage;

        if (!string.IsNullOrWhiteSpace(setVisualSetId))
            buddy.visualSetId = setVisualSetId;

        buddy.maxHealth += maxHealthBonus;
        buddy.health = Mathf.Min(buddy.maxHealth, buddy.health + Mathf.Max(healthBonus, maxHealthBonus));
        buddy.damage += attackBonus;
        buddy.defense += defenseBonus;
        buddy.moveSpeed = Mathf.Max(1f, buddy.moveSpeed + moveSpeedBonus);
        buddy.attackCooldown = Mathf.Max(0.15f, buddy.attackCooldown + attackCooldownBonus);

        if (setCollectsFood)
            buddy.collectsFood = collectsFoodValue;

        if (setOnlyFightsAfterHit)
            buddy.onlyFightsAfterHit = onlyFightsAfterHitValue;

        if (buddy.chosenCardIds == null)
            buddy.chosenCardIds = new List<string>();

        if (!string.IsNullOrWhiteSpace(cardId) && !buddy.chosenCardIds.Contains(cardId))
            buddy.chosenCardIds.Add(cardId);

        if (isEvolutionCard || isTypeChoiceCard)
        {
            buddy.pendingEvolution = false;
            buddy.runsWaitingForEvolution = 0;
            buddy.evolutionLevelWaiting = 0;
        }
    }
}
