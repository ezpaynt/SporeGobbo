# Testing, evidence and controlled execution

**Approved documentation, installed 2026-11-02; not execution permission.** Each run needs applicable task authorization, current source/scope review, owned storage and clean protections. Documentation installation permits no Unity/build/gameplay tests or acceptance changes. Commands describe inspected tooling; see CURRENT_WORK for the source-pin mismatch.

## Required safety reading and paths

Tooling workspace: `C:/Users/ezpay/Documents/SporeGobbo`. Live Unity project: `C:/Users/ezpay/SporeGobbo`; never substitute it as a test project. Active candidate and protected references are identified in CURRENT_WORK.

Read these originals, including scope updates and limits:

- [safety README](/C:/Users/ezpay/Documents/SporeGobbo/safety_checkpoint/README.md)
- [incident/findings](/C:/Users/ezpay/Documents/SporeGobbo/safety_checkpoint/findings.md)
- [Stage6.4D runner](/C:/Users/ezpay/Documents/SporeGobbo/stage64d_work/run.py), its source-review and selected inventory/parser.

The historical runner enumerates old suites and later milestone extensions; old baseline checks are not current-candidate approval. Never launch legacy installers/captures/unfiltered namespaces/raw Unity commands because a file exists. A new suite/output/lifecycle is reviewed before adding it.

Preflight must verify exact source and allowlist, metadata/packages/settings, full indirect persistence/scene effects, owned absolute non-reparse paths, disk guard, unique storage token/marker and Unity company/product/productGUID/cloud isolation before Unity starts. Source, real saves and native preference protection are separate. The runner is not an OS sandbox. Real saves are never fixtures. Preserve drift rather than reset/clean/revert. Failure/timeout/incomplete postflight stops the scope.

### Preserved operational details

- Preserve fresh productGUID, cleared cloned/cloud/organization identity and disabled UnityConnect/analytics startup in disposable copies; names alone were insufficient. GUI/test-player detection/isolation requires the reviewed fail-closed path.
- No historical binary/direct EXE/original launcher workaround. Do not export test storage into normal play or create ownership markers in real storage.
- Do not edit Integration or pinned workflow/acceptance files during a controlled run using them. Source/metadata serialization drift is failure, not automatically generated output. Preserve original preference/domain-reload/native-isolation limits.
- Historical milestone commands/installers/roots/stop points retain scope; later authorization does not rewrite baselines or broaden suites. Keep safety/capture-driver/recovery references separate; consolidate discovery, not evidence.

## Existing execution map — conditional reference

Existing Stage6.4D command forms from the tooling workspace, conditional on a newly authorized matching gate:

```powershell
& 'C:/Users/ezpay/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' -B 'C:/Users/ezpay/Documents/SporeGobbo/stage64d_work/run.py' --mode tests --scope collision-contract
& 'C:/Users/ezpay/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' -B 'C:/Users/ezpay/Documents/SporeGobbo/stage64d_work/run.py' --mode tests --scope regression
```

| Existing selector | Inspected behavior / limitation |
|---|---|
| `tests / collision-contract` | Stage64DNavigationTests + Stage64DValidationContractTests; isolated development profile. Reviewed 100-case inventory; exact historical-category exclusion |
| `tests / regression` | Fixed retained regression selector plus Stage64DNavigationTests; 412-case inventory, normal profile; same reviewed historical exclusion |
| `tests / navigation` | Existing Stage64DNavigationTests selector. Not interchangeable with strict 100/412 completeness scopes; historical ignores can prevent strict aggregate success. Not a recommended acceptance shortcut |
| `build / regression` | Existing build path requires the regression gate first; no build authorized here. No player/capture mode is implied by this runner |

Runner owns `D:/SporeGobboStage64D`, a checked disposable project and unique run/storage roots. It verifies ownership and current source/workflow/acceptance hashes, isolated identity and protection. Existing free-space guards are 4.5 GiB for fresh / 1.25 GiB for reuse; these are pinned implementation facts, not permission to lower them or delete old builds. Unity test host timeout in this runner is 1800 seconds; fixture/native replay deadlines can be much shorter and remain unchanged.

[Source review](/C:/Users/ezpay/Documents/SporeGobbo/stage64d_work/source-review.json), [strict parser](/C:/Users/ezpay/Documents/SporeGobbo/stage64d_work/acceptance_results.py), [100 inventory](/C:/Users/ezpay/Documents/SporeGobbo/stage64d_work/collision-contract-inventory.json), [412 inventory](/C:/Users/ezpay/Documents/SporeGobbo/stage64d_work/regression-inventory.json). Do not auto-regenerate expected cases from a passing result. Refreshing pins/inventories is an approval/review step, not a workaround for failure.

