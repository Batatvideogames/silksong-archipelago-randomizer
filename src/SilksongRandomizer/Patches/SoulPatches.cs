using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using QuestPlaymakerActions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SilksongRandomizer.Patches
{
    internal static class SoulPatches
    {
        internal static bool TryGetOwnedAmount(ToolItem tool, out int amount)
        {
            amount = 0;
            SaveState state = SaveState.Instance;
            if (state == null || !state.IsRandomized(ItemType.Tool) ||
                tool == null || tool.name != "Silk Snare")
            {
                return false;
            }
            amount = state.receivedItems != null && state.receivedItems.Contains(
                ItemSet.GetCanonicalItemName("Tool: Snare Setter")) ? 1 : 0;
            return true;
        }

        [HarmonyPatch(typeof(ToolItem), nameof(ToolItem.GetSavedAmount))]
        private static class TurnInAmount
        {
            [HarmonyPrefix]
            private static bool Prefix(ToolItem __instance, ref int __result)
            {
                if (!TryGetOwnedAmount(__instance, out int amount)) return true;
                __result = amount;
                return false;
            }
        }

        [HarmonyPatch(typeof(CheckIfToolUnlocked), "get_IsTrue")]
        private static class CaretakerToolDialogue
        {
            [HarmonyPostfix]
            private static void Postfix(CheckIfToolUnlocked __instance, ref bool __result)
            {
                if (__instance.Owner == null ||
                    __instance.Owner.scene.name != "Song_Enclave" ||
                    __instance.Owner.name != "Enclave Caretaker" ||
                    __instance.Fsm?.Name != "Dialogue" ||
                    __instance.Tool == null || __instance.Tool.IsNone)
                {
                    return;
                }
                if (TryGetOwnedAmount(__instance.Tool.Value as ToolItem, out int amount))
                {
                    __result = amount > 0;
                }
            }
        }

        private static string Source(string scene, string owner)
        {
            if (scene == "Bonetown" && owner == "Churchkeeper") return "Maiden Soul";
            if (scene == "Belltown_basement_03" && owner == "Bell Hermit") return "Hermit Soul";
            return null;
        }

        private static bool IsActive(string source)
        {
            SaveState state = SaveState.Instance;
            return source != null && state != null && state.IsRandomized(ItemType.Soul) &&
                state.IsLocationEnabled(source) && state.IsLocationInSeed(source);
        }

        private static string Source(FsmStateAction action)
        {
            if (action?.Owner == null || action.Fsm == null) return null;
            string source = Source(action.Owner.scene.name, action.Owner.name);
            string fsm = source == "Maiden Soul" ? "Conversation" : "Dialogue";
            return action.Fsm.Name == fsm && IsActive(source) ? source : null;
        }

        [HarmonyPatch(typeof(DeactivateIfPlayerdataTrue), "ForceEvaluate")]
        private static class DropUncheckedSource
        {
            [HarmonyPrefix]
            private static void Prefix(DeactivateIfPlayerdataTrue __instance)
            {
                string source = Source(__instance.gameObject.scene.name, __instance.gameObject.name);
                if (!IsActive(source) || SaveState.Instance.IsLocationChecked(source) ||
                    (__instance.boolName != "soulSnareReady" && __instance.boolName != "blackThreadWorld") ||
                    !PlayerData.instance.GetBool(__instance.boolName)) return;
                SavedItem proxy = CollectibleSourcePatches.GetProxyItem(source, ItemType.Soul, true);
                foreach (CollectableItemPickup existing in Resources.FindObjectsOfTypeAll<CollectableItemPickup>())
                {
                    if (existing != null && existing.gameObject.scene == __instance.gameObject.scene &&
                        existing.Item == proxy) return;
                }
                CollectableItemPickup prefab = GlobalSettings.Gameplay.CollectableItemPickupInstantPrefab;
                if (prefab == null) return;
                CollectableItemPickup pickup = UnityEngine.Object.Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(pickup.gameObject, __instance.gameObject.scene);
                pickup.transform.position = __instance.transform.position + new Vector3(0f, 0.5f, 0f);
                pickup.SetItem(proxy);
            }
        }

        [HarmonyPatch(typeof(CheckQuestState), "DoQuestAction")]
        private static class OfferAfterCompletion
        {
            [HarmonyPrefix]
            private static bool Prefix(CheckQuestState __instance, FullQuestBase quest)
            {
                string source = Source(__instance);
                if (source == null || quest?.name != "Soul Snare" || !quest.IsCompleted ||
                    SaveState.Instance.IsLocationChecked(source)) return true;
                __instance.Fsm.Event(__instance.TrackedEvent);
                return false;
            }
        }

        [HarmonyPatch(typeof(GetQuestState), "DoQuestAction")]
        private static class ReadSourceCompletion
        {
            [HarmonyPostfix]
            private static void Postfix(GetQuestState __instance, FullQuestBase quest)
            {
                if (Source(__instance) != null && __instance.State?.Name == "Snare Soul Dlg" &&
                    quest?.name == "Soul Snare") __instance.IsCompleted.Value = false;
            }
        }

        [HarmonyPatch(typeof(CollectableItemGetDataV2), "DoAction")]
        private static class ReadSourceReward
        {
            [HarmonyPostfix]
            private static void Postfix(CollectableItemGetDataV2 __instance)
            {
                string source = Source(__instance);
                if (source == null || __instance.State?.Name != "Snare Soul Dlg") return;
                bool collected = SaveState.Instance.IsLocationChecked(source);
                __instance.CollectedAmount.Value = collected ? 1 : 0;
                __instance.IsHoldingAny.Value = collected;
            }
        }

        [HarmonyPatch(typeof(SavedItemGetV2), "OnEnter")]
        private static class CollectSource
        {
            [HarmonyPrefix]
            private static bool Prefix(SavedItemGetV2 __instance)
            {
                string source = Source(__instance);
                if (source == null || __instance.State?.Name != "Give Soul") return true;
                string asset = source == "Maiden Soul" ? "Snare Soul Churchkeeper" : "Snare Soul Bell Hermit";
                if (__instance.Item?.Value?.name != asset) return true;
                CollectibleSourcePatches.GetProxyItem(source, ItemType.Soul, true).Get(false);
                __instance.Finish();
                return false;
            }
        }
    }
}
