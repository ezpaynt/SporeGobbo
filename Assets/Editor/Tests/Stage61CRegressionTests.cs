#if UNITY_EDITOR
using System;using System.Collections.Generic;using System.Linq;using System.IO;using NUnit.Framework;using UnityEngine;using SporeGobbo.CampCandidate;using SporeGobbo.Geometry;
public sealed class Stage61CRegressionTests
{
 [Test]public void PriorLayoutDeltasAreRejectedEvenWhenEmpty(){var s=CampRegionSnapshot.Pristine();s.layoutId="camp.settlement.61b.v1";Assert.Throws<InvalidDataException>(()=>s.ValidatePhysical());}
 [Test]public void FirstDormitoryAndOptionalRootAreIndependent(){var a=SettlementCampLayout.Create();var t=new RegionTerrainState(a.Id,192,192,a);foreach(var room in a.Bedrooms)Assert.That(t.Route(a.Marker("fire").Interaction,room.Entrance,3).Count,Is.GreaterThan(0),room.Id);Assert.That(SettlementCampLayout.RootCells().All(c=>a.Category(c.X,c.Y)==CampCell.FirstBuddy),Is.True);Assert.That(a.Marker("shop.reserved").Anchor.X,Is.GreaterThan(120));Assert.That(a.Category(84,111),Is.EqualTo(CampCell.Open));}
 [TestCase(false)][TestCase(true)]public void NewRecruitIsNeverReportedAsOriginalSquadOrCampResident(bool lostSquad)
 {
  var go=new GameObject("owned report fixture");try{var state=go.AddComponent<GameState>();var leader=new GobboUnitSaveData{uniqueId="leader",health=10,maxHealth=10};state.leader=leader;var squad=new GobboUnitSaveData{uniqueId="departed",displayName="Departed",health=10,maxHealth=10};var resident=new GobboUnitSaveData{uniqueId="resident",displayName="Resident",health=10,maxHealth=10};var recruit=new GobboUnitSaveData{uniqueId="recruit",displayName="Recruited",health=10,maxHealth=10};state.ownedGobbos.Clear();if(!lostSquad)state.ownedGobbos.Add(squad);state.ownedGobbos.Add(resident);state.ownedGobbos.Add(recruit);
   RunSummaryService.BuildRunSummary(state,leader,new List<string>{"departed","resident"},new List<GobboUnitSaveData>{squad,resident},new List<string>{"departed"},new List<string>(),0,true);
   Assert.That(state.lastRun.activeBuddyReports.Single().buddyId,Is.EqualTo("departed"));Assert.That(state.lastRun.activeBuddyReports.Single().died,Is.EqualTo(lostSquad));Assert.That(state.lastRun.reserveBuddyReports.Single(r=>r.buddyId=="recruit").role,Is.EqualTo("Recruited this run"));Assert.That(state.lastRun.reserveBuddyReports.Single(r=>r.buddyId=="resident").role,Is.EqualTo("Stayed at Camp"));Assert.That(state.lastRun.newBuddyNames,Does.Contain("Recruited the Baby"));
  }finally{UnityEngine.Object.DestroyImmediate(go);}
 }
 [Test]public void RealEmptyDepartureSnapshotRemainsEmptyAfterFirstRecruit()
 {
  var host=new GameObject("owned empty departure regression");try{var state=host.AddComponent<GameState>();state.ownedGobbos.Clear();state.activeSquadIds.Clear();Assert.That(state.TryBeginRunSnapshot(),Is.True);state.ownedGobbos.Add(new GobboUnitSaveData{uniqueId="first-recruit",displayName="First recruit",health=8,maxHealth=10});state.activeSquadIds.Add("first-recruit");state.SaveFromRun();Assert.That(state.lastRun.buddiesStart,Is.Zero);Assert.That(state.lastRun.activeBuddyReports,Is.Empty);Assert.That(state.lastRun.reserveBuddyReports.Single().role,Is.EqualTo("Recruited this run"));Assert.That(state.lastRun.buddiesFound,Is.EqualTo(1));}finally{UnityEngine.Object.DestroyImmediate(host);}
 }
 [Test]public void OptionalPocketCannotBeReachedByOrdinaryDirtBypass()
 {var a=SettlementCampLayout.Create();var seen=new HashSet<DualTile>();var queue=new Queue<DualTile>();queue.Enqueue(new DualTile(84,116));while(queue.Count>0){var p=queue.Dequeue();if(!seen.Add(p))continue;Assert.That(p.X==129&&p.Y==95,Is.False,"optional root bypass");foreach(var d in new[]{new DualTile(-1,0),new DualTile(1,0),new DualTile(0,-1),new DualTile(0,1)}){var n=new DualTile(p.X+d.X,p.Y+d.Y);if(n.X<0||n.X>=192||n.Y<0||n.Y>=192)continue;var k=a.Category(n.X,n.Y);if(k!=CampCell.Permanent&&k!=CampCell.FirstBuddy&&!seen.Contains(n))queue.Enqueue(n);}}}
 [Test]public void CentralCirculationHasNoRetiredOneCellDebris()
 {var a=SettlementCampLayout.Create();foreach(var p in new[]{new DualTile(106,123),new DualTile(107,107),new DualTile(59,103)})Assert.That(a.Category(p.X,p.Y),Is.EqualTo(CampCell.Open));var world=new RegionTerrainState(a.Id,192,192,a);Assert.That(world.Route(a.Marker("fire").Interaction,new Point2(96.5,136.5),3).Count,Is.GreaterThan(0));}
 [Test]public void ExistingFireAndResourceFramesHaveMeasuredContactCalibration()
 {foreach(string path in new[]{"Assets/logs.png","Assets/fire1.png","Assets/Fire2.png","Assets/fire3.png","Assets/Resources/singleMush.png","Assets/Resources/2mush.png","Assets/Resources/3mush.png","Assets/Resources/shiny1.png","Assets/Resources/shiny2.png","Assets/Resources/shiny3.png"})foreach(var sprite in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()){Assert.That(CampSpriteContacts.HasCalibration(sprite),Is.True,sprite.name);var metrics=CampSpriteContacts.Frame(sprite);Assert.That(metrics.x,Is.GreaterThan(0));Assert.That(metrics.y,Is.InRange(0,.5f));}}
}
#endif
