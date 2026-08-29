using System.Collections.Generic;
using SporeGobbo.CampLifecycle;
using UnityEditor;
using UnityEngine;

public static class CampSpatialContractGizmo
{
    const string ShowMasterPlanKey = "SporeGobbo.ShowCampResidentialMasterPlan";
    const string ShowSimpleCompositionKey = "SporeGobbo.ShowCampSimpleComposition";
    const string ShowDetailedContractKey = "SporeGobbo.ShowCampDetailedContract";

    [MenuItem("Tools/Spore Gobbo/Camp/Show Simple Camp Composition")]
    static void ToggleSimpleComposition()
    {
        TogglePreference(ShowSimpleCompositionKey, "Tools/Spore Gobbo/Camp/Show Simple Camp Composition", true);
    }

    [MenuItem("Tools/Spore Gobbo/Camp/Show Simple Camp Composition", true)]
    static bool ValidateSimpleCompositionToggle() => ValidatePreference(
        ShowSimpleCompositionKey, "Tools/Spore Gobbo/Camp/Show Simple Camp Composition", true);

    [MenuItem("Tools/Spore Gobbo/Camp/Show Detailed Spatial Contract")]
    static void ToggleDetailedContract()
    {
        TogglePreference(ShowDetailedContractKey, "Tools/Spore Gobbo/Camp/Show Detailed Spatial Contract", false);
    }

    [MenuItem("Tools/Spore Gobbo/Camp/Show Detailed Spatial Contract", true)]
    static bool ValidateDetailedContractToggle() => ValidatePreference(
        ShowDetailedContractKey, "Tools/Spore Gobbo/Camp/Show Detailed Spatial Contract", false);

    [MenuItem("Tools/Spore Gobbo/Camp/Show 50-Buddy Residential Plan")]
    static void ToggleMasterPlan()
    {
        EditorPrefs.SetBool(ShowMasterPlanKey, !EditorPrefs.GetBool(ShowMasterPlanKey, true));
        Menu.SetChecked("Tools/Spore Gobbo/Camp/Show 50-Buddy Residential Plan",
            EditorPrefs.GetBool(ShowMasterPlanKey, true));
        SceneView.RepaintAll();
    }

    [MenuItem("Tools/Spore Gobbo/Camp/Show 50-Buddy Residential Plan", true)]
    static bool ValidateMasterPlanToggle()
    {
        Menu.SetChecked("Tools/Spore Gobbo/Camp/Show 50-Buddy Residential Plan",
            EditorPrefs.GetBool(ShowMasterPlanKey, true));
        return true;
    }

    [MenuItem("Tools/Spore Gobbo/Camp/Validate 50-Buddy Residential Plan")]
    public static void LogMasterPlanReport()
    {
        CampResidentialMasterPlan plan = LoadMasterPlan();
        if (plan == null)
        {
            Debug.LogError("No CampResidentialMasterPlan asset found.");
            return;
        }
        List<string> issues = plan.ValidatePlan();
        HashSet<Vector2Int> allResidential = new HashSet<Vector2Int>();
        int connectorTotal = 0;
        System.Text.StringBuilder report = new System.Text.StringBuilder();
        Dictionary<string, int> neighborhoodCapacity = new Dictionary<string, int>();
        report.AppendLine("[CampResidential Master Plan] revision=" + plan.planRevision +
            " capacity=" + plan.TotalCapacity + " slots=" + plan.TotalAuthoredSlots);
        foreach (CampResidentialPlanRoom room in plan.rooms)
        {
            HashSet<Vector2Int> roomCells = RasterizeRoom(room);
            HashSet<Vector2Int> connectorCells = RasterizeConnector(room);
            connectorTotal += connectorCells.Count;
            if (!neighborhoodCapacity.ContainsKey(room.neighborhoodId)) neighborhoodCapacity[room.neighborhoodId] = 0;
            neighborhoodCapacity[room.neighborhoodId] += room.capacity;
            allResidential.UnionWith(roomCells);
            allResidential.UnionWith(connectorCells);
            report.AppendLine(room.roomId + " capacity=" + room.capacity +
                " chamberCells=" + roomCells.Count + " connectorCells=" + connectorCells.Count);
        }
        foreach (KeyValuePair<string, int> neighborhood in neighborhoodCapacity)
            report.AppendLine("neighborhood=" + neighborhood.Key + " capacity=" + neighborhood.Value);
        int campCells = plan.plannedCampBounds.size.x * plan.plannedCampBounds.size.y;
        float percent = campCells > 0 ? allResidential.Count * 100f / campCells : 0f;
        report.AppendLine("reservedUnion=" + allResidential.Count + " connectorSum=" + connectorTotal +
            " plannedCampCells=" + campCells + " residentialPercent=" + percent.ToString("0.00") +
            "% nonResidential=" + Mathf.Max(0, campCells - allResidential.Count));
        if (issues.Count == 0) report.AppendLine("validation=PASS");
        else foreach (string issue in issues) report.AppendLine("ISSUE: " + issue);
        Debug.Log(report.ToString());
    }

