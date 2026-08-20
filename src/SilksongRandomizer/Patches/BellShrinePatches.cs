using HarmonyLib;
using System;
using System.Reflection;

namespace SilksongRandomizer.Patches
{
    internal static class BellShrinePatches
    {
        private sealed class Entry
        {
            internal readonly string SceneName;
            internal readonly string ObjectName;
            internal readonly string PlayerDataFlag;
            internal readonly string LocationName;

            internal Entry(
                string sceneName,
                string objectName,
                string playerDataFlag,
                string locationName)
            {
                SceneName = sceneName;
                ObjectName = objectName;
                PlayerDataFlag = playerDataFlag;
                LocationName = locationName;
            }
        }

        private sealed class JudgeBell
        {
            internal readonly string PlayerDataFlag;
            internal readonly string ItemName;

            internal JudgeBell(string playerDataFlag, string itemName)
            {
                PlayerDataFlag = playerDataFlag;
                ItemName = itemName;
            }
        }

        private struct JudgeStateSnapshot
        {
            internal bool Applied;
            internal PlayerData PlayerData;
            internal bool[] NativeValues;
        }

        private static readonly Entry[] Entries =
        {
            new Entry(
                "Bellshrine",
                "Bellshrine Sequence",
                "bellShrineBoneForest",
                "The Marrow - Bellshrine"),
            new Entry(
                "Bellshrine_05",
                "Bellshrine Sequence",
                "bellShrineWilds",
                "Deep Docks - Bellshrine"),
            new Entry(
                "Bellshrine_02",
                "Bellshrine Sequence",
                "bellShrineGreymoor",
                "Greymoor - Bellshrine"),
            new Entry(
                "Bellshrine_03",
                "Bellshrine Sequence",
                "bellShrineShellwood",
                "Shellwood - Bellshrine"),
            new Entry(
                "Belltown_Shrine",
                "Bellshrine Sequence Bellhart",
                "bellShrineBellhart",
                "Bellhart - Bellshrine"),
        };

        // The Judge door checks these five shrine flags. Swap in AP bell
        // ownership only while that door checks or animates its locks.
        private static readonly JudgeBell[] JudgeBells =
        {
            new JudgeBell("bellShrineBoneForest", "Bell: The Marrow"),
            new JudgeBell("bellShrineWilds", "Bell: Deep Docks"),
            new JudgeBell("bellShrineGreymoor", "Bell: Greymoor"),
            new JudgeBell("bellShrineShellwood", "Bell: Shellwood"),
            new JudgeBell("bellShrineBellhart", "Bell: Bellhart"),
        };

        private static readonly FieldInfo IsCompleteBoolField =
            AccessTools.Field(typeof(StateChangeSequence), "isCompleteBool");

        [HarmonyPatch(typeof(StateChangeSequence), "SetIsCompleteBool")]
        private static class SetIsCompleteBoolPatch
        {
            [HarmonyPostfix]
            private static void Postfix(StateChangeSequence __instance)
            {
                ReportPhysicalCompletion(__instance);
            }
        }

