using GlobalEnums;
using System;

namespace SilksongRandomizer
{
    /// <summary>
    /// Preserves only Archipelago-owned mutations across the game's native
    /// memory snapshot. Vanilla restores ordinary memory-scene
    /// health, Silk, Rosaries and Shell Shards on exit. Callers must therefore
    /// use these helpers only for received items or linked values.
    /// </summary>
    internal static class MemorySequenceSync
    {
        internal static bool HasRecordedSnapshot(PlayerData playerData)
        {
            return playerData != null &&
                   playerData.HasStoredMemoryState &&
                   playerData.PreMemoryState.IsRecorded;
        }

        internal static bool CanApplyDurableReceipt(
            PlayerData playerData,
            HeroController hero,
            Item item)
        {
            if (!HasRecordedSnapshot(playerData) ||
                hero == null ||
                item == null ||
                item.Type == ItemType.Trap ||
                item.Type == ItemType.Crest)
            {
                return false;
            }

            GameManager gameManager = GameManager.SilentInstance;
            return gameManager != null &&
                   gameManager.IsGameplayScene() &&
                   !gameManager.isPaused;
        }

        internal static bool TryCaptureCurrency(
            PlayerData playerData,
            CurrencyType currencyType,
            out int snapshotValue)
        {
            snapshotValue = 0;
            if (!HasRecordedSnapshot(playerData) ||
                !IsSupportedCurrency(currencyType))
            {
                return false;
            }

            HeroItemsState snapshot = playerData.PreMemoryState;
            snapshotValue = currencyType == CurrencyType.Money
                ? snapshot.Rosaries
                : snapshot.ShellShards;
            return true;
        }

        internal static int GetPersistentCurrency(
            PlayerData playerData,
            CurrencyType currencyType,
            int liveValue)
        {
            return TryCaptureCurrency(
                playerData,
                currencyType,
                out int snapshotValue)
                    ? snapshotValue
                    : liveValue;
        }

        internal static void RebaseCurrencyDelta(
            PlayerData playerData,
            CurrencyType currencyType,
            int snapshotBefore,
            int liveBefore,
            int liveAfter)
        {
            if (!HasRecordedSnapshot(playerData) ||
                !IsSupportedCurrency(currencyType))
            {
                return;
            }

            int maximum = GetCurrencyMaximum(currencyType);
            int rebased = RebaseValue(
                snapshotBefore,
                liveBefore,
                liveAfter,
                maximum
            );
            MirrorCurrency(playerData, currencyType, rebased);
        }

        internal static void MirrorCurrency(
            PlayerData playerData,
            CurrencyType currencyType,
            int value)
        {
            if (!HasRecordedSnapshot(playerData) ||
                !IsSupportedCurrency(currencyType))
            {
                return;
            }

            value = Clamp(value, 0, GetCurrencyMaximum(currencyType));
            HeroItemsState snapshot = playerData.PreMemoryState;
            if (currencyType == CurrencyType.Money)
            {
                snapshot.Rosaries = value;
            }
            else
            {
                snapshot.ShellShards = value;
            }

            playerData.PreMemoryState = snapshot;
        }

        internal static bool TryCaptureSilk(
            PlayerData playerData,
            out int snapshotValue)
        {
            snapshotValue = 0;
            if (!HasRecordedSnapshot(playerData))
            {
                return false;
            }

            snapshotValue = playerData.PreMemoryState.Silk;
            return true;
        }

        internal static int GetPersistentSilk(
            PlayerData playerData,
            int liveValue)
        {
            return TryCaptureSilk(playerData, out int snapshotValue)
                ? snapshotValue
                : liveValue;
        }

        internal static void RebaseSilkDelta(
            PlayerData playerData,
            int snapshotBefore,
            int liveBefore,
            int liveAfter)
        {
            if (!HasRecordedSnapshot(playerData))
            {
                return;
            }

            int maximum;
            try
            {
                maximum = Math.Max(0, playerData.CurrentSilkMax);
            }
            catch
            {
                maximum = Math.Max(0, playerData.silkMax);
            }

            MirrorSilk(
                playerData,
                RebaseValue(
                    snapshotBefore,
                    liveBefore,
                    liveAfter,
                    maximum
                )
            );
        }

        internal static void MirrorSilk(
            PlayerData playerData,
            int value)
        {
            if (!HasRecordedSnapshot(playerData))
            {
                return;
            }

            int maximum;
            try
            {
                maximum = Math.Max(0, playerData.CurrentSilkMax);
            }
            catch
            {
                maximum = Math.Max(0, playerData.silkMax);
            }

            HeroItemsState snapshot = playerData.PreMemoryState;
            snapshot.Silk = Clamp(value, 0, maximum);
            playerData.PreMemoryState = snapshot;
        }

        internal static void SynchronizeMaskHealth(
            PlayerData playerData,
            int desiredMaxHealth,
            bool refillNewMask)
        {
            if (!HasRecordedSnapshot(playerData))
            {
                return;
            }

            desiredMaxHealth = Math.Max(0, desiredMaxHealth);
            HeroItemsState snapshot = playerData.PreMemoryState;
            snapshot.Health = refillNewMask
                ? desiredMaxHealth
                : Clamp(snapshot.Health, 0, desiredMaxHealth);
            playerData.PreMemoryState = snapshot;
        }

        internal static int RebaseValue(
            int snapshotBefore,
            int liveBefore,
            int liveAfter,
            int maximum)
        {
            long rebased =
                (long)snapshotBefore + ((long)liveAfter - liveBefore);
            return (int)Math.Min(
                Math.Max(0, maximum),
                Math.Max(0L, rebased)
            );
        }

        private static int GetCurrencyMaximum(CurrencyType currencyType)
        {
            try
            {
                return Math.Max(
                    0,
                    GlobalSettings.Gameplay.GetCurrencyCap(currencyType)
                );
            }
            catch
            {
                return int.MaxValue;
            }
        }

        private static bool IsSupportedCurrency(CurrencyType currencyType)
        {
            return currencyType == CurrencyType.Money ||
                   currencyType == CurrencyType.Shard;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Min(Math.Max(value, minimum), maximum);
        }
    }
}
