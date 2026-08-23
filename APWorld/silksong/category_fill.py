from __future__ import annotations

from collections import defaultdict
from typing import Iterable

from .items import get_vanilla_reward_name
from .locations import (
    OBSERVATION_LOCATION_CATEGORIES,
    PAIRED_LOCATION_CATEGORIES,
)
from .minor_families import get_base_randomization_category


def _placement_category(value) -> str | None:
    return getattr(value, "silksong_placement_category", None)


def _assign_items(locations: list, items: list) -> None:
    for location in locations:
        if location.item is not None:
            location.item.location = None
        location.item = None
    for location, item in zip(locations, items):
        location.item = item
        item.location = location


def _can_fill_without_access(multiworld, location, item) -> bool:
    return location.can_fill(
        multiworld.state,
        item,
        check_access=False,
    )


def _items_obey_fill_rules(
    multiworld,
    locations: list,
    items: list,
) -> bool:
    return all(
        _can_fill_without_access(multiworld, location, item)
        for location, item in zip(locations, items)
    )


def _match_items_to_locations(
    multiworld,
    category: str,
    locations: list,
    preferred_items: list,
) -> list:
    """Find a rule-safe perfect matching, preferring the native baseline."""

    if _items_obey_fill_rules(multiworld, locations, preferred_items):
        return preferred_items

    item_index_by_id = {
        id(item): index
        for index, item in enumerate(preferred_items)
    }
    candidates_by_location: dict[int, list[int]] = {}
    for location_index, location in enumerate(locations):
        preferred_index = item_index_by_id[
            id(preferred_items[location_index])
        ]
        candidates = [
            item_index
            for item_index, item in enumerate(preferred_items)
            if _can_fill_without_access(multiworld, location, item)
        ]
        if preferred_index in candidates:
            candidates.remove(preferred_index)
            candidates.insert(0, preferred_index)
        if not candidates:
            if category == "Relic":
                detail = (
                    "Jubilana is progression-restricted, so it needs an "
                    "available non-progression Relic that is also legal "
                    "under exclusion, locality and prior plando rules. "
                    if location.name
                    == "Relic: Choral Commandment (Jubilana)"
                    else ""
                )
                raise ValueError(
                    f"Relic shuffle has no legal reward for "
                    f"{location.name!r}. {detail}Exact Relic shuffle cannot "
                    "bypass these constraints. Remove the conflicting "
                    "exclude_locations, locality or plando setting or use "
                    "relic_randomization: anywhere."
                )
            raise ValueError(
                f"{category} shuffle has no legal reward for "
                f"{location.name!r}. Check excluded/local item settings."
            )
        candidates_by_location[location_index] = candidates

    location_by_item: dict[int, int] = {}

    def assign_location(
        location_index: int,
        seen_items: set[int],
    ) -> bool:
        for item_index in candidates_by_location[location_index]:
            if item_index in seen_items:
                continue
            seen_items.add(item_index)
            previous_location = location_by_item.get(item_index)
            if (
                previous_location is None
                or assign_location(previous_location, seen_items)
            ):
                location_by_item[item_index] = location_index
                return True
        return False

    for location_index in sorted(
        range(len(locations)),
        key=lambda index: len(candidates_by_location[index]),
    ):
        if not assign_location(location_index, set()):
            raise ValueError(
                f"{category} shuffle cannot satisfy all location safety, "
                "exclusion and locality rules."
            )

    item_by_location = {
        location_index: preferred_items[item_index]
        for item_index, location_index in location_by_item.items()
    }
    return [
        item_by_location[location_index]
        for location_index in range(len(locations))
    ]


def _pop_matching(items: list, predicate):
    for index, item in enumerate(items):
        if predicate(item):
            return items.pop(index)
    return None


def _build_native_baseline(
    category: str,
    locations: list,
    items: list,
) -> list:
    """Pair each player's category items with their native source first."""

    remaining = list(items)
    assigned: dict[object, object] = {}
    deferred = []

    native_category = get_base_randomization_category(category)
    for location in locations:
        reward_name = get_vanilla_reward_name(
            location.name,
            native_category,
        )
        item = _pop_matching(
            remaining,
            lambda candidate: (
                candidate.player == location.player
                and candidate.name == reward_name
            ),
        )
        if item is None:
            deferred.append(location)
        else:
            assigned[location] = item

    # The selected starting Crest is precollected and replaced with one
    # currency item. That produces exactly one same-player deferred pair.
    # The global fallback also keeps this compatible with legal cross-player
    # plando that already consumed a native copy.
    for location in deferred:
        item = _pop_matching(
            remaining,
            lambda candidate: candidate.player == location.player,
        )
        if item is None:
            item = remaining.pop(0)
        assigned[location] = item

    if remaining:
        raise ValueError(
            f"Could not build the {category} shuffle baseline. "
            f"{len(remaining)} items were left over."
        )
    return [assigned[location] for location in locations]


