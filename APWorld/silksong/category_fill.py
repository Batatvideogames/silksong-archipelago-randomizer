from __future__ import annotations

from collections import defaultdict
from typing import Iterable

from BaseClasses import CollectionState, LocationProgressType
from Options import OptionError

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
    return location.player == item.player and location.can_fill(
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
    # currency item. That produces exactly one deferred pair.
    for location in deferred:
        assigned[location] = remaining.pop(0)

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
    reachability_context=None,
) -> bool:
    unreachable_locations, unbeaten_players = (
        _shuffle_reachability_failures(
            multiworld,
            shuffled_locations,
            silksong_players,
            reachability_context,
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
    reachability_context=None,
    maximum_state=None,
) -> tuple[list[str], list[int]]:
    from Fill import sweep_from_pool

    if maximum_state is None:
        if reachability_context is None:
            maximum_state = sweep_from_pool(multiworld.state, multiworld.itempool)
        else:
            maximum_pool_state, filled_locations = reachability_context
            maximum_state = sweep_from_pool(maximum_pool_state, locations=filled_locations)
    minimal_players = _minimal_accessibility_players(multiworld)
    unreachable_locations = sorted(
        (
            f"P{location.player} {location.name}"
            for location in shuffled_locations
            if (
                not location.can_reach(maximum_state)
                and getattr(
                    location,
                    "progress_type",
                    LocationProgressType.DEFAULT,
                ) != LocationProgressType.EXCLUDED
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


def _build_shuffle_reachability_context(multiworld):
    from Fill import sweep_from_pool

    return (
        sweep_from_pool(
            multiworld.state,
            multiworld.itempool,
            locations=(),
        ),
        tuple(multiworld.get_filled_locations()),
    )


def _repair_shuffle_swaps(
    multiworld, shuffled_locations, silksong_players,
    candidates, protected_locations, reachability_context,
) -> bool:
    from Fill import sweep_from_pool

    priority = {"Skill": 0, "Bellway": 1, "Ventrica": 2, "Melody": 3, "Spell": 4, "BellShrine": 5}
    candidates = sorted(
        (location for location in candidates if location not in protected_locations),
        key=lambda location: (priority.get(_placement_category(location), 6),
                              location.player, _placement_category(location), location.name),
    )
    if len(candidates) < 2:
        return False
    pool_state, filled_locations = reachability_context
    all_items_state = sweep_from_pool(pool_state, locations=filled_locations)
    for location in candidates:
        all_items_state.collect(location.item, True)
    if not _shuffle_is_accessible(
        multiworld, shuffled_locations, silksong_players,
        (all_items_state, filled_locations),
    ):
        return False

    baseline = [location.item for location in candidates]
    accepted = False
    attempts = 0
    try:
        failures = tuple(map(set, _shuffle_reachability_failures(
            multiworld, shuffled_locations, silksong_players, reachability_context,
        )))
        while any(failures) and attempts < 128:
            state = sweep_from_pool(pool_state, locations=filled_locations)
            reachable_count = sum(location.can_reach(state) for location in filled_locations)
            reached = [location for location in candidates if location.can_reach(state)]
            blocked = [location for location in candidates
                       if location.item.advancement and not location.can_reach(state)]
            improved = False
            for source in blocked:
                for target in reached:
                    if (source.player != target.player
                            or _placement_category(source) != _placement_category(target)
                            or not _can_fill_without_access(multiworld, source, target.item)
                            or not _can_fill_without_access(multiworld, target, source.item)):
                        continue
                    before = [source.item, target.item]
                    _assign_items([source, target], before[::-1])
                    attempts += 1
                    trial_state = sweep_from_pool(pool_state, locations=filled_locations)
                    remaining = tuple(map(set, _shuffle_reachability_failures(
                        multiworld, shuffled_locations, silksong_players, reachability_context,
                        maximum_state=trial_state,
                    )))
                    improves_reach = (remaining == failures and
                                     sum(location.can_reach(trial_state) for location in filled_locations)
                                     > reachable_count)
                    if all(a <= b for a, b in zip(remaining, failures)) and (remaining != failures or improves_reach):
                        failures = remaining
                        improved = True
                        break
                    _assign_items([source, target], before)
                    if attempts >= 128:
                        break
                if improved or attempts >= 128:
                    break
            if not improved:
                return False
        accepted = not any(failures)
        return accepted
    finally:
        if not accepted:
            _assign_items(candidates, baseline)


def _repair_shuffle_bootstrap(
    multiworld, shuffled_locations, silksong_players,
    candidates, protected_locations, reachability_context,
) -> bool:
    from Fill import FillError, fill_restrictive, sweep_from_pool

    if _repair_shuffle_swaps(
        multiworld, shuffled_locations, silksong_players,
        candidates, protected_locations, reachability_context,
    ):
        return True

    skill_locations = [location for location in candidates
                       if _placement_category(location) == "Skill"
                       and location not in protected_locations]
    if len(skill_locations) < 2:
        return False

    pool_state, filled_locations = reachability_context
    assumed_state = pool_state.copy()
    for location in candidates:
        if location not in skill_locations:
            assumed_state.collect(location.item, True)
    skill_items = [location.item for location in skill_locations]
    maximum_state = sweep_from_pool(assumed_state, skill_items, locations=filled_locations)
    if any(_shuffle_reachability_failures(
        multiworld, shuffled_locations, silksong_players,
        maximum_state=maximum_state,
    )):
        return False

    baseline = [location.item for location in candidates]
    accepted = False
    try:
        for location in skill_locations:
            location.item.location = None
            location.item = None
        fill_restrictive(
            multiworld, assumed_state, skill_locations[:], skill_items,
            lock=True, one_item_per_player=False, name="Skill shuffle",
        )
        if skill_items or any(location.item is None for location in skill_locations):
            return False
        accepted = _repair_shuffle_swaps(
            multiworld, shuffled_locations, silksong_players,
            candidates, protected_locations, reachability_context,
        )
        return accepted
    except FillError:
        return False
    finally:
        if not accepted:
            _assign_items(candidates, baseline)


def _validate_unrestricted_opening(multiworld):
    if multiworld.players != 1:
        return
    items = [item for item in multiworld.itempool
             if item.advancement and _placement_category(item) is None]
    if not items:
        return
    state = CollectionState(multiworld)
    for item in multiworld.itempool:
        if _placement_category(item) is not None:
            state.collect(item, True)
    for location in multiworld.get_filled_locations():
        if _placement_category(location) is not None:
            state.collect(location.item, True)
    state.sweep_for_advancements()
    if multiworld.has_beaten_game(state):
        return
    if any(location.can_reach(state) and any(
            location.can_fill(state, item, check_access=False) for item in items)
            for location in multiworld.get_unfilled_locations()):
        return
    raise OptionError(
        "The starting and entrance settings leave no reachable check that can hold "
        "unrestricted progression, even with all category-shuffled items available. "
        "Set an early check category to Anywhere, change the starting location, "
        "or provide a starting movement item."
    )


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

    _validate_unrestricted_opening(multiworld)
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

    items_by_lane: dict[tuple[str, int], list] = defaultdict(list)
    locations_by_lane: dict[tuple[str, int], list] = defaultdict(list)
    for item in shuffled_items:
        items_by_lane[(_placement_category(item), item.player)].append(item)
    for location in shuffled_locations:
        locations_by_lane[
            (_placement_category(location), location.player)
        ].append(location)

    lanes = sorted(set(items_by_lane) | set(locations_by_lane))
    for category, player in lanes:
        category_items = items_by_lane[(category, player)]
        category_locations = locations_by_lane[(category, player)]
        if len(category_items) != len(category_locations):
            raise ValueError(
                f"P{player} {category} shuffle has "
                f"{len(category_items)} items but "
                f"{len(category_locations)} available locations. Check "
                "plando, start_inventory_from_pool, Item Links and category "
                "settings for incompatible placements."
            )
        category_items.sort(key=lambda item: item.name)
        category_locations.sort(key=lambda location: location.name)

    planned_items_by_lane: dict[tuple[str, int], list] = {}
    for category, player in lanes:
        lane = (category, player)
        category_items = items_by_lane[lane]
        category_locations = locations_by_lane[lane]
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
        planned_items_by_lane[lane] = _match_items_to_locations(
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

    protected_locations = set()
    opening_state = CollectionState(multiworld)
    opening_state.sweep_for_advancements()
    for player in silksong_players:
        lane = ("Skill", player)
        if lane in planned_items_by_lane:
            world = multiworld.worlds[player]
            if world.is_early_dash_enabled() and opening_state.has("Swift Step", player):
                multiworld.local_early_items[player].pop("Swift Step", None)
            elif world.is_early_dash_enabled():
                dash = next((item for item in planned_items_by_lane[lane]
                             if item.name == "Swift Step"), None)
                candidates = [location for location in locations_by_lane[lane]
                              if dash is not None and location.can_fill(opening_state, dash)]
                if not candidates:
                    raise OptionError("Early Dash with Skill shuffle needs a reachable starting Skill check. "
                                      "Disable Early Dash or change the starting settings; Quill is a separate check.")
                opening = multiworld.random.choice(candidates)
                _place_bootstrap_item(locations_by_lane[lane], planned_items_by_lane[lane],
                                      player, opening.name, "Swift Step")
                protected_locations.add(opening)
            if not any(location.name == "Swift Step" for location in protected_locations
                       if location.player == player):
                _place_bootstrap_item(locations_by_lane[lane], planned_items_by_lane[lane],
                                      player, "Swift Step", "Cling Grip")
            if not any(location.name == "Cling Grip" for location in protected_locations
                       if location.player == player):
                _place_bootstrap_item(locations_by_lane[lane], planned_items_by_lane[lane],
                                      player, "Cling Grip", "Clawline")

    # The modeled Bellhart/Greymoor/Shellwood cluster otherwise has no
    # external entrance. Put either cluster Bellway at the first Deep Docks
    # booth, keeping the choice randomized and inside the Bellway lane.
    for player in silksong_players:
        lane = ("Bellway", player)
        if lane in planned_items_by_lane:
            cluster_source = multiworld.random.choice(
                (
                    "Bellway: Bellhart",
                    "Bellway: Greymoor",
                )
            )
            _place_bootstrap_item(
                locations_by_lane[lane],
                planned_items_by_lane[lane],
                player,
                "Deep Docks - Bellway",
                cluster_source,
            )

    for player in silksong_players:
        lane = ("Ventrica", player)
        if lane in planned_items_by_lane:
            _place_bootstrap_item(
                locations_by_lane[lane],
                planned_items_by_lane[lane],
                player,
                "Underworks - Ventrica",
                "Ventrica: Grand Bellway",
            )

    for player in silksong_players:
        lane = ("Map", player)
        if lane in planned_items_by_lane:
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
                locations_by_lane[lane],
                planned_items_by_lane[lane],
                player,
                "Hunter's March - Map Purchase",
                "Map: Hunter's March",
            )

    for category, player in lanes:
        lane = (category, player)
        if not _items_obey_fill_rules(
            multiworld,
            locations_by_lane[lane],
            planned_items_by_lane[lane],
        ):
            raise ValueError(
                f"The P{player} {category} bootstrap layout conflicts with "
                "a location safety, exclusion or locality rule."
            )

    for lane in lanes:
        for location, item in zip(
            locations_by_lane[lane],
            planned_items_by_lane[lane],
        ):
            multiworld.push_item(location, item, False)
            location.locked = True

    placed_item_ids = {id(item) for item in shuffled_items}
    multiworld.itempool[:] = [
        item
        for item in multiworld.itempool
        if id(item) not in placed_item_ids
    ]
    reachability_context = _build_shuffle_reachability_context(multiworld)

    all_shuffled_locations = active_shuffled_locations
    if not _shuffle_is_accessible(
        multiworld,
        all_shuffled_locations,
        silksong_players,
        reachability_context,
    ) and not _repair_shuffle_bootstrap(
        multiworld, all_shuffled_locations, silksong_players,
        [location for locations in locations_by_lane.values() for location in locations],
        protected_locations, reachability_context,
    ):
        unreachable_locations, unbeaten_players = (
            _shuffle_reachability_failures(
                multiworld,
                all_shuffled_locations,
                silksong_players,
                reachability_context,
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

    for location in protected_locations:
        multiworld.local_early_items[location.player].pop("Swift Step", None)

    # Try a full permutation per lane. If it creates a progression cycle,
    # walk outward from the valid baseline through a handful of individually
    # validated swaps instead. Non-progression rewards can always permute
    # freely after progression positions are settled.
    for lane in lanes:
        locations = [
            location
            for location in locations_by_lane[lane]
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
                reachability_context,
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
                    reachability_context,
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
