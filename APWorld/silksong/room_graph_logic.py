from __future__ import annotations

from dataclasses import dataclass
from itertools import product
from types import MappingProxyType
from typing import Iterable, Mapping

from .room_graph import (
    LogicStatus,
    RequirementMode,
    RequirementSpec,
    load_room_graph,
)


ROOM_NODE_PREFIX = "Room Node: "
ROOM_EVENT_PREFIX = "Room Event: "


@dataclass(frozen=True)
class CompiledRoomClause:
    """One monotonic Archipelago requirement alternative."""

    all_of: tuple[str, ...] = ()
    item_counts: tuple[tuple[str, int], ...] = ()
    require_silk_spear: bool = False
    requires_easy_skips: bool = False


@dataclass(frozen=True)
class CompiledRoomGraph:
    node_requirements: Mapping[str, tuple[CompiledRoomClause, ...]]
    event_requirements: Mapping[str, tuple[CompiledRoomClause, ...]]
    check_requirements: Mapping[str, tuple[CompiledRoomClause, ...]]
    check_source_ids: Mapping[str, tuple[str, ...]]
    authoritative_check_names: frozenset[str]
    quarantined_check_names: frozenset[str]
    structurally_reachable_nodes: frozenset[str]
    semantically_reachable_nodes: frozenset[str]
    skipped_check_ids: tuple[str, ...]


def room_node_name(node_id: str) -> str:
    return ROOM_NODE_PREFIX + node_id


def room_event_name(event_id: str) -> str:
    return ROOM_EVENT_PREFIX + event_id


def _part(
    *all_of: str,
    item_counts: Iterable[tuple[str, int]] = (),
    silk_spear: bool = False,
    easy_skips: bool = False,
) -> CompiledRoomClause:
    return CompiledRoomClause(
        all_of=tuple(all_of),
        item_counts=tuple(item_counts),
        require_silk_spear=silk_spear,
        requires_easy_skips=easy_skips,
    )


_ATOM_ALTERNATIVES: Mapping[str, tuple[CompiledRoomClause, ...]] = {
    "item:beast-crest": (_part("Crest: Beast"),),
    "item:clawline": (_part("Ancestral Art: Clawline"),),
    "item:cling-grip": (_part("Ancestral Art: Cling Grip"),),
    "item:drifters-cloak": (_part("Ability: Drifter's Cloak"),),
    "item:faydown-cloak": (_part("Ability: Faydown Cloak"),),
    # "Filled needle phial" is the state of the original Needle Phial used
    # to finish the Plasmium quest.  Plasmium Phial is the reward, so mapping
    # this atom to it would put that reward behind itself.
    "item:filled-needle-phial": (_part("Tool: Needle Phial"),),
    "item:needle-phial": (_part("Tool: Needle Phial"),),
    "item:needolin": (_part("Ancestral Art: Needolin"),),
    "item:ruined-tool": (_part("Ruined Tool"),),
    "item:shaman-crest": (_part("Crest: Shaman"),),
    # Sharpdart must be usable, not merely received. The existing abstract
    # requirement excludes Architect and accepts every crest with a Silk Skill
    # slot.
    "item:sharpdart": (_part("Usable Sharpdart"),),
    "item:silk-soar": (_part("Ancestral Art: Silk Soar"),),
    "item:silk-spear": (_part(silk_spear=True),),
    "item:simple-key": (_part("Simple Key (Wormways)"),),
    "macro:progressive-swift-step-first": (
        _part("Swift Step"),
        _part("Progressive Swift Step"),
    ),
    "macro:progressive-swift-step-full": (_part("Swift Step"),),
    # Confirmed graph vocabulary: any one of Sharpdart, full Dash or
    # Clawline is horizontal movement tech. Sprint alone is not.
    "macro:horizontal-movement-tech": (
        _part("Usable Sharpdart"),
        _part("Swift Step"),
        _part("Ancestral Art: Clawline"),
    ),
    # The Magma Bell must be equippable on an owned crest.  This abstract
    # requirement accounts for native Blue slots and randomized slot upgrades.
    "macro:usable-magma-bell": (_part("Usable Magma Bell"),),
    # This shorthand means a crest whose diagonal attack can stall
    # Hornet in the air long enough to pogo. It is a crest requirement, not a
    # blanket "any crest" gate.
    "macro:air-stall-pogo-crest": (
        _part("Crest: Shaman"),
        _part("Crest: Beast"),
        _part("Crest: Reaper"),
        _part("Crest: Architect"),
    ),
    "option:easy-skips": (_part(easy_skips=True),),
    # These blockades are the Silkspear-cut silk walls in Mosshome. Requiring
    # usable Silkspear also keeps Architect from falsely satisfying the route.
    "capability:break-silk-blockade": (_part(silk_spear=True),),
    # These describe ordinary environmental interactions, not inventory.
    "capability:break-vines": (_part(),),
    "capability:break-walls": (_part(),),
    "capability:free": (_part(),),
    # The client keeps the randomized Wanderer chapel door open, making its
    # monotonic override a free capability in AP logic.
    "capability:wanderer-door-override": (_part(),),
}


