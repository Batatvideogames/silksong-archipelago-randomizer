using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using TeamCherry.GameCore;
using UnityEngine;
using static UnityEngine.UI.SaveSlotButton;

namespace SilksongRandomizer.Patches
{
    internal static class SavePatches
    {
        public const string VanillaExtension = ".dat";
        public const string SaveExtension = ".randomizersave";
        private const string DataExtension = ".randomizerdata";
        private const string DataBackupExtension = ".bak";
        private const string DataTemporaryExtension = ".tmp";
        private static readonly object SaveLock = new object();

        internal static void NewGame()
        {
            SaveState saveState = new SaveState();
            SaveState.Instance = saveState;
            NewRandomizerIntroSkipPatch.ArmForNewRandomizerSave();
            RandomizerPlugin.Instance.ClearPendingGameplayQueues();
            if (Archipelago.Instance != null && Archipelago.Instance.Connected)
            {
                Archipelago.Instance.ResetReceivedItemQueueCursor();
                saveState.BindToRoom(Archipelago.Instance);
                Archipelago.Instance.Resynchronize();
            }
            BellhomePhaseManager.EnsureBellhomeUnlocked();

            // Crests relock only when that category is randomized. The vanilla
            // Hunter crest stays equipped until the AP-precollected starting
            // crest is safe to apply. Cloakless removes Hornet's needle and
            // softlocks the tutorial room.
            if (saveState.IsRandomized(ItemType.Crest))
            {
                foreach (ToolCrest crest in Resources.FindObjectsOfTypeAll<ToolCrest>())
                {
                    var save = crest.SaveData;
                    save.IsUnlocked = false;
                    crest.SaveData = save;
                }
            }
            RandomizerPlugin.Instance.StartCoroutine(
                StartingCrestFix.EnsureUsableStartingCrest()
            );
            StartingLocationManager.ScheduleIfNeeded();
        }

        internal static bool Load(int slot, out string error)
        {
            error = string.Empty;
            if (!TryLoadState(slot, out SaveState loadedState, out error))
            {
                return false;
            }

            if (loadedState.schemaVersion > SaveState.CurrentSchemaVersion)
            {
                error = "This save uses newer randomizer metadata (schema " +
                        loadedState.schemaVersion + ") than this plugin supports (" +
                        SaveState.CurrentSchemaVersion + ").";
                return false;
            }

            loadedState.InitializeAfterLoad();
            if (!loadedState.IsRoomBound)
            {
                error = "This save has no room binding and cannot be matched safely to an " +
                        "Archipelago seed. Start a new randomizer save.";
                return false;
            }

            if (!loadedState.HasWorldVersionBinding)
            {
                error = GetWorldVersionBindingError(loadedState);
                return false;
            }

            if (!loadedState.HasGoalBinding)
            {
                error = "This save has no goal binding and cannot determine its completion " +
                        "condition safely. Start a new randomizer save.";
                return false;
            }

            if (!loadedState.HasStartingCrestBinding)
            {
                error = "This save has no starting-crest binding and cannot determine " +
                        "its initial crest safely. Start a new randomizer save.";
                return false;
            }

            if (!loadedState.HasStartingLocationBinding)
            {
                error = "This save has no starting-location binding and cannot " +
                        "determine its initial area safely. Start a new randomizer save.";
                return false;
            }

            if (Archipelago.Instance != null && Archipelago.Instance.Connected)
            {
                if (!Archipelago.Instance.ValidateLoadedSave(loadedState, out error))
                {
                    return false;
                }

                SaveState.Instance = loadedState;
                RandomizerPlugin.Instance.ClearPendingGameplayQueues();
                Archipelago.Instance.ResetReceivedItemQueueCursor();
                Archipelago.Instance.Resynchronize();
            }
            else
            {
                SaveState.Instance = loadedState;
                RandomizerPlugin.Instance.ClearPendingGameplayQueues();
            }

            // The native spool progress fields mirror the loaded AP inventory.
            // An existing persistent HUD is redrawn afterward.
            // If it does not exist yet, its first DrawSpool call performs the
            // same synchronization through SpoolFragmentPatches.
            SpoolFragmentPatches.RefreshReceivedSpoolHud();

            RandomizerPlugin.Instance.StartCoroutine(
                StartingCrestFix.EnsureUsableStartingCrest()
            );
            RandomizerPlugin.Instance.StartCoroutine(
                ConsumableToolPatches.SynchronizeReceivedConsumables(
                    loadedState
                )
            );
            StartingLocationManager.ScheduleIfNeeded();
            BellhomePhaseManager.EnsureBellhomeUnlocked();
            Debug.Log("[Randomizer Save] SaveState loaded: " + GetDataPath(slot));
            return true;
        }

