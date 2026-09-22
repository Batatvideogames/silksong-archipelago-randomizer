from __future__ import annotations

import json
import pkgutil
from functools import lru_cache
from dataclasses import replace

from BaseClasses import CollectionState, EntranceType
from Options import OptionError
from entrance_rando import EntranceRandomizationError, disconnect_entrance_for_randomization, randomize_entrances

from .room_graph import load_room_graph
from .room_graph_logic import compile_room_graph, compile_transition_requirements, room_node_name, CompiledRoomClause, _append_requirements

OPPOSITE_GROUP = {'left': 'right', 'right': 'left', 'top': 'bot', 'bot': 'top',
                  'door_in': 'door_out', 'door_out': 'door_in'}

POOL = {entry['id']: entry for entry in json.loads(pkgutil.get_data(__package__, 'entrance_pool.json'))}


def enabled(world):
    return bool(world.options.entrance_randomization)


def scope(world):
    option = getattr(world.options, 'entrance_randomization_scope', None)
    return option.current_key if option is not None else 'full'


def area(source):
    return source.split('/', 1)[0]


@lru_cache(maxsize=3)
def scoped_pool(scope_name):
    if scope_name not in {'full', 'interiors', 'within_areas'}:
        raise OptionError('Unknown entrance randomization scope.')
    return {
        source: data for source, data in POOL.items()
        if (scope_name == 'full'
            or scope_name == 'interiors' and data['group'] in {'door_in', 'door_out'}
            or scope_name == 'within_areas' and area(source) == area(data['vanilla']))
    }


def validate_pairs(pairs, scope_name='full'):
    if not isinstance(pairs, dict) or not pairs or not set(pairs) <= set(scoped_pool(scope_name)):
        raise OptionError('Entrance beta slot data does not match the verified transition pool.')
    for source, target in pairs.items():
        if (POOL[source]['vanilla'] not in pairs
                or not isinstance(target, str) or target not in pairs or pairs.get(target) != source
                or OPPOSITE_GROUP.get(POOL[source]['group']) != POOL[target]['group']
                or scope_name == 'within_areas' and area(source) != area(target)):
            raise OptionError('Entrance beta requires reciprocal, direction-compatible pairs.')
    return dict(pairs)


def members(data):
    return tuple(data.get('members', (data['id'],)))


def endpoint_name(data, ports):
    if data.get('arrival_requires'):
        return f"Entrance landing: {data['name']}"
    return room_node_name(ports[data['id']].source_node_id)


def exit_clauses(data, ports, include_source=True):
    if not data.get('arrival_requires'):
        return compile_transition_requirements(ports[data['id']], include_source=include_source)
    clauses = (compile_transition_requirements(ports[data['return_requires']], include_source=False)
               if data.get('return_requires') else (CompiledRoomClause(),))
    if include_source:
        return tuple(replace(clause, all_of=(endpoint_name(data, ports), *clause.all_of)) for clause in clauses)
    return clauses


def _validate_pool(ports):
    gates = {}
    claimed = set()
    for source, data in POOL.items():
        partner = POOL.get(data['vanilla'])
        port = ports.get(source)
        group_members = members(data)
        if (port is None or partner is None or partner['vanilla'] != source
                or OPPOSITE_GROUP.get(data['group']) != partner['group']
                or source not in group_members or len(set(group_members)) != len(group_members)
                or claimed.intersection(group_members)):
            raise OptionError(f'Entrance beta audit is out of date: {source}')
        claimed.update(group_members)
        expected = compile_transition_requirements(port, include_source=False)
        for member in group_members:
            candidate = ports.get(member)
            if (candidate is None or not candidate.is_compilable or not candidate.requirement.dnf
                    or candidate.source_node_id != port.source_node_id
                    or candidate.target.port_id not in members(partner)
                    or compile_transition_requirements(candidate, include_source=False) != expected):
                raise OptionError(f'Entrance beta group has incompatible logic: {source}')
        arrival = data.get('arrival_requires')
        if arrival and (arrival not in group_members or not ports[arrival].requirement.dnf):
            raise OptionError(f'Entrance beta has an invalid arrival requirement: {source}')
        returning = data.get('return_requires')
        if returning and (not arrival or returning not in group_members):
            raise OptionError(f'Entrance beta has an invalid return requirement: {source}')
        rows = ({'port': source, **data}, *data.get('aliases', ()))
        if {row['port'] for row in rows} != set(group_members):
            raise OptionError(f'Entrance beta group is missing a native gate: {source}')
        for row in rows:
            key = (data['scene'], row['gate'])
            if key in gates:
                raise OptionError(f'Entrance beta has duplicate native gates: {source}')
            gates[key] = (source, row)
    for source, row in gates.values():
        target = gates.get((row['target_scene'], row['target_gate']))
        if target is None or target[0] != POOL[source]['vanilla']:
            raise OptionError(f'Entrance beta native destination is inconsistent: {source}')


