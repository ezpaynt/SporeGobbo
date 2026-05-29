using System.Collections;
using UnityEngine;

public class BuddyUnit : MonoBehaviour
{
    [Header("Runtime Data")]
    public GobboUnitSaveData unitData;

    [Tooltip("Temporary compatibility mirror for older systems that still read BuddyUnit.data.")]
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
        if (follow == null) follow = GetComponent<BuddyFollow>();
        if (combat == null) combat = GetComponent<BuddyCombat>();
        if (scavenger == null) scavenger = GetComponent<BuddyScavenger>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (visualController == null) visualController = GetComponent<GobboVisualController>();
        if (visualController == null) visualController = GetComponentInChildren<GobboVisualController>();
        if (spriteRenderer != null) originalColor = spriteRenderer.color;
    }

    public void Initialize(GobboUnitSaveData newData)
    {
        unitData = newData;
        if (unitData != null)
        {
            unitData.EnsureRuntimeDefaults();
            data = unitData as BuddyData ?? BuddyData.FromUnit(unitData);
            if (data != null) data.EnsureRuntimeDefaults();
        }

        ApplyStats();
        ApplyVisuals();
        initialized = true;
    }

    public void Initialize(BuddyData newData)
    {
        Initialize((GobboUnitSaveData)newData);
    }

    GobboUnitSaveData Data => unitData != null ? unitData : data;

    void SyncCompatFromUnit()
    {
        if (unitData == null || data == null || ReferenceEquals(unitData, data)) return;
        unitData.CopyInto(data);
        data.EnsureRuntimeDefaults();
    }

    public void ApplyStats()
    {
        GobboUnitSaveData d = Data;
        if (d == null) return;
        d.EnsureRuntimeDefaults();
        if (follow != null) follow.followSpeed = d.moveSpeed;
        if (combat != null)
        {
            combat.damage = Mathf.Max(1, d.damage > 0 ? d.damage : d.attack);
            combat.attackCooldown = d.attackCooldown;
        }
        if (scavenger != null) scavenger.enabled = d.collectsFood;
        SyncCompatFromUnit();
    }

    public void ApplyVisuals()
    {
        GobboUnitSaveData d = Data;
        if (d == null) return;
        d.EnsureRuntimeDefaults();
        if (spriteRenderer != null)
        {
            spriteRenderer.color = d.bodyColor;
            originalColor = spriteRenderer.color;
        }
        if (visualController != null) visualController.ApplyIdentity(d.gobboType, d.ageStage, d.visualSetId);
        transform.localScale = GetScaleForType(d.gobboType, d.ageStage);
        SyncCompatFromUnit();
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
        GobboUnitSaveData d = Data;
        if (!initialized || dead || d == null) return;
        int finalDamage = Mathf.Max(1, amount - d.defense);
        d.health -= finalDamage;
        d.hasBeenHit = true;
        SyncCompatFromUnit();
        Flash(hurtColor);
        if (d.health <= 0) Die();
    }

    public void HealFlat(int amount)
    {
        GobboUnitSaveData d = Data;
        if (!initialized || dead || d == null || amount <= 0) return;
        d.health = Mathf.Min(d.maxHealth, d.health + amount);
        SyncCompatFromUnit();
    }

    public void HealPercent(float percent)
    {
        GobboUnitSaveData d = Data;
        if (!initialized || dead || d == null || percent <= 0f) return;
        int amount = Mathf.Max(1, Mathf.RoundToInt(d.maxHealth * percent));
        HealFlat(amount);
    }

    public void ApplyPoison(int damagePerTick, float duration, float tickRate)
    {
        if (!initialized || dead || Data == null) return;
        StartCoroutine(PoisonRoutine(damagePerTick, duration, tickRate));
    }

    public void TakePoison(int damagePerTick, float duration, float tickRate)
    {
        ApplyPoison(damagePerTick, duration, tickRate);
    }

    IEnumerator PoisonRoutine(int damagePerTick, float duration, float tickRate)
    {
        float timer = 0f;
        while (timer < duration && !dead && Data != null)
        {
            GobboUnitSaveData d = Data;
            d.health -= Mathf.Max(1, damagePerTick);
            d.hasBeenHit = true;
            SyncCompatFromUnit();
            Flash(poisonColor);
            if (d.health <= 0)
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
        if (spriteRenderer == null) return;
        if (visualController != null) visualController.SetAnimationState(GobboAnimationState.Hurt);
        StopCoroutine(nameof(FlashRoutine));
        StartCoroutine(FlashRoutine(color));
    }

    IEnumerator FlashRoutine(Color color)
    {
        spriteRenderer.color = color;
        yield return new WaitForSeconds(flashTime);
        if (spriteRenderer != null) spriteRenderer.color = originalColor;
        if (visualController != null) visualController.SetAnimationState(GobboAnimationState.Idle);
    }

    void Die()
    {
        if (dead) return;
        dead = true;
        GobboUnitSaveData d = Data;
        if (visualController != null) visualController.SetAnimationState(GobboAnimationState.Death);
        if (d != null)
        {
            d.EnsureRuntimeDefaults();
            SyncCompatFromUnit();
            if (GameState.Instance != null)
            {
                BuddyData compatDeath = data ?? BuddyData.FromUnit(d);
                GameState.Instance.RegisterBuddyDeath(compatDeath);
                GameState.Instance.RemoveBuddy(d.uniqueId);
            }
            BuddyRoster roster = Object.FindAnyObjectByType<BuddyRoster>();
            if (roster != null) roster.RemoveBuddy(d.uniqueId);
        }
        Destroy(gameObject);
    }
}
