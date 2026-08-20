
using System;
using System.Collections.Concurrent;

namespace SilksongRandomizer
{
    public class ItemSet
    {
        internal const string ProgressiveSilkheartItemName =
            "Progressive Silkheart";

        private static readonly System.Collections.Generic.Dictionary<string, string>
            CurrentFleaDisplayNames =
                new System.Collections.Generic.Dictionary<string, string>(
                    System.StringComparer.OrdinalIgnoreCase)
                {
                    { "Flea: The Marrow", "Flea: The Marrow" },
                    {
                        "Flea: Deep Docks (Bellway)",
                        "Flea: Deep Docks - Bellway"
                    },
                    {
                        "Flea: Deep Docks (Weaver Burial Spire)",
                        "Flea: Deep Docks - Weaver Burial Spire"
                    },
                    {
                        "Flea: Far Fields (Captured)",
                        "Flea: Far Fields - Captured"
                    },
                    {
                        "Flea: Hunter's March",
                        "Flea: Hunter's March"
                    },
                    {
                        "Flea: Greymoor (Craw Lake)",
                        "Flea: Greymoor - Craw Lake"
                    },
                    {
                        "Flea: Greymoor (Tower)",
                        "Flea: Greymoor - Tower"
                    },
                    { "Flea: Shellwood", "Flea: Shellwood" },
                    {
                        "Flea: Pilgrim's Rest",
                        "Flea: Pilgrim's Rest"
                    },
                    {
                        "Flea: Blasted Steps",
                        "Flea: Blasted Steps"
                    },
                    {
                        "Flea: Sinner's Road",
                        "Flea: Sinner's Road"
                    },
                    {
                        "Flea: Exhaust Organ",
                        "Flea: Exhaust Organ"
                    },
                    { "Flea: Bellhart", "Flea: Bellhart" },
                    { "Flea: Wormways", "Flea: Wormways" },
                    {
                        "Flea: The Slab (Cell)",
                        "Flea: The Slab - Cell"
                    },
                    {
                        "Flea: Bilewater (Thieves)",
                        "Flea: Bilewater - Thieves"
                    },
                    {
                        "Flea: Deep Docks (Mines)",
                        "Flea: Deep Docks - Mines"
                    },
                    {
                        "Flea: Wisp Thicket",
                        "Flea: Underworks - Wisp Thicket Passage"
                    },
                    { "Flea: Bilehaven", "Flea: Bilehaven" },
                    {
                        "Flea: Choral Chambers (Spa)",
                        "Flea: Choral Chambers - Spa"
                    },
                    {
                        "Flea: Sands of Karak",
                        "Flea: Sands of Karak"
                    },
                    { "Flea: Mount Fay", "Flea: Mount Fay" },
                    { "Flea: Songclave", "Flea: Songclave" },
                    {
                        "Flea: Choral Chambers (Walled Room)",
                        "Flea: Choral Chambers - Walled Room"
                    },
                    {
                        "Flea: Whispering Vaults",
                        "Flea: Whispering Vaults"
                    },
                    { "Flea: Underworks", "Flea: Underworks" },
                    {
                        "Flea: The Slab (Bellway)",
                        "Flea: The Slab - Bellway"
                    },
                    {
                        "Flea: Greymoor (Kratt)",
                        "Flea: Greymoor - Kratt"
                    },
                    {
                        "Flea: Putrified Ducts (Vog)",
                        "Flea: Putrified Ducts - Vog"
                    },
                    {
                        "Flea: Memorium (Huge Flea)",
                        "Flea: Memorium - Huge Flea"
                    },
                };