# Requirement phrases that refer to action rows. The action itself is reached
# through its node. It is not an AP check.
_ACTION_EVENT_ID_BY_ATOM: Mapping[str, str] = {
    "event:bone-bottom/mosshome-lower/middle-floor-switch-opened":
        "event:bone-bottom/mosshome-middle/flip-switch-to-open-floor-exit",
    "event:bone-bottom/mosshome-lower/spool-ceiling-switch-opened":
        "event:bone-bottom/mosshome-spool/floor-switch-to-open-ceiling-exit",
    "event:bone-bottom/bone-bottom/elevator-switch-flipped":
        "event:bone-bottom/bone-bottom/elevator-switch",
    "event:bone-bottom/mosshome-middle/flip-the-switch-in-this-area":
        "event:bone-bottom/mosshome-middle/flip-switch-to-open-floor-exit",
    "event:bone-bottom/mosshome-spool/switch-in-room-activated":
        "event:bone-bottom/mosshome-spool/floor-switch-to-open-ceiling-exit",
    "event:the-marrow/the-marrow-entrance/platform-lowered":
        "event:the-marrow/the-marrow-shakra-intro/lower-platform-switch-into-floor",
    "event:the-marrow/the-marrow-bellshrine/bell-must-be-rung":
        "event:the-marrow/the-marrow-bellshrine/ring-bell-switch",
    "event:the-marrow/the-marrow-jail-pathway/platform-switch-activated":
        "event:the-marrow/the-marrow-jail-pathway/platform-switch",
    "event:the-marrow/the-marrow-shaft/bell-must-be-rung":
        "event:the-marrow/the-marrow-bellshrine/ring-bell-switch",
    "event:the-marrow/the-marrow-shakra-intro/lowers-platform-into-the-marrow-entrance":
        "event:the-marrow/the-marrow-shakra-intro/lower-platform-switch-into-floor",
    "event:the-marrow/the-marrow-lava-track/activate-track":
        "event:the-marrow/the-marrow-lava-track/activate-track",
    "event:weavenest-atla/weavenest-atla-teleporter/weavenest-atla-power-activation":
        "event:weavenest-atla/weavenest-atla-power/power-activation",
    "event:wormways/wormways-middle/door-must-be-unlocked-from-the-other-side":
        "event:wormways/wormways-shaft/use-simple-key-on-lock",
    "event:wormways/wormways-shaft/door-unlocked":
        "event:wormways/wormways-shaft/use-simple-key-on-lock",
    "event:wormways/wormways-shaft/door-switch-needs-to-be-flipped":
        "event:wormways/wormways-shaft/flip-door-switch",
    "event:deep-docks/deep-docks-bellshrine/deep-docks-bellshrine-activated":
        "event:deep-docks/deep-docks-bellshrine/activate-deep-docks-bellshrine-switch",
    "event:deep-docks/deep-docks-bench-shaft/gate-unlocked-from-other-side":
        "event:deep-docks/deep-docks-upper-spire/door-switch",
    "event:deep-docks/deep-docks-chains-center/ceiling-switch-activated":
        "event:deep-docks/deep-docks-chains-center/ceiling-switch",
    "event:deep-docks/deep-docks-chains-center/door-switch-flipped":
        "event:deep-docks/deep-docks-chains-center/door-switch",
    "event:deep-docks/deep-docks-entrance/switch-flipped":
        "event:deep-docks/deep-docks-entrance/door-switch",
    "event:deep-docks/deep-docks-lace-intro/gate-switch-flipped":
        "event:deep-docks/deep-docks-lace-intro/gate-switch",
    "event:deep-docks/deep-docks-spool-east/platforms-lowered":
        "event:deep-docks/deep-docks-spool-east/platform-lever",
}


