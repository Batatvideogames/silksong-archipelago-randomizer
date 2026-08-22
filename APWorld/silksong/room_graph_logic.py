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
    minimum_skip_tier: int = 0


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
    skip_tier: int = 0,
) -> CompiledRoomClause:
    return CompiledRoomClause(
        all_of=tuple(all_of),
        item_counts=tuple(item_counts),
        require_silk_spear=silk_spear,
        minimum_skip_tier=skip_tier,
    )


_ATOM_ALTERNATIVES: Mapping[str, tuple[CompiledRoomClause, ...]] = {
    "item:architect-crest": (_part("Crest: Architect"),),
    "item:beast-crest": (_part("Crest: Beast"),),
    "item:bellway-blasted-steps": (_part("Bellway: Blasted Steps"),),
    "item:bellway-bellhart": (_part("Bellway: Bellhart"),),
    "item:bellway-shellwood": (_part("Bellway: Shellwood"),),
    "item:bellway-the-slab": (_part("Bellway: The Slab"),),
    "item:clawline": (_part("Ancestral Art: Clawline"),),
    "item:cling-grip": (_part("Ancestral Art: Cling Grip"),),
    "item:craftmetal": (_part("Craftmetal"),),
    "item:drifters-cloak": (_part("Ability: Drifter's Cloak"),),
    "item:faydown-cloak": (_part("Ability: Faydown Cloak"),),
    "item:flea-brew": (_part("Usable Flea Brew"),),
    # "Filled needle phial" is the state of the original Needle Phial used
    # to finish the Plasmium quest.  Plasmium Phial is the reward, so mapping
    # this atom to it would put that reward behind itself.
    "item:filled-needle-phial": (_part("Usable Needle Phial"),),
    "item:hunter-crest": (_part("Crest: Hunter"),),
    "item:needle-phial": (_part("Usable Needle Phial"),),
    "item:needolin": (_part("Ancestral Art: Needolin"),),
    "item:key-of-apostate": (_part("Key of Apostate"),),
    "item:ruined-tool": (_part("Ruined Tool"),),
    "item:reaper-crest": (_part("Crest: Reaper"),),
    "item:rune-rage": (_part("Usable Rune Rage"),),
    "item:shaman-crest": (_part("Crest: Shaman"),),
    # Sharpdart must be usable, not merely received. The existing abstract
    # requirement excludes Architect and accepts every crest with a Silk Skill
    # slot.
    "item:sharpdart": (_part("Usable Sharpdart"),),
    "item:silk-soar": (_part("Ancestral Art: Silk Soar"),),
    "item:silk-spear": (_part(silk_spear=True),),
    "item:simple-key": (_part("Simple Key (Wormways)"),),
    "item:thread-storm": (_part("Usable Thread Storm"),),
    "item:wanderer-crest": (_part("Crest: Wanderer"),),
    "item:witch-crest": (_part("Crest: Witch"),),
    "item:white-key": (_part("White Key"),),
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
    "macro:any-crest": (
        _part("Crest: Hunter"),
        _part("Crest: Reaper"),
        _part("Crest: Shaman"),
        _part("Crest: Architect"),
        _part("Crest: Wanderer"),
        _part("Crest: Beast"),
        _part("Crest: Witch"),
    ),
    "macro:any-non-witch-crest": (
        _part("Crest: Hunter"),
        _part("Crest: Reaper"),
        _part("Crest: Shaman"),
        _part("Crest: Architect"),
        _part("Crest: Wanderer"),
        _part("Crest: Beast"),
    ),
    # The Magma Bell must be equippable on an owned crest.  This abstract
    # requirement accounts for native Blue slots and randomized slot upgrades.
    "macro:usable-magma-bell": (_part("Usable Magma Bell"),),
    # Every authored Scuttlebrace route is the full-speed crawl technique, so
    # merely owning/equipping the tool is insufficient. It also needs the full
    # Swift Step upgrade, never only the first progressive stage.
    "macro:usable-scuttlebrace": (
        _part("Usable Scuttlebrace", "Swift Step"),
    ),
    "option:skips-easy": (_part(skip_tier=1),),
    "option:skips-moderate": (_part(skip_tier=2),),
    "option:skips-difficult": (_part(skip_tier=3),),
    # Bell Beast's silk remains an explicit Silkspear-only gate.
    "capability:break-silk-blockade": (
        _part(silk_spear=True),
        _part("Usable Rune Rage"),
        _part("Usable Sharpdart"),
    ),
    # These describe ordinary environmental interactions, not inventory.
    "capability:break-vines": (_part(),),
    "capability:break-walls": (_part(),),
    "capability:free": (_part(),),
    # The client keeps the randomized Wanderer chapel door open, making its
    # monotonic override a free capability in AP logic.
    "capability:wanderer-door-override": (_part(),),
    "state:act-3": (_part("Act: 3"),),
}


