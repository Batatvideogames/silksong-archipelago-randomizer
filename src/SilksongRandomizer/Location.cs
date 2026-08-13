using System;

namespace SilksongRandomizer
{
    public class Location
    {
        public readonly string SourceName;
        public readonly string Name;
        public readonly ItemType Type;
        public readonly Func<bool> Check;

        public Location(string name, ItemType type, Func<bool> check)
        {
            SourceName = name;
            Name = LocationSet.GetCanonicalLocationName(name);
            Type = type;
            Check = check;
        }
    }
}
