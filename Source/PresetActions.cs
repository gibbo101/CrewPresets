using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace CrewPresets
{
    // Group CRUD. These mutate shared game state, so in Multiplayer every method here is
    // registered as a sync method (see MultiplayerCompat) - UI code must call these and never
    // mutate groups directly. Groups are addressed by index: the list order is itself shared
    // state, identical on all clients.
    public static class PresetActions
    {
        public static void CreateGroup(string name, List<Pawn> members)
        {
            GameComponent_CrewPresets.Get.groups.Add(new CrewGroup
            {
                name = name,
                members = DistinctMembers(members)
            });
        }

        public static void RenameGroup(int index, string newName)
        {
            CrewGroup g = GroupAt(index);
            if (g != null) g.name = newName;
        }

        public static void SetGroupMembers(int index, List<Pawn> members)
        {
            CrewGroup g = GroupAt(index);
            if (g != null) g.members = DistinctMembers(members);
        }

        public static void DeleteGroup(int index)
        {
            List<CrewGroup> groups = GameComponent_CrewPresets.Get.groups;
            if (index >= 0 && index < groups.Count) groups.RemoveAt(index);
        }

        public static void AddMember(int index, Pawn pawn)
        {
            CrewGroup g = GroupAt(index);
            if (g != null && pawn != null) g.members.AddUnique(pawn);
        }

        public static void RemoveMember(int index, Pawn pawn)
        {
            GroupAt(index)?.members.Remove(pawn);
        }

        // --- Gravship launch (ritual assignments + boarding exclusions) ---

        // Applies a group to a gravship launch in ONE synced call, deterministic on every
        // client: crew (humanlike members) become the ritual spectators via
        // TryAssignSpectate/RemoveParticipant, and the engine's boarding exclusions are set
        // to everything NOT in the group (exact-set, like the transferable dialogs).
        // Pilot/copilot role holders and required pawns are never touched. Don't branch on
        // TryAssignSpectate's return value - under MP it can execute out of band.
        public static void ApplyGroupToGravship(RitualRoleAssignments assignments, int index)
        {
            CrewGroup g = GroupAt(index);
            if (g == null || assignments == null) return;
            List<string> skipped = new List<string>();

            foreach (Pawn p in assignments.Participants.ToList()) // getter reuses a temp list; copy
            {
                if (!assignments.PawnSpectating(p)) continue; // leave pilot/copilot alone
                if (g.members.Contains(p) || GravshipPresets.IsRequired(assignments, p)) continue;
                assignments.RemoveParticipant(p);
            }
            foreach (Pawn p in g.members)
            {
                if (p == null || !p.RaceProps.Humanlike) continue; // animals/mechs handled below
                if (p.Dead)
                {
                    skipped.Add(p.LabelShortCap + " (dead)");
                    continue;
                }
                if (!assignments.AllCandidatePawns.Contains(p))
                {
                    skipped.Add(p.LabelShortCap + " (unavailable)");
                    continue;
                }
                if (!assignments.PawnParticipating(p))
                    assignments.TryAssignSpectate(p);
            }

            Building_GravEngine engine = GravshipPresets.EngineFor(assignments);
            if (engine != null)
            {
                List<Pawn> excl = GameComponent_CrewPresets.Get.EnsureExclusionsFor(engine.thingIDNumber);
                excl.Clear();
                foreach (Pawn p in GravshipPresets.AllBoardablePawns(engine.Map))
                    if (!g.members.Contains(p)) excl.Add(p);
            }

            if (MultiplayerCompat.ShowUiForThisClient)
                PresetApplier.ReportSkipped(skipped);
        }

        public static void SetBoardingExcluded(int engineId, Pawn pawn, bool excluded)
        {
            if (pawn == null) return;
            List<Pawn> excl = GameComponent_CrewPresets.Get.EnsureExclusionsFor(engineId);
            if (excluded)
                excl.AddUnique(pawn);
            else
                excl.Remove(pawn);
        }

        public static void SetBoardingAllForKind(int engineId, bool mechs, bool board)
        {
            Building_GravEngine engine = GravshipPresets.EngineById(engineId);
            if (engine == null) return;
            List<Pawn> excl = GameComponent_CrewPresets.Get.EnsureExclusionsFor(engineId);
            foreach (Pawn p in GravshipPresets.BoardablePawns(engine.Map, mechs))
            {
                if (board) excl.Remove(p);
                else excl.AddUnique(p);
            }
        }

        private static CrewGroup GroupAt(int index)
        {
            List<CrewGroup> groups = GameComponent_CrewPresets.Get.groups;
            return index >= 0 && index < groups.Count ? groups[index] : null;
        }

        private static List<Pawn> DistinctMembers(List<Pawn> members)
        {
            return members?.Where(p => p != null).Distinct().ToList() ?? new List<Pawn>();
        }
    }

    // Applying a group is LOCAL UI only (it just ticks rows in an open dialog; the final
    // Accept is vanilla-synced) - deliberately NOT a sync method.
    public static class PresetApplier
    {
        public static void ApplyToTransferables(CrewGroup group, List<TransferableOneWay> transferables, Action notifyChanged)
        {
            List<string> skipped = new List<string>();
            bool anyChanged = false;
            // Exact-set semantics: the group IS the crew. Untick every pawn that isn't a
            // member (items are left alone), then tick the members.
            if (transferables != null)
            {
                foreach (TransferableOneWay t in transferables)
                {
                    if (t.CountToTransfer > 0 && t.AnyThing is Pawn other && !group.members.Contains(other))
                    {
                        t.AdjustTo(0);
                        anyChanged = true;
                    }
                }
            }
            foreach (Pawn p in group.members)
            {
                if (p == null) continue;
                if (p.Dead)
                {
                    skipped.Add(p.LabelShortCap + " (dead)");
                    continue;
                }
                TransferableOneWay tr = transferables?.FirstOrDefault(t => t.AnyThing == p);
                if (tr == null)
                {
                    skipped.Add(p.LabelShortCap + " (unavailable)");
                    continue;
                }
                if (tr.CountToTransfer < 1)
                {
                    tr.AdjustTo(1);
                    anyChanged = true;
                }
            }
            if (anyChanged) notifyChanged?.Invoke();
            ReportSkipped(skipped);
        }

        // One message naming every member that couldn't be applied, with the reason.
        public static void ReportSkipped(List<string> skipped)
        {
            if (skipped.Count > 0)
                Messages.Message("Couldn't add: " + skipped.ToCommaList(), MessageTypeDefOf.RejectInput, historical: false);
        }

        public static List<Pawn> SelectedPawns(List<TransferableOneWay> transferables)
        {
            return transferables?
                .Where(t => t.CountToTransfer > 0 && t.AnyThing is Pawn)
                .Select(t => (Pawn)t.AnyThing)
                .ToList() ?? new List<Pawn>();
        }

        // True when the selection is exactly the group's members that are present in the
        // dialog. Drives the active-preset highlight on the Presets button.
        public static bool MatchesSelection(CrewGroup group, List<Pawn> selected, List<TransferableOneWay> transferables)
        {
            if (transferables == null) return false;
            List<Pawn> present = group.members
                .Where(p => p != null && !p.Dead && transferables.Any(t => t.AnyThing == p))
                .ToList();
            if (present.Count == 0 || selected.Count != present.Count) return false;
            return present.All(selected.Contains);
        }
    }
}