        internal static bool CanLoad(int slot, out string error)
        {
            if (!TryLoadState(slot, out SaveState loadedState, out error))
            {
                return false;
            }

            if (loadedState.schemaVersion > SaveState.CurrentSchemaVersion)
            {
                error = "This save uses newer randomizer metadata (schema " +
                        loadedState.schemaVersion + ") than this plugin supports (" +
                        SaveState.CurrentSchemaVersion + ").";
                return false;
            }

            loadedState.InitializeAfterLoad();
            if (!loadedState.IsRoomBound)
            {
                error = "This save has no room binding and cannot be matched safely to an " +
                        "Archipelago seed. Start a new randomizer save.";
                return false;
            }

            if (!loadedState.HasWorldVersionBinding)
            {
                error = GetWorldVersionBindingError(loadedState);
                return false;
            }

            if (!loadedState.HasGoalBinding)
            {
                error = "This save has no goal binding and cannot determine its completion " +
                        "condition safely. Start a new randomizer save.";
                return false;
            }

            if (!loadedState.HasStartingCrestBinding)
            {
                error = "This save has no starting-crest binding and cannot determine " +
                        "its initial crest safely. Start a new randomizer save.";
                return false;
            }

            if (!loadedState.HasStartingLocationBinding)
            {
                error = "This save has no starting-location binding and cannot " +
                        "determine its initial area safely. Start a new randomizer save.";
                return false;
            }

            if (Archipelago.Instance != null && Archipelago.Instance.Connected &&
                !Archipelago.Instance.ValidateLoadedSave(loadedState, out error))
            {
                return false;
            }

            return true;
        }

        private static string GetWorldVersionBindingError(
            SaveState loadedState
        )
        {
            string savedVersion = loadedState == null ||
                                  string.IsNullOrWhiteSpace(
                                      loadedState.worldVersion
                                  )
                ? "missing"
                : loadedState.worldVersion;
            return "This save has APWorld version '" + savedVersion +
                   "', but this plugin requires the current APWorld version '" +
                   RandomizerPlugin.PluginVersion +
                   "'. Generate a new seed with the current APWorld and " +
                   "start a new randomizer save.";
        }

        internal static bool Save(int slot)
        {
            string path = GetDataPath(slot);
            string tempPath = path + DataTemporaryExtension;
            string backupPath = path + DataBackupExtension;

            lock (SaveLock)
            {
                try
                {
                    if (SaveState.Instance == null)
                    {
                        throw new InvalidOperationException("No active randomizer state exists.");
                    }

                    XmlSerializer serializer = new XmlSerializer(typeof(SaveState));
                    Directory.CreateDirectory(Path.GetDirectoryName(path));

                    using (FileStream stream = new FileStream(
                        tempPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None
                    ))
                    using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    {
                        serializer.Serialize(writer, SaveState.Instance);
                        writer.Flush();
                        stream.Flush(true);
                    }

                    if (File.Exists(path))
                    {
                        File.Replace(tempPath, path, backupPath);
                    }
                    else
                    {
                        File.Move(tempPath, path);
                        File.Copy(path, backupPath, true);
                    }

                    Debug.Log("[Randomizer Save] SaveState saved: " + path);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Randomizer Save] Failed to save SaveState. " + ex);
                    return false;
                }
                finally
                {
                    TryDelete(tempPath);
                }
            }
        }

