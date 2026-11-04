# Engineering standards — approved documentation revision 2026-11-02

These practices operate within approved architecture/contracts; they do not authorize source changes or migrate existing serialized data. Use the existing owner and composition before new infrastructure. A pure helper or subordinate class can improve cohesion without becoming another authority. Justify new retained state, caches, managers and lifecycle ownership; do not impose arbitrary class/method lengths or mandatory patterns.

## Naming and code readability

| Element | House rule for new/materially edited code |
|---|---|
| Types, enum members, methods, properties, events, constants | PascalCase; interfaces begin I |
| Fields, parameters, locals | camelCase without mandatory underscore, matching prevailing project code |
| New Inspector configuration | Prefer `[SerializeField] private` fields; expose deliberate operations/read-only properties where useful |
| Existing public serialized fields / save DTOs | Preserve names/visibility/schema unless separately reviewed; do not mechanically convert to properties |
| Events and callbacks | Events describe occurrence (e.g. Committed); handler names describe the handled event; Unity messages keep their required spelling |
| Source files | Primary component/type name; partial files use Type.Responsibility.cs. Multiple small related contracts can share a cohesive file |
| Tests | Named feature/contract and observable outcome; stage prefix does not establish acceptance status |
| New namespaces | SporeGobbo.Subsystem where existing assembly dependencies allow; not a mass namespace migration |

Use normal readable formatting, explicit units and query purpose, small cohesive methods and comments explaining invariants. Avoid compressed lifecycle/geometry expressions that hide decisions. Do not reformat unrelated files during a gameplay repair. Semantic ambiguity (Sight versus Vision, bodyRadius versus reserve) matters more than cosmetic casing.

The inspected project mixes public camelCase Inspector fields, private camelCase, PascalCase properties/types/methods, mixed constants and global/namespaced code. The house rules are the approved direction for authorized new/materially edited code, not a claim the whole source conforms or permission for cosmetic migration. Microsoft naming guidance informs API casing, while private-field style is project policy: https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names

## Components, configuration and state

- Separate behavior decisions from reusable execution where a real shared responsibility exists. Keep creature-specific attack timing/choices; share equivalent body, terrain, attack execution and damage rules.
- Use existing prefab/Inspector/ScriptableObject configuration for meaningful variation; do not invent a universal ability/data framework for one new enemy.
- Shared ScriptableObject assets represent authored configuration, not mutable per-actor health, cooldown, roster or population state. Copy runtime modifiers into their explicit owner and remove them on lifecycle end.
- Cache only with a named invalidation policy and evidence of benefit. Never add a second authoritative obstacle/roster/save store disguised as a cache.
- Canonical identity comes from the owning campaign/region contract, not prefab name, sprite, layer or transient instance ID. Session teams/contact roles and eventual faction identity are related but distinct.

## Initialization, motion and cleanup

- Establish region/binding/readiness before normal activity. Initialization may span frames and multiple components; Awake alone is not proof all dependencies are ready.
- Declare who may request/apply/stop movement in each state, including attacks and knockback. Distinguish intention, admitted connector, native displacement and task completion. Buddy ActorMotionIntent does not report every enemy action.
- Every geometry query names reference point, supported body geometry, optional reserve and purpose: spawn, routing, voluntary motor admission, physical contact, projectile or attack. Never substitute origin radius for offset physical coverage or walking reserve for attack reach without review.
- Pair subscriptions with explicit disable/destroy/session cancellation according to publisher/subscriber lifetime. Retain delegates when anonymous subscriptions need removal. Global/static publishers can retain scene objects.
- Explicitly own spawned objects, meshes/materials, colliders, route work, claims, contact overrides, modal/camera/routine leases and evidence buffers. Cleanup must handle normal end, cancellation, removal, scene exit, partial initialization and exceptions, and be safe when invoked more than once.
- Restore only settings/resources the service owns; do not undo another owner's later state or resurrect removed/dead canonical actors. A failed restore remains an error, not a reason to save corrupted transient state.
- Use Unity-valid object checks before accessing native components, including destroyed/fake-null references. Visual-only pickups do not gain dummy physical bodies. Fail clearly for unsupported shapes rather than silently omitting physical coverage.

