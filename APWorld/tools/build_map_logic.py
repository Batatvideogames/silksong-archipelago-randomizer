from __future__ import annotations

import argparse
import importlib.util
import json
import sys
import types
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description="Build the map logic shared by the APWorld and mod.")
    parser.add_argument("--ap-root", required=True, type=Path)
    args = parser.parse_args()
    sys.path.insert(0, str(args.ap_root.resolve()))
    root = Path(__file__).resolve().parents[1] / "silksong"
    package = importlib.util.module_from_spec(importlib.util.spec_from_file_location(
        "_map_logic_build", root / "__init__.py", submodule_search_locations=[str(root)]))
    package.__path__ = [str(root)]
    sys.modules[package.__name__] = package
    rules = importlib.import_module(package.__name__ + ".requirements")
    transport = importlib.import_module(package.__name__ + ".map_logic_data")
    requirements = rules.export_requirements()
    payload = transport.normalize({
        "requirements": requirements,
        "abstract_requirements": rules.export_abstract_requirements(),
        "logic_item_dependencies": rules.export_logic_item_dependencies(),
        "logic_events": rules.export_wish_logic_events(requirements),
    })
    entrances = importlib.import_module(package.__name__ + ".entrance_randomization")
    ports = entrances.load_room_graph().transition_by_id
    profiles = {}
    for scope in ("full", "interiors", "within_areas"):
        selected = entrances.scoped_pool(scope)
        disconnected = dict(payload, abstract_requirements=rules.export_abstract_requirements(
            room_node_overrides=entrances._node_overrides((), scope)))
        sources = []
        for source, data in selected.items():
            clauses = entrances.exit_clauses(data, ports)
            sources.append({
                "id": source,
                "vanilla": data["vanilla"],
                "requirements": [rules._compiled_room_clause_requirement(c).as_slot_data() for c in clauses],
            })
        profiles[scope] = {
            "changes": transport.difference(payload, disconnected),
            "sources": sources,
            "destinations": {source: entrances.endpoint_name(data, ports) for source, data in selected.items()},
        }
    payload = {"logic": payload, "entrance_profiles": profiles}
    target = root / "map_logic.json"
    target.write_text(json.dumps(payload, ensure_ascii=False, sort_keys=True, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("Built shared map logic.")


if __name__ == "__main__":
    main()

