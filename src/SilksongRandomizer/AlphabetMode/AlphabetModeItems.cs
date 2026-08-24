using System;
using System.Collections.Generic;

namespace SilksongRandomizer.AlphabetMode
{
    internal static class AlphabetModeItems
    {
        internal static Item[] Append(Item[] items)
        {
            List<Item> combined = new List<Item>(
                items ?? Array.Empty<Item>()
            );
            for (char letter = 'A'; letter <= 'Z'; letter++)
            {
                combined.Add(new Item(
                    "Letter: " + letter,
                    ItemType.Event,
                    null
                ));
            }
            return combined.ToArray();
        }
    }
}

