# Technical Debt

- **CampInteractPrompt stale prefab-source references** — The CampScene prompt remains under its standalone Canvas and its legacy prefab ancestry is unresolved.
- **SampleScene stale LevelUp/UI prefab-source references** — Existing SampleScene prefab-source references require a separate investigation.
- **SampleScene unresolved tankGobboBack sprite** — The serialized sprite reference remains unresolved.
- **HealingWeevilMeat missing legacy FoodItem script** — This known unused legacy prefab retains its missing component.
- **TunnelWeevil unresolved root SpriteRenderer sprite** — The root sprite remains unresolved; runtime directional sprite assignment currently masks it.
- **Canvas.controller ownership unresolved** — The controller’s long-term feature ownership has not been established.
- **CampFire.prefab usage/ownership unclear** — Its relationship to the active CampScene presentation still needs a separate ownership review.
- **GobboUpgrade legacy status** — Whether this asset remains required has not been established.
- **EnemyHealth current usage uncertain** — Its current ownership and active usage require confirmation.
