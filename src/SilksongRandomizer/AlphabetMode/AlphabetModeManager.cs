using System;
using System.Collections.Generic;
using TeamCherry.Localization;

namespace SilksongRandomizer.AlphabetMode
{
    internal static class AlphabetModeManager
    {
        private const string LetterPrefix = "Letter: ";
        private static readonly string[] LetterItemNames =
            BuildLetterItemNames();
        private static bool filterFailureLogged;
        private static bool refreshFailureLogged;
        private static bool displaySnapshotInitialized;
        private static SaveState displaySnapshotState;
        private static bool displaySnapshotActive;
        private static int displaySnapshotMask;
        private static LanguageCode displaySnapshotLanguage;
        private static string displaySnapshotScene = string.Empty;
        [ThreadStatic]
        private static int textBypassDepth;

        internal static string FilterDirectText(string value)
        {
            try
            {
                if (!ShouldFilterCurrentText())
                {
                    return value;
                }

                return AlphabetTextFilter.Filter(value, GetOwnedLetterMask());
            }
            catch (Exception ex)
            {
                LogFilterFailure(ex);
                return value;
            }
        }

        internal static bool IsFilteringCurrentText()
        {
            try
            {
                return ShouldFilterCurrentText();
            }
            catch (Exception ex)
            {
                LogFilterFailure(ex);
                return false;
            }
        }

        internal static void BeginTextBypass()
        {
            textBypassDepth++;
        }

        internal static void EndTextBypass()
        {
            if (textBypassDepth > 0)
            {
                textBypassDepth--;
            }
        }

        internal static bool IsLetterItemName(
            string itemName,
            out int letterIndex)
        {
            letterIndex = -1;
            if (string.IsNullOrEmpty(itemName) ||
                itemName.Length != LetterPrefix.Length + 1 ||
                !itemName.StartsWith(
                    LetterPrefix,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return false;
            }

            letterIndex = AlphabetTextFilter.GetLetterIndex(
                itemName[LetterPrefix.Length]
            );
            return letterIndex >= 0;
        }

        internal static IEnumerable<char> GetOwnedLetters()
        {
            int mask = GetOwnedLetterMask();
            for (int index = 0; index < 26; index++)
            {
                if ((mask & (1 << index)) != 0)
                {
                    yield return (char)('A' + index);
                }
            }
        }

        internal static int GetOwnedLetterMask()
        {
            SaveState state = SaveState.Instance;
            if (state?.receivedItems == null)
            {
                return 0;
            }

            int mask = 0;
            for (int index = 0; index < 26; index++)
            {
                if (state.receivedItems.Contains(LetterItemNames[index]))
                {
                    mask |= 1 << index;
                }
            }
            return mask;
        }

        private static string[] BuildLetterItemNames()
        {
            string[] names = new string[26];
            for (int index = 0; index < names.Length; index++)
            {
                names[index] = LetterPrefix + (char)('A' + index);
            }
            return names;
        }

        internal static void OnReceivedItemCommitted(string itemName)
        {
            if (IsLetterItemName(itemName, out _))
            {
                TryCompleteSpellingBeeGoal();
                ReconcileVisibleText();
            }
        }

        internal static bool TryCompleteSpellingBeeGoal()
        {
            SaveState state = SaveState.Instance;
            if (state == null ||
                state.goalCompleted ||
                !string.Equals(
                    state.goal,
                    Archipelago.SpellingBeeGoal,
                    StringComparison.Ordinal
                ) ||
                !SpellingBeeGoal.HasAllLetters(state.receivedItems))
            {
                return false;
            }

            state.CheckLocation(Archipelago.GoalLocationName);
            return state.goalCompleted;
        }

        internal static void ReconcileVisibleText()
        {
            try
            {
                TryCompleteSpellingBeeGoal();
                SaveState state = SaveState.Instance;
                GameManager gameManager = GameManager.SilentInstance;
                LanguageCode language = Language.CurrentLanguage();
                string scene = gameManager?.GetSceneNameString() ??
                    string.Empty;
                bool active = ShouldFilterCurrentText(language);
                bool wasActive =
                    displaySnapshotInitialized && displaySnapshotActive;
                int mask = active ? GetOwnedLetterMask() : 0;
                bool changed =
                    !displaySnapshotInitialized ||
                    !ReferenceEquals(displaySnapshotState, state) ||
                    displaySnapshotActive != active ||
                    displaySnapshotMask != mask ||
                    displaySnapshotLanguage != language ||
                    !string.Equals(
                        displaySnapshotScene,
                        scene,
                        StringComparison.Ordinal
                    );

                displaySnapshotInitialized = true;
                displaySnapshotState = state;
                displaySnapshotActive = active;
                displaySnapshotMask = mask;
                displaySnapshotLanguage = language;
                displaySnapshotScene = scene;

                if (changed && (wasActive || active))
                {
                    RefreshVisibleText();
                }
            }
            catch (Exception ex)
            {
                LogFilterFailure(ex);
            }
        }

        internal static void RefreshVisibleText()
        {
            try
            {
                GameManager gameManager = GameManager.SilentInstance;
                if (gameManager != null)
                {
                    gameManager.RefreshLocalization();
                }
                AlphabetMapTextManager.Refresh();
            }
            catch (Exception ex)
            {
                if (!refreshFailureLogged)
                {
                    refreshFailureLogged = true;
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Could not refresh Alphabet mode text: " +
                        ex.Message
                    );
                }
            }
        }

        private static bool ShouldFilterCurrentText()
        {
            return ShouldFilterCurrentText(Language.CurrentLanguage());
        }

        private static bool ShouldFilterCurrentText(LanguageCode language)
        {
            SaveState state = SaveState.Instance;
            GameManager gameManager = GameManager.SilentInstance;
            return textBypassDepth == 0 &&
                   state != null &&
                   state.IsRoomBound &&
                   state.alphabetMode &&
                   gameManager != null &&
                   !IsStartMenu(gameManager) &&
                   IsEnglishLanguage(language);
        }

        private static bool IsStartMenu(GameManager gameManager)
        {
            if (gameManager.IsMenuScene())
            {
                return true;
            }

            string sceneName = gameManager.GetSceneNameString();
            return string.IsNullOrEmpty(sceneName) ||
                   sceneName.StartsWith(
                       "Menu_",
                       StringComparison.OrdinalIgnoreCase
                   );
        }

        private static bool IsEnglishLanguage(LanguageCode language)
        {
            return language == LanguageCode.EN ||
                   language == LanguageCode.EN_US ||
                   language == LanguageCode.EN_AU ||
                   language == LanguageCode.EN_NZ ||
                   language == LanguageCode.EN_ZA ||
                   language == LanguageCode.EN_CB ||
                   language == LanguageCode.EN_TT ||
                   language == LanguageCode.EN_GB ||
                   language == LanguageCode.EN_CA ||
                   language == LanguageCode.EN_IE ||
                   language == LanguageCode.EN_JM ||
                   language == LanguageCode.EN_BZ;
        }

        private static void LogFilterFailure(Exception ex)
        {
            if (filterFailureLogged)
            {
                return;
            }

            filterFailureLogged = true;
            RandomizerPlugin.Log?.LogWarning(
                "[RANDOMIZER] Alphabet mode text filtering was skipped: " +
                ex.Message
            );
        }
    }
}