    [DrawGizmo(GizmoType.Active | GizmoType.Selected | GizmoType.NonSelected)]
    static void DrawSpatialContract(HandcraftedCampTerrain terrain, GizmoType gizmoType)
    {
        if (terrain == null || terrain.grid == null) return;
        if (EditorPrefs.GetBool(ShowSimpleCompositionKey, true)) DrawSimpleComposition(terrain);
        if (!EditorPrefs.GetBool(ShowDetailedContractKey, false) || terrain.SpatialContract == null) return;
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
        CampResidentialCatalog residentialCatalog = terrain.GetResidentialCatalog();
        foreach (ResidentialSlotRecord slot in terrain.GetResidentialSlots(1))
        {
            CampResidentialSlotDefinition definition = residentialCatalog?.GetSlot(slot.SlotIndex);
            Vector3 center = terrain.CellToWorld(new Vector2Int(slot.Center.x, slot.Center.y));
            Handles.color = Color.cyan;
            Handles.DrawWireDisc(center, Vector3.forward,
                terrain.CellSize * (float)CampSpatialPolicy.ResidentialClearanceRadiusInCells);
            Handles.Label(center, "Rest " + slot.SlotIndex +
                (definition != null ? "\n" + definition.SleepingClusterId : ""));
            if (definition != null)
            {
                foreach ((int x, int y) reservedCell in definition.ReservedExpansionEnvelope)
                    DrawResidentialCell(terrain, new Vector2Int(reservedCell.x, reservedCell.y),
                        new Color(0.8f, 0.25f, 1f, 0.025f), new Color(0.8f, 0.25f, 1f, 0.12f));
                Vector3[] spine = new Vector3[definition.AuthoredRouteSpine.Count];
                for (int i = 0; i < spine.Length; i++)
                    spine[i] = terrain.CellToWorld(new Vector2Int(
                        definition.AuthoredRouteSpine[i].x, definition.AuthoredRouteSpine[i].y));
                if (spine.Length > 1)
                {
                    Handles.color = new Color(1f, 0.55f, 0.1f, 0.9f);
                    Handles.DrawAAPolyLine(3f, spine);
                }
                foreach ((int x, int y) invalid in definition.GetClearanceDeficitCells(
                             new CampResidentialClearanceProfile(ResidentialClearanceTier.HypotheticalLarger, 2d)))
                    DrawResidentialCell(terrain, new Vector2Int(invalid.x, invalid.y),
                        new Color(1f, 0f, 0f, 0.25f), Color.red);
            }
            foreach (Vector2Int footprintCell in terrain.GetResidentialSlotFootprint(slot.SlotIndex))
            {
                bool stillRequiresDig = terrain.IsBlocked(footprintCell);
                DrawResidentialCell(terrain, footprintCell,
                    stillRequiresDig ? new Color(1f, 0.55f, 0.1f, 0.14f) : new Color(0.2f, 1f, 1f, 0.06f),
                    stillRequiresDig ? new Color(1f, 0.55f, 0.1f, 0.5f) : new Color(0.2f, 1f, 1f, 0.22f));
            }
            foreach ((int x, int y) target in slot.DigTargets)
            {
                Vector3 dig = terrain.CellToWorld(new Vector2Int(target.x, target.y));
                Handles.color = new Color(0.2f, 1f, 1f, 0.5f);
                Handles.DrawWireDisc(dig, Vector3.forward, terrain.CellSize * (float)CampSpatialPolicy.BuddyDigRadiusInCells);
            }
        }
        if (residentialCatalog != null)
            foreach ((int x, int y) shared in residentialCatalog.GetSharedRouteCells())
            {
                Handles.color = Color.yellow;
                Handles.DrawWireDisc(terrain.CellToWorld(new Vector2Int(shared.x, shared.y)),
                    Vector3.forward, terrain.CellSize * 0.18f);
            }
        if (EditorPrefs.GetBool(ShowMasterPlanKey, true)) DrawResidentialMasterPlan(terrain);
    }

