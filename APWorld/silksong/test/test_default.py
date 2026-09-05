import importlib
from collections import Counter
from types import SimpleNamespace
from unittest import TestCase, mock

from BaseClasses import CollectionState, LocationProgressType
from rule_builder.rules import Rule
from test.general import setup_multiworld

from .. import LOGIC_UNKNOWN_REGION_NAME, SilksongWorld
from .. import category_fill, rules
from ..act1_scope import ACT_ONE_EXCLUDED_LOCATION_NAMES
from ..act2_scope import (
    ACT_THREE_ONLY_GOAL_LOCATION_NAMES,
    ACT_TWO_POOL_REMOVALS_BY_SOURCE_CATEGORY,
)
from ..items import QUEST_FILLER_COUNTS
from ..locations import (
    COURIER_DELIVERY_WISH_LOCATION_NAMES,
    QUEST_LOCATION_NAMES,
    SCROUNGE_RELIC_ITEM_NAMES,
    location_data_table,
    location_name_groups,
    location_table,
)
from ..requirements import (
    COMPILED_ROOM_GRAPH,
    EVENT_REQUIREMENTS,
    JUNK_ONLY_LOCATIONS,
    LOGIC_UNKNOWN_LOCATIONS,
    POLLIP_HEART_SOURCE_LOCATION_NAMES,
    PROGRESSION_SAFE_QUEST_LOCATIONS,
    REQUIREMENTS,
    RITE_OF_REBIRTH_EVENT,
    ROOM_GRAPH_PROMOTED_FALLBACK_LOCATIONS,
    ROOM_GRAPH_QUARANTINED_CHECK_NAMES,
    SPOOL_FRAGMENT_ITEM_NAMES,
    _compiled_room_clause_requirement,
)
from ..room_graph_logic import (
    CompiledRoomClause,
    SPOOL_FRAGMENT_COUNT_ITEM,
)
from ..requirement_rules import _compile_item_count
from ..wish_events import (
    SILK_AND_SOUL_LACE_DEFEATED_ITEM,
    SILK_AND_SOUL_MANDATORY_WISH_ITEM,
    WIDOW_DEFEATED_EVENT_ITEM,
    SILK_AND_SOUL_WISH_HALF_POINT_ITEM,
    SILK_AND_SOUL_WISH_POINT_ITEM,
    SONGCLAVE_BOARD_COMPLETION_ITEM,
    WISH_LOGIC_EVENTS,
)
from .bases import SilksongTestBase


CLAWLINE_ITEM = "Clawline"
DEEP_DOCKS_MINES_FLEA = "Flea: Deep Docks - Mines"


