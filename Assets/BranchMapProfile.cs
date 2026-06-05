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

    private void OnValidate()
    {
        if (width <= 0) width = 180;
        if (height <= 0) height = 120;
        if (cellSize <= 0f) cellSize = 0.75f;

        if (autoCenterSpawn)
            spawnCenter = new Vector2Int(width / 2, height / 2);
    }
}