    static void DrawResidentialCell(HandcraftedCampTerrain terrain, Vector2Int cell, Color fill, Color outline)
    {
        Vector3 center = terrain.CellToWorld(cell);
        float half = terrain.CellSize * 0.45f;
        Handles.DrawSolidRectangleWithOutline(new[]
        {
            center + new Vector3(-half, -half), center + new Vector3(half, -half),
            center + new Vector3(half, half), center + new Vector3(-half, half)
        }, fill, outline);
    }

    static void DrawSimpleComposition(HandcraftedCampTerrain terrain)
    {
        HashSet<Vector2Int> openCells = new HashSet<Vector2Int>();
        AddRegions(terrain.authoredOpenRegions, openCells);
        AddRegions(terrain.mainChamberRevealRegions, openCells);
        RemoveRegions(terrain.mainChamberRevealExclusionRegions, openCells);
        if (terrain.reservedStationFootprints != null)
            foreach (CampReservedFootprint footprint in terrain.reservedStationFootprints)
                if (footprint != null) AddBounds(footprint.bounds, openCells);
        if (terrain.SpatialContract != null)
            foreach (CampSpatialZone zone in terrain.SpatialContract.zones)
                if (zone != null && CampSpatialPolicy.IsPermanent(zone.kind)) AddBounds(zone.bounds, openCells);

        Color floor = new Color(0.2f, 0.72f, 0.48f, 0.17f);
        float half = terrain.CellSize * 0.5f;
        foreach (Vector2Int cell in openCells)
        {
            Vector3 center = terrain.CellToWorld(cell);
            Handles.DrawSolidRectangleWithOutline(new[]
            {
                center + new Vector3(-half, -half), center + new Vector3(half, -half),
                center + new Vector3(half, half), center + new Vector3(-half, half)
            }, floor, Color.clear);
        }

        GUIStyle featureStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
        featureStyle.normal.textColor = Color.white;
        if (terrain.reservedStationFootprints != null)
            foreach (CampReservedFootprint footprint in terrain.reservedStationFootprints)
            {
                if (footprint == null) continue;
                Vector2Int cell = new Vector2Int(footprint.bounds.xMin + footprint.bounds.size.x / 2,
                    footprint.bounds.yMin + footprint.bounds.size.y / 2);
                Handles.Label(terrain.CellToWorld(cell), footprint.footprintId.ToUpperInvariant(), featureStyle);
            }
        if (terrain.SpatialContract != null)
            foreach (CampSpatialZone zone in terrain.SpatialContract.zones)
            {
                if (zone == null || zone.kind != CampZoneKind.UnstableCollapse) continue;
                Vector2Int cell = new Vector2Int(zone.bounds.xMin + zone.bounds.size.x / 2,
                    zone.bounds.yMin + zone.bounds.size.y / 2);
                featureStyle.normal.textColor = Color.yellow;
                Handles.Label(terrain.CellToWorld(cell), "COLLAPSE / INTRO", featureStyle);
            }

        CampResidentialMasterPlan plan = LoadMasterPlan();
        if (plan != null)
        {
            for (int i = 0; i < plan.rooms.Count; i++)
            {
                CampResidentialPlanRoom room = plan.rooms[i];
                if (room == null) continue;
                Color color = RoomColor(room);
                DrawRoomLobes(terrain, room, new Color(color.r, color.g, color.b, 0.65f));
                Vector2Int labelCell = new Vector2Int(room.protectedBounds.xMin + room.protectedBounds.size.x / 2,
                    room.protectedBounds.yMin + room.protectedBounds.size.y / 2);
                featureStyle.normal.textColor = color;
                Handles.Label(terrain.CellToWorld(labelCell), room.displayName + "\n" + room.capacity, featureStyle);
            }
            DrawPlannedBounds(terrain, plan);
        }
    }

