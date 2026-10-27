using System.Collections.Generic;
using SporeGobbo.Geometry;

namespace SporeGobbo.CampCandidate
{
    /// <summary>Small source contract. Runtime edits, visibility and rendering are separate consumers.</summary>
    public interface IAuthoredRegionDefinition : ICompleteTerrainDefinition
    {
        string Id { get; }
        int Width { get; }
        int Height { get; }
        double WorldUnitsPerCell { get; }
        Point2 WorldOrigin { get; }
        RegionCellInfo CellAt(int column, int row);
    }

    public readonly struct RegionCellInfo
    {
        public readonly bool InitiallySolid, Permanent;
        public readonly string MaterialId, AccessPolicyId;
        public RegionCellInfo(bool solid, bool permanent, string material, string access)
        {
            InitiallySolid = solid;
            Permanent = permanent;
            MaterialId = material;
            AccessPolicyId = access;
        }
    }

    /// <summary>Bounded structural placeholder: protected footprint and silhouette are explicitly distinct.</summary>
    public sealed class RegionLandmarkStamp
    {
        public string Id { get; }
        public string MaterialId { get; }
        public IReadOnlyList<DualTile> ProtectedCells { get; }
        public ProjectionBounds SilhouetteBounds { get; }
        public RegionLandmarkStamp(string id, string material, IEnumerable<DualTile> cells, ProjectionBounds silhouette)
        {
            Id = id;
            MaterialId = material;
            ProtectedCells = new List<DualTile>(cells).AsReadOnly();
            SilhouetteBounds = silhouette;
        }
    }
}
