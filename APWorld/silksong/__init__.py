from __future__ import annotations

import random

from BaseClasses import (
    CollectionState,
    Item,
    ItemClassification,
    Region,
    Tutorial,
)
from worlds.AutoWorld import WebWorld, World

from .act2_scope import (
    ACT_TWO_GOAL_KEY,
    get_act_two_excluded_location_names,
)
from .category_fill import prefill_category_shuffles
from .items import (
    ACT_TWO_START_WITH_MAP_ITEMS,
    AUTOMATIC_COMPASS_ITEM,
    OPTIONAL_START_REPLACEMENT_ITEM,
    PROGRESSIVE_SWIFT_STEP_ITEM,
    START_WITH_MAP_ITEMS,
    STARTING_CREST_ITEM_BY_KEY,
    SHUFFLE_FIXED_LOCATION_REWARDS,
    TRAP_ITEM_NAME_BY_WEIGHT_OPTION,
    TRAP_ITEM_NAMES,
    SilksongItem,
    build_item_pool_entries,
    get_configured_item_pool_size,
    get_dynamic_trap_capacity,
    get_act_two_skill_balance_precollected_item_name,
    get_logic_audit_precollected_item_names,
    get_vanilla_reward_name,
    item_data_table,
    item_name_groups,
    item_table,
    replace_logic_audit_pool_with_filler,
)
from .locations import (
    INDIVIDUAL_RELIC_TURN_IN_ITEM_NAMES,
    LOCATION_NAMES_BY_CATEGORY,
    NEEDLE_UPGRADE_LOCATION_CATEGORY,
    OBSERVATION_LOCATION_CATEGORIES,
    PAIRED_LOCATION_CATEGORIES,
    PINMASTER_OIL_QUEST_LOCATION,
    PROTOCOL_ONLY_LOCATION_NAMES,
    RELIC_TURN_IN_LOCATION_CATEGORY,
    SCROUNGE_RELIC_ITEM_NAMES,
    VOLATILE_FLINTBEETLES_QUEST_LOCATION,
    SilksongLocation,
    location_data_table,
    location_name_groups,
    location_table,
)
from .minor_families import (
    MINOR_FAMILY_BY_LOCATION,
    MINOR_FAMILY_OPTION_BY_KEY,
    get_minor_family_shuffle_category,
)
from .options import (
    CATEGORY_OPTION_BY_LOCATION_CATEGORY,
    SilksongOptions,
    get_aggregate_minor_mode_key,
    get_category_mode_key,
    get_minor_family_modes,
    get_minor_family_shuffle_placement_category,
)
from .prices import (
    PRICE_CATEGORY_OPTION_NAMES,
    PRICE_MODE_KEYS,
    get_shell_shard_donation_tool_pouch_requirements,
    resolve_purchase_prices,
)
from .requirements import (
    DEFAULT_FLEA_HUNT_GOAL_COUNT,
    FLEA_HUNT_GOAL_KEY,
    JUNK_ONLY_LOCATIONS,
    MAX_FLEA_HUNT_GOAL_COUNT,
    MEMORY_LOCKET_ITEM,
    MIN_FLEA_HUNT_GOAL_COUNT,
    OPTION_DEPENDENT_CREST_SLOT_ITEM_NAMES,
    POLLIP_HEART_COUNT,
    ROSARY_BANK_GATED_LOCATIONS,
    SIMPLE_KEY_ROSARY_BANK,
    UNVERIFIED_PROGRESSION_LOCATIONS,
    export_abstract_requirements,
    export_logic_item_dependencies,
    export_requirements,
)
from .rules import (
    enforce_global_shuffle_item_rules,
    finalize_crest_slot_memory_locket_logic,
    get_active_crest_slot_locations,
    get_crest_slot_memory_locket_count,
    get_crest_slot_progression_item_count,
    set_silksong_rules,
    uses_randomized_memory_lockets_for_crest_slots,
)
from .version import WORLD_VERSION

__version__ = WORLD_VERSION

class SilksongWebWorld(WebWorld):
    theme = "ocean"
    tutorials = [
        Tutorial(
            tutorial_name="Multiworld Setup Guide",
            description="How to generate and connect a Hollow Knight: Silksong Archipelago seed.",
            language="English",
            file_name="setup_en.md",
            link="setup/en",
            authors=["BatAtVideoGames"],
        )
    ]


