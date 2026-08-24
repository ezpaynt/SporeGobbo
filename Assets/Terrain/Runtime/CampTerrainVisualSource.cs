using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class CampTerrainVisualSource : MonoBehaviour, ITerrainVisualSource
{
    const int CampVisualSalt = 0x43A91;
    public HandcraftedCampTerrain campTerrain;
    public BoundsInt VisualBounds => campTerrain != null ? campTerrain.AuthoredBounds : new BoundsInt();
    public int VisualSeed => campTerrain != null ? StableHash(CampVisualSalt, campTerrain.LayoutRevision) : CampVisualSalt;
    void Awake() { if (campTerrain == null) campTerrain = GetComponent<HandcraftedCampTerrain>(); }
    public TerrainVisualCellKind GetVisualKind(Vector2Int cell) { return campTerrain != null ? campTerrain.GetTerrainVisualKind(cell) : TerrainVisualCellKind.OutOfBounds; }
    public bool ShouldRenderFloor(Vector2Int cell) { return GetVisualKind(cell) == TerrainVisualCellKind.Open; }
    public Color GetPresentationTint(Vector2Int cell) { return Color.white; }
    public TileBase GetSpecialBlockedTile(Vector2Int cell) { return null; }
    static int StableHash(int a, int b) { unchecked { uint hash = 2166136261u; hash = (hash ^ (uint)a) * 16777619u; hash = (hash ^ (uint)b) * 16777619u; return (int)hash; } }
}