C# events can retain subscribers; lifecycle ownership is a correctness requirement, not merely style: https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/events/how-to-subscribe-to-and-unsubscribe-from-events

## Serialization and assets

Save through the existing capture/storage boundary using detached validated data. Maintain canonical IDs, format/schema versions, recovery and atomicity. Do not invoke end-of-run accounting from a future checkpoint. No arbitrary Unity-object access on worker threads; an async wrapper does not establish a consistent snapshot.

Unity asset GUIDs and metadata must survive reviewed moves. Preserve `.meta`, serialized fields, save-schema keys, prefab/scene references, asmdefs, reflection/strings, Resources paths and serialized type names. FormerlySerializedAs protects a Unity field rename in its supported context, not arbitrary JSON/type/namespace migration. Consult:

- https://docs.unity.com/en-us/engine/6000.0/manual/assets-and-media/import-assets/asset-metadata
- https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/serialization/formerlyserializedasattribute

New work should be feature-owned with Runtime/Editor/Tests and asset groupings where helpful. Do not move existing code across an asmdef just to match a folder template; default Assembly-CSharp dependencies and Editor/test defines matter. Existing Stage59/Stage61/Preview paths can contain current shared runtime. No deletion based on age/name.

## Performance and diagnostics

Define hardware, build/profile, warmup, workload and source identity. Separate generation/loading, ordinary gameplay, durable saves and teardown. Measure CPU/GPU/physics/navigation costs, allocations, p95/p99/max frames, spike counts and retained/high-water memory. No capacity claim from bitset arithmetic or average FPS.

Prefer bounded local terrain updates and time-budgeted work. Audit route publication/suffix scans, native queries, pairwise coordination and active-actor work as well as search. Reuse buffers/pool objects only when measurement justifies lifecycle complexity; do not pool everything by default. Diagnostics must have explicit capacities, dropped-event counts and stated overhead; instrumented runs are not automatically matched performance evidence.

## Controlled cleanup proposals — not part of this revision

1. Document current semantic names and active historical dependencies first.
2. Apply style to new/changed code without cosmetic churn.
3. Separately propose ambiguous API renames, preserving behavior and compatibility.
4. Review a small subsystem move with GUID/type/schema/assembly/Resources/reflection inventory and recovery.
5. Validate affected prefabs/scenes, saves, discovery/build/test inventories and teardown before acceptance.

No asset move, namespace/assembly change, renaming, formatting campaign or source cleanup is authorized by these documents.

## Workspace and artifact lifecycle

Maintain one authoring checkout. Before creating another copy/output, record owner, exact location, purpose, required recovery inputs and removal condition in the task receipt. Reuse controlled Validation where identity/source/isolation are verified; each process still needs distinct owned output/storage. Extra checkouts need a concrete purpose.

Put task receipts under Operations/Executions. Promote maintained runtime, tests, tools and docs into Project through reviewed scope. Machine configuration and synthetic/private campaigns stay outside public Git. Preserve necessary failures, exact reproduction inputs and decisions; do not automatically preserve every expanded project, build and cache.

Distinguish **inactive**, **preserved** (verified recovery), **removal-approved** (exact owner scope/conditions), and **removed** (receipt/postflight). Age, passed tests and generated-looking names never grant deletion. Do not retire a live session's cwd/instruction root.

At task completion classify artifacts: keep, promote, archive, or propose removal. Remove only under exact applicable approval with path/reparse/process/lock/recovery/protection checks. Preserve drift; never reset/clean it away. A harmless helper correction needs its error and corrected script, not another full backup.

At meaningful completed boundaries review source, `.meta`, tests, tools, configuration and docs for cohesive commits. Report working-only, committed and remotely verified state separately. Publication needs inclusion/privacy/diff/remote checks; preserve main/backup history. Source backup is not gameplay acceptance.

Keep CURRENT_WORK concise; durable rules belong in their guide, immutable receipts in Operations/Archive. No automatic cleanup, synchronization, scheduling or push authority is created. Protected-data changes, architecture changes, publication and deletion retain explicit approval boundaries.
