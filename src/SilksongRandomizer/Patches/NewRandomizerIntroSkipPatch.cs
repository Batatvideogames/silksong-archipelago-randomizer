using HarmonyLib;
using System.Collections;

namespace SilksongRandomizer.Patches
{
    [HarmonyPatch(typeof(OpeningSequence), "Start")]
    internal static class NewRandomizerIntroSkipPatch
    {
        private static bool skipNextOpeningSequence;

        internal static void ArmForNewRandomizerSave()
        {
            skipNextOpeningSequence = true;
        }

        internal static bool TryConsumeSkipRequest()
        {
            if (!skipNextOpeningSequence)
            {
                return false;
            }

            skipNextOpeningSequence = false;
            return true;
        }

        private static void Postfix(
            OpeningSequence __instance,
            ChainSequence ___chainSequence,
            ref IEnumerator __result
        )
        {
            if (!TryConsumeSkipRequest() ||
                __instance == null ||
                ___chainSequence == null ||
                __result == null)
            {
                return;
            }

            __result = RunAndSkipOpening(
                __instance,
                ___chainSequence,
                __result
            );
        }

        private static IEnumerator RunAndSkipOpening(
            OpeningSequence openingSequence,
            ChainSequence chainSequence,
            IEnumerator nativeRoutine
        )
        {
            // The native routine continues so its async hero and world loads,
            // first-level activation, scene refs and camera teardown all run.
            // A skippable cinematic segment enters the game's own skip coroutine
            // instead of bypassing initialization.
            while (nativeRoutine.MoveNext())
            {
                if (chainSequence.IsPlaying &&
                    chainSequence.CanSkipCurrent)
                {
                    IEnumerator skipRoutine = openingSequence.Skip();
                    while (skipRoutine.MoveNext())
                    {
                        yield return skipRoutine.Current;
                    }
                }

                yield return nativeRoutine.Current;
            }
        }
    }
}
