using System;
using System.Collections.Generic;

namespace SilksongRandomizer
{
    internal static class MelodyLocationManifest
    {
        internal const string ArchitectsMelody = "Architect's Melody";
        internal const string ConductorsMelody = "Conductor's Melody";
        internal const string VaultkeepersMelody = "Vaultkeeper's Melody";
        internal const string ElegyOfTheDeep = "Elegy of the Deep";
        internal const string BeastlingCall = "Beastling Call";

        internal sealed class Entry
        {
            internal readonly string LocationName;
            internal readonly string SceneName;
            internal readonly string ObjectName;
            internal readonly string FsmName;
            internal readonly float X;
            internal readonly float Y;
            internal readonly float SceneWidth;
            internal readonly float SceneHeight;

            internal Entry(
                string locationName,
                string sceneName,
                string objectName,
                string fsmName,
                float x,
                float y,
                float sceneWidth,
                float sceneHeight
            )
            {
                LocationName = locationName;
                SceneName = sceneName;
                ObjectName = objectName;
                FsmName = fsmName;
                X = x;
                Y = y;
                SceneWidth = sceneWidth;
                SceneHeight = sceneHeight;
            }
        }

        internal static readonly Entry[] Entries =
        {
            new Entry(
                ArchitectsMelody,
                "Cog_09",
                "puzzle cylinders",
                "Cylinder States",
                29.8558f,
                68.61611f,
                72f,
                117f
            ),
            new Entry(
                ConductorsMelody,
                "Hang_12",
                "Last Conductor NPC",
                "Dialogue",
                12.1715126f,
                8.097562f,
                64f,
                19f
            ),
            new Entry(
                VaultkeepersMelody,
                "Library_08",
                "Librarian",
                "Dialogue",
                29.4930458f,
                11.7311459f,
                115f,
                40f
            ),
            new Entry(
                ElegyOfTheDeep,
                "Tut_04",
                "Snail Shamans Set",
                "Dialogue",
                47.9599991f,
                6.74121094f,
                87f,
                36f
            ),
            new Entry(
                BeastlingCall,
                "Bellway_Centipede_Arena",
                "Bell Beast DefeatedCentipede NPC",
                "Control",
                139.360001f,
                10.03f,
                191f,
                43f
            ),
        };

        internal static bool TryGet(
            string sceneName,
            string objectName,
            string fsmName,
            out Entry entry
        )
        {
            foreach (Entry candidate in Entries)
            {
                if (string.Equals(
                        candidate.SceneName,
                        sceneName,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        candidate.ObjectName,
                        objectName,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        candidate.FsmName,
                        fsmName,
                        StringComparison.Ordinal))
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = null;
            return false;
        }
    }
}
