from collections import Counter
from unittest import TestCase

from BaseClasses import CollectionState, ItemClassification
from rule_builder.rules import Rule
from test.general import setup_multiworld

from .. import SilksongWorld
from ..items import (
    LEDGE_GRAB_ITEM,
    SWIM_ITEM,
    build_item_pool_entries,
    get_dynamic_trap_capacity,
    item_data_table,
)
from ..minor_families import (
    MINOR_FAMILY_KEYS,
    get_minor_family_shuffle_category,
)
from ..options import (
    CATEGORY_OPTION_BY_LOCATION_CATEGORY,
    LedgegrabAbilityRando,
    SwimAbilityRando,
)
from ..requirements import (
    ABSTRACT_REQUIREMENTS,
    LEDGE_GRAB_CAPABILITY_REQUIREMENT,
    SWIM_CAPABILITY_REQUIREMENT,
    get_abstract_requirements,
    get_logic_item_references,
    make_requirements_rule,
    req,
)
from ..requirement_rules import build_requirements_rule


ABILITY_CASES = (
    (False, False, frozenset()),
    (True, False, frozenset((LEDGE_GRAB_ITEM,))),
    (False, True, frozenset((SWIM_ITEM,))),
    (True, True, frozenset((LEDGE_GRAB_ITEM, SWIM_ITEM))),
)

CAPABILITY_ITEMS = {
    LEDGE_GRAB_CAPABILITY_REQUIREMENT: LEDGE_GRAB_ITEM,
    SWIM_CAPABILITY_REQUIREMENT: SWIM_ITEM,
}


class TestInnateAbilityPool(TestCase):
    @staticmethod
    def pool_arguments() -> dict[str, object]:
        category_modes = {
            category: "anywhere"
            for category in CATEGORY_OPTION_BY_LOCATION_CATEGORY
        }
        minor_categories = {
            family_key: get_minor_family_shuffle_category(family_key)
            for family_key in MINOR_FAMILY_KEYS
        }
        return {
            "starting_crest_item": "Crest: Hunter",
            "trap_counts": {},
            "split_dash_and_sprint": False,
            "category_modes": category_modes,
            "minor_shuffle_category_by_family": minor_categories,
        }

    def test_options_default_off_without_changing_default_graph(self) -> None:
        self.assertEqual(LedgegrabAbilityRando.default, 0)
        self.assertEqual(SwimAbilityRando.default, 0)
        self.assertIs(get_abstract_requirements(), ABSTRACT_REQUIREMENTS)
        self.assertIs(
            get_abstract_requirements(
                randomize_ledge_grab=False,
                randomize_swim=False,
            ),
            ABSTRACT_REQUIREMENTS,
        )

        arguments = self.pool_arguments()
        implicit = build_item_pool_entries(**arguments)
        explicit = build_item_pool_entries(
            **arguments,
            randomize_ledge_grab=False,
            randomize_swim=False,
        )

        self.assertEqual(implicit, explicit)
        self.assertFalse(
            any(
                entry.name in CAPABILITY_ITEMS.values()
                for entry in implicit
            )
        )

    def test_options_replace_only_the_enabled_capability_owner(self) -> None:
        baseline = get_abstract_requirements()
        for ledge_grab, swim, expected_items in ABILITY_CASES:
            with self.subTest(ledge_grab=ledge_grab, swim=swim):
                adjusted = get_abstract_requirements(
                    randomize_ledge_grab=ledge_grab,
                    randomize_swim=swim,
                )
                changed_owners = {
                    capability_name
                    for capability_name in baseline
                    if adjusted[capability_name] is not baseline[capability_name]
                }
                expected_owners = {
                    capability_name
                    for capability_name, item_name in CAPABILITY_ITEMS.items()
                    if item_name in expected_items
                }

                self.assertEqual(changed_owners, expected_owners)
                self.assertTrue(all(
                    adjusted[capability_name][0].all_of == (item_name,)
                    for capability_name, item_name in CAPABILITY_ITEMS.items()
                    if capability_name in expected_owners
                ))

    def test_option_only_ability_items_are_logic_dependencies(self) -> None:
        self.assertTrue(
            set(CAPABILITY_ITEMS.values()).issubset(
                get_logic_item_references()
            )
        )

    def test_enabled_items_replace_filler_without_changing_pool_size(self) -> None:
        arguments = self.pool_arguments()
        baseline = build_item_pool_entries(**arguments)
        baseline_capacity = get_dynamic_trap_capacity(
            arguments["category_modes"],
            minor_shuffle_category_by_family=(
                arguments["minor_shuffle_category_by_family"]
            ),
        )

        for ledge_grab, swim, expected_items in ABILITY_CASES:
            with self.subTest(ledge_grab=ledge_grab, swim=swim):
                entries = build_item_pool_entries(
                    **arguments,
                    randomize_ledge_grab=ledge_grab,
                    randomize_swim=swim,
                )
                changed = [
                    (before, after)
                    for before, after in zip(baseline, entries)
                    if before != after
                ]
                ability_counts = Counter(
                    entry.name
                    for entry in entries
                    if entry.name in CAPABILITY_ITEMS.values()
                )

                self.assertEqual(len(entries), len(baseline))
                self.assertEqual(len(changed), len(expected_items))
                self.assertEqual(
                    ability_counts,
                    Counter({name: 1 for name in expected_items}),
                )
                self.assertTrue(all(
                    item_data_table[before.name].classification
                    == ItemClassification.filler
                    for before, _after in changed
                ))
                self.assertTrue(all(
                    (
                        before.source_category,
                        before.placement_category,
                    )
                    == (
                        after.source_category,
                        after.placement_category,
                    )
                    for before, after in changed
                ))
                self.assertEqual(
                    get_dynamic_trap_capacity(
                        arguments["category_modes"],
                        minor_shuffle_category_by_family=(
                            arguments[
                                "minor_shuffle_category_by_family"
                            ]
                        ),
                        randomize_ledge_grab=ledge_grab,
                        randomize_swim=swim,
                    ),
                    baseline_capacity - len(expected_items),
                )

        for item_name in CAPABILITY_ITEMS.values():
            with self.subTest(item_name=item_name):
                self.assertEqual(
                    item_data_table[item_name].classification,
                    ItemClassification.progression,
                )