@lru_cache(maxsize=8)
def _node_overrides(pairs, scope_name='full'):
    from .requirements import _compiled_room_clause_requirement

    graph = load_room_graph()
    ports = graph.transition_by_id
    _validate_pool(ports)
    selected = scoped_pool(scope_name)
    removed = {member: None for data in selected.values() for member in members(data)}
    compiled = compile_room_graph(graph, transition_targets=removed)
    affected = {room_node_name(ports[member].source_node_id) for member in removed}
    clauses = {name: list(compiled.node_requirements[name]) for name in affected}
    for data in selected.values():
        if data.get('arrival_requires'):
            landing = endpoint_name(data, ports)
            clauses[landing] = list(compile_transition_requirements(ports[data['id']]))
            interior = room_node_name(ports[data['id']].source_node_id)
            clauses[interior].extend(
                replace(clause, all_of=(landing, *clause.all_of))
                for clause in compile_transition_requirements(ports[data['arrival_requires']], include_source=False)
            )
    if pairs:
        destinations = dict(pairs)
        for source, data in selected.items():
            target = POOL[destinations.get(source, data['vanilla'])]
            requirements = exit_clauses(data, ports)
            _append_requirements(clauses, endpoint_name(target, ports), requirements)
    return {
        name: tuple(dict.fromkeys(_compiled_room_clause_requirement(clause) for clause in alternatives))
        for name, alternatives in clauses.items()
    }


def node_overrides(world, connected=False):
    if not enabled(world):
        return None
    pairs = getattr(world, '_entrance_pairs', None) if connected else None
    if connected and pairs is None:
        raise OptionError('Entrance layout has not been generated.')
    return _node_overrides(tuple(sorted(pairs.items())) if pairs else (), scope(world))


def create_exits(world, regions):
    if not enabled(world):
        return
    from .native_regions import native_rule_options
    from .requirement_rules import build_requirements_rule
    from .requirements import _compiled_room_clause_requirement

    ports = load_room_graph().transition_by_id
    options = native_rule_options(world)
    world._entrance_exits = {}
    for source, data in scoped_pool(scope(world)).items():
        clauses = exit_clauses(data, ports, include_source=False)
        requirements = tuple(_compiled_room_clause_requirement(clause) for clause in clauses)
        entrance = world.create_entrance(
            regions[endpoint_name(data, ports)],
            regions[endpoint_name(POOL[data['vanilla']], ports)],
            build_requirements_rule(requirements, **options),
            f"Room exit: {data['name']}", force_creation=True,
        )
        entrance.randomization_type = EntranceType.TWO_WAY
        entrance.randomization_group = (area(source), data['group']) if scope(world) == 'within_areas' else data['group']
        world._entrance_exits[source] = entrance


def _disconnect(entrance):
    if entrance.connected_region is not None:
        entrance.connected_region.entrances.remove(entrance)
        entrance.connected_region = None


def _has_early_item_space(world):
    state = CollectionState(world.multiworld)
    state.sweep_for_advancements(locations=(
        location for location in world.multiworld.get_filled_locations() if location.address is None
    ))
    pending = dict(world.multiworld.local_early_items[world.player])
    items = []
    for item in world.multiworld.itempool:
        if item.player == world.player and pending.get(item.name, 0) > 0:
            items.append(item)
            pending[item.name] -= 1
    locations = [location for location in world.get_locations()
                 if location.item is None and location.can_reach(state)]
    assigned = {}

    def place(index, visited):
        for location in locations:
            if location in visited or not location.can_fill(state, items[index], check_access=False):
                continue
            visited.add(location)
            if location not in assigned or place(assigned[location], visited):
                assigned[location] = index
                return True
        return False

    if not all(place(index, set()) for index in range(len(items))):
        return False
    for item in items:
        state.collect(item, True)
    state.sweep_for_advancements()
    return (world.multiworld.completion_condition[world.player](state)
            or any(location.item is None and location not in assigned and location.can_reach(state)
                   for location in world.get_locations()))


