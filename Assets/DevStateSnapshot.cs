using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class DevStateRow
{
    public readonly string label;
    public readonly string value;
    public readonly string note;
    public DevStateRow(string label, object value, string note = "")
    { this.label = label; this.value = value != null ? value.ToString() : "null"; this.note = note; }
}

public sealed class DevStateSection
{
    public readonly string heading;
    public readonly List<DevStateRow> rows = new List<DevStateRow>();
    public DevStateSection(string heading) { this.heading = heading; }
    public DevStateSection Add(string label, object value, string note = "") { rows.Add(new DevStateRow(label, value, note)); return this; }
}

/// <summary>Read-only, transient development documentation built from authoritative state.</summary>
public static class DevStateSnapshot
{
    public static List<DevStateSection> Build(GameState source = null)
    {
        List<DevStateSection> result = new List<DevStateSection>();
        GameState state = source != null ? source : GameState.Instance;
        if (state == null) { result.Add(new DevStateSection("STATE").Add("GameState", "Missing")); return result; }
        state.EnsureRuntimeDefaults();

        CampaignFacts facts = state.campaignFacts;
        result.Add(new DevStateSection("CAMPAIGN / LINEAGE")
            .Add("Lineage ended", state.lineageEnded).Add("Current run", state.currentRunNumber).Add("Camp cycle", state.campCycleNumber)
            .Add("Mushrooms eaten", facts.mushroomsEaten).Add("Weevils killed", facts.weevilsKilled).Add("Blobs killed", facts.blobsKilled));

        RunSummaryData run = state.lastRun;
        result.Add(new DevStateSection(state.HasActiveRunSnapshot ? "CURRENT EXPEDITION" : "LAST FINALIZED EXPEDITION")
            .Add("Snapshot active", state.HasActiveRunSnapshot).Add("Run number", run.runNumber).Add("Survived", run.survived)
            .Add("Time", run.timeSpent.ToString("0.0") + "s").Add("Enemies killed", run.enemiesKilled)
            .Add("Mushrooms eaten", run.mushroomsEaten).Add("Weevils killed", run.weevilsKilled).Add("Blobs killed", run.blobsKilled)
            .Add("Food value", run.foodValueGained).Add("XP gained", run.xpGained).Add("Spores gained", run.sporesGained)
            .Add("Mushrooms gained", run.mushroomsGained).Add("Shinies gained", run.shiniesGained)
            .Add("Buddies found / lost", run.buddiesFound + " / " + run.buddiesLost)
            .Add("New buddies", Join(run.newBuddyNames)).Add("Dead buddies", Join(run.deadBuddyNames))
            .Add("Upgrades", Join(run.upgradesChosen)).Add("Leveled buddies", Join(run.leveledBuddyNames)));

        GobboUnitSaveData leader = state.GetLeader();
        result.Add(BuildGobbo(leader, "LEADER — " + (leader != null ? leader.displayName.ToUpperInvariant() : "MISSING")));
        List<GobboUnitSaveData> buddies = state.ownedGobbos;
        for (int i = 0; i < buddies.Count; i++)
            result.Add(BuildGobbo(buddies[i], "BUDDY " + (i + 1) + " — " + buddies[i].displayName.ToUpperInvariant()));

        result.Add(new DevStateSection("RESOURCES / INVENTORY")
            .Add("Mushrooms", leader != null ? leader.mushrooms : 0, "current balance, not Mushrooms Eaten")
            .Add("Shinies", leader != null ? leader.shinies : 0)
            .Add("Money", leader != null ? leader.money : 0, "compatibility alias")
            .Add("Spores", leader != null ? leader.spores : 0)
            .Add("Permanent item stacks", FormatStacks(state.itemStacks))
            .Add("Current-run snacks", FormatStacks(state.runSnackStacks)).Add("Run snacks finalized", state.runSnackFinalizedThisRun));

        StoryProgressData story = state.storyProgress;
        DevStateSection storySection = new DevStateSection("STORY").Add("Next unlock sequence", story.nextUnlockSequence);
        if (story.completedEvents.Count == 0) storySection.Add("Completed records", "[]");
        else foreach (StoryEventRecord record in story.completedEvents)
            storySection.Add(record.unlockSequence + ". " + record.eventId, "run " + record.runNumber + ", camp cycle " + record.campCycle);
        if (StoryEventCoordinator.HasActiveEvent)
            storySection.Add("Active Story Event", StoryEventCoordinator.ActiveEventId);
        result.Add(storySection);

        MapGenerator map = MapGenerator.Instance;
        PersistentTestCaveTerrainState cave = state.persistentTestCaveTerrain;
        DevStateSection world = new DevStateSection("WORLD / MAP")
            .Add("Scene", SceneManager.GetActiveScene().name).Add("SampleScene mode", SampleSceneModeController.CurrentMode)
            .Add("Run profile", Empty(RunProfileCoordinator.ActiveDisplayName) + " [" + Empty(RunProfileCoordinator.ActiveProfileId) + "]")
            .Add("Map dimensions", map != null && map.Data != null ? map.Data.width + " × " + map.Data.height : "not active")
            .Add("Cell size", map != null ? map.CellSize.ToString("0.###") : "not active")
            .Add("Persistent map ID", Empty(cave.mapId)).Add("Persistent layout version", cave.layoutVersion)
            .Add("Saved cleared cells", cave.clearedCells != null ? cave.clearedCells.Count : 0);
        RunProfile profile = map != null ? map.selectedProfile as RunProfile : null;
        PersistentTestCaveLayout layout = profile != null ? profile.persistentTestCaveLayout : null;
        world.Add("Authored reset pockets", layout != null && layout.pockets != null ? PocketNames(layout.pockets) : "none")
            .Add("Saved state matches layout", layout != null && cave.mapId == layout.mapId && cave.layoutVersion == layout.layoutVersion);
        result.Add(world);

        CampTerrainState camp = state.campTerrainState;
        result.Add(new DevStateSection("CAMP")
            .Add("Camp level", state.campLevel, "save-card value")
            .Add("Main chamber revealed", camp.mainChamberRevealed).Add("Layout revision", camp.layoutRevision)
            .Add("Residential stage", camp.residentialStage).Add("Established residential slots", camp.residentialSlotsEstablished)
            .Add("Ordinary cleared terrain cells", camp.clearedCellCoordinates.Count).Add("Memorial established", camp.memorialEstablished)
            .Add("Station unlocks", Join(state.unlockedStations), "no production writer where empty")
            .Add("Expansion regions", Join(camp.unlockedExpansionRegionIds), "future-facing")
            .Add("Decorations", Join(state.decorationsUnlocked), "future-facing")
            .Add("Active squad IDs", Join(state.activeSquadIds)).Add("Marked successor", Empty(state.markedSuccessorId)));

        DevStateSection deaths = new DevStateSection("DEATHS / MEMORIALS").Add("Records", state.deathHistory.Count,
            "memorial records snapshot generic kills, not newer species counters");
        foreach (DeadBuddyRecord death in state.deathHistory)
            deaths.Add(death.displayName + " [" + death.gobboId + "]", death.GetDisplayLine() + " | seen=" + death.memorialSeen);
        result.Add(deaths);
        return result;
    }

