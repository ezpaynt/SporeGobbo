using UnityEngine;

public enum FoodKind { Food, Mushroom, Meat, HealingMeat }

public class FoodItem : MonoBehaviour
{
    [Header("Food / XP")]
    public int xpValue = 1;
    public int foodValue = 1;
    public string foodName = "Food";

    [Header("Healing")]
    public bool healsPlayer = false;
    public int healAmount = 0;

    [Header("Run Tracking")]
    public FoodKind foodKind = FoodKind.Food;
    public int mushroomValue = 1;
    public int moneyValue = 0;
    public int shinyValue = 0;

    public void Eat(GobboController gobbo)
    {
        if (gobbo == null) return;
        int heal = healsPlayer ? Mathf.Max(0, healAmount) : 0;
        gobbo.EatFood(xpValue, heal, foodValue);
        RegisterCollection();
        Debug.Log("Ate " + foodName + " for " + xpValue + " XP and " + foodValue + " horde food.");
        Destroy(gameObject);
    }

    void RegisterCollection()
    {
        if (GameState.Instance == null) return;
        FoodKind resolvedKind = ResolveFoodKind();
        if (resolvedKind == FoodKind.Mushroom)
        {
            int amount = Mathf.Max(1, mushroomValue);
            CampResourceService.Add(GameState.Instance, CampResourceType.Mushrooms, amount, false);
        }

        int shinyAmount = shinyValue + moneyValue;
        if (shinyAmount > 0)
            CampResourceService.Add(GameState.Instance, CampResourceType.Shinies, shinyAmount, false);
    }

    FoodKind ResolveFoodKind()
    {
        if (foodKind != FoodKind.Food) return foodKind;
        if (!string.IsNullOrWhiteSpace(foodName))
        {
            string lower = foodName.ToLowerInvariant();
            if (lower.Contains("mushroom") || lower.Contains("shroom")) return FoodKind.Mushroom;
            if (lower.Contains("healing") && (lower.Contains("meat") || lower.Contains("flesh"))) return FoodKind.HealingMeat;
            if (lower.Contains("meat") || lower.Contains("flesh")) return FoodKind.Meat;
        }
        return foodKind;
    }
}
