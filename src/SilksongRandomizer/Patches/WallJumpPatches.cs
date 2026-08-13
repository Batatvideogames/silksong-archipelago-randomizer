using HarmonyLib;
using SilksongRandomizer;
using System;

internal static class HeroControllerWallJumpPatch
{
    private static void Apply(PlayerData playerData, out bool oldValue)
    {
        oldValue = playerData.hasWalljump;
        SaveState state = SaveState.Instance;
        if (state != null && state.IsRandomized(ItemType.Skill))
        {
            playerData.hasWalljump = state.canWallJump;
        }
    }

    private static void Restore(PlayerData playerData, bool oldValue)
    {
        playerData.hasWalljump = oldValue;
    }

    [HarmonyPatch(typeof(HeroController), "TryQueueWallJumpInterrupt")]
    private static class TryQueueWallJumpInterruptPatch
    {
        private static void Prefix(PlayerData ___playerData, out bool __state)
        {
            Apply(___playerData, out __state);
        }

        private static void Postfix(PlayerData ___playerData, bool __state)
        {
            Restore(___playerData, __state);
        }
    }

    [HarmonyPatch(typeof(HeroController), "IsFacingNearSlideableWall")]
    private static class IsFacingNearSlideableWallPatch
    {
        private static void Prefix(PlayerData ___playerData, out bool __state)
        {
            Apply(___playerData, out __state);
        }

        private static void Postfix(PlayerData ___playerData, bool __state)
        {
            Restore(___playerData, __state);
        }
    }

    [HarmonyPatch(typeof(HeroController), "CanWallJump", typeof(bool))]
    private static class CanWallJumpPatch
    {
        private static void Prefix(PlayerData ___playerData, out bool __state)
        {
            Apply(___playerData, out __state);
        }

        private static void Postfix(PlayerData ___playerData, bool __state)
        {
            Restore(___playerData, __state);
        }
    }

    [HarmonyPatch(typeof(HeroController), "CanWallScramble")]
    private static class CanWallScramblePatch
    {
        private static void Prefix(PlayerData ___playerData, out bool __state)
        {
            Apply(___playerData, out __state);
        }

        private static void Postfix(PlayerData ___playerData, bool __state)
        {
            Restore(___playerData, __state);
        }
    }
}