        [HarmonyPatch(typeof(StateChangeSequence), "CheckCompleteBool")]
        private static class CheckCompleteBoolPatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                StateChangeSequence __instance,
                bool __result)
            {
                if (__result)
                {
                    // An older save may have a completed shrine whose AP check
                    // was never sent.
                    ReportPhysicalCompletion(__instance);
                }
            }
        }

        [HarmonyPatch]
        private static class JudgeUpdateActivationPatch
        {
            private static MethodBase TargetMethod()
            {
                return AccessTools.Method(
                    typeof(BellShrineGateLock),
                    "UpdateActivation"
                );
            }

            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(out JudgeStateSnapshot __state)
            {
                ApplyJudgeBellOwnership(out __state);
            }

            [HarmonyFinalizer]
            [HarmonyPriority(Priority.Last)]
            private static Exception Finalizer(
                Exception __exception,
                JudgeStateSnapshot __state)
            {
                RestoreJudgeBellOwnership(__state);
                return __exception;
            }
        }

        [HarmonyPatch(typeof(BellShrineGateLock), "GetIsAllUnlocked")]
        private static class JudgeGetIsAllUnlockedPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(out JudgeStateSnapshot __state)
            {
                ApplyJudgeBellOwnership(out __state);
            }

            [HarmonyFinalizer]
            [HarmonyPriority(Priority.Last)]
            private static Exception Finalizer(
                Exception __exception,
                JudgeStateSnapshot __state)
            {
                RestoreJudgeBellOwnership(__state);
                return __exception;
            }
        }

        [HarmonyPatch]
        private static class JudgePlayRoutineMoveNextPatch
        {
            private static MethodBase TargetMethod()
            {
                MethodInfo playRoutine = AccessTools.Method(
                    typeof(BellShrineGateLock),
                    "PlayRoutine"
                );
                return AccessTools.EnumeratorMoveNext(playRoutine);
            }

            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(out JudgeStateSnapshot __state)
            {
                ApplyJudgeBellOwnership(out __state);
            }

            [HarmonyFinalizer]
            [HarmonyPriority(Priority.Last)]
            private static Exception Finalizer(
                Exception __exception,
                JudgeStateSnapshot __state)
            {
                RestoreJudgeBellOwnership(__state);
                return __exception;
            }
        }

        private static void ReportPhysicalCompletion(
            StateChangeSequence sequence)
        {
            SaveState state = SaveState.Instance;
            if (state == null ||
                !TryGetEntry(sequence, out Entry entry) ||
                !IsActive(state, entry.LocationName))
            {
                return;
            }

            state.CheckLocation(entry.LocationName);
        }

        private static void ApplyJudgeBellOwnership(
            out JudgeStateSnapshot snapshot)
        {
            snapshot = default;
            SaveState state = SaveState.Instance;
            PlayerData playerData = PlayerData.instance;
            if (state == null ||
                playerData == null ||
                !state.IsRandomized(ItemType.BellShrine))
            {
                return;
            }

            bool[] nativeValues = new bool[JudgeBells.Length];
            for (int index = 0; index < JudgeBells.Length; index++)
            {
                nativeValues[index] = playerData.GetBool(
                    JudgeBells[index].PlayerDataFlag
                );
            }

            snapshot = new JudgeStateSnapshot
            {
                Applied = true,
                PlayerData = playerData,
                NativeValues = nativeValues,
            };

            try
            {
                for (int index = 0; index < JudgeBells.Length; index++)
                {
                    JudgeBell bell = JudgeBells[index];
                    bool received = state.receivedItems != null &&
                        state.receivedItems.Contains(bell.ItemName);
                    playerData.SetBool(bell.PlayerDataFlag, received);
                }
            }
            catch
            {
                RestoreJudgeBellOwnership(snapshot);
                snapshot = default;
                throw;
            }
        }

        private static void RestoreJudgeBellOwnership(
            JudgeStateSnapshot snapshot)
        {
            if (!snapshot.Applied ||
                snapshot.PlayerData == null ||
                snapshot.NativeValues == null)
            {
                return;
            }

            for (int index = 0; index < JudgeBells.Length; index++)
            {
                snapshot.PlayerData.SetBool(
                    JudgeBells[index].PlayerDataFlag,
                    snapshot.NativeValues[index]
                );
            }
        }

        private static bool IsActive(
            SaveState state,
            string locationName)
        {
            return state != null &&
                   state.IsRandomized(ItemType.BellShrine) &&
                   state.IsLocationEnabled(locationName) &&
                   state.IsLocationInSeed(locationName);
        }

        private static bool TryGetEntry(
            StateChangeSequence sequence,
            out Entry entry)
        {
            entry = null;
            if (sequence == null ||
                sequence.gameObject == null ||
                IsCompleteBoolField == null)
            {
                return false;
            }

            string sceneName = sequence.gameObject.scene.name;
            string playerDataFlag =
                IsCompleteBoolField.GetValue(sequence) as string;
            foreach (Entry candidate in Entries)
            {
                if (string.Equals(
                        sceneName,
                        candidate.SceneName,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        sequence.name,
                        candidate.ObjectName,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        playerDataFlag,
                        candidate.PlayerDataFlag,
                        StringComparison.Ordinal))
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
