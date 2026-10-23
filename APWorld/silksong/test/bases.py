from test.bases import WorldTestBase
from BaseClasses import LocationProgressType
from worlds.AutoWorld import call_all


class SilksongTestBase(WorldTestBase):
    game = "Hollow Knight: Silksong"

    def test_all_state_can_reach_everything(self):
        if not (self.run_default_tests and self.constructed):
            return
        state = self.multiworld.get_all_state(False)
        unknown = self.world.get_logic_unknown_locations()
        for location in self.multiworld.get_locations():
            with self.subTest(location=location.name):
                self.assertTrue(location.can_reach(state))
                if location.name in unknown:
                    self.assertIsNotNone(location.address)
                    self.assertEqual(location.progress_type, LocationProgressType.EXCLUDED)
        self.multiworld.state = state
        self.assertBeatable(True)

    def test_fill(self):
        if not (self.run_default_tests and self.constructed):
            return
        from Fill import distribute_items_restrictive
        distribute_items_restrictive(self.multiworld)
        call_all(self.multiworld, 'post_fill')
        call_all(self.multiworld, 'finalize_multiworld')
        self.assertTrue(self.multiworld.fulfills_accessibility())
        placed = [loc.item for loc in self.multiworld.get_locations() if loc.item and loc.item.code]
        self.assertLessEqual(len(self.multiworld.itempool), len(placed))
