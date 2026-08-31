using GlobalEnums;
using GlobalSettings;
using HarmonyLib;
using HutongGames.PlayMaker;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    internal static class HeroControllerAbilityPatchUtil
    {
        public static T Field<T>(object instance, string name)
        {
            return Traverse.Create(instance).Field(name).GetValue<T>();
        }

        public static T Property<T>(object instance, string name)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            PropertyInfo property = instance.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );
            if (property == null)
            {
                throw new MissingMemberException(instance.GetType().FullName, name);
            }

            return (T)property.GetValue(instance, null);
        }

        public static T ConfigField<T>(object heroController, string name)
        {
            object config = Property<object>(heroController, "Config");
            // HeroControllerConfig exposes public properties such as
            // CanBrolly and CanDoubleJump. Their backing fields use different
            // lowercase names. Looking up the property name as a field returns
            // default(T) and silently disables the ability.
            return Property<T>(config, name);
        }

        public static bool CState(object heroController, string name)
        {
            object cState = Field<object>(heroController, "cState");
            return Traverse.Create(cState).Field(name).GetValue<bool>();
        }

        public static T Call<T>(object instance, string methodName, params object[] args)
        {
            return Traverse.Create(instance).Method(methodName, args).GetValue<T>();
        }

        public static bool ResolveBrollyOwnership(bool vanillaOwned)
        {
            SaveState state = SaveState.Instance;
            bool owned = state == null || !state.IsRandomized(ItemType.Skill)
                ? vanillaOwned
                : state.canBrolly;
            if (NakedTrapManager.SuppressesCloakAbilities)
            {
                return false;
            }
            return owned;
        }

        public static bool ResolveDoubleJumpOwnership(bool vanillaOwned)
        {
            SaveState state = SaveState.Instance;
            bool owned = state == null || !state.IsRandomized(ItemType.Skill)
                ? vanillaOwned
                : state.canDoubleJump;
            if (NakedTrapManager.SuppressesCloakAbilities)
            {
                return false;
            }
            return owned;
        }

        public static bool HasBrolly(PlayerData playerData)
        {
            return ResolveBrollyOwnership(
                playerData != null && playerData.hasBrolly
            );
        }

        public static bool HasDoubleJump(PlayerData playerData)
        {
            return ResolveDoubleJumpOwnership(
                playerData != null && playerData.hasDoubleJump
            );
        }
    }

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.HasNeedolin))]
    internal static class HeroController_HasNeedolin_Patch
    {
        private static bool Prefix(HeroController __instance, ref bool __result)
        {
            SaveState state = SaveState.Instance;
            if (state == null || !state.IsRandomized(ItemType.Skill))
            {
                return true;
            }

            __result = __instance.Config.CanPlayNeedolin && state.canUseNeedolin;
            return false;
        }
    }

    [HarmonyPatch(
        typeof(HeroController),
        "RegainControl",
        new[] { typeof(bool) }
    )]
    internal static class HeroController_RegainControl_BrollyPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions
        )
        {
            List<CodeInstruction> result = new List<CodeInstruction>(instructions);
            FieldInfo vanillaBrolly = AccessTools.Field(
                typeof(PlayerData),
                nameof(PlayerData.hasBrolly)
            );
            FieldInfo vanillaDoubleJump = AccessTools.Field(
                typeof(PlayerData),
                nameof(PlayerData.hasDoubleJump)
            );
            MethodInfo randomizedBrollyOwnership = AccessTools.Method(
                typeof(HeroControllerAbilityPatchUtil),
                nameof(HeroControllerAbilityPatchUtil.HasBrolly)
            );
            MethodInfo randomizedDoubleJumpOwnership = AccessTools.Method(
                typeof(HeroControllerAbilityPatchUtil),
                nameof(HeroControllerAbilityPatchUtil.HasDoubleJump)
            );
            int brollyReplacements = 0;
            int doubleJumpReplacements = 0;

            foreach (CodeInstruction instruction in result)
            {
                if (instruction.opcode == OpCodes.Ldfld &&
                    Equals(instruction.operand, vanillaBrolly))
                {
                    // The PlayerData instance is already on the evaluation
                    // stack, so a static helper call is a drop-in replacement.
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = randomizedBrollyOwnership;
                    brollyReplacements++;
                }
                else if (instruction.opcode == OpCodes.Ldfld &&
                    Equals(instruction.operand, vanillaDoubleJump))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = randomizedDoubleJumpOwnership;
                    doubleJumpReplacements++;
                }
            }

            if (brollyReplacements != 2)
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Expected two Drifter's Cloak ownership reads " +
                    "in HeroController.RegainControl(bool), replaced " +
                    brollyReplacements + "."
                );
            }
            if (doubleJumpReplacements != 1)
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Expected one Faydown Cloak ownership read " +
                    "in HeroController.RegainControl(bool), replaced " +
                    doubleJumpReplacements + "."
                );
            }

            return result;
        }
    }

    [HarmonyPatch(
        typeof(HeroController),
        "TickFrostEffect",
        new[] { typeof(bool) }
    )]
    internal static class HeroController_TickFrostEffect_FaydownPatch
    {
        private struct OwnershipSnapshot
        {
            internal PlayerData PlayerData;
            internal bool NativeDoubleJump;
            internal bool Changed;
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(
            PlayerData ___playerData,
            out OwnershipSnapshot __state
        )
        {
            __state = default;
            SaveState state = SaveState.Instance;
            if (state == null ||
                !state.IsRandomized(ItemType.Skill) ||
                ___playerData == null)
            {
                return;
            }

            bool nativeDoubleJump = ___playerData.hasDoubleJump;
            bool effectiveDoubleJump =
                HeroControllerAbilityPatchUtil.ResolveDoubleJumpOwnership(
                    nativeDoubleJump
                );
            if (nativeDoubleJump == effectiveDoubleJump)
            {
                return;
            }

            // TickFrostEffect snapshots hasDoubleJump into a local before it
            // applies the frost logic. Project AP ownership into the
            // exact PlayerData object owned by this HeroController for the
            // duration of that call, then restore the native source flag in
            // the finalizer. This keeps source completion separate from AP
            // inventory and does not depend on the method's current IL shape.
            __state = new OwnershipSnapshot
            {
                PlayerData = ___playerData,
                NativeDoubleJump = nativeDoubleJump,
                Changed = true,
            };
            ___playerData.hasDoubleJump = effectiveDoubleJump;
        }

        [HarmonyFinalizer]
        [HarmonyPriority(Priority.Last)]
        private static Exception Finalizer(
            Exception __exception,
            OwnershipSnapshot __state
        )
        {
            if (__state.Changed && __state.PlayerData != null)
            {
                __state.PlayerData.hasDoubleJump =
                    __state.NativeDoubleJump;
            }

            return __exception;
        }
    }

    [HarmonyPatch]
    public static class HeroControllerPatches
    {
        [HarmonyPatch(typeof(HeroController), "HasHarpoonDash")]
        internal static class HeroController_HasHarpoonDash_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(HeroController __instance, ref bool __result)
            {
                SaveState state = SaveState.Instance;
                if (state == null || !state.IsRandomized(ItemType.Skill))
                {
                    return true;
                }

                __result =
                    state.canUseHarpoon &&
                    HeroControllerAbilityPatchUtil.ConfigField<bool>(__instance, "CanHarpoonDash");

                return false;
            }
        }

        [HarmonyPatch(typeof(HeroController), "CanDash")]
        internal static class HeroController_CanDash_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(HeroController __instance, ref bool __result)
            {
                SaveState state = SaveState.Instance;
                if (state == null || !state.IsRandomized(ItemType.Skill))
                {
                    return true;
                }

                ActorStates heroState = HeroControllerAbilityPatchUtil.Field<ActorStates>(__instance, "hero_state");
                float dashCooldownTimer = HeroControllerAbilityPatchUtil.Field<float>(__instance, "dashCooldownTimer");
                float attackTime = HeroControllerAbilityPatchUtil.Field<float>(__instance, "attack_time");
                bool airDashed = HeroControllerAbilityPatchUtil.Field<bool>(__instance, "airDashed");
                bool canUseEmergencySwiftStep =
                    WidowSequenceSafety.CanUseEmergencySwiftStep(state);

                __result =
                    heroState != ActorStates.no_input &&
                    heroState != ActorStates.hard_landing &&
                    heroState != ActorStates.dash_landing &&
                    dashCooldownTimer <= 0f &&
                    !HeroControllerAbilityPatchUtil.CState(__instance, "dashing") &&
                    !HeroControllerAbilityPatchUtil.CState(__instance, "backDashing") &&
                    (
                        !HeroControllerAbilityPatchUtil.CState(__instance, "attacking") ||
                        attackTime >= HeroControllerAbilityPatchUtil.ConfigField<float>(__instance, "AttackRecoveryTime")
                    ) &&
                    !HeroControllerAbilityPatchUtil.CState(__instance, "preventDash") &&
                    (
                        HeroControllerAbilityPatchUtil.CState(__instance, "onGround") ||
                        !airDashed ||
                        HeroControllerAbilityPatchUtil.CState(__instance, "wallSliding")
                    ) &&
                    !HeroControllerAbilityPatchUtil.CState(__instance, "hazardDeath") &&
                    (state.canDash || canUseEmergencySwiftStep);

                return false;
            }
        }

        [HarmonyPatch(typeof(HeroController), "CanSprint")]
        internal static class HeroController_CanSprint_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(ref bool __result)
            {
                SaveState state = SaveState.Instance;
                if (state != null && state.IsRandomized(ItemType.Skill))
                {
                    __result =
                        __result &&
                        (
                            state.canSprint ||
                            WidowSequenceSafety.CanUseEmergencySwiftStep(
                                state
                            )
                        );
                }
            }
        }

        [HarmonyPatch(
            typeof(HeroController),
            "HeroDash",
            new[] { typeof(bool) }
        )]
        internal static class HeroController_HeroDash_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix()
            {
                SaveState state = SaveState.Instance;
                if (state == null || !state.IsRandomized(ItemType.Skill))
                {
                    return true;
                }

                // Some native sprint/air-dash transitions call HeroDash
                // directly instead of consulting CanDash. Guard the action
                // itself so Sprint alone cannot leak the Dash upgrade.
                return
                    state.canDash ||
                    WidowSequenceSafety.CanUseEmergencySwiftStep(state);
            }
        }

        [HarmonyPatch(typeof(HeroController), "LookForQueueInput")]
        internal static class HeroController_LookForQueueInput_SprintPatch
        {
            private const string SprintStartSpeedVariable =
                "Sprint Start Speed";
            private const string SprintRegularSpeedVariable =
                "Sprint Speed Regular";
            private static PlayMakerFSM trackedSprintFsm;
            private static float nativeSprintStartSpeed;
            private static bool hasNativeSprintStartSpeed;
            private static bool loggedMissingSprintSpeed;

            [HarmonyPostfix]
            private static void Postfix(HeroController __instance)
            {
                SaveState state = SaveState.Instance;
                bool sprintStartSpeedSuppressed =
                    SynchronizeSprintStartSpeed(__instance, state);
                if (state == null ||
                    !state.IsRandomized(ItemType.Skill) ||
                    !state.canSprint ||
                    state.canDash ||
                    !sprintStartSpeedSuppressed ||
                    __instance == null ||
                    !HeroControllerAbilityPatchUtil.CState(
                        __instance,
                        "onGround"
                    ))
                {
                    return;
                }

                try
                {
                    InputHandler inputHandler =
                        HeroControllerAbilityPatchUtil.Field<InputHandler>(
                            __instance,
                            "inputHandler"
                        );
                    if (inputHandler == null ||
                        !inputHandler.inputActions.Dash.WasPressed ||
                        !HeroControllerAbilityPatchUtil.Field<bool>(
                            __instance,
                            "acceptingInput"
                        ) ||
                        !HeroControllerAbilityPatchUtil.Field<bool>(
                            __instance,
                            "isGameplayScene"
                        ) ||
                        HeroControllerAbilityPatchUtil.Call<bool>(
                            __instance,
                            "IsInputBlocked"
                        ) ||
                        InteractManager.BlockingInteractable != null ||
                        HeroControllerAbilityPatchUtil.Call<bool>(
                            __instance,
                            "IsPaused"
                        ) ||
                        !__instance.CanSprint() ||
                        __instance.sprintFSM == null)
                    {
                        return;
                    }

                    // Sprint and Dash use the same native input. With only
            // Sprint owned, enter the sprint FSM directly,
                    // but only while grounded. The native sprint FSM can
                    // otherwise turn this direct event into an aerial dash.
                    // Once Dash is also owned, the original dash-then-sprint
                    // route remains untouched.
                    __instance.sprintFSM.SendEvent("TRY SPRINT");
                }
                catch (Exception ex)
                {
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Sprint input could not be applied: " +
                        ex.Message
                    );
                }
            }

            private static bool SynchronizeSprintStartSpeed(
                HeroController hero,
                SaveState state
            )
            {
                PlayMakerFSM sprintFsm =
                    hero == null ? null : hero.sprintFSM;
                if (!ReferenceEquals(trackedSprintFsm, sprintFsm))
                {
                    RestoreTrackedSprintStartSpeed();
                    trackedSprintFsm = sprintFsm;
                    hasNativeSprintStartSpeed = false;
                    loggedMissingSprintSpeed = false;
                }

                if (sprintFsm == null)
                {
                    return false;
                }

                FsmFloat startSpeed =
                    sprintFsm.FsmVariables.FindFsmFloat(
                        SprintStartSpeedVariable
                    );
                FsmFloat regularSpeed =
                    sprintFsm.FsmVariables.FindFsmFloat(
                        SprintRegularSpeedVariable
                    );
                if (startSpeed == null || regularSpeed == null)
                {
                    if (!loggedMissingSprintSpeed)
                    {
                        loggedMissingSprintSpeed = true;
                        RandomizerPlugin.Log?.LogWarning(
                            "[RANDOMIZER] Sprint-only Swift Step could not " +
                            "resolve the shipped sprint speed variables; " +
                            "the input was withheld to avoid leaking Dash."
                        );
                    }
                    return false;
                }

                if (!hasNativeSprintStartSpeed)
                {
                    nativeSprintStartSpeed = startSpeed.Value;
                    hasNativeSprintStartSpeed = true;
                }

                bool sprintOnly =
                    state != null &&
                    state.IsRandomized(ItemType.Skill) &&
                    state.canSprint &&
                    !state.canDash;
                startSpeed.Value = sprintOnly
                    ? regularSpeed.Value
                    : nativeSprintStartSpeed;
                return sprintOnly;
            }

            private static void RestoreTrackedSprintStartSpeed()
            {
                if (trackedSprintFsm == null ||
                    !hasNativeSprintStartSpeed)
                {
                    return;
                }

                FsmFloat startSpeed =
                    trackedSprintFsm.FsmVariables.FindFsmFloat(
                        SprintStartSpeedVariable
                    );
                if (startSpeed != null)
                {
                    startSpeed.Value = nativeSprintStartSpeed;
                }
            }
        }

        [HarmonyPatch(typeof(HeroController), "GetCanAirDashCancel")]
        internal static class HeroController_GetCanAirDashCancel_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(
                HeroController __instance,
                ref bool __result
            )
            {
                SaveState state = SaveState.Instance;
                if (state != null && state.IsRandomized(ItemType.Skill))
                {
                    __result =
                        (
                            state.canDash ||
                            WidowSequenceSafety.CanUseEmergencySwiftStep(
                                state
                            )
                        ) &&
                        !__instance.GetAirdashed();
                }
            }
        }

        [HarmonyPatch(typeof(HeroWaterController), "CanDash")]
        internal static class HeroWaterController_CanDash_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(
                HeroWaterController __instance,
                ref bool __result
            )
            {
                SaveState state = SaveState.Instance;
                if (state == null || !state.IsRandomized(ItemType.Skill))
                {
                    return true;
                }

                bool isEnterTumbling =
                    HeroControllerAbilityPatchUtil.Field<bool>(
                        __instance,
                        "isEnterTumbling"
                    );
                double nextDashTime =
                    HeroControllerAbilityPatchUtil.Field<double>(
                        __instance,
                        "nextDashTime"
                    );
                __result =
                    !isEnterTumbling &&
                    state.canDash &&
                    Time.timeAsDouble >= nextDashTime;
                return false;
            }
        }

        [HarmonyPatch(typeof(HeroController), "CanDoubleJump", new[] { typeof(bool) })]
        internal static class HeroController_CanDoubleJump_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(HeroController __instance, ref bool __result, bool checkControlState = true)
            {
                SaveState state = SaveState.Instance;
                if (state == null || !state.IsRandomized(ItemType.Skill))
                {
                    return true;
                }

                ActorStates heroState = HeroControllerAbilityPatchUtil.Field<ActorStates>(__instance, "hero_state");
                bool controlRelinquished = HeroControllerAbilityPatchUtil.Field<bool>(__instance, "controlReqlinquished");
                bool doubleJumped = HeroControllerAbilityPatchUtil.Field<bool>(__instance, "doubleJumped");

                bool controlOk =
                    !checkControlState ||
                    
                        heroState != ActorStates.no_input &&
                        heroState != ActorStates.hard_landing &&
                        heroState != ActorStates.dash_landing &&
                        !controlRelinquished
                    ;

                __result =
                    controlOk &&
                    HeroControllerAbilityPatchUtil.ResolveDoubleJumpOwnership(
                        state.canDoubleJump
                    ) &&
                    !doubleJumped &&
                    !HeroControllerAbilityPatchUtil.Call<bool>(__instance, "IsDashLocked") &&
                    !HeroControllerAbilityPatchUtil.CState(__instance, "wallSliding") &&
                    !HeroControllerAbilityPatchUtil.CState(__instance, "backDashing") &&
                    !HeroControllerAbilityPatchUtil.Call<bool>(__instance, "IsAttackLocked") &&
                    !HeroControllerAbilityPatchUtil.CState(__instance, "bouncing") &&
                    !HeroControllerAbilityPatchUtil.CState(__instance, "shroomBouncing") &&
                    !HeroControllerAbilityPatchUtil.CState(__instance, "onGround") &&
                    !HeroControllerAbilityPatchUtil.CState(__instance, "doubleJumping") &&
                    HeroControllerAbilityPatchUtil.ConfigField<bool>(__instance, "CanDoubleJump") &&
                    !HeroControllerAbilityPatchUtil.Call<bool>(__instance, "TryQueueWallJumpInterrupt") &&
                    !HeroControllerAbilityPatchUtil.Call<bool>(__instance, "IsApproachingSolidGround");

                return false;
            }
        }

        [HarmonyPatch(typeof(HeroController), "CanFloat", new[] { typeof(bool) })]
        internal static class HeroController_CanFloat_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(HeroController __instance, ref bool __result, bool checkControlState = true)
            {
                SaveState state = SaveState.Instance;
                if (state == null || !state.IsRandomized(ItemType.Skill))
                {
                    return true;
                }

                ActorStates heroState = HeroControllerAbilityPatchUtil.Field<ActorStates>(__instance, "hero_state");
                bool controlRelinquished = HeroControllerAbilityPatchUtil.Field<bool>(__instance, "controlReqlinquished");
                int ledgeBufferSteps = HeroControllerAbilityPatchUtil.Field<int>(__instance, "ledgeBufferSteps");

                bool controlOk =
                    !checkControlState ||
                    
                        heroState != ActorStates.no_input &&
                        heroState != ActorStates.hard_landing &&
                        heroState != ActorStates.dash_landing &&
                        !controlRelinquished
                    ;

                __result =
                    controlOk &&
                    !HeroControllerAbilityPatchUtil.Call<bool>(__instance, "CanInfiniteAirJump") &&
                    HeroControllerAbilityPatchUtil.ResolveBrollyOwnership(
                        state.canBrolly
                    ) &&
                    !HeroControllerAbilityPatchUtil.CState(__instance, "onGround") &&
                    !HeroControllerAbilityPatchUtil.Call<bool>(__instance, "IsDashLocked") &&
                    !HeroControllerAbilityPatchUtil.CState(__instance, "swimming") &&
                    ledgeBufferSteps <= 0 &&
                    !HeroControllerAbilityPatchUtil.CState(__instance, "wallSliding") &&
                    !HeroControllerAbilityPatchUtil.Call<bool>(__instance, "IsAttackLocked") &&
                    !HeroControllerAbilityPatchUtil.CState(__instance, "hazardDeath") &&
                    !HeroControllerAbilityPatchUtil.CState(__instance, "hazardRespawning") &&
                    (
                        !HeroControllerAbilityPatchUtil.CState(__instance, "doubleJumping") ||
                        HeroControllerAbilityPatchUtil.CState(__instance, "inUpdraft")
                    ) &&
                    HeroControllerAbilityPatchUtil.Call<bool>(__instance, "CanDoFSMCancelMove") &&
                    HeroControllerAbilityPatchUtil.ConfigField<bool>(__instance, "CanBrolly") &&
                    !HeroControllerAbilityPatchUtil.Call<bool>(__instance, "TryQueueWallJumpInterrupt") &&
                    !HeroControllerAbilityPatchUtil.Call<bool>(__instance, "IsApproachingSolidGround");

                return false;
            }
        }

        [HarmonyPatch(typeof(HeroController), "CanNailCharge")]
        internal static class HeroController_CanNailCharge_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(HeroController __instance, ref bool __result)
            {
                SaveState state = SaveState.Instance;
                if (state == null || !state.IsRandomized(ItemType.Skill))
                {
                    return true;
                }

                bool controlRelinquished = HeroControllerAbilityPatchUtil.Field<bool>(__instance, "controlReqlinquished");
                bool allowNailChargingWhileRelinquished = HeroControllerAbilityPatchUtil.Field<bool>(__instance, "allowNailChargingWhileRelinquished");

                __result =
                    !HeroControllerAbilityPatchUtil.CState(__instance, "dead") &&
                    !HeroControllerAbilityPatchUtil.CState(__instance, "attacking") &&
                    (!controlRelinquished || allowNailChargingWhileRelinquished) &&
                    !HeroControllerAbilityPatchUtil.CState(__instance, "recoiling") &&
                    !HeroControllerAbilityPatchUtil.CState(__instance, "recoilingLeft") &&
                    !HeroControllerAbilityPatchUtil.CState(__instance, "recoilingRight") &&
                    !HeroControllerAbilityPatchUtil.CState(__instance, "hazardDeath") &&
                    !HeroControllerAbilityPatchUtil.CState(__instance, "hazardRespawning") &&
                    state.canChargeSlash &&
                    HeroControllerAbilityPatchUtil.ConfigField<bool>(__instance, "CanNailCharge") &&
                    !HeroControllerAbilityPatchUtil.Call<bool>(__instance, "IsInputBlocked") &&
                    InteractManager.BlockingInteractable == null;

                return false;
            }
        }

        [HarmonyPatch(typeof(HeroController), "CanSuperJump")]
        internal static class HeroController_CanSuperJump_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(HeroController __instance, ref bool __result)
            {
                SaveState state = SaveState.Instance;
                if (state == null || !state.IsRandomized(ItemType.Skill))
                {
                    return true;
                }

                object gm = HeroControllerAbilityPatchUtil.Field<object>(__instance, "gm");
                bool isPaused = Traverse.Create(gm).Field("isPaused").GetValue<bool>();

                ActorStates heroState = HeroControllerAbilityPatchUtil.Field<ActorStates>(__instance, "hero_state");
                float attackTime = HeroControllerAbilityPatchUtil.Field<float>(__instance, "attack_time");

                __result =
                    !isPaused &&
                    heroState != ActorStates.hard_landing &&
                    heroState != ActorStates.dash_landing &&
                    HeroControllerAbilityPatchUtil.CState(__instance, "onGround") &&
                    !HeroControllerAbilityPatchUtil.CState(__instance, "dashing") &&
                    !HeroControllerAbilityPatchUtil.CState(__instance, "hazardDeath") &&
                    !HeroControllerAbilityPatchUtil.CState(__instance, "hazardRespawning") &&
                    !HeroControllerAbilityPatchUtil.CState(__instance, "backDashing") &&
                    (
                        !HeroControllerAbilityPatchUtil.CState(__instance, "attacking") ||
                        attackTime >= HeroControllerAbilityPatchUtil.ConfigField<float>(__instance, "AttackRecoveryTime")
                    ) &&
                    HeroControllerAbilityPatchUtil.Call<bool>(__instance, "CanDoFSMCancelMove") &&
                    !HeroControllerAbilityPatchUtil.CState(__instance, "recoilFrozen") &&
                    !HeroControllerAbilityPatchUtil.CState(__instance, "recoiling") &&
                    !HeroControllerAbilityPatchUtil.CState(__instance, "transitioning") &&
                    state.canSilkSoar;

                return false;
            }
        }
    }
}
