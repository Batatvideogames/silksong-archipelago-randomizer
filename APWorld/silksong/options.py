from __future__ import annotations

from dataclasses import dataclass

from Options import Choice, PerGameCommonOptions, Range, Toggle

from .minor_families import (
    MINOR_FAMILY_KEYS,
    MINOR_FAMILY_OPTION_BY_KEY,
    get_minor_family_shuffle_category,
)


class CategoryRandomization(Choice):
    """Choose how one source/reward category participates in the seed."""

    option_vanilla = 0
    alias_off = 0
    option_anywhere = 1
    option_shuffle = 2
    default = option_anywhere


class SkillRandomization(CategoryRandomization):
    display_name = "Skill Randomization"


class ToolRandomization(CategoryRandomization):
    display_name = "Tool Randomization"


class SilkSkillRandomization(CategoryRandomization):
    display_name = "Silk Skill Randomization"


class CrestRandomization(CategoryRandomization):
    display_name = "Crest Randomization"


class FleaRandomization(CategoryRandomization):
    display_name = "Flea Randomization"


class CrestSlotRandomization(CategoryRandomization):
    display_name = "Crest Slot Randomization"


class MaskShardRandomization(CategoryRandomization):
    display_name = "Mask Shard Randomization"


class SpoolFragmentRandomization(CategoryRandomization):
    display_name = "Spool Fragment Randomization"


class SilkHeartRandomization(CategoryRandomization):
    display_name = "Silk Heart Randomization"


class BellwayRandomization(CategoryRandomization):
    display_name = "Bellway Randomization"


class VentricaRandomization(CategoryRandomization):
    display_name = "Ventrica Randomization"


class MapRandomization(CategoryRandomization):
    display_name = "Map Randomization"


class MelodyRandomization(CategoryRandomization):
    display_name = "Melody Randomization"


class PinRandomization(CategoryRandomization):
    display_name = "Pin Randomization"


class RelicRandomization(CategoryRandomization):
    display_name = "Relic Randomization"


class CraftingKitRandomization(CategoryRandomization):
    display_name = "Crafting Kit Randomization"


class ToolPouchRandomization(CategoryRandomization):
    display_name = "Tool Pouch Randomization"


class RepeatedCollectibleRandomization(Choice):
    """Randomize a repeated consumable family globally or leave it vanilla."""

    option_vanilla = CategoryRandomization.option_vanilla
    alias_off = option_vanilla
    option_anywhere = CategoryRandomization.option_anywhere
    default = option_vanilla


class MemoryLocketRandomization(RepeatedCollectibleRandomization):
    display_name = "Memory Locket Randomization"


class CraftmetalRandomization(RepeatedCollectibleRandomization):
    display_name = "Craftmetal Randomization"


class MossberryRandomization(RepeatedCollectibleRandomization):
    display_name = "Mossberry Randomization"


class PollipHeartRandomization(CategoryRandomization):
    """Randomize the six finite Pollip Hearts globally or among themselves."""

    display_name = "Pollip Heart Randomization"
    default = CategoryRandomization.option_vanilla


class SilkeaterRandomization(RepeatedCollectibleRandomization):
    display_name = "Silkeater Randomization"


class MajorKeyRandomization(RepeatedCollectibleRandomization):
    display_name = "Major Key Randomization"


class SimpleKeyRandomization(Choice):
    """Randomize four destination-specific keys without fungible spending."""

    option_anywhere = CategoryRandomization.option_anywhere
    option_shuffle = CategoryRandomization.option_shuffle
    default = option_anywhere
    display_name = "Simple Key Randomization"


class MinorFamilyRandomization(CategoryRandomization):
    """Choose how this minor pickup family participates in the seed."""

    default = CategoryRandomization.option_vanilla


class FrayedRosaryStringRandomization(MinorFamilyRandomization):
    display_name = "Frayed Rosary String Randomization"


class RosaryStringRandomization(MinorFamilyRandomization):
    display_name = "Rosary String Randomization"


