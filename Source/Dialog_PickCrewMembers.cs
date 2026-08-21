using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrewPresets
{
    // Member picker for one group: every pawn you could take, laid out like the Work tab -
    // portrait, name and skills in columns - with a tick/cross you can drag down the column
    // to toggle a run of them at once. Click a skill header to sort by it.
    // Replaces the old plain float menu of names, which made you pick blind.
    public class Dialog_PickCrewMembers : Window
    {
        private const float BucketHeight = 24f;
        private const float CheckboxSize = 24f;
        private const float TableX = 30f;   // leaves room for the checkbox column
        private const float BottomBarHeight = 42f;

        private readonly int groupIndex;
        // Edits are buffered: the group is only written on "Done", so closing with the X
        // discards them. It also means a drag across ten rows is one synced change, not ten.
        private readonly HashSet<Pawn> selected;
        private Vector2 scrollPos;
        private SkillDef sortBy;
        private bool sortDescending = true;

        public override Vector2 InitialSize =>
            new Vector2(Mathf.Min(TableX + CrewSkillTable.TableWidth, UI.screenWidth - 40f), 700f);

        public Dialog_PickCrewMembers(int groupIndex)
        {
            this.groupIndex = groupIndex;
            List<CrewGroup> groups = GameComponent_CrewPresets.Get.groups;
            selected = groupIndex < groups.Count
                ? new HashSet<Pawn>(groups[groupIndex].members.Where(p => p != null))
                : new HashSet<Pawn>();
            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            List<CrewGroup> groups = GameComponent_CrewPresets.Get.groups;
            // The group can vanish underneath us if another player deletes it in Multiplayer.
            if (groupIndex >= groups.Count)
            {
                Close();
                return;
            }
            CrewGroup g = groups[groupIndex];

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 35f), "Members of \"" + g.name + "\"");
            Text.Font = GameFont.Small;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(0f, 34f, inRect.width, 22f),
                "Click a tick to add or remove, or hold and drag down the column to set several. Click a skill to sort by it.");
            Widgets.Label(new Rect(0f, 52f, inRect.width, 22f),
                "Changes are saved when you press Done. Closing with the X discards them.");
            GUI.color = Color.white;

            SkillDef clicked = CrewSkillTable.DrawHeader(TableX, 76f, sortBy, sortDescending);
            if (clicked != null)
            {
                if (clicked == sortBy) sortDescending = !sortDescending;
                else { sortBy = clicked; sortDescending = true; }
            }
            float top = 76f + CrewSkillTable.HeaderHeight;

            List<Pawn> candidates = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction.ToList();
            Rect outRect = new Rect(0f, top, inRect.width, inRect.height - top - BottomBarHeight);
            float viewHeight = BucketViewHeight(candidates, p => p.RaceProps.Humanlike, heading: false)
                             + BucketViewHeight(candidates, p => p.RaceProps.IsMechanoid)
                             + BucketViewHeight(candidates, p => !p.RaceProps.Humanlike && !p.RaceProps.IsMechanoid);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, viewHeight);
            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);
            float y = 0f;
            DrawBucket(ref y, viewRect.width, g, candidates, null, p => p.RaceProps.Humanlike);
            DrawBucket(ref y, viewRect.width, g, candidates, "Mechs", p => p.RaceProps.IsMechanoid);
            DrawBucket(ref y, viewRect.width, g, candidates, "Animals", p => !p.RaceProps.Humanlike && !p.RaceProps.IsMechanoid);
            Widgets.EndScrollView();

            if (Widgets.ButtonText(new Rect(inRect.width / 2f - 70f, inRect.height - 35f, 140f, 32f), "Done"))
            {
                PresetActions.SetGroupMembers(groupIndex, selected.ToList());
                Close();
            }
        }

        private void DrawBucket(ref float y, float width, CrewGroup g, List<Pawn> candidates, string label, Func<Pawn, bool> test)
        {
            List<Pawn> list = CrewSkillTable.Sorted(candidates.Where(test), sortBy, sortDescending).ToList();
            if (list.Count == 0) return;
            // Colonists (label null) run straight on from the header rules; the later
            // sections get a separator so they read as deliberate breaks.
            if (label != null) Widgets.ListSeparator(ref y, width, label);
            int i = 0;
            foreach (Pawn p in list)
            {
                Rect row = new Rect(0f, y, width, CrewSkillTable.RowHeight);
                if (i++ % 2 == 1) Widgets.DrawAltRect(row);
                Widgets.DrawHighlightIfMouseover(row);

                bool wasIn = selected.Contains(p);
                bool isIn = wasIn;
                // paintable: drag down the column to toggle a run of pawns in one sweep.
                Widgets.Checkbox(2f, y + (CrewSkillTable.RowHeight - CheckboxSize) / 2f, ref isIn, CheckboxSize, paintable: true);
                if (isIn != wasIn)
                {
                    if (isIn) selected.Add(p);
                    else selected.Remove(p);
                }

                CrewSkillTable.DrawPawnLabel(TableX, y, p);
                CrewSkillTable.DrawWeapon(TableX, y, p);
                CrewSkillTable.DrawCells(TableX, y, p);
                y += CrewSkillTable.RowHeight;
            }
        }

        private static float BucketViewHeight(List<Pawn> candidates, Func<Pawn, bool> test, bool heading = true)
        {
            int n = candidates.Count(test);
            if (n == 0) return 0f;
            return (heading ? BucketHeight : 0f) + n * CrewSkillTable.RowHeight;
        }
    }
}