    static void AddRegions(List<CampTerrainRegion> regions, HashSet<Vector2Int> cells)
    {
        if (regions == null) return;
        foreach (CampTerrainRegion region in regions) if (region != null) AddBounds(region.bounds, cells);
    }

    static void RemoveRegions(List<CampTerrainRegion> regions, HashSet<Vector2Int> cells)
    {
        if (regions == null) return;
        foreach (CampTerrainRegion region in regions)
            if (region != null)
                foreach (Vector3Int position in region.bounds.allPositionsWithin)
                    cells.Remove(new Vector2Int(position.x, position.y));
    }

    static void AddBounds(BoundsInt bounds, HashSet<Vector2Int> cells)
    {
        foreach (Vector3Int position in bounds.allPositionsWithin)
            cells.Add(new Vector2Int(position.x, position.y));
    }

    static void TogglePreference(string key, string menuPath, bool defaultValue)
    {
        EditorPrefs.SetBool(key, !EditorPrefs.GetBool(key, defaultValue));
        Menu.SetChecked(menuPath, EditorPrefs.GetBool(key, defaultValue));
        SceneView.RepaintAll();
    }

    static bool ValidatePreference(string key, string menuPath, bool defaultValue)
    {
        Menu.SetChecked(menuPath, EditorPrefs.GetBool(key, defaultValue));
        return true;
    }

    static void DrawResidentialMasterPlan(HandcraftedCampTerrain terrain)
    {
        CampResidentialMasterPlan plan = LoadMasterPlan();
        if (plan == null) return;
        GUIStyle roomStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
        GUIStyle slotStyle = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
        int globalSlotNumber = 1;
        for (int roomIndex = 0; roomIndex < plan.rooms.Count; roomIndex++)
        {
            CampResidentialPlanRoom room = plan.rooms[roomIndex];
            if (room == null) continue;
            Color color = RoomColor(room);
            DrawRoomBounds(terrain, room, color);
            DrawConnector(terrain, room, color);
            DrawRoomLobes(terrain, room, color);
            roomStyle.normal.textColor = color;
            Vector3 label = terrain.CellToWorld(new Vector2Int(
                room.protectedBounds.xMin + (room.protectedBounds.size.x / 2),
                room.protectedBounds.yMax - 1));
            Handles.Label(label, room.neighborhoodDisplayName + "\n" + room.displayName + " — " + room.capacity +
                (room.currentlyImplemented ? " (CURRENT)" : " (PLANNED)"), roomStyle);

            for (int slotIndex = 0; slotIndex < room.slotCenters.Count; slotIndex++)
            {
                CampCellCoordinate slotCell = room.slotCenters[slotIndex];
                Vector3 center = terrain.CellToWorld(new Vector2Int(slotCell.x, slotCell.y));
                Handles.color = color;
                Handles.DrawWireDisc(center, Vector3.forward,
                    terrain.CellSize * room.approximateSlotFootprintRadiusCells);
                slotStyle.normal.textColor = color;
                Handles.Label(center, globalSlotNumber.ToString(), slotStyle);
                globalSlotNumber++;
            }
            foreach (CampCellCoordinate activity in room.futureActivityPoints)
            {
                Vector3 center = terrain.CellToWorld(new Vector2Int(activity.x, activity.y));
                Handles.color = new Color(color.r, color.g, color.b, 0.8f);
                Handles.DrawWireDisc(center, Vector3.forward, terrain.CellSize * 0.45f);
                Handles.Label(center + Vector3.up * terrain.CellSize * 0.5f, "activity", slotStyle);
            }
            Vector3 breakthrough = terrain.CellToWorld(new Vector2Int(
                room.breakthroughEntrance.x, room.breakthroughEntrance.y));
            Handles.color = Color.magenta;
            Handles.DrawWireDisc(breakthrough, Vector3.forward, terrain.CellSize * 0.75f);
            Handles.Label(breakthrough + Vector3.up * terrain.CellSize * 0.65f,
                room.currentlyImplemented ? "CURRENT ENTRANCE" : "FUTURE BREAKTHROUGH", slotStyle);
        }
        DrawPlannedBounds(terrain, plan);
    }

