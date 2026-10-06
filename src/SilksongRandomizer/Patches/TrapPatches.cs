using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SilksongRandomizer.Patches
{
    [HarmonyPatch(
        typeof(DarknessRegion),
        nameof(DarknessRegion.SetDarknessLevel),
        new Type[] { typeof(int) }
    )]
    internal static class DarknessTrapNativeLevelPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ref int __0)
        {
            TrapManager.ObserveNativeDarknessRequest(ref __0);
        }
    }

    [HarmonyPatch(
        typeof(HeroController),
        nameof(HeroController.SetIsMaggoted),
        new Type[] { typeof(bool) }
    )]
    internal static class MuckmaggotTrapNativeStatusPatch
    {
        [HarmonyPrefix]
        private static void Prefix(bool __0)
        {
            TrapManager.ObserveNativeMuckmaggotRequest(__0);
        }
    }

    [HarmonyPatch]
    internal static class RestoreTemporaryTrapBeforeSavePatch
    {
        [ThreadStatic]
        private static int saveDepth;

        internal static bool IsPreparingSave => saveDepth > 0;

        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(
                typeof(GameManager), nameof(GameManager.CreateSaveGameData),
                new[] { typeof(int) });
            yield return AccessTools.Method(
                typeof(GameManager), nameof(GameManager.GetSaveGameData),
                new[] { typeof(int) });
            yield return AccessTools.Method(
                typeof(GameManager), "SaveGame",
                new[]
                {
                    typeof(int), typeof(Action<bool>), typeof(bool),
                    typeof(AutoSaveName)
                });
        }

        private static void Prefix(out bool __state)
        {
            __state = true;
            if (saveDepth++ != 0) return;

            SlabCaptureWarpSafety.PrepareForSave();
            TrapManager.PrepareForSave();
            BellhomePhaseManager.EnsureBellhomeUnlocked();
        }

        private static void Finalizer(bool __state)
        {
            if (__state && --saveDepth == 0)
            {
                TrapManager.ResumeAfterSave();
            }
        }
    }

    [HarmonyPatch(typeof(SaveGameData), MethodType.Constructor,
        new[] { typeof(PlayerData), typeof(SceneData) })]
    internal static class TemporaryTrapSaveSnapshotPatch
    {
        private static readonly MethodInfo ClonePlayerData =
            AccessTools.Method(typeof(object), "MemberwiseClone");

        private static void Postfix(SaveGameData __instance)
        {
            if (RestoreTemporaryTrapBeforeSavePatch.IsPreparingSave &&
                __instance.playerData != null &&
                (TrapManager.HasCursedCrestSaveSnapshot || NakedTrapManager.HasState))
            {
                // The save queue can serialize this after the trap resumes.
                __instance.playerData =
                    (PlayerData)ClonePlayerData.Invoke(__instance.playerData, null);
            }
        }
    }
}
