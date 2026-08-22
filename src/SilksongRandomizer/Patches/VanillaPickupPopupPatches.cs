using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine.SceneManagement;

namespace SilksongRandomizer.Patches
{
    /// <summary>
    /// Suppresses native acquisition messages only while their corresponding
    /// pickup is an Archipelago location. Randomizer receipt messages use
    /// CollectableUIMsg directly and are left untouched.
    /// </summary>
    internal static class VanillaPickupPopupPatches
    {
        private readonly struct PopupSource
        {
            internal readonly ItemType ItemType;
            internal readonly string LocationName;
            internal readonly string SceneName;
            internal readonly string OwnerName;
            internal readonly string FsmName;
            internal readonly string StateName;

            internal PopupSource(
                ItemType itemType,
                string locationName,
                string sceneName,
                string ownerName,
                string fsmName,
                string stateName
            )
            {
                ItemType = itemType;
                LocationName = locationName;
                SceneName = sceneName;
                OwnerName = ownerName;
                FsmName = fsmName;
                StateName = stateName;
            }

            internal bool Matches(FsmStateAction action)
            {
                return action != null &&
                       action.Owner != null &&
                       string.Equals(
                           action.Owner.scene.name,
                           SceneName,
                           StringComparison.Ordinal
                       ) &&
                       string.Equals(
                           action.Owner.name,
                           OwnerName,
                           StringComparison.Ordinal
                       ) &&
                       action.Fsm != null &&
                       string.Equals(
                           action.Fsm.Name,
                           FsmName,
                           StringComparison.Ordinal
                       ) &&
                       action.State != null &&
                       string.Equals(
                           action.State.Name,
                           StateName,
                           StringComparison.Ordinal
                       );
            }
        }

        private static readonly PopupSource SprintSource = new PopupSource(
            ItemType.Skill,
            "Swift Step",
            "Bone_East_05",
            "Shrine Weaver Ability",
            "Inspection",
            "Powerup Msg"
        );

        private static readonly PopupSource WallJumpSource = new PopupSource(
            ItemType.Skill,
            "Cling Grip",
            "Shellwood_10",
            "Shrine Weaver Ability",
            "Inspection",
            "Powerup Msg"
        );

        private static readonly PopupSource HarpoonDashSource =
            new PopupSource(
                ItemType.Skill,
                "Clawline",
                "Under_18",
                "Shrine Weaver Ability",
                "Inspection",
                "Powerup Msg"
            );

        private static readonly PopupSource NeedolinSource = new PopupSource(
            ItemType.Skill,
            "Needolin",
            "Belltown_Shrine",
            "Spinner Boss",
            "Control",
            "Get Needolin"
        );

        private static readonly PopupSource SuperJumpSource = new PopupSource(
            ItemType.Skill,
            "Silk Soar",
            "Abyss_08",
            "Shrine Weaver Ability",
            "Inspection",
            "Powerup Msg"
        );

        private static readonly PopupSource SilkSpearSource = new PopupSource(
            ItemType.Spell,
            "Silkspear",
            "Mosstown_02",
            "Shrine Weaver Ability",
            "Inspection",
            "Skill Msg"
        );

        private static readonly PopupSource ParrySource = new PopupSource(
            ItemType.Spell,
            "Cross Stitch",
            "Organ_01",
            "Phantom",
            "Control",
            "UI Msg"
        );

        private static readonly PopupSource SilkBossNeedleSource =
            new PopupSource(
                ItemType.Spell,
                "Pale Nails",
                "Cradle_03_Destroyed",
                "Silk Needle Spell Get",
                "Control",
                "Msg"
            );

        private static readonly PopupSource SilkChargeSource = new PopupSource(
            ItemType.Spell,
            "Sharpdart",
            "Crawl_05",
            "Shrine Weaver Ability",
            "Inspection",
            "Skill Msg"
        );

        private static readonly PopupSource SilkBombSource = new PopupSource(
            ItemType.Spell,
            "Rune Rage",
            "Slab_10b",
            "Shrine Weaver Ability",
            "Inspection",
            "Skill Msg"
        );

        private static readonly PopupSource ThreadSphereSource =
            new PopupSource(
                ItemType.Spell,
                "Thread Storm",
                "Greymoor_22",
                "Shrine Weaver Ability",
                "Inspection",
                "Skill Msg"
            );

        private static readonly PopupSource FaydownSource = new PopupSource(
            ItemType.Skill,
            "Faydown Cloak",
            "Peak_08b",
            "DJ Get Sequence",
            "DJ Get Sequence",
            "Msg"
        );

        private static readonly PopupSource DriftersCloakSource =
            new PopupSource(
                ItemType.Skill,
                "Drifter's Cloak",
                "Bone_East_Umbrella",
                "Seamstress",
                "Dialogue",
                "Msg"
            );

