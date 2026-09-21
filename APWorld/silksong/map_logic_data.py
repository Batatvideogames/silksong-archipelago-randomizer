from __future__ import annotations

import copy
import hashlib
import json
import pkgutil
from functools import lru_cache


@lru_cache(maxsize=1)
def load_base():
    raw = pkgutil.get_data(__package__, "map_logic.json")
    if raw is None:
        raise ValueError("Bundled map logic is missing.")
    return hashlib.sha256(raw).hexdigest(), json.loads(raw)


def normalize(payload):
    result = dict(payload)
    events = result["logic_events"]
    result["logic_events"] = {event["location"]: event for event in events}
    result["logic_event_order"] = [event["location"] for event in events]
    return result


def difference(base, value):
    changes = {key: None for key in base.keys() - value.keys()}
    for key, item in value.items():
        if key not in base:
            changes[key] = item
        elif item != base[key]:
            changes[key] = (
                difference(base[key], item)
                if isinstance(base[key], dict) and isinstance(item, dict)
                else item
            )
    return changes


def merge(base, changes):
    result = copy.deepcopy(base)
    for key, value in changes.items():
        if value is None:
            result.pop(key, None)
        elif isinstance(value, dict):
            result[key] = merge(result.get(key, {}), value)
        else:
            result[key] = copy.deepcopy(value)
    return result


def prepare_base(data, slot_data):
    result = copy.deepcopy(data["logic"])
    if slot_data.get("entrance_randomization", "off") == "coupled":
        profile = data["entrance_profiles"][slot_data.get("entrance_randomization_scope", "full")]
        result = merge(result, profile["changes"])
        pairs = slot_data["entrance_pairs"]
        for source in profile["sources"]:
            destination = profile["destinations"][pairs.get(source["id"], source["vanilla"])]
            alternatives = result["abstract_requirements"].setdefault(destination, {"alternatives": []})["alternatives"]
            for requirement in source["requirements"]:
                if requirement not in alternatives:
                    alternatives.append(copy.deepcopy(requirement))
    if not slot_data.get("scuttlebrace_logic", False):
        result["abstract_requirements"].pop("Usable Scuttlebrace", None)
        groups = list(result["requirements"].values()) + list(result["abstract_requirements"].values())
        groups += [event["requirement"] for event in result["logic_events"].values()]
        for group in groups:
            alternatives = []
            for requirement in group["alternatives"]:
                if "Usable Scuttlebrace" in requirement["all_of"]:
                    continue
                any_of = requirement["any_of"]
                filtered = [name for name in any_of if name != "Usable Scuttlebrace"]
                if any_of and not filtered:
                    continue
                requirement["any_of"] = filtered
                alternatives.append(requirement)
            group["alternatives"] = alternatives
    return result


def encode(payload, slot_data):
    identity, data = load_base()
    base = prepare_base(data, slot_data)
    return {"logic_base": identity, "logic_overrides": difference(base, normalize(payload))}


def restore(slot_data):
    identity, data = load_base()
    if slot_data.get("logic_base") != identity:
        raise ValueError("Map logic does not match this APWorld. Use the matching APWorld and mod.")
    base = prepare_base(data, slot_data)
    changes = slot_data.get("logic_overrides")
    if not isinstance(changes, dict) or changes.keys() - base.keys():
        raise ValueError("Invalid map logic overrides.")
    result = merge(base, changes)
    order = result.pop("logic_event_order")
    result["logic_events"] = [result["logic_events"][name] for name in order]
    return result
