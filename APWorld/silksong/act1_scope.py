"""Act 1 goal content exclusions."""

from __future__ import annotations

from .locations import INDIVIDUAL_RELIC_ITEM_BY_TURN_IN_LOCATION
from .requirements import _ESTABLISHED_REQUIREMENTS_BY_LOCATION


ACT_ONE_GOAL_KEY = "act_1"

_POST_ACT_ONE_SOURCE_ACTS = frozenset(("Act 2", "Act 3"))
_UNTAGGED_POST_ACT_ONE_LOCATION_NAMES = frozenset(
    (
        "Throwing Ring",
        "Egg of Flealia",
        "Wish: Fine Pins",
        "Wish: The Wandering Merchant",
        "Wish: My Missing Brother",
        "Wish: Balm for the Wounded",
        "Wish: Cloaks of the Choir",
        "Wish: Building Up Songclave",
        "Wish: Strengthening Songclave",
        "Beastling Call",
        "Craw Summons",
        "Boss: Broodmother",
        "Boss: Second Sentinel",
        "Boss: Trobbio",
        "Boss: Palestag",
        "Boss: Clover Dancers",
        "Boss: Gurr the Outcast",
        "Boss: Raging Conchfly",
    )
)

_POST_ACT_ONE_SOURCE_LOCATION_NAMES = frozenset(
    location_name
    for location_name, requirements in
    _ESTABLISHED_REQUIREMENTS_BY_LOCATION.items()
    if (
        requirements
        and all(
            requirement.act in _POST_ACT_ONE_SOURCE_ACTS
            for requirement in requirements
        )
    )
) | _UNTAGGED_POST_ACT_ONE_LOCATION_NAMES

_POST_ACT_ONE_RELIC_TURN_IN_LOCATION_NAMES = frozenset(
    turn_in_location
    for turn_in_location, relic_item in
    INDIVIDUAL_RELIC_ITEM_BY_TURN_IN_LOCATION.items()
    if relic_item in _POST_ACT_ONE_SOURCE_LOCATION_NAMES
)

ACT_ONE_EXCLUDED_LOCATION_NAMES = (
    _POST_ACT_ONE_SOURCE_LOCATION_NAMES
    | _POST_ACT_ONE_RELIC_TURN_IN_LOCATION_NAMES
)


def get_act_one_excluded_location_names(
    major_key_mode: str = "vanilla",
    boss_mode: str = "anywhere",
) -> frozenset[str]:
    if major_key_mode not in {"vanilla", "shuffle", "anywhere"}:
        raise ValueError(
            f"Unknown Major Key randomization mode: {major_key_mode!r}"
        )
    if boss_mode not in {"vanilla", "shuffle", "anywhere"}:
        raise ValueError(f"Unknown Boss Sanity mode: {boss_mode!r}")

    if major_key_mode == "anywhere" and boss_mode == "anywhere":
        return ACT_ONE_EXCLUDED_LOCATION_NAMES
    return ACT_ONE_EXCLUDED_LOCATION_NAMES | frozenset(("Boss: Crawfather",))


def retains_randomized_craw_summons(
    major_key_mode: str,
    boss_mode: str,
) -> bool:
    return major_key_mode == "anywhere" and boss_mode == "anywhere"