class TestDefaultWorld(SilksongTestBase):
    # Naming the default goal opts this class into WorldTestBase's generic
    # generation, fill, all-state and empty-state checks.
    options = {"goal": "act_3"}

    def test_native_topology_records_path_breadcrumbs(self) -> None:
        self.assertTrue(self.world.topology_present)
        self.assertTrue(
            all(
                not entrance.hide_path
                for entrance in self.world.get_entrances()
            )
        )
        state = self.multiworld.get_all_state(False)
        location = self.world.get_location("Faydown Cloak")

        self.assertTrue(location.can_reach(state))
        path_value = state.path.get(location.parent_region)
        self.assertTrue(path_value)
        path_names = []
        while path_value:
            name, path_value = path_value
            path_names.append(str(name))
        path_names.reverse()
        entrances = [
            self.world.get_entrance(name)
            for name in path_names[1::2]
        ]
        self.assertTrue(entrances)
        self.assertEqual(entrances[0].parent_region.name, "Menu")
        self.assertIs(
            entrances[-1].connected_region,
            location.parent_region,
        )
        for first, second in zip(entrances, entrances[1:]):
            self.assertIs(first.connected_region, second.parent_region)

    def test_world_leaves_universal_tracker_hooks_unoverridden(self) -> None:
        for hook_name in (
            "get_logical_path",
            "explain_path",
            "explain_spot",
            "explain_rule",
        ):
            with self.subTest(hook_name=hook_name):
                self.assertFalse(hasattr(self.world, hook_name))

    def test_native_path_rules_support_rule_builder_explanations(self) -> None:
        state = self.multiworld.get_all_state(False)
        location = self.world.get_location("Silkspear")
        path_value = state.path[location.parent_region]
        path_names = []
        while path_value:
            name, path_value = path_value
            path_names.append(str(name))
        path_names.reverse()
        entrances = [
            self.world.get_entrance(name)
            for name in path_names[1::2]
        ]

        self.assertTrue(entrances)
        self.assertTrue(
            hasattr(location.access_rule, "explain_json")
        )
        self.assertTrue(location.access_rule.explain_json(state))
        for entrance in entrances:
            with self.subTest(entrance=entrance.name):
                self.assertTrue(
                    hasattr(entrance.access_rule, "explain_json")
                )
                self.assertTrue(entrance.access_rule.explain_json(state))

    def test_shellwood_only_weaver_harp_requires_needolin(self) -> None:
        state = CollectionState(self.multiworld)
        self.collect_all_but("Needolin", state)

        self.assertTrue(
            self.multiworld.get_location("Cling Grip", self.player).can_reach(state)
        )
        self.assertTrue(
            self.multiworld.get_location(
                "Shellwood - Pollip Heart #3",
                self.player,
            ).can_reach(state)
        )
        inscription = self.multiworld.get_location(
            "Shellwood - Weaver Harp Inscription",
            self.player,
        )
        self.assertFalse(inscription.can_reach(state))

        state.collect(self.get_item_by_name("Needolin"))
        self.assertTrue(inscription.can_reach(state))

    def test_forge_daughter_checks_are_mapped(self) -> None:
        state = self.multiworld.get_all_state(False)
        names = (
            "Silkshot (Forge Daughter)",
            "Sting Shard",
            "Magma Bell",
            "Forge Daughter - Crafting Kit",
        )

        for name in names:
            with self.subTest(name=name):
                self.assertNotIn(name, LOGIC_UNKNOWN_LOCATIONS)
                self.assertTrue(
                    self.multiworld.get_location(name, self.player).can_reach(state)
                )

    def test_cogwork_core_pristine_core_requires_act_three(self) -> None:
        name = "Cogwork Core - Pristine Core"
        requirements = REQUIREMENTS[name]
        arena_event = (
            "Room Event: cogwork-core/"
            "cogwork-core-east-silk-spool-gauntlet/beat-arena"
        )

        self.assertTrue(requirements)
        self.assertTrue(all(
            {"Act: 3", arena_event} <= set(requirement.all_of)
            for requirement in requirements
        ))
        self.assertIn(name, ACT_ONE_EXCLUDED_LOCATION_NAMES)
        self.assertIn(name, ACT_THREE_ONLY_GOAL_LOCATION_NAMES)
        self.assertEqual(
            {"Pristine Core": 1},
            ACT_TWO_POOL_REMOVALS_BY_SOURCE_CATEGORY[
                "Resource:pristine_core"
            ],
        )

    def test_far_fields_attic_caches_share_movement_gate(self) -> None:
        source = (
            "Room Node: far-fields/far-fields-pinstress-attic"
            "#bottom-left-area"
        )
        target = (
            "Room Node: far-fields/far-fields-pinstress-attic"
            "#upper-right-area"
        )
        clauses = tuple(
            clause
            for clause in COMPILED_ROOM_GRAPH.node_requirements[target]
            if source in clause.all_of
        )
        signatures = {
            frozenset(set(clause.all_of) - {source})
            for clause in clauses
        }

        self.assertEqual(
            {
                frozenset({
                    "Ability: Drifter's Cloak",
                    "Ancestral Art: Cling Grip",
                }),
                frozenset({
                    "Ability: Drifter's Cloak",
                    "Usable Scuttlebrace",
                    "Swift Step",
                }),
                frozenset({"Ancestral Art: Silk Soar"}),
            },
            signatures,
        )
        for name in (
            "Far Fields - Rosary Cache #3",
            "Far Fields - Rosary Cache #4",
        ):
            requirements = REQUIREMENTS[name]
            self.assertTrue(requirements)
            self.assertTrue(all(
                target in requirement.all_of
                for requirement in requirements
            ))


    def test_randomized_scrounge_relics_are_progression(self) -> None:
        for item_name in SCROUNGE_RELIC_ITEM_NAMES:
            with self.subTest(item_name=item_name):
                self.assertTrue(self.world.create_item(item_name).advancement)

    def test_deep_docks_mines_flea_requires_clawline(self) -> None:
        location = self.multiworld.get_location(
            DEEP_DOCKS_MINES_FLEA,
            self.player,
        )

        self.assertIsInstance(location.access_rule, Rule.Resolved)
        self.assertIn(
            CLAWLINE_ITEM,
            location.access_rule.item_dependencies(),
        )

        state = CollectionState(self.multiworld)
        self.collect_all_but(CLAWLINE_ITEM, state)
        self.assertFalse(location.can_reach(state))

        for item in self.get_items_by_name(CLAWLINE_ITEM):
            state.collect(item)
        self.assertTrue(location.can_reach(state))

    def test_high_halls_safeguards_require_clawline(self) -> None:
        locations = tuple(
            self.multiworld.get_location(name, self.player)
            for name in (
                "Cogfly",
                "High Halls - Shell Shard Cache #2",
            )
        )

        state = CollectionState(self.multiworld)
        self.collect_all_but(CLAWLINE_ITEM, state)
        for location in locations:
            with self.subTest(name=location.name, has_clawline=False):
                self.assertFalse(location.can_reach(state))

        for item in self.get_items_by_name(CLAWLINE_ITEM):
            state.collect(item)
        for location in locations:
            with self.subTest(name=location.name, has_clawline=True):
                self.assertTrue(location.can_reach(state))

    def test_diving_bell_and_void_require_act_three(self) -> None:
        state = CollectionState(self.multiworld)
        self.collect_all_but("Snare Setter", state)

        self.assertTrue(state.has("Silk Soar", self.player))
        self.assertTrue(
            state.can_reach_region(
                "Path: Deep Docks - Sauna",
                self.player,
            )
        )
        self.assertFalse(state.can_reach_region("Act: 3", self.player))
        self.assertFalse(
            state.can_reach_region(
                "Path: Deep Docks - Diving Bell",
                self.player,
            )
        )
        self.assertFalse(
            state.can_reach_region(
                "Path: Abyss - Void Tendrils",
                self.player,
            )
        )

        for item in self.get_items_by_name("Snare Setter"):
            state.collect(item)

        self.assertTrue(state.can_reach_region("Act: 3", self.player))
        self.assertTrue(
            state.can_reach_region(
                "Path: Deep Docks - Diving Bell",
                self.player,
            )
        )
        self.assertTrue(
            state.can_reach_region(
                "Path: Abyss - Void Tendrils",
                self.player,
            )
        )

    def test_logic_unknown_locations_unlock_after_victory(self) -> None:
        locations = [
            location
            for location in self.multiworld.get_locations(self.player)
            if location.name in LOGIC_UNKNOWN_LOCATIONS
        ]

        self.assertTrue(locations)
        self.assertTrue(
            all(
                location.parent_region.name == LOGIC_UNKNOWN_REGION_NAME
                for location in locations
            )
        )

        state = CollectionState(self.multiworld)
        self.assertTrue(
            all(not location.can_reach(state) for location in locations)
        )

        state.collect(self.world.create_event("Victory"))
        self.assertTrue(
            all(location.can_reach(state) for location in locations)
        )

    def test_unresolved_mapper_checks_do_not_use_weak_fallbacks(self) -> None:
        unsafe_advancement_fallbacks = {
            "Blasted Steps - Mask Shard",
            "Underworks - Memory Locket",
        }
        self.assertLessEqual(
            unsafe_advancement_fallbacks,
            LOGIC_UNKNOWN_LOCATIONS,
        )
        self.assertLessEqual(
            {
                "Boss: Gurr the Outcast",
                "Far Fields - Rosary Cache #19",
                "Underworks - Shell Shard Cache #13",
            },
            LOGIC_UNKNOWN_LOCATIONS,
        )
        self.assertEqual(
            ROOM_GRAPH_PROMOTED_FALLBACK_LOCATIONS
            & ROOM_GRAPH_QUARANTINED_CHECK_NAMES,
            {
                "Boss: Voltvyrm",
                "Ecstasy of the End - Pale Oil",
                "Egg of Flealia",
                "Fleatopia - Tool Pouch",
                "Greymoor - Bellshrine",
                "Longclaw",
                "Volt Filament",
                "Wish: Passing of the Age",
            },
        )

    def test_pinmaster_oil_is_mapped_but_junk_only(self) -> None:
        name = "Wish: Pinmaster's Oil"
        location = self.multiworld.get_location(name, self.player)

        self.assertIn(name, JUNK_ONLY_LOCATIONS)
        self.assertNotIn(name, LOGIC_UNKNOWN_LOCATIONS)
        self.assertNotEqual(location.parent_region.name, LOGIC_UNKNOWN_REGION_NAME)
        self.assertFalse(location.item_rule(self.world.create_item("Cling Grip")))

    def test_native_entrances_keep_rule_builder_explanations(self) -> None:
        entrances = [
            entrance
            for region in self.multiworld.get_regions(self.player)
            for entrance in region.exits
            if entrance.name.startswith("Silksong Logic: ")
        ]
        always_true_entrances = [
            entrance
            for entrance in entrances
            if isinstance(entrance.access_rule, Rule.Resolved)
            and entrance.access_rule.always_true
        ]

        self.assertTrue(entrances)
        self.assertTrue(always_true_entrances)
        self.assertTrue(all(
            hasattr(entrance.access_rule, "explain_json")
            for entrance in entrances
        ))
        state = CollectionState(self.multiworld)
        for entrance in always_true_entrances:
            with self.subTest(entrance=entrance.name):
                self.assertIn(
                    "True",
                    str(entrance.access_rule.explain_json(state)),
                )

    def test_courier_deliveries_are_not_quest_sanity(self) -> None:
        reserved_ids = {
            'Wish: Bone Bottom Supplies': 835761,
            "Wish: Queen's Egg": 835762,
            "Wish: Survivor's Camp Supplies": 835763,
            'Wish: Fleatopia Supplies': 835764,
            'Wish: Liquid Lacquer': 835765,
            "Wish: Pilgrim's Rest Supplies": 835766,
            'Wish: Songclave Supplies': 835767,
        }
        rescue_ids = {
            'Wish: My Missing Courier': 835778,
            'Wish: My Missing Brother': 835779,
        }

        self.assertEqual(
            COURIER_DELIVERY_WISH_LOCATION_NAMES,
            frozenset(reserved_ids),
        )
        for name, location_id in reserved_ids.items():
            with self.subTest(name=name):
                self.assertEqual(location_table[name], location_id)
                self.assertNotIn(name, location_data_table)
                self.assertNotIn(name, QUEST_LOCATION_NAMES)
                self.assertNotIn(name, location_name_groups['Quests'])
                self.assertNotIn(name, location_name_groups['Quest'])
                self.assertIn(name, REQUIREMENTS)
                with self.assertRaises(KeyError):
                    self.multiworld.get_location(name, self.player)

        for name, location_id in rescue_ids.items():
            with self.subTest(name=name):
                self.assertEqual(location_table[name], location_id)
                self.assertIn(name, location_data_table)
                self.assertIn(name, QUEST_LOCATION_NAMES)
                self.assertIn(name, location_name_groups['Quests'])
                self.multiworld.get_location(name, self.player)

        self.assertEqual(sum(QUEST_FILLER_COUNTS.values()), 25)
        self.assertEqual(len(QUEST_LOCATION_NAMES), 25)
        self.assertEqual(len(location_name_groups['Quests']), 25)

    def test_queens_egg_keeps_its_courier_chain_and_destination(self) -> None:
        location_name = "Wish: Queen's Egg"
        self.assertIn(location_name, PROGRESSION_SAFE_QUEST_LOCATIONS)
        self.assertNotIn(location_name, LOGIC_UNKNOWN_LOCATIONS)
        self.assertNotIn(location_name, JUNK_ONLY_LOCATIONS)

        other_delivery_routes = EVENT_REQUIREMENTS[
            'Event: Other Courier Delivery Completed'
        ]
        self.assertEqual(6, len(other_delivery_routes))

        queen_routes = EVENT_REQUIREMENTS["Event: Queen's Egg Delivered"]
        self.assertEqual(1, len(queen_routes))
        self.assertEqual(
            {
                'Path: Bellhart - Bellhart',
                'Event: Missing Brother Rescued',
                'Event: Other Courier Delivery Completed',
                'Room Node: '
                'sinner-s-road/sinner-s-road-styx-room#right',
            },
            set(queen_routes[0].all_of),
        )


