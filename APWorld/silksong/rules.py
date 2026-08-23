from __future__ import annotations

from BaseClasses import Item, ItemClassification
from rule_builder.rules import Has, HasAny
from worlds.generic.Rules import add_item_rule

from .items import (
    PROGRESSIVE_NEEDLE_UPGRADE_ITEM,
    PROGRESSION_ITEMS,
    get_vanilla_reward_name,
    item_data_table,
)
from .locations import (
    NEEDLE_UPGRADE_LOCATION_CATEGORY,
    OBSERVATION_LOCATION_CATEGORIES,
    PINMASTER_OIL_QUEST_LOCATION,
    RELIC_TURN_IN_LOCATION_CATEGORY,
    SCROUNGE_RELIC_ITEM_NAMES,
    VOLATILE_FLINTBEETLES_QUEST_LOCATION,
    location_data_table,
)
from .options import CATEGORY_OPTION_BY_LOCATION_CATEGORY
from .prices import get_shell_shard_donation_tool_pouch_requirements
from .requirements import (
    CREST_SLOT_LOCATION_NAMES,
    LOGIC_UNKNOWN_LOCATIONS,
    MEMORY_LOCKET_ITEM,
    POLLIP_HEART_COUNT,
    ROSARY_BANK_GATED_LOCATIONS,
    SIMPLE_KEY_ROSARY_BANK,
    UNVERIFIED_PROGRESSION_LOCATIONS,
    get_crawfather_requirements,
    get_logic_item_references,
    validate_requirements,
)
from .requirement_rules import (
    CrestSlotMemoryLocketRule,
    build_goal_rule,
    build_location_rule,
    build_native_source_rule,
    build_requirements_rule,
)

PROGRESSIVE_TOOL_POUCH_ITEM = 'Progressive Tool Pouch'
RESTORATION_OF_BELLHART_QUEST_LOCATION = (
    'Wish: Restoration of Bellhart'
)
CRAWFATHER_LOCATION = 'Boss: Crawfather'
CREST_SLOT_MEMORY_LOCKET_BUFFER = 2


def _is_not_progression_item(item: Item) -> bool:
    return not item.advancement


def _is_not_memory_locket(item: Item) -> bool:
    return item.name != MEMORY_LOCKET_ITEM


def _is_not_rosary_bank_key(item: Item) -> bool:
    return item.name != SIMPLE_KEY_ROSARY_BANK


def uses_randomized_memory_lockets_for_crest_slots(world) -> bool:
    memory_locket_mode = world.get_category_mode('MemoryLocket')
    crest_slot_mode = world.get_category_mode('CrestSlot')
    return (
        memory_locket_mode != 'vanilla'
        and crest_slot_mode != 'vanilla'
    )


def get_crest_slot_progression_item_count(world) -> int:
    return sum(
        bool(
            getattr(location, "item", None) is not None
            and getattr(location.item, "advancement", False)
        )
        for location in get_active_crest_slot_locations(world)
    )


def get_active_crest_slot_locations(world) -> tuple:
    get_location = getattr(world.multiworld, "get_location", None)
    if not callable(get_location):
        return ()

    locations = []
    for location_name in CREST_SLOT_LOCATION_NAMES:
        try:
            location = get_location(location_name, world.player)
        except (KeyError, StopIteration):
            continue
        locations.append(location)
    return tuple(locations)


def get_crest_slot_memory_locket_count(world) -> int:
    """Return the shared consumable budget for randomized Crest Slots.

    Full accessibility must keep every physical slot collectable, so it
    conservatively requires one Locket for every active slot.  Minimal only
    needs the progression-bearing slots for completion. Two spare Lockets
    preserve the established buffer for an optional purchase or mistake.
    """

    frozen_count = getattr(
        world,
        "_crest_slot_memory_locket_count",
        None,
    )
    if frozen_count is not None:
        return int(frozen_count)

    active_count = len(get_active_crest_slot_locations(world))
    if active_count == 0:
        return 0

    accessibility = getattr(world.options, "accessibility", None)
    accessibility_value = getattr(accessibility, "value", accessibility)
    minimal_value = getattr(accessibility, "option_minimal", 2)
    if accessibility is None or accessibility_value != minimal_value:
        return active_count

    return min(
        active_count,
        max(
            1,
            get_crest_slot_progression_item_count(world)
            + CREST_SLOT_MEMORY_LOCKET_BUFFER,
        ),
    )


