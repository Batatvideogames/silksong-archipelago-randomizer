from __future__ import annotations

from dataclasses import dataclass

from Options import (
    Accessibility,
    Choice,
    FreeText,
    PerGameCommonOptions,
    Range,
    Toggle,
    Visibility,
)

from .minor_families import (
    MINOR_FAMILY_KEYS,
    MINOR_FAMILY_OPTION_BY_KEY,
    get_minor_family_shuffle_category,
)


class GlobalRandomization(Choice):
    """Keep a category vanilla or mix it into the global pool."""

    option_vanilla = 0
    alias_off = 0
    option_anywhere = 1
    default = option_anywhere


class CategoryRandomization(GlobalRandomization):
    """Choose how one source/reward category participates in the seed."""

    option_shuffle = 2


class SkillRandomization(CategoryRandomization):
    """Randomizes the 9 traversal skills in the game.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    shuffle: mixes them up between one another
    """

    display_name = "Skill Randomization"


class ToolRandomization(CategoryRandomization):
    """Randomizes the 59 tools in the game.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    shuffle: mixes them up between one another
    """

    display_name = "Tool Randomization"


class SilkSkillRandomization(CategoryRandomization):
    """Randomizes the 6 Silk Skills in the game.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    shuffle: mixes them up between one another
    """

    display_name = "Silk Skill Randomization"


class CrestRandomization(CategoryRandomization):
    """Randomizes the 7 Crests in the game.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    shuffle: mixes them up between one another
    """

    display_name = "Crest Randomization"


class FleaRandomization(CategoryRandomization):
    """Randomizes the 30 Fleas in the game, including Kratt, Vog and the Huge Flea.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    shuffle: mixes them up between one another
    """

    display_name = "Flea Randomization"


class CrestSlotRandomization(CategoryRandomization):
    """Randomizes the 20 Crest Slots in the game.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    shuffle: mixes them up between one another
    """

    display_name = "Crest Slot Randomization"


class MaskShardRandomization(GlobalRandomization):
    """Randomizes the 20 Mask Shards in the game.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    """

    display_name = "Mask Shard Randomization"


class SpoolFragmentRandomization(GlobalRandomization):
    """Randomizes the 18 Spool Fragments in the game.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    """

    display_name = "Spool Fragment Randomization"


class SilkHeartRandomization(GlobalRandomization):
    """Randomizes the 3 Silk Hearts in the game.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    """

    display_name = "Silk Heart Randomization"


class BellwayRandomization(CategoryRandomization):
    """Randomizes the 10 Bellways in the game.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    shuffle: mixes them up between one another
    """

    display_name = "Bellway Randomization"


class VentricaRandomization(CategoryRandomization):
    """Randomizes the 6 Ventricas in the game.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    shuffle: mixes them up between one another
    """

    display_name = "Ventrica Randomization"


class MapRandomization(CategoryRandomization):
    """Randomizes the 28 Maps in the game.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    shuffle: mixes them up between one another
    """

    display_name = "Map Randomization"


class MelodyRandomization(CategoryRandomization):
    """Randomizes the 5 Melodies in the game: the three required to finish Act 2, Beastling Call and Elegy of the Deep.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    shuffle: mixes them up between one another
    """

    display_name = "Melody Randomization"


class PinRandomization(CategoryRandomization):
    """Randomizes the 4 map-pin types in the game.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    shuffle: mixes them up between one another
    """

    display_name = "Pin Randomization"


class RelicRandomization(CategoryRandomization):
    """Randomizes the 21 Relics in the game.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    shuffle: mixes them up between one another
    """

    display_name = "Relic Randomization"


class CraftingKitRandomization(GlobalRandomization):
    """Randomizes the 4 Crafting Kits in the game.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    """

    display_name = "Crafting Kit Randomization"


class ToolPouchRandomization(GlobalRandomization):
    """Randomizes the 4 Tool Pouch upgrades in the game.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    """

    display_name = "Tool Pouch Randomization"


