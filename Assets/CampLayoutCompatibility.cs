using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using SporeGobbo.Geometry;using SporeGobbo.CampCandidate;using SporeGobbo.Safety;
/// <summary>One detached 61C development-layout adapter. Never writes files, repairs roster, or changes live authorities.</summary>
public static class CampLayoutCompatibility
{
 public const string Accepted61CFingerprint="5DEDC732D91F3F48";
 public static SporeSaveSlotData Prepare(SporeSaveSlotData incoming,bool retainedFixtureCodec)
 {
  var snapshot=incoming?.campTerrainState?.regionSnapshot;
  if(snapshot==null||snapshot.layoutId!=LegacyCamp61CLayout.LayoutId)return incoming;
  if(!Persistence.Isolated||!retainedFixtureCodec&&V1RuntimeProfile.StorageProfile!="development")throw new NotSupportedException("61C Camp layout requires explicitly isolated development compatibility; original preserved");
  _ = Persistence.ExperimentRoot; // Validate token, owned marker and link-free ancestry before adapting.
  var old=LegacyCamp61CLayout.Shared;var next=SettlementCampLayout.Shared;
  if(old.Fingerprint()!=Accepted61CFingerprint)throw new NotSupportedException("Accepted 61C authored data differs from compatibility checkpoint");
  CampaignSnapshotValidator.ValidateForLayout(incoming,old);
  var mapped=new HashSet<int>();var roomMap=new Dictionary<int,int>();
  foreach(var room in old.Bedrooms)
  {
   var moved=next.Bedrooms.Single(r=>r.Id==room.Id);if(moved.FirstSlot!=room.FirstSlot||moved.Beds.Count!=room.Beds.Count)throw Unsupported("bed identities changed");
   for(int i=0;i<room.Beds.Count;i++)if(moved.Beds[i].X!=room.Beds[i].X+SettlementCampLayout.DormShiftX||moved.Beds[i].Y!=room.Beds[i].Y+SettlementCampLayout.DormShiftY)throw Unsupported("unrecognized bed relocation");
   foreach(var cell in room.Cells)if(old.Category(cell.X,cell.Y)==CampCell.Residential)
   {int from=cell.Y*192+cell.X,to=(cell.Y+SettlementCampLayout.DormShiftY)*192+cell.X+SettlementCampLayout.DormShiftX;if(roomMap.TryGetValue(from,out int prior)&&prior!=to)throw Unsupported("ambiguous room mapping");roomMap[from]=to;}
  }
  void Preserve(int index)
  {
   if(index<0||index>=192*192)throw Unsupported("mapped cell outside Camp");var category=next.Category(index%192,index/192);
   if(category==CampCell.Open)return;
   if(category!=CampCell.Expansion&&category!=CampCell.Residential&&!(snapshot.rootOpened&&category==CampCell.FirstBuddy))throw Unsupported("existing progress conflicts with new permanent structure");
   mapped.Add(index);
  }
  // Existing authored floor is progress too. Do not silently close it with new walls or shoulders.
  for(int y=0;y<192;y++)for(int x=0;x<192;x++)if(old.Category(x,y)==CampCell.Open)Preserve(y*192+x);
  foreach(int index in snapshot.excavatedCells)
  {
   if(old.Category(index%192,index/192)==CampCell.Residential){if(!roomMap.TryGetValue(index,out int relocated))throw Unsupported("unowned residential excavation");Preserve(relocated);}
   else Preserve(index);
  }
  // Already established beds remain established after their approved physical relocation.
  var catalog=SettlementCampLayout.Catalog(next);foreach(int bed in snapshot.establishedBeds)foreach(var cell in catalog.GetSlot(bed).ExcavationFootprint)Preserve(cell.y*192+cell.x);
  var detached=JsonUtility.FromJson<SporeSaveSlotData>(JsonUtility.ToJson(incoming));var result=snapshot.Clone();result.layoutId=next.Id;result.excavatedCells=mapped.OrderBy(i=>i).ToArray();result.terrainRevision=mapped.Count==0?0:1;detached.campTerrainState.regionSnapshot=result;
  CampaignSnapshotValidator.Validate(detached);return detached;
 }
 static NotSupportedException Unsupported(string reason)=>new NotSupportedException("61C Camp layout cannot be losslessly adapted: "+reason+". Original preserved; use its accepted candidate or review the conflict.");
}