def _place_bootstrap_item(
    locations: list,
    items: list,
    player: int,
    location_name: str,
    item_name: str,
) -> None:
    location_index = next(
        (
            index
            for index, location in enumerate(locations)
            if location.player == player and location.name == location_name
        ),
        None,
    )
    item_index = next(
        (
            index for index, item in enumerate(items)
            if item.player == player and item.name == item_name
        ),
        None,
    )
    if location_index is None or item_index is None:
        return
    items[location_index], items[item_index] = (
        items[item_index],
        items[location_index],
    )


def _shuffle_is_accessible(
    multiworld,
    shuffled_locations: Iterable,
    silksong_players: Iterable[int],
) -> bool:
    unreachable_locations, unbeaten_players = (
        _shuffle_reachability_failures(
            multiworld,
            shuffled_locations,
            silksong_players,
        )
    )
    return not unreachable_locations and not unbeaten_players


def _minimal_accessibility_players(multiworld) -> frozenset[int]:
    players = set()
    for player in multiworld.player_ids:
        options = getattr(multiworld.worlds[player], "options", None)
        accessibility = getattr(options, "accessibility", None)
        if accessibility is None:
            continue
        value = getattr(accessibility, "value", accessibility)
        minimal_value = getattr(accessibility, "option_minimal", 2)
        if value == minimal_value or value in ("minimal", "none"):
            players.add(player)
    return frozenset(players)


def _shuffle_reachability_failures(
    multiworld,
    shuffled_locations: Iterable,
    silksong_players: Iterable[int],
) -> tuple[list[str], list[int]]:
    from Fill import sweep_from_pool

    maximum_state = sweep_from_pool(
        multiworld.state,
        multiworld.itempool,
    )
    minimal_players = _minimal_accessibility_players(multiworld)
    unreachable_locations = sorted(
        (
            f"P{location.player} {location.name}"
            for location in shuffled_locations
            if (
                not location.can_reach(maximum_state)
                and (
                    location.player not in minimal_players
                    or (
                        location.item is not None
                        and location.item.advancement
                        and location.item.player not in minimal_players
                    )
                )
            )
        ),
        key=str.casefold,
    )
    unbeaten_players = sorted(
        player
        for player in silksong_players
        if not multiworld.has_beaten_game(maximum_state, player)
    )
    return unreachable_locations, unbeaten_players


