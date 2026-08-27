from __future__ import annotations

import random
from unittest import mock

from BaseClasses import CollectionState

from .. import SilksongWorld
from ..requirement_rules import (
    NativeSourceRule,
    _enable_native_source_memo,
)
from .bases import SilksongTestBase


def explicit_native_source_result(
    rule: NativeSourceRule.Resolved,
    state: CollectionState,
) -> bool:
    assumed_state = state.copy()
    assumed_state.collect(rule.assumed_item, True)
    if (
        rule.anchor_requirement_name is not None
        and not assumed_state.can_reach_region(
            rule.anchor_requirement_name,
            rule.player,
        )
    ):
        return False
    return rule.child(assumed_state)


class TestNativeSourceMemo(SilksongTestBase):
    options = {
        "goal": "act_3",
        "skill_randomization": "vanilla",
        "silk_skill_randomization": "vanilla",
        "split_dash_and_sprint": True,
        "major_key_randomization": "vanilla",
        "bellway_randomization": "vanilla",
    }

    def native_source_rules(self) -> list[NativeSourceRule.Resolved]:
        rules = [
            location.access_rule
            for location in self.multiworld.get_locations(self.player)
            if isinstance(location.access_rule, NativeSourceRule.Resolved)
        ]
        self.assertTrue(rules)
        return rules

    def test_finalize_enables_and_resets_memo(self) -> None:
        rule = self.native_source_rules()[0]
        state = CollectionState(self.multiworld)
        expected = explicit_native_source_result(rule, state)

        self.assertEqual(rule._evaluate(state), expected)
        self.assertEqual(rule._evaluate(state), expected)
        self.assertFalse(
            hasattr(self.multiworld, "_silksong_native_source_memo")
        )

        inventory_key = (
            self.player,
            tuple(sorted(state.prog_items[self.player].items())),
            state.allow_partial_entrances,
        )
        setattr(
            self.multiworld,
            "_silksong_native_source_memo",
            {
                "inventory_keys": {inventory_key: inventory_key},
                "results": {(id(rule), inventory_key): not expected},
            },
        )
        with (
            mock.patch.object(
                self.world,
                "uses_vog_playthrough_hints",
                return_value=False,
            ),
            mock.patch.object(self.world, "build_vog_hint_plan"),
        ):
            SilksongWorld.stage_finalize_multiworld(self.multiworld)

        memo = getattr(self.multiworld, "_silksong_native_source_memo")
        self.assertEqual(memo, {"inventory_keys": {}, "results": {}})
        self.assertEqual(rule._evaluate(state), expected)
        with mock.patch.object(
            CollectionState,
            "copy",
            side_effect=AssertionError("memo hit copied state"),
        ):
            self.assertEqual(rule._evaluate(state), expected)

    def test_matches_explicit_result_across_real_states(self) -> None:
        rules = self.native_source_rules()
        items = [
            item
            for item in self.multiworld.get_items()
            if item.player == self.player and item.advancement
        ]
        random.Random(34567).shuffle(items)
        milestones = {
            round(index * len(items) / 16)
            for index in range(17)
        }

        _enable_native_source_memo(self.multiworld)
        state = CollectionState(self.multiworld)
        for index in range(len(items) + 1):
            if index in milestones:
                cold_state = state.copy()
                warm_state = state.copy()
                for region in self.multiworld.get_regions(self.player):
                    warm_state.can_reach_region(region.name, self.player)
                partial_state = state.copy()
                partial_state.allow_partial_entrances = True

                for rule in rules:
                    expected_cold = explicit_native_source_result(
                        rule,
                        cold_state,
                    )
                    expected_warm = explicit_native_source_result(
                        rule,
                        warm_state,
                    )
                    self.assertEqual(expected_warm, expected_cold)
                    self.assertEqual(
                        rule._evaluate(cold_state),
                        expected_cold,
                    )
                    self.assertEqual(
                        rule._evaluate(warm_state),
                        expected_warm,
                    )

                    expected_partial = explicit_native_source_result(
                        rule,
                        partial_state,
                    )
                    self.assertEqual(
                        rule._evaluate(partial_state),
                        expected_partial,
                    )
                    self.assertEqual(
                        rule._evaluate(partial_state.copy()),
                        expected_partial,
                    )

            if index < len(items):
                state.collect(items[index], True)

        memo = getattr(self.multiworld, "_silksong_native_source_memo")
        canonical_inventories = memo["inventory_keys"]
        self.assertTrue(canonical_inventories)
        for _, inventory_key in memo["results"]:
            self.assertIs(
                inventory_key,
                canonical_inventories[inventory_key],
            )
