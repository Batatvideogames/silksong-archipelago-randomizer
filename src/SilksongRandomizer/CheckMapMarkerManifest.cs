using System;
using System.Collections.Generic;
using GlobalEnums;
using UnityEngine;

namespace SilksongRandomizer
{
    public enum CheckMapMarkerMode
    {
        Off = 0,
        MappedRooms = 1,
        OwnedMaps = 2,
        All = 3,
    }

    internal enum MapMarkerPositionConfidence
    {
        ExactUpstream,
        RoomCenter,
    }

    internal sealed class MapCheckPosition
    {
        internal readonly string LocationName;
        internal readonly string SceneName;
        internal readonly Vector2 PositionInScene;
        internal readonly Vector2 SceneSize;
        internal readonly MapMarkerPositionConfidence Confidence;

        internal MapCheckPosition(
            string locationName,
            string sceneName,
            float positionX,
            float positionY,
            float sceneWidth,
            float sceneHeight,
            MapMarkerPositionConfidence confidence
        )
        {
            LocationName = locationName;
            SceneName = sceneName;
            PositionInScene = new Vector2(positionX, positionY);
            SceneSize = new Vector2(sceneWidth, sceneHeight);
            Confidence = confidence;
        }
    }

    internal static class CheckMapMarkerManifest
    {
        // Coordinates and room dimensions are from Timothy Marriott's
        // public Silksong-Rando resources and flibber-hk's public datamined
        // pickup census. Entries without a matching AP identity are omitted.
        private static readonly MapCheckPosition[] StaticPositions =
        {
            new MapCheckPosition("Skill Unlock: Double Jump", "Peak_08b", 279.5f, 105.53f, 336f, 138f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Skill Unlock: Silk Soar", "Abyss_08", 86.91002f, 9.861683f, 164f, 106f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Skill Unlock: Wall Jump", "Shellwood_10", 40.59f, 79.24f, 79f, 102f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Skill Unlock: Drifter's Cloak", "Bone_East_Umbrella", 27.63f, 8.37f, 55f, 26f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Skill Unlock: Dash", "Bone_East_05", 100.030000f, 13.270000f, 190f, 47f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Skill Unlock: Harpoon", "Under_18", 26.3499985f, 13.23f, 160f, 50f, MapMarkerPositionConfidence.ExactUpstream),
            // The Needolin reward root is at y=52.0096, outside this 95x38
            // room's projection bounds. The marker uses the in-bounds Bellhart
            // shrine anchor from the same physical source building.
            new MapCheckPosition("Skill Unlock: Needolin", "Belltown_Shrine", 54.09f, 21.1f, 95f, 38f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Spell Unlock: Silk Spear", "Mosstown_02", 86.94f, 52.31f, 160f, 65f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Spell Unlock: Thread Sphere", "Greymoor_22", 39.82f, 36.49f, 110f, 50f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Unlock: Dead Mans Purse", "Crawl_01", 54.5f, 85.1f, 150f, 130f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Unlock: Wisp Lantern", "Belltown_08", 54.55f, 11.31f, 115f, 47f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Unlock: Harpoon", "Belltown_Room_shellwood", 37.023f, 7.07f, 55f, 26f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Unlock: Straight Pin", "Bone_12", 41.13f, 27.81f, 58f, 43f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Pouch: Loddie", "Bone_12", 23.02f, 4.70f, 58f, 43f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Unlock: Bone Necklace", "Bone_17", 12.701f, 6.477f, 35f, 25f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Unlock: Flintstone", "Dock_02b", 26.57f, 103.541954f, 94f, 120f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Unlock: Bell Bind", "Dock_03b", 154.09f, 107.277481f, 168f, 120f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Unlock: Sprintmaster", "Bone_East_Weavehome", 124.243004f, 90.5460052f, 205f, 120f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Beast Shard: Marrowmaw", "Tut_01", 101.233429f, 17.286209f, 120f, 120f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Beast Shard: Craggler", "Crawl_04", 83.519997f, 14.89f, 165f, 20f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Beast Shard: Pilgrim's Rest", "Bone_East_10_Church", 139.320007f, 10.97f, 200f, 30f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Beast Shard: Memorium", "Arborium_02", 111.046463f, 8.935061f, 132f, 29f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Beast Shard: Sprintmaster", "Sprintmaster_Cave", 88.930000f, 14.120000f, 236f, 51f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Unlock: Tri Pin", "Greymoor_15b", 184.319992f, 100.362526f, 220f, 141f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Unlock: Barbed Wire", "Dust_Barb", 28.95f, 4.32f, 45f, 32f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Unlock: Maggot Charm", "Aqueduct_06", 126.24f, 8.177498f, 150f, 120f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Unlock: Quick Sling", "Shadow_11", 7.59999943f, 84.27748f, 29f, 112f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Unlock: Magnetite Dice", "Coral_33", 20.2100067f, 60.29f, 58f, 74f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Unlock: Conch Drill", "Coral_Tower_01", 80.87f, 7.31000376f, 138f, 22f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Unlock: Screw Attack", "Under_14", 72.1062851f, 6.71430731f, 115f, 25f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Unlock: Quickbind", "Ward_03", 96.49f, 32.78f, 240f, 52f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Unlock: Dazzle Bind", "Library_13", 73.61f, 13.23f, 124f, 56f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Dark Mirror", "Library_13", 73.76f, 13.23f, 124f, 56f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Unlock: Silk Snare", "Weave_14", 66.53f, 6.716298f, 76f, 19f, MapMarkerPositionConfidence.ExactUpstream),
            // The Moss Creep lives in the Mosstown_02c interior, but the
            // vanilla map anchors Hornet and that interior's entrance at the
            // right-hand Mosstown_02 transition. That visible map anchor keeps
            // so the check marker agrees with the in-game Hornet-head marker.
            new MapCheckPosition("Druid's Eye", "Mosstown_02", 157.5f, 34f, 160f, 65f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Druid's Eyes", "Mosstown_02", 157.5f, 34f, 160f, 65f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Unlock: Curve Claws", "Ant_21", 44.78f, 76.93f, 138f, 89f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Curvesickle", "Bone_East_22", 46.03f, 4.81f, 59f, 35f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Unlock: Fractured Mask", "Ant_Merchant", 20.9f, 15.19f, 152f, 30f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Unlock: Lightning Rod", "Arborium_07", 154.27f, 10.54f, 180f, 22f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Unlock: Revenge Crystal", "Bellway_Peak_02", 94.1f, 9.5f, 101f, 37f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Unlock: Rosary Cannon", "Hang_06_bank", 10.7113f, 10.2359f, 129f, 60f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Unlock: Weighted Anklet", "Bone_East_10_Room", 44.099998f, 13.850000f, 155f, 50f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Unlock: White Ring", "Weave_03", 14.42f, 19.34f, 284f, 35f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Unlock: Zap Imbuement", "Coral_29", 175.7869f, 23.21f, 302f, 92f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Ruined Tool", "Shadow_Weavehome", 76.642f, 54.577f, 177f, 68f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Pickup: Weaver Totem Witch", "Shellwood_25", 281.47f, 26.2774963f, 290f, 40f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Pickup: Psalm Cylinder Library Roof", "Library_09", 25.5419712f, 12.2116928f, 160f, 106f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Pickup: Bone Record Wisp Top", "Wisp_08", 10.18f, 114.573372f, 30f, 125f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Pickup: Weaver Totem Bonetown_upper_room", "Bonetown", 132.73f, 62.52f, 315f, 90f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Pickup: Librarian Melody Cylinder", "Library_10", 14.18f, 3.87f, 150f, 30f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Pickup: Psalm Cylinder Ward", "Under_08", 67.42f, 47.48f, 135f, 58f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Pickup: Psalm Cylinder Librarian", "Library_08", 93.69f, 30.2774963f, 115f, 40f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Pickup: Psalm Cylinder Hang", "Hang_10", 72.65701f, 15.277503f, 92f, 48f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Pickup: Seal Chit Ward Corpse", "Ward_02b", 51.1199722f, 3.97160482f, 151f, 31f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Pickup: Weaver Record Sprint_Challenge", "Bone_East_Weavehome", 51.32f, 63.3246346f, 205f, 120f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Pickup: Weaver Record Weave_08", "Weave_08", 41.6772652f, 54.1253242f, 77f, 67f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Pickup: Bone Record Understore_Map_Room", "Under_16", 71.36f, 15.2774982f, 80f, 60f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Pickup: Bone Record Bone_East_14", "Bone_East_14", 42.2058868f, 40.2774925f, 140f, 80f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Pickup: Seal Chit Aspid_01", "Aspid_01", 63.31f, 65.27748f, 80f, 273f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Pickup: Seal Chit Silk Siphon", "Ward_05", 133.04f, 4.27749872f, 140f, 23f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Pickup: Bone Record Greymoor_flooded_corridor", "Greymoor_21", 72.17f, 8.28f, 80f, 25f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Pickup: Weaver Totem Slab_Bottom", "Slab_12", 98.51f, 27.28058f, 110f, 40f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Pickup: Ancient Egg Abyss Middle", "Abyss_04", 98.2433548f, 50.34f, 115f, 81f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Pickup: Weaver Record Conductor", "Hang_12", 15.21f, 4.91f, 64f, 19f, MapMarkerPositionConfidence.ExactUpstream),

            // Belltown_Room_Relic has no GameMapScene of its own. Anchor all
            // of Scrounge's turn-ins at the exterior door4 transition.
            // Identical coordinates produce one grouped marker.
            new MapCheckPosition("Relic Turn-in: Weaver Effigy (Keelal, Shellwood)", "Belltown", 63.138689f, 24.92f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Turn-in: Bone Scroll (Wisp Thicket)", "Belltown", 63.138689f, 24.92f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Turn-in: Weaver Effigy (Camora, Moss Grotto)", "Belltown", 63.138689f, 24.92f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Turn-in: Choral Commandment (Jubilana)", "Belltown", 63.138689f, 24.92f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Turn-in: Rune Harp (High Halls)", "Belltown", 63.138689f, 24.92f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Turn-in: Choral Commandment (Western Whiteward)", "Belltown", 63.138689f, 24.92f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Turn-in: Rune Harp (Weavenest Cindril)", "Belltown", 63.138689f, 24.92f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Turn-in: Rune Harp (Weavenest Atla)", "Belltown", 63.138689f, 24.92f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Turn-in: Bone Scroll (Underworks)", "Belltown", 63.138689f, 24.92f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Turn-in: Bone Scroll (Far Fields)", "Belltown", 63.138689f, 24.92f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Turn-in: Choral Commandment (Moss Grotto)", "Belltown", 63.138689f, 24.92f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Turn-in: Choral Commandment (Eastern Whiteward)", "Belltown", 63.138689f, 24.92f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Turn-in: Bone Scroll (Greymoor)", "Belltown", 63.138689f, 24.92f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Turn-in: Weaver Effigy (Atla, The Slab)", "Belltown", 63.138689f, 24.92f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Turn-in: Arcane Egg", "Belltown", 63.138689f, 24.92f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),

            new MapCheckPosition("Relic Turn-in: Psalm Cylinder (East Whispering Vaults)", "Library_08", 29.4930458f, 11.7311459f, 115f, 40f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Turn-in: Sacred Cylinder", "Library_08", 29.4930458f, 11.7311459f, 115f, 40f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Turn-in: Psalm Cylinder (Underworks)", "Library_08", 29.4930458f, 11.7311459f, 115f, 40f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Turn-in: Psalm Cylinder (Vaultkeeper Cardinius)", "Library_08", 29.4930458f, 11.7311459f, 115f, 40f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Turn-in: Psalm Cylinder (High Halls)", "Library_08", 29.4930458f, 11.7311459f, 115f, 40f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic Turn-in: Psalm Cylinder (Grindle)", "Library_08", 29.4930458f, 11.7311459f, 115f, 40f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Bell Shrine Completion: bellShrineBoneForest", "Bellshrine", 20.5f, 17.29f, 43f, 25f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Bell Shrine Completion: bellShrineWilds", "Bellshrine_05", 20.5f, 17.29f, 43f, 25f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Bell Shrine Completion: bellShrineGreymoor", "Bellshrine_02", 20.5f, 17.29f, 43f, 25f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Bell Shrine Completion: bellShrineShellwood", "Bellshrine_03", 20.5f, 17.29f, 43f, 25f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Bell Shrine Completion: bellShrineBellhart", "Belltown_Shrine", 54.09f, 21.1f, 95f, 38f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Pale Oil: Whispering Vaults", "Library_03", 8.18f, 41.53f, 117f, 64f, MapMarkerPositionConfidence.ExactUpstream),
            // Fleatopia is an event-scene variant of Aqueduct_05. Its pickup
            // coordinates project safely against the base room's
            // dimensions, while avoiding a festival-only GameMapScene miss.
            new MapCheckPosition("Fleatopia - Rosary Necklace", "Aqueduct_05", 152.783f, 16.2396f, 332f, 100f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Fleatopia - Rosary String", "Aqueduct_05", 154.043f, 15.9796f, 332f, 100f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Pale Oil: Ecstasy of the End", "Aqueduct_05", 153.113f, 15.3196f, 332f, 100f, MapMarkerPositionConfidence.ExactUpstream),

            new MapCheckPosition("Sharpdart", "Crawl_05", 22.850002f, 16.25f, 225f, 35f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Rune Rage", "Slab_10b", 38.349998f, 9.46f, 60f, 30f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Pale Nails", "Cradle_03_Destroyed", 49.439999f, 133.399994f, 80f, 190f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Cross Stitch", "Organ_01", 77.440002f, 104.239998f, 160f, 229f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Pimpillo", "Wisp_06", 20.02f, 4.46f, 35f, 19f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Cogfly", "Hang_09", 71.100494f, 35.713406f, 114f, 48f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Silkshot (Original)", "Peak_12", 18.079f, 7.2077f, 50f, 22f, MapMarkerPositionConfidence.ExactUpstream),
            // Roach Guts is offered and turned in at Crull and Benjin before
            // Act 3. Their corpse pickup for an unfinished Wish is resolved
            // below so this marker always follows the current physical source.
            new MapCheckPosition("Tacks", "Dust_Shack", 22.413942f, 6.931353f, 38f, 30f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Pollip Pouch", "Room_Witch", 16.780001f, 8.36f, 33f, 19f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Needle Phial", "Crawl_08", 52.860001f, 10.161187f, 105f, 22f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Plasmium Phial", "Crawl_08", 52.860001f, 10.161187f, 105f, 22f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Pin Badge", "Peak_07", 38.049999f, 90.489998f, 115f, 150f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Silkshot (Forge Daughter)", "Room_Forge", 114.190002f, 34.540005f, 140f, 69f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Sting Shard", "Room_Forge", 114.190002f, 34.540005f, 140f, 69f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Magma Bell", "Room_Forge", 114.190002f, 34.540005f, 140f, 69f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Longclaw", "Room_Huntress", 24.24f, 10.43f, 35f, 25f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Cogwork Wheel", "Under_17", 76.820007f, 27.17063f, 164f, 48f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Sawtooth Circlet", "Under_17", 76.820007f, 27.17063f, 164f, 48f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Scuttlebrace", "Under_17", 76.820007f, 27.17063f, 164f, 48f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Silkshot (Twelfth Architect)", "Under_17", 76.820007f, 27.17063f, 164f, 48f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Magnetite Brooch", "Bonetown", 277.10223f, 8.04f, 315f, 90f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Multibinder", "Belltown", 55.43f, 7.88f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Thief's Mark", "Coral_42", 24.99f, 21.43f, 62f, 32f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Snitch Pick", "Coral_42", 24.99f, 21.43f, 62f, 32f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Throwing Ring", "Shadow_24", 270.890015f, 9.647703f, 296f, 34f, MapMarkerPositionConfidence.ExactUpstream),
            // Chapel interiors have no trustworthy room-shaped map bounds.
            // Each chapel uses its exterior transition as the marker anchor.
            new MapCheckPosition("Crest: Beast", "Ant_20", 183.736f, 32.79f, 222f, 48f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Crest: Wanderer", "Bonegrave", 197.97f, 6.73f, 315f, 82f, MapMarkerPositionConfidence.ExactUpstream),
            // The Crest is collected inside Greymoor_20c, whose map scene has
            // no bounds sprite. Anchor it to the centre of Greymoor_20b: the
            // visible Reaper chapel at the same far-west Greymoor entrance.
            new MapCheckPosition("Crest: Reaper", "Greymoor_20b", 25f, 10f, 50f, 20f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Crest: Architect", "Under_17", 26.26f, 34.7f, 164f, 48f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Crest: Shaman", "Tut_03", 10.006f, 16.857f, 126f, 35f, MapMarkerPositionConfidence.ExactUpstream),

            // Simple Key sources. The Bone Bottom check is anchored at Pebb,
            // its normal source. Grindle's Act 3 stock is a fallback for that
            // same AP check rather than a fifth independent acquisition.
            new MapCheckPosition("Simple Key: Bone Bottom Shop", "Bonetown", 277.102230f, 8.040000f, 315f, 90f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Simple Key: Songclave Shop", "Song_Enclave", 78.989998f, 8.140000f, 120f, 33f, MapMarkerPositionConfidence.ExactUpstream),

            // Fixed collectible and named-key sources. Vendor fallbacks and
            // the Volatile Flintbeetles Act 3 replacement are resolved below
            // so each canonical AP check has only one current-world marker.
            new MapCheckPosition("Memory Locket: Pilgrim's Rest Shop", "Bone_East_10_Room", 44.099998f, 13.850000f, 155f, 50f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Pouch: Pilgrim's Rest", "Bone_East_10_Room", 44.099998f, 13.850000f, 155f, 50f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Memory Locket: Hunter's March", "Ant_20", 147.390000f, 13.790000f, 222f, 48f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Memory Locket: Greymoor", "Greymoor_16", 130.900000f, 51.480000f, 171f, 60f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Memory Locket: Halfway Home", "Halfway_01", 8.292786f, 13.000000f, 56f, 32f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Memory Locket: Bellhart Shop", "Belltown", 55.430000f, 7.880000f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Memory Locket: Bellhart Roof", "Belltown", 59.168686f, 66.165000f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Memory Locket: Volatile Flintbeetles", "Bonetown", 285.650000f, 9.380000f, 315f, 90f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Memory Locket: The Marrow", "Bone_18", 38.150000f, 20.510000f, 49f, 33f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Memory Locket: Choral Chambers", "Bellway_City", 67.570000f, 24.290000f, 112f, 36f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Memory Locket: Wormways", "Crawl_09", 130.490000f, 3.340000f, 150f, 58f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Memory Locket: Blasted Steps", "Coral_02", 202.320000f, 43.560000f, 245f, 80f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Memory Locket: Underworks", "Under_08", 61.070000f, 16.470000f, 135f, 58f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Memory Locket: Whispering Vaults", "Library_08", 105.340000f, 33.480000f, 115f, 40f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Memory Locket: Bilewater West", "Shadow_20", 17.713000f, 23.860000f, 230f, 65f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Memory Locket: Deep Docks", "Dock_13", 15.260000f, 3.290000f, 35f, 76f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Memory Locket: Bilewater East", "Shadow_27", 195.090000f, 11.760000f, 205f, 25f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Memory Locket: The Slab", "Slab_Cell_Quiet", 42.441864f, 30.400000f, 53f, 60f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Memory Locket: Memorium", "Arborium_05", 3.950000f, 7.270000f, 132f, 29f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Memory Locket: Far Fields (Act 3)", "Bone_East_25", 151.952515f, 6.346909f, 157f, 27f, MapMarkerPositionConfidence.ExactUpstream),
            // Coral_23 mirrors its pickup root on X. The composed upstream
            // PositionInScene places it in the upper-left nook below Watcher.
            new MapCheckPosition("Memory Locket: Sands of Karak", "Coral_23", 90.7400055f, 50.31117f, 316f, 102f, MapMarkerPositionConfidence.ExactUpstream),

            new MapCheckPosition("Craftmetal: Bone Bottom Shop", "Bonetown", 277.102230f, 8.040000f, 315f, 90f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Craftmetal: The Marrow", "Bone_07", 41.310000f, 4.980000f, 92f, 74f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Craftmetal: Deep Docks", "Dock_03", 7.700000f, 81.890000f, 168f, 120f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Craftmetal: Blasted Steps", "Coral_32", 74.510000f, 9.060000f, 170f, 115f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Craftmetal: Underworks", "Under_19b", 7.570000f, 7.180000f, 135f, 22f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Craftmetal: Putrified Ducts", "Aqueduct_05", 328.900000f, 16.350000f, 332f, 100f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Craftmetal: Wisp Thicket", "Wisp_05", 46.406150f, 59.935950f, 57f, 69f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Craftmetal: Songclave Shop", "Song_Enclave", 78.990000f, 8.140000f, 120f, 33f, MapMarkerPositionConfidence.ExactUpstream),

            new MapCheckPosition("Mossberry: Moss Grotto #1", "Tut_01b", 87.787000f, 93.797000f, 160f, 99f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Mossberry: Moss Grotto #2", "Tut_02", 138.147000f, 53.087000f, 150f, 59f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Mossberry: Bone Bottom", "Bonetown", 282.340000f, 49.300000f, 315f, 90f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Mossberry: Mosshome", "Bone_05b", 60.180000f, 25.080000f, 80f, 30f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Mossberry: Bonegrave", "Bonegrave", 252.640000f, 42.330000f, 315f, 82f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Mossberry: Weavenest Atla", "Weave_03", 164.057000f, 30.487000f, 284f, 35f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Mossberry: Memorium", "Arborium_04", 69.597000f, 14.627000f, 132f, 34f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Pollip Heart: Shellwood #1", "Shellwood_02", 75.529994f, 79.839998f, 80f, 102f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Pollip Heart: Shellwood #2", "Shellwood_20", 42.569998f, 34.749999f, 95f, 43f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Pollip Heart: Shellwood #3", "Shellwood_10", 9.519998f, 27.259996f, 79f, 102f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Pollip Heart: Shellwood #4", "Shellwood_26", 25.529401f, 84.396917f, 135f, 130f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Pollip Heart: Shellwood #5", "Shellwood_15", 13.329997f, 6.369997f, 135f, 25f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Pollip Heart: Shellwood #6", "Shellwood_01", 92.295070f, 83.429197f, 132f, 98f, MapMarkerPositionConfidence.ExactUpstream),

            new MapCheckPosition("Silkeater: Deep Docks", "Dock_14", 23.340000f, 9.380000f, 29f, 17f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Silkeater: Greymoor", "Greymoor_04", 19.420000f, 145.690000f, 40f, 170f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Silkeater: Blasted Steps", "Coral_37", 19.110069f, 12.700000f, 80f, 21f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Silkeater: Exhaust Organ", "Organ_01", 135.530000f, 40.370000f, 160f, 229f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Silkeater: Choral Chambers West", "Song_24", 52.990000f, 21.530000f, 61f, 55f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Silkeater: Choral Chambers East", "Song_09b", 151.970000f, 139.340000f, 165f, 146f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Silkeater: Whispering Vaults", "Library_14", 54.510000f, 17.260000f, 65f, 30f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Silkeater: Whiteward", "Ward_04", 24.196329f, 22.264452f, 35f, 28f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Silkeater: The Cradle", "Tube_Hub", 20.350000f, 8.780000f, 135f, 145f, MapMarkerPositionConfidence.ExactUpstream),

            new MapCheckPosition("Key of Apostate", "Aqueduct_04", 7.570004f, 38.651804f, 185f, 60f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("White Key", "Song_Enclave", 102.076000f, 6.290000f, 120f, 33f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Surgeon's Key", "Ward_07", 13.134485f, 7.159246f, 130f, 20f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Architect's Key", "Under_17", 76.831480f, 27.930000f, 164f, 48f, MapMarkerPositionConfidence.ExactUpstream),
            // Craw Summons can trigger at multiple eligible benches. Anchor
            // the grouped check at its fixed Craw Lake/court doorway instead.
            new MapCheckPosition("Craw Summons", "Room_CrowCourt_02", 19.410000f, 44.277000f, 70f, 92f, MapMarkerPositionConfidence.ExactUpstream),

            // Shakra's authored NPC roots at the first shop where each
            // randomized purchase becomes available. Later encounters can
            // still sell missed stock. Each marker uses the first shop where
            // its product is available.
            new MapCheckPosition("Item: Quill", "Bone_04", 56.810001f, 14.640000f, 233f, 31f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Compass", "Bone_04", 56.810001f, 14.640000f, 233f, 31f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Purchase: Mosslands", "Bone_04", 56.810001f, 14.640000f, 233f, 31f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Purchase: The Marrow", "Bone_04", 56.810001f, 14.640000f, 233f, 31f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Purchase: Deep Docks", "Bone_East_01", 16.990000f, 8.700000f, 47f, 83f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Purchase: Far Fields", "Bone_East_21", 18.780001f, 5.807000f, 40f, 20f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Purchase: Wormways", "Crawl_01", 30.910000f, 58.430000f, 150f, 130f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Purchase: Hunter's March", "Ant_04_mid", 198.811740f, 15.244139f, 235f, 37f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Purchase: Greymoor", "Greymoor_02", 67.983109f, 5.863520f, 90f, 150f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Purchase: Bellhart", "Belltown", 102.260002f, 22.850000f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Purchase: Shellwood", "Shellwood_16", 44.360001f, 11.390000f, 85f, 25f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Purchase: Blasted Steps", "Coral_12", 67.790001f, 8.820000f, 94f, 78f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Purchase: Sinner's Road", "Dust_10", 112.309998f, 44.842865f, 160f, 80f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Purchase: Mount Fay", "Peak_02", 61.160000f, 9.640000f, 100f, 212f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Purchase: Sands of Karak", "Coral_40", 12.820000f, 9.900000f, 60f, 24f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Purchase: Bilewater", "Shadow_23", 35.570000f, 7.870000f, 52f, 19f, MapMarkerPositionConfidence.ExactUpstream),

            // Static map pickups and machines. Choral Chambers and the
            // Cradle each have two simultaneous sources for one canonical AP
            // check, so both anchors are listed.
            new MapCheckPosition("Map Pickup: Weavenest Atla", "Weave_12", 14.018001f, 11.054f, 55f, 25f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Purchase: Grand Gate", "Song_19_entrance", 40.099998f, 7.008f, 75f, 109f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Pickup: Underworks", "Under_16", 65.095837f, 35.326239f, 80f, 60f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Purchase: Choral Chambers", "Bellway_City", 102.589996f, 10.04f, 112f, 36f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Purchase: Choral Chambers", "Song_01b", 102.720039f, 2.859291f, 124f, 19f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Purchase: Whispering Vaults", "Library_04", 10.27f, 205.830002f, 67f, 220f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Purchase: Whiteward", "Ward_01", 54.509998f, 19.98f, 75f, 111f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Pickup: Cogwork Core", "Cog_Bench", 26.264f, 29.038498f, 45f, 40f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Purchase: Memorium", "Arborium_11", 13.35f, 11.16f, 222f, 61f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Purchase: High Halls", "Hang_06b", 51f, 3.067242f, 62f, 22f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Pickup: The Slab", "Slab_20", 86.57f, 14.07f, 94f, 24f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Pickup: Putrified Ducts", "Aqueduct_07", 28.93f, 28.98f, 41f, 40f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Purchase: The Cradle", "Cradle_02", 54.080002f, 67.167824f, 81f, 110f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Purchase: The Cradle", "Tube_Hub", 27.035999f, 29.396001f, 135f, 145f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Pickup: Verdania", "Clover_20", 133.55f, 16.2774963f, 150f, 47f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Map Pickup: The Abyss", "Abyss_12", 13.940001f, 26.83f, 33f, 40f, MapMarkerPositionConfidence.ExactUpstream),

            new MapCheckPosition("Pin Purchase: Bench Pins", "Bone_04", 56.810001f, 14.640000f, 233f, 31f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Pin Purchase: Bellway Pins", "Bone_04", 56.810001f, 14.640000f, 233f, 31f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Pin Purchase: Vendor Pins", "Bone_East_01", 16.990000f, 8.700000f, 47f, 83f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Pin Purchase: Ventrica Pins", "Belltown", 102.260002f, 22.850000f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),

            // Vendor and reward-giver roots for the remaining tools,
            // relics, crest and progressive crafting-kit sources.
            new MapCheckPosition("Spider Strings", "Song_Enclave", 78.989998f, 8.140000f, 120f, 33f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Ascendant's Grip", "Song_Enclave", 78.989998f, 8.140000f, 120f, 33f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Spool Extender", "Song_Enclave", 78.989998f, 8.140000f, 120f, 33f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Reserve Bind", "Coral_42", 24.990000f, 21.430000f, 62f, 32f, MapMarkerPositionConfidence.ExactUpstream),
            // Greymoor_08_caravan and Aqueduct_05_caravan are event-state
            // layers authored in their visible base scenes' coordinates.
            new MapCheckPosition("Flea Brew", "Greymoor_08", 37.950001f, 5.030001f, 161f, 38f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Egg of Flealia", "Aqueduct_05", 123.043003f, 9.847813f, 332f, 100f, MapMarkerPositionConfidence.ExactUpstream),
            // The Doctor's interior has no GameMapScene. Its authored
            // entrance in Wisp_03 is the visible map source for this reward.
            new MapCheckPosition("Crest: Witch", "Wisp_03", 50.790268f, 5.916079f, 179f, 47f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic: Choral Commandment (Jubilana)", "Song_Enclave", 78.989998f, 8.140000f, 120f, 33f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Relic: Psalm Cylinder (Grindle)", "Coral_42", 24.990000f, 21.430000f, 62f, 32f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Crafting Kit: Forge Daughter", "Room_Forge", 114.190002f, 34.540005f, 140f, 69f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Crafting Kit: Twelfth Architect", "Under_17", 76.831480f, 27.930002f, 164f, 48f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Crafting Kit: Grindle", "Coral_42", 24.990000f, 21.430000f, 62f, 32f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Crafting Kit: Crawbug Clearing (Creige)", "Belltown", 48.457670f, 5.752009f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),

            new MapCheckPosition("Mask Shard: Pebb (Bone Bottom) / Grindle (Act 3)", "Bonetown", 277.102230f, 8.040000f, 315f, 90f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Mask Shard: Wormways", "Crawl_02", 22.231581f, 4.839001f, 30f, 150f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Mask Shard: Far Fields (Above the Seamstress)", "Bone_East_20", 95.129998f, 12.184626f, 150f, 33f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Mask Shard: Shellwood", "Shellwood_14", 127.889999f, 12.671935f, 135f, 19f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Mask Shard: The Marrow - Deep Docks Passage", "Dock_08", 50.169998f, 22.040001f, 110f, 35f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Mask Shard: Weavenest Atla", "Weave_05b", 159.067291f, 28.709999f, 221f, 43f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Mask Shard: Savage Beastfly", "Belltown", 48.457670f, 5.752009f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Mask Shard: Cogwork Core", "Song_09", 41.560001f, 43.854599f, 50f, 85f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Mask Shard: Whispering Vaults", "Library_05", 16.839999f, 71.220003f, 49f, 79f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Mask Shard: Bilewater", "Shadow_13", 309.273376f, 63.556236f, 320f, 74f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Mask Shard: Far Fields (Skull Cave)", "Bone_East_LavaChallenge", 23.469999f, 277.850006f, 29f, 289f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Mask Shard: The Slab (Key of the Apostate)", "Slab_17", 19.240001f, 69.740002f, 81f, 77f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Mask Shard: Mount Fay", "Peak_04c", 67.629997f, 27.430000f, 105f, 48f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Mask Shard: Wisp Thicket", "Wisp_07", 251.410004f, 24.900000f, 290f, 39f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Mask Shard: Jubilana (Songclave)", "Song_Enclave", 78.989998f, 8.140000f, 120f, 33f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Mask Shard: Blasted Steps", "Coral_19b", 74.860001f, 109.019997f, 118f, 117f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Mask Shard: Fastest in Pharloom", "Sprintmaster_Cave", 88.930000f, 14.120000f, 236f, 51f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Mask Shard: The Hidden Hunter", "Belltown", 48.457670f, 5.752009f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Mask Shard: Dark Hearts", "Belltown", 48.457670f, 5.752009f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Mask Shard: Brightvein", "Peak_06", 28.120001f, 226.300003f, 51f, 233f, MapMarkerPositionConfidence.ExactUpstream),

            new MapCheckPosition("Spool Fragment: Bone Bottom", "Bone_11b", 31.370001f, 7.241133f, 52f, 18f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Spool Fragment: Deep Docks (Central)", "Bone_East_13", 41.740002f, 26.615089f, 128f, 35f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Spool Fragment: Greymoor", "Greymoor_02", 30.440001f, 138.690002f, 90f, 150f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Spool Fragment: The Slab", "Peak_01", 84.440002f, 192.710007f, 100f, 300f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Spool Fragment: Weavenest Atla", "Weave_11", 13.300000f, 15.090000f, 130f, 32f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Spool Fragment: Frey (Bellhart)", "Belltown", 55.430000f, 7.880000f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),
            // Greymoor_08_caravan is an event-state version of the visible
            // Greymoor_08 map scene and uses the same authored coordinates.
            new MapCheckPosition("Spool Fragment: Flea Caravan", "Greymoor_08", 37.950001f, 5.030001f, 161f, 38f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Spool Fragment: Cogwork Core", "Cog_07", 83.570000f, 16.280001f, 90f, 87f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Spool Fragment: Underworks (East)", "Library_11b", 23.080000f, 7.950000f, 115f, 158f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Spool Fragment: Grand Gate", "Song_19_entrance", 21.120001f, 94.900002f, 75f, 109f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Spool Fragment: Underworks (Gauntlet)", "Under_10", 22.219999f, 9.430000f, 80f, 20f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Spool Fragment: Whiteward", "Ward_01", 28.696068f, 7.392422f, 75f, 111f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Spool Fragment: Balm for the Wounded", "Ward_09", 60.680000f, 5.330000f, 150f, 18f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Spool Fragment: Deep Docks (Southeast)", "Dock_03c", 131.059998f, 55.950001f, 163f, 80f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Spool Fragment: High Halls", "Hang_03_top", 14.790000f, 160.619995f, 30f, 170f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Spool Fragment: Memorium", "Arborium_09", 19.040001f, 33.070000f, 66f, 112f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Spool Fragment: Grindle (Blasted Steps)", "Coral_42", 24.990000f, 21.430000f, 62f, 32f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Spool Fragment: Jubilana (Songclave)", "Song_Enclave", 78.989998f, 8.140000f, 120f, 33f, MapMarkerPositionConfidence.ExactUpstream),

            // Moss Mother's own encounter FSM carries defeatedMossMother at
            // this authored transform in the visible Tut_03 map scene.
            new MapCheckPosition("Boss: Moss Mother", "Tut_03", 54.773903f, 25.76f, 126f, 35f, MapMarkerPositionConfidence.ExactUpstream),
            // Bone_05_boss is an additive fight scene authored in the visible
            // Bone_05 world coordinate system. The encounter trigger and the
            // post-fight Silk Heart therefore project through Bone_05 without
            // inventing a separate hidden map room.
            new MapCheckPosition("Boss: Bell Beast", "Bone_05", 93.285164f, 7.283544f, 201f, 22f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Silk Heart: Bell Beast", "Bone_05", 85.275162f, 8.813543f, 201f, 22f, MapMarkerPositionConfidence.ExactUpstream),
            // Bonetown_boss is an additive encounter scene without its own
            // GameMapScene. The live Skull Tyrant transform is authored in
            // Bonetown world coordinates, so project that point through
            // the visible Bonetown map scene.
            new MapCheckPosition("Boss: Skull Tyrant (Bone Bottom)", "Bonetown", 313.029999f, 9.82f, 315f, 90f, MapMarkerPositionConfidence.ExactUpstream),
            // Remaining boss checks use the boss root (or an
            // authored arena centre when the enemy is spawned dynamically).
            // Additive encounter scenes share the visible host room's world
            // coordinates, so their points project through that map room.
            new MapCheckPosition("Boss: Fourth Chorus", "Bone_East_08", 80.849998f, 7.460000f, 150f, 50f, MapMarkerPositionConfidence.ExactUpstream),
            // Signis and Gron are authored at 22.56/8.91 and 38.36/19.64.
            // Their shared check sits at the midpoint of the pair.
            new MapCheckPosition("Boss: Forebrothers Signis & Gron", "Dock_09", 30.460000f, 14.275000f, 77f, 35f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Boss: Moorwing", "Greymoor_05", 50.650002f, 41.599998f, 110f, 75f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Boss: Shrine Guardian Seth", "Shellwood_22", 104.800003f, 6.810000f, 182f, 28f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Boss: Disgraced Chef Lugoli", "Dust_Chef", 41.773548f, 49.950001f, 60f, 74f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Boss: Phantom", "Organ_01", 77.440002f, 104.239998f, 160f, 229f, MapMarkerPositionConfidence.ExactUpstream),
            // Groal is spawned below the room before wave six. The marker uses
            // the encounter FSM's immutable arena centre instead of that
            // off-map pre-spawn transform.
            new MapCheckPosition("Boss: Groal the Great", "Shadow_18", 60.770000f, 16.100000f, 117f, 45f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Boss: Watcher at the Edge", "Coral_39", 126.110001f, 8.440000f, 192f, 43f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Boss: Lace (Cradle)", "Song_Tower_01", 59.193787f, 100.517998f, 130f, 140f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Silk Heart: The Unravelled", "Ward_02", 50.759998f, 14.000000f, 150f, 90f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Silk Heart: Lace (Cradle)", "Song_Tower_01", 54.610001f, 106.970001f, 130f, 140f, MapMarkerPositionConfidence.ExactUpstream),
            // Wish checks are anchored where the completion/reward actually
            // happens, never at an objective enemy or the place a delivery is
            // accepted. Board wishes use the Turn In Effect Point.
            // NPC wishes use the actor whose FSM completes/stores the reward.
            // Bone Bottom Wishwall turn-ins use its bonebottom_quest_board root.
            new MapCheckPosition("Wish: Bone Bottom Repairs", "Bonetown", 285.649990f, 9.380000f, 315f, 90f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Wish: A Lifesaving Bridge", "Bonetown", 285.649990f, 9.380000f, 315f, 90f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Wish: An Icon of Hope", "Bonetown", 285.649990f, 9.380000f, 315f, 90f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Wish: Garb of the Pilgrims", "Bonetown", 285.649990f, 9.380000f, 315f, 90f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Wish: Volatile Flintbeetles", "Bonetown", 285.649990f, 9.380000f, 315f, 90f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Wish: The Terrible Tyrant", "Bonetown", 285.649990f, 9.380000f, 315f, 90f, MapMarkerPositionConfidence.ExactUpstream),

            new MapCheckPosition("Wish: Restoration of Bellhart", "Belltown", 48.457670f, 5.752009f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Wish: Bellhart's Glory", "Belltown", 48.457670f, 5.752009f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Wish: Hero's Call", "Belltown", 48.457670f, 5.752009f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Wish: Silver Bells", "Belltown", 48.457670f, 5.752009f, 109f, 75f, MapMarkerPositionConfidence.ExactUpstream),

            // Songclave Wishwall turn-ins. Both normal and Act 3 boards have
            // the same authored Turn In Effect Point.
            new MapCheckPosition("Wish: Fine Pins", "Song_Enclave", 70.555757f, 4.579541f, 120f, 33f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Wish: Cloaks of the Choir", "Song_Enclave", 70.555757f, 4.579541f, 120f, 33f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Wish: Building Up Songclave", "Song_Enclave", 70.555757f, 4.579541f, 120f, 33f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Wish: Strengthening Songclave", "Song_Enclave", 70.555757f, 4.579541f, 120f, 33f, MapMarkerPositionConfidence.ExactUpstream),

            new MapCheckPosition("Wish: Pinmaster's Oil", "Belltown_Room_pinsmith", 25.809999f, 8.360000f, 55f, 26f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Wish: Advanced Alchemy", "Crawl_08", 52.860001f, 10.161187f, 105f, 22f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Wish: Bugs of Pharloom", "Halfway_01", 25.150000f, 20.850695f, 56f, 32f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Pouch: Bugs of Pharloom", "Halfway_01", 25.150000f, 20.850695f, 56f, 32f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Wish: Passing of the Age", "Aqueduct_05", 238.389999f, 84.800003f, 332f, 100f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Wish: The Wandering Merchant", "Song_07", 43.815227f, 5.911398f, 80f, 24f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Wish: The Lost Merchant", "Arborium_11", 129.820007f, 12.190000f, 222f, 61f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Wish: My Missing Courier", "Aspid_01", 27.020000f, 112.160004f, 80f, 273f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Wish: My Missing Brother", "Dust_04", 21.459999f, 69.480003f, 110f, 90f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Wish: Balm for the Wounded", "Ward_09", 60.680000f, 5.330000f, 150f, 18f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Wish: A Vassal Lost", "Coral_37", 65.670025f, 8.640000f, 80f, 21f, MapMarkerPositionConfidence.ExactUpstream),

            // Delivery wishes are checked at their recipients rather than
            // Tipp and Pill's Bellhart counter.
            new MapCheckPosition("Wish: Bone Bottom Supplies", "Bonetown", 290.819990f, 7.370000f, 315f, 90f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Wish: Queen's Egg", "Dust_11", 104.379997f, 9.720000f, 140f, 40f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Wish: Survivor's Camp Supplies", "Bone_10", 30.779999f, 15.329999f, 115f, 69f, MapMarkerPositionConfidence.ExactUpstream),
            // Fleatopia is an event variant of Aqueduct_05 and shares the
            // base room's world-coordinate system.
            new MapCheckPosition("Wish: Fleatopia Supplies", "Aqueduct_05", 123.043003f, 9.847813f, 332f, 100f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Tool Pouch: Fleatopia", "Aqueduct_05", 123.043003f, 9.847813f, 332f, 100f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Wish: Liquid Lacquer", "Peak_Mask_Maker", 13.680000f, 7.780000f, 33f, 30f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Wish: Pilgrim's Rest Supplies", "Bone_East_10_Room", 44.099998f, 13.850000f, 155f, 50f, MapMarkerPositionConfidence.ExactUpstream),
            // In Act 3 Sherma takes this delivery at nearly the same point.
            // The ordinary-world Caretaker is the first physical recipient.
            new MapCheckPosition("Wish: Songclave Supplies", "Song_Enclave", 59.919998f, 7.640000f, 120f, 33f, MapMarkerPositionConfidence.ExactUpstream),

            // Needle Strike is awarded by Pinstress. Anchor the check to her
            // authored standing NPC rather than a generic room centre.
            new MapCheckPosition("Needle Strike", "Room_Pinstress", 28.420000f, 8.804682f, 50f, 26f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Bellway: Deep Docks", "Bellway_02", 83.709999f, 16.146082f, 120f, 50f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Bellway: Far Fields", "Bellway_03", 69.339996f, 8.15f, 150f, 60f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Bellway: Greymoor", "Bellway_04", 69.459999f, 8.13f, 95f, 30f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Bellway: Bellhart", "Belltown_basement", 28.280001f, 95.239998f, 95f, 135f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Bellway: Blasted Steps", "Bellway_08", 94.31765f, 12.225822f, 150f, 33f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Bellway: Grand Bellway", "Bellway_City", 39.873043f, 10.18f, 112f, 36f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Bellway: The Slab", "Slab_06", 44.530001f, 5.35f, 101f, 35f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Bellway: Shellwood", "Shellwood_19", 57.939999f, 5.14f, 127f, 19f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Bellway: Bilewater", "Bellway_Shadow", 51.493725f, 21.130001f, 75f, 41f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Bellway: Putrified Ducts", "Bellway_Aqueduct", 51.493725f, 21.130001f, 96f, 78f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Ventrica: Choral Chambers", "Song_01b", 48.010056f, 6.577774f, 124f, 19f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Ventrica: Underworks", "Under_22", 66.209175f, 6.59f, 81f, 20f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Ventrica: Grand Bellway", "Bellway_City", 81.660004f, 13.62f, 112f, 36f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Ventrica: High Halls", "Hang_06b", 31.501278f, 6.61f, 62f, 22f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Ventrica: Songclave", "Song_Enclave_Tube", 16.076864f, 8.58f, 52f, 22f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Ventrica: Memorium", "Arborium_Tube", 16.076864f, 8.58f, 32f, 22f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Pinmaster Plinney: Sharpened Needle", "Belltown_Room_pinsmith", 25.809999f, 8.360000f, 55f, 26f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Pinmaster Plinney: Shining Needle", "Belltown_Room_pinsmith", 25.809999f, 8.360000f, 55f, 26f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Pinmaster Plinney: Hivesteel Needle", "Belltown_Room_pinsmith", 25.809999f, 8.360000f, 55f, 26f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Pinmaster Plinney: Pale Steel Needle", "Belltown_Room_pinsmith", 25.809999f, 8.360000f, 55f, 26f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Pale Oil: Great Taste of Pharloom", "Song_09b", 113.603394f, 133.169998f, 165f, 146f, MapMarkerPositionConfidence.ExactUpstream),
            // This reward is created at runtime after the two Cog_07 arena
            // enemies die. Their immutable authored spawn midpoint is the
            // stable physical source for the resulting core.
            new MapCheckPosition("Cogwork Core - Pristine Core", "Cog_07", 28.230000f, 77.690003f, 90f, 87f, MapMarkerPositionConfidence.ExactUpstream),

            new MapCheckPosition("Boss: Crawfather", "Room_CrowCourt_02", 32.389004f, 28.914000f, 70f, 92f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Boss: Sister Splinter", "Shellwood_18", 45.959999f, 16.879999f, 140f, 35f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Boss: Nyleth", "Shellwood_11b", 17.220000f, 149.440002f, 120f, 170f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Boss: Crust King Khann", "Coral_Tower_01", 80.000000f, 10.000000f, 138f, 22f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Boss: Great Conchflies", "Coral_11", 54.726971f, 25.630910f, 180f, 31f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Boss: Last Judge", "Coral_Judge_Arena", 31.799999f, 36.000000f, 62f, 42f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Boss: First Sinner", "Slab_10b", 38.349998f, 9.460000f, 60f, 30f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Boss: Broodmother", "Slab_16b", 54.000000f, 10.700001f, 80f, 32f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Boss: Trobbio", "Library_13", 73.760002f, 16.379999f, 124f, 56f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Boss: Tormented Trobbio", "Library_13", 77.059998f, 16.299999f, 124f, 56f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Boss: Cogwork Dancers", "Cog_Dancers", 49.789999f, 32.330000f, 80f, 45f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Boss: Second Sentinel", "Hang_17b", 30.220001f, 0.090000f, 55f, 21f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Boss: Palestag", "Clover_19", 31.100000f, 14.060000f, 100f, 30f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Boss: Clover Dancers", "Clover_01", 119.220001f, 8.160000f, 175f, 22f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Boss: Skarrsinger Karmelita", "Ant_Queen", 28.986334f, 22.675333f, 55f, 34f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Boss: Father of the Flame", "Belltown_08", 56.060001f, 11.540000f, 115f, 47f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Boss: Gurr the Outcast", "Bone_East_18b", 172.210007f, 8.889999f, 327f, 108f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Boss: Raging Conchfly", "Coral_27", 15.940002f, 26.670000f, 202f, 46f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Boss: The Unravelled", "Ward_02", 50.849998f, 8.280000f, 150f, 90f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Boss: Voltvyrm", "Coral_29", 173.176903f, 33.150002f, 302f, 92f, MapMarkerPositionConfidence.ExactUpstream),
            // Spinner's actor is authored above the visible room bounds.
            // This in-bounds Boss Scene anchor is the fight controller.
            new MapCheckPosition("Boss: Widow", "Belltown_Shrine", 60.820099f, 17.691548f, 95f, 38f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Boss: Skull Tyrant (The Marrow)", "Bone_15", 99.570000f, 15.640000f, 113f, 29f, MapMarkerPositionConfidence.ExactUpstream),

            // Quest markers describe where the player receives/turns in the
            // reward, never an objective site. Great Gourmand's servant ends
            // the quest and gives its reward at this transform. The
            // adjacent Pale Oil uses its own physical holder above. Other
            // wishes remain omitted until their reward/check event is located.
            new MapCheckPosition("Wish: Great Taste of Pharloom", "Song_09b", 109.64f, 132.79f, 165f, 146f, MapMarkerPositionConfidence.ExactUpstream),

            new MapCheckPosition("Save Flea: The Marrow (Tangle)", "Bone_06", 101.336548f, 24.435129f, 112f, 50f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Save Flea: Deep Docks Bellway (Eepy)", "Dock_16", 50.924f, 20.280001f, 75f, 39f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Save Flea: Deep Docks Weaver Burial Spire (Squeesh)", "Bone_East_05", 31.386938f, 38.563137f, 190f, 47f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Save Flea: Far Fields Captured (Thoughtless)", "Bone_East_17b", 86.11141f, 30.183959f, 115f, 64f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Save Flea: Hunter's March (Yapper)", "Ant_03", 43.299999f, 70.239998f, 50f, 78f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Save Flea: Greymoor Craw Lake (Gwah)", "Greymoor_15b", 178.990005f, 53.549999f, 220f, 141f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Save Flea: Greymoor Tower (Snoozles)", "Greymoor_06", 25.469999f, 139.119995f, 40f, 207f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Save Flea: Shellwood (Shelly)", "Shellwood_03", 24.209999f, 49.009998f, 40f, 135f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Save Flea: Pilgrim's Rest (Sleeby)", "Bone_East_10_Church", 181.938055f, 22.203874f, 200f, 30f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Save Flea: Blasted Steps (Nini)", "Coral_35", 6.47f, 143.089996f, 29f, 153f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Save Flea: Sinner's Road (Stelf)", "Dust_12", 26.1f, 3.58f, 44f, 16f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Save Flea: Exhaust Organ (Rangle)", "Dust_09", 5.336548f, 35.02f, 180f, 52f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Save Flea: Bellhart (Bellphy)", "Belltown_04", 10.85f, 89.68f, 85f, 97f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Save Flea: Wormways (Snacc)", "Crawl_06", 76.869331f, 28.110001f, 85f, 35f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Save Flea: The Slab Cell (Sway)", "Slab_13", 36f, 9.5f, 100f, 25f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Save Flea: Bilewater Thieves (Cower)", "Shadow_28", 30.76f, 21.719999f, 65f, 34f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Save Flea: Deep Docks Mines (Le Bomba)", "Dock_03d", 101.531937f, 68.146599f, 110f, 78f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Save Flea: Wisp Thicket (Fidget)", "Under_23", 18.23f, 23.401985f, 160f, 35f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Save Flea: Bilehaven (Spangle)", "Shadow_10", 79.639999f, 42.240002f, 210f, 70f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Save Flea: Choral Cambers Spa (Oowa)", "Song_14", 61.722489f, 8.61472f, 72f, 21f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Save Flea: Sands of Karak (Crustly)", "Coral_24", 36.43f, 46.07f, 183f, 55f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Save Flea: Mount Fay (Ice Cube)", "Peak_05c", 246.660004f, 115.870003f, 268f, 125f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Save Flea: Songclave (Groggy)", "Library_09", 126.790001f, 99.82f, 160f, 106f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Save Flea: Choral Cambers Walled (Mimi)", "Song_11", 50.280293f, 164.148178f, 60f, 188f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Save Flea: Whispering Vaults (Birdy)", "Library_01", 48.522492f, 88.60472f, 60f, 112f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Save Flea: Underworks (Boomy)", "Under_21", 9.34f, 15.69f, 87f, 26f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Save Flea: The Slab Bellway (Honk Shoo)", "Slab_06", 84.489998f, 28.08f, 101f, 35f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Flea: Greymoor - Kratt", "Greymoor_24", 76.089996f, 15.68f, 84f, 26f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Flea: Putrified Ducts - Vog", "Bellway_Aqueduct", 71.007004f, 63.120625f, 96f, 78f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition("Flea: Memorium - Huge Flea", "Arborium_08", 33.650002f, 34.549999f, 132f, 60f, MapMarkerPositionConfidence.ExactUpstream),
        };

        // Beastling Call is received from the roaming Bell Eater. Its reward
        // sequence runs in an additive arena that has no visible map scene,
        // so place the check at the Bell Centipede Loader in whichever
        // Bellway currently hosts that NPC.
        private static readonly MapCheckPosition[] BeastlingCallStations =
        {
            new MapCheckPosition(MelodyLocationManifest.BeastlingCall, "Bellway_01", 80.653992f, 8.015218f, 100f, 45f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition(MelodyLocationManifest.BeastlingCall, "Bellway_02", 73.190002f, 16.160000f, 120f, 50f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition(MelodyLocationManifest.BeastlingCall, "Bellway_03", 80.653992f, 8.015218f, 150f, 60f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition(MelodyLocationManifest.BeastlingCall, "Bellway_04", 80.653992f, 8.015218f, 95f, 30f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition(MelodyLocationManifest.BeastlingCall, "Belltown_basement", 18.190001f, 94.889999f, 95f, 135f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition(MelodyLocationManifest.BeastlingCall, "Bellway_08", 84.809998f, 12.860000f, 150f, 33f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition(MelodyLocationManifest.BeastlingCall, "Bellway_City", 49.799999f, 11.070000f, 112f, 36f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition(MelodyLocationManifest.BeastlingCall, "Slab_06", 35.189999f, 5.880000f, 101f, 35f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition(MelodyLocationManifest.BeastlingCall, "Shellwood_19", 67.730003f, 6.070000f, 127f, 19f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition(MelodyLocationManifest.BeastlingCall, "Bone_05", 97.099998f, 5.690000f, 201f, 22f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition(MelodyLocationManifest.BeastlingCall, "Bellway_Shadow", 62.799999f, 21.600000f, 75f, 41f, MapMarkerPositionConfidence.ExactUpstream),
            new MapCheckPosition(MelodyLocationManifest.BeastlingCall, "Bellway_Aqueduct", 62.880001f, 21.889999f, 96f, 78f, MapMarkerPositionConfidence.ExactUpstream),
        };

        private static readonly Dictionary<string, Vector2> DirectMapPositions =
            new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase)
            {
            };

        private static readonly Dictionary<string, Vector2> MinorSceneSizes =
            new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase)
            {
                { "ant_04_left", new Vector2(145f, 37f) },
                { "aqueduct_01", new Vector2(265f, 42f) },
                { "arborium_11", new Vector2(222f, 61f) },
                { "bone_10", new Vector2(115f, 69f) },
                { "bone_east_01", new Vector2(47f, 83f) },
                { "bone_east_04b", new Vector2(44f, 100f) },
                { "bone_east_14", new Vector2(140f, 80f) },
                { "bone_east_24", new Vector2(263f, 76f) },
                { "cog_07", new Vector2(90f, 87f) },
                { "cog_10", new Vector2(71f, 60f) },
                { "coral_03", new Vector2(60f, 215f) },
                { "coral_36", new Vector2(70f, 58f) },
                { "crawl_02", new Vector2(30f, 150f) },
                { "dock_02", new Vector2(125f, 101f) },
                { "dock_11", new Vector2(148f, 77f) },
                { "dust_01", new Vector2(155f, 27f) },
                { "dust_06", new Vector2(30f, 190f) },
                { "bellshrine_coral", new Vector2(43f, 40f) },
                { "greymoor_05", new Vector2(110f, 75f) },
                { "greymoor_12", new Vector2(180f, 35f) },
                { "greymoor_15", new Vector2(78f, 84f) },
                { "greymoor_15b", new Vector2(220f, 141f) },
                { "hang_06_bank", new Vector2(129f, 60f) },
                { "hang_16", new Vector2(104f, 30f) },
                { "library_02", new Vector2(120f, 64f) },
                { "library_09", new Vector2(160f, 106f) },
                { "library_12", new Vector2(136f, 116f) },
                { "mosstown_02", new Vector2(160f, 65f) },
                { "room_forge", new Vector2(140f, 69f) },
                { "shadow_02", new Vector2(70f, 207f) },
                { "shellwood_01", new Vector2(132f, 98f) },
                { "shellwood_01b", new Vector2(48f, 109f) },
                { "shellwood_13", new Vector2(141f, 80f) },
                { "shellwood_25", new Vector2(290f, 40f) },
                { "slab_02", new Vector2(125f, 31f) },
                { "slab_04", new Vector2(100f, 37f) },
                { "slab_18", new Vector2(100f, 76f) },
                { "slab_22", new Vector2(260f, 50f) },
                { "song_04", new Vector2(143f, 68f) },
                { "song_07", new Vector2(80f, 24f) },
                { "song_09", new Vector2(50f, 85f) },
                { "tut_01", new Vector2(120f, 120f) },
                { "under_03", new Vector2(60f, 25f) },
                { "under_07c", new Vector2(85f, 106f) },
                { "under_12", new Vector2(40f, 20f) },
                { "under_17", new Vector2(164f, 48f) },
                { "under_18", new Vector2(160f, 50f) },
                { "wisp_02", new Vector2(230f, 46f) },
                { "wisp_03", new Vector2(179f, 47f) },
            };

        // Every group below contains one cache category in one room. Its source
        // roots occupy one very tight (at most 5x5 world-unit) cluster. The
        // two Moss Grotto groups also appear as single visible pickup clusters.
        // The renderer only groups identical projected anchors, so assigning
        // the shared centroid here avoids a broad proximity rule accidentally
        // merging unrelated checks elsewhere in the room.
        private static readonly string[][] ExplicitMinorCacheMarkerGroups =
        {
            new[] { "Bellhart - Rosary Cache #1", "Bellhart - Rosary Cache #2" },
            new[] { "Bone Bottom - Rosary Cache #4", "Bone Bottom - Rosary Cache #5" },
            new[] { "Bone Bottom - Rosary Cache #6", "Bone Bottom - Rosary Cache #7" },
            new[] { "Bone Bottom - Rosary Cache #8", "Bone Bottom - Rosary Cache #9" },
            new[] { "Choral Chambers - Rosary Cache #1", "Choral Chambers - Rosary Cache #2" },
            new[] { "Choral Chambers - Rosary Cache #5", "Choral Chambers - Rosary Cache #6" },
            new[] { "Choral Chambers - Rosary Cache #8", "Choral Chambers - Rosary Cache #9" },
            new[] { "Choral Chambers - Rosary Cache #11", "Choral Chambers - Rosary Cache #12", "Choral Chambers - Rosary Cache #13" },
            new[] { "Choral Chambers - Rosary Cache #15", "Choral Chambers - Rosary Cache #16" },
            new[] { "Choral Chambers - Rosary Cache #17", "Choral Chambers - Rosary Cache #18", "Choral Chambers - Rosary Cache #19" },
            new[] { "Deep Docks - Rosary Cache #1", "Deep Docks - Rosary Cache #2" },
            new[] { "Deep Docks - Rosary Cache #5", "Deep Docks - Rosary Cache #6" },
            new[] { "Far Fields - Rosary Cache #3", "Far Fields - Rosary Cache #4" },
            new[] { "Far Fields - Rosary Cache #5", "Far Fields - Rosary Cache #6" },
            new[] { "Far Fields - Rosary Cache #7", "Far Fields - Rosary Cache #8" },
            new[] { "Far Fields - Rosary Cache #12", "Far Fields - Rosary Cache #13" },
            new[] { "Far Fields - Rosary Cache #14", "Far Fields - Rosary Cache #15" },
            new[] { "Far Fields - Rosary Cache #16", "Far Fields - Rosary Cache #17" },
            new[] { "Far Fields - Rosary Cache #20", "Far Fields - Rosary Cache #21", "Far Fields - Pale Rosary Necklace" },
            new[] { "Greymoor - Rosary Cache #2", "Greymoor - Rosary Cache #3" },
            new[] { "Greymoor - Rosary Cache #4", "Greymoor - Rosary Cache #5" },
            new[] { "Greymoor - Rosary Cache #7", "Greymoor - Rosary Cache #8" },
            new[] { "Greymoor - Rosary Cache #11", "Greymoor - Rosary Cache #12" },
            new[] { "Greymoor - Rosary Cache #15", "Greymoor - Rosary Cache #16" },
            new[] { "Greymoor - Rosary Cache #23", "Greymoor - Rosary Cache #24", "Greymoor - Rosary Cache #25" },
            new[] { "Greymoor - Rosary Cache #29", "Greymoor - Rosary Cache #30" },
            new[] { "Greymoor - Rosary Cache #32", "Greymoor - Rosary Cache #33" },
            new[] { "High Halls - Rosary Cache #1", "High Halls - Rosary Cache #2" },
            new[] { "High Halls - Rosary Cache #3", "High Halls - Rosary Cache #4" },
            new[] { "Hunter's March - Rosary Cache #1", "Hunter's March - Rosary Cache #2" },
            new[] { "Hunter's March - Rosary Cache #4", "Hunter's March - Rosary Cache #5" },
            new[] { "Hunter's March - Rosary Cache #6", "Hunter's March - Rosary Cache #7" },
            new[] { "Hunter's March - Rosary Cache #8", "Hunter's March - Rosary Cache #9" },
            new[] { "Mosshome - Rosary Cache #1", "Mosshome - Rosary Cache #2" },
            new[] { "Mosshome - Rosary Cache #3", "Mosshome - Rosary Cache #4" },
            new[] { "Mount Fay - Rosary Cache #1", "Mount Fay - Rosary Cache #2", "Mount Fay - Rosary Cache #3" },
            new[] { "Sinner's Road - Rosary Cache #2", "Sinner's Road - Rosary Cache #3" },
            new[] { "Sinner's Road - Rosary Cache #5", "Sinner's Road - Rosary Cache #6", "Sinner's Road - Rosary Cache #7" },
            new[] { "The Marrow - Rosary Cache #1", "The Marrow - Rosary Cache #2" },
            new[] { "The Marrow - Rosary Cache #3", "The Marrow - Rosary Cache #4" },
            new[] { "The Marrow - Rosary Cache #14", "The Marrow - Rosary Cache #15" },
            new[] { "The Slab - Rosary Cache #2", "The Slab - Rosary Cache #3" },
            new[] { "Underworks - Rosary Cache #2", "Underworks - Rosary Cache #3" },
            new[] { "Whispering Vaults - Rosary Cache #2", "Whispering Vaults - Rosary Cache #3" },
            new[] { "Whispering Vaults - Rosary Cache #6", "Whispering Vaults - Rosary Cache #7" },
            new[] { "Whiteward - Rosary Cache #1", "Whiteward - Rosary Cache #2" },
            new[] { "Blasted Steps - Shell Shard Cache #2", "Blasted Steps - Shell Shard Cache #3" },
            new[] { "Deep Docks - Shell Shard Cache #1", "Deep Docks - Shell Shard Cache #2" },
            new[] { "Deep Docks - Shell Shard Cache #6", "Deep Docks - Shell Shard Cache #7" },
            new[] { "Greymoor - Shell Shard Cache #1", "Greymoor - Shell Shard Cache #2" },
            new[] { "Greymoor - Shell Shard Cache #4", "Greymoor - Shell Shard Cache #5" },
            new[] { "Hunter's March - Shell Shard Cache #3", "Hunter's March - Shell Shard Cache #4" },
            new[] { "Mount Fay - Shell Shard Cache #1", "Mount Fay - Shell Shard Cache #2" },
            new[] { "Mount Fay - Shell Shard Cache #3", "Mount Fay - Shell Shard Cache #4" },
            new[] { "Moss Grotto - Shell Shard Cache #3", "Moss Grotto - Shell Shard Cache #4" },
            new[] { "Moss Grotto - Shell Shard Cache #5", "Moss Grotto - Shell Shard Cache #6", "Moss Grotto - Shell Shard Cache #7" },
            new[] { "Putrified Ducts - Shell Shard Cache #4", "Putrified Ducts - Shell Shard Cache #5" },
            new[] { "Putrified Ducts - Shell Shard Cache #9", "Putrified Ducts - Shell Shard Cache #10" },
            new[] { "Sands of Karak - Shell Shard Cache #5", "Sands of Karak - Shell Shard Cache #6" },
            new[] { "Shellwood - Shell Shard Cache #1", "Shellwood - Shell Shard Cache #2" },
            new[] { "Shellwood - Shell Shard Cache #4", "Shellwood - Shell Shard Cache #5", "Shellwood - Shell Shard Cache #6" },
            new[] { "Sinner's Road - Shell Shard Cache #4", "Sinner's Road - Shell Shard Cache #5" },
            new[] { "Sinner's Road - Shell Shard Cache #6", "Sinner's Road - Shell Shard Cache #7" },
            new[] { "The Marrow - Shell Shard Cache #2", "The Marrow - Shell Shard Cache #3" },
            new[] { "The Marrow - Shell Shard Cache #5", "The Marrow - Shell Shard Cache #6" },
            new[] { "The Slab - Shell Shard Cache #6", "The Slab - Shell Shard Cache #7" },
            new[] { "Underworks - Shell Shard Cache #7", "Underworks - Shell Shard Cache #8" },
            new[] { "Wisp Thicket - Shell Shard Cache #6", "Wisp Thicket - Shell Shard Cache #7" },
        };

        private static readonly Dictionary<string, Vector2>
            ExplicitMinorCacheGroupAnchors =
                BuildExplicitMinorCacheGroupAnchors();

        private static Dictionary<string, Vector2>
            BuildExplicitMinorCacheGroupAnchors()
        {
            Dictionary<string, MinorCacheManifest.Entry> entriesByName =
                new Dictionary<string, MinorCacheManifest.Entry>(
                    StringComparer.OrdinalIgnoreCase
                );
            foreach (MinorCacheManifest.Entry entry in
                     MinorCacheManifest.Entries)
            {
                entriesByName[entry.LocationName] = entry;
            }

            Dictionary<string, Vector2> anchors =
                new Dictionary<string, Vector2>(
                    StringComparer.OrdinalIgnoreCase
                );
            foreach (string[] group in ExplicitMinorCacheMarkerGroups)
            {
                float totalX = 0f;
                float totalY = 0f;
                string sceneName = null;
                bool valid = group != null && group.Length >= 2;
                if (valid)
                {
                    foreach (string locationName in group)
                    {
                        if (!entriesByName.TryGetValue(
                                locationName,
                                out MinorCacheManifest.Entry entry
                            ) ||
                            (sceneName != null &&
                             !string.Equals(
                                 sceneName,
                                 entry.SceneName,
                                 StringComparison.OrdinalIgnoreCase
                             )))
                        {
                            valid = false;
                            break;
                        }

                        sceneName = entry.SceneName;
                        totalX += entry.X;
                        totalY += entry.Y;
                    }
                }

                if (!valid)
                {
                    // An invalid future manifest edit leaves the affected
                    // checks at their source coordinates.
                    continue;
                }

                Vector2 anchor = new Vector2(
                    totalX / group.Length,
                    totalY / group.Length
                );
                foreach (string locationName in group)
                {
                    anchors[locationName] = anchor;
                }
            }

            return anchors;
        }

        internal static IEnumerable<MapCheckPosition> GetPositions()
        {
            foreach (MapCheckPosition position in StaticPositions)
            {
                yield return position;
            }

            foreach (MelodyLocationManifest.Entry melody in
                     MelodyLocationManifest.Entries)
            {
                if (string.Equals(
                        melody.LocationName,
                        MelodyLocationManifest.BeastlingCall,
                        StringComparison.OrdinalIgnoreCase
                    ))
                {
                    continue;
                }

                if (string.Equals(
                        melody.LocationName,
                        MelodyLocationManifest.ElegyOfTheDeep,
                        StringComparison.OrdinalIgnoreCase
                    ))
                {
                    // Elegy shares the Shaman chapel. Its physical dialogue
                    // remains in Tut_04. Only the marker uses the door.
                    yield return new MapCheckPosition(
                        melody.LocationName,
                        "Tut_03",
                        10.006f,
                        16.857f,
                        126f,
                        35f,
                        MapMarkerPositionConfidence.ExactUpstream
                    );
                    continue;
                }

                yield return new MapCheckPosition(
                    melody.LocationName,
                    melody.SceneName,
                    melody.X,
                    melody.Y,
                    melody.SceneWidth,
                    melody.SceneHeight,
                    MapMarkerPositionConfidence.ExactUpstream
                );
            }

            MapCheckPosition beastlingPosition =
                GetCurrentBeastlingCallPosition();
            if (beastlingPosition != null)
            {
                yield return beastlingPosition;
            }

            foreach (MinorPickupManifest.Entry minor in MinorPickupManifest.Entries)
            {
                // A zero coordinate in the source denotes a dynamically
                // spawned reward without a position. Unknown scene variants
                // are not assigned guessed dimensions.
                if ((minor.X == 0f && minor.Y == 0f) ||
                    !MinorSceneSizes.TryGetValue(
                        minor.SceneName,
                        out Vector2 sceneSize))
                {
                    continue;
                }

                yield return new MapCheckPosition(
                    minor.LocationName,
                    minor.SceneName,
                    minor.X,
                    minor.Y,
                    sceneSize.x,
                    sceneSize.y,
                    MapMarkerPositionConfidence.ExactUpstream
                );
            }

            foreach (MinorCacheManifest.Entry cache in MinorCacheManifest.Entries)
            {
                string markerScene = cache.SceneName;
                float markerX = cache.X;
                float markerY = cache.Y;
                float markerWidth = cache.SceneWidth;
                float markerHeight = cache.SceneHeight;
                if (string.Equals(
                        cache.SceneName,
                        "Chapel_Wanderer",
                        StringComparison.OrdinalIgnoreCase
                    ))
                {
                    // The four Bonegrave caches share the Wanderer chapel's
                    // exterior transition and one grouped map marker.
                    markerScene = "Bonegrave";
                    markerX = 197.97f;
                    markerY = 6.73f;
                    markerWidth = 315f;
                    markerHeight = 82f;
                }
                if (ExplicitMinorCacheGroupAnchors.TryGetValue(
                        cache.LocationName,
                        out Vector2 groupAnchor
                    ))
                {
                    markerX = groupAnchor.x;
                    markerY = groupAnchor.y;
                }
                if (
                    string.Equals(
                        cache.LocationName,
                        "Moss Grotto - Shell Shard Cache #3",
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    string.Equals(
                        cache.LocationName,
                        "Moss Grotto - Shell Shard Cache #4",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    // These two fragments belong to one tight breakable
                    // cluster. One shared map anchor keeps the marker
                    // readable while the tooltip lists both AP checks.
                    markerX = 144.875f;
                    markerY = 53.025f;
                }
                if (
                    string.Equals(
                        cache.LocationName,
                        "Moss Grotto - Shell Shard Cache #5",
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    string.Equals(
                        cache.LocationName,
                        "Moss Grotto - Shell Shard Cache #6",
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    string.Equals(
                        cache.LocationName,
                        "Moss Grotto - Shell Shard Cache #7",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    // The three fragments are another single visible cache
                    // cluster and share one tooltip marker.
                    markerX = 14.05f;
                    markerY = 35.263333f;
                }
                if (
                    string.Equals(
                        cache.LocationName,
                        "The Marrow (Mosslands Passage) - Rosary Cache #2",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    // The cache root is its ceiling suspension point. That root
                    // remains the runtime identity while the map marker sits at
                    // the centre of its four visible rosary bead groups.
                    markerY = 86.4946f;
                }
                if (
                    string.Equals(
                        cache.LocationName,
                        "Deep Docks - Rosary Cache #7",
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    string.Equals(
                        cache.LocationName,
                        "Deep Docks - Rosary Cache #8",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    // These two adjacent hanging strings are one visible cluster.
                    // Their distinct roots remain for runtime matching while both
                    // checks share one readable map marker.
                    markerX = 7.9868115f;
                    markerY = 28.3041935f;
                }

                yield return new MapCheckPosition(
                    cache.LocationName,
                    markerScene,
                    markerX,
                    markerY,
                    markerWidth,
                    markerHeight,
                    MapMarkerPositionConfidence.ExactUpstream
                );
            }

            foreach (LoreTabletManifest.Entry lore in
                     LoreTabletManifest.Entries)
            {
                yield return new MapCheckPosition(
                    lore.LocationName,
                    lore.MarkerSceneName,
                    lore.MarkerX,
                    lore.MarkerY,
                    lore.SceneWidth,
                    lore.SceneHeight,
                    MapMarkerPositionConfidence.ExactUpstream
                );
            }
        }

        private static MapCheckPosition GetCurrentBeastlingCallPosition()
        {
            FastTravelLocations location = PlayerData.instance == null
                ? FastTravelLocations.Bonetown
                : PlayerData.instance.FastTravelNPCLocation;
            if (location == FastTravelLocations.None)
            {
                location = FastTravelLocations.Bonetown;
            }

            string sceneName = FastTravelScenes.GetSceneName(location);
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return null;
            }

            foreach (MapCheckPosition station in BeastlingCallStations)
            {
                if (string.Equals(
                        station.SceneName,
                        sceneName,
                        StringComparison.OrdinalIgnoreCase
                    ))
                {
                    return station;
                }
            }

            return null;
        }

        internal static bool TryGetDirectMapPosition(
            string locationName,
            out Vector2 mapPosition
        )
        {
            return DirectMapPositions.TryGetValue(
                locationName ?? string.Empty,
                out mapPosition
            );
        }

        internal static MapCheckPosition ResolveCurrentWorldVariant(
            MapCheckPosition position
        )
        {
            if (position == null)
            {
                return null;
            }

            if (
                PlayerData.instance != null &&
                PlayerData.instance.mortKeptWeightedAnklet &&
                string.Equals(
                    position.LocationName,
                    "Tool Unlock: Weighted Anklet",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                // If Mort kept an unpurchased Weighted Anklet when Act 3
                // began, its physical source is his Greymoor corpse rather
                // than the ordinary Pilgrim's Rest shop.
                return new MapCheckPosition(
                    position.LocationName,
                    "Bone_East_07",
                    13.5f,
                    111.33f,
                    40f,
                    190f,
                    MapMarkerPositionConfidence.ExactUpstream
                );
            }

            if (
                PlayerData.instance != null &&
                PlayerData.instance.rhinoRuckus &&
                string.Equals(
                    position.LocationName,
                    "Beast Shard: Pilgrim's Rest",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                // The Pilgrim's Rest Rhinogrund is moved out of the church
                // during the native Rhino Ruckus state. Its relocated
                // actor carries the same Beast Shard reward and AP check.
                return new MapCheckPosition(
                    position.LocationName,
                    "Bone_East_10_Room",
                    46.938773f,
                    13.87f,
                    155f,
                    50f,
                    MapMarkerPositionConfidence.ExactUpstream
                );
            }

            if (
                PlayerData.instance != null &&
                PlayerData.instance.blackThreadWorld
            )
            {
                if (
                    string.Equals(
                        position.LocationName,
                        "Tacks",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    // Act 3 replaces the living Dust Traders with the free
                    // Tack pickup on their corpses in the same hut.
                    return new MapCheckPosition(
                        position.LocationName,
                        "Dust_Shack",
                        15.73441f,
                        5.286931f,
                        38f,
                        30f,
                        MapMarkerPositionConfidence.ExactUpstream
                    );
                }

                if (
                    string.Equals(
                        position.LocationName,
                        "Mask Shard: Pebb (Bone Bottom) / Grindle (Act 3)",
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    string.Equals(
                        position.LocationName,
                        "Simple Key: Bone Bottom Shop",
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    string.Equals(
                        position.LocationName,
                        "Memory Locket: Pilgrim's Rest Shop",
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    string.Equals(
                        position.LocationName,
                        "Craftmetal: Bone Bottom Shop",
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    string.Equals(
                        position.LocationName,
                        "Tool Pouch: Pilgrim's Rest",
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    string.Equals(
                        position.LocationName,
                        "Tool Unlock: Magnetite Dice",
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    string.Equals(
                        position.LocationName,
                        "Magnetite Brooch",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    // Unpurchased Pebb and Mort stock moves to Grindle's
                    // Blasted Steps shop when the Black Thread World begins.
                    return new MapCheckPosition(
                        position.LocationName,
                        "Coral_42",
                        24.990000f,
                        21.430000f,
                        62f,
                        32f,
                        MapMarkerPositionConfidence.ExactUpstream
                    );
                }

                if (
                    string.Equals(
                        position.LocationName,
                        "Tool Pouch: Loddie",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    // Loddie's Act 3 fallback is a direct pickup in the same
                    // room rather than the ordinary minigame reward actor.
                    return new MapCheckPosition(
                        position.LocationName,
                        "Bone_12",
                        23.04f,
                        5.35f,
                        58f,
                        43f,
                        MapMarkerPositionConfidence.ExactUpstream
                    );
                }

                if (
                    string.Equals(
                        position.LocationName,
                        "Mask Shard: Jubilana (Songclave)",
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    string.Equals(
                        position.LocationName,
                        "Spool Fragment: Jubilana (Songclave)",
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    string.Equals(
                        position.LocationName,
                        "Spider Strings",
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    string.Equals(
                        position.LocationName,
                        "Ascendant's Grip",
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    string.Equals(
                        position.LocationName,
                        "Spool Extender",
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    string.Equals(
                        position.LocationName,
                        "Relic: Choral Commandment (Jubilana)",
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    string.Equals(
                        position.LocationName,
                        "Simple Key: Songclave Shop",
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    string.Equals(
                        position.LocationName,
                        "Craftmetal: Songclave Shop",
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    string.Equals(
                        position.LocationName,
                        "White Key",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    // Jubilana moves farther right within Songclave in its
                    // Black Thread World state.
                    return new MapCheckPosition(
                        position.LocationName,
                        "Song_Enclave",
                        101.469997f,
                        7.586237f,
                        120f,
                        33f,
                        MapMarkerPositionConfidence.ExactUpstream
                    );
                }
            }

            if (
                (
                    string.Equals(
                        position.LocationName,
                        "Wish: Volatile Flintbeetles",
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    string.Equals(
                        position.LocationName,
                        "Memory Locket: Volatile Flintbeetles",
                        StringComparison.OrdinalIgnoreCase
                    )
                ) &&
                PlayerData.instance != null &&
                PlayerData.instance.blackThreadWorld
            )
            {
                // An unfinished Volatile Flintbeetles wish becomes its
                // Memory Locket ground fallback here when Act 3 starts.
                return new MapCheckPosition(
                    position.LocationName,
                    "Bone_10",
                    18.810000f,
                    16.249999f,
                    115f,
                    69f,
                    MapMarkerPositionConfidence.ExactUpstream
                );
            }

            return position;
        }
    }
}
