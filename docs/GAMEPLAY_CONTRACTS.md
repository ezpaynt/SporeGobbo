> **2026-11-03 local cutover VERIFIED:** D:/SporeGobbo/Project is the sole active development candidate; original Integration is frozen rollback and original live remains protected. V0-V6 completed: 43 synthetic checks, unchanged 412-case native regression, fresh Windows64 Development build and 40 ordered tools-OFF journey checks. All one-use execution permissions are consumed. No retry, publication, retirement, broader testing or Stage6.4D acceptance is authorized. Stage6.4D remains UNACCEPTED / UNINSTALLED.

# Gameplay contracts

**Approved documentation record, installed 2026-11-02.** Approved requirements below are restated from OWNER-D3 in DECISIONS, not inferred from code. They do not claim current acceptance. Changes require owner approval; tests implement guarantees, not redefine them.

## Approved intent

| ID | Required observable behavior | Scope / limits |
|---|---|---|
| SG-01 | Leader plus up to five Buddies, six total | Roster identities and active squad remain canonical; not a walking-order requirement |
| SG-02 | Friendly player–Buddy passage; bounded Buddy–Buddy cooperation | Ordinary friendly Camp/expedition context; prefer separation and distinct resting positions, no permanent stacking or repeated oscillation; Pit/session exceptions below |
| SG-03 | Solid terrain and meaningful hostile physical contact | Relationships/context, not species, govern hostility. A wild Gobbo may be hostile. No hostile phasing or terrain bypass |
| SG-03A | Pit and expeditions reuse real combat/contact services | Opposing participants retain effective physical contact; same-team/spectator rules and owned settings restore on resolution/cancel/removal/teardown. A suitable Pit demo never substitutes for world validation |
| SG-04 | Body-compatible reachable travel and useful group passage | Mixed sizes; no universal widening or automatic excavation. A larger body may need another/wider route. Individually valid routes do not prove simultaneous crowd feasibility |
| SG-04A | Calm, bounded obstruction handling and meaningful resumption | A sealed/oversized route or player occupying the only exit can legitimately block. Do not oscillate or pathfind constantly; react to meaningful changes without extending original duty deadlines or treating sidesteps as destination progress |
| SG-05 | Authorized normal digging creates a passage traversable by the digging Gobbo's current body | Cardinal, diagonal and turning continuous movement/digging; protected/resistant terrain and reach rules remain. Does not guarantee passage for larger Buddies or growth afterward; exact extra margin undecided |
| SG-06 | One-time authored Intro, persistent Camp, repeatable expeditions and persistent Gobbo identities/progression | Existing campaign, roster, inventory, history, residential and succession authorities remain; current example cave is not the final world |
| SG-07 | Within a playthrough, revisit modified persistent territory | Excavation/discovery and important world changes must have approved persistence semantics; exact return/death/quit/crash/checkpoint transactions are open, not currently implemented for the example expedition |
| SG-08 | Procedurally varied terrain and landmark arrangements between playthroughs | Authored references remain compatible with shared terrain; reusable stamps are a possible technique, not a required algorithm or finished generator |
| SG-09 | Equivalent creature infrastructure is reused | Shared bodies/terrain/navigation/attacks/damage/lifecycle; new behavior is allowed. No copied campaign, combat or save simulation |
| SG-10 | Loose spores, food and shinies do not physically block actors; collection is intentional | Walking over a resource is not collection; use the current interaction binding. Running Mushrooms are living physical actors, not loose pickups |

SG-01/02/03/03A/05/07/08/09 are explicitly reaffirmed by OWNER-D3. SG-04/04A/10 retain the owner's earlier movement/resource instructions identified in DECISIONS; this documentation revision does not change their collision tolerances or deadlines.

## Owner-provided direction with detailed contracts pending

- **Expedition loop:** physical return/backtracking and discoverable natural return holes matter. Approximately 10–15 minutes is a playtest aim including return, not an approved exhaustion duration, outward-only travel budget or shortcut spacing. Future starting points at return holes are undecided.
- **Belly / Camp Haul / Exhaustion:** distinct responsibilities. Eating fills personal Belly and earns growth XP; full Belly need not end exploration. Haul carries food home for Buddy development and can scale with companions. Exhaustion is an independent, initially nonlethal expedition limit with warnings and a less desirable collapse return. Exact progression/distribution/timer/penalty rules remain tuning/design questions, not current implemented economy.
- **Growth:** Draft 3 records visible run fullness/swelling and lasting Camp growth as direction. Do not silently reinterpret that as all personal XP being deferred to Camp, conflate Belly with Haul, or derive approved levels/collider timing from current scale formulas. How physical size changes are admitted, delayed or presented requires an approved detailed contract.
- **Recovery:** Draft 3 records automatic safe mid-run recovery checkpoints as direction, distinct from end-run rewards. Payload, interval, recovery location, crash/death semantics and transaction design remain open. No checkpoint implementation is authorized here.
- **Ecology:** visible creature interactions with compact offscreen population state; rates, migration, transformations, spawn caps and unloaded-time accounting remain open.
- **Capacity:** 1024² reference, 1200² intermediate, 1500² stretch/desired capacity; 60 FPS target and 30 FPS lower-end planning target. Hardware/workload, final map sizes and streaming boundaries are not decided or demonstrated.
- **Future encounters:** Princess-type Weevil event is intended. Queen/Giant phases, wild Gobbo conversation/recruitment, intra-Camp conflict and permanent Fight Pit consequences are not finalized.

## Boundaries not to infer

No approved final growth/XP formula, dig margin, faction reward economy, spawn/den regeneration thresholds, boss transformation, technical region topology, generation algorithm or final artwork follows from this guide. Existing development controls and temporary nonlethal Pit lab are not the finished gladiator feature. Preserve open choices; propose changes separately.

## Verification

TESTING maps contracts to independently falsifiable witnesses. A source implementation, a focused native pass and owner acceptance are different statuses. CURRENT_WORK identifies retained results and gaps; no test count completes an unimplemented future requirement.
