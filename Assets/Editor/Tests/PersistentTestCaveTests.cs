using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class PersistentTestCaveTests
{
    [Test]
    public void IntroOpeningStillRegistersTerrainAndAcceptsPlayerDig()
    {
        RunProfile intro = AssetDatabase.LoadAssetAtPath<RunProfile>("Assets/Intro Opening.asset");
        Assert.NotNull(intro);

        GameObject owner = new GameObject("Intro Dig Regression Test");
        owner.SetActive(false);
        GameObject tilemapObject = new GameObject("Intro Dirt Tilemap Test Fixture");
        try
        {
            owner.AddComponent<Grid>();
            tilemapObject.transform.SetParent(owner.transform, false);
            Tilemap dirtTilemap = tilemapObject.AddComponent<Tilemap>();
            tilemapObject.AddComponent<TilemapRenderer>();
            MapGenerator generator = owner.AddComponent<MapGenerator>();
            generator.dirtTilemap = dirtTilemap;
            generator.generateOnStart = false;
            generator.useProfilesByRunNumber = false;
            generator.selectedProfile = intro;
            generator.generateRunContent = false;
            generator.requireRunExit = false;
            owner.SetActive(true);
            // Edit Mode does not dispatch this runtime MonoBehaviour callback for an
            // object created inside a synchronous test. Invoke the production hook.
            typeof(MapGenerator).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(generator, null);
            generator.Generate();

            IDiggableTerrain terrain = generator;
            Assert.AreSame(generator, DiggableTerrainService.Active);

            Vector2Int target = intro.spawnCenter + Vector2Int.up * (intro.spawnRadius + 2);
            Assert.True(terrain.IsBlocked(target), "Test target must begin as Intro dirt.");
            TerrainDigResult result = terrain.DigCircle(terrain.CellToWorld(target), intro.cellSize * 1.25f,
                SporeGobbo.CampLifecycle.TerrainDigAuthority.Player, 0, null);

            Assert.True(result.Changed, "Player Dig must remove dirt in the Intro map.");
            Assert.False(terrain.IsBlocked(target));
        }
        finally
        {
            Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void PersistentExcavationRoundTripsWithoutOpeningUnmodifiedDirt()
    {
        PersistentTestCaveTerrainState state = new PersistentTestCaveTerrainState();
        state.EnsureIdentity("persistent-test-cave", 1);
        Vector2Int a = new Vector2Int(30, 50);
        Vector2Int b = new Vector2Int(31, 50);
        Vector2Int c = new Vector2Int(32, 50);
        state.RecordCleared(a); state.RecordCleared(b); state.RecordCleared(c);

        MapData reloaded = new MapData(120, 120, .6f);
        reloaded.FillBlocked();
        PersistentTestCaveRules.ApplySavedExcavation(reloaded, state.Clone(), new HashSet<Vector2Int>());

        Assert.False(reloaded.IsBlocked(a));
        Assert.False(reloaded.IsBlocked(b));
        Assert.False(reloaded.IsBlocked(c));
        Assert.True(reloaded.IsBlocked(new Vector2Int(33, 50)));
    }

    [Test]
    public void ResetPocketRestoresButAdjacentPersistentCellRemainsOpen()
    {
        PersistentTestCaveLayout layout = AssetDatabase.LoadAssetAtPath<PersistentTestCaveLayout>(
            "Assets/Persistent Test Cave Layout.asset");
        Assert.NotNull(layout);
        HashSet<Vector2Int> reset = PersistentTestCaveRules.BuildResetCells(layout, 120, 120);
        Vector2Int resetCell = new Vector2Int(72, 84);
        Vector2Int adjacentPersistentCell = new Vector2Int(79, 84);
        Assert.True(reset.Contains(resetCell));
        Assert.False(reset.Contains(adjacentPersistentCell));

        PersistentTestCaveTerrainState state = new PersistentTestCaveTerrainState();
        state.EnsureIdentity(layout.mapId, layout.layoutVersion);
        state.RecordCleared(resetCell);
        state.RecordCleared(adjacentPersistentCell);
        MapData reloaded = new MapData(120, 120, .6f);
        reloaded.FillBlocked();
        PersistentTestCaveRules.ApplySavedExcavation(reloaded, state, reset);

        Assert.True(reloaded.IsBlocked(resetCell));
        Assert.False(reloaded.IsBlocked(adjacentPersistentCell));
    }

    [Test]
    public void AllThreeCreaturePocketsAreExplicitResetZones()
    {
        PersistentTestCaveLayout layout = AssetDatabase.LoadAssetAtPath<PersistentTestCaveLayout>(
            "Assets/Persistent Test Cave Layout.asset");
        HashSet<Vector2Int> reset = PersistentTestCaveRules.BuildResetCells(layout, 120, 120);
        Assert.True(reset.Contains(new Vector2Int(72, 84)), "Weevil pocket");
        Assert.True(reset.Contains(new Vector2Int(72, 60)), "Blob pocket");
        Assert.True(reset.Contains(new Vector2Int(72, 36)), "Mushroom pocket");
    }

    [Test]
    public void CreatureAndResourceContentIsAuthoredToRepopulate()
    {
        PersistentTestCaveLayout layout = AssetDatabase.LoadAssetAtPath<PersistentTestCaveLayout>(
            "Assets/Persistent Test Cave Layout.asset");
        Assert.That(layout.pockets.Exists(p => p.content == PersistentTestCaveLayout.PocketContent.Weevils && p.count > 0));
        Assert.That(layout.pockets.Exists(p => p.content == PersistentTestCaveLayout.PocketContent.Blobs && p.count > 0));
        Assert.That(layout.pockets.Exists(p => p.content == PersistentTestCaveLayout.PocketContent.Mushrooms && p.count > 0));
        Assert.That(layout.pockets.Exists(p => p.content == PersistentTestCaveLayout.PocketContent.Shinies && p.count > 0));
        Assert.That(layout.pockets.Exists(p => p.content == PersistentTestCaveLayout.PocketContent.Exit));

        MapData map = new MapData(120, 120, .6f);
        foreach (PersistentTestCaveLayout.Pocket pocket in layout.pockets)
        {
            CampData content = PersistentTestCaveRules.CreatePocketContent(pocket, map, 42);
            Assert.AreEqual(pocket.count,
                content.enemyCount + content.exactBlobSpitterCount + content.mushroomCount + content.shinyCount +
                (content.hasExitPortal ? pocket.count : 0), pocket.label);
        }
    }

    [Test]
    public void LayoutVersionChangeStartsFreshAndCampaignClonesAreIndependent()
    {
        PersistentTestCaveTerrainState first = new PersistentTestCaveTerrainState();
        first.EnsureIdentity("persistent-test-cave", 1);
        first.RecordCleared(new Vector2Int(40, 40));
        PersistentTestCaveTerrainState secondCampaign = first.Clone();
        secondCampaign.clearedCells.Clear();
        Assert.AreEqual(1, first.clearedCells.Count);
        Assert.AreEqual(0, secondCampaign.clearedCells.Count);

        first.EnsureIdentity("persistent-test-cave", 2);
        Assert.AreEqual(0, first.clearedCells.Count);
    }

    [Test]
    public void NormalProductionProfileDoesNotSelectPersistentLayout()
    {
        RunProfile normal = AssetDatabase.LoadAssetAtPath<RunProfile>("Assets/Normal Run Baseline.asset");
        RunProfile persistent = AssetDatabase.LoadAssetAtPath<RunProfile>("Assets/Persistent Test Cave.asset");
        Assert.NotNull(normal);
        Assert.NotNull(persistent);
        Assert.IsNull(normal.persistentTestCaveLayout);
        Assert.NotNull(persistent.persistentTestCaveLayout);
        Assert.AreEqual(120, persistent.width);
        Assert.AreEqual(120, persistent.height);
    }
}
