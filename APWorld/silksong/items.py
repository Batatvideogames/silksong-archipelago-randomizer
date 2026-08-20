from __future__ import annotations

from dataclasses import dataclass
from random import Random
from typing import Dict, FrozenSet, Mapping

from BaseClasses import Item, ItemClassification

from .act1_scope import (
    get_act_one_excluded_location_names,
    retains_randomized_craw_summons,
)
from .act2_scope import (
    ACT_TWO_EXCLUDED_LOCATION_NAMES,
    trim_act_two_pool_entries,
)
from .display_names import clean_item_display_name
from .minor_families import (
    MINOR_FAMILY_REWARD_COUNTS,
    MINOR_FAMILY_SHUFFLE_CATEGORIES,
    MINOR_REWARD_BY_LOCATION,
    MINOR_REWARD_COUNTS,
    get_base_randomization_category,
    get_minor_family_shuffle_category_for_location,
)
from .locations import (
    INDIVIDUAL_RELIC_TURN_IN_ITEM_NAMES,
    LOCATION_NAMES_BY_CATEGORY,
    MASK_SHARD_LOCATION_NAMES,
    NEEDLE_UPGRADE_LOCATION_CATEGORY,
    OBSERVATION_LOCATION_CATEGORIES,
    RELIC_TURN_IN_LOCATION_CATEGORY,
    SPOOL_FRAGMENT_LOCATION_NAMES,
    canonicalize_location_name,
    get_item_first_location_name,
    location_data_table,
)
from .lore_tablets import (
    LORE_TABLET_CATEGORY,
    LORE_TABLET_ITEM_BY_LOCATION,
    LORE_TABLET_ITEM_NAMES,
)
from .requirements import (
    SIMPLE_KEY_GREEN_PRINCE,
    get_logic_item_references,
)
from .verdania_scope import VERDANIA_LOCATION_NAMES

ITEM_BASE_ID = 835000


@dataclass(frozen=True)
class SilksongItemData:
    code: int
    category: str
    classification: ItemClassification


