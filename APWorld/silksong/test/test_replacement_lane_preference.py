from collections import Counter
from random import Random
from unittest import TestCase

from BaseClasses import ItemClassification

from ..alphabet_mode import (
    ALPHABET_ITEM_NAMES,
    replace_filler_with_alphabet_items,
)
from ..items import (
    ItemPoolEntry,
    LEDGE_GRAB_ITEM,
    SWIM_ITEM,
    item_data_table,
    replace_filler_with_innate_ability_items,
)


class ReplacementLanePreferenceTest(TestCase):
    @staticmethod
    def lane_counts(entries):
        return Counter(entry.placement_category for entry in entries)

    @staticmethod
    def entries(unrestricted, quest=0, boss=0):
        return [
            *(
                ItemPoolEntry("Rosaries (60)", "SimpleKey")
                for _ in range(unrestricted)
            ),
            *(
                ItemPoolEntry("Rosaries (60)", "Quest", "Quest")
                for _ in range(quest)
            ),
            *(
                ItemPoolEntry("Rosaries (60)", "Boss", "Boss")
                for _ in range(boss)
            ),
        ]

    @staticmethod
    def is_filler(name):
        return (
            item_data_table[name].classification
            == ItemClassification.filler
        )

    def test_alphabet_uses_unrestricted_fillers_first(self):
        entries = self.entries(26, quest=1, boss=1)
        lane_counts = self.lane_counts(entries)

        replace_filler_with_alphabet_items(
            entries,
            self.is_filler,
            Random(0),
        )

        self.assertEqual(lane_counts, self.lane_counts(entries))
        self.assertEqual(
            set(ALPHABET_ITEM_NAMES),
            {
                entry.name
                for entry in entries
                if entry.placement_category is None
            },
        )

    def test_alphabet_falls_back_to_restricted_fillers(self):
        entries = self.entries(24, quest=2, boss=1)
        lane_counts = self.lane_counts(entries)

        replace_filler_with_alphabet_items(
            entries,
            self.is_filler,
            Random(0),
        )

        self.assertEqual(lane_counts, self.lane_counts(entries))
        self.assertEqual(
            24,
            sum(
                entry.name in ALPHABET_ITEM_NAMES
                and entry.placement_category is None
                for entry in entries
            ),
        )
        self.assertEqual(
            2,
            sum(
                entry.name in ALPHABET_ITEM_NAMES
                and entry.placement_category is not None
                for entry in entries
            ),
        )

    def test_innate_abilities_use_unrestricted_fillers_first(self):
        entries = self.entries(2, quest=1)
        lane_counts = self.lane_counts(entries)

        replace_filler_with_innate_ability_items(
            entries,
            (LEDGE_GRAB_ITEM, SWIM_ITEM),
            Random(0),
        )

        self.assertEqual(lane_counts, self.lane_counts(entries))
        self.assertEqual(
            {LEDGE_GRAB_ITEM, SWIM_ITEM},
            {
                entry.name
                for entry in entries
                if entry.placement_category is None
            },
        )

    def test_innate_abilities_fall_back_to_restricted_fillers(self):
        entries = self.entries(1, quest=1, boss=1)
        lane_counts = self.lane_counts(entries)

        replace_filler_with_innate_ability_items(
            entries,
            (LEDGE_GRAB_ITEM, SWIM_ITEM),
            Random(0),
        )

        self.assertEqual(lane_counts, self.lane_counts(entries))
        self.assertEqual(
            1,
            sum(
                entry.name in {LEDGE_GRAB_ITEM, SWIM_ITEM}
                and entry.placement_category is None
                for entry in entries
            ),
        )
        self.assertEqual(
            1,
            sum(
                entry.name in {LEDGE_GRAB_ITEM, SWIM_ITEM}
                and entry.placement_category is not None
                for entry in entries
            ),
        )
