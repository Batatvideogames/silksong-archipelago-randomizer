using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SilksongRandomizer.Patches
{
    /// <summary>
    /// Keeps Beastling Call unavailable during the Act 3 wake-up sequence
    /// without changing its durable Archipelago ownership.
    /// </summary>
    internal static class BeastlingCallAct3Safety
    {
        private static PlayerData suppressedPlayerData;
        private static SaveState suppressedState;
        private static bool nativeValueBeforeSuppression;

        internal static void Update()
        {
            Refresh();
        }

        internal static void Refresh()
        {
            try
            {
                RefreshCore();
            }
            catch (Exception ex)
            {
                RestoreTrackedValue();
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Beastling Call Act 3 safety could not " +
                    "refresh: " + ex.Message
                );
            }
        }

        internal static void Reset()
        {
            RestoreTrackedValue();
        }

        internal static void PreserveSerializedOwnership(
            PlayerData playerData,
            ref string serializedSave
        )
        {
            if (!ShouldPersistOwnership(playerData) ||
                string.IsNullOrEmpty(serializedSave))
            {
                return;
            }

            const string unavailable =
                "\"UnlockedFastTravelTeleport\":false";
            const string available =
                "\"UnlockedFastTravelTeleport\":true";
            if (serializedSave.IndexOf(
                    unavailable,
                    StringComparison.Ordinal
                ) >= 0)
            {
                serializedSave = serializedSave.Replace(
                    unavailable,
                    available
                );
            }
        }

        private static void RefreshCore()
        {
            SaveState state = SaveState.Instance;
            PlayerData playerData = PlayerData.instance;
            bool shouldSuppress =
                state?.IsRoomBound == true &&
                state.canUseBeastlingCall &&
                playerData != null &&
                FastTravelUtil.IsActThreeWakeSequenceUnsafe();

            if (!shouldSuppress)
            {
                RestoreTrackedValue();
                return;
            }

            if (suppressedPlayerData != null &&
                (
                    !ReferenceEquals(suppressedPlayerData, playerData) ||
                    !ReferenceEquals(suppressedState, state)
                ))
            {
                RestoreTrackedValue();
            }

            if (suppressedPlayerData == null)
            {
                suppressedPlayerData = playerData;
                suppressedState = state;
                nativeValueBeforeSuppression =
                    playerData.UnlockedFastTravelTeleport;
            }

            playerData.UnlockedFastTravelTeleport = false;
        }

        private static void RestoreTrackedValue()
        {
            PlayerData playerData = suppressedPlayerData;
            if (playerData == null)
            {
                suppressedState = null;
                nativeValueBeforeSuppression = false;
                return;
            }

            SaveState currentState = SaveState.Instance;
            bool restoredValue = nativeValueBeforeSuppression;
            if (ReferenceEquals(currentState, suppressedState) &&
                currentState != null)
            {
                restoredValue = currentState.canUseBeastlingCall;
            }
            else if (ReferenceEquals(PlayerData.instance, playerData) &&
                     IsManagedSource(currentState))
            {
                restoredValue = currentState.canUseBeastlingCall;
            }

            playerData.UnlockedFastTravelTeleport = restoredValue;
            suppressedPlayerData = null;
            suppressedState = null;
            nativeValueBeforeSuppression = false;
        }

        private static bool ShouldPersistOwnership(PlayerData playerData)
        {
            if (playerData == null)
            {
                return false;
            }

            if (ReferenceEquals(playerData, suppressedPlayerData) &&
                suppressedState?.canUseBeastlingCall == true)
            {
                return true;
            }

            SaveState state = SaveState.Instance;
            return ReferenceEquals(playerData, PlayerData.instance) &&
                   state?.IsRoomBound == true &&
                   state.canUseBeastlingCall;
        }

        private static bool IsManagedSource(SaveState state)
        {
            return state?.IsRoomBound == true &&
                   state.IsRandomized(ItemType.Melody) &&
                   state.IsLocationInSeed(
                       MelodyLocationManifest.BeastlingCall
                   );
        }
    }

    [HarmonyPatch(
        typeof(GameManager),
        "StartBlackThreadWorld",
        new Type[] { }
    )]
    internal static class BeastlingCallAct3StartPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            BeastlingCallAct3Safety.Refresh();
        }
    }

    [HarmonyPatch]
    internal static class BeastlingCallSaveSerializationPatch
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            MethodInfo generic = AccessTools.Method(
                typeof(SaveDataUtility),
                "SerializeSaveData"
            ) as MethodInfo;
            if (generic == null || !generic.IsGenericMethodDefinition)
            {
                yield break;
            }

            yield return generic.MakeGenericMethod(typeof(SaveGameData));
            yield return generic.MakeGenericMethod(typeof(RestorePointData));
        }

        [HarmonyPostfix]
        private static void Postfix(
            object saveData,
            ref string __result
        )
        {
            PlayerData playerData = null;
            if (saveData is SaveGameData gameData)
            {
                playerData = gameData.playerData;
            }
            else if (saveData is RestorePointData restorePoint)
            {
                playerData = restorePoint.saveGameData?.playerData;
            }

            BeastlingCallAct3Safety.PreserveSerializedOwnership(
                playerData,
                ref __result
            );
        }
    }
}
