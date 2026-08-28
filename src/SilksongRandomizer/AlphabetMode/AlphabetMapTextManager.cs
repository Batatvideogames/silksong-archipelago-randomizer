using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TeamCherry.Localization;
using TMProOld;

namespace SilksongRandomizer.AlphabetMode
{
    internal static class AlphabetMapTextManager
    {
        private sealed class TextRecord
        {
            internal readonly TMP_Text Text;
            internal bool HasEnglishOriginal;
            internal string Original;
            internal string Rendered;

            internal TextRecord(TMP_Text text)
            {
                Text = text;
                Original = text.text ?? string.Empty;
                Rendered = Original;
                HasEnglishOriginal = TryCaptureLocalizedSource(this);
            }
        }

        private static readonly FieldInfo WideLabelTextField =
            AccessTools.Field(
                typeof(InventoryItemWideMapZone),
                "labelText"
            );
        private static readonly FieldInfo MapZoneInfoField =
            AccessTools.Field(typeof(GameMap), "mapZoneInfo");
        private static readonly Type ZoneInfoType =
            typeof(GameMap).GetNestedType(
                "ZoneInfo",
                BindingFlags.NonPublic
            );
        private static readonly FieldInfo ZoneTextsField =
            ZoneInfoType == null
                ? null
                : AccessTools.Field(ZoneInfoType, "texts");
        private static readonly List<TextRecord> WideRecords =
            new List<TextRecord>();
        private static readonly List<TextRecord> GameRecords =
            new List<TextRecord>();
        private static InventoryWideMap currentWideMap;
        private static GameMap currentGameMap;
        private static bool gameRecordsCaptured;
        private static bool refreshFailureLogged;

        internal static void Capture(InventoryWideMap wideMap)
        {
            try
            {
                CaptureWideMap(wideMap);
            }
            catch (Exception ex)
            {
                LogFailure(ex);
            }
        }

        private static void CaptureWideMap(InventoryWideMap wideMap)
        {
            if (wideMap == null)
            {
                return;
            }

            if (wideMap != currentWideMap)
            {
                currentWideMap = wideMap;
                WideRecords.Clear();
                foreach (InventoryItemWideMapZone zone in
                         wideMap.DefaultSelectables)
                {
                    TMP_Text text = WideLabelTextField?.GetValue(zone)
                        as TMP_Text;
                    if (text != null)
                    {
                        WideRecords.Add(new TextRecord(text));
                    }
                }
            }

            Refresh(WideRecords);
        }

        internal static void Capture(GameMap gameMap)
        {
            try
            {
                CaptureGameMap(gameMap);
            }
            catch (Exception ex)
            {
                gameRecordsCaptured = GameRecords.Count > 0;
                LogFailure(ex);
            }
        }

        private static void CaptureGameMap(GameMap gameMap)
        {
            if (gameMap == null)
            {
                return;
            }

            if (gameMap != currentGameMap)
            {
                currentGameMap = gameMap;
                GameRecords.Clear();
                gameRecordsCaptured = false;
            }

            if (!gameRecordsCaptured)
            {
                gameRecordsCaptured = TryCaptureGameRecords(gameMap);
            }

            Refresh(GameRecords);
        }

        private static bool TryCaptureGameRecords(GameMap gameMap)
        {
            if (MapZoneInfoField == null || ZoneTextsField == null)
            {
                return false;
            }

            Array zoneInfos = MapZoneInfoField.GetValue(gameMap) as Array;
            if (zoneInfos == null)
            {
                return false;
            }

            GameRecords.Clear();
            HashSet<TMP_Text> captured = new HashSet<TMP_Text>();
            foreach (object zoneInfo in zoneInfos)
            {
                if (zoneInfo == null)
                {
                    continue;
                }

                IEnumerable<TMP_Text> texts =
                    ZoneTextsField.GetValue(zoneInfo)
                    as IEnumerable<TMP_Text>;
                if (texts == null)
                {
                    continue;
                }

                foreach (TMP_Text text in texts)
                {
                    if (text != null && captured.Add(text))
                    {
                        GameRecords.Add(new TextRecord(text));
                    }
                }
            }
            return GameRecords.Count > 0;
        }

        internal static void Refresh()
        {
            try
            {
                RefreshTrackedText();
            }
            catch (Exception ex)
            {
                LogFailure(ex);
            }
        }

        private static void RefreshTrackedText()
        {
            Refresh(WideRecords);
            Refresh(GameRecords);
        }

        internal static bool NeedsEnglishSource()
        {
            if (!AlphabetModeManager.IsEnglishLanguage(
                    Language.CurrentLanguage()
                ))
            {
                return false;
            }

            return NeedsEnglishSource(WideRecords) ||
                   NeedsEnglishSource(GameRecords);
        }

        private static bool NeedsEnglishSource(
            List<TextRecord> records)
        {
            foreach (TextRecord record in records)
            {
                if (record.Text != null && !record.HasEnglishOriginal)
                {
                    return true;
                }
            }
            return false;
        }

        internal static void CaptureEnglishSource()
        {
            if (!AlphabetModeManager.IsEnglishLanguage(
                    Language.CurrentLanguage()
                ))
            {
                return;
            }

            CaptureEnglishSource(WideRecords);
            CaptureEnglishSource(GameRecords);
        }

        private static void CaptureEnglishSource(
            List<TextRecord> records)
        {
            foreach (TextRecord record in records)
            {
                if (record.Text == null)
                {
                    continue;
                }

                if (TryCaptureLocalizedSource(record))
                {
                    continue;
                }

                record.Original = record.Text.text ?? string.Empty;
                record.Rendered = record.Original;
                record.HasEnglishOriginal = true;
            }
        }

