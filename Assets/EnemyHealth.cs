using System.Collections;
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IEnemyDamageReceiver
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
        ReceiveEnemyDamage(new EnemyDamageRequest(amount));
    }

    public EnemyDamageResult ReceiveEnemyDamage(EnemyDamageRequest request)
    {
        if (isDead || request == null || request.amount<=0) return EnemyDamageResult.Reject(this,EnemyDamageFailure.Dead);

        int before=health; int applied=EnemyDamage.Constrain(this,request,request.amount);
        if(applied<=0)return EnemyDamageResult.Reject(this,EnemyDamageFailure.Protected);
        health -= applied;

        Debug.Log(gameObject.name + " took " + request.amount + " damage. HP: " + health);

        if (spriteRenderer != null)
        {
            StopAllCoroutines();
            StartCoroutine(FlashRed());
        }

        if (health <= 0)
            Die(request.gobboSourceId);
        return EnemyDamage.Receipt(this,before,health,request);
    }

    IEnumerator FlashRed()
    {
        spriteRenderer.color = hitColor;
        yield return new WaitForSeconds(flashTime);
        spriteRenderer.color = originalColor;
    }

    void Die(string killerGobboId)
    {
        if (isDead) return;
        isDead = true;

        if (GameState.Instance != null)
        {
            if (string.IsNullOrWhiteSpace(killerGobboId)) GameState.Instance.RegisterEnemyKilled();
            else GameState.Instance.RegisterWeevilKilled(killerGobboId);
        }

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
                foodItem.foodName = "Healing Weevil Parts";
                foodItem.healsPlayer = true;
                foodItem.healAmount = healingMeatHealAmount;
            }
            else
            {
                foodItem.foodKind = FoodKind.Meat;
                foodItem.foodName = "Weevil Parts";
                foodItem.healsPlayer = false;
                foodItem.healAmount = 0;
            }
        }
    }
}