class TestInnateAbilityWorlds(TestCase):
    worlds = {}

    @classmethod
    def setUpClass(cls) -> None:
        for ledge_grab, swim, _expected_items in ABILITY_CASES:
            multiworld = setup_multiworld(
                SilksongWorld,
                steps=("generate_early", "create_regions", "create_items"),
                seed=12345,
                options={
                    "goal": "act_3",
                    "ledgegrab_ability_rando": ledge_grab,
                    "swim_ability_rando": swim,
                },
            )
            cls.worlds[(ledge_grab, swim)] = multiworld.worlds[1]

    @staticmethod
    def capability_entrance(world, capability_name: str):
        return world.multiworld.get_entrance(
            f"Silksong Logic: {capability_name} [1]",
            world.player,
        )

    def test_pool_items_and_slot_data_match_each_option_combination(self) -> None:
        for ledge_grab, swim, expected_items in ABILITY_CASES:
            with self.subTest(ledge_grab=ledge_grab, swim=swim):
                world = self.worlds[(ledge_grab, swim)]
                ability_items = [
                    item
                    for item in world.multiworld.itempool
                    if item.name in CAPABILITY_ITEMS.values()
                ]
                slot_data = world.fill_slot_data()

                self.assertEqual(
                    Counter(item.name for item in ability_items),
                    Counter({name: 1 for name in expected_items}),
                )
                self.assertTrue(all(
                    item.classification == ItemClassification.progression
                    for item in ability_items
                ))
                self.assertEqual(
                    slot_data["ledgegrab_ability_rando"],
                    ledge_grab,
                )
                self.assertEqual(slot_data["swim_ability_rando"], swim)
                for capability_name, item_name in CAPABILITY_ITEMS.items():
                    alternatives = slot_data["abstract_requirements"][
                        capability_name
                    ]["alternatives"]
                    self.assertEqual(len(alternatives), 1)
                    self.assertEqual(
                        alternatives[0]["all_of"],
                        [item_name] if item_name in expected_items else [],
                    )

    def test_only_enabled_capability_entrances_are_item_gated(self) -> None:
        for ledge_grab, swim, expected_items in ABILITY_CASES:
            with self.subTest(ledge_grab=ledge_grab, swim=swim):
                world = self.worlds[(ledge_grab, swim)]
                for capability_name, item_name in CAPABILITY_ITEMS.items():
                    entrance = self.capability_entrance(
                        world,
                        capability_name,
                    )
                    enabled = item_name in expected_items

                    if not enabled:
                        self.assertIs(
                            entrance.access_rule,
                            type(entrance).access_rule,
                        )
                        continue

                    self.assertIsInstance(entrance.access_rule, Rule.Resolved)
                    self.assertEqual(
                        set(entrance.access_rule.item_dependencies()),
                        {item_name},
                    )
                    state = CollectionState(world.multiworld)
                    self.assertFalse(entrance.access_rule(state))
                    state.collect(world.create_item(item_name))
                    self.assertTrue(entrance.access_rule(state))

    def test_off_capability_entrances_keep_ap_default_access_rule(self) -> None:
        world = self.worlds[(False, False)]
        entrances = [
            self.capability_entrance(world, capability_name)
            for capability_name in CAPABILITY_ITEMS
        ]

        self.assertTrue(all(
            entrance.access_rule is type(entrance).access_rule
            for entrance in entrances
        ))
        self.assertFalse(any(
            isinstance(entrance.access_rule, Rule.Resolved)
            and entrance.access_rule.always_true
            for entrance in entrances
        ))

    @staticmethod
    def collect(world, *item_names: str) -> CollectionState:
        state = CollectionState(world.multiworld)
        for item_name in item_names:
            state.collect(world.create_item(item_name))
        return state

    def test_legacy_evaluator_keeps_capability_additive_and_dnf_intact(
        self,
    ) -> None:
        world = self.worlds[(True, True)]
        for capability_name, item_name in CAPABILITY_ITEMS.items():
            option_arguments = {
                "randomize_ledge_grab": item_name == LEDGE_GRAB_ITEM,
                "randomize_swim": item_name == SWIM_ITEM,
            }
            conjunction = (
                req(capability_name, "Needolin", crest=False),
            )
            innate_rule = make_requirements_rule(
                conjunction,
                world.player,
            )
            randomized_rule = make_requirements_rule(
                conjunction,
                world.player,
                **option_arguments,
            )

            with self.subTest(item_name=item_name, case="other-only"):
                state = self.collect(world, "Needolin")
                self.assertTrue(innate_rule(state))
                self.assertFalse(randomized_rule(state))

            with self.subTest(item_name=item_name, case="ability-only"):
                state = self.collect(world, item_name)
                self.assertFalse(innate_rule(state))
                self.assertFalse(randomized_rule(state))

            with self.subTest(item_name=item_name, case="both"):
                state = self.collect(world, "Needolin", item_name)
                self.assertTrue(innate_rule(state))
                self.assertTrue(randomized_rule(state))

            with self.subTest(item_name=item_name, case="separate-dnf"):
                dnf_rule = make_requirements_rule(
                    (
                        *conjunction,
                        req("Needle Strike", crest=False),
                    ),
                    world.player,
                    **option_arguments,
                )
                state = self.collect(world, "Needle Strike")
                self.assertTrue(dnf_rule(state))

    def test_non_native_rulebuilder_keeps_capability_additive_and_dnf_intact(
        self,
    ) -> None:
        world = self.worlds[(True, True)]
        for capability_name, item_name in CAPABILITY_ITEMS.items():
            option_arguments = {
                "randomize_ledge_grab": item_name == LEDGE_GRAB_ITEM,
                "randomize_swim": item_name == SWIM_ITEM,
            }
            conjunction = (
                req(capability_name, "Needolin", crest=False),
            )
            randomized_rule = build_requirements_rule(
                conjunction,
                **option_arguments,
            ).resolve(world)

            with self.subTest(item_name=item_name, case="other-only"):
                self.assertFalse(
                    randomized_rule(self.collect(world, "Needolin"))
                )

            with self.subTest(item_name=item_name, case="ability-only"):
                self.assertFalse(
                    randomized_rule(self.collect(world, item_name))
                )

            with self.subTest(item_name=item_name, case="both"):
                self.assertTrue(
                    randomized_rule(
                        self.collect(world, "Needolin", item_name)
                    )
                )

            with self.subTest(item_name=item_name, case="separate-dnf"):
                dnf_rule = build_requirements_rule(
                    (
                        *conjunction,
                        req("Needle Strike", crest=False),
                    ),
                    **option_arguments,
                ).resolve(world)
                self.assertTrue(
                    dnf_rule(self.collect(world, "Needle Strike"))
                )
