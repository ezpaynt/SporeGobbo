using System.Collections.Generic;
using UnityEngine;
using SporeGobbo.CampLifecycle;
/// <summary>Read-only terrain/catalog contract for the existing deterministic residential planner.</summary>
public interface IResidentialPlanTerrain
{
    float CellSize {get;}
    CampResidentialCatalog GetResidentialCatalog();
    List<Vector2Int> GetResidentialSlotFootprint(int slot);
    List<Vector2Int> GetResidentialConstructionRoute(int slot);
    CampDigCategory GetSpatialDigCategory(Vector2Int cell);
    bool IsBlocked(Vector2Int cell);
    Vector2 CellToWorld(Vector2Int cell);
    Vector2Int WorldToCell(Vector2 point);
    bool IsDerivedTerrainCollider(Collider2D collider);
}
public sealed class ProductionResidentialPlanTerrain : IResidentialPlanTerrain
{
    readonly HandcraftedCampTerrain terrain;
    public ProductionResidentialPlanTerrain(HandcraftedCampTerrain terrain){this.terrain=terrain;}
    public float CellSize=>terrain.CellSize;
    public CampResidentialCatalog GetResidentialCatalog()=>terrain.GetResidentialCatalog();
    public List<Vector2Int> GetResidentialSlotFootprint(int slot)=>terrain.GetResidentialSlotFootprint(slot);
    public List<Vector2Int> GetResidentialConstructionRoute(int slot)=>terrain.GetResidentialConstructionRoute(slot);
    public CampDigCategory GetSpatialDigCategory(Vector2Int cell)=>terrain.GetSpatialDigCategory(cell);
    public bool IsBlocked(Vector2Int cell)=>terrain.IsBlocked(cell);
    public Vector2 CellToWorld(Vector2Int cell)=>terrain.CellToWorld(cell);
    public Vector2Int WorldToCell(Vector2 point)=>terrain.WorldToCell(point);
    public bool IsDerivedTerrainCollider(Collider2D collider)=>collider is UnityEngine.Tilemaps.TilemapCollider2D;
}
