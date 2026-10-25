# Stage 1: scene-independent cave geometry

This assembly has no UnityEngine references, GameObjects, assets, renderers, collision, navigation, save logic, or terrain authority. `IOccupancySource` is a read-only adapter contract. The caller owns terrain and must invalidate derived caches after edits. A full terrain replacement requires `Clear()` or a fresh cache. No camera-relative projection is implemented.

## Contracts

- Logical kit coordinates: X right, Y down; integer edges, occupancy centers at `(x+.5,y+.5)`. A dual patch `(x,y)` spans four neighboring occupancy centers. `LogicalGridTransform` converts to the existing Y-up world using caller-provided origin, row count and cell size; it never changes gameplay scale.
- Bits: TL=1, TR=2, BR=4, BL=8. Connect-solid is the default, preserving separation of diagonally touching open samples. Actual actor footprints remain a gameplay responsibility. Contour art is not collision geometry.
- Curves: radius .5, kappa .5522847498307936, 10 subdivisions. Exact endpoints bypass polynomial evaluation. All six canonical sample arrays are built once and exposed read-only.
- Complement changes the saddle policy. Cases 5 and 10 with the same connect-solid policy are not identical geometric complements. Rotation/reflection preserve either consistent policy.
- `WallOrientationCache` retains the reference 5x5 Gaussian finite-difference normal calculation, including its near-zero fallback, while precomputing weights and reusing exact shared-point results. Wall samples hold logical positions, normals and orientation weights independent of any projection.
- `WallGeometry.Project` derives attached wall top/bottom/lip vertices from cached samples. Default reference depth is 2.15 cells, lip width .22. Height can change independently of terrain. Adjacent samples define strip quads; no mesh allocation, winding or triangulation policy is introduced here.
- Incremental invalidation follows the reference half-open 8x8 wall-patch halo; ordinary contour classification depends on four dual patches. The cache is bounded by its caller's use and is not a streaming/world manager. Invalidation currently scans cached orientation keys; do not retain an entire 1024-square world's wall cache in one instance. Future streaming should own bounded caches with global neighbor sampling.
- True-world-edge sampling clamps like the reference. Future internal chunk boundaries must access global neighbors, never clamp at their own edge. Sampling/patch ownership for independently streamed chunks belongs to Stage 2 or later.

## Reference versus normalized projection

`Tests/Reference~` preserves the original Python source, version-2 exported specification, complete 96x62 occupancy fixture and original projection arrays. The supplied kit archive was named V1; its exported schema is version 2. Numeric golden CSVs are generated directly from that Python source. No workbook pixel conversion is performed at runtime.

The exported central scale is 11.25 by 10 (aspect 1.125). `StaticGridProjection.Normalize` divides each axis by its explicitly supplied central dimension, then applies a shared square scale (default 1). This intentionally removes the 12.5% central horizontal stretch while retaining all per-axis compression/foreground ratios. In normalized coordinates the output equals `(referenceX/11.25,referenceY/10)`; restoring those axis multipliers reverses the normalization exactly up to floating-point rounding. This is the sole intentional visual normalization.

Static original projection and original row-scaled depth remain available. Reference piecewise-constant local scale has jumps when crossing row boundaries (e.g. full back height 21.5 to 23.65 reference units at y=52). No smoothing or moving-camera substitute is introduced. Curves/filter/depth rules otherwise retain numerical parity, tested at 1e-15 for curve samples and 1e-8 display units for reference normal/wall fixtures. Bit-identical Python/C# arithmetic is not promised.

## Tests

Unity Edit Mode assembly `SporeGobbo.Geometry.EditModeTests` exercises all cases and saddle policies, rotation/reflection/complement, shared anchors/tangents, world/logical conversions, original/normalized projection, Python curve/normal/wall goldens, complete-fixture shared endpoints, seeded incremental add/remove edits including true-world corners, cache reuse and finite uniform-field behavior.

`Tests/Reference~/build_reference.py <extracted-kit-directory>` regenerates frozen goldens from the supplied kit. Original `locked_geometry.py` SHA-256: `bd5455262ba7878b62000a2062c93ac263308f64348c2c0b087467d71ed5cb96`. The reference directory intentionally ends in `~` so Python files do not become Unity assets; tests read it directly. Reference inputs are not changed by tests.

## Deliberately deferred

Mesh fills, vertex welding/UVs, renderer and scenes, chunk streaming, camera following/inverse picking, actor sorting, adaptive tessellation, physics and MapGen integration. Before Stage 2, retain original static output as the parity mode and decide whether a moving-camera wall-height rule should remove the documented row-scale jumps. The near-zero orientation fallback and decorative contour/collision mismatch remain reference behavior to inspect visually, not silently redesign.