CURRENT_ITEM_SOURCE_ROWS: tuple[tuple[str, str], ...] = (
    ('Ability: Faydown Cloak', 'Skill'),
    ('Ability: Needle Strike', 'Skill'),
    ('Ancestral Art: Silk Soar', 'Skill'),
    ('Ancestral Art: Cling Grip', 'Skill'),
    ("Ability: Drifter's Cloak", 'Skill'),
    ('Ancestral Art: Swift Step', 'Skill'),
    ('Ancestral Art: Clawline', 'Skill'),
    ('Item: Quill', 'Skill'),
    ('Ancestral Art: Needolin', 'Skill'),
    ('Tool: Silkshot (Forge Daughter)', 'Tool'),
    ('Tool: Silkshot (Twelfth Architect)', 'Tool'),
    ('Tool: Silkshot (Original)', 'Tool'),
    ('Tool: Volt Filament', 'Tool'),
    ('Tool: Tacks', 'Tool'),
    ('Tool: Pollip Pouch', 'Tool'),
    ('Tool: Snare Setter', 'Tool'),
    ('Tool: Wispfire Lantern', 'Tool'),
    ('Tool: Memory Crystal', 'Tool'),
    ('Tool: Voltvessels', 'Tool'),
    ('Tool: Threefold Pin', 'Tool'),
    ('Tool: Warding Bell', 'Tool'),
    ('Tool: Rosary Cannon', 'Tool'),
    ('Tool: Longpin', 'Tool'),
    ('Tool: Sawtooth Circlet', 'Tool'),
    ('Progressive Claw Mirror', 'Tool'),
    ('Tool: Throwing Ring', 'Tool'),
    ("Tool: Delver's Drill", 'Tool'),
    ('Tool: Quick Sling', 'Tool'),
    ('Tool: Spider Strings', 'Tool'),
    ('Tool: Fractured Mask', 'Tool'),
    ('Tool: Silkspeed Anklets', 'Tool'),
    ('Tool: Weighted Belt', 'Tool'),
    ('Tool: Multibinder', 'Tool'),
    ('Tool: Reserve Bind', 'Tool'),
    ('Tool: Injector Band', 'Tool'),
    ('Tool: Barbed Bracelet', 'Tool'),
    ('Tool: Sting Shard', 'Tool'),
    ('Tool: Conchcutter', 'Tool'),
    ('Tool: Pimpillo', 'Tool'),
    ('Tool: Straight Pin', 'Tool'),
    ('Tool: Cogfly', 'Tool'),
    ('Progressive Curveclaw', 'Tool'),
    ('Tool: Cogwork Wheel', 'Tool'),
    ('Tool: Flea Brew', 'Tool'),
    ('Tool: Magnetite Dice', 'Tool'),
    ('Tool: Magnetite Brooch', 'Tool'),
    ('Tool: Weavelight', 'Tool'),
    ('Tool: Pin Badge', 'Tool'),
    ('Tool: Needle Phial', 'Tool'),
    ('Tool: Plasmium Phial', 'Tool'),
    ('Tool: Shell Satchel', 'Tool'),
    ("Tool: Dead Bug's Purse", 'Tool'),
    ('Tool: Scuttlebrace', 'Tool'),
    ("Tool: Thief's Mark", 'Tool'),
    ('Tool: Compass', 'Tool'),
    ('Tool: Snitch Pick', 'Tool'),
    ('Tool: Flintslate', 'Tool'),
    ('Tool: Wreath of Purity', 'Tool'),
    ('Tool: Magma Bell', 'Tool'),
    ("Tool: Ascendant's Grip", 'Tool'),
    ('Tool: Longclaw', 'Tool'),
    ('Tool: Shard Pendant', 'Tool'),
    ('Tool: Spool Extender', 'Tool'),
    ('Tool: Egg of Flealia', 'Tool'),
    ('Silk Skill: Silkspear', 'Spell'),
    ('Silk Skill: Cross Stitch', 'Spell'),
    ('Silk Skill: Pale Nails', 'Spell'),
    ('Silk Skill: Sharpdart', 'Spell'),
    ('Silk Skill: Rune Rage', 'Spell'),
    ('Silk Skill: Thread Storm', 'Spell'),
    ('Crest: Witch', 'Crest'),
    ('Crest: Beast', 'Crest'),
    ('Crest: Architect', 'Crest'),
    ('Crest: Shaman', 'Crest'),
    ('Crest: Reaper', 'Crest'),
    ('Crest: Wanderer', 'Crest'),
    ('Crest: Hunter', 'Crest'),
    ('Flea: The Marrow', 'Flea'),
    ('Flea: Deep Docks (Bellway)', 'Flea'),
    ('Flea: Deep Docks (Weaver Burial Spire)', 'Flea'),
    ('Flea: Far Fields (Captured)', 'Flea'),
    ("Flea: Hunter's March", 'Flea'),
    ('Flea: Greymoor (Craw Lake)', 'Flea'),
    ('Flea: Greymoor (Tower)', 'Flea'),
    ('Flea: Shellwood', 'Flea'),
    ("Flea: Pilgrim's Rest", 'Flea'),
    ('Flea: Blasted Steps', 'Flea'),
    ("Flea: Sinner's Road", 'Flea'),
    ('Flea: Exhaust Organ', 'Flea'),
    ('Flea: Bellhart', 'Flea'),
    ('Flea: Wormways', 'Flea'),
    ('Flea: The Slab (Cell)', 'Flea'),
    ('Flea: Bilewater (Thieves)', 'Flea'),
    ('Flea: Deep Docks (Mines)', 'Flea'),
    ('Flea: Wisp Thicket', 'Flea'),
    ('Flea: Bilehaven', 'Flea'),
    ('Flea: Choral Chambers (Spa)', 'Flea'),
    ('Flea: Sands of Karak', 'Flea'),
    ('Flea: Mount Fay', 'Flea'),
    ('Flea: Songclave', 'Flea'),
    ('Flea: Choral Chambers (Walled Room)', 'Flea'),
    ('Flea: Whispering Vaults', 'Flea'),
    ('Flea: Underworks', 'Flea'),
    ('Flea: The Slab (Bellway)', 'Flea'),
    ('Crest Slot: Hunter (Red 1)', 'CrestSlot'),
    ('Crest Slot: Hunter (Blue 1)', 'CrestSlot'),
    ('Crest Slot: Hunter (Yellow 1)', 'CrestSlot'),
    ('Crest Slot: Reaper (Red 1)', 'CrestSlot'),
    ('Crest Slot: Reaper (Blue 1)', 'CrestSlot'),
    ('Crest Slot: Reaper (Yellow 1)', 'CrestSlot'),
    ('Crest Slot: Wanderer (Blue 1)', 'CrestSlot'),
    ('Crest Slot: Wanderer (Blue 2)', 'CrestSlot'),
    ('Crest Slot: Wanderer (Yellow 1)', 'CrestSlot'),
    ('Crest Slot: Beast (Yellow 1)', 'CrestSlot'),
    ('Crest Slot: Beast (Yellow 2)', 'CrestSlot'),
    ('Crest Slot: Witch (Red 1)', 'CrestSlot'),
    ('Crest Slot: Witch (Blue 1)', 'CrestSlot'),
    ('Crest Slot: Witch (Blue 2)', 'CrestSlot'),
    ('Crest Slot: Architect (Blue 1)', 'CrestSlot'),
    ('Crest Slot: Architect (Yellow 1)', 'CrestSlot'),
    ('Crest Slot: Architect (Yellow 2)', 'CrestSlot'),
    ('Crest Slot: Architect (Blue 2)', 'CrestSlot'),
    ('Crest Slot: Shaman (Blue 1)', 'CrestSlot'),
    ('Crest Slot: Shaman (Blue 2)', 'CrestSlot'),
    ('Mask Shard #1', 'MaskShard'),
    ('Mask Shard #2', 'MaskShard'),
    ('Mask Shard #3', 'MaskShard'),
    ('Mask Shard #4', 'MaskShard'),
    ('Mask Shard #5', 'MaskShard'),
    ('Mask Shard #6', 'MaskShard'),
    ('Mask Shard #7', 'MaskShard'),
    ('Mask Shard #8', 'MaskShard'),
    ('Mask Shard #9', 'MaskShard'),
    ('Mask Shard #10', 'MaskShard'),
    ('Mask Shard #11', 'MaskShard'),
    ('Mask Shard #12', 'MaskShard'),
    ('Mask Shard #13', 'MaskShard'),
    ('Mask Shard #14', 'MaskShard'),
    ('Mask Shard #15', 'MaskShard'),
    ('Mask Shard #16', 'MaskShard'),
    ('Mask Shard #17', 'MaskShard'),
    ('Mask Shard #18', 'MaskShard'),
    ('Mask Shard #19', 'MaskShard'),
    ('Mask Shard #20', 'MaskShard'),
    ('Spool Fragment #1', 'SpoolFragment'),
    ('Spool Fragment #2', 'SpoolFragment'),
    ('Spool Fragment #3', 'SpoolFragment'),
    ('Spool Fragment #4', 'SpoolFragment'),
    ('Spool Fragment #5', 'SpoolFragment'),
    ('Spool Fragment #6', 'SpoolFragment'),
    ('Spool Fragment #7', 'SpoolFragment'),
    ('Spool Fragment #8', 'SpoolFragment'),
    ('Spool Fragment #9', 'SpoolFragment'),
    ('Spool Fragment #10', 'SpoolFragment'),
    ('Spool Fragment #11', 'SpoolFragment'),
    ('Spool Fragment #12', 'SpoolFragment'),
    ('Spool Fragment #13', 'SpoolFragment'),
    ('Spool Fragment #14', 'SpoolFragment'),
    ('Spool Fragment #15', 'SpoolFragment'),
    ('Spool Fragment #16', 'SpoolFragment'),
    ('Spool Fragment #17', 'SpoolFragment'),
    ('Spool Fragment #18', 'SpoolFragment'),
    ('Bellway: Deep Docks', 'Bellway'),
    ('Bellway: Far Fields', 'Bellway'),
    ('Bellway: Greymoor', 'Bellway'),
    ('Bellway: Bellhart', 'Bellway'),
    ('Bellway: Blasted Steps', 'Bellway'),
    ('Bellway: Grand Bellway', 'Bellway'),
    ('Bellway: The Slab', 'Bellway'),
    ('Bellway: Shellwood', 'Bellway'),
    ('Bellway: Bilewater', 'Bellway'),
    ('Bellway: Putrified Ducts', 'Bellway'),
    ('Ventrica: Choral Chambers', 'Ventrica'),
    ('Ventrica: Underworks', 'Ventrica'),
    ('Ventrica: Grand Bellway', 'Ventrica'),
    ('Ventrica: High Halls', 'Ventrica'),
    ('Ventrica: Songclave', 'Ventrica'),
    ('Ventrica: Memorium', 'Ventrica'),
    # Maps sold by Shakra. These entries are her carried stock.
    ('Map: Mosslands', 'Map'),
    ('Map: The Marrow', 'Map'),
    ('Map: Deep Docks', 'Map'),
    ('Map: Far Fields', 'Map'),
    ('Map: Wormways', 'Map'),
    ("Map: Hunter's March", 'Map'),
    ('Map: Greymoor', 'Map'),
    ('Map: Bellhart', 'Map'),
    ('Map: Shellwood', 'Map'),
    ('Map: Blasted Steps', 'Map'),
    ("Map: Sinner's Road", 'Map'),
    ('Map: Mount Fay', 'Map'),
    ('Map: Sands of Karak', 'Map'),
    ('Map: Bilewater', 'Map'),
    # All four native Crafting Kit sources can be suppressed and award this
    # ordered progressive upgrade.
    ('Progressive Crafting Kit', 'Upgrade'),
    # Named mapping pins only. The generic A-E player markers stay vanilla.
    ('Bench Pins', 'Pin'),
    ('Ventrica Pins', 'Pin'),
    ('Bellway Pins', 'Pin'),
    ('Vendor Pins', 'Pin'),
    # Relic identities come from the native SavedItem assets.
    ('Relic: Weaver Effigy (Keelal, Shellwood)', 'Relic'),
    ('Relic: Psalm Cylinder (East Whispering Vaults)', 'Relic'),
    ('Relic: Bone Scroll (Wisp Thicket)', 'Relic'),
    ('Relic: Weaver Effigy (Camora, Moss Grotto)', 'Relic'),
    ('Relic: Sacred Cylinder', 'Relic'),
    ('Relic: Choral Commandment (Jubilana)', 'Relic'),
    ('Relic: Psalm Cylinder (Underworks)', 'Relic'),
    ('Relic: Rune Harp (High Halls)', 'Relic'),
    ('Relic: Psalm Cylinder (Vaultkeeper Cardinius)', 'Relic'),
    ('Relic: Psalm Cylinder (High Halls)', 'Relic'),
    ('Relic: Choral Commandment (Western Whiteward)', 'Relic'),
    ('Relic: Rune Harp (Weavenest Cindril)', 'Relic'),
    ('Relic: Psalm Cylinder (Grindle)', 'Relic'),
    ('Relic: Rune Harp (Weavenest Atla)', 'Relic'),
    ('Relic: Bone Scroll (Underworks)', 'Relic'),
    ('Relic: Bone Scroll (Far Fields)', 'Relic'),
    ('Relic: Choral Commandment (Moss Grotto)', 'Relic'),
    ('Relic: Choral Commandment (Eastern Whiteward)', 'Relic'),
    ('Relic: Bone Scroll (Greymoor)', 'Relic'),
    ('Relic: Weaver Effigy (Atla, The Slab)', 'Relic'),
    ('Relic: Arcane Egg', 'Relic'),
    ('Rosaries (60)', 'Currency'),
    ('Shell Shards (80)', 'Currency'),
    ('Stagger Trap', 'Trap'),
    ('Rosary Spill Trap', 'Trap'),
    ('Darkness Trap', 'Trap'),
    ('Cursed Crest Trap', 'Trap'),
    ('Muckmaggot Status Trap', 'Trap'),
    ('Progressive Swift Step', 'Skill'),
    ('Progressive Needle Upgrade', 'NeedleUpgrade'),
    ('Pale Oil', 'NeedleUpgrade'),
    ("Progressive Druid's Eyes", 'Upgrade'),
    # automatic_compass supplies the position-marker half at the start.
    # Receiving this conditional Skill grants Quill's map-updating half.
    ('Progressive Compass', 'Skill'),
    # Appended one-time consumables. Duplicate physical sources share these
    # repeatable AP identities.
    ('Frayed Rosary String', 'Resource'),
    ('Rosary String', 'Resource'),
    ('Rosary Necklace', 'Resource'),
    ('Heavy Rosary Necklace', 'Resource'),
    ('Pale Rosary Necklace', 'Resource'),
    ('Shard Bundle', 'Resource'),
    ('Beast Shard', 'Resource'),
    ('Pristine Core', 'Resource'),
    # Breakable rosary and shell caches use compact repeatable filler.
    ('Rosaries (10)', 'Resource'),
    ('Shell Shards (10)', 'Resource'),
    # Shared prerequisite for the three mutually exclusive vanilla Silkshot
    # repairs.  Randomized seeds return it after the first two active repairs,
    # allowing every AP repair check to remain physically completable.
    ('Ruined Tool', 'Tool'),
    # Each AP item opens exactly one vanilla Simple Key door. Keys are never
    # represented as a fungible, consumable counter.
    ('Simple Key (Wormways)', 'Key'),
    ('Simple Key (Deep Docks)', 'Key'),
    ('Simple Key (Green Prince)', 'Key'),
    ('Simple Key (Rosary Bank)', 'Key'),
    # Static maps use their own physical pickups or map machines.
    ('Map: Weavenest Atla', 'Map'),
    ('Map: Grand Gate', 'Map'),
    ('Map: Underworks', 'Map'),
    ('Map: Choral Chambers', 'Map'),
    ('Map: Whispering Vaults', 'Map'),
    ('Map: Whiteward', 'Map'),
    ('Map: Cogwork Core', 'Map'),
    ('Map: Memorium', 'Map'),
    ('Map: High Halls', 'Map'),
    ('Map: The Slab', 'Map'),
    ('Map: Putrified Ducts', 'Map'),
    ('Map: The Cradle', 'Map'),
    ('Map: Verdania', 'Map'),
    ('Map: The Abyss', 'Map'),
    # Learned songs have distinct gameplay effects and their own source
    # category, separate from movement abilities and Silk Skills.
    ("Architect's Melody", 'Melody'),
    ("Conductor's Melody", 'Melody'),
    ("Vaultkeeper's Melody", 'Melody'),
    ('Elegy of the Deep', 'Melody'),
    ('Beastling Call', 'Melody'),
    ('Memory Locket', 'MemoryLocket'),
    ('Craftmetal', 'Craftmetal'),
    ('Mossberry', 'Mossberry'),
    ('Silkeater', 'Silkeater'),
    ('Key of Indolent', 'MajorKey'),
    ('Key of Heretic', 'MajorKey'),
    ('Key of Apostate', 'MajorKey'),
    ('White Key', 'MajorKey'),
    ("Surgeon's Key", 'MajorKey'),
    ("Architect's Key", 'MajorKey'),
    ('Craw Summons', 'MajorKey'),
    # The three named caravan recruits use their own persistent world-state
    # flags, but are Flea items for pool, milestone and Flea Hunt purposes.
    ('Flea: Greymoor (Kratt)', 'Flea'),
    ('Flea: Putrified Ducts (Vog)', 'Flea'),
    ('Flea: Memorium (Huge Flea)', 'Flea'),
    ('Naked Trap', 'Trap'),
    # Three copies of this ordered reward match the three Silk Heart sources.
    ('Progressive Silkheart', 'SilkHeart'),
    ('Progressive Tool Pouch', 'ToolPouch'),
    # Six copies share one item ID. Receipt order is preserved by
    # the AP item index and the client grants one native Shell Flower each.
    ('Pollip Heart', 'PollipHeart'),
    ('Growstone', 'Resource'),
    ('Rosaries (8)', 'Resource'),
    ('Rosaries (30)', 'Resource'),
    ('Rosaries (70)', 'Resource'),
    ('Rosaries (75)', 'Resource'),
    ('Rosaries (84)', 'Resource'),
    ('Rosaries (90)', 'Resource'),
    ('Rosaries (105)', 'Resource'),
    ('Rosaries (112)', 'Resource'),
    ('Rosaries (115)', 'Resource'),
    ('Rosaries (130)', 'Resource'),
    ('Rosaries (155)', 'Resource'),
    ('Shell Shards (35)', 'Resource'),
    ('Bell: The Marrow', 'BellShrine'),
    ('Bell: Deep Docks', 'BellShrine'),
    ('Bell: Greymoor', 'BellShrine'),
    ('Bell: Shellwood', 'BellShrine'),
    ('Bell: Bellhart', 'BellShrine'),
)

