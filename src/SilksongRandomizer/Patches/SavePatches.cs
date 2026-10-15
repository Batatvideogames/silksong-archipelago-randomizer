using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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
        private const string DataBackupExtension = ".bak";
        private const string DataTemporaryExtension = ".tmp";
        private const string DataPendingExtension = ".pending";
        private static readonly object SaveLock = new object();
        private static readonly HashSet<string> PendingWrites =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                SteelSoulSettings.ApplyNewGame(saveState, PlayerData.instance);
                PlayerData.instance.IsSilkSpoolBroken = false;
                Archipelago.Instance.Resynchronize();
            }

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
            if (!TryLoadValidatedState(slot, out SaveState loadedState, out error))
            {
                return false;
            }

            if (Archipelago.Instance != null && Archipelago.Instance.Connected)
            {
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
            MossMotherWarpSafety.ReconcileSavedDefeat();
            ShakraStockPersistencePatches.ReconcileShakraStock(
                loadedState,
                PlayerData.instance
            );
            MelodyPatches.ReconcileReceivedOwnership();

            // The native spool progress fields mirror the loaded AP inventory.
            // An existing persistent HUD is redrawn afterward.
            // If it does not exist yet, its first DrawSpool call performs the
            // same synchronization through SpoolFragmentPatches.
            SpoolFragmentPatches.RefreshReceivedSpoolHud();

            RandomizerPlugin.Instance.StartCoroutine(
                StartingCrestFix.EnsureUsableStartingCrest()
            );
            RandomizerPlugin.Instance.StartCoroutine(
                ReceivedItemReconciliation
                    .ReconcileReceivedMajorKeysAfterLoad(loadedState)
            );
            RandomizerPlugin.Instance.StartCoroutine(
                ConsumableToolPatches.SynchronizeReceivedConsumables(
                    loadedState
                )
            );
            StartingLocationManager.ScheduleIfNeeded();
            Debug.Log("[Randomizer Save] SaveState loaded: " + GetDataPath(slot));
            return true;
        }

        internal static bool CanLoadPlayerData(int slot, PlayerData playerData, out string error)
        {
            return TryLoadValidatedState(slot, out SaveState state, out error) &&
                   SteelSoulSettings.ValidatePlayerData(state, playerData, out error);
        }

        internal static bool CanLoad(int slot, out string error)
        {
            return TryLoadValidatedState(slot, out _, out error);
        }

        private static bool TryLoadValidatedState(int slot, out SaveState loadedState, out string error)
        {
            return TryLoadState(slot, out loadedState, out error) &&
                   ValidateLoadedState(loadedState, out error);
        }

        private static bool ValidateLoadedState(SaveState loadedState, out string error)
        {
            error = string.Empty;
            if (loadedState.schemaVersion != SaveState.CurrentSchemaVersion)
            {
                error = "This save uses randomizer metadata schema " +
                        loadedState.schemaVersion + ", but this plugin requires schema " +
                        SaveState.CurrentSchemaVersion + ". Start a new randomizer save.";
                return false;
            }

            if (!TryInitializeLoadedState(loadedState, out error))
            {
                return false;
            }
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

        private static bool TryInitializeLoadedState(SaveState loadedState, out string error)
        {
            try
            {
                loadedState.InitializeAfterLoad();
                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                error = "Randomizer metadata could not be initialized. The save was not loaded " +
                        "and no progress was replaced. " + ex.Message;
                return false;
            }
        }

        internal static bool HasOfflineLoadableSave()
        {
            for (int slot = 1; slot <= 4; slot++)
            {
                if (CanLoad(slot, out _))
                {
                    return true;
                }
            }

            return false;
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

        internal sealed class PendingWrite
        {
            internal readonly string Path;
            internal readonly string Hash;
            private bool completed;

            internal PendingWrite(string path, string hash)
            {
                Path = path;
                Hash = hash;
            }

            internal bool TryComplete(bool nativeSaved, out bool saved)
            {
                saved = false;
                lock (SaveLock)
                {
                    if (completed) return false;
                    completed = true;
                    try
                    {
                        if (nativeSaved)
                        {
                            if (GetNativeHash(Path) != Hash)
                                throw new InvalidDataException("The game save changed during the write.");
                            CommitPending(Path);
                            saved = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[Randomizer Save] AP data is still pending. " + ex);
                    }
                    finally
                    {
                        PendingWrites.Remove(Path);
                    }
                    return true;
                }
            }
        }

        internal static bool TryPrepareSave(int slot, byte[] bytes, SaveState state,
            out PendingWrite pending, out string error)
        {
            pending = null;
            if (!SaveFolderManager.CanWriteCurrentSave(out error)) return false;
            string path = GetDataPath(slot);
            string tempPath = path + DataTemporaryExtension;
            lock (SaveLock)
            {
                if (!PendingWrites.Add(path))
                {
                    error = "A save for this slot is still in progress.";
                    return false;
                }
                try
                {
                    if (state == null || bytes == null || bytes.Length == 0)
                        throw new InvalidOperationException("No complete randomizer save is available.");
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    string pendingPath = path + DataPendingExtension;
                    if (File.Exists(pendingPath) &&
                        File.Exists(System.IO.Path.ChangeExtension(path, SaveExtension)) &&
                        TryDeserialize(pendingPath, out SaveState previous, out _) &&
                        previous.nativeSaveHash == GetNativeHash(path))
                    {
                        // This was the last completed game save before an interrupted AP write.
                        CommitPending(path);
                    }

                    string hash;
                    using (SHA256 algorithm = SHA256.Create())
                        hash = Convert.ToBase64String(algorithm.ComputeHash(bytes));
                    string previousHash = state.nativeSaveHash;
                    try
                    {
                        state.nativeSaveHash = hash;
                        using (FileStream stream = new FileStream(
                            tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                        {
                            new XmlSerializer(typeof(SaveState)).Serialize(writer, state);
                            writer.Flush();
                            stream.Flush(true);
                        }
                    }
                    finally
                    {
                        state.nativeSaveHash = previousHash;
                    }
                    if (File.Exists(pendingPath)) File.Replace(tempPath, pendingPath, null);
                    else File.Move(tempPath, pendingPath);
                    pending = new PendingWrite(path, hash);
                    error = string.Empty;
                    return true;
                }
                catch (Exception ex)
                {
                    PendingWrites.Remove(path);
                    error = "AP data could not be prepared. The game save was not written. " + ex.Message;
                    Debug.LogWarning("[Randomizer Save] " + error);
                    return false;
                }
                finally
                {
                    TryDelete(tempPath);
                }
            }
        }

        private static void CommitPending(string path)
        {
            if (File.Exists(path))
                File.Replace(path + DataPendingExtension, path, path + DataBackupExtension);
            else File.Move(path + DataPendingExtension, path);
        }

        private static string GetNativeHash(string dataPath)
        {
            using (FileStream stream = File.OpenRead(Path.ChangeExtension(dataPath, SaveExtension)))
            using (SHA256 algorithm = SHA256.Create())
                return Convert.ToBase64String(algorithm.ComputeHash(stream));
        }

        internal static bool IsWritePending(int slot)
        {
            lock (SaveLock) return PendingWrites.Contains(GetDataPath(slot));
        }

        internal static void Delete(int slot)
        {
            TryDelete(GetDataPath(slot));
            TryDelete(GetDataPath(slot) + DataBackupExtension);
            TryDelete(GetDataPath(slot) + DataTemporaryExtension);
            TryDelete(GetDataPath(slot) + DataPendingExtension);
        }

        private static bool TryLoadState(int slot, out SaveState state, out string error)
        {
            state = null;
            if (!SaveFolderManager.CanAccessSaves(out error)) return false;
            string path = GetDataPath(slot);
            lock (SaveLock)
            {
                if (PendingWrites.Contains(path))
                {
                    error = "A save for this slot is still in progress.";
                    return false;
                }
                try
                {
                    string hash = GetNativeHash(path);
                    foreach (string candidate in new[] { path + DataPendingExtension, path, path + DataBackupExtension })
                    {
                        if (File.Exists(candidate) &&
                            TryDeserialize(candidate, out SaveState loaded, out _) &&
                            loaded.nativeSaveHash == hash)
                        {
                            state = loaded;
                            if (candidate != path)
                                Debug.LogWarning("[Randomizer Save] Using matching AP data: " + candidate);
                            error = string.Empty;
                            return true;
                        }
                    }
                    error = "The game save and AP data for slot " + slot +
                        " do not match, or the AP data is missing or unreadable. " +
                        "Loading was stopped and the files were left untouched.";
                }
                catch (Exception ex)
                {
                    error = "The game save for slot " + slot +
                        " could not be checked. Loading was stopped and the files were left untouched. " + ex.Message;
                }
                return false;
            }
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

                state.RequireExplicitSchemaVersion();

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
            return SaveFolderManager.GetDataPath(slot);
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

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartNewGame), new Type[] { typeof(bool), typeof(bool) })]
    internal static class StartNewGamePatch
    {
        private static bool Prefix(ref bool __0, out bool __state)
        {
            __state = false;
            if (!SaveFolderManager.CanAccessSaves(out string error))
            {
                RandomizerPlugin.Instance.ReportBlockingError(error);
                return false;
            }
            __state = Archipelago.Instance != null && Archipelago.Instance.Connected;
            if (__state)
            {
                __0 = Archipelago.Instance.GameMode == SteelSoulSettings.SteelSoul;
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

    [HarmonyPatch(
        typeof(UIManager),
        nameof(UIManager.StartNewGame),
        new Type[] { typeof(bool), typeof(bool) }
    )]
    internal static class StartNewGameFromMenuPatch
    {
        private static bool Prefix()
        {
            if (!SaveFolderManager.CanAccessSaves(out string error))
            {
                RandomizerPlugin.Instance.ReportBlockingError(error);
                return false;
            }
            if (Archipelago.Instance != null && Archipelago.Instance.Connected)
            {
                return true;
            }

            RandomizerPlugin.Instance.ReportBlockingError(
                "Connect to Archipelago before starting a new randomizer save. " +
                "Offline play is available after that save has been bound once."
            );
            return false;
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
        private static bool Prefix(SaveGameData saveGameData, int saveSlot, out bool __state)
        {
            __state = false;
            if (SavePatches.CanLoadPlayerData(saveSlot, saveGameData?.playerData, out string error))
            {
                __state = true;
                return true;
            }

            RandomizerPlugin.Instance.ReportBlockingError(error);
            throw new InvalidDataException(error);
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
    internal static class DeferReceivedItemsDuringSavePatch
    {
        private static int pendingSaves;
        private static readonly Queue<SaveRequest> Queued = new Queue<SaveRequest>();
        private static bool scheduled;
        private static bool replaying;
        internal static bool IsSaving => Volatile.Read(ref pendingSaves) > 0 || scheduled || Queued.Count > 0;

        private sealed class SaveRequest
        {
            private readonly GameManager manager;
            private readonly MethodBase method;
            private readonly object[] arguments;
            private readonly SaveState state = SaveState.Instance;
            private readonly PlayerData player = PlayerData.instance;
            private readonly Action<bool> callback;
            private int notified;

            internal SaveRequest(GameManager manager, MethodBase method, object[] arguments, Action<bool> callback)
            {
                this.manager = manager;
                this.method = method;
                this.arguments = (object[])arguments.Clone();
                this.callback = callback;
                int index = Array.FindIndex(method.GetParameters(), parameter => parameter.Name == "ogCallback");
                this.arguments[index] = new Action<bool>(Notify);
            }

            private void Notify(bool saved)
            {
                if (Interlocked.Exchange(ref notified, 1) == 0) callback?.Invoke(saved);
            }

            internal void Run()
            {
                if (manager == null || SaveState.Instance != state || PlayerData.instance != player)
                {
                    Notify(false);
                    return;
                }
                try
                {
                    replaying = true;
                    method.Invoke(manager, arguments);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception is TargetInvocationException invocation ? invocation.InnerException : exception);
                    Notify(false);
                }
                finally { replaying = false; }
            }
        }

        private static void ScheduleNext()
        {
            if (Volatile.Read(ref pendingSaves) != 0 || scheduled || Queued.Count == 0) return;
            scheduled = true;
            CoreLoop.InvokeNext(() =>
            {
                scheduled = false;
                if (Volatile.Read(ref pendingSaves) != 0) return;
                SaveRequest request = Queued.Dequeue();
                try { request.Run(); }
                finally { ScheduleNext(); }
            });
        }

        internal sealed class PendingSave
        {
            private int completed;

            internal PendingSave()
            {
                Interlocked.Increment(ref pendingSaves);
            }

            internal void Complete(Action callback = null)
            {
                if (Interlocked.Exchange(ref completed, 1) != 0) return;
                try { callback?.Invoke(); }
                finally
                {
                    Interlocked.Decrement(ref pendingSaves);
                    ScheduleNext();
                }
            }
        }

        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(
                typeof(GameManager), "SaveGame",
                new[]
                {
                    typeof(int), typeof(Action<bool>), typeof(bool),
                    typeof(AutoSaveName)
                });
            yield return AccessTools.Method(
                typeof(GameManager), nameof(GameManager.SaveGameData),
                new[]
                {
                    typeof(int), typeof(SaveGameData), typeof(bool),
                    typeof(Action<bool>)
                });
        }

        [HarmonyPriority(Priority.First)]
        private static bool Prefix(GameManager __instance, MethodBase __originalMethod, object[] __args, ref Action<bool> ogCallback,
            out PendingSave __state)
        {
            __state = null;
            bool replay = replaying;
            replaying = false;
            if (SaveState.Instance == null) return true;
            int index = Array.FindIndex(__originalMethod.GetParameters(), parameter => parameter.Name == "ogCallback");
            Action<bool> originalCallback = ogCallback;
            if (!replay && IsSaving)
            {
                Queued.Enqueue(new SaveRequest(__instance, __originalMethod, __args, originalCallback));
                return false;
            }

            PendingSave pending = new PendingSave();
            __state = pending;
            ogCallback = new Action<bool>(successful =>
            {
                pending.Complete(() => originalCallback?.Invoke(successful));
            });
            __args[index] = ogCallback;
            return true;
        }

        private static void Postfix(bool __runOriginal, PendingSave __state)
        {
            if (!__runOriginal) __state?.Complete();
        }

        [HarmonyPriority(Priority.Last)]
        private static void Finalizer(Exception __exception, PendingSave __state)
        {
            if (__exception != null) __state?.Complete();
        }
    }

    [HarmonyPatch]
    internal static class WriteSaveSlotPatch
    {
        private static MethodBase TargetMethod()
        {
            return SavePatches.GetWriteSaveSlotMethod();
        }

        private static bool Prefix(int slotIndex, byte[] bytes, ref Action<bool> callback,
            out SavePatches.PendingWrite __state)
        {
            if (!SavePatches.TryPrepareSave(slotIndex, bytes, SaveState.Instance, out __state, out string error))
            {
                RandomizerPlugin.Instance.ReportBlockingError(error);
                callback?.Invoke(false);
                return false;
            }
            SavePatches.PendingWrite pending = __state;
            Action<bool> originalCallback = callback;
            callback = successful =>
            {
                if (pending.TryComplete(successful, out bool saved))
                    originalCallback?.Invoke(saved);
            };
            return true;
        }

        private static void Finalizer(Exception __exception, SavePatches.PendingWrite __state)
        {
            if (__exception != null) __state?.TryComplete(false, out _);
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
        private static bool Prefix(int slotIndex, ref Action<bool> callback)
        {
            if (!SaveFolderManager.CanAccessSaves(out string error))
            {
                RandomizerPlugin.Instance.ReportBlockingError(error);
                callback?.Invoke(false);
                return false;
            }
            if (SavePatches.IsWritePending(slotIndex))
            {
                RandomizerPlugin.Instance.ReportBlockingError("A save for this slot is still in progress.");
                callback?.Invoke(false);
                return false;
            }
            Action<bool> originalCallback = callback;
            callback = successful =>
            {
                if (successful)
                {
                    SavePatches.Delete(slotIndex);
                }

                originalCallback?.Invoke(successful);
            };
            return true;
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