        private static readonly PopupSource NeedleStrikeSource =
            new PopupSource(
                ItemType.Skill,
                "Needle Strike",
                "Room_Pinstress",
                "Pinstress Interior Ground Sit",
                "Behaviour",
                "Msg"
            );

        [ThreadStatic]
        private static int randomizedMapGetDepth;

        private static readonly FieldInfo FleaTargetTemplateField =
            AccessTools.Field(
                typeof(QuestTargetPlayerDataBools),
                "pdFieldTemplate"
            );

        private static bool IsActiveApLocation(PopupSource source)
        {
            SaveState state = SaveState.Instance;
            return state != null &&
                   state.IsRandomized(source.ItemType) &&
                   state.IsLocationEnabled(source.LocationName) &&
                   state.IsLocationInSeed(source.LocationName);
        }

        private static bool IsExactOrdinaryFleaPopup(
            SavedItemGetV2 action
        )
        {
            SaveState state = SaveState.Instance;
            return state != null &&
                   state.IsRoomBound &&
                   state.IsRandomized(ItemType.Flea) &&
                   action != null &&
                   action.Owner != null &&
                   string.Equals(
                       action.Owner.name,
                       "Flea Rescue Activation",
                       StringComparison.Ordinal
                   ) &&
                   action.Fsm != null &&
                   string.Equals(
                       action.Fsm.Name,
                       "Control",
                       StringComparison.Ordinal
                   ) &&
                   action.State != null &&
                   string.Equals(
                       action.State.Name,
                       "End",
                       StringComparison.Ordinal
                   ) &&
                   action.Item?.Value is QuestTargetPlayerDataBools target &&
                   FleaTargetTemplateField != null &&
                   string.Equals(
                       FleaTargetTemplateField.GetValue(target) as string,
                       "SavedFlea_",
                       StringComparison.Ordinal
                   );
        }

        private static bool ShouldSuppress(
            FsmStateAction action,
            PopupSource source
        )
        {
            return source.Matches(action) && IsActiveApLocation(source);
        }

        private static bool TryGetPowerUpSource(
            PowerUpGetMsg.PowerUps powerUp,
            out PopupSource source
        )
        {
            switch (powerUp)
            {
                case PowerUpGetMsg.PowerUps.Sprint:
                    source = SprintSource;
                    return true;
                case PowerUpGetMsg.PowerUps.WallJump:
                    source = WallJumpSource;
                    return true;
                case PowerUpGetMsg.PowerUps.HarpoonDash:
                    source = HarpoonDashSource;
                    return true;
                case PowerUpGetMsg.PowerUps.Needolin:
                    source = NeedolinSource;
                    return true;
                case PowerUpGetMsg.PowerUps.SuperJump:
                    source = SuperJumpSource;
                    return true;
                default:
                    source = default;
                    return false;
            }
        }

        private static bool TryGetSilkSkillSource(
            ToolItemSkill skill,
            out PopupSource source
        )
        {
            if (skill == null)
            {
                source = default;
                return false;
            }

            switch (skill.name)
            {
                case "Silk Spear":
                    source = SilkSpearSource;
                    return true;
                case "Parry":
                    source = ParrySource;
                    return true;
                case "Silk Boss Needle":
                    source = SilkBossNeedleSource;
                    return true;
                case "Silk Charge":
                    source = SilkChargeSource;
                    return true;
                case "Silk Bomb":
                    source = SilkBombSource;
                    return true;
                case "Thread Sphere":
                    source = ThreadSphereSource;
                    return true;
                default:
                    source = default;
                    return false;
            }
        }

        private static bool TryGetItemMessageSource(
            CreateUIMsgGetItem action,
            out PopupSource source
        )
        {
            if (FaydownSource.Matches(action))
            {
                source = FaydownSource;
                return true;
            }

            if (DriftersCloakSource.Matches(action))
            {
                source = DriftersCloakSource;
                return true;
            }

            if (NeedleStrikeSource.Matches(action))
            {
                source = NeedleStrikeSource;
                return true;
            }

            source = default;
            return false;
        }

        private static bool TryGetAbilityCollectableSource(
            string playerDataField,
            out PopupSource source
        )
        {
            switch (playerDataField)
            {
                case "hasDoubleJump":
                    source = FaydownSource;
                    return true;
                case "hasChargeSlash":
                    source = NeedleStrikeSource;
                    return true;
                case "hasSuperJump":
                    source = SuperJumpSource;
                    return true;
                case "hasWalljump":
                    source = WallJumpSource;
                    return true;
                case "hasBrolly":
                    source = DriftersCloakSource;
                    return true;
                case "hasDash":
                    source = SprintSource;
                    return true;
                case "hasHarpoonDash":
                    source = HarpoonDashSource;
                    return true;
                case "hasNeedolin":
                    source = NeedolinSource;
                    return true;
                default:
                    // Mapper Quill writes hasQuill directly in
                    // ShopItem.SetPurchased. No PlayerDataCollectable source
                    // identifies that purchase, so leave hasQuill popups
                    // native without a purchase-scoped source.
                    source = default;
                    return false;
            }
        }

