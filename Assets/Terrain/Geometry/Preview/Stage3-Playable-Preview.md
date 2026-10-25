# Stage 3: isolated playable cave

Open `Assets/Terrain/Geometry/Preview/Scenes/CaveGeometryPreview.unity`, or use **Tools â†’ Spore Gobbo â†’ Geometry â†’ Open Stage 3 Playable Preview**. Press Play. The scene is intentionally outside the production build flow. The original Stage 2 scene is `CaveGeometryStage2Reference.unity`; the Stage 2 menu still opens it. Outside Play, Stage 3 displays the baked reference geometry.

## Controls

- WASD: move. Mouse: aim. Hold left mouse or Space: repeat excavation at the configured interval. R: reset.
- The right panel selects 1, 3, or 9 cell square collision footprints, movement speed, single-cell/3-wide/circular-radius digging, patch size, and diagnostics.
- The yellow/orange square is the actual AABB body, centered on the small Gobbo marker. The smaller cell rings show exactly the next edit batch. Orange warns that the brush cannot guarantee enough clearance for the body. No collision bypass occurs.
- A 3-wide brush is a logical 3Ã—3 square. A circular brush selects cell centers within its radius of the target cell center. Its conservative clearance warning uses the square body's half diagonal. Grid alignment and oblique travel can require additional excavation even with an apparently sufficient brush.
- Normal digging targets a cell just beyond the body's aimed edge; holding the action while moving excavates naturally. Optional direct editing targets the mouse cell with unlimited reach and is explicitly a debug mode.
- Existing left panel: grid, contours, walls, lips, whole cave, tunnel and chamber views. Selecting a fixed view disables follow; enable follow again in the right panel. All views retain a uniform orthographic grid.
- Changing footprint finds the nearest valid open position; it never carves terrain to fit. An unavailable footprint is rejected. Changing patch size deliberately resets the fixture and reallocates the diagnostic pool. Ordinary dig/reset operations reuse the pool.

## Implementation

`PlayableTerrain` owns one mutable boolean occupancy array. The supplied immutable fixture supplies reset data. Collision, brush selection, contour classifications, and Gaussian wall orientation read this same mutable source. There are no terrain colliders or production terrain services.

`PreviewMotion` uses a square AABB, swept in steps no longer than 0.08 cells, then X/Y sliding when a combined step is blocked. Touching a cell boundary is permitted; positive overlap with solid cells is blocked. This is a preview collision method rather than a general physics engine. Visual wall faces and lips have no effect on collision.

`PreviewEditSession` removes all changed cells before invalidation. `ContourTerrainCache.InvalidateCells` invalidates the reference eight-dual-tile halo, and wall samples are invalidated in one cache scan per batch. A union of owning patch origins prevents duplicate rebuilds. Every patch reads neighboring occupancy globally. `PreviewMeshBuilder.BuildPatch` is extracted from the Stage 2 builder, with unchanged curves, fills, winding, wall height and lips. No topology filtering happens during movement or camera changes.

The runtime creates four mesh slots per dual-grid patch, including empty layers, and reuses those Mesh and GameObject instances through digging and reset. Static baked renderers are disabled during Play except the floor and optional grid; the baked objects/assets remain in the scene for edit-mode visibility. The runtime adds four diagnostic/edge meshes. Border occupancy is rebuilt as one small edge mesh per successful dig. No per-cell or per-segment objects exist.

Player, brush, boundary and dirty overlay buffers are reused between frames. Dirty terrain patch assembly still uses temporary collections. The Inspector/configuration belongs to this scene's one preview driver; there are no new global managers.

Ordinary updates rebuild local patches. The explicit full-rebuild comparison mode clears all derived caches and rebuilds all patches, using the same upload path. It is intentionally expensive. Dirty patch outlines persist for 0.65 seconds. The UI distinguishes build+upload timing from vertex/triangle counts. Managed allocation measurement uses `GC.GetAllocatedBytesForCurrentThread` and excludes native allocations and GPU work.

On the tested Unity Mono runtime this allocation API returns zero even while collections are allocated. The preview therefore reports allocation measurement as unavailable, and the benchmark serializes `-1` for unavailable KiB. This is not evidence of zero allocation. Use a standalone Development Player's Unity Profiler GC.Alloc recording for trustworthy allocation attribution.

## Files

Paths below are relative to `Assets/Terrain/Geometry/` (Unity creates matching `.meta` files).

Added:

- `Preview/Runtime/PlayableTerrain.cs`: mutable occupancy, AABB movement, logical brushes, incremental edit session.
- `Preview/Runtime/PlayableCavePreview.cs`: input, fixed runtime mesh pool, footprint/brush overlays and diagnostics.
- `Preview/Editor/PlayablePreviewTools.cs`: scene installation, menu, reproducible scripted benchmark and camera captures.
- `Preview/Tests/PlayablePreviewTests.cs`: collision, brush, parity, boundary, reset, determinism and pool tests.
- `Preview/Tests/PlayableLifecycleTests.cs`: actual Play Mode startup and virtual keyboard input through the runtime input tick.
- `Preview/Scenes/CaveGeometryStage2Reference.unity`: preserved Stage 2 scene.
- `Preview/Stage3-Playable-Preview.md`: this guide/report.

Modified:

- `Runtime/WallGeometry.cs` and `Runtime/ContourTerrainCache.cs`: batch invalidation, retaining the original single-cell API and reference geometry.
- `Preview/Runtime/PreviewMeshBuilder.cs`: extract the existing patch builder and allow uploading into an existing Mesh.
- `Preview/Runtime/CaveGeometryPreview.cs`: retained view buttons disable player follow; context-specific guide text.
- `Preview/Runtime/SporeGobbo.Geometry.Preview.asmdef` and `Preview/Tests/SporeGobbo.Geometry.Preview.EditModeTests.asmdef`: explicit Input System references.
- `Preview/Editor/GeometryPreviewSceneTools.cs`: Stage 2 menu/build target the preserved reference scene.
- `Preview/Scenes/CaveGeometryPreview.unity`: Stage 3 driver and supplied fixture/material references.

Protected production source/scene hashes were checked before and after implementation: SampleScene, CampScene, MapGenerator, GobboController and SporeSaveManager remained unchanged during this work. The repository already contains unrelated uncommitted changes; these were left in place.

## Scope and visual limits

No changes to production movement, MapGen, Camp, saves, progression, camera compression, or the persistent world are part of Stage 3. The existing input code depends on those systems, so this scene uses the Input System directly with the existing WASD/mouse conventions.

No visual redesign was applied: the reference 2.15-cell wall height and 0.22-cell lip rule remain. Diagonal scallops, lobed pillar edges, heavy-looking faces, and wall depth obscuring narrow logical passages remain visible. A one-cell passage can be collision-traversable while visually obscured by the intentionally deep wall. This must be resolved as a separate art/projection decision, without making collision depend on the drawing.

This small experiment still allocates mesh assembly lists/arrays per dirty patch. Cache invalidation scans cached wall sample keys once per edit batch; it is not a spatial index for a 1024Ã—1024 world. Neither allocation behavior nor whole-world cache storage is a production streaming solution. The next stage should profile a standalone player, reduce measured allocation hot spots, and decide on visual depth/readability before considering production integration.


## Validation results — 2026-10-23

The complete Stage 1–3 suite passed **84/84 tests** in 66.49 seconds: 65 retained Stage 1/2 cases and 19 Stage 3 cases. After the last overlay-buffer reuse/next-brush-preview cleanup, both affected focused tests passed again (2/2): actual Play Mode input/startup and mesh-pool/preview stability through 100 movement/overlay ticks, digging and reset.

New coverage includes 1/3/9-cell footprint clearance, swept movement against a thin wall, diagonal corner blocking, newly excavated cells becoming immediately traversable, exact single/three-wide/radius brush selection, clipped boundary brushes, undersized-brush warnings, unique dirty patches across boundaries, deterministic randomized excavation/reset parity with a fresh full build at 8/16 patch sizes, exact triangle parity across partition sizes, no normal evaluations during movement, stable Mesh identities/object counts, and Play Mode startup without a production MapGenerator. Retained Stage 2 checks cover mesh winding, bounds, all marching cases and the saved reference scene. No geometry rule was changed.

The input smoke test uses synthetic Input System devices with explicit input processing inside Play Mode, then calls the same `TickInput` method that `Update` calls. Batch Editor focus did not reliably deliver the virtual keyboard events through its own player loop; this is a deterministic controller test, not a claim of a manual human playthrough.

Artifacts in the workspace:

- `stage3-validated-tests.xml` / `.log`: full 84-test result.
- `stage3-overlay-tests.xml` / `.log`: final two focused checks.
- `stage3-captures/benchmark.json`: machine-readable measurements.
- `stage3-captures/digging-{8|16}-{1|3|9}-incremental.png`: actual Unity camera captures partway through the scripted excavation.
- `stage3-captures/digging-{8|16}-3-full.png`: equivalent full-rebuild captures.
- `stage3-captures/excavated-overview.png`: full cave after the 3-cell excavation, with patch boundaries.

### Performance measurements