class LoreTabletRandomization(CategoryRandomization):
    """Randomizes the 38 Lore Tablets in the game.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    shuffle: mixes them up between one another
    """

    display_name = "Lore Tablet Randomization"
    default = CategoryRandomization.option_vanilla


class RepeatedCollectibleRandomization(GlobalRandomization):
    """Randomize a repeated consumable family globally or leave it vanilla."""

    default = GlobalRandomization.option_vanilla


class MemoryLocketRandomization(RepeatedCollectibleRandomization):
    """Randomizes the 20 Memory Lockets in the game.

    If Crest Slots are randomized, using a Memory Locket will not unlock the
    slot for use, but whatever took its place in that slot remains its AP check.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    """

    display_name = "Memory Locket Randomization"


class CraftmetalRandomization(RepeatedCollectibleRandomization):
    """Randomizes the 8 Craftmetals in the game.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    """

    display_name = "Craftmetal Randomization"


class MossberryRandomization(RepeatedCollectibleRandomization):
    """Randomizes the 7 Mossberries in the game. Three are required for the Moss Druid's quest, while giving her the remaining four earns another check.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    """

    display_name = "Mossberry Randomization"


class PollipHeartRandomization(RepeatedCollectibleRandomization):
    """Randomizes the 6 finite Pollip Hearts in the game. They are required for Greyroot's quest in Shellwood.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    """

    display_name = "Pollip Heart Randomization"
    default = CategoryRandomization.option_vanilla


class SilkeaterRandomization(RepeatedCollectibleRandomization):
    """Randomizes the 9 Silkeaters in the game.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    """

    display_name = "Silkeater Randomization"


class MajorKeyRandomization(CategoryRandomization):
    """Randomizes Key of Apostate, White Key, Surgeon's Key, Architect's Key and Craw Summons.

    Key of Heretic and Key of Indolent remain at their vanilla locations until
    logic for the Slab is finished.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    shuffle: mixes them up between one another
    """

    display_name = "Major Key Randomization"
    default = CategoryRandomization.option_vanilla


class SimpleKeyRandomization(Choice):
    """Randomize four destination-specific keys without fungible spending.

    anywhere: mixes them into the global item pool
    shuffle: mixes them up between one another
    """

    option_anywhere = CategoryRandomization.option_anywhere
    option_shuffle = CategoryRandomization.option_shuffle
    default = option_anywhere
    display_name = "Simple Key Randomization"


class MinorFamilyRandomization(RepeatedCollectibleRandomization):
    """Choose how this minor pickup family participates in the seed."""


class MinorCacheRandomization(CategoryRandomization):
    """Choose how a cache family with varied amounts joins the seed."""

    default = CategoryRandomization.option_vanilla


class FrayedRosaryStringRandomization(MinorFamilyRandomization):
    """Randomizes the 19 Frayed Rosary Strings in the game.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    """

    display_name = "Frayed Rosary String Randomization"


class RosaryStringRandomization(MinorFamilyRandomization):
    """Randomizes the 4 Rosary Strings in the game.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    """

    display_name = "Rosary String Randomization"


class RosaryNecklaceRandomization(MinorFamilyRandomization):
    """Randomizes the 5 Rosary Necklaces in the game.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    """

    display_name = "Rosary Necklace Randomization"


class HeavyRosaryNecklaceRandomization(MinorFamilyRandomization):
    """Randomizes the 3 Heavy Rosary Necklaces in the game.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    """

    display_name = "Heavy Rosary Necklace Randomization"


class PaleRosaryNecklaceRandomization(MinorFamilyRandomization):
    """Randomizes the 2 Pale Rosary Necklaces in the game.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    """

    display_name = "Pale Rosary Necklace Randomization"


class ShardBundleRandomization(MinorFamilyRandomization):
    """Randomizes the 12 Shard Bundles in the game.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    """

    display_name = "Shard Bundle Randomization"