        private static bool IsExactActiveAbilityCollectable(
            bool isMap,
            string playerDataField,
            string sceneName
        )
        {
            return !isMap &&
                   TryGetAbilityCollectableSource(
                       playerDataField,
                       out PopupSource source
                   ) &&
                   string.Equals(
                       sceneName,
                       source.SceneName,
                       StringComparison.Ordinal
                   ) &&
                   IsActiveApLocation(source);
        }

        private static bool IsExactManifestScene(
            StaticMapManifest.Entry entry,
            string sceneName
        )
        {
            if (entry == null || entry.Sources == null)
            {
                return false;
            }

            foreach (StaticMapManifest.Source source in entry.Sources)
            {
                if (source != null && string.Equals(
                        source.SceneName,
                        sceneName,
                        StringComparison.Ordinal
                    ))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsExactActiveMapCollectable(
            PlayerDataCollectable collectable,
            bool isMap,
            string playerDataField,
            string sceneName
        )
        {
            SaveState state = SaveState.Instance;
            if (state == null ||
                !state.IsRandomized(ItemType.Map) ||
                collectable == null ||
                !isMap ||
                !StaticMapManifest.TryGetByAssetName(
                    collectable.name,
                    out StaticMapManifest.Entry entry
                ) ||
                !string.Equals(
                    playerDataField,
                    entry.PlayerDataBool,
                    StringComparison.Ordinal
                ) ||
                !IsExactManifestScene(entry, sceneName))
            {
                return false;
            }

            return state.IsLocationEnabled(entry.LocationName) &&
                   state.IsLocationInSeed(entry.LocationName);
        }

        [HarmonyPatch(
            typeof(SavedItemGetV2),
            nameof(SavedItemGetV2.OnEnter))]
        internal static class RandomizedOrdinaryFleaPopupPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(
                SavedItemGetV2 __instance,
                out bool __state)
            {
                __state = false;
                if (!IsExactOrdinaryFleaPopup(__instance) ||
                    __instance.ShowPopup == null ||
                    !__instance.ShowPopup.Value)
                {
                    return;
                }

                __state = true;
                __instance.ShowPopup.Value = false;
            }

            [HarmonyFinalizer]
            private static Exception Finalizer(
                Exception __exception,
                SavedItemGetV2 __instance,
                bool __state)
            {
                if (__state && __instance?.ShowPopup != null)
                {
                    __instance.ShowPopup.Value = true;
                }

                return __exception;
            }
        }

        [HarmonyPatch(
            typeof(PlayerDataCollectable),
            nameof(PlayerDataCollectable.Get),
            new Type[] { typeof(bool) })]
        internal static class RandomizedMapGetPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(
                PlayerDataCollectable __instance,
                bool ___isMap,
                string ___linkedPDBool,
                ref bool showPopup,
                out bool __state)
            {
                string sceneName = SceneManager.GetActiveScene().name;
                __state = IsExactActiveMapCollectable(
                    __instance,
                    ___isMap,
                    ___linkedPDBool,
                    sceneName
                );
                bool suppressSmallPopup =
                    __state ||
                    IsExactActiveAbilityCollectable(
                        ___isMap,
                        ___linkedPDBool,
                        sceneName
                    );
                if (!suppressSmallPopup)
                {
                    return;
                }

                if (__state)
                {
                    randomizedMapGetDepth++;
                }

                // FakeCollectable.Get would otherwise show the native map or
                // ability name before Archipelago reports the location's real
                // item.
                showPopup = false;
            }

            [HarmonyFinalizer]
            private static Exception Finalizer(
                Exception __exception,
                bool __state)
            {
                if (__state)
                {
                    randomizedMapGetDepth = Math.Max(
                        0,
                        randomizedMapGetDepth - 1
                    );
                }

                return __exception;
            }
        }

        [HarmonyPatch(
            typeof(GameManager),
            nameof(GameManager.UpdateGameMapWithPopup),
            new Type[] { typeof(float) })]
        internal static class RandomizedMapUpdatePopupPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(
                GameManager __instance,
                ref bool __result)
            {
                if (randomizedMapGetDepth <= 0)
                {
                    return true;
                }

                // The map refresh and caller's returned update state remain
                // intact while only MapUpdatedPopupPrefab is omitted.
                __result = __instance.UpdateGameMap();
                return false;
            }
        }

