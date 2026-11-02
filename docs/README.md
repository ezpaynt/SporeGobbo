# Spore Gobbo engineering documentation

**Installed: 2026-11-02 approved documentation revision for the Stage6.4D candidate only.** No gameplay acceptance, execution or Git publication is authorized. Original Draft3/review evidence remains unchanged. Read [project AGENTS](../AGENTS.md) first; it does not replace workspace safety.

## Document map

| Document | Purpose and authority |
|---|---|
| [AGENTS](../AGENTS.md) | Agent startup, safety, approval boundary and reporting |
| [ARCHITECTURE](ARCHITECTURE.md) | Source-verified current ownership, dependencies, lifetimes, extension seams and limitations |
| [GAMEPLAY_CONTRACTS](GAMEPLAY_CONTRACTS.md) | Approved behavior independent of prototype implementation; explicit unresolved details |
| [TESTING](TESTING.md) | Safe execution map, coverage ledger, independent witnesses and strict acceptance |
| [ENGINEERING_STANDARDS](ENGINEERING_STANDARDS.md) | Practical Unity/C# naming, serialization, lifecycle, performance and migration approved house practices for authorized development |
| [DECISIONS](DECISIONS.md) | Stable significant decisions, provenance, alternatives, supersession and change control |
| [CURRENT_WORK](CURRENT_WORK.md) | Replaceable source/evidence/authorization snapshot; not evergreen architecture |

CURRENT_WORK is the only additional reference document. It prevents hashes, stage failures and local paths from accumulating in the evergreen guides. Its dated assertions must be refreshed through authorized read-only inspection before execution; it cannot grant approval.

## Authority and evidence

- **Approved contract:** owner-approved requirement/boundary, with provenance. Explicit owner instructions bind when given; installing documentation records approval, not creates it.
- **Verified implementation:** source-backed description for the identity in CURRENT_WORK. Source verification is not native runtime proof or owner acceptance.
- **Open question/direction:** unresolved detail or proposed approach; no feature implementation permission.
- Implementation status (staged, measured, pending validation, accepted by owner) is separate from these classifications. If source contradicts approved intent, report the discrepancy; do not rewrite intent to excuse it.

This revision preserves the explicit decisions in the owner's Draft 3 review request. Previously quoted directions without a complete independent approval record are labeled as such. The owner approved this documentation and house practices for installation; open gameplay, execution scopes and Git operations remain separately gated.

## Maintenance and revision gate

Update verified source descriptions/coverage after authorized changes. Record changed approved decisions only after approval, preserving supersession; list every documentation delta in the task report. Keep append-only failed evidence intact and current status out of evergreen docs.

This approved installation adds root AGENTS and seven docs/ files, with append-only pointers in workspace AGENTS and V1_FOUNDATION. Future revisions need applicable authorization and an explicit allowlist. Verify links, preserve safety/history, and report all documentation changes. Stage6.4D acceptance, Git backup and local consolidation remain separate actions.

## Existing-reference classification and disposition

| Reference | Status/use |
|---|---|
| [Workspace AGENTS](/C:/Users/ezpay/Documents/SporeGobbo/AGENTS.md) | Active durable safety instructions; append-only pointer replaces no original rule |
| [Safety README](/C:/Users/ezpay/Documents/SporeGobbo/safety_checkpoint/README.md) and [findings](/C:/Users/ezpay/Documents/SporeGobbo/safety_checkpoint/findings.md) | Mandatory separate operational references and incident evidence; retain all storage, identity/cloud, native-preference, path, postflight and recovery protections |
| [V1_FOUNDATION](../V1_FOUNDATION.md) | Retained source/scene overview with current-doc pointer; dated acceptance applies to its original payload |
| [Root ARCHITECTURE](../ARCHITECTURE.md) | Legacy CampScene/MapGenerator/RunProfile reference, not current replacement-region baseline |
| [PROJECT_CONTEXT](../PROJECT_CONTEXT.md) | Historical architecture philosophy/mixed gameplay proposals; old growth numbers/speculation not newly approved |
| [TECH_DEBT](../TECH_DEBT.md) | Historical leads to verify, not acceptance/deletion authority |
| [Geometry README](../Assets/Terrain/Geometry/README.md) | Stage1 numerical contracts; assembly-wide no-authority wording predates later state/routing additions |
| [Preview README](../Assets/Terrain/Geometry/Preview/README.md) and [Stage3 guide](../Assets/Terrain/Geometry/Preview/Stage3-Playable-Preview.md) | Historical behavior only. Direct Play/raw test/rebuild/capture commands do not authorize current execution |
| [MAIN_CHAT_CONTEXT](/C:/Users/ezpay/Documents/SporeGobbo/MAIN_CHAT_CONTEXT.md), combat audit/design reviews | Historical handoff/recommendations; retrieve primary owner decisions for unresolved story/growth/succession/ecology. SG-01 supersedes old squad uncertainty |
| Stage60/61C/later guides and reports | Scoped procedures, baselines, outputs, recovery and validation. Later explicit permission supersedes stage limits, not durable safety |
| [Stage64D STATUS](/C:/Users/ezpay/Documents/SporeGobbo/stage64d_work/STATUS.md), diagnostics, archives | Cumulative history; CURRENT_WORK identifies current source/latest verdict. No automatic archive, cleanup or replay |
| Source comments | Implementation/invariant evidence, not owner approval. Flag stale logical/physical-authority shorthand without source edits during documentation work |

All existing references remain in place. This index supersedes conflicting current-baseline interpretations, not legacy facts. Historical commands require newly authorized reviewed workflows. Root AGENTS references workspace safety explicitly because discovery at a nested Git root may not include parents outside it.
