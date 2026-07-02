# Design — Crew Presets

Status: **IN DEVELOPMENT** (started 2026-07-01; scaffold + storage layer built).
Sibling to **Transporter Loadouts** (`../ModIdeas/TransporterLoadouts-DESIGN.md`) — split out 2026-07-01:
crew references save-specific pawns; loadouts are global item-defs, so they're separate mods that
*pair* if both installed.

## Concept
Save named groups of colonists/animals/mechs ("Mining Crew", "Strike Team", "Pack Train") once, then
apply them with one click in any pawn-selection dialog — instead of hand-picking the same pawns every
time. Born from the MP pain of "my pawns are mixed in with my friend's" when loading a shuttle.

## Scope: standalone mod, no dependencies
Patches the **vanilla** selection dialogs, so it helps every transporter/launch (drop pods, Royalty/Core
shuttle, gravships, pit gates, caravans, our shuttles, other mods' craft). No Odyssey/Biotech dependency.

## What a group is
- A **named list of pawn references** — any mix of **colonists, animals, and mechs** (the dialogs list all
  three). Examples: "Mining Crew" (colonists), "Pack Train" (muffalo), "Strike Team" (mechanitor + mechs).
- **Per-save storage** (a `GameComponent` scribed with the save), like bills — pawn membership is inherently
  save-specific. Decided AGAINST a cross-save name-template layer (per-save only, simpler).

## Target dialogs (all use vanilla `TransferableOneWay` / `CountToTransfer`, each its own `Window`)
- `RimWorld/Dialog_LoadTransporters.cs` — shuttles/transporters. **START HERE.**
- `RimWorld/Dialog_FormCaravan.cs` — caravans.
- `RimWorld/Dialog_EnterPortal.cs` — pit gate / bunker / portal entry.
- `RimWorld.Planet/Dialog_SplitCaravan.cs` — caravan splitting.
- `RimWorld/Dialog_BeginGravshipLaunch.cs` — **SUPPORTED via a second mechanism** (2026-07-02; Luke:
  the big MP pain point). It subclasses `Dialog_BeginRitual` (ritual role assignments, not
  transferables). We patch the base `Dialog_BeginLordJob.DoWindowContents`, gated on the gravship
  ritual behavior, and apply via `TryAssignSpectate`/`RemoveParticipant` — which the Multiplayer mod
  syncs natively (and its `RitualBeginProxy` replacement dialog calls base, so the button shows in
  MP sessions too). See `CLAUDE.md` for the full notes.

## Mechanism
- **UI**: Harmony postfix on each dialog's `DoWindowContents` to add a small **Presets** control:
  "Save current selection as…", "Apply <group>", "Manage groups…".
- **Apply** = for each member, find its transferable in *this* dialog and set `CountToTransfer = 1`.
  Members not present → skipped, collected into one message: *"Couldn't add: Bob (off-map), Sara (dead)."*
  Never errors out.
- **Save current selection** = read the dialog's ticked pawns into a new named group (reuse the shuttle mod's
  `DialogAccess` AccessTools.FieldRefAccess pattern to reach the private `transferables`).
- **Editor window**: create/rename/delete; add/remove members under Colonists / Animals / Mechs headings.
- **Dead/absent**: greyed with one-click remove; genuinely-gone pawns lazily pruned; null refs handled on load.

## Capacity
Caps are per-type and **already owned by each dialog** (seat count / mech bandwidth / cargo mass). The preset
just ticks members; anything over a cap gets the dialog's existing red bar / over-cap block. No custom logic.

## Multiplayer
- **Group CRUD** mutates shared game state → `MP.RegisterSyncMethod` (MultiplayerAPI; ship the `0MultiplayerAPI`
  stub, as the shuttle mod does).
- **Applying** a group in a dialog is **local UI only** (just ticking; final Accept/launch is vanilla-synced).
- Deterministic generation; references sync by ID.

## Build order
1. `GameComponent` storing `List<CrewGroup>` (name + List<Pawn> members); `ExposeData` w/ `Scribe_References`.
2. Editor window (CRUD + members grouped by type).
3. Patch `Dialog_LoadTransporters`: Presets button → apply (skip-and-warn) + "save selection".
4. Extend to `Dialog_FormCaravan`, `Dialog_EnterPortal`, `Dialog_SplitCaravan`.
5. Verify + support `Dialog_BeginGravshipLaunch`.
6. MP sync pass on the CRUD methods.

## Open questions
- Confirm `Dialog_BeginGravshipLaunch` uses transferables (or how its crew picker works).
- Button placement/styling per dialog. Mod name + packageId.
