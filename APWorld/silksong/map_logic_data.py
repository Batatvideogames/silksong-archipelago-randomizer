from __future__ import annotations

import base64
import gzip
import io
import hashlib
import json
import pkgutil
from functools import lru_cache


REQUIREMENT_DEFAULTS = {
    "all_of": [], "any_of": [], "required_locations": [], "item_counts": [],
    "require_any_crest": False, "require_silk_spear": False, "minimum_skip_tier": 0,
    "act": "", "path": "",
}


def compact_requirement(requirement):
    return {key: value for key, value in requirement.items()
            if key not in REQUIREMENT_DEFAULTS or value != REQUIREMENT_DEFAULTS[key]}


def _requirement_groups(payload, include_silk=True):
    yield from payload["requirements"].values()
    yield from (group for name, group in payload["abstract_requirements"].items()
                if include_silk or not name.startswith("Silk "))
    yield from (event["requirement"] for event in payload["logic_events"].values())


def compact(payload):
    result = _copy_json(payload)
    for group in _requirement_groups(result):
        group["alternatives"] = [compact_requirement(rule) for rule in group["alternatives"]]
    return result


def restore_defaults(payload):
    # Silk layers are already sparse in live slot data.
    for group in _requirement_groups(payload, include_silk=False):
        for rule in group["alternatives"]:
            for key, value in REQUIREMENT_DEFAULTS.items():
                if key not in rule:
                    rule[key] = _copy_json(value)
    return payload


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


def _copy_json(value):
    if isinstance(value, dict):
        return {key: _copy_json(item) for key, item in value.items()}
    if isinstance(value, list):
        return [_copy_json(item) for item in value]
    return value


def _merge_changes(result, changes):
    for key, value in changes.items():
        if value is None:
            result.pop(key, None)
        elif isinstance(value, dict):
            result[key] = _merge_changes(result.get(key, {}), value)
        else:
            result[key] = _copy_json(value)
    return result


def merge(base, changes):
    result = _copy_json(base)
    for key, value in changes.items():
        if value is None:
            result.pop(key, None)
        elif isinstance(value, dict):
            result[key] = merge(result.get(key, {}), value)
        else:
            result[key] = _copy_json(value)
    return result


def prepare_base(data, slot_data):
    result = _copy_json(data["logic"])
    if slot_data.get("entrance_randomization", "off") == "coupled":
        profile = data["entrance_profiles"][slot_data.get("entrance_randomization_scope", "full")]
        result = _merge_changes(result, profile["changes"])
        pairs = slot_data["entrance_pairs"]
        for source in profile["sources"]:
            destination = profile["destinations"][pairs.get(source["id"], source["vanilla"])]
            alternatives = result["abstract_requirements"].setdefault(destination, {"alternatives": []})["alternatives"]
            for requirement in source["requirements"]:
                if requirement not in alternatives:
                    alternatives.append(_copy_json(requirement))
    if not slot_data.get("scuttlebrace_logic", False):
        result["abstract_requirements"].pop("Usable Scuttlebrace", None)
        groups = list(result["requirements"].values()) + list(result["abstract_requirements"].values())
        groups += [event["requirement"] for event in result["logic_events"].values()]
        for group in groups:
            alternatives = []
            for requirement in group["alternatives"]:
                if "Usable Scuttlebrace" in requirement.get("all_of", ()):
                    continue
                any_of = requirement.get("any_of", ())
                filtered = [name for name in any_of if name != "Usable Scuttlebrace"]
                if any_of and not filtered:
                    continue
                if any_of:
                    requirement["any_of"] = filtered
                alternatives.append(requirement)
            group["alternatives"] = alternatives
    return result


def encode(payload, slot_data):
    identity, data = load_base()
    base = (data["logic"]
            if slot_data.get("entrance_randomization", "off") != "coupled"
            and slot_data.get("scuttlebrace_logic", False)
            else prepare_base(data, slot_data))
    changes = difference(base, compact(normalize(payload)))
    if slot_data.get('enemy_soul_silk_logic') == 1:
        raw = json.dumps(changes, separators=(',', ':')).encode()
        changes = {'encoding': 'gzip+base64', 'data': base64.b64encode(gzip.compress(raw, mtime=0)).decode('ascii')}
    return {"logic_base": identity, "logic_overrides": changes}


def restore(slot_data):
    identity, data = load_base()
    if slot_data.get("logic_base") != identity:
        raise ValueError("Map logic does not match this APWorld. Use the matching APWorld and mod.")
    base = prepare_base(data, slot_data)
    changes = slot_data.get("logic_overrides")
    if isinstance(changes, dict) and 'encoding' in changes:
        if set(changes) != {'encoding', 'data'} or changes['encoding'] != 'gzip+base64':
            raise ValueError('Invalid compressed map logic.')
        try:
            with gzip.GzipFile(fileobj=io.BytesIO(base64.b64decode(changes['data'], validate=True))) as stream:
                raw = stream.read(64 * 1024 * 1024 + 1)
            if len(raw) > 64 * 1024 * 1024:
                raise ValueError('Map logic exceeds the size limit.')
            changes = json.loads(raw)
        except (TypeError, OSError, EOFError, ValueError) as ex:
            raise ValueError('Invalid compressed map logic.') from ex
    if not isinstance(changes, dict) or changes.keys() - base.keys():
        raise ValueError("Invalid map logic overrides.")
    result = restore_defaults(_merge_changes(base, changes))
    order = result.pop("logic_event_order")
    result["logic_events"] = [result["logic_events"][name] for name in order]
    return result
