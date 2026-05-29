using UnityEngine;

/// <summary>
/// Runtime spawning helpers for unified saved gobbos.
/// This lets recruitment/run systems spawn scene buddy objects from GobboUnitSaveData
/// without converting the saved identity back into BuddyData.
/// </summary>
public static class GobboRuntimeSpawnExtensions
{
    public static void SpawnGobboUnit(this GobboController player, GobboUnitSaveData data)
    {
        if (player == null || data == null) return;
        Vector2 spawnPos = (Vector2)player.transform.position + Random.insideUnitCircle * player.buddySpawnRadius;
        player.SpawnGobboUnit(data, spawnPos);
    }

    public static void SpawnGobboUnit(this GobboController player, GobboUnitSaveData data, Vector2 spawnPosition)
    {
        if (player == null) return;

        if (player.buddyPrefab == null)
        {
            Debug.LogWarning("No buddy prefab assigned on GobboController.");
            return;
        }

        if (data == null)
        {
            Debug.LogWarning("Tried to spawn buddy with no GobboUnitSaveData.");
            return;
        }

        data.isLeader = false;
        data.EnsureRuntimeDefaults();

        GameObject buddyObject = Object.Instantiate(player.buddyPrefab, spawnPosition, Quaternion.identity);
        buddyObject.name = string.IsNullOrWhiteSpace(data.displayName) ? "Gobbo Buddy" : data.displayName;

        int buddyLayer = LayerMask.NameToLayer("Buddy");
        if (buddyLayer >= 0) buddyObject.layer = buddyLayer;

        BuddyUnit unit = buddyObject.GetComponent<BuddyUnit>();
        if (unit != null) unit.Initialize(data);
        else Debug.LogWarning("Buddy prefab is missing BuddyUnit.", buddyObject);

        BuddyFollow follow = buddyObject.GetComponent<BuddyFollow>();
        if (follow != null)
        {
            follow.SetPlayer(player.transform);
            Vector2 offset = Random.insideUnitCircle;
            if (offset.sqrMagnitude < 0.001f) offset = Vector2.right;
            follow.SetFormationOffset(offset.normalized * player.buddyFormationSpread);
            follow.enabled = player.followersFollowing;
        }

        BuddyCombat combat = buddyObject.GetComponent<BuddyCombat>();
        if (combat != null)
        {
            combat.SetPlayer(player.transform);
            combat.enabled = player.followersAggressive;
        }

        BuddyBrain brain = buddyObject.GetComponent<BuddyBrain>();
        if (brain != null)
        {
            brain.allowCombat = true;
            brain.allowFollowing = true;
            brain.allowScavenging = data.collectsFood;
        }

        BuddyScavenger scavenger = buddyObject.GetComponent<BuddyScavenger>();
        if (scavenger != null) scavenger.enabled = data.collectsFood;

        player.AddFollower(1);
        Debug.Log("Spawned gobbo unit: " + data.displayName + " / " + data.gobboType + " / " + data.uniqueId);
    }
}
