using System;using System.Collections.Generic;using UnityEngine;
/// <summary>Small explicit catalog of implemented opponents. No spawning authority or generic factory.</summary>
public static class CampFightPitCatalog
{
 public sealed class Entry
 {public readonly string Id,Name;public readonly bool ExistingBuddy;readonly Func<SettlementCampRegion,GameObject> prefab;public Entry(string id,string name,bool buddy,Func<SettlementCampRegion,GameObject> prefab){Id=id;Name=name;ExistingBuddy=buddy;this.prefab=prefab;}public GameObject Prefab(SettlementCampRegion region)=>prefab?.Invoke(region);}
 public static readonly IReadOnlyList<Entry> Entries=Array.AsReadOnly(new[]{new Entry("Weevil","Ordinary Weevil",false,r=>r.fightPitWeevil),new Entry("Blob","Blob Spitter",false,r=>r.fightPitBlob),new Entry("Buddy","Real Buddy",true,null)});
 public static Entry Get(string id){foreach(var entry in Entries)if(entry.Id==id)return entry;throw new ArgumentException("Opponent not implemented: "+id);}
}
