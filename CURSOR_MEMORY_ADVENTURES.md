# Solace — Adventures & adventure crystals (agent / user memory)

Use this file to restore context in new chats or as project notes.

## Client vs server IDs (`POST /1/api/v1.1/adventures/scrolls/{id}`)

- The path **`{id}`** may be either:
  - **Catalog item UUID** (from `items.json` / items catalog), e.g. `81a84b7e-928f-7157-254c-6543e90dbc59` for `minecraft:adventure_crystal_uncommon` — crystals are often **stackable**, so there is **no** per-item instance UUID in inventory.
  - **Non-stackable instance id** when the crystal (or future item) is stored with instances; then `{id}` is the instance UUID.
- Server resolves catalog id first, then falls back to `TryGetNonStackableByInstanceId`.
- Consumption: **`TakeItems(catalogId, 1)`** for stackable; **`TakeItems(catalogId, [instanceId])`** for non-stackable paths.
- Template pick uses the catalog item **`Name`** (`minecraft:adventure_crystal_*`) via `AdventuresConfig.TryPickTemplateForCrystalItem`.

## Map / AR icons (textures vs “beams only”)

- **`minecraft:adventure_crystal_*`** in `activeLocations[].icon` often yields beams without proper map art.
- Map-facing icon should be **`genoa:adventure_generic_map`**, **`genoa:adventure_generic_map_b`**, or **`genoa:adventure_generic_map_c`** (rarity-based).
- Code: **`Solace.StaticData.AdventureMapIcons.ToClientMapIcon`** — used in ApiServer (scroll redeem + tappables merge) and **TappablesGenerator** `AdventureGenerator` for spawned adventures.

## Static data layout

- Under static data root: **`staticdata/adventures/`**
  - **`adventures-spawn.json`** — spawn timing, `crystalTypes[]` with `folder`, `icon`, `rarity`, `pickWeight`.
  - Per rarity folder (e.g. `common/`, `uncommon/`): **`*-buildplates.json`** lists `buildplates[]` with `templateId` (encounter template UUID) and `weight`; companion **`.zip`** files use the same base name as the template id when packaged.
- **`templateId`** is the encounter template id, **not** a random filename; stripping `.zip` in config is handled where applicable.
- Keep JSON **strictly valid** (no trailing commas).

## Services to redeploy after changes

- **ApiServer** (scroll redeem, locations JSON, buildplates API).
- **TappablesGenerator** (world adventure spawns + correct icons at publish time).

## Request body for scroll redeem

- JSON must include coordinates under **`coordinate`** or **`playerCoordinate`** (nested object), or top-level **`latitude`/`longitude`** or **`lat`/`lon`** (parser is flexible / case-insensitive).

## Longer doc in repo

- **`ADVENTURES_STATICDATA.md`** — full staticdata contract and Earth API notes.

## Past pitfalls (resolved in code)

- **400 on redeem** when the client sent catalog UUID but server only accepted non-stackable instance ids.
- **`InvalidOperationException` MoveToImmutable** in generator when building immutable collections with wrong Count/Capacity — fixed by using **`ToImmutable()`** (or equivalent) in adventures staticdata path.
