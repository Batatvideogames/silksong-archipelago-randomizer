using HarmonyLib;
using System;
using TeamCherry.Localization;

namespace SilksongRandomizer.AlphabetMode
{
    [HarmonyPatch(
        typeof(Language),
        nameof(Language.Get),
        new Type[] { typeof(string), typeof(string) }
    )]
    internal static class LanguageGetAlphabetPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(ref string __result)
        {
            __result = AlphabetModeManager.FilterDirectText(__result);
        }
    }

    [HarmonyPatch(typeof(RandomizerPlugin), "Update")]
    internal static class AlphabetModeUpdatePatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            AlphabetModeManager.ReconcileVisibleText();
        }
    }
}

