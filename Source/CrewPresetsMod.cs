using UnityEngine;
using Verse;

namespace CrewPresets
{
    public class CrewPresetsSettings : ModSettings
    {
        // Whether to add one "<skill> skill" entry per SkillDef to the "Sort by" dropdowns
        // shared by the transferable dialogs. See SkillSorters.
        public bool skillSorters = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref skillSorters, "skillSorters", true);
            base.ExposeData();
        }
    }

    public class CrewPresetsMod : Mod
    {
        public static CrewPresetsSettings Settings { get; private set; }

        public CrewPresetsMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<CrewPresetsSettings>();
        }

        public override string SettingsCategory() => "Crew Presets";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(inRect);
            list.CheckboxLabeled("Add skills to the \"Sort by\" dropdowns", ref Settings.skillSorters,
                "Adds \"cooking skill\", \"shooting skill\" and so on to the sort dropdowns in the transporter, shuttle, caravan and portal dialogs, so you can order colonists by skill while picking who goes. Highest first.\n\nThe dropdown is shared with the trade dialog, where these are just noise - turn this off to keep that list short.");
            list.End();
        }

        // Applied live: no restart needed to add or remove the sorter entries.
        public override void WriteSettings()
        {
            base.WriteSettings();
            SkillSorters.ApplySetting();
        }
    }
}
