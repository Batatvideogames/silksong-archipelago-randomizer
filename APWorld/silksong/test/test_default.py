import importlib
from types import SimpleNamespace
from unittest import TestCase, mock

from BaseClasses import CollectionState
from rule_builder.rules import Rule

from .. import LOGIC_UNKNOWN_REGION_NAME, SilksongWorld
from .. import category_fill, rules
from ..locations import SCROUNGE_RELIC_ITEM_NAMES
from ..requirements import LOGIC_UNKNOWN_LOCATIONS
from .bases import SilksongTestBase


CLAWLINE_ITEM = "Clawline"
DEEP_DOCKS_MINES_FLEA = "Flea: Deep Docks - Mines"


class TestDefaultWorld(SilksongTestBase):
    # Naming the default goal opts this class into WorldTestBase's generic
    # generation, fill, all-state and empty-state checks.
    options = {"goal": "act_3"}

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
                SimpleNamespace(silksong_placement_category="Skill")
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
            "Legal cross-player Skill",
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
