from __future__ import annotations

from dataclasses import dataclass


SONGCLAVE_BOARD_COMPLETION_ITEM = (
    "Songclave Board Wish Completion"
)
SILK_AND_SOUL_MANDATORY_WISH_ITEM = (
    "Silk and Soul Mandatory Wish Completion"
)
SILK_AND_SOUL_WISH_POINT_ITEM = "Silk and Soul Wish Point"
SILK_AND_SOUL_WISH_HALF_POINT_ITEM = (
    "Silk and Soul Wish Half-Point"
)
SILK_AND_SOUL_LACE_DEFEATED_ITEM = (
    "Silk and Soul: Lace Defeated"
)


@dataclass(frozen=True)
class WishLogicEvent:
    """One addressless AP event derived from a verified logical source."""

    location_name: str
    item_name: str
    source_location: str = ""
    source_region: str = ""

    def __post_init__(self) -> None:
        if bool(self.source_location) == bool(self.source_region):
            raise ValueError(
                "Wish logic events need exactly one source location or region."
            )


def _location_event(
    label: str,
    item_name: str,
    source_location: str,
) -> WishLogicEvent:
    return WishLogicEvent(
        f"Wish Logic: {label}",
        item_name,
        source_location=source_location,
    )


def _region_event(
    label: str,
    item_name: str,
    source_region: str,
) -> WishLogicEvent:
    return WishLogicEvent(
        f"Wish Logic: {label}",
        item_name,
        source_region=source_region,
    )


# Donation 2 appears after four completed Enclave-board Wishes. Donation 1
# and these four verified pre-Donation-2 completions are safe logical sources.
# Final Audience remains excluded until its physical Second Sentinel route is
# mapped; Pain, Anguish and Misery and the remaining board Wishes are later.
SONGCLAVE_BOARD_EVENTS: tuple[WishLogicEvent, ...] = (
    _region_event(
        "Building Up Songclave board credit",
        SONGCLAVE_BOARD_COMPLETION_ITEM,
        "Event: Building Up Songclave Completed",
    ),
    _region_event(
        "Fine Pins board credit",
        SONGCLAVE_BOARD_COMPLETION_ITEM,
        "Event: Fine Pins Completed",
    ),
    _region_event(
        "Wandering Merchant board credit",
        SONGCLAVE_BOARD_COMPLETION_ITEM,
        "Event: Wandering Merchant Rescued",
    ),
    _region_event(
        "Balm for the Wounded board credit",
        SONGCLAVE_BOARD_COMPLETION_ITEM,
        "Event: Balm for the Wounded Completed",
    ),
    _region_event(
        "Cloaks of the Choir board credit",
        SONGCLAVE_BOARD_COMPLETION_ITEM,
        "Event: Cloaks of the Choir Completed",
    ),
)


# Trail's End is the tenth mandatory Wish and remains a direct abstract event.
SILK_AND_SOUL_MANDATORY_EVENTS: tuple[WishLogicEvent, ...] = (
    _region_event(
        "Flexile Spines mandatory completion",
        SILK_AND_SOUL_MANDATORY_WISH_ITEM,
        "Event: Flexile Spines Completed",
    ),
    _region_event(
        "Bone Bottom Repairs mandatory completion",
        SILK_AND_SOUL_MANDATORY_WISH_ITEM,
        "Event: Bone Bottom Repairs Completed",
    ),
    _region_event(
        "A Lifesaving Bridge mandatory completion",
        SILK_AND_SOUL_MANDATORY_WISH_ITEM,
        "Event: Lifesaving Bridge Completed",
    ),
    _location_event(
        "Crawbug Clearing mandatory completion",
        SILK_AND_SOUL_MANDATORY_WISH_ITEM,
        "Crafting Kit Source: Crow Feathers",
    ),
    _region_event(
        "Building Up Songclave mandatory completion",
        SILK_AND_SOUL_MANDATORY_WISH_ITEM,
        "Event: Building Up Songclave Completed",
    ),
    _region_event(
        "Strengthening Songclave mandatory completion",
        SILK_AND_SOUL_MANDATORY_WISH_ITEM,
        "Event: Strengthening Songclave Completed",
    ),
    _location_event(
        "Restoration of Bellhart mandatory completion",
        SILK_AND_SOUL_MANDATORY_WISH_ITEM,
        "Quest Completion: Belltown House Start",
    ),
    _location_event(
        "Bellhart's Glory mandatory completion",
        SILK_AND_SOUL_MANDATORY_WISH_ITEM,
        "Quest Completion: Belltown House Mid",
    ),
    _region_event(
        "Balm for the Wounded mandatory completion",
        SILK_AND_SOUL_MANDATORY_WISH_ITEM,
        "Event: Balm for the Wounded Completed",
    ),
)