class SilksongWorld(World):
    """Hollow Knight: Silksong item randomizer support for the uploaded BepInEx client."""

    game = "Hollow Knight: Silksong"
    author = "BatAtVideoGames"
    web = SilksongWebWorld()
    options_dataclass = SilksongOptions
    options: SilksongOptions

    item_name_to_id = item_table
    location_name_to_id = location_table
    item_name_groups = item_name_groups
    location_name_groups = location_name_groups

    required_client_version = (0, 6, 0)
    _resolved_starting_crest: str | None = None
    _resolved_trap_counts: dict[str, int] | None = None
    _crest_slot_memory_locket_count: int | None = None

    @classmethod
    def rule_from_dict(cls, data):
        # Custom RuleBuilder classes register when their module is imported.
        # Do that here as well as during generation so a fresh tracker or
        # diagnostic process can deserialize an exported rule immediately.
        from . import requirement_rules as _requirement_rules

        del _requirement_rules
        return super().rule_from_dict(data)

    def resolve_starting_crest(self) -> str:
        if self._resolved_starting_crest is not None:
            return self._resolved_starting_crest

        configured_crest = (
            'hunter'
            if self.get_category_mode('Crest') == 'vanilla'
            else self.options.starting_crest.current_key
        )
        if configured_crest not in STARTING_CREST_ITEM_BY_KEY:
            raise ValueError(
                f"Unknown Silksong starting crest option: "
                f"{configured_crest!r}"
            )

        self._resolved_starting_crest = configured_crest
        return configured_crest

    def get_starting_location_key(self) -> str:
        option = getattr(self.options, "starting_location", None)
        key = "vanilla" if option is None else option.current_key
        if key not in {"vanilla", "bone_bottom"}:
            raise ValueError(
                f"Unknown Silksong starting location: {key!r}"
            )
        return key

    def get_goal_key(self) -> str:
        option = getattr(self.options, "goal", None)
        return "act_3" if option is None else option.current_key

    def is_act_two_content_scope(self) -> bool:
        return self.get_goal_key() == ACT_TWO_GOAL_KEY

    def get_start_with_map_item_names(self) -> tuple[str, ...]:
        if not self.is_start_with_maps_enabled():
            return ()
        return (
            ACT_TWO_START_WITH_MAP_ITEMS
            if self.is_act_two_content_scope()
            else START_WITH_MAP_ITEMS
        )

    def get_act_two_excluded_location_names(self) -> frozenset[str]:
        if not self.is_act_two_content_scope():
            return frozenset()
        return get_act_two_excluded_location_names(
            STARTING_CREST_ITEM_BY_KEY[self.resolve_starting_crest()],
            self.get_category_mode('Skill'),
        )

    def generate_early(self) -> None:
        self._crest_slot_memory_locket_count = None
        starting_crest = self.resolve_starting_crest()
        category_modes = self.get_category_modes()
        goal_key = self.get_goal_key()
        if self.is_logic_audit_mode_enabled():
            permitted_non_anywhere_categories = (
                {'CrestSlot'}
                if category_modes.get('MemoryLocket') == 'anywhere'
                else set()
            )
            non_anywhere_categories = sorted(
                category
                for category, mode in category_modes.items()
                if (
                    mode != "anywhere"
                    and category not in permitted_non_anywhere_categories
                )
            )
            if non_anywhere_categories:
                raise ValueError(
                    "logic_audit_mode requires every randomization category "
                    "to use anywhere; received non-anywhere categories: "
                    f"{non_anywhere_categories!r}."
                )
            if self.is_early_dash_enabled():
                raise ValueError(
                    "logic_audit_mode requires early_dash: false because "
                    "Swift Step is already precollected."
                )
        get_configured_item_pool_size(
            STARTING_CREST_ITEM_BY_KEY[starting_crest],
            self.resolve_trap_counts(),
            self.is_split_dash_and_sprint(),
            category_modes,
            self.is_needle_upgrade_randomization_enabled(),
            self.is_start_with_maps_enabled(),
            self.is_automatic_compass_enabled(),
            self.get_minor_family_shuffle_categories(),
            individual_relic_turn_ins=(
                self.is_individual_relic_turn_ins_enabled()
            ),
            act_two_only=self.is_act_two_content_scope(),
        )
        if self.is_early_dash_enabled():
            player_early_items = self.multiworld.local_early_items[
                self.player
            ]
            early_item_name = (
                PROGRESSIVE_SWIFT_STEP_ITEM
                if self.is_split_dash_and_sprint()
                else "Swift Step"
            )
            early_item_count = 2 if self.is_split_dash_and_sprint() else 1
            player_early_items[early_item_name] = (
                player_early_items.get(early_item_name, 0)
                + early_item_count
            )

    def is_split_dash_and_sprint(self) -> bool:
        return (
            bool(self.options.split_dash_and_sprint.value)
            and self.get_category_mode('Skill') == 'anywhere'
        )

    def is_early_dash_enabled(self) -> bool:
        return (
            bool(self.options.early_dash.value)
            and self.get_category_mode('Skill') != 'vanilla'
        )

    def is_vanilla_flea_hunt_check(self, location_name: str) -> bool:
        return (
            self.get_goal_key() == FLEA_HUNT_GOAL_KEY
            and self.get_category_mode('Flea') == 'vanilla'
            and location_name in LOCATION_NAMES_BY_CATEGORY['Flea']
        )

    def is_logic_audit_mode_enabled(self) -> bool:
        return bool(
            getattr(
                getattr(self.options, "logic_audit_mode", None),
                "value",
                0,
            )
        )

    def is_individual_relic_turn_ins_enabled(self) -> bool:
        return bool(
            getattr(
                getattr(
                    self.options,
                    "individual_relic_turn_ins",
                    None,
                ),
                "value",
                0,
            )
        )

    def is_easy_skips_enabled(self) -> bool:
        return bool(
            getattr(
                getattr(self.options, "easy_skips", None),
                "value",
                0,
            )
        )

    def get_flea_hunt_goal_count(self) -> int:
        option = getattr(self.options, "flea_hunt_count", None)
        count = (
            DEFAULT_FLEA_HUNT_GOAL_COUNT
            if option is None
            else int(option.value)
        )
        if not (
            MIN_FLEA_HUNT_GOAL_COUNT
            <= count
            <= MAX_FLEA_HUNT_GOAL_COUNT
        ):
            raise ValueError(
                "flea_hunt_count must be between "
                f"{MIN_FLEA_HUNT_GOAL_COUNT} and "
                f"{MAX_FLEA_HUNT_GOAL_COUNT}; received {count!r}."
            )
        return count

    def is_needle_upgrade_randomization_enabled(self) -> bool:
        return bool(
            getattr(
                getattr(
                    self.options,
                    "randomize_needle_upgrades",
                    None,
                ),
                "value",
                0,
            )
        )

    def is_start_with_maps_enabled(self) -> bool:
        return bool(
            getattr(
                getattr(self.options, "start_with_maps", None),
                "value",
                0,
            )
        )

    def is_automatic_compass_enabled(self) -> bool:
        return bool(
            getattr(
                getattr(self.options, "automatic_compass", None),
                "value",
                0,
            )
        )

    def get_bellway_access_key(self) -> str:
        option = getattr(self.options, "bellway_access", None)
        key = (
            "bell_beast_required"
            if option is None
            else option.current_key
        )
        if key not in {
            "bell_beast_required",
            "randomized_stations",
        }:
            raise ValueError(
                f"Unknown Bellway access mode: {key!r}"
            )
        return key

    def allows_bellways_before_bell_beast(self) -> bool:
        return self.get_bellway_access_key() == "randomized_stations"

    def get_check_map_markers_key(self) -> str:
        option = getattr(self.options, "check_map_markers", None)
        key = "off" if option is None else option.current_key
        if key not in {"off", "mapped_rooms", "owned_maps", "all"}:
            raise ValueError(
                f"Unknown check map marker mode: {key!r}"
            )
        return key

    def get_enemy_rosary_multiplier_key(self) -> str:
        option = getattr(self.options, "enemy_rosary_multiplier", None)
        key = "x1" if option is None else option.current_key
        if key not in {"x1", "x1_5", "x2", "x3"}:
            raise ValueError(
                f"Unknown enemy Rosary multiplier: {key!r}"
            )
        return key

    def get_enemy_shard_multiplier_key(self) -> str:
        option = getattr(self.options, "enemy_shard_multiplier", None)
        key = "x1" if option is None else option.current_key
        if key not in {"x1", "x1_5", "x2", "x3"}:
            raise ValueError(
                f"Unknown enemy Shell Shard multiplier: {key!r}"
            )
        return key

    def get_purchase_price_modes(self) -> dict[str, str]:
        modes: dict[str, str] = {}
        for category, option_name in PRICE_CATEGORY_OPTION_NAMES.items():
            option = getattr(self.options, option_name, None)
            key = "vanilla" if option is None else option.current_key
            if key not in PRICE_MODE_KEYS:
                raise ValueError(
                    f"Unknown {option_name} mode: {key!r}"
                )
            modes[category] = key
        return modes

    def get_purchase_prices(self) -> dict[str, int]:
        cached = getattr(self, "_resolved_purchase_prices", None)
        if cached is None:
            cached = resolve_purchase_prices(
                getattr(self, "random", None) or random.Random(0),
                self.get_purchase_price_modes(),
            )
            self._resolved_purchase_prices = cached
        return dict(cached)

    def is_faster_dialogue_enabled(self) -> bool:
        return bool(
            getattr(
                getattr(self.options, "faster_dialogue", None),
                "value",
                0,
            )
        )

    def is_death_link_enabled(self) -> bool:
        return bool(
            getattr(
                getattr(self.options, "death_link", None),
                "value",
                0,
            )
        )

    def is_silk_link_enabled(self) -> bool:
        return bool(
            getattr(
                getattr(self.options, "silk_link", None),
                "value",
                0,
            )
        )

    def is_rosary_link_enabled(self) -> bool:
        return bool(
            getattr(
                getattr(self.options, "rosary_link", None),
                "value",
                0,
            )
        )

    def is_shell_shard_link_enabled(self) -> bool:
        return bool(
            getattr(
                getattr(self.options, "shell_shard_link", None),
                "value",
                0,
            )
        )

    def is_quest_sanity_enabled(self) -> bool:
        return self.get_category_mode('Quest') != 'vanilla'

    def get_category_mode(self, category: str) -> str:
        return get_category_mode_key(self.options, category)

    def get_minor_family_modes(self) -> dict[str, str]:
        return get_minor_family_modes(self.options)

    def get_location_randomization_mode(
        self,
        location_name: str,
        category: str,
    ) -> str:
        if category != "Resource":
            return self.get_category_mode(category)
        try:
            family_key = MINOR_FAMILY_BY_LOCATION[location_name]
        except KeyError as exc:
            raise ValueError(
                f"No minor pickup family for {location_name!r}."
            ) from exc
        return self.get_minor_family_modes()[family_key]

    def get_minor_family_shuffle_categories(self) -> dict[str, str]:
        return {
            family_key: get_minor_family_shuffle_placement_category(
                self.options,
                family_key,
            )
            for family_key in self.get_minor_family_modes()
        }

    def get_location_shuffle_placement_category(
        self,
        location_name: str,
        category: str,
    ) -> str:
        if category != "Resource":
            return category
        family_key = MINOR_FAMILY_BY_LOCATION[location_name]
        return self.get_minor_family_shuffle_categories()[family_key]

    def get_category_modes(self) -> dict[str, str]:
        modes = {
            category: self.get_category_mode(category)
            for category in CATEGORY_OPTION_BY_LOCATION_CATEGORY
        }
        modes["Resource"] = get_aggregate_minor_mode_key(self.options)
        modes.update(
            {
                get_minor_family_shuffle_category(family_key): mode
                for family_key, mode in
                self.get_minor_family_modes().items()
            }
        )
        return modes

    def resolve_trap_counts(self) -> dict[str, int]:
        if self._resolved_trap_counts is not None:
            return dict(self._resolved_trap_counts)

        percentage = int(self.options.trap_percentage.value)
        trap_capacity = get_dynamic_trap_capacity(
            self.get_category_modes(),
            self.is_split_dash_and_sprint(),
            STARTING_CREST_ITEM_BY_KEY[self.resolve_starting_crest()],
            self.is_needle_upgrade_randomization_enabled(),
            self.is_start_with_maps_enabled(),
            self.is_automatic_compass_enabled(),
            self.get_minor_family_shuffle_categories(),
            self.is_individual_relic_turn_ins_enabled(),
            self.is_act_two_content_scope(),
        )
        if percentage > 0 and trap_capacity == 0:
            raise ValueError(
                "trap_percentage is above zero, but the configured random "
                "pool contains no trap-replaceable filler."
            )
        total_traps = (trap_capacity * percentage + 50) // 100
        weights = {
            item_name: int(getattr(self.options, option_name).value)
            for option_name, item_name
            in TRAP_ITEM_NAME_BY_WEIGHT_OPTION.items()
        }
        total_weight = sum(weights.values())
        if total_traps > 0 and total_weight == 0:
            raise ValueError(
                "trap_percentage is above zero, but every trap weight is "
                "zero. Give at least one enabled trap a positive weight."
            )

        counts = {item_name: 0 for item_name in TRAP_ITEM_NAMES}
        if total_traps > 0:
            remainders: dict[int, list[str]] = {}
            assigned = 0
            for item_name, weight in weights.items():
                count, remainder = divmod(
                    total_traps * weight,
                    total_weight,
                )
                counts[item_name] = count
                assigned += count
                if weight > 0:
                    remainders.setdefault(remainder, []).append(item_name)

            remaining = total_traps - assigned
            for remainder in sorted(remainders, reverse=True):
                tied_names = remainders[remainder]
                if len(tied_names) > 1:
                    self.random.shuffle(tied_names)
                for item_name in tied_names:
                    if remaining == 0:
                        break
                    counts[item_name] += 1
                    remaining -= 1
                if remaining == 0:
                    break

        self._resolved_trap_counts = counts
        return dict(counts)

    def create_item(
        self,
        name: str,
        placement_category: str | None = None,
    ) -> Item:
        data = item_data_table[name]
        classification = data.classification
        if (
            name == SIMPLE_KEY_ROSARY_BANK
            and not self.is_rosary_bank_key_progression_enabled()
        ):
            classification = ItemClassification.useful
        if name in OPTION_DEPENDENT_CREST_SLOT_ITEM_NAMES:
            crest_slot_mode = self.get_category_mode('CrestSlot')
            memory_locket_mode = self.get_category_mode('MemoryLocket')
            if (
                crest_slot_mode == 'vanilla'
                or (
                    crest_slot_mode == 'shuffle'
                    and memory_locket_mode == 'vanilla'
                )
            ):
                # Received slots are real logic items except when their lane
                # can only place them behind physical Crest Slot checks whose
                # vanilla Locket spending AP cannot model.
                classification = ItemClassification.useful
        if (
            (
                name in SCROUNGE_RELIC_ITEM_NAMES
                and self.get_category_mode('Relic') != 'vanilla'
            )
            or (
                name in INDIVIDUAL_RELIC_TURN_IN_ITEM_NAMES
                and self.is_individual_relic_turn_ins_enabled()
            )
        ):
            classification = ItemClassification.progression
        if (
            name == MEMORY_LOCKET_ITEM
            and uses_randomized_memory_lockets_for_crest_slots(self)
        ):
            # These consumable logic keys must stay where restrictive fill
            # proved them reachable. Core progression balancing otherwise
            # has permission to move progression after the shared Crest Slot
            # requirement has been frozen.
            classification |= ItemClassification.skip_balancing
        return SilksongItem(
            name,
            classification,
            data.code,
            self.player,
            placement_category,
        )

    def is_rosary_bank_key_progression_enabled(self) -> bool:
        return any(
            self.get_location_randomization_mode(
                location_name,
                location_data_table[location_name].category,
            ) != 'vanilla'
            for location_name in ROSARY_BANK_GATED_LOCATIONS
        )

    def create_event(self, name: str) -> Item:
        return SilksongItem(name, ItemClassification.progression, self.item_name_to_id[name], self.player)

    def create_items(self) -> None:
        starting_crest = self.resolve_starting_crest()
        starting_crest_item = STARTING_CREST_ITEM_BY_KEY[starting_crest]
        category_modes = self.get_category_modes()
        precollected_option_items = {
            *self.get_start_with_map_item_names(),
        }
        option_replaced_item_names = {
            *precollected_option_items,
            *(
                (AUTOMATIC_COMPASS_ITEM,)
                if self.is_automatic_compass_enabled()
                else ()
            ),
        }

        if category_modes['Crest'] != 'vanilla':
            self.multiworld.push_precollected(
                self.create_item(starting_crest_item)
            )

        for category in PAIRED_LOCATION_CATEGORIES:
            for location_name in LOCATION_NAMES_BY_CATEGORY[category]:
                if (
                    location_name
                    in self.get_act_two_excluded_location_names()
                ):
                    continue
                mode = self.get_location_randomization_mode(
                    location_name,
                    category,
                )
                if mode == 'vanilla':
                    reward_name = get_vanilla_reward_name(
                        location_name,
                        category,
                    )
                    self.multiworld.get_location(
                        location_name,
                        self.player,
                    ).place_locked_item(
                        self.create_item(
                            OPTIONAL_START_REPLACEMENT_ITEM
                            if reward_name in option_replaced_item_names
                            else reward_name
                        )
                    )
            if (
                category != 'Resource'
                and category_modes[category] == 'shuffle'
            ):
                for location_name, reward_name in (
                    SHUFFLE_FIXED_LOCATION_REWARDS
                    .get(category, {})
                    .items()
                ):
                    if (
                        location_name
                        in self.get_act_two_excluded_location_names()
                    ):
                        continue
                    self.multiworld.get_location(
                        location_name,
                        self.player,
                    ).place_locked_item(
                        self.create_item(
                            OPTIONAL_START_REPLACEMENT_ITEM
                            if reward_name in option_replaced_item_names
                            else reward_name
                        )
                    )

        for item_name in sorted(precollected_option_items):
            self.multiworld.push_precollected(self.create_item(item_name))

        pool_entries = list(build_item_pool_entries(
            starting_crest_item,
            self.resolve_trap_counts(),
            self.is_split_dash_and_sprint(),
            category_modes,
            self.is_needle_upgrade_randomization_enabled(),
            self.is_start_with_maps_enabled(),
            self.is_automatic_compass_enabled(),
            self.get_minor_family_shuffle_categories(),
            getattr(self, 'random', None),
            self.is_individual_relic_turn_ins_enabled(),
            self.is_act_two_content_scope(),
        ))

        if self.is_act_two_content_scope():
            balance_item_name = (
                get_act_two_skill_balance_precollected_item_name(
                    pool_entries,
                    category_modes,
                    self.is_automatic_compass_enabled(),
                )
            )
            if balance_item_name is not None:
                self.multiworld.push_precollected(
                    self.create_item(balance_item_name)
                )

        # `anywhere` normally has ample filler/useful rewards for quarantined
        # sources. Very small configurations such as Map-only or Melody-only
        # do not. In that specific shortage, retain the same conservative
        # native rewards used by category shuffle instead of making a valid
        # option combination impossible to fill.
        if not self.is_logic_audit_mode_enabled():
            get_locations = getattr(
                self.multiworld,
                'get_locations',
                None,
            )
            unfilled_locations = (
                {
                    location.name: location
                    for location in get_locations()
                    if (
                        location.player == self.player
                        and location.address is not None
                        and location.item is None
                        and getattr(
                            location,
                            'silksong_placement_category',
                            None,
                        ) is None
                    )
                }
                if callable(get_locations)
                else {}
            )
            junk_only_names = {
                location_name
                for location_name in unfilled_locations
                if location_name in JUNK_ONLY_LOCATIONS
            }
            nonprogression_only_names = junk_only_names | {
                location_name
                for location_name in unfilled_locations
                if (
                    location_name in UNVERIFIED_PROGRESSION_LOCATIONS
                    or (
                        location_data_table[location_name].category
                        == 'CrestSlot'
                        and category_modes.get('MemoryLocket') == 'vanilla'
                    )
                )
            }
            classifications = tuple(
                self.create_item(entry.name).classification
                for entry in pool_entries
                if entry.placement_category is None
            )
            junk_item_count = sum(
                classification in (
                    ItemClassification.filler,
                    ItemClassification.trap,
                )
                for classification in classifications
            )
            nonprogression_item_count = sum(
                not (
                    classification
                    & ItemClassification.progression
                )
                for classification in classifications
            )
            safety_shortage = (
                junk_item_count < len(junk_only_names)
                or nonprogression_item_count
                < len(nonprogression_only_names)
            )
            if safety_shortage:
                for category, fixed_rewards in (
                    SHUFFLE_FIXED_LOCATION_REWARDS.items()
                ):
                    if category_modes.get(category) != 'anywhere':
                        continue
                    for location_name, reward_name in fixed_rewards.items():
                        location = unfilled_locations.get(location_name)
                        if location is None:
                            continue
                        pool_reward = (
                            OPTIONAL_START_REPLACEMENT_ITEM
                            if reward_name in option_replaced_item_names
                            else reward_name
                        )
                        pool_index = next(
                            (
                                index
                                for index, entry in enumerate(pool_entries)
                                if (
                                    entry.name == pool_reward
                                    and entry.source_category == category
                                )
                            ),
                            None,
                        )
                        if pool_index is None:
                            raise ValueError(
                                f"Could not reserve fixed {category} reward "
                                f"{pool_reward!r} for {location_name!r}."
                            )
                        pool_entries.pop(pool_index)
                        location.place_locked_item(
                            self.create_item(pool_reward)
                        )

        pool_entries = tuple(pool_entries)
        if self.is_logic_audit_mode_enabled():
            for item_name in get_logic_audit_precollected_item_names(
                pool_entries
            ):
                self.multiworld.push_precollected(
                    self.create_item(item_name)
                )
            pool_entries = replace_logic_audit_pool_with_filler(
                pool_entries
            )

        self.multiworld.itempool.extend(
            self.create_item(entry.name, entry.placement_category)
            for entry in pool_entries
        )

    def create_regions(self) -> None:
        menu = Region("Menu", self.player, self.multiworld)
        pharloom = Region("Pharloom", self.player, self.multiworld)
        randomize_needle_upgrades = (
            self.is_needle_upgrade_randomization_enabled()
        )

        fixed_shuffle_locations = {
            location_name
            for locations in SHUFFLE_FIXED_LOCATION_REWARDS.values()
            for location_name in locations
        }
        for name, data in location_data_table.items():
            if name in PROTOCOL_ONLY_LOCATION_NAMES:
                continue
            if (
                name in self.get_act_two_excluded_location_names()
            ):
                continue
            if (
                data.category == RELIC_TURN_IN_LOCATION_CATEGORY
                and not self.is_individual_relic_turn_ins_enabled()
            ):
                continue
            if (
                data.category == NEEDLE_UPGRADE_LOCATION_CATEGORY
                and not randomize_needle_upgrades
            ):
                continue
            if (
                name == PINMASTER_OIL_QUEST_LOCATION
                and randomize_needle_upgrades
            ):
                continue
            if (
                name == VOLATILE_FLINTBEETLES_QUEST_LOCATION
                and self.get_category_mode('MemoryLocket') != 'vanilla'
            ):
                continue
            if (
                name == 'Wish: Bugs of Pharloom'
                and self.get_category_mode('ToolPouch') != 'vanilla'
            ):
                continue
            mode = (
                self.get_location_randomization_mode(
                    name,
                    data.category,
                )
                if (
                    data.category == "Resource"
                    or data.category in CATEGORY_OPTION_BY_LOCATION_CATEGORY
                )
                else 'anywhere'
            )
            if (
                data.category in OBSERVATION_LOCATION_CATEGORIES
                and mode == 'vanilla'
            ):
                continue

            address = (
                None
                if (
                    data.category in PAIRED_LOCATION_CATEGORIES
                    and mode == 'vanilla'
                    and not self.is_vanilla_flea_hunt_check(name)
                    and not (
                        self.is_start_with_maps_enabled()
                        and data.category == 'Map'
                    )
                    and not (
                        self.is_automatic_compass_enabled()
                        and data.category == 'Tool'
                        and get_vanilla_reward_name(
                            name,
                            data.category,
                        ) == AUTOMATIC_COMPASS_ITEM
                    )
                )
                else data.code
            )
            location = SilksongLocation(
                self.player,
                name,
                address,
                pharloom,
            )
            location.silksong_randomization_category = data.category
            if (
                mode == 'shuffle'
                and name not in fixed_shuffle_locations
            ):
                location.silksong_placement_category = (
                    self.get_location_shuffle_placement_category(
                        name,
                        data.category,
                    )
                )
            pharloom.locations.append(location)

        self.multiworld.regions += [menu, pharloom]
        menu.connect(pharloom)

    def set_rules(self) -> None:
        set_silksong_rules(self)

    @classmethod
    def stage_pre_fill(cls, multiworld) -> None:
        prefill_category_shuffles(multiworld, cls.game)

    @classmethod
    def stage_set_rules(cls, multiworld) -> None:
        enforce_global_shuffle_item_rules(multiworld)

    @classmethod
    def stage_fill_hook(
        cls,
        multiworld,
        progitempool,
        _usefulitempool,
        _filleritempool,
        _fill_locations,
    ) -> None:
        combined_players = {
            player
            for player in multiworld.player_ids
            if (
                multiworld.worlds[player].game == cls.game
                and uses_randomized_memory_lockets_for_crest_slots(
                    multiworld.worlds[player]
                )
            )
        }
        if not combined_players:
            return

        delayed_lockets = [
            item
            for item in progitempool
            if (
                item.player in combined_players
                and item.name == MEMORY_LOCKET_ITEM
            )
        ]
        if not delayed_lockets:
            return
        delayed_ids = {id(item) for item in delayed_lockets}
        progitempool[:] = [
            *delayed_lockets,
            *(
                item
                for item in progitempool
                if id(item) not in delayed_ids
            ),
        ]

    @staticmethod
    def _build_crest_slotless_state(multiworld, excluded_slots):
        state = CollectionState(multiworld)
        state.sweep_for_advancements(
            locations=(
                location
                for location in multiworld.get_filled_locations()
                if location not in excluded_slots
            )
        )
        return state

    @classmethod
    def _repair_crest_slot_locket_budget(
        cls,
        multiworld,
        world,
        excluded_slots,
        slotless_state,
    ):
        while True:
            required_count = get_crest_slot_memory_locket_count(world)
            reachable_count = slotless_state.count(
                MEMORY_LOCKET_ITEM,
                world.player,
            )
            if reachable_count >= required_count:
                return slotless_state

            progression_slots = sorted(
                get_active_crest_slot_locations(world),
                key=lambda location: location.name,
            )
            progression_slots = [
                location
                for location in progression_slots
                if (
                    location.item is not None
                    and location.item.advancement
                    and not location.locked
                )
            ]
            replacement_locations = sorted(
                (
                    location
                    for location in multiworld.get_filled_locations()
                    if (
                        location.player == world.player
                        and location not in excluded_slots
                        and location.address is not None
                        and location.item is not None
                        and not location.item.advancement
                        and not location.locked
                        and location.can_reach(slotless_state)
                    )
                ),
                key=lambda location: location.name,
            )

            swap_pair = next(
                (
                    (slot_location, replacement_location)
                    for slot_location in progression_slots
                    for replacement_location in replacement_locations
                    if (
                        replacement_location.can_fill(
                            slotless_state,
                            slot_location.item,
                            False,
                        )
                        and slot_location.can_fill(
                            slotless_state,
                            replacement_location.item,
                            False,
                        )
                    )
                ),
                None,
            )
            if swap_pair is None:
                return slotless_state

            from Fill import swap_location_item

            swap_location_item(*swap_pair)
            slotless_state = cls._build_crest_slotless_state(
                multiworld,
                excluded_slots,
            )

    @classmethod
    def stage_post_fill(cls, multiworld) -> None:
        combined_worlds = [
            multiworld.worlds[player]
            for player in multiworld.player_ids
            if (
                multiworld.worlds[player].game == cls.game
                and uses_randomized_memory_lockets_for_crest_slots(
                    multiworld.worlds[player]
                )
            )
        ]
        if not combined_worlds:
            return

        excluded_slots = set()
        for world in combined_worlds:
            excluded_slots.update(get_active_crest_slot_locations(world))

        # First move only as much progression out of Crest Slots as needed
        # for the available independent Lockets plus the two-Locket buffer.
        slotless_state = cls._build_crest_slotless_state(
            multiworld,
            excluded_slots,
        )
        for world in combined_worlds:
            slotless_state = cls._repair_crest_slot_locket_budget(
                multiworld,
                world,
                excluded_slots,
                slotless_state,
            )

        # Freeze the resulting count before core progression balancing. The
        # slot placements are locked so that balancing cannot change it.
        for world in combined_worlds:
            finalize_crest_slot_memory_locket_logic(world)

        # Prove every required Locket can be reached without assuming any
        # reward from the consumable-gated Crest Slot checks themselves.
        slotless_state = cls._build_crest_slotless_state(
            multiworld,
            excluded_slots,
        )
        for world in combined_worlds:
            progression_count = get_crest_slot_progression_item_count(
                world
            )
            required_count = get_crest_slot_memory_locket_count(world)
            reachable_count = slotless_state.count(
                MEMORY_LOCKET_ITEM,
                world.player,
            )
            if reachable_count < required_count:
                from Fill import FillError

                raise FillError(
                    f"{multiworld.player_name[world.player]}'s Crest Slot "
                    f"checks contain {progression_count} progression "
                    "rewards, but only "
                    f"{reachable_count} of the required {required_count} "
                    "Memory Lockets are reachable without Crest Slot "
                    "rewards. Check forced placements, exclusions, and "
                    "category settings.",
                    multiworld=multiworld,
                )

    def fill_slot_data(self) -> dict[str, object]:
        goal_key = self.get_goal_key()
        flea_hunt_count = self.get_flea_hunt_goal_count()
        category_modes = self.get_category_modes()
        minor_family_modes = self.get_minor_family_modes()
        purchase_price_modes = self.get_purchase_price_modes()
        purchase_prices = self.get_purchase_prices()
        bone_bottom_statue_tool_pouch_count = (
            get_shell_shard_donation_tool_pouch_requirements(
                purchase_prices
            )['Wish: An Icon of Hope']
        )
        slot_data = {
            "world_version": WORLD_VERSION,
            "item_name_to_id": dict(self.item_name_to_id),
            "location_name_to_id": dict(self.location_name_to_id),
            "goal": goal_key,
            "flea_hunt_count": flea_hunt_count,
            "starting_location": self.get_starting_location_key(),
            "starting_crest": self.resolve_starting_crest(),
            "early_dash": self.is_early_dash_enabled(),
            "split_dash_and_sprint": self.is_split_dash_and_sprint(),
            "randomize_needle_upgrades":
                self.is_needle_upgrade_randomization_enabled(),
            "individual_relic_turn_ins":
                self.is_individual_relic_turn_ins_enabled(),
            "logic_audit_mode": self.is_logic_audit_mode_enabled(),
            "easy_skips": self.is_easy_skips_enabled(),
            "start_with_maps": self.is_start_with_maps_enabled(),
            "automatic_compass": self.is_automatic_compass_enabled(),
            "check_map_markers": self.get_check_map_markers_key(),
            "bellway_access": self.get_bellway_access_key(),
            "enemy_rosary_multiplier":
                self.get_enemy_rosary_multiplier_key(),
            "enemy_shard_multiplier":
                self.get_enemy_shard_multiplier_key(),
            "purchase_prices": purchase_prices,
            "faster_dialogue": self.is_faster_dialogue_enabled(),
            "death_link": self.is_death_link_enabled(),
            "silk_link": self.is_silk_link_enabled(),
            "rosary_link": self.is_rosary_link_enabled(),
            "shell_shard_link": self.is_shell_shard_link_enabled(),
            "trap_counts": self.resolve_trap_counts(),
            "requirements": export_requirements(
                goal_key,
                flea_hunt_count,
                self.allows_bellways_before_bell_beast(),
                (
                    get_crest_slot_memory_locket_count(self)
                    if uses_randomized_memory_lockets_for_crest_slots(self)
                    else 0
                ),
                (
                    POLLIP_HEART_COUNT
                    if self.get_category_mode('PollipHeart') != 'vanilla'
                    else 0
                ),
                bone_bottom_statue_tool_pouch_count,
            ),
            "abstract_requirements": export_abstract_requirements(
                self.allows_bellways_before_bell_beast(),
                self.get_category_mode('CrestSlot') != 'vanilla',
                self.get_starting_location_key(),
            ),
            "logic_item_dependencies": export_logic_item_dependencies(
                self.is_split_dash_and_sprint()
            ),
        }
        slot_data.update(
            {
                option_name: category_modes[category]
                for category, option_name
                in CATEGORY_OPTION_BY_LOCATION_CATEGORY.items()
            }
        )
        slot_data["minor_pickup_randomization"] = category_modes["Resource"]
        slot_data.update(
            {
                option_name: minor_family_modes[family_key]
                for family_key, option_name
                in MINOR_FAMILY_OPTION_BY_KEY.items()
            }
        )
        slot_data.update(
            {
                option_name: purchase_price_modes[category]
                for category, option_name
                in PRICE_CATEGORY_OPTION_NAMES.items()
            }
        )
        return slot_data
