from __future__ import annotations

from BaseClasses import Item, ItemClassification
from rule_builder.rules import Has, HasAny
from worlds.generic.Rules import add_item_rule

from .items import (
    PROGRESSIVE_NEEDLE_UPGRADE_ITEM,
    PROGRESSION_ITEMS,
    item_data_table,
)
from .locations import (
    NEEDLE_UPGRADE_LOCATION_CATEGORY,
    OBSERVATION_LOCATION_CATEGORIES,
    PINMASTER_OIL_QUEST_LOCATION,
    PROTOCOL_ONLY_LOCATION_NAMES,
    RELIC_TURN_IN_LOCATION_CATEGORY,
    SCROUNGE_RELIC_ITEM_NAMES,
    VOLATILE_FLINTBEETLES_QUEST_LOCATION,
    location_data_table,
)
from .minor_families import (
    get_minor_family_shuffle_category_for_location,
)
from .options import (
    CATEGORY_OPTION_BY_LOCATION_CATEGORY,
    get_category_mode_key,
)
from .prices import get_shell_shard_donation_tool_pouch_requirements
from .requirements import (
    CREST_SLOT_LOCATION_NAMES,
    DEFAULT_FLEA_HUNT_GOAL_COUNT,
    JUNK_ONLY_LOCATIONS,
    MEMORY_LOCKET_ITEM,
    POLLIP_HEART_COUNT,
    POLLIP_HEART_ITEM,
    ROSARY_BANK_GATED_LOCATIONS,
    SIMPLE_KEY_ROSARY_BANK,
    UNVERIFIED_PROGRESSION_LOCATIONS,
    validate_requirements,
)
from .requirement_rules import (
    CrestSlotMemoryLocketRule,
    build_goal_rule,
    build_location_rule,
    build_native_source_rule,
)

CREST_SLOT_MEMORY_LOCKET_BUFFER = 2
PROGRESSIVE_TOOL_POUCH_ITEM = 'Progressive Tool Pouch'
RESTORATION_OF_BELLHART_QUEST_LOCATION = (
    'Wish: Restoration of Bellhart'
)
POLLIP_POUCH_LOCATION = 'Pollip Pouch'


def _is_not_progression_item(item: Item) -> bool:
    return not item.advancement


def _is_not_memory_locket(item: Item) -> bool:
    return item.name != MEMORY_LOCKET_ITEM


def _is_not_rosary_bank_key(item: Item) -> bool:
    return item.name != SIMPLE_KEY_ROSARY_BANK


def _is_junk_item(item: Item) -> bool:
    return item.classification in (
        ItemClassification.filler,
        ItemClassification.trap,
    )


