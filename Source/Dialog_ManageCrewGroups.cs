using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrewPresets
{
    // Group editor: create/rename/delete groups, add/remove members (grouped under
    // Colonists / Mechs / Animals headings), with each colonist's skills laid out in
    // columns so you can see who you're picking. All mutations route through PresetActions
    // so they sync in Multiplayer.
    public class Dialog_ManageCrewGroups : Window
    {
        private const float HeaderHeight = 30f;
        private const float BucketHeight = 22f;
        private const float GroupSpacing = 12f;

        private Vector2 scrollPos;
        private static SkillDef sortBy;
        private static bool sortDescending = true;

        public override Vector2 InitialSize =>
            new Vector2(Mathf.Min(CrewSkillTable.TableWidth, UI.screenWidth - 40f), 640f);

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

            // One skill header for the whole table, outside the scroll view so it stays put.
            SkillDef clicked = CrewSkillTable.DrawHeader(0f, 40f, sortBy, sortDescending);
            if (clicked != null)
            {
                if (clicked == sortBy) sortDescending = !sortDescending;
                else { sortBy = clicked; sortDescending = true; }
            }
            float top = 40f + CrewSkillTable.HeaderHeight;

            Rect outRect = new Rect(0f, top, inRect.width, inRect.height - top);
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
                Find.WindowStack.Add(new Dialog_PickCrewMembers(index));
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
            List<Pawn> list = CrewSkillTable.Sorted(g.members.Where(p => p != null && test(p)), sortBy, sortDescending).ToList();
            if (list.Count == 0) return;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(12f, y, width - 12f, BucketHeight), label);
            GUI.color = Color.white;
            y += BucketHeight;
            int i = 0;
            foreach (Pawn p in list)
            {
                Rect row = new Rect(0f, y, width, CrewSkillTable.RowHeight);
                if (i++ % 2 == 1) Widgets.DrawAltRect(row);
                Widgets.DrawHighlightIfMouseover(row);
                bool gone = p.Dead || p.Destroyed;
                if (gone) GUI.color = Color.gray;
                CrewSkillTable.DrawPawnLabel(0f, y, p, p.Dead ? " (dead)" : p.Destroyed ? " (gone)" : null);
                GUI.color = Color.white;
                if (!gone) CrewSkillTable.DrawCells(0f, y, p);
                Pawn pawn = p;
                if (Widgets.ButtonText(new Rect(CrewSkillTable.RemoveButtonX, y + 3f, CrewSkillTable.RemoveButtonWidth, 22f), "X"))
                    PresetActions.RemoveMember(index, pawn);
                y += CrewSkillTable.RowHeight;
            }
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
            return n == 0 ? 0f : BucketHeight + n * CrewSkillTable.RowHeight;
        }
    }
}
