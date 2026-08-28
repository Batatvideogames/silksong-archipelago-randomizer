from __future__ import annotations

import random
import unittest
from collections import Counter
from dataclasses import fields
from types import SimpleNamespace
from unittest import mock

from test.general import setup_multiworld

from BaseClasses import CollectionState, ItemClassification
from Fill import distribute_items_restrictive
from Options import OptionError
from worlds.AutoWorld import call_all
from worlds.silksong import SilksongWorld
from worlds.silksong.items import (
    ITEM_BASE_ID,
    ITEM_TABLE_SOURCE,
    TRAP_ITEM_NAMES,
    build_item_pool_entries,
    get_dynamic_trap_capacity,
    item_data_table,
    item_name_groups,
    item_table,
)
from worlds.silksong.locations import location_table
from worlds.silksong.minor_families import (
    MINOR_FAMILY_KEYS,
    MINOR_FAMILY_OPTION_BY_KEY,
    get_minor_family_shuffle_category,
)
from worlds.silksong.options import (
    CATEGORY_OPTION_BY_LOCATION_CATEGORY,
    AlphabetMode,
    Goal,
    SilksongOptions,
    SpellingBeePhrase,
)
from worlds.silksong.alphabet_mode import (
    ALPHABET_ITEM_COUNT,
    ALPHABET_ITEM_NAMES,
    ALPHABET_ITEM_ROWS,
    SPELLING_BEE_GOAL_KEY,
    parse_spelling_bee_phrase,
)
from worlds.silksong.requirements import (
    get_goal_requirements,
    get_logic_item_references,
)
from worlds.silksong.rules import get_active_crest_slot_locations


