using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace SilksongRandomizer
{
    internal static class SaveFolderManager
    {
        private static readonly MethodInfo NativePathMethod = AccessTools.Method(
            typeof(DesktopPlatform), "GetSaveSlotPath");
        private static ConfigEntry<string> lastDirectory;
        private static ConfigEntry<bool> defaultNoticeDismissed;
        private static string sessionDirectory;
        private static string notice;
        private static bool needsConfirmation;

        internal static void Initialize(ConfigFile config)
        {
            lastDirectory = config.Bind("Saves", "Last Folder", string.Empty,
                "Last acknowledged save folder.");
            defaultNoticeDismissed = config.Bind("Saves", "Default Notice Dismissed", false,
                "Whether the offline save folder notice has been acknowledged.");
            sessionDirectory = null;
            notice = null;
            needsConfirmation = false;
        }

        internal static string GetDataPath(int slot)
        {
            if (slot < 1 || slot > 4) throw new ArgumentOutOfRangeException(nameof(slot));
            if (!(Platform.Current is DesktopPlatform platform) || NativePathMethod == null)
                throw new InvalidOperationException("The game save folder is unavailable.");
            Type usage = NativePathMethod.GetParameters()[1].ParameterType;
            string savePath = (string)NativePathMethod.Invoke(platform,
                new[] { (object)slot, Enum.Parse(usage, "Primary") });
            if (Path.GetFileName(savePath) != "user" + slot + ".randomizersave")
                throw new InvalidOperationException("The game is not using randomizer save filenames.");
            return Path.ChangeExtension(savePath, ".randomizerdata");
        }

        private static void CheckFolder()
        {
            if (lastDirectory == null) throw new InvalidOperationException("Save settings are not ready.");
            string directory = Path.GetFullPath(Path.GetDirectoryName(GetDataPath(1)));
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (sessionDirectory != null)
            {
                if (!string.Equals(directory, sessionDirectory, comparison))
                    throw new InvalidOperationException("The game changed its save folder during play. Restart before continuing.");
                return;
            }

            sessionDirectory = directory;
            if (!string.IsNullOrEmpty(lastDirectory.Value) &&
                !string.Equals(lastDirectory.Value, directory, comparison))
            {
                notice = "The game is using a different save folder. This can happen when its account service " +
                    "is unavailable. Some save slots may not appear. Your other saves have not been moved or deleted.";
            }
            else if (Path.GetFileName(directory) == "default" && !defaultNoticeDismissed.Value)
            {
                notice = "The game is using its offline save folder. You can play offline, " +
                    "but saves from your account folder may not appear here.";
            }
            needsConfirmation = !string.IsNullOrEmpty(notice);
            if (!needsConfirmation) RememberFolder();
            RandomizerPlugin.Log?.LogInfo("[Randomizer Save] Folder: " + directory);
        }

        private static void RememberFolder()
        {
            lastDirectory.Value = sessionDirectory;
            if (Path.GetFileName(sessionDirectory) == "default") defaultNoticeDismissed.Value = true;
        }

        internal static bool CanAccessSaves(out string error)
        {
            try
            {
                CheckFolder();
                error = needsConfirmation ? "Read the save folder notice in F3 before continuing." : string.Empty;
            }
            catch (Exception ex)
            {
                error = "The game save folder could not be checked. " + ex.Message;
            }
            return error.Length == 0;
        }

        internal static bool CanWriteCurrentSave(out string error) => CanAccessSaves(out error);

        internal static void DrawNotice()
        {
            if (CanAccessSaves(out string error)) return;
            if (!needsConfirmation)
            {
                GUILayout.Label(error);
                return;
            }
            GUILayout.Label(notice);
            GUILayout.Label("Current save location: " + sessionDirectory);
            if (GUILayout.Button("Continue with the game's saves"))
            {
                try
                {
                    RememberFolder();
                    needsConfirmation = false;
                }
                catch (Exception ex)
                {
                    RandomizerPlugin.Log?.LogWarning("[Randomizer Save] Could not remember the folder. " + ex.Message);
                }
            }
            GUILayout.Space(8f);
        }
    }
}
