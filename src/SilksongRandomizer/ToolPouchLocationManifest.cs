using System;
using System.Collections.Generic;

namespace SilksongRandomizer
{
    internal static class ToolPouchLocationManifest
    {
        internal const string NativeAssetName = "Tool Pouch Pickup";
        internal const string PilgrimsRestLocation =
            "Tool Pouch: Pilgrim's Rest";
        internal const string LoddieLocation = "Tool Pouch: Loddie";
        internal const string BugsOfPharloomLocation =
            "Tool Pouch: Bugs of Pharloom";
        internal const string FleatopiaLocation = "Tool Pouch: Fleatopia";

        internal const string LoddieScene = "Bone_12";
        internal const string LoddieNpcObject = "Lady Bug Large";
        internal const string LoddieNpcFsm = "Convo";
        internal const string LoddieRewardState = "Reward 1";

        internal const string LoddieActThreeObject =
            "Ladybug Craft Pickup";
        internal const string LoddieActThreeFsm = "FSM";
        internal const string LoddieActThreePromptTypeState =
            "Prompt Type";
        internal const string LoddieActThreeFreePromptState =
            "Prompt - Tool Pouch";
        internal const string LoddieActThreePaidPromptState = "Prompt";
        internal const string LoddieActThreeWaitState = "Wait";
        internal const string LoddieActThreeCancelState = "Cancel";

        internal const string FleatopiaScene = "Aqueduct_05_caravan";
        internal const string FleatopiaNpcObject =
            "Caravan Troupe Leader Fleatopia NPC";
        internal const string FleatopiaNpcFsm = "Dialogue";
        internal const string FleatopiaRewardState = "Award Tool Pouch";

        internal static readonly string[] LocationNames =
        {
            PilgrimsRestLocation,
            LoddieLocation,
            BugsOfPharloomLocation,
            FleatopiaLocation,
        };

        internal static IEnumerable<Location> AppendTo(
            IEnumerable<Location> existing)
        {
            HashSet<string> names = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );
            if (existing != null)
            {
                foreach (Location location in existing)
                {
                    if (location == null)
                    {
                        continue;
                    }

                    names.Add(location.Name);
                    yield return location;
                }
            }

            foreach (string locationName in LocationNames)
            {
                if (names.Add(locationName))
                {
                    yield return new Location(
                        locationName,
                        ItemType.ToolPouch,
                        () => false
                    );
                }
            }
        }
    }
}