class TestQuestSanityShuffle(SilksongTestBase):
    options = {
        'goal': 'act_3',
        'quest_sanity': 'shuffle',
    }

    def test_courier_deliveries_stay_out_of_shuffle(self) -> None:
        for name in COURIER_DELIVERY_WISH_LOCATION_NAMES:
            with self.subTest(name=name):
                with self.assertRaises(KeyError):
                    self.multiworld.get_location(name, self.player)
        self.multiworld.get_location(
            'Wish: My Missing Courier',
            self.player,
        )
        self.multiworld.get_location(
            'Wish: My Missing Brother',
            self.player,
        )


class TestActOneQuestSanityAnywhere(SilksongTestBase):
    options = {
        'goal': 'act_1',
        'quest_sanity': 'anywhere',
    }

    def test_courier_deliveries_stay_out_of_act_one(self) -> None:
        for name in COURIER_DELIVERY_WISH_LOCATION_NAMES:
            with self.subTest(name=name):
                with self.assertRaises(KeyError):
                    self.multiworld.get_location(name, self.player)

    def test_pollip_quest_stays_in_act_one(self) -> None:
        state = self.multiworld.get_all_state(False)
        for name in (*POLLIP_HEART_SOURCE_LOCATION_NAMES, "Pollip Pouch"):
            with self.subTest(name=name):
                self.assertTrue(
                    self.multiworld.get_location(
                        name,
                        self.player,
                    ).can_reach(state)
                )

    def test_missing_courier_stays_in_act_one(self) -> None:
        location = self.multiworld.get_location(
            'Wish: My Missing Courier',
            self.player,
        )
        self.assertTrue(
            location.can_reach(self.multiworld.get_all_state(False))
        )

    def test_confirmed_skip_routes_stay_out_without_skips(self) -> None:
        for name in (
            "Flea: Sinner's Road",
            "Sinner's Road - Shard Bundle",
            "Bilewater - Rosary Cache #1",
            "Bilewater - Rosary Cache #2",
            "Sinner's Road - Rosary Cache #5",
            "Sinner's Road - Rosary Cache #6",
            "Sinner's Road - Rosary Cache #7",
            "Deep Docks - Rosary Cache #1",
            "Deep Docks - Rosary Cache #2",
        ):
            with self.subTest(name=name):
                with self.assertRaises(KeyError):
                    self.multiworld.get_location(name, self.player)

    def test_citadel_checks_stay_out_of_act_one(self) -> None:
        prefixes = (
            "choral-chambers/",
            "cogwork-core/",
            "underworks/",
            "grand-gate/",
        )
        for location in self.multiworld.get_locations(self.player):
            source_ids = COMPILED_ROOM_GRAPH.check_source_ids.get(
                location.name,
                (),
            )
            with self.subTest(name=location.name):
                self.assertFalse(
                    any(
                        source_id.startswith(prefixes)
                        for source_id in source_ids
                    )
                )

    def test_craw_lake_checks_stay_in_act_one(self) -> None:
        state = self.multiworld.get_all_state(False)
        names = (
            "Flea: Greymoor - Craw Lake",
            "Greymoor - Frayed Rosary String #1",
            "Greymoor - Rosary Cache #18",
            "Greymoor - Rosary Cache #19",
            "Greymoor - Rosary Cache #21",
            "Greymoor - Rosary Cache #22",
        )
        for name in names:
            with self.subTest(name=name):
                self.assertTrue(
                    self.multiworld.get_location(
                        name,
                        self.player,
                    ).can_reach(state)
                )

        for name in (
            "Greymoor - Rosary Cache #2",
            "Greymoor - Rosary Cache #3",
            "Greymoor - Rosary Cache #20",
        ):
            with self.subTest(name=name):
                with self.assertRaises(KeyError):
                    self.multiworld.get_location(
                        name,
                        self.player,
                    )


