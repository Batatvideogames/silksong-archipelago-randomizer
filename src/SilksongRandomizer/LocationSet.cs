
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SilksongRandomizer.Patches;
using UnityEngine;
using static SteelSoulQuestSpot;

namespace SilksongRandomizer
{
    public class LocationSet
    {
        internal const string DriftersCloakLocationName =
            "Drifter's Cloak";
        internal const string DriftersCloakQuestAssetName = "Brolly Get";

        internal static void RestoreDriftersCloakSourceFlag()
        {
            PlayerData playerData = PlayerData.instance;
            if (playerData != null)
            {
                playerData.hasBrolly = true;
            }
        }

        internal static bool IsDriftersCloakSourceCompleted()
        {
            PlayerData playerData = PlayerData.instance;
            if (playerData == null)
            {
                return false;
            }

            if (playerData.hasBrolly)
            {
                return true;
            }

            if (playerData.QuestCompletionData == null)
            {
                return false;
            }

            QuestCompletionData.Completion completion =
                playerData.QuestCompletionData.GetData(
                    DriftersCloakQuestAssetName
                );
            bool completed =
                completion.IsCompleted || completion.WasEverCompleted;
            if (completed)
            {
                // Flexile Spines normally sets this separate native source
                // flag after its reward popup. The randomized popup skip
            // bypasses that late action, but Fourth Chorus
                // and other world-state consumers still require the flag.
                // Usable glide remains governed by SaveState.canBrolly.
                RestoreDriftersCloakSourceFlag();
            }

            return completed;
        }

        internal static readonly string[] MaskShardLocationNames =
        {
            "Mask Shard: Pebb (Bone Bottom) / Grindle (Act 3)",
            "Mask Shard: Wormways",
            "Mask Shard: Far Fields (Above the Seamstress)",
            "Mask Shard: Shellwood",
            "Mask Shard: The Marrow - Deep Docks Passage",
            "Mask Shard: Weavenest Atla",
            "Mask Shard: Savage Beastfly",
            "Mask Shard: Cogwork Core",
            "Mask Shard: Whispering Vaults",
            "Mask Shard: Bilewater",
            "Mask Shard: Far Fields (Skull Cave)",
            "Mask Shard: The Slab (Key of the Apostate)",
            "Mask Shard: Mount Fay",
            "Mask Shard: Wisp Thicket",
            "Mask Shard: Jubilana (Songclave)",
            "Mask Shard: Blasted Steps",
            "Mask Shard: Fastest in Pharloom",
            "Mask Shard: The Hidden Hunter",
            "Mask Shard: Dark Hearts",
            "Mask Shard: Brightvein",
        };

        internal static readonly string[] SpoolFragmentLocationNames =
        {
            "Spool Fragment: Bone Bottom",
            "Spool Fragment: Deep Docks (Central)",
            "Spool Fragment: Greymoor",
            "Spool Fragment: The Slab",
            "Spool Fragment: Weavenest Atla",
            "Spool Fragment: Frey (Bellhart)",
            "Spool Fragment: Flea Caravan",
            "Spool Fragment: Cogwork Core",
            "Spool Fragment: Underworks (East)",
            "Spool Fragment: Grand Gate",
            "Spool Fragment: Underworks (Gauntlet)",
            "Spool Fragment: Whiteward",
            "Spool Fragment: Balm for the Wounded",
            "Spool Fragment: Deep Docks (Southeast)",
            "Spool Fragment: High Halls",
            "Spool Fragment: Memorium",
            "Spool Fragment: Grindle (Blasted Steps)",
            "Spool Fragment: Jubilana (Songclave)",
        };

        internal static readonly string[] SilkHeartLocationNames =
        {
            "Silk Heart: Bell Beast",
            "Silk Heart: The Unravelled",
            "Silk Heart: Lace (Cradle)",
        };

        // These current AP location names match former vanilla
        // item names. ItemSet still translates those item identities to their
        // progressive replacements, but location canonicalization must stop
        // here: the APWorld exposes the checks as Claw Mirror and Curveclaw,
        // not as Progressive Claw Mirror / Progressive Curveclaw.
        private static readonly Dictionary<string, string>
            CanonicalLocationItemNameCollisions =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase
                )
                {
                    { "Claw Mirror", "Claw Mirror" },
                    { "Curveclaw", "Curveclaw" },
                };

        private static readonly string[] LocationFirstDisplayItemLabels =
        {
            "Mask Shard",
            "Spool Fragment",
            "Silk Heart",
            "Bellway",
            "Ventrica",
            "Map Purchase",
            "Map Pickup",
            "Bellshrine",
            "Crafting Kit",
            "Simple Key",
            "Memory Locket",
            "Craftmetal",
            "Mossberry",
            "Pollip Heart",
            "Silkeater",
            "Tool Pouch",
            "Beast Shard",
            "Rosary Necklace",
            "Heavy Rosary Necklace",
            "Frayed Rosary String",
            "Rosary String",
            "Pale Rosary Necklace",
            "Rosary Cache",
            "Shell Shard Cache",
            "Pristine Core",
            "Shard Bundle",
            "Shell Shard Bundle",
            "Pale Oil",
        };