class BeastShardRandomization(MinorFamilyRandomization):
    """Randomizes the 7 Beast Shard upgrades in the game.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    """

    display_name = "Beast Shard Randomization"


class PristineCoreRandomization(MinorFamilyRandomization):
    """Randomizes the 2 Pristine Cores in the game.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    """

    display_name = "Pristine Core Randomization"


class RosaryCacheRandomization(MinorCacheRandomization):
    """Randomizes the 200 Rosary Caches in the game.

    These are the Rosaries usually seen strung up and out in the open while exploring.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    shuffle: mixes them up between one another
    """

    display_name = "Rosary Cache Randomization"


class ShellShardCacheRandomization(MinorCacheRandomization):
    """Randomizes the 153 Shell Shard Caches in the game.

    These are the Shards found in fossils that must be hit several times.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    shuffle: mixes them up between one another
    """

    display_name = "Shell Shard Cache Randomization"


class BossSanity(CategoryRandomization):
    """Randomizes the 34 drops from bosses in the game. This does not randomize the bosses themselves.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    shuffle: mixes them up between one another
    """

    display_name = "Boss Sanity"


class BellShrineSanity(CategoryRandomization):
    """Randomizes the 5 Judge Bell Shrines."""

    display_name = "Bell Shrine Sanity"
    default = CategoryRandomization.option_vanilla


class QuestSanity(CategoryRandomization):
    """Vanilla keeps quest rewards unchanged. Shuffle randomizes rewards only among quest checks. Anywhere joins the global item pool.

    This covers 25 quest rewards without randomizing the quests themselves.
    """

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
    "NeedleUpgrade": "needle_upgrade_randomization",
    "PaleOil": "pale_oil_randomization",
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
    "LoreTablet": "lore_tablet_randomization",
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
    option = getattr(options, option_name)
    value = int(option.value)
    if (
        value == CategoryRandomization.option_shuffle
        and not hasattr(type(option), "option_shuffle")
    ):
        raise ValueError(f"{option_name} does not support shuffle.")
    return value


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

    option = getattr(options, option_name)
    if (
        int(option.value) == CategoryRandomization.option_shuffle
        and not hasattr(type(option), "option_shuffle")
    ):
        raise ValueError(f"{option_name} does not support shuffle.")
    key = option.current_key
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


class SilksongAccessibility(Accessibility):
    """Set rules for reachability of your items/locations.

    **Full:** ensure everything can be reached and acquired.

    **Minimal:** ensure what is needed to reach your goal can be acquired.

    LogicUnknown locations never hold advancement items and remain out of logic
    until your goal is completed.
    Their map marker shows that the route is still incomplete.
    """

    display_name = "Accessibility"
    default = Accessibility.option_full


class Goal(Choice):
    """Act 1-3 require beating the chosen act, Cursed Ending is self
    explanatory, Flea Hunt is getting the set amount of fleas and Spelling Bee
    is getting every unique letter in the chosen phrase with Alphabet Rando on
    """

    display_name = "Goal"
    option_act_1 = 3
    option_act_2 = 0
    option_act_3 = 1
    option_flea_hunt = 2
    option_cursed_ending = 4
    option_spelling_bee = 5
    default = option_act_3


class SpellingBeePhrase(FreeText):
    """The word or phrase used by the Spelling Bee goal.

    Only A-Z letters and spaces are valid. Other characters change the goal to
    Act 2 and do not automatically enable Alphabet Mode.
    """

    display_name = "Spelling Bee Phrase"
    default = "Hornet"


class FleaHuntCount(Range):
    """Number of distinct AP Fleas received for Flea Hunt victory."""

    display_name = "Flea Hunt Count"
    range_start = 1
    range_end = 30
    default = 30


class StartingLocation(Choice):
    """Use the vanilla opening or experimental Bone Bottom start with normal cloak."""

    display_name = "Starting Location"
    option_vanilla = 0
    option_bone_bottom = 1
    default = option_vanilla


