# CLAUDE.md — Crew Presets

A RimWorld 1.6 mod: save named groups of pawns (colonists/animals/mechs — "Mining Crew",
"Strike Team", "Pack Train") and apply them with one click in the vanilla pawn-selection
dialogs. This file is mod-specific; the **workspace** `../CLAUDE.md` (global RimWorld rules)
also applies — on conflict, this file wins.

Full design: `DESIGN.md` (in this folder). Sibling future mod: **Transporter Loadouts**
(`../ModIdeas/TransporterLoadouts-DESIGN.md`) — separate mod, pairs with this one if both installed.

## Scope rules
- **Standalone, no DLC/mod dependencies** (Harmony only). Patches the *vanilla* dialogs so it
  works for drop pods, shuttles, gravships-adjacent flows, caravans, and modded craft.
- Target dialogs (all vanilla `TransferableOneWay` / `CountToTransfer` based):
  `Dialog_LoadTransporters` (first), `Dialog_FormCaravan`, `Dialog_EnterPortal`,
  `Dialog_SplitCaravan`.
- **Gravship launch — SUPPORTED, same global groups** (Luke 2026-07-02: "the big one" for MP;
  groups stay global since animals/mechs selection works). `Dialog_BeginGravshipLaunch`
  subclasses `Dialog_BeginRitual` (role assignments, no transferables). Patch point is the
  BASE `Dialog_BeginLordJob.DoWindowContents`, gated on
  `assignments.Ritual?.behavior is RitualBehaviorWorker_GravshipLaunch` — this covers BOTH the
  vanilla dialog AND the Multiplayer mod's `RitualBeginProxy` replacement (it calls base).
  - **Crew**: group's humanlike members become the ritual spectators ("Crew" section).
    Pilot/copilot role holders, forced and required pawns are never touched — pilot/copilot
    are picked in the dialog's own role UI, not by presets.
  - **Animals/mechs (`GravshipPresets.cs`)**: vanilla boards ALL colony animals/mechs via two
    checkboxes (`RitualBehaviorWorker_GravshipLaunch.TryExecuteOn` fills
    `engine.pawnsToBoard`). We add per-pawn choice as a per-engine EXCLUSION set
    (`BoardingExclusions`, scribed in the GameComponent; exclusion-based so new tames default
    to boarding). `Window_GravshipBoarding` docks beside the launch dialog with per-pawn
    checkboxes + All/None; a `TryExecuteOn` postfix REMOVES excluded pawns from
    `pawnsToBoard` (filter-only → the vanilla checkboxes stay the master switches).
  - **MP:** `ApplyGroupToGravship(assignments, index)` is ONE synced method (MP serializes
    `RitualRoleAssignments` by session id — verified in MP's SyncDictDlc); boarding toggles
    (`SetBoardingExcluded`/`SetBoardingAllForKind`) are synced; `TryExecuteOn` runs in the
    synced ritual-start path so the filter is deterministic. MP does NOT sync raw
    `TryAssign` — never call it directly from UI. UI messages inside synced methods are
    gated by `MultiplayerCompat.ShowUiForThisClient`.
  - Apply is exact-set across all three kinds: crew = humanlike members; exclusions = every
    boardable animal/mech NOT in the group.

## Architecture (C#, `Source/`)
- `CrewGroup` (IExposable: name + `List<Pawn>` by `LookMode.Reference`) +
  `GameComponent_CrewPresets` (per-save storage, like bills; lazy-prunes null/dead refs).
- Apply = find each member's transferable in the open dialog, set `CountToTransfer = 1`;
  absent/dead members are skipped and reported in one message. Caps (seats/bandwidth/mass) are
  the dialog's own job — we only tick.
- Reach each dialog's private `transferables` via `AccessTools.FieldRefAccess` (the shuttle
  mod's `DialogAccess` pattern, see `../OdysseyShuttleVariants/Source/HarmonyPatches.cs`).
- **MP:** group CRUD mutates shared state → `MP.RegisterSyncMethod` (MultiplayerAPI stub
  `0MultiplayerAPI.dll` ships in `Assemblies/`, csproj copies it). Applying a preset in a
  dialog is local UI only (the final Accept is vanilla-synced).

## Conventions
- Def prefix (when defs are ever needed): `CrewPresets_`. Harmony ID: `luke.crewpresets`.
- Save discipline: once published, don't rename Scribe keys (`groups`, `name`, `members`).

## Build / deploy / test
- `cd Source && dotnet build -c Release` (.NET 10 SDK → net48). CopyToMod drops both DLLs
  into `Assemblies/` (gitignored).
- Deploy: copy the mod (minus `Source/`, `obj/`, `bin/`, `.git/`) into
  `D:\SteamLibrary\steamapps\common\RimWorld\Mods\CrewPresets` (robocopy `/MIR /XD Source obj bin .git`).
  C# changes need a full game restart.
- Reference first: `../../RimWorld-Decompiled/Assembly-CSharp/` (read-only) and `../Docs/`.

## Build order (from DESIGN.md)
1. ✅ Storage: `CrewGroup` + `GameComponent_CrewPresets`.
2. ✅ Editor window (CRUD + members grouped Colonists/Mechs/Animals).
3. ✅ Patch `Dialog_LoadTransporters`: preset chips + "Presets..." menu (apply / save selection / manage).
4. ✅ Extended to `Dialog_FormCaravan`, `Dialog_EnterPortal`, `Dialog_SplitCaravan` (shared
   `PresetUI.DrawHeaderControls` + per-dialog `DialogHooks`: transferables field, `tab` enum
   [Pawns=0, chips draw on Pawns tab only], cache-dirty method — FormCaravan
   `Notify_TransferablesChanged`, Load/Split `CountToTransferChanged`, EnterPortal none).
5. ✅ MP sync pass on the CRUD methods (`MultiplayerCompat`).

## UX decisions (Luke, 2026-07-01/02)
- **Apply = exact-set**, not additive: applying a group unticks every non-member pawn
  (items untouched). Skip-and-warn for dead/absent members still applies.
- **One "Presets..." button, NO per-group chips** (chips tried 2026-07-01, removed on Luke's
  feedback — only one fit beside the centered title). The button label becomes
  "Preset: <name>" tinted green when the selection exactly matches a group.
- **Left-click** = menu (Apply <group> / Save current selection as... / Manage groups...).
  **Right-click a group ROW in that menu** = overwrite it with the current selection
  (`FloatMenuOption_CrewGroup` intercepts button-1 before base DoGUI — vanilla FloatMenuOption
  fires on ANY mouse button). Right-click on the button itself does nothing (Luke 2026-07-02).
- Controls show on the **Pawns tab only** (transferable dialogs).

## Remaining
- In-game test of all four dialogs; MP smoke-test of group CRUD sync.
- First commit + GitHub repo when Luke asks.