class TestActOneMultibinderAnywhere(SilksongTestBase):
    options = {
        'goal': 'act_1',
        'tool_randomization': 'anywhere',
    }

    def test_multibinder_stays_randomized(self) -> None:
        self.multiworld.get_location('Multibinder', self.player)
        self.assertEqual(
            1,
            sum(
                item.name == 'Multibinder'
                for item in self.multiworld.itempool
            ),
        )


class TestActOneEasySkipGreymoor(SilksongTestBase):
    options = {
        'goal': 'act_1',
        'quest_sanity': 'anywhere',
        'skips': 'easy',
    }

    def test_easy_skip_caches_stay_in_act_one(self) -> None:
        state = self.multiworld.get_all_state(False)
        for name in (
            "Greymoor - Rosary Cache #2",
            "Greymoor - Rosary Cache #3",
        ):
            with self.subTest(name=name):
                self.assertTrue(
                    self.multiworld.get_location(
                        name,
                        self.player,
                    ).can_reach(state)
                )

        with self.assertRaises(KeyError):
            self.multiworld.get_location(
                "Greymoor - Rosary Cache #20",
                self.player,
            )

    def test_confirmed_easy_skip_routes_stay_in_act_one(self) -> None:
        state = self.multiworld.get_all_state(False)
        for name in (
            "Flea: Sinner's Road",
            "Sinner's Road - Shard Bundle",
            "Bilewater - Rosary Cache #1",
            "Bilewater - Rosary Cache #2",
            "Sinner's Road - Rosary Cache #5",
            "Sinner's Road - Rosary Cache #6",
            "Sinner's Road - Rosary Cache #7",
        ):
            with self.subTest(name=name):
                self.assertTrue(
                    self.multiworld.get_location(
                        name,
                        self.player,
                    ).can_reach(state)
                )


class TestCrestSlotPostFillPerformance(TestCase):
    def test_progression_item_is_evaluated_once_per_repair(self) -> None:
        progression_item = SimpleNamespace(
            name="Progression",
            player=1,
            advancement=True,
        )

        def make_location(name, item):
            location = mock.Mock()
            location.name = name
            location.player = 1
            location.address = 1
            location.item = item
            location.locked = False
            location.can_reach.return_value = True
            location.can_fill.return_value = True
            return location

        slot = make_location("Crest Slot", progression_item)
        replacements = [
            make_location(
                name,
                SimpleNamespace(
                    name=f"Filler {name}",
                    player=1,
                    advancement=False,
                ),
            )
            for name in ("C", "A", "B")
        ]
        multiworld = SimpleNamespace(
            get_filled_locations=lambda: (slot, *replacements),
        )
        world = SimpleNamespace(player=1)
        initial_state = mock.Mock()
        initial_state.count.return_value = 0
        candidate_state = mock.Mock()
        candidate_state.count.side_effect = lambda name, player: int(
            name == progression_item.name and player == progression_item.player
        )
        initial_state.copy.return_value = candidate_state
        world_module = importlib.import_module(SilksongWorld.__module__)

        def swap_items(first, second):
            first.item, second.item = second.item, first.item

        with (
            mock.patch.object(
                world_module,
                "get_crest_slot_memory_locket_count",
                return_value=1,
            ),
            mock.patch(
                "Fill.sweep_from_pool",
                return_value=candidate_state,
            ) as sweep_from_pool,
            mock.patch(
                "Fill.swap_location_item",
                side_effect=swap_items,
            ),
        ):
            result = SilksongWorld._repair_crest_slot_locket_budget(
                multiworld,
                world,
                {slot},
                initial_state,
            )

        self.assertIs(result, candidate_state)
        self.assertEqual(slot.item.name, "Filler A")
        self.assertEqual(sweep_from_pool.call_count, 1)


