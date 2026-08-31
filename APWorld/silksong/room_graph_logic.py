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
SPOOL_FRAGMENT_COUNT_ITEM = "__room_graph_spool_fragments__"


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
    "item:architects-key": (_part("Architect's Key"),),
    "item:beast-crest": (_part("Crest: Beast"),),
    "item:bell-shellwood": (_part("Bell: Shellwood"),),
    "item:bellway-blasted-steps": (_part("Bellway: Blasted Steps"),),
    "item:bellway-bilewater": (_part("Bellway: Bilewater"),),
    "item:bellway-bellhart": (_part("Bellway: Bellhart"),),
    "item:bellway-far-fields": (_part("Bellway: Far Fields"),),
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
    "item:needle-strike": (_part("Ability: Needle Strike"),),
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
    "path:bellways": (_part("Path: Bellways"),),
    "path:bilewater-twisted-bud": (
        _part("Path: Bilewater - Twisted Bud"),
    ),
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
    "macro:crest-pogo": (
        _part("Crest: Hunter", skip_tier=1),
        _part("Crest: Reaper", skip_tier=1),
        _part("Crest: Shaman", skip_tier=1),
        _part("Crest: Architect", skip_tier=1),
        _part("Crest: Wanderer", skip_tier=1),
        _part("Crest: Beast", skip_tier=1),
        _part("Crest: Witch", skip_tier=1),
    ),
    "macro:architect-charge": (
        _part("Ability: Needle Strike", "Crest: Architect"),
    ),
    "macro:beast-charge": (
        _part("Ability: Needle Strike", "Crest: Beast"),
    ),
    "macro:hunter-charge": (
        _part("Ability: Needle Strike", "Crest: Hunter"),
    ),
    "macro:reaper-charge": (
        _part("Ability: Needle Strike", "Crest: Reaper"),
    ),
    "macro:shaman-charge": (
        _part("Ability: Needle Strike", "Crest: Shaman"),
    ),
    "macro:wanderer-charge": (
        _part("Ability: Needle Strike", "Crest: Wanderer"),
    ),
    "macro:witch-charge": (
        _part("Ability: Needle Strike", "Crest: Witch"),
    ),
    "macro:blasted-crest-pogo": (
        _part("Crest: Hunter", skip_tier=1),
        _part("Crest: Reaper", skip_tier=1),
        _part("Crest: Shaman", skip_tier=1),
        _part("Crest: Architect", skip_tier=1),
        _part("Crest: Beast", skip_tier=1),
    ),
    "macro:blasted-proficient-beast": (
        _part("Crest: Beast", skip_tier=1),
    ),
    "macro:blasted-easy-flea-brew-stall": (
        _part("Usable Flea Brew", skip_tier=1),
    ),
    "macro:blasted-flea-brew-stall": (
        _part("Usable Flea Brew", skip_tier=3),
    ),
    "macro:blasted-easy-heal-stall": (_part(skip_tier=1),),
    "macro:blasted-heal-stall": (_part(skip_tier=3),),
    "macro:any-non-hunter-crest": (
        _part("Crest: Reaper", skip_tier=1),
        _part("Crest: Shaman", skip_tier=1),
        _part("Crest: Architect", skip_tier=1),
        _part("Crest: Wanderer", skip_tier=1),
        _part("Crest: Beast", skip_tier=1),
        _part("Crest: Witch", skip_tier=1),
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
    # Every mapped Scuttlebrace route is the full-speed crawl technique, so
    # merely owning/equipping the tool is insufficient. It also needs the full
    # Swift Step upgrade, never only the first progressive stage.
    "macro:usable-scuttlebrace": (
        _part("Usable Scuttlebrace", "Swift Step"),
    ),
    "macro:usable-curveclaw": tuple(
        _part("Progressive Curveclaw", crest)
        for crest in (
            "Crest: Hunter",
            "Crest: Reaper",
            "Crest: Architect",
            "Crest: Wanderer",
            "Crest: Beast",
            "Crest: Witch",
        )
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
    "capability:ledge-grab": (_part("Capability: Ledge Grab"),),
    "capability:swim": (_part("Capability: Swim"),),
    # The client keeps the randomized Wanderer chapel door open, making its
    # monotonic override a free capability in AP logic.
    "capability:wanderer-door-override": (_part(),),
    "state:act-1": (_part("Act: 1"),),
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
        "event:weavenest-atla/weavenest-atla-power/weavenest-atla-power-activation",
    "event:weavenest-atla/weavenest-atla-grotto/defeat-double-moss-mother":
        "event:weavenest-atla/weavenest-atla-grotto/double-moss-mother-boss-fight",
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
        "event:whispering-vaults/grand-bellway-library/"
        "flick-the-first-lever"
    ): (
        "event:whispering-vaults/grand-bellway-library/"
        "whispering-vaults-flip-switch-6-left"
    ),
    (
        "event:whispering-vaults/vaultkeeper-cauldron-entrance/"
        "open-door-from-right-side-switch-1"
    ): (
        "event:whispering-vaults/vaultkeeper-cauldron-entrance/"
        "whispering-vaults-flip-switch-1-down"
    ),
    (
        "event:deep-docks/deep-docks-magma-slug-tunnels/"
        "right-door-switch-must-be-flipped"
    ):
        "event:deep-docks/deep-docks-magma-slug-tunnels/right-door-switch",
    "event:far-fields/far-fields-deep-docks-loopback/gate-switch-flipped":
        "event:far-fields/far-fields-deep-docks-loopback/gate-switch",
    "event:far-fields/far-fields-deep-lower-west/door-switch-activated":
        "event:far-fields/far-fields-deep-lower-west/door-switch",
    "event:far-fields/far-fields-entrance-east/deep-docks-bellshrine-activated":
        "event:deep-docks/deep-docks-bellshrine/activate-deep-docks-bellshrine-switch",
    "event:hunter-s-march/chapel-of-the-beast/door-switch-flipped":
        "event:hunter-s-march/chapel-of-the-beast/door-switch",
    "event:hunter-s-march/hunter-s-march-deep-docks-passage/defeat-gauntlet":
        "event:hunter-s-march/hunter-s-march-deep-docks-passage/gauntlet-fight",
    "event:hunter-s-march/hunter-s-march-map-shop/defeat-gauntlet":
        "event:hunter-s-march/hunter-s-march-map-shop/gauntlet-fight",
}