        private static bool TryCaptureLocalizedSource(TextRecord record)
        {
            if (!AlphabetModeManager.IsEnglishLanguage(
                    Language.CurrentLanguage()
                ))
            {
                return false;
            }

            SetTextMeshProGameText localizer =
                record.Text.GetComponent<SetTextMeshProGameText>();
            if (localizer == null)
            {
                return false;
            }

            AlphabetModeManager.BeginTextBypass();
            try
            {
                record.Original = localizer.Text.ToString();
                record.Rendered = record.Original;
                record.HasEnglishOriginal = true;
                return true;
            }
            finally
            {
                AlphabetModeManager.EndTextBypass();
            }
        }

        private static void LogFailure(Exception ex)
        {
            if (refreshFailureLogged)
            {
                return;
            }

            refreshFailureLogged = true;
            RandomizerPlugin.Log?.LogWarning(
                "[RANDOMIZER] Could not refresh Alphabet mode map text: " +
                ex.Message
            );
        }

        internal static void ReleaseTextBypass(ref bool active)
        {
            if (!active)
            {
                return;
            }

            active = false;
            AlphabetModeManager.EndTextBypass();
        }

        internal static void Clear(GameMap gameMap)
        {
            if (gameMap != currentGameMap)
            {
                return;
            }

            currentGameMap = null;
            GameRecords.Clear();
        }

        private static void Refresh(List<TextRecord> records)
        {
            bool filterActive = AlphabetModeManager.IsFilteringCurrentText();
            bool english = AlphabetModeManager.IsEnglishLanguage(
                Language.CurrentLanguage()
            );

            for (int index = records.Count - 1; index >= 0; index--)
            {
                TextRecord record = records[index];
                if (record.Text == null)
                {
                    records.RemoveAt(index);
                    continue;
                }

                string current = record.Text.text ?? string.Empty;
                if (!english)
                {
                    record.Rendered = current;
                    continue;
                }

                if (!filterActive &&
                    (!record.HasEnglishOriginal ||
                     !string.Equals(
                        current,
                        record.Rendered,
                        StringComparison.Ordinal
                    )))
                {
                    if (!TryCaptureLocalizedSource(record))
                    {
                        record.Original = current;
                        record.HasEnglishOriginal = true;
                    }
                }
                if (!record.HasEnglishOriginal)
                {
                    record.Rendered = current;
                    continue;
                }

                string rendered = AlphabetModeManager.FilterDirectText(
                    record.Original
                );
                if (!string.Equals(
                        current,
                        rendered,
                        StringComparison.Ordinal
                    ))
                {
                    record.Text.text = rendered;
                }
                record.Rendered = rendered;
            }
        }
    }

    [HarmonyPatch(typeof(InventoryMapManager), "EnsureWideMapSpawned")]
    internal static class AlphabetWideMapCreatePatch
    {
        private static void Prefix(out bool __state)
        {
            AlphabetModeManager.BeginTextBypass();
            __state = true;
        }

        private static void Postfix(
            InventoryWideMap ___wideMap,
            ref bool __state)
        {
            AlphabetMapTextManager.Capture(___wideMap);
            AlphabetMapTextManager.ReleaseTextBypass(ref __state);
            AlphabetMapTextManager.Refresh();
        }

        private static Exception Finalizer(
            Exception __exception,
            ref bool __state)
        {
            AlphabetMapTextManager.ReleaseTextBypass(ref __state);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(InventoryMapManager), "EnsureGameMapSpawned")]
    internal static class AlphabetGameMapCreatePatch
    {
        private static void Prefix(out bool __state)
        {
            AlphabetModeManager.BeginTextBypass();
            __state = true;
        }

        private static void Postfix(
            GameMap ___gameMap,
            ref bool __state)
        {
            AlphabetMapTextManager.Capture(___gameMap);
            AlphabetMapTextManager.ReleaseTextBypass(ref __state);
            AlphabetMapTextManager.Refresh();
        }

        private static Exception Finalizer(
            Exception __exception,
            ref bool __state)
        {
            AlphabetMapTextManager.ReleaseTextBypass(ref __state);
            return __exception;
        }
    }

    [HarmonyPatch(
        typeof(InventoryWideMap),
        nameof(InventoryWideMap.UpdatePositions)
    )]
    internal static class AlphabetWideMapRefreshPatch
    {
        private static void Postfix(InventoryWideMap __instance)
        {
            AlphabetMapTextManager.Capture(__instance);
        }
    }

    [HarmonyPatch(typeof(GameMap), nameof(GameMap.OnStart))]
    internal static class AlphabetGameMapStartPatch
    {
        private static void Postfix(GameMap __instance)
        {
            AlphabetMapTextManager.Capture(__instance);
        }
    }

    [HarmonyPatch(
        typeof(GameMap),
        nameof(GameMap.SetupMap),
        new[] { typeof(bool) }
    )]
    internal static class AlphabetGameMapSetupPatch
    {
        private static void Postfix(GameMap __instance)
        {
            AlphabetMapTextManager.Capture(__instance);
        }
    }

    [HarmonyPatch(typeof(GameMap), nameof(GameMap.WorldMap))]
    internal static class AlphabetGameMapOpenPatch
    {
        private static void Postfix(GameMap __instance)
        {
            AlphabetMapTextManager.Capture(__instance);
        }
    }

    [HarmonyPatch(typeof(GameMap), "OnDestroy")]
    internal static class AlphabetGameMapDestroyPatch
    {
        private static void Prefix(GameMap __instance)
        {
            AlphabetMapTextManager.Clear(__instance);
        }
    }
}
