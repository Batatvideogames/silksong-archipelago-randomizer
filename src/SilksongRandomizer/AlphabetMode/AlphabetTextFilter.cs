using System;
using System.Text;

namespace SilksongRandomizer.AlphabetMode
{
    internal static class AlphabetTextFilter
    {
        internal const int FullMask = (1 << 26) - 1;

        internal static string Filter(string value, int ownedMask)
        {
            if (string.IsNullOrEmpty(value) || ownedMask == FullMask)
            {
                return value;
            }

            StringBuilder filtered = new StringBuilder(value.Length);
            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                if (current == '<')
                {
                    int tagEnd = value.IndexOf('>', index + 1);
                    if (tagEnd >= 0)
                    {
                        filtered.Append(value, index, tagEnd - index + 1);
                        index = tagEnd;
                        continue;
                    }
                }

                int letterIndex = GetLetterIndex(current);
                if (letterIndex < 0 ||
                    (ownedMask & (1 << letterIndex)) != 0)
                {
                    filtered.Append(current);
                }
            }

            return filtered.ToString();
        }

        internal static int GetLetterIndex(char value)
        {
            if (value >= 'A' && value <= 'Z')
            {
                return value - 'A';
            }
            if (value >= 'a' && value <= 'z')
            {
                return value - 'a';
            }
            return -1;
        }
    }
}

