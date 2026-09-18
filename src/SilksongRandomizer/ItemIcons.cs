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
            // Skills
            { "Needle Strike", "prompt_nail_art" },
            { "Silk Soar", "Inv_0029_spell_core_outer_icons_0000_1_super_jump" },
            { "Cling Grip", "Inv_0029_spell_core_outer_icons_0000_1_wall_jump" },
            { "Clawline", "Inv_0029_spell_core_outer_icons_0000_1_harpoon_dash" },
            { "Needolin", "Inv_0029_spell_core_outer_icons_0000_1" },
            { "Swift Step", "Inv_0029_spell_core_outer_icons_0000_1_sprint" },
            { "Progressive Swift Step", "Inv_0029_spell_core_outer_icons_0000_1_sprint" },

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
            { "Longpin", "T_longneedle" },
            { "Sawtooth Circlet", "T_cogwork_saw" },
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
            { "Wreath of Purity", "T_attunement_charm" },
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
            { "Dead Bug's Purse", "T_dead_purse" },
            { "Scuttlebrace", "T_steel_spine" },
            { "Shard Pendant", "Hornet_Bone_Necklace" },

            // Skills / Tools
            { "Drifter's Cloak", "I_spine_cloak" },
            { "Faydown Cloak", "I_spine_cloak_down" },
            { "Flintslate", "Hornet_icon_0003_T_flintstone" },
            { "Key of Apostate", "I_slab_key" },
            { "Progressive Tool Pouch", "Inv_tool_pouch_upgrade" },
            { "Magnetite Dice", "_0006_I_magnetite_dice" },
            { "Pollip Heart", "_0007_shell_flower_purple_icon" },
            { "Egg of Flealia", "Flea_Egg" },
            { "Craftmetal", "Hornet_Tool_Metal" },
            { "White Key", "I_key_whiteward" },
            { "Surgeon's Key", "I_chute_key_whiteward" },
            { "Architect's Key", "I_key_architect" },
            { "Progressive Compass", "T_Compass" },
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
            { "Bellway Pins", "Hornet_icon_bell_clapper" },
            { "Vendor Pins", "pin_shop" },

            // Generic Upgrades & Collectables
            { "Mask Shard", "Hornet_Spool_Upgrade_Shop_Icon_Heart" },
            { "Spool Fragment", "Hornet_Spool_Upgrade_Shop_Icon" },
            { "Flea", "Flea_Scoreboard_Icons_0001_Generic" },
            { "Lore", "Hornet_icon_0002_R_bone_record" },
            { "Bellway", "Hornet_icon_bell_clapper" },
            { "Map", "I_map" },
            { "Ventrica", "pin_tube_station" },
            { "Progressive Silkheart", "silk_heart_inv_icon" },
            { "Crest Slot", "UI_tool_slot_attack0000" }, // Need to color these slots but for now a generic icon... attack0000 is Red, explore0000 is Yellow, defend0000 is Blue.

            // Traps
            { "Cursed Crest Trap", "cursed_death0004"},
            { "Naked Trap", "Hornet_Cloakless_Frost_Death0000" },
            { "Rosary Spill Trap", "I_rosary_icon_mid_clean"},
            { "Darkness Trap", "Hornet_death_pieces_0000s_0000_death_spider_core"},

            // Quest Items
            { "Maiden Soul", "snail_icon__0000_churchkeeper_soul" },
            { "Hermit Soul", "snail_icon__0001_bell_hermit_soul" },
            { "Seeker Soul", "snail_icon__0001_bell_swamp_soul" },
            { "Pollen Heart", "flower_queen_heart_icon0000" },
            { "Hunter's Heart", "ant_queen_heart_icon0002" },
            { "Encrusted Heart", "coral_king_heart_icon0000" },
            { "Twisted Bud", "mandrake_icon0000" },
        };

        private static readonly Dictionary<string, CachedIcon> IconCache = 
            new Dictionary<string, CachedIcon>(StringComparer.OrdinalIgnoreCase);

        private struct CachedIcon
        {
            public Sprite Sprite;
            public float Scale;
        }

        public static string GetSpriteName(string itemName)
            => Mappings.TryGetValue(itemName, out string spriteName) ? spriteName : null;

        public static Sprite GetSprite(string itemName, Sprite fallback)
            => GetSprite(itemName, fallback, out _);

        public static Sprite GetSprite(string itemName, Sprite fallback, out float scale)
        {
            scale = 1f;
            if (string.IsNullOrWhiteSpace(itemName)) return fallback;

            if (IconCache.TryGetValue(itemName, out var cached))
            {
                scale = cached.Scale;
                return cached.Sprite;
            }

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
                sprite = fallback;
                Debug.LogWarning($"[ItemIcons Debug] FAILED to resolve icon for: '{itemName}'");
            }

            IconCache[itemName] = new CachedIcon { Sprite = sprite, Scale = scale };
            return sprite;
        }

        private static Sprite ResolveSprite(string cleanName, string strippedName, string rawName, out float scale)
        {
            scale = 1f;

            if (Mappings.TryGetValue(rawName, out string mappedSprite) ||
                Mappings.TryGetValue(strippedName, out mappedSprite) ||
                Mappings.TryGetValue(cleanName, out mappedSprite) ||
                Mappings.TryGetValue("Crest: " + cleanName, out mappedSprite))
            {
                Sprite targetSprite = FindNamedSprite(mappedSprite);
                if (targetSprite != null) return AdjustScale(targetSprite, 1f, out scale);
            }

            Sprite sprite = FindNamedSprite(cleanName) ?? FindNamedSprite(strippedName);
            return sprite != null ? AdjustScale(sprite, 1f, out scale) : null;
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
                result = result.Substring(0, colonIdx);

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