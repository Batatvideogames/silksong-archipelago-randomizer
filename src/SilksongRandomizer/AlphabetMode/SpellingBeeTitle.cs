using System;
using UnityEngine;

namespace SilksongRandomizer.AlphabetMode
{
    internal static class SpellingBeeTitle
    {
        private const string LocalizationKey =
            "RANDOMIZER_SPELLING_BEE_COMPLETE";
        private static string pendingPhrase = string.Empty;
        private static string displayPhrase = string.Empty;
        private static bool failureLogged;

        internal static void Queue(string phrase)
        {
            if (SpellingBeeGoal.IsValidPhrase(phrase))
            {
                pendingPhrase = phrase;
            }
        }

        internal static string ResolveText(
            string key,
            string currentText)
        {
            if (string.IsNullOrEmpty(displayPhrase))
            {
                return currentText;
            }

            if (string.Equals(
                key,
                LocalizationKey + "_MAIN",
                StringComparison.Ordinal
            ))
            {
                return displayPhrase;
            }

            return string.Equals(
                    key,
                    LocalizationKey + "_SUB",
                    StringComparison.Ordinal
                ) ||
                string.Equals(
                    key,
                    LocalizationKey + "_SUPER",
                    StringComparison.Ordinal
                )
                    ? string.Empty
                    : currentText;
        }

        internal static void Update()
        {
            if (string.IsNullOrEmpty(pendingPhrase))
            {
                return;
            }

            try
            {
                AreaTitle title = AreaTitle.SilentInstance;
                if ((object)title == null ||
                    title.gameObject.activeSelf)
                {
                    return;
                }

                GameObject titleObject = title.gameObject;
                PlayMakerFSM fsm = FSMUtility.GetFSM(titleObject);
                if ((object)fsm == null)
                {
                    return;
                }

                displayPhrase = pendingPhrase;
                FSMUtility.SetBool(fsm, "Visited", false);
                FSMUtility.SetBool(fsm, "NPC Title", false);
                FSMUtility.SetBool(fsm, "City Title", false);
                FSMUtility.SetBool(fsm, "Display Right", false);
                FSMUtility.SetBool(fsm, "Title Has Subscript", false);
                FSMUtility.SetBool(fsm, "Title Has Superscript", false);
                FSMUtility.SetString(fsm, "Area Event", LocalizationKey);
                titleObject.SetActive(true);
                pendingPhrase = string.Empty;
            }
            catch (Exception ex)
            {
                pendingPhrase = string.Empty;
                if (failureLogged)
                {
                    return;
                }

                failureLogged = true;
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Could not display the Spelling Bee title: " +
                    ex.Message
                );
            }
        }
    }
}
