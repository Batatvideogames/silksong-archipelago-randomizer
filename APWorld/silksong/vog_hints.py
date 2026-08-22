from __future__ import annotations

import hashlib
from typing import Iterable

from BaseClasses import CollectionState, ItemClassification

from .requirements import REQUIREMENTS


VOG_HINT_ROOM_AREA_NAMES: dict[str, str] = {
    "bone-bottom": "Mosslands",
    "the-marrow": "The Marrow",
    "weavenest-atla": "Weavenest",
    "wormways": "Wormways",
    "deep-docks": "Deep Docks",
    "shellwood": "Shellwood",
    "bellhart": "Bellhart",
    "far-fields": "Far Fields",
    "hunter-s-march": "Hunter's March",
    "cogwork-core": "Outlying Citadel",
    "grand-gate": "Grand Gate",
    "greymoor": "Greymoor",
    "moss-grotto": "Mosslands",
    "sinner-s-road": "Sinner's Road",
    "underworks": "Underworks",
    "whisp-thicket": "Greymoor",
    "blasted-steps": "Blasted Steps",
    "the-mist": "The Mist",
    "whispering-vaults": "Whispering Vaults",
    "high-halls": "High Halls",
    "whiteward": "Whiteward",
    "white-ward": "Whiteward",
    "bilewater": "Bilewater",
    "sands-of-karak": "Sands of Karak",
    "the-slab": "The Slab",
}

# Folder names usually match Vog's hint areas. The Choral Chambers folder also
# holds Grand Bellway and Songclave rooms, so those rooms need their own entries.
VOG_HINT_ROOM_ID_AREA_NAMES: dict[str, str] = {
    "choral-chambers/choral-chambers-below-ventrica": "Choral Chambers",
    "choral-chambers/choral-chambers-flea-room": "Choral Chambers",
    "choral-chambers/choral-chambers-flea-shaft": "Choral Chambers",
    "choral-chambers/choral-chambers-maintenance-tunnel": "Choral Chambers",
    "choral-chambers/choral-chambers-merchant-room": "Choral Chambers",
    "choral-chambers/choral-chambers-outside-spa": "Choral Chambers",
    "choral-chambers/choral-chambers-over-dininig": "Choral Chambers",
    "choral-chambers/choral-chambers-ventrica-room": "Choral Chambers",
    "choral-chambers/grand-bellway": "Choral Chambers",
    "choral-chambers/grand-bellway-side-room": "Choral Chambers",
    "choral-chambers/songclave": "Choral Chambers",
    "choral-chambers/songclave-tube": "Choral Chambers",
}

VOG_HINT_SHAKRA_AREAS = frozenset(
    (
        "Mosslands",
        "The Marrow",
        "Deep Docks",
        "Far Fields",
        "Wormways",
        "Hunter's March",
        "Greymoor",
        "Bellhart",
        "Shellwood",
        "Blasted Steps",
        "Sinner's Road",
        "Mount Fay",
        "Sands of Karak",
        "Bilewater",
    )
)

