using System.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class MapDebugOverlay : MonoBehaviour
{
    [Header("Debug Overlay")]
    public bool showDebugOverlay = true;
    public bool drawOnlyWhenSelected = false;

    [Header("Layers")]
    public bool showMapBounds = true;
    public bool showOpenWalkableCells = true;
    public bool showHiddenRoomCells = true;
    public bool showHiddenTunnelCells = true;
    public bool showRevealTriggerCells = true;
    public bool showSpawnCells = true;
    public bool showBossExitCells = true;
    public bool showLabels = true;

    [Header("Drawing")]
    [Range(0f, 1f)] public float cellAlpha = 0.32f;
    [Range(0f, 0.7f)] public float cellInset = 0.08f;
    public float zOffset = -0.15f;

    [Header("Colors")]
    public Color openWalkableColor = new Color(0.35f, 1f, 0.45f, 1f);
    public Color hiddenRoomColor = new Color(1f, 0.65f, 0.12f, 1f);
    public Color hiddenTunnelColor = new Color(0.15f, 0.65f, 1f, 1f);
    public Color revealTriggerColor = new Color(1f, 0.05f, 0.85f, 1f);
    public Color spawnColor = new Color(0.1f, 1f, 0.1f, 1f);
    public Color bossExitColor = new Color(1f, 0.1f, 0.05f, 1f);
    public Color boundsColor = new Color(1f, 1f, 1f, 0.9f);

    [Header("Report")]
    public bool logReportOnStart = false;

    private void Start()
    {
        if (Application.isPlaying && logReportOnStart)
            StartCoroutine(LogReportAfterGeneration());
    }

    private IEnumerator LogReportAfterGeneration()
    {
        yield return null;
        LogDebugReport();
    }

    [ContextMenu("Log Map Debug Report")]
    public void LogDebugReport()
    {
        MapGenerator generator = GetGenerator();
        if (generator == null)
        {
            Debug.LogWarning("MapDebugOverlay could not find a MapGenerator to report.", this);
            return;
        }

        Debug.Log(generator.BuildDebugTextReport(), this);
    }

    private void OnDrawGizmos()
    {
        if (drawOnlyWhenSelected) return;
        DrawOverlay();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawOnlyWhenSelected) return;
        DrawOverlay();
    }

    private void DrawOverlay()
    {
        if (!showDebugOverlay) return;

        MapGenerator generator = GetGenerator();
        if (generator == null || generator.Data == null) return;

        MapDebugSnapshot snapshot = generator.BuildDebugSnapshot();
        if (snapshot == null || snapshot.width <= 0 || snapshot.height <= 0) return;

        if (showMapBounds) DrawBounds(snapshot);
        if (showOpenWalkableCells) DrawCells(snapshot, snapshot.openWalkableCells, openWalkableColor);
        if (showHiddenTunnelCells) DrawCells(snapshot, snapshot.hiddenTunnelCells, hiddenTunnelColor);
        if (showHiddenRoomCells) DrawCells(snapshot, snapshot.hiddenRoomCells, hiddenRoomColor);
        if (showRevealTriggerCells) DrawCells(snapshot, snapshot.revealTriggerCells, revealTriggerColor, 0.95f);
        if (showSpawnCells) DrawCells(snapshot, snapshot.spawnCells, spawnColor, 0.8f);
        if (showBossExitCells) DrawCells(snapshot, snapshot.bossExitCells, bossExitColor, 0.75f);
        if (showLabels) DrawLabels(snapshot);
    }

    private MapGenerator GetGenerator()
    {
        MapGenerator generator = GetComponent<MapGenerator>();
        if (generator != null) return generator;

        generator = GetComponentInParent<MapGenerator>();
        if (generator != null) return generator;

        return MapGenerator.Instance != null ? MapGenerator.Instance : Object.FindAnyObjectByType<MapGenerator>(FindObjectsInactive.Include);
    }

    private void DrawCells(MapDebugSnapshot snapshot, System.Collections.Generic.List<Vector2Int> cells, Color color, float alphaMultiplier = 1f)
    {
        if (cells == null || cells.Count == 0) return;

        Color drawColor = color;
        drawColor.a = Mathf.Clamp01(cellAlpha * alphaMultiplier);
        Gizmos.color = drawColor;

        Vector3 size = snapshot.CellSize3D(cellInset);
        foreach (Vector2Int cell in cells)
        {
            Vector3 center = snapshot.CellCenterToWorld(cell);
            center.z += zOffset;
            Gizmos.DrawCube(center, size);
        }
    }

    private void DrawBounds(MapDebugSnapshot snapshot)
    {
        Vector3 center = new Vector3(
            snapshot.origin.x + snapshot.width * snapshot.cellSize * 0.5f,
            snapshot.origin.y + snapshot.height * snapshot.cellSize * 0.5f,
            zOffset);
        Vector3 size = new Vector3(snapshot.width * snapshot.cellSize, snapshot.height * snapshot.cellSize, 0.01f);

        Gizmos.color = boundsColor;
        Gizmos.DrawWireCube(center, size);
    }

    private void DrawLabels(MapDebugSnapshot snapshot)
    {
#if UNITY_EDITOR
        foreach (MapDebugAreaSnapshot area in snapshot.areas)
        {
            if (area == null) continue;

            string label = "Area " + area.id + " " + area.areaType;
            if (area.hasExitPortal) label += " EXIT";
            if (area.enemyCount > 0 || area.bossEnemyCount > 0 || area.mushroomCount > 0 || area.sporeCount > 0 || area.shinyCount > 0)
            {
                label += "\nE:" + area.enemyCount + " B:" + area.bossEnemyCount +
                         " M:" + area.mushroomCount + " S:" + area.sporeCount +
                         " Sh:" + area.shinyCount;
            }

            Vector3 pos = new Vector3(area.centerWorld.x, area.centerWorld.y, zOffset - 0.05f);
            Handles.Label(pos, label);
        }

        Handles.Label(snapshot.CellCenterToWorld(snapshot.spawnCenter), "SPAWN");
#endif
    }
}