class RosaryNecklaceRandomization(MinorFamilyRandomization):
    display_name = "Rosary Necklace Randomization"


class HeavyRosaryNecklaceRandomization(MinorFamilyRandomization):
    display_name = "Heavy Rosary Necklace Randomization"


class PaleRosaryNecklaceRandomization(MinorFamilyRandomization):
    display_name = "Pale Rosary Necklace Randomization"


class ShardBundleRandomization(MinorFamilyRandomization):
    display_name = "Shard Bundle Randomization"


class BeastShardRandomization(MinorFamilyRandomization):
    display_name = "Beast Shard Randomization"


class PristineCoreRandomization(MinorFamilyRandomization):
    display_name = "Pristine Core Randomization"


class RosaryCacheRandomization(MinorFamilyRandomization):
    display_name = "Rosary Cache Randomization"


class ShellShardCacheRandomization(MinorFamilyRandomization):
    display_name = "Shell Shard Cache Randomization"


class BossSanity(CategoryRandomization):
    display_name = "Boss Sanity"


class BellShrineSanity(CategoryRandomization):
    display_name = "Bell Shrine Sanity"


class QuestSanity(CategoryRandomization):
    """Vanilla keeps quest rewards unchanged. Shuffle randomizes rewards only among quest checks. Anywhere joins the global item pool."""

    display_name = "Quest Sanity"


CATEGORY_OPTION_BY_LOCATION_CATEGORY: dict[str, str] = {
    "Skill": "skill_randomization",
    "Tool": "tool_randomization",
    "Spell": "silk_skill_randomization",
    "Crest": "crest_randomization",
    "Flea": "flea_randomization",
    "CrestSlot": "crest_slot_randomization",
    "MaskShard": "mask_shard_randomization",
    "SpoolFragment": "spool_fragment_randomization",
    "SilkHeart": "silk_heart_randomization",
    "Bellway": "bellway_randomization",
    "Ventrica": "ventrica_randomization",
    "Map": "map_randomization",
    "Melody": "melody_randomization",
    "Pin": "pin_randomization",
    "Relic": "relic_randomization",
    "Upgrade": "crafting_kit_randomization",
    "Key": "simple_key_randomization",
    "MemoryLocket": "memory_locket_randomization",
    "Craftmetal": "craftmetal_randomization",
    "Mossberry": "mossberry_randomization",
    "PollipHeart": "pollip_heart_randomization",
    "Silkeater": "silkeater_randomization",
    "MajorKey": "major_key_randomization",
    "ToolPouch": "tool_pouch_randomization",
    "Boss": "boss_sanity",
    "BellShrine": "bell_shrine_sanity",
    "Quest": "quest_sanity",
}

CATEGORY_MODE_KEY_BY_VALUE: dict[int, str] = {
    CategoryRandomization.option_vanilla: "vanilla",
    CategoryRandomization.option_anywhere: "anywhere",
    CategoryRandomization.option_shuffle: "shuffle",
}


def get_category_mode_value(options, category: str) -> int:
    option_name = CATEGORY_OPTION_BY_LOCATION_CATEGORY[category]
    option = getattr(options, option_name, None)
    return (
        (
            CategoryRandomization.option_vanilla
            if category in {
                "Resource",
                "MemoryLocket",
                "Craftmetal",
                "Mossberry",
                "PollipHeart",
                "Silkeater",
                "MajorKey",
                "ToolPouch",
            }
            else CategoryRandomization.option_anywhere
        )
        if option is None
        else int(option.value)
    )


def get_category_mode_key(options, category: str) -> str:
    value = get_category_mode_value(options, category)
    try:
        return CATEGORY_MODE_KEY_BY_VALUE[value]
    except KeyError as exc:
        raise ValueError(
            f"Unknown {category} randomization mode value: {value!r}"
        ) from exc