ITEM_TABLE_SOURCE: tuple[tuple[str, str], ...] = tuple(
    (clean_item_display_name(name), category)
    for name, category in CURRENT_ITEM_SOURCE_ROWS
) + tuple(
    (item_name, LORE_TABLET_CATEGORY)
    for item_name in LORE_TABLET_ITEM_NAMES
)

# Rename in place so every established numeric item ID remains unchanged.
# Item IDs stay tied to the ITEM_TABLE_SOURCE positions above. Native asset
# identifiers resolve to concise protocol names without assigning new IDs.
NATIVE_ITEM_NAME_TO_TABLE_NAME: Mapping[str, str] = {
    'Skill: Double Jump': 'Ability: Faydown Cloak',
    'Skill: Charge Slash': 'Ability: Needle Strike',
    'Skill: Silk Soar': 'Ancestral Art: Silk Soar',
    'Skill: Wall Jump': 'Ancestral Art: Cling Grip',
    'Skill: Dash': 'Ancestral Art: Swift Step',
    'Skill: Harpoon': 'Ancestral Art: Clawline',
    'Skill: Quill': 'Item: Quill',
    'Skill: Needolin': 'Ancestral Art: Needolin',
    'Tool: WebShot Forge': 'Tool: Silkshot (Forge Daughter)',
    'Tool: WebShot Architect': 'Tool: Silkshot (Twelfth Architect)',
    'Tool: WebShot Weaver': 'Tool: Silkshot (Original)',
    'Tool: Zap Imbuement': 'Tool: Volt Filament',
    'Tool: Tack': 'Tool: Tacks',
    'Tool: Poison Pouch': 'Tool: Pollip Pouch',
    'Tool: Silk Snare': 'Tool: Snare Setter',
    'Tool: Wisp Lantern': 'Tool: Wispfire Lantern',
    'Tool: Revenge Crystal': 'Tool: Memory Crystal',
    'Tool: Lightning Rod': 'Tool: Voltvessels',
    'Tool: Tri Pin': 'Tool: Threefold Pin',
    'Tool: Bell Bind': 'Tool: Warding Bell',
    'Tool: Harpoon': 'Tool: Longpin',
    'Tool: Brolly Spike': 'Tool: Sawtooth Circlet',
    'Tool: Dazzle Bind': 'Progressive Claw Mirror',
    'Tool: Dazzle Bind Upgraded': 'Progressive Claw Mirror',
    'Tool: Shakra Ring': 'Tool: Throwing Ring',
    'Tool: Screw Attack': "Tool: Delver's Drill",
    'Tool: Musician Charm': 'Tool: Spider Strings',
    'Tool: Sprintmaster': 'Tool: Silkspeed Anklets',
    'Tool: Weighted Anklet': 'Tool: Weighted Belt',
    'Tool: Multibind': 'Tool: Multibinder',
    'Tool: Quickbind': 'Tool: Injector Band',
    'Tool: Barbed Wire': 'Tool: Barbed Bracelet',
    'Tool: Conch Drill': 'Tool: Conchcutter',
    'Tool: Pimpilo': 'Tool: Pimpillo',
    'Tool: Cogwork Flier': 'Tool: Cogfly',
    'Tool: Curve Claws': 'Progressive Curveclaw',
    'Tool: Curve Claws Upgraded': 'Progressive Curveclaw',
    'Tool: Cogwork Saw': 'Tool: Cogwork Wheel',
    'Tool: Rosary Magnet': 'Tool: Magnetite Brooch',
    'Tool: White Ring': 'Tool: Weavelight',
    'Tool: Pinstress Tool': 'Tool: Pin Badge',
    'Tool: Extractor': 'Tool: Needle Phial',
    'Tool: Lifeblood Syringe': 'Tool: Plasmium Phial',
    'Tool: Dead Mans Purse': "Tool: Dead Bug's Purse",
    'Tool: Thief Charm': "Tool: Thief's Mark",
    'Tool: Thief Claw': 'Tool: Snitch Pick',
    'Tool: Mosscreep Tool 1': "Progressive Druid's Eyes",
    'Tool: Mosscreep Tool 2': "Progressive Druid's Eyes",
    'Tool: Flintstone': 'Tool: Flintslate',
    'Tool: Maggot Charm': 'Tool: Wreath of Purity',
    'Tool: Lava Charm': 'Tool: Magma Bell',
    'Tool: Wallcling': "Tool: Ascendant's Grip",
    'Tool: Longneedle': 'Tool: Longclaw',
    'Tool: Bone Necklace': 'Tool: Shard Pendant',
    'Tool: Flea Charm': 'Tool: Egg of Flealia',
    'Spell: Silk Spear': 'Silk Skill: Silkspear',
    'Spell: Parry': 'Silk Skill: Cross Stitch',
    'Spell: Silk Boss Needle': 'Silk Skill: Pale Nails',
    'Spell: Silk Charge': 'Silk Skill: Sharpdart',
    'Spell: Silk Bomb': 'Silk Skill: Rune Rage',
    'Spell: Thread Sphere': 'Silk Skill: Thread Storm',
    'Flea: The Marrow (Tangle)': 'Flea: The Marrow',
    "Flea: Hunter's March (Yapper)": "Flea: Hunter's March",
    'Flea: Shellwood (Shelly)': 'Flea: Shellwood',
    "Flea: Pilgrim's Rest (Sleeby)": "Flea: Pilgrim's Rest",
    'Flea: Blasted Steps (Nini)': 'Flea: Blasted Steps',
    "Flea: Sinner's Road (Stelf)": "Flea: Sinner's Road",
    'Flea: Exhaust Organ (Rangle)': 'Flea: Exhaust Organ',
    'Flea: Bellhart (Bellphy)': 'Flea: Bellhart',
    'Flea: Wormways (Snacc)': 'Flea: Wormways',
    'Flea: Bilehaven (Spangle)': 'Flea: Bilehaven',
    'Flea: Sands of Karak (Crustly)': 'Flea: Sands of Karak',
    'Flea: Mount Fay (Ice Cube)': 'Flea: Mount Fay',
    'Flea: Songclave (Groggy)': 'Flea: Songclave',
    'Flea: Whispering Vaults (Birdy)': 'Flea: Whispering Vaults',
    'Flea: Underworks (Boomy)': 'Flea: Underworks',
    'Hunter Slot: Red 0 -2': 'Crest Slot: Hunter (Red 1)',
    'Hunter Slot: Blue -2 0': 'Crest Slot: Hunter (Blue 1)',
    'Hunter Slot: Yellow 2 0': 'Crest Slot: Hunter (Yellow 1)',
    'Reaper Slot: Red 0 -1': 'Crest Slot: Reaper (Red 1)',
    'Reaper Slot: Blue -1 1': 'Crest Slot: Reaper (Blue 1)',
    'Reaper Slot: Yellow 1 1': 'Crest Slot: Reaper (Yellow 1)',
    'Wanderer Slot: Blue -2 1': 'Crest Slot: Wanderer (Blue 1)',
    'Wanderer Slot: Blue 2 1': 'Crest Slot: Wanderer (Blue 2)',
    'Wanderer Slot: Yellow 0 -2': 'Crest Slot: Wanderer (Yellow 1)',
    'Beast Slot: Yellow -1 0': 'Crest Slot: Beast (Yellow 1)',
    'Beast Slot: Yellow 1 0': 'Crest Slot: Beast (Yellow 2)',
    'Witch Slot: Red 0 -2': 'Crest Slot: Witch (Red 1)',
    'Witch Slot: Blue -1 0': 'Crest Slot: Witch (Blue 1)',
    'Witch Slot: Blue 1 0': 'Crest Slot: Witch (Blue 2)',
    'Architect Slot: Blue -1 2': 'Crest Slot: Architect (Blue 1)',
    'Architect Slot: Yellow 1 2': 'Crest Slot: Architect (Yellow 1)',
    'Architect Slot: Yellow 2 0': 'Crest Slot: Architect (Yellow 2)',
    'Architect Slot: Blue -2 0': 'Crest Slot: Architect (Blue 2)',
    'Shaman Slot: Blue -1 0': 'Crest Slot: Shaman (Blue 1)',
    'Shaman Slot: Blue 1 0': 'Crest Slot: Shaman (Blue 2)',
    'Pin: Bench': 'Bench Pins',
    'Pin: Ventrica': 'Ventrica Pins',
    'Pin: Bellway': 'Bellway Pins',
    'Pin: Vendor': 'Vendor Pins',
    'Relic: Weaver Totem Witch':
        'Relic: Weaver Effigy (Keelal, Shellwood)',
    'Relic: Psalm Cylinder Library Roof':
        'Relic: Psalm Cylinder (East Whispering Vaults)',
    'Relic: Bone Record Wisp Top':
        'Relic: Bone Scroll (Wisp Thicket)',
    'Relic: Weaver Totem Bonetown_upper_room':
        'Relic: Weaver Effigy (Camora, Moss Grotto)',
    'Relic: Librarian Melody Cylinder': 'Relic: Sacred Cylinder',
    'Relic: Seal Chit City Merchant':
        'Relic: Choral Commandment (Jubilana)',
    'Relic: Psalm Cylinder Ward': 'Relic: Psalm Cylinder (Underworks)',
    'Relic: Weaver Record Conductor': 'Relic: Rune Harp (High Halls)',
    'Relic: Psalm Cylinder Librarian':
        'Relic: Psalm Cylinder (Vaultkeeper Cardinius)',
    'Relic: Psalm Cylinder Hang': 'Relic: Psalm Cylinder (High Halls)',
    'Relic: Seal Chit Ward Corpse':
        'Relic: Choral Commandment (Western Whiteward)',
    'Relic: Weaver Record Sprint_Challenge':
        'Relic: Rune Harp (Weavenest Cindril)',
    'Relic: Psalm Cylinder Grindle': 'Relic: Psalm Cylinder (Grindle)',
    'Relic: Weaver Record Weave_08': 'Relic: Rune Harp (Weavenest Atla)',
    'Relic: Bone Record Understore_Map_Room':
        'Relic: Bone Scroll (Underworks)',
    'Relic: Bone Record Bone_East_14':
        'Relic: Bone Scroll (Far Fields)',
    'Relic: Seal Chit Aspid_01':
        'Relic: Choral Commandment (Moss Grotto)',
    'Relic: Seal Chit Silk Siphon':
        'Relic: Choral Commandment (Eastern Whiteward)',
    'Relic: Bone Record Greymoor_flooded_corridor':
        'Relic: Bone Scroll (Greymoor)',
    'Relic: Weaver Totem Slab_Bottom':
        'Relic: Weaver Effigy (Atla, The Slab)',
    'Relic: Ancient Egg Abyss Middle': 'Relic: Arcane Egg',
}