Focused later diagnostics have their own pinned overlays, inventory, roots and shorter deadlines. The retained CrowdingEvidenceCompletion execute_package/run_focused files are historical inspected execution evidence, not a general ongoing launcher. Re-execution would overwrite package result files: authorize a new owned evidence location and exact reviewed scope instead of rerunning them in place.

## Hierarchy and test ownership

1. Pure/component: deterministic input, query math, transaction/policy and serializer rules.
2. Native component: actual prefabs/body shapes, production terrain/clearance, native motor/solver/contact and restoration. Script simulation is real physics but not the full automatic input/FixedUpdate/player loop.
3. Feature integration: actual actors/AI/input/damage/commands/recruitment/Pit and cleanup.
4. Few complete journeys: transitions, run accounting, Continue/succession and resource teardown.
5. Matched performance/lifetime: representative standalone builds, hardware/workload/frame distributions and retained resources, separate from heavy observers.
6. Historical/disposable diagnostics: immutable evidence, explicitly classified; not silently counted as acceptance.

Test owner is the component/contract owner plus its reviewed acceptance scope. Existing tests span Assets/Editor/Tests, Assets/Tests/EditMode and geometry/camp assemblies; no test migration authorized. A new feature normally adds focused positive/negative coverage plus one relevant integration, not another full journey for every local invariant. Keep representative end-to-end checks when splitting expensive witnesses later.

## Contract-to-test ledger

Each row references exact names where available; parameterized case lists/multiplicity are authoritative in the pinned JSON inventories, not manually duplicated here. Source identity/evidence freshness is CURRENT_WORK. A row is not complete merely because a test is named.

| Contract | Owner / exact tests or evidence | Positive/negative witness and remaining gap |
|---|---|---|
| SG-01 identity/squad | GameState, roster/spawners; five-Buddy native replays and squad/report regressions in 412 inventory | Canonical IDs, count and reports; cap six intent reaffirmed. Latest combined candidate still needs repeated journey acceptance |
| SG-02/03/03A | Coordinator/contact lease + real combat: `Stage64DValidationContractTests.RealPlayerBuddyEnemyAndOpposingPitContacts` | Real input, hostile physical blocking, opposing Pit contact/filter restoration. Component math alone is insufficient; retain old raw observations separately |
| SG-04 travel | `Stage64DCooperativeCampArrivalTests.MixedSizeArrivalOrdersAndOversizedGoalRejection`; retained five-Buddy native replays | Mixed sizes/orders, convergence, oversized rejection; latest broader repeated/performance gate pending |
| SG-04A waiting | `Stage64DNavigationTests.CompletedYieldWaitsCalmlyForStationaryPlayerAndResumesWhenTheyMove`; `CalmWaitInvalidatesOnMeaningfulDutyOrTerrainChange` parameterized cases | Stationary block and genuine resumption, deadline/task ownership; sideways yield is not goal progress |
| Shared waypoint/route | `Stage64DNavigationTests.CapturedBuddy4CachedSuffixReconnectsAfterYieldUsingNativeClearance`; `SharedExecutableContractRoutesCapturedCampCornerUnderNativePhysics` | Actual captured geometry and strict physical traversal, not nominal proximity alone; inspect consumer-specific paths too |
| SG-05 digging | `Stage64DNavigationTests.CurrentBodyExcavatesCardinalDiagonalAndTurningAtSubcellPhases` (five radii); `ExactNativeDiagonalCornerLockUsesCurrentBodySweep`; `DigCorridorDoesNotGuaranteeLargerBuddyOrLaterGrowthFits` | Positive current-body passage, negative larger-body/growth promise; require real-input excavation journey separately. Footprint wall tests alone do NOT prove digging |
| Physical footprint safety | Overlay `Stage64DFootprintContractTests.NativePeerPushAndTerrainBlockingUseActualBodies` | Actual prefab/derived terrain/native wall contacts + bounded travel/no wall crossing; real peer push outside reserve followed by safe escape |
| Voluntary rejection | Overlay `Stage64DFootprintContractTests.RealOffsetBodyKeepsReserveAndEscapesWithoutTeleport` | Full/X/Y rejection commands zero and native zero displacement; valid connector escape without teleport. Independent of wall witness |
| Spawn/SG-10 | `Stage64DSpawnBindingTests.VisualOnlySporeDoesNotObstructOrMoveValidActorSpawn`, `MixedViewsRetainPhysicalActorSpawnSeparation`, `SolidTerrainStillRejectsSpawnWithVisualOnlyEntries` | Visual exclusion + physical actor separation + solid-terrain rejection. Intentional pickup interaction needs real-service coverage, not inferred from bind tests |
| Hostile bite | `Stage64DWeevilBiteTests.NativeDirectionalBitesAndUnchangedCooldown`; `SharedContactUsesRealBodiesNotSensorsOrDetachedChildren` | Native approaches, actual damage/cooldown and collider ownership. HistoricalOriginMismatchAndForcedWindupDiagnostic is explicit diagnostic coverage, not proof of natural attack alone |
| Input/commands | Global `BuddyCommandWheelTests`; semantic input/context tests; real-service validation above | Pure wheel open/confirm/cancel/mode checks plus Dynamic input delivery, modal cancellation and actual command effects |
| SG-06 persistence | Stage61PersistenceTests, Stage62B/62C and Stage63B integration in 412 inventory; PersistenceSafetyTests.IndirectDeathAutosaveWritesOnlySyntheticSlot | Camp edits/identity/save fault/recovery and succession; strict Continue/return/native cleanup still require their actual journey. Do not infer from DTO tests |
| Crowd/return/cleanup | `Stage64DCooperativeCombatCrowdingTests.RealSharedEnemyCombatRemovalReturnAndThreeWeevilCorner` | Required actual combat/pursuit/return/removal/second departure/Menu cleanup. Latest case failed before final assertions; no passing subset signs off package |
| SG-07 persistent world | Existing save primitives and Camp snapshots only | No current persistent example-expedition guarantee. Future region capture/reload/crash tests require approved semantics |
| SG-08 generation | Current authored layouts/landmarks and pure geometry regressions | No final variation generator acceptance; future seed variation/connectivity/landmark/persistence properties needed |
| SG-09 extension | Shared attack/clearance/binding tests above plus feature-specific behavior | New type eligibility/targeting/context tests are still needed; no universal 'all future creatures work' test |
| Performance/lifetime | Retained Stage63B/64B matched reports; future matched Stage64D run | Previous small-workload results only. 1024²/1200²/1500² and actor dormancy not certified |

