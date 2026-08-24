using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BranchMapProfile", menuName = "Spore Gobbo/Maps/Branch Map Profile")]
public class BranchMapProfile : ScriptableObject
{
    [Header("Seed")]
    public int seed = 0;
    public bool randomSeed = true;

    [Header("Generation Bounds")]
    public int width = 180;
    public int height = 120;
    public float cellSize = 0.75f;

    [Header("Spawn")]
    public bool autoCenterSpawn = true;
    [HideInInspector] public Vector2Int spawnCenter = new Vector2Int(90, 60);
    public int spawnRadius = 5;

    [Header("Dirt Darkness")]
    public int darkDistance = 20;
    public int darkerDistance = 35;

    [Header("Filler")]
    public int fillerTunnelCount = 12;
    public int fillerPocketCount = 8;
    public int fillerLootPocketCount = 0;
    public int fillerMinDistanceFromMainPath = 7;
    public int fillerMaxDistanceFromMainPath = 35;

    [Header("Branches")]
    public List<MapGenerator.BranchSettings> branches = new List<MapGenerator.BranchSettings>();

    [Header("Optional Terminal Pocket")]
    public bool createTerminalPocketAtPrimaryBranchEnd;
    [Min(1)] public int terminalPocketRadius = 5;

    [Header("Optional Attached Pocket Shaping")]
    public bool useOrganicAttachedPockets;
    [Range(0f, 0.75f)] public float attachedPocketEdgeIrregularity = 0.3f;
    [Range(0, 5)] public int attachedPocketOverlapCells = 2;

    private void OnValidate()
    {
        if (width <= 0) width = 180;
        if (height <= 0) height = 120;
        if (cellSize <= 0f) cellSize = 0.75f;
        if (terminalPocketRadius < 1) terminalPocketRadius = 1;
        attachedPocketEdgeIrregularity = Mathf.Clamp(attachedPocketEdgeIrregularity, 0f, 0.75f);
        attachedPocketOverlapCells = Mathf.Clamp(attachedPocketOverlapCells, 0, 5);

        if (autoCenterSpawn)
            spawnCenter = new Vector2Int(width / 2, height / 2);
    }
}