NATIVE_ITEM_NAME_TO_CURRENT: Mapping[str, str] = {
    native_name: clean_item_display_name(table_name)
    for native_name, table_name in NATIVE_ITEM_NAME_TO_TABLE_NAME.items()
}
NATIVE_ITEM_NAME_TO_CURRENT = {
    **NATIVE_ITEM_NAME_TO_CURRENT,
    **{
        source_name: current_name
        for source_name, _category in CURRENT_ITEM_SOURCE_ROWS
        if (
            current_name := clean_item_display_name(source_name)
        ) != source_name
    },
    # These are the native ToolItem identities used by the two physical
    # Mosscreep sources. Current rooms award the one progressive item.
    'Tool: Mosscreep Tool 1': "Progressive Druid's Eyes",
    'Tool: Mosscreep Tool 2': "Progressive Druid's Eyes",
}

POOL_ONLY_USEFUL_ITEM_NAMES: tuple[str, ...] = (
    'Shell Satchel',
    'Growstone',
)
POOL_ONLY_FILLER_SOURCE_CATEGORIES: tuple[str, ...] = (
    'Quest',
    'Boss',
    RELIC_TURN_IN_LOCATION_CATEGORY,
)

PROGRESSION_ITEMS: FrozenSet[str] = frozenset(
    name
    for name, category in ITEM_TABLE_SOURCE
    if category in {
        "Skill",
        "Flea",
        "Crest",
        "Key",
        "Map",
        "Melody",
        "MemoryLocket",
        "Craftmetal",
        "Mossberry",
        "PollipHeart",
        "MajorKey",
        "ToolPouch",
    }
) | (
    get_logic_item_references()
    - frozenset(INDIVIDUAL_RELIC_TURN_IN_ITEM_NAMES)
) | frozenset({
    'Relic: Sacred Cylinder',
    'Progressive Crafting Kit',
})

USEFUL_ITEMS: FrozenSet[str] = frozenset(
    name
    for name, category in ITEM_TABLE_SOURCE
    if name not in {
        'Relic: Sacred Cylinder',
        'Progressive Crafting Kit',
    } and category in {
        "Tool",
        "Spell",
        "CrestSlot",
        "MaskShard",
        "SpoolFragment",
        "SilkHeart",
        "Upgrade",
        "Pin",
        "Relic",
        "Silkeater",
        "BellShrine",
    }
) | frozenset(POOL_ONLY_USEFUL_ITEM_NAMES)

item_table: Dict[str, int] = {
    name: ITEM_BASE_ID + index
    for index, (name, _category) in enumerate(ITEM_TABLE_SOURCE)
}

VICTORY_ITEM_NAME = "Victory"
ITEM_CATEGORY_BY_NAME: Dict[str, str] = dict(ITEM_TABLE_SOURCE)

VANILLA_ONLY_ITEM_NAMES: FrozenSet[str] = frozenset({
    'Key of Indolent',
    'Key of Heretic',
})

item_name_groups: Dict[str, set[str]] = {
    "Skills": {name for name, category in ITEM_TABLE_SOURCE if category == "Skill"},
    "Spells": {name for name, category in ITEM_TABLE_SOURCE if category == "Spell"},
    "Crests": {name for name, category in ITEM_TABLE_SOURCE if category == "Crest"},
    "Tools": {
        name
        for name, category in ITEM_TABLE_SOURCE
        if category == "Tool" and name not in VANILLA_ONLY_ITEM_NAMES
    },
    "Fleas": {name for name, category in ITEM_TABLE_SOURCE if category == "Flea"},
    "MaskShard": {name for name, category in ITEM_TABLE_SOURCE if category == "MaskShard"},
    "SpoolFragment": {name for name, category in ITEM_TABLE_SOURCE if category == "SpoolFragment"},
    "SilkHeart": {
        name
        for name, category in ITEM_TABLE_SOURCE
        if category == "SilkHeart" and name not in VANILLA_ONLY_ITEM_NAMES
    },
    "Bellway": {name for name, category in ITEM_TABLE_SOURCE if category == "Bellway"},
    "Ventrica": {name for name, category in ITEM_TABLE_SOURCE if category == "Ventrica"},
    "Maps": {name for name, category in ITEM_TABLE_SOURCE if category == "Map"},
    "Melodies": {
        name for name, category in ITEM_TABLE_SOURCE if category == "Melody"
    },
    "Upgrades": {name for name, category in ITEM_TABLE_SOURCE if category == "Upgrade"},
    "Pins": {name for name, category in ITEM_TABLE_SOURCE if category == "Pin"},
    "Relics": {name for name, category in ITEM_TABLE_SOURCE if category == "Relic"},
    "Memory Lockets": {
        name for name, category in ITEM_TABLE_SOURCE
        if category == "MemoryLocket"
    },
    "Craftmetals": {
        name for name, category in ITEM_TABLE_SOURCE
        if category == "Craftmetal"
    },
    "Mossberries": {
        name for name, category in ITEM_TABLE_SOURCE
        if category == "Mossberry"
    },
    "Pollip Hearts": {
        name for name, category in ITEM_TABLE_SOURCE
        if category == "PollipHeart"
    },
    "Silkeaters": {
        name for name, category in ITEM_TABLE_SOURCE
        if category == "Silkeater"
    },
    "Tool Pouches": {
        name for name, category in ITEM_TABLE_SOURCE
        if category == "ToolPouch"
    },
    "Major Keys": {
        name for name, category in ITEM_TABLE_SOURCE
        if category == "MajorKey" and name not in VANILLA_ONLY_ITEM_NAMES
    },
    "Simple Keys": {
        name for name, category in ITEM_TABLE_SOURCE if category == "Key"
    },
    "Currency": {name for name, category in ITEM_TABLE_SOURCE if category == "Currency"},
    "Resources": {name for name, category in ITEM_TABLE_SOURCE if category == "Resource"},
    "Traps": {name for name, category in ITEM_TABLE_SOURCE if category == "Trap"},
    "Needle Upgrades": {
        name
        for name, category in ITEM_TABLE_SOURCE
        if category == "NeedleUpgrade"
    },
    "Lore": {
        name
        for name, category in ITEM_TABLE_SOURCE
        if category == LORE_TABLET_CATEGORY
    },
}


def _classification_for(name: str) -> ItemClassification:
    if ITEM_CATEGORY_BY_NAME[name] == "Trap":
        return ItemClassification.trap
    if name in PROGRESSION_ITEMS:
        return ItemClassification.progression
    if name in USEFUL_ITEMS:
        return ItemClassification.useful
    return ItemClassification.filler


item_data_table: Dict[str, SilksongItemData] = {
    name: SilksongItemData(item_table[name], category, _classification_for(name))
    for name, category in ITEM_TABLE_SOURCE
}

# Archipelago permits several copies to share one item ID/name. The client
# applies these by received-item index, so progressive upgrades and currency
# remain repeatable without inventing numbered aliases.
ITEM_POOL_COUNTS: Dict[str, int] = {
    'Progressive Crafting Kit': 4,
    "Progressive Druid's Eyes": 2,
    'Progressive Claw Mirror': 2,
    'Progressive Curveclaw': 2,
    'Progressive Silkheart': 3,
    # Boss checks use ordinary currency rewards.
    'Rosaries (60)': 17,
    'Shell Shards (80)': 17,
    'Memory Locket': 20,
    'Craftmetal': 8,
    'Mossberry': 7,
    'Pollip Heart': 6,
    'Silkeater': 9,
    'Progressive Tool Pouch': 4,
    'Shell Satchel': 1,
    'Growstone': 1,
}

PROGRESSIVE_SWIFT_STEP_ITEM = 'Progressive Swift Step'
REGULAR_SWIFT_STEP_ITEM = 'Swift Step'
PROGRESSIVE_NEEDLE_UPGRADE_ITEM = 'Progressive Needle Upgrade'
PALE_OIL_ITEM = 'Pale Oil'
SILK_SOAR_ITEM = 'Silk Soar'
QUILL_ITEM = 'Item: Quill'
PROGRESSIVE_COMPASS_ITEM = 'Progressive Compass'
NEEDLE_UPGRADE_POOL_COUNTS: Mapping[str, int] = {
    PROGRESSIVE_NEEDLE_UPGRADE_ITEM: 4,
    PALE_OIL_ITEM: 3,
}

# Quest Sanity adds one ordinary currency filler per added quest check. These
# copies participate in trap_percentage whenever Quest Sanity is randomized.
QUEST_FILLER_COUNTS: Dict[str, int] = {
    'Rosaries (60)': 17,
    'Shell Shards (80)': 15,
}

# These items enter the pool only when their corresponding option is enabled.
CONDITIONAL_POOL_ITEM_NAMES: FrozenSet[str] = frozenset({
    PROGRESSIVE_SWIFT_STEP_ITEM,
    PROGRESSIVE_COMPASS_ITEM,
    *NEEDLE_UPGRADE_POOL_COUNTS,
})

TRAP_ITEM_NAME_BY_WEIGHT_OPTION: Dict[str, str] = {
    'stagger_trap_weight': 'Stagger Trap',
    'rosary_spill_trap_weight': 'Rosary Spill Trap',
    'darkness_trap_weight': 'Darkness Trap',
    'cursed_crest_trap_weight': 'Cursed Crest Trap',
    'muckmaggot_status_trap_weight': 'Muckmaggot Status Trap',
    'naked_trap_weight': 'Naked Trap',
}
TRAP_ITEM_NAMES: tuple[str, ...] = tuple(
    TRAP_ITEM_NAME_BY_WEIGHT_OPTION.values()
)