        // Native game identifiers emitted by assets, FSMs and player data.
        // These are translated to the current Archipelago item identities.
        internal static readonly System.Collections.Generic.Dictionary<string, string>
            NativeItemNameAliases =
                new System.Collections.Generic.Dictionary<string, string>(
                    System.StringComparer.OrdinalIgnoreCase)
                {
                    { "Skill: Double Jump", "Ability: Faydown Cloak" },
                    { "Skill: Charge Slash", "Ability: Needle Strike" },
                    { "Skill: Silk Soar", "Ancestral Art: Silk Soar" },
                    { "Skill: Wall Jump", "Ancestral Art: Cling Grip" },
                    { "Skill: Dash", "Ancestral Art: Swift Step" },
                    { "Skill: Harpoon", "Ancestral Art: Clawline" },
                    { "Skill: Quill", "Item: Quill" },
                    { "Skill: Needolin", "Ancestral Art: Needolin" },
                    { "Spell: Silk Spear", "Silk Skill: Silkspear" },
                    { "Spell: Parry", "Silk Skill: Cross Stitch" },
                    { "Spell: Silk Boss Needle", "Silk Skill: Pale Nails" },
                    { "Spell: Silk Charge", "Silk Skill: Sharpdart" },
                    { "Spell: Silk Bomb", "Silk Skill: Rune Rage" },
                    { "Spell: Thread Sphere", "Silk Skill: Thread Storm" },
                    { "Flea: The Marrow (Tangle)", "Flea: The Marrow" },
                    {
                        "Flea: Deep Docks Bellway (Eepy)",
                        "Flea: Deep Docks (Bellway)"
                    },
                    {
                        "Flea: Deep Docks Weaver Burial Spire (Squeesh)",
                        "Flea: Deep Docks (Weaver Burial Spire)"
                    },
                    {
                        "Flea: Far Fields Captured (Thoughtless)",
                        "Flea: Far Fields (Captured)"
                    },
                    { "Flea: Hunter's March (Yapper)", "Flea: Hunter's March" },
                    {
                        "Flea: Greymoor Craw Lake (Gwah)",
                        "Flea: Greymoor (Craw Lake)"
                    },
                    {
                        "Flea: Greymoor Tower (Snoozles)",
                        "Flea: Greymoor (Tower)"
                    },
                    { "Flea: Shellwood (Shelly)", "Flea: Shellwood" },
                    { "Flea: Pilgrim's Rest (Sleeby)", "Flea: Pilgrim's Rest" },
                    { "Flea: Blasted Steps (Nini)", "Flea: Blasted Steps" },
                    { "Flea: Sinner's Road (Stelf)", "Flea: Sinner's Road" },
                    { "Flea: Exhaust Organ (Rangle)", "Flea: Exhaust Organ" },
                    { "Flea: Bellhart (Bellphy)", "Flea: Bellhart" },
                    { "Flea: Wormways (Snacc)", "Flea: Wormways" },
                    { "Flea: The Slab Cell (Sway)", "Flea: The Slab (Cell)" },
                    {
                        "Flea: Bilewater Thieves (Cower)",
                        "Flea: Bilewater (Thieves)"
                    },
                    {
                        "Flea: Deep Docks Mines (Le Bomba)",
                        "Flea: Deep Docks (Mines)"
                    },
                    { "Flea: Wisp Thicket (Fidget)", "Flea: Wisp Thicket" },
                    { "Flea: Bilehaven (Spangle)", "Flea: Bilehaven" },
                    {
                        "Flea: Choral Cambers Spa (Oowa)",
                        "Flea: Choral Chambers (Spa)"
                    },
                    {
                        "Flea: Sands of Karak (Crustly)",
                        "Flea: Sands of Karak"
                    },
                    { "Flea: Mount Fay (Ice Cube)", "Flea: Mount Fay" },
                    { "Flea: Songclave (Groggy)", "Flea: Songclave" },
                    {
                        "Flea: Choral Cambers Walled (Mimi)",
                        "Flea: Choral Chambers (Walled Room)"
                    },
                    {
                        "Flea: Whispering Vaults (Birdy)",
                        "Flea: Whispering Vaults"
                    },
                    { "Flea: Underworks (Boomy)", "Flea: Underworks" },
                    {
                        "Flea: The Slab Bellway (Honk Shoo)",
                        "Flea: The Slab (Bellway)"
                    },
                    {
                        "Hunter Slot: Red 0 -2",
                        "Crest Slot: Hunter (Red 1)"
                    },
                    {
                        "Hunter Slot: Blue -2 0",
                        "Crest Slot: Hunter (Blue 1)"
                    },
                    {
                        "Hunter Slot: Yellow 2 0",
                        "Crest Slot: Hunter (Yellow 1)"
                    },
                    {
                        "Reaper Slot: Red 0 -1",
                        "Crest Slot: Reaper (Red 1)"
                    },
                    {
                        "Reaper Slot: Blue -1 1",
                        "Crest Slot: Reaper (Blue 1)"
                    },
                    {
                        "Reaper Slot: Yellow 1 1",
                        "Crest Slot: Reaper (Yellow 1)"
                    },
                    {
                        "Wanderer Slot: Blue -2 1",
                        "Crest Slot: Wanderer (Blue 1)"
                    },
                    {
                        "Wanderer Slot: Blue 2 1",
                        "Crest Slot: Wanderer (Blue 2)"
                    },
                    {
                        "Wanderer Slot: Yellow 0 -2",
                        "Crest Slot: Wanderer (Yellow 1)"
                    },
                    {
                        "Beast Slot: Yellow -1 0",
                        "Crest Slot: Beast (Yellow 1)"
                    },
                    {
                        "Beast Slot: Yellow 1 0",
                        "Crest Slot: Beast (Yellow 2)"
                    },
                    {
                        "Witch Slot: Red 0 -2",
                        "Crest Slot: Witch (Red 1)"
                    },
                    {
                        "Witch Slot: Blue -1 0",
                        "Crest Slot: Witch (Blue 1)"
                    },
                    {
                        "Witch Slot: Blue 1 0",
                        "Crest Slot: Witch (Blue 2)"
                    },
                    {
                        "Architect Slot: Blue -1 2",
                        "Crest Slot: Architect (Blue 1)"
                    },
                    {
                        "Architect Slot: Yellow 1 2",
                        "Crest Slot: Architect (Yellow 1)"
                    },
                    {
                        "Architect Slot: Yellow 2 0",
                        "Crest Slot: Architect (Yellow 2)"
                    },
                    {
                        "Architect Slot: Blue -2 0",
                        "Crest Slot: Architect (Blue 2)"
                    },
                    {
                        "Shaman Slot: Blue -1 0",
                        "Crest Slot: Shaman (Blue 1)"
                    },
                    {
                        "Shaman Slot: Blue 1 0",
                        "Crest Slot: Shaman (Blue 2)"
                    },
                    {
                        "Tool: WebShot Forge",
                        "Tool: Silkshot (Forge Daughter)"
                    },
                    {
                        "Tool: WebShot Architect",
                        "Tool: Silkshot (Twelfth Architect)"
                    },
                    {
                        "Tool: WebShot Weaver",
                        "Tool: Silkshot (Original)"
                    },
                    { "Tool: Zap Imbuement", "Tool: Volt Filament" },
                    { "Tool: Tack", "Tool: Tacks" },
                    { "Tool: Poison Pouch", "Tool: Pollip Pouch" },
                    { "Tool: Silk Snare", "Tool: Snare Setter" },
                    { "Tool: Wisp Lantern", "Tool: Wispfire Lantern" },
                    { "Tool: Revenge Crystal", "Tool: Memory Crystal" },
                    { "Tool: Lightning Rod", "Tool: Voltvessels" },
                    { "Tool: Tri Pin", "Tool: Threefold Pin" },
                    { "Tool: Bell Bind", "Tool: Warding Bell" },
                    { "Tool: Harpoon", "Tool: Longpin" },
                    { "Tool: Brolly Spike", "Tool: Sawtooth Circlet" },
                    {
                        "Tool: Dazzle Bind",
                        "Progressive Claw Mirror"
                    },
                    {
                        "Tool: Dazzle Bind Upgraded",
                        "Progressive Claw Mirror"
                    },
                    { "Tool: Shakra Ring", "Tool: Throwing Ring" },
                    { "Tool: Screw Attack", "Tool: Delver's Drill" },
                    { "Tool: Musician Charm", "Tool: Spider Strings" },
                    {
                        "Tool: Sprintmaster",
                        "Tool: Silkspeed Anklets"
                    },
                    { "Tool: Weighted Anklet", "Tool: Weighted Belt" },
                    { "Tool: Multibind", "Tool: Multibinder" },
                    { "Tool: Quickbind", "Tool: Injector Band" },
                    { "Tool: Barbed Wire", "Tool: Barbed Bracelet" },
                    { "Tool: Conch Drill", "Tool: Conchcutter" },
                    { "Tool: Pimpilo", "Tool: Pimpillo" },
                    { "Tool: Cogwork Flier", "Tool: Cogfly" },
                    {
                        "Tool: Curve Claws",
                        "Progressive Curveclaw"
                    },
                    {
                        "Tool: Curve Claws Upgraded",
                        "Progressive Curveclaw"
                    },
                    { "Tool: Cogwork Saw", "Tool: Cogwork Wheel" },
                    {
                        "Tool: Rosary Magnet",
                        "Tool: Magnetite Brooch"
                    },
                    { "Tool: White Ring", "Tool: Weavelight" },
                    { "Tool: Pinstress Tool", "Tool: Pin Badge" },
                    { "Tool: Extractor", "Tool: Needle Phial" },
                    {
                        "Tool: Lifeblood Syringe",
                        "Tool: Plasmium Phial"
                    },
                    {
                        "Tool: Dead Mans Purse",
                        "Tool: Dead Bug's Purse"
                    },
                    { "Tool: Thief Charm", "Tool: Thief's Mark" },
                    { "Tool: Thief Claw", "Tool: Snitch Pick" },
                    {
                        "Tool: Mosscreep Tool 1",
                        "Progressive Druid's Eyes"
                    },
                    {
                        "Tool: Mosscreep Tool 2",
                        "Progressive Druid's Eyes"
                    },
                    { "Tool: Flintstone", "Tool: Flintslate" },
                    { "Tool: Maggot Charm", "Tool: Wreath of Purity" },
                    { "Tool: Lava Charm", "Tool: Magma Bell" },
                    { "Tool: Wallcling", "Tool: Ascendant's Grip" },
                    { "Tool: Longneedle", "Tool: Longclaw" },
                    { "Tool: Bone Necklace", "Tool: Shard Pendant" },
                    { "Tool: Flea Charm", "Tool: Egg of Flealia" },
                    { "Pin: Bench", "Bench Pins" },
                    { "Pin: Ventrica", "Ventrica Pins" },
                    { "Pin: Bellway", "Bellway Pins" },
                    { "Pin: Vendor", "Vendor Pins" },
                    {
                        "Relic: Weaver Totem Witch",
                        "Relic: Weaver Effigy (Keelal, Shellwood)"
                    },
                    {
                        "Relic: Psalm Cylinder Library Roof",
                        "Relic: Psalm Cylinder (East Whispering Vaults)"
                    },
                    {
                        "Relic: Bone Record Wisp Top",
                        "Relic: Bone Scroll (Wisp Thicket)"
                    },
                    {
                        "Relic: Weaver Totem Bonetown_upper_room",
                        "Relic: Weaver Effigy (Camora, Moss Grotto)"
                    },
                    {
                        "Relic: Librarian Melody Cylinder",
                        "Relic: Sacred Cylinder"
                    },
                    {
                        "Relic: Seal Chit City Merchant",
                        "Relic: Choral Commandment (Jubilana)"
                    },
                    {
                        "Relic: Psalm Cylinder Ward",
                        "Relic: Psalm Cylinder (Underworks)"
                    },
                    {
                        "Relic: Weaver Record Conductor",
                        "Relic: Rune Harp (High Halls)"
                    },
                    {
                        "Relic: Psalm Cylinder Librarian",
                        "Relic: Psalm Cylinder (Vaultkeeper Cardinius)"
                    },
                    {
                        "Relic: Psalm Cylinder Hang",
                        "Relic: Psalm Cylinder (High Halls)"
                    },
                    {
                        "Relic: Seal Chit Ward Corpse",
                        "Relic: Choral Commandment (Western Whiteward)"
                    },
                    {
                        "Relic: Weaver Record Sprint_Challenge",
                        "Relic: Rune Harp (Weavenest Cindril)"
                    },
                    {
                        "Relic: Psalm Cylinder Grindle",
                        "Relic: Psalm Cylinder (Grindle)"
                    },
                    {
                        "Relic: Weaver Record Weave_08",
                        "Relic: Rune Harp (Weavenest Atla)"
                    },
                    {
                        "Relic: Bone Record Understore_Map_Room",
                        "Relic: Bone Scroll (Underworks)"
                    },
                    {
                        "Relic: Bone Record Bone_East_14",
                        "Relic: Bone Scroll (Far Fields)"
                    },
                    {
                        "Relic: Seal Chit Aspid_01",
                        "Relic: Choral Commandment (Moss Grotto)"
                    },
                    {
                        "Relic: Seal Chit Silk Siphon",
                        "Relic: Choral Commandment (Eastern Whiteward)"
                    },
                    {
                        "Relic: Bone Record Greymoor_flooded_corridor",
                        "Relic: Bone Scroll (Greymoor)"
                    },
                    {
                        "Relic: Weaver Totem Slab_Bottom",
                        "Relic: Weaver Effigy (Atla, The Slab)"
                    },
                    {
                        "Relic: Ancient Egg Abyss Middle",
                        "Relic: Arcane Egg"
                    },
                };

