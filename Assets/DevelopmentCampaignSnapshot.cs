using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Versioned development format using the existing campaign DTO and one bounded Camp payload.</summary>
[Serializable]
public sealed class DevelopmentCampaignSnapshot
{
    public const string Format = "SporeGobbo.DevelopmentCampaign";
    public string format, campaignId, payload, sha256;
    public int version;
    public long generation;

    public static string Encode(SporeSaveSlotData campaign, string id, long generation)
    {
        ValidateCampaign(campaign);
        if (string.IsNullOrWhiteSpace(id) || !id.StartsWith("development-", StringComparison.Ordinal) || generation < 1)
            throw new InvalidDataException("Invalid development campaign identity/generation");
        string payload = JsonUtility.ToJson(campaign);
        return JsonUtility.ToJson(new DevelopmentCampaignSnapshot
        { format = Format, version = 10, campaignId = id, generation = generation, payload = payload, sha256 = CampaignSnapshotStorage.Hash(payload) }, true);
    }
    public static DevelopmentCampaignSnapshot Decode(string text, int expectedSlot, out SporeSaveSlotData campaign)
    {
        var envelope = JsonUtility.FromJson<DevelopmentCampaignSnapshot>(text);
        if (envelope == null || envelope.format != Format || envelope.version != 10)
            throw new NotSupportedException("Unsupported development campaign format/version; original preserved");
        if (string.IsNullOrWhiteSpace(envelope.campaignId) || !envelope.campaignId.StartsWith("development-", StringComparison.Ordinal) ||
            envelope.generation < 1 || string.IsNullOrEmpty(envelope.payload) || envelope.sha256 != CampaignSnapshotStorage.Hash(envelope.payload))
            throw new InvalidDataException("Campaign checksum/identity validation failed");
        campaign = JsonUtility.FromJson<SporeSaveSlotData>(envelope.payload);
        if (campaign == null || campaign.slotIndex != expectedSlot) throw new InvalidDataException("Campaign slot identity mismatch");
        ValidateCampaign(campaign);
        return envelope;
    }
    public static void ValidateCampaign(SporeSaveSlotData campaign)
    {
        if (campaign == null || !campaign.hasSave || campaign.saveVersion != SporeSaveSlotData.CurrentSaveVersion ||
            campaign.slotIndex < 1 || campaign.slotIndex > SporeSaveManager.SlotCount || campaign.leader == null ||
            string.IsNullOrWhiteSpace(campaign.leader.uniqueId) || !campaign.leader.isLeader || campaign.leader.campResidentialSlotId != 0 ||
            campaign.ownedGobbos == null || campaign.activeSquadIds == null || campaign.deathHistory == null || campaign.campTerrainState?.regionSnapshot == null)
            throw new InvalidDataException("Incomplete development campaign snapshot");
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
        campaign.campTerrainState.regionSnapshot.ValidateCampaign(campaign);
    }
}
