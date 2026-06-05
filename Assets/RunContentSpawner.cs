using System.Collections.Generic;
using UnityEngine;

public class RunContentSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject enemyPrefab;
    public GameObject bossEnemyPrefab;
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

    private readonly HashSet<int> spawnedCampIds = new HashSet<int>();
    private readonly HashSet<int> spawnedTunnelIds = new HashSet<int>();

    public void ResetSpawnedContentTracking()
    {
        spawnedCampIds.Clear();
        spawnedTunnelIds.Clear();
    }

    private IEnumerator<WaitForSeconds> Start()
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
            return;

        foreach (TunnelData tunnel in map.Data.tunnels)
        {
            if (tunnel.revealed)
                SpawnTunnel(tunnel);
        }

        foreach (CampData camp in map.Data.camps)
        {
            if (camp.revealed)
                SpawnCamp(camp);
        }
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

        for (int i = 0; i < camp.enemyCount; i++)
            SpawnInCircle(normalEnemy, camp.center, camp.radius, enemyParent);

        for (int i = 0; i < camp.bossEnemyCount; i++)
            SpawnInCircle(bossEnemy, camp.center, camp.radius, enemyParent);

        for (int i = 0; i < camp.mushroomCount; i++)
            SpawnInCircle(mushroomPrefab, camp.center, camp.radius, itemParent);

        for (int i = 0; i < camp.sporeCount; i++)
            SpawnInCircle(sporePrefab, camp.center, camp.radius, itemParent);

        for (int i = 0; i < camp.shinyCount; i++)
            SpawnInCircle(shinyPrefab, camp.center, camp.radius, itemParent);

        if (camp.hasExitPortal)
            SpawnInCircle(exitPortalPrefab, camp.center, Mathf.Max(0.5f, camp.radius * 0.5f), exitParent);

        Debug.Log(
            $"RunContentSpawner spawned camp content | Id:{camp.id} NormalEnemies:{camp.enemyCount} BossEnemies:{camp.bossEnemyCount} Mushrooms:{camp.mushroomCount} Spores:{camp.sporeCount} Shinies:{camp.shinyCount} Exit:{camp.hasExitPortal}",
            this
        );
    }

    private void SpawnInCircle(GameObject prefab, Vector2 center, float radius, Transform parent)
    {
        if (prefab == null)
            return;

        if (!TryFindClearPoint(center, radius, out Vector2 point))
            point = center;

        Spawn(prefab, point, parent);
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