def uses_randomized_memory_lockets_for_crest_slots(world) -> bool:
    category_mode_getter = getattr(world, "get_category_mode", None)
    memory_locket_mode = (
        category_mode_getter('MemoryLocket')
        if callable(category_mode_getter)
        else get_category_mode_key(world.options, 'MemoryLocket')
    )
    crest_slot_mode = (
        category_mode_getter('CrestSlot')
        if callable(category_mode_getter)
        else get_category_mode_key(world.options, 'CrestSlot')
    )
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
    frozen_count = getattr(
        world,
        "_crest_slot_memory_locket_count",
        None,
    )
    if frozen_count is not None:
        return int(frozen_count)
    active_slot_count = len(get_active_crest_slot_locations(world))
    progression_count = get_crest_slot_progression_item_count(world)
    return min(
        active_slot_count,
        max(
            1,
            progression_count + CREST_SLOT_MEMORY_LOCKET_BUFFER,
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


def _make_local_filler_item_rule(player: int):
    def local_filler_item_rule(item: Item) -> bool:
        return (
            item.player == player
            and item.classification == ItemClassification.filler
        )

    return local_filler_item_rule


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


def enforce_global_shuffle_item_rules(multiworld) -> None:
    for location in multiworld.get_locations():
        target_category = getattr(
            location,
            "silksong_placement_category",
            None,
        )
        target_game = getattr(location, "game", None)

        def item_rule(
            item: Item,
            target_category: str | None = target_category,
            target_game: str | None = target_game,
        ) -> bool:
            item_category = getattr(
                item,
                "silksong_placement_category",
                None,
            )
            if item_category is None:
                return True
            return (
                target_game == "Hollow Knight: Silksong"
                and target_category == item_category
            )

        add_item_rule(location, item_rule)


def set_silksong_rules(world) -> None:
    validate_requirements(
        location_data_table.keys(),
        item_data_table.keys(),
        PROGRESSION_ITEMS,
    )
    split_dash_and_sprint = bool(
        getattr(
            getattr(world.options, "split_dash_and_sprint", None),
            "value",
            0,
        )
    )
    randomize_needle_upgrades = bool(
        getattr(
            getattr(
                world.options,
                "randomize_needle_upgrades",
                None,
            ),
            "value",
            0,
        )
    )
    bellway_access_getter = getattr(
        world,
        "allows_bellways_before_bell_beast",
        None,
    )
    allow_bellways_before_bell_beast = (
        bool(bellway_access_getter())
        if callable(bellway_access_getter)
        else False
    )
    easy_skips_getter = getattr(
        world,
        "is_easy_skips_enabled",
        None,
    )
    easy_skips_enabled = (
        bool(easy_skips_getter())
        if callable(easy_skips_getter)
        else False
    )
    logic_audit_mode_getter = getattr(
        world,
        "is_logic_audit_mode_enabled",
        None,
    )
    logic_audit_mode = (
        bool(logic_audit_mode_getter())
        if callable(logic_audit_mode_getter)
        else False
    )
    individual_relic_turn_ins = bool(
        getattr(
            getattr(
                world.options,
                "individual_relic_turn_ins",
                None,
            ),
            "value",
            0,
        )
    )
    logic_audit_item_rule = (
        _make_local_filler_item_rule(world.player)
        if logic_audit_mode
        else None
    )
    category_mode_getter = getattr(world, "get_category_mode", None)
    relic_mode = (
        category_mode_getter('Relic')
        if callable(category_mode_getter)
        else get_category_mode_key(world.options, 'Relic')
    )
    memory_locket_mode = (
        category_mode_getter('MemoryLocket')
        if callable(category_mode_getter)
        else get_category_mode_key(world.options, 'MemoryLocket')
    )
    crest_slot_mode = (
        category_mode_getter('CrestSlot')
        if callable(category_mode_getter)
        else get_category_mode_key(world.options, 'CrestSlot')
    )
    pollip_heart_mode = (
        category_mode_getter('PollipHeart')
        if callable(category_mode_getter)
        else get_category_mode_key(world.options, 'PollipHeart')
    )
    randomized_memory_lockets_for_crest_slots = (
        memory_locket_mode != 'vanilla'
        and crest_slot_mode != 'vanilla'
    )
    randomized_crest_slots_enabled = crest_slot_mode != 'vanilla'
    starting_location_getter = getattr(
        world,
        "get_starting_location_key",
        None,
    )
    starting_location = (
        starting_location_getter()
        if callable(starting_location_getter)
        else 'vanilla'
    )
    purchase_price_getter = getattr(world, 'get_purchase_prices', None)
    shell_shard_donation_pouch_requirements = (
        get_shell_shard_donation_tool_pouch_requirements(
            purchase_price_getter()
            if callable(purchase_price_getter)
            else {}
        )
    )
    # The Bone Bottom fossil does not exist until the statue donation is
    # completed. Inherit that donation's randomized hard-capacity gate so a
    # downstream item cannot be considered reachable before Hornet can carry
    # enough Shell Shards to construct the statue.
    shell_shard_donation_pouch_requirements[
        'Shell Shard Cache: Bone Bottom'
    ] = shell_shard_donation_pouch_requirements.get(
        'Wish: An Icon of Hope',
        0,
    )
    act_two_exclusion_getter = getattr(
        world,
        "get_act_two_excluded_location_names",
        None,
    )
    excluded_location_names = (
        act_two_exclusion_getter()
        if callable(act_two_exclusion_getter)
        else frozenset()
    )
    world._silksong_rule_builder_rules = {}

    for location_name, location_data in location_data_table.items():
        if location_name == "Goal":
            continue
        if location_name in PROTOCOL_ONLY_LOCATION_NAMES:
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
            and callable(location_mode_getter := getattr(
                world,
                "get_category_mode",
                None,
            ))
            and location_mode_getter('MemoryLocket') != 'vanilla'
        ):
            continue
        if (
            location_name == 'Wish: Bugs of Pharloom'
            and callable(location_mode_getter := getattr(
                world,
                "get_category_mode",
                None,
            ))
            and location_mode_getter('ToolPouch') != 'vanilla'
        ):
            continue

        location_mode_getter = getattr(
            world,
            "get_location_randomization_mode",
            None,
        )
        mode = (
            location_mode_getter(
                location_name,
                location_data.category,
            )
            if (
                callable(location_mode_getter)
                and location_data.category
                in CATEGORY_OPTION_BY_LOCATION_CATEGORY
            )
            else (
                get_category_mode_key(
                    world.options,
                    location_data.category,
                )
                if location_data.category
                in CATEGORY_OPTION_BY_LOCATION_CATEGORY
                else "anywhere"
            )
        )
        if (
            location_data.category in OBSERVATION_LOCATION_CATEGORIES
            and mode == "vanilla"
        ):
            continue

        location = world.multiworld.get_location(location_name, world.player)
        if mode == "vanilla":
            location_rule = build_native_source_rule(
                location_name,
                location_data.category,
                split_dash_and_sprint=split_dash_and_sprint,
                allow_bellways_before_bell_beast=(
                    allow_bellways_before_bell_beast
                ),
                easy_skips_enabled=easy_skips_enabled,
                randomized_crest_slots_enabled=(
                    randomized_crest_slots_enabled
                ),
                starting_location=starting_location,
            )
        else:
            location_rule = build_location_rule(
                location_name,
                split_dash_and_sprint=split_dash_and_sprint,
                allow_bellways_before_bell_beast=(
                    allow_bellways_before_bell_beast
                ),
                easy_skips_enabled=easy_skips_enabled,
                randomized_crest_slots_enabled=(
                    randomized_crest_slots_enabled
                ),
                starting_location=starting_location,
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
            pollip_heart_mode != 'vanilla'
            and location_name == POLLIP_POUCH_LOCATION
        ):
            # Shell Flowers consumes exactly six Shell Flowers before its
            # Pollip Pouch reward. In randomized modes those six collectibles
            # are the repeated AP Pollip Heart item.
            location_rule = location_rule & Has(
                POLLIP_HEART_ITEM,
                POLLIP_HEART_COUNT,
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

        if logic_audit_mode:
            # In mixed rooms, logic audit checks reject foreign advancement
            # and useful items, including foreign filler that could strand
            # this player's replacement pool elsewhere.
            add_item_rule(location, logic_audit_item_rule)

        # Vanilla logic events and the fixed shuffle exceptions
        # already have their sole legal reward.
        if getattr(location, "item", None) is not None:
            continue

        if mode == "shuffle":
            placement_category_getter = getattr(
                world,
                "get_location_shuffle_placement_category",
                None,
            )
            placement_category = (
                placement_category_getter(
                    location_name,
                    location_data.category,
                )
                if callable(placement_category_getter)
                else (
                    get_minor_family_shuffle_category_for_location(
                        location_name
                    )
                    if location_data.category == "Resource"
                    else location_data.category
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

        if location_name in JUNK_ONLY_LOCATIONS:
            add_item_rule(location, _is_junk_item)
        # When Memory Lockets are randomized, every Crest Slot shares a
        # fill-aware Locket gate equal to the number of progression rewards
        # placed across the slots plus a two-Locket safety buffer. Lockets
        # themselves stay out of the slots.
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

    goal_option = getattr(world.options, "goal", None)
    goal_key = (
        "act_3"
        if goal_option is None
        else goal_option.current_key
    )
    flea_hunt_count_getter = getattr(
        world,
        "get_flea_hunt_goal_count",
        None,
    )
    flea_hunt_count = (
        flea_hunt_count_getter()
        if callable(flea_hunt_count_getter)
        else DEFAULT_FLEA_HUNT_GOAL_COUNT
    )
    goal = world.multiworld.get_location("Goal", world.player)
    goal_rule = build_goal_rule(
        goal_key,
        split_dash_and_sprint=split_dash_and_sprint,
        flea_hunt_count=flea_hunt_count,
        allow_bellways_before_bell_beast=(
            allow_bellways_before_bell_beast
        ),
        easy_skips_enabled=easy_skips_enabled,
        randomized_crest_slots_enabled=(
            randomized_crest_slots_enabled
        ),
        starting_location=starting_location,
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