VOG_HINT_AREA_OVERRIDES: dict[str, frozenset[str]] = {
    "Throwing Ring": frozenset(("Bilewater",)),
    "Egg of Flealia": frozenset(("Putrified Ducts",)),
    "Bellhart - Bellshrine": frozenset(("Bellhart",)),
    "Boss: Bell Beast": frozenset(("The Marrow",)),
    "Boss: Last Judge": frozenset(("Grand Gate",)),
    "Relic: Weaver Effigy (Keelal, Shellwood)":
        frozenset(("Shellwood",)),
    "Boss: Crawfather": frozenset(("Greymoor",)),
    "Boss: Broodmother": frozenset(("The Slab",)),
    "Boss: Second Sentinel": frozenset(("High Halls",)),
    "Boss: Palestag": frozenset(("Verdania",)),
    "Boss: Clover Dancers": frozenset(("Verdania",)),
    "Wish: Queen's Egg": frozenset(("Sinner's Road",)),
    "Wish: Fine Pins": frozenset(("Songclave",)),
    "Wish: The Wandering Merchant":
        frozenset(("Choral Chambers",)),
    "Wish: My Missing Brother": frozenset(("Sinner's Road",)),
    "Wish: Balm for the Wounded": frozenset(("Whiteward",)),
    "Wish: Cloaks of the Choir": frozenset(("Songclave",)),
    "Wish: Building Up Songclave": frozenset(("Songclave",)),
    "Wish: Strengthening Songclave": frozenset(("Songclave",)),
    "Craw Summons": frozenset(("Greymoor",)),
    "Craggler - Beast Shard": frozenset(("Wormways",)),
    "Boss: Gurr the Outcast": frozenset(("Far Fields",)),
    "Boss: Raging Conchfly": frozenset(("Sands of Karak",)),
    "Pin Purchase: Bellway Pins": VOG_HINT_SHAKRA_AREAS,
    "Pin Purchase: Vendor Pins": VOG_HINT_SHAKRA_AREAS,
    **{
        location_name: frozenset(("Bellhart", "Shellwood"))
        for location_name in (
            "Pinmaster Plinney: Sharpened Needle",
            "Pinmaster Plinney: Shining Needle",
            "Pinmaster Plinney: Hivesteel Needle",
            "Pinmaster Plinney: Pale Steel Needle",
        )
    },
}

VOG_HINT_AREALESS_LOCATIONS = frozenset(
    {
        "Crest: Hunter",
        "Goal",
        "Beastling Call",
        *(
            f"Crest Slot: {crest} ({color} {slot})"
            for crest, color, slot in (
                ("Hunter", "Red", 1),
                ("Hunter", "Blue", 1),
                ("Hunter", "Yellow", 1),
                ("Reaper", "Red", 1),
                ("Reaper", "Blue", 1),
                ("Reaper", "Yellow", 1),
                ("Wanderer", "Blue", 1),
                ("Wanderer", "Blue", 2),
                ("Wanderer", "Yellow", 1),
                ("Beast", "Yellow", 1),
                ("Beast", "Yellow", 2),
                ("Witch", "Red", 1),
                ("Witch", "Blue", 1),
                ("Witch", "Blue", 2),
                ("Architect", "Blue", 1),
                ("Architect", "Yellow", 1),
                ("Architect", "Yellow", 2),
                ("Architect", "Blue", 2),
                ("Shaman", "Blue", 1),
                ("Shaman", "Blue", 2),
            )
        ),
    }
)

def empty_vog_hint_plan() -> dict[str, object]:
    return {
        "woth_areas": [],
        "foolish_areas": [],
        "general_locations": [],
        "general_count": 0,
    }


def copy_vog_hint_plan(
    plan: dict[str, object],
) -> dict[str, object]:
    return {
        "woth_areas": list(plan.get("woth_areas", ())),
        "foolish_areas": list(plan.get("foolish_areas", ())),
        "general_locations": list(
            plan.get("general_locations", ())
        ),
        "general_count": int(plan.get("general_count", 0)),
    }


def get_vog_hint_areas(location_name: str) -> frozenset[str]:
    override_areas = VOG_HINT_AREA_OVERRIDES.get(location_name)
    if override_areas is not None:
        return override_areas

    areas: set[str] = set()
    for requirement in REQUIREMENTS.get(location_name, ()):
        path_name = requirement.path_name.strip()
        if path_name:
            area_name = path_name.split(" - ", 1)[0].strip()
            if area_name:
                areas.add(area_name)

        for dependency in (*requirement.all_of, *requirement.any_of):
            if not dependency.startswith("Room Node: "):
                continue
            room_id = dependency.removeprefix("Room Node: ").split(
                "#",
                1,
            )[0]
            area_name = VOG_HINT_ROOM_ID_AREA_NAMES.get(room_id)
            area_id = room_id.split(
                "/",
                1,
            )[0]
            if area_name is None:
                area_name = VOG_HINT_ROOM_AREA_NAMES.get(area_id)
            if area_name:
                areas.add(area_name)

    return frozenset(areas)


def _location_sort_key(location) -> tuple[int, str, int, str]:
    item = location.item
    return (
        int(location.player),
        str(location.name),
        int(item.player),
        str(item.name),
    )


