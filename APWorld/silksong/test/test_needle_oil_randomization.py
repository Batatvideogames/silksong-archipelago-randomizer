from collections import Counter
from unittest import TestCase

from ..items import (
    PAIRED_ITEM_CATEGORIES,
    PALE_OIL_ITEM,
    PROGRESSIVE_NEEDLE_UPGRADE_ITEM,
    build_item_pool_entries,
    item_data_table,
)
from ..locations import (
    NEEDLE_UPGRADE_LOCATION_CATEGORY,
    NEEDLE_UPGRADE_LOCATION_NAMES,
    PALE_OIL_LOCATION_CATEGORY,
    PALE_OIL_LOCATION_NAMES,
    location_data_table,
    location_table,
)
from ..options import (
    NeedleUpgradeRandomization,
    PaleOilRandomization,
)
from ..requirements import (
    _get_location_self_dependencies,
    get_crawfather_requirements,
    get_pinmaster_oil_requirements,
)
from .bases import SilksongTestBase


class TestNeedleOilRandomization(TestCase):
    @staticmethod
    def build_split_pool(
        needle_mode: str,
        pale_oil_mode: str,
    ) -> Counter[tuple[str, str]]:
        modes = {
            category: 'vanilla'
            for category in PAIRED_ITEM_CATEGORIES
        }
        modes.update({
            'Boss': 'vanilla',
            'Quest': 'vanilla',
            NEEDLE_UPGRADE_LOCATION_CATEGORY: needle_mode,
            PALE_OIL_LOCATION_CATEGORY: pale_oil_mode,
        })
        entries = build_item_pool_entries(
            'Crest: Hunter',
            {},
            False,
            modes,
        )
        return Counter(
            (entry.name, entry.source_category)
            for entry in entries
            if entry.source_category in {
                NEEDLE_UPGRADE_LOCATION_CATEGORY,
                PALE_OIL_LOCATION_CATEGORY,
            }
        )

    def test_options_default_to_vanilla(self) -> None:
        self.assertEqual(
            NeedleUpgradeRandomization.default,
            NeedleUpgradeRandomization.option_vanilla,
        )
        self.assertEqual(
            PaleOilRandomization.default,
            PaleOilRandomization.option_vanilla,
        )

    def test_split_modes_add_only_their_own_items(self) -> None:
        expected_needle = Counter({
            (PROGRESSIVE_NEEDLE_UPGRADE_ITEM, 'NeedleUpgrade'): 4,
        })
        expected_oil = Counter({(PALE_OIL_ITEM, 'PaleOil'): 3})

        self.assertEqual(self.build_split_pool('vanilla', 'vanilla'), Counter())
        self.assertEqual(self.build_split_pool('anywhere', 'vanilla'), expected_needle)
        self.assertEqual(self.build_split_pool('vanilla', 'anywhere'), expected_oil)
        self.assertEqual(
            self.build_split_pool('anywhere', 'anywhere'),
            expected_needle + expected_oil,
        )

    def test_split_metadata_preserves_order_and_ids(self) -> None:
        names = (*NEEDLE_UPGRADE_LOCATION_NAMES, *PALE_OIL_LOCATION_NAMES)
        ids = [location_table[name] for name in names]
        self.assertEqual(ids, list(range(835787, 835794)))
        self.assertEqual(
            item_data_table[PROGRESSIVE_NEEDLE_UPGRADE_ITEM].code,
            835226,
        )
        self.assertTrue(all(
            location_data_table[name].category == NEEDLE_UPGRADE_LOCATION_CATEGORY
            for name in NEEDLE_UPGRADE_LOCATION_NAMES
        ))
        self.assertTrue(all(
            location_data_table[name].category == PALE_OIL_LOCATION_CATEGORY
            for name in PALE_OIL_LOCATION_NAMES
        ))
        self.assertEqual(
            item_data_table[PALE_OIL_ITEM].code,
            835227,
        )

    def test_randomized_oil_uses_received_items_for_pinmaster(self) -> None:
        requirements = get_pinmaster_oil_requirements(True)
        self.assertEqual(len(requirements), 1)
        self.assertIn("Event: Widow Defeated", requirements[0].all_of)
        self.assertEqual(
            {
                (count.item_names, count.minimum)
                for count in requirements[0].item_counts
            },
            {
                ((PROGRESSIVE_NEEDLE_UPGRADE_ITEM,), 1),
                ((PALE_OIL_ITEM,), 1),
            },
        )

    def test_either_randomized_lane_gates_crawfather_by_upgrades(self) -> None:
        for needle_randomized, oil_randomized in (
            (True, False),
            (False, True),
            (True, True),
        ):
            with self.subTest(
                needle_randomized=needle_randomized,
                oil_randomized=oil_randomized,
            ):
                requirements = get_crawfather_requirements(
                    needle_randomized,
                    oil_randomized,
                )
                self.assertTrue(requirements)
                self.assertTrue(all(
                    any(
                        count.item_names
                        == (PROGRESSIVE_NEEDLE_UPGRADE_ITEM,)
                        and count.minimum == 2
                        for count in requirement.item_counts
                    )
                    for requirement in requirements
                ))

    def test_progressive_upgrade_sources_are_not_self_locked(self) -> None:
        dependencies = _get_location_self_dependencies(
            NEEDLE_UPGRADE_LOCATION_NAMES,
            item_data_table,
        )
        self.assertFalse(dependencies)


