using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BuddyData
{
    [Header("Identity")]
    public string uniqueId = "";
    public string buddyName = "Gobbo";
    public BuddyType buddyType = BuddyType.Baby;

    [Header("Growth")]
    public GobboAgeStage ageStage = GobboAgeStage.Baby;
    public int level = 1;
    public int xp = 0;
    public int xpToNextLevel = 1; // TESTING: baby buddies grow after almost any food haul.
    public int campLevel = 1;
    public bool pendingEvolution = false;
    public int evolutionLevelWaiting = 0;
    public int runsWaitingForEvolution = 0;
    public bool neglectedElder = false;

    [Header("Mood")]
    [Range(0, 100)] public int happiness = 100;
    [Range(0, 100)] public int loyalty = 100;

    [Header("Stats")]
    public int maxHealth = 8;
    public int health = 8;
    public int damage = 1;
    public int defense = 0;

    public float moveSpeed = 3.2f;
    public float attackCooldown = 0.9f;

    [Header("Behavior")]
    public bool onlyFightsAfterHit = false;
    public bool collectsFood = false;
    public bool hasBeenHit = false;

    [Header("Roster State")]
    public bool isInActiveSquad = false;
    public bool survivedLastRun = true;

    [Header("Visual")]
    public Color bodyColor = Color.green;
    public string visualSetId = "baby";
    public string portraitId = "";
    public string equippedHat = "";

    [Header("Cards / Mutations")]
    public List<string> chosenCardIds = new List<string>();
    public List<string> mutationIds = new List<string>();
    public List<string> upgradeIds = new List<string>();

    [Header("Future Expansion")]
    public string equippedItem = "";

    public void EnsureId()
    {
        if (string.IsNullOrWhiteSpace(uniqueId))
            uniqueId = Guid.NewGuid().ToString("N");

        EnsureRuntimeDefaults();
    }

    public void EnsureRuntimeDefaults()
    {
        if (chosenCardIds == null) chosenCardIds = new List<string>();
        if (mutationIds == null) mutationIds = new List<string>();
        if (upgradeIds == null) upgradeIds = new List<string>();

        if (string.IsNullOrWhiteSpace(buddyName))
            buddyName = "Gobbo";

        if (level <= 0)
            level = 1;

        if (campLevel <= 0)
            campLevel = level;

        if (xp < 0)
            xp = 0;

        if (xpToNextLevel <= 0)
            xpToNextLevel = BuddyProgression.TestBabyXPToNextLevel;

        if (maxHealth <= 0)
            maxHealth = buddyType == BuddyType.Baby ? 8 : 5;

        if (health <= 0 || health > maxHealth)
            health = maxHealth;

        if (damage <= 0)
            damage = 1;

        if (moveSpeed <= 0f)
            moveSpeed = buddyType == BuddyType.Baby ? 3.2f : 3.5f;

        if (attackCooldown <= 0f)
            attackCooldown = 0.9f;

        happiness = Mathf.Clamp(happiness <= 0 ? 100 : happiness, 0, 100);
        loyalty = Mathf.Clamp(loyalty <= 0 ? 100 : loyalty, 0, 100);

        if (string.IsNullOrWhiteSpace(visualSetId))
            visualSetId = buddyType == BuddyType.Baby
                ? "baby"
                : buddyType.ToString().ToLowerInvariant() + "_" + ageStage.ToString().ToLowerInvariant();
    }

    public BuddyData Clone()
    {
        EnsureRuntimeDefaults();

        BuddyData copy = new BuddyData();

        copy.uniqueId = uniqueId;
        copy.buddyName = buddyName;
        copy.buddyType = buddyType;

        copy.ageStage = ageStage;
        copy.level = level;
        copy.xp = xp;
        copy.xpToNextLevel = xpToNextLevel;
        copy.campLevel = campLevel;
        copy.pendingEvolution = pendingEvolution;
        copy.evolutionLevelWaiting = evolutionLevelWaiting;
        copy.runsWaitingForEvolution = runsWaitingForEvolution;
        copy.neglectedElder = neglectedElder;

        copy.happiness = happiness;
        copy.loyalty = loyalty;

        copy.maxHealth = maxHealth;
        copy.health = health;
        copy.damage = damage;
        copy.defense = defense;
        copy.moveSpeed = moveSpeed;
        copy.attackCooldown = attackCooldown;

        copy.onlyFightsAfterHit = onlyFightsAfterHit;
        copy.collectsFood = collectsFood;
        copy.hasBeenHit = hasBeenHit;

        copy.isInActiveSquad = isInActiveSquad;
        copy.survivedLastRun = survivedLastRun;

        copy.bodyColor = bodyColor;
        copy.visualSetId = visualSetId;
        copy.portraitId = portraitId;
        copy.equippedHat = equippedHat;

        copy.chosenCardIds = chosenCardIds != null ? new List<string>(chosenCardIds) : new List<string>();
        copy.mutationIds = mutationIds != null ? new List<string>(mutationIds) : new List<string>();
        copy.upgradeIds = upgradeIds != null ? new List<string>(upgradeIds) : new List<string>();
        copy.equippedItem = equippedItem;

        return copy;
    }
}