Windows 10  (10.0.19045) 64bit; Intel(R) Core(TM) i7-6850K CPU @ 3.60GHz; Unity 6000.4.7f1 Editor synchronous CPU build + Mesh upload; 480 simulated 60 Hz movement/dig steps with diagnostic mesh upload per scenario; excludes IMGUI, presentation and GPU frame time; allocation KiB -1 means Mono allocation counter unavailable; totalStepMs excludes diagnostic capture

Each scenario advances 480 deterministic 1/60-second movement steps (eight simulated seconds) at 5 cells/second while holding dig. The 1-cell body uses a single-cell brush; the 3-cell body uses the 3×3 brush; the 9-cell body uses a radius of approximately 7.16 cells. Timings include synchronous geometry construction and Mesh upload; per-step timings also include reused overlay mesh updates. Capturing PNGs is excluded. These are Editor CPU samples, not presented-frame FPS or standalone-player guarantees. IMGUI, GPU presentation, native allocation and render-driver costs are not measured.

| Patch | Body | Mode | Successful digs | Build + upload avg / worst ms | Patches avg / max |
|---|---|---|---:|---:|---:|
| 8 | 1×1 | Incremental | 23 | 8.11 / 19.35 | 3.48 / 4 |
| 8 | 3×3 | Incremental | 23 | 9.90 / 23.13 | 3.48 / 4 |
| 8 | 9×9 | Incremental | 34 | 14.97 / 60.82 | 6.50 / 10 |
| 16 | 1×1 | Incremental | 23 | 7.19 / 12.26 | 2.26 / 4 |
| 16 | 3×3 | Incremental | 23 | 8.95 / 17.83 | 2.26 / 4 |
| 16 | 9×9 | Incremental | 34 | 9.70 / 14.57 | 2.38 / 4 |
| 8 | 3×3 | Full comparison | 23 | 125.13 / 173.43 | 96.00 / 96 |
| 16 | 3×3 | Full comparison | 23 | 140.85 / 216.19 | 24.00 / 24 |

| Patch | Body | Live mesh / object pool | Active nonempty scene meshes | Total scene objects | Live vertices / triangles | Step avg / worst ms |
|---|---|---:|---:|---:|---:|---:|
| 8 | 1×1 | 388 / 388 | 233 | 471 | 116,334 / 38,778 | 0.59 / 19.58 |
| 8 | 3×3 | 388 / 388 | 233 | 471 | 114,702 / 38,234 | 0.67 / 23.37 |
| 8 | 9×9 | 388 / 388 | 218 | 471 | 105,402 / 35,134 | 1.50 / 61.37 |
| 16 | 1×1 | 100 / 100 | 81 | 183 | 114,606 / 38,202 | 0.40 / 12.36 |
| 16 | 3×3 | 100 / 100 | 81 | 183 | 112,974 / 37,658 | 0.51 / 17.98 |
| 16 | 9×9 | 100 / 100 | 81 | 183 | 105,642 / 35,214 | 0.96 / 15.01 |

Pool counts include empty layer slots, the dynamic border and diagnostic meshes. Vertex/triangle totals include the live diagnostic overlays and vary with the dirty-highlight lifetime; the exact triangle parity test compares the four terrain layers separately. Total scene objects also include the disabled baked reference objects. Default Unity object searches omit the `DontSave` pool; the reported total explicitly adds it. Active mesh counts include the static floor and enabled nonempty live meshes.

Managed allocation bytes were **unavailable** on this Mono runtime and are reported as `-1`, rather than incorrectly claiming zero. Terrain rebuilds still allocate temporary mesh-assembly collections. The 8-cell/9-cell-body scenario had a 60.82 ms CPU spike. Even the 16-cell/3-cell-body run peaked at 17.83 ms before GPU/IMGUI cost. This is playable preview evidence, not a guarantee that every frame meets 60 FPS.

**Recommendation:** retain 16-cell patches for this fixture (100 live slots versus 388, with lower measured worst cases). Profile a standalone Development Player before deciding how much pooling/spatial indexing is necessary. Do not extrapolate these results to a 1024×1024 loaded world.

### Recommended Stage 4 decisions (not implemented)

1. Inspect the Play preview together and settle the wall-depth/readability treatment for one- and three-cell passages, using the preserved Stage 2 scene for comparison. Keep visual wall treatment separate from logical collision.
2. Profile a standalone Development Player, recording GC.Alloc and CPU/GPU frames under continuous digging. Pool measured terrain-builder hot spots and spatially index wall-cache invalidation if profiling supports it.
3. Agree on bounded chunk residency/streaming and dirty-update budgets before a larger world experiment; four GameObjects per patch across an entire 1024-square map is not the intended production architecture.
4. Keep nonlinear camera projection and production/persistence integration as separately authorized work.

Stage 3 stops here. No Stage 4 systems were added.
