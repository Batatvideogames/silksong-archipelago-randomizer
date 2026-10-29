using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilksongRandomizer
{
    public static class ItemIcons
    {
        private const float TargetIconSize = 110f;
        private const float MinIconSize = 72f;

        public static readonly Dictionary<string, string> Mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Hunter's Journal", "QI_Journal" },
            { "Journal Entry", "Hornet_Inv_pane_icons_0003_journal_killed" },
            { "Journal Completion", "Hornet_Inv_pane_icons_0003_journal_killed" },
            { "Diving Bell Key", "dock_key" },
            { "Simple Key", "I_token_of_faith" },
            { "Everbloom", "I_white_flower" },
            { "Sprintmaster's Memento", "sprintmaster_memento" },
            { "Grey Memento", "grey_warrior_memento" },
            { "Surface Memento", "surface_memento" },
            { "Boss Credit", "Hornet_Inv_pane_icons_0003_journal_killed" },
            { "Boss", "Hornet_Inv_pane_icons_0003_journal_killed" },
            { "Story credit", "Hornet_Inv_pane_icons_0003_journal_killed" },
            { "Wish", "Hornet_Inv_pane_icons_0002_wish" },

            // Skills
            { "Bind", "Inv_0029_spell_core_outer_icons_eva_heal" },
            { "Hunter Bind", "Crest__0003_hunter_sil" },
            { "Wanderer Bind", "Crest__0001_wanderer_sil" },
            { "Reaper Bind", "Crest__0000_reaper_sil" },
            { "Beast Bind", "Crest__0002_warrior_sil" },
            { "Architect Bind", "Crest__0016_toolmaster_full" },
            { "Witch Bind", "Crest__0006_witch_sil" },
            { "Shaman Bind", "Crest__0009_spell_full" },
            { "Needle Strike", "prompt_nail_art" },
            { "Silk Soar", "Inv_0029_spell_core_outer_icons_0000_1_super_jump" },
            { "Cling Grip", "Inv_0029_spell_core_outer_icons_0000_1_wall_jump" },
            { "Ledge Grab", "Inv_0029_spell_core_outer_icons_0000_1_wall_jump" },
            { "Clawline", "Inv_0029_spell_core_outer_icons_0000_1_harpoon_dash" },
            { "Needolin", "Inv_0029_spell_core_outer_icons_0000_1" },
            { "Swift Step", "Inv_0029_spell_core_outer_icons_0000_1_sprint" },
            { "Progressive Swift Step", "Inv_0029_spell_core_outer_icons_0000_1_sprint" },
            { "Sprint", "Inv_0029_spell_core_outer_icons_0000_1_sprint" },

            { "Cling Grip (Left)", "Inv_0029_spell_core_outer_icons_0000_1_wall_jump" },
            { "Cling Grip (Right)", "Inv_0029_spell_core_outer_icons_0000_1_wall_jump" },
            { "Clawline (Left)", "Inv_0029_spell_core_outer_icons_0000_1_harpoon_dash" },
            { "Clawline (Right)", "Inv_0029_spell_core_outer_icons_0000_1_harpoon_dash" },
            { "Swift Step (Left)", "Inv_0029_spell_core_outer_icons_0000_1_sprint" },
            { "Swift Step (Right)", "Inv_0029_spell_core_outer_icons_0000_1_sprint" },
            { "Dash (Left)", "Inv_0029_spell_core_outer_icons_0000_1_sprint" },
            { "Dash (Right)", "Inv_0029_spell_core_outer_icons_0000_1_sprint" },
            { "Ledge Grab (Left)", "Inv_0029_spell_core_outer_icons_0000_1_wall_jump" },
            { "Ledge Grab (Right)", "Inv_0029_spell_core_outer_icons_0000_1_wall_jump" },

            // Silk Spells
            { "Silkspear", "S_needle_throw" },
            { "Cross Stitch", "S_parry" },
            { "Pale Nails", "S_finger_blade" },
            { "Sharpdart", "S_thread_dash" },
            { "Rune Rage", "S_bomb" },
            { "Thread Storm", "S_thread_sphere" },

            // Crests
            { "Crest: Hunter", "Crest__0003_hunter_sil" },
            { "Crest: Reaper", "Crest__0000_reaper_sil" },
            { "Crest: Wanderer", "Crest__0001_wanderer_sil" },
            { "Crest: Beast", "Crest__0002_warrior_sil" },
            { "Crest: Witch", "Crest__0006_witch_sil" },
            { "Crest: Architect", "Crest__0016_toolmaster_full" },
            { "Crest: Shaman", "Crest__0009_spell_full" },

            // Crest Slots
            { "Crest Slot: Hunter (Red 1)", "UI_tool_slot_attack0000" },
            { "Crest Slot: Hunter (Blue 1)", "UI_tool_slot_defend0000" },
            { "Crest Slot: Hunter (Yellow 1)", "UI_tool_slot_explore0000" },
            { "Crest Slot: Reaper (Red 1)", "UI_tool_slot_attack0000" },
            { "Crest Slot: Reaper (Blue 1)", "UI_tool_slot_defend0000" },
            { "Crest Slot: Reaper (Yellow 1)", "UI_tool_slot_explore0000" },
            { "Crest Slot: Wanderer (Blue 1)", "UI_tool_slot_defend0000" },
            { "Crest Slot: Wanderer (Blue 2)", "UI_tool_slot_defend0000" },
            { "Crest Slot: Wanderer (Yellow 1)", "UI_tool_slot_explore0000" },
            { "Crest Slot: Beast (Yellow 1)", "UI_tool_slot_explore0000" },
            { "Crest Slot: Beast (Yellow 2)", "UI_tool_slot_explore0000" },
            { "Crest Slot: Witch (Red 1)", "UI_tool_slot_attack0000" },
            { "Crest Slot: Witch (Blue 1)", "UI_tool_slot_defend0000" },
            { "Crest Slot: Witch (Blue 2)", "UI_tool_slot_defend0000" },
            { "Crest Slot: Architect (Blue 1)", "UI_tool_slot_defend0000" },
            { "Crest Slot: Architect (Yellow 1)", "UI_tool_slot_explore0000" },
            { "Crest Slot: Architect (Yellow 2)", "UI_tool_slot_explore0000" },
            { "Crest Slot: Architect (Blue 2)", "UI_tool_slot_defend0000" },
            { "Crest Slot: Shaman (Blue 1)", "UI_tool_slot_defend0000" },
            { "Crest Slot: Shaman (Blue 2)", "UI_tool_slot_defend0000" },

            // Eva
            { "Hunter Evolution 1", "Crest__0013_hunter_lvl2_full" },
            { "Hunter Evolution 2", "Crest__0014_hunter_lvl3_silhouette" },
            { "Evolved Hunter Crest", "Crest__0014_hunter_lvl4_silhouette" },
            { "Blue Vesticrest", "UI_tool_slot_defend0000" },
            { "Yellow Vesticrest", "UI_tool_slot_explore0000" },
            { "Sylphsong", "Inv_0029_spell_core_outer_icons_eva_heal" },

            // Tools
            { "Ruined Tool", "_0000_T_web_shot_broken" },
            { "Silkshot (Original)", "_0001_T_web_shot_forge_runes" },
            { "Silkshot (Forge Daughter)", "_0002_T_web_shot_forge" },
            { "Silkshot (Twelfth Architect)", "_0003_T_web_shot_architect" },
            { "Volt Filament", "T_zap_imbuement" },
            { "Tacks", "tiny_tool_icon_tacks" }, // Don't see a T_ prefix for this one.
            { "Voltvessels", "_0004_T_lightning__0000_1" },
            { "Snare Setter", "_0004_T_snare_setter" },
            { "Threefold Pin", "T_tri_pin" },
            { "Rosary Cannon", "_0004_T_rosary_cannon_loaded" },
            { "Straight Pin", "T_straight_pin" },
            { "Longpin", "T_claw_javelin" },
            { "Sawtooth Circlet", "T_brolly_spike" },
            { "Throwing Ring", "T_shakra_ring" },
            { "Delver's Drill", "T_Spine_head" },
            { "Pimpillo", "T_pimpilo" },
            { "Pollip Pouch", "T_poison_pouch" },
            { "Conchcutter", "T_Conch_Drill_Shot" },
            { "Cogfly", "T_cogwork_flier" },
            { "Sting Shard", "T_sting_shard" },
            { "Cogwork Wheel", "T_cogwork_saw" },
            { "Progressive Claw Mirror", "T_dazzle_bind_upg" },

            { "Warding Bell", "Hornet_icon_0001_T_bell_shield" },
            { "Magma Bell", "Hornet_T_lava_charm" },
            { "Lava Charm", "Hornet_T_lava_charm" },
            { "Silkspeed Anklets", "T_icon_sprintmaster" },
            { "Weighted Belt", "T_weighted_anklet" },
            { "Multibinder", "T_multi_bind" },
            { "White Ring", "T_icon_white_ring" },
            { "Injector Band", "T_quick_bind" },
            { "Barbed Bracelet", "T_barbed_wire" },
            { "Spider Strings", "T_attunement_charm" },
            { "Magnetite Brooch", "T_rosary_magnet" },
            { "Rosary Magnet", "T_rosary_magnet" },
            { "Weavelight", "T_icon_white_ring" },
            { "Pin Badge", "T_pinstress_tool" },
            { "Needle Phial", "T_Extractor" },
            { "Plasmium Phial", "T_syringe_lifeblood" },
            { "Thief's Mark", "Thief_Brooch" },
            { "Snitch Pick", "Thief_Claw" },
            { "Wreath of Purity", "poultice_pouch_icon" },
            { "Ascendant's Grip", "T_longneedle_old1" },
            { "Longclaw", "T_longneedle" },
            { "Memory Crystal", "T_revenge_crystal" },
            { "Progressive Curveclaw", "T_curve_claw" },
            { "Progressive Druid's Eyes", "T_mossmedal" },
            { "Spool Extender", "T_spool_bar_extender" },
            { "Wispfire Lantern", "T_wisp_lantern" },
            { "Quick Sling", "T_quick_sling" },
            { "Fractured Mask", "Hornet_T_fractured_mask" },
            { "Reserve Bind", "T_focus_spool" },
            { "Flea Brew", "T_flea_brew" },
            { "Shell Satchel", "T_shell_satchel" },
            { "Growstone", "Growstone_0004" },
            { "Dead Bug's Purse", "T_dead_purse" },
            { "Scuttlebrace", "T_steel_spine" },
            { "Shard Pendant", "Hornet_Bone_Necklace" },

            // Skills / Tools
            { "Drifter's Cloak", "I_spine_cloak" },
            { "Faydown Cloak", "I_spine_cloak_down" },
            { "Flintslate", "Hornet_icon_0003_T_flintstone" },
            { "Key of Indolent", "I_slab_key" },
            { "Key of Heretic", "I_slab_key_gold" },
            { "Key of Apostate", "I_slab_key" },
            { "Progressive Tool Pouch", "Inv_tool_pouch_upgrade" },
            { "Magnetite Dice", "_0006_I_magnetite_dice" },
            { "Pollip Heart", "_0007_shell_flower_purple_icon" },
            { "Egg of Flealia", "Flea_Egg" },
            { "Craftmetal", "Hornet_Tool_Metal" },
            { "White Key", "I_key_whiteward" },
            { "Surgeon's Key", "I_chute_key_whiteward" },
            { "Architect's Key", "I_key_architect" },
            { "Progressive Crafting Kit", "icon_tool_kit_upgrade" },
            { "Progressive Needle Upgrade", "Hornet_Inv_pane_icons_0003_needle_sharpened" },
            { "Pale Oil", "oil_phial" },
            { "Memory Locket", "Charm_Notch" },

            // Rosaries / Shell Shards
            { "Rosaries", "I_rosary_icon_clean" },
            { "Frayed Rosary String", "I_rosary_necklace_0002_1_frayed" },
            { "Rosary String", "I_rosary_necklace_0002_1" },
            { "Rosary Necklace", "I_rosary_necklace_0000_3" },
            { "Heavy Rosary Necklace", "I_rosary_necklace_0000_3_old" },
            { "Pale Rosary Necklace", "I_rosary_necklace_0001_2" },
            { "Shell Shards", "I_shell_shard_icon_large" },
            { "Shard Bundle", "Hornet_Tool_Metal_Pouch" },
            { "Beast Shard", "Icon_Beast_Shard" },

            // Relics
            { "Weaver Effigy", "Hornet_icon_0005_R_saint_locket" },
            { "Psalm Cylinder", "Hornet_icon_0000_R_psalm_cylinder" },
            { "Bone Scroll", "Hornet_icon_0002_R_bone_record" },
            { "Sacred Cylinder", "Hornet_icon_0000_R_psalm_cylinder_white" },
            { "Rune Harp", "Hornet_icon_0002_R_weaver_record" },
            { "Choral Commandment", "Hornet_icon_0000_R_seal_chit" },
            { "Arcane Egg", "Hornet_icon_0004_R_ancient_egg" },
            { "Pristine Core", "Icon_Pristine_Core" },
            { "Craw Summons", "Inv_craw_summons" },
            { "Cogwork Heart", "cog_heart_pieces" },

            // Bellhome
            { "Desk", "desk" },
            { "Gleamlights", "gleamflies" },
            { "Bell Lacquer", "paint" },
            { "Personal Spa", "spa" },
            { "Gramophone", "gramaphone" },

            // Melodies / Cutscenes
            { "Elegy of the Deep", "Deep_Memory_Prompt" },
            { "Beastling Call", "bellbeast_melody_prompts" },
            { "Bellbeast Melody", "bellbeast_melody_prompts" },
            { "Architect's Melody", "melody_prompts_0000_architect" },
            { "Conductor's Melody", "melody_prompts_0001_conductor" },
            { "Vaultkeeper's Melody", "melody_prompts_0002_librarian" },

            // Pins
            { "Bench Pins", "pin_bench" },
            { "Ventrica Pins", "pin_tube_station" },
            { "Bellway Pins", "pin_stag_station" },
            { "Vendor Pins", "pin_shop" },

            // Generic Upgrades & Collectables
            { "Mask Shard", "Hornet_Spool_Upgrade_Shop_Icon_Heart" },
            { "Spool Fragment", "Hornet_Spool_Upgrade_Shop_Icon" },
            { "Flea", "Flea_Scoreboard_Icons_0001_Generic" },
            { Patches.FleaPatches.KrattItemName, "Flea_Scoreboard_Icons_0002_Lech" },
            { Patches.FleaPatches.VogItemName, "Flea_Scoreboard_Icons_0000_Hunter" },
            { Patches.FleaPatches.HugeFleaItemName, "Flea_Scoreboard_Icons_0001_Giant" },
            { "Lore", "Hornet_icon_0002_R_bone_record" },
            { "Bell", "QI_Main_bellshrines_counter" },
            { "Bellway", "Hornet_icon_bell_clapper" },
            { "Map", "I_map" },
            { "Ventrica", "pin_tube_station" },
            { "Progressive Silkheart", "silk_heart_inv_icon" },
            { "Crest Slot", "UI_tool_slot_attack0000" },

            // Traps
            { "Literacy Trap", "Hornet_icon_0002_R_bone_record" },
            { "Cursed Crest Trap", "cursed_death0004"},
            { "Naked Trap", "Hornet_Cloakless_Frost_Death0000" },
            { "Rosary Spill Trap", "rosary_cache__0030_bowl_cache"},
            { "Darkness Trap", "Hornet_death_pieces_0000s_0000_death_spider_core"},

            // Quest Items
            { "Maiden Soul", "snail_icon__0000_churchkeeper_soul" },
            { "Hermit Soul", "snail_icon__0001_bell_hermit_soul" },
            { "Seeker Soul", "snail_icon__0001_bell_swamp_soul" },
            { "Pollen Heart", "flower_queen_heart_icon0000" },
            { "Hunter's Heart", "ant_queen_heart_icon0002" },
            { "Encrusted Heart", "coral_king_heart_icon0000" },
            { "Conjoined Heart", "clover_prince_heart0000" },
            { "Twisted Bud", "mandrake_icon0000" },
            { "Mossberry", "I_moss_berry" },
        };

        private static readonly Dictionary<string, string> Collectables =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Sprintmaster's Memento", "Sprintmaster Memento" },
            { "Guardian's Memento", "Memento Seth" },
            { "Hero's Memento", "Memento Garmond" },
            { "Grey Memento", "Grey Memento" },
            { "Surface Memento", "Memento Surface" },
            { "Craw Memento", "Crowman Memento" },
            { "Everbloom", "White Flower" },
            { "Farsight", "Farsight" },
            { "Cogheart Piece", "Cog Heart Pieces" },
            { "Grass Doll", "Ant Trapper Item" },
            { "Horn Fragment", "Beastfly Remains" },
            { "Crown Fragment", "Skull King Fragment" },
            { "Broodmother's Eye", "Broodmother Remains" },
            { "Crawbell", "Crawbell" },
            { "Materium", "Materium" },
            { "Diving Bell Key", "Dock Key" },
            { "Simple Key", "Simple Key" },
            { "Silkeater", "Silk Grub" },
        };

        private static readonly Dictionary<string, string> Samples =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Flintstone", "Smeltstone" },
            { "Magnetite", "Magnetite" },
            { "Voltstone", "Voltstone" },
        };

        private static readonly Dictionary<string, ToolItemType> SlotColors =
            new Dictionary<string, ToolItemType>(StringComparer.OrdinalIgnoreCase)
        {
            { "UI_tool_slot_attack0000", ToolItemType.Red },
            { "UI_tool_slot_defend0000", ToolItemType.Blue },
            { "UI_tool_slot_explore0000", ToolItemType.Yellow },
        };

        private static readonly Dictionary<string, Sprite> ColoredSlots =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, CachedIcon> IconCache = 
            new Dictionary<string, CachedIcon>(StringComparer.OrdinalIgnoreCase);

        private struct CachedIcon
        {
            public Sprite Sprite;
            public float Scale;
        }

        private static readonly Dictionary<string, float> MissingUntil =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        public static string GetSpriteName(string itemName)
            => Mappings.TryGetValue(itemName, out string spriteName) ? spriteName : null;

        public static Sprite GetSprite(string itemName, Sprite fallback)
            => GetSprite(itemName, fallback, out _);

        public static Sprite GetSprite(string itemName, Sprite fallback, out float scale)
        {
            scale = 1f;
            if (string.IsNullOrWhiteSpace(itemName)) return fallback;

            Sprite soulIcon = JournalRandomization.GetIcon(Normalize(itemName)) ?? NpcSoulIcons.GetIcon(Normalize(itemName)) ??
                EnemySoulState.GetIcon(Normalize(itemName)) ?? GetBossJournalIcon(itemName);
            if (soulIcon != null) return AdjustScale(soulIcon, 1f, out scale);

            if (IconCache.TryGetValue(itemName, out var cached) && cached.Sprite != null)
            {
                scale = cached.Scale;
                return cached.Sprite;
            }

            if (MissingUntil.TryGetValue(itemName, out float retry) && Time.realtimeSinceStartup < retry)
                return fallback;
            string stripped = StripName(itemName);
            string cleanName = Normalize(stripped);

            Sprite sprite = ResolveSprite(cleanName, stripped, itemName, out scale);

            if (sprite != null)
            {
                Debug.Log($"[ItemIcons Debug] SUCCESS for '{itemName}' -> Sprite: '{sprite.name}' (Scale: {scale})");
            }
            else
            {
                scale = 1f;
                MissingUntil[itemName] = Time.realtimeSinceStartup + 2f;
                sprite = fallback;
                Debug.LogWarning($"[ItemIcons Debug] FAILED to resolve icon for: '{itemName}'");
            }

            if (sprite != null && sprite != fallback)
            {
                MissingUntil.Remove(itemName);
                IconCache[itemName] = new CachedIcon { Sprite = sprite, Scale = scale };
            }
            return sprite;
        }

        private static Sprite ResolveSprite(string cleanName, string strippedName, string rawName, out float scale)
        {
            scale = 1f;

            string canonicalName = Normalize(rawName);
            if (Samples.TryGetValue(canonicalName, out string sampleName))
            {
                var sample = CollectionItemSources.FindSample(sampleName);
                if (sample != null && sample.Icon != null) return sample.Icon;
            }
            if (Collectables.TryGetValue(canonicalName, out string collectableName))
            {
                CollectableItem item = CollectableItemManager.GetItemByName(collectableName);
                if (item != null)
                {
                    Sprite icon = item.GetIcon(CollectableItem.ReadSource.GetPopup);
                    if (icon != null) return AdjustScale(icon, item.GetUIMsgIconScale(), out scale);
                }
            }

            if (Mappings.TryGetValue(canonicalName, out string mappedSprite) ||
                Mappings.TryGetValue(strippedName, out mappedSprite) ||
                Mappings.TryGetValue(cleanName, out mappedSprite) ||
                Mappings.TryGetValue("Crest: " + cleanName, out mappedSprite))
            {
                Sprite targetSprite;
                if (SlotColors.TryGetValue(mappedSprite, out ToolItemType color))
                {
                    if (!ColoredSlots.TryGetValue(mappedSprite, out targetSprite) || targetSprite == null)
                    {
                        Sprite original = FindNamedSprite(mappedSprite);
                        if (original != null)
                            ColoredSlots[mappedSprite] = targetSprite = TintSlot(original, color);
                    }
                }
                else targetSprite = FindNamedSprite(mappedSprite);
                if (targetSprite != null) return AdjustScale(targetSprite, 1f, out scale);
            }

            Sprite sprite = FindNamedSprite(cleanName) ?? FindNamedSprite(strippedName);
            return sprite != null ? AdjustScale(sprite, 1f, out scale) : null;
        }

        private static readonly Dictionary<string, string> BossJournalRecords =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Bell Beast", "Bone Beast" },
            { "Bell Eater", "Giant Centipede" },
            { "Broodmother", "Slab Fly Broodmother" },
            { "Clover Dancers", "Clover Dancer" },
            { "Cogwork Dancers", "Clockwork Dancer" },
            { "Crawfather", "Crawfather" },
            { "Crust King Khann", "Coral King" },
            { "Disgraced Chef Lugoli", "Roachkeeper Chef" },
            { "Father of the Flame", "Wisp Pyre Effigy" },
            { "First Sinner", "First Weaver" },
            { "Forebrothers Signis & Gron", "Dock Guard Thrower" },
            { "Fourth Chorus", "Song Golem" },
            { "Grand Mother Silk", "Silk Boss" },
            { "Great Conchflies", "Coral Conch Driller Giant" },
            { "Groal the Great", "Swamp Shaman" },
            { "Gurr the Outcast", "Bone Hunter Trapper" },
            { "Lace (Cradle)", "Lace" },
            { "Lace (Deep Docks)", "Lace" },
            { "Lost Lace", "Lost Lace" },
            { "Garmond and Zaza", "Garmond_Zaza" },
            { "Savage Beastfly (Chapel of the Beast)", "Bone Flyer Giant" },
            { "Savage Beastfly (Far Fields)", "Bone Flyer Giant" },
            { "Shakra", "Shakra" },
            { "Last Judge", "Last Judge" },
            { "Lost Garmond", "Garmond" },
            { "Moorwing", "Vampire Gnat" },
            { "Moss Mother", "Mossbone Mother" },
            { "Nyleth", "Flower Queen" },
            { "Palestag", "Cloverstag White" },
            { "Phantom", "Phantom" },
            { "Pinstress", "Pinstress Boss" },
            { "Plasmified Zango", "Blue Assistant" },
            { "Raging Conchfly", "Coral Conch Driller Giant" },
            { "Second Sentinel", "Song Knight" },
            { "Shrine Guardian Seth", "Seth" },
            { "Sister Splinter", "Splinter Queen" },
            { "Skarrsinger Karmelita", "Hunter Queen" },
            { "Skull Tyrant", "Skull King" },
            { "Skull Tyrant (Bone Bottom)", "Skull King" },
            { "Skull Tyrant (The Marrow)", "Skull King" },
            { "The Unravelled", "Conductor Boss" },
            { "Tormented Trobbio", "Tormented Trobbio" },
            { "Trobbio", "Trobbio" },
            { "Voltvyrm", "Zap Core Enemy" },
            { "Watcher at the Edge", "Coral Warrior Grey" },
            { "Widow", "Spinner Boss" },
            { "Summoned Saviour", "Abyss Mass" },
        };

        private static Sprite GetBossJournalIcon(string itemName)
        {
            string name = Normalize(itemName);
            if (name == BossSoulState.SethSoul) name = "Shrine Guardian Seth";
            else if (name.StartsWith("Boss: ", StringComparison.OrdinalIgnoreCase))
                name = name.Substring("Boss: ".Length);
            else if (name.StartsWith("Boss Credit: ", StringComparison.OrdinalIgnoreCase))
                name = name.Substring("Boss Credit: ".Length);
            else if (name.StartsWith("Soul of ", StringComparison.OrdinalIgnoreCase))
                name = name.Substring("Soul of ".Length);
            else if (name.StartsWith("Story credit: ", StringComparison.OrdinalIgnoreCase))
                name = name.Substring("Story credit: ".Length);
            else
                return null;

            if (name.EndsWith(" (Boss)", StringComparison.Ordinal))
                name = name.Substring(0, name.Length - " (Boss)".Length);
            if (!BossJournalRecords.TryGetValue(name, out string recordName))
                return null;

            EnemyJournalRecord record = EnemyJournalManager.GetRecord(recordName);
            return record != null ? record.IconSprite : null;
        }

        private static string Normalize(string itemName)
        {
            string name = itemName.Replace('_', ' ').Trim();
            if (name.StartsWith("Sent ", StringComparison.Ordinal))
            {
                int sep = name.IndexOf(" to ", 5, StringComparison.Ordinal);
                if (sep > 5) name = name.Substring(5, sep - 5);
            }
            return ItemSet.GetCanonicalItemName(name);
        }

        private static string StripName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;

            string result = name.Trim();

            if (result.StartsWith("Crest:", StringComparison.OrdinalIgnoreCase))
                return result;

            int colonIdx = result.IndexOf(':');
            if (colonIdx > 0) 
                {
                string prefix = result.Substring(0, colonIdx);
                result = Mappings.ContainsKey(prefix) ? prefix : result.Substring(colonIdx + 1).Trim();
            }

            int parenIdx = result.IndexOf('(');
            if (parenIdx > 0) 
                result = result.Substring(0, parenIdx);

            int hashIdx = result.IndexOf('#');
            if (hashIdx > 0) 
                result = result.Substring(0, hashIdx);

            return result.Trim();
        }

        private static Sprite FindNamedSprite(string spriteName)
        {
            if (string.IsNullOrWhiteSpace(spriteName)) return null;

            foreach (var sprite in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (sprite != null && string.Equals(sprite.name, spriteName, StringComparison.OrdinalIgnoreCase))
                {
                    return sprite;
                }
            }
            return null;
        }

        private static Sprite TintSlot(Sprite source, ToolItemType type)
        {
            RenderTexture previous = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            RenderTexture target = null;
            Texture2D texture = null;
            try
            {
                Texture2D atlas = source.texture;
                target = RenderTexture.GetTemporary(atlas.width, atlas.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(atlas, target);
                RenderTexture.active = target;
                int width = Mathf.RoundToInt(source.rect.width);
                int height = Mathf.RoundToInt(source.rect.height);
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = source.name + " " + type,
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = atlas.filterMode,
                    wrapMode = TextureWrapMode.Clamp
                };
                texture.SetPixels32(new Color32[width * height]);
                Rect rect = source.textureRect;
                Vector2 offset = source.textureRectOffset;
                texture.ReadPixels(new Rect(Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y),
                    Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height)),
                    Mathf.RoundToInt(offset.x), Mathf.RoundToInt(offset.y), false);
                Color tint = GlobalSettings.UI.GetToolTypeColor(type);
                Color32[] pixels = texture.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i].r = (byte)Mathf.RoundToInt(pixels[i].r * tint.r);
                    pixels[i].g = (byte)Mathf.RoundToInt(pixels[i].g * tint.g);
                    pixels[i].b = (byte)Mathf.RoundToInt(pixels[i].b * tint.b);
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height),
                    new Vector2(source.pivot.x / width, source.pivot.y / height), source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                Vector2[] vertices = source.vertices;
                for (int i = 0; i < vertices.Length; i++)
                    vertices[i] = vertices[i] * source.pixelsPerUnit + source.pivot;
                sprite.OverrideGeometry(vertices, source.triangles);
                sprite.name = texture.name;
                sprite.hideFlags = HideFlags.HideAndDontSave;
                texture = null;
                return sprite;
            }
            finally
            {
                RenderTexture.active = previous;
                GL.sRGBWrite = previousSrgb;
                if (texture != null) UnityEngine.Object.Destroy(texture);
                if (target != null) RenderTexture.ReleaseTemporary(target);
            }
        }

        private static Sprite AdjustScale(Sprite sprite, float reportedScale, out float scale)
        {
            scale = reportedScale > 0f ? reportedScale : 1f;
            if (sprite == null) return null;

            float maxDim = Mathf.Max(sprite.rect.width, sprite.rect.height) * 100f / sprite.pixelsPerUnit;
            if (maxDim > TargetIconSize)
            {
                scale = Mathf.Min(scale, TargetIconSize / maxDim);
            }
            else if (maxDim > 0f && maxDim < MinIconSize)
            {
                scale = MinIconSize / maxDim;
            }

            return sprite;
        }
    }
}