def finalize_crest_slot_memory_locket_logic(world) -> None:
    if not uses_randomized_memory_lockets_for_crest_slots(world):
        world._crest_slot_memory_locket_count = None
        return

    world._crest_slot_memory_locket_count = (
        get_crest_slot_memory_locket_count(world)
    )
    for location_name in CREST_SLOT_LOCATION_NAMES:
        try:
            location = world.multiworld.get_location(
                location_name,
                world.player,
            )
        except (KeyError, StopIteration):
            continue
        if getattr(location, "item", None) is not None:
            # Progression balancing must not change the number after the
            # access rules and client slot data have agreed on it.
            location.locked = True


def _is_matching_shuffle_item(category: str):
    def item_rule(item: Item) -> bool:
        return (
            getattr(
                item,
                "silksong_placement_category",
                None,
            )
            == category
        )

    return item_rule


def _minimal_accessibility_players(multiworld) -> frozenset[int]:
    players = set()
    for player in multiworld.player_ids:
        options = getattr(multiworld.worlds[player], "options", None)
        accessibility = getattr(options, "accessibility", None)
        if accessibility is None:
            continue
        value = getattr(accessibility, "value", accessibility)
        minimal_value = getattr(accessibility, "option_minimal", 2)
        if value == minimal_value or value in ("minimal", "none"):
            players.add(player)
    return frozenset(players)


def enforce_global_shuffle_item_rules(multiworld) -> None:
    itempool = getattr(multiworld, "itempool", None)
    if itempool is not None and not any(
        getattr(item, "silksong_placement_category", None) is not None
        for item in itempool
    ):
        return
    minimal_players = _minimal_accessibility_players(multiworld)
    for location in multiworld.get_locations():
        target_category = getattr(
            location,
            "silksong_placement_category",
            None,
        )
        target_game = getattr(location, "game", None)
        target_player = location.player

        def item_rule(
            item: Item,
            target_category: str | None = target_category,
            target_game: str | None = target_game,
            target_player: int = target_player,
        ) -> bool:
            item_category = getattr(
                item,
                "silksong_placement_category",
                None,
            )
            if item_category is None:
                return True
            if (
                target_game != "Hollow Knight: Silksong"
                or target_category != item_category
            ):
                return False
            return not (
                item.advancement
                and target_player in minimal_players
                and item.player not in minimal_players
            )

        add_item_rule(location, item_rule)