# Persistent actions inferred from the controlling side.
# Every entry is monotonic: once that node is reachable, the player can perform
# the action and retain the shortcut/state for the rest of the logic search.
_IMPLICIT_EVENT_SOURCE: Mapping[str, tuple[CompiledRoomClause, ...]] = {
    "event:sinner-s-road/sinner-s-road-styx-room/left-right-wall-broken": (
        _part(room_node_name("sinner-s-road/sinner-s-road-styx-room#left")),
    ),
    "event:sinner-s-road/sinner-s-road-styx-room/cage-right-wall-broken": (
        _part(room_node_name("sinner-s-road/sinner-s-road-styx-room#cage")),
    ),
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
    "event:deep-docks/deep-docks-forge/open-airlock-up": (
        _part(room_node_name("deep-docks/deep-docks-forge#gauntlet")),
    ),
    "event:deep-docks/deep-docks-forge/open-airlock-down": (
        _part(room_node_name("deep-docks/deep-docks-forge#forge-daughter")),
    ),
    # The reverse route says the Forge-side crossing is free
    # and activates the airlock.  This is the same state Lace requires from
    # the opposite direction and is unrelated to the spool-room airlock.
    "event:deep-docks/deep-docks-lace-intro/airlock-lever": (
        _part(room_node_name("deep-docks/deep-docks-forge#left-area")),
    ),
    "event:deep-docks/deep-docks-forge/activate-airlock": (
        _part(room_node_name("deep-docks/deep-docks-forge#left-area")),
    ),
    "event:deep-docks/deep-docks-forge/gate-switch-activated": (
        _part(room_node_name("deep-docks/deep-docks-forge#right-area")),
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
    "event:underworks/underworks-east-shaft/switch-3-flipped": (
        _part(
            room_node_name(
                "underworks/underworks-east-shaft#mid-right-entrance"
            )
        ),
    ),
    "event:underworks/underworks-eastern-gauntlet/lever-1-flipped": (
        _part(
            room_node_name(
                "underworks/underworks-eastern-gauntlet#arena"
            )
        ),
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
        _part(
            room_node_name("choral-chambers/choral-chambers-spa#spa"),
            "Ancestral Art: Cling Grip",
        ),
        _part(
            room_node_name("choral-chambers/choral-chambers-spa#spa"),
            "Ancestral Art: Silk Soar",
        ),
        _part(
            room_node_name("choral-chambers/choral-chambers-spa#spa"),
            "Ability: Faydown Cloak",
        ),
    ),
    # This shortcut is opened while approaching from Underworks.
    (
        "event:choral-chambers/choral-chambers-outisde-underworks/"
        "only-opened-from-the-other-side"
    ): (
        _part(
            room_node_name(
                "underworks/underworks-outside-choral-chambers#room"
            ),
            "Ancestral Art: Cling Grip",
        ),
        _part(
            room_node_name(
                "underworks/underworks-outside-choral-chambers#room"
            ),
            "Ancestral Art: Silk Soar",
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
    (
        "event:choral-chambers/high-halls-corridor/"
        "left-door-opened-from-flea-shaft"
    ): (
        _part(
            room_node_name(
                "choral-chambers/choral-chambers-flea-shaft#top-section-3-right"
            )
        ),
    ),
    (
        "event:choral-chambers/high-halls-corridor/"
        "top-door-opened-from-high-halls-vault"
    ): (
        _part(room_node_name("high-halls/high-halls-vault#room")),
    ),
    "event:blasted-steps/great-conchflies/beat-great-conchflies": (
        _part(room_node_name("blasted-steps/great-conchflies#great-conchflies")),
    ),
    "event:blasted-steps/blasted-steps-toll-bench-bottom/lever-broken-from-coral-02": (
        _part(
            room_node_name(
                "blasted-steps/blasted-steps-toll-bench-bottom#top-right-pit-left"
            )
        ),
    ),
    "event:blasted-steps/blasted-steps-wide-long-vertical/lever-broken-from-coral-03": (
        _part(
            room_node_name(
                "blasted-steps/blasted-steps-wide-long-vertical#top-third"
            )
        ),
    ),
    "event:whiteward/whiteward-entrance/set-elevator-to-top": (
        _part(
            room_node_name("whiteward/whiteward-entrance#elevator-shaft"),
            "White Key",
        ),
    ),
    "event:whiteward/whiteward-sherma-gauntlet/balm-for-the-wounded-started": (
        _part("Path: Choral Chambers - Songclave"),
    ),
    "event:whiteward/whiteward-sherma-gauntlet/beat-sherma-gauntlet": (
        _part(room_node_name("whiteward/whiteward-sherma-gauntlet#room")),
    ),
    "event:whiteward/whiteward-unravelled-arena-room/surgeons-key-used": (
        _part(
            room_node_name(
                "whiteward/whiteward-unravelled-arena-room#surgery-tables-left"
            ),
            "Surgeon's Key",
        ),
    ),
    "event:whiteward/whiteward-unravelled-arena-room/beat-unravelled-arena": (
        _part(
            room_node_name(
                "whiteward/whiteward-unravelled-arena-room#key-shaft"
            ),
            "Surgeon's Key",
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
        _part(
            room_node_name("shellwood/shellwood-15#room"),
            "Ability: Faydown Cloak",
        ),
        _part(
            room_node_name("shellwood/shellwood-15#room"),
            "Ability: Drifter's Cloak",
        ),
        _part(
            room_node_name("shellwood/shellwood-15#room"),
            "Swift Step",
        ),
        _part(
            room_node_name("shellwood/shellwood-15#room"),
            "Ancestral Art: Clawline",
        ),
        _part(
            room_node_name("shellwood/shellwood-15#room"),
            "Usable Sharpdart",
        ),
        _part(
            room_node_name("shellwood/shellwood-15#room"),
            "Crest: Beast",
        ),
    ),
    "event:shellwood/shellwood-26/upper-wall-broken": (
        _part(room_node_name("shellwood/shellwood-26#upper-area")),
    ),
    "event:far-fields/far-fields-deep-docks-backdoor/door-opened": (
        _part(
            room_node_name(
                "deep-docks/deep-docks-chains-upper-east#chain-platforms"
            )
        ),
    ),
    "event:far-fields/far-fields-deep-entrance/right-exit-door-switch-activated": (
        _part(
            room_node_name(
                "far-fields/far-fields-deep-entrance#right-exit-area"
            )
        ),
    ),
    "event:far-fields/far-fields-deep-fort/grunt-defeated": (
        _part(room_node_name("far-fields/far-fields-deep-fort#main-area")),
    ),
    "event:far-fields/far-fields-fort-upper-passage/platform-lowered": (
        _part(
            room_node_name(
                "far-fields/far-fields-fort-upper-passage#left-exit-area"
            )
        ),
    ),
    "event:far-fields/far-fields-pilgrim-s-rest/deep-passage-opened": (
        _part(
            room_node_name(
                "far-fields/far-fields-pilgrim-s-rest-deep-passage#room"
            )
        ),
    ),
    "event:far-fields/far-fields-pinstress-room/attic-blast-opened": (
        _part(
            room_node_name(
                "far-fields/far-fields-pinstress-attic#bottom-left-area"
            )
        ),
    ),
    "event:far-fields/far-fields-skull-room-west/bone-bridge-wall-broken": (
        _part(
            room_node_name(
                "far-fields/far-fields-skull-room-west#bone-bridge"
            )
        ),
    ),
    "event:far-fields/far-fields-skull-room-west/lower-left-wall-broken": (
        _part(
            room_node_name(
                "far-fields/far-fields-skull-room-west#lower-left-exit-area"
            )
        ),
    ),
    "event:far-fields/far-fields-upper-shaft/bridge-lever-activated": (
        _part(
            room_node_name(
                "far-fields/far-fields-upper-shaft#left-march-bridge-room"
            )
        ),
    ),
    "event:far-fields/far-fields-upper-shaft/lower-gate-opened": (
        _part(
            room_node_name("far-fields/far-fields-upper-shaft#the-bottom")
        ),
    ),
    "event:far-fields/far-fields-upper-shaft/shaft-opened": (
        _part(
            room_node_name(
                "far-fields/far-fields-upper-shaft#top-wind-tunnel"
            ),
            "Ability: Drifter's Cloak",
        ),
    ),
    "event:greymoor/greymoor-bellshrine/activated": (
        _part(
            room_node_name("greymoor/greymoor-bellshrine#room"),
            "Event: Craw Lake Cleared",
        ),
    ),
    "event:greymoor/greymoor-craw-lake/lever-broken-from-greymoor-crow-nest": (
        _part(
            room_node_name("greymoor/greymoor-crow-nest#crow-arena")
        ),
    ),
    "event:greymoor/greymoor-west-bellshrine-room/lever-switch": (
        _part(
            room_node_name(
                "greymoor/greymoor-west-bellshrine-room#middle-section-right"
            )
        ),
    ),
    "event:hunter-s-march/chapel-of-the-beast/boss-defeated": tuple(
        _part(
            room_node_name("hunter-s-march/chapel-of-the-beast#boss-arena"),
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
    "event:hunter-s-march/hunter-s-march-deep-docks-passage/gate-switch-flipped": (
        _part(
            room_node_name(
                "hunter-s-march/hunter-s-march-deep-docks-passage#before-gate"
            )
        ),
    ),
    "event:hunter-s-march/hunter-s-march-entrance/grunt-defeated": (
        _part(
            room_node_name(
                "hunter-s-march/hunter-s-march-entrance#before-door"
            )
        ),
    ),
    "event:hunter-s-march/hunter-s-march-treasure-vault/grunts-defeated": (
        _part(
            room_node_name(
                "hunter-s-march/hunter-s-march-treasure-vault#right-of-door"
            )
        ),
    ),
}


_GLOBAL_EVENT_NAME_BY_ATOM: Mapping[str, str] = {
    "event:global/bell-beast-defeated":
        "Event: Bell Beast Defeated",
    "event:global/cogwork-dancers-defeated":
        "Event: Cogwork Dancers Defeated",
    "event:global/elegy-of-the-deep-learned":
        "Event: Elegy of the Deep Learned",
    "event:global/flexile-spines-completed":
        "Event: Flexile Spines Completed",
    "event:global/five-bellshrines-rung":
        "Event: Five Bellshrines Rung",
    "event:global/last-judge-defeated": "Event: Last Judge Defeated",
    "event:grand-gate/grand-bridge/last-judge-defeated":
        "Event: Last Judge Defeated",
    "event:global/missing-courier-rescued":
        "Event: Missing Courier Rescued",
    "event:global/tormented-trobbio-defeated":
        "Event: Tormented Trobbio Defeated",
    "event:global/trobbio-defeated": "Event: Trobbio Defeated",
    "event:the-marrow/the-marrow-bellway/bell-beast-defeated":
        "Event: Bell Beast Defeated",
}


# The room graph starts at Hornet's opening room. Internal coarse-path seeds
# are excluded because they let flat fallback rules bypass doors, switches
# and room connections.
_NODE_SEEDS: Mapping[str, tuple[CompiledRoomClause, ...]] = {
    "moss-grotto/moss-grotto-center#rock-bottom": (_part(),),
}


_EXTERNAL_BOUNDARY_SEEDS: Mapping[str, tuple[CompiledRoomClause, ...]] = {
    # The mapped Bellway room starts at the existing Blasted Steps endpoint.
    "blasted-steps/blasted-steps-bellway#room": (
        _part("Path: Blasted Steps - Bellway"),
    ),
    # The Sinner's Road entrance uses Hornet's ordinary ledge grab. Keeping
    # the capability in the clause makes it innate when the option is off and
    # an additional item gate when Ledge Grab randomization is enabled.
    "sinner-s-road/sinner-s-road-entrance#room": (
        _part(
            "Path: Greymoor - Halfway House",
            "Capability: Ledge Grab",
        ),
    ),
    # The mapper's unresolved left transition is the missing handoff from
    # Halfway House. Preserve every compilable alternative. Innate Swim keeps
    # the normal graph unchanged; Swim randomization activates the other gates.
    "greymoor/greymoor-lower-halfway-home-path#room": (
        _part("Path: Greymoor - Halfway House", "Swift Step"),
        _part("Path: Greymoor - Halfway House", "Progressive Swift Step"),
        _part("Path: Greymoor - Halfway House", "Ancestral Art: Clawline"),
        _part("Path: Greymoor - Halfway House", "Ability: Faydown Cloak"),
        _part("Path: Greymoor - Halfway House", "Ability: Drifter's Cloak"),
        _part("Path: Greymoor - Halfway House", "Usable Sharpdart"),
        _part(
            "Path: Greymoor - Halfway House",
            "Crest: Beast",
            skip_tier=1,
        ),
        _part(
            "Path: Greymoor - Halfway House",
            "Usable Flea Brew",
            skip_tier=1,
        ),
        _part("Path: Greymoor - Halfway House", "Capability: Swim"),
    ),
    "greymoor/greymoor-east-bellshrine-room#upper-crow-nest": (
        _part(
            "Path: Greymoor - Halfway House",
            "Ancestral Art: Cling Grip",
        ),
    ),
    "greymoor/greymoor-craw-lake#upper-craw-nest": (
        _part(
            "Path: Greymoor - Halfway House",
            "Ancestral Art: Cling Grip",
        ),
    ),
    # The Bone Scroll side room is entered from the lower Halfway House route
    # by swimming or by combining Clawline with the ordinary ledge grab.
    "greymoor/greymoor-bone-scroll-room#room": (
        _part("Path: Greymoor - Halfway House", "Capability: Swim"),
        _part(
            "Path: Greymoor - Halfway House",
            "Ancestral Art: Clawline",
            "Capability: Ledge Grab",
        ),
    ),
    # Last Judge's arena is outside this room set. Defeating it puts Hornet at
    # the top of the Grand Elevator before the collapse.
    "grand-gate/grand-elevator#top": (
        _part("Event: Act 2 Started", "Event: Last Judge Defeated"),
    ),
    # The missing lower Greymoor transition enters the bottom of the Wisp bench
    # room.
    "whisp-thicket/wisp-thicket-bench#bottom": (
        _part("Path: Greymoor - Wisp Thicket"),
    ),
    "bilewater/exhaust-organ-external#left-platform": (
        _part("Path: Bilewater - Exhaust Organ"),
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

    "deep-docks/deep-docks-forge#left-area": (
        _part("Path: Deep Docks - Forge"),
    ),
    "deep-docks/deep-docks-chains-upper-east#chain-platforms": (
        _part("Path: Deep Docks - Forge", "Simple Key (Deep Docks)"),
    ),
    "deep-docks/deep-docks-bellway#room": (
        _part("Path: Bellway - Deep Docks"),
    ),
    "far-fields/far-fields-bellway#bellway": (
        _part("Path: Bellway - Far Fields"),
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


_TRANSITION_EXTRA_REQUIREMENTS: Mapping[
    str, tuple[CompiledRoomClause, ...]
] = {
    "whisp-thicket/wisp-thicket-cave@t": (
        _part("Event: Act 2 Started"),
    ),
    "bilewater/exhaust-organ-interior@ue": (
        _part("Event: Act 2 Started"),
    ),
    "grand-gate/grand-bridge@r": (
        _part("Event: Act 2 Started"),
    ),
}


# Dust_02 is missing its free downward route from the top opening. The y=93
# lever blocks the room edge, not this descent. Keep the movement-gated climb
# in the other direction.
_CURATED_EDGES: tuple[
    tuple[str, str, tuple[CompiledRoomClause, ...]], ...
] = (
    (
        "shellwood/shellwood-10#ground-level",
        "shellwood/shellwood-10#central-level",
        (_part(skip_tier=1),),
    ),
    (
        "bilewater/bilewater-sinner-s-entrance#left-quarter",
        "bilewater/bilewater-sinner-s-entrance#middle-quarter",
        (
            _part("Ancestral Art: Cling Grip", "Ability: Drifter's Cloak"),
            _part("Ancestral Art: Cling Grip", "Ancestral Art: Clawline"),
            _part("Ancestral Art: Cling Grip", "Usable Sharpdart"),
            _part("Ancestral Art: Cling Grip", "Ability: Faydown Cloak", "Capability: Swim"),
            _part("Ability: Faydown Cloak", "Usable Scuttlebrace", "Capability: Swim"),
            _part("Ancestral Art: Clawline", "Usable Scuttlebrace"),
            _part("Usable Sharpdart", "Usable Scuttlebrace"),
            _part("Swift Step", "Usable Scuttlebrace"),
        ),
    ),
    (
        "greymoor/greymoor-east-bellshrine-room#upper-crow-nest",
        "greymoor/greymoor-silver-shells-room#main-path",
        (
            _part("Ancestral Art: Silk Soar"),
            _part("Ability: Faydown Cloak"),
            _part("Ancestral Art: Cling Grip", skip_tier=1),
            _part("Ancestral Art: Cling Grip", "Swift Step"),
            _part("Ancestral Art: Cling Grip", "Ancestral Art: Clawline"),
            _part("Ancestral Art: Cling Grip", "Usable Sharpdart"),
            _part("Ancestral Art: Cling Grip", "Ability: Drifter's Cloak"),
            _part(skip_tier=3),
            _part("Capability: Ledge Grab", "Swift Step", skip_tier=2),
            _part("Capability: Ledge Grab", "Ancestral Art: Clawline", skip_tier=2),
        ),
    ),
    (
        "sinner-s-road/sinner-s-road-vertical-hall-west#top",
        "sinner-s-road/sinner-s-road-vertical-hall-west#upper-right",
        (_part(),),
    ),
    (
        "sinner-s-road/sinner-s-road-styx-room#right",
        "sinner-s-road/sinner-s-road-styx-room#left",
        (
            _part(
                room_event_name(
                    "event:sinner-s-road/sinner-s-road-styx-room/left-right-wall-broken"
                ),
                skip_tier=1,
            ),
            _part(
                room_event_name(
                    "event:sinner-s-road/sinner-s-road-styx-room/left-right-wall-broken"
                ),
                "Ability: Faydown Cloak",
            ),
            _part(
                room_event_name(
                    "event:sinner-s-road/sinner-s-road-styx-room/left-right-wall-broken"
                ),
                "Ancestral Art: Silk Soar",
                "Ability: Drifter's Cloak",
            ),
            _part(
                room_event_name(
                    "event:sinner-s-road/sinner-s-road-styx-room/left-right-wall-broken"
                ),
                "Ancestral Art: Cling Grip",
                "Usable Sharpdart",
            ),
            _part(
                room_event_name(
                    "event:sinner-s-road/sinner-s-road-styx-room/left-right-wall-broken"
                ),
                "Ancestral Art: Cling Grip",
                "Ancestral Art: Clawline",
            ),
            _part(
                room_event_name(
                    "event:sinner-s-road/sinner-s-road-styx-room/left-right-wall-broken"
                ),
                "Ancestral Art: Cling Grip",
                "Swift Step",
            ),
        ),
    ),
    (
        "sinner-s-road/sinner-s-road-styx-room#right",
        "sinner-s-road/sinner-s-road-styx-room#cage",
        (
            _part(
                room_event_name(
                    "event:sinner-s-road/sinner-s-road-styx-room/cage-right-wall-broken"
                )
            ),
        ),
    ),
    (
        "greymoor/greymoor-craw-lake#craw-building",
        "greymoor/greymoor-craw-lake#middle-craw-nest",
        tuple(
            _part(
                room_event_name(
                    "event:greymoor/greymoor-craw-lake/lever-broken-from-greymoor-crow-nest"
                ),
                crest,
                skip_tier=1,
            )
            for crest in (
                "Crest: Hunter",
                "Crest: Reaper",
                "Crest: Wanderer",
                "Crest: Beast",
                "Crest: Witch",
                "Crest: Architect",
                "Crest: Shaman",
            )
        ),
    ),
    (
        "sands-of-karak/sands-of-karak-lower-right-long-room#left-exit",
        "sands-of-karak/sands-of-karak-lower-right-long-room#lower-centre-platform",
        (
            _part("Ancestral Art: Clawline"),
        ),
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
        "the-marrow/the-marrow-lava-track#left-alcove"
    ),
    "The Marrow - Rosary Cache #12": (
        "the-marrow/the-marrow-lava-track#left-alcove"
    ),
    "The Marrow - Rosary Cache #13": (
        "the-marrow/the-marrow-lava-track#right-alcove"
    ),
    "Map Purchase: Wormways": "wormways/wormways-upper-east#upper-area",
    "Pin Purchase: Vendor Pins": "wormways/wormways-upper-east#upper-area",
    "Bellway: Deep Docks": "deep-docks/deep-docks-bellway#room",
    "Bellshrine: Deep Docks": "deep-docks/deep-docks-bellshrine#room",
    "Shellwood - Shellgrave Inscription": "shellwood/shellgrave#room",
    "Shellwood - Weaver Harp Inscription": (
        "shellwood/shellwood-10#ground-level"
    ),
    "Bellshrine: Bellhart": "bellhart/belltown-shrine#arena",
    "Curveclaw": "hunter-s-march/hunter-s-march-skarr-shop#skarr-shop",
    "Far Fields - Rosary Chest #1": (
        "far-fields/far-fields-fort-upper-passage#left-exit-area"
    ),
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


_GREYMOOR_EAST_CACHE_POGO_REQUIREMENTS = tuple(
    _part(
        room_node_name(
            "greymoor/greymoor-east-bellshrine-room#upper-crow-nest"
        ),
        crest,
        movement,
        skip_tier=1,
    )
    for crest in (
        "Crest: Hunter",
        "Crest: Reaper",
        "Crest: Wanderer",
        "Crest: Beast",
        "Crest: Witch",
        "Crest: Architect",
        "Crest: Shaman",
    )
    for movement in (
        "Ancestral Art: Clawline",
        "Usable Sharpdart",
        "Swift Step",
        "Ability: Drifter's Cloak",
    )
)


# Existing AP locations whose source is expressed by a persistent graph
# event rather than by a dedicated Included check row.
_EXISTING_LOCATION_CLAUSE_BINDINGS: Mapping[
    str, tuple[CompiledRoomClause, ...]
] = {
    "Greymoor - Rosary Cache #2": _GREYMOOR_EAST_CACHE_POGO_REQUIREMENTS,
    "Greymoor - Rosary Cache #3": _GREYMOOR_EAST_CACHE_POGO_REQUIREMENTS,
    "Beast Shard: Craggler": (
        _part(
            room_event_name(
                "event:wormways/wormways-craggler-hallway/defeat-craggler"
            )
        ),
    ),
    "Drifter's Cloak": (
        _part(
            room_node_name("far-fields/far-fields-pinstress-hut-interior#room"),
            "Event: Flexile Spines Completed",
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
    if atom.startswith("count:spool-fragment:"):
        try:
            minimum = int(atom.rsplit(":", 1)[1])
        except ValueError as exc:
            raise ValueError(
                f"invalid room-graph Spool Fragment count atom: {atom!r}"
            ) from exc
        if minimum < 1 or minimum > 18:
            raise ValueError(
                f"invalid room-graph Spool Fragment count atom: {atom!r}"
            )
        return (_part(item_counts=((SPOOL_FRAGMENT_COUNT_ITEM, minimum),)),)
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
    compiler check, normal items and coarse paths are available.
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
                alternative.minimum_skip_tier == 0
                and all(
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
            requirements = _compile_spec(
                transition.requirement,
                room_node_name(transition.source_node_id),
            )
            extra_requirements = _TRANSITION_EXTRA_REQUIREMENTS.get(
                transition.id,
                (_part(),),
            )
            _append_requirements(
                node_requirements,
                room_node_name(target_node_id),
                (
                    _merge(requirement, extra_requirement)
                    for requirement, extra_requirement in product(
                        requirements,
                        extra_requirements,
                    )
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
            if (
                not requirements
                or all(
                    requirement.minimum_skip_tier > 0
                    for requirement in requirements
                )
            ):
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
