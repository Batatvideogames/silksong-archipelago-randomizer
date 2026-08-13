"""Act 2 goal content exclusions.

The table maps physical sources unavailable before the Act 2 ending to AP
location names. Shared locations remain when at least one source is available
before that ending.
"""

from __future__ import annotations

from collections import Counter
from typing import Iterable, Mapping


ACT_TWO_GOAL_KEY = "act_2"

# Canonical AP locations whose physical sources are unavailable before the
# Act 2 ending. Two source-level exceptions apply:
#
# * Silk Soar's Abyss source leaves, but its item is not listed in the
#   pool-removal table. Randomized Skill modes keep one shuffled copy.
# * Map Purchase: The Cradle remains because Cradle_02 is a valid Act 2 source.
#   Tube_Hub is only the same check's post-Act-3 fallback.
#
# Runtfeast has no separate AP Wish location.  It shares the canonical
# Longclaw check with Act 2 Broodfeast, so that shared location remains.
ACT_TWO_HAND_TESTED_UNAVAILABLE_LOCATION_NAMES: frozenset[str] = frozenset(
    (
        "Silk Soar",
        "Relic: Arcane Egg",
        "Map Pickup: The Abyss",
        "Map Pickup: Verdania",
        "Shell Shard Cache: The Abyss #1",
        "Shell Shard Cache: The Abyss #2",
        "Shell Shard Cache: The Abyss #3",
        "Shell Shard Cache: The Abyss #4",
        "Shell Shard Cache: The Abyss #5",
        "Shell Shard Cache: The Abyss #6",
        "Shell Shard Cache: The Abyss #7",
        "Shell Shard Cache: The Abyss #8",
        "Shell Shard Cache: The Abyss #9",
        "Rosary Cache: Far Fields #19",
        "Boss: Gurr the Outcast",
        "Boss: Clover Dancers",
        "Boss: Palestag",
        "Mask Shard: The Hidden Hunter",
        "Mask Shard: Dark Hearts",
        "Silkeater: The Cradle",
        "Wish: Passing of the Age",
        "Wish: Advanced Alchemy",
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


def get_act_two_excluded_location_names(
    _starting_crest_item: str,
    skill_mode: str = "anywhere",
) -> frozenset[str]:
    """Return goal exclusions for the configured Skill source behavior.

    Vanilla Skill keeps Silk Soar as an addressless locked native source. Its
    existing post-goal rule lets maximum logic acquire the item without
    inventing a starting grant. Randomized Skill modes omit that physical
    source and keep the item in their random pool instead.
    """

    if skill_mode == "vanilla":
        return (
            ACT_TWO_EXCLUDED_LOCATION_NAMES
            - ACT_TWO_VANILLA_SKILL_RETAINED_LOCATION_NAMES
        )
    if skill_mode not in {"shuffle", "anywhere"}:
        raise ValueError(f"Unknown Skill randomization mode: {skill_mode!r}")
    return ACT_TWO_EXCLUDED_LOCATION_NAMES


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
    "MaskShard": {
        "Mask Shard #18": 1,
        "Mask Shard #19": 1,
    },
    "Silkeater": {"Silkeater": 1},
    "Resource:rosary_cache": {"Rosaries (10)": 1},
    "Resource:shell_shard_cache": {"Shell Shards (10)": 9},
    "Boss": {
        "Rosaries (60)": 2,
        "Shell Shards (80)": 1,
    },
    "Quest": {
        "Rosaries (60)": 1,
        "Shell Shards (80)": 1,
    },
    "RelicTurnIn": {"Shell Shards (80)": 1},
}


def trim_act_two_pool_entries(
    entries: Iterable,
    _starting_crest_item: str,
):
    remaining = list(entries)
    removals = {
        category: Counter(item_counts)
        for category, item_counts in
        ACT_TWO_POOL_REMOVALS_BY_SOURCE_CATEGORY.items()
    }

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
