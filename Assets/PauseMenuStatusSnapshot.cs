using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class PauseMenuStatusSnapshot
{
    public PlayerPauseSnapshot player = new PlayerPauseSnapshot();
    public RunPauseSnapshot run = new RunPauseSnapshot();
    public List<BuddyPauseSnapshot> activeSquad = new List<BuddyPauseSnapshot>();
}

public sealed class PlayerPauseSnapshot
{
    public string name = "Gobbo", type = "Unknown", stage = "Unknown";
    public int level, xp, xpRequired, health, maxHealth, attack, defense, digPower, followers, sporeMendAmount;
    public float moveSpeed, attackCooldown, attackRange, attackRadius, dashSpeed, dashDuration, dashCooldown, critChance, critDamage, digRadius;
    public float digRange, digTickRate, sporeMendCooldown, dashBiteRange, dashBiteMultiplier, dashBiteCooldown;
    public bool poisoned, hasSporeMend, hasDashBite;
    public int snackHealth, snackAttack, snackDefense;
    public List<string> cards = new List<string>(), abilities = new List<string>(), traits = new List<string>();
}

public sealed class RunPauseSnapshot
{
    public int number, enemies, mushrooms, shinies, spores, snacks, squadSize, squadMax, newBuddies;
    public float elapsed;
    public List<string> upgrades = new List<string>();
}

public sealed class BuddyPauseSnapshot
{
    public string name = "Gobbo", type = "Unknown", stage = "Unknown", growth = "None";
    public int level, xp, xpRequired, health, maxHealth, attack, defense, snackHealth, snackAttack, snackDefense;
    public float moveSpeed, attackCooldown;
    public bool alive;
    public List<string> traits = new List<string>();
}

public static class PauseMenuStatusSnapshotBuilder
{
    public static PauseMenuStatusSnapshot Build()
    {
        PauseMenuStatusSnapshot result = new PauseMenuStatusSnapshot();
        GameState state = GameState.Instance;
        GobboController live = Object.FindAnyObjectByType<GobboController>();
        GobboUnitSaveData leader = state != null ? state.leader : null;
        BuildPlayer(result.player, live, leader);
        BuildRun(result.run, state, live);
        BuildSquad(result.activeSquad, state);
        return result;
    }

    static void BuildPlayer(PlayerPauseSnapshot p, GobboController live, GobboUnitSaveData saved)
    {
        if (live != null)
        {
            p.name = live.displayName; p.type = TypeLabel(live.gobboType); p.stage = Label(live.ageStage.ToString());
            p.level = live.level; p.xp = live.xp; p.xpRequired = live.xpToNextLevel; p.health = live.health; p.maxHealth = live.maxHealth;
            p.attack = live.attack; p.defense = live.defense; p.moveSpeed = live.moveSpeed; p.attackCooldown = live.attackCooldown;
            p.attackRange = live.attackRange; p.attackRadius = live.attackRadius; p.dashSpeed = live.dashSpeed;
            p.dashDuration = live.dashDuration; p.dashCooldown = live.dashCooldown; p.critChance = live.critChance;
            p.critDamage = live.critDamageMultiplier; p.digPower = live.digPower; p.digRadius = live.GetCurrentEffectiveDigRadius();
            p.digRange = live.digRange; p.digTickRate = live.digTickRate; p.followers = live.followerCount;
            p.hasSporeMend = live.hasSporeMend; p.sporeMendAmount = live.sporeMendAmount; p.sporeMendCooldown = live.sporeMendCooldown;
            p.hasDashBite = live.hasDashBite; p.dashBiteRange = live.dashBiteRange; p.dashBiteMultiplier = live.dashBiteDamageMultiplier; p.dashBiteCooldown = live.dashBiteCooldown;
            p.poisoned = live.isPoisoned;
        }
        else if (saved != null)
        {
            p.name = saved.displayName; p.type = TypeLabel(saved.gobboType); p.stage = Label(saved.ageStage.ToString());
            p.level = saved.level; p.xp = saved.xp; p.xpRequired = saved.xpToNextLevel; p.health = saved.health; p.maxHealth = saved.maxHealth;
            p.attack = saved.attack; p.defense = saved.defense; p.moveSpeed = saved.moveSpeed; p.attackCooldown = saved.attackCooldown;
            p.attackRange = saved.attackRange; p.attackRadius = saved.attackRadius; p.dashSpeed = saved.dashSpeed;
            p.dashDuration = saved.dashDuration; p.dashCooldown = saved.dashCooldown; p.critChance = saved.critChance;
            p.critDamage = saved.critDamageMultiplier; p.digPower = saved.digPower; p.digRadius = saved.digRadius;
        }
        if (saved == null) return;
        p.snackHealth = saved.snackMaxHealthBonus; p.snackAttack = saved.snackAttackBonus; p.snackDefense = saved.snackDefenseBonus;
        p.cards = CardLabels(saved.chosenCardIds); p.traits = Labels(saved.traitIds); AddUnique(p.traits, saved.primaryTraitId);
        if (live != null ? live.hasSporeMend : saved.hasSporeMend) p.abilities.Add("Spore Mend");
        if (live != null ? live.hasDashBite : saved.hasDashBite) p.abilities.Add("Dash Bite");
    }

