from __future__ import annotations

import base64
import gzip
import json
import random
from collections import Counter

from BaseClasses import (
    CollectionState,
    Item,
    ItemClassification,
    Region,
    Tutorial,
)
from rule_builder.cached_world import CachedRuleBuilderWorld
from worlds.AutoWorld import WebWorld

from .act1_scope import (
    ACT_ONE_GOAL_KEY,
    get_act_one_excluded_location_names,
)
from .act2_scope import (
    ACT_THREE_ONLY_GOAL_LOCATION_NAMES,
    ACT_TWO_GOAL_KEY,
    CURSED_ENDING_GOAL_KEY,
    get_act_two_excluded_location_names,
)
from .category_fill import prefill_category_shuffles
from .items import (
    ACT_TWO_START_WITH_MAP_ITEMS,
    AUTOMATIC_COMPASS_ITEM,
    OPTIONAL_START_REPLACEMENT_ITEM,
    POOL_ONLY_USEFUL_ITEM_NAMES,
    QUILL_ITEM,
    PROGRESSIVE_SWIFT_STEP_ITEM,
    START_WITH_MAP_ITEMS,
    STARTING_CREST_ITEM_BY_KEY,
    SHUFFLE_FIXED_LOCATION_REWARDS,
    TRAP_ITEM_NAME_BY_WEIGHT_OPTION,
    TRAP_ITEM_NAMES,
    ItemPoolEntry,
    SilksongItem,
    build_item_pool_entries,
    get_act_one_start_with_map_items,
    get_configured_item_pool_size,
    get_dynamic_trap_capacity,
    get_act_two_skill_balance_precollected_item_name,
    get_vanilla_reward_name,
    item_data_table,
    item_name_groups,
    item_table,
)
from .locations import (
    INDIVIDUAL_RELIC_TURN_IN_ITEM_NAMES,
    LOCATION_NAMES_BY_CATEGORY,
    NEEDLE_UPGRADE_LOCATION_CATEGORY,
    OBSERVATION_LOCATION_CATEGORIES,
    PAIRED_LOCATION_CATEGORIES,
    PINMASTER_OIL_QUEST_LOCATION,
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
from .native_regions import (
    choose_location_anchor,
    connect_native_logic_regions,
    create_native_logic_region_map,
    native_source_requires_assumption,
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
    CRAWFATHER_LOCATION,
    FLEA_HUNT_GOAL_KEY,
    JUDGE_BELL_ITEMS,
    LOGIC_UNKNOWN_LOCATIONS,
    MAX_FLEA_HUNT_GOAL_COUNT,
    MEMORY_LOCKET_ITEM,
    MIN_FLEA_HUNT_GOAL_COUNT,
    OPTION_DEPENDENT_CREST_SLOT_ITEM_NAMES,
    POLLIP_HEART_COUNT,
    ROSARY_BANK_GATED_LOCATIONS,
    SIMPLE_KEY_GREEN_PRINCE,
    SIMPLE_KEY_ROSARY_BANK,
    THREEFOLD_MELODY_ITEMS,
    export_abstract_requirements,
    export_logic_item_dependencies,
    export_requirements,
    get_location_requirements,
    get_logic_item_references,
    is_logic_unknown_location,
    normalize_trails_end_requirement,
)
from .rules import (
    enforce_global_shuffle_item_rules,
    finalize_crest_slot_memory_locket_logic,
    get_active_crest_slot_locations,
    get_crest_slot_memory_locket_count,
    get_crest_slot_progression_item_count,
    restore_global_shuffle_item_rules,
    set_silksong_rules,
    uses_randomized_memory_lockets_for_crest_slots,
)
from .version import WORLD_VERSION
from .vog_hints import (
    build_vog_hint_plan,
    copy_vog_hint_plan,
    empty_vog_hint_plan,
    find_required_playthrough_locations,
)
from .verdania_scope import (
    ACT_THREE_GOAL_KEY,
    VERDANIA_LOCATION_NAMES,
)

__version__ = WORLD_VERSION

LOGIC_PAYLOAD_FORMAT = "gzip_base64_v1"
LOGIC_PAYLOAD_FIELDS = (
    "requirements",
    "abstract_requirements",
    "logic_item_dependencies",
)


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


class SilksongWorld(CachedRuleBuilderWorld):
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
    _vog_hint_plan: dict[str, object] | None = None

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
        key = self.options.starting_location.current_key
        if key not in {"vanilla", "bone_bottom"}:
            raise ValueError(
                f"Unknown Silksong starting location: {key!r}"
            )
        return key

    def get_goal_key(self) -> str:
        return self.options.goal.current_key

    def is_act_two_content_scope(self) -> bool:
        return self.get_goal_key() in {
            ACT_TWO_GOAL_KEY,
            CURSED_ENDING_GOAL_KEY,
        }

    def is_act_one_content_scope(self) -> bool:
        return self.get_goal_key() == ACT_ONE_GOAL_KEY

    def get_start_with_map_item_names(self) -> tuple[str, ...]:
        if not self.is_start_with_maps_enabled():
            return ()
        if self.is_act_one_content_scope():
            return get_act_one_start_with_map_items(
                self.get_goal_excluded_location_names()
            )
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

    def get_goal_excluded_location_names(self) -> frozenset[str]:
        goal_key = self.get_goal_key()
        verdania_location_names = (
            frozenset()
            if goal_key == ACT_THREE_GOAL_KEY
            else VERDANIA_LOCATION_NAMES
        )
        act_three_only_location_names = (
            ACT_THREE_ONLY_GOAL_LOCATION_NAMES
            if goal_key in {
                ACT_ONE_GOAL_KEY,
                ACT_TWO_GOAL_KEY,
                CURSED_ENDING_GOAL_KEY,
            }
            else frozenset()
        )
        if self.is_act_one_content_scope():
            return (
                get_act_one_excluded_location_names(
                    self.get_category_mode('MajorKey'),
                    self.get_category_mode('Boss'),
                    self.is_needle_upgrade_randomization_enabled(),
                    self.get_act_one_donation_tool_pouch_requirements(),
                )
                | verdania_location_names
                | act_three_only_location_names
            )
        return (
            self.get_act_two_excluded_location_names()
            | verdania_location_names
            | act_three_only_location_names
        )

    def get_act_one_donation_tool_pouch_requirements(self) -> dict[str, int]:
        if not self.is_act_one_content_scope():
            return {}
        return get_shell_shard_donation_tool_pouch_requirements(
            self.get_purchase_prices()
        )

    def generate_early(self) -> None:
        self._crest_slot_memory_locket_count = None
        self._vog_hint_plan = None
        starting_crest = self.resolve_starting_crest()
        category_modes = self.get_category_modes()
        goal_key = self.get_goal_key()
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
            start_fully_mapped=self.is_start_fully_mapped_enabled(),
            act_one_only=self.is_act_one_content_scope(),
            exclude_verdania=(goal_key != ACT_THREE_GOAL_KEY),
            retain_green_prince_key=(goal_key == ACT_TWO_GOAL_KEY),
            alphabet_mode=self.is_alphabet_mode_enabled(),
            act_one_donation_tool_pouch_requirements=(
                self.get_act_one_donation_tool_pouch_requirements()
            ),
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
            player_early_items[early_item_name] = (
                player_early_items.get(early_item_name, 0)
                + 1
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

    def is_individual_relic_turn_ins_enabled(self) -> bool:
        return bool(self.options.individual_relic_turn_ins.value)

    def get_skips_tier(self) -> int:
        return int(self.options.skips.value)

    def is_scuttlebrace_logic_enabled(self) -> bool:
        return bool(self.options.scuttlebrace_logic.value)

    def get_flea_hunt_goal_count(self) -> int:
        count = int(self.options.flea_hunt_count.value)
        if not (
            MIN_FLEA_HUNT_GOAL_COUNT
            <= count
            <= MAX_FLEA_HUNT_GOAL_COUNT
        ):
            raise ValueError(
                "flea_hunt_count must be between "
                f"{MIN_FLEA_HUNT_GOAL_COUNT} and "
                f"{MAX_FLEA_HUNT_GOAL_COUNT} but received {count!r}."
            )
        return count

    def is_needle_upgrade_randomization_enabled(self) -> bool:
        return bool(self.options.randomize_needle_upgrades.value)

    def is_alphabet_mode_enabled(self) -> bool:
        from .alphabet_mode import SPELLING_BEE_GOAL_KEY

        return bool(self.options.alphabet_mode.value) or (
            self.get_goal_key() == SPELLING_BEE_GOAL_KEY
        )

    def is_start_with_maps_enabled(self) -> bool:
        return self.is_start_fully_mapped_enabled() or bool(
            self.options.start_with_maps.value
        )

    def is_start_fully_mapped_enabled(self) -> bool:
        return bool(self.options.start_fully_mapped.value)

    def is_automatic_compass_enabled(self) -> bool:
        return bool(self.options.automatic_compass.value)

    def get_bellway_access_key(self) -> str:
        key = self.options.bellway_access.current_key
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

    def get_trails_end_requirement_key(self) -> str:
        return normalize_trails_end_requirement(
            self.options.trails_end_requirement.current_key
        )

    def get_check_map_markers_key(self) -> str:
        key = self.options.check_map_markers.current_key
        if key not in {"off", "mapped_rooms", "owned_maps", "all"}:
            raise ValueError(
                f"Unknown check map marker mode: {key!r}"
            )
        return key

    def uses_randomized_bell_markers(self) -> bool:
        return bool(self.options.randomized_bell_markers.value)

    def uses_randomized_melody_markers(self) -> bool:
        return bool(self.options.randomized_melody_markers.value)

    def get_randomized_item_marker_locations(self) -> dict[str, str]:
        item_names = set()
        if self.uses_randomized_bell_markers():
            item_names.update(JUDGE_BELL_ITEMS)
        if self.uses_randomized_melody_markers():
            item_names.update(THREEFOLD_MELODY_ITEMS)
        if not item_names:
            return {}

        return {
            location.item.name: location.name
            for location in sorted(
                self.multiworld.get_filled_locations(),
                key=lambda candidate: (candidate.player, candidate.name),
            )
            if (
                location.player == self.player
                and location.address is not None
                and location.item is not None
                and location.item.player == self.player
                and location.item.name in item_names
            )
        }

    def get_enemy_rosary_multiplier_key(self) -> str:
        key = self.options.enemy_rosary_multiplier.current_key
        if key not in {"x1", "x1_5", "x2", "x3", "x5", "x10"}:
            raise ValueError(
                f"Unknown enemy Rosary multiplier: {key!r}"
            )
        return key

    def get_enemy_shard_multiplier_key(self) -> str:
        key = self.options.enemy_shard_multiplier.current_key
        if key not in {"x1", "x1_5", "x2", "x3", "x5", "x10"}:
            raise ValueError(
                f"Unknown enemy Shell Shard multiplier: {key!r}"
            )
        return key

    def get_purchase_price_modes(self) -> dict[str, str]:
        modes: dict[str, str] = {}
        for category, option_name in PRICE_CATEGORY_OPTION_NAMES.items():
            key = getattr(self.options, option_name).current_key
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
        return bool(self.options.faster_dialogue.value)

    def is_death_link_enabled(self) -> bool:
        return bool(self.options.death_link.value)

    def get_death_link_cocoon_key(self) -> str:
        key = self.options.death_link_cocoon.current_key
        if key not in {"vanilla", "cocoonless", "cocoon"}:
            raise ValueError(
                f"Unknown Death Link cocoon mode: {key!r}"
            )
        return key

    def is_silk_link_enabled(self) -> bool:
        return bool(self.options.silk_link.value)

    def is_rosary_link_enabled(self) -> bool:
        return bool(self.options.rosary_link.value)

    def is_shell_shard_link_enabled(self) -> bool:
        return bool(self.options.shell_shard_link.value)

    def get_vog_hint_counts(self) -> tuple[int, int, int]:
        option_names = (
            "vog_woth_hints",
            "vog_foolish_hints",
            "vog_general_hints",
        )
        return tuple(
            max(
                0,
                min(
                    30,
                    int(getattr(self.options, option_name).value),
                ),
            )
            for option_name in option_names
        )

    def get_vog_hint_plan(self) -> dict[str, object]:
        plan = getattr(self, "_vog_hint_plan", None)
        if plan is None:
            plan = self.build_vog_hint_plan()
        return copy_vog_hint_plan(plan)

    def build_vog_hint_plan(self) -> dict[str, object]:
        if not any(self.get_vog_hint_counts()):
            plan = empty_vog_hint_plan()
        else:
            cache_name = "_silksong_vog_required_locations"
            required_locations = getattr(
                self.multiworld,
                cache_name,
                None,
            )
            if required_locations is None:
                required_locations = find_required_playthrough_locations(
                    self.multiworld
                )
                setattr(
                    self.multiworld,
                    cache_name,
                    required_locations,
                )
            plan = build_vog_hint_plan(self, required_locations)
        self._vog_hint_plan = plan
        return copy_vog_hint_plan(plan)

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
            self.is_start_fully_mapped_enabled(),
            act_one_only=self.is_act_one_content_scope(),
            exclude_verdania=(
                self.get_goal_key() != ACT_THREE_GOAL_KEY
            ),
            retain_green_prince_key=(
                self.get_goal_key() == ACT_TWO_GOAL_KEY
            ),
            alphabet_mode=self.is_alphabet_mode_enabled(),
            act_one_donation_tool_pouch_requirements=(
                self.get_act_one_donation_tool_pouch_requirements()
            ),
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
        if (
            name == 'Scuttlebrace'
            and not self.is_scuttlebrace_logic_enabled()
        ):
            classification = ItemClassification.useful
        if (
            name == SIMPLE_KEY_GREEN_PRINCE
            and self.get_goal_key() != ACT_THREE_GOAL_KEY
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
        return True

    def create_event(self, name: str) -> Item:
        return SilksongItem(
            name,
            ItemClassification.progression,
            None,
            self.player,
        )

    def create_items(self) -> None:
        starting_crest = self.resolve_starting_crest()
        starting_crest_item = STARTING_CREST_ITEM_BY_KEY[starting_crest]
        category_modes = self.get_category_modes()
        precollected_option_items = {
            *self.get_start_with_map_item_names(),
        }
        option_replaced_item_names = {
            *precollected_option_items,
            *((QUILL_ITEM,) if self.is_start_fully_mapped_enabled() else ()),
            *(
                (AUTOMATIC_COMPASS_ITEM,)
                if self.is_automatic_compass_enabled()
                else ()
            ),
        }

        def place_fixed_reward(
            location_name: str,
            reward_name: str,
        ) -> None:
            effective_reward_name = (
                OPTIONAL_START_REPLACEMENT_ITEM
                if reward_name in option_replaced_item_names
                else reward_name
            )
            effective_reward = self.create_item(effective_reward_name)
            if (
                location_name in LOGIC_UNKNOWN_LOCATIONS
                and effective_reward.advancement
            ):
                # Keep required items off checks with incomplete routes.
                self.multiworld.push_precollected(effective_reward)
                effective_reward = self.create_item(
                    OPTIONAL_START_REPLACEMENT_ITEM
                )
            self.multiworld.get_location(
                location_name,
                self.player,
            ).place_locked_item(effective_reward)

        if category_modes['Crest'] != 'vanilla':
            self.multiworld.push_precollected(
                self.create_item(starting_crest_item)
            )

        for category in PAIRED_LOCATION_CATEGORIES:
            for location_name in LOCATION_NAMES_BY_CATEGORY[category]:
                if (
                    location_name
                    in self.get_goal_excluded_location_names()
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
                    place_fixed_reward(
                        location_name,
                        reward_name,
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
                        in self.get_goal_excluded_location_names()
                    ):
                        continue
                    place_fixed_reward(
                        location_name,
                        reward_name,
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
            self.is_start_fully_mapped_enabled(),
            act_one_only=self.is_act_one_content_scope(),
            exclude_verdania=(
                self.get_goal_key() != ACT_THREE_GOAL_KEY
            ),
            retain_green_prince_key=(
                self.get_goal_key() == ACT_TWO_GOAL_KEY
            ),
            alphabet_mode=self.is_alphabet_mode_enabled(),
            act_one_donation_tool_pouch_requirements=(
                self.get_act_one_donation_tool_pouch_requirements()
            ),
        ))

        pool_item_names = {entry.name for entry in pool_entries}
        for item_name in POOL_ONLY_USEFUL_ITEM_NAMES:
            if item_name not in pool_item_names:
                self.multiworld.push_precollected(
                    self.create_item(item_name)
                )

        if self.is_act_two_content_scope():
            balance_item_name = (
                get_act_two_skill_balance_precollected_item_name(
                    pool_entries,
                    category_modes,
                    self.is_automatic_compass_enabled(),
                    self.is_start_fully_mapped_enabled(),
                )
            )
            if balance_item_name is not None:
                self.multiworld.push_precollected(
                    self.create_item(balance_item_name)
                )

        get_locations = getattr(self.multiworld, 'get_locations', None)
        addressed_unfilled_locations = (
            [
                location
                for location in get_locations()
                if (
                    location.player == self.player
                    and location.address is not None
                    and location.item is None
                )
            ]
            if callable(get_locations)
            else []
        )
        location_lane_counts = Counter(
            getattr(
                location,
                'silksong_placement_category',
                None,
            )
            for location in addressed_unfilled_locations
        )
        original_pool_size = len(pool_entries)
        original_pool_lane_counts = Counter(
            entry.placement_category
            for entry in pool_entries
        )
        if (
            callable(get_locations)
            and location_lane_counts != original_pool_lane_counts
        ):
            raise AssertionError(
                "Silksong location and item placement lanes are unbalanced: "
                f"{location_lane_counts!r} != {original_pool_lane_counts!r}."
            )

        unknown_demand_by_lane = Counter(
            getattr(
                location,
                'silksong_placement_category',
                None,
            )
            for location in addressed_unfilled_locations
            if location.name in LOGIC_UNKNOWN_LOCATIONS
        )
        for placement_category, unknown_demand in (
            unknown_demand_by_lane.items()
        ):
            matching_indices = [
                index
                for index, entry in enumerate(pool_entries)
                if entry.placement_category == placement_category
            ]
            nonadvancement_count = sum(
                not self.create_item(pool_entries[index].name).advancement
                for index in matching_indices
            )
            shortage = unknown_demand - nonadvancement_count
            if shortage <= 0:
                continue
            advancement_indices = [
                index
                for index in matching_indices
                if self.create_item(pool_entries[index].name).advancement
            ]
            if len(advancement_indices) < shortage:
                raise ValueError(
                    "LogicUnknown placement safety could not replace "
                    f"{shortage} advancement item(s) in lane "
                    f"{placement_category!r}."
                )
            for index in advancement_indices[:shortage]:
                displaced_entry = pool_entries[index]
                self.multiworld.push_precollected(
                    self.create_item(
                        displaced_entry.name,
                        displaced_entry.placement_category,
                    )
                )
                pool_entries[index] = ItemPoolEntry(
                    OPTIONAL_START_REPLACEMENT_ITEM,
                    displaced_entry.source_category,
                    displaced_entry.placement_category,
                )

        if len(pool_entries) != original_pool_size:
            raise AssertionError(
                "LogicUnknown placement safety changed the item-pool size."
            )
        if Counter(
            entry.placement_category
            for entry in pool_entries
        ) != original_pool_lane_counts:
            raise AssertionError(
                "LogicUnknown placement safety changed placement lanes."
            )
        for placement_category, unknown_demand in (
            unknown_demand_by_lane.items()
        ):
            nonadvancement_count = sum(
                not self.create_item(entry.name).advancement
                for entry in pool_entries
                if entry.placement_category == placement_category
            )
            if nonadvancement_count < unknown_demand:
                raise AssertionError(
                    "LogicUnknown placement safety left too few "
                    f"non-advancement items in lane {placement_category!r}."
                )

        accessibility = getattr(self.options, 'accessibility', None)
        if (
            self.is_act_one_content_scope()
            and uses_randomized_memory_lockets_for_crest_slots(self)
            and getattr(accessibility, 'value', accessibility)
            == getattr(accessibility, 'option_full', 0)
        ):
            precollected_items = getattr(
                self.multiworld,
                'precollected_items',
                (),
            )
            if isinstance(precollected_items, dict):
                player_precollected_items = precollected_items.get(
                    self.player,
                    (),
                )
            else:
                player_precollected_items = precollected_items
            available_locket_count = sum(
                entry.name == MEMORY_LOCKET_ITEM
                for entry in pool_entries
            ) + sum(
                item.name == MEMORY_LOCKET_ITEM
                and getattr(item, 'player', self.player) == self.player
                for item in player_precollected_items
            )
            active_slot_count = len(
                get_active_crest_slot_locations(self)
            )
            for _copy_index in range(
                max(0, active_slot_count - available_locket_count)
            ):
                self.multiworld.push_precollected(
                    self.create_item(MEMORY_LOCKET_ITEM)
                )

        pool_entries = tuple(pool_entries)

        self.multiworld.itempool.extend(
            self.create_item(entry.name, entry.placement_category)
            for entry in pool_entries
        )

    def create_regions(self) -> None:
        menu = Region("Menu", self.player, self.multiworld)
        pharloom = Region("Pharloom", self.player, self.multiworld)
        self.multiworld.regions += [menu, pharloom]
        native_regions = create_native_logic_region_map(self)
        abstract_names = self._silksong_native_abstract_names
        self._silksong_native_location_anchors = {}
        self._silksong_native_assumed_source_locations = set()
        menu.connect(pharloom)
        connect_native_logic_regions(self, menu, native_regions)
        randomize_needle_upgrades = (
            self.is_needle_upgrade_randomization_enabled()
        )
        pollip_heart_count = (
            POLLIP_HEART_COUNT
            if self.get_category_mode("PollipHeart") != "vanilla"
            else 0
        )
        logic_item_references = get_logic_item_references()

        fixed_shuffle_locations = {
            location_name
            for locations in SHUFFLE_FIXED_LOCATION_REWARDS.values()
            for location_name in locations
        }
        for name, data in location_data_table.items():
            if (
                name in self.get_goal_excluded_location_names()
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
                    and not (
                        self.is_start_fully_mapped_enabled()
                        and data.category == 'Skill'
                        and get_vanilla_reward_name(
                            name,
                            data.category,
                        ) == QUILL_ITEM
                    )
                )
                else data.code
            )
            anchor = None
            if (
                name != "Goal"
                and name != CRAWFATHER_LOCATION
                and not is_logic_unknown_location(name)
            ):
                anchor = choose_location_anchor(
                    get_location_requirements(
                        name,
                        pollip_heart_count=pollip_heart_count,
                    ),
                    abstract_names,
                )
            reward_name = (
                get_vanilla_reward_name(name, data.category)
                if mode == "vanilla"
                else ""
            )
            uses_native_source = (
                reward_name in logic_item_references
                and native_source_requires_assumption(
                    self,
                    name,
                    reward_name,
                    pollip_heart_count,
                    anchor,
                )
            )
            if uses_native_source:
                self._silksong_native_assumed_source_locations.add(name)
            parent_anchor = None if uses_native_source else anchor
            parent_region = (
                native_regions[parent_anchor]
                if parent_anchor is not None
                else pharloom
            )
            self._silksong_native_location_anchors[name] = anchor
            location = SilksongLocation(
                self.player,
                name,
                address,
                parent_region,
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
            parent_region.locations.append(location)

    def set_rules(self) -> None:
        set_silksong_rules(self)

    @classmethod
    def stage_pre_fill(cls, multiworld) -> None:
        scope = getattr(
            multiworld,
            "_silksong_shuffle_item_rule_scope",
            None,
        )
        try:
            prefill_category_shuffles(multiworld, cls.game)
        finally:
            restore_global_shuffle_item_rules(scope)
            if hasattr(
                multiworld,
                "_silksong_shuffle_item_rule_scope",
            ):
                delattr(
                    multiworld,
                    "_silksong_shuffle_item_rule_scope",
                )

    @classmethod
    def stage_generate_basic(cls, multiworld) -> None:
        multiworld._silksong_shuffle_item_rule_scope = (
            enforce_global_shuffle_item_rules(multiworld)
        )

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
                excluded_slots,
                key=lambda location: (location.player, location.name),
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
                        location not in excluded_slots
                        and location.address is not None
                        and location.item is not None
                        and not location.item.advancement
                        and not location.locked
                        and location.can_reach(slotless_state)
                    )
                ),
                key=lambda location: (location.player, location.name),
            )
            if not progression_slots or not replacement_locations:
                return slotless_state

            from Fill import swap_location_item

            improved_state = None
            fallback_swap = None
            fallback_state = None
            for slot_location in progression_slots:
                for replacement_location in replacement_locations:
                    if not (
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
                    ):
                        continue

                    slot_item = slot_location.item
                    previous_item_count = slotless_state.count(
                        slot_item.name,
                        slot_item.player,
                    )
                    swap_location_item(slot_location, replacement_location)
                    candidate_state = cls._build_crest_slotless_state(
                        multiworld,
                        excluded_slots,
                    )
                    candidate_count = candidate_state.count(
                        MEMORY_LOCKET_ITEM,
                        world.player,
                    )
                    if candidate_count > reachable_count:
                        improved_state = candidate_state
                        break
                    if (
                        fallback_swap is None
                        and candidate_state.count(
                            slot_item.name,
                            slot_item.player,
                        ) > previous_item_count
                    ):
                        fallback_swap = (
                            slot_location,
                            replacement_location,
                        )
                        fallback_state = candidate_state
                    swap_location_item(slot_location, replacement_location)
                if improved_state is not None:
                    break

            if improved_state is None:
                if fallback_swap is None:
                    return slotless_state
                swap_location_item(*fallback_swap)
                improved_state = fallback_state
            slotless_state = improved_state

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

        # Move only the progression needed to keep two Lockets in reserve.
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
            # Minimal may leave optional Crest Slots beyond the goal. That is
            # fine when they hold no progression because AP cannot count native
            # Lockets outside the room. Fail if progression would be trapped.
            if progression_count and reachable_count < required_count:
                from Fill import FillError

                raise FillError(
                    f"{multiworld.player_name[world.player]}'s Crest Slot "
                    f"checks contain {progression_count} progression "
                    "rewards, but only "
                    f"{reachable_count} of the required {required_count} "
                    "Memory Lockets are reachable without Crest Slot "
                    "rewards. Check forced placements, exclusions and "
                    "category settings.",
                    multiworld=multiworld,
                )

    @classmethod
    def stage_finalize_multiworld(cls, multiworld) -> None:
        worlds = [
            multiworld.worlds[player]
            for player in multiworld.player_ids
            if multiworld.worlds[player].game == cls.game
        ]
        if not worlds:
            return

        if any(any(world.get_vog_hint_counts()) for world in worlds):
            required_locations = find_required_playthrough_locations(
                multiworld
            )
            setattr(
                multiworld,
                "_silksong_vog_required_locations",
                required_locations,
            )

        for world in worlds:
            world.build_vog_hint_plan()

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
            "alphabet_mode": self.is_alphabet_mode_enabled(),
            "individual_relic_turn_ins":
                self.is_individual_relic_turn_ins_enabled(),
            "skips": self.get_skips_tier(),
            "scuttlebrace_logic":
                self.is_scuttlebrace_logic_enabled(),
            "start_with_maps": self.is_start_with_maps_enabled(),
            "start_fully_mapped":
                self.is_start_fully_mapped_enabled(),
            "automatic_compass": self.is_automatic_compass_enabled(),
            "check_map_markers": self.get_check_map_markers_key(),
            "randomized_bell_markers":
                self.uses_randomized_bell_markers(),
            "randomized_melody_markers":
                self.uses_randomized_melody_markers(),
            "randomized_item_marker_locations":
                self.get_randomized_item_marker_locations(),
            "vog_hints": self.get_vog_hint_plan(),
            "bellway_access": self.get_bellway_access_key(),
            "trails_end_requirement":
                self.get_trails_end_requirement_key(),
            "enemy_rosary_multiplier":
                self.get_enemy_rosary_multiplier_key(),
            "enemy_shard_multiplier":
                self.get_enemy_shard_multiplier_key(),
            "purchase_prices": purchase_prices,
            "faster_dialogue": self.is_faster_dialogue_enabled(),
            "death_link": self.is_death_link_enabled(),
            "death_link_cocoon": self.get_death_link_cocoon_key(),
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
                randomize_needle_upgrades=(
                    self.is_needle_upgrade_randomization_enabled()
                ),
                scuttlebrace_logic_enabled=(
                    self.is_scuttlebrace_logic_enabled()
                ),
            ),
            "abstract_requirements": export_abstract_requirements(
                self.allows_bellways_before_bell_beast(),
                self.get_category_mode('CrestSlot') != 'vanilla',
                self.get_starting_location_key(),
                self.get_trails_end_requirement_key(),
                self.is_scuttlebrace_logic_enabled(),
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

    def modify_multidata(self, multidata: dict) -> None:
        slot_data = multidata["slot_data"][self.player]
        logic_payload = {
            key: slot_data.pop(key)
            for key in LOGIC_PAYLOAD_FIELDS
        }
        payload_json = json.dumps(
            logic_payload,
            ensure_ascii=False,
            separators=(",", ":"),
            sort_keys=True,
        ).encode("utf-8")
        slot_data["logic_payload_format"] = LOGIC_PAYLOAD_FORMAT
        slot_data["logic_payload"] = base64.b64encode(
            gzip.compress(payload_json, mtime=0)
        ).decode("ascii")
