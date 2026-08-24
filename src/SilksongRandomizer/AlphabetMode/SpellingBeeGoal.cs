using System.Collections.Generic;

namespace SilksongRandomizer.AlphabetMode
{
    internal static class SpellingBeeGoal
    {
        internal static bool HasAllLetters(ISet<string> receivedItems)
        {
            if (receivedItems == null)
            {
                return false;
            }

            for (char letter = 'A'; letter <= 'Z'; letter++)
            {
                if (!receivedItems.Contains("Letter: " + letter))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