class TestVogHintPerformance(TestCase):
    class ReachableCollectionState:
        def __init__(self, multiworld):
            self.multiworld = multiworld

        def can_reach(self, _location):
            return True

        def collect(self, _item, _event, _location):
            return None

        def copy(self):
            return self

    @staticmethod
    def _progression_locations(count):
        locations = []
        for index in range(count):
            item = SimpleNamespace(
                advancement=True,
                player=1,
                name=f"Item {index:02}",
            )
            location = mock.Mock()
            location.player = 1
            location.name = f"Location {index:02}"
            location.item = item
            locations.append(location)
        return locations

    @staticmethod
    def _vog_world(counts):
        world = SilksongWorld.__new__(SilksongWorld)
        world.player = 1
        world.multiworld = SimpleNamespace()
        world.get_vog_hint_counts = lambda: counts
        return world

    def test_foolish_only_finalize_skips_playthrough_pruning(self) -> None:
        world = SimpleNamespace(
            game=SilksongWorld.game,
            get_vog_hint_counts=lambda: (0, 5, 0),
            uses_vog_playthrough_hints=lambda: False,
            build_vog_hint_plan=mock.Mock(),
        )
        multiworld = SimpleNamespace(
            player_ids=(1,),
            worlds={1: world},
        )
        world_module = importlib.import_module(SilksongWorld.__module__)

        with mock.patch.object(
            world_module,
            "find_required_playthrough_locations",
        ) as find_required:
            SilksongWorld.stage_finalize_multiworld(multiworld)

        find_required.assert_not_called()
        world.build_vog_hint_plan.assert_called_once_with()

    def test_foolish_only_plan_skips_fallback_pruning(self) -> None:
        world = self._vog_world((0, 5, 0))
        expected = {
            "woth_areas": [],
            "foolish_areas": ["Shellwood"],
            "general_locations": [],
            "general_count": 0,
        }
        world_module = importlib.import_module(SilksongWorld.__module__)

        with (
            mock.patch.object(
                world_module,
                "find_required_playthrough_locations",
            ) as find_required,
            mock.patch.object(
                world_module,
                "build_vog_hint_plan",
                return_value=expected,
            ) as build_plan,
        ):
            result = SilksongWorld.build_vog_hint_plan(world)

        find_required.assert_not_called()
        required_locations = build_plan.call_args.args[1]
        self.assertFalse(required_locations)
        self.assertEqual(result, expected)

    def test_woth_finalize_still_prunes_once(self) -> None:
        foolish_world = SimpleNamespace(
            game=SilksongWorld.game,
            get_vog_hint_counts=lambda: (0, 5, 0),
            uses_vog_playthrough_hints=lambda: False,
            build_vog_hint_plan=mock.Mock(),
        )
        woth_world = SimpleNamespace(
            game=SilksongWorld.game,
            get_vog_hint_counts=lambda: (5, 0, 0),
            uses_vog_playthrough_hints=lambda: True,
            build_vog_hint_plan=mock.Mock(),
        )
        multiworld = SimpleNamespace(
            player_ids=(1, 2),
            worlds={1: foolish_world, 2: woth_world},
        )
        required_locations = frozenset((object(),))
        world_module = importlib.import_module(SilksongWorld.__module__)

        with mock.patch.object(
            world_module,
            "find_required_playthrough_locations",
            return_value=required_locations,
        ) as find_required:
            SilksongWorld.stage_finalize_multiworld(multiworld)

        find_required.assert_called_once_with(multiworld)
        self.assertIs(
            multiworld._silksong_vog_required_locations,
            required_locations,
        )
        foolish_world.build_vog_hint_plan.assert_called_once_with()
        woth_world.build_vog_hint_plan.assert_called_once_with()

    def test_playthrough_pruning_batches_removable_locations(self) -> None:
        locations = self._progression_locations(33)
        multiworld = SimpleNamespace(
            get_filled_locations=lambda: locations,
            has_beaten_game=lambda _state: True,
            can_beat_game=mock.Mock(return_value=True),
        )
        vog_module = importlib.import_module("worlds.silksong.vog_hints")

        with mock.patch.object(
            vog_module,
            "CollectionState",
            self.ReachableCollectionState,
        ):
            required = vog_module.find_required_playthrough_locations(
                multiworld
            )

        self.assertEqual(required, frozenset())
        self.assertEqual(multiworld.can_beat_game.call_count, 3)

    def test_failed_batch_keeps_sequential_pruning_result(self) -> None:
        first, second = self._progression_locations(2)

        def can_beat_game(_state, required_locations):
            return first in required_locations or second in required_locations

        multiworld = SimpleNamespace(
            get_filled_locations=lambda: (first, second),
            has_beaten_game=lambda _state: True,
            can_beat_game=mock.Mock(side_effect=can_beat_game),
        )
        vog_module = importlib.import_module("worlds.silksong.vog_hints")

        with mock.patch.object(
            vog_module,
            "CollectionState",
            self.ReachableCollectionState,
        ):
            required = vog_module.find_required_playthrough_locations(
                multiworld
            )

        self.assertEqual(required, frozenset((second,)))
        self.assertEqual(multiworld.can_beat_game.call_count, 3)