def get_minor_family_mode_key(options, family_key: str) -> str:
    try:
        option_name = MINOR_FAMILY_OPTION_BY_KEY[family_key]
    except KeyError as exc:
        raise ValueError(
            f"Unknown minor pickup family: {family_key!r}"
        ) from exc

    option = getattr(options, option_name, None)
    key = "vanilla" if option is None else option.current_key
    if key not in {"vanilla", "shuffle", "anywhere"}:
        raise ValueError(
            f"Unknown {option_name} mode: {key!r}"
        )
    return key


def get_minor_family_modes(options) -> dict[str, str]:
    return {
        family_key: get_minor_family_mode_key(options, family_key)
        for family_key in MINOR_FAMILY_KEYS
    }


def get_minor_family_shuffle_placement_category(
    options,
    family_key: str,
) -> str:
    """Return the independent shuffle lane for one minor pickup family."""

    try:
        option_name = MINOR_FAMILY_OPTION_BY_KEY[family_key]
    except KeyError as exc:
        raise ValueError(
            f"Unknown minor pickup family: {family_key!r}"
        ) from exc

    return get_minor_family_shuffle_category(family_key)


def get_aggregate_minor_mode_key(options) -> str:
    modes = tuple(get_minor_family_modes(options).values())
    if all(mode == "vanilla" for mode in modes):
        return "vanilla"
    if any(mode == "anywhere" for mode in modes):
        return "anywhere"
    return "shuffle"


class Goal(Choice):
    """Choose the victory condition.

    Sources verified unavailable before the Act 2 ending are excluded. When
    Skills are randomized, Silk Soar remains in the item pool while its Abyss
    source is omitted.
    """

    display_name = "Goal"
    option_act_2 = 0
    option_act_3 = 1
    option_flea_hunt = 2
    default = option_act_3


class FleaHuntCount(Range):
    """Number of distinct AP Fleas received for Flea Hunt victory."""

    display_name = "Flea Hunt Count"
    range_start = 1
    range_end = 30
    default = 20


class StartingLocation(Choice):
    """Use the vanilla opening or experimental Bone Bottom start with normal cloak."""

    display_name = "Starting Location"
    option_vanilla = 0
    option_bone_bottom = 1
    default = option_vanilla


class StartingCrest(Choice):
    """Choose Hornet's starting Crest when Crests are randomized."""

    display_name = "Starting Crest"
    # YAML ``random`` is handled by Archipelago's Choice parser and resolves
    # to one of these concrete values before the world is generated.
    option_hunter = 0
    option_wanderer = 1
    option_reaper = 2
    option_beast = 3
    option_architect = 4
    option_witch = 5
    option_shaman = 6
    default = option_hunter


class EarlyDash(Toggle):
    """Place Dash early when Skills are randomized."""

    display_name = "Early Dash"
    default = 0


class SplitDashAndSprint(Toggle):
    """Use separate Sprint and Dash items with Skills set to anywhere."""

    display_name = "Split Dash And Sprint"
    default = 0


class RandomizeNeedleUpgrades(Toggle):
    """Shuffle Plinney's Needle upgrades and their three Pale Oils."""

    display_name = "Randomize Needle Upgrades"
    default = 0


class IndividualRelicTurnIns(Toggle):
    """Make Scrounge's 15 relic and Cardinius's 6 cylinder deposits individual AP checks instead of paying Rosaries."""

    display_name = "Individual Relic Turn-ins"
    default = 0


class LogicAuditMode(Toggle):
    """Start with every randomized progression/useful item, make checks filler."""

    display_name = "Logic Audit Mode"
    default = 0


class EasySkips(Toggle):
    """Allow explicitly documented low-difficulty movement skips in logic."""

    display_name = "Easy Skips"
    default = 0


class StartWithMaps(Toggle):
    """Start with every currently randomized Shakra map."""

    display_name = "Start With Maps"
    default = 0


class AutomaticCompass(Toggle):
    """Always show Hornet on maps without granting or equipping Compass."""

    display_name = "Automatic Compass"
    default = 0