        internal static void Delete(int slot)
        {
            TryDelete(GetDataPath(slot));
            TryDelete(GetDataPath(slot) + DataBackupExtension);
            TryDelete(GetDataPath(slot) + DataTemporaryExtension);
        }

        private static bool TryLoadState(int slot, out SaveState state, out string error)
        {
            string path = GetDataPath(slot);
            string backupPath = path + DataBackupExtension;
            state = null;
            error = string.Empty;

            if (!File.Exists(path) && !File.Exists(backupPath))
            {
                error = "Randomizer metadata is missing for slot " + slot +
                        ". The save was not loaded so no blank state could overwrite it.";
                return false;
            }

            Exception primaryError = null;
            if (File.Exists(path) && TryDeserialize(path, out state, out primaryError))
            {
                return true;
            }

            Exception backupError = null;
            if (File.Exists(backupPath) && TryDeserialize(backupPath, out state, out backupError))
            {
                Debug.LogWarning("[Randomizer Save] Recovered metadata from backup: " + backupPath);
                return true;
            }

            error = "Randomizer metadata for slot " + slot +
                    " is unreadable. The save was blocked; primary error: " +
                    (primaryError == null ? "missing" : primaryError.Message) +
                    "; backup error: " +
                    (backupError == null ? "missing" : backupError.Message);
            return false;
        }

        private static bool TryDeserialize(string path, out SaveState state, out Exception error)
        {
            state = null;
            error = null;

            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(SaveState));
                using (FileStream stream = File.OpenRead(path))
                {
                    state = serializer.Deserialize(stream) as SaveState;
                }

