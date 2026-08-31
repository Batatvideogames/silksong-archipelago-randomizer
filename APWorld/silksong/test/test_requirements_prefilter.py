from unittest import TestCase
from unittest.mock import patch

from worlds.silksong import requirements as requirements_module
from worlds.silksong.items import item_data_table
from worlds.silksong.locations import location_data_table


class TestLocationSelfDependencyPrefilter(TestCase):
    def test_referenced_rewards_are_evaluated_before_unreferenced_rewards(self):
        built_locations = []

        def build_rule(location_name, _player, skips_tier):
            built_locations.append(location_name)
            return lambda state: state.has("Swift Step", 1)

        reward_names = {
            "Swift Step": "Swift Step",
            "Silkspear": "Rosaries",
        }
        with (
            patch.object(
                requirements_module,
                "get_logic_item_references",
                return_value=frozenset({"Swift Step"}),
            ),
            patch.object(
                requirements_module,
                "_get_vanilla_reward_name",
                side_effect=reward_names.__getitem__,
            ),
            patch.object(
                requirements_module,
                "make_rule",
                side_effect=build_rule,
            ),
        ):
            dependencies = (
                requirements_module._get_location_self_dependencies(
                    ("Swift Step", "Silkspear"),
                    ("Swift Step", "Rosaries"),
                )
            )

        self.assertEqual(
            dependencies,
            frozenset({"Swift Step -> Swift Step"}),
        )
        self.assertEqual(built_locations, ["Swift Step"])

    def test_prefilter_matches_unfiltered_world_data(self):
        location_names = tuple(location_data_table)
        item_names = tuple(item_data_table)

        with patch.object(
            requirements_module,
            "make_rule",
            wraps=requirements_module.make_rule,
        ) as optimized_make_rule:
            optimized = requirements_module._get_location_self_dependencies(
                location_names,
                item_names,
            )

        all_rewards = frozenset(
            requirements_module._get_vanilla_reward_name(location_name)
            for location_name in location_names
        )
        with (
            patch.object(
                requirements_module,
                "get_logic_item_references",
                return_value=all_rewards,
            ),
            patch.object(
                requirements_module,
                "make_rule",
                wraps=requirements_module.make_rule,
            ) as unfiltered_make_rule,
        ):
            unfiltered = requirements_module._get_location_self_dependencies(
                location_names,
                item_names,
            )

        self.assertEqual(optimized, unfiltered)
        self.assertLess(
            optimized_make_rule.call_count,
            unfiltered_make_rule.call_count,
        )
