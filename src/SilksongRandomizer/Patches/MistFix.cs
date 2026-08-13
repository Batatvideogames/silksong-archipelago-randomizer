using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;

namespace SilksongRandomizer.Patches
{
    internal class MistFix
    {
        [HarmonyPatch(typeof(MazeController), "LinkDoors", new Type[] {typeof(IReadOnlyList<TransitionPoint>) })]
        internal static class MazeController_LinkDoors_Patch
        {
            private static void Prefix(out bool __state)
            {
                __state = PlayerData.instance.hasNeedolin;
                SaveState state = SaveState.Instance;
                if (state != null && state.IsRandomized(ItemType.Skill))
                {
                    PlayerData.instance.hasNeedolin = state.canUseNeedolin;
                }
            }

            private static Exception Finalizer(Exception __exception, bool __state)
            {
                PlayerData.instance.hasNeedolin = __state;
                return __exception;
            }
        }
    }
}