class TestAlphabetMode(unittest.TestCase):
    @staticmethod
    def minor_placement_categories() -> dict[str, str]:
        return {
            family_key: get_minor_family_shuffle_category(family_key)
            for family_key in MINOR_FAMILY_KEYS
        }

    @classmethod
    def modes(cls, mode: str) -> dict[str, str]:
        modes = {
            category: mode
            for category in CATEGORY_OPTION_BY_LOCATION_CATEGORY
        }
        modes.update(
            {
                get_minor_family_shuffle_category(family_key): mode
                for family_key in MINOR_FAMILY_KEYS
            }
        )
        return modes

    @classmethod
    def standard_modes(cls) -> dict[str, str]:
        vanilla_categories = {
            "MajorKey",
            "MemoryLocket",
            "Craftmetal",
            "Mossberry",
            "PollipHeart",
            "Silkeater",
            "LoreTablet",
            "BellShrine",
        }
        modes = {
            category: (
                "vanilla"
                if category in vanilla_categories
                else "anywhere"
            )
            for category in CATEGORY_OPTION_BY_LOCATION_CATEGORY
        }
        modes.update(
            {
                get_minor_family_shuffle_category(family_key): "vanilla"
                for family_key in MINOR_FAMILY_KEYS
            }
        )
        return modes

    @classmethod
    def build_standard_goal(
        cls,
        goal: str,
        *,
        alphabet_mode: bool,
        trap_counts: dict[str, int] | None = None,
        trap_randomizer: random.Random | None = None,
    ):
        return build_item_pool_entries(
            "Crest: Hunter",
            trap_counts,
            False,
            cls.standard_modes(),
            start_with_maps=True,
            automatic_compass=True,
            minor_shuffle_category_by_family=(
                cls.minor_placement_categories()
            ),
            trap_randomizer=trap_randomizer,
            act_one_only=(goal == "act_1"),
            act_two_only=(goal == "act_2"),
            start_fully_mapped=True,
            exclude_verdania=(goal != "act_3"),
            retain_green_prince_key=(goal == "act_2"),
            alphabet_mode=alphabet_mode,
        )

    @staticmethod
    def make_world(
        alphabet_mode: bool,
        *,
        goal: str = "act_3",
        spelling_bee_phrase: str = "Hornet",
        set_rules: bool = False,
    ):
        options = {
            option_name: "anywhere"
            for option_name in CATEGORY_OPTION_BY_LOCATION_CATEGORY.values()
        }
        options.update(
            {
                option_name: "anywhere"
                for option_name in MINOR_FAMILY_OPTION_BY_KEY.values()
            }
        )
        options["goal"] = goal
        options["alphabet_mode"] = alphabet_mode
        options["spelling_bee_phrase"] = spelling_bee_phrase
        steps = ["generate_early", "create_regions", "create_items"]
        if set_rules:
            steps.append("set_rules")
        multiworld = setup_multiworld(
            SilksongWorld,
            steps=tuple(steps),
            seed=12345,
            options=options,
        )
        return multiworld.worlds[1]

    def test_letters_are_unique_progression_items_appended_after_existing_ids(
        self,
    ) -> None:
        self.assertEqual(ALPHABET_ITEM_COUNT, 26)
        self.assertEqual(
            ALPHABET_ITEM_NAMES,
            tuple(f"Letter: {letter}" for letter in "ABCDEFGHIJKLMNOPQRSTUVWXYZ"),
        )
        self.assertEqual(ITEM_TABLE_SOURCE[-26:], ALPHABET_ITEM_ROWS)
        self.assertEqual(
            [item_table[name] for name in ALPHABET_ITEM_NAMES],
            list(range(835338, 835364)),
        )
        self.assertEqual(
            item_table[ALPHABET_ITEM_NAMES[0]],
            ITEM_BASE_ID + len(ITEM_TABLE_SOURCE) - 26,
        )
        self.assertEqual(
            item_name_groups["Alphabet Letters"],
            set(ALPHABET_ITEM_NAMES),
        )
        self.assertTrue(
            all(
                item_data_table[name].classification
                == ItemClassification.progression
                for name in ALPHABET_ITEM_NAMES
            )
        )
        self.assertTrue(
            set(ALPHABET_ITEM_NAMES).isdisjoint(
                get_logic_item_references()
            )
        )

    def test_spelling_bee_option_and_exact_goal_rule(self) -> None:
        option = Goal.from_any(SPELLING_BEE_GOAL_KEY)
        self.assertEqual(option.value, Goal.option_spelling_bee)
        self.assertEqual(option.current_key, SPELLING_BEE_GOAL_KEY)
        self.assertEqual(Goal.default, Goal.option_act_3)

        phrase = SpellingBeePhrase.from_any("Fishies")
        self.assertEqual(phrase.value, "Fishies")
        phrase_data = parse_spelling_bee_phrase(phrase.value)
        self.assertEqual(
            phrase_data,
            (
                "Fishies",
                (
                    "Letter: F",
                    "Letter: I",
                    "Letter: S",
                    "Letter: H",
                    "Letter: E",
                ),
            ),
        )

        requirements = get_goal_requirements(
            SPELLING_BEE_GOAL_KEY,
            spelling_bee_item_names=phrase_data[1],
        )
        self.assertEqual(len(requirements), 1)
        requirement = requirements[0]
        self.assertEqual(requirement.all_of, phrase_data[1])
        self.assertFalse(requirement.any_of)
        self.assertFalse(requirement.item_counts)
        self.assertFalse(requirement.require_any_crest)

    def test_goal_description_and_alphabet_option_order(self) -> None:
        self.assertEqual(
            " ".join((Goal.__doc__ or "").split()),
            (
                "Act 1-3 require beating the chosen act, Cursed Ending is "
                "self explanatory, Flea Hunt is getting the set amount of "
                "fleas and Spelling Bee is getting every unique letter in "
                "the chosen phrase with Alphabet Rando on"
            ),
        )
        option_names = tuple(
            field.name for field in fields(SilksongOptions)
        )
        trap_index = option_names.index("trap_percentage")
        self.assertEqual(
            option_names[trap_index - 1],
            "alphabet_mode",
        )

    def test_spelling_bee_keeps_every_letter_and_needs_the_phrase(self) -> None:
        world = self.make_world(
            False,
            goal=SPELLING_BEE_GOAL_KEY,
            set_rules=True,
        )
        slot_data = world.fill_slot_data()

        self.assertTrue(world.is_alphabet_mode_enabled())
        self.assertEqual(world.options.alphabet_mode.value, 1)
        self.assertEqual(slot_data["goal"], SPELLING_BEE_GOAL_KEY)
        self.assertEqual(slot_data["spelling_bee_phrase"], "Hornet")
        self.assertTrue(slot_data["alphabet_mode"])
        self.assertEqual(
            Counter(
                item.name
                for item in world.multiworld.itempool
                if item.name in ALPHABET_ITEM_NAMES
            ),
            Counter({name: 1 for name in ALPHABET_ITEM_NAMES}),
        )
        required_names = (
            "Letter: H",
            "Letter: O",
            "Letter: R",
            "Letter: N",
            "Letter: E",
            "Letter: T",
        )
        letter_items = {
            item.name: item
            for item in world.multiworld.itempool
            if item.name in ALPHABET_ITEM_NAMES
        }
        self.assertTrue(
            all(
                letter_items[name].classification
                == ItemClassification.progression
                for name in required_names
            )
        )
        self.assertTrue(
            all(
                letter_items[name].classification
                == ItemClassification.useful
                for name in ALPHABET_ITEM_NAMES
                if name not in required_names
            )
        )

        goal = world.multiworld.get_location("Goal", world.player)
        state = CollectionState(world.multiworld)
        for _index in range(26):
            state.collect(world.create_item("Letter: A"))
        self.assertFalse(goal.can_reach(state))

        for item_name in required_names[:-1]:
            state.collect(world.create_item(item_name))
        self.assertFalse(goal.can_reach(state))

        state.collect(world.create_item(required_names[-1]))
        self.assertTrue(goal.can_reach(state))

    def test_phrase_normalization_and_invalid_fallback(self) -> None:
        self.assertEqual(
            parse_spelling_bee_phrase("  Hornet   Nest  "),
            (
                "Hornet Nest",
                (
                    "Letter: H",
                    "Letter: O",
                    "Letter: R",
                    "Letter: N",
                    "Letter: E",
                    "Letter: T",
                    "Letter: S",
                ),
            ),
        )
        for invalid in ("", "   ", "Hornet!", "Act 2", "Café", "Hornet\tNest"):
            with self.subTest(phrase=invalid):
                self.assertIsNone(parse_spelling_bee_phrase(invalid))

        world = self.make_world(
            False,
            goal=SPELLING_BEE_GOAL_KEY,
            spelling_bee_phrase="Hornet!",
        )
        slot_data = world.fill_slot_data()
        self.assertEqual(world.get_goal_key(), "act_2")
        self.assertFalse(world.is_alphabet_mode_enabled())
        self.assertEqual(slot_data["goal"], "act_2")
        self.assertEqual(slot_data["spelling_bee_phrase"], "")
        self.assertFalse(slot_data["alphabet_mode"])

        alphabet_world = self.make_world(
            True,
            goal=SPELLING_BEE_GOAL_KEY,
            spelling_bee_phrase="Hornet!",
        )
        self.assertEqual(alphabet_world.get_goal_key(), "act_2")
        self.assertTrue(alphabet_world.is_alphabet_mode_enabled())
        self.assertEqual(
            Counter(
                item.name
                for item in alphabet_world.multiworld.itempool
                if item.name in ALPHABET_ITEM_NAMES
            ),
            Counter({name: 1 for name in ALPHABET_ITEM_NAMES}),
        )

    def test_disabled_mode_keeps_the_pool_unchanged(self) -> None:
        modes = self.modes("anywhere")
        arguments = {
            "starting_crest_item": "Crest: Hunter",
            "trap_counts": {},
            "split_dash_and_sprint": False,
            "category_modes": modes,
            "minor_shuffle_category_by_family": (
                self.minor_placement_categories()
            ),
        }
        implicit = build_item_pool_entries(**arguments)
        explicit = build_item_pool_entries(
            **arguments,
            alphabet_mode=False,
        )

        self.assertEqual(implicit, explicit)
        self.assertFalse(
            any(entry.name in ALPHABET_ITEM_NAMES for entry in implicit)
        )
        self.assertEqual(
            get_dynamic_trap_capacity(modes),
            get_dynamic_trap_capacity(modes, alphabet_mode=False),
        )

    def test_enabled_mode_replaces_filler_and_preserves_every_lane(
        self,
    ) -> None:
        modes = self.modes("shuffle")
        arguments = {
            "starting_crest_item": "Crest: Hunter",
            "trap_counts": {},
            "split_dash_and_sprint": False,
            "category_modes": modes,
            "minor_shuffle_category_by_family": (
                self.minor_placement_categories()
            ),
        }
        baseline = build_item_pool_entries(**arguments)
        alphabet = build_item_pool_entries(
            **arguments,
            alphabet_mode=True,
        )
        replaced_pairs = [
            (before, after)
            for before, after in zip(baseline, alphabet)
            if before != after
        ]

        self.assertEqual(len(baseline), len(alphabet))
        self.assertEqual(len(replaced_pairs), 26)
        self.assertEqual(
            {after.name for _before, after in replaced_pairs},
            set(ALPHABET_ITEM_NAMES),
        )
        self.assertTrue(
            all(
                item_data_table[before.name].classification
                == ItemClassification.filler
                for before, _after in replaced_pairs
            )
        )
        self.assertTrue(
            all(
                (
                    before.source_category,
                    before.placement_category,
                )
                == (
                    after.source_category,
                    after.placement_category,
                )
                for before, after in replaced_pairs
            )
        )
        self.assertEqual(
            Counter(
                (entry.source_category, entry.placement_category)
                for entry in baseline
            ),
            Counter(
                (entry.source_category, entry.placement_category)
                for entry in alphabet
            ),
        )

    def test_standard_goals_have_all_letters_without_new_locations(
        self,
    ) -> None:
        expected_filler_counts = {
            "act_1": 32,
            "act_2": 79,
            "act_3": 86,
        }
        for goal, filler_count in expected_filler_counts.items():
            with self.subTest(goal=goal):
                baseline = self.build_standard_goal(
                    goal,
                    alphabet_mode=False,
                )
                alphabet = self.build_standard_goal(
                    goal,
                    alphabet_mode=True,
                )
                self.assertEqual(len(baseline), len(alphabet))
                self.assertEqual(
                    sum(
                        item_data_table[entry.name].classification
                        == ItemClassification.filler
                        for entry in baseline
                    ),
                    filler_count,
                )
                self.assertEqual(
                    Counter(
                        entry.name
                        for entry in alphabet
                        if entry.name in ALPHABET_ITEM_NAMES
                    ),
                    Counter({name: 1 for name in ALPHABET_ITEM_NAMES}),
                )
        self.assertTrue(
            set(ALPHABET_ITEM_NAMES).isdisjoint(location_table)
        )

    def test_full_trap_pools_keep_all_letters(self) -> None:
        modes = self.standard_modes()
        for goal in ("act_1", "act_2", "act_3"):
            with self.subTest(goal=goal):
                capacity = get_dynamic_trap_capacity(
                    modes,
                    start_with_maps=True,
                    automatic_compass=True,
                    minor_shuffle_category_by_family=(
                        self.minor_placement_categories()
                    ),
                    act_one_only=(goal == "act_1"),
                    act_two_only=(goal == "act_2"),
                    start_fully_mapped=True,
                    exclude_verdania=(goal != "act_3"),
                    retain_green_prince_key=(goal == "act_2"),
                    alphabet_mode=True,
                )
                trap_counts = {
                    trap_name: (
                        capacity
                        if trap_name == "Stagger Trap"
                        else 0
                    )
                    for trap_name in TRAP_ITEM_NAMES
                }
                entries = self.build_standard_goal(
                    goal,
                    alphabet_mode=True,
                    trap_counts=trap_counts,
                    trap_randomizer=random.Random(12345),
                )

                self.assertEqual(
                    sum(
                        entry.name in ALPHABET_ITEM_NAMES
                        for entry in entries
                    ),
                    26,
                )
                self.assertEqual(
                    sum(entry.name == "Stagger Trap" for entry in entries),
                    capacity,
                )
                self.assertFalse(
                    any(
                        item_data_table[entry.name].classification
                        == ItemClassification.filler
                        for entry in entries
                    )
                )

    def test_sparse_pool_has_a_clear_generation_error(self) -> None:
        modes = self.modes("vanilla")
        with self.assertRaisesRegex(
            OptionError,
            r"alphabet_mode needs 26 filler rewards.*only 0 are available",
        ):
            build_item_pool_entries(
                "Crest: Hunter",
                {},
                False,
                modes,
                minor_shuffle_category_by_family=(
                    self.minor_placement_categories()
                ),
                alphabet_mode=True,
            )

    def test_pre_fill_rejects_progression_capacity_shortage(self) -> None:
        progression_items = (
            SimpleNamespace(advancement=True),
            SimpleNamespace(advancement=True),
        )
        multiworld = SimpleNamespace(
            worlds={
                1: SimpleNamespace(
                    game=SilksongWorld.game,
                    is_alphabet_mode_enabled=lambda: True,
                )
            },
            itempool=[
                *progression_items,
                SimpleNamespace(advancement=False),
            ],
            state=object(),
            get_unfilled_locations=lambda: (
                SimpleNamespace(
                    can_fill=lambda _state, item, check_access: (
                        item.advancement
                    )
                ),
                SimpleNamespace(
                    can_fill=lambda _state, _item, check_access: False
                ),
                SimpleNamespace(
                    can_fill=lambda _state, _item, check_access: False
                ),
            ),
        )
        world_module = __import__(
            SilksongWorld.__module__,
            fromlist=("prefill_category_shuffles",),
        )
        with (
            mock.patch.object(world_module, "prefill_category_shuffles"),
            self.assertRaisesRegex(
                OptionError,
                r"2 progression items for 1 progression-legal locations",
            ),
        ):
            SilksongWorld.stage_pre_fill(multiworld)

    def test_pre_fill_rejects_non_alphabet_capacity_shortage(self) -> None:
        progression_items = (
            SimpleNamespace(advancement=True),
            SimpleNamespace(advancement=True),
        )
        multiworld = SimpleNamespace(
            worlds={
                1: SimpleNamespace(
                    game=SilksongWorld.game,
                    is_alphabet_mode_enabled=lambda: False,
                )
            },
            itempool=[
                *progression_items,
                SimpleNamespace(advancement=False),
            ],
            state=object(),
            get_unfilled_locations=lambda: (
                SimpleNamespace(
                    can_fill=lambda _state, item, check_access: (
                        item.advancement
                    )
                ),
                SimpleNamespace(
                    can_fill=lambda _state, _item, check_access: False
                ),
                SimpleNamespace(
                    can_fill=lambda _state, _item, check_access: False
                ),
            ),
        )
        world_module = __import__(
            SilksongWorld.__module__,
            fromlist=("prefill_category_shuffles",),
        )
        with (
            mock.patch.object(world_module, "prefill_category_shuffles"),
            self.assertRaises(OptionError) as error,
        ):
            SilksongWorld.stage_pre_fill(multiworld)

        self.assertIn(
            "2 progression items for 1 progression-legal locations",
            str(error.exception),
        )
        self.assertNotIn("Disable Alphabet Mode", str(error.exception))

    def test_world_generation_uses_the_option_and_keeps_pool_balance(
        self,
    ) -> None:
        world = self.make_world(True)

        self.assertEqual(
            Counter(
                item.name
                for item in world.multiworld.itempool
                if item.name in ALPHABET_ITEM_NAMES
            ),
            Counter({name: 1 for name in ALPHABET_ITEM_NAMES}),
        )
        self.assertEqual(
            len(world.multiworld.itempool),
            sum(
                location.address is not None and location.item is None
                for location in world.multiworld.get_locations()
            ),
        )
        self.assertEqual(
            sum(
                item.name in TRAP_ITEM_NAMES
                for item in world.multiworld.itempool
            ),
            sum(world.resolve_trap_counts().values()),
        )

    def test_filled_crest_slots_export_non_locket_item_flags(self) -> None:
        world = self.make_world(False, set_rules=True)
        multiworld = world.multiworld
        call_all(multiworld, "connect_entrances")
        call_all(multiworld, "generate_basic")
        call_all(multiworld, "pre_fill")
        distribute_items_restrictive(multiworld)
        call_all(multiworld, "post_fill")

        expected = {
            location.name: int(location.item.classification)
            for location in get_active_crest_slot_locations(world)
            if location.item is not None
            and location.item.name != "Memory Locket"
        }
        self.assertEqual(
            world.fill_slot_data()["crest_slot_item_flags"],
            expected,
        )

    def test_slot_data_and_item_ownership_are_per_player(self) -> None:
        world = self.make_world(False)
        self.assertFalse(world.fill_slot_data()["alphabet_mode"])
        world.options.alphabet_mode = AlphabetMode(1)
        self.assertTrue(world.fill_slot_data()["alphabet_mode"])

        first_world = SilksongWorld.__new__(SilksongWorld)
        first_world.player = 1
        second_world = SilksongWorld.__new__(SilksongWorld)
        second_world.player = 2
        first_letter = first_world.create_item("Letter: A")
        second_letter = second_world.create_item("Letter: A")

        self.assertEqual(first_letter.code, second_letter.code)
        self.assertEqual(first_letter.player, 1)
        self.assertEqual(second_letter.player, 2)
        self.assertEqual(first_letter.classification, ItemClassification.progression)
        self.assertEqual(second_letter.classification, ItemClassification.progression)

    def test_progression_letter_fill_matrix_is_beatable(self) -> None:
        cases = (
            (SPELLING_BEE_GOAL_KEY, False),
            ("act_1", True),
            ("act_2", True),
            ("act_3", True),
        )
        for goal_key, alphabet_mode in cases:
            with self.subTest(
                goal=goal_key,
                alphabet_mode=alphabet_mode,
            ):
                world = self.make_world(
                    alphabet_mode,
                    goal=goal_key,
                    set_rules=True,
                )
                multiworld = world.multiworld
                call_all(multiworld, "connect_entrances")
                call_all(multiworld, "generate_basic")
                call_all(multiworld, "pre_fill")
                distribute_items_restrictive(multiworld)
                call_all(multiworld, "post_fill")
                self.assertTrue(multiworld.can_beat_game())

    def test_option_defaults_off(self) -> None:
        self.assertEqual(AlphabetMode.default, 0)
        self.assertEqual(AlphabetMode.from_any(False).value, 0)
        self.assertEqual(AlphabetMode.from_any(True).value, 1)


if __name__ == "__main__":
    unittest.main()