class TestShufflePerformance(TestCase):
    @staticmethod
    def _multiworld(locations):
        full_accessibility = SimpleNamespace(
            value=0,
            option_minimal=2,
        )
        minimal_accessibility = SimpleNamespace(
            value=2,
            option_minimal=2,
        )
        return SimpleNamespace(
            itempool=[
                SimpleNamespace(
                    silksong_placement_category="Skill",
                    advancement=False,
                )
            ],
            player_ids=[1, 2],
            worlds={
                1: SimpleNamespace(
                    options=SimpleNamespace(
                        accessibility=full_accessibility,
                    )
                ),
                2: SimpleNamespace(
                    options=SimpleNamespace(
                        accessibility=minimal_accessibility,
                    )
                ),
            },
            get_locations=lambda: locations,
        )

    @staticmethod
    def _item(
        name,
        player,
        category="Skill",
        advancement=True,
    ):
        return SimpleNamespace(
            name=name,
            player=player,
            advancement=advancement,
            silksong_placement_category=category,
        )

    def test_shuffle_rules_restore_original_item_rules(self) -> None:
        original_rule = lambda item: item.name != "Blocked"
        location = SimpleNamespace(
            player=2,
            game=SilksongWorld.game,
            silksong_placement_category="Skill",
            item_rule=original_rule,
        )
        multiworld = self._multiworld([location])

        scope = rules.enforce_global_shuffle_item_rules(multiworld)
        self.assertFalse(location.item_rule(self._item("Remote", 1)))
        self.assertTrue(location.item_rule(self._item("Local", 2)))
        self.assertFalse(
            location.item_rule(
                self._item("Wrong lane", 2, category="Map")
            )
        )
        self.assertFalse(
            location.item_rule(
                self._item(
                    "Blocked",
                    2,
                    category=None,
                    advancement=False,
                )
            )
        )

        rules.restore_global_shuffle_item_rules(scope)

        self.assertIs(location.item_rule, original_rule)
        self.assertTrue(location.item_rule(self._item("Remote", 1)))

    def test_two_player_category_shuffles_keep_local_ownership(self) -> None:
        options = {
            "skill_randomization": "shuffle",
            "bellway_randomization": "shuffle",
            "ventrica_randomization": "shuffle",
            "map_randomization": "shuffle",
            "relic_randomization": "shuffle",
        }
        baseline_skill_layouts = {}
        original_accessibility_check = category_fill._shuffle_is_accessible

        def capture_baseline(
            multiworld,
            locations,
            players,
            reachability_context,
        ):
            if not baseline_skill_layouts:
                for player in players:
                    baseline_skill_layouts[player] = {
                        location.name: location.item.name
                        for location in multiworld.get_filled_locations(player)
                        if getattr(
                            location,
                            "silksong_placement_category",
                            None,
                        ) == "Skill"
                    }
            return original_accessibility_check(
                multiworld,
                locations,
                players,
                reachability_context,
            )

        with mock.patch.object(
            category_fill,
            "_shuffle_is_accessible",
            side_effect=capture_baseline,
        ):
            multiworld = setup_multiworld(
                [SilksongWorld, SilksongWorld],
                seed=82026,
                options=options,
            )
        shuffled_locations = [
            location
            for location in multiworld.get_filled_locations()
            if getattr(
                location,
                "silksong_placement_category",
                None,
            ) is not None
        ]

        self.assertTrue(shuffled_locations)
        self.assertEqual(set(baseline_skill_layouts), {1, 2})
        for layout in baseline_skill_layouts.values():
            self.assertEqual(layout["Clawline"], "Rosaries (60)")
            self.assertEqual(layout["Item: Quill"], "Swift Step")
            self.assertEqual(layout["Swift Step"], "Cling Grip")
            self.assertEqual(layout["Cling Grip"], "Clawline")
            self.assertEqual(layout["Silk Soar"], "Silk Soar")
        lane_location_counts = {}
        lane_item_counts = {}
        for location in shuffled_locations:
            with self.subTest(
                player=location.player,
                location=location.name,
            ):
                self.assertEqual(location.item.player, location.player)
                self.assertEqual(
                    location.item.silksong_placement_category,
                    location.silksong_placement_category,
                )
            location_lane = (
                location.player,
                location.silksong_placement_category,
            )
            item_lane = (
                location.item.player,
                location.item.silksong_placement_category,
            )
            lane_location_counts[location_lane] = (
                lane_location_counts.get(location_lane, 0) + 1
            )
            lane_item_counts[item_lane] = (
                lane_item_counts.get(item_lane, 0) + 1
            )

        self.assertEqual(lane_item_counts, lane_location_counts)
        all_state = multiworld.get_all_state(False)
        for player in multiworld.player_ids:
            self.assertTrue(multiworld.has_beaten_game(all_state, player))
            self.assertFalse(any(
                item.name == "Clawline"
                for item in multiworld.precollected_items[player]
            ))
            clawline_location = next(
                location
                for location in shuffled_locations
                if (
                    location.item.player == player
                    and location.item.name == "Clawline"
                )
            )
            self.assertNotIn(
                clawline_location.name,
                LOGIC_UNKNOWN_LOCATIONS,
            )
            self.assertTrue(clawline_location.can_reach(all_state))

        self.assertIn("Clawline", LOGIC_UNKNOWN_LOCATIONS)
        for location in shuffled_locations:
            if location.progress_type == LocationProgressType.EXCLUDED:
                continue
            self.assertTrue(location.can_reach(all_state))

    def test_shuffle_rule_cleanup_preserves_later_wrappers(self) -> None:
        original_rule = lambda _item: True
        location = SimpleNamespace(
            player=2,
            game=SilksongWorld.game,
            silksong_placement_category="Skill",
            item_rule=original_rule,
        )
        scope = rules.enforce_global_shuffle_item_rules(
            self._multiworld([location])
        )
        installed_rule = location.item_rule
        location.item_rule = lambda item: (
            item.name != "Outer blocked"
            and installed_rule(item)
        )

        rules.restore_global_shuffle_item_rules(scope)

        self.assertTrue(location.item_rule(self._item("Remote", 1)))
        self.assertFalse(
            location.item_rule(self._item("Outer blocked", 1))
        )

    def test_reachability_context_collects_pool_once(self) -> None:
        state = object()
        itempool = (object(),)
        filled_location = object()
        pool_state = object()
        maximum_state_one = object()
        maximum_state_two = object()
        multiworld = SimpleNamespace(
            state=state,
            itempool=itempool,
            player_ids=[],
            worlds={},
            get_filled_locations=lambda: [filled_location],
            has_beaten_game=lambda _state, _player: False,
        )

        with mock.patch(
            "Fill.sweep_from_pool",
            side_effect=[
                pool_state,
                maximum_state_one,
                maximum_state_two,
            ],
        ) as sweep_from_pool:
            context = category_fill._build_shuffle_reachability_context(
                multiworld
            )
            for _ in range(2):
                self.assertEqual(
                    category_fill._shuffle_reachability_failures(
                        multiworld,
                        (),
                        (),
                        context,
                    ),
                    ([], []),
                )

        self.assertEqual(
            sweep_from_pool.call_args_list,
            [
                mock.call(state, itempool, locations=()),
                mock.call(pool_state, locations=(filled_location,)),
                mock.call(pool_state, locations=(filled_location,)),
            ],
        )

    def test_stage_pre_fill_restores_rules_on_failure(self) -> None:
        original_rule = lambda _item: True
        location = SimpleNamespace(
            player=1,
            game=SilksongWorld.game,
            silksong_placement_category="Skill",
            item_rule=original_rule,
        )
        multiworld = self._multiworld([location])
        SilksongWorld.stage_generate_basic(multiworld)
        self.assertIsNot(location.item_rule, original_rule)
        world_module = importlib.import_module(SilksongWorld.__module__)

        with (
            mock.patch.object(
                world_module,
                "prefill_category_shuffles",
                side_effect=RuntimeError("failure"),
            ),
            self.assertRaisesRegex(RuntimeError, "failure"),
        ):
            SilksongWorld.stage_pre_fill(multiworld)

        self.assertIs(location.item_rule, original_rule)
        self.assertFalse(
            hasattr(
                multiworld,
                "_silksong_shuffle_item_rule_scope",
            )
        )


    def test_shuffle_scope_composes_with_locality_and_exclusions(
        self,
    ) -> None:
        def locality_and_exclusion_rule(item):
            return item.player == 1 and item.name != "Excluded"

        location = SimpleNamespace(
            player=1,
            game=SilksongWorld.game,
            silksong_placement_category="Skill",
            item_rule=locality_and_exclusion_rule,
        )
        scope = rules.enforce_global_shuffle_item_rules(
            self._multiworld([location])
        )

        self.assertTrue(location.item_rule(self._item("Local", 1)))
        self.assertFalse(location.item_rule(self._item("Remote", 2)))
        self.assertFalse(location.item_rule(self._item("Excluded", 1)))
        self.assertFalse(
            location.item_rule(
                self._item("Wrong lane", 1, category="Map")
            )
        )
        self.assertFalse(
            location.item_rule(
                self._item(
                    "Anywhere remote",
                    2,
                    category=None,
                    advancement=False,
                )
            )
        )

        rules.restore_global_shuffle_item_rules(scope)

        self.assertIs(location.item_rule, locality_and_exclusion_rule)

    def test_plando_checks_see_shuffle_scope_until_pre_fill(
        self,
    ) -> None:
        original_rule = lambda _item: True
        location = SimpleNamespace(
            player=1,
            game=SilksongWorld.game,
            silksong_placement_category="Skill",
            item_rule=original_rule,
            item=None,
        )
        multiworld = self._multiworld([location])
        SilksongWorld.stage_generate_basic(multiworld)
        legal_plando = self._item(
            "Legal local Skill",
            1,
            advancement=False,
        )
        remote_plando = self._item(
            "Illegal cross-player Skill",
            2,
            advancement=False,
        )
        illegal_plando = self._item(
            "Illegal Map",
            1,
            category="Map",
            advancement=False,
        )

        self.assertTrue(location.item_rule(legal_plando))
        self.assertFalse(location.item_rule(remote_plando))
        self.assertFalse(location.item_rule(illegal_plando))
        location.item = legal_plando
        world_module = importlib.import_module(SilksongWorld.__module__)

        with mock.patch.object(
            world_module,
            "prefill_category_shuffles",
        ):
            SilksongWorld.stage_pre_fill(multiworld)

        self.assertIs(location.item, legal_plando)
        self.assertIs(location.item_rule, original_rule)

    def test_zero_shuffle_skips_global_rule_scope(self) -> None:
        original_rule = lambda _item: True
        location = SimpleNamespace(
            player=1,
            game=SilksongWorld.game,
            silksong_placement_category=None,
            item_rule=original_rule,
        )
        multiworld = self._multiworld([location])
        multiworld.itempool = [
            SimpleNamespace(silksong_placement_category=None)
        ]

        SilksongWorld.stage_generate_basic(multiworld)

        self.assertIs(location.item_rule, original_rule)
        self.assertIsNone(
            multiworld._silksong_shuffle_item_rule_scope
        )

    def test_stage_pre_fill_restores_rules_on_success(self) -> None:
        original_rule = lambda _item: True
        location = SimpleNamespace(
            player=1,
            game=SilksongWorld.game,
            silksong_placement_category="Skill",
            item_rule=original_rule,
        )
        multiworld = self._multiworld([location])
        SilksongWorld.stage_generate_basic(multiworld)
        self.assertIsNot(location.item_rule, original_rule)
        world_module = importlib.import_module(SilksongWorld.__module__)

        with mock.patch.object(
            world_module,
            "prefill_category_shuffles",
        ) as prefill:
            SilksongWorld.stage_pre_fill(multiworld)

        prefill.assert_called_once_with(
            multiworld,
            SilksongWorld.game,
        )
        self.assertIs(location.item_rule, original_rule)
        self.assertFalse(
            hasattr(
                multiworld,
                "_silksong_shuffle_item_rule_scope",
            )
        )


