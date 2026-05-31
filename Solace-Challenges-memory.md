# Solace — Minecraft Earth challenges (memory)

Use this as project context for Genoa clients, `/player/challenges`, and daily/tutorial behavior.

## Client (Genoa)

- Native lib: **genoa** (`libgenoa.so`). Challenge card copy comes from the APK **`en_US.lang`** keys:
  - `challenge_<referenceId>.title`
  - `challenge_<referenceId>.desc` / `.desc.single` / `.desc.multiple`
- **`CHALLENGE_<uuid>`** style headings are a **fallback** when `.title` is missing for that id.
- **`/player/challenges` response**: object **keys** are Project Earth–style **filename UUIDs** (challenge JSON names). Each entry’s **`referenceId`** is the inner id used for localization above. Do not confuse storage key with `referenceId`.

## Solace defaults (`Challenges.cs`)

- **Season stubs** (journal): `00000000-…-0001` / `…-0002`; persona item UUIDs must stay as in `ChallengesController` or the journal can hang on “updating”.
- **Daily group** (filename key): `29ebe650-072f-4f70-996f-4ffdda93ed1f` — parent row `ParentId == null`, category `retention`, duration `PersonalTimed`.
- **Retail `referenceId`s** (have `.title` in stock 0.33 `en_US`):
  - Parent daily: `2619913d-6504-4c74-9fc9-e03649a70efc` (“Tap tap tap” / collect tappables).
  - Timed children: `2b64c950-…` (Treasure Hunt), `06eb0e50-…` (Best defense), `bd9d3fd7-…` (Chop chop).
  - Tutorial: `7c1dc118-…` (“Adventure begins”).
- **Legacy migration**: old all-zero keys, inner-keyed rows, and old Project Earth inner ids are remapped in `MigrateLegacyChallengeIds` / `RepairFilenameKeyedReferenceIds`.

## `NormalizeForClient` (critical behavior)

- **Does not** reset a row just because `IsComplete` is true — **complete but unclaimed** must survive repeated `GET /player/challenges`.
- **Resets** daily/tutorial retention rows when the window **`IsExpired`** OR **`State == "Claimed"`** (reward taken → fresh window / replay same day).
- Daily/tutorial header: fixes `Duration` / `Category` / `Type`; does **not** force `State = Active` if already **`Complete`** or **`Claimed`**.

## Server hooks

- **`ChallengesProgress.AfterTappableRedeemed`**: on successful tappable redeem, increments **`DefaultDailyChallengeId`** parent `CurrentCount` (Active/Unlocked only), updates `PercentComplete`, sets **`Complete`** when `CurrentCount >= TotalThreshold`. Chained from `TappablesController` after redeemed-tappables update.
- **Other daily children** (chests, mobs, oak log) still need **`modifyState`** from the client or future server hooks (adventures/inventory) — not auto-driven by tappables alone.

## `ChallengesController`

- No `ChallengeFallbackTitles` or large JSON debug logs; **`title` / `readableName`** omitted (null) so the client uses pack strings.
- **`clientProperties`**: pass-through from DB only.
- **`modifyState`**: `ChallengesLogic.ApplyStateChange`; claim path applies `ClaimRewards` via `Rewards.ToRedeemQuery`.

## Tests

- **`Solace.ApiServer.Tests`** project was removed (was challenges-only xUnit). Build with `dotnet build src/Solace.ApiServer/Solace.ApiServer.csproj`.

## Useful external extracts

- Full lang dump: e.g. `rg '^challenge_[0-9a-f-]{36}\\.' .../en_US.lang` → pairs of `.title` / `.desc*` per id.
- Wiki [Earth:Challenges](https://minecraft.wiki/w/Earth:Challenges) is approximate; trust **APK strings** + **PE JSON** for exact copy and objectives.

## Files to open when changing challenges

- `src/Solace.DB/Models/Player/Challenges.cs` — defaults, migration, `NormalizeForClient`, `RefreshExpiredChallengeWindows`.
- `src/Solace.ApiServer/Controllers/EarthApi/ChallengesController.cs` — GET/POST shape, `ToRecord`, persona merge.
- `src/Solace.ApiServer/Utils/ChallengesLogic.cs` — state transitions and progress math.
- `src/Solace.ApiServer/Utils/ChallengesProgress.cs` — tappable → daily parent increment.
- `src/Solace.ApiServer/Controllers/EarthApi/TappablesController.cs` — redeem chain.
