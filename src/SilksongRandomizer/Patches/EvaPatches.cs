using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using System;
using System.Collections;

namespace SilksongRandomizer.Patches
{
    internal static class EvaPatches
    {
        internal const string FirstEvolution = "Hunter Evolution 1";
        internal const string SecondEvolution = "Hunter Evolution 2";
        internal const string YellowSource = "Yellow Vesticrest";
        internal const string BlueSource = "Blue Vesticrest";
        internal const string EvolutionItem = "Evolved Hunter Crest";
        internal const string SylphsongSource = "Sylphsong";

        private static bool IsEva(FsmStateAction action)
        {
            return action?.Owner != null && action.Owner.scene.name == "Weave_10" &&
                action.Owner.name == "Crest Upgrade Shrine" && action.Fsm?.Name == "Dialogue";
        }

        private static bool IsRandomized(string location)
        {
            SaveState state = SaveState.Instance;
            return state != null && state.IsLocationEnabled(location) && state.IsLocationInSeed(location);
        }

        private static bool TracksEvolution(string source)
        {
            SaveState state = SaveState.Instance;
            return source != null && state != null &&
                (IsRandomized(source) || !state.IsRandomized(ItemType.Eva));
        }

        private static bool EvolutionCollected(string source)
        {
            return IsRandomized(source)
                ? SaveState.Instance.IsLocationChecked(source)
                : SaveState.Instance.evaVanillaRewards.Contains(source);
        }

        private static string EvolutionSource(ToolCrest crest)
        {
            return crest?.name == "Hunter_v2" ? FirstEvolution :
                crest?.name == "Hunter_v3" ? SecondEvolution : null;
        }

        private static string SlotSource(string field)
        {
            return field == "UnlockedExtraYellowSlot" ? YellowSource :
                field == "UnlockedExtraBlueSlot" ? BlueSource : null;
        }

        internal static void GrantSylphsong()
        {
            PlayerData.instance.HasBoundCrestUpgrader = true;
        }

        [HarmonyPatch(typeof(PlayerDataVariableTest), "OnEnter")]
        private static class ReadSylphsongSource
        {
            [HarmonyPrefix]
            private static bool Prefix(PlayerDataVariableTest __instance)
            {
                if (!IsEva(__instance) || !IsRandomized(SylphsongSource) ||
                    __instance.VariableName?.Value != "HasBoundCrestUpgrader") return true;
                bool collected = SaveState.Instance.IsLocationChecked(SylphsongSource);
                __instance.Fsm.Event(collected.Equals(__instance.ExpectedValue.GetValue())
                    ? __instance.IsExpectedEvent : __instance.IsNotExpectedEvent);
                __instance.Finish();
                return false;
            }
        }

        [HarmonyPatch(typeof(SetPlayerDataVariable), "OnEnter")]
        private static class CollectSylphsong
        {
            [HarmonyPrefix]
            private static bool Prefix(SetPlayerDataVariable __instance)
            {
                if (!IsEva(__instance) || !IsRandomized(SylphsongSource) ||
                    __instance.VariableName?.Value != "HasBoundCrestUpgrader") return true;
                SaveState.Instance.CheckLocation(SylphsongSource);
                __instance.Finish();
                return false;
            }
        }

        [HarmonyPatch(typeof(SpawnPowerUpGetMsg), "OnEnter")]
        private static class SylphsongPopup
        {
            [HarmonyPrefix]
            private static bool Prefix(SpawnPowerUpGetMsg __instance)
            {
                if (!IsEva(__instance) || !IsRandomized(SylphsongSource) ||
                    __instance.State?.Name != "Set Bound") return true;
                __instance.Finish();
                return false;
            }
        }

        internal static void GrantHunter()
        {
            if (!Utils.ForceCrest("Hunter"))
                throw new InvalidOperationException("Hunter crest is not ready.");
            ApplyEvolution(SaveState.Instance.hunterEvolutionCount, true);
        }

        internal static void GrantEvolution()
        {
            SaveState state = SaveState.Instance;
            int count = Math.Min(2, state.hunterEvolutionCount + 1);
            ApplyEvolution(count, state.receivedItems.Contains("Crest: Hunter") || !state.IsRandomized(ItemType.Crest));
            state.hunterEvolutionCount = count;
        }

        internal static void ApplyEvolution(int count, bool ownsHunter)
        {
            if (!ownsHunter || count <= 0) return;
            ToolCrest crest = ToolItemManager.GetCrestByName(count >= 2 ? "Hunter_v3" : "Hunter_v2");
            if (crest == null) throw new InvalidOperationException("Hunter evolution is not ready.");
            string equippedCrest = PlayerData.instance.CurrentCrestID;
            bool previous = ToolPatches.canCrestBeUnlockedByRandomizer;
            ToolPatches.canCrestBeUnlockedByRandomizer = true;
            try { crest.Unlock(); }
            finally { ToolPatches.canCrestBeUnlockedByRandomizer = previous; }
            if (crest.IsEquipped && equippedCrest != crest.name)
                ToolItemManager.SendEquippedChangedEvent(true);
        }

        [HarmonyPatch(typeof(GetIsCrestUnlocked), "get_IsTrue")]
        private static class ReadEvolutionSource
        {
            [HarmonyPrefix]
            private static bool Prefix(GetIsCrestUnlocked __instance, ref bool __result)
            {
                if (!IsEva(__instance)) return true;
                string source = EvolutionSource(__instance.Crest?.Value as ToolCrest);
                if (!TracksEvolution(source)) return true;
                __result = EvolutionCollected(source);
                return false;
            }
        }

