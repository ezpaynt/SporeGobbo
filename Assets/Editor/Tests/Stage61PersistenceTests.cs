#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using SporeGobbo.Safety;
public sealed class Stage61PersistenceTests
{
    static SporeSaveSlotData Fresh()
    {
        var data = SporeSaveSlotData.CreateNew(3, "Development recovery C", "CampScene");
        data.campTerrainState.regionSnapshot = CampRegionSnapshot.Pristine();
        return data;
    }
    [Test] public void DevelopmentEnvelopeRoundTripsDetachedCampaignAndPristineRegion()
    {
        var original = Fresh();
        string text = DevelopmentCampaignSnapshot.Encode(original, "development-recovery-c", 1);
        var envelope = DevelopmentCampaignSnapshot.Decode(text, 3, out var loaded);
        Assert.That(envelope.generation, Is.EqualTo(1));
        Assert.That(loaded.leader.uniqueId, Is.EqualTo(original.leader.uniqueId));
        loaded.campTerrainState.regionSnapshot.establishedBeds = new[] {1};
        Assert.That(original.campTerrainState.regionSnapshot.establishedBeds, Is.Empty);
    }
    [Test] public void UnsupportedVersionChecksumAndSlotAreRejected()
    {
        string text = DevelopmentCampaignSnapshot.Encode(Fresh(), "development-recovery-c", 1);
        Assert.Throws<NotSupportedException>(() => DevelopmentCampaignSnapshot.Decode(text.Replace("\"version\": 10", "\"version\": 99"), 3, out _));
        Assert.Throws<InvalidDataException>(() => DevelopmentCampaignSnapshot.Decode(text, 2, out _));
        var envelope = JsonUtility.FromJson<DevelopmentCampaignSnapshot>(text); envelope.payload += " ";
        Assert.Throws<InvalidDataException>(() => DevelopmentCampaignSnapshot.Decode(JsonUtility.ToJson(envelope), 3, out _));
    }
    [Test] public void IncompleteBedsAndProtectedExcavationAreRejected()
    {
        var data = Fresh(); data.campTerrainState.regionSnapshot.establishedBeds = new[] {1};
        Assert.Throws<InvalidDataException>(() => DevelopmentCampaignSnapshot.ValidateCampaign(data));
        data = Fresh(); data.campTerrainState.regionSnapshot.excavatedCells = new[] {0}; data.campTerrainState.regionSnapshot.terrainRevision = 1;
        Assert.Throws<InvalidDataException>(() => DevelopmentCampaignSnapshot.ValidateCampaign(data));
    }
    [Test] public void InterruptedWritesKeepPrimaryAndBackupAndPreserveFailureEvidence()
    {
        string directory = Persistence.ExperimentPath("Stage61D/" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "campaign-c.json");
        Action<string> validate = text => DevelopmentCampaignSnapshot.Decode(text, 3, out _);
        string first = DevelopmentCampaignSnapshot.Encode(Fresh(), "development-recovery-c", 1);
        string second = DevelopmentCampaignSnapshot.Encode(Fresh(), "development-recovery-c", 2);
        CampaignSnapshotStorage.Commit(path, first, validate);
        CampaignSnapshotStorage.Commit(path, second, validate);
        foreach (string stage in new[] {"flushed", "validated"})
        {
            Assert.Throws<IOException>(() => CampaignSnapshotStorage.Commit(path, first, validate, at => {if(at == stage)throw new IOException("synthetic interruption");}));
            Assert.That(CampaignSnapshotStorage.Read(path), Is.EqualTo(second));
            Assert.That(CampaignSnapshotStorage.Read(path + ".bak"), Is.EqualTo(first));
        }
        Assert.That(Directory.GetFiles(directory, "*.pending-*").Length, Is.EqualTo(2));
        File.WriteAllText(path, "synthetic corrupt primary");
        Assert.Throws<ArgumentException>(() => CampaignSnapshotStorage.Commit(path, second, validate));
        CampaignSnapshotStorage.Recover(path, validate);
        Assert.That(CampaignSnapshotStorage.Read(path), Is.EqualTo(first));
        Assert.That(CampaignSnapshotStorage.Read(path + ".bak"), Is.EqualTo(first));
        Assert.That(Directory.GetFiles(directory, "*.failed-*").Length, Is.EqualTo(1));
    }
    [Test] public void DeathNormalizationDoesNotReviveUnresolvedLeader()
    {
        var data = Fresh(); data.leader.isDead = true; data.leader.health = 0;
        data.Normalize();
        Assert.That(data.leader.isDead, Is.True); Assert.That(data.leader.health, Is.Zero);
        Assert.Throws<InvalidDataException>(() => DevelopmentCampaignSnapshot.ValidateCampaign(data));
    }
    [Test] public void DeathRemovesRosterAndSquadBeforeMemorialAutosave()
    {
        string current="SporeGobbo_CurrentSlot",last="SporeGobbo_LastPlayedSlot";
        int oldCurrent=Persistence.GetInt(current,0),oldLast=Persistence.GetInt(last,0);
        var host=new GameObject("owned death transaction test");
        try
        {
            Persistence.DeleteKey(current);Persistence.DeleteKey(last);
            var state=host.AddComponent<GameState>();
            var unit=new GobboUnitSaveData{uniqueId="development-death-order",displayName="Death order",isDead=true,health=0};
            state.ownedGobbos.Add(unit);state.activeSquadIds.Add(unit.uniqueId);
            state.RegisterGobboDeath(unit);
            Assert.That(state.ownedGobbos.Exists(g=>g.uniqueId==unit.uniqueId),Is.False);
            Assert.That(state.activeSquadIds.Contains(unit.uniqueId),Is.False);
            Assert.That(state.deathHistory.Exists(record=>record.gobboId==unit.uniqueId),Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            if(oldCurrent>0)Persistence.SetInt(current,oldCurrent);else Persistence.DeleteKey(current);
            if(oldLast>0)Persistence.SetInt(last,oldLast);else Persistence.DeleteKey(last);
        }
    }
    [Test] public void StaleArrivalAcknowledgementCannotConsumeNewArrival()
    {
        CampArrivalContext.SetPending(CampArrivalMode.NewGameIntro); int first = CampArrivalContext.Generation;
        CampArrivalContext.SetPending(CampArrivalMode.ReturnedFromRun);
        Assert.That(CampArrivalContext.Acknowledge(first), Is.False);
        Assert.That(CampArrivalContext.PeekOrDefault(), Is.EqualTo(CampArrivalMode.ReturnedFromRun));
        Assert.That(CampArrivalContext.Acknowledge(CampArrivalContext.Generation), Is.True);
        CampArrivalContext.Clear();
    }
    [Test] public void DurableReturnContextRoundTripsButPendingDeathCannotCommit()
    {
        var data=Fresh();data.pendingCampArrivalMode=(int)CampArrivalMode.ReturnedFromRun;data.pendingCampArrivalId=Guid.NewGuid().ToString("N");
        string text=DevelopmentCampaignSnapshot.Encode(data,"development-arrival",3);
        DevelopmentCampaignSnapshot.Decode(text,3,out var loaded);
        Assert.That(loaded.pendingCampArrivalId,Is.EqualTo(data.pendingCampArrivalId));
        Assert.That(loaded.pendingCampArrivalMode,Is.EqualTo((int)CampArrivalMode.ReturnedFromRun));
        data.pendingCampArrivalMode=(int)CampArrivalMode.PostDeathSuccession;
        Assert.Throws<InvalidDataException>(()=>DevelopmentCampaignSnapshot.Encode(data,"development-arrival",4));
    }
    [Test] public void RealSuccessorPromotionVacatesItsOldBed()
    {
        var host=new GameObject("owned promotion ownership test");
        try
        {
            var state=host.AddComponent<GameState>();var successor=new GobboUnitSaveData{uniqueId="development-successor",displayName="Successor",campResidentialSlotId=7,health=5,maxHealth=5};
            state.ownedGobbos.Add(successor);state.activeSquadIds.Add(successor.uniqueId);
            Assert.That(state.PromoteBuddyToLeader(successor.uniqueId),Is.True);
            Assert.That(state.GetLeader().campResidentialSlotId,Is.Zero);
            Assert.That(state.ownedGobbos.Exists(unit=>unit.uniqueId==successor.uniqueId),Is.False);
            Assert.That(state.activeSquadIds.Contains(successor.uniqueId),Is.False);
        }
        finally{UnityEngine.Object.DestroyImmediate(host);}
    }
    [Test] public void CandidateSuccessionButtonCannotRunBothSerializedAndRuntimeCommands()
    {
        var root=new GameObject("owned serialized click target");var child=new GameObject("owned click child");var buttonHost=new GameObject("owned candidate click",typeof(RectTransform));
        try
        {
            child.transform.SetParent(root.transform);var button=buttonHost.AddComponent<UnityEngine.UI.Button>();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick,root.transform.DetachChildren);
            Assert.That(button.onClick.GetPersistentEventCount(),Is.EqualTo(1));
            int calls=0;CampSuccessionUI.BindOwnedButton(button,()=>calls++,true);button.onClick.Invoke();
            Assert.That(calls,Is.EqualTo(1));Assert.That(button.onClick.GetPersistentEventCount(),Is.Zero);
            Assert.That(child.transform.parent,Is.EqualTo(root.transform),"Old serialized command must not execute alongside the candidate command");
        }
        finally{UnityEngine.Object.DestroyImmediate(buttonHost);UnityEngine.Object.DestroyImmediate(child);UnityEngine.Object.DestroyImmediate(root);}
    }
    [Test] public void ReviewedFixturesHaveDistinctCampaignsAndValidPhysicalOwnership()
    {
        var ids=new System.Collections.Generic.HashSet<string>();
        for(int slot=1;slot<=3;slot++)
        {
            string name=slot==1?"FreshA":slot==2?"EstablishedB":"RecoveryC";
            var asset=Resources.Load<TextAsset>("Stage61Fixtures/"+name);Assert.That(asset,Is.Not.Null);
            var envelope=DevelopmentCampaignSnapshot.Decode(asset.text,slot,out var data);Assert.That(ids.Add(envelope.campaignId),Is.True);
            Assert.That(data.campTerrainState.regionSnapshot.establishedBeds.Length,Is.EqualTo(slot==2?10:0));
            if(slot==1)Assert.That(SporeSaveManager.ShouldLoadIntro(data),Is.True);
        }
    }
    [Test] public void OwnedPlayerLeaseRejectsSecondWriterAndReleasesWithoutDeletingHistory()
    {
        string path=Persistence.ExperimentPath(".spore-player-storage-lock");
        using(var first=DevelopmentCampEntry.AcquireOwnedStorageLease())
            Assert.Throws<IOException>(()=>{using(var second=DevelopmentCampEntry.AcquireOwnedStorageLease()){} });
        using(var after=DevelopmentCampEntry.AcquireOwnedStorageLease())Assert.That(after.CanWrite,Is.True);
        Assert.That(File.Exists(path),Is.True);
    }
    [Test] public void SharedAuthoredDefinitionNeverSharesMutableOccupancy()
    {
        var definition=SporeGobbo.CampCandidate.SettlementCampLayout.Shared;
        Assert.That(ReferenceEquals(definition,SporeGobbo.CampCandidate.SettlementCampLayout.Shared),Is.True);
        int id=Enumerable.Range(0,192*192).First(i=>definition.Category(i%192,i/192)==SporeGobbo.CampCandidate.CampCell.Expansion);
        var cut=new SporeGobbo.Geometry.DualTile(id%192,id/192);
        var first=new SporeGobbo.Geometry.RegionTerrainState("owned-a",192,192,definition);var second=new SporeGobbo.Geometry.RegionTerrainState("owned-b",192,192,definition);
        Assert.That(first.Commit(new[]{cut},0,c=>true),Is.EqualTo(1));
        Assert.That(first.World.IsSolid(cut.X,cut.Y),Is.False);Assert.That(second.World.IsSolid(cut.X,cut.Y),Is.True);
        Assert.That(definition.Category(cut.X,cut.Y),Is.EqualTo(SporeGobbo.CampCandidate.CampCell.Expansion));
    }
    [Test] public void PhysicalValidationCacheIsBoundedAndCannotHideMutationOrBadOwnership()
    {
        var definition=SporeGobbo.CampCandidate.SettlementCampLayout.Shared;
        foreach(int id in Enumerable.Range(0,192*192).Where(i=>definition.Category(i%192,i/192)==SporeGobbo.CampCandidate.CampCell.Expansion).Take(12))
        {var snapshot=CampRegionSnapshot.Pristine();snapshot.excavatedCells=new[]{id};snapshot.terrainRevision=1;snapshot.ValidatePhysical();}
        Assert.That(CampRegionSnapshot.PhysicalValidationCacheEntries,Is.LessThanOrEqualTo(8));
        var modified=CampRegionSnapshot.Pristine();modified.ValidatePhysical();modified.excavatedCells=new[]{0};modified.terrainRevision=1;
        Assert.Throws<InvalidDataException>(()=>modified.ValidatePhysical());
        var asset=Resources.Load<TextAsset>("Stage61Fixtures/EstablishedB");DevelopmentCampaignSnapshot.Decode(asset.text,2,out var data);
        data.campTerrainState.regionSnapshot.ValidatePhysical();data.ownedGobbos[0].campResidentialSlotId=1;data.ownedGobbos[1].campResidentialSlotId=1;
        Assert.Throws<InvalidDataException>(()=>DevelopmentCampaignSnapshot.ValidateCampaign(data));
    }
}
#endif
