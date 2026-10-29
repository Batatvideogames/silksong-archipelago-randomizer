using HarmonyLib;

namespace SilksongRandomizer.Patches
{
    internal static class BindPatches
    {
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.CanBind))]
        private static class Healing
        {
            private static void Postfix(ref bool __result) => __result &= BindManager.CanBind(SaveState.Instance);
        }
    }
}
