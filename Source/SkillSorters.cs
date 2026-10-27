using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CrewPresets
{
    // Orders a transferable list by one skill, highest level first. Anything that doesn't
    // have that skill - items, animals, mechs, and colonists the skill is disabled for -
    // sinks to the bottom.
    public class TransferableComparer_Skill : TransferableComparer
    {
        private readonly SkillDef skill;

        public TransferableComparer_Skill(SkillDef skill)
        {
            this.skill = skill;
        }

        public override int Compare(Transferable lhs, Transferable rhs)
        {
            return LevelOf(rhs).CompareTo(LevelOf(lhs));
        }

        private int LevelOf(Transferable tr)
        {
            if (!(tr?.AnyThing is Pawn pawn) || pawn.skills == null) return -1;
            SkillRecord rec = pawn.skills.GetSkill(skill);
            return rec == null || rec.TotallyDisabled ? -1 : rec.Level;
        }
    }

    // Adds a "<skill> skill" entry to the transferable dialogs' "Sort by" dropdowns without
    // patching any dialog: TransferableUIUtility.DoTransferableSorters builds its dropdown
    // straight from DefDatabase<TransferableSorterDef>, and every transferable dialog uses
    // that same widget - so registering defs here reaches all of them (transporters and
    // shuttles, form caravan, split caravan, enter portal) at once.
    //
    // The defs are built in code rather than XML for two reasons: one comparer instance per
    // skill can't come from TransferableSorterDef.comparerClass (Activator needs a
    // parameterless type), and the mod setting can add/remove them live. We seed the def's
    // cached comparer directly, so comparerClass is never consulted.
    [StaticConstructorOnStartup]
    public static class SkillSorters
    {
        private static readonly FieldInfo ComparerField = AccessTools.Field(typeof(TransferableSorterDef), "comparerInt");
        private static readonly MethodInfo RemoveMethod = AccessTools.Method(typeof(DefDatabase<TransferableSorterDef>), "Remove");

        private static readonly List<TransferableSorterDef> defs = BuildDefs();
        private static bool registered;

        static SkillSorters()
        {
            ApplySetting();
        }

        public static void ApplySetting()
        {
            bool want = CrewPresetsMod.Settings?.skillSorters ?? true;
            if (want == registered) return;
            foreach (TransferableSorterDef def in defs)
            {
                if (want) DefDatabase<TransferableSorterDef>.Add(def);
                else RemoveMethod?.Invoke(null, new object[] { def });
            }
            registered = want;
        }

        private static List<TransferableSorterDef> BuildDefs()
        {
            List<TransferableSorterDef> list = new List<TransferableSorterDef>();
            foreach (SkillDef skill in DefDatabase<SkillDef>.AllDefsListForReading.OrderBy(s => s.label))
            {
                TransferableSorterDef def = new TransferableSorterDef
                {
                    // Prefixed: defNames share one namespace across every mod.
                    defName = "CrewPresets_Skill_" + skill.defName,
                    // Lowercase to match the vanilla entries ("market value", "hit points percentage").
                    label = skill.label + " skill",
                };
                ComparerField.SetValue(def, new TransferableComparer_Skill(skill));
                list.Add(def);
            }
            return list;
        }
    }
}