# Persistent actions inferred from the controlling side.
# Every entry is monotonic: once that node is reachable, the player can perform
# the action and retain the shortcut/state for the rest of the logic search.
_IMPLICIT_EVENT_SOURCE: Mapping[str, tuple[CompiledRoomClause, ...]] = {
    "event:bone-bottom/bone-bottom/door-opened-from-other-side": (
        _part(room_node_name("bone-bottom/bonegrave#graveyard")),
    ),
    "event:bone-bottom/bonegrave/wall-broken-from-other-side": (
        _part(room_node_name("bone-bottom/bonegrave#upper-left-exit")),
    ),
    # The Mosshome Upper row identifies the side from which this one-way wall
    # is broken. Once its main area is reachable, the Big Fall
    # middle-right entrance remains open permanently.
    (
        "event:bone-bottom/the-big-fall/"
        "breakable-wall-must-be-opened-from-the-other-side"
    ): (
        _part(room_node_name("bone-bottom/mosshome-upper#main-area")),
    ),
    "event:bone-bottom/ruined-chapel/moss-mother-defeated": (
        _part(room_node_name("bone-bottom/ruined-chapel#boss-room")),
    ),
    # This blocker disappears after leaving Moss Grotto. Reaching Bone Bottom
    # ground level confirms the departure without making the ceiling exit a
    # start route.
    (
        "event:bone-bottom/moss-grotto-center/"
        "has-loading-zone-blocker-until-you-first-leave-moss-grotto"
    ): (
        _part(room_node_name("bone-bottom/bone-bottom#ground-level")),
    ),
    "event:the-marrow/the-marrow-bellway/bell-beast-defeated": (
        _part(
            room_node_name("the-marrow/the-marrow-bellway#boss-room"),
            silk_spear=True,
        ),
    ),
    "event:the-marrow/the-marrow-entrance/defeat-gauntlet": (
        _part(room_node_name("the-marrow/the-marrow-entrance#gauntlet-room")),
    ),
    "event:the-marrow/the-marrow-entrance/flipped-door-switch": (
        _part(room_node_name("the-marrow/the-marrow-entrance#after-gauntlet")),
    ),
    "event:the-marrow/the-marrow-flea-caravan/door-switch-activated": (
        _part(room_node_name("the-marrow/the-marrow-flea-caravan#behind-metal-gate")),
    ),
    "event:the-marrow/the-marrow-shaft/door-switch-flipped": (
        _part(room_node_name("the-marrow/the-marrow-shaft#upper-shaft")),
    ),
    "event:the-marrow/the-marrow-shakra-intro/door-switch-flipped": (
        _part(room_node_name("the-marrow/the-marrow-shakra-intro#behind-gate")),
    ),
    "event:the-marrow/the-marrow-skull-tyrant-arena/defeat-skull-tyrant": (
        _part(room_node_name("the-marrow/the-marrow-skull-tyrant-arena#room")),
    ),
    "event:weavenest-atla/weavenest-atla-grotto/defeat-double-moss-mother": (
        _part(room_node_name("weavenest-atla/weavenest-atla-grotto#boss-room")),
    ),
    "event:wormways/wormways-craggler-hallway/defeat-craggler": (
        _part(room_node_name("wormways/wormways-craggler-hallway#room")),
    ),
    "event:wormways/wormways-shaft/breakable-wall-must-be-opened-from-the-other-side": (
        _part(room_node_name("wormways/wormways-upper-west#main-area")),
    ),
    "event:deep-docks/deep-docks-chains-center/wall-opened-from-lower-east": (
        _part(room_node_name("deep-docks/deep-docks-chains-lower-east#lower-lava-platform")),
    ),
    "event:deep-docks/deep-docks-chains-upper-east/door-opened-from-the-other-side": (
        _part(room_node_name("deep-docks/deep-docks-chains-upper-east#upper-left-hallway")),
    ),
    "event:deep-docks/deep-docks-chains-upper-east/wall-broken": (
        _part(room_node_name("deep-docks/deep-docks-chains-upper-east#chain-platforms")),
    ),
    "event:deep-docks/deep-docks-chains-upper-east/gate-opened-from-the-other-side": (
        _part(
            room_node_name("deep-docks/deep-docks-chains-upper-east#chain-platforms"),
            "Ancestral Art: Clawline",
        ),
    ),
    "event:deep-docks/deep-docks-entrance/defeat-gauntlet": (
        _part(room_node_name("deep-docks/deep-docks-entrance#gauntlet")),
    ),
    "event:deep-docks/deep-docks-forebrothers/defeat-forebrothers": (
        _part(room_node_name("deep-docks/deep-docks-forebrothers#boss-area")),
    ),
    "event:deep-docks/deep-docks-forge/gauntlet-defeated": (
        _part(room_node_name("deep-docks/deep-docks-forge#gauntlet")),
    ),
    # The authored reciprocal direction says the Forge-side crossing is free
    # and activates the airlock.  This is the same state Lace requires from
    # the opposite direction and is unrelated to the spool-room airlock.
    "event:deep-docks/deep-docks-lace-intro/airlock-lever": (
        _part(room_node_name("deep-docks/deep-docks-forge#left-area")),
    ),
    "event:deep-docks/deep-docks-forge/activate-airlock": (
        _part(room_node_name("deep-docks/deep-docks-forge#left-area")),
    ),
    # The switch is on the left side. The separate Forge-Daughter action must
    # not create a closed route through the current geometry.
    "event:deep-docks/deep-docks-forge/gate-switch-activated": (
        _part(room_node_name("deep-docks/deep-docks-forge#left-area")),
    ),
    "event:deep-docks/deep-docks-lace-intro/defeat-lace": (
        _part(room_node_name("deep-docks/deep-docks-lace-intro#boss-arena")),
    ),
    (
        "event:deep-docks/deep-docks-lower-west-shaft/"
        "must-be-opened-from-the-other-side-for-the-first-time"
    ): (
        _part(room_node_name("deep-docks/deep-docks-sauna#room")),
    ),
    "event:deep-docks/is-this-still-deep-docks-east/wall-must-be-destroyed-from-the-other-side": (
        _part(room_node_name("deep-docks/is-this-still-deep-docks-west#ground")),
    ),
    "event:deep-docks/is-this-still-deep-docks-east/floor-must-be-destroyed-from-the-other-side": (
        _part(room_node_name("deep-docks/deep-docks-spire-lower#room")),
    ),
    "event:deep-docks/deep-docks-map-shop/flip-switch-to-lower-platform": (
        _part(room_node_name("deep-docks/deep-docks-map-shop#lower-area")),
    ),
}


