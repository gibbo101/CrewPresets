using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrewPresets
{
    // Column geometry and drawing for the skill grid on the group screens, styled after the
    // vanilla Work tab: portrait + "name, title" on the left, then one column per SkillDef
    // with staggered headers so full skill names fit above narrow columns.
    //
    // Vanilla's SkillUI.DrawSkill can't be reused - it draws a 24px full-width row per skill
    // (label + fillable bar + number), ~290px of height per pawn. We draw compact cells
    // instead but keep its conventions: a greyed "-" for disabled skills, and passion icons.
    internal static class CrewSkillTable
    {
        public const float PortraitSize = 28f;
        public const float NameTextWidth = 160f;
        public const float CellWidth = 42f;
        public const float HeaderHeight = 46f;
        public const float RowHeight = 30f;
        public const float RemoveButtonWidth = 40f;
        public const float BoxSize = 26f;

        private const float HeaderLabelHeight = 20f;

        private static readonly Color DisabledSkillColor = new Color(1f, 1f, 1f, 0.5f);
        private static readonly Color LevelColor = new Color(0.93f, 0.9f, 0.72f);
        private static readonly Color HeaderLineColor = new Color(1f, 1f, 1f, 0.45f);

        // Not cached: mods can add SkillDefs, and this is only read while a dialog is open.
        public static List<SkillDef> Skills => DefDatabase<SkillDef>.AllDefsListForReading;

        public static float SkillsX => 4f + PortraitSize + 4f + NameTextWidth + 8f;
        public static float SkillsWidth => Skills.Count * CellWidth;
        public static float RemoveButtonX => SkillsX + SkillsWidth + 10f;

        // Dialog width that fits the whole table plus scrollbar and margin.
        public static float TableWidth => RemoveButtonX + RemoveButtonWidth + 16f + 24f;

        // Skill names are wider than their columns, so alternate them between two rows the
        // way the Work tab does, with a rule dropping to the column it belongs to.
        // Returns the skill whose header was clicked this frame, or null.
        public static SkillDef DrawHeader(float x, float y, SkillDef sortBy = null, bool descending = true)
        {
            SkillDef clicked = null;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            List<SkillDef> skills = Skills;
            for (int i = 0; i < skills.Count; i++)
            {
                float centre = x + SkillsX + i * CellWidth + CellWidth / 2f;
                float labelY = y + (i % 2 == 0 ? 0f : HeaderLabelHeight);
                // 82 wide, not the full 90: same-row neighbours sit 2 columns (84px) apart.
                Rect label = new Rect(centre - 41f, labelY, 82f, HeaderLabelHeight);
                Rect column = new Rect(centre - CellWidth / 2f, label.yMax, CellWidth, y + HeaderHeight - label.yMax);

                if (skills[i] == sortBy) GUI.color = Color.cyan;
                Widgets.Label(label, skills[i].skillLabel.CapitalizeFirst() + (skills[i] == sortBy ? (descending ? " v" : " ^") : ""));
                GUI.color = Color.white;
                TooltipHandler.TipRegion(label, skills[i].LabelCap + " - click to sort by this skill.");
                Widgets.DrawHighlightIfMouseover(label);

                GUI.color = HeaderLineColor;
                Widgets.DrawLineVertical(centre, label.yMax, column.height);
                GUI.color = Color.white;

                if (Widgets.ButtonInvisible(label) || Widgets.ButtonInvisible(column))
                    clicked = skills[i];
            }
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            return clicked;
        }

        // Level as shown in the cells; -1 for anything that hasn't got the skill at all, so
        // animals, mechs and disabled skills sort to the bottom rather than reading as 0.
        public static int LevelFor(Pawn pawn, SkillDef skill)
        {
            if (pawn?.skills == null) return -1;
            SkillRecord rec = pawn.skills.GetSkill(skill);
            return rec == null || rec.TotallyDisabled ? -1 : rec.GetLevelForUI();
        }

        // Orders pawns by one skill (highest first unless ascending), name as the tiebreak.
        // A null skill just sorts by name.
        public static IEnumerable<Pawn> Sorted(IEnumerable<Pawn> pawns, SkillDef skill, bool descending)
        {
            if (skill == null) return pawns.OrderBy(p => p.LabelShort);
            return descending
                ? pawns.OrderByDescending(p => LevelFor(p, skill)).ThenBy(p => p.LabelShort)
                : pawns.OrderBy(p => LevelFor(p, skill)).ThenBy(p => p.LabelShort);
        }

        // Portrait + "name, title", matching PawnColumnWorker_Label's shape.
        public static void DrawPawnLabel(float x, float y, Pawn pawn, string suffix = null)
        {
            Widgets.ThingIcon(new Rect(x + 4f, y + (RowHeight - PortraitSize) / 2f, PortraitSize, PortraitSize), pawn);
            string label = pawn.LabelShortCap;
            string title = pawn.story?.TitleShortCap;
            if (!title.NullOrEmpty()) label += ", " + title;
            if (!suffix.NullOrEmpty()) label += suffix;

            Rect rect = new Rect(x + 4f + PortraitSize + 4f, y, NameTextWidth, RowHeight);
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.WordWrap = false;
            Widgets.Label(rect, label.Truncate(NameTextWidth));
            Text.WordWrap = true;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        // One pawn's skill cells, drawn as Work-tab style boxes: background shaded by level,
        // the level centred on it, and a grey passion flame in the bottom-right quadrant.
        // Pawns without a skill tracker (animals, mechs) draw nothing.
        public static void DrawCells(float x, float y, Pawn pawn)
        {
            if (pawn?.skills == null) return;
            Text.Anchor = TextAnchor.MiddleCenter;
            List<SkillDef> skills = Skills;
            for (int i = 0; i < skills.Count; i++)
            {
                Rect cell = new Rect(x + SkillsX + i * CellWidth, y, CellWidth, RowHeight);
                Rect box = new Rect(cell.x + (CellWidth - BoxSize) / 2f, cell.y + (RowHeight - BoxSize) / 2f, BoxSize, BoxSize);
                SkillRecord rec = pawn.skills.GetSkill(skills[i]);
                if (rec == null || rec.TotallyDisabled)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.2f);
                    GUI.DrawTexture(box, WidgetsWork.WorkBoxBGTex_Awful);
                    GUI.color = DisabledSkillColor;
                    Widgets.Label(box, "-");
                    GUI.color = Color.white;
                    TooltipHandler.TipRegion(box, skills[i].LabelCap + ": incapable");
                    continue;
                }
                int level = rec.GetLevelForUI();
                DrawBox(box, level, rec.passion);
                GUI.color = LevelColor;
                Widgets.Label(box, level.ToStringCached());
                GUI.color = Color.white;
                TooltipHandler.TipRegion(box, skills[i].LabelCap + ": " + level
                    + (rec.passion > Passion.None ? " (" + rec.passion.GetLabel() + ")" : ""));
            }
            Text.Anchor = TextAnchor.UpperLeft;
        }

        // Mirrors WidgetsWork.DrawWorkBoxBackground, but keyed to one skill's level rather
        // than the average of a work type's relevant skills.
        private static void DrawBox(Rect box, int level, Passion passion)
        {
            Texture2D lower, upper;
            float t;
            if (level < 4)
            {
                lower = WidgetsWork.WorkBoxBGTex_Awful;
                upper = WidgetsWork.WorkBoxBGTex_Bad;
                t = level / 4f;
            }
            else if (level <= 14)
            {
                lower = WidgetsWork.WorkBoxBGTex_Bad;
                upper = WidgetsWork.WorkBoxBGTex_Mid;
                t = (level - 4f) / 10f;
            }
            else
            {
                lower = WidgetsWork.WorkBoxBGTex_Mid;
                upper = WidgetsWork.WorkBoxBGTex_Excellent;
                t = (level - 14f) / 6f;
            }
            GUI.DrawTexture(box, lower);
            GUI.color = new Color(1f, 1f, 1f, t);
            GUI.DrawTexture(box, upper);
            GUI.color = Color.white;

            if (passion > Passion.None)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.4f);
                Rect corner = box;
                corner.xMin = box.center.x;
                corner.yMin = box.center.y;
                GUI.DrawTexture(corner, passion == Passion.Major
                    ? WidgetsWork.PassionWorkboxMajorIcon
                    : WidgetsWork.PassionWorkboxMinorIcon);
                GUI.color = Color.white;
            }
        }
    }
}