        private static readonly string[] RedundantItemPrefixes =
        {
            "Ability: ",
            "Ancestral Art: ",
            "Tool: ",
            "Silk Skill: ",
        };

        private static readonly Func<string, string>
            CanonicalItemNameFactory = ResolveCanonicalItemName;
        private static readonly ConcurrentDictionary<string, string>
            CanonicalItemNames = new ConcurrentDictionary<string, string>();

        internal static string GetCanonicalItemName(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
            {
                return itemName;
            }

            return CanonicalItemNames.GetOrAdd(
                itemName,
                CanonicalItemNameFactory
            );
        }

        private static string ResolveCanonicalItemName(string itemName)
        {
            string canonicalName = NativeItemNameAliases.TryGetValue(
                itemName,
                out string renamedItem
            )
                ? renamedItem
                : itemName;

            if (CurrentFleaDisplayNames.TryGetValue(
                    canonicalName,
                    out string fleaDisplayName))
            {
                return fleaDisplayName;
            }

            foreach (string prefix in RedundantItemPrefixes)
            {
                if (canonicalName.StartsWith(
                        prefix,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    return canonicalName.Substring(prefix.Length);
                }
            }

            return canonicalName;
        }

        public Item[] items = LoreTabletManifest.AppendItems(new Item[]
        {
            // Skills
            new Item("Ability: Faydown Cloak", ItemType.Skill, ItemGrants.GrantFaydownCloak),
            new Item("Ability: Needle Strike", ItemType.Skill, () => { SaveState.Instance.canChargeSlash = true; }),
            new Item("Ancestral Art: Silk Soar", ItemType.Skill, () => { SaveState.Instance.canSilkSoar = true; }),
            new Item("Ancestral Art: Cling Grip", ItemType.Skill, ItemGrants.GrantClingGrip),
            new Item("Ability: Drifter's Cloak", ItemType.Skill, () => { SaveState.Instance.canBrolly = true; }),
            new Item("Ancestral Art: Swift Step", ItemType.Skill, ItemGrants.GrantDash),
            new Item("Progressive Swift Step", ItemType.Skill, ItemGrants.GrantProgressiveSwiftStep, true),
            new Item("Ancestral Art: Clawline", ItemType.Skill, () => { SaveState.Instance.canUseHarpoon = true; }),
            new Item("Item: Quill", ItemType.Skill, () => { SaveState.Instance.canUseQuill = true; }),
            new Item("Progressive Compass", ItemType.Skill, () => { SaveState.Instance.canUseQuill = true; }),
            new Item("Ancestral Art: Needolin", ItemType.Skill, () => { SaveState.Instance.canUseNeedolin = true; }),

            // Repeatable filler and progressive upgrades. Each copy is applied once
            // for its Archipelago received-item index.
            new Item("Rosaries (60)", ItemType.Currency, () => { ItemGrants.GrantRosaries(60); }, true),
            new Item("Shell Shards (80)", ItemType.Currency, () => { ItemGrants.GrantShellShards(80); }, true),
            new Item("Frayed Rosary String", ItemType.Resource, () => { ItemGrants.GrantCollectable("Rosary_Set_Frayed"); }, true),
            new Item("Rosary String", ItemType.Resource, () => { ItemGrants.GrantCollectable("Rosary_Set_Small"); }, true),
            new Item("Rosary Necklace", ItemType.Resource, () => { ItemGrants.GrantCollectable("Rosary_Set_Medium"); }, true),
            new Item("Heavy Rosary Necklace", ItemType.Resource, () => { ItemGrants.GrantCollectable("Rosary_Set_Large"); }, true),
            new Item("Pale Rosary Necklace", ItemType.Resource, () => { ItemGrants.GrantCollectable("Rosary_Set_Huge_White"); }, true),
            new Item("Shard Bundle", ItemType.Resource, () => { ItemGrants.GrantCollectable("Shard Pouch"); }, true),
            new Item("Beast Shard", ItemType.Resource, () => { ItemGrants.GrantCollectable("Great Shard"); }, true),
            new Item("Pristine Core", ItemType.Resource, () => { ItemGrants.GrantCollectable("Pristine Core"); }, true),
            new Item("Progressive Crafting Kit", ItemType.Upgrade, ItemGrants.GrantCraftingKitUpgrade, true),
            new Item("Progressive Tool Pouch", ItemType.ToolPouch, ItemGrants.GrantToolPouchUpgrade, true),
            new Item("Progressive Druid's Eyes", ItemType.Upgrade, ItemGrants.GrantProgressiveDruidsEye, true),
            new Item("Progressive Needle Upgrade", ItemType.NeedleUpgrade, ItemGrants.GrantProgressiveNeedleUpgrade, true),
            new Item("Pale Oil", ItemType.NeedleUpgrade, ItemGrants.GrantPaleOil, true),

            // Optional repeatable traps. The APWorld replaces an equal number
            // of currency fillers, so enabling these never changes pool size.
            new Item("Stagger Trap", ItemType.Trap, TrapManager.TriggerStagger, true),
            new Item("Rosary Spill Trap", ItemType.Trap, TrapManager.TriggerRosarySpill, true),
            new Item("Darkness Trap", ItemType.Trap, TrapManager.TriggerDarkness, true),
            new Item("Cursed Crest Trap", ItemType.Trap, TrapManager.TriggerCursedCrest, true),
            new Item("Muckmaggot Status Trap", ItemType.Trap, TrapManager.TriggerMuckmaggotStatus, true),
            new Item("Naked Trap", ItemType.Trap, NakedTrapManager.Trigger, true),

            // Shakra maps
            new Item("Map: Mosslands", ItemType.Map, () => { ItemGrants.GrantMap(pd => pd.HasMossGrottoMap = true); }),
            new Item("Map: The Marrow", ItemType.Map, () => { ItemGrants.GrantMap(pd => pd.HasBoneforestMap = true); }),
            new Item("Map: Deep Docks", ItemType.Map, () => { ItemGrants.GrantMap(pd => pd.HasDocksMap = true); }),
            new Item("Map: Far Fields", ItemType.Map, () => { ItemGrants.GrantMap(pd => pd.HasWildsMap = true); }),
            new Item("Map: Wormways", ItemType.Map, () => { ItemGrants.GrantMap(pd => pd.HasCrawlMap = true); }),
            new Item("Map: Hunter's March", ItemType.Map, () => { ItemGrants.GrantMap(pd => pd.HasHuntersNestMap = true); }),
            new Item("Map: Greymoor", ItemType.Map, () => { ItemGrants.GrantMap(pd => pd.HasGreymoorMap = true); }),
            new Item("Map: Bellhart", ItemType.Map, () => { ItemGrants.GrantMap(pd => pd.HasBellhartMap = true); }),
            new Item("Map: Shellwood", ItemType.Map, () => { ItemGrants.GrantMap(pd => pd.HasShellwoodMap = true); }),
            new Item("Map: Sands of Karak", ItemType.Map, () => { ItemGrants.GrantMap(pd => pd.HasCoralMap = true); }),
            new Item("Map: Sinner's Road", ItemType.Map, () => { ItemGrants.GrantMap(pd => pd.HasDustpensMap = true); }),
            new Item("Map: Mount Fay", ItemType.Map, () => { ItemGrants.GrantMap(pd => pd.HasPeakMap = true); }),
            new Item("Map: Blasted Steps", ItemType.Map, () => { ItemGrants.GrantMap(pd => pd.HasJudgeStepsMap = true); }),
            new Item("Map: Bilewater", ItemType.Map, () => { ItemGrants.GrantMap(pd => pd.HasSwampMap = true); }),

            // Maps found outside Shakra's stock. These grant only their map
            // ownership flag. The physical pickup or machine remains an
            // independent AP check until the player actually reaches it.
            new Item("Map: Weavenest Atla", ItemType.Map, () => { ItemGrants.GrantMap(pd => pd.HasWeavehomeMap = true); }),
            new Item("Map: Grand Gate", ItemType.Map, () => { ItemGrants.GrantMap(pd => pd.HasSongGateMap = true); }),
            new Item("Map: Underworks", ItemType.Map, () => { ItemGrants.GrantMap(pd => pd.HasCitadelUnderstoreMap = true); }),
            new Item("Map: Choral Chambers", ItemType.Map, () => { ItemGrants.GrantMap(pd => pd.HasHallsMap = true); }),
            new Item("Map: Whispering Vaults", ItemType.Map, () => { ItemGrants.GrantMap(pd => pd.HasLibraryMap = true); }),
            new Item("Map: Whiteward", ItemType.Map, () => { ItemGrants.GrantMap(pd => pd.HasWardMap = true); }),
            new Item("Map: Cogwork Core", ItemType.Map, () => { ItemGrants.GrantMap(pd => pd.HasCogMap = true); }),
            new Item("Map: Memorium", ItemType.Map, () => { ItemGrants.GrantMap(pd => pd.HasArboriumMap = true); }),
            new Item("Map: High Halls", ItemType.Map, () => { ItemGrants.GrantMap(pd => pd.HasHangMap = true); }),
            new Item("Map: The Slab", ItemType.Map, () => { ItemGrants.GrantMap(pd => pd.HasSlabMap = true); }),
            new Item("Map: Putrified Ducts", ItemType.Map, () => { ItemGrants.GrantMap(pd => pd.HasAqueductMap = true); }),
            new Item("Map: The Cradle", ItemType.Map, () => { ItemGrants.GrantMap(pd => pd.HasCradleMap = true); }),
            new Item("Map: Verdania", ItemType.Map, () => { ItemGrants.GrantMap(pd => pd.HasCloverMap = true); }),
            new Item("Map: The Abyss", ItemType.Map, () => { ItemGrants.GrantMap(pd => pd.HasAbyssMap = true); }),

            // Automatic map pins
            new Item("Bench Pins", ItemType.Pin, () => { ItemGrants.GrantPin(pd => pd.hasPinBench = true); }),
            new Item("Ventrica Pins", ItemType.Pin, () => { ItemGrants.GrantPin(pd => pd.hasPinTube = true); }),
            new Item("Bellway Pins", ItemType.Pin, () => { ItemGrants.GrantPin(pd => pd.hasPinStag = true); }),
            new Item("Vendor Pins", ItemType.Pin, () => { ItemGrants.GrantPin(pd => pd.hasPinShop = true); }),

            // Individual relic assets
            new Item("Relic: Weaver Effigy (Keelal, Shellwood)", ItemType.Relic, () => { ItemGrants.GrantRelic("Weaver Totem Witch"); }),
            new Item("Relic: Psalm Cylinder (East Whispering Vaults)", ItemType.Relic, () => { ItemGrants.GrantRelic("Psalm Cylinder Library Roof"); }),
            new Item("Relic: Bone Scroll (Wisp Thicket)", ItemType.Relic, () => { ItemGrants.GrantRelic("Bone Record Wisp Top"); }),
            new Item("Relic: Weaver Effigy (Camora, Moss Grotto)", ItemType.Relic, () => { ItemGrants.GrantRelic("Weaver Totem Bonetown_upper_room"); }),
            new Item("Relic: Sacred Cylinder", ItemType.Relic, () => { ItemGrants.GrantRelic("Librarian Melody Cylinder"); }),
            new Item("Relic: Choral Commandment (Jubilana)", ItemType.Relic, () => { ItemGrants.GrantRelic("Seal Chit City Merchant"); }),
            new Item("Relic: Psalm Cylinder (Underworks)", ItemType.Relic, () => { ItemGrants.GrantRelic("Psalm Cylinder Ward"); }),
            new Item("Relic: Rune Harp (High Halls)", ItemType.Relic, () => { ItemGrants.GrantRelic("Weaver Record Conductor"); }),
            new Item("Relic: Psalm Cylinder (Vaultkeeper Cardinius)", ItemType.Relic, () => { ItemGrants.GrantRelic("Psalm Cylinder Librarian"); }),
            new Item("Relic: Psalm Cylinder (High Halls)", ItemType.Relic, () => { ItemGrants.GrantRelic("Psalm Cylinder Hang"); }),
            new Item("Relic: Choral Commandment (Western Whiteward)", ItemType.Relic, () => { ItemGrants.GrantRelic("Seal Chit Ward Corpse"); }),
            new Item("Relic: Rune Harp (Weavenest Cindril)", ItemType.Relic, () => { ItemGrants.GrantRelic("Weaver Record Sprint_Challenge"); }),
            new Item("Relic: Psalm Cylinder (Grindle)", ItemType.Relic, () => { ItemGrants.GrantRelic("Psalm Cylinder Grindle"); }),
            new Item("Relic: Rune Harp (Weavenest Atla)", ItemType.Relic, () => { ItemGrants.GrantRelic("Weaver Record Weave_08"); }),
            new Item("Relic: Bone Scroll (Underworks)", ItemType.Relic, () => { ItemGrants.GrantRelic("Bone Record Understore_Map_Room"); }),
            new Item("Relic: Bone Scroll (Far Fields)", ItemType.Relic, () => { ItemGrants.GrantRelic("Bone Record Bone_East_14"); }),
            new Item("Relic: Choral Commandment (Moss Grotto)", ItemType.Relic, () => { ItemGrants.GrantRelic("Seal Chit Aspid_01"); }),
            new Item("Relic: Choral Commandment (Eastern Whiteward)", ItemType.Relic, () => { ItemGrants.GrantRelic("Seal Chit Silk Siphon"); }),
            new Item("Relic: Bone Scroll (Greymoor)", ItemType.Relic, () => { ItemGrants.GrantRelic("Bone Record Greymoor_flooded_corridor"); }),
            new Item("Relic: Weaver Effigy (Atla, The Slab)", ItemType.Relic, () => { ItemGrants.GrantRelic("Weaver Totem Slab_Bottom"); }),
            new Item("Relic: Arcane Egg", ItemType.Relic, () => { ItemGrants.GrantRelic("Ancient Egg Abyss Middle"); }),

            // Tools
            new Item("Ruined Tool", ItemType.Tool, ItemGrants.GrantRuinedTool),
            new Item("Tool: Silkshot (Forge Daughter)", ItemType.Tool, null),
            new Item("Tool: Silkshot (Twelfth Architect)", ItemType.Tool, null),
            new Item("Tool: Silkshot (Original)", ItemType.Tool, null),

            new Item("Tool: Volt Filament", ItemType.Tool, null),
            new Item("Tool: Tacks", ItemType.Tool, null),
            new Item("Tool: Pollip Pouch", ItemType.Tool, null),
            new Item("Tool: Snare Setter", ItemType.Tool, null),
            new Item("Tool: Wispfire Lantern", ItemType.Tool, null),
            new Item("Tool: Memory Crystal", ItemType.Tool, null),
            new Item("Tool: Voltvessels", ItemType.Tool, null),
            new Item("Tool: Threefold Pin", ItemType.Tool, null),
            new Item("Tool: Warding Bell", ItemType.Tool, null),
            new Item("Tool: Rosary Cannon", ItemType.Tool, null),
            new Item("Tool: Longpin", ItemType.Tool, null),
            new Item("Tool: Sawtooth Circlet", ItemType.Tool, null),
            new Item("Progressive Claw Mirror", ItemType.Tool, ItemGrants.GrantProgressiveClawMirror, true),
            new Item("Tool: Throwing Ring", ItemType.Tool, null),
            new Item("Tool: Delver's Drill", ItemType.Tool, null),
            new Item("Tool: Quick Sling", ItemType.Tool, null),
            new Item("Tool: Spider Strings", ItemType.Tool, null),
            new Item("Tool: Fractured Mask", ItemType.Tool, null),
            new Item("Tool: Silkspeed Anklets", ItemType.Tool, null),
            new Item("Tool: Weighted Belt", ItemType.Tool, null),
            new Item("Tool: Multibinder", ItemType.Tool, null),
            new Item("Tool: Reserve Bind", ItemType.Tool, null),
            new Item("Tool: Injector Band", ItemType.Tool, null),
            new Item("Tool: Barbed Bracelet", ItemType.Tool, null),
            new Item("Tool: Sting Shard", ItemType.Tool, null),
            new Item("Tool: Conchcutter", ItemType.Tool, null),
            new Item("Tool: Pimpillo", ItemType.Tool, null),
            new Item("Tool: Straight Pin", ItemType.Tool, null),
            new Item("Tool: Cogfly", ItemType.Tool, null),
            new Item("Progressive Curveclaw", ItemType.Tool, ItemGrants.GrantProgressiveCurveclaw, true),
            new Item("Tool: Cogwork Wheel", ItemType.Tool, null),
            new Item("Tool: Flea Brew", ItemType.Tool, null),
            new Item("Tool: Magnetite Dice", ItemType.Tool, null),
            new Item("Tool: Magnetite Brooch", ItemType.Tool, null),
            new Item("Tool: Weavelight", ItemType.Tool, null),
            new Item("Tool: Pin Badge", ItemType.Tool, null),
            new Item("Tool: Needle Phial", ItemType.Tool, null),
            new Item("Tool: Plasmium Phial", ItemType.Tool, null),
            new Item("Tool: Shell Satchel", ItemType.Tool, () => { ItemGrants.GrantTool("Shell Satchel"); }),
            new Item("Tool: Dead Bug's Purse", ItemType.Tool, null),
            new Item("Tool: Scuttlebrace", ItemType.Tool, null),
            new Item("Tool: Thief's Mark", ItemType.Tool, null),
            new Item("Tool: Compass", ItemType.Tool, null),
            new Item("Tool: Snitch Pick", ItemType.Tool, null),
            new Item("Tool: Flintslate", ItemType.Tool, null),
            new Item("Tool: Wreath of Purity", ItemType.Tool, null),
            new Item("Tool: Magma Bell", ItemType.Tool, null),
            new Item("Tool: Ascendant's Grip", ItemType.Tool, null),
            new Item("Tool: Longclaw", ItemType.Tool, null),
            new Item("Tool: Shard Pendant", ItemType.Tool, null),
            new Item("Tool: Spool Extender", ItemType.Tool, null),
            new Item("Tool: Egg of Flealia", ItemType.Tool, null),

            // Spells
            new Item("Silk Skill: Silkspear", ItemType.Spell, null),
            new Item("Silk Skill: Cross Stitch", ItemType.Spell, null),
            new Item("Silk Skill: Pale Nails", ItemType.Spell, null),
            new Item("Silk Skill: Sharpdart", ItemType.Spell, null),
            new Item("Silk Skill: Rune Rage", ItemType.Spell, null),
            new Item("Silk Skill: Thread Storm", ItemType.Spell, null),
            
            // Crests
            new Item("Crest: Witch", ItemType.Crest, () => { Utils.ForceCrest("Witch");}),
            new Item("Crest: Beast", ItemType.Crest, () => { Utils.ForceCrest("Warrior");}),
            new Item("Crest: Architect", ItemType.Crest, () => { Utils.ForceCrest("Toolmaster");}),
            new Item("Crest: Shaman", ItemType.Crest, () => { Utils.ForceCrest("Spell");}),
            new Item("Crest: Reaper", ItemType.Crest, () => { Utils.ForceCrest("Reaper");}),
            new Item("Crest: Wanderer", ItemType.Crest, () => { Utils.ForceCrest("Wanderer");}),
            new Item("Crest: Hunter", ItemType.Crest, () => { Utils.ForceCrest("Hunter");}),

            // Fleas
            new Item("Flea: The Marrow", ItemType.Flea, () => { SaveState.Instance.SavedFlea_Bone_06 = true; }),
            new Item("Flea: Deep Docks (Bellway)", ItemType.Flea, () => { SaveState.Instance.SavedFlea_Dock_16 = true; }),
            new Item("Flea: Deep Docks (Weaver Burial Spire)", ItemType.Flea, () => { SaveState.Instance.SavedFlea_Bone_East_05 = true; }),
            new Item("Flea: Far Fields (Captured)", ItemType.Flea, () => { SaveState.Instance.SavedFlea_Bone_East_17b = true; }),
            new Item("Flea: Hunter's March", ItemType.Flea, () => { SaveState.Instance.SavedFlea_Ant_03 = true; }),
            new Item("Flea: Greymoor (Craw Lake)", ItemType.Flea, () => { SaveState.Instance.SavedFlea_Greymoor_15b = true; }),
            new Item("Flea: Greymoor (Tower)", ItemType.Flea, () => { SaveState.Instance.SavedFlea_Greymoor_06 = true; }),
            new Item("Flea: Shellwood", ItemType.Flea, () => { SaveState.Instance.SavedFlea_Shellwood_03 = true; }),
            new Item("Flea: Pilgrim's Rest", ItemType.Flea, () => { SaveState.Instance.SavedFlea_Bone_East_10_Church = true; }),
            new Item("Flea: Blasted Steps", ItemType.Flea, () => { SaveState.Instance.SavedFlea_Coral_35 = true; }),
            new Item("Flea: Sinner's Road", ItemType.Flea, () => { SaveState.Instance.SavedFlea_Dust_12 = true; }),
            new Item("Flea: Exhaust Organ", ItemType.Flea, () => { SaveState.Instance.SavedFlea_Dust_09 = true; }),
            new Item("Flea: Bellhart", ItemType.Flea, () => { SaveState.Instance.SavedFlea_Belltown_04 = true; }),
            new Item("Flea: Wormways", ItemType.Flea, () => { SaveState.Instance.SavedFlea_Crawl_06 = true; }),
            new Item("Flea: The Slab (Cell)", ItemType.Flea, () => { SaveState.Instance.SavedFlea_Slab_Cell = true; }),
            new Item("Flea: Bilewater (Thieves)", ItemType.Flea, () => { SaveState.Instance.SavedFlea_Shadow_28 = true; }),
            new Item("Flea: Deep Docks (Mines)", ItemType.Flea, () => { SaveState.Instance.SavedFlea_Dock_03d = true; }),
            new Item("Flea: Wisp Thicket", ItemType.Flea, () => { SaveState.Instance.SavedFlea_Under_23 = true; }),
            new Item("Flea: Bilehaven", ItemType.Flea, () => { SaveState.Instance.SavedFlea_Shadow_10 = true; }),
            new Item("Flea: Choral Chambers (Spa)", ItemType.Flea, () => { SaveState.Instance.SavedFlea_Song_14 = true; }),
            new Item("Flea: Sands of Karak", ItemType.Flea, () => { SaveState.Instance.SavedFlea_Coral_24 = true; }),
            new Item("Flea: Mount Fay", ItemType.Flea, () => { SaveState.Instance.SavedFlea_Peak_05c = true; }),
            new Item("Flea: Songclave", ItemType.Flea, () => { SaveState.Instance.SavedFlea_Library_09 = true; }),
            new Item("Flea: Choral Chambers (Walled Room)", ItemType.Flea, () => { SaveState.Instance.SavedFlea_Song_11 = true; }),
            new Item("Flea: Whispering Vaults", ItemType.Flea, () => { SaveState.Instance.SavedFlea_Library_01 = true; }),
            new Item("Flea: Underworks", ItemType.Flea, () => { SaveState.Instance.SavedFlea_Under_21 = true; }),
            new Item("Flea: The Slab (Bellway)", ItemType.Flea, () => { SaveState.Instance.SavedFlea_Slab_06 = true; }),
            new Item("Flea: Greymoor (Kratt)", ItemType.Flea, ItemGrants.GrantKrattFlea),
            new Item("Flea: Putrified Ducts (Vog)", ItemType.Flea, ItemGrants.GrantVogFlea),
            new Item("Flea: Memorium (Huge Flea)", ItemType.Flea, ItemGrants.GrantHugeFlea),

            // Crest Slots
            new Item("Crest Slot: Hunter (Red 1)", ItemType.CrestSlot, null),
            new Item("Crest Slot: Hunter (Blue 1)", ItemType.CrestSlot, null),
            new Item("Crest Slot: Hunter (Yellow 1)", ItemType.CrestSlot, null),
            new Item("Crest Slot: Reaper (Red 1)", ItemType.CrestSlot, null),
            new Item("Crest Slot: Reaper (Blue 1)", ItemType.CrestSlot, null),
            new Item("Crest Slot: Reaper (Yellow 1)", ItemType.CrestSlot, null),
            new Item("Crest Slot: Wanderer (Blue 1)", ItemType.CrestSlot, null),
            new Item("Crest Slot: Wanderer (Blue 2)", ItemType.CrestSlot, null),
            new Item("Crest Slot: Wanderer (Yellow 1)", ItemType.CrestSlot, null),
            new Item("Crest Slot: Beast (Yellow 1)", ItemType.CrestSlot, null),
            new Item("Crest Slot: Beast (Yellow 2)", ItemType.CrestSlot, null),
            new Item("Crest Slot: Witch (Red 1)", ItemType.CrestSlot, null),
            new Item("Crest Slot: Witch (Blue 1)", ItemType.CrestSlot, null),
            new Item("Crest Slot: Witch (Blue 2)", ItemType.CrestSlot, null),
            new Item("Crest Slot: Architect (Blue 1)", ItemType.CrestSlot, null),
            new Item("Crest Slot: Architect (Yellow 1)", ItemType.CrestSlot, null),
            new Item("Crest Slot: Architect (Yellow 2)", ItemType.CrestSlot, null),
            new Item("Crest Slot: Architect (Blue 2)", ItemType.CrestSlot, null),
            new Item("Crest Slot: Shaman (Blue 1)", ItemType.CrestSlot, null),
            new Item("Crest Slot: Shaman (Blue 2)", ItemType.CrestSlot, null),

            // Mask shards
            new Item("Mask Shard #1", ItemType.MaskShard, null),
            new Item("Mask Shard #2", ItemType.MaskShard, null),
            new Item("Mask Shard #3", ItemType.MaskShard, null),
            new Item("Mask Shard #4", ItemType.MaskShard, null),
            new Item("Mask Shard #5", ItemType.MaskShard, null),
            new Item("Mask Shard #6", ItemType.MaskShard, null),
            new Item("Mask Shard #7", ItemType.MaskShard, null),
            new Item("Mask Shard #8", ItemType.MaskShard, null),
            new Item("Mask Shard #9", ItemType.MaskShard, null),
            new Item("Mask Shard #10", ItemType.MaskShard, null),
            new Item("Mask Shard #11", ItemType.MaskShard, null),
            new Item("Mask Shard #12", ItemType.MaskShard, null),
            new Item("Mask Shard #13", ItemType.MaskShard, null),
            new Item("Mask Shard #14", ItemType.MaskShard, null),
            new Item("Mask Shard #15", ItemType.MaskShard, null),
            new Item("Mask Shard #16", ItemType.MaskShard, null),
            new Item("Mask Shard #17", ItemType.MaskShard, null),
            new Item("Mask Shard #18", ItemType.MaskShard, null),
            new Item("Mask Shard #19", ItemType.MaskShard, null),
            new Item("Mask Shard #20", ItemType.MaskShard, null),

            // Thread spools
            new Item("Spool Fragment #1", ItemType.SpoolFragment, null),
            new Item("Spool Fragment #2", ItemType.SpoolFragment, null),
            new Item("Spool Fragment #3", ItemType.SpoolFragment, null),
            new Item("Spool Fragment #4", ItemType.SpoolFragment, null),
            new Item("Spool Fragment #5", ItemType.SpoolFragment, null),
            new Item("Spool Fragment #6", ItemType.SpoolFragment, null),
            new Item("Spool Fragment #7", ItemType.SpoolFragment, null),
            new Item("Spool Fragment #8", ItemType.SpoolFragment, null),
            new Item("Spool Fragment #9", ItemType.SpoolFragment, null),
            new Item("Spool Fragment #10", ItemType.SpoolFragment, null),
            new Item("Spool Fragment #11", ItemType.SpoolFragment, null),
            new Item("Spool Fragment #12", ItemType.SpoolFragment, null),
            new Item("Spool Fragment #13", ItemType.SpoolFragment, null),
            new Item("Spool Fragment #14", ItemType.SpoolFragment, null),
            new Item("Spool Fragment #15", ItemType.SpoolFragment, null),
            new Item("Spool Fragment #16", ItemType.SpoolFragment, null),
            new Item("Spool Fragment #17", ItemType.SpoolFragment, null),
            new Item("Spool Fragment #18", ItemType.SpoolFragment, null),

            // Silk hearts
            new Item("Progressive Silkheart", ItemType.SilkHeart, ItemGrants.GrantProgressiveSilkheart, true),
            
            // Bellway
            new Item("Bellway: Deep Docks", ItemType.Bellway, () =>
            {
                SaveState.Instance.UnlockedDocksStation = true;
                Patches.TravelPatches.RefreshBellBeastAfterApUnlock(
                    "UnlockedDocksStation"
                );
            }),
            new Item("Bellway: Far Fields", ItemType.Bellway, () =>
            {
                SaveState.Instance.UnlockedBoneforestEastStation = true;
                Patches.TravelPatches.RefreshBellBeastAfterApUnlock(
                    "UnlockedBoneforestEastStation"
                );
            }),
            new Item("Bellway: Greymoor", ItemType.Bellway, () =>
            {
                SaveState.Instance.UnlockedGreymoorStation = true;
                Patches.TravelPatches.RefreshBellBeastAfterApUnlock(
                    "UnlockedGreymoorStation"
                );
            }),
            new Item("Bellway: Bellhart", ItemType.Bellway, () =>
            {
                SaveState.Instance.UnlockedBelltownStation = true;
                Patches.TravelPatches.RefreshBellBeastAfterApUnlock(
                    "UnlockedBelltownStation"
                );
            }),
            new Item("Bellway: Blasted Steps", ItemType.Bellway, () =>
            {
                SaveState.Instance.UnlockedCoralTowerStation = true;
                Patches.TravelPatches.RefreshBellBeastAfterApUnlock(
                    "UnlockedCoralTowerStation"
                );
            }),
            new Item("Bellway: Grand Bellway", ItemType.Bellway, () =>
            {
                SaveState.Instance.UnlockedCityStation = true;
                Patches.TravelPatches.RefreshBellBeastAfterApUnlock(
                    "UnlockedCityStation"
                );
            }),
            new Item("Bellway: The Slab", ItemType.Bellway, () =>
            {
                SaveState.Instance.UnlockedPeakStation = true;
                Patches.TravelPatches.RefreshBellBeastAfterApUnlock(
                    "UnlockedPeakStation"
                );
            }),
            new Item("Bellway: Shellwood", ItemType.Bellway, () =>
            {
                SaveState.Instance.UnlockedShellwoodStation = true;
                Patches.TravelPatches.RefreshBellBeastAfterApUnlock(
                    "UnlockedShellwoodStation"
                );
            }),
            new Item("Bellway: Bilewater", ItemType.Bellway, () =>
            {
                SaveState.Instance.UnlockedShadowStation = true;
                Patches.TravelPatches.RefreshBellBeastAfterApUnlock(
                    "UnlockedShadowStation"
                );
            }),
            new Item("Bellway: Putrified Ducts", ItemType.Bellway, () =>
            {
                SaveState.Instance.UnlockedAqueductStation = true;
                Patches.TravelPatches.RefreshBellBeastAfterApUnlock(
                    "UnlockedAqueductStation"
                );
            }),

            // Ventrica
            new Item("Ventrica: Choral Chambers", ItemType.Ventrica, () => { SaveState.Instance.UnlockedSongTube = true; }),
            new Item("Ventrica: Underworks", ItemType.Ventrica, () => { SaveState.Instance.UnlockedUnderTube = true; }),
            new Item("Ventrica: Grand Bellway", ItemType.Ventrica, () => { SaveState.Instance.UnlockedCityBellwayTube = true; }),
            new Item("Ventrica: High Halls", ItemType.Ventrica, () => { SaveState.Instance.UnlockedHangTube = true; }),
            new Item("Ventrica: Songclave", ItemType.Ventrica, () => { SaveState.Instance.UnlockedEnclaveTube = true; }),
            new Item("Ventrica: Memorium", ItemType.Ventrica, () => { SaveState.Instance.UnlockedArboriumTube = true; }),

            // Minor caches. Kept as compact repeatable filler rather
            // than granting a full vanilla string or shard bundle per rock.
            new Item("Rosaries (10)", ItemType.Resource, () => { ItemGrants.GrantRosaries(10); }, true),
            new Item("Shell Shards (10)", ItemType.Resource, () => { ItemGrants.GrantShellShards(10); }, true),

            // Destination-specific, non-consumable Simple Keys. Each AP
            // receipt opens exactly one vanilla lock and never grants the
            // native fungible inventory item.
            new Item("Simple Key (Wormways)", ItemType.SimpleKey, SimpleKeyDoorManager.GrantWormwaysKey),
            new Item("Simple Key (Deep Docks)", ItemType.SimpleKey, SimpleKeyDoorManager.GrantDeepDocksKey),
            new Item("Simple Key (Green Prince)", ItemType.SimpleKey, SimpleKeyDoorManager.GrantGreenPrinceKey),
            new Item("Simple Key (Rosary Bank)", ItemType.SimpleKey, SimpleKeyDoorManager.GrantRosaryBankKey),

            // Learned Needolin songs. Receiving one grants its native effect
            // without completing the still-unchecked physical source.
            new Item("Architect's Melody", ItemType.Melody, () => { ItemGrants.GrantMelody(pd => pd.HasMelodyArchitect = true); }),
            new Item("Conductor's Melody", ItemType.Melody, () => { ItemGrants.GrantMelody(pd => pd.HasMelodyConductor = true); }),
            new Item("Vaultkeeper's Melody", ItemType.Melody, () => { ItemGrants.GrantMelody(pd => pd.HasMelodyLibrarian = true); }),
            new Item("Elegy of the Deep", ItemType.Melody, () => { ItemGrants.GrantMelody(pd => pd.hasNeedolinMemoryPowerup = true); }),
            new Item("Beastling Call", ItemType.Melody, ItemGrants.GrantBeastlingCall),

            // Repeatable native collectibles. Physical sources are replaced
            // separately, so AP receipt is the sole way these counters grow.
            new Item("Memory Locket", ItemType.MemoryLocket, () => { ItemGrants.GrantCollectable("Crest Socket Unlocker"); }, true),
            new Item("Craftmetal", ItemType.Craftmetal, () => { ItemGrants.GrantCollectable("Tool Metal"); }, true),
            new Item("Mossberry", ItemType.Mossberry, () => { ItemGrants.GrantCollectable("Mossberry"); }, true),
            new Item("Pollip Heart", ItemType.PollipHeart, () => { ItemGrants.GrantCollectable("Shell Flower"); }, true),
            new Item("Silkeater", ItemType.Silkeater, () => { ItemGrants.GrantCollectable("Silk Grub"); }, true),

            // Named progression keys preserve their exact vanilla gate.
            new Item("Key of Indolent", ItemType.MajorKey, () => { ItemGrants.GrantSlabKey(pd => pd.HasSlabKeyA = true); }),
            new Item("Key of Heretic", ItemType.MajorKey, () => { ItemGrants.GrantSlabKey(pd => pd.HasSlabKeyB = true); }),
            new Item("Key of Apostate", ItemType.MajorKey, () => { ItemGrants.GrantSlabKey(pd => pd.HasSlabKeyC = true); }),
            new Item("White Key", ItemType.MajorKey, ItemGrants.GrantWhiteKey),
            new Item("Surgeon's Key", ItemType.MajorKey, ItemGrants.GrantSurgeonsKey),
            new Item("Architect's Key", ItemType.MajorKey, ItemGrants.GrantArchitectsKey),
            new Item("Craw Summons", ItemType.MajorKey, ItemGrants.GrantCrawSummons),
            new Item("Growstone", ItemType.Resource, () => { ItemGrants.GrantCollectable("Growstone"); }),
            new Item("Rosaries (8)", ItemType.Resource, () => { ItemGrants.GrantRosaries(8); }, true),
            new Item("Rosaries (30)", ItemType.Resource, () => { ItemGrants.GrantRosaries(30); }, true),
            new Item("Rosaries (70)", ItemType.Resource, () => { ItemGrants.GrantRosaries(70); }, true),
            new Item("Rosaries (75)", ItemType.Resource, () => { ItemGrants.GrantRosaries(75); }, true),
            new Item("Rosaries (84)", ItemType.Resource, () => { ItemGrants.GrantRosaries(84); }, true),
            new Item("Rosaries (90)", ItemType.Resource, () => { ItemGrants.GrantRosaries(90); }, true),
            new Item("Rosaries (105)", ItemType.Resource, () => { ItemGrants.GrantRosaries(105); }, true),
            new Item("Rosaries (112)", ItemType.Resource, () => { ItemGrants.GrantRosaries(112); }, true),
            new Item("Rosaries (115)", ItemType.Resource, () => { ItemGrants.GrantRosaries(115); }, true),
            new Item("Rosaries (130)", ItemType.Resource, () => { ItemGrants.GrantRosaries(130); }, true),
            new Item("Rosaries (155)", ItemType.Resource, () => { ItemGrants.GrantRosaries(155); }, true),
            new Item("Shell Shards (35)", ItemType.Resource, () => { ItemGrants.GrantShellShards(35); }, true),
            new Item("Bell: The Marrow", ItemType.BellShrine, ItemGrants.GrantBell),
            new Item("Bell: Deep Docks", ItemType.BellShrine, ItemGrants.GrantBell),
            new Item("Bell: Greymoor", ItemType.BellShrine, ItemGrants.GrantBell),
            new Item("Bell: Shellwood", ItemType.BellShrine, ItemGrants.GrantBell),
            new Item("Bell: Bellhart", ItemType.BellShrine, ItemGrants.GrantBell),
        });
    }
}