        private static readonly Tuple<string, string>[] PairedItemPrefixes =
        {
            Tuple.Create("Skill Unlock: ", "Skill: "),
            Tuple.Create("Tool Unlock: ", "Tool: "),
            Tuple.Create("Spell Unlock: ", "Spell: "),
            Tuple.Create("Crest Unlock: ", "Crest: "),
            Tuple.Create("Save Flea: ", "Flea: "),
        };

        private static readonly Tuple<string, string>[] DirectItemPrefixes =
        {
            Tuple.Create("Mask Shard Unlock #", "Mask Shard #"),
            Tuple.Create("Spool Fragment Unlock #", "Spool Fragment #"),
            Tuple.Create("Silk Heart Unlock #", "Silk Heart #"),
            Tuple.Create("Bellway Unlock: ", "Bellway: "),
            Tuple.Create("Ventrica Unlock: ", "Ventrica: "),
        };

        private static readonly Dictionary<string, string> ExplicitLocationRenames =
            BuildExplicitLocationRenames();

        private static Dictionary<string, string>
            BuildExplicitLocationRenames()
        {
            Dictionary<string, string> renames =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase
                )
            {
                {
                    "Skill Unlock: Drifter's Cloak",
                    DriftersCloakLocationName
                },
                {
                    "Bell Shrine Completion: bellShrineBoneForest",
                    "Bellshrine: The Marrow"
                },
                {
                    "Bell Shrine Completion: bellShrineWilds",
                    "Bellshrine: Deep Docks"
                },
                {
                    "Bell Shrine Completion: bellShrineGreymoor",
                    "Bellshrine: Greymoor"
                },
                {
                    "Bell Shrine Completion: bellShrineShellwood",
                    "Bellshrine: Shellwood"
                },
                {
                    "Bell Shrine Completion: bellShrineBellhart",
                    "Bellshrine: Bellhart"
                },
                { "Boss Completion: defeatedMossMother", "Boss: Moss Mother" },
                {
                    "Boss Completion: skullKingKilled",
                    "Boss: Skull Tyrant (Bone Bottom)"
                },
                { "Boss Completion: defeatedBellBeast", "Boss: Bell Beast" },
                {
                    "Boss Completion: defeatedAntQueen",
                    "Boss: Skarrsinger Karmelita"
                },
                {
                    "Boss Completion: defeatedSongGolem",
                    "Boss: Fourth Chorus"
                },
                {
                    "Boss Completion: defeatedDockForemen",
                    "Boss: Forebrothers Signis & Gron"
                },
                {
                    "Boss Completion: defeatedVampireGnatBoss",
                    "Boss: Moorwing"
                },
                {
                    "Boss Completion: defeatedCrowCourt",
                    "Boss: Crawfather"
                },
                {
                    "Boss Completion: defeatedSplinterQueen",
                    "Boss: Sister Splinter"
                },
                {
                    "Boss Completion: defeatedSeth",
                    "Boss: Shrine Guardian Seth"
                },
                { "Boss Completion: defeatedFlowerQueen", "Boss: Nyleth" },
                {
                    "Boss Completion: defeatedRoachkeeperChef",
                    "Boss: Disgraced Chef Lugoli"
                },
                { "Boss Completion: defeatedPhantom", "Boss: Phantom" },
                {
                    "Boss Completion: DefeatedSwampShaman",
                    "Boss: Groal the Great"
                },
                {
                    "Boss Completion: defeatedCoralKing",
                    "Boss: Crust King Khann"
                },
                {
                    "Boss Completion: defeatedCoralDrillers",
                    "Boss: Great Conchflies"
                },
                {
                    "Boss Completion: defeatedLastJudge",
                    "Boss: Last Judge"
                },
                {
                    "Boss Completion: defeatedGreyWarrior",
                    "Boss: Watcher at the Edge"
                },
                {
                    "Boss Completion: defeatedFirstWeaver",
                    "Boss: First Sinner"
                },
                {
                    "Boss Completion: defeatedBroodMother",
                    "Boss: Broodmother"
                },
                { "Boss Completion: defeatedTrobbio", "Boss: Trobbio" },
                {
                    "Boss Completion: defeatedTormentedTrobbio",
                    "Boss: Tormented Trobbio"
                },
                {
                    "Boss Completion: defeatedCogworkDancers",
                    "Boss: Cogwork Dancers"
                },
                {
                    "Boss Completion: defeatedLaceTower",
                    "Boss: Lace (Cradle)"
                },
                {
                    "Boss Completion: defeatedSongChevalierBoss",
                    "Boss: Second Sentinel"
                },
                {
                    "Boss Completion: defeatedWhiteCloverstag",
                    "Boss: Palestag"
                },
                {
                    "Boss Completion: defeatedCloverDancers",
                    "Boss: Clover Dancers"
                },
                {
                    "Boss Completion: defeatedWispPyreEffigy",
                    "Boss: Father of the Flame"
                },
                {
                    "Boss Completion: defeatedAntTrapper",
                    "Boss: Gurr the Outcast"
                },
                {
                    "Boss Completion: defeatedCoralDrillerSolo",
                    "Boss: Raging Conchfly"
                },
                {
                    "Boss Completion: wardBossDefeated",
                    "Boss: The Unravelled"
                },
                {
                    "Boss Completion: defeatedZapCoreEnemy",
                    "Boss: Voltvyrm"
                },
                {
                    "Boss Completion: spinnerDefeated",
                    "Boss: Widow"
                },
                {
                    "Boss Completion: skullKingDefeated",
                    "Boss: Skull Tyrant (The Marrow)"
                },
                {
                    "Crafting Kit Source: Forge Tool Kit",
                    "Crafting Kit: Forge Daughter"
                },
                {
                    "Crafting Kit Source: Architect Tool Kit",
                    "Crafting Kit: Twelfth Architect"
                },
                {
                    "Crafting Kit Source: Grindle Tool Kit",
                    "Crafting Kit: Grindle"
                },
                {
                    "Crafting Kit Source: Crow Feathers",
                    "Crafting Kit: Crawbug Clearing (Creige)"
                },
                {
                    "Quest Completion: A Pinsmiths Tools",
                    "Wish: Pinmaster's Oil"
                },
                {
                    "Quest Completion: Belltown House Start",
                    "Wish: Restoration of Bellhart"
                },
                {
                    "Quest Completion: Belltown House Mid",
                    "Wish: Bellhart's Glory"
                },
                {
                    "Quest Completion: Building Materials",
                    "Wish: Bone Bottom Repairs"
                },
                {
                    "Quest Completion: Building Materials (Bridge)",
                    "Wish: A Lifesaving Bridge"
                },
                {
                    "Quest Completion: Building Materials (Statue)",
                    "Wish: An Icon of Hope"
                },
                {
                    "Quest Completion: Courier Delivery Bonebottom",
                    "Wish: Bone Bottom Supplies"
                },
                {
                    "Quest Completion: Courier Delivery Dustpens Slave",
                    "Wish: Queen's Egg"
                },
                {
                    "Quest Completion: Courier Delivery Fixer",
                    "Wish: Survivor's Camp Supplies"
                },
                {
                    "Quest Completion: Courier Delivery Fleatopia",
                    "Wish: Fleatopia Supplies"
                },
                {
                    "Quest Completion: Courier Delivery Mask Maker",
                    "Wish: Liquid Lacquer"
                },
                {
                    "Quest Completion: Courier Delivery Pilgrims Rest",
                    "Wish: Pilgrim's Rest Supplies"
                },
                {
                    "Quest Completion: Courier Delivery Songclave",
                    "Wish: Songclave Supplies"
                },
                {
                    "Quest Completion: Extractor Blue Worms",
                    "Wish: Advanced Alchemy"
                },
                { "Quest Completion: Fine Pins", "Wish: Fine Pins" },
                {
                    "Quest Completion: Garmond Black Threaded",
                    "Wish: Hero's Call"
                },
                {
                    "Quest Completion: Great Gourmand",
                    "Wish: Great Taste of Pharloom"
                },
                { "Quest Completion: Journal", "Wish: Bugs of Pharloom" },
                {
                    "Quest Completion: Mr Mushroom",
                    "Wish: Passing of the Age"
                },
                {
                    "Quest Completion: Pilgrim Rags",
                    "Wish: Garb of the Pilgrims"
                },
                {
                    "Quest Completion: Rock Rollers",
                    "Wish: Volatile Flintbeetles"
                },
                {
                    "Quest Completion: Save City Merchant",
                    "Wish: The Wandering Merchant"
                },
                {
                    "Quest Completion: Save City Merchant Bridge",
                    "Wish: The Lost Merchant"
                },
                {
                    "Quest Completion: Save Courier Short",
                    "Wish: My Missing Courier"
                },
                {
                    "Quest Completion: Save Courier Tall",
                    "Wish: My Missing Brother"
                },
                {
                    "Quest Completion: Save Sherma",
                    "Wish: Balm for the Wounded"
                },
                {
                    "Quest Completion: Shiny Bell Goomba",
                    "Wish: Silver Bells"
                },
                {
                    "Quest Completion: Skull King",
                    "Wish: The Terrible Tyrant"
                },
                {
                    "Quest Completion: Song Pilgrim Cloaks",
                    "Wish: Cloaks of the Choir"
                },
                {
                    "Quest Completion: Songclave Donation 1",
                    "Wish: Building Up Songclave"
                },
                {
                    "Quest Completion: Songclave Donation 2",
                    "Wish: Strengthening Songclave"
                },
                {
                    "Quest Completion: Steel Sentinel Pt2",
                    "Wish: A Vassal Lost"
                },
                {
                    "Tool Unlock: Mosscreep Tool 1",
                    "Druid's Eye"
                },
                {
                    "Tool Unlock: Mosscreep Tool 2",
                    "Druid's Eyes"
                },
                {
                    "Tool Unlock: Dazzle Bind",
                    "Claw Mirror"
                },
                {
                    "Tool Unlock: Dazzle Bind Upgraded",
                    "Dark Mirror"
                },
                {
                    "Tool Unlock: Curve Claws",
                    "Curveclaw"
                },
                {
                    "Tool Unlock: Curve Claws Upgraded",
                    "Curvesickle"
                },
            };

