# Architecture Vision

## Current Situation

The project was built iteratively.

Many systems work correctly but are more tightly coupled than desired.

The goal is NOT to rewrite everything.

The goal is to gradually organize the project while preserving working gameplay.

Whenever possible:

* Keep working systems working.
* Move responsibilities into clearer locations.
* Reduce hidden dependencies.
* Avoid giant "god classes."

---

# Desired Architecture Philosophy

A system should have one clear responsibility.

Examples:

Good:

CampSceneController

* Camp flow.

BuddyRosterService

* Buddy storage and roster logic.

RunSummaryService

* Run result processing.

CampFireRecovery

* Healing calculations.

Bad:

GameState

* Camp flow.
* Save logic.
* UI logic.
* Buddy logic.
* Map logic.
* Run logic.

GameState should gradually become a data container and coordinator, not the owner of every gameplay feature.

---

# Refactor Strategy

Refactors should be evolutionary.

Not revolutionary.

Preferred:

1. Create replacement system.
2. Move functionality.
3. Verify behavior.
4. Remove old code.

Avoid:

1. Delete old system.
2. Rewrite everything.
3. Hope it works.

---

# Camp Architecture Goals

Camp systems should become independent modules.

Examples:

* Fire Pit
* Squad Select
* Bone Wall
* Beds
* Goop Pile
* Exit Tunnel

Each should own its own behavior.

CampSceneController should coordinate them rather than contain their logic.

Desired future:

CampSceneController
-> FirePitController
-> SquadSelectController
-> BoneWallController
-> CampRosterManager
-> CampUnlockManager

---

# Buddy System Architecture Goals

Buddy data is one of the most important systems in the game.

Buddy information should eventually exist independently of scenes.

A buddy should survive:

* Scene changes
* Camp visits
* Save/load cycles
* Leader death

Avoid storing important buddy information inside temporary scene objects.

BuddyData should be treated as long-term persistent data.

Scene objects should be views of BuddyData.

---

# Save System Goals

The save system is considered critical infrastructure.

Changes should be conservative.

Avoid:

* Renaming serialized fields.
* Changing save formats unnecessarily.
* Breaking existing save slots.

Whenever possible:

* Migrate data.
* Preserve compatibility.
* Add fields rather than replacing fields.

---

# Map Generation Vision

Current map generation is transitioning from a tile-filling mindset to a structure-first mindset.

The generator should think in:

* Branches
* Camps
* Rooms
* Connections
* Landmarks

Not:

* Width
* Height
* Rectangle counts

The player experience should feel like discovering a cave network.

The generator should first create structure.

Visual tiles should be applied afterward.

---

# Map Generator Separation Goals

Desired responsibilities:

Map Profile

* Defines generation rules.

Branch Generator

* Creates structure.

Room Generator

* Creates spaces.

Connection Generator

* Creates links.

Spawn Generator

* Places content.

Tile Painter

* Paints visuals.

The long-term goal is to separate these concerns.

Avoid one giant script doing everything.

---

# Camp Unlock Philosophy

New camp features should feel earned.

Examples:

First buddy death
-> Bone Wall appears.

Population growth
-> New bed areas unlock.

Future systems should be hidden until unlocked.

Camp should physically evolve over time.

---

# UI Philosophy

UI should be data-driven.

Whenever possible:

UI displays data.

UI should not own game logic.

Examples:

Good:

RunSummaryUI
shows run results.

RunSummaryService
calculates run results.

Bad:

RunSummaryUI
calculates rewards.

---

# Technical Debt Priorities

Current areas worth gradually improving:

1. Camp scene organization.
2. Map generation organization.
3. Save/load separation.
4. Buddy data ownership.
5. Interaction system consistency.
6. Removal of obsolete map generation remnants.

These improvements should happen while maintaining playable builds.

---

# What Success Looks Like

Future development should feel like:

"I know exactly where this feature belongs."

Examples:

New camp building?
Camp system.

New buddy trait?
Buddy system.

New room type?
Map generation.

New save field?
Save system.

The project should become easier to understand over time, not harder.

Every refactor should reduce confusion.
