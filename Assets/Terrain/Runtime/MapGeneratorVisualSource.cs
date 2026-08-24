using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class MapGeneratorVisualSource : MonoBehaviour, ITerrainVisualSource
{
    public MapGenerator mapGenerator;
    public BoundsInt VisualBounds => mapGenerator != null && mapGenerator.Data != null ? new BoundsInt(0, 0, 0, mapGenerator.Data.width, mapGenerator.Data.height, 1) : new BoundsInt();
    public int VisualSeed => mapGenerator != null ? mapGenerator.RuntimeMapSeed : 0;
    void Awake() { if (mapGenerator == null) mapGenerator = GetComponent<MapGenerator>(); }
    public TerrainVisualCellKind GetVisualKind(Vector2Int cell) { return mapGenerator != null ? mapGenerator.GetTerrainVisualKind(cell) : TerrainVisualCellKind.OutOfBounds; }
    public bool ShouldRenderFloor(Vector2Int cell) { return GetVisualKind(cell) == TerrainVisualCellKind.Open; }
    public Color GetPresentationTint(Vector2Int cell) { return mapGenerator != null ? mapGenerator.GetTerrainPresentationTint(cell) : Color.white; }
    public TileBase GetSpecialBlockedTile(Vector2Int cell) { return mapGenerator != null ? mapGenerator.GetSpecialTerrainPresentationTile(cell) : null; }
}