STARTING_CREST_ITEM_BY_KEY: Dict[str, str] = {
    'hunter': 'Crest: Hunter',
    'wanderer': 'Crest: Wanderer',
    'reaper': 'Crest: Reaper',
    'beast': 'Crest: Beast',
    'architect': 'Crest: Architect',
    'witch': 'Crest: Witch',
    'shaman': 'Crest: Shaman',
}
STARTING_CREST_KEYS: tuple[str, ...] = tuple(STARTING_CREST_ITEM_BY_KEY)
STARTING_CREST_REPLACEMENT_ITEM = 'Rosaries (60)'
START_WITH_MAP_ITEMS: tuple[str, ...] = tuple(
    name
    for name, category in ITEM_TABLE_SOURCE
    if category == 'Map' and name != 'Map: Verdania'
)
ACT_TWO_START_WITH_MAP_ITEMS: tuple[str, ...] = tuple(
    name
    for name in START_WITH_MAP_ITEMS
    if name != 'Map: The Abyss'
)
AUTOMATIC_COMPASS_ITEM = 'Compass'
OPTIONAL_START_REPLACEMENT_ITEM = 'Rosaries (60)'


@dataclass(frozen=True)
class ItemPoolEntry:
    name: str
    source_category: str
    placement_category: str | None = None


def add_pool_only_useful_items(entries: list[ItemPoolEntry]) -> None:
    present_names = {
        entry.name
        for entry in entries
        if entry.name in POOL_ONLY_USEFUL_ITEM_NAMES
    }
    for item_name in POOL_ONLY_USEFUL_ITEM_NAMES:
        if item_name in present_names:
            continue
        replacement_index = None
        for classification in (
            ItemClassification.filler,
            ItemClassification.trap,
        ):
            replacement_index = next(
                (
                    index
                    for source_category in POOL_ONLY_FILLER_SOURCE_CATEGORIES
                    for index, entry in enumerate(entries)
                    if entry.source_category == source_category
                    and item_data_table[entry.name].classification
                    == classification
                ),
                None,
            )
            if replacement_index is not None:
                break
        if replacement_index is None:
            continue
        replaced_entry = entries[replacement_index]
        entries[replacement_index] = ItemPoolEntry(
            item_name,
            replaced_entry.source_category,
            replaced_entry.placement_category,
        )


OBSERVATION_FILLER_COUNTS_BY_CATEGORY: Dict[str, Dict[str, int]] = {
    # Observation checks use ordinary filler rather than boss rewards.
    'Boss': {
        'Rosaries (60)': 17,
        'Shell Shards (80)': 17,
    },
    'Quest': dict(QUEST_FILLER_COUNTS),
}

# Enabling the fifteen Scrounge and six Cardinius deposit checks adds the same
# number of unrestricted rewards. These ordinary filler copies are eligible
# for the configured trap percentage like every other filler source.
RELIC_TURN_IN_FILLER_COUNTS: Dict[str, int] = {
    'Rosaries (60)': 11,
    'Shell Shards (80)': 10,
}

PAIRED_ITEM_CATEGORIES: tuple[str, ...] = (
    'Skill',
    'Tool',
    'Spell',
    'Crest',
    'Flea',
    'CrestSlot',
    'MaskShard',
    'SpoolFragment',
    'SilkHeart',
    'Bellway',
    'Ventrica',
    'Map',
    'Melody',
    'Pin',
    'Relic',
    'Upgrade',
    'Key',
    'MemoryLocket',
    'Craftmetal',
    'Mossberry',
    'PollipHeart',
    'Silkeater',
    'MajorKey',
    'Resource',
    'ToolPouch',
    'BellShrine',
    LORE_TABLET_CATEGORY,
)

REPEATED_CATEGORY_ITEM_COUNTS: Mapping[str, Mapping[str, int]] = {
    'SilkHeart': {'Progressive Silkheart': 3},
    'MemoryLocket': {'Memory Locket': 20},
    'Craftmetal': {'Craftmetal': 8},
    'Mossberry': {'Mossberry': 7},
    'PollipHeart': {'Pollip Heart': 6},
    'Silkeater': {'Silkeater': 9},
    'ToolPouch': {'Progressive Tool Pouch': 4},
}

OBSERVATION_ITEM_CATEGORIES: tuple[str, ...] = (
    'Boss',
    'Quest',
)

SHUFFLE_FIXED_LOCATION_REWARDS: Dict[str, Dict[str, str]] = {
    # These checks cannot hold arbitrary useful or progression
    # rewards. Keeping their own optional reward fixed preserves that safety
    # while the remaining category is shuffled.
    'Tool': {
        'Throwing Ring': 'Throwing Ring',
    },
    # These map sources have incomplete access logic. Their native rewards do
    # not unlock routes, so fixing only those rewards
    # preserves Map shuffle without exposing arbitrary progression there.
    'Map': {
        'The Slab - Map Pickup': 'Map: The Slab',
        'Putrified Ducts - Map Pickup': 'Map: Putrified Ducts',
        'The Cradle - Map Purchase': 'Map: The Cradle',
        'Verdania - Map Pickup': 'Map: Verdania',
        'The Abyss - Map Pickup': 'Map: The Abyss',
    },
    # Beastling Call is Act 3-only and still progression-quarantined. Keeping
    # its own non-routing song here prevents a Melody-shuffle cycle.
    'Melody': {
        'Beastling Call': 'Beastling Call',
    },
}


def get_category_item_names(category: str) -> tuple[str, ...]:
    """Return the exact reward multiset paired with a source category."""

    if category in MINOR_FAMILY_SHUFFLE_CATEGORIES:
        family_key = category.split(":", 1)[1]
        return tuple(
            item_name
            for item_name, count in
            MINOR_FAMILY_REWARD_COUNTS[family_key].items()
            for _copy_index in range(count)
        )
    if category == 'Tool':
        return (
            *(
                name
                for name, item_category in ITEM_TABLE_SOURCE
                if (
                    item_category == 'Tool'
                    and name not in VANILLA_ONLY_ITEM_NAMES
                    and name not in POOL_ONLY_USEFUL_ITEM_NAMES
                )
            ),
            "Progressive Druid's Eyes",
            "Progressive Druid's Eyes",
            # Each progressive identity already appears once in the Tool
            # table above. These second copies balance the added upgrade
            # sources while preserving strict base-before-upgrade receipt.
            'Progressive Claw Mirror',
            'Progressive Curveclaw',
        )
    if category == 'Upgrade':
        return ('Progressive Crafting Kit',) * 4
    if category == 'Resource':
        return tuple(
            MINOR_REWARD_BY_LOCATION[location_name]
            for location_name in MINOR_REWARD_BY_LOCATION
        )
    if category in REPEATED_CATEGORY_ITEM_COUNTS:
        return tuple(
            item_name
            for item_name, count
            in REPEATED_CATEGORY_ITEM_COUNTS[category].items()
            for _copy_index in range(count)
        )

    excluded_names = (
        set(TRAP_ITEM_NAMES)
        | set(CONDITIONAL_POOL_ITEM_NAMES)
        | set(VANILLA_ONLY_ITEM_NAMES)
        | set(POOL_ONLY_USEFUL_ITEM_NAMES)
    )
    return tuple(
        name
        for name, item_category in ITEM_TABLE_SOURCE
        if item_category == category and name not in excluded_names
    )


def get_vanilla_reward_name(
    location_name: str,
    category: str,
) -> str:
    """Map an AP source to the native reward represented by that source."""

    location_name = canonicalize_location_name(location_name)
    source_location_name = get_item_first_location_name(location_name)

    if category == 'Key':
        try:
            return {
                'Simple Key: Bone Bottom Shop':
                    'Simple Key (Wormways)',
                'Simple Key: Roachkeeper':
                    'Simple Key (Green Prince)',
                'Simple Key: Songclave Shop':
                    'Simple Key (Rosary Bank)',
                'Simple Key: Sands of Karak East Bench':
                    'Simple Key (Deep Docks)',
            }[source_location_name]
        except KeyError as exc:
            raise ValueError(
                f"No native Key reward mapping for {location_name!r}."
            ) from exc
    if category in REPEATED_CATEGORY_ITEM_COUNTS:
        return next(iter(REPEATED_CATEGORY_ITEM_COUNTS[category]))
    if category == 'MajorKey':
        try:
            return {
                'Key of Indolent': 'Key of Indolent',
                'Key of Heretic': 'Key of Heretic',
                'Key of Apostate': 'Key of Apostate',
                'White Key': 'White Key',
                "Surgeon's Key": "Surgeon's Key",
                "Architect's Key": "Architect's Key",
                'Craw Summons': 'Craw Summons',
            }[location_name]
        except KeyError as exc:
            raise ValueError(
                f"No native MajorKey reward mapping for {location_name!r}."
            ) from exc
    if category == 'BellShrine':
        try:
            return {
                'The Marrow - Bellshrine': 'Bell: The Marrow',
                'Deep Docks - Bellshrine': 'Bell: Deep Docks',
                'Greymoor - Bellshrine': 'Bell: Greymoor',
                'Shellwood - Bellshrine': 'Bell: Shellwood',
                'Bellhart - Bellshrine': 'Bell: Bellhart',
            }[location_name]
        except KeyError as exc:
            raise ValueError(
                f"No native BellShrine reward mapping for "
                f"{location_name!r}."
            ) from exc
    if category == LORE_TABLET_CATEGORY:
        try:
            return LORE_TABLET_ITEM_BY_LOCATION[location_name]
        except KeyError as exc:
            raise ValueError(
                f"No native Lore Tablet reward mapping for "
                f"{location_name!r}."
            ) from exc
    if location_name in {
        "Druid's Eye",
        "Druid's Eyes",
    }:
        return "Progressive Druid's Eyes"
    if location_name in {'Claw Mirror', 'Dark Mirror'}:
        return 'Progressive Claw Mirror'
    if location_name in {'Curveclaw', 'Curvesickle'}:
        return 'Progressive Curveclaw'
    if category == 'Upgrade':
        return 'Progressive Crafting Kit'
    if category == 'MaskShard':
        try:
            return (
                f'Mask Shard #'
                    f'{MASK_SHARD_LOCATION_NAMES.index(location_name) + 1}'
            )
        except ValueError as exc:
            raise ValueError(
                f"No native MaskShard reward mapping for "
                f"{location_name!r}."
            ) from exc
    if category == 'SpoolFragment':
        try:
            return (
                f'Spool Fragment #'
                    f'{SPOOL_FRAGMENT_LOCATION_NAMES.index(location_name) + 1}'
            )
        except ValueError as exc:
            raise ValueError(
                f"No native SpoolFragment reward mapping for "
                f"{location_name!r}."
            ) from exc
    if get_base_randomization_category(category) == 'Resource':
        try:
            return MINOR_REWARD_BY_LOCATION[location_name]
        except KeyError as exc:
            raise ValueError(
                f"No native Resource reward mapping for {location_name!r}."
            ) from exc
    if source_location_name in ITEM_CATEGORY_BY_NAME:
        return source_location_name
    if source_location_name.startswith('Pin Purchase: '):
        native_name = source_location_name.replace(
            'Pin Purchase: ',
            '',
            1,
        )
    elif source_location_name.startswith('Map Purchase: '):
        native_name = source_location_name.replace(
            'Map Purchase: ',
            'Map: ',
            1,
        )
    elif source_location_name.startswith('Map Pickup: '):
        native_name = source_location_name.replace(
            'Map Pickup: ',
            'Map: ',
            1,
        )
    else:
        raise ValueError(
            f"No native {category} reward mapping for {location_name!r}."
        )
    return NATIVE_ITEM_NAME_TO_CURRENT.get(native_name, native_name)