_GLOBAL_EVENT_NAME_BY_ATOM: Mapping[str, str] = {
    "event:the-marrow/the-marrow-bellway/bell-beast-defeated":
        "Event: Bell Beast Defeated",
}


# The room graph starts at Hornet's opening room. Internal coarse-path seeds
# are excluded because they let flat fallback rules bypass doors, switches
# and room connections.
_NODE_SEEDS: Mapping[str, tuple[CompiledRoomClause, ...]] = {
    "bone-bottom/moss-grotto-center#rock-bottom": (_part(),),
}


# Deep Docks uses the room graph, but its western entrance crosses Hunter's
# March which is not yet covered. That external boundary remains until Hunter's
# March is included. From this port onward every route comes from the Deep
# Docks nodes and one-way states. This is not an internal Deep Docks bypass.
_EXTERNAL_BOUNDARY_SEEDS: Mapping[str, tuple[CompiledRoomClause, ...]] = {
    "deep-docks/is-this-still-deep-docks-west#upper-level": (
        _part("Path: Hunter's March - Trap"),
    ),
    # The graph does not yet model the Deep Docks Simple-Key door or the Far
    # Fields backdoor, so the Lower Deep Docks boundary remains at this node.
    "deep-docks/deep-docks-chains-upper-east#chain-platforms": (
        _part("Path: Deep Docks - Lower"),
    ),
}


_ALL_NODE_SEEDS: Mapping[str, tuple[CompiledRoomClause, ...]] = {
    **_NODE_SEEDS,
    **_EXTERNAL_BOUNDARY_SEEDS,
}


# These links resolve through the room graph, so the compiler adds no extra
# geometry.
_CURATED_EDGES: tuple[tuple[str, str, tuple[CompiledRoomClause, ...]], ...] = ()


