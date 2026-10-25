# Stage 2 — isolated uniform cave preview

Open `Assets/Terrain/Geometry/Preview/Scenes/CaveGeometryPreview.unity`, or use **Tools > Spore Gobbo > Geometry > Open Stage 2 Preview**. It is not added to Build Settings and does not replace CampScene or SampleScene.

The baked meshes display immediately in Edit Mode. Open Unity's **Game** tab to see the authored overview camera without pressing Play. Select **Cave Geometry Preview — Stage 2 only** in the hierarchy for independent grid, contour, wall-face and foreground-lip checkboxes. Enter Play Mode for on-screen toggles and three camera framing buttons. There are no digging interactions or gameplay actors.

## Geometry contract

- The copied immutable 96x62 occupancy text is byte-equivalent (after line-ending normalization) to the supplied Stage 1 reference fixture.
- Stage 1 `ContourTerrainCache`, `ContourGeometry` and `WallGeometry.Project` are the only contour/normal/depth source. Projection is uniform, one render unit per logical cell, with Y reflected for Unity. Gameplay scale is untouched.
- Opaque flat colors, back-face culling and no textures, lighting tricks or detached shadow pieces. The static reference maximum back-wall depth is 2.15 cells; foreground lip is .22 cells weighted by the reference orientation filter.
- Rounded solid fills are constructed from the SAME sampled contour edges plus solid perimeter edges, then ear-clipped. This removes the square-fill versus rounded-outline mismatch without changing the Stage 1 curves.
- Geometry is batched into 16x16 dual-tile patches per layer. No GameObject per cell or contour segment. Meshes are baked assets, not rebuilt each frame. Display toggles only enable/disable renderers.
- Triangles are checked again after double-to-float conversion to omit collapsed slivers and enforce camera-facing winding. Mesh bounds receive a .0001-unit outward size margin for float center/extents roundoff; vertices and reference contour positions are unchanged.
- The Gobbo is an orange placeholder with approximately one-cell body diameter and projecting ears, at logical (50,39). It has no movement, collision, AI or saves.
- World edges use half-cell fill strips and terminate at the fixture crop; they are not a proposed streamed-chunk boundary policy.

## Reference limitations to inspect honestly

The fixed filter still produces a visible repeated contour cadence and may cause depth variation on small formations. A 2.15-cell wall occludes a substantial fraction of a three-cell passage; no height reduction is applied to hide this. Faces/lips use the original reference projection offsets, not a redesigned surface. The preview uses layer order for a single static Gobbo, not a finished actor-occlusion system. No nonlinear camera projection, streaming, local excavation, UV/art pipeline, or physics integration is implemented.

## Regeneration and validation

**Tools > Spore Gobbo > Geometry > Rebuild Stage 2 Preview Assets** deterministically replaces only owned preview meshes/materials and the preview scene. It never edits production scenes or terrain. Save other work before invoking it.

`GeometryPreviewSceneTools.BuildSceneAndCapture` is a batch entry point; pass `-geometryPreviewOutput <directory>` to render an overview, tunnel, Gobbo/pillar, outline-disabled joins and grid detail. Captures need a real graphics device (omit `-nographics`).

Run Unity Edit Mode assemblies `SporeGobbo.Geometry.EditModeTests` and `SporeGobbo.Geometry.Preview.EditModeTests`. Tests cover all 16 rounded fill areas, Python-reference Stage 1 parity, deterministic output, finite bounds, triangle winding, reference wall depth and saved-scene isolation/toggles.