class TestWishLogicEvents(SilksongTestBase):
    options = {"goal": "act_3"}

    def test_verified_completions_are_locked_hidden_native_events(self) -> None:
        active_events = self.world._silksong_active_wish_logic_events
        self.assertEqual(set(active_events), {
            event.location_name for event in WISH_LOGIC_EVENTS
        })

        item_counts = Counter()
        for event in WISH_LOGIC_EVENTS:
            with self.subTest(event=event.location_name):
                location = self.world.get_location(event.location_name)
                self.assertIsNone(location.address)
                self.assertFalse(location.show_in_spoiler)
                self.assertTrue(location.locked)
                self.assertIsNotNone(location.item)
                self.assertEqual(location.item.name, event.item_name)
                self.assertNotEqual(
                    location.parent_region.name,
                    LOGIC_UNKNOWN_REGION_NAME,
                )
                item_counts[event.item_name] += 1

        self.assertEqual(item_counts, Counter({
            SONGCLAVE_BOARD_COMPLETION_ITEM: 5,
            SILK_AND_SOUL_MANDATORY_WISH_ITEM: 9,
            SILK_AND_SOUL_WISH_POINT_ITEM: 16,
            SILK_AND_SOUL_WISH_HALF_POINT_ITEM: 6,
            SILK_AND_SOUL_LACE_DEFEATED_ITEM: 1,
            WIDOW_DEFEATED_EVENT_ITEM: 1,
        }))

    def test_widow_defeat_is_a_checked_source_event(self) -> None:
        event = next(
            event for event in WISH_LOGIC_EVENTS
            if event.item_name == WIDOW_DEFEATED_EVENT_ITEM
        )
        self.assertEqual(event.source_location, "Boss: Widow")
        self.assertTrue(event.requires_checked_source)
        self.assertNotIn(WIDOW_DEFEATED_EVENT_ITEM, EVENT_REQUIREMENTS)

    def test_songclave_and_silk_and_soul_counts_are_exact(self) -> None:
        strengthening = EVENT_REQUIREMENTS[
            "Event: Strengthening Songclave Completed"
        ]
        self.assertEqual(len(strengthening), 1)
        self.assertIn(
            "Event: Building Up Songclave Completed",
            strengthening[0].all_of,
        )
        self.assertEqual(
            strengthening[0].item_counts[0].minimum,
            4,
        )
        self.assertEqual(
            strengthening[0].item_counts[0].item_names,
            (SONGCLAVE_BOARD_COMPLETION_ITEM,),
        )

        thresholds = set()
        for requirement in EVENT_REQUIREMENTS[
            "Event: Silk and Soul Completed"
        ]:
            counts = {
                count.item_names[0]: count.minimum
                for count in requirement.item_counts
            }
            self.assertEqual(
                counts[SILK_AND_SOUL_MANDATORY_WISH_ITEM],
                9,
            )
            self.assertIn(
                SILK_AND_SOUL_LACE_DEFEATED_ITEM,
                requirement.all_of,
            )
            thresholds.add((
                counts[SILK_AND_SOUL_WISH_POINT_ITEM],
                counts.get(SILK_AND_SOUL_WISH_HALF_POINT_ITEM, 0),
            ))
        self.assertEqual(
            thresholds,
            {(17, 0), (16, 2), (15, 4), (14, 6)},
        )

    def test_witch_and_bellhart_keep_their_wish_predecessors(self) -> None:
        self.assertTrue(all(
            RITE_OF_REBIRTH_EVENT in requirement.all_of
            for requirement in REQUIREMENTS["Crest: Witch"]
        ))
        self.assertTrue(all(
            "Wish: Restoration of Bellhart"
            in requirement.required_locations
            for requirement in REQUIREMENTS[
                "Wish: Bellhart's Glory"
            ]
        ))


