using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrewPresets
{
    // Group editor: create/rename/delete groups, add/remove members (grouped under
    // Colonists / Mechs / Animals headings). All mutations route through PresetActions
    // so they sync in Multiplayer.
    public class Dialog_ManageCrewGroups : Window
    {
        private const float HeaderHeight = 30f;
        private const float BucketHeight = 22f;
        private const float RowHeight = 24f;
        private const float GroupSpacing = 12f;

        private Vector2 scrollPos;

        public override Vector2 InitialSize => new Vector2(520f, 640f);

        public Dialog_ManageCrewGroups()
        {
            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 35f), "Crew groups");
            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(new Rect(inRect.width - 160f, 0f, 160f, 30f), "New group..."))
            {
                Find.WindowStack.Add(new Dialog_NameCrewGroup("Name the new group", "",
                    name => PresetActions.CreateGroup(name, new List<Pawn>())));
            }

            List<CrewGroup> groups = GameComponent_CrewPresets.Get.groups;
            if (groups.Count == 0)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(0f, 45f, inRect.width, 60f),
                    "No groups yet. Create one here, or tick pawns in a load/caravan dialog and use Presets > \"Save current selection as...\".");
                GUI.color = Color.white;
                return;
            }

            Rect outRect = new Rect(0f, 40f, inRect.width, inRect.height - 40f);
            float viewHeight = groups.Sum(GroupHeight);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, viewHeight);
            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);
            float y = 0f;
            for (int i = 0; i < groups.Count; i++)
                DrawGroup(ref y, viewRect.width, i, groups[i]);
            Widgets.EndScrollView();
        }

        private void DrawGroup(ref float y, float width, int index, CrewGroup g)
        {
            Widgets.Label(new Rect(0f, y + 3f, width - 250f, 24f), g.name + " (" + g.members.Count + ")");
            float bx = width - 245f;
            if (Widgets.ButtonText(new Rect(bx, y, 75f, 24f), "Rename"))
            {
                Find.WindowStack.Add(new Dialog_NameCrewGroup("Rename group", g.name,
                    name => PresetActions.RenameGroup(index, name)));
            }
            if (Widgets.ButtonText(new Rect(bx + 82f, y, 75f, 24f), "Add..."))
                OpenAddMemberMenu(index, g);
            if (Widgets.ButtonText(new Rect(bx + 164f, y, 75f, 24f), "Delete"))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Delete crew group \"" + g.name + "\"?",
                    () => PresetActions.DeleteGroup(index)));
            }
            y += HeaderHeight;
            DrawBucket(ref y, width, index, g, "Colonists", p => p.RaceProps.Humanlike);
            DrawBucket(ref y, width, index, g, "Mechs", p => p.RaceProps.IsMechanoid);
            DrawBucket(ref y, width, index, g, "Animals", p => !p.RaceProps.Humanlike && !p.RaceProps.IsMechanoid);
            y += GroupSpacing;
        }

        private void DrawBucket(ref float y, float width, int index, CrewGroup g, string label, Func<Pawn, bool> test)
        {
            List<Pawn> list = g.members.Where(p => p != null && test(p)).ToList();
            if (list.Count == 0) return;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(12f, y, width - 12f, BucketHeight), label);
            GUI.color = Color.white;
            y += BucketHeight;
            foreach (Pawn p in list)
            {
                bool gone = p.Dead || p.Destroyed;
                if (gone) GUI.color = Color.gray;
                Widgets.Label(new Rect(24f, y + 2f, width - 84f, 22f),
                    p.LabelShortCap + (p.Dead ? " (dead)" : p.Destroyed ? " (gone)" : ""));
                GUI.color = Color.white;
                Pawn pawn = p;
                if (Widgets.ButtonText(new Rect(width - 50f, y, 40f, 22f), "X"))
                    PresetActions.RemoveMember(index, pawn);
                y += RowHeight;
            }
        }

        private static void OpenAddMemberMenu(int index, CrewGroup g)
        {
            List<FloatMenuOption> opts = new List<FloatMenuOption>();
            IEnumerable<Pawn> candidates = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction
                .Where(p => !g.members.Contains(p))
                .OrderBy(p => p.RaceProps.Humanlike ? 0 : p.RaceProps.IsMechanoid ? 1 : 2)
                .ThenBy(p => p.LabelShort);
            foreach (Pawn p in candidates)
            {
                Pawn pawn = p;
                opts.Add(new FloatMenuOption(pawn.LabelShortCap + ", " + pawn.KindLabel,
                    () => PresetActions.AddMember(index, pawn)));
            }
            if (opts.Count == 0)
                opts.Add(new FloatMenuOption("(no pawns available)", null));
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        private static float GroupHeight(CrewGroup g)
        {
            float h = HeaderHeight + GroupSpacing;
            h += BucketHeight_(g, p => p.RaceProps.Humanlike);
            h += BucketHeight_(g, p => p.RaceProps.IsMechanoid);
            h += BucketHeight_(g, p => !p.RaceProps.Humanlike && !p.RaceProps.IsMechanoid);
            return h;
        }

        private static float BucketHeight_(CrewGroup g, Func<Pawn, bool> test)
        {
            int n = g.members.Count(p => p != null && test(p));
            return n == 0 ? 0f : BucketHeight + n * RowHeight;
        }
    }
}