        [HarmonyPatch(typeof(UnlockCrest), "OnEnter")]
        private static class CollectEvolution
        {
            [HarmonyPrefix]
            private static bool Prefix(UnlockCrest __instance)
            {
                if (!IsEva(__instance)) return true;
                string source = EvolutionSource(__instance.Crest?.Value as ToolCrest);
                if (!TracksEvolution(source)) return true;
                if (IsRandomized(source)) SaveState.Instance.CheckLocation(source);
                else if (!EvolutionCollected(source))
                {
                    GrantEvolution();
                    SaveState.Instance.evaVanillaRewards.Add(source);
                }
                __instance.Finish();
                return false;
            }
        }

        [HarmonyPatch(typeof(GetPlayerDataVariable), "OnEnter")]
        private static class ReadVesticrestSource
        {
            [HarmonyPrefix]
            private static bool Prefix(GetPlayerDataVariable __instance)
            {
                if (!IsEva(__instance)) return true;
                string source = SlotSource(__instance.VariableName?.Value);
                if (source == null || !IsRandomized(source)) return true;
                __instance.StoreValue.SetValue(SaveState.Instance.IsLocationChecked(source));
                __instance.Finish();
                return false;
            }
        }

        [HarmonyPatch(typeof(SetPlayerDataVariable), "OnEnter")]
        private static class CollectVesticrest
        {
            [HarmonyPrefix]
            private static bool Prefix(SetPlayerDataVariable __instance)
            {
                if (!IsEva(__instance)) return true;
                string source = SlotSource(__instance.VariableName?.Value);
                if (source == null || !IsRandomized(source)) return true;
                SaveState.Instance.CheckLocation(source);
                __instance.Finish();
                return false;
            }
        }

        [HarmonyPatch(typeof(AutoEquipCrestV4), "OnEnter")]
        private static class PreserveEquippedCrest
        {
            [HarmonyPrefix]
            private static bool Prefix(AutoEquipCrestV4 __instance)
            {
                if (!IsEva(__instance) || SaveState.Instance == null) return true;
                __instance.Finish();
                return false;
            }
        }

        private static bool IsRewardPopup(FsmStateAction action)
        {
            return IsEva(action) && IsRandomized(FirstEvolution) &&
                (action.State?.Name == "Combo Bar Prompt" ||
                 action.State?.Name == "Unlock First Slot" ||
                 action.State?.Name == "Unlock Other Slot");
        }

        private static IEnumerator CompletePopup(Fsm fsm, string state)
        {
            yield return null;
            if (fsm != null && fsm.ActiveStateName == state)
                fsm.Event("GET ITEM MSG END");
        }

        [HarmonyPatch(typeof(CreateObject), "OnEnter")]
        private static class RewardPopup
        {
            [HarmonyPrefix]
            private static bool Prefix(CreateObject __instance)
            {
                if (!IsRewardPopup(__instance) || RandomizerPlugin.Instance == null) return true;
                __instance.Finish();
                RandomizerPlugin.Instance.StartCoroutine(CompletePopup(__instance.Fsm, __instance.State.Name));
                return false;
            }
        }

        [HarmonyPatch(typeof(SetFsmString), "OnEnter")]
        private static class RewardPopupText
        {
            [HarmonyPrefix]
            private static bool Prefix(SetFsmString __instance)
            {
                if (!IsRewardPopup(__instance) || RandomizerPlugin.Instance == null) return true;
                __instance.Finish();
                return false;
            }
        }

        internal static int CountPoints(SaveState state)
        {
            int points = 0;
            foreach (ToolCrest crest in ToolItemManager.GetAllCrests())
            {
                if (crest == null || crest.IsHidden || !crest.IsBaseVersion ||
                    CrestNames.IsHunterInternalName(crest.name)) continue;
                bool owned = state.IsRandomized(ItemType.Crest)
                    ? state.receivedItems.Contains(CrestNames.GetItemNameFromInternal(crest.name))
                    : crest.IsUnlocked;
                if (!owned) continue;
                for (int index = 0; index < crest.Slots.Length; index++)
                {
                    ToolCrest.SlotInfo slot = crest.Slots[index];
                    if (!slot.IsLocked) { points++; continue; }
                    if (state.IsRandomized(ItemType.CrestSlot))
                    {
                        string item = ItemSet.GetCanonicalItemName(
                            CrestNames.GetPublicCrestName(crest.name) + " Slot: " + slot.Type + " " +
                            (int)slot.Position.x + " " + (int)slot.Position.y);
                        if (state.receivedItems.Contains(item)) points++;
                    }
                    else
                    {
                        var slots = crest.SaveData.Slots;
                        if (slots != null && index < slots.Count && slots[index].IsUnlocked) points++;
                    }
                }
            }
            return points;
        }

        [HarmonyPatch(typeof(CountCrestUnlockPoints), "OnEnter")]
        private static class CrestPoints
        {
            [HarmonyPrefix]
            private static bool Prefix(CountCrestUnlockPoints __instance)
            {
                SaveState state = SaveState.Instance;
                if (!IsEva(__instance) || state == null ||
                    (!state.IsRandomized(ItemType.Crest) && !state.IsRandomized(ItemType.CrestSlot) && !state.IsRandomized(ItemType.Eva))) return true;
                __instance.StoreCurrentPoints.Value = CountPoints(state);
                __instance.StoreMaxPoints.Value = 37;
                __instance.Finish();
                return false;
            }
        }
    }
}