    static void DrawRoomBounds(HandcraftedCampTerrain terrain, CampResidentialPlanRoom room, Color color)
    {
        Vector3 min = terrain.grid.CellToWorld(room.protectedBounds.min);
        Vector3 max = terrain.grid.CellToWorld(room.protectedBounds.max);
        Handles.DrawSolidRectangleWithOutline(new[]
        {
            new Vector3(min.x, min.y), new Vector3(max.x, min.y),
            new Vector3(max.x, max.y), new Vector3(min.x, max.y)
        }, new Color(color.r, color.g, color.b, 0.025f), new Color(color.r, color.g, color.b, 0.32f));
    }

    static void DrawRoomLobes(HandcraftedCampTerrain terrain, CampResidentialPlanRoom room, Color color)
    {
        foreach (CampResidentialPlanLobe lobe in room.chamberLobes)
        {
            if (lobe == null) continue;
            Vector3 center = terrain.CellToWorld(new Vector2Int(lobe.center.x, lobe.center.y));
            Handles.color = new Color(color.r, color.g, color.b, 0.75f);
            Matrix4x4 old = Handles.matrix;
            Handles.matrix = Matrix4x4.TRS(center, Quaternion.identity,
                new Vector3(terrain.CellSize * lobe.radiusX, terrain.CellSize * lobe.radiusY, 1f));
            Handles.DrawWireDisc(Vector3.zero, Vector3.forward, 1f);
            Handles.matrix = old;
        }
    }

    static void DrawConnector(HandcraftedCampTerrain terrain, CampResidentialPlanRoom room, Color color)
    {
        if (room.connectorWaypoints == null || room.connectorWaypoints.Count < 2) return;
        Vector3[] points = new Vector3[room.connectorWaypoints.Count];
        for (int i = 0; i < points.Length; i++)
        {
            CampCellCoordinate cell = room.connectorWaypoints[i];
            points[i] = terrain.CellToWorld(new Vector2Int(cell.x, cell.y));
        }
        Handles.color = new Color(color.r, color.g, color.b, 0.9f);
        Handles.DrawAAPolyLine(terrain.CellSize * (room.connectorHalfWidth * 2f + 0.6f), points);
        Handles.DrawAAPolyLine(2f, points);
    }

    static void DrawPlannedBounds(HandcraftedCampTerrain terrain, CampResidentialMasterPlan plan)
    {
        Vector3 min = terrain.grid.CellToWorld(plan.plannedCampBounds.min);
        Vector3 max = terrain.grid.CellToWorld(plan.plannedCampBounds.max);
        Handles.DrawSolidRectangleWithOutline(new[]
        {
            new Vector3(min.x, min.y), new Vector3(max.x, min.y),
            new Vector3(max.x, max.y), new Vector3(min.x, max.y)
        }, Color.clear, new Color(0.85f, 0.85f, 0.85f, 0.7f));
        Handles.Label(new Vector3(min.x, max.y), "PLANNED CAMP " +
            plan.plannedCampBounds.size.x + " x " + plan.plannedCampBounds.size.y, EditorStyles.boldLabel);
    }