# Existing AP locations whose room records represent the
# physical action/room but do not spell the current AP check name as an
# Included check row.  These are identity bindings only. Access still comes
# entirely from the room graph.
_EXISTING_LOCATION_NODE_BINDINGS: Mapping[str, str] = {
    "Beast Shard: Marrowmaw": "bone-bottom/moss-grotto-center#rock-bottom",
    "Bellshrine: The Marrow": "the-marrow/the-marrow-bellshrine#room",
    "Tool Pouch: Loddie": "the-marrow/the-marrow-jail#room",
    "Rosary Cache: The Marrow #11": (
        "the-marrow/the-marrow-lava-track#left-maze"
    ),
    "Rosary Cache: The Marrow #12": (
        "the-marrow/the-marrow-lava-track#left-maze"
    ),
    "Rosary Cache: The Marrow #13": (
        "the-marrow/the-marrow-lava-track#right-maze"
    ),
    "Map Purchase: Wormways": "wormways/wormways-upper-east#upper-area",
    "Pin Purchase: Vendor Pins": "wormways/wormways-upper-east#upper-area",
    "Bellway: Deep Docks": "deep-docks/deep-docks-bellway#room",
    "Bellshrine: Deep Docks": "deep-docks/deep-docks-bellshrine#room",
}


# Existing AP locations whose source is expressed by a persistent graph
# event rather than by a dedicated Included check row.
_EXISTING_LOCATION_CLAUSE_BINDINGS: Mapping[
    str, tuple[CompiledRoomClause, ...]
] = {
    "Beast Shard: Craggler": (
        _part(
            room_event_name(
                "event:wormways/wormways-craggler-hallway/defeat-craggler"
            )
        ),
    ),
}


def _merge(left: CompiledRoomClause, right: CompiledRoomClause) -> CompiledRoomClause:
    merged_counts: dict[str, int] = dict(left.item_counts)
    for item_name, minimum in right.item_counts:
        merged_counts[item_name] = max(merged_counts.get(item_name, 0), minimum)
    return CompiledRoomClause(
        all_of=tuple(dict.fromkeys((*left.all_of, *right.all_of))),
        item_counts=tuple(merged_counts.items()),
        require_silk_spear=left.require_silk_spear or right.require_silk_spear,
        requires_easy_skips=(
            left.requires_easy_skips or right.requires_easy_skips
        ),
    )


def _event_requirement_name(atom: str) -> str:
    global_name = _GLOBAL_EVENT_NAME_BY_ATOM.get(atom)
    if global_name is not None:
        return global_name
    action_event_id = _ACTION_EVENT_ID_BY_ATOM.get(atom)
    return room_event_name(action_event_id or atom)


def _atom_alternatives(atom: str) -> tuple[CompiledRoomClause, ...]:
    alternatives = _ATOM_ALTERNATIVES.get(atom)
    if alternatives is not None:
        return alternatives
    if atom.startswith("count:mossberry:"):
        try:
            minimum = int(atom.rsplit(":", 1)[1])
        except ValueError as exc:
            raise ValueError(
                f"invalid room-graph mossberry count atom: {atom!r}"
            ) from exc
        if minimum < 1:
            raise ValueError(
                f"invalid room-graph mossberry count atom: {atom!r}"
            )
        return (_part(item_counts=(("Mossberry", minimum),)),)
    if atom.startswith("event:"):
        return (_part(_event_requirement_name(atom)),)
    raise ValueError(f"unsupported room-graph atom: {atom!r}")


def _compile_spec(
    requirement: RequirementSpec,
    *prefix: str,
) -> tuple[CompiledRoomClause, ...]:
    if requirement.mode is RequirementMode.UNRESOLVED:
        return ()
    if requirement.mode is RequirementMode.INHERIT:
        return (_part(*prefix),)

    compiled: list[CompiledRoomClause] = []
    for source_clause in requirement.dnf:
        products: Iterable[tuple[CompiledRoomClause, ...]] = product(
            *(_atom_alternatives(atom) for atom in source_clause)
        ) if source_clause else ((),)
        for product_parts in products:
            clause = _part(*prefix)
            for part in product_parts:
                clause = _merge(clause, part)
            compiled.append(clause)
    return tuple(dict.fromkeys(compiled))


def _append_requirements(
    target: dict[str, list[CompiledRoomClause]],
    name: str,
    requirements: Iterable[CompiledRoomClause],
) -> None:
    bucket = target.setdefault(name, [])
    for requirement in requirements:
        if requirement not in bucket:
            bucket.append(requirement)


