using System;
using System.Collections.Generic;
using GlobalEnums;
using HarmonyLib;
using System.Collections;

namespace SilksongRandomizer.Patches
{
    internal static class CutsceneSkipPatches
    {
        private const string OpeningSequenceScene = "Opening_Sequence";

        private static readonly HashSet<string> NativeSkipModeScenes =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "Bone_East_Umbrella",
                "Belltown",
                "Room_Pinstress",
                "Belltown_Room_pinsmith",
                "Belltown_Room_doctor",
                "Bellway_City",
                "City_Lace_cutscene",
                "Opening_Sequence_Act3",
                "Belltown_Room_Spare",
            };

        private static readonly HashSet<string> EndingScenes =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "End_Credits",
                "End_Credits_Scroll",
                "Menu_Credits",
                "End_Game_Completion",
                "BetaEnd",
                "PermaDeath",
                "PermaDeath_Unlock",
                "Cinematic_MrMushroom",
                "GG_End_Sequence",
                "Opening_Sequence_Act3",
            };

        internal static bool IsEndingScene(string sceneName)
        {
            return !string.IsNullOrEmpty(sceneName) &&
                   (
                       EndingScenes.Contains(sceneName) ||
                       sceneName.StartsWith(
                           "Cinematic_Ending_",
                           StringComparison.Ordinal
                       )
                   );
        }

        private static bool IsActive(out string sceneName)
        {
            SaveState state = SaveState.Instance;
            GameManager gameManager = GameManager.instance;
            sceneName = gameManager?.sceneName ?? string.Empty;
            return state != null &&
                   state.IsRoomBound &&
                   gameManager != null;
        }

        [HarmonyPatch(typeof(InputHandler), nameof(InputHandler.SetSkipMode))]
        private static class InputHandlerSetSkipModePatch
        {
            [HarmonyPrefix]
            private static void Prefix(ref SkipPromptMode newMode)
            {
                if (newMode != SkipPromptMode.SKIP_PROMPT ||
                    !IsActive(out string sceneName) ||
                    sceneName == OpeningSequenceScene ||
                    IsEndingScene(sceneName) ||
                    NativeSkipModeScenes.Contains(sceneName))
                {
                    return;
                }

                newMode = SkipPromptMode.SKIP_INSTANT;
            }
        }

        [HarmonyPatch(
            typeof(SkippableSequence),
            nameof(SkippableSequence.CanSkip),
            MethodType.Getter
        )]
        private static class SkippableSequenceCanSkipPatch
        {
            [HarmonyPrefix]
            private static void Prefix(SkippableSequence __instance)
            {
                if (__instance != null &&
                    IsActive(out string sceneName) &&
                    sceneName != OpeningSequenceScene &&
                    !IsEndingScene(sceneName))
                {
                    __instance.AllowSkip();
                }
            }
        }
    }

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
            // Keep the opening coroutine running so scene loading and camera cleanup finish.
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