def _has_possible_category_progression(world):
    from collections import defaultdict
    from .category_fill import _placement_category

    multiworld = world.multiworld
    state = CollectionState(multiworld)
    pending = defaultdict(list)
    for item in multiworld.itempool:
        category = _placement_category(item)
        if item.player == world.player and category:
            pending[category].append(item)
        else:
            state.collect(item, True)
    for location in multiworld.get_filled_locations():
        if location.item.code is not None and (
                location.player != world.player or location.item.player != world.player):
            state.collect(location.item, True)
    locations = defaultdict(list)
    for location in world.get_locations():
        category = _placement_category(location)
        if location.item is None and category:
            locations[category].append(location)
    changed = True
    while changed:
        state.sweep_for_advancements()
        changed = False
        for category, items in tuple(pending.items()):
            if any(location.can_reach(state) and any(
                    location.can_fill(state, item, check_access=False) for item in items)
                    for location in locations[category]):
                for item in items:
                    state.collect(item, True)
                del pending[category]
                changed = True
    return (multiworld.has_beaten_game(state, world.player)
            and _has_possible_melody_progression(world))


def _has_possible_melody_progression(world):
    from .category_fill import _placement_category

    multiworld = world.multiworld
    items = [item for item in multiworld.itempool
             if item.player == world.player and _placement_category(item) == 'Melody']
    if not items:
        return True
    locations = [location for location in world.get_locations()
                 if location.item is None and _placement_category(location) == 'Melody']
    state = CollectionState(multiworld)
    for item in multiworld.itempool:
        if item not in items:
            state.collect(item, True)
    for location in multiworld.get_filled_locations():
        if location.item.code is not None and (
                location.player != world.player or location.item.player != world.player):
            state.collect(location.item, True)
    pending_states = [(state, tuple(range(len(items))), tuple(range(len(locations))))]
    visited = set()
    while pending_states:
        state, pending, slots = pending_states.pop()
        key = pending, slots
        if key in visited:
            continue
        visited.add(key)
        if len(visited) > 128:
            return True
        state.sweep_for_advancements()
        if multiworld.has_beaten_game(state, world.player):
            return True
        for location_index in slots:
            location = locations[location_index]
            if not location.can_reach(state):
                continue
            for item_index in pending:
                if not location.can_fill(state, items[item_index], check_access=False):
                    continue
                trial = state.copy()
                trial.collect(items[item_index], True)
                pending_states.append((
                    trial,
                    tuple(index for index in pending if index != item_index),
                    tuple(index for index in slots if index != location_index),
                ))
    return False


def _randomize_group(world, exits):
    destinations = {entrance: entrance.connected_region for entrance in exits.values()}
    incoming = {entrance.parent_region: list(entrance.parent_region.entrances) for entrance in exits.values()}
    groups = {entrance.randomization_group: [(area(source), OPPOSITE_GROUP[POOL[source]['group']])]
              for source, entrance in exits.items()} if scope(world) == 'within_areas' else {
                  group: [opposite] for group, opposite in OPPOSITE_GROUP.items()}
    for attempt in range(10):
        for entrance in exits.values():
            disconnect_entrance_for_randomization(entrance)
        try:
            result = randomize_entrances(world, coupled=True, target_group_lookup=groups, exits=list(exits.values()))
            if not _has_early_item_space(world):
                raise EntranceRandomizationError('Entrance layout has insufficient checks for local early items.')
            if scope(world) != 'within_areas' and not _has_possible_category_progression(world):
                raise EntranceRandomizationError('Entrance layout blocks category progression.')
            return result
        except EntranceRandomizationError as error:
            for region, entrances in incoming.items():
                region.entrances[:] = entrances
            for entrance, destination in destinations.items():
                entrance.connected_region = destination
            world.multiworld.state.stale[world.player] = True
            if attempt == 9:
                raise OptionError('Entrance beta could not find a valid coupled layout after ten attempts. '
                                  'Generate with a different seed.') from error