class StartingCrest(Choice):
    """Choose Hornet's starting Crest when Crests are randomized.

    random selects one of the seven Crests during generation.
    """

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
    default = "random"


class EarlyDash(Toggle):
    """Place Dash early when Skills are randomized."""

    display_name = "Early Dash"
    default = 1


class SplitDashAndSprint(Toggle):
    """Use separate Sprint and Dash items with Skills set to anywhere."""

    display_name = "Split Dash And Sprint"
    default = 0


class LedgegrabAbilityRando(Toggle):
    """EXPERIMENTAL FEATURE: Disables Hornets Ledge Grab Ability and must be found as item to acquire that ability, enable at own risk"""

    display_name = "Ledgegrab Ability Rando"
    default = 0
    visibility = Visibility.none


class SwimAbilityRando(Toggle):
    """EXPERIMENTAL FEATURE: Disables Hornets Swim Ability and must be found as item to acquire that ability, enable at own risk"""

    display_name = "Swim Ability Rando"
    default = 0
    visibility = Visibility.none


class NeedleUpgradeRandomization(GlobalRandomization):
    """Randomizes Plinney's four Needle upgrades.

    vanilla: leaves them at Plinney
    anywhere: mixes them into the global item pool
    """

    display_name = "Needle Upgrade Randomization"
    default = GlobalRandomization.option_vanilla


class PaleOilRandomization(GlobalRandomization):
    """Randomizes the three Pale Oils in the game.

    vanilla: leaves them where they normally are
    anywhere: mixes them into the global item pool
    """

    display_name = "Pale Oil Randomization"
    default = GlobalRandomization.option_vanilla


class AlphabetMode(Toggle):
    """Add Letter A through Letter Z to the item pool and hide unowned letters in game text.

    Text filtering only runs while the game language is English.
    This needs at least 26 randomized filler rewards.
    """

    display_name = "Alphabet Mode"
    default = 0


class TrailsEndRequirement(Choice):
    """Choose whether Trail's End uses 14 Shakra map-stock purchases or received maps."""

    display_name = "Trail's End Requirement"
    option_shakra_stock = 0
    option_owned_maps = 1
    default = option_shakra_stock


class IndividualRelicTurnIns(Toggle):
    """Make Scrounge's 15 relic and Cardinius's 6 cylinder deposits individual AP checks instead of paying Rosaries."""

    display_name = "Individual Relic Turn-ins"
    default = 0


class Skips(Choice):
    """Choose the hardest explicitly labelled movement skips allowed in logic."""

    display_name = "Skips"
    option_none = 0
    option_easy = 1
    option_moderate = 2
    option_difficult = 3
    default = option_none


class ScuttlebraceLogic(Toggle):
    """Allow Scuttlebrace movement routes in logic"""

    display_name = "Scuttlebrace Logic"
    default = 0


class StartWithMaps(Toggle):
    """Start with every eligible map except Lost Verdania."""

    display_name = "Start With Maps"
    default = 1


class StartFullyMapped(Toggle):
    """Start with the full world map drawn and all starting maps owned."""

    display_name = "Start Fully Mapped"
    default = 1


class AutomaticCompass(Toggle):
    """Always show Hornet on maps without granting or equipping Compass."""

    display_name = "Automatic Compass"
    default = 1


class CheckMapMarkers(Choice):
    """Show enabled, unchecked Archipelago checks on the in-game map."""

    display_name = "Check Map Markers"
    option_off = 0
    option_mapped_rooms = 1
    option_owned_maps = 2
    option_all = 3
    default = option_all


class RandomizedBellMarkers(Toggle):
    """Move the five Grand Gate Bell markers to their randomized local checks."""

    display_name = "Randomized Bell Markers"
    default = 1


class RandomizedMelodyMarkers(Toggle):
    """Move Threefold Melody markers to their randomized local checks."""

    display_name = "Randomized Melody Markers"
    default = 1


class VogHintCount(Range):
    """Maximum number of hints Vog can offer from one hint category."""

    range_start = 0
    range_end = 30


