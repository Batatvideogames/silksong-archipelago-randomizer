using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;

namespace SilksongRandomizer.Patches
{
    internal static class QuillPatches
    {
        private static bool CanUseQuill()
        {
            SaveState state = SaveState.Instance;
            if (state != null && state.startFullyMapped)
            {
                return true;
            }

            if (state == null || !state.IsRandomized(ItemType.Skill))
            {
                return PlayerData.instance != null &&
                       PlayerData.instance.hasQuill;
            }

            return state.canUseQuill;
        }

        private static bool CanUseQuill(PlayerData playerData)
        {
            SaveState state = SaveState.Instance;
            if (state != null && state.startFullyMapped)
            {
                return true;
            }

            return state == null || !state.IsRandomized(ItemType.Skill)
                ? playerData != null && playerData.hasQuill
                : state.canUseQuill;
        }

        [HarmonyPatch(typeof(GameMap), "SetupMap", new[] { typeof(bool) })]
        private static class GameMap_SetupMap_Patch
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return ReplacePlayerDataHasQuillReads(instructions);
            }
        }

        [HarmonyPatch(typeof(PlayerData), "get_CanUpdateMap")]
        private static class PlayerData_CanUpdateMap_Patch
        {
            private static bool Prefix(PlayerData __instance, ref bool __result)
            {
                SaveState state = SaveState.Instance;
                if (state != null && state.startFullyMapped)
                {
                    __result = true;
                    return false;
                }

                if (state == null || !state.IsRandomized(ItemType.Skill))
                {
                    return true;
                }

                __result = CanUseQuill() && __instance.HasAnyMap;
                return false;
            }
        }

        [HarmonyPatch(typeof(AddScenesMapped), "OnEnter")]
        private static class AddScenesMapped_OnEnter_Patch
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return ReplacePlayerDataHasQuillReads(instructions);
            }
        }

        [HarmonyPatch(typeof(SetMappedOnStart), "Start")]
        private static class SetMappedOnStart_Start_Patch
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return ReplacePlayerDataHasQuillReads(instructions);
            }
        }

        private static IEnumerable<CodeInstruction> ReplacePlayerDataHasQuillReads(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo hasQuillField = AccessTools.Field(typeof(PlayerData), "hasQuill");
            PropertyInfo hasQuillProperty = typeof(PlayerData).GetProperty(
                "hasQuill",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );
            MethodInfo hasQuillGetter = hasQuillProperty?.GetGetMethod(true);
            MethodInfo replacement = AccessTools.Method(
                typeof(QuillPatches),
                nameof(CanUseQuill),
                new[] { typeof(PlayerData) }
            );

            foreach (CodeInstruction instruction in instructions)
            {
                if (hasQuillField != null && instruction.LoadsField(hasQuillField))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                }
                else if (hasQuillGetter != null && instruction.Calls(hasQuillGetter))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                }

                yield return instruction;
            }
        }
    }
}
