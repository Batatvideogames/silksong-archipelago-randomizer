from unittest import TestCase

from ..requirements import (
    INNATE_CAPABILITY_REQUIREMENTS,
    LEDGE_GRAB_CAPABILITY_REQUIREMENT,
    PATH_REQUIREMENTS,
    RANDOMIZED_INNATE_CAPABILITY_REQUIREMENTS,
    SWIM_CAPABILITY_REQUIREMENT,
    get_abstract_requirements,
    req,
)
from ..room_graph import LogicStatus, RequirementMode, load_room_graph
from ..room_graph_logic import (
    CompiledRoomClause,
    _EXTERNAL_BOUNDARY_SEEDS,
    room_node_name,
)


def clause(
    *all_of: str,
    skip_tier: int = 0,
) -> CompiledRoomClause:
    return CompiledRoomClause(
        all_of=all_of,
        minimum_skip_tier=skip_tier,
    )


class TestInnateAbilityGraphRows(TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.graph = load_room_graph()

    def assert_active_dnf(
        self,
        row,
        expected: tuple[tuple[str, ...], ...],
    ) -> None:
        self.assertEqual(row.status, LogicStatus.ACTIVE)
        self.assertEqual(row.requirement.mode, RequirementMode.EXPRESSION)
        self.assertEqual(row.requirement.dnf, expected)

    def test_craw_entrance_lower_right_dnf(self) -> None:
        row = self.graph.transition_by_id[
            "greymoor/greymoor-craw-lake-entrance@lr"
        ]
        self.assert_active_dnf(
            row,
            (
                ("capability:swim",),
                ("option:skips-moderate",),
                ("item:flea-brew", "option:skips-easy"),
                ("macro:progressive-swift-step-first",),
                ("item:clawline",),
                ("item:sharpdart",),
                ("item:drifters-cloak",),
                ("item:faydown-cloak",),
            ),
        )

    def test_west_bellshrine_top_left_dnf(self) -> None:
        row = self.graph.transition_by_id[
            "greymoor/greymoor-west-bellshrine-room@tl"
        ]
        self.assert_active_dnf(
            row,
            (
                ("capability:ledge-grab",),
                ("item:cling-grip",),
                ("item:faydown-cloak",),
                ("macro:usable-scuttlebrace", "option:skips-moderate"),
                (
                    "macro:progressive-swift-step-first",
                    "item:clawline",
                ),
                ("item:sharpdart",),
                ("item:silk-soar",),
            ),
        )

    def test_upper_halfway_tall_platform_dnf_both_directions(
        self,
    ) -> None:
        forward = self.graph.connection_by_id[
            "greymoor/greymoor-upper-halfway-home-path"
            "#left-section>right-section@tp"
        ]
        reverse = self.graph.connection_by_id[
            "greymoor/greymoor-upper-halfway-home-path"
            "#right-section>left-section@tp"
        ]
        self.assert_active_dnf(
            forward,
            (
                ("capability:swim",),
                ("item:faydown-cloak",),
                ("macro:usable-scuttlebrace", "option:skips-moderate"),
                ("option:skips-moderate",),
                ("option:skips-easy", "capability:ledge-grab"),
                ("option:skips-easy", "item:cling-grip"),
                (
                    "item:flea-brew",
                    "option:skips-easy",
                    "capability:ledge-grab",
                ),
                (
                    "item:flea-brew",
                    "option:skips-easy",
                    "item:cling-grip",
                ),
                ("item:clawline",),
                ("macro:progressive-swift-step-first",),
                ("item:sharpdart",),
                ("item:beast-crest", "option:skips-easy"),
                ("item:hunter-crest", "option:skips-easy"),
                ("item:reaper-crest", "option:skips-easy"),
                ("item:witch-crest", "option:skips-easy"),
                ("item:architect-crest", "option:skips-easy"),
                ("item:shaman-crest", "option:skips-easy"),
            ),
        )
        self.assert_active_dnf(
            reverse,
            (
                ("capability:swim",),
                ("item:faydown-cloak",),
                ("macro:usable-scuttlebrace", "option:skips-moderate"),
                ("option:skips-moderate",),
                ("option:skips-easy", "item:cling-grip"),
                ("option:skips-easy", "capability:ledge-grab"),
                (
                    "item:cling-grip",
                    "macro:progressive-swift-step-first",
                ),
                ("item:cling-grip", "item:clawline"),
                ("item:cling-grip", "item:sharpdart"),
                ("item:silk-soar", "item:clawline"),
                ("item:silk-soar", "macro:progressive-swift-step-full"),
                ("item:silk-soar", "item:sharpdart"),
            ),
        )

    def test_throwing_ring_conjunction_and_dnf(self) -> None:
        row = self.graph.check_by_id[
            "bilewater/bilewater-waterfall::check/throwing-ring"
        ]
        self.assert_active_dnf(
            row,
            (
                (
                    "item:faydown-cloak",
                    "capability:swim",
                    "item:cling-grip",
                    "capability:ledge-grab",
                ),
                (
                    "item:faydown-cloak",
                    "capability:swim",
                    "item:cling-grip",
                    "macro:progressive-swift-step-full",
                ),
                (
                    "item:faydown-cloak",
                    "capability:swim",
                    "item:cling-grip",
                    "item:clawline",
                ),
                (
                    "item:faydown-cloak",
                    "capability:swim",
                    "item:cling-grip",
                    "item:sharpdart",
                ),
            ),
        )

    def test_upper_craw_nest_checks_preserve_drifter_conjunctions(
        self,
    ) -> None:
        expected = (
            ("item:silk-soar",),
            ("item:clawline",),
            ("item:drifters-cloak", "item:faydown-cloak"),
            ("item:drifters-cloak", "capability:ledge-grab"),
            ("item:drifters-cloak", "item:sharpdart"),
            (
                "item:drifters-cloak",
                "macro:progressive-swift-step-first",
            ),
            (
                "item:drifters-cloak",
                "item:flea-brew",
                "option:skips-easy",
            ),
            (
                "macro:progressive-swift-step-first",
                "item:sharpdart",
            ),
            (
                "macro:progressive-swift-step-first",
                "item:faydown-cloak",
                "item:flea-brew",
                "option:skips-easy",
            ),
            (
                "macro:progressive-swift-step-full",
                "item:faydown-cloak",
            ),
        )
        for row_id in (
            "greymoor/greymoor-craw-lake"
            "::check/greymoor-rosary-cache-19",
            "greymoor/greymoor-craw-lake::check/threefold-pin",
        ):
            with self.subTest(row_id=row_id):
                self.assert_active_dnf(
                    self.graph.check_by_id[row_id],
                    expected,
                )

    def test_greymoor_external_boundaries(self) -> None:
        self.assertEqual(
            _EXTERNAL_BOUNDARY_SEEDS[
                "sinner-s-road/sinner-s-road-entrance#room"
            ],
            (
                clause(
                    "Path: Greymoor - Halfway House",
                    "Capability: Ledge Grab",
                ),
            ),
        )

    def test_abyss_escape_keeps_universal_final_gauntlet(self) -> None:
        common = (
            "Path: Abyss - Void Tendrils",
            "Ancestral Art: Silk Soar",
            "Ability: Drifter's Cloak",
            "Ancestral Art: Cling Grip",
            "Ability: Faydown Cloak",
        )
        self.assertEqual(
            PATH_REQUIREMENTS["Path: Abyss - Escape"],
            (
                req(*common, skip_tier=1),
                req(*common, "Ancestral Art: Clawline"),
            ),
        )
        self.assertEqual(
            _EXTERNAL_BOUNDARY_SEEDS[
                "greymoor/greymoor-lower-halfway-home-path#room"
            ],
            (
                clause("Path: Greymoor - Halfway House", "Swift Step"),
                clause(
                    "Path: Greymoor - Halfway House",
                    "Progressive Swift Step",
                ),
                clause(
                    "Path: Greymoor - Halfway House",
                    "Ancestral Art: Clawline",
                ),
                clause(
                    "Path: Greymoor - Halfway House",
                    "Ability: Faydown Cloak",
                ),
                clause(
                    "Path: Greymoor - Halfway House",
                    "Ability: Drifter's Cloak",
                ),
                clause(
                    "Path: Greymoor - Halfway House",
                    "Usable Sharpdart",
                ),
                clause(
                    "Path: Greymoor - Halfway House",
                    "Crest: Beast",
                    skip_tier=1,
                ),
                clause(
                    "Path: Greymoor - Halfway House",
                    "Usable Flea Brew",
                    skip_tier=1,
                ),
                clause(
                    "Path: Greymoor - Halfway House",
                    "Capability: Swim",
                ),
            ),
        )
        self.assertEqual(
            _EXTERNAL_BOUNDARY_SEEDS[
                "greymoor/greymoor-bone-scroll-room#room"
            ],
            (
                clause(
                    "Path: Greymoor - Halfway House",
                    "Capability: Swim",
                ),
                clause(
                    "Path: Greymoor - Halfway House",
                    "Ancestral Art: Clawline",
                    "Capability: Ledge Grab",
                ),
            ),
        )

    def test_options_change_only_capability_owners(self) -> None:
        baseline = get_abstract_requirements()
        representative_consumers = (
            room_node_name(
                "greymoor/greymoor-craw-lake#lower-left-section"
            ),
            room_node_name(
                "greymoor/greymoor-upper-halfway-home-path#right-section"
            ),
            room_node_name(
                "greymoor/greymoor-upper-halfway-home-path#left-section"
            ),
            room_node_name("sinner-s-road/sinner-s-road-entrance#room"),
            room_node_name(
                "greymoor/greymoor-lower-halfway-home-path#room"
            ),
            room_node_name("greymoor/greymoor-bone-scroll-room#room"),
            "Path: Abyss - Escape",
        )
        for randomize_ledge_grab, randomize_swim in (
            (False, False),
            (True, False),
            (False, True),
            (True, True),
        ):
            with self.subTest(
                randomize_ledge_grab=randomize_ledge_grab,
                randomize_swim=randomize_swim,
            ):
                adjusted = get_abstract_requirements(
                    randomize_ledge_grab=randomize_ledge_grab,
                    randomize_swim=randomize_swim,
                )
                for consumer in representative_consumers:
                    self.assertEqual(adjusted[consumer], baseline[consumer])
                self.assertEqual(
                    adjusted[LEDGE_GRAB_CAPABILITY_REQUIREMENT],
                    (
                        RANDOMIZED_INNATE_CAPABILITY_REQUIREMENTS[
                            LEDGE_GRAB_CAPABILITY_REQUIREMENT
                        ]
                        if randomize_ledge_grab
                        else INNATE_CAPABILITY_REQUIREMENTS[
                            LEDGE_GRAB_CAPABILITY_REQUIREMENT
                        ]
                    ),
                )
                self.assertEqual(
                    adjusted[SWIM_CAPABILITY_REQUIREMENT],
                    (
                        RANDOMIZED_INNATE_CAPABILITY_REQUIREMENTS[
                            SWIM_CAPABILITY_REQUIREMENT
                        ]
                        if randomize_swim
                        else INNATE_CAPABILITY_REQUIREMENTS[
                            SWIM_CAPABILITY_REQUIREMENT
                        ]
                    ),
                )