def get_act_one_start_with_map_items(
    excluded_location_names: FrozenSet[str],
) -> tuple[str, ...]:
    retained_rewards = {
        get_vanilla_reward_name(location_name, "Map")
        for location_name in LOCATION_NAMES_BY_CATEGORY["Map"]
        if location_name not in excluded_location_names
    }
    return tuple(
        item_name
        for item_name in START_WITH_MAP_ITEMS
        if item_name in retained_rewards
    )


def _trim_act_one_pool_entries(
    entries: list[ItemPoolEntry],
    category_modes: Mapping[str, str],
    starting_crest_item: str,
    split_dash_and_sprint: bool,
    automatic_compass: bool,
    removed_option_items_by_category: Mapping[str, tuple[str, ...]],
    randomize_needle_upgrades: bool,
    individual_relic_turn_ins: bool,
) -> list[ItemPoolEntry]:
    major_key_mode = category_modes.get("MajorKey", "vanilla")
    boss_mode = category_modes.get("Boss", "anywhere")
    excluded_location_names = get_act_one_excluded_location_names(
        major_key_mode,
        boss_mode,
    )
    retain_craw_summons = retains_randomized_craw_summons(
        major_key_mode,
        boss_mode,
    )

    for location_name in sorted(excluded_location_names):
        location_data = location_data_table[location_name]
        category = location_data.category

        if (
            category == RELIC_TURN_IN_LOCATION_CATEGORY
            and not individual_relic_turn_ins
        ):
            continue
        if (
            category == NEEDLE_UPGRADE_LOCATION_CATEGORY
            and not randomize_needle_upgrades
        ):
            continue
        if (
            location_name == "Wish: Pinmaster's Oil"
            and randomize_needle_upgrades
        ):
            continue
        if (
            location_name == "Wish: Bugs of Pharloom"
            and category_modes.get("ToolPouch", "vanilla") != "vanilla"
        ):
            continue

        source_category = (
            get_minor_family_shuffle_category_for_location(location_name)
            if category == "Resource"
            else category
        )
        if source_category == RELIC_TURN_IN_LOCATION_CATEGORY:
            active = individual_relic_turn_ins
        elif source_category == NEEDLE_UPGRADE_LOCATION_CATEGORY:
            active = randomize_needle_upgrades
        else:
            active = category_modes.get(source_category, "anywhere") != "vanilla"
        if not active:
            continue

        if location_name == "Craw Summons" and retain_craw_summons:
            continue

        if source_category in {
            *OBSERVATION_LOCATION_CATEGORIES,
            RELIC_TURN_IN_LOCATION_CATEGORY,
        }:
            entry_index = next(
                (
                    index
                    for index, entry in enumerate(entries)
                    if (
                        entry.source_category == source_category
                        and item_data_table[entry.name].classification
                        == ItemClassification.filler
                    )
                ),
                None,
            )
        elif source_category == NEEDLE_UPGRADE_LOCATION_CATEGORY:
            source_location_name = get_item_first_location_name(
                location_name
            )
            reward_name = (
                PALE_OIL_ITEM
                if source_location_name.startswith("Pale Oil: ")
                else PROGRESSIVE_NEEDLE_UPGRADE_ITEM
            )
            entry_index = next(
                (
                    index
                    for index, entry in enumerate(entries)
                    if (
                        entry.source_category == source_category
                        and entry.name == reward_name
                    )
                ),
                None,
            )
        else:
            reward_name = get_vanilla_reward_name(
                location_name,
                category,
            )
            if reward_name == starting_crest_item and category == "Crest":
                reward_name = STARTING_CREST_REPLACEMENT_ITEM
            elif (
                reward_name == REGULAR_SWIFT_STEP_ITEM
                and split_dash_and_sprint
            ):
                reward_name = PROGRESSIVE_SWIFT_STEP_ITEM
            elif reward_name == QUILL_ITEM and automatic_compass:
                reward_name = PROGRESSIVE_COMPASS_ITEM
            elif reward_name in removed_option_items_by_category.get(
                category,
                (),
            ):
                reward_name = OPTIONAL_START_REPLACEMENT_ITEM

            entry_index = next(
                (
                    index
                    for index, entry in enumerate(entries)
                    if (
                        entry.source_category == source_category
                        and entry.name == reward_name
                    )
                ),
                None,
            )

        if entry_index is None:
            raise ValueError(
                "Act 1 content trim could not remove an item for "
                f"{location_name!r} from the {source_category!r} pool lane."
            )
        entries.pop(entry_index)

    if retain_craw_summons:
        balance_index = next(
            (
                index
                for index, entry in enumerate(entries)
                if (
                    entry.source_category == "Boss"
                    and entry.placement_category is None
                    and item_data_table[entry.name].classification
                    == ItemClassification.filler
                )
            ),
            None,
        )
        if balance_index is None:
            raise ValueError(
                "Act 1 Craw Summons balancing needs one Boss filler item."
            )
        entries.pop(balance_index)

    return entries


def get_sprint_filler_source(
    category_modes: Mapping[str, str],
) -> str | None:
    """Prefer the starting-crest filler for split Swift Step balancing."""

    return (
        'Crest'
        if category_modes.get('Crest', 'anywhere') == 'anywhere'
        else None
    )


def _get_act_two_skill_balance_fallback_item_name(
    automatic_compass: bool,
) -> str:
    """Return the Skill item precollected only when no filler can balance."""

    return PROGRESSIVE_COMPASS_ITEM if automatic_compass else QUILL_ITEM


def _balance_act_two_retained_silk_soar(
    entries: list[ItemPoolEntry],
    category_modes: Mapping[str, str],
    automatic_compass: bool,
    start_fully_mapped: bool = False,
) -> None:
    """Keep Silk Soar while balancing its omitted physical source.

    Anywhere mode substitutes Silk Soar for one unrestricted filler whenever
    possible. Skill shuffle has no spare location in its lane. A Skill-only
    anywhere seed has no filler at all. Those narrow cases remove
    Quill (or automatic compass's Progressive Compass replacement) from the
    random pool. ``SilksongWorld.create_items`` precollects that item so
    no useful inventory is lost.
    """

    skill_mode = category_modes.get('Skill', 'anywhere')
    if skill_mode == 'vanilla':
        return
    if skill_mode not in {'shuffle', 'anywhere'}:
        raise ValueError(f"Unknown Skill randomization mode: {skill_mode!r}")

    silk_soar_count = sum(
        entry.source_category == 'Skill' and entry.name == SILK_SOAR_ITEM
        for entry in entries
    )
    if silk_soar_count != 1:
        raise ValueError(
            "Act 2 Skill balancing requires exactly one randomized "
            f"{SILK_SOAR_ITEM!r}; found {silk_soar_count}."
        )

    balance_index = None
    if skill_mode == 'anywhere':
        balance_index = next(
            (
                index
                for index, entry in enumerate(entries)
                if (
                    entry.placement_category is None
                    and item_data_table[entry.name].classification
                    == ItemClassification.filler
                )
            ),
            None,
        )

    if balance_index is None and start_fully_mapped:
        balance_index = next(
            (
                index
                for index, entry in enumerate(entries)
                if (
                    entry.source_category == 'Skill'
                    and entry.name == OPTIONAL_START_REPLACEMENT_ITEM
                )
            ),
            None,
        )

    if balance_index is None:
        fallback_item_name = (
            _get_act_two_skill_balance_fallback_item_name(
                automatic_compass
            )
        )
        balance_index = next(
            (
                index
                for index, entry in enumerate(entries)
                if (
                    entry.source_category == 'Skill'
                    and entry.name == fallback_item_name
                )
            ),
            None,
        )
        if balance_index is None:
            raise ValueError(
                "Act 2 Skill balancing could not find fallback item "
                f"{fallback_item_name!r}."
            )

    entries.pop(balance_index)


def get_act_two_skill_balance_precollected_item_name(
    entries: tuple[ItemPoolEntry, ...] | list[ItemPoolEntry],
    category_modes: Mapping[str, str],
    automatic_compass: bool,
    start_fully_mapped: bool = False,
) -> str | None:
    """Identify the Skill item moved from a constrained Act 2 pool."""

    if (
        category_modes.get('Skill', 'anywhere') == 'vanilla'
        or start_fully_mapped
    ):
        return None
    fallback_item_name = _get_act_two_skill_balance_fallback_item_name(
        automatic_compass
    )
    if any(
        entry.source_category == 'Skill'
        and entry.name == fallback_item_name
        for entry in entries
    ):
        return None
    return fallback_item_name


def get_dynamic_trap_capacity(
    category_modes: Mapping[str, str],
    split_dash_and_sprint: bool = False,
    starting_crest_item: str = 'Crest: Hunter',
    randomize_needle_upgrades: bool = False,
    start_with_maps: bool = False,
    automatic_compass: bool = False,
    minor_shuffle_category_by_family: Mapping[str, str] | None = None,
    individual_relic_turn_ins: bool = False,
    act_two_only: bool = False,
    start_fully_mapped: bool = False,
    act_one_only: bool = False,
    exclude_verdania: bool = False,
    retain_green_prince_key: bool = False,
) -> int:
    """Count every filler-classified entry in the configured random pool."""

    return sum(
        item_data_table[entry.name].classification
        == ItemClassification.filler
        for entry in build_item_pool_entries(
            starting_crest_item,
            None,
            split_dash_and_sprint,
            category_modes,
            randomize_needle_upgrades,
            start_with_maps,
            automatic_compass,
            minor_shuffle_category_by_family,
            individual_relic_turn_ins=individual_relic_turn_ins,
            act_two_only=act_two_only,
            start_fully_mapped=start_fully_mapped,
            act_one_only=act_one_only,
            exclude_verdania=exclude_verdania,
            retain_green_prince_key=retain_green_prince_key,
        )
    )