def _randomize_exits(world, exits):
    if scope(world) != 'within_areas':
        return _randomize_group(world, exits).pairings
    destinations = {entrance: entrance.connected_region for entrance in exits.values()}
    incoming = {entrance.parent_region: list(entrance.parent_region.entrances) for entrance in exits.values()}
    area_names = sorted({area(source) for source in exits})
    for attempt in range(10):
        if attempt:
            world.random.shuffle(area_names)
        pairings = []
        try:
            for area_name in area_names:
                selected = {source: entrance for source, entrance in exits.items() if area(source) == area_name}
                pairings.extend(_randomize_group(world, selected).pairings)
            if not _has_possible_category_progression(world):
                raise OptionError('Entrance layout blocks category progression.')
            return pairings
        except OptionError:
            for region, entrances in incoming.items():
                region.entrances[:] = entrances
            for entrance, destination in destinations.items():
                entrance.connected_region = destination
            world.multiworld.state.stale[world.player] = True
            if attempt == 9:
                raise


def connect_exits(world):
    if not enabled(world):
        return
    all_exits = world._entrance_exits
    passthrough = getattr(world.multiworld, 're_gen_passthrough', {}).get(world.game, {})
    restored = passthrough.get('entrance_pairs')
    deferred = getattr(world.multiworld, 'enforce_deferred_connections', 'off') != 'off'
    if restored is not None:
        pairs = validate_pairs(restored, scope(world))
        exits = {source: all_exits[source] for source in pairs}
        for source, entrance in exits.items():
            _disconnect(entrance)
            if not deferred:
                entrance.connect(exits[pairs[source]].parent_region)
    else:
        all_state = world.multiworld.get_all_state()
        reachable = {source for source, entrance in all_exits.items() if entrance.can_reach(all_state)}
        exits = {source: entrance for source, entrance in all_exits.items()
                 if source in reachable and POOL[source]['vanilla'] in reachable}
        if not exits:
            raise OptionError('No verified entrance pairs are reachable with these settings.')
        result = _randomize_exits(world, exits)
        by_name = {entrance.name: source for source, entrance in exits.items()}
        pairs = validate_pairs({by_name[source]: by_name[target] for source, target in result}, scope(world))
        if deferred:
            for entrance in exits.values():
                _disconnect(entrance)
    world._entrance_exits = exits
    world._entrance_pairs = pairs
    world._entrance_deferred = deferred
    world.multiworld.state.stale[world.player] = True
    if not deferred:
        for source, target in pairs.items():
            world.multiworld.spoiler.set_entrance(POOL[source]['name'], POOL[target]['name'], 'entrance', world.player)


def reconnect_found(world, value):
    if not enabled(world) or not getattr(world, '_entrance_deferred', False) or not isinstance(value, dict):
        return
    for source, target in value.items():
        if not isinstance(source, str) or not isinstance(target, str):
            continue
        if source not in world._entrance_pairs or world._entrance_pairs[source] != target:
            continue
        for start, end in ((source, target), (target, source)):
            entrance = world._entrance_exits[start]
            if entrance.connected_region is None:
                entrance.connect(world._entrance_exits[end].parent_region)
    world.multiworld.state.stale[world.player] = True


def slot_entrances(world):
    if not enabled(world):
        return {'entrance_randomization': 'off', 'entrance_randomization_scope': scope(world)}
    pairs = validate_pairs(world._entrance_pairs, scope(world))
    return {
        'entrance_randomization': 'coupled',
        'entrance_randomization_scope': scope(world),
        'entrance_pairs': pairs,
        'entrance_layout': {
            source: {
                'scene': data['scene'], 'gate': data['gate'],
                'group': data['group'], 'is_door': data['is_door'],
                'vanilla_scene': data['target_scene'], 'vanilla_gate': data['target_gate'],
                'target': pairs[source], 'target_scene': POOL[pairs[source]]['scene'],
                'target_gate': POOL[pairs[source]]['gate'],
                'aliases': [{'gate': alias['gate'], 'vanilla_scene': alias['target_scene'],
                             'vanilla_gate': alias['target_gate']} for alias in data.get('aliases', ())],
            }
            for source, data in POOL.items() if source in pairs
        },
    }