class VogWothHints(VogHintCount):
    """Maximum Way of the Hero area hints offered by Vog."""

    display_name = "Vog Way of the Hero Hints"
    default = 0


class VogFoolishHints(VogHintCount):
    """Maximum Foolish area hints offered by Vog."""

    display_name = "Vog Foolish Hints"
    default = 0


class VogGeneralHints(VogHintCount):
    """Maximum exact item location hints offered by Vog."""

    display_name = "Vog General Hints"
    default = 0


class BellwayAccess(Choice):
    """Choose whether Bell Beast must be defeated before using Bellways."""

    display_name = "Bellway Access"
    option_bell_beast_required = 0
    option_randomized_stations = 1
    default = option_randomized_stations


class EnemyRosaryMultiplier(Choice):
    """Multiply Rosaries dropped by defeated enemies only."""

    display_name = "Enemy Rosary Multiplier"
    option_x1 = 0
    option_x1_5 = 1
    option_x2 = 2
    option_x3 = 3
    option_x5 = 4
    option_x10 = 5
    default = option_x2


class EnemyShardMultiplier(Choice):
    """Multiply Shell Shards dropped by defeated enemies only."""

    display_name = "Enemy Shell Shard Multiplier"
    option_x1 = 0
    option_x1_5 = 1
    option_x2 = 2
    option_x3 = 3
    option_x5 = 4
    option_x10 = 5
    default = option_x2


class PurchasePriceRandomization(Choice):
    """Choose how one purchase family derives its prices for this seed."""

    option_vanilla = 0
    option_free = 1
    option_shuffle = 2
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


class VogHintPrices(PurchasePriceRandomization):
    """Randomize prices for Vog's Hero, Foolish and General hints."""

    display_name = "Vog Hint Prices"


class FasterDialogue(Toggle):
    """Speed up ordinary NPC dialogue without skipping conversations."""

    display_name = "Faster Dialogue"
    default = 1


class FasterSilkheartAnimation(Toggle):
    """Collect boss Silkhearts on contact. Off keeps the shortened pickup sequence and skips memories."""

    display_name = "Faster Silkheart Animation"
    default = 0


class DeathLink(Toggle):
    """Share deaths with other DeathLink-enabled players."""

    display_name = "Death Link"
    default = 0


class DeathLinkCocoon(Choice):
    """Choose how received DeathLinks affect your death cocoon and Rosaries.

    vanilla: Received DeathLinks replace an existing cocoon and move your
    carried Rosaries into the new cocoon.
    cocoonless: Received DeathLinks leave your carried Rosaries and any
    existing cocoon untouched.
    cocoon: The first received DeathLink can create a normal cocoon. Later
    received DeathLinks leave that cocoon and your carried Rosaries untouched.

    Your own deaths always use the normal game behavior.
    This option has no effect when Death Link is disabled.
    """

    display_name = "Death Link Cocoon"
    option_vanilla = 0
    option_cocoonless = 1
    option_cocoon = 2
    default = 2


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


class KnockbackLink(Toggle):
    """Share hit and Stagger Trap knockback with other enabled players.

    Incoming impacts use their direction at normal Stagger Trap strength.
    """

    display_name = "Knockback Link"
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
    """Relative frequency of Stagger Traps, zero disables them.

    When staggered, Hornet receives knockback but takes no damage.
    """

    display_name = "Stagger Trap Weight"


class RosarySpillTrapWeight(TrapWeight):
    """Relative frequency of Rosary Spill Traps, zero disables them."""

    display_name = "Rosary Spill Trap Weight"


class DarknessTrapWeight(TrapWeight):
    """Relative frequency of Darkness Traps, zero disables them.

    The screen is darkened to a small area of light around Hornet for 20 seconds.
    """

    display_name = "Darkness Trap Weight"


