#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Debug = UnityEngine.Debug;

public static class TerrainPresentationValidationRunner
{
    const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    const string CampScenePath = "Assets/Scenes/CampScene.unity";
    const string ReportName = "terrain-presentation-validation.txt";

    [MenuItem("Terrain Tools/Run Presentation Validation")]
    public static void Run()
    {
        var lines = new List<string>();
        int errors = 0;
        ValidateSampleMode(SampleSceneMode.Intro, lines, ref errors);
        ValidateSampleMode(SampleSceneMode.NormalRun, lines, ref errors);
        ValidateCamp(lines, ref errors);
        lines.Add($"RESULT errors={errors}");
        string reportPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", ReportName);
        File.WriteAllLines(reportPath, lines);
        foreach (string line in lines) Debug.Log("TERRAIN VALIDATION | " + line);
        if (errors == 0) Debug.Log("Terrain presentation validation passed. " + reportPath);
        else Debug.LogError($"Terrain presentation validation failed with {errors} errors. {reportPath}");
    }

    static void ValidateSampleMode(SampleSceneMode mode, List<string> lines, ref int errors)
    {
        EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
        SampleSceneModeController controller = UnityEngine.Object.FindAnyObjectByType<SampleSceneModeController>(FindObjectsInactive.Include);
        MapGenerator map = UnityEngine.Object.FindAnyObjectByType<MapGenerator>(FindObjectsInactive.Include);
        if (controller == null || map == null) { Fail(lines, ref errors, mode + ": missing controller or generator"); return; }
        controller.editorPreviewMode = mode;
        controller.ConfigureForEditorPreview();
        Stopwatch timer = Stopwatch.StartNew();
        map.Generate();
        timer.Stop();
        TerrainPresentationRenderer renderer = map.terrainPresentationRenderer;
        if (renderer == null || !renderer.ValidateRuntimeReferences()) { Fail(lines, ref errors, mode + ": renderer invalid"); return; }
        ValidateMapCells(map, renderer, lines, ref errors, mode.ToString());
        ValidateStableVariants(renderer, lines, ref errors, mode.ToString());
        MeasurePresentation(renderer, lines, mode.ToString());
        lines.Add($"{mode}: seed={map.RuntimeMapSeed} generationMs={timer.Elapsed.TotalMilliseconds:F2} blocked={renderer.LastBuiltBlockedCells} floor={renderer.LastBuiltFloorCells} tilemaps={UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length}");
    }

    static void ValidateMapCells(MapGenerator map, TerrainPresentationRenderer renderer, List<string> lines, ref int errors, string context)
    {
        int hiddenLeaks = 0, specialMismatches = 0, dirtChecked = 0;
        BoundsInt bounds = new BoundsInt(0, 0, 0, map.Data.width, map.Data.height, 1);
        foreach (Vector3Int position in bounds.allPositionsWithin)
        {
            var cell = new Vector2Int(position.x, position.y);
            TerrainVisualCellKind kind = map.GetTerrainVisualKind(cell);
            bool hasFloor = renderer.floorPresentation.GetTile(position) != null;
            if (hasFloor != (kind == TerrainVisualCellKind.Open)) hiddenLeaks++;
            if (kind == TerrainVisualCellKind.Dirt && renderer.blockedTerrainPresentation.GetTile(position) != null) dirtChecked++;
            if (kind == TerrainVisualCellKind.Root || kind == TerrainVisualCellKind.Stone || kind == TerrainVisualCellKind.RevealDirt)
            {
                if (renderer.blockedTerrainPresentation.GetTile(position) != map.GetSpecialTerrainPresentationTile(cell)) specialMismatches++;
            }
        }
        if (hiddenLeaks != 0) Fail(lines, ref errors, $"{context}: hidden/open floor mismatches={hiddenLeaks}");
        if (specialMismatches != 0) Fail(lines, ref errors, $"{context}: special tile mismatches={specialMismatches}");
        if (dirtChecked == 0) Fail(lines, ref errors, $"{context}: no normal dirt presentation cells");
    }

    static void ValidateStableVariants(TerrainPresentationRenderer renderer, List<string> lines, ref int errors, string context)
    {
        Vector2Int[] samples = { new Vector2Int(0, 0), new Vector2Int(17, 29), new Vector2Int(-4, 9), new Vector2Int(103, 51) };
        foreach (Vector2Int cell in samples)
        {
            int first = renderer.GetStableVariantOrdinal(cell, 5, false);
            int second = renderer.GetStableVariantOrdinal(cell, 5, false);
            if (first != second) Fail(lines, ref errors, $"{context}: unstable dirt ordinal at {cell}");
        }
    }

    static void MeasurePresentation(TerrainPresentationRenderer renderer, List<string> lines, string context)
    {
        Stopwatch full = Stopwatch.StartNew();
        renderer.RebuildAndEnable();
        full.Stop();
        long before = GC.GetAllocatedBytesForCurrentThread();
        Stopwatch dirty = Stopwatch.StartNew();
        renderer.MarkDirty(new Vector2Int(1, 1));
        renderer.FlushImmediate();
        dirty.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        lines.Add($"{context}: presentationBuildMs={full.Elapsed.TotalMilliseconds:F2} oneCellDirtyMs={dirty.Elapsed.TotalMilliseconds:F3} dirtyCells={renderer.LastDirtyCellCount} managedBytes={allocated}");
    }

    static void ValidateCamp(List<string> lines, ref int errors)
    {
        EditorSceneManager.OpenScene(CampScenePath, OpenSceneMode.Single);
        HandcraftedCampTerrain camp = UnityEngine.Object.FindAnyObjectByType<HandcraftedCampTerrain>(FindObjectsInactive.Include);
        if (camp == null) { Fail(lines, ref errors, "Camp: terrain missing"); return; }
        Stopwatch timer = Stopwatch.StartNew();
        camp.RebuildFromBaseline();
        timer.Stop();
        TerrainPresentationRenderer renderer = camp.terrainPresentationRenderer;
        if (renderer == null || !renderer.ValidateRuntimeReferences()) { Fail(lines, ref errors, "Camp: renderer invalid"); return; }
        int mismatches = 0;
        foreach (Vector3Int position in camp.AuthoredBounds.allPositionsWithin)
        {
            var cell = new Vector2Int(position.x, position.y);
            bool shouldHaveFloor = camp.GetTerrainVisualKind(cell) == TerrainVisualCellKind.Open;
            if ((renderer.floorPresentation.GetTile(position) != null) != shouldHaveFloor) mismatches++;
        }
        if (mismatches != 0) Fail(lines, ref errors, $"Camp: floor mismatches={mismatches}");
        MeasurePresentation(renderer, lines, "Camp");
        lines.Add($"Camp: reconstructionMs={timer.Elapsed.TotalMilliseconds:F2} blocked={renderer.LastBuiltBlockedCells} floor={renderer.LastBuiltFloorCells} tilemaps={UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length}");
    }

    static void Fail(List<string> lines, ref int errors, string message) { errors++; lines.Add("ERROR " + message); }
}
#endif
