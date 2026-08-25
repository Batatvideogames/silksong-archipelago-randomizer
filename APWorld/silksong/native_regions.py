from __future__ import annotations

from collections.abc import Mapping

from BaseClasses import Region
from rule_builder.rules import Rule

from .requirement_rules import build_location_rule, build_requirements_rule
from .requirements import (
    LocationRequirement,
    get_abstract_requirements,
)
from .room_graph_logic import ROOM_NODE_PREFIX


def native_rule_options(world) -> dict[str, object]:
    return {
        "split_dash_and_sprint": world.is_split_dash_and_sprint(),
        "allow_bellways_before_bell_beast": (
            world.allows_bellways_before_bell_beast()
        ),
        "skips_tier": world.get_skips_tier(),
        "randomized_crest_slots_enabled": (
            world.get_category_mode("CrestSlot") != "vanilla"
        ),
        "starting_location": world.get_starting_location_key(),
        "trails_end_requirement": world.get_trails_end_requirement_key(),
        "scuttlebrace_logic_enabled": (
            world.is_scuttlebrace_logic_enabled()
        ),
        "native_abstract_regions": True,
    }


def get_native_abstract_requirements(world):
    return get_abstract_requirements(
        world.allows_bellways_before_bell_beast(),
        world.get_category_mode("CrestSlot") != "vanilla",
        world.get_starting_location_key(),
        world.get_trails_end_requirement_key(),
    )


def choose_requirement_anchor(
    requirement: LocationRequirement,
    abstract_names: frozenset[str],
    owner: str | None = None,
) -> str | None:
    candidates = tuple(
        name
        for name in requirement.all_of
        if name in abstract_names
    )
    return next(
        (name for name in candidates if name != owner),
        candidates[0] if candidates else None,
    )


def choose_location_anchor(
    requirements: tuple[LocationRequirement, ...],
    abstract_names: frozenset[str],
) -> str | None:
    if not requirements:
        return None
    common = set(
        name
        for name in requirements[0].all_of
        if name in abstract_names
    )
    for requirement in requirements[1:]:
        common.intersection_update(
            name
            for name in requirement.all_of
            if name in abstract_names
        )
    if not common:
        return None
    return next(
        (
            name
            for name in sorted(common)
            if name.startswith(ROOM_NODE_PREFIX)
        ),
        sorted(common)[0],
    )


def create_native_logic_region_map(world) -> Mapping[str, Region]:
    requirements = get_native_abstract_requirements(world)
    abstract_names = frozenset(requirements)
    regions = {
        name: Region(name, world.player, world.multiworld)
        for name in abstract_names
    }
    world.multiworld.regions.extend(regions.values())
    world._silksong_native_abstract_requirements = requirements
    world._silksong_native_abstract_names = abstract_names
    return regions


def native_source_requires_assumption(
    world,
    location_name: str,
    reward_name: str,
    pollip_heart_count: int,
    anchor_requirement_name: str | None,
) -> bool:
    child = build_location_rule(
        location_name,
        pollip_heart_count=pollip_heart_count,
        anchor_requirement_name=anchor_requirement_name,
        **native_rule_options(world),
    ).resolve(world)
    if (
        child.player != world.player
        or reward_name in child.item_dependencies()
        or child.location_dependencies()
        or child.entrance_dependencies()
    ):
        return True
    pending = set(child.region_dependencies())
    if anchor_requirement_name is not None:
        pending.add(anchor_requirement_name)
    visited: set[str] = set()
    while pending:
        region_name = pending.pop()
        if region_name in visited:
            continue
        try:
            region = world.multiworld.get_region(
                region_name,
                world.player,
            )
        except KeyError:
            return True
        if region.player != world.player:
            return True
        visited.add(region_name)
        for entrance in region.entrances:
            access_rule = entrance.access_rule
            if (
                not isinstance(access_rule, Rule.Resolved)
                or access_rule.player != world.player
                or reward_name in access_rule.item_dependencies()
                or access_rule.location_dependencies()
                or access_rule.entrance_dependencies()
            ):
                return True
            pending.update(access_rule.region_dependencies())
            parent_region = entrance.parent_region
            if (
                parent_region is None
                or parent_region.player != world.player
            ):
                return True
            pending.add(parent_region.name)
    return False


def connect_native_logic_regions(
    world,
    menu: Region,
    regions: Mapping[str, Region],
) -> None:
    requirements = world._silksong_native_abstract_requirements
    abstract_names = world._silksong_native_abstract_names
    options = native_rule_options(world)

    for owner, alternatives in requirements.items():
        target = regions[owner]
        for index, requirement in enumerate(alternatives, 1):
            anchor = choose_requirement_anchor(
                requirement,
                abstract_names,
                owner,
            )
            entrance = world.create_entrance(
                regions[anchor] if anchor is not None else menu,
                target,
                build_requirements_rule(
                    (requirement,),
                    anchor_requirement_name=anchor,
                    **options,
                ),
                f"Silksong Logic: {owner} [{index}]",
            )
            if entrance is None:
                continue
