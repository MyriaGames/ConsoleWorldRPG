# Myria — Console Client

Myria is a text-based fantasy RPG. This repository (`Myria.Console`) is its terminal/console
client: you create a character (choosing from multiple playable races and classes), explore a
room-based world of cities, forests, caves and dungeons, fight monsters, complete quests, gather
and craft, and — in multiplayer — chat, party up, trade, and join guilds with other players on a
shared realm server.

## Features

Grounded in the actual `Commands/` and shared `Myria.Lib` data (`Data/common/*.json`) that ship
with this client:

- **Character creation** — pick one of several races (e.g. Myralu, Rotuka, Iymva, Zalu, …), each
  with its own stat bonuses/growth and some race-specific class restrictions, and one of twelve
  classes (Archer, Hunter, Knight, Fighter, Barbarian, Cleric, Rogue, ElementalMage, ArcanMage,
  Druid, SoulsKnight, RunicMage).
- **Exploration** — a room-based world (`look`, `move <direction>`) spanning cities, forests,
  caves and dungeons, with an in-game map (`map`, `map world`) and an NPC interaction system
  (`go to <npc>`).
- **Combat** — turn-based fights against monsters, including multiplayer group combat, with loot
  drops and lootable corpses (`loot`, `look corpses`).
- **Inventory & equipment** — equip/unequip items (`equip`, `unequip`), manage your inventory.
- **Skills** — a skill system with skill slots and skill fusion/combination (`skills`, `slots`,
  `combine`).
- **Quests, jobs & gathering** — a quest manager, job/profession system with daily ticks and
  gathering bonuses, resource gathering (`gather <resource>`), and crafting via recipes.
- **Day/night cycle** — an in-game day cycle that advances time and drives daily job ticks and
  penalties.
- **Multiplayer social systems** (via the realm server over SignalR) — live chat, friends and
  block lists (`friend list`, `friend requests`), parties (`party`, invite/accept/kick), player
  shops (`shop visit <name>`), player-to-player trading (`trade accept <id>`), and guilds
  (creation, ranks, invites, rookies).
- **Singleplayer mode** — play entirely offline against a local, file-based save with no server
  required.

## Architecture

Myria is split across several repositories under the `MyriaGames` GitHub organization:

- **[Myria.Console](https://github.com/MyriaGames/ConsoleWorldRPG)** (this repo) — the terminal
  client, built on .NET's `System.Console`.
- **[Myria.Lib](https://github.com/MyriaGames/MyriaLib)** — the shared game-logic library.
  Nearly all core logic (entities, races/classes, rooms, quests, jobs, combat, skills, loot,
  crafting, localization, and the shared game data under `Data/common`) lives here and is
  referenced by every client as a project reference, not duplicated in this repo.
- **[MyriaAuthServer](https://github.com/MyriaGames/MyriaAuthServer)** — the authentication
  server: account registration/login and realm listing.
- **[MyriaServer](https://github.com/MyriaGames/MyriaServer)** — the realm/world server: real-time
  multiplayer gameplay (chat, parties, trading, guilds, group combat, character persistence) over
  REST and SignalR.
- **[MyriaRPG](https://github.com/MyriaGames/MyriaRPG)** — a sibling WPF desktop client for the
  same backend and shared library.
- **[MyriaWorld](https://github.com/MyriaGames/MyriaWorld)** — an in-development MonoGame client,
  also built on the same backend and shared library.

This client can run entirely standalone in singleplayer mode, or connect to an Auth/Realm server
pair for multiplayer.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (the project targets `net8.0`).

## Getting started

This client references `Myria.Lib` via a relative project reference
(`../Myria.Lib/Myria.Lib.Core/Myria.Lib.Core.csproj`), so clone `Myria.Lib` as a sibling directory
next to `Myria.Console`:

```
some-folder/
├── Myria.Console/   (this repo)
└── Myria.Lib/
```

```bash
git clone https://github.com/MyriaGames/ConsoleWorldRPG.git Myria.Console
git clone https://github.com/MyriaGames/MyriaLib.git Myria.Lib
cd Myria.Console
dotnet build
dotnet run
```

On launch you'll get a menu with the following options:

- **Singleplayer** — plays immediately against a local save file (`Data/users`,
  `Data/saves`); no server connection is made.
- **Login** / **Register** — authenticates against the configured Auth server, then lets you pick
  an available realm (world server) to play on before selecting or creating a character.
- **Settings** — change the UI language or the configured server address.

## Configuration

The server address (used for Login/Register) is set in-app via the **Settings** menu on the main
menu, not through a config file — type a bare `host:port` or a full `http(s)://` URL. It defaults
to a local `http://localhost:5050` for development. Any address you type that isn't
`localhost`/`127.0.0.1`/`::1` is normalized to `https://` automatically, so credentials aren't
sent in the clear when pointing at a remote server. Since most operators run self-signed
certificates, the client uses trust-on-first-use certificate pinning per server address rather
than rejecting the connection outright.

## Legal / Privacy

Multiplayer mode sends your username and a hashed password to the Auth server and creates an
account there. The `Legal/` folder contains the real, currently-in-effect legal documents for the
Myria project (Austrian/GDPR-scoped):

- [`Legal/Impressum.md`](Legal/Impressum.md) — legal notice / operator disclosure.
- [`Legal/Datenschutzerklaerung.md`](Legal/Datenschutzerklaerung.md) — privacy policy, covering
  what account and gameplay data is processed and your GDPR rights.
- [`Legal/Nutzungsbedingungen.md`](Legal/Nutzungsbedingungen.md) — terms of use for creating and
  playing on an account.

Note: unlike the WPF client, this Console client does not currently have a self-service "delete my
account" option. To delete a multiplayer account created through this client, use the WPF client's
account deletion feature, or contact the operator by email — see the privacy policy above for
details.

## License

MIT — see [LICENSE](LICENSE).

## Status

Myria is an active alpha-stage hobby project. Features, save formats, and network protocols may
change without notice, and no uptime or data-permanence guarantees are made for any hosted Auth or
Realm server.
