using System.Collections.Generic;
using UnityEngine;

public class GobboCardDatabase : MonoBehaviour
{
    public static GobboCardDatabase Instance { get; private set; }

    [Header("Cards")]
    public List<GobboCard> cards = new List<GobboCard>();

    void Awake()
    {
        Instance = this;
        CreateDefaultsIfEmpty();
    }

    public List<GobboCard> GetChoicesForPlayer(GobboController gobbo, GobboCardContext context, int count, List<string> excludeIds = null)
    {
        List<GobboCard> available = new List<GobboCard>();
        foreach (GobboCard card in cards)
        {
            if (card == null || !card.CanAppearForPlayer(gobbo, context)) continue;
            if (excludeIds != null && excludeIds.Contains(card.cardId)) continue;
            available.Add(card);
        }
        return PickRandom(available, count);
    }

    public List<GobboCard> GetChoicesForBuddy(GobboUnitSaveData buddy, GobboCardContext context, int count, List<string> excludeIds = null)
    {
        List<GobboCard> available = new List<GobboCard>();
        foreach (GobboCard card in cards)
        {
            if (card == null || !card.CanAppearForBuddy(buddy, context)) continue;
            if (excludeIds != null && excludeIds.Contains(card.cardId)) continue;
            available.Add(card);
        }
        return PickRandom(available, count);
    }

    List<GobboCard> PickRandom(List<GobboCard> pool, int count)
    {
        List<GobboCard> result = new List<GobboCard>();
        while (result.Count < count && pool.Count > 0)
        {
            int index = Random.Range(0, pool.Count);
            result.Add(pool[index]);
            pool.RemoveAt(index);
        }
        return result;
    }

    void CreateDefaultsIfEmpty()
    {
        if (cards != null && cards.Count > 0) return;
        cards = new List<GobboCard>();

        AddTypeChoice(BuddyType.Fast, "Fast Gobbo", "Become a twitchy little sprint freak.\n+speed, faster attacks, less health.", 2, -10, 0.9f, -0.08f);
        AddTypeChoice(BuddyType.Fat, "Fat Gobbo", "Become a big food-built lump. +health, slower but harder to kill.", 35, 0, -0.45f, 0.08f);
        AddTypeChoice(BuddyType.Scavenger, "Scavenger Gobbo", "Become a grabby little trash prince. Collects food and smells value.", 5, 0, 0.25f, 0f, true);
        AddTypeChoice(BuddyType.Tank, "Tank Gobbo", "Become a walking wall.\n+defense and health, slower movement.", 25, 2, -0.35f, 0.05f);
        AddTypeChoice(BuddyType.Thrower, "Thrower Gobbo", "Become a future rock-lobber. +range now, projectile system later.", 5, 0, 0f, 0f, false, 0.2f);
        AddTypeChoice(BuddyType.Strong, "Strong Gobbo", "Become a punchy diggy brute. +attack and dig power.", 10, 0, -0.1f, 0f, false, 0f, 2, 1);
        AddTypeChoice(BuddyType.Fungal, "Fungal Gobbo", "Become a weird bloom-body.\nUnlocks the first healing path later.", 10, 0, -0.05f, 0f, false, 0f, 0, 0, true);
        AddTypeChoice(BuddyType.Explosive, "Explosive Gobbo", "Become a dangerous pop-bellied hazard. Blast cards later.", 5, 0, 0.05f, 0f);

        cards.Add(new GobboCard { cardId = "jagged_teeth", cardName = "Jagged Teeth", description = "+3 Attack, +10% crit chance.", attackBonus = 3, critChanceBonus = 0.10f });
        cards.Add(new GobboCard { cardId = "mushroom_gut", cardName = "Mushroom Gut", description = "+25 Max Health.\nBigger, harder to squish.", maxHealthBonus = 25 });
        cards.Add(new GobboCard { cardId = "tunnel_rat", cardName = "Tunnel Rat", description = "+1 Dig Power, +0.25 Dig Radius, -0.3 Move Speed.", digPowerBonus = 1, moveSpeedBonus = -0.3f });
        cards.Add(new GobboCard { cardId = "loose_spine", cardName = "Loose Spine", description = "Dash recovers faster and hits higher speed.", dashCooldownBonus = -0.15f, dashSpeedBonus = 2f });
        cards.Add(new GobboCard { cardId = "headbutter", cardName = "Headbutter", description = "+3 Knockback, +0.1 Attack Radius.", knockbackBonus = 3f, attackRadiusBonus = 0.1f });
        cards.Add(new GobboCard { cardId = "long_arms", cardName = "Long Arms", description = "+0.25 Attack Range, +0.1 Attack Radius.", attackRangeBonus = 0.25f, attackRadiusBonus = 0.1f });
        cards.Add(new GobboCard { cardId = "mean_little_legs", cardName = "Mean Little Legs", description = "+0.5 Move Speed, faster attacks.", moveSpeedBonus = 0.5f, attackCooldownBonus = -0.1f });
        cards.Add(new GobboCard { cardId = "spore_mend", cardName = "Spore Mend", description = "Unlock R self-heal.\nFlat heal on cooldown.", unlockSporeMend = true, minLevel = 6, playerAllowed = true, buddyAllowed = false });
        cards.Add(new GobboCard { cardId = "dash_bite", cardName = "Dash Bite", description = "Unlock middle-click lunge bite.", unlockDashBite = true, minLevel = 6, playerAllowed = true, buddyAllowed = false });

        AddClassCard(BuddyType.Fast, "skitter_legs", "Skitter Legs", "+0.6 Move Speed.", moveSpeedBonus: 0.6f);
        AddClassCard(BuddyType.Fast, "snap_bite", "Snap Bite", "Faster attacks.", attackCooldownBonus: -0.12f);
        AddClassCard(BuddyType.Fat, "big_gut", "Big Gut", "+30 Max Health.", maxHealthBonus: 30);
        AddClassCard(BuddyType.Fat, "meat_sponge", "Meat Sponge", "+2 Defense.", defenseBonus: 2);
        AddClassCard(BuddyType.Scavenger, "grabby_hands", "Grabby Hands", "Scavenger behavior stays enabled and future pickup range can hook here.", setCollectsFood: true);
        AddClassCard(BuddyType.Tank, "bark_shield", "Bark Shield", "+3 Defense.", defenseBonus: 3);
        AddClassCard(BuddyType.Thrower, "weird_arm", "Weird Arm", "+0.35 Attack Range now.\nProjectile range later.", attackRangeBonus: 0.35f);
        AddClassCard(BuddyType.Strong, "root_muscles", "Root Muscles", "+2 Attack, +1 Dig Power.", attackBonus: 2, digPowerBonus: 1);
        AddClassCard(BuddyType.Fungal, "soft_rot", "Soft Rot", "+20 Max Health. Future poison/heal hook.", maxHealthBonus: 20);
        AddClassCard(BuddyType.Explosive, "pop_belly", "Pop Belly", "+0.2 Attack Radius.\nFuture blast radius hook.", attackRadiusBonus: 0.2f);

        foreach (BuddyType type in System.Enum.GetValues(typeof(BuddyType)))
        {
            if (type == BuddyType.Baby) continue;
            AddEvolution(type, 6, GobboAgeStage.Stage1, type + " First Mutation", "First visible mutation for this body line.");
            AddEvolution(type, 12, GobboAgeStage.Stage2, type + " Adult Mutation", "The class identity gets louder.");
            AddEvolution(type, 24, GobboAgeStage.Stage3, type + " Monster Mutation", "A stranger and stronger grown form.");
            AddEvolution(type, 48, GobboAgeStage.Stage4, type + " Ancient Mutation", "The ridiculous endgame freak version.");
        }
    }