class TestRoomGraphCountRequirements(TestCase):
    def test_forced_act_three_boss_flags_remain_logic_sources(self) -> None:
        for location_name in ('Boss: Fourth Chorus', 'Boss: Trobbio'):
            with self.subTest(location=location_name):
                self.assertTrue(
                    any(
                        'Event: Act 3 Started' in requirement.all_of
                        for requirement in REQUIREMENTS[location_name]
                    )
                )

    def test_six_spool_fragments_use_all_fragment_items(self) -> None:
        requirement = _compiled_room_clause_requirement(
            CompiledRoomClause(
                item_counts=((SPOOL_FRAGMENT_COUNT_ITEM, 6),)
            )
        )

        self.assertEqual(len(requirement.item_counts), 1)
        fragment_count = requirement.item_counts[0]
        self.assertEqual(fragment_count.minimum, 6)
        self.assertEqual(
            fragment_count.item_names,
            SPOOL_FRAGMENT_ITEM_NAMES,
        )
        self.assertEqual(
            fragment_count.as_slot_data(),
            {
                "items": list(SPOOL_FRAGMENT_ITEM_NAMES),
                "minimum": 6,
            },
        )

        multiworld = setup_multiworld(SilksongWorld, steps=())
        rule = _compile_item_count(fragment_count).resolve(
            multiworld.worlds[1]
        )
        five_fragment_state = CollectionState(multiworld)
        five_fragment_state.prog_items[1].update(
            SPOOL_FRAGMENT_ITEM_NAMES[:5]
        )
        six_fragment_state = CollectionState(multiworld)
        six_fragment_state.prog_items[1].update(
            SPOOL_FRAGMENT_ITEM_NAMES[:6]
        )

        self.assertFalse(rule(five_fragment_state))
        self.assertTrue(rule(six_fragment_state))


class TestShellwoodSkipBoundary(TestCase):
    @staticmethod
    def _setup(skips: str, bellway_randomization: str):
        return setup_multiworld(
            SilksongWorld,
            steps=(
                "generate_early",
                "create_regions",
                "create_items",
                "set_rules",
            ),
            options={
                "goal": "act_3",
                "bellway_access": "randomized_stations",
                "bellway_randomization": bellway_randomization,
                "skips": skips,
            },
        )

    def test_blasted_steps_bellway_does_not_unlock_sister_without_skips(
        self,
    ) -> None:
        for bellway_randomization in ("vanilla", "anywhere", "shuffle"):
            for skips, expected in (("none", False), ("easy", True)):
                with self.subTest(
                    bellway_randomization=bellway_randomization,
                    skips=skips,
                ):
                    multiworld = self._setup(skips, bellway_randomization)
                    world = multiworld.worlds[1]
                    state = CollectionState(multiworld)
                    location = multiworld.get_location(
                        "Boss: Sister Splinter",
                        1,
                    )

                    self.assertFalse(location.can_reach(state))
                    state.collect(world.create_item("Bellway: Blasted Steps"))
                    self.assertEqual(location.can_reach(state), expected)
                    state.collect(world.create_item("Cling Grip"))
                    self.assertTrue(location.can_reach(state))

    def test_sprint_alone_does_not_unlock_shellwood_checks(self) -> None:
        multiworld = setup_multiworld(
            SilksongWorld,
            steps=(
                "generate_early",
                "create_regions",
                "create_items",
                "set_rules",
            ),
            options={
                "goal": "act_3",
                "bellway_access": "randomized_stations",
                "bellway_randomization": "anywhere",
                "skips": "none",
                "split_dash_and_sprint": True,
            },
        )
        world = multiworld.worlds[1]
        state = CollectionState(multiworld)
        state.collect(world.create_item("Bellway: Blasted Steps"))
        state.collect(world.create_item("Progressive Swift Step"))

        for location_name in (
            "Boss: Sister Splinter",
            "Shellwood - Mask Shard",
        ):
            with self.subTest(location_name=location_name):
                location = multiworld.get_location(location_name, 1)
                self.assertFalse(location.can_reach(state))

    def test_full_swift_does_not_make_upper_bellhart_jump(self) -> None:
        multiworld = setup_multiworld(
            SilksongWorld,
            steps=(
                "generate_early",
                "create_regions",
                "create_items",
                "set_rules",
            ),
            options={
                "goal": "act_3",
                "skips": "none",
                "split_dash_and_sprint": True,
            },
        )
        world = multiworld.worlds[1]
        state = CollectionState(multiworld)
        for item_name in (
            "Progressive Swift Step",
            "Progressive Swift Step",
            "Progressive Silkheart",
            "Simple Key (Wormways)",
            "Crest: Hunter",
        ):
            state.collect(world.create_item(item_name))

        widow = multiworld.get_location("Boss: Widow", 1)
        self.assertFalse(widow.can_reach(state))

        state.collect(world.create_item("Cling Grip"))
        self.assertTrue(widow.can_reach(state))