def find_required_playthrough_locations(multiworld) -> frozenset[object]:
    """Return the locations retained by conventional playthrough pruning."""

    progression_locations = {
        location
        for location in multiworld.get_filled_locations()
        if location.item.advancement
    }
    if not progression_locations:
        return frozenset()

    state = CollectionState(multiworld)
    state_cache = [state.copy()]
    collection_spheres: list[set[object]] = []
    sphere_candidates = set(progression_locations)

    while sphere_candidates:
        sphere = {
            location
            for location in sphere_candidates
            if state.can_reach(location)
        }
        if not sphere:
            if multiworld.has_beaten_game(state):
                break
            raise RuntimeError(
                "Vog hint generation could not build a complete "
                "playthrough."
            )

        for location in sorted(sphere, key=_location_sort_key):
            state.collect(location.item, True, location)
        sphere_candidates.difference_update(sphere)
        collection_spheres.append(sphere)
        state_cache.append(state.copy())

    required_locations = {
        location
        for sphere in collection_spheres
        for location in sphere
    }
    for sphere_index in range(len(collection_spheres) - 1, -1, -1):
        for location in sorted(
            collection_spheres[sphere_index],
            key=_location_sort_key,
        ):
            required_locations.remove(location)
            if not multiworld.can_beat_game(
                state_cache[sphere_index],
                required_locations,
            ):
                required_locations.add(location)

    return frozenset(required_locations)


def _hint_sort_key(
    world,
    category: str,
    value: str,
) -> tuple[bytes, str]:
    seed_name = str(getattr(world.multiworld, "seed_name", ""))
    digest = hashlib.sha256(
        (
            seed_name
            + "\0"
            + str(world.player)
            + "\0"
            + category
            + "\0"
            + value
        ).encode("utf-8")
    ).digest()
    return digest, value


def _is_addressed(location) -> bool:
    return getattr(
        location,
        "address",
        getattr(location, "code", None),
    ) is not None


def _is_progression_or_useful(item) -> bool:
    major_flags = (
        ItemClassification.progression
        | ItemClassification.useful
    )
    return bool(item.classification & major_flags)


def _ordered_values(
    world,
    category: str,
    values: Iterable[str],
) -> list[str]:
    return sorted(
        set(values),
        key=lambda value: _hint_sort_key(world, category, value),
    )


def build_vog_hint_plan(
    world,
    required_locations: Iterable[object],
) -> dict[str, object]:
    woth_count, foolish_count, general_count = (
        world.get_vog_hint_counts()
    )
    if not any((woth_count, foolish_count, general_count)):
        return empty_vog_hint_plan()

    required_location_set = set(required_locations)
    local_locations = [
        location
        for location in world.multiworld.get_locations()
        if location.player == world.player and _is_addressed(location)
    ]

    area_members: dict[str, list[object]] = {}
    incomplete_areas: set[str] = set()
    major_locations: list[object] = []

    for location in local_locations:
        areas = get_vog_hint_areas(location.name)
        if location.item is None:
            incomplete_areas.update(areas)
            continue

        if _is_progression_or_useful(location.item):
            major_locations.append(location)

        for area_name in areas:
            area_members.setdefault(area_name, []).append(location)

    woth_candidates = (
        next(iter(areas))
        for location in required_location_set
        if location.player == world.player and _is_addressed(location)
        for areas in (get_vog_hint_areas(location.name),)
        if len(areas) == 1
    )
    woth_areas = _ordered_values(
        world,
        "woth",
        woth_candidates,
    )[:woth_count]

    foolish_candidates = (
        area_name
        for area_name, members in area_members.items()
        if (
            area_name not in incomplete_areas
            and members
            and all(
                member.item is not None
                and not _is_progression_or_useful(member.item)
                for member in members
            )
        )
    )
    foolish_areas = _ordered_values(
        world,
        "foolish",
        foolish_candidates,
    )[:foolish_count]

    general_candidates = (
        location.name
        for location in major_locations
        if location not in required_location_set
    )
    general_locations = _ordered_values(
        world,
        "general",
        general_candidates,
    )

    return {
        "woth_areas": woth_areas,
        "foolish_areas": foolish_areas,
        "general_locations": general_locations,
        "general_count": min(general_count, len(general_locations)),
    }