def _trim_verdania_pool_entries(
    entries: list[ItemPoolEntry],
    category_modes: Mapping[str, str],
) -> list[ItemPoolEntry]:
    """Remove rewards paired with the five Lost Verdania checks."""

    for location_name in sorted(VERDANIA_LOCATION_NAMES):
        location_data = location_data_table[location_name]
        source_category = location_data.category
        if category_modes.get(source_category, "anywhere") == "vanilla":
            continue

        if source_category in OBSERVATION_LOCATION_CATEGORIES:
            entry_index = next(
                (
                    index
                    for index, entry in enumerate(entries)
                    if (
                        entry.source_category == source_category
                        and item_data_table[entry.name].classification
                        == ItemClassification.filler
                    )
                ),
                None,
            )
        else:
            reward_name = get_vanilla_reward_name(
                location_name,
                source_category,
            )
            entry_index = next(
                (
                    index
                    for index, entry in enumerate(entries)
                    if (
                        entry.source_category == source_category
                        and entry.name == reward_name
                    )
                ),
                None,
            )

        if entry_index is None:
            raise ValueError(
                "Lost Verdania content trim could not remove an item for "
                f"{location_name!r} from the {source_category!r} pool lane."
            )
        entries.pop(entry_index)

    return entries


def _replace_verdania_only_key_pool_entry(
    entries: list[ItemPoolEntry],
    category_modes: Mapping[str, str],
) -> None:
    """Replace the Green Prince key outside the Act 3 content scope."""

    if category_modes.get("Key", "anywhere") == "vanilla":
        return

    entry_index = next(
        (
            index
            for index, entry in enumerate(entries)
            if (
                entry.source_category == "Key"
                and entry.name == SIMPLE_KEY_GREEN_PRINCE
            )
        ),
        None,
    )
    if entry_index is None:
        raise ValueError(
            "Lost Verdania content trim could not replace "
            f"{SIMPLE_KEY_GREEN_PRINCE!r} in the Key pool lane."
        )

    replaced_entry = entries[entry_index]
    entries[entry_index] = ItemPoolEntry(
        OPTIONAL_START_REPLACEMENT_ITEM,
        replaced_entry.source_category,
        replaced_entry.placement_category,
    )


def build_item_pool_entries(
    starting_crest_item: str,
    trap_counts: Mapping[str, int] | None,
    split_dash_and_sprint: bool,
    category_modes: Mapping[str, str],
    randomize_needle_upgrades: bool = False,
    start_with_maps: bool = False,
    automatic_compass: bool = False,
    minor_shuffle_category_by_family: Mapping[str, str] | None = None,
    trap_randomizer: Random | None = None,
    individual_relic_turn_ins: bool = False,
    act_two_only: bool = False,
    start_fully_mapped: bool = False,
    act_one_only: bool = False,
    exclude_verdania: bool = False,
    retain_green_prince_key: bool = False,
) -> tuple[ItemPoolEntry, ...]:
    """Build the unfilled-location pool for the selected category modes."""

    if act_one_only and act_two_only:
        raise ValueError("Act 1 and Act 2 content scopes cannot overlap.")
    exclude_verdania_content = (
        exclude_verdania or act_one_only or act_two_only
    )

    entries: list[ItemPoolEntry] = []
    act_one_excluded_location_names = (
        get_act_one_excluded_location_names(
            category_modes.get("MajorKey", "vanilla"),
            category_modes.get("Boss", "anywhere"),
        )
        if act_one_only
        else frozenset()
    )
    goal_excluded_location_names = (
        act_one_excluded_location_names
        if act_one_only
        else ACT_TWO_EXCLUDED_LOCATION_NAMES
        if act_two_only
        else VERDANIA_LOCATION_NAMES
        if exclude_verdania
        else frozenset()
    )
    start_with_map_items = (
        get_act_one_start_with_map_items(
            act_one_excluded_location_names
        )
        if act_one_only
        else ACT_TWO_START_WITH_MAP_ITEMS
        if act_two_only
        else START_WITH_MAP_ITEMS
    )
    removed_option_items_by_category: Dict[str, tuple[str, ...]] = {
        'Map': start_with_map_items if start_with_maps else (),
        'Skill': (
            (
                PROGRESSIVE_COMPASS_ITEM
                if automatic_compass
                else QUILL_ITEM
            ),
        ) if start_fully_mapped else (),
        'Tool': (
            (AUTOMATIC_COMPASS_ITEM,)
            if automatic_compass
            else ()
        ),
    }

    pool_categories = (
        *(
            category
            for category in PAIRED_ITEM_CATEGORIES
            if category != 'Resource'
        ),
        *MINOR_FAMILY_SHUFFLE_CATEGORIES,
    )
    for category in pool_categories:
        mode = category_modes.get(category, 'anywhere')
        if mode == 'vanilla':
            continue

        item_names = list(get_category_item_names(category))
        if category == 'Crest':
            try:
                item_names.remove(starting_crest_item)
            except ValueError as exc:
                raise ValueError(
                    f"Starting crest is not present in the Crest pool: "
                    f"{starting_crest_item!r}"
                ) from exc
            item_names.append(STARTING_CREST_REPLACEMENT_ITEM)
        elif category == 'Skill' and split_dash_and_sprint:
            try:
                item_names.remove(REGULAR_SWIFT_STEP_ITEM)
            except ValueError as exc:
                raise ValueError(
                    'Split Swift Step requires the regular Swift Step item '
                    'in the Skill pool.'
                ) from exc
            item_names.extend((PROGRESSIVE_SWIFT_STEP_ITEM,) * 2)

        if category == 'Skill' and automatic_compass:
            try:
                quill_index = item_names.index(QUILL_ITEM)
            except ValueError as exc:
                raise ValueError(
                    'automatic_compass requires Quill in the Skill pool.'
                ) from exc
            item_names[quill_index] = PROGRESSIVE_COMPASS_ITEM

        for removed_name in removed_option_items_by_category.get(
            category,
            (),
        ):
            try:
                item_names.remove(removed_name)
            except ValueError as exc:
                raise ValueError(
                    f"Option-removed item is not present in the "
                    f"{category} pool: {removed_name!r}"
                ) from exc
            item_names.append(OPTIONAL_START_REPLACEMENT_ITEM)

        if mode == 'shuffle':
            removed_option_items = set(
                removed_option_items_by_category.get(category, ())
            )
            for fixed_location, fixed_reward in (
                SHUFFLE_FIXED_LOCATION_REWARDS
                .get(category, {})
                .items()
            ):
                # Goal-scoped sources are not locked shuffle locations. Their
                # rewards remain in this lane until the trim below removes the
                # corresponding copy in shuffle or anywhere.
                if (
                    fixed_location in goal_excluded_location_names
                ):
                    continue
                pool_reward = (
                    OPTIONAL_START_REPLACEMENT_ITEM
                    if fixed_reward in removed_option_items
                    else fixed_reward
                )
                try:
                    item_names.remove(pool_reward)
                except ValueError as exc:
                    raise ValueError(
                        f"Fixed {category} shuffle reward is absent from "
                        f"its pool: {pool_reward!r}"
                    ) from exc

        placement_category = (
            (
                (minor_shuffle_category_by_family or {}).get(
                    category.split(":", 1)[1],
                    category,
                )
                if category in MINOR_FAMILY_SHUFFLE_CATEGORIES
                else category
            )
            if mode == 'shuffle'
            else None
        )
        entries.extend(
            ItemPoolEntry(
                item_name,
                category,
                placement_category,
            )
            for item_name in item_names
        )

    for category in OBSERVATION_ITEM_CATEGORIES:
        mode = category_modes.get(category, 'anywhere')
        if mode == 'vanilla':
            continue
        placement_category = category if mode == 'shuffle' else None
        for item_name, count in (
            OBSERVATION_FILLER_COUNTS_BY_CATEGORY[category].items()
        ):
            entries.extend(
                ItemPoolEntry(
                    item_name,
                    category,
                    placement_category,
                )
                for _copy_index in range(count)
            )

    if individual_relic_turn_ins:
        for item_name, count in RELIC_TURN_IN_FILLER_COUNTS.items():
            entries.extend(
                ItemPoolEntry(
                    item_name,
                    RELIC_TURN_IN_LOCATION_CATEGORY,
                )
                for _copy_index in range(count)
            )

    if (
        randomize_needle_upgrades
        and category_modes.get('Quest', 'anywhere') != 'vanilla'
    ):
        # Pinmaster's Oil is the same vanilla interaction represented by the
        # randomized Plinney chain. Its Quest Sanity filler counterpart is
        # omitted so that action cannot report two AP locations.
        duplicate_quest_filler_index = next(
            (
                index
                for index, entry in enumerate(entries)
                if (
                    entry.source_category == 'Quest'
                    and entry.name in {
                        'Rosaries (60)',
                        'Shell Shards (80)',
                    }
                )
            ),
            None,
        )
        if duplicate_quest_filler_index is None:
            raise ValueError(
                "Could not remove the duplicate Pinmaster's Oil quest filler."
            )
        entries.pop(duplicate_quest_filler_index)

    if (
        category_modes.get('MemoryLocket', 'vanilla') != 'vanilla'
        and category_modes.get('Quest', 'anywhere') != 'vanilla'
    ):
        # Volatile Flintbeetles' Wish completion and Act 3 ground fallback
        # are the same physical Memory Locket source. When that source is an
        # AP location, remove the duplicate Quest Sanity check and its filler.
        duplicate_quest_filler_index = next(
            (
                index
                for index, entry in enumerate(entries)
                if (
                    entry.source_category == 'Quest'
                    and entry.name in {
                        'Rosaries (60)',
                        'Shell Shards (80)',
                    }
                )
            ),
            None,
        )
        if duplicate_quest_filler_index is None:
            raise ValueError(
                "Could not remove the duplicate Volatile Flintbeetles "
                "quest filler."
            )
        entries.pop(duplicate_quest_filler_index)

    if (
        category_modes.get('ToolPouch', 'vanilla') != 'vanilla'
        and category_modes.get('Quest', 'anywhere') != 'vanilla'
    ):
        # Bugs of Pharloom has one physical Tool Pouch reward. When that
        # source becomes the Tool Pouch AP location, its Quest Sanity location
        # is removed above and its matching Quest filler must leave the pool.
        duplicate_quest_filler_index = next(
            (
                index
                for index, entry in enumerate(entries)
                if (
                    entry.source_category == 'Quest'
                    and entry.name in {
                        'Rosaries (60)',
                        'Shell Shards (80)',
                    }
                )
            ),
            None,
        )
        if duplicate_quest_filler_index is None:
            raise ValueError(
                "Could not remove the duplicate Bugs of Pharloom "
                "quest filler."
            )
        entries.pop(duplicate_quest_filler_index)

    if randomize_needle_upgrades:
        entries.extend(
            ItemPoolEntry(item_name, 'NeedleUpgrade')
            for item_name, count in NEEDLE_UPGRADE_POOL_COUNTS.items()
            for _copy_index in range(count)
        )

    if act_one_only:
        entries = _trim_act_one_pool_entries(
            entries,
            category_modes,
            starting_crest_item,
            split_dash_and_sprint,
            automatic_compass,
            removed_option_items_by_category,
            randomize_needle_upgrades,
            individual_relic_turn_ins,
        )
    elif act_two_only:
        entries = list(
            trim_act_two_pool_entries(entries, starting_crest_item)
        )
    elif exclude_verdania:
        entries = _trim_verdania_pool_entries(entries, category_modes)

    # Act 2 may keep the otherwise Verdania-only key as an optional bingo
    # collectible without restoring any Verdania locations or requirements.
    if exclude_verdania_content and not retain_green_prince_key:
        _replace_verdania_only_key_pool_entry(entries, category_modes)

    if split_dash_and_sprint:
        preferred_filler_source = get_sprint_filler_source(category_modes)
        unrestricted_filler_indices = tuple(
            index
            for index, entry in enumerate(entries)
            if (
                entry.placement_category is None
                and item_data_table[entry.name].classification
                == ItemClassification.filler
            )
        )
        filler_index = next(
            (
                index
                for index in unrestricted_filler_indices
                if entries[index].source_category
                == preferred_filler_source
            ),
            (
                unrestricted_filler_indices[0]
                if unrestricted_filler_indices
                else None
            ),
        )
        if filler_index is None:
            raise ValueError(
                "split_dash_and_sprint needs one unrestricted filler item "
                "from an 'anywhere' category to balance its second "
                "Progressive Swift Step."
            )
        entries.pop(filler_index)

    if act_two_only:
        _balance_act_two_retained_silk_soar(
            entries,
            category_modes,
            automatic_compass,
            start_fully_mapped,
        )

    add_pool_only_useful_items(entries)

    configured_traps = {
        trap_name: int((trap_counts or {}).get(trap_name, 0))
        for trap_name in TRAP_ITEM_NAMES
    }
    unknown_traps = set(trap_counts or {}) - set(TRAP_ITEM_NAMES)
    if unknown_traps:
        raise ValueError(
            f"Unknown trap item names: {sorted(unknown_traps)!r}"
        )
    if any(count < 0 for count in configured_traps.values()):
        raise ValueError("Trap counts cannot be negative.")

    trap_names = tuple(
        trap_name
        for trap_name in TRAP_ITEM_NAMES
        for _copy_index in range(configured_traps[trap_name])
    )
    eligible_indices = [
        index
        for index, entry in enumerate(entries)
        if (
            item_data_table[entry.name].classification
            == ItemClassification.filler
        )
    ]
    if trap_randomizer is not None:
        # Shuffle the complete filler pool so each entry has the configured
        # trap chance.
        trap_randomizer.shuffle(eligible_indices)
    trap_capacity = len(eligible_indices)
    if len(trap_names) > trap_capacity:
        raise ValueError(
            f"At most {trap_capacity} traps can replace filler items in the "
            f"configured random pool; received {len(trap_names)}."
        )

    for entry_index, trap_name in zip(eligible_indices, trap_names):
        replaced_entry = entries[entry_index]
        entries[entry_index] = ItemPoolEntry(
            trap_name,
            replaced_entry.source_category,
            replaced_entry.placement_category,
        )
    return tuple(entries)


