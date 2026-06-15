using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class GobboController : MonoBehaviour
{
    [Header("Identity")]
    public string displayName = "Gobbo";

    [Header("Growth Identity")]
    public BuddyType gobboType = BuddyType.Baby;
    public GobboAgeStage ageStage = GobboAgeStage.Baby;
    public string visualSetId = "baby";
    public bool pendingEvolution = false;
    public int evolutionLevelWaiting = 0;
    public List<string> chosenCardIds = new List<string>();

    [Header("Stats")]
    public int level = 1;
    public int xp = 0;
    public int xpToNextLevel = 10;
    public int maxHealth = 100;
    public int health = 100;
    public int attack = 5;
    public int defense = 2;
    public int digPower = 1;

    [Header("Combat Stats")]
    public float attackRange = 0.85f;
    public float attackRadius = 0.45f;
    public float attackCooldown = 0.7f;
    public float critChance = 0f;
    public float critDamageMultiplier = 1.5f;

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float dashSpeed = 12f;
    public float dashDuration = 0.12f;
    public float dashCooldown = 0.7f;
    public float bodyRadius = 0.32f;
    public float wallContactPadding = 0.08f;

    [Header("Directional Sprites")]
    public bool faceCursor = true;
    [Tooltip("When Face Cursor is off, turn toward WASD movement. Good for camp.")]
    public bool faceMovementWhenNotFacingCursor = true;
    public Sprite front;
    public Sprite frontLeft;
    public Sprite frontRight;
    public Sprite back;
    public Sprite backLeft;
    public Sprite backRight;

    [Header("Digging")]
    public float digRange = 0.8f;
    public float digRadius = 0.65f;
    public float digComfortPadding = 0.35f;
    public float digBonusRadius = 0f;
    public int minimumClearedTileWidth = 3;
    public float digTickRate = 0.05f;
    public LayerMask diggableLayers;

    [Header("Attack")]
    public LayerMask enemyLayers;
    public GameObject attackDebugPrefab;
    public Transform currentAttackTarget;

    [Header("Interaction")]
    public float interactRange = 1.2f;

    [Header("Buddies")]
    public int followerCount = 0;
    public int maxFollowers = 999;
    public bool followersFollowing = true;
    public bool followersAggressive = true;
    public GameObject buddyPrefab;
    public BuddyRoster buddyRoster;
    public float buddySpawnRadius = 1.2f;
    public float buddyFormationSpread = 1.2f;

    [Header("Size")]
    public float baseSize = 1f;
    public float sizePerFollower = 0.05f;
    public float maxSize = 1.5f;
    public bool healthControlsSize = false;
    public float healthSizeMultiplier = 0f;
    public float maxHealthSizeBonus = 0.6f;

    [Header("Spores")]
    public int sporeCount = 0;
    public float sporePlaceRange = 1.5f;
    public GameObject plantedSporePrefab;

    [Header("Level Up")]
    public LevelUpScreen levelUpScreen;
    public float xpCurveMultiplier = 1.45f;

    [Header("Abilities")]
    public bool hasSporeMend = false;
    public bool hasDashBite = false;

    [Header("Spore Mend")]
    public int sporeMendAmount = 25;
    public float sporeMendCooldown = 8f;

    [Header("Dash Bite")]
    public float dashBiteRange = 4f;
    public float dashBiteStopDistance = 0.55f;
    public float dashBiteDamageMultiplier = 1.25f;
    public float dashBiteCooldown = 1.2f;

    [Header("Roar")]
    public string roarType = "tiny";
    public float roarCooldown = 0.5f;
    public AudioSource roarAudio;

    [Header("Player Damage Visuals")]
    public SpriteRenderer spriteRenderer;
    public GobboVisualController visualController;
    public Color hurtColor = Color.red;
    public float hurtFlashTime = 0.08f;
    public GameObject deathSplatPrefab;

    [Header("Knockback")]
    public float knockbackForce = 6f;
    public float knockbackDuration = 0.12f;

    [Header("Poison")]
    public bool isPoisoned = false;
    public Color poisonColor = new Color(0.6f, 1f, 0.25f);

    private Rigidbody2D rb;
    private SporeInventory sporeInventory;
    private Color originalColor;

    private Vector2 moveInput;
    private Vector2 aimDirection = Vector2.down;

    private bool isDashing = false;
    private bool isDead = false;
    private bool isKnockedBack = false;

    private float dashTimer = 0f;
    private float dashCooldownTimer = 0f;
    private float digTimer = 0f;
    private float roarCooldownTimer = 0f;
    private float knockbackTimer = 0f;
    private float attackCooldownTimer = 0f;
    private float sporeMendCooldownTimer = 0f;
    private float dashBiteCooldownTimer = 0f;

    private Vector2 knockbackVelocity;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        sporeInventory = GetComponent<SporeInventory>();

        if (buddyRoster == null)
            buddyRoster = Object.FindAnyObjectByType<BuddyRoster>();

        gameObject.tag = "Player";
    }

    void Start()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (visualController == null)
            visualController = GetComponent<GobboVisualController>();

        if (visualController == null)
            visualController = GetComponentInChildren<GobboVisualController>();

        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;

        if (health <= 0)
            health = maxHealth;

        RefreshAfterSaveLoad();
        ApplyBuddyModes();
        UpdateDirectionalSprite();
    }

    void Update()
    {
        if (isDead)
            return;

        ReadInput();
        UpdateAimDirection();
        UpdateTimers();
        HandleActions();
    }

    void FixedUpdate()
    {
        if (isDead)
            return;

        Move();
        TileMover.KeepOutOfWalls(rb, GetCollisionBodyRadius());
    }

    public void RefreshAfterSaveLoad()
    {
        health = Mathf.Clamp(health, 1, maxHealth);
        UpdateSize();
        UpdateDirectionalSprite();
    }

    void ReadInput()
    {
        moveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        ).normalized;
    }

    void UpdateAimDirection()
    {
        if (faceCursor)
        {
            if (Camera.main == null)
                return;

            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0f;

            Vector2 direction = mouseWorld - transform.position;

            if (direction.sqrMagnitude > 0.001f)
                aimDirection = direction.normalized;

            UpdateDirectionalSprite();
            return;
        }

        if (faceMovementWhenNotFacingCursor && moveInput.sqrMagnitude > 0.001f)
        {
            aimDirection = moveInput.normalized;
            UpdateDirectionalSprite();
        }
    }

    void UpdateDirectionalSprite()
    {
        if (visualController != null)
        {
            visualController.ApplyIdentity(gobboType, ageStage, visualSetId);
            visualController.SetDirection(aimDirection);
            return;
        }

        if (spriteRenderer == null)
            return;

        Vector2 dir = aimDirection;

        if (dir.y > 0.35f)
        {
            if (dir.x < -0.35f && backLeft != null)
                spriteRenderer.sprite = backLeft;
            else if (dir.x > 0.35f && backRight != null)
                spriteRenderer.sprite = backRight;
            else if (back != null)
                spriteRenderer.sprite = back;
        }
        else
        {
            if (dir.x < -0.35f && frontLeft != null)
                spriteRenderer.sprite = frontLeft;
            else if (dir.x > 0.35f && frontRight != null)
                spriteRenderer.sprite = frontRight;
            else if (front != null)
                spriteRenderer.sprite = front;
        }
    }

    void UpdateTimers()
    {
        if (dashCooldownTimer > 0f)
            dashCooldownTimer -= Time.deltaTime;

        if (attackCooldownTimer > 0f)
            attackCooldownTimer -= Time.deltaTime;

        if (roarCooldownTimer > 0f)
            roarCooldownTimer -= Time.deltaTime;

        if (sporeMendCooldownTimer > 0f)
            sporeMendCooldownTimer -= Time.deltaTime;

        if (dashBiteCooldownTimer > 0f)
            dashBiteCooldownTimer -= Time.deltaTime;

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;

            if (dashTimer <= 0f)
                isDashing = false;
        }

        if (isKnockedBack)
        {
            knockbackTimer -= Time.deltaTime;

            if (knockbackTimer <= 0f)
                isKnockedBack = false;
        }
    }

    void Move()
    {
        if (isKnockedBack)
        {
            TileMover.Move(rb, knockbackVelocity, GetCollisionBodyRadius());
            return;
        }

        Vector2 desiredVelocity = isDashing
            ? aimDirection * dashSpeed
            : moveInput * moveSpeed;

        if (visualController != null)
            visualController.SetAnimationState(isDashing ? GobboAnimationState.Dash : (moveInput.sqrMagnitude > 0.01f ? GobboAnimationState.Walk : GobboAnimationState.Idle));

        TileMover.Move(rb, desiredVelocity, GetCollisionBodyRadius());
    }

    void HandleActions()
    {
        if (Input.GetMouseButton(0))
            TryDig();
        else
            digTimer = 0f;

        if (Input.GetMouseButtonDown(1))
            BasicAttack();

        if (Input.GetMouseButtonDown(2))
            TryDashBite();

        if (Input.GetKeyDown(KeyCode.LeftShift))
            TryDash();

        if (Input.GetKeyDown(KeyCode.E))
            Interact();

        if (Input.GetKeyDown(KeyCode.Q))
            PlaceSpore();

        if (Input.GetKeyDown(KeyCode.F))
            ToggleFollowersFollow();

        if (Input.GetKeyDown(KeyCode.C))
            ToggleFollowerCombat();

        if (Input.GetKeyDown(KeyCode.Space))
            Roar();

        if (Input.GetKeyDown(KeyCode.R))
            SpecialAbility();
    }

    void TryDig()
    {
        digTimer -= Time.deltaTime;

        if (digTimer > 0f)
            return;

        digTimer = digTickRate;
        Dig();
    }

    void Dig()
    {
        if (visualController != null)
            visualController.SetAnimationState(GobboAnimationState.Dig);

        Vector2 digStart = transform.position;
        Vector2 digPoint = digStart + aimDirection.normalized * digRange;
        float effectiveDigRadius = GetEffectiveDigRadius();

        if (MapGenerator.Instance != null)
            DigCapsule(digStart, digPoint, effectiveDigRadius);

        Collider2D[] hits = diggableLayers.value == 0
            ? Physics2D.OverlapCircleAll(digPoint, effectiveDigRadius)
            : Physics2D.OverlapCircleAll(digPoint, effectiveDigRadius, diggableLayers);

        foreach (Collider2D hit in hits)
        {
            RevealCover revealCover = hit.GetComponent<RevealCover>();

            if (revealCover != null)
                revealCover.Dig(digPower);
        }
    }

    void DigCapsule(Vector2 start, Vector2 end, float radius)
    {
        if (MapGenerator.Instance == null)
            return;

        float distance = Vector2.Distance(start, end);
        float step = GetDigSweepStepDistance();
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / step));

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector2 point = Vector2.Lerp(start, end, t);
            MapGenerator.Instance.DigCircle(point, radius);
        }
    }

    float GetEffectiveDigRadius()
    {
        float bodyFitRadius = GetCollisionBodyRadius() + Mathf.Max(0f, digComfortPadding) + Mathf.Max(0f, digBonusRadius);
        float legacyInspectorRadius = Mathf.Max(0f, digRadius);
        float minimumTileRadius = GetMinimumClearedTileRadius();

        return Mathf.Max(bodyFitRadius, legacyInspectorRadius, minimumTileRadius);
    }

    float GetCollisionBodyRadius()
    {
        return GetScaledBodyRadius() + Mathf.Max(0f, wallContactPadding);
    }

    float GetScaledBodyRadius()
    {
        Vector3 scale = transform.lossyScale;
        float largestAxis = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        return bodyRadius * Mathf.Max(1f, largestAxis);
    }

    float GetMinimumClearedTileRadius()
    {
        int tileWidth = Mathf.Max(1, minimumClearedTileWidth);

        if (tileWidth % 2 == 0)
            tileWidth += 1;

        int rings = tileWidth / 2;
        if (rings <= 0)
            return 0f;

        float cellSize = MapGenerator.Instance != null ? MapGenerator.Instance.map.cellSize : 1f;
        return cellSize * rings * Mathf.Sqrt(2f);
    }

    float GetDigSweepStepDistance()
    {
        float cellSize = MapGenerator.Instance != null ? MapGenerator.Instance.map.cellSize : 1f;
        return Mathf.Max(0.1f, cellSize * 0.5f);
    }

    void BasicAttack()
    {
        if (attackCooldownTimer > 0f)
            return;

        attackCooldownTimer = attackCooldown;

        if (visualController != null)
            visualController.SetAnimationState(GobboAnimationState.Attack);

        Vector2 attackPoint = (Vector2)transform.position + aimDirection.normalized * attackRange;

        if (attackDebugPrefab != null)
        {
            GameObject marker = Instantiate(attackDebugPrefab, attackPoint, Quaternion.identity);
            marker.transform.localScale = Vector3.one * attackRadius * 2f;
            Destroy(marker, 0.15f);
        }

        Collider2D[] hits = enemyLayers.value == 0
            ? Physics2D.OverlapCircleAll(attackPoint, attackRadius)
            : Physics2D.OverlapCircleAll(attackPoint, attackRadius, enemyLayers);

        int damage = CalculateAttackDamage(1f);
        int hitCount = 0;

        foreach (Collider2D hit in hits)
        {
            if (hit.gameObject == gameObject)
                continue;

            if (!MapPathfinder.HasLineOfWalkableSight(transform.position, hit.transform.position))
                continue;

            DamageEnemyObject(hit.gameObject, damage);
            currentAttackTarget = hit.transform;
            TellBuddiesToAttack(currentAttackTarget);
            hitCount++;
        }

        Debug.Log("Gobbo attack checked. Enemies hit: " + hitCount);
    }

    int CalculateAttackDamage(float multiplier)
    {
        int damage = Mathf.Max(1, Mathf.RoundToInt(attack * multiplier));

        if (Random.value < critChance)
        {
            damage = Mathf.Max(1, Mathf.RoundToInt(damage * critDamageMultiplier));
            Debug.Log("CRIT! " + damage);
        }

        return damage;
    }

    void DamageEnemyObject(GameObject target, int damage)
    {
        target.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
    }

    void TryDashBite()
    {
        if (!hasDashBite || dashBiteCooldownTimer > 0f)
            return;

        Transform target = GetEnemyUnderCursor();

        if (target == null)
            return;

        float distance = Vector2.Distance(transform.position, target.position);

        if (distance > dashBiteRange)
            return;

        if (!MapPathfinder.HasLineOfWalkableSight(transform.position, target.position))
            return;

        dashBiteCooldownTimer = dashBiteCooldown;
        attackCooldownTimer = attackCooldown;

        Vector2 toTarget = ((Vector2)target.position - (Vector2)transform.position).normalized;
        Vector2 desiredPosition = (Vector2)target.position - toTarget * dashBiteStopDistance;

        if (MapGenerator.Instance == null || MapGenerator.Instance.IsWorldPositionClearForBody(desiredPosition, GetCollisionBodyRadius()))
            rb.position = desiredPosition;

        aimDirection = toTarget;
        UpdateDirectionalSprite();

        int damage = CalculateAttackDamage(dashBiteDamageMultiplier);
        DamageEnemyObject(target.gameObject, damage);

        currentAttackTarget = target;
        TellBuddiesToAttack(target);

        StartKnockback(-toTarget, knockbackForce * 0.45f, knockbackDuration);
    }

    Transform GetEnemyUnderCursor()
    {
        if (Camera.main == null)
            return null;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 point = new Vector2(mouseWorld.x, mouseWorld.y);

        Collider2D hit = enemyLayers.value == 0
            ? Physics2D.OverlapPoint(point)
            : Physics2D.OverlapPoint(point, enemyLayers);

        return hit != null ? hit.transform : null;
    }

    void TellBuddiesToAttack(Transform target)
    {
        if (target == null)
            return;

        BuddyCombat[] buddies = Object.FindObjectsByType<BuddyCombat>(
            FindObjectsSortMode.None
        );

        foreach (BuddyCombat buddy in buddies)
        {
            if (buddy == null)
                continue;

            buddy.SetTarget(target);
        }
    }

    void TryDash()
    {
        if (dashCooldownTimer > 0f)
            return;

        isDashing = true;
        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;
    }

    void Interact()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, interactRange);

        ICampInteractable nearestCampInteractable = null;
        float nearestCampDistance = Mathf.Infinity;

        foreach (Collider2D hit in hits)
        {
            ICampInteractable campInteractable = hit.GetComponent<ICampInteractable>();

            if (campInteractable == null)
                continue;

            float distance = Vector2.Distance(transform.position, hit.transform.position);

            if (distance < nearestCampDistance)
            {
                nearestCampDistance = distance;
                nearestCampInteractable = campInteractable;
            }
        }

        if (nearestCampInteractable != null)
        {
            nearestCampInteractable.Interact(this);
            return;
        }

        FoodItem nearestFood = null;
        float nearestDistance = Mathf.Infinity;

        foreach (Collider2D hit in hits)
        {
            FoodItem food = hit.GetComponent<FoodItem>();

            if (food == null)
                continue;

            float distance = Vector2.Distance(transform.position, food.transform.position);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestFood = food;
            }
        }

        if (nearestFood != null)
        {
            nearestFood.Eat(this);
            return;
        }

        Debug.Log("Nothing interactable nearby.");
    }

    void PlaceSpore()
    {
        if (plantedSporePrefab == null)
        {
            Debug.LogWarning("No planted spore prefab assigned on GobboController.");
            return;
        }

        bool hasSpore = false;

        if (sporeInventory != null)
            hasSpore = sporeInventory.UseSpore();
        else if (sporeCount > 0)
        {
            sporeCount--;
            hasSpore = true;
        }

        if (!hasSpore)
        {
            Debug.Log("No spores.");
            return;
        }

        Vector2 placePoint = (Vector2)transform.position + aimDirection.normalized * sporePlaceRange;
        GameObject plantedSpore = Instantiate(plantedSporePrefab, placePoint, Quaternion.identity);

        SporeGrow grow = plantedSpore.GetComponent<SporeGrow>();

        if (grow != null && grow.buddyPrefab == null)
            grow.buddyPrefab = buddyPrefab;
    }

    public void SpawnBuddy()
    {
        GobboUnitSaveData data = new GobboUnitSaveData();
        data.displayName = "Buddy";
        data.gobboType = BuddyType.Baby;
        data.ageStage = GobboAgeStage.Baby;
        BuddyProgression.PrepareNewBaby(data);

        if (GameState.Instance != null)
        {
            GameState.Instance.AddGobbo(data, true);
            GameState.Instance.RegisterGobboFound(data);
        }
        else
        {
            Debug.LogWarning("No GameState found. Spawning unsaved gobbo unit.");
        }

        Vector2 spawnPos = (Vector2)transform.position + Random.insideUnitCircle * buddySpawnRadius;
        SpawnBuddy(data, spawnPos);
    }

    public void SpawnBuddy(GobboUnitSaveData data)
    {
        Vector2 spawnPos = (Vector2)transform.position + Random.insideUnitCircle * buddySpawnRadius;
        SpawnBuddy(data, spawnPos);
    }

    public void SpawnBuddy(GobboUnitSaveData data, Vector2 spawnPosition)
    {
        if (buddyPrefab == null)
        {
            Debug.LogWarning("No buddy prefab assigned on GobboController.");
            return;
        }

        if (data == null)
        {
            Debug.LogWarning("Tried to spawn buddy with no GobboUnitSaveData.");
            return;
        }

        data.isLeader = false;
        data.EnsureRuntimeDefaults();

        GameObject buddyObject = Instantiate(buddyPrefab, spawnPosition, Quaternion.identity);
        buddyObject.name = data.displayName;
        buddyObject.layer = LayerMask.NameToLayer("Buddy");

        BuddyUnit unit = buddyObject.GetComponent<BuddyUnit>();

        if (unit != null)
            unit.Initialize(data);
        else
            Debug.LogWarning("Buddy prefab is missing BuddyUnit.");

        BuddyFollow follow = buddyObject.GetComponent<BuddyFollow>();

        if (follow != null)
        {
            follow.SetPlayer(transform);
            follow.SetFormationOffset(Random.insideUnitCircle.normalized * buddyFormationSpread);
            follow.enabled = followersFollowing;
        }

        BuddyCombat combat = buddyObject.GetComponent<BuddyCombat>();

        if (combat != null)
        {
            combat.SetPlayer(transform);
            combat.enabled = followersAggressive;
        }

        AddFollower(1);

        Debug.Log("Spawned buddy: " + data.displayName + " / " + data.gobboType);
    }

    public bool PullReserveBuddyIntoRun()
    {
        if (GameState.Instance == null)
            return false;

        GobboUnitSaveData data = GameState.Instance.PullFirstReserveGobbo();

        if (data == null)
            return false;

        SpawnBuddy(data);
        return true;
    }

    void ToggleFollowersFollow()
    {
        followersFollowing = !followersFollowing;
        ApplyBuddyModes();

        Debug.Log(followersFollowing ? "Followers: FOLLOW" : "Followers: STAY");
    }

    void ToggleFollowerCombat()
    {
        followersAggressive = !followersAggressive;
        ApplyBuddyModes();

        Debug.Log(followersAggressive ? "Followers: BITE" : "Followers: PASSIVE");
    }

    void ApplyBuddyModes()
    {
        BuddyFollow[] follows = Object.FindObjectsByType<BuddyFollow>(
            FindObjectsSortMode.None
        );

        foreach (BuddyFollow follow in follows)
        {
            if (follow != null)
                follow.enabled = followersFollowing;
        }

        BuddyCombat[] combats = Object.FindObjectsByType<BuddyCombat>(
            FindObjectsSortMode.None
        );

        foreach (BuddyCombat combat in combats)
        {
            if (combat != null)
                combat.enabled = followersAggressive;
        }
    }

    void Roar()
    {
        if (roarCooldownTimer > 0f)
            return;

        roarCooldownTimer = roarCooldown;

        Debug.Log("Gobbo roar: " + roarType);

        if (roarAudio != null)
            roarAudio.Play();
    }

    void SpecialAbility()
    {
        if (hasSporeMend)
        {
            TrySporeMend();
            return;
        }

        Debug.Log("No special ability yet.");
    }

    void TrySporeMend()
    {
        if (sporeMendCooldownTimer > 0f)
            return;

        sporeMendCooldownTimer = sporeMendCooldown;
        Heal(sporeMendAmount);
        Debug.Log("Spore Mend healed " + sporeMendAmount);
    }

    public void EatFood(int value, int healAmount = 0, int foodValue = 0)
    {
        AddXP(value);

        if (foodValue > 0 && GameState.Instance != null)
            GameState.Instance.RegisterFoodValueGained(foodValue);

        if (healAmount > 0)
            Heal(healAmount);
    }

    public void AddXP(int amount)
    {
        if (amount <= 0)
            return;

        if (GameState.Instance != null)
            GameState.Instance.RegisterXPGained(amount);

        xp += amount;

        while (xp >= xpToNextLevel)
        {
            xp -= xpToNextLevel;
            LevelUp();
        }
    }

    void LevelUp()
    {
        level++;

        // Leveling increases max health, but it should NOT fully heal during the run.
        // The camp report should show the health you actually reached the portal with.
        // Full camp recovery happens after the summary/growth menus, when entering camp visuals.
        int missingHealthBeforeLevel = Mathf.Max(0, maxHealth - health);
        maxHealth += 5;
        health = Mathf.Clamp(maxHealth - missingHealthBeforeLevel, 1, maxHealth);

        if (level % 3 == 0)
        {
            attack += 1;
            defense += 1;
        }

        if (BuddyProgression.IsEvolutionLevel(level))
        {
            pendingEvolution = true;
            evolutionLevelWaiting = level;
        }

        xpToNextLevel = Mathf.Max(xpToNextLevel + 1, Mathf.RoundToInt(xpToNextLevel * xpCurveMultiplier));

        Debug.Log("LEVEL UP: " + level);

        if (levelUpScreen == null)
            levelUpScreen = Object.FindAnyObjectByType<LevelUpScreen>();

        if (levelUpScreen != null)
            levelUpScreen.ShowChoices(this);
        else
            Debug.LogWarning("No LevelUpScreen found in scene.");
    }

    public bool NeedsEvolutionChoice()
    {
        return pendingEvolution || (level == 2 && gobboType == BuddyType.Baby);
    }

    public void ClearPendingEvolutionIfCurrentLevelHandled()
    {
        pendingEvolution = false;
        evolutionLevelWaiting = 0;
    }

    public void Heal(int amount)
    {
        health = Mathf.Min(maxHealth, health + amount);
        Debug.Log("Healed: " + amount);
    }

    public void TakeDamage(int amount)
    {
        if (isDead)
            return;

        int damageTaken = Mathf.Max(1, amount - defense);
        health -= damageTaken;
        FlashHurtColor();
        Debug.Log("Gobbo took damage: " + damageTaken);

        if (health <= 0)
            Die();
    }

    public void TakePoison(int damagePerTick, float duration, float tickRate)
    {
        if (!isActiveAndEnabled || isDead) return;
        StartCoroutine(PoisonRoutine(damagePerTick, duration, tickRate));
    }

    IEnumerator PoisonRoutine(int damagePerTick, float duration, float tickRate)
    {
        isPoisoned = true;
        float timer = 0f;

        while (timer < duration && !isDead)
        {
            FlashHurtColor(poisonColor);
            TakeDamage(damagePerTick);
            yield return new WaitForSeconds(tickRate);
            timer += tickRate;
        }

        isPoisoned = false;
    }

    void FlashHurtColor()
    {
        FlashHurtColor(hurtColor);
    }

    void FlashHurtColor(Color color)
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
        yield return new WaitForSeconds(hurtFlashTime);
        if (spriteRenderer != null) spriteRenderer.color = originalColor;
        if (visualController != null) visualController.SetAnimationState(GobboAnimationState.Idle);
    }

    void StartKnockback(Vector2 direction, float force, float duration)
    {
        isKnockedBack = true;
        knockbackTimer = duration;
        knockbackVelocity = direction.normalized * force;
    }

    void Die()
    {
        if (isDead)
            return;

        isDead = true;

        if (visualController != null)
            visualController.SetAnimationState(GobboAnimationState.Death);

        if (GameState.Instance != null)
        {
            GameState.Instance.SavePlayer(this);
            GameState.Instance.leader.isDead = true;
            if (string.IsNullOrWhiteSpace(GameState.Instance.leader.causeOfDeath))
                GameState.Instance.leader.causeOfDeath = "The leader got chewed up in the dirt.";
        }

        if (deathSplatPrefab != null)
            Instantiate(deathSplatPrefab, transform.position, Quaternion.identity);

        gameObject.SetActive(false);
    }

    void AddFollower(int amount)
    {
        followerCount += amount;
        UpdateSize();
    }

    void UpdateSize()
    {
        float size = baseSize + followerCount * sizePerFollower;

        if (healthControlsSize)
            size += maxHealth * healthSizeMultiplier;

        size += Mathf.Max(0f, maxHealth - 100) / 100f * maxHealthSizeBonus;
        size = Mathf.Min(size, maxSize);
        transform.localScale = Vector3.one * size;
    }
}
