using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Persistent Test Cave Layout", menuName = "Spore Gobbo/Maps/Persistent Test Cave Layout")]
public sealed class PersistentTestCaveLayout : ScriptableObject
{
    public enum PocketContent { Weevils, Blobs, Mushrooms, Shinies, Exit }

    [Serializable]
    public sealed class Pocket
    {
        public string label = "Pocket";
        public Vector2Int center = new Vector2Int(60, 60);
        [Min(2)] public int radius = 4;
        public PocketContent content;
        [Min(0)] public int count = 1;
        public bool startsRevealed;
    }

    [Serializable]
    public sealed class AuthoredEventLocation
    {
        public string markerKey;
        public Vector2Int actorCell;
        public Vector2Int focusCell;
        [Tooltip("Camera offset while focused. Z should normally remain -10.")]
        public Vector3 cameraOffset = new Vector3(0f, 0f, -10f);
        [Range(0.5f, 1f), Tooltip("Relative to the player's current orthographic size; smaller is closer.")]
        public float cameraZoomMultiplier = 0.82f;
        public Vector2Int triggerCell;
        [Min(0.5f), Tooltip("Radius in map cells.")] public float triggerRadius = 2f;
        public List<Vector2Int> departureWaypoints = new List<Vector2Int>();
        public Vector2Int despawnCell;
    }

    [Header("Persistent Map Identity")]
    public string mapId = "persistent-test-cave";
    [Min(1)] public int layoutVersion = 1;

    [Header("Base Layout")]
    public Vector2Int spawnCenter = new Vector2Int(18, 60);
    [Min(2)] public int spawnRadius = 5;
    [Tooltip("An Inspector-visible planning rectangle. Dirt here behaves like all non-reset dirt: player excavation persists.")]
    public RectInt persistentDigTestArea = new RectInt(28, 43, 27, 34);
    [Tooltip("Authored cells that begin open every expedition, normally the spawn chamber and short access lane.")]
    public List<RectInt> initiallyOpenAreas = new List<RectInt>();

    [Header("Explicit Reset Pockets")]
    public List<Pocket> pockets = new List<Pocket>();

    [Header("Named Story Event Locations")]
    public List<AuthoredEventLocation> eventLocations = new List<AuthoredEventLocation>();

    public bool TryGetEventLocation(string markerKey, out AuthoredEventLocation location)
    {
        location = null;
        if (string.IsNullOrWhiteSpace(markerKey) || eventLocations == null) return false;
        for (int i = 0; i < eventLocations.Count; i++)
        {
            AuthoredEventLocation candidate = eventLocations[i];
            if (candidate != null && string.Equals(candidate.markerKey, markerKey, StringComparison.Ordinal))
            { location = candidate; return true; }
        }
        return false;
    }

    public bool IsValidFor(RunProfile profile, out string error)
    {
        error = "";
        if (profile == null) { error = "RunProfile is missing."; return false; }
        if (string.IsNullOrWhiteSpace(mapId)) { error = "Map ID is blank."; return false; }
        if (layoutVersion < 1) { error = "Layout version must be positive."; return false; }
        if (profile.width != 120 || profile.height != 120) { error = "Persistent Test Cave must be 120x120."; return false; }
        if (!InBounds(spawnCenter, profile.width, profile.height)) { error = "Spawn is outside the map."; return false; }
        if (pockets == null || pockets.Count == 0) { error = "At least one pocket is required."; return false; }
        foreach (Pocket pocket in pockets)
        {
            if (pocket == null) { error = "A pocket entry is null."; return false; }
            if (pocket.radius < 2 || pocket.center.x - pocket.radius - 1 < 0 || pocket.center.y - pocket.radius - 1 < 0 ||
                pocket.center.x + pocket.radius + 1 >= profile.width || pocket.center.y + pocket.radius + 1 >= profile.height)
            { error = "Pocket '" + pocket.label + "' does not fit inside the map with its reset boundary."; return false; }
        }
        var markerKeys = new HashSet<string>(StringComparer.Ordinal);
        if (eventLocations != null) foreach (AuthoredEventLocation location in eventLocations)
        {
            if (location == null || string.IsNullOrWhiteSpace(location.markerKey))
            { error = "An authored event location or marker key is missing."; return false; }
            if (!markerKeys.Add(location.markerKey))
            { error = "Duplicate authored event marker '" + location.markerKey + "'."; return false; }
            if (!InBounds(location.actorCell, profile.width, profile.height) ||
                !InBounds(location.focusCell, profile.width, profile.height) ||
                !InBounds(location.triggerCell, profile.width, profile.height) ||
                !InBounds(location.despawnCell, profile.width, profile.height))
            { error = "Authored event marker '" + location.markerKey + "' is outside the map."; return false; }
            if (location.cameraZoomMultiplier < 0.5f || location.cameraZoomMultiplier > 1f)
            { error = "Authored event marker '" + location.markerKey + "' has an invalid camera zoom multiplier."; return false; }
            foreach (Vector2Int waypoint in location.departureWaypoints)
                if (!InBounds(waypoint, profile.width, profile.height))
                { error = "Authored event marker '" + location.markerKey + "' has an out-of-bounds waypoint."; return false; }
        }
        return true;
    }

    static bool InBounds(Vector2Int cell, int width, int height) =>
        cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;
}
