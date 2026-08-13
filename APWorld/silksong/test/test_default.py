from BaseClasses import CollectionState
from rule_builder.rules import Rule

from ..locations import SCROUNGE_RELIC_ITEM_NAMES
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
