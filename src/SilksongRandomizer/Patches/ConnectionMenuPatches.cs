using HarmonyLib;

namespace SilksongRandomizer.Patches
{
    [HarmonyPatch(typeof(InputHandler), "SetCursorEnabled")]
    internal static class ConnectionMenuCursorPatch
    {
        private static bool Prefix()
        {
            if (!RandomizerPlugin.IsConnectionGuiOpen) return true;
            RandomizerPlugin.Instance.UnlockCursorForConnectionGui();
            return false;
        }
    }
}
