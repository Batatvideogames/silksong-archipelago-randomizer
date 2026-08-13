from __future__ import annotations

from collections import Counter


# Native CollectableItemPickup identities. Alternate Flea Festival state
# copies share the same scene, asset and position and therefore represent one
# physical source apiece.
MINOR_PICKUP_SOURCE: tuple[tuple[str, str, str, float, float], ...] = (
    ("Rosary Necklace: Hunter's March", 'ant_04_left', 'Rosary_Set_Medium', 85.02, 26.99),
    ('Frayed Rosary String: Putrified Ducts', 'aqueduct_01', 'Rosary_Set_Frayed', 256.19, 14.67),
    ('Rosary Necklace: Fleatopia', 'aqueduct_05_festival', 'Rosary_Set_Medium', 152.783, 16.2396),
    ('Rosary String: Fleatopia', 'aqueduct_05_festival', 'Rosary_Set_Small', 154.043, 15.9796),
    ('Shard Bundle: Memorium', 'arborium_11', 'Shard Pouch', 211.22, 10.27),
    ('Frayed Rosary String: The Marrow (Flea Caravan Passage)', 'bone_10', 'Rosary_Set_Frayed', 98.9849, 41.3206),
    ('Frayed Rosary String: Deep Docks', 'bone_east_04b', 'Rosary_Set_Frayed', 6.462, 87.1857),
    ('Rosary String: Far Fields', 'bone_east_14', 'Rosary_Set_Small', 13.82, 7.461),
    ('Pale Rosary Necklace: Far Fields', 'bone_east_24', 'Rosary_Set_Huge_White', 248.93, 67.4),
    ('Pristine Core: Cogwork Core', 'cog_07', 'Pristine Core', 0.0, 0.0),
    ('Shard Bundle: Cogwork Core', 'cog_10', 'Shard Pouch', 6.78, 44.67),
    ('Frayed Rosary String: Blasted Steps', 'coral_03', 'Rosary_Set_Frayed', 38.64, 5.53),
    ('Beast Shard: Blasted Steps', 'coral_36', 'Great Shard', 20.0542, 48.5738),
    ('Frayed Rosary String: Wormways', 'crawl_02', 'Rosary_Set_Frayed', 3.14, 142.3655),
    ('Shard Bundle: Deep Docks #1', 'dock_02', 'Shard Pouch', 38.79, 3.5175),
    ('Beast Shard: Deep Docks', 'dock_11', 'Great Shard', 100.63, 6.28),
    ("Frayed Rosary String: Sinner's Road", 'dust_01', 'Rosary_Set_Frayed', 76.43, 2.7178),
    ("Shard Bundle: Sinner's Road", 'dust_06', 'Shard Pouch', 26.11, 96.25),
    ('Shard Bundle: Greymoor #1', 'greymoor_05', 'Shard Pouch', 59.01, 62.74),
    ('Shard Bundle: Greymoor #2', 'greymoor_12', 'Shard Pouch', 61.29, 14.4),
    ('Frayed Rosary String: Greymoor #1', 'greymoor_15', 'Rosary_Set_Frayed', 64.41, 24.21),
    ('Frayed Rosary String: Greymoor #2', 'greymoor_15b', 'Rosary_Set_Frayed', 91.86, 35.26),
    ('Pale Rosary Necklace: High Halls', 'hang_06_bank', 'Rosary_Set_Huge_White', 73.41, 35.3895),
    ('Frayed Rosary String: High Halls', 'hang_16', 'Rosary_Set_Frayed', 68.15, 6.38),
    ('Heavy Rosary Necklace: Whispering Vaults', 'library_02', 'Rosary_Set_Large', 76.437, 48.02),
    ('Heavy Rosary Necklace: Songclave', 'library_09', 'Rosary_Set_Large', 14.08, 36.42),
    ('Shard Bundle: Whispering Vaults', 'library_12', 'Shard Pouch', 126.39, 24.4143),
    ('Frayed Rosary String: Bone Bottom (Silkspear Passage)', 'mosstown_02', 'Rosary_Set_Frayed', 35.32, 40.27),
    ('Shard Bundle: Deep Docks #2', 'room_forge', 'Shard Pouch', 5.08, 37.38),
    ('Frayed Rosary String: Bilewater', 'shadow_02', 'Rosary_Set_Frayed', 49.21, 161.48),
    ('Frayed Rosary String: Shellwood', 'shellwood_01', 'Rosary_Set_Frayed', 48.99, 23.27),
    ('Rosary String: Shellwood #1', 'shellwood_01b', 'Rosary_Set_Small', 33.13, 72.38),
    ('Shard Bundle: Shellwood', 'shellwood_13', 'Shard Pouch', 82.2928, 67.2101),
    ('Rosary String: Shellwood #2', 'shellwood_25', 'Rosary_Set_Small', 83.3, 25.4172),
    ('Frayed Rosary String: The Slab #1', 'slab_02', 'Rosary_Set_Frayed', 53.4572, 21.2107),
    ('Shard Bundle: The Slab', 'slab_04', 'Shard Pouch', 9.4863, 15.6907),
    ('Frayed Rosary String: The Slab #2', 'slab_18', 'Rosary_Set_Frayed', 17.2344, 21.2003),
    ('Frayed Rosary String: The Slab #3', 'slab_22', 'Rosary_Set_Frayed', 28.3, 30.39),
    ('Heavy Rosary Necklace: Choral Chambers', 'song_04', 'Rosary_Set_Large', 121.49, 37.57),
    ('Rosary Necklace: Choral Chambers #1', 'song_07', 'Rosary_Set_Medium', 3.83, 4.91),
    ('Rosary Necklace: Choral Chambers #2', 'song_09', 'Rosary_Set_Medium', 39.38, 8.2749),
    ('Frayed Rosary String: Moss Grotto', 'tut_01', 'Rosary_Set_Frayed', 43.96, 20.43),
    ('Shard Bundle: Underworks #1', 'under_03', 'Shard Pouch', 4.5876, 6.2287),
    ('Frayed Rosary String: Underworks #1', 'under_07c', 'Rosary_Set_Frayed', 75.57, 76.08),
    ('Frayed Rosary String: Underworks #2', 'under_12', 'Rosary_Set_Frayed', 33.13, 10.29),
    ('Pristine Core: Underworks', 'under_17', 'Pristine Core', 75.44, 23.37),
    ('Shard Bundle: Underworks #2', 'under_18', 'Shard Pouch', 34.14, 34.3),
    ('Rosary Necklace: Wisp Thicket', 'wisp_02', 'Rosary_Set_Medium', 111.32, 37.99),
    ('Frayed Rosary String: Greymoor #3', 'wisp_03', 'Rosary_Set_Frayed', 62.89, 35.37),
)

MINOR_PICKUP_REWARD_BY_ASSET: dict[str, str] = {
    'Rosary_Set_Frayed': 'Frayed Rosary String',
    'Rosary_Set_Small': 'Rosary String',
    'Rosary_Set_Medium': 'Rosary Necklace',
    'Rosary_Set_Large': 'Heavy Rosary Necklace',
    'Rosary_Set_Huge_White': 'Pale Rosary Necklace',
    'Shard Pouch': 'Shard Bundle',
    'Great Shard': 'Beast Shard',
    'Pristine Core': 'Pristine Core',
}

MINOR_PICKUP_LOCATION_NAMES: tuple[str, ...] = tuple(
    location_name for location_name, *_identity in MINOR_PICKUP_SOURCE
)
MINOR_PICKUP_REWARD_BY_LOCATION: dict[str, str] = {
    location_name: MINOR_PICKUP_REWARD_BY_ASSET[asset_name]
    for location_name, _scene, asset_name, _x, _y in MINOR_PICKUP_SOURCE
}
MINOR_PICKUP_REWARD_COUNTS: dict[str, int] = dict(
    Counter(MINOR_PICKUP_REWARD_BY_LOCATION.values())
)