        [HarmonyPatch(
            typeof(SpawnPowerUpGetMsg),
            nameof(SpawnPowerUpGetMsg.OnEnter))]
        internal static class RandomizedPowerUpGetMessagePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(SpawnPowerUpGetMsg __instance)
            {
                if (__instance == null ||
                    __instance.PowerUp == null ||
                    !(__instance.PowerUp.Value is
                        PowerUpGetMsg.PowerUps powerUp) ||
                    !TryGetPowerUpSource(powerUp, out PopupSource source) ||
                    !ShouldSuppress(__instance, source))
                {
                    return true;
                }

                // Each whitelisted source transitions on the action's normal
                // FINISHED signal. Everything else, including EvaHeal and
                // duplicate enum use in other FSMs, remains vanilla.
                __instance.Finish();
                return false;
            }
        }

        [HarmonyPatch(
            typeof(CreateUIMsgGetItem),
            nameof(CreateUIMsgGetItem.OnEnter))]
        internal static class RandomizedItemGetMessagePatch
        {
            private const string GetItemMessageEndedEvent =
                "GET ITEM MSG END";

            [HarmonyPrefix]
            private static bool Prefix(CreateUIMsgGetItem __instance)
            {
                if (!TryGetItemMessageSource(
                        __instance,
                        out PopupSource source
                    ) ||
                    !IsActiveApLocation(source))
                {
                    return true;
                }

                if (DriftersCloakSource.Matches(__instance))
                {
                    // The native Seamstress state writes hasBrolly after this
                    // popup action. Sending its completion event immediately
                    // skips that later action, so preserve the source/story
                    // flag before leaving the state. AP ownership still
                    // controls whether Hornet can actually glide.
                    LocationSet.RestoreDriftersCloakSourceFlag();
                    SaveState.Instance.CheckLocation(source.LocationName);
                }

                // CreateUIMsgGetItem normally registers this completion
                // event before enabling its generic prefab. Complete only the
                // three source FSMs so skipping the false full-screen
                // panel cannot strand their owning sequences.
                __instance.Finish();
                __instance.Fsm.Event(GetItemMessageEndedEvent);
                return false;
            }
        }

        [HarmonyPatch(
            typeof(SpawnSkillGetMsg),
            nameof(SpawnSkillGetMsg.OnEnter))]
        internal static class RandomizedSilkSkillGetMessagePatch
        {
            private const string SkillGetMessageFadedOutEvent =
                "SKILL GET MSG FADED OUT";
            private const string SkillGetMessageEndedEvent =
                "SKILL GET MSG ENDED";

            [HarmonyPrefix]
            private static bool Prefix(SpawnSkillGetMsg __instance)
            {
                ToolItemSkill skill =
                    __instance == null ||
                    __instance.Skill == null
                        ? null
                        : __instance.Skill.Value as ToolItemSkill;
                if (!TryGetSilkSkillSource(
                        skill,
                        out PopupSource source
                    ) ||
                    !ShouldSuppress(__instance, source))
                {
                    return true;
                }

                if (ParrySource.Matches(__instance))
                {
                    RandomizerPlugin plugin = RandomizerPlugin.Instance;
                    if (plugin == null)
                    {
                        return true;
                    }

                    // Phantom's UI Msg state starts SpawnSkillGetMsg before
                    // its FADED OUT and ENDED AddEventRegister actions. Finish
                    // this action now so those registrations can run, then
                    // reproduce SkillGetMsg's two lifecycle signals on later
                    // frames. Sending them here synchronously loses both
                    // events and leaves the scene fader permanently black.
                    __instance.Finish();
                    plugin.StartCoroutine(
                        CompleteCrossStitchSequenceAfterRegisters(
                            __instance.Fsm
                        )
                    );
                    return false;
                }

                // The shrine and Pale Nails FSMs wait only for the
                // SpawnSkillGetMsg action's normal completion transition.
                __instance.Finish();
                return false;
            }

            private static IEnumerator
                CompleteCrossStitchSequenceAfterRegisters(Fsm phantomFsm)
            {
                // StartCoroutine runs to its first yield immediately. This
                // frame boundary lets Phantom's two following
                // AddEventRegister actions finish first.
                yield return null;
                if (phantomFsm == null ||
                    !string.Equals(
                        phantomFsm.ActiveStateName,
                        "UI Msg",
                        StringComparison.Ordinal
                    ))
                {
                    yield break;
                }

                EventRegister.SendEvent(SkillGetMessageFadedOutEvent);

                // FADED OUT enters Get Control and starts its native screen
                // fade before ENDED advances the death sequence.
                yield return null;
                if (phantomFsm == null ||
                    !string.Equals(
                        phantomFsm.ActiveStateName,
                        "Get Control",
                        StringComparison.Ordinal
                    ))
                {
                    yield break;
                }

                EventRegister.SendEvent(SkillGetMessageEndedEvent);
            }
        }
    }
}
