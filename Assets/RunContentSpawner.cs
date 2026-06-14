using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RunContentSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject enemyPrefab;
    public GameObject bossEnemyPrefab;
    public GameObject blobSpitterPrefab;
    public GameObject mushroomPrefab;
    public GameObject sporePrefab;
    public GameObject shinyPrefab;
    public GameObject exitPortalPrefab;

    [Header("Parents")]
    public Transform enemyParent;
    public Transform itemParent;
    public Transform exitParent;

    [Header("Spawn Rules")]
    public bool spawnInitialRevealedContentOnStart = true;
    public float objectClearRadius = 0.35f;
    public int placementTriesPerObject = 40;

    [Header("Camp Enemy Counts")]
    public bool overrideCampWeevilCount = false;
    public int weevilsPerCampMin = 3;
    public int weevilsPerCampMax = 3;
    public bool overrideBossCampWeevilCount = false;
    public int weevilsPerBossCampMin = 2;
    public int weevilsPerBossCampMax = 2;

    [Header("Blob Spitters")]
    public int blobSpittersPerCampMin = 0;
    public int blobSpittersPerCampMax = 1;
    [Range(0f, 1f)] public float blobSpitterSpawnChance = 0.35f;
    public int blobSpittersPerBossCampMin = 0;
    public int blobSpittersPerBossCampMax = 1;
    [Range(0f, 1f)] public float blobSpitterBossSpawnChance = 0.5f;
    public float blobSpitterMinDistanceFromSpawn = 8f;

    private readonly HashSet<int> spawnedCampIds = new HashSet<int>();
    private readonly HashSet<int> spawnedTunnelIds = new HashSet<int>();

    public void ResetSpawnedContentTracking()
    {
        spawnedCampIds.Clear();
        spawnedTunnelIds.Clear();
    }

    private IEnumerator Start()
    {
        if (!spawnInitialRevealedContentOnStart)
            yield break;

        float wait = 0f;
        while ((MapGenerator.Instance == null || MapGenerator.Instance.Data == null) && wait < 3f)
        {
            wait += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }

        SpawnInitialContent();
    }

    public void SpawnInitialContent()
    {
        MapGenerator map = MapGenerator.Instance;
        if (map == null || map.Data == null)
        {
            Debug.LogWarning("RunContentSpawner: SpawnInitialContent called, but MapGenerator/Data is missing.", this);
            return;
        }

        int revealedTunnels = 0;
        int revealedCamps = 0;

        foreach (TunnelData tunnel in map.Data.tunnels)
        {
            if (tunnel.revealed)
            {
                revealedTunnels++;
                SpawnTunnel(tunnel);
            }
        }

        foreach (CampData camp in map.Data.camps)
        {
            if (camp.revealed)
            {
                revealedCamps++;
                SpawnCamp(camp);
            }
        }

        Debug.Log(
            $"RunContentSpawner initial content check | Tunnels:{map.Data.tunnels.Count} RevealedTunnels:{revealedTunnels} Camps:{map.Data.camps.Count} RevealedCamps:{revealedCamps}",
            this
        );
    }

    public void SpawnTunnel(TunnelData tunnel)
    {
        if (tunnel == null || spawnedTunnelIds.Contains(tunnel.id))
            return;

        spawnedTunnelIds.Add(tunnel.id);

        if (enemyPrefab == null)
        {
            Debug.LogWarning($"RunContentSpawner: tunnel {tunnel.id} wanted 1 enemy, but Enemy Prefab is missing.", this);
            return;
        }

        Vector2 spawnPoint = tunnel.enemySpawnPoint;

        if (!TryFindClearPoint(spawnPoint, Mathf.Max(0.5f, tunnel.radius), out Vector2 clearPoint))
            clearPoint = spawnPoint;

        Spawn(enemyPrefab, clearPoint, enemyParent);

        Debug.Log($"RunContentSpawner spawned tunnel enemy | Tunnel:{tunnel.id}", this);
    }

    public void SpawnCamp(CampData camp)
    {
        if (camp == null || spawnedCampIds.Contains(camp.id))
            return;

        spawnedCampIds.Add(camp.id);

        GameObject normalEnemy = enemyPrefab;
        GameObject bossEnemy = bossEnemyPrefab != null ? bossEnemyPrefab : enemyPrefab;
        int shinyCount = GetEffectiveShinyCount(camp);
        int weevilCount = GetWeevilCount(camp);
        int blobSpitterCount = GetBlobSpitterCount(camp);

        for (int i = 0; i < weevilCount; i++)
            SpawnInCircle(normalEnemy, camp.center, camp.radius, enemyParent);

        for (int i = 0; i < camp.bossEnemyCount; i++)
            SpawnInCircle(bossEnemy, camp.center, camp.radius, enemyParent);

        for (int i = 0; i < blobSpitterCount; i++)
            SpawnInCircle(blobSpitterPrefab, camp.center, camp.radius, enemyParent);

        for (int i = 0; i < camp.mushroomCount; i++)
            SpawnInCircle(mushroomPrefab, camp.center, camp.radius, itemParent);

        for (int i = 0; i < camp.sporeCount; i++)
            SpawnInCircle(sporePrefab, camp.center, camp.radius, itemParent);

        for (int i = 0; i < shinyCount; i++)
            SpawnShinyInCircle(camp.center, camp.radius, itemParent);

        if (camp.hasExitPortal)
            SpawnInCircle(exitPortalPrefab, camp.center, Mathf.Max(0.5f, camp.radius * 0.5f), exitParent);

        Debug.Log(
            $"RunContentSpawner spawned camp content | Id:{camp.id} Weevils:{weevilCount} BossEnemies:{camp.bossEnemyCount} BlobSpitters:{blobSpitterCount} Mushrooms:{camp.mushroomCount} Spores:{camp.sporeCount} Shinies:{shinyCount} Exit:{camp.hasExitPortal}",
            this
        );
    }

    private int GetWeevilCount(CampData camp)
    {
        if (camp == null)
            return 0;

        if (camp.isBossCamp && overrideBossCampWeevilCount)
            return Random.Range(Mathf.Max(0, weevilsPerBossCampMin), Mathf.Max(weevilsPerBossCampMin, weevilsPerBossCampMax) + 1);

        if (!camp.isBossCamp && overrideCampWeevilCount)
            return Random.Range(Mathf.Max(0, weevilsPerCampMin), Mathf.Max(weevilsPerCampMin, weevilsPerCampMax) + 1);

        return camp.enemyCount;
    }

    private int GetBlobSpitterCount(CampData camp)
    {
        if (camp == null || blobSpitterPrefab == null)
            return 0;

        if (camp.enemyCount <= 0 && camp.bossEnemyCount <= 0)
            return 0;

        if (IsTooCloseToSpawn(camp.center))
            return 0;

        bool isBossCamp = camp.isBossCamp || camp.bossEnemyCount > 0 || camp.hasExitPortal;
        float chance = isBossCamp ? blobSpitterBossSpawnChance : blobSpitterSpawnChance;

        if (Random.value > chance)
            return 0;

        int min = isBossCamp ? blobSpittersPerBossCampMin : blobSpittersPerCampMin;
        int max = isBossCamp ? blobSpittersPerBossCampMax : blobSpittersPerCampMax;
        min = Mathf.Max(0, min);
        max = Mathf.Max(min, max);
        return Random.Range(min, max + 1);
    }

    private bool IsTooCloseToSpawn(Vector2 position)
    {
        MapGenerator map = MapGenerator.Instance;
        if (map == null || map.Data == null)
            return false;

        Vector2 spawnWorld = map.Data.CellToWorld(map.map.spawnCenter);
        return Vector2.Distance(position, spawnWorld) < Mathf.Max(0f, blobSpitterMinDistanceFromSpawn);
    }

    private int GetEffectiveShinyCount(CampData camp)
    {
        if (camp == null) return 0;
        if (camp.shinyCount > 0) return camp.shinyCount;

        bool isBossCamp = camp.isBossCamp || camp.bossEnemyCount > 0 || camp.hasExitPortal;
        bool isResourceCamp = camp.enemyCount >= 3 && camp.mushroomCount >= 3;
        return isBossCamp || isResourceCamp ? 1 : 0;
    }

    private void SpawnShinyInCircle(Vector2 center, float radius, Transform parent)
    {
        GameObject shiny = SpawnInCircle(shinyPrefab, center, radius, parent);
        EnsureShinyPickup(shiny);
    }

    private void EnsureShinyPickup(GameObject shiny)
    {
        if (shiny == null) return;

        if (shiny.GetComponent<CollectibleShiny>() == null)
            shiny.AddComponent<CollectibleShiny>();

        Collider2D collider = shiny.GetComponent<Collider2D>();
        if (collider == null)
        {
            CircleCollider2D circle = shiny.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = 0.5f;
        }
    }

    private GameObject SpawnInCircle(GameObject prefab, Vector2 center, float radius, Transform parent)
    {
        if (prefab == null)
            return null;

        if (!TryFindClearPoint(center, radius, out Vector2 point))
            point = center;

        return Spawn(prefab, point, parent);
    }

    private GameObject Spawn(GameObject prefab, Vector2 position, Transform parent)
    {
        if (prefab == null)
            return null;

        Vector3 pos = new Vector3(position.x, position.y, 0f);
        return Instantiate(prefab, pos, Quaternion.identity, parent);
    }

    private bool TryFindClearPoint(Vector2 center, float radius, out Vector2 point)
    {
        MapGenerator map = MapGenerator.Instance;

        for (int i = 0; i < placementTriesPerObject; i++)
        {
            Vector2 offset = Random.insideUnitCircle * Mathf.Max(0.1f, radius * 0.85f);
            Vector2 candidate = center + offset;

            if (map == null || map.IsWorldPositionClearForBody(candidate, objectClearRadius))
            {
                point = candidate;
                return true;
            }
        }

        point = center;
        return false;
    }
}