class NeedleOilWorldAssertions:
    needle_randomized = False
    pale_oil_randomized = False

    def test_active_locations_and_items_match_split_modes(self) -> None:
        active_locations = {
            location.name
            for location in self.world.get_locations()
            if location.address is not None
        }
        item_counts = Counter(
            item.name for item in self.multiworld.itempool
        )
        self.assertEqual(
            set(NEEDLE_UPGRADE_LOCATION_NAMES) <= active_locations,
            self.needle_randomized,
        )
        self.assertEqual(
            set(PALE_OIL_LOCATION_NAMES) <= active_locations,
            self.pale_oil_randomized,
        )
        self.assertEqual(
            item_counts[PROGRESSIVE_NEEDLE_UPGRADE_ITEM],
            4 if self.needle_randomized else 0,
        )
        self.assertEqual(
            item_counts[PALE_OIL_ITEM],
            3 if self.pale_oil_randomized else 0,
        )
        for location_name in (
            *NEEDLE_UPGRADE_LOCATION_NAMES,
            *PALE_OIL_LOCATION_NAMES,
        ):
            if location_name in active_locations:
                self.assertIsNotNone(
                    self.world.get_location(location_name).parent_region
                )
        self.assertEqual(
            self.world.create_item(PROGRESSIVE_NEEDLE_UPGRADE_ITEM).code,
            item_data_table[PROGRESSIVE_NEEDLE_UPGRADE_ITEM].code,
        )
        self.assertEqual(
            self.world.create_item(PALE_OIL_ITEM).code,
            item_data_table[PALE_OIL_ITEM].code,
        )


class TestNeedleAnywherePaleOilVanilla(
    NeedleOilWorldAssertions,
    SilksongTestBase,
):
    needle_randomized = True
    options = {
        'needle_upgrade_randomization': 'anywhere',
        'pale_oil_randomization': 'vanilla',
    }


class TestNeedleVanillaPaleOilAnywhere(
    NeedleOilWorldAssertions,
    SilksongTestBase,
):
    pale_oil_randomized = True
    options = {
        'needle_upgrade_randomization': 'vanilla',
        'pale_oil_randomization': 'anywhere',
    }


class TestNeedleAnywherePaleOilAnywhere(
    NeedleOilWorldAssertions,
    SilksongTestBase,
):
    needle_randomized = True
    pale_oil_randomized = True
    options = {
        'needle_upgrade_randomization': 'anywhere',
        'pale_oil_randomization': 'anywhere',
    }
