## This file isn't actually expected to be run from the apworld, it's just an example automation script on how to generate the two important json files

import json

defined_groups = [ # copied and converted from https://github.com/Batatvideogames/silksong-archipelago-randomizer/blob/f342dd8a648b3860aaaf06fd8fb832ae75dbcc68/src/SilksongRandomizer/CheckMapMarkerManifest.cs#L682
    [ "Bellhart - Rosary Cache #1", "Bellhart - Rosary Cache #2" ],
    [ "Bone Bottom - Rosary Cache #4", "Bone Bottom - Rosary Cache #5" ],
    [ "Bone Bottom - Rosary Cache #6", "Bone Bottom - Rosary Cache #7" ],
    [ "Bone Bottom - Rosary Cache #8", "Bone Bottom - Rosary Cache #9" ],
    [ "Choral Chambers - Rosary Cache #1", "Choral Chambers - Rosary Cache #2" ],
    [ "Choral Chambers - Rosary Cache #5", "Choral Chambers - Rosary Cache #6" ],
    [ "Choral Chambers - Rosary Cache #8", "Choral Chambers - Rosary Cache #9" ],
    [ "Choral Chambers - Rosary Cache #11", "Choral Chambers - Rosary Cache #12", "Choral Chambers - Rosary Cache #13" ],
    [ "Choral Chambers - Rosary Cache #15", "Choral Chambers - Rosary Cache #16" ],
    [ "Choral Chambers - Rosary Cache #17", "Choral Chambers - Rosary Cache #18", "Choral Chambers - Rosary Cache #19" ],
    [ "Deep Docks - Rosary Cache #1", "Deep Docks - Rosary Cache #2" ],
    [ "Deep Docks - Rosary Cache #5", "Deep Docks - Rosary Cache #6" ],
    [ "Far Fields - Rosary Cache #3", "Far Fields - Rosary Cache #4" ],
    [ "Far Fields - Rosary Cache #5", "Far Fields - Rosary Cache #6" ],
    [ "Far Fields - Rosary Cache #7", "Far Fields - Rosary Cache #8" ],
    [ "Far Fields - Rosary Cache #12", "Far Fields - Rosary Cache #13" ],
    [ "Far Fields - Rosary Cache #14", "Far Fields - Rosary Cache #15" ],
    [ "Far Fields - Rosary Cache #16", "Far Fields - Rosary Cache #17" ],
    [ "Far Fields - Rosary Cache #20", "Far Fields - Rosary Cache #21", "Far Fields - Pale Rosary Necklace" ],
    [ "Far Fields - Shell Shard Cache #2", "Far Fields - Shell Shard Cache #3", "Far Fields - Rosary Cache #18" ],
    [ "Far Fields - Shell Shard Cache #4", "Far Fields - Shell Shard Cache #5" ],
    [ "Greymoor - Rosary Cache #2", "Greymoor - Rosary Cache #3" ],
    [ "Greymoor - Rosary Cache #4", "Greymoor - Rosary Cache #5" ],
    [ "Greymoor - Rosary Cache #7", "Greymoor - Rosary Cache #8" ],
    [ "Greymoor - Rosary Cache #11", "Greymoor - Rosary Cache #12" ],
    [ "Greymoor - Rosary Cache #15", "Greymoor - Rosary Cache #16" ],
    [ "Greymoor - Rosary Cache #23", "Greymoor - Rosary Cache #24", "Greymoor - Rosary Cache #25" ],
    [ "Greymoor - Rosary Cache #29", "Greymoor - Rosary Cache #30" ],
    [ "Greymoor - Rosary Cache #32", "Greymoor - Rosary Cache #33" ],
    [ "High Halls - Rosary Cache #1", "High Halls - Rosary Cache #2" ],
    [ "High Halls - Rosary Cache #3", "High Halls - Rosary Cache #4" ],
    [ "Hunter's March - Rosary Cache #1", "Hunter's March - Rosary Cache #2" ],
    [ "Hunter's March - Rosary Cache #4", "Hunter's March - Rosary Cache #5" ],
    [ "Hunter's March - Rosary Cache #6", "Hunter's March - Rosary Cache #7" ],
    [ "Hunter's March - Rosary Cache #8", "Hunter's March - Rosary Cache #9" ],
    [ "Mosshome - Rosary Cache #1", "Mosshome - Rosary Cache #2" ],
    [ "Mosshome - Rosary Cache #3", "Mosshome - Rosary Cache #4" ],
    [ "Mount Fay - Rosary Cache #1", "Mount Fay - Rosary Cache #2", "Mount Fay - Rosary Cache #3" ],
    [ "Sinner's Road - Rosary Cache #2", "Sinner's Road - Rosary Cache #3" ],
    [ "Sinner's Road - Rosary Cache #5", "Sinner's Road - Rosary Cache #6", "Sinner's Road - Rosary Cache #7" ],
    [ "The Marrow - Rosary Cache #1", "The Marrow - Rosary Cache #2" ],
    [ "The Marrow - Rosary Cache #3", "The Marrow - Rosary Cache #4" ],
    [ "The Marrow - Rosary Cache #14", "The Marrow - Rosary Cache #15" ],
    [ "The Slab - Rosary Cache #2", "The Slab - Rosary Cache #3" ],
    [ "Underworks - Rosary Cache #2", "Underworks - Rosary Cache #3" ],
    [ "Whispering Vaults - Rosary Cache #2", "Whispering Vaults - Rosary Cache #3" ],
    [ "Whispering Vaults - Rosary Cache #6", "Whispering Vaults - Rosary Cache #7" ],
    [ "Whiteward - Rosary Cache #1", "Whiteward - Rosary Cache #2" ],
    [ "Blasted Steps - Shell Shard Cache #2", "Blasted Steps - Shell Shard Cache #3" ],
    [ "Deep Docks - Shell Shard Cache #1", "Deep Docks - Shell Shard Cache #2" ],
    [ "Deep Docks - Shell Shard Cache #6", "Deep Docks - Shell Shard Cache #7" ],
    [ "Greymoor - Shell Shard Cache #1", "Greymoor - Shell Shard Cache #2" ],
    [ "Greymoor - Shell Shard Cache #4", "Greymoor - Shell Shard Cache #5" ],
    [ "Hunter's March - Shell Shard Cache #3", "Hunter's March - Shell Shard Cache #4" ],
    [ "Mount Fay - Shell Shard Cache #1", "Mount Fay - Shell Shard Cache #2" ],
    [ "Mount Fay - Shell Shard Cache #3", "Mount Fay - Shell Shard Cache #4" ],
    [ "Moss Grotto - Shell Shard Cache #3", "Moss Grotto - Shell Shard Cache #4" ],
    [ "Moss Grotto - Shell Shard Cache #5", "Moss Grotto - Shell Shard Cache #6", "Moss Grotto - Shell Shard Cache #7" ],
    [ "Putrified Ducts - Shell Shard Cache #4", "Putrified Ducts - Shell Shard Cache #5" ],
    [ "Putrified Ducts - Shell Shard Cache #9", "Putrified Ducts - Shell Shard Cache #10" ],
    [ "Sands of Karak - Shell Shard Cache #5", "Sands of Karak - Shell Shard Cache #6" ],
    [ "Shellwood - Shell Shard Cache #1", "Shellwood - Shell Shard Cache #2" ],
    [ "Shellwood - Shell Shard Cache #4", "Shellwood - Shell Shard Cache #5", "Shellwood - Shell Shard Cache #6" ],
    [ "Sinner's Road - Shell Shard Cache #4", "Sinner's Road - Shell Shard Cache #5" ],
    [ "Sinner's Road - Shell Shard Cache #6", "Sinner's Road - Shell Shard Cache #7" ],
    [ "The Marrow - Shell Shard Cache #2", "The Marrow - Shell Shard Cache #3" ],
    [ "The Marrow - Shell Shard Cache #5", "The Marrow - Shell Shard Cache #6" ],
    [ "The Slab - Shell Shard Cache #6", "The Slab - Shell Shard Cache #7" ],
    [ "Underworks - Shell Shard Cache #7", "Underworks - Shell Shard Cache #8" ],
    [ "Wisp Thicket - Shell Shard Cache #6", "Wisp Thicket - Shell Shard Cache #7" ],
]

