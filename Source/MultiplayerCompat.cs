using Multiplayer.API;
using Verse;

namespace CrewPresets
{
    // Registers the shared-state mutations as sync methods when the Multiplayer mod is
    // running. Applying a preset in a TRANSFERABLE dialog stays local (it only ticks rows;
    // Accept is vanilla-synced). The gravship apply and boarding toggles mutate the shared
    // ritual/boarding state, so they sync as single calls (RitualRoleAssignments serializes
    // via MP's session sync worker). When Multiplayer is absent, the shipped 0MultiplayerAPI
    // stub reports MP.enabled = false.
    [StaticConstructorOnStartup]
    public static class MultiplayerCompat
    {
        static MultiplayerCompat()
        {
            if (!MP.enabled) return;
            MP.RegisterSyncMethod(typeof(PresetActions), nameof(PresetActions.CreateGroup));
            MP.RegisterSyncMethod(typeof(PresetActions), nameof(PresetActions.RenameGroup));
            MP.RegisterSyncMethod(typeof(PresetActions), nameof(PresetActions.SetGroupMembers));
            MP.RegisterSyncMethod(typeof(PresetActions), nameof(PresetActions.DeleteGroup));
            MP.RegisterSyncMethod(typeof(PresetActions), nameof(PresetActions.AddMember));
            MP.RegisterSyncMethod(typeof(PresetActions), nameof(PresetActions.RemoveMember));
            MP.RegisterSyncMethod(typeof(PresetActions), nameof(PresetActions.ApplyGroupToGravship));
            MP.RegisterSyncMethod(typeof(PresetActions), nameof(PresetActions.SetBoardingExcluded));
            MP.RegisterSyncMethod(typeof(PresetActions), nameof(PresetActions.SetBoardingAllForKind));
        }

        // For UI side-effects inside synced methods: only the issuing client shows them.
        public static bool ShowUiForThisClient => !MP.enabled || MP.IsExecutingSyncCommandIssuedBySelf;
    }
}
