# Spore Gobbo V1 foundation candidate

This separate repository is the normal V1 integration candidate. It is not the official production cutover. The preserved archive branch and historical root ARCHITECTURE.md describe older production systems; use this entry point for the current scene loop.

The build begins in Assets/Scenes/MainMenu.unity, then uses Assets/Intro/FirstGobboIntro.unity, Assets/Stage61/FutureCampCandidate.unity and Assets/Expedition/FirstExpedition.unity. GameSceneCatalog is the destination resolver. The Stage61 folder name is retained for source/GUID compatibility and does not make the canonical Camp a preview storage profile.

GameState owns campaign facts, roster, resources, progression, relationships and history. Existing run-return and Camp lifecycle services coordinate arrival, reward summaries and succession. Each current region owns one logical RegionTerrainState; collision, navigation and projected rendering derive from that authority. CampRegionSnapshot holds bounded physical edits/bed geometry and references canonical Gobbo identities rather than owning a second roster.

SporeSaveManager and V1CampaignSnapshot use the existing validated atomic CampaignSnapshotStorage. Fresh normal V1 and development namespaces are distinct; legacy campaigns are preserved without implicit migration. Protected players, tests and development profiles require an owned storage root. Runtime correctness does not depend on enabling the F9 tools.

The current projected camera and 0.6 cell scale are preserved. ReferenceCameraSettings supplies the accepted composition recipe; geometry, actor feet and inverse interaction use the shared contract. Historical Stage45/Stage59 helpers that remain active are shared implementation dependencies, not permission to run old preview orchestration. Do not delete entire stage folders.

F9 is the small development hub. Data/Journal remain read-only; God Mode defaults OFF; Camp synthetic-return controls use the existing fake builder and real return transaction. These are development-only capabilities, not campaign authorities. Controlled standalone launchers, approved safety tooling, exact recovery archives and detailed reports are delivered separately under the Stage6.3B work directory. Do not run broad legacy tests or open a disposable evidence project as your development project.

Known scope limits include unfinished growth-choice presentation, arbitrary height-aware foreground occlusion, final art/audio, procedural generation and the historical ComputeBuffer shutdown warning. Current acceptance and measured limitations are documented in the Stage6.3B reports; source compilation alone is not acceptance. No push, main merge, legacy deletion or native save migration is part of this candidate.


## Current engineering documentation — installed 2026-11-02

Start candidate engineering work with [AGENTS](AGENTS.md) and [docs/README](docs/README.md); [docs/CURRENT_WORK](docs/CURRENT_WORK.md) identifies source/authorization. The preceding overview remains compatibility/context guidance; root ARCHITECTURE describes legacy systems. Documentation approval does not accept/install Stage6.4D gameplay, authorize tests, migrate saves or publish Git changes.
