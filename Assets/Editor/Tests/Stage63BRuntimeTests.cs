#if UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using SporeGobbo.Safety;

public class Stage63BRuntimeTests
{
    static SporeSaveSlotData Fresh()
    {
        var data=SporeSaveSlotData.CreateNew(2,"V1 contract fixture",GameSceneCatalog.Intro);
        data.campTerrainState.regionSnapshot=CampRegionSnapshot.Pristine();
        return data;
    }
    static string Encoded()=>V1CampaignSnapshot.Encode(Fresh(),V1RuntimeProfile.CampaignIdPrefix+"contract",1);
    [Test] public void ReturnCannotPrepareOutcomeWithoutSelectedCampaign()
    {
        string key=V1RuntimeProfile.PreferencePrefix+"CurrentSlot";
        bool present=Persistence.HasKey(key);int previous=Persistence.GetInt(key,0);
        try { Persistence.DeleteKey(key);Assert.Throws<InvalidDataException>(()=>RunReturnService.RequireSelectedCampaign()); }
        finally { if(present)Persistence.SetInt(key,previous);else Persistence.DeleteKey(key); }
    }
    [Test] public void NormalDestinationsDoNotRequireDevelopmentTools()
    {
        Assert.That(GameSceneCatalog.Intro,Is.EqualTo("FirstGobboIntro"));
        Assert.That(GameSceneCatalog.Camp,Is.EqualTo("FutureCampCandidate"));
        Assert.That(GameSceneCatalog.Run,Is.EqualTo("FirstExpedition"));
        Assert.That(GameSceneCatalog.Resolve("CampScene"),Is.EqualTo(GameSceneCatalog.Camp));
        Assert.That(GameSceneCatalog.ResolveIntro("SampleScene"),Is.EqualTo(GameSceneCatalog.Intro));
    }
    [TestCase(null,"normal")][TestCase("normal","normal")][TestCase("development","development")]
    public void ExplicitStorageProfiles(string input,string expected)=>Assert.That(V1RuntimeProfile.ParseStorageProfile(input),Is.EqualTo(expected));
    [Test] public void UnknownStorageFailsClosed()=>Assert.Throws<InvalidOperationException>(()=>V1RuntimeProfile.ParseStorageProfile("production-typo"));
    [Test] public void ProtectedIdentityRequiresIsolationIndependentOfFirstScene()
    {
        Assert.That(Persistence.ProtectedPlayerIdentity("SporeGobboSafety","Anything"),Is.True);
        Assert.That(Persistence.ProtectedPlayerIdentity("Other","Safety_V1"),Is.True);
        Assert.That(Persistence.ProtectedPlayerIdentity("Ordinary","SporeGobbo"),Is.False);
    }
    [Test] public void V1RoundTripUsesDetachedCampaignAndBoundedRegion()
    {
        var original=Fresh();string before=JsonUtility.ToJson(original);
        string text=V1CampaignSnapshot.Encode(original,V1RuntimeProfile.CampaignIdPrefix+"contract",7);
        var envelope=V1CampaignSnapshot.Decode(text,2,out var restored);
        Assert.That(envelope.generation,Is.EqualTo(7));Assert.That(envelope.storageProfile,Is.EqualTo(V1RuntimeProfile.StorageProfile));
        Assert.That(JsonUtility.ToJson(restored),Is.EqualTo(before));Assert.That(JsonUtility.ToJson(original),Is.EqualTo(before));
        Assert.That(restored,Is.Not.SameAs(original));
    }
    [Test] public void V1RejectsWrongSlotAndPayloadTampering()
    {
        string text=Encoded();Assert.Throws<InvalidDataException>(()=>V1CampaignSnapshot.Decode(text,1,out _));
        var envelope=JsonUtility.FromJson<V1CampaignSnapshot>(text);envelope.payload+=" ";
        Assert.Throws<InvalidDataException>(()=>V1CampaignSnapshot.Decode(JsonUtility.ToJson(envelope),2,out _));
    }
    [Test] public void V1RejectsUnknownVersionAndForeignProfile()
    {
        var envelope=JsonUtility.FromJson<V1CampaignSnapshot>(Encoded());envelope.version++;
        Assert.Throws<NotSupportedException>(()=>V1CampaignSnapshot.Decode(JsonUtility.ToJson(envelope),2,out _));
        envelope.version=V1CampaignSnapshot.CurrentVersion;envelope.storageProfile=V1RuntimeProfile.DevelopmentStorage?"normal":"development";
        Assert.Throws<NotSupportedException>(()=>V1CampaignSnapshot.Decode(JsonUtility.ToJson(envelope),2,out _));
    }
    [Test] public void LegacyCampaignsAreRecognizedWithoutMigration()
    {
        Assert.Throws<IncompatibleLegacyCampaignException>(()=>V1CampaignSnapshot.Decode(JsonUtility.ToJson(Fresh()),2,out _));
        string legacy=DevelopmentCampaignSnapshot.Encode(Fresh(),"development-contract",1);
        Assert.Throws<IncompatibleLegacyCampaignException>(()=>V1CampaignSnapshot.Decode(legacy,2,out _));
    }
    [Test] public void StrictCampaignValidationRejectsMissingRegionAndUnsupportedDtoVersion()
    {
        var data=Fresh();data.campTerrainState.regionSnapshot=null;
        Assert.Throws<InvalidDataException>(()=>V1CampaignSnapshot.Encode(data,V1RuntimeProfile.CampaignIdPrefix+"contract",1));
        data=Fresh();data.saveVersion++;
        Assert.Throws<InvalidDataException>(()=>V1CampaignSnapshot.Encode(data,V1RuntimeProfile.CampaignIdPrefix+"contract",1));
    }
}
#endif
