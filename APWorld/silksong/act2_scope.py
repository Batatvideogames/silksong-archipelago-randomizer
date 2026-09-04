"""Act 2 goal content exclusions.

The table maps physical sources unavailable before the Act 2 ending to AP
location names. Shared locations remain when at least one source is available
before that ending.
"""

from __future__ import annotations

from collections import Counter
from typing import Iterable, Mapping

from .lore_tablets import (
    LORE_TABLET_ACT_THREE_LOCATION_NAMES,
    LORE_TABLET_ITEM_BY_LOCATION,
)


ACT_TWO_GOAL_KEY = "act_2"
CURSED_ENDING_GOAL_KEY = "cursed_ending"

# Fatal Resolve cannot be finished before Act 3 changes Pinstress. Keep it out
# of Act 1, Act 2 and Cursed Ending. Flea Hunt is not tied to an act.
ACT_THREE_ONLY_GOAL_LOCATION_NAMES: frozenset[str] = frozenset(
    ("Wish: Fatal Resolve",)
)

# Canonical AP locations whose physical sources are unavailable before the
# Act 2 ending. Two source-level exceptions apply:
#
# * Silk Soar's Abyss source leaves, but its item is not listed in the
#   pool-removal table. Randomized Skill modes keep one shuffled copy.
# * The Cradle - Map Purchase remains because Cradle_02 is a valid Act 2 source.
#   Tube_Hub is only the same check's post-Act-3 fallback.
#
# Runtfeast has no separate AP Wish location.  It shares the canonical
# Longclaw check with Act 2 Broodfeast, so that shared location remains.
ACT_TWO_HAND_TESTED_UNAVAILABLE_LOCATION_NAMES: frozenset[str] = frozenset(
    (
        "Silk Soar",
        "Crest: Shaman",
        "Relic: Arcane Egg",
        "The Abyss - Map Pickup",
        "Verdania - Map Pickup",
        "The Abyss - Shell Shard Cache #1",
        "The Abyss - Shell Shard Cache #2",
        "The Abyss - Shell Shard Cache #3",
        "The Abyss - Shell Shard Cache #4",
        "The Abyss - Shell Shard Cache #5",
        "The Abyss - Shell Shard Cache #6",
        "The Abyss - Shell Shard Cache #7",
        "The Abyss - Shell Shard Cache #8",
        "The Abyss - Shell Shard Cache #9",
        "Far Fields - Rosary Cache #19",
        "Boss: Skarrsinger Karmelita",
        "Boss: Crust King Khann",
        "Boss: Nyleth",
        "Boss: Tormented Trobbio",
        "Boss: Gurr the Outcast",
        "Boss: Clover Dancers",
        "Boss: Palestag",
        "Dark Mirror",
        "The Hidden Hunter - Mask Shard",
        "Dark Hearts - Mask Shard",
        "The Cradle - Silkeater",
        "Wish: Passing of the Age",
        "Wish: Advanced Alchemy",
        "Ecstasy of the End - Pale Oil",
        "Pinmaster Plinney: Pale Steel Needle",
        *LORE_TABLET_ACT_THREE_LOCATION_NAMES,
    )
)

ACT_TWO_REQUIRED_DEPENDENCY_LOCATION_NAMES: frozenset[str] = frozenset(
    ("Relic Turn-in: Arcane Egg",)
)

ACT_TWO_EXCLUDED_LOCATION_NAMES: frozenset[str] = (
    ACT_TWO_HAND_TESTED_UNAVAILABLE_LOCATION_NAMES
    | ACT_TWO_REQUIRED_DEPENDENCY_LOCATION_NAMES
)
ACT_TWO_VANILLA_SKILL_RETAINED_LOCATION_NAMES: frozenset[str] = frozenset(
    ("Silk Soar",)
)
ACT_TWO_SHAMAN_SLOT_LOCATION_NAMES: frozenset[str] = frozenset(
    (
        "Crest Slot: Shaman (Blue 1)",
        "Crest Slot: Shaman (Blue 2)",
    )
)