def prefill_category_shuffles(
    multiworld,
    game_name: str,
) -> None:
    """Place exact-category shuffle pools before Archipelago's generic fill.

    Archipelago's generic restrictive fill can spend minutes backtracking
    across many mutually exclusive lanes. Start from a known reachable native
    layout, break the two modeled bootstrap cycles, then randomize each lane
    while retaining only reachable progression layouts.
    """

    shuffled_items = [
        item
        for item in multiworld.itempool
        if _placement_category(item) is not None
    ]
    active_shuffled_locations = [
        location
        for location in (
            multiworld.get_locations()
            if callable(getattr(multiworld, "get_locations", None))
            else multiworld.get_unfilled_locations()
        )
        if _placement_category(location) is not None
    ]
    shuffled_locations = [
        location
        for location in multiworld.get_unfilled_locations()
        if _placement_category(location) is not None
    ]
    if not shuffled_items and not active_shuffled_locations:
        return

    items_by_category: dict[str, list] = defaultdict(list)
    locations_by_category: dict[str, list] = defaultdict(list)
    for item in shuffled_items:
        items_by_category[_placement_category(item)].append(item)
    for location in shuffled_locations:
        locations_by_category[_placement_category(location)].append(location)

    categories = sorted(
        set(items_by_category) | set(locations_by_category)
    )
    for category in categories:
        category_items = items_by_category[category]
        category_locations = locations_by_category[category]
        if len(category_items) != len(category_locations):
            raise ValueError(
                f"{category} shuffle has {len(category_items)} items but "
                f"{len(category_locations)} available locations. Check "
                "plando, start_inventory_from_pool, Item Links and category "
                "settings for incompatible placements."
            )
        category_items.sort(key=lambda item: (item.player, item.name))
        category_locations.sort(
            key=lambda location: (location.player, location.name)
        )

    planned_items_by_category: dict[str, list] = {}
    for category in categories:
        category_items = items_by_category[category]
        category_locations = locations_by_category[category]
        if category in OBSERVATION_LOCATION_CATEGORIES:
            planned_items = list(category_items)
            multiworld.random.shuffle(planned_items)
        elif (
            get_base_randomization_category(category)
            in PAIRED_LOCATION_CATEGORIES
        ):
            planned_items = _build_native_baseline(
                category,
                category_locations,
                category_items,
            )
        else:
            raise ValueError(
                f"Unsupported Silksong shuffle category: {category!r}"
            )
        planned_items_by_category[category] = _match_items_to_locations(
            multiworld,
            category,
            category_locations,
            planned_items,
        )

    silksong_players = sorted(
        player
        for player in multiworld.player_ids
        if multiworld.worlds[player].game == game_name
    )

    # Root the shuffled Skill lane from Quill through Silk Soar.
    if "Skill" in planned_items_by_category:
        for player in silksong_players:
            _place_bootstrap_item(
                locations_by_category["Skill"],
                planned_items_by_category["Skill"],
                player,
                "Item: Quill",
                "Swift Step",
            )
            _place_bootstrap_item(
                locations_by_category["Skill"],
                planned_items_by_category["Skill"],
                player,
                "Swift Step",
                "Cling Grip",
            )
            _place_bootstrap_item(
                locations_by_category["Skill"],
                planned_items_by_category["Skill"],
                player,
                "Cling Grip",
                "Silk Soar",
            )

    # The modeled Bellhart/Greymoor/Shellwood cluster otherwise has no
    # external entrance. Put either cluster Bellway at the first Deep Docks
    # booth, keeping the choice randomized and inside the Bellway lane.
    if "Bellway" in planned_items_by_category:
        for player in silksong_players:
            cluster_source = multiworld.random.choice(
                (
                    "Bellway: Bellhart",
                    "Bellway: Greymoor",
                )
            )
            _place_bootstrap_item(
                locations_by_category["Bellway"],
                planned_items_by_category["Bellway"],
                player,
                "Deep Docks - Bellway",
                cluster_source,
            )

    if "Ventrica" in planned_items_by_category:
        for player in silksong_players:
            _place_bootstrap_item(
                locations_by_category["Ventrica"],
                planned_items_by_category["Ventrica"],
                player,
                "Underworks - Ventrica",
                "Ventrica: Grand Bellway",
            )

    if "Map" in planned_items_by_category:
        for player in silksong_players:
            world = multiworld.worlds[player]
            get_requirement = getattr(
                world,
                "get_trails_end_requirement_key",
                None,
            )
            if not callable(get_requirement):
                continue
            if get_requirement() != "owned_maps":
                continue
            _place_bootstrap_item(
                locations_by_category["Map"],
                planned_items_by_category["Map"],
                player,
                "Hunter's March - Map Purchase",
                "Map: Hunter's March",
            )

    for category in categories:
        if not _items_obey_fill_rules(
            multiworld,
            locations_by_category[category],
            planned_items_by_category[category],
        ):
            raise ValueError(
                f"The {category} bootstrap layout conflicts with a location "
                "safety, exclusion or locality rule."
            )

    for category in categories:
        for location, item in zip(
            locations_by_category[category],
            planned_items_by_category[category],
        ):
            multiworld.push_item(location, item, False)
            location.locked = True

    placed_item_ids = {id(item) for item in shuffled_items}
    multiworld.itempool[:] = [
        item
        for item in multiworld.itempool
        if id(item) not in placed_item_ids
    ]

    all_shuffled_locations = active_shuffled_locations
    if not _shuffle_is_accessible(
        multiworld,
        all_shuffled_locations,
        silksong_players,
    ):
        unreachable_locations, unbeaten_players = (
            _shuffle_reachability_failures(
                multiworld,
                all_shuffled_locations,
                silksong_players,
            )
        )
        details = []
        if unreachable_locations:
            details.append(
                "Unreachable tagged locations: "
                + ", ".join(unreachable_locations)
                + "."
            )
        if unbeaten_players:
            details.append(
                "Unbeaten players: "
                + ", ".join(f"P{player}" for player in unbeaten_players)
                + "."
            )
        raise ValueError(
            "The safe category-shuffle baseline is not reachable. "
            + " ".join(details)
            + " "
            "This indicates a logic or plando conflict."
        )

    protected_locations = set()
    for player in silksong_players:
        world = multiworld.worlds[player]
        early_dash_enabled = getattr(world, "is_early_dash_enabled", None)
        if not (
            early_dash_enabled()
            if callable(early_dash_enabled)
            else bool(world.options.early_dash.value)
        ):
            continue
        player_skill_locations = [
            location
            for location in locations_by_category.get("Skill", ())
            if location.player == player
        ]
        get_location = getattr(multiworld, "get_location", None)
        existing_quill = (
            get_location("Item: Quill", player)
            if callable(get_location)
            else None
        )
        if (
            existing_quill is not None
            and existing_quill.item is not None
            and existing_quill.item.player == player
            and existing_quill.item.name == "Swift Step"
        ):
            # A tagged Quill was filled by this pre-fill and must remain
            # reserved through the later Skill permutation. A Quill outside
            # this list was already filled by plando, so it is already locked
            # independently and needs no additional protection here.
            if existing_quill in player_skill_locations:
                protected_locations.add(existing_quill)
            multiworld.local_early_items[player].pop(
                "Swift Step",
                None,
            )
            continue
        if not player_skill_locations:
            continue
        quill_location = next(
            (
                location
                for location in player_skill_locations
                if (
                    location.name == "Item: Quill"
                    and location.item is not None
                    and location.item.player == player
                    and location.item.name == "Swift Step"
                )
            ),
            None,
        )
        if quill_location is None:
            raise ValueError(
                "early_dash with Skill shuffle could not reserve Dash at "
                f"{multiworld.player_name[player]}'s opening Skill source."
            )
        protected_locations.add(quill_location)
        # The normal early-item phase runs after pre-fill. We have already
        # fulfilled this local guarantee, so remove the stale request.
        multiworld.local_early_items[player].pop("Swift Step", None)

    # Try a full permutation per lane. If it creates a progression cycle,
    # walk outward from the valid baseline through a handful of individually
    # validated swaps instead. Non-progression rewards can always permute
    # freely after progression positions are settled.
    for category in categories:
        locations = [
            location
            for location in locations_by_category[category]
            if location not in protected_locations
        ]
        if len(locations) < 2:
            continue

        original_items = [location.item for location in locations]
        if not any(item.advancement for item in original_items):
            for _attempt in range(4):
                randomized_items = list(original_items)
                multiworld.random.shuffle(randomized_items)
                if _items_obey_fill_rules(
                    multiworld,
                    locations,
                    randomized_items,
                ):
                    _assign_items(locations, randomized_items)
                    break
            continue

        randomized_items = list(original_items)
        multiworld.random.shuffle(randomized_items)
        accepted_full_shuffle = False
        if _items_obey_fill_rules(
            multiworld,
            locations,
            randomized_items,
        ):
            _assign_items(locations, randomized_items)
            accepted_full_shuffle = _shuffle_is_accessible(
                multiworld,
                all_shuffled_locations,
                silksong_players,
            )
        if not accepted_full_shuffle:
            _assign_items(locations, original_items)
            attempts = min(12, max(4, len(locations)))
            for _attempt in range(attempts):
                first, second = multiworld.random.sample(locations, 2)
                if not (
                    _can_fill_without_access(
                        multiworld,
                        first,
                        second.item,
                    )
                    and _can_fill_without_access(
                        multiworld,
                        second,
                        first.item,
                    )
                ):
                    continue
                first.item, second.item = second.item, first.item
                first.item.location = first
                second.item.location = second
                if not _shuffle_is_accessible(
                    multiworld,
                    all_shuffled_locations,
                    silksong_players,
                ):
                    first.item, second.item = second.item, first.item
                    first.item.location = first
                    second.item.location = second

        non_progression_locations = [
            location
            for location in locations
            if not location.item.advancement
        ]
        if len(non_progression_locations) > 1:
            original_non_progression_items = [
                location.item
                for location in non_progression_locations
            ]
            for _attempt in range(4):
                non_progression_items = list(
                    original_non_progression_items
                )
                multiworld.random.shuffle(non_progression_items)
                if _items_obey_fill_rules(
                    multiworld,
                    non_progression_locations,
                    non_progression_items,
                ):
                    _assign_items(
                        non_progression_locations,
                        non_progression_items,
                    )
                    break
