using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace CrewPresets
{
    // Gravship-specific helpers. The launch dialog's crew is the ritual-assignment system;
    // animals/mechs are boarded in bulk by RitualBehaviorWorker_GravshipLaunch. We add
    // per-animal/mech choice as a per-engine EXCLUSION set (see BoardingExclusions): the
    // boarding panel unticks pawns, and a filter postfix removes them from pawnsToBoard at
    // launch. Filter-only, so the dialog's board-colony-animals/mechs checkboxes stay the
    // master switches.
    public static class GravshipPresets
    {
        private static readonly FieldInfo ritualTargetField = AccessTools.Field(typeof(RitualRoleAssignments), "ritualTarget");

        public static Building_GravEngine EngineFor(RitualRoleAssignments assignments)
        {
            if (assignments == null) return null;
            return ((TargetInfo)ritualTargetField.GetValue(assignments)).Thing?.TryGetComp<CompPilotConsole>()?.engine;
        }

        public static Building_GravEngine EngineById(int engineId)
        {
            foreach (Map map in Find.Maps)
            {
                foreach (Building_GravEngine e in map.listerBuildings.AllBuildingsColonistOfClass<Building_GravEngine>())
                {
                    if (e.thingIDNumber == engineId) return e;
                }
            }
            return null;
        }

        // Same population the vanilla gather in TryExecuteOn considers.
        public static List<Pawn> BoardablePawns(Map map, bool mechs)
        {
            List<Pawn> result = new List<Pawn>();
            if (map == null) return result;
            foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
            {
                if (p.Downed) continue;
                if (mechs ? p.IsColonyMech : p.IsColonyAnimal) result.Add(p);
            }
            return result;
        }

        public static bool IsBoardingAllowed(int engineId, Pawn pawn)
        {
            List<Pawn> excl = GameComponent_CrewPresets.Get.ExclusionsFor(engineId);
            return excl == null || !excl.Contains(pawn);
        }

        // The group a launch setup currently matches: crew (non-required spectators) equals
        // the group's humanlike members, and the allowed animals/mechs equal the group's.
        // Pilot/copilot are chosen in the dialog itself and ignored here.
        public static bool MatchesGroup(CrewGroup group, RitualRoleAssignments assignments, Building_GravEngine engine)
        {
            List<Pawn> presentCrew = group.members
                .Where(p => p != null && !p.Dead && p.RaceProps.Humanlike && assignments.AllCandidatePawns.Contains(p))
                .ToList();
            List<Pawn> spectators = assignments.SpectatorsForReading
                .Where(p => !PresetApplier.IsRequired(assignments, p))
                .ToList();
            if (spectators.Count != presentCrew.Count || !presentCrew.All(p => spectators.Contains(p) || assignments.PawnParticipating(p)))
                return false;
            if (engine == null) return presentCrew.Count > 0;
            List<Pawn> allowed = CurrentlyAllowed(engine);
            List<Pawn> groupBoardable = group.members
                .Where(p => p != null && !p.Dead && !p.Downed && p.Spawned && p.Map == engine.Map && (p.IsColonyAnimal || p.IsColonyMech))
                .ToList();
            if (allowed.Count != groupBoardable.Count) return false;
            return groupBoardable.All(allowed.Contains);
        }

        public static List<Pawn> CurrentlyAllowed(Building_GravEngine engine)
        {
            List<Pawn> all = BoardablePawns(engine.Map, mechs: false);
            all.AddRange(BoardablePawns(engine.Map, mechs: true));
            return all.Where(p => IsBoardingAllowed(engine.thingIDNumber, p)).ToList();
        }

        // Current launch setup as preset members: non-required crew + allowed animals/mechs.
        public static List<Pawn> CaptureMembers(RitualRoleAssignments assignments, Building_GravEngine engine)
        {
            List<Pawn> result = assignments.SpectatorsForReading
                .Where(p => p.RaceProps.Humanlike && !PresetApplier.IsRequired(assignments, p))
                .ToList();
            if (engine != null) result.AddRange(CurrentlyAllowed(engine));
            return result;
        }
    }

    // Removes excluded animals/mechs from the boarding set the moment vanilla builds it.
    // TryExecuteOn runs in the synced ritual-start path, so this is deterministic in MP.
    [HarmonyPatch(typeof(RitualBehaviorWorker_GravshipLaunch), nameof(RitualBehaviorWorker_GravshipLaunch.TryExecuteOn))]
    public static class Patch_GravshipBoardingFilter
    {
        public static void Postfix(TargetInfo target)
        {
            Building_GravEngine engine = target.Thing?.TryGetComp<CompPilotConsole>()?.engine;
            if (engine?.pawnsToBoard == null) return;
            List<Pawn> excl = GameComponent_CrewPresets.Get.ExclusionsFor(engine.thingIDNumber);
            if (excl.NullOrEmpty()) return;
            engine.pawnsToBoard.RemoveWhere(p => excl.Contains(p));
        }
    }

    // Companion panel docked to the right of the gravship launch dialog (vanilla or the MP
    // proxy): per-animal/mech boarding checkboxes. Toggles route through the synced
    // PresetActions.SetBoardingExcluded so MP clients stay in step.
    public class Window_GravshipBoarding : Window
    {
        private const float RowHeight = 26f;
        private const float HeaderHeight = 26f;

        private readonly Window anchor;
        private readonly Building_GravEngine engine;
        private Vector2 scrollPos;

        public Window Anchor => anchor;

        public override Vector2 InitialSize => new Vector2(300f, 560f);
        protected override float Margin => 12f;

        public Window_GravshipBoarding(Window anchor, Building_GravEngine engine)
        {
            this.anchor = anchor;
            this.engine = engine;
            layer = WindowLayer.Dialog;
            draggable = false;
            doCloseX = false;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            closeOnCancel = false;
            absorbInputAroundWindow = false;
            forcePause = false;
            preventCameraMotion = false;
            soundAppear = null;
            soundClose = null;
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            if (anchor == null || !Find.WindowStack.IsOpen(anchor) || engine == null || engine.Destroyed)
            {
                Close(doCloseSound: false);
                return;
            }
            windowRect.x = Mathf.Min(anchor.windowRect.xMax + 2f, UI.screenWidth - windowRect.width);
            windowRect.y = anchor.windowRect.y;
        }

        private const string NoteText = "Untick animals or mechs to leave them behind. The dialog's board-colony checkboxes are the master switches.";

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), "Boarding");
            Text.Font = GameFont.Small;
            GUI.color = Color.gray;
            float noteHeight = Text.CalcHeight(NoteText, inRect.width);
            Widgets.Label(new Rect(0f, 32f, inRect.width, noteHeight), NoteText);
            GUI.color = Color.white;
            float top = 32f + noteHeight + 8f;

            List<Pawn> animals = GravshipPresets.BoardablePawns(engine.Map, mechs: false);
            List<Pawn> mechs = GravshipPresets.BoardablePawns(engine.Map, mechs: true);

            Rect outRect = new Rect(0f, top, inRect.width, inRect.height - top);
            float viewHeight = SectionHeight(animals) + (ModsConfig.BiotechActive ? SectionHeight(mechs) : 0f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, viewHeight);
            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);
            float y = 0f;
            DrawSection(ref y, viewRect.width, "Animals", animals, mechsKind: false);
            if (ModsConfig.BiotechActive)
                DrawSection(ref y, viewRect.width, "Mechs", mechs, mechsKind: true);
            Widgets.EndScrollView();
        }

        private static float SectionHeight(List<Pawn> pawns)
        {
            return pawns.Count == 0
                ? HeaderHeight + RowHeight + 8f // header + the "none" row
                : HeaderHeight + pawns.Count * RowHeight + 8f;
        }

        private void DrawSection(ref float y, float width, string label, List<Pawn> pawns, bool mechsKind)
        {
            GUI.color = Color.gray;
            Widgets.Label(new Rect(0f, y + 2f, width - 110f, 22f), label);
            GUI.color = Color.white;
            if (pawns.Count > 0)
            {
                if (Widgets.ButtonText(new Rect(width - 104f, y, 50f, 22f), "All"))
                    PresetActions.SetBoardingAllForKind(engine.thingIDNumber, mechsKind, board: true);
                if (Widgets.ButtonText(new Rect(width - 50f, y, 50f, 22f), "None"))
                    PresetActions.SetBoardingAllForKind(engine.thingIDNumber, mechsKind, board: false);
            }
            y += HeaderHeight;
            if (pawns.Count == 0)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(8f, y + 2f, width - 8f, 22f), "(none in colony)");
                GUI.color = Color.white;
                y += RowHeight + 8f;
                return;
            }
            foreach (Pawn p in pawns)
            {
                bool allowed = GravshipPresets.IsBoardingAllowed(engine.thingIDNumber, p);
                bool newAllowed = allowed;
                // paintable: click-drag over rows toggles them all, like the transferable widgets
                Widgets.CheckboxLabeled(new Rect(8f, y, width - 8f, 24f), p.LabelShortCap, ref newAllowed, paintable: true);
                if (newAllowed != allowed)
                    PresetActions.SetBoardingExcluded(engine.thingIDNumber, p, !newAllowed);
                y += RowHeight;
            }
            y += 8f;
        }
    }
}