    static void BuildRun(RunPauseSnapshot r, GameState state, GobboController live)
    {
        if (state == null) return;
        RunSummaryData run = state.lastRun; GobboUnitSaveData leader = state.leader;
        r.number = Mathf.Max(1, state.currentRunNumber); r.enemies = run != null ? Mathf.Max(0, run.enemiesKilled) : 0;
        r.mushrooms = leader != null ? Mathf.Max(0, leader.mushrooms) : 0; r.shinies = leader != null ? Mathf.Max(0, leader.shinies) : 0;
        SporeInventory spores = live != null ? live.GetComponent<SporeInventory>() : null;
        if (spores == null) spores = Object.FindAnyObjectByType<SporeInventory>();
        r.spores = spores != null ? Mathf.Max(0, spores.spores) : leader != null ? Mathf.Max(0, leader.spores) : 0;
        r.snacks = SumStacks(state.runSnackStacks); r.squadSize = state.activeSquadIds != null ? state.activeSquadIds.Count : 0;
        r.squadMax = Mathf.Max(0, state.maxActiveSquad); r.newBuddies = run != null ? Mathf.Max(run.buddiesFound, run.newBuddyNames != null ? run.newBuddyNames.Count : 0) : 0;
        r.elapsed = state.GetCurrentRunElapsedTime(); r.upgrades = run != null ? Labels(run.upgradesChosen) : new List<string>();
    }

    static void BuildSquad(List<BuddyPauseSnapshot> result, GameState state)
    {
        if (state == null || state.activeSquadIds == null) return;
        BuddyUnit[] live = Object.FindObjectsByType<BuddyUnit>(FindObjectsInactive.Include);
        foreach (string id in state.activeSquadIds)
        {
            GobboUnitSaveData data = live.Where(u => u != null && u.unitData != null).Select(u => u.unitData).FirstOrDefault(u => u.uniqueId == id);
            if (data == null && state.ownedGobbos != null) data = state.ownedGobbos.FirstOrDefault(u => u != null && u.uniqueId == id);
            if (data == null) continue;
            BuddyPauseSnapshot b = new BuddyPauseSnapshot
            {
                name = data.displayName, type = TypeLabel(data.gobboType), stage = Label(data.ageStage.ToString()), level = data.level,
                xp = data.xp, xpRequired = data.xpToNextLevel, health = data.health, maxHealth = data.maxHealth, attack = data.damage,
                defense = data.defense, moveSpeed = data.moveSpeed, attackCooldown = data.attackCooldown,
                snackHealth = data.snackMaxHealthBonus, snackAttack = data.snackAttackBonus, snackDefense = data.snackDefenseBonus,
                growth = Growth(data), alive = !data.isDead, traits = Labels(data.traitIds)
            };
            AddUnique(b.traits, data.primaryTraitId); if (data.collectsFood) AddLabel(b.traits, "Scavenger");
            if (data.onlyFightsAfterHit) AddLabel(b.traits, "Fights After Hit"); result.Add(b);
        }
    }

    static string Growth(GobboUnitSaveData d)
    {
        if (d.pendingGrowthChoiceType != BuddyGrowthChoiceType.None) return Label(d.pendingGrowthChoiceType.ToString()) + " (Level " + Mathf.Max(1, d.pendingGrowthLevelWaiting) + ")";
        int queued = d.pendingGrowthQueue != null ? d.pendingGrowthQueue.Count : 0; return queued > 0 ? queued + " queued" : "None";
    }

    static int SumStacks(List<InventoryStackSaveData> stacks) { int total = 0; if (stacks != null) foreach (InventoryStackSaveData s in stacks) if (s != null) total += Mathf.Max(0, s.quantity); return total; }
    static List<string> CardLabels(List<string> ids)
    {
        List<string> result = new List<string>(); if (ids == null) return result; GobboCardDatabase db = GobboCardDatabase.Instance;
        foreach (string id in ids) { if (string.IsNullOrWhiteSpace(id)) continue; GobboCard card = db != null && db.cards != null ? db.cards.FirstOrDefault(c => c != null && c.cardId == id) : null; result.Add(card != null && !string.IsNullOrWhiteSpace(card.cardName) ? card.cardName : Label(id)); }
        return result;
    }
    static List<string> Labels(List<string> values) { List<string> result = new List<string>(); if (values != null) foreach (string v in values) if (!string.IsNullOrWhiteSpace(v)) result.Add(Label(v)); return result; }
    static void AddUnique(List<string> values, string raw) { if (!string.IsNullOrWhiteSpace(raw)) AddLabel(values, Label(raw)); }
    static void AddLabel(List<string> values, string label) { if (!values.Contains(label)) values.Add(label); }
    static string TypeLabel(BuddyType type) { return type == BuddyType.Explosive ? "Boom" : Label(type.ToString()); }
    static string Label(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "None"; string text = value.Replace("_", " ").Replace("-", " ");
        for (int i = 1; i < text.Length; i++) if (char.IsUpper(text[i]) && !char.IsWhiteSpace(text[i - 1])) { text = text.Insert(i, " "); i++; }
        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text.ToLowerInvariant());
    }
}
