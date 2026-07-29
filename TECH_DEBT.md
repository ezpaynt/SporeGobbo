# Technical Debt

- **CampInteractPrompt stale prefab-source references** — The CampScene prompt remains under its standalone Canvas and its legacy prefab ancestry is unresolved.
- **SampleScene stale LevelUp/UI prefab-source references** — Existing SampleScene prefab-source references require a separate investigation.
- **SampleScene unresolved tankGobboBack sprite** — The serialized sprite reference remains unresolved.
- **HealingWeevilMeat missing legacy FoodItem script** — This known unused legacy prefab retains its missing component.
- **TunnelWeevil unresolved root SpriteRenderer sprite** — The root sprite remains unresolved; runtime directional sprite assignment currently masks it.
- **Canvas.controller ownership unresolved** — The controller’s long-term feature ownership has not been established.
- **CampFire.prefab usage/ownership unclear** — Its relationship to the active CampScene presentation still needs a separate ownership review.
- **GobboUpgrade legacy status** — Whether this asset remains required has not been established.
- **EnemyHealth current usage uncertain** — Its current ownership and active usage require confirmation.

## Procedural Cave and Run Profile Debt

- **MapGenerator is oversized** — It now owns topology, influence, formations, clearance, tile presentation, diagnostics, and custom editor tooling. This is safe for the current checkpoint but should eventually split along the boundaries documented in `PROJECT_CONTEXT.md`.
- **Normal profile uses development overrides** — `Normal Run Baseline` is classified as production but currently enables the fallback/minimum layer to guarantee composition and influence coverage. Those constraints should eventually become explicit production rules or the overrides should be disabled before a release build.
- **Runtime content placement is not fully seed-isolated** — Geometry, dirt influence, stones, roots, and clearance use deterministic local inputs. `RunContentSpawner` still uses `UnityEngine.Random` for some runtime positions and counts, so a map seed alone is not a complete content-placement replay key.
- **Legacy BranchMapProfile compatibility is type-level only** — `RunProfile` inherits the old geometry asset, but `RunProfileCoordinator` intentionally rejects an incomplete legacy profile. Any remaining legacy profile must be migrated before use.
- **Generation and presentation remain concentrated** — Placeholder tint/material logic is interleaved with generator code. Final watercolor art should be introduced through a dedicated presentation boundary rather than expanding `MapGenerator` further.
- **Formation acceptance can fall below requested root attempts** — Clearance correction prioritizes safe traversal, so unsafe roots may be rejected. Normal currently averages near four accepted roots but can accept fewer on individual seeds.
- **Editor diagnostics live beside production generation code** — The fixed-seed validator and inspector controls are editor-gated, but moving them to dedicated Editor tooling would reduce file size and review cost.
- **Visual and interaction approval remains incomplete** — Automated fixed-seed, clearance, scene, prefab, and reference validation passes, but final root silhouettes, dirt transitions, player-camera composition, pause-menu interaction, and final environmental art still require direct Play Mode review.
- **Content spawning precedes formation construction** — Current planned-area reservations protect generated content regions, but the ordering creates coupling between structure data, runtime spawn behavior, and later formation placement.