    void AddTypeChoice(BuddyType type, string name, string desc, int hp, int defense, float speed, float cd, bool collectsFood = false, float range = 0f, int attack = 0, int digPower = 0, bool fungal = false)
    {
        cards.Add(new GobboCard
        {
            cardId = "choose_" + type.ToString().ToLowerInvariant(),
            cardName = name,
            description = desc,
            isTypeChoiceCard = true,
            isEvolutionCard = true,
            minLevel = 2,
            requiresSpecificType = true,
            requiredType = BuddyType.Baby,
            changesType = true,
            setType = type,
            changesAgeStage = true,
            setAgeStage = GobboAgeStage.Young,
            setVisualSetId = type.ToString().ToLowerInvariant() + "_young",
            maxHealthBonus = hp,
            defenseBonus = defense,
            moveSpeedBonus = speed,
            attackCooldownBonus = cd,
            attackRangeBonus = range,
            attackBonus = attack,
            digPowerBonus = digPower,
            setCollectsFood = collectsFood,
            collectsFoodValue = collectsFood,
            unlockSporeMend = false
        });
    }

    void AddEvolution(BuddyType type, int level, GobboAgeStage stage, string name, string desc)
    {
        cards.Add(new GobboCard
        {
            cardId = type.ToString().ToLowerInvariant() + "_stage_" + level,
            cardName = name,
            description = desc,
            isEvolutionCard = true,
            minLevel = level,
            requiresSpecificType = true,
            requiredType = type,
            changesAgeStage = true,
            setAgeStage = stage,
            setVisualSetId = type.ToString().ToLowerInvariant() + "_" + stage.ToString().ToLowerInvariant(),
            maxHealthBonus = level >= 24 ? 20 : 10,
            attackBonus = level >= 12 ? 1 : 0,
            defenseBonus = type == BuddyType.Tank || type == BuddyType.Fat ? 1 : 0
        });
    }

    void AddClassCard(BuddyType type, string id, string name, string desc, int maxHealthBonus = 0, int attackBonus = 0, int defenseBonus = 0, int digPowerBonus = 0, float moveSpeedBonus = 0f, float attackCooldownBonus = 0f, float attackRangeBonus = 0f, float attackRadiusBonus = 0f, bool setCollectsFood = false)
    {
        cards.Add(new GobboCard
        {
            cardId = id,
            cardName = name,
            description = desc,
            minLevel = 3,
            requiresSpecificType = true,
            requiredType = type,
            maxHealthBonus = maxHealthBonus,
            attackBonus = attackBonus,
            defenseBonus = defenseBonus,
            digPowerBonus = digPowerBonus,
            moveSpeedBonus = moveSpeedBonus,
            attackCooldownBonus = attackCooldownBonus,
            attackRangeBonus = attackRangeBonus,
            attackRadiusBonus = attackRadiusBonus,
            setCollectsFood = setCollectsFood,
            collectsFoodValue = setCollectsFood
        });
    }
}