class CursedCrestTrapWeight(TrapWeight):
    """Relative frequency of Cursed Crest Traps, zero disables them.

    Hornet is inflicted with the Cursed Crest for 2 minutes, as if she had done
    the Rite of Rebirth quest with Greyroot.
    """

    display_name = "Cursed Crest Trap Weight"


class MuckmaggotStatusTrapWeight(TrapWeight):
    """Relative frequency of Muckmaggot Status Traps, zero disables them.

    Hornet is afflicted with Muckmaggots as if she fell into Bilewater. Binding
    or using a bench removes them.
    """

    display_name = "Muckmaggot Status Trap Weight"


class NakedTrapWeight(TrapWeight):
    """Relative frequency of two-minute Naked Traps, zero disables them.

    Hornet is left naked as if she were in the Slab escape sequence for 2 minutes.
    """

    display_name = "Naked Trap Weight"


@dataclass
class SilksongOptions(PerGameCommonOptions):
    accessibility: SilksongAccessibility
    goal: Goal
    spelling_bee_phrase: SpellingBeePhrase
    flea_hunt_count: FleaHuntCount
    starting_location: StartingLocation
    starting_crest: StartingCrest
    early_dash: EarlyDash
    split_dash_and_sprint: SplitDashAndSprint
    ledgegrab_ability_rando: LedgegrabAbilityRando
    swim_ability_rando: SwimAbilityRando
    trails_end_requirement: TrailsEndRequirement
    skips: Skips
    scuttlebrace_logic: ScuttlebraceLogic
    start_with_maps: StartWithMaps
    start_fully_mapped: StartFullyMapped
    automatic_compass: AutomaticCompass
    check_map_markers: CheckMapMarkers
    randomized_bell_markers: RandomizedBellMarkers
    randomized_melody_markers: RandomizedMelodyMarkers
    vog_woth_hints: VogWothHints
    vog_foolish_hints: VogFoolishHints
    vog_general_hints: VogGeneralHints
    bellway_access: BellwayAccess
    enemy_rosary_multiplier: EnemyRosaryMultiplier
    enemy_shard_multiplier: EnemyShardMultiplier
    normal_shop_prices: NormalShopPrices
    bellway_prices: BellwayPrices
    map_prices: MapPrices
    pin_prices: PinPrices
    upgrade_prices: UpgradePrices
    donation_prices: DonationPrices
    vog_hint_prices: VogHintPrices
    faster_dialogue: FasterDialogue
    faster_silkheart_animation: FasterSilkheartAnimation
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
    needle_upgrade_randomization: NeedleUpgradeRandomization
    pale_oil_randomization: PaleOilRandomization
    melody_randomization: MelodyRandomization
    pin_randomization: PinRandomization
    relic_randomization: RelicRandomization
    crafting_kit_randomization: CraftingKitRandomization
    major_key_randomization: MajorKeyRandomization
    simple_key_randomization: SimpleKeyRandomization
    memory_locket_randomization: MemoryLocketRandomization
    craftmetal_randomization: CraftmetalRandomization
    mossberry_randomization: MossberryRandomization
    pollip_heart_randomization: PollipHeartRandomization
    silkeater_randomization: SilkeaterRandomization
    tool_pouch_randomization: ToolPouchRandomization
    lore_tablet_randomization: LoreTabletRandomization
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
    individual_relic_turn_ins: IndividualRelicTurnIns
    death_link: DeathLink
    death_link_cocoon: DeathLinkCocoon
    silk_link: SilkLink
    rosary_link: RosaryLink
    shell_shard_link: ShellShardLink
    knockback_link: KnockbackLink
    alphabet_mode: AlphabetMode
    trap_percentage: TrapPercentage
    stagger_trap_weight: StaggerTrapWeight
    rosary_spill_trap_weight: RosarySpillTrapWeight
    darkness_trap_weight: DarknessTrapWeight
    cursed_crest_trap_weight: CursedCrestTrapWeight
    muckmaggot_status_trap_weight: MuckmaggotStatusTrapWeight
    naked_trap_weight: NakedTrapWeight
