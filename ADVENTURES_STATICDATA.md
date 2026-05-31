# Adventure crystals (static data)

Adventure crystals spawn on the map the same way tappables and encounters do: **Solace.TappablesGenerator** publishes `adventureSpawn` events on the `tappables` event-bus channel, and **Solace.ApiServer** stores them and exposes them on `GET .../locations/{lat}/{lon}` as `activeLocations` with `type` **`PlayerAdventure`**.

Starting a world uses the same buildplate pipeline as encounters: **`POST .../multiplayer/adventures/{adventureId}/instances`** with body `{"tileId":"<tile_x>_<tile_y>"}` (same shape as encounter instances). The launcher loads the chosen template via `loadEncounter` / global **encounter buildplates** in the database.

## Layout under `staticdata/adventures/`

| Path | Purpose |
|------|---------|
| `adventures-spawn.json` | Spawn tuning (counts, delays, duration, per-cycle chance, list of crystal types). |
| `{folder}/{folder}-buildplates.json` | Weighted pool of **template IDs** for that crystal tier (`folder` must match the directory name). |

### Inventory crystal vs zip file vs `templateId`

- **`POST .../adventures/scrolls/{instanceId}`** (implemented in Solace) uses **`instanceId` = the non-stackable inventory instance UUID** of `minecraft:adventure_crystal_*` in the player’s inventory. It is **not** the encounter template id and **not** the zip filename.
- The **`.zip`** next to `uncommon-buildplates.json` is only a convenient place to store world exports; the game server does **not** read that zip from this path when starting a world.
- Each **`templateId`** in `*-buildplates.json` must be the **global encounter buildplate id** already stored in Earth DB (`encounterBuildplates`), same as encounter spawns. Strip **`.zip`** from ids in JSON if present; the loader strips it automatically.

### Valid `uncommon-buildplates.json` shape

The root object must start with `{` and there must be **no trailing comma** after the last array element:

```json
{
  "buildplates": [
    { "templateId": "your-encounter-template-uuid", "weight": 10 }
  ]
}
```

### Scroll redeem without map spawns

Even if `adventures-spawn.json` has `"crystalTypes": []`, Solace still loads `common/common-buildplates.json`, `uncommon/uncommon-buildplates.json`, etc. when those files exist, so **placing a crystal from inventory** can still pick a template. **Random map spawns** still require non-empty `crystalTypes` in `adventures-spawn.json` and a running **TappablesGenerator**.

If the `adventures` folder or `adventures-spawn.json` is missing, adventure spawning is disabled (no errors).

### Optional extra tier

The vanilla catalog also includes **`minecraft:adventure_crystal_oobe`**. To support it, add folder `staticdata/adventures/oobe/`, file `oobe/oobe-buildplates.json`, and an entry in `crystalTypes` with `"folder": "oobe"` and `"icon": "minecraft:adventure_crystal_oobe"`.

## `adventures-spawn.json`

| Field | Type | Meaning |
|-------|------|---------|
| `minCount` / `maxCount` | int | After a successful spawn roll, how many crystals to place on that tile for that generator tick (inclusive). |
| `minSpawnDelayMs` / `maxSpawnDelayMs` | int64 | Random delay after `currentTime` before the crystal becomes active. |
| `minDurationMs` / `maxDurationMs` | int64 | How long the crystal stays valid (`expirationTime` − `spawnTime`). |
| `chancePerSpawnCycle` | int | One roll per tile per spawn invocation: spawn happens if `random(0, chancePerSpawnCycle) == 0` (so `4` ≈ 25% like the old encounter default). |
| `crystalTypes` | array | Weighted crystal tiers; one tier is picked per crystal. |

Each **crystalTypes** entry:

| Field | Type | Meaning |
|-------|------|---------|
| `folder` | string | Subdirectory name under `adventures/`; must have `adventures/{folder}/{folder}-buildplates.json`. |
| `icon` | string | Item id shown on the map (e.g. `minecraft:adventure_crystal_common`). |
| `rarity` | string | `COMMON`, `UNCOMMON`, `RARE`, `EPIC`, or `LEGENDARY` (drives API rarity metadata). |
| `pickWeight` | int | Relative weight when choosing which tier a spawned crystal uses. |

## `{folder}/{folder}-buildplates.json`

```json
{
  "buildplates": [
    { "templateId": "your_template_id_in_global_encounter_buildplates", "weight": 10 },
    { "templateId": "another_template", "weight": 1 }
  ]
}
```

- **`templateId`**: Must match an **encounter buildplate** id stored in Earth DB (`encounterBuildplates` global object), same as encounter spawns. Import templates with your existing encounter/buildplate tooling.
- **`weight`**: Relative probability within that tier’s pool.

## End-to-end checklist

1. Put world templates into global **encounter buildplates** with stable ids.
2. Reference those ids in the appropriate `*-buildplates.json` files with weights.
3. Tune `adventures-spawn.json` (chance, counts, lifetimes).
4. Run **TappablesGenerator** with `--dir` pointing at the parent of `adventures` (same as other static data).
5. Clients load locations → see `PlayerAdventure` → request **`/multiplayer/adventures/{id}/instances`** with the location’s `tileId`.

## Example (enable after you have real template ids)

Committed `adventures-spawn.json` uses `"crystalTypes": []` so adventure spawning stays off until you configure it. When ready, populate `crystalTypes` and replace placeholders in each `*-buildplates.json`.

```json
{
  "minCount": 0,
  "maxCount": 1,
  "minSpawnDelayMs": 60000,
  "maxSpawnDelayMs": 120000,
  "minDurationMs": 300000,
  "maxDurationMs": 600000,
  "chancePerSpawnCycle": 6,
  "crystalTypes": [
    {
      "folder": "common",
      "icon": "minecraft:adventure_crystal_common",
      "rarity": "COMMON",
      "pickWeight": 100
    },
    {
      "folder": "uncommon",
      "icon": "minecraft:adventure_crystal_uncommon",
      "rarity": "UNCOMMON",
      "pickWeight": 40
    }
  ]
}
```

Example `common/common-buildplates.json`:

```json
{
  "buildplates": [
    { "templateId": "my_imported_encounter_template", "weight": 10 },
    { "templateId": "another_template", "weight": 1 }
  ]
}
```

The repo also includes tier folders under `staticdata/adventures/{common,uncommon,rare,epic,legendary}/` with placeholder `REPLACE_WITH_ENCOUNTER_BUILDPLATE_ID` entries you can edit.
