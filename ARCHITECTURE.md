# Architecture

## CampScene Hierarchy

- **Systems** — Runtime managers, shared databases, `GameState`, initialization, interaction, and death/succession systems.
- **World** — Static boundaries, walls, covers, and dig targets.
- **Markers** — Spawn points, buddy positions, wander anchors, bed anchors, and station placement markers.
- **Stations** — Interactive Camp locations such as the CampFire, CampPortal, resource pile, beds, and bones wall.
- **Visuals** — Camp background, foreground cover, lighting, and visual rig.
- **UI** — Camp-specific panels, canvases, messages, and modal presentation.
- **Debug** — Camp-only debug tooling.
- **Canvas** — Retained separately because `CampInteractPrompt` has unresolved legacy prefab ancestry.
- **Main Camera** and **EventSystem** — Retained as conventional scene roots.

Gobbo visual data is canonical shared data used by playable, buddy, and camp gobbos.

## Run Profile Architecture

`RunProfile` is the complete configuration asset for a generated run. It inherits the legacy `BranchMapProfile` geometry schema and adds four owned sections:

- **Identity** — stable ID, display name, purpose, and development-only classification.
- **Environment** — dirt influence, distant dirt presentation, stone formations, root formations, and clearance policy.
- **Content** — encounter, resource, portal, and snack-spawn configuration consumed by `RunContentSpawner`.
- **Difficulty and development overrides** — explicit opt-in configuration; difficulty overrides remain unsupported and are rejected by validation.

`RunProfileCoordinator` is a code-level application service. It validates and applies the selected profile to `MapGenerator` and `RunContentSpawner`; it is not a scene component. Play Mode generation and **Generate Map Now** both enter through `MapGenerator.Generate`, which applies the selected profile before generation.

The retained canonical profiles are:

- **Normal Run Baseline** — current 120×120 production-scale composition baseline and the profile selected by `SampleScene`.
- **Tiny Full-Path Test** — 72×72 development-only smoke profile.
- **Compact Full-Feature Test** — 84×84 development-only feature-inspection profile.

The former `IntroLevelMapProfile` and compatibility-only `Current Prototype Baseline` were removed after `SampleScene` moved to the Normal profile.

## Procedural Cave Environment

`MapGenerator` currently owns the complete procedural cave pipeline:

1. topology, branches, rooms, camps, boss, exit, and filler areas;
2. deterministic dirt-influence sources and organic material-density tinting;
3. distant dirt variation and cold/dark edge transition;
4. irregular stone footprints;
5. trunk-first root formations, branches, knots, and controlled root/stone overlap;
6. clearance-aware formation erosion/rejection and critical-route validation;
7. tile painting, debug fingerprints, reports, and editor validation controls.

Stone and root cells are one blocked, undiggable terrain layer. When root and stone share a cell, root presentation wins while stone ownership remains recoverable. Current colors are placeholder visualization for future artwork; they are not final environmental art.

The authoritative traversal radius is derived from the base player/buddy sizes plus the navigation margin. Formations may be corrected or rejected rather than weakening the passage-width requirement.

## Run Pause and Status Menu

The run status UI extends the existing `PauseMenuController` and `PausePanel`; it does not introduce a second pause system. `PauseMenuStatusSnapshot` gathers read-only live data and `PauseMenuRunView` presents it. Camp continues to use its existing pause behavior. The current layout is functional but remains visually provisional, and Options remains a placeholder.