room_graph_data = {}

with open("../room_graph_data.json") as rgd:
    room_graph_data = json.load(rgd)

entrance_pool = {}

with open("../entrance_pool.json") as ep:
    entrance_pool = json.load(ep)

room_geometry = {}

with open("room_geometry_data.json") as rgd: # Place in this folder, gotten from https://github.com/zerounit-dev/silksong-rando-ap-logic/blob/main/room_geometry_data.json
    room_geometry = json.load(rgd)

print("It loaded")

IMAGE_SCALING = 6.473361 # Determined by brute force, replace with actual image size when we have it

# Load entrance names first so we can ignore any that aren't real

entrance_name_lookup = {}

for entrance in entrance_pool:
    entrance_name_lookup[entrance["id"]] = entrance["name"]

location_name_lookup = {} # This one is going to be annoying

for room in room_graph_data["rooms"]:
    temp_locations = []
    if "checks" in room:
        temp_locations.extend(room["checks"])
    if "locations" in room:
        temp_locations.extend(room["locations"]) # Because y'all can't decide to be right or wrong
    for location in temp_locations:
        location_name_lookup[location["label"]] = location["canonical_location"]

maps_json = [] # We're going to build it up as an arbitrary list of dicts, then print it into a file
locations_json = []

maps_mapping = {} # we need a map from scene name to map index, and i want it in python so it's easier to import

