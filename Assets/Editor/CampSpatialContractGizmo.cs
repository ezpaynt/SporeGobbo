using SporeGobbo.CampLifecycle;
using UnityEditor;
using UnityEngine;

public static class CampSpatialContractGizmo
{
    [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
    static void DrawSpatialContract(HandcraftedCampTerrain terrain, GizmoType gizmoType)
    {
        if (terrain == null || terrain.grid == null || terrain.SpatialContract == null) return;
        foreach (CampSpatialZone zone in terrain.SpatialContract.zones)
        {
            if (zone == null) continue;
            Vector3 min = terrain.grid.CellToWorld(zone.bounds.min);
            Vector3 max = terrain.grid.CellToWorld(zone.bounds.max);
            Vector3 center = (min + max) * 0.5f;
            Vector3 size = max - min;
            Color color = ColorFor(zone.kind);
            Color fill = new Color(color.r, color.g, color.b, 0.08f);
            Handles.DrawSolidRectangleWithOutline(new[]
            {
                new Vector3(min.x, min.y, 0f), new Vector3(max.x, min.y, 0f),
                new Vector3(max.x, max.y, 0f), new Vector3(min.x, max.y, 0f)
            }, fill, color);
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = color;
            Handles.Label(center + Vector3.up * size.y * 0.05f, zone.zoneId + "\n" + zone.kind, style);
        }
        foreach (ResidentialSlotRecord slot in terrain.GetResidentialSlots(1))
        {
            Vector3 center = terrain.CellToWorld(new Vector2Int(slot.Center.x, slot.Center.y));
            Handles.color = Color.cyan;
            Handles.DrawWireDisc(center, Vector3.forward,
                terrain.CellSize * (float)CampSpatialPolicy.ResidentialClearanceRadiusInCells);
            Handles.Label(center, "Rest " + slot.SlotIndex);
            foreach (Vector2Int footprintCell in terrain.GetResidentialSlotFootprint(slot.SlotIndex))
            {
                Vector3 footprintCenter = terrain.CellToWorld(footprintCell);
                float half = terrain.CellSize * 0.45f;
                Handles.DrawSolidRectangleWithOutline(new[]
                {
                    footprintCenter + new Vector3(-half, -half), footprintCenter + new Vector3(half, -half),
                    footprintCenter + new Vector3(half, half), footprintCenter + new Vector3(-half, half)
                }, new Color(0.2f, 1f, 1f, 0.06f), new Color(0.2f, 1f, 1f, 0.22f));
            }
            foreach ((int x, int y) target in slot.DigTargets)
            {
                Vector3 dig = terrain.CellToWorld(new Vector2Int(target.x, target.y));
                Handles.color = new Color(0.2f, 1f, 1f, 0.5f);
                Handles.DrawWireDisc(dig, Vector3.forward, terrain.CellSize * (float)CampSpatialPolicy.BuddyDigRadiusInCells);
            }
        }
    }

    static Color ColorFor(CampZoneKind kind)
    {
        switch (kind)
        {
            case CampZoneKind.HomeCore: return new Color(1f, 0.5f, 0.1f);
            case CampZoneKind.PermanentExit: return new Color(1f, 0.15f, 0.15f);
            case CampZoneKind.PermanentMemorial: return new Color(0.95f, 0.9f, 0.72f);
            case CampZoneKind.ResidentialEntrance: return Color.magenta;
            case CampZoneKind.UnstableCollapse: return Color.yellow;
            case CampZoneKind.IntroArrivalClearance: return new Color(0.2f, 1f, 0.3f);
            case CampZoneKind.NormalArrivalClearance: return new Color(0.55f, 1f, 0.2f);
            case CampZoneKind.CirculationClearance: return Color.cyan;
            case CampZoneKind.GeneralUnreserved: return Color.gray;
            default:
                if (CampSpatialPolicy.IsResidential(kind))
                {
                    float stage = (int)kind - (int)CampZoneKind.ResidentialStage1;
                    return Color.Lerp(new Color(0.2f, 0.9f, 1f), new Color(0.1f, 0.3f, 1f), stage / 4f);
                }
                return Color.white;
        }
    }
}
