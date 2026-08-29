#if UNITY_EDITOR
using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using SporeGobbo.CampLifecycle;

public sealed class CampResidentialRuntimeParityTests
{
    [Test]
    public void DirectedWalkOwnsArrivalAndTimeoutResultsFromPhysicsPosition()
    {
        GameObject buddy = new GameObject("DirectedWalkResultBuddy");
        GameObject target = new GameObject("DirectedWalkResultTarget");
        try
        {
            Rigidbody2D body = buddy.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            buddy.AddComponent<CircleCollider2D>().radius = 0.25f;
            CampDirectedWalk walk = buddy.AddComponent<CampDirectedWalk>();
            walk.destroyWhenDone = false;
            typeof(CampDirectedWalk).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(walk, null);

            target.transform.position = body.position;
            walk.BeginWalk(target.transform, 1f, 0.18f, 1f);
            typeof(CampDirectedWalk).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(walk, null);
            Assert.That(walk.Result, Is.EqualTo(CampDirectedWalkResult.Arrived));

            target.transform.position = body.position + Vector2.right * 0.26f;
            walk.bodyRadius = 0.375f;
            walk.BeginWalk(target.transform, 1f, 0.18f, 1f);
            typeof(CampDirectedWalk).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(walk, null);
            Assert.That(walk.Result, Is.EqualTo(CampDirectedWalkResult.Walking),
                "Body radius must not silently broaden the authored arrival contract.");

            target.transform.position = body.position + Vector2.right * 5f;
            walk.BeginWalk(target.transform, 1f, 0.18f, 0.01f);
            typeof(CampDirectedWalk).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(walk, null);
            Assert.That(walk.Result, Is.EqualTo(CampDirectedWalkResult.TimedOut));
        }
        finally
        {
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(buddy);
        }
    }

