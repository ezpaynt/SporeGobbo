using System;
using System.Collections.Generic;
using SporeGobbo.CampLifecycle;
using UnityEngine;

[Serializable]
public sealed class CampSpatialZone
{
    public string zoneId = "";
    public CampZoneKind kind;
    public BoundsInt bounds;
    [TextArea] public string purpose = "";
}

[CreateAssetMenu(menuName = "Spore Gobbo/Camp Spatial Contract", fileName = "CampSpatialContract")]
public sealed class CampSpatialContract : ScriptableObject
{
    public int contractRevision = 1;
    public List<CampSpatialZone> zones = new List<CampSpatialZone>();

    public IEnumerable<CampSpatialZone> ZonesAt(Vector2Int cell)
    {
        foreach (CampSpatialZone zone in zones)
            if (zone != null && zone.bounds.Contains(new Vector3Int(cell.x, cell.y, zone.bounds.z)))
                yield return zone;
    }

    public CampDigCategory Classify(Vector2Int cell)
    {
        List<CampZoneKind> kinds = new List<CampZoneKind>();
        foreach (CampSpatialZone zone in ZonesAt(cell)) kinds.Add(zone.kind);
        return CampSpatialPolicy.Classify(kinds);
    }

    public CampSpatialZone Find(string zoneId)
    {
        return zones.Find(zone => zone != null && zone.zoneId == zoneId);
    }

    public List<string> ValidateContract()
    {
        List<CampZoneRecord> records = new List<CampZoneRecord>();
        List<CampCellRect> stages = new List<CampCellRect>();
        foreach (CampSpatialZone zone in zones)
        {
            if (zone == null) continue;
            CampCellRect rect = new CampCellRect(zone.bounds.x, zone.bounds.y, zone.bounds.size.x, zone.bounds.size.y);
            records.Add(new CampZoneRecord(zone.kind, rect));
            if (CampSpatialPolicy.IsResidential(zone.kind)) stages.Add(rect);
        }
        List<string> issues = CampSpatialPolicy.Validate(records);
        if (!CampSpatialPolicy.IsOrderedAndConnected(stages))
            issues.Add("Residential stages must contain exactly five entries in connected Stage 1–5 order.");
        CampSpatialZone entrance = zones.Find(zone => zone != null && zone.kind == CampZoneKind.ResidentialEntrance);
        CampSpatialZone stageOne = zones.Find(zone => zone != null && zone.kind == CampZoneKind.ResidentialStage1);
        if (entrance == null || stageOne == null || !ToRect(entrance.bounds).Touches(ToRect(stageOne.bounds)))
            issues.Add("Residential entrance must touch Residential Stage 1.");
        return issues;
    }

    static CampCellRect ToRect(BoundsInt bounds) => new CampCellRect(bounds.x, bounds.y, bounds.size.x, bounds.size.y);
}
