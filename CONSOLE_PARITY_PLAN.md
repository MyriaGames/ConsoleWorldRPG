# Myria.ConsoleApp — Next Round: Data Sync, Localization, Server Settings

Previous plan (bringing console to Wpf feature parity: realm selection, auth
error surfacing, guild property/base, skill-fusion scoping, NpcInteraction
split) is complete — see git history for that phase. This plan covers the
next three things: root-causing the "wrong rooms" bug, giving console its
own localization coverage, and making the server address configurable.
§1–§3 are all complete. §4 below covers a live bug-hunt session against the
debug server after §1–§3 landed.

## 4. Bug hunt findings (live session against debug server)

Logged in with a real test account, created a character, and drove the
console through a full session. Found one major systemic bug (fixed) and
two smaller ones (not yet fixed).

### Done: game content displayed raw locale keys almost everywhere
Every room, item, monster, quest, and job name/description was printing its
literal locale key instead of resolved text (e.g. `room.royas.main`,
`item.starter_gauntlets`, `job.miner.name`) — this is a different, deeper
bug than the §2 sweep: §2 correctly localized console's own literal English
strings, but never touched this separate layer where `Myria.Lib.Core`'s
shared data files store locale *keys* (not display text) in `Name`/
`Description` fields, meant to be resolved via `Localization.T(...)` at
display time (Wpf does this everywhere, e.g.
`room.IsLiteral ? room.Name : Localization.T(room.Name)`,
`Localization.T(item.Name)`). Console never did this resolution anywhere.

Confirmed which fields are affected (no `IsLiteral` escape hatch except
Room): **Room** (Name/Description, conditional on `IsLiteral`), **Item**
(always), **Monster** (Name/Description, always), **Quest** (Name/
Description/dialog line Text, always — except the hardcoded `"..."`
fallback dialog line, which is literal and must NOT be wrapped), **Job**
(Name/Description, always). **Not affected:** `Skill.Name` and
`GatheringSpot.Name`/`Description` are plain literal text in the data files.