missing_locations = []
skipped_locations = []
missing_entrnaces = []
skipped_entrances = []

## Assemble maps

for room in room_geometry["rooms"]:
    temp_children = []
    temp_map = {"name":room["name"]}
    room_x = room["scene_width"]
    room_y = room["scene_height"]
    if "locations" in room:
        for location in room["locations"]:
            if "name" not in location or location["name"] not in location_name_lookup:
                if "name" in location: #if they forgot this i cannot help them
                    skipped_locations.append(location["name"])
                continue # If i cannot see it, it cannot hurt me
            if "image_x" not in location or "image_y" not in location:
                missing_locations.append(location_name_lookup[location["name"]])
                continue
            if location["image_x"] is None or location["image_y"] is None:
                missing_locations.append(location_name_lookup[location["name"]])
                continue
            temp_location = {
                "name":location_name_lookup[location["name"]],
                "sections":[{"name":location_name_lookup[location["name"]]}],
                "map_locations":[{"map":room["name"],"x":int(IMAGE_SCALING * room_x * location["image_x"]),"y":int(IMAGE_SCALING * room_y * location["image_y"])}] # Actually in image coords cause they love me
            }
            temp_children.append(temp_location)
    if "transitions" in room:
        for entrance in room["transitions"]:
            if "id" not in entrance or entrance["id"] not in entrance_name_lookup:
                if "id" in entrance:
                    skipped_entrances.append(entrance["id"])
                continue 
            if "image_x" not in entrance or "image_y" not in entrance:
                missing_entrnaces.append(entrance_name_lookup[entrance["id"]])
                continue
            if entrance["image_x"] is None or entrance["image_y"] is None:
                missing_entrnaces.append(entrance_name_lookup[entrance["id"]])
                continue
            temp_entrance = {
                "name":entrance_name_lookup[entrance["id"]],
                "sections":[{"name":entrance_name_lookup[entrance["id"]]}],
                "map_locations":[{"map":room["name"],"x":int(IMAGE_SCALING * room_x * entrance["image_x"]),"y":int(IMAGE_SCALING * room_y * entrance["image_y"])}] # Actually in image coords cause they love me
            }
            temp_children.append(temp_entrance)
    if temp_children:
        temp_map["children"] = temp_children
        locations_json.append(temp_map) # Don't add a map with nothing on it
        maps_json.append({"name":room["name"], "img":f"01-geometry/{room['image_guid']}.webp"})
        maps_mapping[room["in_game_id"]] = len(maps_mapping)


with open("maps.json", "w") as maps:
    json.dump(maps_json,maps,indent=1)

print("Finished maps")
print(len(maps_json))


with open("locations.json", "w") as locs:
    json.dump(locations_json, locs, indent=1)

print("Finished locs")
print(len(locations_json))

with open("errors.json", "w") as errors:
    json.dump({
            "missing_locs":missing_locations,
            "missing_ents":missing_entrnaces,
            "skipped_locs":skipped_locations,
            "skipped_ents":skipped_entrances
        }, errors,indent=1)

with open("mapping.py", "w") as mapping:
    mapping.write("# Generated File\n\n")
    mapping.write("from typing import Any\n\n")
    mapping.write("mapping = {\n")
    for map, id in maps_mapping.items():
        mapping.write(f'\t"{map}":{id},\n')
    mapping.write("}\n\n\n")
    mapping.write("def ut_page_index(data: Any) -> int:\n")
    mapping.write("\tif data in mapping:\n")
    mapping.write("\t\treturn mapping[data]\n")
    mapping.write("\treturn 0\n\n")