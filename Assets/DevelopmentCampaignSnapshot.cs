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
        campaign=CampLayoutCompatibility.Prepare(campaign,true);
        ValidateCampaign(campaign);
        return envelope;
    }
    public static void ValidateCampaign(SporeSaveSlotData campaign) => CampaignSnapshotValidator.Validate(campaign);
}
