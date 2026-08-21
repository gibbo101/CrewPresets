using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrewPresets
{
    // Group editor: create/rename/delete groups and edit their members, with each member's
    // skills in the same Work-tab style table the picker uses. All mutations route through
    // PresetActions so they sync in Multiplayer.
    public class Dialog_ManageCrewGroups : Window
    {
        private const float GroupHeaderHeight = 30f;
        private const float GroupSpacing = 14f;

        private const float BottomBarHeight = 42f;

        private Vector2 scrollPos;
        // Sort is per group: each group's header controls only its own rows.
        private readonly Dictionary<int, SkillDef> sortBy = new Dictionary<int, SkillDef>();
        private readonly Dictionary<int, bool> sortDescending = new Dictionary<int, bool>();

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
            if (Widgets.ButtonText(new Rect(inRect.width - 194f, 0f, 160f, 30f), "New group..."))
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

            float top = 44f;

            Rect outRect = new Rect(0f, top, inRect.width, inRect.height - top - BottomBarHeight);
            float viewHeight = groups.Sum(GroupHeight);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, viewHeight);
            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);
            float y = 0f;
            for (int i = 0; i < groups.Count; i++)
                DrawGroup(ref y, viewRect.width, i, groups[i]);
            Widgets.EndScrollView();

            if (Widgets.ButtonText(new Rect(inRect.width / 2f - 70f, inRect.height - 35f, 140f, 32f), "Done"))
                Close();
        }

        private void DrawGroup(ref float y, float width, int index, CrewGroup g)
        {
            // Divider between groups, sitting in the spacing left by the previous one.
            if (index > 0)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.25f);
                Widgets.DrawLineHorizontal(0f, y - GroupSpacing / 2f, width);
                GUI.color = Color.white;
            }
            Widgets.Label(new Rect(0f, y + 3f, width - 250f, 24f), g.name + " (" + g.members.Count + ")");
            float bx = width - 245f;
            if (Widgets.ButtonText(new Rect(bx, y, 75f, 24f), "Rename"))
            {
                Find.WindowStack.Add(new Dialog_NameCrewGroup("Rename group", g.name,
                    name => PresetActions.RenameGroup(index, name)));
            }
            if (Widgets.ButtonText(new Rect(bx + 82f, y, 75f, 24f), "Members"))
                Find.WindowStack.Add(new Dialog_PickCrewMembers(index));
            if (Widgets.ButtonText(new Rect(bx + 164f, y, 75f, 24f), "Delete"))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Delete crew group \"" + g.name + "\"?",
                    () => PresetActions.DeleteGroup(index)));
            }
            y += GroupHeaderHeight;

            // Skill columns belong to this group's rows, so they're drawn per group - and
            // skipped entirely when there's nobody here to label.
            if (HasSkilledMembers(g))
            {
                SkillDef clicked = CrewSkillTable.DrawHeader(0f, y, SortBy(index), Descending(index));
                if (clicked != null)
                {
                    if (clicked == SortBy(index)) sortDescending[index] = !Descending(index);
                    else { sortBy[index] = clicked; sortDescending[index] = true; }
                }
                y += CrewSkillTable.HeaderHeight;
            }

            int i = 0;
            foreach (Pawn p in OrderedMembers(g, index))
            {
                Rect row = new Rect(0f, y, width, CrewSkillTable.RowHeight);
                if (i++ % 2 == 1) Widgets.DrawAltRect(row);
                Widgets.DrawHighlightIfMouseover(row);
                bool gone = p.Dead || p.Destroyed;
                if (gone) GUI.color = Color.gray;
                CrewSkillTable.DrawPawnLabel(0f, y, p, p.Dead ? " (dead)" : p.Destroyed ? " (gone)" : null);
                CrewSkillTable.DrawWeapon(0f, y, p);
                GUI.color = Color.white;
                if (!gone) CrewSkillTable.DrawCells(0f, y, p);
                Pawn pawn = p;
                if (Widgets.ButtonText(new Rect(CrewSkillTable.RemoveButtonX, y + 3f, CrewSkillTable.RemoveButtonWidth, 22f), "X"))
                    PresetActions.RemoveMember(index, pawn);
                y += CrewSkillTable.RowHeight;
            }
            y += GroupSpacing;
        }

        // Colonists first, then mechs, then animals; the chosen skill sort applies within
        // each of those. No sub-headings - only colonists have boxes, so the split reads
        // on its own and the headings only added dead bands to the table.
        private SkillDef SortBy(int index) => sortBy.TryGetValue(index, out SkillDef d) ? d : null;

        private bool Descending(int index) => !sortDescending.TryGetValue(index, out bool b) || b;

        private IEnumerable<Pawn> OrderedMembers(CrewGroup g, int index)
        {
            List<Pawn> members = g.members.Where(p => p != null).ToList();
            SkillDef skill = SortBy(index);
            bool desc = Descending(index);
            return CrewSkillTable.Sorted(members.Where(p => p.RaceProps.Humanlike), skill, desc)
                .Concat(CrewSkillTable.Sorted(members.Where(p => p.RaceProps.IsMechanoid), skill, desc))
                .Concat(CrewSkillTable.Sorted(members.Where(p => !p.RaceProps.Humanlike && !p.RaceProps.IsMechanoid), skill, desc));
        }

        private static bool HasSkilledMembers(CrewGroup g)
        {
            return g.members.Any(p => p != null && p.skills != null);
        }

        private static float GroupHeight(CrewGroup g)
        {
            return GroupHeaderHeight + GroupSpacing
                + (HasSkilledMembers(g) ? CrewSkillTable.HeaderHeight : 0f)
                + g.members.Count(p => p != null) * CrewSkillTable.RowHeight;
        }
    }
}
