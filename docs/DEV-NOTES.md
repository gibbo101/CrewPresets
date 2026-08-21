# Development notes (archived Claude session log)

Design decisions and session-by-session progress, archived 2026-07-02 from Claude Code
session memory into the repo. Append-style log — newer entries may supersede older ones.

**BOTH MODS NOW EXIST AS REAL FOLDERS+REPOS.** Crew Presets = feature-complete SP, first commit done
(bac40ad). Mod B (Transporter Loadouts) GRADUATED from ModIdeas 2026-07-02 — its own folder+git repo at
`TransporterLoadouts/`, DESIGN.md moved in (ModIdeas/ now empty), SLICE 1 built+deployed+committed
(dd1e32c). See the "TRANSPORTER LOADOUTS" section near the bottom for its state.

**SCAFFOLD BUILT 2026-07-01 at `CrewPresets/`** (name "Crew Presets", packageId `luke.crewpresets`,
Harmony ID `luke.crewpresets`, def prefix `CrewPresets_` if ever needed): About.xml (Harmony dep only,
loadAfter rwmt.Multiplayer optional) + Manifest 0.1.0 + csproj (shuttle-mod recipe: net48/C#12/
Krafs.Rimworld.Ref 1.6.*/Lib.Harmony ExcludeAssets=runtime/MultiplayerAPI 0.5.0 + CopyToMod ships
0MultiplayerAPI stub) + own git repo (identity set; NO commits yet — Luke commits when he asks; no GitHub
remote yet) + mod CLAUDE.md. DESIGN doc MOVED `ModIdeas/CrewPresets-DESIGN.md` → `CrewPresets/DESIGN.md`.
**Build order step 1 DONE + builds clean:** `Source/CrewGroup.cs` = `CrewGroup` (IExposable, name +
List<Pawn> LookMode.Reference, PostLoadInit null-prune, AvailableMembers helper) +
`GameComponent_CrewPresets` (per-save, LookMode.Deep groups) + HarmonyPatches.cs bootstrap (PatchAll).
**GRAVSHIP QUESTION RESOLVED (2026-07-01, decompile): `Dialog_BeginGravshipLaunch` subclasses
`Dialog_BeginRitual` — ritual role-ASSIGNMENT system, NOT transferables → OUT of v1** (4 transferable
dialogs only; gravship = possible later extension via ritual assignments).
**FIRST SLICE BUILT SAME SESSION (steps 2, 3 AND 5 for Dialog_LoadTransporters) — builds clean, DEPLOYED
to D:\...\Mods\CrewPresets 2026-07-01 23:40, NOT yet in-game-tested, NOT committed.** Files:
`PresetActions.cs` (static synced CRUD — Create/Rename/Delete/AddMember/RemoveMember, groups addressed by
INDEX since list order is shared state; + `PresetApplier` = LOCAL apply: find member's transferable,
AdjustTo(1), skip-and-warn "Couldn't add: X (dead), Y (unavailable)", then invoke private
`CountToTransferChanged` via AccessTools to dirty mass caches) + `Dialog_NameCrewGroup.cs` (name-entry,
callback-based so names route through synced actions — vanilla Dialog_Rename rejected: writes
RenamableLabel directly = MP-unsafe) + `Dialog_ManageCrewGroups.cs` (editor: New/Rename/Add.../Delete +
member rows under Colonists/Mechs/Animals headings, dead/gone grayed, add-menu =
PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction) + HarmonyPatches.cs
(`Patch_LoadTransporters_Presets`: DoWindowContents postfix draws "Presets..." button TOP-RIGHT at
inRect.xMax-150 — postfix inRect is the method's mutated/contracted local, window-space coords, clear of
the shuttle mod's top-LEFT readout) + `MultiplayerCompat.cs` (registers the 5 CRUD sync methods).
**FEEDBACK ROUND 1 (Luke in-game 2026-07-01/02) — 3 changes shipped (built+deployed 00:04, needs
game restart; NOT committed):** (1) **apply = EXACT-SET** (Luke's bug report: "MJ doesn't get de-selected"
— applying a group now unticks all non-member PAWNS, items untouched; additive rejected); (2) **group
CHIPS on the dialog header** (Luke: see lists without pressing button + show active preset) — one button
per group right-to-left beside "Presets...", green-tinted when the selection EXACTLY matches the group's
present members (`PresetApplier.MatchesSelection`), overflow chips still in the menu (minX =
inRect.center+230 keeps clear of centered title); (3) chips/button on **Pawns tab only** (private `tab`
enum via reflection, Pawns=0 in all four dialogs). (4) **STEP 4 DONE same round (Luke asked for
caravans):** generalized to shared `PresetUI.DrawHeaderControls` + per-dialog `DialogHooks(type,
notifyMethodName)` — now patches all four: Dialog_LoadTransporters + Dialog_SplitCaravan (dirty method
`CountToTransferChanged`), Dialog_FormCaravan (`Notify_TransferablesChanged`; its transferables field is
PUBLIC), Dialog_EnterPortal (NO cache-dirty method, pass null). All four dialogs share the same header
shape (35px centered title, inRect local ContractedBy(17) → postfix inRect.xMax = width-17).
DEPLOY GOTCHA: robocopy retries while RimWorld is open (DLL user-mapped/locked) — use /R:20 /W:5; verify
with Get-FileHash source-vs-deployed.
**FEEDBACK ROUND 2 (Luke 2026-07-02, built+deployed 00:16, NOT committed):**
(1) **CHIPS REMOVED** (Luke: only "Group 1" fit with >1 group) → ONE "Presets..." button; label becomes
"Preset: <name>" + green tint when selection exactly matches a group (keeps the active-indicator ask).
LEFT-click = Apply/Save-as/Manage menu; **RIGHT-click = "Overwrite <group> with current selection"**
(Luke's final spec — evolved from "add selected to group"; his flow: tick extra pawn → right-click →
overwrite). Right-click handled BEFORE ButtonText (MouseDown button==1 + Event.Use). New synced
`PresetActions.SetGroupMembers(index, List<Pawn>)` + registered in MultiplayerCompat.
(2) **GRAVSHIP SUPPORT SHIPPED (Luke: "the big one, solves the big MP painpoint").** KEY ARCHITECTURE
(from decompile + MP source read): patch BASE `Dialog_BeginLordJob.DoWindowContents` (Dialog_BeginRitual
does NOT override it; MP's RitualBeginProxy override CALLS base → one postfix covers SP + MP), gate on
`assignments.Ritual?.behavior is RitualBehaviorWorker_GravshipLaunch` (protected `assignments` field via
AccessTools). Apply mutates ONLY via `RitualRoleAssignments.TryAssignSpectate`/`RemoveParticipant` —
**the MP mod itself registers BOTH as sync methods (SyncDelegates.cs:327-328) → our bulk-apply syncs for
FREE in MP ritual sessions.** Never remove Forced()/ExtraRequiredPawns (launch organizer); ignore them in
the active-match; animals/mechs in a group are skipped with a note (they board via the dialog's
bulk checkboxes, not per-pawn assignment); NEVER branch on TryAssignSpectate's return (MP intercepts →
returns default locally, executes later). Button drawn top-right of ritual header (inRect.xMax-160, y=0).
**FEEDBACK ROUND 3 (Luke 2026-07-02, built+deployed 00:36, NOT committed) — big gravship rework:**
(1) Right-click moved OFF the button ONTO the menu's group rows (`FloatMenuOption_CrewGroup` subclass:
intercepts Event button==1 in DoGUI override before base — NEEDED because vanilla FloatMenuOption fires
its action on ANY mouse button; returning true closes the menu).
(2) **Gravship presets pivot (supersedes the started-then-reverted separate GravshipCrew store):** Luke
first wanted separate gravship groups w/ pilot+copilot, THEN pivoted: "gravship preset could stay global
with other presets if you can make selecting animals and mechs a thing" + wants an animals/mechs SECTION
in the launch UI. FINAL ARCHITECTURE: (a) ONE global group list everywhere; (b) gravship apply =
`PresetActions.ApplyGroupToGravship(assignments, index)` — ONE SYNCED method (MP's SyncDictDlc
serializes RitualRoleAssignments BY SESSION ID → registerable param; MP does NOT sync raw
role-TryAssign, never call from UI) — crew=humanlike members via TryAssignSpectate/RemoveParticipant
(only touches SPECTATORS; pilot/copilot role holders + Forced/ExtraRequired never removed —
pilot/copilot stay MANUAL in the dialog's role UI); (c) **animals/mechs = per-engine EXCLUSION set**
(`BoardingExclusions` scribed in GameComponent, keyed engine.thingIDNumber; exclusion-based so new tames
default-board like vanilla); (d) **`Window_GravshipBoarding` panel docks right of the launch dialog**
(anchor-follow in WindowUpdate; auto-closes with dialog; per-pawn checkboxes + All/None; toggles =
synced SetBoardingExcluded/SetBoardingAllForKind); (e) **Harmony postfix on
`RitualBehaviorWorker_GravshipLaunch.TryExecuteOn`** removes excluded pawns from `engine.pawnsToBoard`
— FILTER-ONLY (never adds) so vanilla's board-colony-animals/mechs checkboxes stay master switches;
TryExecuteOn runs in the synced ritual-start path → MP-deterministic. (f) Apply = exact-set on all 3
kinds (exclusions := all boardable minus group). Gravship dialog button uses
GravshipPresets.CaptureMembers/MatchesGroup (crew + allowed animals/mechs) for save/overwrite/active.
KEY DECOMPILE FACTS: gravship ritual roles = pilot (required, max1) + copilot (optional, max1), both
countsAsParticipant=false but PawnParticipating counts role-holders; spectators list = the "Crew"
section; RitualRoleAssignments.ritualTarget (private) → Thing.TryGetComp<CompPilotConsole>().engine.
NEXT: (1) Luke tests: transferable dialogs (exact-set, active-name button, right-click-row overwrite) +
gravship (panel, preset apply, launch with exclusions — verify excluded animals actually stay behind);
(2) MP smoke-test: CRUD + gravship session apply + boarding toggles; (3) commit + GitHub when Luke asks.
KNOWN LIMITATIONS: stale-index race on simultaneous MP group edits (benign); exclusions computed at
apply-time (new tames after apply will board); boarding panel lists spawned non-downed only.
**FEEDBACK ROUND 4 (Luke 2026-07-02 morning, polish; built+deployed ~09:15+):** (1) boarding-panel note
height now Text.CalcHeight-measured (was clipped at fixed 44px); (2) empty Animals/Mechs sections show
header + gray "(none in colony)" instead of vanishing (Luke thought it looked broken); (3) right-click
overwrite now opens a Dialog_MessageBox.CreateConfirmation FIRST (Luke: "should bring up an option, not
auto save") — was instant-overwrite; (4) boarding checkboxes `paintable: true` (Widgets.CheckboxLabeled
8th param) = vanilla click-drag painting like transferable lists (Luke's ask; each painted row still
fires the synced toggle). Right-click-on-menu-rows CONFIRMED WORKING in-game by Luke (incl gravship);
gravship panel + mech listing CONFIRMED showing. FloatMenu fact: it closes on ANY unhandled MouseDown
(FloatMenu.cs:293) AFTER options draw — our row-level intercept runs first, so it works.
**SESSION END 2026-07-02: Luke "I think its all working"** — SP feature set confirmed good (transferable
dialogs + gravship + boarding panel + all round-4 polish). DLL deploy trick that worked: robocopy
/R:60 /W:5 in background retries until Luke exits the game, then verify with Get-FileHash.
STILL OUTSTANDING: (a) **FIRST COMMIT DONE 2026-07-02 (commit bac40ad, `master`, local-only, still NO
GitHub remote)** — squashed the whole working mod into one initial commit; 13 files (About x2, CLAUDE.md,
DESIGN.md, 9 Source/*.cs incl PresetActions/GravshipPresets/FloatMenuOption_CrewGroup/Dialog_Manage/
Dialog_Name/HarmonyPatches/MultiplayerCompat/CrewGroup + csproj); Assemblies/+bin/obj gitignored & clean.
GitHub repo creation still deferred to Workshop-publish time; (b) MP 2-client test (group CRUD sync,
gravship session apply, boarding toggles); (c) Workshop prep eventually (Preview.png, About polish).

**Mod A — Crew Presets:** save named groups of pawns (colonists/animals/mechs — "Mining Crew", "Strike Team",
"Pack Train") once, apply with one click in any pawn-selection dialog. From the MP pain of "my pawns mixed with
my friend's." PER-SAVE (GameComponent, pawn refs).
**Mod B — Transporter Loadouts:** **per-shuttle-INSTANCE default cargo** — each shuttle remembers its own manifest
(e.g. Shuttle 1 = 50 meals + 100 chemfuel; Shuttle 2 = mortar + 50 incendiary shells), pre-filled when you open
ITS load dialog. Standalone, Harmony-only, no Odyssey dep.


**SKILL SORTERS (2026-08-20, built + deployed, NOT committed, NOT yet in-game-tested)** — from a
Workshop comment (Taylor_MH): wants Skill Sort-style "find colonists by highest Cooking" but Skill Sort
doesn't work with Cargo Shuttles. KEY FINDING: no dialog patch needed at all —
`TransferableUIUtility.OpenSorterChangeFloatMenu` builds the "Sort by" dropdown straight from
`DefDatabase<TransferableSorterDef>.AllDefsListForReading`, and `TransferableOneWayWidget` (shared by ALL
four transferable dialogs + trade) applies it as `OrderBy(sorter1).ThenBy(sorter2)`. So registering defs
covers transporters/shuttles, form caravan, split caravan and enter portal in one shot — including
modded craft, since they all reuse Dialog_LoadTransporters' widget.
IMPLEMENTATION (`Source/SkillSorters.cs`): defs are BUILT IN CODE, not XML, because (a)
`TransferableSorterDef.comparerClass` is instantiated by `Activator.CreateInstance` = parameterless only,
so a per-skill comparer can't come from it, and (b) the mod setting has to add/remove them live. We set
the def's private cached `comparerInt` field via AccessTools, so `comparerClass` is never consulted (left
null). Registration happens in a `[StaticConstructorOnStartup]` (SkillDefs are loaded by then); removal
uses the private `DefDatabase<T>.Remove` via AccessTools. `TransferableComparer_Skill` sorts DESCENDING
(highest first — vanilla comparers are ascending, but "highest Cooking" is the whole point); items,
animals, mechs and disabled skills all evaluate to -1 and sink.
SETTING (`Source/CrewPresetsMod.cs` — first `Mod`/`ModSettings` classes in this mod): `skillSorters`,
default ON, checkbox under "Crew Presets" in mod options. Rationale for the toggle: the sorter dropdown
is GLOBAL, so the ~12 skill entries also show up in the TRADE dialog where they're noise.
`WriteSettings` calls `SkillSorters.ApplySetting()` so toggling takes effect without a restart (safe:
the options window can't be open while a modal transferable dialog is).
Also added a feature bullet to About.xml's Workshop description.
STILL OPEN from the same comment thread: Luke's public reply promised pawn SKILLS SHOWN ON THE GROUP
EDITOR (`Dialog_ManageCrewGroups` rows + the "Add..." float menu labels) — not started.

**QUEUED (Luke 2026-08-21): bump `RimWorld.MultiplayerAPI` 0.5 -> 0.6** in `Source/CrewPresets.csproj`.
Player.log warns `Mod Crew Presets uses an older API version (mod: 0.5, current: 0.6)`. Nothing is broken
at 0.5, so **do it AFTER the current MP test run finishes** - the bump reships `0MultiplayerAPI.dll` via
the CopyToMod target, and swapping it mid-playthrough risks disturbing the live co-op session.

**SKILL TABLE ON THE GROUP SCREENS (2026-08-21, tested in-game green, committed)** — the Workshop
request resolved properly. The sorters (previous entry) stay, but they were NOT what Luke wanted: his
Workshop reply promised skills on the group screen itself. Shared `CrewSkillTable` draws a Work-tab
style grid used by both group dialogs: portrait + "name, title", equipped-weapon icon column (mirrors
`TransferableOneWayWidget.DrawEquippedWeapon` - icon + LabelCap tooltip), then one box per SkillDef.
Boxes mirror `WidgetsWork.DrawWorkBoxBackground` (same WorkBoxBG textures, same 0-4/4-14/14-20
thresholds) keyed to ONE skill's level instead of a work type's relevant-skill average; passion flame
in the bottom-right quadrant, TINTED YELLOW (the workbox art is greyscale, so tinting beats swapping in
the bigger character-sheet icons). Metrics copied from vanilla: 39px columns, 25px box, 50px header
(`PawnColumnWorker_WorkPriority.GetMinWidth`). Staggered two-row headers with a rule dropping to the
column, since skill names are wider than 39px.
`Dialog_PickCrewMembers` REPLACES the old add-member float menu: whole colony, paintable
`Widgets.Checkbox(paintable: true)` tick/cross column (vanilla's green-tick/red-X art + drag-painting),
click a skill header to sort. **Edits are BUFFERED in a local HashSet and written on "Done" via one
synced `SetGroupMembers`** - Luke's bug: closing with the X still saved. Bonus: a drag across ten rows
is now one sync command, not ten. Button renamed "Add..." -> "Members" (the screen removes too).
LAYOUT GOTCHAS: (1) `Window.InitialSize` counts the window's own 18px margins, which
`DoWindowContents` never sees - miss them and the last column falls off the right edge; (2) the skill
header must be drawn PER GROUP on the manage screen, not pinned at the top - pinned, it floats over
nothing when a group is empty and lines up with only the first group; (3) sort state must be per group
(was static = every group sorted together); (4) group divider goes BETWEEN groups, not under the group
name; (5) both dialogs need an explicit "Done" - the close X alone reads as cancel.
ALSO: `RimWorld.MultiplayerAPI` 0.5 -> 0.6 (the queued bump; compiled with no code changes, stub
reships via CopyToMod).
CONSIDERED AND DROPPED: the same grid on the transfer dialogs' Pawns tab. `TransferableOneWayWidget.DoRow`
packs columns RIGHT-TO-LEFT (11 `width -=` steps) and gives the remainder to the name block - on
Load Transporters ~519px of 1024 is left and the grid needs 468, so it needs the dialog widened plus a
DoRow postfix re-deriving vanilla's column arithmetic from private draw* flags. Fragile across game
updates and collides with other dialog patchers (incl. our own OdysseyShuttleVariants). Luke: "leave as
it is". If revisited, wire our column headers to the widget's private `sorter1` and delete the dropdown
sorter entries + their setting.