def _has_only_declared_room_references(
    clause: CompiledRoomClause,
    declared_owners: Mapping[str, object],
) -> bool:
    return all(
        not reference.startswith((ROOM_NODE_PREFIX, ROOM_EVENT_PREFIX))
        or reference in declared_owners
        for reference in clause.all_of
    )


def _structural_reachability(
    graph,
    authoritative_node_ids: frozenset[str],
    extra_edges: Iterable[tuple[str, str]],
) -> frozenset[str]:
    outgoing: dict[str, set[str]] = {}
    transition_by_id = graph.transition_by_id
    for room in graph.authoritative_rooms:
        for transition in room.transitions:
            if not transition.is_compilable:
                continue
            target_port = transition_by_id[transition.target.port_id]
            if target_port.source_node_id not in authoritative_node_ids:
                continue
            outgoing.setdefault(transition.source_node_id, set()).add(
                target_port.source_node_id
            )
        for connection in room.connections:
            if not connection.is_compilable:
                continue
            outgoing.setdefault(connection.source_node_id, set()).add(
                connection.target_node_id
            )
    for source, target in extra_edges:
        outgoing.setdefault(source, set()).add(target)

    reachable = set(_ALL_NODE_SEEDS)
    pending = list(reachable)
    while pending:
        source = pending.pop()
        for target in outgoing.get(source, ()):
            if target in reachable:
                continue
            reachable.add(target)
            pending.append(target)
    return frozenset(reachable)


def _semantic_reachability(
    node_requirements: Mapping[str, Iterable[CompiledRoomClause]],
    event_requirements: Mapping[str, Iterable[CompiledRoomClause]],
) -> frozenset[str]:
    """Resolve nodes which have a satisfiable monotonic route with all items.

    Structural connectivity alone admits closed cycles such as a room whose
    only entrance needs a switch located inside that same room.  For this
    compiler check, normal items, options and coarse paths are available.
    room nodes and room events still have to derive from a real seed.
    """

    owners = {
        **node_requirements,
        **event_requirements,
    }
    reachable: set[str] = set()
    changed = True
    while changed:
        changed = False
        for owner, alternatives in owners.items():
            if owner in reachable:
                continue
            if any(
                all(
                    (
                        reference in reachable
                        if reference.startswith(
                            (ROOM_NODE_PREFIX, ROOM_EVENT_PREFIX)
                        )
                        else True
                    )
                    for reference in alternative.all_of
                )
                for alternative in alternatives
            ):
                reachable.add(owner)
                changed = True

    return frozenset(
        name[len(ROOM_NODE_PREFIX):]
        for name in reachable
        if name.startswith(ROOM_NODE_PREFIX)
    )