    static DevStateSection BuildGobbo(GobboUnitSaveData g, string heading)
    {
        DevStateSection s = new DevStateSection(heading);
        if (g == null) return s.Add("Record", "Missing");
        s.Add("Stable ID", g.uniqueId).Add("Role", g.isLeader ? "Leader" : "Buddy").Add("Status", g.isDead ? "Dead" : "Alive")
            .Add("Type / age", g.gobboType + " / " + g.ageStage).Add("Level / XP", g.level + " / " + g.xp + " of " + g.xpToNextLevel)
            .Add("Health", g.health + " / " + g.maxHealth).Add("Attack / damage alias / defense", g.attack + " / " + g.damage + " / " + g.defense)
            .Add("Move / attack range / cooldown", g.moveSpeed + " / " + g.attackRange + " / " + g.attackCooldown)
            .Add("Dash", g.dashSpeed + " speed, " + g.dashDuration + "s, " + g.dashCooldown + "s cooldown")
            .Add("Dig", g.digPower + " power, " + g.digRadius + " radius, " + g.digRange + " range, " + g.digTickRate + "s tick")
            .Add("Growth", g.pendingGrowthChoiceType + ", queue=" + g.pendingGrowthQueue.Count + ", evolutionPending=" + g.pendingEvolution)
            .Add("Happiness", g.happiness).Add("Loyalty", g.loyalty, "not used yet")
            .Add("Runs survived", g.runsSurvived).Add("Nights survived", g.nightsSurvived, "compatibility alias")
            .Add("Kills", g.kills).Add("Mushrooms eaten", g.mushroomsEaten).Add("Weevils killed", g.weevilsKilled).Add("Blobs killed", g.blobsKilled)
            .Add("Cause of death", Empty(g.causeOfDeath)).Add("Residential slot", g.campResidentialSlotId)
            .Add("Primary trait", Empty(g.primaryTraitId)).Add("Traits", Join(g.traitIds)).Add("Abilities", Join(g.abilityIds))
            .Add("Relationships", FormatRelationships(g.relationships), "not used yet")
            .Add("Cards", Join(g.chosenCardIds)).Add("Evolution history", Join(g.evolutionHistoryIds))
            .Add("Items", Join(g.itemIds)).Add("Mutations", Join(g.mutationIds)).Add("Upgrades", Join(g.upgradeIds))
            .Add("Unlocked abilities", Join(g.unlockedAbilities)).Add("Unlocked cosmetics", Join(g.unlockedCosmetics))
            .Add("Equipped cosmetics", Join(g.equippedCosmetics)).Add("Equipped hat", Empty(g.equippedHat))
            .Add("Snack bonuses", "HP " + g.snackMaxHealthBonus + ", ATK " + g.snackAttackBonus + ", DEF " + g.snackDefenseBonus);
        return s;
    }

