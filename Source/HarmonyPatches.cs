using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace CrewPresets
{
    [StaticConstructorOnStartup]
    public static class HarmonyBootstrap
    {
        static HarmonyBootstrap()
        {
            new Harmony("luke.crewpresets").PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    // Reflective access to the bits every supported transferable dialog shares: a
    // `transferables` list, a private `tab` enum whose first value is the Pawns tab, and
    // (optionally) a private method that dirties the mass/food caches after CountToTransfer
    // changes behind its back.
    internal sealed class DialogHooks
    {
        private readonly FieldInfo transferablesField;
        private readonly FieldInfo tabField;
        private readonly MethodInfo notifyMethod;

        public DialogHooks(Type dialogType, string notifyMethodName)
        {
            transferablesField = AccessTools.Field(dialogType, "transferables");
            tabField = AccessTools.Field(dialogType, "tab");
            notifyMethod = notifyMethodName == null ? null : AccessTools.Method(dialogType, notifyMethodName);
        }

        public List<TransferableOneWay> Transferables(Window dlg) => (List<TransferableOneWay>)transferablesField.GetValue(dlg);
        public bool OnPawnsTab(Window dlg) => Convert.ToInt32(tabField.GetValue(dlg)) == 0;
        public void NotifyChanged(Window dlg) => notifyMethod?.Invoke(dlg, null);
    }

    // The single "Presets..." button drawn into a dialog's header. Shows the active group's
    // name (green) when the current selection exactly matches one. Left-click: apply / save
    // selection / manage. Right-click: overwrite a group with the current selection.
    internal static class PresetUI
    {
        private static readonly Color ActiveColor = new Color(0.55f, 1f, 0.55f);
        private const float ButtonWidth = 160f;

        public static void DrawHeaderControls(Window dlg, Rect inRect, DialogHooks hooks)
        {
            if (!hooks.OnPawnsTab(dlg))
                return;
            List<TransferableOneWay> transferables = hooks.Transferables(dlg);
            DrawButton(new Rect(inRect.xMax - ButtonWidth, 6f, ButtonWidth, 30f),
                () => PresetApplier.SelectedPawns(transferables),
                (g, sel) => PresetApplier.MatchesSelection(g, sel, transferables),
                g => PresetApplier.ApplyToTransferables(g, transferables, () => hooks.NotifyChanged(dlg)));
        }

        public static void DrawButton(Rect btn, Func<List<Pawn>> selectedGetter,
            Func<CrewGroup, List<Pawn>, bool> isActive, Action<CrewGroup> apply)
        {
            Text.Font = GameFont.Small;
            List<CrewGroup> groups = GameComponent_CrewPresets.Get.groups;
            List<Pawn> selected = selectedGetter();
            CrewGroup active = groups.FirstOrDefault(g => isActive(g, selected));

            if (active != null) GUI.color = ActiveColor;
            bool clicked = Widgets.ButtonText(btn, (active != null ? "Preset: " + active.name : "Presets...").Truncate(btn.width - 20f));
            GUI.color = Color.white;
            TooltipHandler.TipRegion(btn, (active != null
                ? "The current selection matches \"" + active.name + "\".\n\n"
                : "") + "Apply a crew preset, save the current selection, or manage groups.\nIn the menu, right-click a group to overwrite it with the current selection.");
            if (clicked)
                OpenPresetsMenu(groups, selectedGetter, apply);
        }

        private static void OpenPresetsMenu(List<CrewGroup> groups, Func<List<Pawn>> selectedGetter, Action<CrewGroup> apply)
        {
            List<FloatMenuOption> opts = new List<FloatMenuOption>();
            for (int i = 0; i < groups.Count; i++)
            {
                int index = i;
                CrewGroup group = groups[i];
                opts.Add(new FloatMenuOption_CrewGroup("Apply: " + group.name + " (" + group.members.Count + ")",
                    () => apply(group),
                    () => OverwriteGroup(index, group, selectedGetter()))
                {
                    tooltip = "Left-click: select exactly this crew.\nRight-click: overwrite this group with the current selection."
                });
            }
            opts.Add(new FloatMenuOption("Save current selection as...", () =>
            {
                List<Pawn> selected = selectedGetter();
                if (selected.Count == 0)
                {
                    Messages.Message("No pawns are selected.", MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }
                Find.WindowStack.Add(new Dialog_NameCrewGroup("Name this crew group", "",
                    name => PresetActions.CreateGroup(name, selected)));
            }));
            opts.Add(new FloatMenuOption("Manage groups...", () => Find.WindowStack.Add(new Dialog_ManageCrewGroups())));
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        private static void OverwriteGroup(int index, CrewGroup group, List<Pawn> selected)
        {
            if (selected.Count == 0)
            {
                Messages.Message("No pawns are selected.", MessageTypeDefOf.RejectInput, historical: false);
                return;
            }
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "Overwrite \"" + group.name + "\" with the current selection (" + selected.Count + " pawn" + (selected.Count == 1 ? "" : "s") + ")?",
                () =>
                {
                    PresetActions.SetGroupMembers(index, selected);
                    Messages.Message("\"" + group.name + "\" now has " + selected.Count + " member" + (selected.Count == 1 ? "" : "s") + ".",
                        MessageTypeDefOf.TaskCompletion, historical: false);
                }));
        }
    }

    [HarmonyPatch(typeof(Dialog_LoadTransporters), nameof(Dialog_LoadTransporters.DoWindowContents))]
    public static class Patch_LoadTransporters_Presets
    {
        private static readonly DialogHooks Hooks = new DialogHooks(typeof(Dialog_LoadTransporters), "CountToTransferChanged");

        public static void Postfix(Dialog_LoadTransporters __instance, Rect inRect) =>
            PresetUI.DrawHeaderControls(__instance, inRect, Hooks);
    }

    [HarmonyPatch(typeof(Dialog_FormCaravan), nameof(Dialog_FormCaravan.DoWindowContents))]
    public static class Patch_FormCaravan_Presets
    {
        private static readonly DialogHooks Hooks = new DialogHooks(typeof(Dialog_FormCaravan), "Notify_TransferablesChanged");

        public static void Postfix(Dialog_FormCaravan __instance, Rect inRect) =>
            PresetUI.DrawHeaderControls(__instance, inRect, Hooks);
    }

    [HarmonyPatch(typeof(Dialog_SplitCaravan), nameof(Dialog_SplitCaravan.DoWindowContents))]
    public static class Patch_SplitCaravan_Presets
    {
        private static readonly DialogHooks Hooks = new DialogHooks(typeof(Dialog_SplitCaravan), "CountToTransferChanged");

        public static void Postfix(Dialog_SplitCaravan __instance, Rect inRect) =>
            PresetUI.DrawHeaderControls(__instance, inRect, Hooks);
    }

    // The portal dialog keeps no mass/food caches, so there is nothing to notify.
    [HarmonyPatch(typeof(Dialog_EnterPortal), nameof(Dialog_EnterPortal.DoWindowContents))]
    public static class Patch_EnterPortal_Presets
    {
        private static readonly DialogHooks Hooks = new DialogHooks(typeof(Dialog_EnterPortal), null);

        public static void Postfix(Dialog_EnterPortal __instance, Rect inRect) =>
            PresetUI.DrawHeaderControls(__instance, inRect, Hooks);
    }

    // Gravship launch. The crew picker is the RITUAL assignment system, not transferables:
    // vanilla uses Dialog_BeginGravshipLaunch and the Multiplayer mod replaces it with its
    // RitualBeginProxy - BOTH are Dialog_BeginRitual subclasses that run this base
    // DoWindowContents (the proxy calls base), so one postfix covers SP and MP. We gate on
    // the gravship ritual behavior so ordinary rituals (weddings etc.) are untouched.
    // Presets are the SAME global groups as everywhere else: humanlike members become the
    // crew, and the group's animals/mechs drive the per-engine boarding exclusions (see
    // GravshipPresets / Window_GravshipBoarding). Apply is one synced call. Pilot/copilot
    // are picked in the dialog's own role UI and are never touched by a preset.
    [HarmonyPatch(typeof(Dialog_BeginLordJob), nameof(Dialog_BeginLordJob.DoWindowContents))]
    public static class Patch_GravshipLaunch_Presets
    {
        private static readonly FieldInfo assignmentsField = AccessTools.Field(typeof(Dialog_BeginRitual), "assignments");

        private static Window_GravshipBoarding openPanel;

        public static void Postfix(Dialog_BeginLordJob __instance, Rect inRect)
        {
            if (!(__instance is Dialog_BeginRitual))
                return;
            if (!(assignmentsField.GetValue(__instance) is RitualRoleAssignments assignments))
                return;
            if (!(assignments.Ritual?.behavior is RitualBehaviorWorker_GravshipLaunch))
                return;

            Building_GravEngine engine = GravshipPresets.EngineFor(assignments);
            EnsureBoardingPanel(__instance, engine);

            // Top-right of the header row; the header label + icon sit top-left.
            PresetUI.DrawButton(new Rect(inRect.xMax - 160f, inRect.y, 160f, 30f),
                () => GravshipPresets.CaptureMembers(assignments, engine),
                (g, sel) => GravshipPresets.MatchesGroup(g, assignments, engine),
                g => PresetActions.ApplyGroupToGravship(assignments, GameComponent_CrewPresets.Get.groups.IndexOf(g)));
        }

        private static void EnsureBoardingPanel(Window dlg, Building_GravEngine engine)
        {
            if (engine == null) return;
            if (openPanel != null && Find.WindowStack.IsOpen(openPanel) && openPanel.Anchor == dlg) return;
            openPanel = new Window_GravshipBoarding(dlg, engine);
            Find.WindowStack.Add(openPanel);
        }
    }
}