def get_act_two_excluded_location_names(
    starting_crest_item: str,
    skill_mode: str = "anywhere",
) -> frozenset[str]:
    """Return goal exclusions for the configured Skill source behavior.

    Vanilla Skill keeps Silk Soar as an addressless locked native source. Its
    existing post-goal rule lets maximum logic acquire the item without
    inventing a starting grant. Randomized Skill modes omit that physical
    source and keep the item in their random pool instead.
    """

    excluded = ACT_TWO_EXCLUDED_LOCATION_NAMES
    if starting_crest_item != "Crest: Shaman":
        excluded |= ACT_TWO_SHAMAN_SLOT_LOCATION_NAMES

    if skill_mode == "vanilla":
        return excluded - ACT_TWO_VANILLA_SKILL_RETAINED_LOCATION_NAMES
    if skill_mode not in {"shuffle", "anywhere"}:
        raise ValueError(f"Unknown Skill randomization mode: {skill_mode!r}")
    return excluded


# Pool entries carry their source lane, so repeated identities can be removed
# without touching identical rewards belonging to a different category.  The
# Boss, Quest and RelicTurnIn checks use generic filler rather than a native
# item identity. Their lane reductions remove the same number of copies.
ACT_TWO_POOL_REMOVALS_BY_SOURCE_CATEGORY: Mapping[
    str,
    Mapping[str, int],
] = {
    "Map": {
        "Map: The Abyss": 1,
        "Map: Verdania": 1,
    },
    "Relic": {"Relic: Arcane Egg": 1},
    "Tool": {"Progressive Claw Mirror": 1},
    "MaskShard": {
        "Mask Shard #18": 1,
        "Mask Shard #19": 1,
    },
    "Silkeater": {"Silkeater": 1},
    "NeedleUpgrade": {"Progressive Needle Upgrade": 1},
    "PaleOil": {"Pale Oil": 1},
    "Resource:rosary_cache": {"Rosaries (10)": 1},
    "Resource:shell_shard_cache": {"Shell Shards (10)": 9},
    "Boss": {
        "Rosaries (60)": 4,
        "Shell Shards (80)": 3,
    },
    "Quest": {
        "Rosaries (60)": 1,
        "Shell Shards (80)": 1,
    },
    "RelicTurnIn": {"Shell Shards (80)": 1},
    "LoreTablet": {
        LORE_TABLET_ITEM_BY_LOCATION[location_name]: 1
        for location_name in LORE_TABLET_ACT_THREE_LOCATION_NAMES
    },
}


def trim_act_two_pool_entries(
    entries: Iterable,
    starting_crest_item: str,
):
    remaining = list(entries)
    removals = {
        category: Counter(item_counts)
        for category, item_counts in
        ACT_TWO_POOL_REMOVALS_BY_SOURCE_CATEGORY.items()
    }
    if starting_crest_item == "Crest: Shaman":
        removals.setdefault("Crest", Counter())["Rosaries (60)"] += 1
    else:
        removals.setdefault("Crest", Counter())["Crest: Shaman"] += 1
        removals.setdefault("CrestSlot", Counter()).update(
            {
                item_name: 1
                for item_name in ACT_TWO_SHAMAN_SLOT_LOCATION_NAMES
            }
        )

    for source_category, item_counts in removals.items():
        if not any(
            entry.source_category == source_category
            for entry in remaining
        ):
            continue
        for item_name, count in item_counts.items():
            for _copy_index in range(count):
                entry_index = next(
                    (
                        index
                        for index, entry in enumerate(remaining)
                        if (
                            entry.source_category == source_category
                            and entry.name == item_name
                        )
                    ),
                    None,
                )
                if entry_index is None:
                    raise ValueError(
                        "Act 2 content trim could not remove "
                        f"{item_name!r} from the active "
                        f"{source_category!r} pool lane."
                    )
                remaining.pop(entry_index)
    return tuple(remaining)