_SILK_AND_SOUL_FULL_POINT_SOURCES: tuple[tuple[str, str], ...] = (
    ("Berry Picking point", "Tool Unlock: Mosscreep Tool 2"),
    (
        "An Icon of Hope point",
        "Quest Completion: Building Materials (Statue)",
    ),
    (
        "Volatile Flintbeetles point",
        "Quest Completion: Rock Rollers",
    ),
    (
        "My Missing Courier point",
        "Quest Completion: Save Courier Short",
    ),
    (
        "My Missing Brother point",
        "Quest Completion: Save Courier Tall",
    ),
    ("Fine Pins point", "Quest Completion: Fine Pins"),
    ("Roach Guts point", "Tool Unlock: Tack"),
    ("Great Taste point", "Quest Completion: Great Gourmand"),
    (
        "Alchemist's Assistant point",
        "Tool Unlock: Lifeblood Syringe",
    ),
    ("Savage Beastfly point", "Mask Shard: Savage Beastfly"),
    ("Broodfeast point", "Tool Unlock: Longneedle"),
    ("Garb of the Pilgrims point", "Quest Completion: Pilgrim Rags"),
    (
        "The Wandering Merchant point",
        "Quest Completion: Save City Merchant",
    ),
    (
        "The Lost Merchant point",
        "Quest Completion: Save City Merchant Bridge",
    ),
    (
        "Cloaks of the Choir point",
        "Quest Completion: Song Pilgrim Cloaks",
    ),
)


# Pinmaster's Oil, Terrible Tyrant, Bugs of Pharloom, Silver Bells, Final
# Audience and Wailing Mother deliberately do not contribute yet: their exact
# service, turn-in, counter or traversal routes are still quarantined. This is
# conservative but leaves more than the required 17 points available.
SILK_AND_SOUL_FULL_POINT_EVENTS: tuple[WishLogicEvent, ...] = tuple(
    _location_event(label, SILK_AND_SOUL_WISH_POINT_ITEM, source)
    for label, source in _SILK_AND_SOUL_FULL_POINT_SOURCES
) + (
    _region_event(
        "Rite of the Pollip point",
        SILK_AND_SOUL_WISH_POINT_ITEM,
        "Event: Rite of the Pollip Completed",
    ),
)


_SILK_AND_SOUL_HALF_POINT_SOURCES: tuple[tuple[str, str], ...] = (
    (
        "Bone Bottom Supplies half-point",
        "Quest Completion: Courier Delivery Bonebottom",
    ),
    (
        "Pilgrim's Rest Supplies half-point",
        "Quest Completion: Courier Delivery Pilgrims Rest",
    ),
    (
        "Songclave Supplies half-point",
        "Quest Completion: Courier Delivery Songclave",
    ),
    (
        "Fleatopia Supplies half-point",
        "Quest Completion: Courier Delivery Fleatopia",
    ),
    (
        "Queen's Egg half-point",
        "Quest Completion: Courier Delivery Dustpens Slave",
    ),
    (
        "Liquid Lacquer half-point",
        "Quest Completion: Courier Delivery Mask Maker",
    ),
)


SILK_AND_SOUL_HALF_POINT_EVENTS: tuple[WishLogicEvent, ...] = tuple(
    _location_event(label, SILK_AND_SOUL_WISH_HALF_POINT_ITEM, source)
    for label, source in _SILK_AND_SOUL_HALF_POINT_SOURCES
)


SILK_AND_SOUL_FIXED_EVENTS: tuple[WishLogicEvent, ...] = (
    _location_event(
        "Lace Tower defeated",
        SILK_AND_SOUL_LACE_DEFEATED_ITEM,
        "Boss Completion: defeatedLaceTower",
    ),
)


WISH_LOGIC_EVENTS: tuple[WishLogicEvent, ...] = (
    *SONGCLAVE_BOARD_EVENTS,
    *SILK_AND_SOUL_MANDATORY_EVENTS,
    *SILK_AND_SOUL_FULL_POINT_EVENTS,
    *SILK_AND_SOUL_HALF_POINT_EVENTS,
    *SILK_AND_SOUL_FIXED_EVENTS,
)
WISH_LOGIC_EVENT_ITEM_NAMES: frozenset[str] = frozenset(
    event.item_name for event in WISH_LOGIC_EVENTS
)
