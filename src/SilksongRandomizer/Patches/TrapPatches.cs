using HarmonyLib;
using System;

namespace SilksongRandomizer.Patches
{
    [HarmonyPatch(
        typeof(DarknessRegion),
        nameof(DarknessRegion.SetDarknessLevel),
        new Type[] { typeof(int) }
    )]
    internal static class DarknessTrapNativeLevelPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ref int __0)
        {
            TrapManager.ObserveNativeDarknessRequest(ref __0);
        }
    }

    [HarmonyPatch(
        typeof(HeroController),
        nameof(HeroController.SetIsMaggoted),
        new Type[] { typeof(bool) }
    )]
    internal static class MuckmaggotTrapNativeStatusPatch
    {
        [HarmonyPrefix]
        private static void Prefix(bool __0)
        {
            TrapManager.ObserveNativeMuckmaggotRequest(__0);
        }
    }
}
