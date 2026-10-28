using System;
using SporeGobbo.CampCandidate;
using System.IO;
using System.Linq;
using System.Collections.Generic;

/// <summary>One strict detached campaign/region validator shared by V1 and retained fixture decoding.</summary>
public static class CampaignSnapshotValidator
{
    public static void Validate(SporeSaveSlotData campaign)=>ValidateForLayout(campaign,SettlementCampLayout.Shared);
    internal static void ValidateForLayout(SporeSaveSlotData campaign,AuthoredCamp definition)
    {
        if (campaign == null || !campaign.hasSave || campaign.saveVersion != SporeSaveSlotData.CurrentSaveVersion ||
            campaign.slotIndex < 1 || campaign.slotIndex > SporeSaveManager.SlotCount || campaign.leader == null ||
            string.IsNullOrWhiteSpace(campaign.leader.uniqueId) || !campaign.leader.isLeader || campaign.leader.campResidentialSlotId != 0 ||
            campaign.ownedGobbos == null || campaign.activeSquadIds == null || campaign.deathHistory == null || campaign.campTerrainState?.regionSnapshot == null)
            throw new InvalidDataException("Incomplete campaign snapshot");
        if (!campaign.lineageEnded && (campaign.leader.isDead || campaign.leader.health <= 0))
            throw new InvalidDataException("Unresolved succession cannot be committed");
        if (campaign.pendingCampArrivalMode != -1 && campaign.pendingCampArrivalMode != (int)CampArrivalMode.ReturnedFromRun && campaign.pendingCampArrivalMode != (int)CampArrivalMode.NewGameIntro && campaign.pendingCampArrivalMode != (int)CampArrivalMode.LoadedSave ||
            campaign.pendingCampArrivalMode >= 0 && !Guid.TryParseExact(campaign.pendingCampArrivalId,"N",out _) ||
            campaign.pendingCampArrivalMode == -1 && !string.IsNullOrEmpty(campaign.pendingCampArrivalId))
            throw new InvalidDataException("Invalid or unresolved persisted arrival context");
        var identities = new HashSet<string> { campaign.leader.uniqueId };
        var living = new HashSet<string>();
        foreach (var unit in campaign.ownedGobbos)
        {
            if (unit == null || string.IsNullOrWhiteSpace(unit.uniqueId) || unit.isLeader || !identities.Add(unit.uniqueId))
                throw new InvalidDataException("Invalid or duplicate roster identity");
            if (!unit.isDead && unit.health > 0) living.Add(unit.uniqueId);
        }
        if (campaign.activeSquadIds.Count > campaign.maxActiveSquad || campaign.activeSquadIds.Distinct().Count() != campaign.activeSquadIds.Count ||
            campaign.activeSquadIds.Any(id => !living.Contains(id))) throw new InvalidDataException("Invalid active squad references");
        foreach (var record in campaign.deathHistory)
            if (record != null && !string.IsNullOrEmpty(record.gobboId) &&
                (living.Contains(record.gobboId) || !campaign.leader.isDead && record.gobboId == campaign.leader.uniqueId))
                throw new InvalidDataException("Living identity reuses a deceased Gobbo identity");
        if (!string.IsNullOrEmpty(campaign.markedSuccessorId) && !living.Contains(campaign.markedSuccessorId))
            throw new InvalidDataException("Marked successor is not a living roster member");
        campaign.campTerrainState.regionSnapshot.ValidateCampaign(campaign,definition);
    }
}
