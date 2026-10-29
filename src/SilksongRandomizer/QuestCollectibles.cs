using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SilksongRandomizer
{
    internal static class QuestCollectibles
    {
        [ThreadStatic] private static string sourceQuest;

        private static bool Pending(string location)
        {
            var state = SaveState.Instance;
            return state?.IsRoomBound == true && state.IsRandomized(ItemType.QuestCollectible) &&
                state.IsLocationEnabled(location) && state.IsLocationInSeed(location) &&
                !state.IsLocationChecked(location);
        }

        internal static bool KeepHangingPickup(Component owner) => Pending("Grass Doll") &&
            owner.gameObject.scene.name == "Bone_East_18b" && Utils.GetHierarchyPath(owner.transform) == "ant_item_string";

        private static string QuestFor(Component owner)
        {
            string scene = owner.gameObject.scene.name;
            string path = Utils.GetHierarchyPath(owner.transform);
            if (scene == "Bone_East_08" && Pending("Horn Fragment") &&
                (path == "Boss Beastfly Loader" || path == "Boss Scene/Battle End Floor" ||
                 path == "Boss Scene/Pre Activation Floor" || path == "Beastfly Tracking Scene")) return "Beastfly Hunt";
            if (scene == "Bone_East_18b" && (Pending("Grass Doll") ||
                (SaveState.Instance?.IsRoomBound == true && SaveState.Instance.IsRandomized(ItemType.QuestCollectible) &&
                 SaveState.Instance.IsLocationInSeed("Grass Doll") &&
                 SaveState.Instance.IsLocationInSeed("Far Fields - Rosary Cache #19") &&
                 !SaveState.Instance.IsLocationChecked("Far Fields - Rosary Cache #19"))) &&
                (path == "Boss Scene" || path == "Ant Trapper Quest Scene (3)" || path == "ant_item_string" ||
                 path == "ant_item_string/Item Holder/Collectable Item Pickup")) return "Ant Trapper";
            if (scene == "Slab_16" && Pending("Broodmother's Eye") && path == "Broodmother Trail Quest Parent")
                return "Broodmother Hunt";
            return null;
        }

        [HarmonyPatch]
        private static class SourceScope
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(TestGameObjectActivator), "Evaluate");
                yield return AccessTools.Method(typeof(SceneAdditiveLoadConditional), "TryTestLoad");
            }

            private static void Prefix(Component __instance, out string __state)
            {
                __state = sourceQuest;
                sourceQuest = QuestFor(__instance);
            }

            private static Exception Finalizer(Exception __exception, string __state)
            {
                sourceQuest = __state;
                return __exception;
            }
        }

        [HarmonyPatch(typeof(QuestPlaymakerActions.QuestFsmAction), "OnEnter")]
        private static class BeastflyScope
        {
            private static void Prefix(QuestPlaymakerActions.QuestFsmAction __instance, out string __state)
            {
                __state = sourceQuest;
                if (__instance.Fsm?.GameObject?.scene.name == "Bone_East_08" &&
                    __instance.Fsm.Name == "Control" && __instance.State?.Name == "Beastfly?" &&
                    Utils.GetHierarchyPath(__instance.Fsm.GameObject.transform) == "Boss Scene" && Pending("Horn Fragment"))
                    sourceQuest = "Beastfly Hunt";
            }

            private static Exception Finalizer(Exception __exception, string __state)
            {
                sourceQuest = __state;
                return __exception;
            }
        }

        [HarmonyPatch]
        private static class KeepSource
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.PropertyGetter(typeof(FullQuestBase), "IsCompleted");
                yield return AccessTools.PropertyGetter(typeof(FullQuestBase), "CanComplete");
            }

            private static void Postfix(FullQuestBase __instance, ref bool __result)
            {
                if (sourceQuest != null && __instance.name == sourceQuest) __result = false;
            }
        }

        [HarmonyPatch(typeof(GameManager), "RemoveIncompleteActiveQuest")]
        private static class KeepCrown
        {
            private static bool Prefix(string questName) => questName != "Skull King" ||
                SaveState.Instance?.IsRoomBound != true || !SaveState.Instance.IsRandomized(ItemType.QuestCollectible);
        }
    }
}