def set_silksong_rules(world) -> None:
    validate_requirements(
        location_data_table.keys(),
        item_data_table.keys(),
        PROGRESSION_ITEMS,
    )
    split_dash_and_sprint = world.is_split_dash_and_sprint()
    randomize_needle_upgrades = bool(
        world.options.randomize_needle_upgrades.value
    )
    allow_bellways_before_bell_beast = (
        world.allows_bellways_before_bell_beast()
    )
    skips_tier = world.get_skips_tier()
    scuttlebrace_logic_enabled = (
        world.is_scuttlebrace_logic_enabled()
    )
    individual_relic_turn_ins = bool(
        world.options.individual_relic_turn_ins.value
    )
    relic_mode = world.get_category_mode('Relic')
    memory_locket_mode = world.get_category_mode('MemoryLocket')
    crest_slot_mode = world.get_category_mode('CrestSlot')
    pollip_heart_mode = world.get_category_mode('PollipHeart')
    pollip_heart_count = (
        POLLIP_HEART_COUNT
        if pollip_heart_mode != 'vanilla'
        else 0
    )
    randomized_memory_lockets_for_crest_slots = (
        memory_locket_mode != 'vanilla'
        and crest_slot_mode != 'vanilla'
    )
    randomized_crest_slots_enabled = crest_slot_mode != 'vanilla'
    starting_location = world.get_starting_location_key()
    trails_end_requirement = world.get_trails_end_requirement_key()
    shell_shard_donation_pouch_requirements = (
        get_shell_shard_donation_tool_pouch_requirements(
            world.get_purchase_prices()
        )
    )
    # The Bone Bottom fossil does not exist until the statue donation is
    # completed. Inherit that donation's randomized hard-capacity gate so a
    # downstream item cannot be considered reachable before Hornet can carry
    # enough Shell Shards to construct the statue.
    shell_shard_donation_pouch_requirements[
        'Bone Bottom - Shell Shard Cache'
    ] = shell_shard_donation_pouch_requirements.get(
        'Wish: An Icon of Hope',
        0,
    )
    excluded_location_names = world.get_goal_excluded_location_names()
    logic_item_references = get_logic_item_references()
    world._silksong_rule_builder_rules = {}

    for location_name, location_data in location_data_table.items():
        if location_name == "Goal":
            continue
        if location_name in excluded_location_names:
            continue
        if (
            location_data.category == RELIC_TURN_IN_LOCATION_CATEGORY
            and not individual_relic_turn_ins
        ):
            continue
        if (
            location_data.category == NEEDLE_UPGRADE_LOCATION_CATEGORY
            and not randomize_needle_upgrades
        ):
            continue
        if (
            location_name == PINMASTER_OIL_QUEST_LOCATION
            and randomize_needle_upgrades
        ):
            continue
        if (
            location_name == VOLATILE_FLINTBEETLES_QUEST_LOCATION
            and world.get_category_mode('MemoryLocket') != 'vanilla'
        ):
            continue
        if (
            location_name == 'Wish: Bugs of Pharloom'
            and world.get_category_mode('ToolPouch') != 'vanilla'
        ):
            continue

        mode = (
            world.get_location_randomization_mode(
                location_name,
                location_data.category,
            )
            if location_data.category
            in {*CATEGORY_OPTION_BY_LOCATION_CATEGORY, "Resource"}
            else "anywhere"
        )
        if (
            location_data.category in OBSERVATION_LOCATION_CATEGORIES
            and mode == "vanilla"
        ):
            continue

        location = world.multiworld.get_location(location_name, world.player)
        if (
            mode == "vanilla"
            and get_vanilla_reward_name(
                location_name,
                location_data.category,
            ) in logic_item_references
        ):
            location_rule = build_native_source_rule(
                location_name,
                location_data.category,
                split_dash_and_sprint=split_dash_and_sprint,
                allow_bellways_before_bell_beast=(
                    allow_bellways_before_bell_beast
                ),
                skips_tier=skips_tier,
                randomized_crest_slots_enabled=(
                    randomized_crest_slots_enabled
                ),
                starting_location=starting_location,
                trails_end_requirement=trails_end_requirement,
                scuttlebrace_logic_enabled=(
                    scuttlebrace_logic_enabled
                ),
                pollip_heart_count=pollip_heart_count,
            )
        else:
            location_rule = build_location_rule(
                location_name,
                split_dash_and_sprint=split_dash_and_sprint,
                allow_bellways_before_bell_beast=(
                    allow_bellways_before_bell_beast
                ),
                skips_tier=skips_tier,
                randomized_crest_slots_enabled=(
                    randomized_crest_slots_enabled
                ),
                starting_location=starting_location,
                trails_end_requirement=trails_end_requirement,
                scuttlebrace_logic_enabled=(
                    scuttlebrace_logic_enabled
                ),
                pollip_heart_count=pollip_heart_count,
            )
            if location_name == CRAWFATHER_LOCATION:
                location_rule = build_requirements_rule(
                    get_crawfather_requirements(
                        randomize_needle_upgrades
                    ),
                    split_dash_and_sprint=split_dash_and_sprint,
                    allow_bellways_before_bell_beast=(
                        allow_bellways_before_bell_beast
                    ),
                    skips_tier=skips_tier,
                    randomized_crest_slots_enabled=(
                        randomized_crest_slots_enabled
                    ),
                    starting_location=starting_location,
                    trails_end_requirement=trails_end_requirement,
                    scuttlebrace_logic_enabled=(
                        scuttlebrace_logic_enabled
                    ),
                )
            if (
                randomized_memory_lockets_for_crest_slots
                and location_data.category == "CrestSlot"
            ):
                location_rule = (
                    location_rule
                    & CrestSlotMemoryLocketRule()
                )
        required_tool_pouch_count = (
            shell_shard_donation_pouch_requirements.get(location_name, 0)
        )
        if required_tool_pouch_count > 0:
            location_rule = location_rule & Has(
                PROGRESSIVE_TOOL_POUCH_ITEM,
                required_tool_pouch_count,
            )
        if (
            randomize_needle_upgrades
            and location_name == RESTORATION_OF_BELLHART_QUEST_LOCATION
        ):
            # FullQuestBase Belltown House Start requires nailUpgrades > 0.
            # The client mirrors received Progressive Needle Upgrades into
            # that native field, so this is the shuffled-mode gate.
            location_rule = location_rule & Has(
                PROGRESSIVE_NEEDLE_UPGRADE_ITEM
            )

        if (
            relic_mode != 'vanilla'
            and location_name == RESTORATION_OF_BELLHART_QUEST_LOCATION
        ):
            # BelltownRelicDealerGaveRelic is set only when Scrounge takes at
            # least one deposited relic. In vanilla mode the free Moss Grotto
            # Commandment supplies it. Shuffled modes need an actual received
            # Scrounge-compatible relic before this Wish can appear.
            location_rule = location_rule & HasAny(
                *SCROUNGE_RELIC_ITEM_NAMES
            )

        world._silksong_rule_builder_rules[location_name] = location_rule
        world.set_rule(location, location_rule)

        # Vanilla logic events and the fixed shuffle exceptions
        # already have their sole legal reward.
        if getattr(location, "item", None) is not None:
            continue

        if mode == "shuffle":
            placement_category = (
                world.get_location_shuffle_placement_category(
                    location_name,
                    location_data.category,
                )
            )
            add_item_rule(
                location,
                _is_matching_shuffle_item(placement_category),
            )

        if location_name in ROSARY_BANK_GATED_LOCATIONS:
            # The door's own key is excluded from these locations. Its
            # classification is option-dependent: useful when every bank
            # reward is vanilla and progression whenever at least one becomes
            # an AP check.
            add_item_rule(location, _is_not_rosary_bank_key)

        if location_name in LOGIC_UNKNOWN_LOCATIONS:
            add_item_rule(location, _is_not_progression_item)
        # Randomized Lockets cannot sit behind their all-20 Crest Slot cost.
        # With vanilla Lockets their count is not represented, so the
        # conservative non-progression restriction remains.
        elif location_data_table[location_name].category == "CrestSlot":
            add_item_rule(
                location,
                _is_not_memory_locket
                if randomized_memory_lockets_for_crest_slots
                else _is_not_progression_item,
            )
        elif location_name in UNVERIFIED_PROGRESSION_LOCATIONS:
            add_item_rule(location, _is_not_progression_item)

    goal_key = world.get_goal_key()
    flea_hunt_count = world.get_flea_hunt_goal_count()
    goal = world.multiworld.get_location("Goal", world.player)
    goal_rule = build_goal_rule(
        goal_key,
        split_dash_and_sprint=split_dash_and_sprint,
        flea_hunt_count=flea_hunt_count,
        pollip_heart_count=pollip_heart_count,
        allow_bellways_before_bell_beast=(
            allow_bellways_before_bell_beast
        ),
        skips_tier=skips_tier,
        randomized_crest_slots_enabled=(
            randomized_crest_slots_enabled
        ),
        starting_location=starting_location,
        trails_end_requirement=trails_end_requirement,
        scuttlebrace_logic_enabled=scuttlebrace_logic_enabled,
    )
    world._silksong_rule_builder_rules["Goal"] = goal_rule
    world.set_rule(
        goal,
        goal_rule,
    )
    goal.place_locked_item(world.create_event("Victory"))
    completion_rule = Has("Victory")
    world._silksong_rule_builder_rules["Completion"] = completion_rule
    world.set_completion_rule(completion_rule)
