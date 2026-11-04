
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
            "Bone Bottom Shop - Mask Shard (Grindle in Act 3)",
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
            "Songclave Shop - Mask Shard",
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
            "Songclave Shop - Spool Fragment",
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

        private static readonly Dictionary<string, string> MapperLocationNames =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "Bone Bottom - Shell Shard Cache", "Bone Bottom Icon of Hope Statue" },
                { "shell shard cache bone bottom", "Bone Bottom Icon of Hope Statue" },
                { "Choir's Final Rites Beads", "Far Fields Chorus - Choir's Final Rites Beads" },
                { "Choral Chambers - Heavy Rosary Necklace", "Outside Spa - Heavy necklace" },
                { "Heavy Rosary Necklace - Choral Chambers", "Outside Spa - Heavy necklace" },
                { "Flea: Choral Chambers - Spa", "Choral Chambers - Caged Flea" },
                { "Maintenance Worker's Wage Rosary cache", "Maintenance Tunnel - Worker's Wage cache" },
                { "Maintenance Tunnel's Rosary cache budget", "Maintenance Tunnel - Rosary cache budget" },
                { "Maintenance Head chief's embezzled Rosary cache", "Maintenance Tunnel - Head chief's embezzled Rosary cache" },
                { "Flea: Choral Chambers - Walled Room", "Choral Chambers - Flea master of saws" },
                { "Flea Shaft's stolen rosaries", "Flea Shaft - stolen rosaries" },
                { "Flea Shaft's bounty", "Flea Shaft - bounty" },
                { "Flea Shaft's kidnappers' bonus", "Flea Shaft - kidnappers' bonus" },
                { "Mergwin's hidden stash", "Over dining - Mergwin's hidden stash" },
                { "Great Gourmand's treasury", "Over dining - Great Gourmand's treasury" },
                { "Choral Chambers East - Silkeater", "Dining room - Silk eater" },
                { "Silkeater: Choral Chambers East", "Dining room - Silk eater" },
                { "Merchant's funds", "Merchant room - Merchant's funds" },
                { "Ventrica's stored funds", "Ventrica room - stored funds" },
                { "Ventrica's maintenance fees", "Ventrica room - maintenance fees" },
                { "Ventrica's closed door budget", "Ventrica room - closed door budget" },
                { "Ventrica worker's wage", "Below Ventrica - worker's wage" },
                { "Ventrica worker's stolen funds", "Below Ventrica - worker's stolen funds" },
                { "Choral Chambers - Shell Shard Cache", "Below Ventrica - worker's remains" },
                { "Ventrica worker's remains", "Below Ventrica - worker's remains" },
                { "Ventrica Window's repairing fees", "Below Ventrica - Window's repairing fees" },
                { "Ventrica Window's cleaning fees", "Below Ventrica - Window's cleaning fees" },
                { "Choral Chambers West - Silkeater", "Grand Bellway - Silk eater" },
                { "Silkeater: Choral Chambers West", "Grand Bellway - Silk eater" },
                { "High Halls - Shell Shard Cache #2", "Small Room - Maestro remains" },
                { "High Halls - Shell Shard Cache #1", "Cogfly Room - cogfly bones" },
                { "High Halls - Rosary Chest", "Big Shaft - Minister's chest" },
                { "Horn Fragment", "Far Fields Chorus - Horn Fragment" },
                { "Reed's personal chest", "Corridor to High Halls - Reed's personal chest" },
                { "Dining Hall's food budget", "Over dining - food budget" },
                { "Choral Chambers - Rosary Necklace #1", "Merchant room - Merchant's prized necklace" },
                { "Rosary Necklace - Choral Chambers #1", "Merchant room - Merchant's prized necklace" },
                { "Choristor's precious belongings", "Above Ventrica - Choristor's precious belongings" },
                { "Old Ventrica gains", "Above Ventrica - Old gains" },
                { "Old Ventrica treasury", "Above Ventrica - Old treasury" },

                { "Rosary Cache: Sinner's Road #4", "Sinner's Road - Rosary Cache: Sinner’s Road #4" },
                { "Sinner's Road - Rosary Cache #4", "Sinner's Road - Rosary Cache: Sinner’s Road #4" },
                { "Flea: Exhaust Organ", "Exhaust Organ - Flea Is Tired Of Organ Music" },
                { "Bilewater - Shell Shard Cache #7", "Bilewater - Third Third Of The Shards In A Mothed Up Bilewater Closet" },
                { "Sinner's Road - Shell Shard Cache #1", "Sinner's Road - A Few Spare Shards In The Muckroach Kennel Attic" },
                { "Flea: Bilewater - Thieves", "Bilewater - The Flea That Bilewater Thieves Stole" },
                { "Bilewater East - Memory Locket", "Bilewater - Locket At The End of A LONG Hallway of Moths" },
                { "Flea: Bilehaven", "Bilewater - The Flea That Couldn't Make It Past All The Bilewater Traps" },
                { "Sinner's Road - Shell Shard Cache #3", "Sinner's Road - Last Few Spare Shards In The Muckroach Kennel Attic" },
                { "Bilewater - Shell Shard Cache #6", "Bilewater - Second Third Of The Shards In A Mothed Up Bilewater Closet" },
                { "Bilewater - Shell Shard Cache #5", "Bilewater - First Third Of The Shards In A Mothed Up Bilewater Closet" },
                { "Sinner's Road - Shell Shard Cache #2", "Sinner's Road - A Few More Spare Shards In The Muckroach Kennel Attic" },
                { "Shell Shard Cache: Sinner's Road #1", "Sinner's Road - A Few Spare Shards In The Muckroach Kennel Attic" },
                { "Shell Shard Cache: Sinner's Road #2", "Sinner's Road - A Few More Spare Shards In The Muckroach Kennel Attic" },
                { "Shell Shard Cache: Sinner's Road #3", "Sinner's Road - Last Few Spare Shards In The Muckroach Kennel Attic" },
                { "Shell Shard Cache: Bilewater #5", "Bilewater - First Third Of The Shards In A Mothed Up Bilewater Closet" },
                { "Shell Shard Cache: Bilewater #6", "Bilewater - Second Third Of The Shards In A Mothed Up Bilewater Closet" },
                { "Shell Shard Cache: Bilewater #7", "Bilewater - Third Third Of The Shards In A Mothed Up Bilewater Closet" },
                { "Memory Locket: Bilewater East", "Bilewater - Locket At The End of A LONG Hallway of Moths" },
                { "Beast Shard: Sprintmaster", "Swift beast shard" },
                { "Sprintmaster - Beast Shard", "Swift beast shard" },
                { "Flea: Far Fields - Captured", "Far Fields - Fort Flea Rescue - Flea" },
                { "Shell Shard Cache: Far Fields #5", "Far Fields - Deep Entrance Skarr Sentinel's Trap Shards" },
                { "Far Fields - Shell Shard Cache #5", "Far Fields - Deep Entrance Skarr Sentinel's Trap Shards" },
                { "Rosary Cache: Far Fields #21", "Far Fields - Deep Entrance -Bounty Rosary Stash - \"To be paid in full to the Hunter who fells Gurr\"" },
                { "Far Fields - Rosary Cache #21", "Far Fields - Deep Entrance -Bounty Rosary Stash - \"To be paid in full to the Hunter who fells Gurr\"" },
                { "Shell Shard Cache: Far Fields #6", "Far Fields - Deep Entrance -Cage Materials" },
                { "Far Fields - Shell Shard Cache #6", "Far Fields - Deep Entrance -Cage Materials" },
                { "Shell Shard Cache: Far Fields #7", "Far Fields - Deep Entrance - Curveclaw Reserve" },
                { "Far Fields - Shell Shard Cache #7", "Far Fields - Deep Entrance - Curveclaw Reserve" },
                { "Pale Rosary Necklace: Far Fields", "Far Fields - Deep Entrance -Queen's Pearls of Hero" },
                { "Far Fields - Pale Rosary Necklace", "Far Fields - Deep Entrance -Queen's Pearls of Hero" },
                { "Shell Shard Cache: Far Fields #4", "Far Fields - Deep Entrance Skarr Gate Watch Spears" },
                { "Far Fields - Shell Shard Cache #4", "Far Fields - Deep Entrance Skarr Gate Watch Spears" },
                { "Rosary Cache: Far Fields #20", "Far Fields - Deep Entrance -Queen's Gate Sentry Funds" },
                { "Far Fields - Rosary Cache #20", "Far Fields - Deep Entrance -Queen's Gate Sentry Funds" },
                { "Shell Shard Cache: Sinner's Road #4", "Sinner's Road - Shell Shards Lugoli Probably Threw Out: The First" },
                { "Sinner's Road - Shell Shard Cache #4", "Sinner's Road - Shell Shards Lugoli Probably Threw Out: The First" },
                { "Rosary Cache: Bilewater #1", "Bilewater - Box Of Rosaries Right Inside Bilewater" },
                { "Bilewater - Rosary Cache #1", "Bilewater - Box Of Rosaries Right Inside Bilewater" },
                { "Rosary Cache: Far Fields #17", "Far Fields - Fort Flea Rescue - Queen's Last Rosaries Stipend" },
                { "Far Fields - Rosary Cache #17", "Far Fields - Fort Flea Rescue - Queen's Last Rosaries Stipend" },
                { "Memory Locket: Bilewater West", "Bilewater - Memory Locket Annoyingly Nearby the Twisted Chud" },
                { "Bilewater West - Memory Locket", "Bilewater - Memory Locket Annoyingly Nearby the Twisted Chud" },
                { "Rosary Cache: Far Fields #16", "Far Fields - Fort Flea Rescue - Fort Barracks Ration Rosaries" },
                { "Far Fields - Rosary Cache #16", "Far Fields - Fort Flea Rescue - Fort Barracks Ration Rosaries" },
                { "Shell Shard Cache: Sinner's Road #7", "Sinner's Road - Right Shards In the Hall of the Spike Swingers" },
                { "Sinner's Road - Shell Shard Cache #7", "Sinner's Road - Right Shards In the Hall of the Spike Swingers" },
                { "Shell Shard Cache: Sinner's Road #6", "Sinner's Road - Left Shards In the Hall of the Spike Swingers" },
                { "Sinner's Road - Shell Shard Cache #6", "Sinner's Road - Left Shards In the Hall of the Spike Swingers" },
                { "Rosary Cache: Far Fields #12", "Far Fields - Lower Fort Sentry's Personal Rosaries" },
                { "Far Fields - Rosary Cache #12", "Far Fields - Lower Fort Sentry's Personal Rosaries" },
                { "Frayed Rosary String: Sinner's Road", "Sinner's Road - Caged Rosaries In the Entryway" },
                { "Sinner's Road - Frayed Rosary String", "Sinner's Road - Caged Rosaries In the Entryway" },
                { "Flea: Sinner's Road", "Sinner's Road - Sinner's Flea" },
                { "Rosary Cache: Sinner's Road #2", "Sinner's Road - First Half of the Rosaries They Took From the Gay Frog When They Put Him In Prison" },
                { "Sinner's Road - Rosary Cache #2", "Sinner's Road - First Half of the Rosaries They Took From the Gay Frog When They Put Him In Prison" },
                { "Rosary Cache: Sinner's Road #1", "Sinner's Road - Gross Rosaries In the Sinner's Basement" },
                { "Sinner's Road - Rosary Cache #1", "Sinner's Road - Gross Rosaries In the Sinner's Basement" },
                { "Shard Bundle: Sinner's Road", "Sinner's Road - Shards on Bilewater's Doorstep" },
                { "Sinner's Road - Shard Bundle", "Sinner's Road - Shards on Bilewater's Doorstep" },
                { "Rosary Cache: Sinner's Road #6", "Sinner's Road - Eastern Sinner's Cage Rosary Secundus" },
                { "Sinner's Road - Rosary Cache #6", "Sinner's Road - Eastern Sinner's Cage Rosary Secundus" },
                { "Rosary Cache: Far Fields #2", "Far Fields Chorus - Choir's Final Rites Beads" },
                { "Far Fields - Rosary Cache #2", "Far Fields Chorus - Choir's Final Rites Beads" },
                { "Shell Shard Cache: Sinner's Road #5", "Sinner's Road - Shell Shards Lugoli Probably Threw Out: The Second" },
                { "Sinner's Road - Shell Shard Cache #5", "Sinner's Road - Shell Shards Lugoli Probably Threw Out: The Second" },
                { "Rosary Cache: Sinner's Road #8", "Sinner's Road - Rosaries Looking Over The Sinner's Bench" },
                { "Sinner's Road - Rosary Cache #8", "Sinner's Road - Rosaries Looking Over The Sinner's Bench" },
                { "Simple Key: Roachkeeper", "Sinner's Road - Key Hoarding Roachkeeper In the Attic" },
                { "Roachkeeper - Simple Key", "Sinner's Road - Key Hoarding Roachkeeper In the Attic" },
                { "Rosary Cache: Far Fields #11", "Far Fields - Lower Fort Sentry's Rosaries Bonds" },
                { "Far Fields - Rosary Cache #11", "Far Fields - Lower Fort Sentry's Rosaries Bonds" },
                { "Silkeater: Exhaust Organ", "Exhaust Organ - Silk Grub Listening To Mozart In the Cocoon" },
                { "Exhaust Organ - Silkeater", "Exhaust Organ - Silk Grub Listening To Mozart In the Cocoon" },
                { "Rosary Cache: Bilewater #2", "Bilewater - A Few Remaining Rosaries Next To A Dead Guy In Bilewater" },
                { "Bilewater - Rosary Cache #2", "Bilewater - A Few Remaining Rosaries Next To A Dead Guy In Bilewater" },
                { "Rosary Cache: Bilewater #7", "Bilewater - Other Quick Rosaries In The Western Bilewater Rooms" },
                { "Bilewater - Rosary Cache #7", "Bilewater - Other Quick Rosaries In The Western Bilewater Rooms" },
                { "Rosary Cache: Sinner's Road #3", "Sinner's Road - Second Half of the Rosaries They Took From the Gay Frog When They Put Him In Prison" },
                { "Sinner's Road - Rosary Cache #3", "Sinner's Road - Second Half of the Rosaries They Took From the Gay Frog When They Put Him In Prison" },
                { "Rosary Cache: Sinner's Road #7", "Sinner's Road - Eastern Sinner's Cage Rosary Tertius" },
                { "Sinner's Road - Rosary Cache #7", "Sinner's Road - Eastern Sinner's Cage Rosary Tertius" },
                { "Rosary Cache: Bilewater #6", "Bilewater - First Quick Rosaries In The Western Bilewater Rooms" },
                { "Bilewater - Rosary Cache #6", "Bilewater - First Quick Rosaries In The Western Bilewater Rooms" },
                { "Sinner's Road - Rosary Chest", "Sinner's Road - That Chest In the Halls of the Spike Swingers That's RIGHT THERE" },
                { "Rosary Cache: Far Fields #13", "Far Fields Lower Fort Last Rosaries Stipend - Queen's Final Support" },
                { "Far Fields - Rosary Cache #13", "Far Fields Lower Fort Last Rosaries Stipend - Queen's Final Support" },
                { "Rosary Cache: Sinner's Road #5", "Sinner's Road - Eastern Sinner's Cage Rosary Primus" },
                { "Sinner's Road - Rosary Cache #5", "Sinner's Road - Eastern Sinner's Cage Rosary Primus" },
                { "Shellwood - Rosary Cache", "Rosary cache: Shellwood Top Room" },
                { "Shellwood - Shell Shard Cache #3", "Shell Shard Cache: Greyroots Diddy Basement 3" },
                { "Shellwood - Shell Shard Cache #6", "Shell shard Cache: Outside Longpin 3" },
                { "Pollip Heart: Shellwood #2", "Pollip Heart Flower Room" },
                { "Shellwood - Shell Shard Cache #1", "Shell Shard Cache: Greyroots Diddy Basement 1" },
                { "Shellwood - Shell Shard Cache #5", "Shell shard Cache: Outside Longpin 2" },
                { "Shellwood - Shell Shard Cache #2", "Shell Shard Cache: Greyroots Diddy Basement 2" },
                { "Pollip Heart: Shellwood #6", "Pollip Heart Right Side Big Room" },
                { "Shellwood - Shell Shard Cache #4", "Shell shard Cache: Outside Longpin 1" },
                { "Pollip Heart: Shellwood #5", "Pollip Heart Hidden Room" },
                { "Pollip Heart: Shellwood #3", "Pollip Heart Cling Grip Room" },
                { "Pollip Heart: Shellwood #1", "Pollip Heart Left Big Room" },
                { "Mask Shard: Shellwood", "Mask Shard: Shellwood" },
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
                { "Tool Unlock: Musician Charm", "Songclave Shop - Spider Strings" },
                { "Tool Unlock: Rosary Magnet", "Bone Bottom Shop - Magnetite Brooch (Grindle in Act 3)" },
                { "Tool Unlock: Wallcling", "Songclave Shop - Ascendant's Grip" },
                { "Tool Unlock: Spool Extender", "Songclave Shop - Spool Extender" },
                { "Simple Key: Bone Bottom Shop", "Bone Bottom Shop - Simple Key (Grindle in Act 3)" },
                { "Craftmetal: Bone Bottom Shop", "Bone Bottom Shop - Craftmetal (Grindle in Act 3)" },
                { "Relic Pickup: Seal Chit City Merchant", "Songclave Shop - Choral Commandment" },
                {
                    "Relic: Bone Scroll (Greymoor)",
                    "Relic: Bone Scroll (Greymoor) at bugs lonely resting place"
                },
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
                { "Quest Completion: Broodmother Hunt", "Wish: The Wailing Mother" },
                { "Quest Completion: Song Knight", "Wish: Final Audience" },
                { "Quest Completion: Tormented Trobbio", "Wish: Pain, Anguish and Misery" },
                { "Quest Completion: Pinstress Battle", "Wish: Fatal Resolve" },
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
                        ItemSet.GetSourceDisplayName(currentName);
                }

                if (CanonicalLocationItemNameCollisions.TryGetValue(
                        renamedLocation,
                        out collisionLocationName))
                {
                    return GetLocationFirstDisplayName(collisionLocationName);
                }

                string canonicalName =
                    ItemSet.GetSourceDisplayName(renamedLocation);
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

            if (MapperLocationNames.TryGetValue(locationName, out string mappedName))
            {
                return mappedName;
            }
            if (MapperLocationNames.ContainsValue(locationName))
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
                    return ItemSet.GetSourceDisplayName(
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
                return ItemSet.GetSourceDisplayName(sourceItemName);
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

                return PinPurchasePrefix + ItemSet.GetSourceDisplayName(
                    "Pin: " + pinName
                );
            }

            const string RelicPickupPrefix = "Relic Pickup: ";
            if (locationName.StartsWith(
                    RelicPickupPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return ItemSet.GetSourceDisplayName(
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
            new Location(MinorPickupManifest.LoddieNecklace, ItemType.Resource,
                () => PlayerData.instance != null && PlayerData.instance.pinGalleriesCompleted >= 2),
            // Skills
            new Location("Skill Unlock: Double Jump", ItemType.Skill, () => { return PlayerData.instance.hasDoubleJump; }),
            new Location("Skill Unlock: Charge Slash", ItemType.Skill, () => { return PlayerData.instance.hasChargeSlash; }),
            new Location("Skill Unlock: Silk Soar", ItemType.Skill, () => { return PlayerData.instance.hasSuperJump; }),
            new Location("Skill Unlock: Wall Jump", ItemType.Skill, () => { return PlayerData.instance.hasWalljump; }),
            new Location("Skill Unlock: Drifter's Cloak", ItemType.Skill, IsDriftersCloakSourceCompleted),
            new Location("Skill Unlock: Dash", ItemType.Skill, () => { return PlayerData.instance.hasDash; }),
            new Location("Skill Unlock: Harpoon", ItemType.Skill, () => { return PlayerData.instance.hasHarpoonDash; }),
            new Location("Skill Unlock: Quill", ItemType.Quill, () => { return PlayerData.instance.hasQuill; }),
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
            new Location("Tool Unlock: Shell Satchel", ItemType.Tool, null),
            new Location("Tool Unlock: Scuttlebrace", ItemType.Tool, null),
            new Location("Tool Unlock: Thief Charm", ItemType.Tool, null),
            new Location("Tool Unlock: Compass", ItemType.Compass, null),
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

            new Location("Hunter Evolution 1", ItemType.Eva, null),
            new Location("Hunter Evolution 2", ItemType.Eva, null),
            new Location("Yellow Vesticrest", ItemType.Eva, null),
            new Location("Blue Vesticrest", ItemType.Eva, null),
            new Location("Sylphsong", ItemType.Eva, null),
            new Location("Maiden Soul", ItemType.Soul, null),
            new Location("Hermit Soul", ItemType.Soul, null),
            new Location("Seeker Soul", ItemType.Soul, null),
            new Location("Pollen Heart", ItemType.OldHeart, null),
            new Location("Hunter's Heart", ItemType.OldHeart, null),
            new Location("Encrusted Heart", ItemType.OldHeart, null),
            new Location("Conjoined Heart", ItemType.OldHeart, null),
            new Location("Twisted Bud", ItemType.TwistedBud, null),
            new Location("Everbloom", ItemType.Everbloom, null),
            new Location("Sprintmaster's Memento", ItemType.Memento, null),
            new Location("Guardian's Memento", ItemType.Memento, null),
            new Location("Hero's Memento", ItemType.Memento, null),
            new Location("Grey Memento", ItemType.Memento, null),
            new Location("Surface Memento", ItemType.Memento, null),
            new Location("Craw Memento", ItemType.Memento, null),
            new Location("Materium", ItemType.Materium, null),
            new Location("Farsight", ItemType.Farsight, null),
            new Location("Desk", ItemType.Bellhome, null),
            new Location("Gleamlights", ItemType.Bellhome, null),
            new Location("Bell Lacquer", ItemType.Bellhome, null),
            new Location("Personal Spa", ItemType.Bellhome, null),
            new Location("Gramophone", ItemType.Bellhome, null),
            new Location("Crow bell", ItemType.Bellhome, null),
            new Location("Flintstone", ItemType.Materium, null),
            new Location("Magnetite Outcrop", ItemType.Materium, null),
            new Location("Voltstone", ItemType.Materium, null),
            new Location("Grass Doll", ItemType.QuestCollectible, null),
            new Location("Horn Fragment", ItemType.QuestCollectible, null),
            new Location("crown fragment", ItemType.QuestCollectible, null),
            new Location("Broodmother's Eye", ItemType.QuestCollectible, null),
            new Location("Choral Chambers Cogheart Piece", ItemType.Cogheart, null),
            new Location("Whispering Vaults: Cogheart Piece", ItemType.Cogheart, null),
            new Location("Memorimum Cogheart Piece", ItemType.Cogheart, null),
            new Location("Boss: Summoned Saviour", ItemType.Boss, null),
            new Location("Boss: Lace (Deep Docks)", ItemType.Boss, null),

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
            .Concat(JournalRandomization.Locations()).ToArray();
    }
}
