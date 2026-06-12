using System.Collections.Generic;

public static class CampReportTextBuilder
{
    public static string BuildRunStatsText(RunSummaryData run, GobboUnitSaveData leader)
    {
        if (run == null || leader == null) return "";

        return "You made it back to camp!" +
            "\n\nRun: " + run.runNumber +
            "\nLevel: " + run.playerLevelStart + " \u2192 " + run.playerLevelEnd +
            "\nXP gained: " + run.xpGained +
            "\nHealth: " + leader.health + " / " + leader.maxHealth +
            "\nAttack: " + leader.attack +
            "\nDefense: " + leader.defense +
            "\nDig Power: " + leader.digPower +
            "\nDig Radius: " + leader.digRadius.ToString("0.00") +
            "\n\nFood for the horde: " + run.foodValueGained +
            "\nSpores gained: " + run.sporesGained + " Total: " + leader.spores +
            "\nMushrooms gained: " + run.mushroomsGained + " Total: " + leader.mushrooms +
            "\nShinies gained: " + run.shiniesGained + " Total: " + leader.shinies +
            "\nEnemies killed: " + run.enemiesKilled;
    }

    public static string BuildMiddleSurvivorSummary(RunSummaryData run, List<GobboUnitSaveData> pending, int totalOwnedGobbos)
    {
        if (run == null) return "";
        pending ??= new List<GobboUnitSaveData>();

        string text = "Roll call";

        text += "\n\nNew buddies: " + run.buddiesFound;
        if (run.newBuddyNames != null && run.newBuddyNames.Count > 0)
        {
            foreach (string name in run.newBuddyNames)
                text += "\n+ " + name;
        }
        else text += "\n- Nobody new joined.";

        text += "\n\nBuddies lost: " + run.buddiesLost;
        if (run.deadBuddyNames != null && run.deadBuddyNames.Count > 0)
        {
            foreach (string name in run.deadBuddyNames)
                text += "\n- " + name;
        }
        else text += "\n- Nobody died.";

        text += "\n\nLeveled up:";
        if (run.leveledBuddyNames != null && run.leveledBuddyNames.Count > 0)
        {
            foreach (string name in run.leveledBuddyNames)
                text += "\n\u2191 " + name;
        }
        else text += "\n- Nobody leveled this time.";

        text += "\n\nReady to grow: " + pending.Count;
        if (pending.Count > 0)
            text += "\nPress the Grow Ready Buddy button before camp opens.";

        text += "\n\nTotal little guys: " + totalOwnedGobbos;
        return text;
    }

    public static string FormatRunBuddyReport(BuddyRunReport report)
    {
        if (report == null) return "Missing buddy report";

        string line = report.displayName;
        line += "\nLv " + report.levelStart + " \u2192 " + report.levelEnd;
        line += " | XP +" + report.xpGained;
        line += " | Kills +" + report.killsGained;
        line += "\nNights +" + report.nightsGained + " | Happy " + report.happinessEnd;
        if (!string.IsNullOrWhiteSpace(report.traitLabel) && report.traitLabel != "None")
            line += " | " + report.traitLabel;
        if (report.readyToGrow) line += "\nREADY TO GROW";
        if (report.died) line += "\nDIED";
        return line;
    }

    public static string FormatCampBuddyReport(BuddyRunReport report)
    {
        if (report == null) return "Missing buddy report";

        string line = report.displayName;
        line += "\nLv " + report.levelStart + " \u2192 " + report.levelEnd;
        line += " | Camp XP +" + report.xpGained;
        line += "\nNights " + report.nightsEnd + " | Happy " + report.happinessEnd;
        if (!string.IsNullOrWhiteSpace(report.traitLabel) && report.traitLabel != "None")
            line += " | " + report.traitLabel;
        if (report.readyToGrow) line += "\nREADY TO GROW";
        return line;
    }

    public static string FormatBuddyLine(GobboUnitSaveData buddy)
    {
        if (buddy == null) return "Missing buddy";
        buddy.EnsureRuntimeDefaults();
        string trait = buddy.traitIds != null && buddy.traitIds.Count > 0 ? buddy.traitIds[0] : "None";
        return buddy.displayName + " the " + buddy.gobboType + " " + buddy.ageStage +
            " Lv " + buddy.level +
            " XP " + buddy.xp + "/" + buddy.xpToNextLevel +
            " HP " + buddy.health + "/" + buddy.maxHealth +
            " Nights " + buddy.runsSurvived +
            " Kills " + buddy.kills +
            " Happy " + buddy.happiness +
            (trait != "None" ? " Trait " + trait : "") +
            (buddy.pendingEvolution ? " READY TO GROW" : "") +
            (buddy.runsWaitingForEvolution >= 2 ? " ANGRY DOT" : "");
    }
}
