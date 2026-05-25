using System.Collections;
using UnityEngine;

public class BuddyUnit : MonoBehaviour
{
    [Header("Runtime Data")]
    public BuddyData data;

    [Header("References")]
    public BuddyFollow follow;
    public BuddyCombat combat;
    public BuddyScavenger scavenger;
    public SpriteRenderer spriteRenderer;
    public GobboVisualController visualController;

    [Header("Damage Visuals")]
    public Color hurtColor = Color.red;
    public Color poisonColor = new Color(0.6f, 1f, 0.25f);
    public float flashTime = 0.08f;

    private Color originalColor;
    private bool initialized = false;
    private bool dead = false;

    void Awake()
    {
        if (follow == null)
            follow = GetComponent<BuddyFollow>();

        if (combat == null)
            combat = GetComponent<BuddyCombat>();

        if (scavenger == null)
            scavenger = GetComponent<BuddyScavenger>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (visualController == null)
            visualController = GetComponent<GobboVisualController>();

        if (visualController == null)
            visualController = GetComponentInChildren<GobboVisualController>();

        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;
    }

    public void Initialize(BuddyData newData)
    {
        data = newData;

        if (data != null)
            data.EnsureId();

        ApplyStats();
        ApplyVisuals();

        initialized = true;
    }

    public void ApplyStats()
    {
        if (data == null)
            return;

        if (follow != null)
            follow.followSpeed = data.moveSpeed;

        if (combat != null)
        {
            combat.damage = data.damage;
            combat.attackCooldown = data.attackCooldown;
        }

        if (scavenger != null)
            scavenger.enabled = data.collectsFood;
    }

    public void ApplyVisuals()
    {
        if (data == null)
            return;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = data.bodyColor;
            originalColor = spriteRenderer.color;
        }

        if (visualController != null)
            visualController.ApplyIdentity(data.buddyType, data.ageStage, data.visualSetId);

        transform.localScale = GetScaleForType(data.buddyType, data.ageStage);
    }

    Vector3 GetScaleForType(BuddyType type, GobboAgeStage stage)
    {
        float stageBonus = 1f;

        switch (stage)
        {
            case GobboAgeStage.Baby: stageBonus = 0.75f; break;
            case GobboAgeStage.Young: stageBonus = 1f; break;
            case GobboAgeStage.Stage1: stageBonus = 1.05f; break;
            case GobboAgeStage.Stage2: stageBonus = 1.12f; break;
            case GobboAgeStage.Stage3: stageBonus = 1.22f; break;
            case GobboAgeStage.Stage4: stageBonus = 1.35f; break;
            case GobboAgeStage.NeglectedElder: stageBonus = 1.2f; break;
        }

        switch (type)
        {
            case BuddyType.Fat:
            case BuddyType.Tank:
                return new Vector3(1.2f * stageBonus, 1.2f * stageBonus, 1f);

            case BuddyType.Fast:
                return new Vector3(0.9f * stageBonus, 0.9f * stageBonus, 1f);

            case BuddyType.Baby:
                return new Vector3(0.75f, 0.75f, 1f);
        }

        return new Vector3(stageBonus, stageBonus, 1f);
    }

    public void TakeDamage(int amount)
    {
        if (!initialized || dead || data == null)
            return;

        int finalDamage = Mathf.Max(1, amount - data.defense);
        data.health -= finalDamage;
        data.hasBeenHit = true;

        Flash(hurtColor);

        if (data.health <= 0)
            Die();
    }

    public void HealFlat(int amount)
    {
        if (!initialized || dead || data == null || amount <= 0)
            return;

        data.health = Mathf.Min(data.maxHealth, data.health + amount);
    }

    public void HealPercent(float percent)
    {
        if (!initialized || dead || data == null || percent <= 0f)
            return;

        int amount = Mathf.Max(1, Mathf.RoundToInt(data.maxHealth * percent));
        HealFlat(amount);
    }

    public void ApplyPoison(int damagePerTick, float duration, float tickRate)
    {
        if (!initialized || dead || data == null)
            return;

        StartCoroutine(PoisonRoutine(damagePerTick, duration, tickRate));
    }

    public void TakePoison(int damagePerTick, float duration, float tickRate)
    {
        ApplyPoison(damagePerTick, duration, tickRate);
    }

    IEnumerator PoisonRoutine(int damagePerTick, float duration, float tickRate)
    {
        float timer = 0f;

        while (timer < duration && !dead && data != null)
        {
            data.health -= Mathf.Max(1, damagePerTick);
            data.hasBeenHit = true;

            Flash(poisonColor);

            if (data.health <= 0)
            {
                Die();
                yield break;
            }

            yield return new WaitForSeconds(tickRate);
            timer += tickRate;
        }
    }

    void Flash(Color color)
    {
        if (spriteRenderer == null)
            return;

        if (visualController != null)
            visualController.SetAnimationState(GobboAnimationState.Hurt);

        StopCoroutine(nameof(FlashRoutine));
        StartCoroutine(FlashRoutine(color));
    }

    IEnumerator FlashRoutine(Color color)
    {
        spriteRenderer.color = color;
        yield return new WaitForSeconds(flashTime);

        if (spriteRenderer != null)
            spriteRenderer.color = originalColor;

        if (visualController != null)
            visualController.SetAnimationState(GobboAnimationState.Idle);
    }

    void Die()
    {
        if (dead)
            return;

        dead = true;

        if (visualController != null)
            visualController.SetAnimationState(GobboAnimationState.Death);

        if (data != null)
        {
            data.EnsureId();

            if (GameState.Instance != null)
            {
                GameState.Instance.RegisterBuddyDeath(data);
                GameState.Instance.RemoveBuddy(data.uniqueId);
            }

            BuddyRoster roster = Object.FindAnyObjectByType<BuddyRoster>();

            if (roster != null)
                roster.RemoveBuddy(data.uniqueId);
        }

        Destroy(gameObject);
    }
}
