using System.Collections.Generic;

namespace SilksongRandomizer.AlphabetMode
{
    internal static class SpellingBeeGoal
    {
        internal static bool IsValidPhrase(string phrase)
        {
            return TryGetRequiredLetterItemNames(
                phrase,
                out HashSet<string> _
            );
        }

        internal static bool HasRequiredLetters(
            ISet<string> receivedItems,
            string phrase)
        {
            if (receivedItems == null ||
                !TryGetRequiredLetterItemNames(
                    phrase,
                    out HashSet<string> itemNames
                ))
            {
                return false;
            }

            foreach (string itemName in itemNames)
            {
                if (!receivedItems.Contains(itemName))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryGetRequiredLetterItemNames(
            string phrase,
            out HashSet<string> itemNames)
        {
            itemNames = new HashSet<string>();
            if (string.IsNullOrEmpty(phrase))
            {
                return false;
            }

            foreach (char character in phrase)
            {
                if (character == ' ')
                {
                    continue;
                }
                if (!(
                    character >= 'A' && character <= 'Z' ||
                    character >= 'a' && character <= 'z'
                ))
                {
                    return false;
                }

                itemNames.Add(
                    "Letter: " + char.ToUpperInvariant(character)
                );
            }
            return itemNames.Count > 0;
        }
    }
}
