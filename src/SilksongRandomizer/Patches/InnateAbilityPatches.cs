using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SilksongRandomizer.Patches
{
    [HarmonyPatch(
        typeof(HeroController),
        nameof(HeroController.CheckClamberLedge)
    )]
    internal static class InnateAbilityLedgeGrabPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(
            ref float y,
            ref UnityEngine.Collider2D clamberedCollider,
            ref bool __result
        )
        {
            if (InnateAbilityManager.LedgeGrabEnabled)
            {
                return true;
            }

            y = 0f;
            clamberedCollider = null;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(HeroController), "Update")]
    internal static class InnateAbilityAirMantleUpdatePatch
    {
        private static readonly FieldInfo MantleFsmField =
            AccessTools.Field(
                typeof(HeroController),
                nameof(HeroController.mantleFSM)
            );

        private static readonly MethodInfo SendEventMethod =
            AccessTools.Method(
                typeof(PlayMakerFSM),
                nameof(PlayMakerFSM.SendEvent),
                new[] { typeof(string) }
            );

        private static readonly MethodInfo GatedSendMethod =
            AccessTools.Method(
                typeof(InnateAbilityAirMantleUpdatePatch),
                nameof(SendAirMantleIfEnabled)
            );

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions
        )
        {
            List<CodeInstruction> patched =
                new List<CodeInstruction>(instructions);
            int replacements = 0;

            for (int index = 2; index < patched.Count; index++)
            {
                if (patched[index - 2].opcode == OpCodes.Ldfld &&
                    Equals(patched[index - 2].operand, MantleFsmField) &&
                    patched[index - 1].opcode == OpCodes.Ldstr &&
                    Equals(patched[index - 1].operand, "AIR MANTLE") &&
                    patched[index].opcode == OpCodes.Callvirt &&
                    Equals(patched[index].operand, SendEventMethod))
                {
                    patched[index].opcode = OpCodes.Call;
                    patched[index].operand = GatedSendMethod;
                    replacements++;
                }
            }

            if (replacements != 1)
            {
                throw new InvalidOperationException(
                    "Expected exactly one AIR MANTLE send in " +
                    "HeroController.Update, found " + replacements + "."
                );
            }

            return patched;
        }

        private static void SendAirMantleIfEnabled(
            PlayMakerFSM mantleFsm,
            string eventName
        )
        {
            if (InnateAbilityManager.LedgeGrabEnabled)
            {
                mantleFsm.SendEvent(eventName);
            }
        }
    }

    [HarmonyPatch(
        typeof(PlayMakerFSM),
        nameof(PlayMakerFSM.SendEvent),
        new[] { typeof(string) }
    )]
    internal static class InnateAbilityAirMantleEventPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(
            PlayMakerFSM __instance,
            string eventName
        )
        {
            if (!string.Equals(
                    eventName,
                    "AIR MANTLE",
                    StringComparison.Ordinal
                ) ||
                InnateAbilityManager.LedgeGrabEnabled)
            {
                return true;
            }

            HeroController hero = HeroController.instance;
            return hero == null ||
                   !ReferenceEquals(__instance, hero.mantleFSM);
        }
    }

    [HarmonyPatch(
        typeof(SurfaceWaterRegion),
        "DoTriggerEnter",
        new[] { typeof(HeroController) }
    )]
    internal static class InnateAbilitySurfaceWaterEntryPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(HeroController hero)
        {
            if (InnateAbilityManager.SwimEnabled)
            {
                return true;
            }

            InnateAbilityManager.RespawnFromDisabledWater(hero);
            return false;
        }
    }

    [HarmonyPatch(
        typeof(HeroWaterController),
        nameof(HeroWaterController.EnterWaterRegion),
        new[] { typeof(SurfaceWaterRegion) }
    )]
    internal static class InnateAbilityWaterEntryPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(HeroWaterController __instance)
        {
            if (InnateAbilityManager.SwimEnabled)
            {
                return true;
            }

            HeroController hero =
                __instance.GetComponent<HeroController>();
            InnateAbilityManager.RespawnFromDisabledWater(hero);
            return false;
        }
    }
}
