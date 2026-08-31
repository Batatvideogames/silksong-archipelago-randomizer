using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
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
        private static void Postfix(
            string key,
            string sheetTitle,
            ref string __result)
        {
            __result = SpellingBeeTitle.ResolveText(key, __result);
            __result = AlphabetModeManager.FilterLocalizedText(key, __result);
            __result = DeathLinkManager.ResolvePermadeathBodyText(
                key,
                sheetTitle,
                __result
            );
        }
    }

    [HarmonyPatch(typeof(DisplayBossTitle), "OnEnter")]
    internal static class DisplayBossTitleAlphabetPatch
    {
        [HarmonyPrefix]
        private static void Prefix(DisplayBossTitle __instance)
        {
            AlphabetModeManager.RegisterBossTitle(
                __instance?.bossTitle?.Value
            );
        }
    }

    [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
    internal static class GrandMotherSilkTitleAlphabetPatch
    {
        private const string SceneName = "Cradle_03";
        private const string ObjectPath = "Boss Scene/Boss Title";
        private const string FsmName = "Title Control";
        private const string MainTitleKey = "SILK_MAIN";
        private const string SuperTitleKey = "SILK_SUPER";

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(PlayMakerFSM __instance)
        {
            if (__instance == null ||
                __instance.gameObject == null ||
                !string.Equals(
                    __instance.gameObject.scene.name,
                    SceneName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    Utils.GetHierarchyPath(__instance.transform),
                    ObjectPath,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    __instance.FsmName,
                    FsmName,
                    StringComparison.Ordinal))
            {
                return;
            }

            FsmState init = __instance.Fsm?.GetState("Init");
            FsmState idle = __instance.Fsm?.GetState("Idle");
            FsmState swishStart =
                __instance.Fsm?.GetState("Swish Start");
            FsmState flashEffect =
                __instance.Fsm?.GetState("Flash Effect");
            FsmState titleUp =
                __instance.Fsm?.GetState("Title Up");
            FsmState titleDown =
                __instance.Fsm?.GetState("Title Down");

            bool hasRegister =
                ContainsAction<RegisterGrandMotherSilkTitle>(swishStart);
            bool hasClear =
                ContainsAction<ClearGrandMotherSilkTitle>(titleDown);
            if (hasRegister && hasClear)
            {
                return;
            }

            if (hasRegister || hasClear ||
                !HasExactTitleGraph(
                    __instance,
                    init,
                    idle,
                    swishStart,
                    flashEffect,
                    titleUp,
                    titleDown))
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Grand Mother Silk title filtering was " +
                    "not installed because the validated native title " +
                    "sequence changed."
                );
                return;
            }

            RegisterGrandMotherSilkTitle register =
                new RegisterGrandMotherSilkTitle();
            register.Init(swishStart);
            FsmStateAction[] swishActions = swishStart.Actions;
            FsmStateAction[] patchedSwishActions =
                new FsmStateAction[swishActions.Length + 1];
            patchedSwishActions[0] = register;
            Array.Copy(
                swishActions,
                0,
                patchedSwishActions,
                1,
                swishActions.Length
            );
            swishStart.Actions = patchedSwishActions;

            ClearGrandMotherSilkTitle clear =
                new ClearGrandMotherSilkTitle();
            clear.Init(titleDown);
            FsmStateAction[] titleDownActions = titleDown.Actions;
            FsmStateAction[] patchedTitleDownActions =
                new FsmStateAction[titleDownActions.Length + 1];
            Array.Copy(
                titleDownActions,
                patchedTitleDownActions,
                titleDownActions.Length
            );
            patchedTitleDownActions[titleDownActions.Length] = clear;
            titleDown.Actions = patchedTitleDownActions;
        }

        private static bool HasExactTitleGraph(
            PlayMakerFSM fsm,
            FsmState init,
            FsmState idle,
            FsmState swishStart,
            FsmState flashEffect,
            FsmState titleUp,
            FsmState titleDown)
        {
            return fsm.FsmStates != null &&
                   fsm.FsmStates.Length == 6 &&
                   (fsm.FsmGlobalTransitions == null ||
                    fsm.FsmGlobalTransitions.Length == 0) &&
                   init?.Actions != null &&
                   init.Actions.Length == 3 &&
                   init.Actions[0] is FindNamedChild &&
                   init.Actions[1] is FindNamedChild &&
                   init.Actions[2] is FindNamedChild &&
                   HasOnlyTransition(
                       init,
                       FsmEvent.Finished.Name,
                       "Idle") &&
                   idle?.Actions != null &&
                   idle.Actions.Length == 0 &&
                   HasOnlyTransition(
                       idle,
                       "TITLE UP",
                       "Swish Start") &&
                   swishStart?.Actions != null &&
                   swishStart.Actions.Length == 2 &&
                   swishStart.Actions[0] is ActivateGameObject &&
                   swishStart.Actions[1] is Wait &&
                   HasOnlyTransition(
                       swishStart,
                       FsmEvent.Finished.Name,
                       "Flash Effect") &&
                   flashEffect?.Actions != null &&
                   flashEffect.Actions.Length == 4 &&
                   flashEffect.Actions[0] is ActivateGameObject &&
                   flashEffect.Actions[1] is Wait &&
                   flashEffect.Actions[2] is ApplyMusicCue &&
                   flashEffect.Actions[3] is TransitionToAudioSnapshot &&
                   HasOnlyTransition(
                       flashEffect,
                       FsmEvent.Finished.Name,
                       "Title Up") &&
                   titleUp?.Actions != null &&
                   titleUp.Actions.Length == 2 &&
                   titleUp.Actions[0] is FadeNestedFadeGroup &&
                   titleUp.Actions[1] is Wait &&
                   HasOnlyTransition(
                       titleUp,
                       FsmEvent.Finished.Name,
                       "Title Down") &&
                   titleDown?.Actions != null &&
                   titleDown.Actions.Length == 1 &&
                   titleDown.Actions[0] is FadeNestedFadeGroup &&
                   titleDown.Transitions != null &&
                   titleDown.Transitions.Length == 0;
        }

        private static bool ContainsAction<T>(FsmState state)
            where T : FsmStateAction
        {
            if (state?.Actions == null)
            {
                return false;
            }

            foreach (FsmStateAction action in state.Actions)
            {
                if (action is T)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasOnlyTransition(
            FsmState state,
            string eventName,
            string targetStateName)
        {
            FsmTransition[] transitions = state?.Transitions;
            return transitions != null &&
                   transitions.Length == 1 &&
                   transitions[0] != null &&
                   string.Equals(
                       transitions[0].EventName,
                       eventName,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       transitions[0].ToState,
                       targetStateName,
                       StringComparison.Ordinal);
        }

        private sealed class RegisterGrandMotherSilkTitle :
            FsmStateAction
        {
            public override void OnEnter()
            {
                try
                {
                    AlphabetModeManager.RegisterBossTitlePair(
                        MainTitleKey,
                        SuperTitleKey
                    );
                    AlphabetModeManager.RefreshVisibleText();
                }
                finally
                {
                    Finish();
                }
            }
        }

        private sealed class ClearGrandMotherSilkTitle :
            FsmStateAction
        {
            public override void OnEnter()
            {
                AlphabetModeManager.ClearBossTitles();
                Finish();
            }
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