            for (int index = 0; index < MaskShardLocationNames.Length; index++)
            {
                string currentName = MaskShardLocationNames[index];
                renames.Add(
                    "Mask Shard Unlock #" + (index + 1),
                    currentName
                );
            }

            for (
                int index = 0;
                index < SpoolFragmentLocationNames.Length;
                index++
            )
            {
                string currentName = SpoolFragmentLocationNames[index];
                renames.Add(
                    "Spool Fragment Unlock #" + (index + 1),
                    currentName
                );
            }

            for (int index = 0; index < SilkHeartLocationNames.Length; index++)
            {
                string currentName = SilkHeartLocationNames[index];
                renames.Add(
                    "Silk Heart Unlock #" + (index + 1),
                    currentName
                );
            }

            return renames;
        }

        internal static readonly Dictionary<string, string>
            SourceLocationNameAliases = BuildSourceLocationNameAliases();

        private static readonly Func<string, string>
            CanonicalLocationNameFactory = ResolveCanonicalLocationName;
        private static readonly ConcurrentDictionary<string, string>
            CanonicalLocationNames =
                new ConcurrentDictionary<string, string>();

        internal static string GetCanonicalLocationName(string locationName)
        {
            if (string.IsNullOrWhiteSpace(locationName))
            {
                return locationName;
            }

            ConcurrentDictionary<string, string> cache =
                CanonicalLocationNames;
            return cache == null
                ? ResolveCanonicalLocationName(locationName)
                : cache.GetOrAdd(
                    locationName,
                    CanonicalLocationNameFactory
                );
        }

        private static string ResolveCanonicalLocationName(
            string locationName)
        {
            string currentName = locationName;
            HashSet<string> visitedNames = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );
            while (visitedNames.Add(currentName))
            {
                if (CanonicalLocationItemNameCollisions.TryGetValue(
                        currentName,
                        out string collisionLocationName))
                {
                    return GetLocationFirstDisplayName(collisionLocationName);
                }

                string renamedLocation;
                if (ExplicitLocationRenames.TryGetValue(
                             currentName,
                             out string explicitName))
                {
                    renamedLocation = explicitName;
                }
                else if (SourceLocationNameAliases != null &&
                         SourceLocationNameAliases.TryGetValue(
                             currentName,
                             out string sourceAlias))
                {
                    renamedLocation = sourceAlias;
                }
                else
                {
                    renamedLocation =
                        ConvertPairedLocationName(currentName) ??
                        ItemSet.GetCanonicalItemName(currentName);
                }

                if (CanonicalLocationItemNameCollisions.TryGetValue(
                        renamedLocation,
                        out collisionLocationName))
                {
                    return GetLocationFirstDisplayName(collisionLocationName);
                }

                string canonicalName =
                    ItemSet.GetCanonicalItemName(renamedLocation);
                if (string.Equals(
                        currentName,
                        canonicalName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return GetLocationFirstDisplayName(canonicalName);
                }

                currentName = canonicalName;
            }

                // A malformed source-alias cycle cannot hang location
            // resolution. The last unique name is returned deterministically.
            return GetLocationFirstDisplayName(currentName);
        }

        private static string GetLocationFirstDisplayName(string locationName)
        {
            if (string.IsNullOrWhiteSpace(locationName))
            {
                return locationName;
            }

            foreach (string itemLabel in LocationFirstDisplayItemLabels)
            {
                string prefix = itemLabel + ": ";
                if (locationName.StartsWith(
                        prefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return FormatLocationFirstName(
                        locationName.Substring(prefix.Length),
                        itemLabel
                    );
                }
            }

            return locationName;
        }

        private static string FormatLocationFirstName(
            string sourceName,
            string itemLabel)
        {
            int numberSeparator = sourceName.LastIndexOf(
                " #",
                StringComparison.Ordinal
            );
            if (numberSeparator > 0)
            {
                string number = sourceName.Substring(numberSeparator + 2);
                if (number.Length > 0 && number.All(char.IsDigit))
                {
                    return sourceName.Substring(0, numberSeparator) +
                           " - " + itemLabel + " #" + number;
                }
            }

            return sourceName + " - " + itemLabel;
        }

        internal static string[] GetRoomLocationNameCandidates(
            string locationName
        )
        {
            return new[] { GetCanonicalLocationName(locationName) };
        }

        private static string ConvertPairedLocationName(string locationName)
        {
            foreach (Tuple<string, string> prefixes in PairedItemPrefixes)
            {
                if (locationName.StartsWith(
                        prefixes.Item1,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return ItemSet.GetCanonicalItemName(
                        prefixes.Item2 +
                        locationName.Substring(prefixes.Item1.Length)
                    );
                }
            }

            int slotSeparator = locationName.IndexOf(
                " Slot Unlock: ",
                StringComparison.OrdinalIgnoreCase
            );
            if (slotSeparator > 0)
            {
                string sourceItemName =
                    locationName.Substring(0, slotSeparator) +
                    " Slot: " +
                    locationName.Substring(
                        slotSeparator + " Slot Unlock: ".Length
                    );
                return ItemSet.GetCanonicalItemName(sourceItemName);
            }

            foreach (Tuple<string, string> prefixes in DirectItemPrefixes)
            {
                if (locationName.StartsWith(
                        prefixes.Item1,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return prefixes.Item2 +
                           locationName.Substring(prefixes.Item1.Length);
                }
            }

            const string PinPurchasePrefix = "Pin Purchase: ";
            if (locationName.StartsWith(
                    PinPurchasePrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                string pinName =
                    locationName.Substring(PinPurchasePrefix.Length);
                if (pinName.EndsWith(
                        " Pins",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return locationName;
                }

                return PinPurchasePrefix + ItemSet.GetCanonicalItemName(
                    "Pin: " + pinName
                );
            }

            const string RelicPickupPrefix = "Relic Pickup: ";
            if (locationName.StartsWith(
                    RelicPickupPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return ItemSet.GetCanonicalItemName(
                    "Relic: " +
                    locationName.Substring(RelicPickupPrefix.Length)
                );
            }

            return null;
        }

        private static Dictionary<string, string>
            BuildSourceLocationNameAliases()
        {
            Dictionary<string, string> aliases =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase
                );
            foreach (Location location in new LocationSet().Locations)
            {
                if (!string.Equals(
                        location.SourceName,
                        location.Name,
                        StringComparison.Ordinal))
                {
                    aliases[location.SourceName] = location.Name;
                }
            }

            return aliases;
        }

        public Location[] Locations = LoreTabletManifest.AppendTo(
            BeastShardSourceManifest.AppendTo(
            ToolPouchLocationManifest.AppendTo(
            CardiniusCylinderTurnInManifest.AppendTo(
            ScroungeRelicTurnInManifest.AppendTo(
            CollectibleSourceManifest.AppendTo(
            MinorCacheManifest.AppendTo(
            MinorPickupManifest.AppendTo(
            QuestLocationManifest.AppendTo(
            MaskAndSpoolLocationManifest.AppendTo(
            CoreLocationManifest.AppendTo(new Location[]
        {
            // Skills
            new Location("Skill Unlock: Double Jump", ItemType.Skill, () => { return PlayerData.instance.hasDoubleJump; }),
            new Location("Skill Unlock: Charge Slash", ItemType.Skill, () => { return PlayerData.instance.hasChargeSlash; }),
            new Location("Skill Unlock: Silk Soar", ItemType.Skill, () => { return PlayerData.instance.hasSuperJump; }),
            new Location("Skill Unlock: Wall Jump", ItemType.Skill, () => { return PlayerData.instance.hasWalljump; }),
            new Location("Skill Unlock: Drifter's Cloak", ItemType.Skill, IsDriftersCloakSourceCompleted),
            new Location("Skill Unlock: Dash", ItemType.Skill, () => { return PlayerData.instance.hasDash; }),
            new Location("Skill Unlock: Harpoon", ItemType.Skill, () => { return PlayerData.instance.hasHarpoonDash; }),
            new Location("Skill Unlock: Quill", ItemType.Skill, () => { return PlayerData.instance.hasQuill; }),
            new Location("Skill Unlock: Needolin", ItemType.Skill, () => { return PlayerData.instance.hasNeedolin; }),

            // Tools
            new Location("Ruined Tool", ItemType.Tool, null),
            new Location("Tool Unlock: WebShot Forge", ItemType.Tool, null),
            new Location("Tool Unlock: WebShot Architect", ItemType.Tool, null),
            new Location("Tool Unlock: WebShot Weaver", ItemType.Tool, null),

            new Location("Tool Unlock: Zap Imbuement", ItemType.Tool, null),
            new Location("Tool Unlock: Tack", ItemType.Tool, null),
            new Location("Tool Unlock: Poison Pouch", ItemType.Tool, null),
            new Location("Tool Unlock: Silk Snare", ItemType.Tool, null),
            new Location("Tool Unlock: Wisp Lantern", ItemType.Tool, null),
            new Location("Tool Unlock: Revenge Crystal", ItemType.Tool, null),
            new Location("Tool Unlock: Lightning Rod", ItemType.Tool, null),
            new Location("Tool Unlock: Tri Pin", ItemType.Tool, null),
            new Location("Tool Unlock: Bell Bind", ItemType.Tool, null),
            new Location("Tool Unlock: Rosary Cannon", ItemType.Tool, null),
            new Location("Tool Unlock: Harpoon", ItemType.Tool, null),
            new Location("Tool Unlock: Brolly Spike", ItemType.Tool, null),
            new Location("Tool Unlock: Dazzle Bind", ItemType.Tool, null),
            new Location("Tool Unlock: Dazzle Bind Upgraded", ItemType.Tool, null),
            new Location("Tool Unlock: Shakra Ring", ItemType.Tool, null),
            new Location("Tool Unlock: Screw Attack", ItemType.Tool, null),
            new Location("Tool Unlock: Quick Sling", ItemType.Tool, null),
            new Location("Tool Unlock: Musician Charm", ItemType.Tool, null),
            new Location("Tool Unlock: Fractured Mask", ItemType.Tool, null),
            new Location("Tool Unlock: Sprintmaster", ItemType.Tool, null),
            new Location("Tool Unlock: Weighted Anklet", ItemType.Tool, null),
            new Location("Tool Unlock: Multibind", ItemType.Tool, null),
            new Location("Tool Unlock: Reserve Bind", ItemType.Tool, null),
            new Location("Tool Unlock: Quickbind", ItemType.Tool, null),
            new Location("Tool Unlock: Barbed Wire", ItemType.Tool, null),
            new Location("Tool Unlock: Sting Shard", ItemType.Tool, null),
            new Location("Tool Unlock: Conch Drill", ItemType.Tool, null),
            new Location("Tool Unlock: Pimpilo", ItemType.Tool, null),
            new Location("Tool Unlock: Straight Pin", ItemType.Tool, null),
            new Location("Tool Unlock: Cogwork Flier", ItemType.Tool, null),
            new Location("Tool Unlock: Curve Claws", ItemType.Tool, null),
            new Location("Tool Unlock: Curve Claws Upgraded", ItemType.Tool, null),
            new Location("Tool Unlock: Cogwork Saw", ItemType.Tool, null),
            new Location("Tool Unlock: Flea Brew", ItemType.Tool, null),
            new Location("Tool Unlock: Magnetite Dice", ItemType.Tool, null),
            new Location("Tool Unlock: Rosary Magnet", ItemType.Tool, null),
            new Location("Tool Unlock: White Ring", ItemType.Tool, null),
            new Location("Tool Unlock: Pinstress Tool", ItemType.Tool, null),
            new Location("Tool Unlock: Extractor", ItemType.Tool, null),
            new Location("Tool Unlock: Lifeblood Syringe", ItemType.Tool, null),
            new Location("Tool Unlock: Dead Mans Purse", ItemType.Tool, null),
            new Location("Tool Unlock: Scuttlebrace", ItemType.Tool, null),
            new Location("Tool Unlock: Thief Charm", ItemType.Tool, null),
            new Location("Tool Unlock: Compass", ItemType.Tool, null),
            new Location("Tool Unlock: Thief Claw", ItemType.Tool, null),
            new Location("Tool Unlock: Mosscreep Tool 1", ItemType.Tool, null),
            new Location("Tool Unlock: Mosscreep Tool 2", ItemType.Tool, null),
            new Location("Tool Unlock: Flintstone", ItemType.Tool, null),
            new Location("Tool Unlock: Maggot Charm", ItemType.Tool, null),
            new Location("Tool Unlock: Lava Charm", ItemType.Tool, null),
            new Location("Tool Unlock: Wallcling", ItemType.Tool, null),
            new Location("Tool Unlock: Longneedle", ItemType.Tool, null),
            new Location("Tool Unlock: Bone Necklace", ItemType.Tool, null),
            new Location("Tool Unlock: Spool Extender", ItemType.Tool, null),
            new Location("Tool Unlock: Flea Charm", ItemType.Tool, null),

            // Spells
            new Location("Spell Unlock: Silk Spear", ItemType.Spell, null),
            new Location("Spell Unlock: Parry", ItemType.Spell, null),
            new Location("Spell Unlock: Silk Boss Needle", ItemType.Spell, null),
            new Location("Spell Unlock: Silk Charge", ItemType.Spell, null),
            new Location("Spell Unlock: Silk Bomb", ItemType.Spell, null),
            new Location("Spell Unlock: Thread Sphere", ItemType.Spell, null),

            // Crests
            new Location("Crest Unlock: Witch", ItemType.Crest, null),
            new Location("Crest Unlock: Beast", ItemType.Crest, null),
            new Location("Crest Unlock: Architect", ItemType.Crest, null),
            new Location("Crest Unlock: Shaman", ItemType.Crest, null),
            new Location("Crest Unlock: Reaper", ItemType.Crest, null),
            new Location("Crest Unlock: Wanderer", ItemType.Crest, null),
            new Location("Crest Unlock: Hunter", ItemType.Crest, () => { return HeroController.instance != null && HeroController.instance.CanJump(); }),

            // Fleas
            new Location("Save Flea: The Marrow (Tangle)", ItemType.Flea, () => PlayerData.instance.SavedFlea_Bone_06),
            new Location("Save Flea: Deep Docks Bellway (Eepy)", ItemType.Flea, () => PlayerData.instance.SavedFlea_Dock_16),
            new Location("Save Flea: Deep Docks Weaver Burial Spire (Squeesh)", ItemType.Flea, () => PlayerData.instance.SavedFlea_Bone_East_05),
            new Location("Save Flea: Far Fields Captured (Thoughtless)", ItemType.Flea, () => PlayerData.instance.SavedFlea_Bone_East_17b),
            new Location("Save Flea: Hunter's March (Yapper)", ItemType.Flea, () => PlayerData.instance.SavedFlea_Ant_03),
            new Location("Save Flea: Greymoor Craw Lake (Gwah)", ItemType.Flea, () => PlayerData.instance.SavedFlea_Greymoor_15b),
            new Location("Save Flea: Greymoor Tower (Snoozles)", ItemType.Flea, () => PlayerData.instance.SavedFlea_Greymoor_06),
            new Location("Save Flea: Shellwood (Shelly)", ItemType.Flea, () => PlayerData.instance.SavedFlea_Shellwood_03),
            new Location("Save Flea: Pilgrim's Rest (Sleeby)", ItemType.Flea, () => PlayerData.instance.SavedFlea_Bone_East_10_Church),
            new Location("Save Flea: Blasted Steps (Nini)", ItemType.Flea, () => PlayerData.instance.SavedFlea_Coral_35),
            new Location("Save Flea: Sinner's Road (Stelf)", ItemType.Flea, () => PlayerData.instance.SavedFlea_Dust_12),
            new Location("Save Flea: Exhaust Organ (Rangle)", ItemType.Flea, () => PlayerData.instance.SavedFlea_Dust_09),
            new Location("Save Flea: Bellhart (Bellphy)", ItemType.Flea, () => PlayerData.instance.SavedFlea_Belltown_04),
            new Location("Save Flea: Wormways (Snacc)", ItemType.Flea, () => PlayerData.instance.SavedFlea_Crawl_06),
            new Location("Save Flea: The Slab Cell (Sway)", ItemType.Flea, () => PlayerData.instance.SavedFlea_Slab_Cell),
            new Location("Save Flea: Bilewater Thieves (Cower)", ItemType.Flea, () => PlayerData.instance.SavedFlea_Shadow_28),
            new Location("Save Flea: Deep Docks Mines (Le Bomba)", ItemType.Flea, () => PlayerData.instance.SavedFlea_Dock_03d),
            new Location("Save Flea: Wisp Thicket (Fidget)", ItemType.Flea, () => PlayerData.instance.SavedFlea_Under_23),
            new Location("Save Flea: Bilehaven (Spangle)", ItemType.Flea, () => PlayerData.instance.SavedFlea_Shadow_10),
            new Location("Save Flea: Choral Cambers Spa (Oowa)", ItemType.Flea, () => PlayerData.instance.SavedFlea_Song_14),
            new Location("Save Flea: Sands of Karak (Crustly)", ItemType.Flea, () => PlayerData.instance.SavedFlea_Coral_24),
            new Location("Save Flea: Mount Fay (Ice Cube)", ItemType.Flea, () => PlayerData.instance.SavedFlea_Peak_05c),
            new Location("Save Flea: Songclave (Groggy)", ItemType.Flea, () => PlayerData.instance.SavedFlea_Library_09),
            new Location("Save Flea: Choral Cambers Walled (Mimi)", ItemType.Flea, () => PlayerData.instance.SavedFlea_Song_11),
            new Location("Save Flea: Whispering Vaults (Birdy)", ItemType.Flea, () => PlayerData.instance.SavedFlea_Library_01),
            new Location("Save Flea: Underworks (Boomy)", ItemType.Flea, () => PlayerData.instance.SavedFlea_Under_21),
            new Location("Save Flea: The Slab Bellway (Honk Shoo)", ItemType.Flea, () => PlayerData.instance.SavedFlea_Slab_06),
            // Named NPC Fleas are reported by exact source-state hooks. Their
            // native flags are also set by remotely received AP items so the
            // recruited NPCs visibly join the caravan. Polling those flags
            // here would incorrectly auto-complete an unvisited source.
            new Location("Flea: Greymoor - Kratt", ItemType.Flea, null),
            new Location("Flea: Putrified Ducts - Vog", ItemType.Flea, null),
            new Location("Flea: Memorium - Huge Flea", ItemType.Flea, null),

            // Crest Slots
            new Location("Hunter Slot Unlock: Red 0 -2", ItemType.CrestSlot, null),
            new Location("Hunter Slot Unlock: Blue -2 0", ItemType.CrestSlot, null),
            new Location("Hunter Slot Unlock: Yellow 2 0", ItemType.CrestSlot, null),
            new Location("Reaper Slot Unlock: Red 0 -1", ItemType.CrestSlot, null),
            new Location("Reaper Slot Unlock: Blue -1 1", ItemType.CrestSlot, null),
            new Location("Reaper Slot Unlock: Yellow 1 1", ItemType.CrestSlot, null),
            new Location("Wanderer Slot Unlock: Blue -2 1", ItemType.CrestSlot, null),
            new Location("Wanderer Slot Unlock: Blue 2 1", ItemType.CrestSlot, null),
            new Location("Wanderer Slot Unlock: Yellow 0 -2", ItemType.CrestSlot, null),
            new Location("Beast Slot Unlock: Yellow -1 0", ItemType.CrestSlot, null),
            new Location("Beast Slot Unlock: Yellow 1 0", ItemType.CrestSlot, null),
            new Location("Witch Slot Unlock: Red 0 -2", ItemType.CrestSlot, null),
            new Location("Witch Slot Unlock: Blue -1 0", ItemType.CrestSlot, null),
            new Location("Witch Slot Unlock: Blue 1 0", ItemType.CrestSlot, null),
            new Location("Architect Slot Unlock: Blue -1 2", ItemType.CrestSlot, null),
            new Location("Architect Slot Unlock: Yellow 1 2", ItemType.CrestSlot, null),
            new Location("Architect Slot Unlock: Yellow 2 0", ItemType.CrestSlot, null),
            new Location("Architect Slot Unlock: Blue -2 0", ItemType.CrestSlot, null),
            new Location("Shaman Slot Unlock: Blue -1 0", ItemType.CrestSlot, null),
            new Location("Shaman Slot Unlock: Blue 1 0", ItemType.CrestSlot, null),

            // Silk Hearts are source-stable. The memory scene is entered by
            // the corresponding physical reward and survives save/reload,
            // unlike silkRegenMax, whose count depends on acquisition order.
            new Location("Silk Heart Unlock #1", ItemType.SilkHeart, () => {
                return PlayerData.instance.scenesVisited != null &&
                       PlayerData.instance.scenesVisited.Contains(
                           "Memory_Silk_Heart_BellBeast"
                       );
            }),
            new Location("Silk Heart Unlock #2", ItemType.SilkHeart, () => {
                return PlayerData.instance.scenesVisited != null &&
                       PlayerData.instance.scenesVisited.Contains(
                           "Memory_Silk_Heart_WardBoss"
                       );
            }),
            new Location("Silk Heart Unlock #3", ItemType.SilkHeart, () => {
                return PlayerData.instance.scenesVisited != null &&
                       PlayerData.instance.scenesVisited.Contains(
                           "Memory_Silk_Heart_LaceTower"
                       );
            }),

            // Bellway
            new Location("Bellway Unlock: Deep Docks", ItemType.Bellway, () => { return PlayerData.instance.UnlockedDocksStation; }),
            new Location("Bellway Unlock: Far Fields", ItemType.Bellway, () => { return PlayerData.instance.UnlockedBoneforestEastStation; }),
            new Location("Bellway Unlock: Greymoor", ItemType.Bellway, () => { return PlayerData.instance.UnlockedGreymoorStation; }),
            new Location("Bellway Unlock: Bellhart", ItemType.Bellway, () => { return PlayerData.instance.UnlockedBelltownStation; }),
            new Location("Bellway Unlock: Blasted Steps", ItemType.Bellway, () => { return PlayerData.instance.UnlockedCoralTowerStation; }),
            new Location("Bellway Unlock: Grand Bellway", ItemType.Bellway, () => { return PlayerData.instance.UnlockedCityStation; }),
            new Location("Bellway Unlock: The Slab", ItemType.Bellway, () => { return PlayerData.instance.UnlockedPeakStation; }),
            new Location("Bellway Unlock: Shellwood", ItemType.Bellway, () => { return PlayerData.instance.UnlockedShellwoodStation; }),
            new Location("Bellway Unlock: Bilewater", ItemType.Bellway, () => { return PlayerData.instance.UnlockedShadowStation; }),
            new Location("Bellway Unlock: Putrified Ducts", ItemType.Bellway, () => { return PlayerData.instance.UnlockedAqueductStation; }),

            // Ventrica
            new Location("Ventrica Unlock: Choral Chambers", ItemType.Ventrica, () => { return PlayerData.instance.UnlockedSongTube; }),
            new Location("Ventrica Unlock: Underworks", ItemType.Ventrica, () => { return PlayerData.instance.UnlockedUnderTube; }),
            new Location("Ventrica Unlock: Grand Bellway", ItemType.Ventrica, () => { return PlayerData.instance.UnlockedCityBellwayTube; }),
            new Location("Ventrica Unlock: High Halls", ItemType.Ventrica, () => { return PlayerData.instance.UnlockedHangTube; }),
            new Location("Ventrica Unlock: Songclave", ItemType.Ventrica, () => { return PlayerData.instance.UnlockedEnclaveTube; }),
            new Location("Ventrica Unlock: Memorium", ItemType.Ventrica, () => { return PlayerData.instance.UnlockedArboriumTube; }),

            new Location("Goal", ItemType.Event,
                Patches.GoalState.IsConfiguredGoalComplete),

            // Static map sources are intercepted by StaticMapPatches. They
            // cannot poll the native ownership flags because an AP-received
            // map item must not complete its still-unvisited physical source.
            new Location("Map Pickup: Weavenest Atla", ItemType.Map, null),
            new Location("Map Purchase: Grand Gate", ItemType.Map, null),
            new Location("Map Pickup: Underworks", ItemType.Map, null),
            new Location("Map Purchase: Choral Chambers", ItemType.Map, null),
            new Location("Map Purchase: Whispering Vaults", ItemType.Map, null),
            new Location("Map Purchase: Whiteward", ItemType.Map, null),
            new Location("Map Pickup: Cogwork Core", ItemType.Map, null),
            new Location("Map Purchase: Memorium", ItemType.Map, null),
            new Location("Map Purchase: High Halls", ItemType.Map, null),
            new Location("Map Pickup: The Slab", ItemType.Map, null),
            new Location("Map Pickup: Putrified Ducts", ItemType.Map, null),
            new Location("Map Purchase: The Cradle", ItemType.Map, null),
            new Location("Map Pickup: Verdania", ItemType.Map, null),
            new Location("Map Pickup: The Abyss", ItemType.Map, null),

            // Learned-song rewards are source-driven FSM checks. Native
                // ownership flags are not polled because receiving
            // an AP item must not auto-complete its physical source.
            new Location("Architect's Melody", ItemType.Melody, null),
            new Location("Conductor's Melody", ItemType.Melody, null),
            new Location("Vaultkeeper's Melody", ItemType.Melody, null),
            new Location("Elegy of the Deep", ItemType.Melody, null),
            new Location("Beastling Call", ItemType.Melody, null),
        })))))))))))
            .ToArray();
    }
}