class CheckMapMarkers(Choice):
    """Show enabled, unchecked Archipelago checks on the in-game map."""

    display_name = "Check Map Markers"
    option_off = 0
    option_mapped_rooms = 1
    option_owned_maps = 2
    option_all = 3
    default = option_off


class BellwayAccess(Choice):
    """Choose whether Bell Beast must be defeated before using Bellways."""

    display_name = "Bellway Access"
    option_bell_beast_required = 0
    option_randomized_stations = 1
    default = option_bell_beast_required


class EnemyRosaryMultiplier(Choice):
    """Multiply Rosaries dropped by defeated enemies only."""

    display_name = "Enemy Rosary Multiplier"
    option_x1 = 0
    option_x1_5 = 1
    option_x2 = 2
    option_x3 = 3
    default = option_x1


class EnemyShardMultiplier(Choice):
    """Multiply Shell Shards dropped by defeated enemies only."""

    display_name = "Enemy Shell Shard Multiplier"
    option_x1 = 0
    option_x1_5 = 1
    option_x2 = 2
    option_x3 = 3
    default = option_x1


class PurchasePriceRandomization(Choice):
    """Choose how one purchase family derives its prices for this seed."""

    option_vanilla = 0
    option_free = 1
    option_shuffled = 2
    option_cheap = 3
    option_expensive = 4
    default = option_vanilla


class NormalShopPrices(PurchasePriceRandomization):
    """Randomize prices for ShopItem purchases other than maps and pins."""

    display_name = "Normal Shop Prices"


class BellwayPrices(PurchasePriceRandomization):
    """Randomize prices charged by Bellway toll machines."""

    display_name = "Bellway Prices"


class MapPrices(PurchasePriceRandomization):
    """Randomize prices for Shakra's area maps."""

    display_name = "Map Prices"


class PinPrices(PurchasePriceRandomization):
    """Randomize prices for Shakra's permanent map pins."""

    display_name = "Pin Prices"


class UpgradePrices(PurchasePriceRandomization):
    """Randomize the two paid Needle-upgrade service prices."""

    display_name = "Upgrade Prices"


class DonationPrices(PurchasePriceRandomization):
    """Randomize Rosary and Shell Shard Wish donation requirements."""

    display_name = "Donation Prices"


class FasterDialogue(Toggle):
    """Speed up ordinary NPC dialogue without skipping conversations."""

    display_name = "Faster Dialogue"
    default = 0


class DeathLink(Toggle):
    """Share deaths with other DeathLink-enabled players."""

    display_name = "Death Link"
    default = 0


class SilkLink(Toggle):
    """Share only the base Spool's current Silk with other enabled Silksong players."""

    display_name = "Silk Link"
    default = 0


class RosaryLink(Toggle):
    """Share loose Rosary gains and costs with other enabled Silksong players."""

    display_name = "Rosary Link"
    default = 0


class ShellShardLink(Toggle):
    """Share base-pouch Shell Shard gains and costs with enabled Silksong players."""

    display_name = "Shell Shard Link"
    default = 0


class TrapPercentage(Range):
    """Percent of all filler items in the random pool replaced by traps."""

    display_name = "Trap Percentage"
    range_start = 0
    range_end = 100
    default = 0


class TrapWeight(Range):
    """Relative trap frequency. Zero disables that trap type."""

    range_start = 0
    range_end = 100
    default = 25


class StaggerTrapWeight(TrapWeight):
    """Relative frequency of Stagger Traps, zero disables them."""

    display_name = "Stagger Trap Weight"


class RosarySpillTrapWeight(TrapWeight):
    """Relative frequency of Rosary Spill Traps, zero disables them."""

    display_name = "Rosary Spill Trap Weight"


class DarknessTrapWeight(TrapWeight):
    """Relative frequency of Darkness Traps, zero disables them."""

    display_name = "Darkness Trap Weight"


class CursedCrestTrapWeight(TrapWeight):
    """Relative frequency of Cursed Crest Traps, zero disables them."""

    display_name = "Cursed Crest Trap Weight"


