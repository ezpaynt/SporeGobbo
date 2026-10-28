using System;
using System.IO;
using UnityEngine;

/// <summary>The V1 envelope around the existing detached campaign DTO. It owns no live game state.</summary>
[Serializable]
public sealed class V1CampaignSnapshot
{
    public const string Format = "SporeGobbo.V1Campaign";
    public const int CurrentVersion = 1;
    public string format, storageProfile, campaignId, payload, sha256;
    public int version;
    public long generation;
    static readonly Unity.Profiling.ProfilerMarker serialize = new Unity.Profiling.ProfilerMarker("Spore.V1.SaveSerialize");
    static readonly Unity.Profiling.ProfilerMarker validate = new Unity.Profiling.ProfilerMarker("Spore.V1.SaveValidate");

    public static string Encode(SporeSaveSlotData campaign, string id, long generation)
    {
        using (validate.Auto()) CampaignSnapshotValidator.Validate(campaign);
        if (string.IsNullOrWhiteSpace(id) || !id.StartsWith(V1RuntimeProfile.CampaignIdPrefix, StringComparison.Ordinal) || generation < 1)
            throw new InvalidDataException("Invalid V1 campaign identity/generation");
        using (serialize.Auto())
        {
            string payload = JsonUtility.ToJson(campaign);
            return JsonUtility.ToJson(new V1CampaignSnapshot { format = Format, version = CurrentVersion,
                storageProfile = V1RuntimeProfile.StorageProfile, campaignId = id, generation = generation,
                payload = payload, sha256 = CampaignSnapshotStorage.Hash(payload) }, true);
        }
    }
    public static V1CampaignSnapshot Decode(string text, int expectedSlot, out SporeSaveSlotData campaign)
    {
        using var profile = validate.Auto();
        var envelope = JsonUtility.FromJson<V1CampaignSnapshot>(text);
        if (envelope != null && envelope.format == "SporeGobbo.DevelopmentCampaign" ||
            envelope != null && string.IsNullOrEmpty(envelope.format) && text.Contains("\"saveVersion\"") && text.Contains("\"leader\""))
            throw new IncompatibleLegacyCampaignException("Legacy campaign preserved. V1 requires a fresh campaign; import is not implemented.");
        if (envelope == null || string.IsNullOrEmpty(envelope.format)) throw new InvalidDataException("Malformed V1 campaign envelope");
        if (envelope.format != Format || envelope.version != CurrentVersion)
            throw new NotSupportedException("Unsupported V1 campaign format/version; original preserved");
        if (envelope.storageProfile != V1RuntimeProfile.StorageProfile)
            throw new NotSupportedException("Campaign belongs to a different V1 storage profile");
        if (string.IsNullOrWhiteSpace(envelope.campaignId) || !envelope.campaignId.StartsWith(V1RuntimeProfile.CampaignIdPrefix, StringComparison.Ordinal) ||
            envelope.generation < 1 || string.IsNullOrEmpty(envelope.payload) || envelope.sha256 != CampaignSnapshotStorage.Hash(envelope.payload))
            throw new InvalidDataException("V1 campaign checksum/identity validation failed");
        campaign = JsonUtility.FromJson<SporeSaveSlotData>(envelope.payload);
        if (campaign == null || campaign.slotIndex != expectedSlot) throw new InvalidDataException("V1 campaign slot identity mismatch");
        campaign=CampLayoutCompatibility.Prepare(campaign,false);
        CampaignSnapshotValidator.Validate(campaign);
        return envelope;
    }
}

public sealed class IncompatibleLegacyCampaignException : NotSupportedException
{
    public IncompatibleLegacyCampaignException(string message) : base(message) { }
}