def compile_room_graph() -> CompiledRoomGraph:
    graph = load_room_graph()
    authoritative_rooms = graph.authoritative_rooms
    authoritative_node_ids = frozenset(
        node.id for room in authoritative_rooms for node in room.nodes
    )
    unknown_seed_ids = tuple(sorted(set(_ALL_NODE_SEEDS) - authoritative_node_ids))
    if unknown_seed_ids:
        raise ValueError(
            "room graph compiler has undeclared node seeds: "
            + ", ".join(unknown_seed_ids)
        )
    node_requirements: dict[str, list[CompiledRoomClause]] = {
        room_node_name(node_id): [] for node_id in authoritative_node_ids
    }
    for node_id, requirements in _ALL_NODE_SEEDS.items():
        _append_requirements(
            node_requirements,
            room_node_name(node_id),
            requirements,
        )

    transition_by_id = graph.transition_by_id
    structural_edges: list[tuple[str, str]] = []
    for room in authoritative_rooms:
        for transition in room.transitions:
            if not transition.is_compilable:
                continue
            target_port = transition_by_id[transition.target.port_id]
            target_node_id = target_port.source_node_id
            if target_node_id not in authoritative_node_ids:
                continue
            structural_edges.append((transition.source_node_id, target_node_id))
            _append_requirements(
                node_requirements,
                room_node_name(target_node_id),
                _compile_spec(
                    transition.requirement,
                    room_node_name(transition.source_node_id),
                ),
            )
        for connection in room.connections:
            if not connection.is_compilable:
                continue
            structural_edges.append(
                (connection.source_node_id, connection.target_node_id)
            )
            _append_requirements(
                node_requirements,
                room_node_name(connection.target_node_id),
                _compile_spec(
                    connection.requirement,
                    room_node_name(connection.source_node_id),
                ),
            )

    for source, target, requirements in _CURATED_EDGES:
        structural_edges.append((source, target))
        _append_requirements(
            node_requirements,
            room_node_name(target),
            (
                _merge(_part(room_node_name(source)), requirement)
                for requirement in requirements
            ),
        )

    event_requirements: dict[str, list[CompiledRoomClause]] = {}
    for room in authoritative_rooms:
        for event in room.events:
            if not event.is_compilable:
                continue
            # The Shakra switch row puts its effect in the requirement column.
            # Reaching the switch is sufficient to lower the platform.
            if event.id.endswith("/lower-platform-switch-into-floor"):
                requirements = (_part(room_node_name(event.node_id)),)
            else:
                requirements = _compile_spec(
                    event.requirement,
                    room_node_name(event.node_id),
                )
            _append_requirements(
                event_requirements,
                room_event_name(event.id),
                requirements,
            )

    for atom, requirements in _IMPLICIT_EVENT_SOURCE.items():
        _append_requirements(
            event_requirements,
            _event_requirement_name(atom),
            requirements,
        )

    semantically_reachable_nodes = _semantic_reachability(
        node_requirements,
        event_requirements,
    )
    reachable_nodes = _structural_reachability(
        graph,
        authoritative_node_ids,
        ((source, target) for source, target, _requirements in _CURATED_EDGES),
    )
    check_requirements: dict[str, list[CompiledRoomClause]] = {}
    check_source_ids: dict[str, list[str]] = {}
    skipped_checks: list[str] = []
    declared_owners = {
        **node_requirements,
        **event_requirements,
    }
    present_authoritative_checks = frozenset(
        check.canonical_location
        for room in authoritative_rooms
        for check in room.checks
        if check.currently_randomized and check.canonical_location is not None
    )
    for room in authoritative_rooms:
        for check in room.checks:
            if (
                not check.currently_randomized
                or
                not check.is_compilable
                or check.node_id not in semantically_reachable_nodes
            ):
                skipped_checks.append(check.id)
                continue
            requirements = tuple(
                requirement
                for requirement in _compile_spec(
                    check.requirement,
                    room_node_name(check.node_id),
                )
                if _has_only_declared_room_references(
                    requirement,
                    declared_owners,
                )
            )
            if not requirements:
                skipped_checks.append(check.id)
                continue
            _append_requirements(
                check_requirements,
                check.canonical_location,
                requirements,
            )
            check_source_ids.setdefault(check.canonical_location, []).append(
                check.id
            )

    for location_name, node_id in _EXISTING_LOCATION_NODE_BINDINGS.items():
        if node_id not in semantically_reachable_nodes:
            continue
        _append_requirements(
            check_requirements,
            location_name,
            (_part(room_node_name(node_id)),),
        )
        check_source_ids.setdefault(location_name, []).append(
            f"derived-node:{node_id}"
        )

    for location_name, requirements in _EXISTING_LOCATION_CLAUSE_BINDINGS.items():
        valid_requirements = tuple(
            requirement
            for requirement in requirements
            if _has_only_declared_room_references(requirement, declared_owners)
        )
        if not valid_requirements:
            continue
        _append_requirements(
            check_requirements,
            location_name,
            valid_requirements,
        )
        check_source_ids.setdefault(location_name, []).append(
            "derived-clause:" + location_name
        )

    present_authoritative_checks = frozenset(
        (
            *present_authoritative_checks,
            *_EXISTING_LOCATION_NODE_BINDINGS,
            *_EXISTING_LOCATION_CLAUSE_BINDINGS,
        )
    )

    return CompiledRoomGraph(
        node_requirements=MappingProxyType(
            {name: tuple(rows) for name, rows in node_requirements.items()}
        ),
        event_requirements=MappingProxyType(
            {name: tuple(rows) for name, rows in event_requirements.items()}
        ),
        check_requirements=MappingProxyType(
            {name: tuple(rows) for name, rows in check_requirements.items()}
        ),
        check_source_ids=MappingProxyType(
            {name: tuple(ids) for name, ids in check_source_ids.items()}
        ),
        authoritative_check_names=frozenset(check_requirements),
        quarantined_check_names=frozenset(
            present_authoritative_checks - set(check_requirements)
        ),
        structurally_reachable_nodes=reachable_nodes,
        semantically_reachable_nodes=semantically_reachable_nodes,
        skipped_check_ids=tuple(sorted(skipped_checks)),
    )