The full `BuddyCommandWheelTests` declaration is global in the inspected source despite its Input test assembly. An assembly name is not a test namespace. Use full names from inventory/source, not inferred folder/assembly names.

## Historical classification and negative outcomes

Exactly two reviewed methods carry `Stage64DHistoricalDiagnostic` and retain Ignore reasons/assertions:

- `Stage64DNavigationTests.CapturedCampBuddy2EntryAndNativeCornerContactReproduction`
- `Stage64DNavigationTests.NativeCreatureAndPitBodyContactsRemainFirm`

Only the reviewed collision-contract/regression scopes apply the supported category filter `!^Stage64DHistoricalDiagnostic$`. Do not broadly exclude diagnostics or alter discovery. The historical offset-reserve pose diagnostic remains explicitly preserved; failure of the old unsupported post-physics reserve assertion is not retrospectively a pass.

## Acceptance completeness and independent evidence

Success requires reviewed source/overlay/scope/profile/inventory, exit code zero, genuinely Passed aggregate and selected suites/cases, exact identities/multiplicity, Runnable state and complete execution timestamps. Reject missing/duplicated/skipped/ignored/explicit/inconclusive/failed/empty/incomplete selections and inconsistent counts. The existing parser checks these for the two pinned scopes. Synthetic parser tests validate reporting, not gameplay.

Every required witness in a package must pass with source compatibility. Preserve the original scenario's own gameplay, deadlines and cleanup; a separate native wall witness removes only a justified incidental-contact demand. Normal Menu teardown, exception cleanup and unexecuted cleanup are different facts. Test teardown is not a substitute for skipped end-of-journey assertions.

If failure: preserve inputs, first divergence, actual query/intent/native receipts, limits/dropped events and protection outcome. Classify gameplay, fixture, obsolete contract, incomplete evidence or uncertainty before changing anything. No timeout extension, collision-margin reduction or blanket Idle exemption. Test/runner changes remain separate reviewed changes from gameplay corrections.

## Large-world validation — future authorization required

Benchmark controls then 1024²/1200²/1500² with representative connected/fragmented terrain; mixed-size squad and staged active-creature counts; digging/combat/projectiles; generation/collider rebuild; sparse/heavily edited save/load and repeated teardown. Record cold/warm load, peak/resident memory, collider/path counts, route latency, CPU/GPU/GC and p95/p99/max/spike counts at 16.7/33.3/50/100 ms. Define hardware/build/workload first; no giant cross-product or reduced camera/body fidelity to pass. Reuse existing profiling, not a second profiler.