    public static string Format(List<DevStateSection> sections)
    {
        StringBuilder text = new StringBuilder(8192);
        foreach (DevStateSection section in sections)
        {
            text.Append("<size=25><b>").Append(section.heading).Append("</b></size>\n");
            foreach (DevStateRow row in section.rows)
            {
                text.Append("<b>").Append(row.label).Append(":</b> ").Append(row.value);
                if (!string.IsNullOrWhiteSpace(row.note)) text.Append("  <color=#B8BA9E>(").Append(row.note).Append(")</color>");
                text.Append('\n');
            }
            text.Append('\n');
        }
        return text.ToString();
    }

    static string Join(List<string> values) => values == null || values.Count == 0 ? "[]" : "[" + string.Join(", ", values) + "]";
    static string Empty(string value) => string.IsNullOrWhiteSpace(value) ? "(none)" : value;
    static string FormatStacks(List<InventoryStackSaveData> stacks)
    {
        if (stacks == null || stacks.Count == 0) return "[]";
        List<string> values = new List<string>(); foreach (InventoryStackSaveData stack in stacks) if (stack != null) values.Add(stack.itemId + " ×" + stack.quantity);
        return "[" + string.Join(", ", values) + "]";
    }
    static string FormatRelationships(List<GobboRelationshipSaveData> entries)
    {
        if (entries == null || entries.Count == 0) return "[]";
        List<string> values = new List<string>(); foreach (GobboRelationshipSaveData r in entries) if (r != null) values.Add(r.otherGobboId + ":" + r.relationshipType + "=" + r.points);
        return "[" + string.Join(", ", values) + "]";
    }
    static string PocketNames(List<PersistentTestCaveLayout.Pocket> pockets)
    {
        List<string> values = new List<string>(); foreach (PersistentTestCaveLayout.Pocket pocket in pockets) if (pocket != null) values.Add(pocket.label + " (" + pocket.content + ")");
        return "[" + string.Join(", ", values) + "]";
    }
}
