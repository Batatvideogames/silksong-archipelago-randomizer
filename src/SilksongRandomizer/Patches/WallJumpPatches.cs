using HarmonyLib;
using SilksongRandomizer;
using System;
using HutongGames.PlayMaker.Actions;

internal static class HeroControllerWallJumpPatch
{
    private static void Apply(HeroController hero, PlayerData playerData, out bool oldValue)
    {
        oldValue = playerData.hasWalljump;
        SaveState state = SaveState.Instance;
        if (state != null && state.IsRandomized(ItemType.Skill))
        {
            playerData.hasWalljump = state.canWallJump && DirectionalAbilities.Cling(state, DirectionalAbilities.WallRight(hero));
        }
    }

    private static void Restore(PlayerData playerData, bool oldValue)
    {
        playerData.hasWalljump = oldValue;
    }

    [HarmonyPatch(typeof(PlayerDataVariableTest), nameof(PlayerDataVariableTest.OnEnter))]
    private static class DrifterWallSlide
    {
        private static bool Prefix(PlayerDataVariableTest __instance)
        {
            SaveState state = SaveState.Instance;
            if (state == null || !state.IsRandomized(ItemType.Skill) ||
                __instance.VariableName.Value != "hasWalljump" ||
                __instance.Fsm.Name != "Umbrella Float") return true;
            string name = __instance.State.Name;
            if (name != "Start Wall Slide L?" && name != "Start Wall Slide R?") return true;
            HeroController hero = HeroController.instance;
            if (hero == null || !ReferenceEquals(__instance.Fsm, hero.umbrellaFSM?.Fsm)) return true;
            bool owns = state.canWallJump && DirectionalAbilities.Cling(state, name == "Start Wall Slide R?");
            __instance.Fsm.Event(owns.Equals(__instance.ExpectedValue.GetValue()) ?
                __instance.IsExpectedEvent : __instance.IsNotExpectedEvent);
            __instance.Finish();
            return false;
        }
    }

    [HarmonyPatch(typeof(HeroController), "DoWallJump")]
    private static class WallJumpAction
    {
        private static bool Prefix(HeroController __instance)
        {
            SaveState state = SaveState.Instance;
            return state == null || !state.IsRandomized(ItemType.Skill) ||
                (state.canWallJump && DirectionalAbilities.Cling(state, DirectionalAbilities.WallRight(__instance)));
        }
    }

    [HarmonyPatch(typeof(HeroController), "BeginWallSlide", typeof(bool))]
    private static class BeginWallSlidePatch
    {
        private static bool Prefix(ref bool ___touchingWallL, ref bool ___touchingWallR,
            out (bool left, bool right) __state)
        {
            __state = (___touchingWallL, ___touchingWallR);
            SaveState state = SaveState.Instance;
            if (state == null || !state.IsRandomized(ItemType.Skill)) return true;
            if (!state.canWallJump) return false;
            ___touchingWallL &= DirectionalAbilities.Cling(state, false);
            ___touchingWallR &= DirectionalAbilities.Cling(state, true);
            return true;
        }

        private static void Finalizer(ref bool ___touchingWallL, ref bool ___touchingWallR,
            (bool left, bool right) __state)
        {
            ___touchingWallL = __state.left;
            ___touchingWallR = __state.right;
        }
    }

    [HarmonyPatch(typeof(HeroController), "TryQueueWallJumpInterrupt")]
    private static class TryQueueWallJumpInterruptPatch
    {
        private static void Prefix(HeroController __instance, PlayerData ___playerData, out bool __state)
        {
            Apply(__instance, ___playerData, out __state);
        }

        private static void Postfix(PlayerData ___playerData, bool __state)
        {
            Restore(___playerData, __state);
        }
    }

    [HarmonyPatch(typeof(HeroController), "IsFacingNearSlideableWall")]
    private static class IsFacingNearSlideableWallPatch
    {
        private static void Prefix(HeroController __instance, PlayerData ___playerData, out bool __state)
        {
            __state = ___playerData.hasWalljump;
            SaveState state = SaveState.Instance;
            if (state != null && state.IsRandomized(ItemType.Skill))
                ___playerData.hasWalljump = state.canWallJump && DirectionalAbilities.Cling(state, __instance.cState.facingRight);
        }

        private static void Postfix(PlayerData ___playerData, bool __state)
        {
            Restore(___playerData, __state);
        }
    }

    [HarmonyPatch(typeof(HeroController), "CanWallJump", typeof(bool))]
    private static class CanWallJumpPatch
    {
        private static void Prefix(HeroController __instance, PlayerData ___playerData, out bool __state)
        {
            Apply(__instance, ___playerData, out __state);
        }

        private static void Postfix(PlayerData ___playerData, bool __state)
        {
            Restore(___playerData, __state);
        }
    }

    [HarmonyPatch(typeof(HeroController), "CanWallScramble")]
    private static class CanWallScramblePatch
    {
        private static void Prefix(HeroController __instance, PlayerData ___playerData, out bool __state)
        {
            Apply(__instance, ___playerData, out __state);
        }

        private static void Postfix(PlayerData ___playerData, bool __state)
        {
            Restore(___playerData, __state);
        }
    }
}
