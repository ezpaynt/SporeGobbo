using System;
using UnityEngine;
using SporeGobbo.CampLifecycle;
/// <summary>Only the spatial/catalog surface used by existing Camp gameplay. No roster or economy.</summary>
public interface ICampSpatialTerrain:IDiggableTerrain,IResidentialPlanTerrain
{
 new float CellSize{get;}
 new bool IsBlocked(Vector2Int cell);
 new Vector2 CellToWorld(Vector2Int cell);
 new Vector2Int WorldToCell(Vector2 position);
 int TotalResidentialCapacity{get;}
 bool Ready{get;}
 bool ResidentialEntranceOpen{get;}
 Vector2 ResidentialEntranceWorld{get;}
 bool TryOpenResidentialEntrance(GobboController player);
 ResidentialSlotRecord GetResidentialSlot(int slot);
 int GetResidentialProgressionIndexForSlot(int slot);
 bool CompleteResidentialSlotForProgression(int progression,int slot);
 bool HasOpenAccessToFootprint(string markerId);
}
public static class CampSpatialTerrain
{
 public static ICampSpatialTerrain Active=>DiggableTerrainService.Active as ICampSpatialTerrain;
 // Temporary serialized legacy-reference compatibility, removable after legacy scene retirement.
 public static ICampSpatialTerrain Resolve(HandcraftedCampTerrain serializedReference)
 {
  var active=Active;
  if(serializedReference!=null&&active!=null&&!ReferenceEquals(serializedReference,active))throw new InvalidOperationException("Conflicting Camp terrain reference");
  return active;
 }
}