Also confirmed several server-sent DTOs pass the same raw keys through
unresolved (server doesn't localize before sending) — `StartCombatResult.
MonsterName`, `GroupCombatantState.Name` (monsters only, not player names),
`ShopListing.Name` (used by `ShopSale`/`ShopBuyResult`/listing lines),
`TradeItemDto.Name` — all now resolved client-side too.

**Fixed:** added `Room.DisplayName()`/`DisplayDescription()` extensions in
`Utils/ConsoleExtensions.cs` (respects `IsLiteral`); wrapped every other
affected field at its display site across `Utils/Printer.cs`,
`Utils/ConsoleExtensions.cs`, `Systems/CombatSystem.cs`,
`Commands/{Gather,Inventory,Combat,Shop,Trade}Commands.cs`,
`Commands/MapCommands.cs` (the current-room star highlight on `map` was
silently never matching, for the same reason), and
`Entities/NPCs/{QuestDialog,Shop,Craft,Upgrade}Handler.cs`. Verified live:
room name/description, inventory item names, and job names all resolve
correctly now.

**Known follow-up, not fixed:** `InventoryCommands.ShowItem`'s fallback
match (`Character.WeaponSlot?.Name.Equals(itemName, ...)`) compares the
typed item name against the raw `Item.Name` *key*, so a player typing the
item's actual display name to `show` an equipped item will never match.
`InventoryUtils.ResolveInventoryItem` itself is fine (matches by `Id`
prefix, unaffected). Also, room occupant lists (`go to healer` → "You see
the following people: - healer_default...") show raw NPC ids rather than a
display name — unclear if this is even meant to resolve (no equivalent
found on the Wpf side to confirm the right fix), lower priority.

### Done: crash on unexpected EOF during character selection
`LoginManager.SelectOrCreateCharacter` did `Console.ReadLine()!.Trim()` at
two call sites — if stdin ended unexpectedly (piped/redirected input, or a
real terminal EOF), this threw an unhandled `NullReferenceException` and
killed the process. Audited the rest of the app for the same
`Console.ReadLine()!` pattern — these were the only two occurrences. Added
`LoginManager.ReadLineOrExit()` (prints the goodbye message and exits
cleanly via `Environment.Exit(0)` on EOF instead of crashing) and switched
both call sites to it. Verified: piping input that runs dry at the
character-selection prompt now exits gracefully instead of crashing.

### Done: hub connection race after login
`Game.Start()` did `_ = TryConnectHubAsync(_player);` (fire-and-forget, not
awaited) immediately before entering the command loop, so commands typed
right after login could silently run in offline mode for a moment while
the connection was still establishing. Changed to
`TryConnectHubAsync(_player).GetAwaiter().GetResult()` (blocking, matching
the sync-over-async convention used everywhere else in this codebase) so
the command loop only starts once the hub connection attempt has actually
finished. Verified live: the `[Online — connected to multiplayer server]`
message now always prints before the room prompt/first command, and
`players` immediately reflects the correct online state instead of
reporting offline mode right after login.

### Done (2nd bug-hunt pass, with a real Wpf player in the room): whole-input lowercasing corrupted chat text and broke every name-based social command
Follow-up bug hunt with a real second player (Wpf client, character "Test")
in the room found this the hard way: `friend add Test`, `party invite Test`,
`trade Test`, and `w Test <message>` all failed or misbehaved.
`CommandRouter.HandleCommand` did `input = input.Trim().ToLower();` on the
**entire** input line before any handler saw it. This corrupted two things:
chat message text (readability only), and — much worse — every argument
that has to match another player's exact character name, since the server
matches character names case-sensitively. `friend add Test` was silently
becoming `friend add test`, which doesn't exist, so the request always
"failed" with a generic not-found message.

Also found while chasing this: `w`/`whisper` sends channel `"private"`, but
`GameHub.SendMessage`'s switch only recognizes `"whisper"` — anything else
falls through to the `default:` case, which broadcasts to the whole room
group. So whispers were never actually private — they always went to the
current room. Fixed `PlayerCommands.cs` to send `"whisper"`, matching what
Wpf's `ChatChannel.Whisper => "whisper"` mapping actually sends.

**Fixed:** `CommandRouter` now lowercases only the command verb (first
word) instead of the whole line, preserving case for chat text and
arguments — command handlers' existing `input.StartsWith("verb ")` checks
are unaffected since players type verbs in the documented lowercase form
(minor residual risk: the *second* word of a multi-word verb like `shop
add`/`friend add` isn't forcibly lowercased, so unusual capitalization
there — e.g. typing "Add" — could theoretically fail to match; not
observed in testing and low-impact since players copy the lowercase verbs
shown in `help`). Verified live with a real Wpf player in the room: `say`
now preserves message capitalization, `w Test ...` now shows under
`[whisper]` (not `[room]`) and reaches only the target, and `friend add
Test`/`party invite Test`/`trade Test` all now succeed instead of silently
failing on the mangled name.

## 1. Game data is a stale, hand-duplicated fork of Myria.Lib's — root cause of the room bug

**Finding:** Myria.Wpf's `.csproj` links its `Data/common/**/*.json` directly
from `Myria.Lib.Core`'s copy (`<Content Include="..\Myria.Lib\Myria.Lib.Core\Data\common\**\*.json" LinkBase="Data\common\">`)
— there is exactly one copy of world data, and Wpf always builds against it.

`Myria.ConsoleApp.csproj` instead lists 13 physically duplicated files under
its own `Data\common\` that were copy-pasted at some point and never kept in
sync. Diffing every shared file against `Myria.Lib.Core`'s canonical copy
(md5, current as of this session):

- **All 13** of Console's `Data/common/*.json` files differ from Lib's:
  `caves`, `cities`, `classes`, `dungeons`, `forests`, `items`, `jobs`,
  `monsters`, `npcs`, `quests`, `races`, `recipes`, `rooms`,
  `skill_combinations`, `skills`.
- `rooms.json` specifically: **console has 56 rooms, Lib/Server/Mono have
  87** — this is the direct cause of the "wrong rooms" symptom. Console is
  running against a world that's missing 31 rooms the server actually uses,
  so room IDs the server sends can resolve to nothing or to the wrong room
  client-side.
- Console is also **entirely missing** `effects.json`, `loot_tables.json`,
  and `starting_items.json` (all non-rune, all loaded by the shared
  `Myria.Lib.Core.Services.GameService.InitializeGame`), plus the
  rune-specific files (`base_runes.json`, `rune_families.json`,
  `rune_word_pairs.json`, `rune_words.json`) which are correctly out of
  scope. Missing `effects.json`/`loot_tables.json`/`starting_items.json`
  likely means silent `GameLog.Error` fallbacks to empty data somewhere in
  loot generation, skill effects, and starting equipment — needs checking
  once the file is back in sync.

**Fix:** Change `Myria.ConsoleApp.csproj` to link `Data/common/**/*.json`
from `Myria.Lib.Core` exactly like Wpf does, instead of maintaining its own
copies. Delete the stale duplicated files from
`Myria.ConsoleApp/Data/common/`. This makes room/item/monster/quest/etc.
data structurally impossible to drift again, for either client.

**Not in scope for this fix:** `Myria.ConsoleApp/Data/Maps/*.json`
(`city_lumina_map.json`, `world_map.json`, etc.) — these are console's own
ASCII local-map layouts for the `map` command, unrelated to Wpf's
differently-named `Data/Maps/{caves,cities,dungeons,forests}.json` (which
appear to be a leftover/differently-scoped set on the Wpf side). Not a
priority; flag only if `map` command output looks wrong during testing.

## 2. Console needs its own localization coverage

**Mechanism built:** `Myria.Lib.Core.Systems.Localization` now supports
`RegisterAdditionalLocaleDirectory(path)` — any number of projects can
register a directory containing their own `en.json`/`de.json`, and those
get merged on top of the shared base strings every time a language loads
(including language switches, not just startup). Both clients are wired up:

- **Wpf**: `App.xaml.cs` registers `Data/locales-wpf`. Split ~370 keys that
  were Wpf-page-only (`pg.*` except `pg.fight.*`, which is genuinely shared
  combat-log text used by `Myria.Lib.Core`'s `CombatEncounter`/
  `GroupCombatEncounter`/`EffectProcessor` as well as console; plus
  `app.general.UI.*`) out of `Myria.Lib.Core/Data/locales/{en,de}.json` into
  new `Myria.Wpf/Data/locales-wpf/{en,de}.json` files, confirmed via grep
  that nothing outside Wpf referenced them before moving.
- **Console**: retired its own near-duplicate `Localization` class
  (`Systems/Localisation.cs` — same shape as Lib's, different namespace,
  existed only to avoid a name clash) in favor of a
  `global using Localization = Myria.Lib.Core.Systems.Localization;` alias.
  `Services/GameService.cs` now registers console's existing
  `Data/misc/locales` directory as its addition source — no file moves
  needed there, it was already console-only content.
- Fixed `Systems/CombatSystem.cs` (`EncounterRunner`), which had hand-rolled
  an English-only `_logTemplates` dict duplicating `pg.fight.log.*` — now
  calls the shared `Localization.T(...)` directly, so combat log text is
  localized in German too, for both online and offline combat.

**Finding:** Console has only **47** `Localization.T(...)` call sites but
**668** raw `Console.Write`/`Console.WriteLine` calls across the codebase —
the vast majority of console UI text (menus, prompts, errors, NPC dialog
scaffolding, command output) is hardcoded English with no `de.json`
equivalent. Console's own locale files
(`Myria.ConsoleApp/Data/misc/locales/{en,de}.json`) currently have only 125
keys.

Also found: `Systems/CombatSystem.cs` hand-rolls its own English-only
`_logTemplates` dictionary for combat log messages (`pg.fight.log.hit`,
`.miss`, `.skillHit`, etc.) instead of using `Myria.Lib.Core`'s own
`Localization` system, which already has these exact keys authored in
**both languages** at `Myria.Lib.Core/Data/locales/{en,de}.json` (Wpf uses
this directly). Console should call into that instead of duplicating a
partial, English-only copy.

**Fix (large, doing incrementally):**
- Done: routed `Systems/CombatSystem.cs`'s combat log printing through the
  shared `Localization.T(key, args)` instead of the private `_logTemplates`
  dict — fixed online AND offline combat log localization for German in one
  move, deleted the duplicated English-only text.
- Done: `Utils/Printer.cs` fully converted — `ShowHelp`/`ShowDebugHelp`,
  `ShowLook`, `ShowStatus`, `ShowCharacter`, `ShowJobs`, `ShowEquipSuccess`/
  `Fail`, `FormatCooldown` — all now call `Localization.T(...)` with keys
  under a `console.*` namespace, both `en.json`/`de.json` authored (124 new
  keys each). Verified in a live run in both languages, including the help
  table's alignment (fixed a padding edge case where the longest command
  syntax ran into the dash with no space — now uses `.PadRight(27)` before
  handing the string to the format template instead of relying on the
  composite-format alignment component).
- Done: `Commands/NpcCommands.cs` (no strings to localize), `DebugCommands.cs`,
  `LootCommands.cs`, `MapCommands.cs`, `MovementCommands.cs`,
  `GatherCommands.cs`, `PartyCommands.cs` — 56 new keys (28 each language)
  merged, verified live (`party`/`loot`/`map`/`move`/`gather` all confirmed
  showing localized text).
- Noticed in passing, not fixed: some room descriptions print raw
  (`room.royas.main`/`room.royas.desc` literally, not resolved) — a
  pre-existing gap in the shared Lib locale data itself, unrelated to this
  console-side sweep. Worth a separate look later.
- Done: all of `Commands/*.cs` — `PlayerCommands.cs`, `CombatCommands.cs`,
  `ShopCommands.cs`, `GuildCommands.cs`, `InventoryCommands.cs`,
  `TradeCommands.cs`, `SkillCommands.cs`, `SocialCommands.cs`. 195 new/updated
  keys (191 genuinely new) merged into both languages. Build clean, verified
  live in both EN and DE (`skills`, `combine`, `shop`, `trade`, `guild`,
  `friend`, `inventory`, `unequip`, `fight` all confirmed).
  `Commands/` is now fully localized.
- Done: all 9 files in `Entities/NPCs/` — `NpcInteractionHandler.cs`,
  `QuestDialogHandler.cs`, `HealerHandler.cs`, `ShopHandler.cs`,
  `CraftHandler.cs`, `UpgradeHandler.cs`, `JobMasterHandler.cs`,
  `ClassMasterHandler.cs`, `SkillMasterHandler.cs`. 92 new keys merged into
  both languages. Along the way fixed a real (not just missing-translation)
  bug in `ShopHandler.cs`: `NpcActionResult.MessageKey`/`MessageArgs` are
  explicitly meant to be passed through `Localization.T(...)` (per the
  field's own doc comment in `Myria.Lib.Core`), but both `TryBuy` and
  `SellMenu` were printing the raw key string (e.g. literally
  `npc.healer.notEnoughMoney`) instead of resolving it — now fixed in both
  places. Build clean, verified live in EN and DE (`go to healer` → heal →
  leave → `fight` → `unequip`, plus race-description text, all confirmed
  correctly localized/colored).
  `Entities/NPCs/` is now fully localized.
- Done: `Services/LoginManager.cs` — `Register()`, realm listing, character
  select/create menu, and `CreateNewCharacter()`'s race/class selection flow.
  14 new keys. Also fixed a genuine bug found here: `ui_creation_name_set`,
  `ui_creation_choose_class`, `ui_creation_classes`, and
  `ui_creation_class_set` existed in `en.json` but were **missing entirely**
  from `de.json` — German players hit the character creation flow and saw
  literal `[ui_creation_class_set]` etc. instead of text. Added all four to
  `de.json`. Verified live: full German character-creation run now shows
  zero raw `[key]` markers end to end.

**§2 is now complete.** Every file under `Myria.ConsoleApp` that prints
user-facing text goes through `Localization.T(...)`, backed by console's own
`Data/misc/locales/{en,de}.json` merged on top of the shared
`Myria.Lib.Core` base via `Localization.RegisterAdditionalLocaleDirectory`.
Remaining known gaps are both explicitly out of console's scope:
`Character.Inventory.ListItems()` (shared `Myria.Lib.Core` code prints
hardcoded English regardless of client) and the pre-existing "some room
descriptions print raw keys" gap noted earlier (a content-authoring gap in
the shared locale data, not a console code issue).
- Keep server-supplied strings (e.g. `Reason` fields from hub/REST results)
  as-is — those come from the server in English and localizing them would
  require server-side i18n, which is out of scope here.
- Key-authoring pattern established: `console.<area>.<thing>` for
  labels/messages, `console.help.desc.<command>` for help-table entries,
  `console.common.*` for cross-cutting small pieces (list bullets, "(none)",
  cooldown formatting). Reuse this convention for the remaining files.

## 3. Configurable server address in settings

**Finding:** `Myria.Lib.Core.Models.Settings.Settings` already has a
`ServerAddress` field (persisted via the shared `SettingsService`) and Wpf
already uses it: `App.xaml.cs` applies
`ServerApiService.AuthBaseUrl = ServerApiService.NormalizeAddress(Settings.Current.ServerAddress)`
at startup if the field is non-empty, and `Page_SettingsGame` lets the
player edit it. `NormalizeAddress` just prefixes `http://` if the user typed
a bare `host:port`.

Console currently has no way to change `ConsoleHubClient.AuthBaseUrl` /
`BaseUrl` at all — they're hardcoded to `localhost:5050` / `localhost:5001`
defaults with no settings hook (the realm `BaseUrl` itself is still chosen
via the Phase-1 realm-selection menu; this is specifically about which
**Auth server** to point at, matching Wpf's exact scope for this field).

**Done:**
- Added `ConsoleHubClient.NormalizeAddress` (mirrors Wpf's
  `ServerApiService.NormalizeAddress`) and apply `Settings.Current.ServerAddress`
  to `AuthBaseUrl` in `GameService.InitializeGame`, right after
  `SettingsService.Load()`/`Localization.Load()`.
- `Game.ShowSettingsMenu()` now has a "Server address" option: shows current
  value or "default" if empty, prompts for a new one, normalizes, saves via
  `Settings.Current.ServerAddress` + `SettingsService.Save()`. Takes effect
  on next login, same as Wpf (field is only read at startup).
- Along the way, fixed a pre-existing German locale typo
  (`app.startmassage` → `app.startmessage`) that made German players see
  the literal `[app.startmessage]` at boot, and fixed a real ordering bug in
  `Game.Start()` where the startup message was localized before
  `GameService.InitializeGame` had loaded any strings at all.
- Verified end-to-end: set → normalized to `http://...` → persisted to
  `Data/Misc/settings.json` → reloaded correctly on next run.

## Suggested order

1. Data sync fix (§1) — small, mechanical, and unblocks accurate testing of
   everything else (no point localizing or debugging behavior against a
   wrong world).
2. Server address setting (§3) — small, self-contained.
3. Localization sweep (§2) — large, incremental, can continue across many
   sessions; start with the `CombatSystem.cs` shared-localization fix since
   that's a one-file win, then work outward.
