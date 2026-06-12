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






# Progression Vision

## Core Philosophy

XP is earned during runs.

Growth decisions happen in camp.

The run generates progress.

The camp determines what that progress becomes.

The goal is for camp to feel like the place where the player shapes the tribe.

---

## Player Progression

The player gobbo is the leader.

Player progression is primarily run-focused.

Player XP is earned during the run.

Level-ups happen immediately.

Each level-up presents choices.

Examples:

* Stat cards
* Ability cards
* Class cards
* Future special cards

The player should feel like they are continuously growing during a run.

---

## Buddy Progression

Buddy progression should be camp-focused.

Buddies earn XP from:

* Active squad participation
* Passive camp XP
* Future spore feeding
* Future events

Buddy growth decisions should not interrupt normal gameplay during runs.

Instead, growth decisions accumulate and are handled in camp.

---

## Buddy Growth Levels

Not every buddy level should be treated the same.

Normal levels:

* Generate stat card choices.

Milestone levels:

* Generate evolution or growth choices.

Current milestone targets:

* Level 2
* Level 6
* Level 12
* Level 24
* Level 48

Milestones represent major life stages.

Normal levels represent gradual development.

---

## Buddy Growth Queue

A buddy can become ready for growth.

Growth should remain pending until the player addresses it.

Examples:

* Pending stat card choice
* Pending evolution choice
* Future pending trait choice
* Future pending mutation choice

Camp systems should be able to ask:

"Which buddies are ready to grow?"

without needing to know the specific growth type.

---

## Ignoring Growth

Ignoring growth is a valid player choice.

Buddies should not require immediate attention.

However, growth may be delayed for too long.

Potential consequences:

* Reduced happiness
* Personality changes
* Strange traits
* Neglected elder outcomes

The goal is to create stories, not punishments.

Neglect should make buddies weird rather than simply weaker.

---

## Evolution Philosophy

Evolution choices are special.

The first major evolution determines a buddy's class.

Examples:

* Tank
* Fast
* Fungal
* Scavenger
* Strong
* Explosive
* Thrower
* Fat

Later milestones should eventually offer:

* Class specialization
* Mutations
* Traits
* Elder paths
* Unique growth branches

Evolutions should feel memorable.

---

## Buddy Stat Cards

Normal buddy levels should eventually provide card choices.

The player chooses one option from several random choices.

Examples:

* Max Health
* Attack
* Defense
* Move Speed
* Attack Speed
* Crit Chance

The initial implementation should use a small simple card pool.

Complex class-specific cards can be added later.

---

## Rerolls

Buddy growth choices may be rerolled.

Rerolls should cost shinies.

The same resource economy should support:

* Buddy card rerolls
* Future player rerolls
* Future camp spending

---

## Spores

Spores are more than a resource.

Spores represent future tribe members.

The player should have meaningful choices regarding spores.

Potential actions:

* Hatch
* Store
* Feed
* Future special uses

Spores should not become mandatory buddies.

---

## Spore Mound Vision

The Spore Mound is the long-term home for stored spores.

Possible future actions:

* Hatch stored spores
* Feed spores to buddies for XP
* Track spore age
* Trigger future events

The Spore Mound should become a decision point rather than simple storage.

---

## Stored Spore Philosophy

Stored spores are safe.

Run-hatched spores are risky.

Run-hatched spores gain XP immediately but can die.

Stored spores remain flexible but gain no immediate benefit.

Both choices should be valid.

---

## Spoiled Spores

Spoiled spores should be strange rather than purely negative.

Possible outcomes:

* Unique traits
* Mutations
* Special evolution paths
* Rare buddy variants

The goal is curiosity rather than punishment.

Players should occasionally choose to keep spores longer to see what happens.

---

## Camp Philosophy

Camp should provide decisions.

Camp should not exist only for storage.

Examples:

Runs generate:
* XP
* Resources
* Buddies

Camp determines:
* Growth
* Squad composition
* Resource spending
* Evolution paths
* Future tribe development

The player should look forward to returning home.

---

## Long-Term Goal

The gameplay loop should feel like:

Run
→ Return Home
→ Make Decisions
→ Grow Tribe
→ Begin Next Run

The tribe should become more interesting over time.

The camp should gradually transform from a small shelter into a living gobbo settlement filled with history, relationships, growth, weirdness, and player-driven choices.