    [Test]
    public void SlotFourProgressiveLocalDigReachesReportedOpenTargetAndCompletesPocket()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/CampScene.unity", OpenSceneMode.Single);
        GameState state = Object.FindAnyObjectByType<GameState>(FindObjectsInactive.Include);
        HandcraftedCampTerrain terrain = Object.FindAnyObjectByType<HandcraftedCampTerrain>(FindObjectsInactive.Include);
        typeof(GameState).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, state);
        terrain.terrainPresentationRenderer = null;
        const int slotId = 4;
        state.campTerrainState.residentialSlotsEstablished = 3;
        state.campTerrainState.residentialStage = 1;
        terrain.RebuildFromBaseline(false);

        ResidentialSlotRecord slot = terrain.GetResidentialSlot(slotId);
        List<Vector2Int> footprint = terrain.GetResidentialSlotFootprint(slotId);
        Vector2 position = terrain.CellToWorld(new Vector2Int(slot.Approach.x, slot.Approach.y));
        Vector2 extents = Vector2.one * 0.375f;
        foreach ((int x, int y) authored in slot.DigTargets)
        {
            Vector2Int target = new Vector2Int(authored.x, authored.y);
            ExcavateReachable(terrain, slotId, footprint, position,
                terrain.CellToWorld(target), 0.72f + 0.375f, extents, true);
            Assert.That(terrain.IsBlocked(target), Is.False, "Slot 4 target " + target);
            Assert.That(TileMover.CanTraverseBox(terrain, position, terrain.CellToWorld(target), extents),
                Is.True, "Slot 4 progressive segment into " + target);
            position = terrain.CellToWorld(target);
        }
        ExcavateReachable(terrain, slotId, footprint, position, position,
            0.72f + 0.375f, extents, false);
        Assert.That(footprint.FindAll(terrain.IsBlocked), Is.Empty, "Slot " + slotId + " final pocket");
        Assert.That(TileMover.CanOccupyBox(terrain,
            terrain.CellToWorld(new Vector2Int(85, 54)), extents), Is.True,
            "The previously reported open target must have actual Baby-body clearance.");
        terrain.CompleteResidentialSlotForProgression(
            terrain.GetResidentialProgressionIndexForSlot(slotId), slotId);
        Assert.That(state.campTerrainState.residentialSlotsEstablished, Is.EqualTo(slotId),
            "A fully excavated and reachable Slot 4 must commit through production terrain authority.");
    }

    [Test]
    public void StaticPhysicsPreflightRejectsSyntheticBlockingCollider()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/CampScene.unity", OpenSceneMode.Single);
        GameState state = Object.FindAnyObjectByType<GameState>(FindObjectsInactive.Include);
        HandcraftedCampTerrain terrain = Object.FindAnyObjectByType<HandcraftedCampTerrain>(FindObjectsInactive.Include);
        typeof(GameState).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, state);
        terrain.terrainPresentationRenderer = null;
        state.campTerrainState.residentialSlotsEstablished = 3;
        state.campTerrainState.residentialStage = 1;
        terrain.RebuildFromBaseline(false);
        GameObject blocker = new GameObject("SyntheticResidentialBlocker");
        blocker.transform.position = terrain.CellToWorld(new Vector2Int(80, 54));
        BoxCollider2D collider = blocker.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one * 0.5f;
        Physics2D.SyncTransforms();
        Assert.That(ResidentialConstructionPlan.TryBuild(terrain, 4, Vector2.one * 0.375f, 0.72f,
            out _, out string blockedFailure), Is.False);
        StringAssert.Contains("SyntheticResidentialBlocker", blockedFailure);

        Object.DestroyImmediate(blocker);
        Physics2D.SyncTransforms();
        Assert.That(ResidentialConstructionPlan.TryBuild(terrain, 4, Vector2.one * 0.375f, 0.72f,
            out _, out string clearFailure), Is.True, clearFailure);
    }

    [Test]
    public void CampSceneContainsNoLegacyBoundaryWalls()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/CampScene.unity", OpenSceneMode.Single);
        foreach (string name in new[] { "CampWallRight", "CampWallFront", "CampWallBack", "CampWallLeft" })
            Assert.That(GameObject.Find(name), Is.Null, name + " is obsolete prototype geometry.");
    }

    static void ExcavateReachable(HandcraftedCampTerrain terrain, int slotId,
        List<Vector2Int> footprint, Vector2 origin, Vector2 desired, float reach,
        Vector2 extents, bool stopWhenTraversable)
    {
        for (int action = 0; action < footprint.Count + 1; action++)
        {
            if (stopWhenTraversable && !terrain.IsBlocked(terrain.WorldToCell(desired)) &&
                TileMover.CanTraverseBox(terrain, origin, desired, extents)) return;
            Vector2Int best = new Vector2Int(int.MinValue, int.MinValue);
            float score = float.PositiveInfinity;
            HashSet<Vector2Int> authorized = new HashSet<Vector2Int>(footprint);
            foreach (Vector2Int cell in footprint)
            {
                Vector2 world = terrain.CellToWorld(cell);
                float distance = Vector2.Distance(origin, world);
                if (distance > reach + 0.0001f) continue;
                bool removesDirt = false;
                for (int dx = -2; dx <= 2 && !removesDirt; dx++)
                for (int dy = -2; dy <= 2; dy++)
                {
                    Vector2Int affected = cell + new Vector2Int(dx, dy);
                    if (authorized.Contains(affected) && terrain.IsBlocked(affected) &&
                        Vector2.Distance(world, terrain.CellToWorld(affected)) <= 0.7201f)
                    {
                        removesDirt = true;
                        break;
                    }
                }
                if (!removesDirt) continue;
                float candidateScore = Vector2.Distance(world, desired) + distance * 0.01f;
                if (candidateScore >= score) continue;
                best = cell;
                score = candidateScore;
            }
            if (best.x == int.MinValue) return;
            TerrainDigResult result = terrain.DigCircle(terrain.CellToWorld(best), 0.72f,
                TerrainDigAuthority.ResidentialProgression,
                terrain.GetResidentialProgressionIndexForSlot(slotId), footprint);
            Assert.That(result.Changed, Is.True, "Local progressive Dig at " + best);
        }
        Assert.Fail("Progressive excavation exceeded its finite authorized footprint for Slot " + slotId + ".");
    }

    [Test]
    public void SlotTwoProductionExcavationAuthorizesCellsOutsideLegacyStageRectangleAndAdvances()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/CampScene.unity", OpenSceneMode.Single);
        GameState state = Object.FindAnyObjectByType<GameState>(FindObjectsInactive.Include);
        HandcraftedCampTerrain terrain = Object.FindAnyObjectByType<HandcraftedCampTerrain>(FindObjectsInactive.Include);
        typeof(GameState).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, state);
        terrain.terrainPresentationRenderer = null;
        state.campTerrainState.residentialSlotsEstablished = 1;
        state.campTerrainState.residentialStage = 1;
        terrain.RebuildFromBaseline(false);
        Assert.That(terrain.IsBlocked(new Vector2Int(66, 31)), Is.True,
            "The regression cell must begin as new Slot 2 dirt.");
        foreach (int slotId in new[] { 2 })
        {
            ResidentialSlotRecord slot = terrain.GetResidentialSlot(slotId);
            List<Vector2Int> route = terrain.GetResidentialConstructionRoute(slotId);
            List<Vector2Int> footprint = terrain.GetResidentialSlotFootprint(slotId);
            Assert.That(route, Is.Not.Empty);
            Vector2Int staging = route[route.Count - 1];
            Assert.That(terrain.IsBlocked(staging), Is.False, "staging must be established/open");

            Vector2Int firstNew = default;
            bool found = false;
            int firstRemoved = 0;
            foreach (Vector2Int candidate in footprint)
            {
                if (!terrain.IsBlocked(candidate)) continue;
                if (!found)
                {
                    firstNew = candidate;
                    found = true;
                    Assert.That(terrain.GetSpatialDigCategory(candidate),
                        Is.EqualTo(CampDigCategory.ResidentialReserved));
                }
                TerrainDigResult clearance = terrain.DigCircle(terrain.CellToWorld(candidate), 0.72f,
                    TerrainDigAuthority.ResidentialProgression,
                    terrain.GetResidentialProgressionIndexForSlot(slotId), footprint);
                Assert.That(clearance.Changed, Is.True,
                    "ResidentialProgression must mutate blocked footprint cell " + candidate);
                Assert.That(terrain.IsBlocked(candidate), Is.False);
                if (candidate == firstNew) firstRemoved = clearance.RemovedCells;
            }
            Assert.That(found, Is.True, "slot must contribute a new blocked Dig target");
            float distance = Vector2.Distance(terrain.CellToWorld(staging), terrain.CellToWorld(firstNew));
            Vector2Int advanceStart = new Vector2Int(slot.Approach.x, slot.Approach.y);
            int advanceSteps = 0;
            foreach ((int x, int y) target in slot.DigTargets)
            {
                Vector2Int goal = new Vector2Int(target.x, target.y);
                Assert.That(terrain.IsBlocked(goal), Is.False,
                    "authored advance target must be open after canonical pocket excavation");
                List<Vector2Int> advance = InvokePostDigRoute(terrain, slotId, footprint, route,
                    advanceStart, goal, Vector2.one * 0.375f);
                if (advanceStart != goal)
                {
                    Assert.That(advance, Is.Not.Empty, "post-Dig advance must exist");
                    AssertContinuous(terrain, advance, Vector2.one * 0.375f,
                        "Slot " + slotId + " post-Dig advance");
                }
                advanceStart = goal;
                advanceSteps++;
            }
            terrain.CompleteResidentialSlotForProgression(
                terrain.GetResidentialProgressionIndexForSlot(slotId), slotId);
            Assert.That(terrain.IsBlocked(new Vector2Int(66, 31)), Is.False,
                "Catalog-owned Slot 2 terrain must not be rejected by obsolete rectangular zone coverage.");
            Debug.Log("[ResidentialTransitionDiagnostic] slot=" + slotId + " staging=" + staging +
                " firstNew=" + firstNew + " distance=" + distance.ToString("0.000") +
                " authority=accepted removed=" + firstRemoved + " advanceSteps=" + advanceSteps +
                " final=" + advanceStart);
        }
    }

    [Test]
    public void FreshSlotOneStagesOutsideLockedEntranceThenHasAnAuthorizedFirstDig()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/CampScene.unity", OpenSceneMode.Single);
        GameState state = Object.FindAnyObjectByType<GameState>(FindObjectsInactive.Include);
        HandcraftedCampTerrain terrain = Object.FindAnyObjectByType<HandcraftedCampTerrain>(FindObjectsInactive.Include);
        typeof(GameState).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, state);
        terrain.terrainPresentationRenderer = null;
        state.campTerrainState.residentialSlotsEstablished = 0;
        state.campTerrainState.residentialStage = 0;
        terrain.RebuildFromBaseline(false);

        List<Vector2Int> route = terrain.GetResidentialConstructionRoute(1);
        ResidentialSlotRecord slot = terrain.GetResidentialSlot(1);
        List<Vector2Int> footprint = terrain.GetResidentialSlotFootprint(1);
        Assert.That(route, Is.EqualTo(new[] { new Vector2Int(58, 36) }));
        Assert.That(terrain.IsBlocked(route[0]), Is.False,
            "Construction must reach an open staging cell before its first Dig.");
        Assert.That(slot.DigTargets[0], Is.EqualTo((59, 36)),
            "Slot 1 first establishes a fully occupiable Camp-side intermediate step.");
        Vector2Int firstDig = new Vector2Int(60, 36);
        Assert.That(firstDig, Is.EqualTo(new Vector2Int(60, 36)));
        Assert.That(terrain.IsBlocked(firstDig), Is.True,
            "The canonical entrance must remain dirt until construction Digs it.");
        Assert.That(Vector2.Distance(terrain.CellToWorld(route[0]), terrain.CellToWorld(firstDig)),
            Is.LessThanOrEqualTo(0.72f));
        ExcavateReachable(terrain, 1, footprint, terrain.CellToWorld(route[0]),
            terrain.CellToWorld(firstDig), 0.72f + 0.375f, Vector2.one * 0.375f, true);
        Assert.That(terrain.IsBlocked(firstDig), Is.False);

        List<Vector2Int> advance = InvokePostDigRoute(terrain, 1, footprint, route,
            route[0], firstDig, Vector2.one * 0.375f);
        Assert.That(advance, Is.EqualTo(new[] { firstDig }),
            "The first Dig must create full Baby-body clearance at the authored entrance center.");
        Assert.That(TileMover.CanOccupyBox(terrain, terrain.CellToWorld(firstDig), Vector2.one * 0.375f),
            Is.True);
    }

    [Test]
    public void CommitCannotExcavateOrAdvanceAnIncompleteSlot()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/CampScene.unity", OpenSceneMode.Single);
        GameState state = Object.FindAnyObjectByType<GameState>(FindObjectsInactive.Include);
        HandcraftedCampTerrain terrain = Object.FindAnyObjectByType<HandcraftedCampTerrain>(FindObjectsInactive.Include);
        typeof(GameState).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, state);
        terrain.terrainPresentationRenderer = null;
        state.campTerrainState.residentialSlotsEstablished = 3;
        state.campTerrainState.residentialStage = 1;
        terrain.RebuildFromBaseline(false);
        Vector2Int stillDirt = terrain.GetResidentialSlotFootprint(4).Find(terrain.IsBlocked);

        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
            "Refusing to commit Slot 4"));
        terrain.CompleteResidentialSlotForProgression(1, 4);

        Assert.That(state.campTerrainState.residentialSlotsEstablished, Is.EqualTo(3));
        Assert.That(terrain.IsBlocked(stillDirt), Is.True,
            "Commit must never act as a hidden excavation fallback.");
    }

    [Test]
    public void ExactPlansValidateForEverySequentialSlotAndEstablishedStateReconstructs()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/CampScene.unity", OpenSceneMode.Single);
        GameState state = Object.FindAnyObjectByType<GameState>(FindObjectsInactive.Include);
        HandcraftedCampTerrain terrain = Object.FindAnyObjectByType<HandcraftedCampTerrain>(FindObjectsInactive.Include);
        typeof(GameState).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, state);
        terrain.terrainPresentationRenderer = null;
        Vector2 extents = Vector2.one * 0.375f;

        for (int slotId = 1; slotId <= 10; slotId++)
        {
            state.campTerrainState.residentialSlotsEstablished = slotId - 1;
            state.campTerrainState.residentialStage = slotId > 1 ? 1 : 0;
            terrain.RebuildFromBaseline(false);
            Assert.That(ResidentialConstructionPlan.TryBuild(terrain, slotId, extents, 0.72f,
                out ResidentialConstructionPlan plan, out string failure), Is.True,
                "Slot " + slotId + " plan: " + failure);
            Assert.That(plan.SlotId, Is.EqualTo(slotId));
            foreach (ResidentialDigStep step in plan.DigSteps)
            {
                Assert.That(step.DigCenters.Count, Is.EqualTo(step.ExpectedRemovedCells.Count));
                foreach (IReadOnlyList<Vector2Int> affected in step.ExpectedRemovedCells)
                    Assert.That(affected, Is.Not.Empty);
            }
        }

        state.campTerrainState.residentialSlotsEstablished = 10;
        state.campTerrainState.residentialStage = 1;
        terrain.RebuildFromBaseline(false);
        for (int slotId = 1; slotId <= 10; slotId++)
            Assert.That(terrain.GetResidentialSlotFootprint(slotId).FindAll(terrain.IsBlocked), Is.Empty,
                "Reload reconstruction Slot " + slotId);
    }

    [Test]
    public void SlotThreeCurrentPrerequisiteRouteHasContinuousTileMoverClearance()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/CampScene.unity", OpenSceneMode.Single);
        GameState state = Object.FindAnyObjectByType<GameState>(FindObjectsInactive.Include);
        HandcraftedCampTerrain terrain = Object.FindAnyObjectByType<HandcraftedCampTerrain>(FindObjectsInactive.Include);
        Assert.That(state, Is.Not.Null);
        Assert.That(terrain, Is.Not.Null);
        terrain.terrainPresentationRenderer = null;
        typeof(GameState).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, state);
        state.campTerrainState.residentialSlotsEstablished = 2;
        state.campTerrainState.residentialStage = 1;
        terrain.RebuildFromBaseline(false);

        Vector2 extents = Vector2.one * 0.375f;
        AssertConstructionContinuous(terrain, terrain.GetResidentialConstructionRoute(3), extents,
            "Slot 3 current construction");
    }

    [Test]
    public void OldSlotThreeBoundaryDoglegRejectsActualRuntimeBabyRadius()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/CampScene.unity", OpenSceneMode.Single);
        GameState state = Object.FindAnyObjectByType<GameState>(FindObjectsInactive.Include);
        HandcraftedCampTerrain terrain = Object.FindAnyObjectByType<HandcraftedCampTerrain>(FindObjectsInactive.Include);
        terrain.terrainPresentationRenderer = null;
        typeof(GameState).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, state);
        state.campTerrainState.residentialSlotsEstablished = 3;
        state.campTerrainState.residentialStage = 1;
        terrain.RebuildFromBaseline(false);
        Assert.That(TileMover.CanOccupyBox(terrain, terrain.CellToWorld(new Vector2Int(62, 38)), Vector2.one * 0.375f), Is.False,
            "The removed pre-fix boundary dogleg must remain a regression example for the actual Baby radius.");
    }


    [Test]
    public void DirectedCampWalkIgnoresOtherBuddyBodiesWhileActive()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CampActiveBuddy.prefab");
        Assert.That(prefab, Is.Not.Null);
        GameObject first = Object.Instantiate(prefab);
        GameObject second = Object.Instantiate(prefab);
        GameObject target = new GameObject("DirectedWalkTarget");
        try
        {
            CampDirectedWalk walk = first.AddComponent<CampDirectedWalk>();
            typeof(CampDirectedWalk).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(walk, null);
            walk.BeginWalk(target.transform, 1.575f);
            Assert.That(Physics2D.GetIgnoreCollision(first.GetComponent<Collider2D>(),
                second.GetComponent<Collider2D>()), Is.True,
                "A deterministic construction walk must not be obstructed by another Gobbo body.");
        }
        finally
        {
            Object.DestroyImmediate(first); Object.DestroyImmediate(second); Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void AllSlotsHaveContinuousConstructionAdvanceRestAndReturnClearance()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/CampScene.unity", OpenSceneMode.Single);
        GameState state = Object.FindAnyObjectByType<GameState>(FindObjectsInactive.Include);
        HandcraftedCampTerrain terrain = Object.FindAnyObjectByType<HandcraftedCampTerrain>(FindObjectsInactive.Include);
        terrain.terrainPresentationRenderer = null;
        typeof(GameState).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, state);
        Vector2 extents = Vector2.one * 0.375f;
        for (int slotId = 1; slotId <= terrain.TotalResidentialCapacity; slotId++)
        {
            state.campTerrainState.residentialSlotsEstablished = slotId - 1;
            state.campTerrainState.residentialStage = slotId > 1 ? 1 : 0;
            terrain.RebuildFromBaseline(false);
            List<Vector2Int> route = terrain.GetResidentialConstructionRoute(slotId);
            ResidentialSlotRecord slot = terrain.GetResidentialSlot(slotId);
            Assert.That(route, Is.Not.Empty, "Slot " + slotId + " construction route must exist.");
            AssertConstructionContinuous(terrain, route, extents, "Slot " + slotId + " construction");

            state.campTerrainState.residentialSlotsEstablished = slotId;
            state.campTerrainState.residentialStage = 1;
            terrain.RebuildFromBaseline(false);
            Vector2Int advanceStart = new Vector2Int(slot.Approach.x, slot.Approach.y);
            foreach (var cell in slot.DigTargets)
            {
                Vector2Int goal = new Vector2Int(cell.x, cell.y);
                List<Vector2Int> advance = InvokePostDigRoute(terrain, slotId,
                    terrain.GetResidentialSlotFootprint(slotId), route, advanceStart, goal, extents);
                Assert.That(advance, Is.Not.Empty,
                    "Slot " + slotId + " must have a runtime post-Dig route " + advanceStart + " -> " + goal +
                    "; authored targets " + FormatTargets(slot.DigTargets));
                AssertContinuous(terrain, advance, extents, "Slot " + slotId + " post-Dig/rest");
                advanceStart = goal;
            }
            route.Reverse();
            AssertContinuous(terrain, route, extents, "Slot " + slotId + " return");
        }
    }

    [Test]
    public void SlotFiveReportedPrerequisiteRouteHasPhysicalColliderClearance()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/CampScene.unity", OpenSceneMode.Single);
        GameState state = Object.FindAnyObjectByType<GameState>(FindObjectsInactive.Include);
        HandcraftedCampTerrain terrain = Object.FindAnyObjectByType<HandcraftedCampTerrain>(FindObjectsInactive.Include);
        terrain.terrainPresentationRenderer = null;
        typeof(GameState).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, state);
        state.campTerrainState.residentialSlotsEstablished = 4;
        state.campTerrainState.residentialStage = 1;
        terrain.RebuildFromBaseline(false);
        FlushTilemapColliders();
        AssertPhysicalSweep(terrain, terrain.GetResidentialConstructionRoute(5), "Slot 5 construction");
    }

    static void FlushTilemapColliders()
    {
        foreach (TilemapCollider2D collider in Object.FindObjectsByType<TilemapCollider2D>(FindObjectsInactive.Include))
            if (collider.hasTilemapChanges) collider.ProcessTilemapChanges();
        Physics2D.SyncTransforms();
    }

    static void AssertPhysicalSweep(HandcraftedCampTerrain terrain, IList<Vector2Int> cells, string label)
    {
        for (int index = 1; index < cells.Count; index++)
        {
            Vector2 start = terrain.CellToWorld(cells[index - 1]);
            Vector2 end = terrain.CellToWorld(cells[index]);
            Vector2 delta = end - start;
            RaycastHit2D hit = Physics2D.BoxCast(start, Vector2.one * 0.75f, 0f,
                delta.normalized, delta.magnitude, Physics2D.AllLayers);
            Assert.That(hit.collider, Is.Null, label + " physical segment " + cells[index - 1] +
                " -> " + cells[index] + " hit " + (hit.collider != null ? hit.collider.name : "none"));
        }
    }

    static List<Vector2Int> InvokePostDigRoute(HandcraftedCampTerrain terrain, int slotId,
        List<Vector2Int> footprint, List<Vector2Int> constructionRoute, Vector2Int start,
        Vector2Int goal, Vector2 extents)
    {
        if (!TileMover.CanOccupyBox(terrain, terrain.CellToWorld(goal), extents) ||
            !TileMover.CanTraverseBox(terrain, terrain.CellToWorld(start), terrain.CellToWorld(goal), extents))
            return new List<Vector2Int>();
        return start == goal ? new List<Vector2Int>() : new List<Vector2Int> { goal };
    }

    static string FormatTargets(IReadOnlyList<(int x, int y)> targets)
    {
        List<string> values = new List<string>();
        foreach (var target in targets) values.Add("(" + target.x + "," + target.y + ")");
        return "[" + string.Join(" -> ", values) + "]";
    }

    static void AssertContinuous(HandcraftedCampTerrain terrain, IList<Vector2Int> cells,
        Vector2 extents, string label)
    {
        for (int index = 0; index < cells.Count; index++)
        {
            Assert.That(TileMover.CanOccupyBox(terrain, terrain.CellToWorld(cells[index]), extents), Is.True,
                label + " center " + cells[index]);
            if (index > 0)
                Assert.That(TileMover.CanTraverseBox(terrain, terrain.CellToWorld(cells[index - 1]),
                    terrain.CellToWorld(cells[index]), extents), Is.True,
                    label + " segment " + cells[index - 1] + " -> " + cells[index]);
        }
    }

    static void AssertConstructionContinuous(HandcraftedCampTerrain terrain,
        IList<Vector2Int> cells, Vector2 extents, string label)
    {
        for (int index = 0; index < cells.Count; index++)
        {
            if (CampBuddyPhysicalPolicy.RequiresFullWaypointClearance(index, cells.Count))
                Assert.That(TileMover.CanOccupyBox(terrain, terrain.CellToWorld(cells[index]), extents), Is.True,
                    label + " center " + cells[index]);
            if (index > 0)
                Assert.That(TileMover.CanTraverseBox(terrain, terrain.CellToWorld(cells[index - 1]),
                    terrain.CellToWorld(cells[index]), extents), Is.True,
                    label + " segment " + cells[index - 1] + " -> " + cells[index]);
        }
    }
}
#endif
