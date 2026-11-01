using HarmonyLib;
using HutongGames.PlayMaker.Actions;

namespace SilksongRandomizer.Patches
{
    internal static class HalfwayHomePatches
    {
        [HarmonyPatch(typeof(SendEventToRegister), nameof(SendEventToRegister.OnEnter))]
        private static class KeepPlatformPatch
        {
            private static bool Prefix(SendEventToRegister __instance)
            {
                if (SaveState.Instance?.IsRoomBound != true ||
                    __instance.eventName?.Value != "ANT SMASH PLAT" ||
                    __instance.State?.Name != "Smash Plat" || __instance.Fsm?.Name != "Control" ||
                    __instance.Fsm.GameObject == null || __instance.Fsm.GameObject.scene.name != "Ant_08" ||
                    Utils.GetHierarchyPath(__instance.Fsm.GameObject.transform) !=
                        "Battle Scene/Wave 2/Bone Hunter Throw") return true;

                __instance.Finish();
                return false;
            }
        }
    }
}