def get_configured_item_pool_size(
    starting_crest_item: str,
    trap_counts: Mapping[str, int] | None,
    split_dash_and_sprint: bool,
    category_modes: Mapping[str, str],
    randomize_needle_upgrades: bool = False,
    start_with_maps: bool = False,
    automatic_compass: bool = False,
    minor_shuffle_category_by_family: Mapping[str, str] | None = None,
    individual_relic_turn_ins: bool = False,
    act_two_only: bool = False,
    start_fully_mapped: bool = False,
    act_one_only: bool = False,
    exclude_verdania: bool = False,
    retain_green_prince_key: bool = False,
) -> int:
    return len(
        build_item_pool_entries(
            starting_crest_item,
            trap_counts,
            split_dash_and_sprint,
            category_modes,
            randomize_needle_upgrades,
            start_with_maps,
            automatic_compass,
            minor_shuffle_category_by_family,
            individual_relic_turn_ins=individual_relic_turn_ins,
            act_two_only=act_two_only,
            start_fully_mapped=start_fully_mapped,
            act_one_only=act_one_only,
            exclude_verdania=exclude_verdania,
            retain_green_prince_key=retain_green_prince_key,
        )
    )


def _get_adjusted_pool_counts(
    trap_counts: Mapping[str, int] | None = None,
    split_dash_and_sprint: bool = False,
    quest_sanity: bool = True,
    randomize_needle_upgrades: bool = False,
    randomize_minor_pickups: bool = False,
) -> Dict[str, int]:
    configured_traps = {
        trap_name: int((trap_counts or {}).get(trap_name, 0))
        for trap_name in TRAP_ITEM_NAMES
    }
    unknown_traps = set(trap_counts or {}) - set(TRAP_ITEM_NAMES)
    if unknown_traps:
        raise ValueError(
            f"Unknown trap item names: {sorted(unknown_traps)!r}"
        )
    if any(count < 0 for count in configured_traps.values()):
        raise ValueError("Trap counts cannot be negative.")

    counts = {
        name: (
            (
                MINOR_REWARD_COUNTS.get(name, 0)
            )
            if category == 'Resource' and randomize_minor_pickups
            else (
                0
                if (
                    category == 'Resource'
                    or name in TRAP_ITEM_NAMES
                    or name in CONDITIONAL_POOL_ITEM_NAMES
                    or name in VANILLA_ONLY_ITEM_NAMES
                    or name in POOL_ONLY_USEFUL_ITEM_NAMES
                )
                else 1
            )
        )
        for name, category in ITEM_TABLE_SOURCE
    }
    counts.update(ITEM_POOL_COUNTS)
    counts['Rosaries (60)'] -= len(POOL_ONLY_USEFUL_ITEM_NAMES)
    if quest_sanity:
        for item_name, count in QUEST_FILLER_COUNTS.items():
            counts[item_name] += count
    counts[REGULAR_SWIFT_STEP_ITEM] = int(
        not split_dash_and_sprint
    )
    counts[PROGRESSIVE_SWIFT_STEP_ITEM] = (
        2 if split_dash_and_sprint else 0
    )
    for item_name, count in NEEDLE_UPGRADE_POOL_COUNTS.items():
        counts[item_name] = count if randomize_needle_upgrades else 0
    if quest_sanity and randomize_needle_upgrades:
        counts['Rosaries (60)'] -= 1
    # Split Swift Step's extra copy consumes the starting-crest replacement.
    counts['Rosaries (60)'] -= int(split_dash_and_sprint)

    total_traps = sum(configured_traps.values())
    trap_capacity = sum(
        count
        for item_name, count in counts.items()
        if (
            item_data_table[item_name].classification
            == ItemClassification.filler
        )
    )
    if total_traps > trap_capacity:
        raise ValueError(
            f"At most {trap_capacity} traps can replace filler items in this "
            f"pool; received {total_traps}."
        )

    remaining_traps = total_traps
    for item_name, _category in ITEM_TABLE_SOURCE:
        if (
            remaining_traps == 0
            or item_data_table[item_name].classification
            != ItemClassification.filler
        ):
            continue
        replaced_count = min(counts[item_name], remaining_traps)
        counts[item_name] -= replaced_count
        remaining_traps -= replaced_count

    counts.update(configured_traps)
    return counts


def iter_item_pool_names(
    starting_crest_item: str | None = None,
    trap_counts: Mapping[str, int] | None = None,
    split_dash_and_sprint: bool = False,
    quest_sanity: bool = True,
    randomize_needle_upgrades: bool = False,
    randomize_minor_pickups: bool = False,
):
    pool_counts = _get_adjusted_pool_counts(
        trap_counts,
        split_dash_and_sprint,
        quest_sanity,
        randomize_needle_upgrades,
        randomize_minor_pickups,
    )
    removed_starting_crest = False
    for name, _category in ITEM_TABLE_SOURCE:
        if _category == 'Resource':
            default_count = (
                (
                    MINOR_REWARD_COUNTS.get(name, 0)
                )
                if randomize_minor_pickups
                else 0
            )
        else:
            default_count = (
                0
                if (
                    name in TRAP_ITEM_NAMES
                    or name in CONDITIONAL_POOL_ITEM_NAMES
                    or name in VANILLA_ONLY_ITEM_NAMES
                    or name in POOL_ONLY_USEFUL_ITEM_NAMES
                )
                else 1
            )
        for _copy_index in range(pool_counts.get(name, default_count)):
            if name == starting_crest_item and not removed_starting_crest:
                removed_starting_crest = True
                continue
            yield name

    if starting_crest_item is not None:
        if not removed_starting_crest:
            raise ValueError(
                f"Starting crest is not present in the item pool: "
                f"{starting_crest_item!r}"
            )
        yield STARTING_CREST_REPLACEMENT_ITEM


def get_item_pool_size(
    trap_counts: Mapping[str, int] | None = None,
    split_dash_and_sprint: bool = False,
    quest_sanity: bool = True,
    randomize_needle_upgrades: bool = False,
    randomize_minor_pickups: bool = False,
) -> int:
    return sum(
        1
        for _name in iter_item_pool_names(
            None,
            trap_counts,
            split_dash_and_sprint,
            quest_sanity,
            randomize_needle_upgrades,
            randomize_minor_pickups,
        )
    )


class SilksongItem(Item):
    game = "Hollow Knight: Silksong"

    def __init__(
        self,
        name: str,
        classification: ItemClassification,
        code: int,
        player: int,
        placement_category: str | None = None,
    ) -> None:
        super().__init__(name, classification, code, player)
        self.silksong_placement_category = placement_category
