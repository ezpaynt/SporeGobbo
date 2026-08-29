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
        foreach (CampSpatialZone zone in zones)
        {
            if (zone == null) continue;
            CampCellRect rect = new CampCellRect(zone.bounds.x, zone.bounds.y, zone.bounds.size.x, zone.bounds.size.y);
            records.Add(new CampZoneRecord(zone.kind, rect));
        }
        return CampSpatialPolicy.Validate(records);
    }
}