                if (state == null)
                {
                    throw new InvalidDataException("The file deserialized to null.");
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                return false;
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Randomizer Save] Failed to delete " + path + ": " + ex.Message);
            }
        }

        internal static string GetDataPath(int slot)
        {
            return Path.Combine(Application.persistentDataPath, "user" + slot + DataExtension);
        }

        internal static MethodBase GetSaveFileNameMethod()
        {
            Type usageType = AccessTools.Inner(typeof(Platform), "SaveSlotFileNameUsage");

            if (usageType != null)
            {
                return AccessTools.Method(typeof(Platform), "GetSaveSlotFileName", new[] { typeof(int), usageType });
            }

            return AccessTools.Method(typeof(Platform), "GetSaveSlotFileName", new[] { typeof(int) });
        }

        internal static MethodBase GetWriteSaveSlotMethod()
        {
            return AccessTools.Method(
                typeof(DesktopPlatform),
                "WriteSaveSlot",
                new[] { typeof(int), typeof(byte[]), typeof(Action<bool>) }
            );
        }
    }

    [HarmonyPatch(
        typeof(GameManager),
        nameof(GameManager.CreateSaveGameData),
        new Type[] { typeof(int) }
    )]
    internal static class RestoreTemporaryTrapBeforeSavePatch
    {
        private static void Prefix()
        {
            SlabCaptureWarpSafety.PrepareForSave();
            LogicAuditCloakManager.PrepareForSave();
            TrapManager.PrepareForSave();
            BellhomePhaseManager.EnsureBellhomeUnlocked();
        }

        private static void Postfix()
        {
            TrapManager.ResumeAfterSave();
            LogicAuditCloakManager.ResumeAfterSave();
        }
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartNewGame), new Type[] { typeof(bool), typeof(bool) })]
    internal static class StartNewGamePatch
    {
        private static bool Prefix(out bool __state)
        {
            __state = Archipelago.Instance != null && Archipelago.Instance.Connected;
            if (__state)
            {
                return true;
            }

            RandomizerPlugin.Instance.ReportBlockingError(
                "Connect to Archipelago before starting a new randomizer save. " +
                "Offline play is available after that save has been bound once."
            );
            return false;
        }

        private static void Postfix(bool __state)
        {
            if (__state)
            {
                SavePatches.NewGame();
            }
        }
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.LoadGame), new Type[] { typeof(int), typeof(Action<bool>) })]
    internal static class ValidateLoadGamePatch
    {
        private static bool Prefix(int saveSlot, Action<bool> callback)
        {
            if (SavePatches.CanLoad(saveSlot, out string error))
            {
                return true;
            }

            RandomizerPlugin.Instance.ReportBlockingError(error);
            callback?.Invoke(false);
            return false;
        }
    }

    [HarmonyPatch(typeof(GameManager), "SetLoadedGameData", new Type[] { typeof(SaveGameData), typeof(int) })]
    internal static class LoadGamePatch
    {
        private static bool Prefix(int saveSlot, out bool __state)
        {
            __state = false;
            if (SavePatches.CanLoad(saveSlot, out string error))
            {
                __state = true;
                return true;
            }

            RandomizerPlugin.Instance.ReportBlockingError(error);
            return false;
        }

        private static void Postfix(SaveGameData saveGameData, int saveSlot, bool __state)
        {
            if (__state && !SavePatches.Load(saveSlot, out string error))
            {
                RandomizerPlugin.Instance.ReportBlockingError(error);
            }
        }
    }

    [HarmonyPatch]
    internal static class WriteSaveSlotPatch
    {
        private static MethodBase TargetMethod()
        {
            return SavePatches.GetWriteSaveSlotMethod();
        }

        private static void Prefix(int slotIndex, ref Action<bool> callback)
        {
            Action<bool> originalCallback = callback;
            callback = successful =>
            {
                bool randomizerSaved = successful && SavePatches.Save(slotIndex);
                originalCallback?.Invoke(successful && randomizerSaved);
            };
        }
    }

    [HarmonyPatch]
    internal static class SaveFileNamePatch
    {
        private static MethodBase TargetMethod()
        {
            return SavePatches.GetSaveFileNameMethod();
        }

        private static void Postfix(int __0, ref string __result)
        {
            if (!string.IsNullOrEmpty(__result))
            {
                __result = __result.Replace(SavePatches.VanillaExtension, SavePatches.SaveExtension);
            }
        }
    }

    [HarmonyPatch(typeof(DesktopPlatform), "ClearSaveSlot", new Type[] { typeof(int), typeof(Action<bool>) })]
    internal static class ClearRandomizerSavePatch
    {
        private static void Prefix(int slotIndex, ref Action<bool> callback)
        {
            Action<bool> originalCallback = callback;
            callback = successful =>
            {
                if (successful)
                {
                    SavePatches.Delete(slotIndex);
                }

                originalCallback?.Invoke(successful);
            };
        }
    }

    [HarmonyPatch]
    internal static class RandomizerRestoreDirectoryPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(SaveRestoreHandler),
                "GetDirectoryName",
                new[] { typeof(int) }
            );
        }

        private static void Postfix(ref string __result)
        {
            if (!string.IsNullOrEmpty(__result) &&
                !__result.StartsWith("Randomizer_", StringComparison.Ordinal))
            {
                __result = "Randomizer_" + __result;
            }
        }
    }

    [HarmonyPatch]
    internal static class RandomizerVersionBackupPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(SaveRestoreHandler),
                "GetVersionedBackupName",
                new[] { typeof(int) }
            );
        }

        private static void Postfix(ref string __result)
        {
            if (!string.IsNullOrEmpty(__result) &&
                !__result.StartsWith("randomizer_", StringComparison.Ordinal))
            {
                __result = "randomizer_" + __result;
            }
        }
    }
}
