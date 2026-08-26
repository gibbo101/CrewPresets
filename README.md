# Crew Presets

A RimWorld 1.6 mod. Save named groups of colonists, animals and mechs — "Mining Crew",
"Strike Team", "Pack Train" — and apply them with one click in the vanilla pawn-selection
dialogs, instead of hand-picking the same pawns every time.

**Steam Workshop:** https://steamcommunity.com/sharedfiles/filedetails/?id=3756372833

## Features

- Save the current selection as a named group from inside any supported dialog.
- Apply a group with one click. Apply is *exact-set*: non-members are unticked, items untouched.
- Build groups on a screen that shows every pawn's skills, passions and equipped weapon,
  laid out like the Work tab. Click a skill column to sort by it; drag the tick column to
  add or remove a run of pawns in one sweep.
- Groups are stored in the save file, like bills.
- Optional skill entries in the "Sort by" dropdown of the transfer dialogs (mod setting).

## Supported dialogs

| Dialog | Covers |
| --- | --- |
| `Dialog_LoadTransporters` | Drop pods, shuttles, modded craft |
| `Dialog_FormCaravan` | Caravans |
| `Dialog_SplitCaravan` | Caravan splitting |
| `Dialog_EnterPortal` | Pit gates / portals |
| `Dialog_BeginGravshipLaunch` | Gravship launch (crew, plus per-pawn animal/mech boarding) |

## Requirements

- RimWorld 1.6
- [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)

No DLC required. No other mod dependencies.

## Multiplayer

Built with the [Multiplayer](https://github.com/rwmt/Multiplayer) mod in mind — group
creation, renaming, deletion and membership edits are synced, and gravship apply/boarding
runs through synced methods. Runs fine without Multiplayer installed (the API stub reports
`MP.enabled == false` and the sync code no-ops).

## Building

```
cd Source
dotnet build -c Release
```

Any modern .NET SDK compiles the `net48` target — no .NET Framework Developer Pack needed.
`Krafs.Rimworld.Ref` supplies the game reference assemblies, so no path to your RimWorld
install is required. The build drops `CrewPresets.dll` and the `0MultiplayerAPI.dll` stub
into `Assemblies/` (gitignored — it is a build artifact).

To test in-game, copy the mod folder (minus `Source/`, `obj/`, `bin/`, `.git/`) into your
RimWorld `Mods/` directory. C# changes require a full game restart.

## Companion mods

- **[Transporter Loadouts](https://steamcommunity.com/sharedfiles/filedetails/?id=3756377821)** —
  every transporter remembers its own default cargo. Crew Presets picks the people; that one
  picks the freight.
- **[Odyssey Shuttle Variants](https://steamcommunity.com/sharedfiles/filedetails/?id=3755775382)** —
  buildable, role-distinct craft for Odyssey.

## License

[MIT](LICENSE)
