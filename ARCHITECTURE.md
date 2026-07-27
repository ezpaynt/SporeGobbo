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
