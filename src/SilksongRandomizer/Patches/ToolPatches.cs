using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using GlobalEnums;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    public class ToolPatches
    {
        public static bool canUnlockedCrestBeEquipped = false;
        public static bool canCrestBeUnlockedByRandomizer = false;
        [ThreadStatic]
        private static int nativeRiteCursedCrestEquipDepth;
        [ThreadStatic]
        private static int nativeCureOwnedCrestEquipDepth;
        private static bool pendingHeroInputReset;
        private static int pendingHeroInputResetRequestFrame = -1;
        private static readonly MethodInfo CancelDashMethod =
            AccessTools.Method(
                typeof(HeroController),
                "CancelDash",
                new[] { typeof(bool) }
            );

        private static bool IsAutomaticCompassTool(
            SaveState state,
            ToolItem tool)
        {
            return state != null &&
                   state.automaticCompass &&
                   tool != null &&
                   ReferenceEquals(
                       tool,
                       GlobalSettings.Gameplay.CompassTool
                   );
        }

        private static bool IsPoolShellSatchel(
            SaveState state,
            ToolItem tool)
        {
            return state != null &&
                   state.IsRoomBound &&
                   tool != null &&
                   string.Equals(
                       tool.name,
                       "Shell Satchel",
                       StringComparison.OrdinalIgnoreCase
                   );
        }

        private static bool HasReceivedShellSatchel(SaveState state)
        {
            return state != null &&
                   state.receivedItems != null &&
                   state.receivedItems.Contains("Shell Satchel");
        }

        internal static void EnsureReceivedBaseCrestsUnlocked()
        {
            SaveState state = SaveState.Instance;
            if (state == null ||
                !state.IsRandomized(ItemType.Crest) ||
                PlayerData.instance == null)
            {
                return;
            }

            bool previousBypass = canCrestBeUnlockedByRandomizer;
            canCrestBeUnlockedByRandomizer = true;
            try
            {
                foreach (ToolCrest crest in ToolItemManager.GetAllCrests())
                {
                    if (crest == null ||
                        !crest.IsBaseVersion ||
                        crest.IsUnlocked ||
                        !state.receivedItems.Contains(
                            CrestNames.GetItemNameFromInternal(crest.name)
                        ))
                    {
                        continue;
                    }

                    // Older randomizer builds tracked crest ownership only in
                    // SaveState. Initialize the native base crest once so its
                    // slots and upgrade-version visibility work normally.
                    crest.Unlock();
                }
            }
            finally
            {
                canCrestBeUnlockedByRandomizer = previousBypass;
            }
        }

        internal static void RemoveAutomaticCompassFromCrests()
        {
            SaveState state = SaveState.Instance;
            if (state == null ||
                !state.automaticCompass ||
                PlayerData.instance == null)
            {
                return;
            }

            ToolItem compass = GlobalSettings.Gameplay.CompassTool;
            if (compass != null)
            {
                // Any stale equipped copy is cleared. Automatic Compass is a
                // virtual option and consumes no Crest slot.
                ToolItemManager.RemoveToolFromAllCrests(compass);
            }
        }

        internal static void RemoveUnreceivedSilkspearFromCrests()
        {
            SaveState state = SaveState.Instance;
            if (state == null ||
                !state.IsRandomized(ItemType.Spell) ||
                PlayerData.instance == null)
            {
                return;
            }

            string itemName = ItemSet.GetCanonicalItemName(
                "Spell: Silk Spear"
            );
            if (state.receivedItems.Contains(itemName))
            {
                return;
            }

            ToolItem silkspear = ToolItemManager.GetToolByName("Silk Spear");
            if (silkspear != null)
            {
                // Unlocking a crest initializes its vanilla neutral skill slot
                // with Silkspear. That default loadout reference remains absent
                // until Archipelago grants the skill.
                ToolItemManager.RemoveToolFromAllCrests(silkspear);
            }
        }

        internal static bool SetRandomizerCrest(
            ToolCrest crest,
            bool markTemporary,
            bool deferRuntimeRefresh = false
        )
        {
            PlayerData playerData = PlayerData.instance;
            if (crest == null || playerData == null)
            {
                return false;
            }

            string crestName = crest.name;
            if (string.IsNullOrEmpty(crestName))
            {
                return false;
            }

            if (NakedTrapManager.TryDeferRandomizerCrestChange(
                    crest,
                    markTemporary))
            {
                // The crest is already unlocked by the caller. Naked Trap
                // keeps the live Cloakless crest and records this as the
                // crest to equip when its timer expires.
                RemoveUnreceivedSilkspearFromCrests();
                ResetHeroInputAfterCrestChange();
                return true;
            }

            bool changesCrest = !string.Equals(
                playerData.CurrentCrestID,
                crestName,
                StringComparison.Ordinal
            );
            if (changesCrest)
            {
                PrepareHeroForCrestChange();
                playerData.PreviousCrestID = playerData.CurrentCrestID;
            }

            // AutoEquip is intended for a player-initiated vanilla reward. It
            // also schedules the Tools pane and runs its transfer animation.
            // Doing that from the AP receive queue can leave the hero's move
            // input latched in one direction. Set the already-initialized
            // native crest data directly, then send the normal refresh event.
            ToolItemManager.SetEquippedCrest(crestName);
            playerData.IsCurrentCrestTemp = markTemporary;
            if (!deferRuntimeRefresh)
            {
                ToolItemManager.RefreshEquippedState();
                ToolItemManager.SendEquippedChangedEvent(true);
            }
            RemoveUnreceivedSilkspearFromCrests();
            ResetHeroInputAfterCrestChange();
            return string.Equals(
                playerData.CurrentCrestID,
                crestName,
                StringComparison.Ordinal);
        }

        /// <summary>
        /// Runs the permanent Cursed Crest equip used by Greyroot's Rite of
        /// Rebirth cutscene. The scoped token exists only
        /// while ToolItemManager.AutoEquip is on this call stack, so ordinary
        /// crest ownership filtering remains active everywhere else.
        /// </summary>
        internal static bool AutoEquipNativeRiteCursedCrest(ToolCrest crest)
        {
            ToolCrest nativeCursed = GlobalSettings.Gameplay.CursedCrest;
            if (crest == null || nativeCursed == null ||
                !string.Equals(
                    crest.name,
                    nativeCursed.name,
                    StringComparison.Ordinal))
            {
                return false;
            }

            nativeRiteCursedCrestEquipDepth++;
            try
            {
                // Uses the AutoEquipCrest arguments from the cutscene:
                // markTemp=false, removeTools=true.
                ToolItemManager.AutoEquip(crest, false, true);
            }
            finally
            {
                nativeRiteCursedCrestEquipDepth--;
            }

            PlayerData playerData = PlayerData.instance;
            return playerData != null &&
                   string.Equals(
                       playerData.CurrentCrestID,
                       crest.name,
                       StringComparison.Ordinal) &&
                   !playerData.IsCurrentCrestTemp;
        }

        internal static bool AutoEquipOwnedCrestAfterNativeCure(
            ToolCrest crest)
        {
            SaveState state = SaveState.Instance;
            ToolCrest nativeCursed = GlobalSettings.Gameplay.CursedCrest;
            if (state == null ||
                !state.IsRandomized(ItemType.Crest) ||
                crest == null ||
                nativeCursed == null ||
                string.Equals(
                    crest.name,
                    nativeCursed.name,
                    StringComparison.Ordinal) ||
                !state.receivedItems.Contains(
                    CrestNames.GetItemNameFromInternal(crest.name)
                ))
            {
                return false;
            }

            nativeCureOwnedCrestEquipDepth++;
            try
            {
                ToolItemManager.AutoEquip(crest, false, true);
            }
            finally
            {
                nativeCureOwnedCrestEquipDepth--;
            }

            PlayerData playerData = PlayerData.instance;
            return playerData != null &&
                   string.Equals(
                       playerData.CurrentCrestID,
                       crest.name,
                       StringComparison.Ordinal) &&
                   !playerData.IsAnyCursed &&
                   !playerData.IsCurrentCrestTemp;
        }

        internal static void PrepareHeroForCrestChange()
        {
            HeroController hero = HeroController.instance;
            if (hero == null || hero.cState == null)
            {
                return;
            }

            bool sprintOwnsControl = DoesSprintOwnHeroControl(hero);
            if (!CanSafelyReleaseCrestMotion(hero, sprintOwnsControl))
            {
                return;
            }

            // A live config refresh sends HC CONFIG UPDATED to the native
            // Sprint FSM. Its Cancel All route clears sprint flags, but skips
            // the normal Regain Control Shared state. End a dash/sprint while
            // the old config still owns it so that refresh cannot strand the
            // hero in no_input with the old horizontal velocity.
            if (hero.cState.dashing)
            {
                try
                {
                    if (CancelDashMethod == null)
                    {
                        throw new MissingMethodException(
                            typeof(HeroController).FullName,
                            "CancelDash(bool)"
                        );
                    }

                    CancelDashMethod.Invoke(hero, new object[] { true });
                }
                catch (Exception ex)
                {
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Active dash could not be released " +
                        "before a crest change: " + ex.Message
                    );
                }
            }

            if (!sprintOwnsControl)
            {
                return;
            }

            try
            {
                hero.sprintFSM.SendEvent("SPRINT CANCEL");
                hero.RegainControl();
                hero.StartAnimationControlToIdle();
            }
            catch (Exception ex)
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Active sprint could not be released " +
                    "before a crest change: " + ex.Message
                );
            }
        }

        private static bool DoesSprintOwnHeroControl(HeroController hero)
        {
            if (hero == null || hero.cState == null ||
                hero.sprintFSM == null || !hero.controlReqlinquished)
            {
                return false;
            }

            var nativeSprintFlag = hero.sprintFSM.FsmVariables?.FindFsmBool(
                "Is Sprinting"
            );
            return hero.cState.isSprinting ||
                   (nativeSprintFlag != null && nativeSprintFlag.Value);
        }

        private static bool CanSafelyReleaseCrestMotion(
            HeroController hero,
            bool sprintOwnsControl
        )
        {
            GameManager gameManager = GameManager.SilentInstance;
            PlayerData playerData = PlayerData.instance;
            bool hasOrdinaryControl =
                !hero.controlReqlinquished &&
                hero.hero_state != ActorStates.no_input &&
                hero.CanInput();
            return gameManager != null &&
                   playerData != null &&
                   gameManager.GameState == GameState.PLAYING &&
                   gameManager.IsGameplayScene() &&
                   !gameManager.isPaused &&
                   !gameManager.IsLoadingSceneTransition &&
                   !gameManager.IsInSceneTransition &&
                   !TransitionPoint.IsTransitionBlocked &&
                   !BossSceneController.IsTransitioning &&
                   !playerData.HasStoredMemoryState &&
                   !hero.cState.transitioning &&
                   !hero.cState.dead &&
                   !hero.cState.hazardDeath &&
                   !hero.cState.hazardRespawning &&
                   (hasOrdinaryControl || sprintOwnsControl);
        }

        internal static void ResetHeroInputAfterCrestChange()
        {
            pendingHeroInputReset = true;
            pendingHeroInputResetRequestFrame = Time.frameCount;
        }

        private static void TryApplyPendingHeroInputReset(
            HeroController hero)
        {
            if (!pendingHeroInputReset ||
                Time.frameCount <= pendingHeroInputResetRequestFrame ||
                !CanSafelyNeutralizeHeroInput(hero))
            {
                return;
            }

            // Neutralize the sampled axes and InControl action state without
            // changing HeroController's ownership of control. This runs from
            // HeroController.Update on a later frame, after the crest refresh
            // and Sprint FSM have both had time to settle. Applying it in the
            // same frame lets those systems sample and relatch the old dash
            // direction after the reset.
            hero.move_input = 0f;
            hero.vertical_input = 0f;
            hero.ClearActionsInputState();
            hero.ClearJumpInputState();
            pendingHeroInputReset = false;
            pendingHeroInputResetRequestFrame = -1;
        }

        private static bool CanSafelyNeutralizeHeroInput(
            HeroController hero)
        {
            GameManager gameManager = GameManager.SilentInstance;
            return hero != null &&
                   gameManager != null &&
                   gameManager.GameState == GameState.PLAYING &&
                   gameManager.IsGameplayScene() &&
                   !hero.controlReqlinquished &&
                   hero.hero_state != ActorStates.no_input &&
                   hero.CanInput();
        }

        [HarmonyPatch(typeof(HeroController), "Update")]
        internal static class HeroController_Update_CrestInputReset_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(HeroController __instance)
            {
                // AP items and timed traps can change a crest during a death,
                // transition, menu or scripted sequence. Wait until the
                // native controller reports ordinary gameplay input again.
                // RegainControl is not forced over the sequence that owns it.
                TryApplyPendingHeroInputReset(__instance);
            }
        }

        [HarmonyPatch(typeof(ToolItemManager), "AutoEquip", new[] { typeof(ToolCrest), typeof(bool), typeof(bool) })]
        internal static class ToolItemManager_AutoEquipCrest_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(ToolCrest crest)
            {
                if (crest == null)
                {
                    return false;
                }

                SaveState state = SaveState.Instance;
                if (state == null || !state.IsRandomized(ItemType.Crest))
                {
                    return true;
                }

                // Only Greyroot's Set Cursed wrapper holds this token and only
                // for the native Cursed Crest. Ordinary shrines, Eva, inventory
                // and arbitrary AutoEquip calls remain subject to AP ownership.
                ToolCrest nativeCursed = GlobalSettings.Gameplay.CursedCrest;
                if (nativeRiteCursedCrestEquipDepth > 0 &&
                    nativeCursed != null &&
                    string.Equals(
                        crest.name,
                        nativeCursed.name,
                        StringComparison.Ordinal))
                {
                    return true;
                }

                if (nativeCureOwnedCrestEquipDepth > 0 &&
                    !string.Equals(
                        crest.name,
                        nativeCursed?.name,
                        StringComparison.Ordinal) &&
                    state.receivedItems.Contains(
                        CrestNames.GetItemNameFromInternal(crest.name)
                    ))
                {
                    return true;
                }

                // Cloakless remains available to the game's opening/story
                // events. Hunter variants must follow AP ownership like every
                // other crest. Eva otherwise equips Hunter_v2 over a different
                // randomized starting crest.
                if (crest.name == "Cloakless")
                {
                    return true;
                }

                // AP grants use the temporary bypass while applying the item.
                // Afterwards, normal inventory switching is allowed only for
                // crests actually received by this randomizer save.
                if (canUnlockedCrestBeEquipped)
                {
                    return true;
                }

                if (!state.receivedItems.Contains(
                    CrestNames.GetItemNameFromInternal(crest.name)
                ))
                {
                    return false;
                }

                if (CrestNames.IsHunterInternalName(crest.name) &&
                    !crest.IsBaseVersion)
                {
                    // Eva's FSM separately unlocks an upgraded Hunter and then
                    // tries to force-equip it. The upgrade remains but that
                    // automatic switch applies only when Hunter was already
                    // equipped. Manual inventory switching remains available.
                    return PlayerData.instance != null &&
                           CrestNames.IsHunterInternalName(
                               PlayerData.instance.CurrentCrestID
                           );
                }

                return true;
            }

            [HarmonyPostfix]
            private static void Postfix()
            {
                RemoveUnreceivedSilkspearFromCrests();
            }
        }

        [HarmonyPatch(typeof(ToolItemManager), "AutoEquip", new[] { typeof(ToolItem) })]
        internal static class ToolItemManager_AutoEquipTool_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(ToolItem tool)
            {
                if (tool == null)
                {
                    return true;
                }

                SaveState state = SaveState.Instance;
                if (IsPoolShellSatchel(state, tool) &&
                    !HasReceivedShellSatchel(state))
                {
                    return false;
                }
                if (IsAutomaticCompassTool(state, tool))
                {
                    // The option supplies the marker effect directly, so its
                    // vanilla source does not occupy a tool slot later.
                    return false;
                }

                ItemType itemType = tool.Type == ToolItemType.Skill
                    ? ItemType.Spell
                    : ItemType.Tool;
                if (state == null || !state.IsRandomized(itemType))
                {
                    return true;
                }

                Debug.Log("TRIED TO EQUIP " + tool.name);
                Debug.Log("WAS ALREADY UNLOCKED: " + tool.IsUnlocked);
                if (tool.Type == ToolItemType.Skill)
                {
                    SaveState.Instance.CheckLocation("Spell Unlock: " + tool.name);
                    if (!tool.IsUnlocked && !HasReceivedSilkSkill(tool))
                    {
                        RandomizerPlugin.Instance.StartCoroutine(DelayRelock(tool));
                    }
                    return false;
                }
                return true;
            }
        }

        static IEnumerator DelayRelock(ToolItem tool)
        {
            while (!tool.IsUnlocked && !HasReceivedSilkSkill(tool))
            {
                yield return null;
            }

            if (HasReceivedSilkSkill(tool))
            {
                yield break;
            }

            yield return new WaitForSeconds(1.0f);

            // A received Silk Skill may be
            // equipped even while its native ToolItem still reports locked.
            // The vanilla shrine unlocks that native item before this delay
            // expires, so relocking it here would call ToolItem.Lock(), which
            // removes the skill from every crest. An owned skill and the
            // player's existing loadout remain untouched.
            if (HasReceivedSilkSkill(tool))
            {
                yield break;
            }

            Debug.Log("Relocked " + tool.name);
            tool.Lock();
            yield return null;
            while (!HasReceivedSilkSkill(tool))
            {
                yield return null;
            }
            Debug.Log("Reunlocked " + tool.name);
            var data = tool.SavedData;
            data.IsHidden = false;
            tool.SavedData = data;
            tool.Unlock();
        }

        private static bool HasReceivedSilkSkill(ToolItem tool)
        {
            SaveState state = SaveState.Instance;
            return state != null &&
                   state.receivedItems != null &&
                   tool != null &&
                   state.receivedItems.Contains(
                       ItemSet.GetCanonicalItemName(
                           "Spell: " + tool.name
                       )
                   );
        }

        [HarmonyPatch(typeof(ToolItem), "Unlock", new[] { typeof(Action), typeof(ToolItem.PopupFlags) })]
        internal static class ToolItem_Unlock_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(
                ToolItem __instance,
                Action afterTutorialMsg,
                ref ToolItem.PopupFlags popupFlags)
            {
                string toolName = "<null>";

                if (__instance != null && !string.IsNullOrEmpty(__instance.name))
                {
                    toolName = __instance.name;
                }

                Debug.Log("[RANDOMIZER] Tried to Unlock: " + toolName);

                SaveState state = SaveState.Instance;
                if (state != null && state.IsRoomBound)
                {
                    popupFlags &= ~ToolItem.PopupFlags.Tutorial;
                }

                if (IsPoolShellSatchel(state, __instance))
                {
                    popupFlags &= ~(
                        ToolItem.PopupFlags.ItemGet |
                        ToolItem.PopupFlags.Tutorial
                    );
                    return true;
                }
                ItemType itemType =
                    __instance != null &&
                    __instance.Type == ToolItemType.Skill
                        ? ItemType.Spell
                        : ItemType.Tool;
                bool isAutomaticCompass =
                    IsAutomaticCompassTool(state, __instance);
                if (state == null ||
                    (
                        !state.IsRandomized(itemType) &&
                        !isAutomaticCompass
                    ))
                {
                    return true;
                }
                if (RuinedToolPatches.TryHandleWebShotRepair(
                        state,
                        toolName))
                {
                }
                else if (
                    toolName == "Silk Spear" ||
                    toolName == "Parry" ||
                    toolName == "Silk Boss Needle" ||
                    toolName == "Silk Charge" ||
                    toolName == "Silk Bomb" ||
                    toolName == "Thread Sphere"
                    )
                {
                    SaveState.Instance.CheckLocation("Spell Unlock: " + toolName);
                }
                else
                {
                    SaveState.Instance.CheckLocation("Tool Unlock: " + toolName);
                }

                // The location contains an Archipelago item rather than this
                // native ToolItem. The game's unlock bookkeeping and callbacks
                // remain while the misleading item-name popup and the
                // first-tool tutorial. The AP receipt notification is
                // queued separately by RandomizerPlugin.
                popupFlags &= ~(
                    ToolItem.PopupFlags.ItemGet |
                    ToolItem.PopupFlags.Tutorial
                );

                return true;
            }

            [HarmonyPostfix]
            private static void Postfix(ToolItem __instance)
            {
                SaveState state = SaveState.Instance;
                if (state == null ||
                    !state.IsRandomized(ItemType.Tool) ||
                    !ItemGrants.TryGetProgressiveToolState(
                        __instance,
                        out int _level,
                        out int _requiredLevel,
                        out bool _isBaseTier))
                {
                    return;
                }

                // Native physical sources still perform their bookkeeping.
                // Immediately restore the AP-owned tier so a source
                // containing another item cannot leak, consume or equip its
                // vanilla Claw Mirror / Curveclaw reward.
                if (!ItemGrants.TrySynchronizeProgressiveToolEquips())
                {
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Progressive tool assets were not " +
                        "ready after native ToolItem.Unlock."
                    );
                }
            }
        }

        [HarmonyPatch(typeof(ToolItem), "Lock", new Type[0])]
        internal static class ToolItem_Lock_ProgressiveTool_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(ToolItem __instance)
            {
                SaveState state = SaveState.Instance;
                if (state == null ||
                    !state.IsRandomized(ItemType.Tool) ||
                    !ItemGrants.TryGetProgressiveToolState(
                        __instance,
                        out int level,
                        out int requiredLevel,
                        out bool isBaseTier))
                {
                    return true;
                }

                bool isCurrentReceivedTier = isBaseTier
                    ? level == 1
                    : level >= requiredLevel;
                // Mottled Child consumes Curve Claws through ToolItem.Lock
                // and native upgrade Unlock also locks its replacement. AP
                // ownership must retain the currently active received tier.
                return !isCurrentReceivedTier;
            }
        }

        [HarmonyPatch(typeof(ToolItem), "GetSavedAmount", new Type[0])]
        internal static class ToolItem_GetSavedAmount_ProgressiveTool_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(
                ToolItem __instance,
                ref int __result)
            {
                SaveState state = SaveState.Instance;
                if (state == null ||
                    !state.IsRandomized(ItemType.Tool) ||
                    !ItemGrants.TryGetProgressiveToolState(
                        __instance,
                        out int level,
                        out int requiredLevel,
                        out bool _isBaseTier))
                {
                    return true;
                }

                // Mottled Child's dialogue queries GetSavedAmount rather than
                // inventory visibility. The base prerequisite remains true after
                // its second tier is received while the upgrade requires two
                // progressive copies.
                __result = level >= requiredLevel ? 1 : 0;
                return false;
            }
        }

        [HarmonyPatch(typeof(ToolItem), "get_IsUnlocked", new Type[0])]
        internal static class ToolItem_IsUnlocked_ShellSatchel_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(
                ToolItem __instance,
                ref bool __result)
            {
                SaveState state = SaveState.Instance;
                if (IsPoolShellSatchel(state, __instance))
                {
                    __result = HasReceivedShellSatchel(state);
                    return false;
                }

                if (state != null &&
                    state.IsRandomized(ItemType.Tool) &&
                    ItemGrants.TryGetProgressiveToolState(
                        __instance,
                        out int level,
                        out int requiredLevel,
                        out bool _isBaseTier))
                {
                    __result = level >= requiredLevel;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(ToolItem), "get_IsUnlockedNotHidden", new Type[0])]
        internal static class ToolItem_IsUnlockedNotHidden_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(ToolItem __instance, ref bool __result)
            {
                SaveState state = SaveState.Instance;
                if (state == null)
                {
                    return true;
                }

                if (IsAutomaticCompassTool(state, __instance))
                {
                    // Older APWorlds precollected Tool: Compass for this
                    // option. Hide that protocol item as well as the native
                    // source. Only the permanent map-marker effect remains.
                    __result = false;
                    return false;
                }

                ItemType itemType = __instance.Type == ToolItemType.Skill
                    ? ItemType.Spell
                    : ItemType.Tool;
                if (!state.IsRandomized(itemType))
                {
                    return true;
                }

                if (itemType == ItemType.Tool &&
                    ItemGrants.TryGetProgressiveToolState(
                        __instance,
                        out int progressiveLevel,
                        out int requiredLevel,
                        out bool isBaseTier))
                {
                    __result = isBaseTier
                        ? progressiveLevel == 1
                        : progressiveLevel >= requiredLevel;
                    return false;
                }

                if (itemType == ItemType.Tool && state.druidsEyeLevel > 0)
                {
                    if (__instance.name == "Mosscreep Tool 1")
                    {
                        __result = state.druidsEyeLevel == 1;
                        return false;
                    }

                    if (__instance.name == "Mosscreep Tool 2")
                    {
                        __result = state.druidsEyeLevel >= 2;
                        return false;
                    }
                }

                if (__instance.Type == ToolItemType.Skill)
                {
                    __result = state.receivedItems.Contains(
                        ItemSet.GetCanonicalItemName(
                            "Spell: " + __instance.name
                        )
                    );
                    return false;
                }
                else
                {
                    __result = state.receivedItems.Contains(
                        ItemSet.GetCanonicalItemName(
                            "Tool: " + __instance.name
                        )
                    );
                    return false;
                }
            }
        }

        [HarmonyPatch(typeof(InventoryToolCrest), "get_IsUnlocked", new Type[0])]
        internal static class InventoryToolCrest_IsUnlocked_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(InventoryToolCrest __instance, ref bool __result)
            {
                SaveState state = SaveState.Instance;
                if (state == null ||
                    !state.IsRandomized(ItemType.Crest) ||
                    __instance.CrestData == null)
                {
                    return true;
                }

                if (!state.receivedItems.Contains(
                    CrestNames.GetItemNameFromInternal(
                        __instance.CrestData.name
                    )
                ))
                {
                    __result = false;
                    return false;
                }

                // Native IsVisible selects one unlocked Hunter
                // version and hides superseded variants.
                return true;
            }
        }

        [HarmonyPatch(typeof(InventoryPane), "get_IsAvailable", new Type[0])]
        internal static class InventoryPane_IsAvailable_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(ref bool __result)
            {
                SaveState state = SaveState.Instance;
                if (state == null ||
                    (
                        !state.IsRandomized(ItemType.Tool) &&
                        !state.IsRandomized(ItemType.Spell) &&
                        !state.IsRandomized(ItemType.Crest) &&
                        !state.IsRandomized(ItemType.CrestSlot)
                    ))
                {
                    return true;
                }

                __result = true;
                return false;
            }
        }
    }
}
