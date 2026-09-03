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
        internal const string QuillItemName = "Item: Quill";
        internal const string ProgressiveCompassItemName =
            "Progressive Compass";

        internal static void GrantQuill()
        {
            SaveState state = SaveState.Instance;
            if (state == null)
            {
                throw new InvalidOperationException(
                    "Cannot grant Quill without an active randomizer save."
                );
            }

            state.canUseQuill = true;
            RefreshQuillMap();
        }

        internal static bool ReconcileReceivedQuill(
            SaveState state,
            PlayerData playerData,
            bool hasReceivedQuill
        )
        {
            if (state == null ||
                !hasReceivedQuill ||
                state.canUseQuill)
            {
                return false;
            }

            state.canUseQuill = true;
            RefreshQuillMap(playerData);
            return true;
        }

        private static void RefreshQuillMap(
            PlayerData knownPlayerData = null
        )
        {
            GameManager gameManager = GameManager.UnsafeInstance;
            PlayerData playerData = knownPlayerData ??
                (gameManager == null ? null : gameManager.playerData);
            if (playerData == null)
            {
                return;
            }

            // Keep the native flag false until Shakra's Quill is purchased.
            // That flag is also the source-location completion signal.
            playerData.mapUpdateQueued = true;
            playerData.HasSeenMapUpdated = false;
            CollectableItemManager.IncrementVersion();

            if (gameManager == null)
            {
                return;
            }

            try
            {
                gameManager.UpdateGameMap();
            }
            catch (Exception exception)
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Could not refresh the map after " +
                    "receiving Quill; a later map refresh will retry: " +
                    exception.Message
                );
            }
        }

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

        private static int GetQuillState(PlayerData playerData)
        {
            int nativeState = playerData == null
                ? 0
                : playerData.QuillState;
            SaveState state = SaveState.Instance;
            bool virtualizeState = state != null &&
                (state.startFullyMapped ||
                 (state.IsRandomized(ItemType.Skill) &&
                  state.canUseQuill));
            return ResolveQuillState(virtualizeState, nativeState);
        }

        private static int ResolveQuillState(
            bool virtualizeState,
            int nativeState
        )
        {
            return virtualizeState
                ? Math.Max(1, nativeState)
                : nativeState;
        }

        [HarmonyPatch(typeof(GameMap), "SetupMap", new[] { typeof(bool) })]
        private static class GameMap_SetupMap_Patch
        {
            private static SaveState refreshedState;
            private static GameMap refreshedMap;
            private static GameMap hiddenModeMap;

            [HarmonyPrefix]
            private static void Prefix(
                GameMap __instance,
                bool pinsOnly,
                ref int ___lastMappedCount
            )
            {
                if (CollectableItemManager.IsInHiddenMode())
                {
                    hiddenModeMap = __instance;
                }
                else if (!pinsOnly &&
                         ReferenceEquals(__instance, hiddenModeMap))
                {
                    hiddenModeMap = null;
                    ___lastMappedCount = int.MinValue;
                }

                SaveState state = SaveState.Instance;
                PlayerData playerData = PlayerData.instance;
                if (state == null ||
                    !state.startFullyMapped ||
                    playerData == null ||
                    !playerData.mapAllRooms ||
                    (ReferenceEquals(state, refreshedState) &&
                     ReferenceEquals(__instance, refreshedMap)))
                {
                    return;
                }

                refreshedState = state;
                refreshedMap = __instance;
                ___lastMappedCount = int.MinValue;
            }

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
                return ReplacePlayerDataQuillStateReads(
                    ReplacePlayerDataHasQuillReads(instructions)
                );
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

        private static IEnumerable<CodeInstruction>
            ReplacePlayerDataQuillStateReads(
                IEnumerable<CodeInstruction> instructions
            )
        {
            FieldInfo quillStateField = AccessTools.Field(
                typeof(PlayerData),
                "QuillState"
            );
            PropertyInfo quillStateProperty = typeof(PlayerData).GetProperty(
                "QuillState",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic
            );
            MethodInfo quillStateGetter =
                quillStateProperty?.GetGetMethod(true);
            MethodInfo replacement = AccessTools.Method(
                typeof(QuillPatches),
                nameof(GetQuillState),
                new[] { typeof(PlayerData) }
            );

            foreach (CodeInstruction instruction in instructions)
            {
                if (quillStateField != null &&
                    instruction.LoadsField(quillStateField))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                }
                else if (quillStateGetter != null &&
                         instruction.Calls(quillStateGetter))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                }

                yield return instruction;
            }
        }
    }
}
