from collections import defaultdict
from dataclasses import replace

from .requirements import req


def _components(graph, mutable=frozenset()):
    edges = {name: set() for name in graph}
    reverse = {name: set() for name in graph}
    for owner, rules in graph.items():
        for all_of, any_of, predicate in rules:
            if owner in mutable:
                continue
            if len(all_of) == 1 and not any_of and predicate == 0:
                source = next(iter(all_of))
                if source in graph and source not in mutable:
                    edges[source].add(owner)
                    reverse[owner].add(source)
    visited = set()
    order = []
    for name in graph:
        if name in visited:
            continue
        visited.add(name)
        stack = [(name, iter(edges[name]))]
        while stack:
            node, children = stack[-1]
            child = next(children, None)
            if child is None:
                order.append(node)
                stack.pop()
            elif child not in visited:
                visited.add(child)
                stack.append((child, iter(edges[child])))
    visited.clear()
    for name in reversed(order):
        if name in visited:
            continue
        group = set()
        pending = [name]
        while pending:
            node = pending.pop()
            if node in visited:
                continue
            visited.add(node)
            group.add(node)
            pending.extend(reverse[node] - visited)
        if len(group) > 1:
            yield group


def _simplify(graph, mutable=frozenset()):
    aliases = {name: name for name in graph}
    empty = (frozenset(), frozenset(), 0)
    while True:
        changed = False
        true = {n for n, rules in graph.items() if n not in mutable and empty in rules}
        false = {n for n, rules in graph.items() if n not in mutable and not rules}
        for owner, rules in graph.items():
            updated = set()
            for all_of, any_of, predicate in rules:
                all_of = frozenset(aliases.get(n, n) for n in all_of)
                any_of = frozenset(aliases.get(n, n) for n in any_of)
                if owner in all_of or all_of & false:
                    continue
                all_of -= true
                if any_of & true:
                    any_of = frozenset()
                elif any_of:
                    any_of -= false
                    if not any_of:
                        continue
                updated.add((all_of, any_of, predicate))
            if empty in updated:
                updated = {empty}
            else:
                groups = defaultdict(list)
                for all_of, any_of, predicate in updated:
                    groups[any_of, predicate].append(all_of)
                updated = set()
                for (any_of, predicate), group in groups.items():
                    smaller = []
                    for atoms in sorted(group, key=len):
                        if not any(previous <= atoms for previous in smaller):
                            smaller.append(atoms)
                            updated.add((atoms, any_of, predicate))
            changed |= updated != rules
            graph[owner] = updated
        merges = {}
        for group in _components(graph, mutable):
            canonical = min(group)
            merges.update((name, canonical) for name in group if name != canonical)
        signatures = {}
        for name in sorted(graph):
            if name in merges or name in mutable:
                continue
            signature = frozenset(graph[name])
            other = signatures.setdefault(signature, name)
            if other != name:
                merges[name] = other
        if merges:
            def canonical(name):
                while name in merges:
                    name = merges[name]
                return name
            merged = defaultdict(set)
            for name, rules in graph.items():
                merged[canonical(name)].update(rules)
            graph = dict(merged)
            aliases = {name: canonical(target) for name, target in aliases.items()}
            changed = True
        if not changed:
            return graph, aliases


def compact_requirements(world, original, additional_roots=()):
    from .requirement_rules import build_requirements_rule
    from .native_regions import native_rule_options
    from .entrance_randomization import enabled as entrances_enabled

    if entrances_enabled(world):
        return original
    names = world._silksong_native_abstract_names
    options = native_rule_options(world)
    graph = {}
    constants = {}
    predicates = {req(crest=False): 0}
    for owner, rules in original.items():
        retained = set()
        for rule in rules:
            refs = tuple(n for n in rule.all_of if n in names)
            condition = replace(rule, all_of=tuple(n for n in rule.all_of if n not in names))
            if condition not in constants:
                resolved = build_requirements_rule(
                    (condition,), extra_abstract_requirement_names=names, **options,
                ).resolve(world)
                constants[condition] = (resolved.always_true, resolved.always_false)
            always_true, always_false = constants[condition]
            if always_false:
                continue
            if always_true:
                rule = req(*refs, crest=False)
            predicate = replace(rule, all_of=(), any_of=())
            predicate_id = predicates.setdefault(predicate, len(predicates))
            retained.add((frozenset(rule.all_of), frozenset(rule.any_of), predicate_id))
        graph[owner] = retained
    mutable = frozenset(world._progression_events)
    graph, aliases = _simplify(graph, mutable)
    predicates = tuple(predicates)

    def anchor_order(name):
        if name.startswith('Silk ('):
            return 0, name
        if name.startswith(('Room Node: ', 'Entrance landing: ', 'Silk restock ')):
            return 1, name
        return 2, name

    result = {}
    for name, target in aliases.items():
        if name != target:
            result[name] = (req(target, crest=False),)
        else:
            result[name] = tuple(sorted((
                replace(predicates[predicate],
                        all_of=tuple(sorted(all_of, key=anchor_order)),
                        any_of=tuple(sorted(any_of)))
                for all_of, any_of, predicate in graph[name]
            ), key=repr))
    internal_prefixes = ('Silk (', 'Silk route: ', 'Silk capacity: ',
                         'Silk regeneration: ', 'Silk refill: ', 'Silk restock (')
    roots = {name for name in original if not name.startswith(internal_prefixes)}
    roots.update(additional_roots)
    retained_rules = (*world._progression_location_rules.values(),
                      *(original[name] for name in mutable))
    for rules in retained_rules:
        for rule in rules:
            roots.update(n for n in (*rule.all_of, *rule.any_of) if n in result)
    needed = set()
    pending = list(roots)
    while pending:
        name = pending.pop()
        if name in needed:
            continue
        needed.add(name)
        for rule in result[name]:
            pending.extend(n for n in (*rule.all_of, *rule.any_of) if n in result and n not in needed)
    return {name: rules for name, rules in result.items() if name in needed}
