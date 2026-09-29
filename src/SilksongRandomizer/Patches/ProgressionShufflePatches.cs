using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using GlobalEnums;
using HutongGames.PlayMaker.Actions;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    internal static class ProgressionShufflePatches
    {
        private static readonly Dictionary<string, string> BossFlags = ProgressionCatalogue.Map("boss_flags");

        private static readonly Dictionary<string, string> StoryComponents = ProgressionCatalogue.Map("boss_story_components", true);

        private static readonly Dictionary<string, string> StoryActions = ProgressionCatalogue.Map("boss_story_actions", true);

        [ThreadStatic] private static string storyOwner;
        [ThreadStatic] private static int questAvailabilityDepth;

        internal static bool StoryCredit(string boss, bool native) =>
            SaveState.Instance?.progressionShuffle?.TryGetBossCredit(boss, out bool completed) == true
                ? completed : native;

        private static bool StoryFlag(bool native, string field) =>
            BossFlags.TryGetValue(field, out string boss) ? StoryCredit(boss, native) : native;

        private static bool TryComponentCredit(string owner, string field, out bool value)
        {
            value = false;
            return owner != null && StoryComponents.TryGetValue(owner + "|" + field, out string boss) &&
                SaveState.Instance?.progressionShuffle?.TryGetBossCredit(boss, out value) == true;
        }

        private static string OwnerKey(Component owner) =>
            owner == null ? null : owner.gameObject.scene.name + "|" + Utils.GetHierarchyPath(owner.transform);

        private static bool TryActionCredit(FsmStateAction action, string field, out bool value)
        {
            value = false;
            if (SaveState.Instance?.progressionShuffle?.HasBossAssignments != true ||
                !BossFlags.ContainsKey(field ?? string.Empty) ||
                action?.Fsm?.GameObject == null || action.State == null) return false;
            string key = action.Fsm.GameObject.scene.name + "|" + Utils.GetHierarchyPath(action.Fsm.GameObject.transform) +
                "|" + action.Fsm.Name + "|" + action.State.Name + "|" + field;
            return StoryActions.TryGetValue(key, out string boss) &&
                SaveState.Instance?.progressionShuffle?.TryGetBossCredit(boss, out value) == true;
        }

        [HarmonyPatch]
        private static class StoryComponentScopePatch
        {
            [HarmonyTargetMethods]
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(TestGameObjectActivator), "Evaluate");
                yield return AccessTools.Method(typeof(PlayerDataTestResponse), "Evaluate");
                yield return AccessTools.Method(typeof(SceneAdditiveLoadConditional), "TryTestLoad");
                yield return AccessTools.Method(typeof(DeactivatePlayerDataTest), "OnEnable");
                yield return AccessTools.Method(typeof(CrossSceneWalker), "Start");
                yield return AccessTools.Method(typeof(AreaTitleController), "GetAreaTitle");
            }

            [HarmonyPrefix]
            private static void Prefix(Component __instance, out string __state)
            {
                __state = storyOwner;
                storyOwner = OwnerKey(__instance);
            }

            [HarmonyFinalizer]
            private static Exception Finalizer(Exception __exception, string __state)
            {
                storyOwner = __state;
                return __exception;
            }
        }

        [HarmonyPatch]
        private static class QuestAppearanceScopePatch
        {
            [HarmonyTargetMethods]
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.PropertyGetter(typeof(FullQuestBase), "IsAvailable");
                yield return AccessTools.PropertyGetter(typeof(ShopItem), "IsAvailable");
            }

            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix() { questAvailabilityDepth++; }

            [HarmonyFinalizer]
            private static Exception Finalizer(Exception __exception)
            {
                questAvailabilityDepth--;
                return __exception;
            }
        }

        [HarmonyPatch(typeof(PlayerDataBoolTest), nameof(PlayerDataBoolTest.OnEnter))]
        private static class StoryBoolActionPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(PlayerDataBoolTest __instance)
            {
                if (!TryActionCredit(__instance, __instance.boolName.Value, out bool value)) return true;
                __instance.Fsm.Event(value ? __instance.isTrue : __instance.isFalse);
                __instance.Finish();
                return false;
            }
        }

        [HarmonyPatch(typeof(GetPlayerDataBool), nameof(GetPlayerDataBool.OnEnter))]
        private static class StoryGetBoolActionPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(GetPlayerDataBool __instance)
            {
                if (!TryActionCredit(__instance, __instance.boolName.Value, out bool value)) return true;
                __instance.storeValue.Value = value;
                __instance.Finish();
                return false;
            }
        }

        [HarmonyPatch(typeof(PlayerDataVariableTest), nameof(PlayerDataVariableTest.OnEnter))]
        private static class StoryVariableActionPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(PlayerDataVariableTest __instance)
            {
                if (__instance.VariableName.IsNone || __instance.ExpectedValue.IsNone ||
                    __instance.ExpectedValue.RealType != typeof(bool) ||
                    !TryActionCredit(__instance, __instance.VariableName.Value, out bool value)) return true;
                __instance.Fsm.Event(value.Equals(__instance.ExpectedValue.GetValue()) ?
                    __instance.IsExpectedEvent : __instance.IsNotExpectedEvent);
                __instance.Finish();
                return false;
            }
        }

        [HarmonyPatch(typeof(GetPlayerDataVariable), nameof(GetPlayerDataVariable.OnEnter))]
        private static class StoryGetVariableActionPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(GetPlayerDataVariable __instance)
            {
                if (__instance.VariableName.IsNone || __instance.StoreValue.IsNone ||
                    __instance.StoreValue.RealType != typeof(bool) ||
                    !TryActionCredit(__instance, __instance.VariableName.Value, out bool value)) return true;
                __instance.StoreValue.SetValue(value);
                __instance.Finish();
                return false;
            }
        }

        [HarmonyPatch(typeof(DeactivateIfPlayerdataTrue), "ForceEvaluate")]
        private static class StoryDeactivateTruePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(DeactivateIfPlayerdataTrue __instance)
            {
                if (!TryComponentCredit(OwnerKey(__instance), __instance.boolName, out bool value)) return true;
                if (value) (__instance.objectToDeactivate != null ? __instance.objectToDeactivate : __instance.gameObject).SetActive(false);
                return false;
            }
        }

        [HarmonyPatch(typeof(DeactivateIfPlayerdataFalse), "ForceEvaluate")]
        private static class StoryDeactivateFalsePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(DeactivateIfPlayerdataFalse __instance)
            {
                if (!TryComponentCredit(OwnerKey(__instance), __instance.boolName, out bool value)) return true;
                if (!value) (__instance.objectToDeactivate != null ? __instance.objectToDeactivate : __instance.gameObject).SetActive(false);
                return false;
            }
        }

        [HarmonyPatch(typeof(ActivateIfPlayerdataTrue), "Start")]
        private static class StoryActivateTruePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(ActivateIfPlayerdataTrue __instance)
            {
                if (!TryComponentCredit(OwnerKey(__instance), __instance.boolName, out bool value)) return true;
                if (value)
                {
                    __instance.gameObject.SetActive(true);
                    if (__instance.objectToActivate != null) __instance.objectToActivate.SetActive(true);
                }
                return false;
            }
        }

        private static bool CogworkCredit(PlayerData playerData) =>
            SaveState.Instance?.progressionShuffle?.TryGetBossCredit("Boss: Cogwork Dancers", out bool credit) == true
                ? credit : playerData.defeatedCogworkDancers;

        private static bool VoltvyrmCredit(PlayerData playerData) =>
            SaveState.Instance?.progressionShuffle?.TryGetBossCredit("Boss: Voltvyrm", out bool credit) == true
                ? credit : playerData.defeatedZapCoreEnemy;

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.TimePasses))]
        private static class BossStoryCreditPatch
        {
            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                int splinterReads = 0;
                foreach (CodeInstruction instruction in instructions)
                {
                    if (instruction.opcode == OpCodes.Ldfld && instruction.operand is FieldInfo field &&
                        field.DeclaringType == typeof(PlayerData))
                    {
                        string reader = field.Name == "defeatedCogworkDancers" ? nameof(CogworkCredit) :
                            field.Name == "defeatedZapCoreEnemy" ? nameof(VoltvyrmCredit) : null;
                        if (reader != null)
                        {
                            instruction.opcode = OpCodes.Call;
                            instruction.operand = typeof(ProgressionShufflePatches).GetMethod(reader,
                                BindingFlags.Static | BindingFlags.NonPublic);
                        }
                    }
                    yield return instruction;
                    if (instruction.opcode == OpCodes.Ldfld && instruction.operand is FieldInfo storyField &&
                        storyField.DeclaringType == typeof(PlayerData) &&
                        (storyField.Name == "spinnerDefeated" || storyField.Name == "defeatedCoralDrillers" ||
                         storyField.Name == "skullKingDefeated" || storyField.Name == "DefeatedSwampShaman" ||
                         storyField.Name == "defeatedFlowerQueen" ||
                         (storyField.Name == "defeatedSplinterQueen" && ++splinterReads == 1)))
                    {
                        yield return new CodeInstruction(OpCodes.Ldstr, storyField.Name);
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ProgressionShufflePatches), nameof(StoryFlag)));
                    }
                }
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.SethTravelCheck))]
        private static class SethStoryTravelPatch
        {
            [HarmonyTranspiler]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                foreach (CodeInstruction instruction in instructions)
                {
                    yield return instruction;
                    if (instruction.opcode == OpCodes.Ldfld && instruction.operand is FieldInfo field &&
                        field.DeclaringType == typeof(PlayerData) && field.Name == "defeatedFlowerQueen")
                    {
                        yield return new CodeInstruction(OpCodes.Ldstr, field.Name);
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ProgressionShufflePatches), nameof(StoryFlag)));
                    }
                }
            }
        }

        [HarmonyPatch(typeof(GameManager), "StartAct3")]
        private static class PreserveUnfoughtBossesPatch
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                var state = SaveState.Instance?.progressionShuffle;
                PlayerData playerData = PlayerData.instance;
                if (state == null || playerData == null) return;
                if (state.TryGetBossDefeat("Boss: Widow", out bool widow))
                    playerData.spinnerDefeated = widow;
                if (state.TryGetBossDefeat("Boss: Fourth Chorus", out bool chorus))
                    playerData.defeatedSongGolem = chorus;
                if (state.TryGetBossDefeat("Boss: Trobbio", out bool trobbio))
                    playerData.defeatedTrobbio = trobbio;
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.TimePasses))]
        private static class PreserveSkullTyrantInvasionPatch
        {
            [HarmonyPostfix]
            private static void Postfix(GameManager __instance)
            {
                var state = SaveState.Instance?.progressionShuffle;
                PlayerData playerData = PlayerData.instance;
                if (state?.TryGetBossDefeat("Boss: Skull Tyrant (Bone Bottom)", out bool defeated) != true ||
                    defeated || playerData == null || playerData.skullKingKilled || playerData.blackThreadWorld ||
                    QuestManager.GetQuest("Soul Snare")?.IsAccepted != true ||
                    !StoryCredit("Boss: Skull Tyrant (The Marrow)", playerData.skullKingDefeated) ||
                    !(playerData.visitedCitadel || playerData.visitedCoral || playerData.visitedDustpens)) return;
                MapZone zone = __instance.GetCurrentMapZoneEnum();
                if (zone != MapZone.BONETOWN && zone != MapZone.PATH_OF_BONE && zone != MapZone.MOSS_CAVE &&
                    UnityEngine.Random.Range(1, 100) <= 30)
                    playerData.skullKingWillInvade = true;
            }
        }

        [ThreadStatic] private static bool updatingStory;

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.TimePasses))]
        private static class WishStoryUpdatePatch
        {
            [HarmonyPrefix]
            private static void Prefix(out bool __state)
            {
                CaptureAvailableOffers();
                __state = updatingStory;
                updatingStory = true;
            }

            [HarmonyFinalizer]
            private static Exception Finalizer(Exception __exception, bool __state)
            {
                updatingStory = __state;
                if (__exception == null) CaptureAvailableOffers();
                return __exception;
            }
        }

        [HarmonyPatch(typeof(FullQuestBase), "get_IsCompleted")]
        private static class PreserveSentinelOfferPatch
        {
            [HarmonyPostfix]
            private static void Postfix(FullQuestBase __instance, ref bool __result)
            {
                if (!updatingStory || !__result || __instance.name != "Song Knight") return;
                string target = SaveState.Instance?.progressionShuffle?.AssignedWish("Song Knight");
                if (target != null && target != "Song Knight" && QuestManager.GetQuest(target)?.IsCompleted == false)
                    __result = false;
            }
        }

        [HarmonyPatch(typeof(PlayerDataTest.Test), nameof(PlayerDataTest.Test.IsFulfilled))]
        private static class StoryComponentTestPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(PlayerDataTest.Test __instance, ref bool __result)
            {
                if (nativeOfferIdentity == "Belltown House Mid" &&
                    __instance.Type == PlayerDataTest.TestType.Enum &&
                    __instance.FieldName == "BelltownHouseState" &&
                    __instance.NumType == PlayerDataTest.NumTestType.Equal && __instance.IntValue == 1)
                {
                    __result = PlayerData.instance.BelltownHouseState == BelltownHouseStates.Half ||
                        PlayerData.instance.BelltownHouseState == BelltownHouseStates.Full;
                    return false;
                }
                if (__instance.Type == PlayerDataTest.TestType.Bool)
                {
                    bool value;
                    bool matched = TryComponentCredit(storyOwner, __instance.FieldName, out value);
                    if (!matched && questAvailabilityDepth != 0 && BossFlags.TryGetValue(__instance.FieldName, out string boss))
                        matched = SaveState.Instance?.progressionShuffle?.TryGetBossCredit(boss, out value) == true;
                    if (matched)
                    {
                        __result = value == __instance.BoolValue;
                        return false;
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(GameManager), "RemoveIncompleteActiveQuest")]
        private static class PreserveShuffledCollectionPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(string questName) => (questName != "Rock Rollers" && questName != "Skull King") ||
                SaveState.Instance?.progressionShuffle?.OfferForWish(questName) == null;
        }

        [ThreadStatic] private static int nativeOfferDepth;
        [ThreadStatic] private static string nativeOfferIdentity;
        [ThreadStatic] private static int boardTurnInDepth;

        internal static bool IsAssignedOfferRead(string identity) => nativeOfferDepth == 0 &&
            SaveState.Instance?.progressionShuffle?.OfferForWish(identity) != null;

        private static bool ReadNativeOffer(string identity)
        {
            FullQuestBase offer = QuestManager.GetQuest(identity);
            if (offer == null) return false;
            string previousOffer = nativeOfferIdentity;
            nativeOfferIdentity = identity;
            nativeOfferDepth++;
            try { return offer.IsAvailable; }
            finally
            {
                nativeOfferDepth--;
                nativeOfferIdentity = previousOffer;
            }
        }

        private static void CaptureAvailableOffers()
        {
            var state = SaveState.Instance?.progressionShuffle;
            if (state == null || !state.HasWishAssignments || nativeOfferDepth != 0) return;
            bool changed = false;
            foreach (string source in state.WishOfferIds)
                if (!ProgressionShuffleState.IsNpcWish(source) && !state.IsWishOfferUnlocked(source) && ReadNativeOffer(source))
                    changed |= state.RecordWishOfferUnlocked(source);
            if (changed) GameManager.instance?.QueueSaveGame();
        }

        private sealed class NpcWishShopItem : ISimpleShopItem
        {
            internal readonly FullQuestBase Quest;
            internal NpcWishShopItem(FullQuestBase quest) { Quest = quest; }
            public string GetDisplayName() => Quest.DisplayName;
            public Sprite GetIcon() => Quest.QuestType.Icon;
            public int GetCost() => 0;
            public bool DelayPurchase() => true;
        }

        private static readonly Dictionary<SimpleQuestsShopOwner, FullQuestBase> QueuedNpcWishes =
            new Dictionary<SimpleQuestsShopOwner, FullQuestBase>();

        internal static void ClearPendingInteractions() => QueuedNpcWishes.Clear();

        private static bool HasShuffledCourierOffers() =>
            SaveState.Instance?.progressionShuffle?.WishOfferIds.Any(
                name => name.StartsWith("Courier Delivery ", StringComparison.Ordinal)) == true;

        private static bool IsCourierAction(FsmStateAction action, string state) =>
            IsNpcAction(action, "Belltown", "Couriers States/Here/Couriers Quest Giver", "Dialogue", state);

        [HarmonyPatch(typeof(BoolTest), nameof(BoolTest.OnEnter))]
        private static class CourierMenuAccessPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(BoolTest __instance)
            {
                if (!IsCourierAction(__instance, "Has Met?") || !HasShuffledCourierOffers() ||
                    __instance.boolVariable.Name != "Any In Progress" || __instance.isTrue?.Name != "IN PROGRESS")
                    return true;
                __instance.Fsm.Event(__instance.isFalse);
                __instance.Finish();
                return false;
            }
        }

        [HarmonyPatch(typeof(CheckIfCrestEquipped), "get_IsTrue")]
        private static class CourierCurseMenuPatch
        {
            [HarmonyPostfix]
            private static void Postfix(CheckIfCrestEquipped __instance, ref bool __result)
            {
                if (__result && __instance.trueEvent?.Name == "CURSED" &&
                    (IsCourierAction(__instance, "Has Met?") || IsCourierAction(__instance, "Shop?")) &&
                    HasShuffledCourierOffers())
                    __result = false;
            }
        }

        private static bool NeedsNpcWishInteraction(FullQuestBase quest) => quest != null && !quest.IsCompleted &&
            (!quest.IsAccepted || (!ProgressionShuffleState.HasNativeWishFinish(quest.name) && quest.CanComplete));

        private static void CompleteNpcWish(FullQuestBase quest, Action finished)
        {
            if (!quest.IsAccepted || quest.IsCompleted || !quest.CanComplete ||
                ProgressionShuffleState.HasNativeWishFinish(quest.name)) { finished(); return; }
            quest.TryEndQuest(() => {
                if (quest.IsCompleted && quest.RewardItem != null)
                    quest.RewardItem.Get(quest.RewardCount);
                finished();
            }, consumeCurrency: true);
        }

        private static void OpenNpcWish(FullQuestBase quest, Action finished)
        {
            if (!quest.IsAccepted)
                QuestYesNoBox.Open(finished, finished, false, quest, beginQuest: true);
            else if (!NeedsNpcWishInteraction(quest))
                QuestYesNoBox.Open(finished, finished, false, quest, beginQuest: false);
            else
                DialogueYesNoBox.Open(() => CompleteNpcWish(quest, finished), finished, false,
                    "Turn in " + quest.DisplayName + "?", quest.Targets.Select(target => target.Counter).ToList(),
                    quest.Targets.Select(target => target.Count).ToList(), displayHudPopup: false,
                    consumeCurrency: false, null);
        }

        [HarmonyPatch(typeof(SimpleQuestsShopOwner), "GetItems")]
        private static class NpcWishShopItemsPatch
        {
            [HarmonyPrefix]
            private static void Prefix() => nativeOfferDepth++;

            [HarmonyFinalizer]
            private static Exception Finalizer(Exception __exception)
            {
                nativeOfferDepth--;
                return __exception;
            }

            [HarmonyPostfix]
            private static void Postfix(SimpleQuestsShopOwner __instance,
                List<SimpleQuestsShopOwner.ShopItemInfo> ___quests, ref List<ISimpleShopItem> ___currentList,
                ref List<ISimpleShopItem> __result)
            {
                var state = SaveState.Instance?.progressionShuffle;
                if (state == null || !___quests.Any(item => item.CustomDelivery == null &&
                    item.Quest != null && state.AssignedWish(item.Quest.name) != null)) return;
                int completed = ___quests.Count(item => item.Quest != null && item.Quest.IsCompleted);
                var items = (__result ?? new List<ISimpleShopItem>()).Where(item =>
                    !(item is NpcWishShopItem) &&
                    (!(item is SimpleQuestsShopOwner.ShopItemInfo entry) || entry.CustomDelivery != null ||
                     entry.Quest == null || state.AssignedWish(entry.Quest.name) == null)).ToList();
                foreach (var item in ___quests)
                {
                    bool prerequisites = item.AppearCondition.IsFulfilled &&
                        completed >= item.AppearAfterCountCompleted &&
                        item.AppearAfterCompleted.All(quest => quest == null || quest.IsCompleted);
                    if (item.CustomDelivery != null || item.Quest == null) continue;
                    string target = state.AssignedWish(item.Quest.name);
                    if (target == null) continue;
                    bool available = state.IsWishOfferUnlocked(item.Quest.name) ||
                        (prerequisites && ReadNativeOffer(item.Quest.name));
                    if (!available) continue;
                    if (state.RecordWishOfferUnlocked(item.Quest.name)) GameManager.instance?.QueueSaveGame();
                    FullQuestBase assigned = QuestManager.GetQuest(target);
                    if (NeedsNpcWishInteraction(assigned)) items.Add(new NpcWishShopItem(assigned));
                }
                ___currentList = items;
                __result = items;
            }
        }

        [HarmonyPatch(typeof(SimpleQuestsShopOwner), "OnPurchasedItem")]
        private static class NpcWishPurchasePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(SimpleQuestsShopOwner __instance, int itemIndex,
                List<ISimpleShopItem> ___currentList)
            {
                QueuedNpcWishes.Remove(__instance);
                if (itemIndex < 0 || itemIndex >= ___currentList.Count ||
                    !(___currentList[itemIndex] is NpcWishShopItem item)) return true;
                QueuedNpcWishes[__instance] = item.Quest;
                typeof(SimpleQuestsShopOwner).GetProperty("QueuedCustomDelivery").SetValue(__instance, null);
                typeof(SimpleQuestsShopOwner).GetProperty("QueuedQuest").SetValue(__instance, item.Quest as Quest);
                return false;
            }
        }

        private static bool IsNpcAction(FsmStateAction action, string scene, string path, string fsm, string state) =>
            action?.Fsm?.GameObject != null && action.State != null &&
            string.Equals(action.Fsm.GameObject.scene.name, scene, StringComparison.OrdinalIgnoreCase) &&
            action.Fsm.Name == fsm && action.State.Name == state &&
            Utils.GetHierarchyPath(action.Fsm.GameObject.transform) == path;

        private static bool IsHuntressAction(FsmStateAction action, string state) =>
            IsNpcAction(action, "Room_Huntress", "scene/Huntress_set/pre/Huntress", "FSM", state) ||
            IsNpcAction(action, "Room_Huntress", "scene/Huntress_set/post/Incompleted/Huntress Runt", "FSM", state);

        [HarmonyPatch(typeof(DeactivateIfPlayerdataFalse), "ForceEvaluate")]
        private static class PreserveHuntressOfferPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(DeactivateIfPlayerdataFalse __instance)
            {
                if (__instance.boolName != "HuntressRuntAppeared" ||
                    !string.Equals(__instance.gameObject.scene.name, "Room_Huntress", StringComparison.OrdinalIgnoreCase) ||
                    Utils.GetHierarchyPath(__instance.transform) != "scene/Huntress_set/post/Incompleted") return true;
                string assigned = SaveState.Instance?.progressionShuffle?.AssignedWish("Huntress Quest");
                if (assigned == null || QuestManager.GetQuest(assigned).IsCompleted) return true;
                return false;
            }
        }

        private static bool IsScientistAction(FsmStateAction action, string state) =>
            IsNpcAction(action, "Crawl_08", "Area_States/Uninfected/Scientist_set/Blue Scientist", "Behaviour", state) ||
            IsNpcAction(action, "Crawl_08", "Area_States/Infected/Infected States/Sitting/Blue Scientist Sit", "Behaviour", state);

        private static bool NeedsLaboratoryPickup()
        {
            var save = SaveState.Instance;
            if (save == null || !(QuestManager.GetQuest("Extractor Blue").IsAccepted ||
                QuestManager.GetQuest("Extractor Blue Worms").IsAccepted)) return false;
            if (save.IsLocationEnabled("Needle Phial") && save.IsLocationInSeed("Needle Phial"))
                return !save.IsLocationChecked("Needle Phial");
            var tool = ToolItemManager.GetToolByName("Extractor");
            return tool != null && !tool.IsUnlocked;
        }

        private static readonly (string Quest, string Scene, string Path, string Decision, string Prompt, string Exit)[] NpcWishOffers = {
            ("Great Gourmand", "Song_09b", "Gourmand/Great Gourmand Scene/Gourmand Servant", "Quest State?", "Offer Quest", "End Dialogue"),
            ("A Pinsmiths Tools", "Belltown_Room_Pinsmith", "Plinney Inside", "Quest State", "Offer Quest", "End Dialogue"),
            ("Brolly Get", "Bone_East_Umbrella", "_NPCs/Seamstress", "Quest State?", "Quest Prompt", "Talk End 2"),
            ("Shell Flowers", "Room_Witch", "Wood Witch", "Choice", "Flower Quest Yes No 2", "End Dialogue"),
            ("Doctor Curse Cure", "Belltown_Room_Doctor", "Doctor Fly Scene/Doctor Fly", "Quest Active?", "Take Quest?", "End Watch Target"),
        };

        [HarmonyPatch(typeof(QuestYesNo), "DoOpen")]
        private static class NpcWishPromptPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(QuestYesNo __instance)
            {
                FullQuestBase quest;
                string exit;
                if (IsCourierAction(__instance, "Begin Quest?"))
                {
                    var owner = __instance.Fsm.GameObject.GetComponent<SimpleQuestsShopOwner>();
                    if (owner == null || !QueuedNpcWishes.TryGetValue(owner, out quest)) return true;
                    QueuedNpcWishes.Remove(owner);
                    exit = "End Talk";
                }
                else if (IsNpcAction(__instance, "Room_Witch", "Wood Witch", "Dialogue", "Curse Yes No"))
                {
                    var state = SaveState.Instance?.progressionShuffle;
                    string target = state?.AssignedWish("Wood Witch Curse");
                    if (target == null) return true;
                    if (state.RecordWishOfferUnlocked("Wood Witch Curse")) GameManager.instance?.QueueSaveGame();
                    quest = QuestManager.GetQuest(target);
                    if (!NeedsNpcWishInteraction(quest) && QuestManager.GetQuest("Wood Witch Curse").IsAccepted &&
                        !QuestManager.GetQuest("Wood Witch Curse").IsCompleted) return true;
                    exit = "End Dialogue";
                }
                else if (IsHuntressAction(__instance, "Quest Prompt"))
                {
                    string target = SaveState.Instance?.progressionShuffle?.AssignedWish("Huntress Quest");
                    if (target == null) return true;
                    quest = QuestManager.GetQuest(target);
                    exit = "End";
                }
                else if (IsScientistAction(__instance, "Accept Quest?") &&
                    __instance.Quest.Value is FullQuestBase original &&
                    (original.name == "Extractor Blue" || original.name == "Extractor Blue Worms"))
                {
                    string target = SaveState.Instance?.progressionShuffle?.AssignedWish(original.name);
                    if (target == null) return true;
                    quest = QuestManager.GetQuest(target);
                    exit = "End";
                }
                else
                {
                    var offer = NpcWishOffers.FirstOrDefault(entry =>
                        IsNpcAction(__instance, entry.Scene, entry.Path, "Dialogue", entry.Prompt));
                    string target = offer.Quest == null ? null : SaveState.Instance?.progressionShuffle?.AssignedWish(offer.Quest);
                    if (target == null) return true;
                    quest = QuestManager.GetQuest(target);
                    exit = offer.Exit;
                }
                OpenNpcWish(quest, () => {
                    typeof(YesNoAction).GetField("succeeded", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(__instance, true);
                    __instance.Fsm.SetState(exit);
                    __instance.Finish();
                });
                return false;
            }
        }

        private static bool HandleSteelQuestState(QuestPlaymakerActions.QuestFsmAction action)
        {
            var save = SaveState.Instance;
            if (save?.gameMode != SteelSoulSettings.SteelSoul ||
                !(action.Quest.Value is FullQuestBase quest)) return false;
            var progress = save.progressionShuffle;
            if (quest.name == "Steel Sentinel Pt2" &&
                IsNpcAction(action, "Bone_Steel_Servant", "Steel Servant Scene", "Control", "Init") &&
                progress.TryGetBossDefeat("Boss: Summoned Saviour", out bool defeated))
            {
                if (defeated) action.Fsm.Event("COMPLETED");
                action.Finish();
                return true;
            }
            if (quest.name != "Steel Sentinel Pt2" ||
                !IsNpcAction(action, "Coral_37", "Room_States/Steel/Steel Sentinel", "Control", "State?")) return false;
            bool hasCredit = progress.TryGetBossCredit("Boss: Summoned Saviour", out bool credit);
            var firstStage = QuestManager.GetQuest("Steel Sentinel");
            if (hasCredit && credit && firstStage.IsAccepted && SteelSoulSettings.AllSitesVisited(save, PlayerData.instance) &&
                !quest.IsAccepted && !quest.IsCompleted) quest.BeginQuest(null, showPrompt: false);
            if (quest.IsAccepted && !quest.IsCompleted && (!hasCredit || credit)) return false;
            string target = progress.AssignedWish("Steel Sentinel");
            if (target != null && (PlayerData.instance.HasAnyMap || progress.IsWishOfferUnlocked("Steel Sentinel")))
            {
                if (progress.RecordWishOfferUnlocked("Steel Sentinel")) GameManager.instance?.QueueSaveGame();
                if (NeedsNpcWishInteraction(QuestManager.GetQuest(target)))
                {
                    action.Fsm.SetState("Offer");
                    action.Finish();
                    return true;
                }
            }
            if (hasCredit && !credit)
            {
                action.Finish();
                return true;
            }
            return false;
        }

        [HarmonyPatch(typeof(QuestYesNoV2), "DoOpen")]
        private static class HeraldWishPromptPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(QuestYesNoV2 __instance)
            {
                bool steel = IsNpcAction(__instance, "Coral_37", "Room_States/Steel/Steel Sentinel", "Control", "Take Quest?");
                if (!steel && !IsNpcAction(__instance, "Aqueduct_05", "Mr_Mush_Tablet_St/Mr Mushroom Tablet", "Inspection", "Begin Quest?"))
                    return true;
                string target = SaveState.Instance?.progressionShuffle?.AssignedWish(steel ? "Steel Sentinel" : "Mr Mushroom");
                if (target == null) return true;
                OpenNpcWish(QuestManager.GetQuest(target), () => {
                    typeof(YesNoAction).GetField("succeeded", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(__instance, true);
                    __instance.Fsm.SetState(steel ? "End Dlg" : "Dialogue End No");
                    __instance.Finish();
                });
                return false;
            }
        }

        [HarmonyPatch(typeof(QuestPlaymakerActions.QuestFsmAction), nameof(QuestPlaymakerActions.QuestFsmAction.OnEnter))]
        private static class NpcWishOfferPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(QuestPlaymakerActions.QuestFsmAction __instance)
            {
                if (!(__instance is QuestPlaymakerActions.CheckQuestState) && !(__instance is QuestPlaymakerActions.CheckQuestStateV2)) return true;
                if (HandleSteelQuestState(__instance)) return false;
                var progressState = SaveState.Instance?.progressionShuffle;
                if (IsHuntressAction(__instance, "Quest Status?") &&
                    __instance.Quest.Value is FullQuestBase huntress &&
                    ProgressionShuffleState.WishIdentity(huntress.name) == "Huntress Quest")
                {
                    string assigned = progressState?.AssignedWish("Huntress Quest");
                    if (assigned == null) return true;
                    if (progressState.RecordWishOfferUnlocked("Huntress Quest")) GameManager.instance?.QueueSaveGame();
                    if (NeedsNpcWishInteraction(QuestManager.GetQuest(assigned)))
                        __instance.Fsm.SetState("Quest Prompt");
                    else
                    {
                        if (huntress.name == "Huntress Quest Runt" && !huntress.IsAccepted &&
                            QuestManager.GetQuest("Huntress Quest").IsAccepted &&
                            !QuestManager.GetQuest("Huntress Quest").IsCompleted) huntress.BeginQuest(null, showPrompt: false);
                        if (huntress.IsAccepted) return true;
                        __instance.Fsm.SetState("End");
                    }
                    __instance.Finish();
                    return false;
                }
                if (IsScientistAction(__instance, "Choice") &&
                    new[] { "Extractor Blue", "Extractor Blue Worms" }.Any(source =>
                        progressState?.AssignedWish(source) is string replacement &&
                        !QuestManager.GetQuest(replacement).IsCompleted))
                {
                    __instance.Finish();
                    return false;
                }
                if (IsScientistAction(__instance, "Quest State?") &&
                    __instance.Quest.Value is FullQuestBase original &&
                    (original.name == "Extractor Blue" || original.name == "Extractor Blue Worms"))
                {
                    string replacement = progressState?.AssignedWish(original.name);
                    if (replacement == null) return true;
                    if (NeedsLaboratoryPickup())
                    {
                        __instance.Fsm.SetState("Give Extractor");
                        __instance.Finish();
                        return false;
                    }
                    bool available = progressState.IsWishOfferUnlocked(original.name) ||
                        original.name == "Extractor Blue" ||
                        (PlayerData.instance.blackThreadWorld && QuestManager.GetQuest("Extractor Blue").IsCompleted);
                    if (available && progressState.RecordWishOfferUnlocked(original.name))
                        GameManager.instance?.QueueSaveGame();
                    if (available && NeedsNpcWishInteraction(QuestManager.GetQuest(replacement)))
                        __instance.Fsm.SetState("Accept Quest?");
                    else if (original.IsAccepted) return true;
                    else __instance.Fsm.SetState("Pass");
                    __instance.Finish();
                    return false;
                }
                if (IsNpcAction(__instance, "Room_Witch", "Wood Witch", "Dialogue", "Init"))
                {
                    var progress = SaveState.Instance?.progressionShuffle;
                    if (new[] { "Shell Flowers", "Wood Witch Curse" }.Any(source =>
                        progress?.AssignedWish(source) is string target &&
                        !QuestManager.GetQuest(target).IsCompleted &&
                        (!QuestManager.GetQuest(target).IsAccepted || !ProgressionShuffleState.HasNativeWishFinish(target))))
                    {
                        __instance.Finish();
                        return false;
                    }
                }
                if (IsNpcAction(__instance, "Room_Witch", "Wood Witch", "Dialogue", "Choice") &&
                    progressState?.IsWishOfferUnlocked("Wood Witch Curse") == true &&
                    progressState.AssignedWish("Wood Witch Curse") is string curseTarget &&
                    NeedsNpcWishInteraction(QuestManager.GetQuest(curseTarget)))
                {
                    __instance.Fsm.SetState("Curse Yes No");
                    __instance.Finish();
                    return false;
                }
                if (IsNpcAction(__instance, "Aqueduct_05", "Mr_Mush_Tablet_St/Mr Mushroom Tablet", "Inspection", "Idle"))
                {
                    var progress = SaveState.Instance?.progressionShuffle;
                    string assigned = progress?.AssignedWish("Mr Mushroom");
                    if (assigned == null) return true;
                    if (progress.RecordWishOfferUnlocked("Mr Mushroom")) GameManager.instance?.QueueSaveGame();
                    if (QuestManager.GetQuest(assigned).IsCompleted) __instance.Fsm.Event("CANCEL");
                    __instance.Finish();
                    return false;
                }
                var offer = NpcWishOffers.FirstOrDefault(entry =>
                    IsNpcAction(__instance, entry.Scene, entry.Path, "Dialogue", entry.Decision));
                var state = SaveState.Instance?.progressionShuffle;
                string target = offer.Quest == null ? null : state?.AssignedWish(offer.Quest);
                if (target == null) return true;
                if (state.RecordWishOfferUnlocked(offer.Quest)) GameManager.instance?.QueueSaveGame();
                FullQuestBase quest = QuestManager.GetQuest(target);
                if (NeedsNpcWishInteraction(quest))
                {
                    __instance.Fsm.SetState(offer.Prompt);
                    __instance.Finish();
                    return false;
                }
                FullQuestBase native = QuestManager.GetQuest(offer.Quest);
                if (native != null && native.IsAccepted) return true;
                __instance.Fsm.SetState(offer.Exit);
                __instance.Finish();
                return false;
            }
        }

        internal static bool TryOpenPendingDoctorWish(PlayerDataVariableTest action)
        {
            if (!(IsNpcAction(action, "Belltown_Room_Doctor", "Doctor Fly Scene/Doctor Fly", "Dialogue", "Cursed? 2") ||
                  IsNpcAction(action, "Belltown_Room_Doctor", "Doctor Fly Scene/Doctor Fly", "Dialogue", "Cursed? 3"))) return false;
            var state = SaveState.Instance?.progressionShuffle;
            string target = state?.AssignedWish("Doctor Curse Cure");
            if (target == null || !state.IsWishOfferUnlocked("Doctor Curse Cure") ||
                !NeedsNpcWishInteraction(QuestManager.GetQuest(target))) return false;
            action.Fsm.SetState("Take Quest?");
            action.Finish();
            return true;
        }

        [HarmonyPatch(typeof(FullQuestBase), nameof(FullQuestBase.GetIsReadyToTurnIn))]
        private static class NativeWishFinishPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(FullQuestBase __instance, bool atQuestBoard, ref bool __result)
            {
                if (!atQuestBoard || !ProgressionShuffleState.HasNativeWishFinish(__instance.name) ||
                    SaveState.Instance?.progressionShuffle?.OfferForWish(ProgressionShuffleState.WishIdentity(__instance.name)) == null)
                    return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(QuestBoardInteractable), "ProcessQueuedCompletions")]
        private static class BoardTurnInPatch
        {
            [HarmonyPrefix]
            private static void Prefix() { boardTurnInDepth++; }

            [HarmonyFinalizer]
            private static Exception Finalizer(Exception __exception)
            {
                boardTurnInDepth--;
                return __exception;
            }
        }

        [HarmonyPatch(typeof(QuestBoardInteractable), "get_Quests")]
        private static class BoardOffersPatch
        {
            [HarmonyPostfix]
            private static void Postfix(ref IReadOnlyCollection<QuestGroupBase> __result)
            {
                var state = SaveState.Instance?.progressionShuffle;
                if (state == null || !state.HasWishAssignments) return;
                __result = __result.Select(group => {
                    string target = state.AssignedWish(group.name);
                    if (target == null) return group;
                    return QuestManager.GetQuest(target) ?? throw new InvalidOperationException(
                        "The assigned wish could not be loaded: " + target);
                }).ToArray();
            }
        }

        [HarmonyPatch(typeof(QuestBoardInteractable), "GetDisplayedQuests")]
        private static class BoardPreviewPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(QuestBoardInteractable __instance, FullQuestBase ___donateQuest,
                ref IReadOnlyCollection<QuestGroupBase> __result)
            {
                var state = SaveState.Instance?.progressionShuffle;
                if (state == null || !state.HasWishAssignments) return true;
                var donation = ___donateQuest;
                __result = __instance.Quests.SelectMany(group => group.GetQuests())
                    .Where(quest => quest != donation && !quest.IsAccepted && quest.IsAvailable &&
                        (!(quest is IQuestWithCompletion complete) || !complete.IsCompleted))
                    .Take(6).Cast<QuestGroupBase>().ToArray();
                return false;
            }
        }

        [HarmonyPatch(typeof(FullQuestBase), nameof(FullQuestBase.GetDescription))]
        private static class WishTurnInDescriptionPatch
        {
            [HarmonyPostfix]
            private static void Postfix(FullQuestBase __instance, BasicQuestBase.ReadSource readSource,
                TeamCherry.Localization.LocalisedString ___wallDescription, ref string __result)
            {
                var state = SaveState.Instance?.progressionShuffle;
                string offer = state?.OfferForWish(__instance.name);
                if (offer == null) return;
                if (readSource == BasicQuestBase.ReadSource.QuestBoard &&
                    (___wallDescription.IsEmpty || string.IsNullOrWhiteSpace(__result)))
                {
                    __result = __instance.GetDescription(BasicQuestBase.ReadSource.Inventory);
                    return;
                }
                if (offer == __instance.name || __instance.IsCompleted) return;
                string board = state.WishTurnInBoard(__instance.name);
                if (board != null) __result += "\n\nTurn in at the " + board + " wish board.";
                else if (state.WishTurnInNpc(__instance.name) is string npc)
                    __result += "\n\nTurn in to " + npc + ".";
            }
        }

        [HarmonyPatch(typeof(FullQuestBase), "get_IsAvailable")]
        private static class OfferAvailabilityPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(FullQuestBase __instance, ref bool __result)
            {
                var state = SaveState.Instance?.progressionShuffle;
                if (nativeOfferDepth != 0 || state == null || !state.HasWishAssignments) return true;
                string source = state.OfferForWish(__instance.name);
                if (source == null) return true;
                __result = state.IsWishOfferUnlocked(source) ||
                    (!ProgressionShuffleState.IsNpcWish(source) && ReadNativeOffer(source));
                if (__result && state.RecordWishOfferUnlocked(source))
                    GameManager.instance?.QueueSaveGame();
                return false;
            }
        }

        [HarmonyPatch(typeof(FullQuestBase), nameof(FullQuestBase.BeginQuest))]
        private static class CourierParcelPatch
        {
            [HarmonyPrefix]
            private static void Prefix(FullQuestBase __instance, out bool __state) => __state = __instance.IsAccepted;

            [HarmonyPostfix]
            private static void Postfix(FullQuestBase __instance, bool __state)
            {
                if (__state || !__instance.IsAccepted || __instance.IsCompleted ||
                    !ProgressionShuffleState.IsNpcWish(__instance.name) || __instance.name == "Great Gourmand" ||
                    SaveState.Instance?.progressionShuffle?.OfferForWish(__instance.name) == null) return;
                foreach (var target in __instance.Targets)
                    if (target.Counter is DeliveryQuestItem parcel) parcel.Get(target.Count, false);
            }
        }

        [HarmonyPatch(typeof(FullQuestBase), nameof(FullQuestBase.BeginQuest))]
        private static class AcceptedPatch
        {
            [HarmonyPrefix]
            private static void Prefix() => CaptureAvailableOffers();

            [HarmonyPostfix]
            private static void Postfix(FullQuestBase __instance)
            {
                if (__instance.IsAccepted && SaveState.Instance?.progressionShuffle?.RecordWishAccepted(
                    ProgressionShuffleState.WishIdentity(__instance.name)) == true)
                    GameManager.instance?.QueueSaveGame();
            }
        }

        [HarmonyPatch(typeof(FullQuestBase), nameof(FullQuestBase.TryEndQuest))]
        private static class CompletedPatch
        {
            [HarmonyPrefix]
            private static void Prefix() => CaptureAvailableOffers();

            [HarmonyPostfix]
            private static void Postfix(FullQuestBase __instance, bool __result, bool forceEnd)
            {
                var state = SaveState.Instance?.progressionShuffle;
                if (state == null || !__result || __instance.name == "Steel Sentinel" || ProgressionShuffleState.IsWishNotice(__instance.name) || (forceEnd && boardTurnInDepth == 0 &&
                    !ProgressionShuffleState.HasNativeWishFinish(__instance.name)) ||
                    (!__instance.IsAccepted && __instance.name != "Song Knight") || !__instance.IsCompleted) return;
                state.RecordWishAccepted(ProgressionShuffleState.WishIdentity(__instance.name));
                state.RecordWishCompleted(ProgressionShuffleState.WishIdentity(__instance.name));
            }
        }
    }
}