    static CampResidentialMasterPlan LoadMasterPlan()
    {
        string[] guids = AssetDatabase.FindAssets("t:CampResidentialMasterPlan");
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<CampResidentialMasterPlan>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    static HashSet<Vector2Int> RasterizeRoom(CampResidentialPlanRoom room)
    {
        HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
        if (room?.chamberLobes == null) return cells;
        foreach (CampResidentialPlanLobe lobe in room.chamberLobes)
        {
            if (lobe == null) continue;
            for (int x = lobe.center.x - lobe.radiusX; x <= lobe.center.x + lobe.radiusX; x++)
            for (int y = lobe.center.y - lobe.radiusY; y <= lobe.center.y + lobe.radiusY; y++)
            {
                float dx = (x - lobe.center.x) / (float)Mathf.Max(1, lobe.radiusX);
                float dy = (y - lobe.center.y) / (float)Mathf.Max(1, lobe.radiusY);
                if (dx * dx + dy * dy <= 1f && room.protectedBounds.Contains(new Vector3Int(x, y, 0)))
                    cells.Add(new Vector2Int(x, y));
            }
        }
        return cells;
    }

    static HashSet<Vector2Int> RasterizeConnector(CampResidentialPlanRoom room)
    {
        HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
        if (room?.connectorWaypoints == null) return cells;
        for (int i = 1; i < room.connectorWaypoints.Count; i++)
        {
            Vector2Int start = new Vector2Int(room.connectorWaypoints[i - 1].x, room.connectorWaypoints[i - 1].y);
            Vector2Int end = new Vector2Int(room.connectorWaypoints[i].x, room.connectorWaypoints[i].y);
            int steps = Mathf.Max(Mathf.Abs(end.x - start.x), Mathf.Abs(end.y - start.y));
            for (int step = 0; step <= steps; step++)
            {
                float t = steps > 0 ? step / (float)steps : 0f;
                Vector2Int center = Vector2Int.RoundToInt(Vector2.Lerp(start, end, t));
                for (int x = -room.connectorHalfWidth; x <= room.connectorHalfWidth; x++)
                for (int y = -room.connectorHalfWidth; y <= room.connectorHalfWidth; y++)
                    if (x * x + y * y <= room.connectorHalfWidth * room.connectorHalfWidth + 1)
                        cells.Add(center + new Vector2Int(x, y));
            }
        }
        return cells;
    }

    static Color RoomColor(CampResidentialPlanRoom room)
    {
        if (room.currentlyImplemented) return new Color(0.15f, 1f, 1f);
        bool primary = room.neighborhoodId == "primary";
        Color baseColor = primary ? new Color(0.35f, 0.85f, 1f) : new Color(1f, 0.45f, 0.72f);
        float variation = ((room.progressionOrder % 3) - 1) * 0.12f;
        return new Color(
            Mathf.Clamp01(baseColor.r + variation),
            Mathf.Clamp01(baseColor.g + variation),
            Mathf.Clamp01(baseColor.b + variation));
    }

    static Color ColorFor(CampZoneKind kind)
    {
        switch (kind)
        {
            case CampZoneKind.HomeCore: return new Color(1f, 0.5f, 0.1f);
            case CampZoneKind.PermanentExit: return new Color(1f, 0.15f, 0.15f);
            case CampZoneKind.PermanentMemorial: return new Color(0.95f, 0.9f, 0.72f);
            case CampZoneKind.UnstableCollapse: return Color.yellow;
            case CampZoneKind.IntroArrivalClearance: return new Color(0.2f, 1f, 0.3f);
            case CampZoneKind.NormalArrivalClearance: return new Color(0.55f, 1f, 0.2f);
            case CampZoneKind.CirculationClearance: return Color.cyan;
            case CampZoneKind.GeneralUnreserved: return Color.gray;
            default: return Color.white;
        }
    }
}
