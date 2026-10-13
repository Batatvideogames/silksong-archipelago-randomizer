using BepInEx.Bootstrap;
using GlobalEnums;
using HarmonyLib;
using SilksongRandomizer.Patches;
using System;
using System.Reflection;

namespace SilksongRandomizer
{
    internal static class OptionalModCompatibility
    {
        internal const string QoLId = "io.github.vitaxses.qol";
        internal const string FreeCompassId = "x753.FreeCompass";
        internal const string BenchwarpId = "io.github.homothetyhk.benchwarp";

        private static ConstructorInfo benchwarpRespawnConstructor;
        private static object boneBottomStart;
        private static bool initialized;

        private static bool IsApSave => SaveState.Instance?.IsRoomBound == true;

        internal static void Initialize(Harmony harmony)
        {
            if (initialized) return;
            initialized = true;
            Install(QoLId, assembly => InstallQoL(harmony, assembly));
            Install(FreeCompassId, assembly => Patch(harmony, assembly,
                "FreeCompass.FreeCompassMod+ShopItem_Cost_Patch", "ShopItem_Cost_Prefix",
                nameof(AllowFreeCompassPrice)));
            Install(BenchwarpId, assembly => InstallBenchwarp(harmony, assembly));
        }

        private static void Install(string id, Action<Assembly> install)
        {
            if (!Chainloader.PluginInfos.TryGetValue(id, out var plugin) || plugin.Instance == null)
                return;

            try
            {
                install(plugin.Instance.GetType().Assembly);
                RandomizerPlugin.Log?.LogInfo("[RANDOMIZER] Compatibility enabled for " + id + ".");
            }
            catch (Exception ex)
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Compatibility setup failed for " + id + ": " + ex);
            }
        }

        private static MethodInfo RequireMethod(Assembly assembly, string type, string method)
        {
            return AccessTools.DeclaredMethod(assembly.GetType(type, true), method)
                ?? throw new MissingMethodException(type, method);
        }

        private static void Patch(Harmony harmony, Assembly assembly, string type, string method, string prefix)
        {
            harmony.Patch(RequireMethod(assembly, type, method),
                prefix: new HarmonyMethod(typeof(OptionalModCompatibility), prefix));
        }

        private static void InstallQoL(Harmony harmony, Assembly assembly)
        {
            Patch(harmony, assembly, "QoL.Patches.Cutscene.InputHandlerPatch", "Prefix_SetSkipMode",
                nameof(AllowQoLOpeningSkip));
            Patch(harmony, assembly, "QoL.Patches.Cutscene.SkippableSequencePatch", "Prefix_CanSkip",
                nameof(AllowQoLOpeningSkip));
            Patch(harmony, assembly, "QoL.FSMEdits.SkipWeakness", "SkipWeaknessPatch",
                nameof(AllowQoLRewardEdits));
            foreach (string method in new[] { "Widow", "FirstSinner", "SilkHeart" })
                Patch(harmony, assembly, "QoL.FSMEdits.DreamCutscene", method,
                    nameof(AllowQoLRewardEdits));
        }

        private static bool AllowQoLOpeningSkip(ref bool __result)
        {
            if (!IsApSave || GameManager.SilentInstance?.sceneName != "Opening_Sequence")
                return true;

            __result = true;
            return false;
        }

        private static bool AllowQoLRewardEdits()
        {
            return !IsApSave;
        }

        private static bool AllowFreeCompassPrice(ShopItem __0, ref bool __result)
        {
            if (!IsApSave || __0 == null || __0.name != "Mapper Compass Tool" ||
                !__0.IsAvailableNotInfinite ||
                !SaveState.Instance.TryGetPurchasePrice("shop:" + __0.name, out _))
                return true;

            __result = true;
            return false;
        }

        private static void InstallBenchwarp(Harmony harmony, Assembly assembly)
        {
            Type respawn = assembly.GetType("Benchwarp.Benches.RespawnInfo", true);
            benchwarpRespawnConstructor = respawn.GetConstructor(
                new[] { typeof(string), typeof(string), typeof(int), typeof(MapZone) })
                ?? throw new MissingMethodException(respawn.FullName, ".ctor");

            Patch(harmony, assembly, "Benchwarp.ChangeScene", "WarpToRespawn", nameof(PrepareBenchwarp));
            Patch(harmony, assembly, "Benchwarp.ChangeScene", "WarpToDoor", nameof(PrepareBenchwarp));

            Type modifiers = assembly.GetType("Benchwarp.Events.BenchListModifiers", true);
            object startEvent = modifiers.GetProperty("OnGetStartDef")?.GetValue(null)
                ?? throw new MissingMemberException(modifiers.FullName, "OnGetStartDef");
            Type handlerType = typeof(Func<,>).MakeGenericType(respawn, respawn);
            MethodInfo handler = AccessTools.Method(typeof(OptionalModCompatibility), nameof(GetBenchwarpStart))
                .MakeGenericMethod(respawn);
            startEvent.GetType().GetMethod("Add", new[] { handlerType }).Invoke(startEvent,
                new object[] { Delegate.CreateDelegate(handlerType, handler) });
        }

        private static T GetBenchwarpStart<T>(T original) where T : class
        {
            if (!StartingLocationManager.TryGetAlternateStartRespawn(
                    out string scene, out string marker, out int type, out MapZone zone))
                return original;

            if (boneBottomStart == null)
                boneBottomStart = benchwarpRespawnConstructor.Invoke(new object[] { scene, marker, type, zone });
            return (T)boneBottomStart;
        }

        private static bool PrepareBenchwarp()
        {
            if (!IsApSave) return true;
            if (!SlabCaptureWarpSafety.TryRestoreBeforeRecoveryWarp(out string error))
            {
                RandomizerPlugin.Log?.LogWarning("[RANDOMIZER] Benchwarp cancelled: " + error);
                return false;
            }

            EntranceRandomization.PrepareForExternalWarp();
            return true;
        }
    }
}