class MuckmaggotStatusTrapWeight(TrapWeight):
    """Relative frequency of Muckmaggot Status Traps, zero disables them."""

    display_name = "Muckmaggot Status Trap Weight"


class NakedTrapWeight(TrapWeight):
    """Relative frequency of two-minute Naked Traps, zero disables them."""

    display_name = "Naked Trap Weight"


@dataclass
class SilksongOptions(PerGameCommonOptions):
    goal: Goal
    flea_hunt_count: FleaHuntCount
    starting_location: StartingLocation
    starting_crest: StartingCrest
    early_dash: EarlyDash
    split_dash_and_sprint: SplitDashAndSprint
    randomize_needle_upgrades: RandomizeNeedleUpgrades
    individual_relic_turn_ins: IndividualRelicTurnIns
    logic_audit_mode: LogicAuditMode
    easy_skips: EasySkips
    start_with_maps: StartWithMaps
    automatic_compass: AutomaticCompass
    check_map_markers: CheckMapMarkers
    bellway_access: BellwayAccess
    enemy_rosary_multiplier: EnemyRosaryMultiplier
    enemy_shard_multiplier: EnemyShardMultiplier
    normal_shop_prices: NormalShopPrices
    bellway_prices: BellwayPrices
    map_prices: MapPrices
    pin_prices: PinPrices
    upgrade_prices: UpgradePrices
    donation_prices: DonationPrices
    faster_dialogue: FasterDialogue
    death_link: DeathLink
    silk_link: SilkLink
    rosary_link: RosaryLink
    shell_shard_link: ShellShardLink
    skill_randomization: SkillRandomization
    tool_randomization: ToolRandomization
    silk_skill_randomization: SilkSkillRandomization
    crest_randomization: CrestRandomization
    flea_randomization: FleaRandomization
    crest_slot_randomization: CrestSlotRandomization
    mask_shard_randomization: MaskShardRandomization
    spool_fragment_randomization: SpoolFragmentRandomization
    silk_heart_randomization: SilkHeartRandomization
    bellway_randomization: BellwayRandomization
    ventrica_randomization: VentricaRandomization
    map_randomization: MapRandomization
    melody_randomization: MelodyRandomization
    pin_randomization: PinRandomization
    relic_randomization: RelicRandomization
    crafting_kit_randomization: CraftingKitRandomization
    simple_key_randomization: SimpleKeyRandomization
    memory_locket_randomization: MemoryLocketRandomization
    craftmetal_randomization: CraftmetalRandomization
    mossberry_randomization: MossberryRandomization
    pollip_heart_randomization: PollipHeartRandomization
    silkeater_randomization: SilkeaterRandomization
    major_key_randomization: MajorKeyRandomization
    tool_pouch_randomization: ToolPouchRandomization
    frayed_rosary_string_randomization: FrayedRosaryStringRandomization
    rosary_string_randomization: RosaryStringRandomization
    rosary_necklace_randomization: RosaryNecklaceRandomization
    heavy_rosary_necklace_randomization: HeavyRosaryNecklaceRandomization
    pale_rosary_necklace_randomization: PaleRosaryNecklaceRandomization
    shard_bundle_randomization: ShardBundleRandomization
    beast_shard_randomization: BeastShardRandomization
    pristine_core_randomization: PristineCoreRandomization
    rosary_cache_randomization: RosaryCacheRandomization
    shell_shard_cache_randomization: ShellShardCacheRandomization
    boss_sanity: BossSanity
    bell_shrine_sanity: BellShrineSanity
    quest_sanity: QuestSanity
    trap_percentage: TrapPercentage
    stagger_trap_weight: StaggerTrapWeight
    rosary_spill_trap_weight: RosarySpillTrapWeight
    darkness_trap_weight: DarknessTrapWeight
    cursed_crest_trap_weight: CursedCrestTrapWeight
    muckmaggot_status_trap_weight: MuckmaggotStatusTrapWeight
    naked_trap_weight: NakedTrapWeight
