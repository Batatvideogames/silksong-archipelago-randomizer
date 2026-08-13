using HarmonyLib;
using System;
using SetPlayerDataBoolAction =
    HutongGames.PlayMaker.Actions.SetPlayerDataBool;

namespace SilksongRandomizer.Patches
{
    /// <summary>
    /// Reports the existing Deep Docks Lace location from either mutually
    /// exclusive vanilla outcome: defeating Lace in Deep Docks or completing
    /// her later Blasted Bridge encounter. The latter is the story branch that
    /// removes the Deep Docks boss fight.
    /// </summary>
    internal static class LaceAlternativeCompletionPatches
    {
        internal const string LocationName = "Boss: Lace (Deep Docks)";
        private const string EncounterFlag =
            "encounteredLaceBlastedBridge";
        private const string EncounterObjectName =
            "Lace NPC Blasted Bridge";
        private const string EncounterFsmName = "Control";
        private const string EncounterEndStateName = "End";
        private static bool IsEncounterScene(string sceneName)
        {
            return string.Equals(
                       sceneName,
                       "Dust_01",
                       StringComparison.Ordinal
                   ) ||
                   string.Equals(
                       sceneName,
                       "Coral_19",
                       StringComparison.Ordinal
                   );
        }

        [HarmonyPatch(
            typeof(SetPlayerDataBoolAction),
            nameof(SetPlayerDataBoolAction.OnEnter)
        )]
        private static class BlastedBridgeEncounterPatch
        {
            [HarmonyPostfix]
            private static void Postfix(SetPlayerDataBoolAction __instance)
            {
                SaveState state = SaveState.Instance;
                if (
                    __instance == null ||
                    state == null ||
                    !state.IsRandomized(ItemType.Boss) ||
                    !state.IsLocationEnabled(LocationName) ||
                    !state.IsLocationInSeed(LocationName) ||
                    state.IsLocationChecked(LocationName) ||
                    __instance.Owner == null ||
                    !IsEncounterScene(__instance.Owner.scene.name) ||
                    !string.Equals(
                        __instance.Owner.name,
                        EncounterObjectName,
                        StringComparison.Ordinal
                    ) ||
                    __instance.Fsm == null ||
                    !string.Equals(
                        __instance.Fsm.Name,
                        EncounterFsmName,
                        StringComparison.Ordinal
                    ) ||
                    __instance.State == null ||
                    !string.Equals(
                        __instance.State.Name,
                        EncounterEndStateName,
                        StringComparison.Ordinal
                    ) ||
                    __instance.boolName == null ||
                    !string.Equals(
                        __instance.boolName.Value,
                        EncounterFlag,
                        StringComparison.Ordinal
                    ) ||
                    __instance.value == null ||
                    !__instance.value.Value ||
                    PlayerData.instance == null ||
                    !PlayerData.instance.encounteredLaceBlastedBridge
                )
                {
                    return;
                }

                state.CheckLocation(LocationName);
                RandomizerPlugin.Log?.LogInfo(
                    "[RANDOMIZER] The Blasted Bridge Lace encounter " +
                    "reported the shared Deep Docks Lace location."
                );
            }
        }
    }
}
