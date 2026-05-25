using System.Collections;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 3;
    public int health = 3;

    [Header("Drops")]
    public GameObject foodDropPrefab;
    public GameObject healingMeatDropPrefab;
    [Range(0f, 1f)] public float healingMeatChance = 0.15f;
    public int xpDropValue = 3;
    public int foodDropValue = 3;
    public int healingMeatHealAmount = 10;

    [Header("Run Scaling")]
    public bool scaleWithRunNumber = true;
    public int healthPerRun = 2;
    public int xpPerRun = 1;

    [Header("Death Visuals")]
    public GameObject splatPrefab;

    [Header("Hit Flash")]
    public SpriteRenderer spriteRenderer;
    public Color hitColor = Color.red;
    public float flashTime = 0.08f;

    private Color originalColor;
    private bool isDead = false;

    void Start()
    {
        ApplyRunScaling();
        health = maxHealth;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;
    }

    void ApplyRunScaling()
    {
        if (!scaleWithRunNumber || GameState.Instance == null)
            return;

        int extraRuns = Mathf.Max(0, GameState.Instance.currentRunNumber - 1);
        maxHealth += extraRuns * healthPerRun;
        xpDropValue += extraRuns * xpPerRun;
        foodDropValue += extraRuns * xpPerRun;
    }

    public void TakeDamage(int amount)
    {
        if (isDead) return;

        health -= amount;

        Debug.Log(gameObject.name + " took " + amount + " damage. HP: " + health);

        if (spriteRenderer != null)
        {
            StopAllCoroutines();
            StartCoroutine(FlashRed());
        }

        if (health <= 0)
            Die();
    }

    IEnumerator FlashRed()
    {
        spriteRenderer.color = hitColor;
        yield return new WaitForSeconds(flashTime);
        spriteRenderer.color = originalColor;
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        if (GameState.Instance != null)
            GameState.Instance.RegisterEnemyKilled();

        SpawnSplat();
        SpawnFood();

        Destroy(gameObject);
    }

    void SpawnSplat()
    {
        if (splatPrefab == null) return;

        GameObject splat = Instantiate(splatPrefab, transform.position, Quaternion.identity);
        splat.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
    }

    void SpawnFood()
    {
        GameObject prefab = foodDropPrefab;
        bool healing = Random.value < healingMeatChance && healingMeatDropPrefab != null;

        if (healing)
            prefab = healingMeatDropPrefab;

        if (prefab == null) return;

        Vector2 dropOffset = Random.insideUnitCircle * 0.25f;
        GameObject food = Instantiate(prefab, (Vector2)transform.position + dropOffset, Quaternion.identity);

        FoodItem foodItem = food.GetComponent<FoodItem>();

        if (foodItem != null)
        {
            foodItem.xpValue = xpDropValue;
            foodItem.foodValue = foodDropValue;

            if (healing)
            {
                foodItem.foodKind = FoodKind.HealingMeat;
                foodItem.foodName = "Healing Weevil Meat";
                foodItem.healsPlayer = true;
                foodItem.healAmount = healingMeatHealAmount;
            }
            else
            {
                foodItem.foodKind = FoodKind.Meat;
                foodItem.foodName = "Weevil Meat";
                foodItem.healsPlayer = false;
                foodItem.healAmount = 0;
            }
        }
    }
}
