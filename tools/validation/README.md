# Maintained controlled validation

Read [AGENTS](../../AGENTS.md), [current status](../../docs/CURRENT_WORK.md), [testing](../../docs/TESTING.md) and private sibling Operations/Safety procedures. Documentation grants no execution.

## Source restoration

Verify the reviewed development commit/inventory,assets/packages/settings hashes,GUIDs and asmdefs. Use ProjectVersion.txt's Editor version. Git does not preserve empty directories: recreate `Assets/GobboPlayerMovement/Boom`, `Fast`, `Fat`, `Fungal`, `Scavenger`, `Throw` only when absent, with their existing corresponding `.meta` files unchanged. Do not replace GUIDs. Verify descriptors/directories before Unity. File reconstruction is not build/gameplay acceptance.

SporeGobbo.slnx and TestResults-InteractionCleanup.xml are narrowly ignored root generated files; no broad evidence ignore/deletion follows.

## Private configuration

`machine.example.json` is deliberately unusable placeholders. It documents actual keys, not an execution profile. Operations/machine.json,protection-state,archive descriptors/seals,versioned profiles and claims stay private. Missing inputs fail closed. Real values must be independently verified; never guess registry/preference state.

| Keys | Meaning |
|---|---|
| schema,owner,project,operations | Schema1,actual account and exact checkout/sibling Operations; checked at import |
| validation,archive,unity,python | Absolute no-reparse owned locations and verified installed tools |
| live,realData | Historical source identity and independent real product data. Retired-reference replaces only expanded source; it never disables data protection |
| registryRoots,preferenceBaseline | Exact product keys; each values list has name,native type,valueHash of repr(native value). Existing capture procedure handles binary representation. Never public exports |
| execution | Relative Operations/Executions authorization path; fresh reviewed records/create-only claims |
| budgetRoots | Absolute paths and measured baselineBytes used for additional working usage |
| budgetEvidenceAllowanceBytes,additionalAllowanceBytes | Reviewed evidence reserve and total extra-byte cap; zero examples intentionally do not permit work |
| minimumHeadroomBytes | Recorded 5 GiB rule; current runner independently enforces hardcoded 5 GiB. Editing this field cannot change it |
| retirementRoots | Historical fixture metadata, not deletion permission or protection-root authority; use protection-state |

## Existing command definitions

```
python -B tools/validation/run.py --mode build --scope regression
python -B tools/validation/player.py --mode capture --scenario normal --profile normal
```

These definitions are not authorization. Use configured Python. Capture requires current successful reviewed build and fresh isolation. No direct/historical executable fallback. Reusable Validation/Project remains disposable; each process retains distinct owned storage/output and unique identity.

Old cutover pins/claims are consumed and intentionally stale after protection changes. Future execution needs reviewed versioned source/dependency pins,exact inventory/profile/output/deadlines,assembly applicability and one-use phase claims. Never overwrite old records or learn expected inventory from green output.

Determine original-live protection from the actual Operations/protection-state.json and its execution receipt. A proposal does not activate retirement, and completed retirement must not be described as permanently staged. Existing retired-reference mode verifies exact source/root/Git facts from a pinned archive seal and still requires Project,real data and preferences. Missing/corrupt/incomplete proof must fail, not suppress missing-path errors. Synthetic protection checks are not native gameplay evidence. Documentation/reference work alone does not imply another full regression/build/journey.
