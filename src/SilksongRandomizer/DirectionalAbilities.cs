using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using SilksongRandomizer.Patches;

namespace SilksongRandomizer
{
    internal static class DirectionalAbilities
    {
        internal static bool Owns(SaveState state, string ability, bool right)
        {
            string name = ability switch
            {
                "Cling Grip" => right ? "Cling Grip (Right)" : "Cling Grip (Left)",
                "Clawline" => right ? "Clawline (Right)" : "Clawline (Left)",
                "Swift Step" => right ? "Swift Step (Right)" : "Swift Step (Left)",
                "Dash" => right ? "Dash (Right)" : "Dash (Left)",
                "Ledge Grab" => right ? "Ledge Grab (Right)" : "Ledge Grab (Left)",
                _ => null,
            };
            return name != null && state.receivedItems.Contains(name);
        }

        internal static bool Cling(SaveState state, bool wallToRight) =>
            !state.directionalClingGrip || Owns(state, "Cling Grip", wallToRight);

        internal static bool Clawline(SaveState state, bool right) =>
            !state.directionalClawline || Owns(state, "Clawline", right);

        internal static bool Dash(SaveState state, bool right) =>
            !state.directionalSwiftStep ||
            (state.splitDashAndSprint && state.swiftStepLevel >= (right == state.swiftStepFirstRight ? 2 : 3)) ||
            Owns(state, state.splitDashAndSprint ? "Dash" : "Swift Step", right);

        internal static bool Sprint(SaveState state, bool right) =>
            !state.directionalSwiftStep || state.splitDashAndSprint || Owns(state, "Swift Step", right);

        internal static bool WallRight(HeroController hero)
        {
            if (HeroControllerAbilityPatchUtil.Field<bool>(hero, "wallSlidingL")) return false;
            if (HeroControllerAbilityPatchUtil.Field<bool>(hero, "wallSlidingR")) return true;
            if (HeroControllerAbilityPatchUtil.Field<bool>(hero, "touchingWallL")) return false;
            if (HeroControllerAbilityPatchUtil.Field<bool>(hero, "touchingWallR")) return true;
            return hero.cState.facingRight;
        }

        internal static bool InputRight(HeroController hero, bool flipFromWall = false)
        {
            if (flipFromWall && hero.cState.wallSliding) return !hero.cState.facingRight;
            InputHandler input = HeroControllerAbilityPatchUtil.Field<InputHandler>(hero, "inputHandler");
            if (input != null)
            {
                bool left = input.inputActions.Left.IsPressed;
                bool right = input.inputActions.Right.IsPressed;
                if (left != right) return right;
            }
            return hero.cState.facingRight;
        }

        internal static bool CanDash(SaveState state, HeroController hero)
        {
            if (!state.canDash) return false;
            if (!state.directionalSwiftStep) return true;
            InputHandler input = HeroControllerAbilityPatchUtil.Field<InputHandler>(hero, "inputHandler");
            if (input != null && input.inputActions.Down.IsPressed && !hero.cState.onGround &&
                !input.inputActions.Left.IsPressed && !input.inputActions.Right.IsPressed &&
                !(hero.cState.jumping && HeroControllerAbilityPatchUtil.Field<int>(hero, "jump_steps") == 0))
                return Dash(state, false) && Dash(state, true);
            bool right = hero.cState.wallSliding ? !hero.cState.facingRight :
                HeroControllerAbilityPatchUtil.Field<bool>(hero, "dashCurrentFacing") ? hero.cState.facingRight : InputRight(hero);
            return Dash(state, right);
        }

        internal static bool CanDashPressed(SaveState state, HeroController hero)
        {
            if (state.directionalClingGrip && hero.cState.wallSliding &&
                HeroControllerAbilityPatchUtil.Call<bool>(hero, "CanWallScramble"))
                return state.canDash && Dash(state, WallRight(hero));
            return CanDash(state, hero);
        }

        internal static bool CanSprint(SaveState state, HeroController hero) =>
            state.canSprint && Sprint(state, InputRight(hero));

        [HarmonyPatch(typeof(HeroController), nameof(HeroController.CanHarpoonDash))]
        private static class HarpoonAvailable
        {
            private static void Postfix(HeroController __instance, ref bool __result)
            {
                SaveState state = SaveState.Instance;
                if (__result && state != null && state.IsRandomized(ItemType.Skill))
                    __result = Clawline(state, InputRight(__instance, true));
            }
        }

        [HarmonyPatch(typeof(FsmState), nameof(FsmState.OnEnter))]
        private static class MovementState
        {
            private static bool Prefix(FsmState __instance)
            {
                SaveState state = SaveState.Instance;
                if (state == null ||
                    !state.IsRandomized(ItemType.Skill)) return true;
                HeroController hero = HeroController.instance;
                if (hero == null) return true;
                Fsm fsm = __instance.Fsm;
                string name = __instance.Name;
                if (state.directionalClawline && ReferenceEquals(fsm, hero.harpoonDashFSM?.Fsm) &&
                    (name == "Left" || name == "Right" || name == "Ring Check L" || name == "Ring Check R") &&
                    !Clawline(state, name == "Right" || name == "Ring Check R"))
                {
                    fsm.Variables.FindFsmBool("Queued Move").Value = false;
                    fsm.SetState("Cancel All");
                    return false;
                }
                if (ReferenceEquals(fsm, hero.sprintFSM?.Fsm) &&
                    (name == "Ground Sprint L" || name == "Ground Sprint R" || name == "Air Sprint L" || name == "Air Sprint R") &&
                    (!state.canSprint || !Sprint(state, name.EndsWith(" R"))) &&
                    !WidowSequenceSafety.CanUseEmergencySwiftStep(state))
                {
                    fsm.SetState("Regain Control Normal");
                    return false;
                }
                return true;
            }
        }
    }
}