# Requirement phrases that refer to action rows. The action itself is reached
# through its node. It is not an AP check.
_ACTION_EVENT_ID_BY_ATOM: Mapping[str, str] = {
    "event:bone-bottom/mosshome-lower/middle-floor-switch-opened":
        "event:bone-bottom/mosshome-middle/flip-switch-to-open-floor-exit",
    "event:bone-bottom/mosshome-lower/spool-ceiling-switch-opened":
        "event:bone-bottom/mosshome-spool/floor-switch-to-open-ceiling-exit",
    "event:bone-bottom/bone-bottom-town/elevator-switch-flipped":
        "event:bone-bottom/bone-bottom-town/elevator-switch",
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
    "event:deep-docks/deep-docks-magma-slug-tunnels/left-door-switch-flipped":
        "event:deep-docks/deep-docks-magma-slug-tunnels/left-door-switch",
    (
        "event:deep-docks/deep-docks-magma-slug-tunnels/"
        "right-door-switch-must-be-flipped"
    ):
        "event:deep-docks/deep-docks-magma-slug-tunnels/right-door-switch",
}


# Persistent actions inferred from the controlling side.
# Every entry is monotonic: once that node is reachable, the player can perform
# the action and retain the shortcut/state for the rest of the logic search.
_IMPLICIT_EVENT_SOURCE: Mapping[str, tuple[CompiledRoomClause, ...]] = {
    "event:bone-bottom/bone-bottom-town/door-opened-from-other-side": (
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
    "event:moss-grotto/ruined-chapel/moss-mother-defeated": (
        _part(room_node_name("moss-grotto/ruined-chapel#boss-room")),
    ),
    # This blocker disappears after leaving Moss Grotto. Reaching Bone Bottom
    # ground level confirms the departure without making the ceiling exit a
    # start route.
    (
        "event:moss-grotto/moss-grotto-center/"
        "has-loading-zone-blocker-until-you-first-leave-moss-grotto"
    ): (
        _part(room_node_name("bone-bottom/bone-bottom-town#ground-level")),
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
    "event:deep-docks/deep-docks-chains-lower-east/open-airlock-door": (
        _part(
            room_node_name(
                "deep-docks/deep-docks-chains-lower-east#spool-fragment-area"
            )
        ),
    ),
    # The Underworks lift door is opened from the Broken Elevator side.
    "event:underworks/underworks-shaft/must-be-opened-from-the-other-side": (
        _part(room_node_name("underworks/broken-elevator#room")),
    ),
    # Entering the west arena clears its gauntlet state and reopens the return
    # route.
    "event:underworks/underworks-western-gauntlet/gauntlet": (
        _part(room_node_name("underworks/underworks-western-gauntlet#room")),
    ),
    # Clearing the Arena makes the fixed Heretic Key available.
    "event:the-slab/slab-arena/gauntlet": (
        _part(room_node_name("the-slab/slab-arena#arena")),
    ),
    # Each shortcut keeps its own one-way unlock.
    "event:the-slab/slab-cell/entrance-door-opened": (
        _part(room_node_name("the-slab/slab-entrance#room")),
    ),
    "event:the-slab/slab-cell/poodle-wall-opened": (
        _part(room_node_name("the-slab/slab-poodle#room")),
    ),
    "event:the-slab/slab-cell/secret-side-wall-opened": (
        _part(room_node_name("the-slab/slab-secret-side-room#bottom")),
    ),
    # The east-to-west room is the controlling side of this one-way door.
    (
        "event:choral-chambers/choral-chambers-eastern-shaft/"
        "door-should-be-opened-from-the-other-side"
    ): (
        _part(
            room_node_name(
                "choral-chambers/choral-chambers-east-to-west#left-side"
            )
        ),
    ),
    # The Spa side breaks the wall into the lower Flea Shaft.
    (
        "event:choral-chambers/choral-chambers-flea-shaft/"
        "breakable-wall-must-be-opened-from-the-other-side"
    ): (
        _part(room_node_name("choral-chambers/choral-chambers-spa#spa")),
    ),
    # This shortcut is opened while approaching from Underworks.
    (
        "event:choral-chambers/choral-chambers-outisde-underworks/"
        "only-opened-from-the-other-side"
    ): (
        _part(
            room_node_name(
                "underworks/underworks-outside-choral-chambers#room"
            )
        ),
    ),
    "event:choral-chambers/choral-chambers-outside-spa/gauntlet": (
        _part(
            room_node_name(
                "choral-chambers/choral-chambers-outside-spa#gauntlet"
            )
        ),
    ),
    "event:high-halls/high-halls-arena/gauntlet": (
        _part(room_node_name("high-halls/high-halls-arena#room")),
    ),
    "event:whiteward/whiteward-entrance/set-elevator-to-top": (
        _part(
            room_node_name("whiteward/whiteward-entrance#elevator-shaft"),
            "White Key",
        ),
    ),
    "event:whiteward/whiteward-unravelled-arena-room/beat-unravelled-arena": (
        _part(
            room_node_name(
                "whiteward/whiteward-unravelled-arena-room#unravelled-arena"
            )
        ),
    ),
    # The Dancers event comes from their arena and requires an owned crest. The
    # old Choral path was too broad for this event.
    "event:global/cogwork-dancers-defeated": tuple(
        _part(
            room_node_name("cogwork-core/cog-dancers#bossarena"),
            crest,
        )
        for crest in (
            "Crest: Hunter",
            "Crest: Reaper",
            "Crest: Shaman",
            "Crest: Architect",
            "Crest: Wanderer",
            "Crest: Beast",
            "Crest: Witch",
        )
    ),
    "event:the-marrow/the-marrow-skull-wall/opened-from-shellwood": (
        _part(room_node_name("shellwood/mosstown-03#bottom-half")),
    ),
    "event:shellwood/bellshrine-03/activated": (
        _part(room_node_name("shellwood/bellshrine-03#room")),
    ),
    "event:shellwood/mosstown-03/top-exit-opened": (
        _part(room_node_name("shellwood/mosstown-03#top-half")),
    ),
    "event:shellwood/shellwood-02/elevator-activated": (
        _part(room_node_name("shellwood/shellwood-02#ceiling-area")),
    ),
    "event:shellwood/shellwood-02/lower-door-opened": (
        _part(room_node_name("shellwood/shellwood-02#ground-right")),
    ),
    "event:shellwood/shellwood-01/nest-broken": (
        _part(room_node_name("shellwood/shellwood-01#right-platforms")),
    ),
    "event:shellwood/shellwood-01b/elevator-activated": (
        _part(room_node_name("shellwood/shellwood-01b#elevator-platform")),
    ),
    "event:shellwood/shellwood-15/door-opened": (
        _part(room_node_name("shellwood/shellwood-15#room")),
    ),
    "event:shellwood/shellwood-26/upper-wall-broken": (
        _part(room_node_name("shellwood/shellwood-26#upper-area")),
    ),
}


_GLOBAL_EVENT_NAME_BY_ATOM: Mapping[str, str] = {
    "event:global/bell-beast-defeated":
        "Event: Bell Beast Defeated",
    "event:global/cogwork-dancers-defeated":
        "Event: Cogwork Dancers Defeated",
    "event:global/elegy-of-the-deep-learned":
        "Event: Elegy of the Deep Learned",
    "event:global/last-judge-defeated": "Event: Last Judge Defeated",
    "event:global/missing-courier-rescued":
        "Event: Missing Courier Rescued",
    "event:the-marrow/the-marrow-bellway/bell-beast-defeated":
        "Event: Bell Beast Defeated",
}


# The room graph starts at Hornet's opening room. Internal coarse-path seeds
# are excluded because they let flat fallback rules bypass doors, switches
# and room connections.
_NODE_SEEDS: Mapping[str, tuple[CompiledRoomClause, ...]] = {
    "moss-grotto/moss-grotto-center#rock-bottom": (_part(),),
}


# Deep Docks uses the room graph, but its western entrance crosses Hunter's
# March which is not yet covered. That external boundary remains until Hunter's
# March is included. From this port onward every route comes from the Deep
# Docks nodes and one-way states. This is not an internal Deep Docks bypass.
_EXTERNAL_BOUNDARY_SEEDS: Mapping[str, tuple[CompiledRoomClause, ...]] = {
    # The mapped Bellway room starts at the existing Blasted Steps endpoint.
    "blasted-steps/blasted-steps-bellway#room": (
        _part("Path: Blasted Steps - Bellway"),
    ),
    # The west Sinner's Road entrance comes from Greymoor's Halfway House route
    # and needs Cling Grip. Starting at the later Sinner path would skip it.
    "sinner-s-road/sinner-s-road-entrance#room": (
        _part(
            "Path: Greymoor - Halfway House",
            "Ancestral Art: Cling Grip",
        ),
    ),
    # The room graph skips the stretch between Bilewater's Bellway and this
    # entrance. Use the existing east-side route at the boundary.
    "bilewater/bilewater-entrance#room": (
        _part(
            "Path: Bilewater - Bellway",
            "Ancestral Art: Cling Grip",
            "Ancestral Art: Swift Step",
        ),
    ),
    # This room set contains the Bone Scroll side room but not the rest of lower
    # Greymoor. Its exit lands on the existing Halfway House path.
    "greymoor/greymoor-bone-scroll-room#room": (
        _part("Path: Greymoor - Halfway House"),
    ),
    # Last Judge's arena is outside this room set. Defeating it puts Hornet at
    # the top of the Grand Elevator before the collapse.
    "grand-gate/grand-elevator#top": (
        _part("Event: Last Judge Defeated"),
    ),
    # The missing lower Greymoor transition enters the bottom of the Wisp bench
    # room.
    "whisp-thicket/wisp-thicket-bench#bottom": (
        _part("Path: Greymoor - Wisp Thicket"),
    ),
    # These station connections sit outside the imported rooms. Each route
    # stops at its station.
    "choral-chambers/choral-chambers-ventrica-room#ventrica": (
        _part("Path: Ventrica - Choral Chambers"),
    ),
    "choral-chambers/grand-bellway#base": (
        _part("Path: Bellway - Grand Bellway"),
        _part("Path: Ventrica - Grand Bellway"),
    ),
    "choral-chambers/songclave-tube#room": (
        _part("Path: Ventrica - Songclave"),
    ),
    "deep-docks/is-this-still-deep-docks-west#upper-level": (
        _part("Path: Hunter's March - Trap"),
    ),
    # The graph does not yet model the Deep Docks Simple-Key door or the Far
    # Fields backdoor, so the Lower Deep Docks boundary remains at this node.
    "deep-docks/deep-docks-chains-upper-east#chain-platforms": (
        _part("Path: Deep Docks - Lower"),
    ),
    "deep-docks/deep-docks-bellway#room": (
        _part("Path: Bellway - Deep Docks"),
    ),
    "shellwood/shellwood-19#right-puddle": (
        _part("Path: Bellway - Shellwood"),
    ),
    "bellhart/belltown-06#lower-level": (
        _part("Path: Greymoor - Halfway House"),
    ),
    "bellhart/belltown-basement#room": (
        _part("Path: Bellway - Bellhart"),
    ),
    "bellhart/belltown#ground-level": (
        _part("Event: Widow Defeated"),
    ),
    "shellwood/shellwood-01b#bench-toll": (
        _part("Event: Widow Defeated"),
    ),
    # Bellway travel reaches this room but does not skip the Slab route.
    "the-slab/slab-bellway#room": (
        _part("Path: Bellway - The Slab"),
    ),
    # The capture sequence ends at Slab_Cell L1.
    "the-slab/slab-cell#l1": (
        _part("Path: The Slab - Capture Event"),
    ),
}


_ALL_NODE_SEEDS: Mapping[str, tuple[CompiledRoomClause, ...]] = {
    **_NODE_SEEDS,
    **_EXTERNAL_BOUNDARY_SEEDS,
}


# Dust_02 is missing its free downward route from the top opening. The y=93
# lever blocks the room edge, not this descent. Keep the movement-gated climb
# in the other direction.
_CURATED_EDGES: tuple[
    tuple[str, str, tuple[CompiledRoomClause, ...]], ...
] = (
    (
        "sinner-s-road/sinner-s-road-vertical-hall-west#top",
        "sinner-s-road/sinner-s-road-vertical-hall-west#upper-right",
        (_part(),),
    ),
)


# Existing AP locations whose room records represent the
# physical action/room but do not spell the current AP check name as an
# Included check row.  These are identity bindings only. Access still comes
# entirely from the room graph.
_EXISTING_LOCATION_NODE_BINDINGS: Mapping[str, str] = {
    "Beast Shard: Marrowmaw": "moss-grotto/moss-grotto-center#rock-bottom",
    "Bellhart - Outer Sign": "bellhart/belltown-07#room",
    "Mosshome - Moss Plaque": "bone-bottom/mosshome-upper#main-area",
    "The Marrow - Entrance Inscription": (
        "the-marrow/the-marrow-bell-bench#bell-bench"
    ),
    "The Marrow (Flea Caravan) - Rosary Dish": (
        "the-marrow/the-marrow-flea-caravan#main-area"
    ),
    "Bellshrine: The Marrow": "the-marrow/the-marrow-bellshrine#room",
    "Tool Pouch: Loddie": "the-marrow/the-marrow-jail#room",
    "The Marrow - Rosary Cache #11": (
        "the-marrow/the-marrow-lava-track#left-maze"
    ),
    "The Marrow - Rosary Cache #12": (
        "the-marrow/the-marrow-lava-track#left-maze"
    ),
    "The Marrow - Rosary Cache #13": (
        "the-marrow/the-marrow-lava-track#right-maze"
    ),
    "Map Purchase: Wormways": "wormways/wormways-upper-east#upper-area",
    "Pin Purchase: Vendor Pins": "wormways/wormways-upper-east#upper-area",
    "Bellway: Deep Docks": "deep-docks/deep-docks-bellway#room",
    "Bellshrine: Deep Docks": "deep-docks/deep-docks-bellshrine#room",
    "Shellwood - Shellgrave Inscription": "shellwood/shellgrave#room",
    "Shellwood - Weaver Harp Inscription": (
        "shellwood/shellwood-10#lower-level"
    ),
    "Bellshrine: Bellhart": "bellhart/belltown-shrine#arena",
    "Relic Turn-in: Weaver Effigy (Keelal, Shellwood)": (
        "bellhart/belltown-room-relic#room"
    ),
    "Relic Turn-in: Bone Scroll (Wisp Thicket)": (
        "bellhart/belltown-room-relic#room"
    ),
    "Relic Turn-in: Weaver Effigy (Camora, Moss Grotto)": (
        "bellhart/belltown-room-relic#room"
    ),
    "Relic Turn-in: Choral Commandment (Jubilana)": (
        "bellhart/belltown-room-relic#room"
    ),
    "Relic Turn-in: Rune Harp (High Halls)": (
        "bellhart/belltown-room-relic#room"
    ),
    "Relic Turn-in: Choral Commandment (Western Whiteward)": (
        "bellhart/belltown-room-relic#room"
    ),
    "Relic Turn-in: Rune Harp (Weavenest Cindril)": (
        "bellhart/belltown-room-relic#room"
    ),
    "Relic Turn-in: Rune Harp (Weavenest Atla)": (
        "bellhart/belltown-room-relic#room"
    ),
    "Relic Turn-in: Bone Scroll (Underworks)": (
        "bellhart/belltown-room-relic#room"
    ),
    "Relic Turn-in: Bone Scroll (Far Fields)": (
        "bellhart/belltown-room-relic#room"
    ),
    "Relic Turn-in: Choral Commandment (Moss Grotto)": (
        "bellhart/belltown-room-relic#room"
    ),
    "Relic Turn-in: Choral Commandment (Eastern Whiteward)": (
        "bellhart/belltown-room-relic#room"
    ),
    "Relic Turn-in: Bone Scroll (Greymoor)": (
        "bellhart/belltown-room-relic#room"
    ),
    "Relic Turn-in: Weaver Effigy (Atla, The Slab)": (
        "bellhart/belltown-room-relic#room"
    ),
    "Relic Turn-in: Arcane Egg": "bellhart/belltown-room-relic#room",
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
        minimum_skip_tier=max(
            left.minimum_skip_tier,
            right.minimum_skip_tier,
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
    if atom.startswith("count:pollip-heart:"):
        try:
            minimum = int(atom.rsplit(":", 1)[1])
        except ValueError as exc:
            raise ValueError(
                f"invalid room-graph pollip-heart count atom: {atom!r}"
            ) from exc
        if minimum < 1:
            raise ValueError(
                f"invalid room-graph pollip-heart count atom: {atom!r}"
            )
        return (_part(item_counts=(("Pollip Heart", minimum),)),)
    if atom.startswith("count:progressive-silkheart:"):
        try:
            minimum = int(atom.rsplit(":", 1)[1])
        except ValueError as exc:
            raise ValueError(
                f"invalid room-graph Progressive Silkheart count atom: {atom!r}"
            ) from exc
        if minimum < 1:
            raise ValueError(
                f"invalid room-graph Progressive Silkheart count atom: {atom!r}"
            )
        return (_part(item_counts=(("Progressive Silkheart", minimum),)),)
    if atom.startswith("count:progressive-needle-upgrade:"):
        try:
            minimum = int(atom.rsplit(":", 1)[1])
        except ValueError as exc:
            raise ValueError(
                "invalid room-graph Progressive Needle Upgrade count atom: "
                f"{atom!r}"
            ) from exc
        if minimum < 1:
            raise ValueError(
                "invalid room-graph Progressive Needle Upgrade count atom: "
                f"{atom!r}"
            )
        return (_part(item_counts=(("Progressive Needle Upgrade", minimum),)),)
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
        # DNF alternatives are monotonic. If an existing clause requires a
        # subset of the new clause's nodes/items/options, the new clause can
        # never unlock anything and is redundant. Conversely, a newly added
        # weaker clause replaces every stronger alternative it subsumes.
        if any(_clause_subsumes(existing, requirement) for existing in bucket):
            continue
        bucket[:] = [
            existing
            for existing in bucket
            if not _clause_subsumes(requirement, existing)
        ]
        bucket.append(requirement)


def _clause_subsumes(
    weaker: CompiledRoomClause,
    stronger: CompiledRoomClause,
) -> bool:
    """Return whether satisfying ``stronger`` always satisfies ``weaker``."""

    if not set(weaker.all_of).issubset(stronger.all_of):
        return False
    if weaker.require_silk_spear and not stronger.require_silk_spear:
        return False
    if weaker.minimum_skip_tier > stronger.minimum_skip_tier:
        return False

    weaker_counts = dict(weaker.item_counts)
    stronger_counts = dict(stronger.item_counts)
    return all(
        minimum <= stronger_counts.get(item_name, 0)
        for item_name, minimum in weaker_counts.items()
    )


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
