from __future__ import annotations

from random import Random
from typing import Callable, Protocol, TypeVar


SPELLING_BEE_GOAL_KEY = "spelling_bee"
ALPHABET_ITEM_CATEGORY = "Alphabet"
ALPHABET_ITEM_NAMES: tuple[str, ...] = tuple(
    f"Letter: {letter}"
    for letter in "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
)
ALPHABET_ITEM_ROWS: tuple[tuple[str, str], ...] = tuple(
    (item_name, ALPHABET_ITEM_CATEGORY)
    for item_name in ALPHABET_ITEM_NAMES
)
ALPHABET_ITEM_COUNT = len(ALPHABET_ITEM_NAMES)


class PoolEntry(Protocol):
    name: str
    source_category: str
    placement_category: str | None


PoolEntryType = TypeVar("PoolEntryType", bound=PoolEntry)


def replace_filler_with_alphabet_items(
    entries: list[PoolEntryType],
    is_filler: Callable[[str], bool],
    randomizer: Random | None = None,
) -> None:
    eligible_indices = [
        index
        for index, entry in enumerate(entries)
        if is_filler(entry.name)
    ]
    if len(eligible_indices) < ALPHABET_ITEM_COUNT:
        raise ValueError(
            "alphabet_mode needs 26 filler rewards in the configured "
            f"random pool but only {len(eligible_indices)} are available. "
            "Enable more filler-bearing checks such as Boss Sanity, Quest "
            "Sanity, Lore Tablets, Rosary Caches or Shell Shard Caches."
        )
    if randomizer is not None:
        randomizer.shuffle(eligible_indices)
    for entry_index, item_name in zip(
        eligible_indices,
        ALPHABET_ITEM_NAMES,
    ):
        replaced_entry = entries[entry_index]
        entries[entry_index] = type(replaced_entry)(
            item_name,
            replaced_entry.source_category,
            replaced_entry.placement_category,
        )
