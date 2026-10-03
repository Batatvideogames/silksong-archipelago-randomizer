from __future__ import annotations

from collections import defaultdict, deque
from dataclasses import dataclass, replace
from heapq import heappop, heappush

from rule_builder.rules import Rule

from .requirements import req
from .silk_economy import MAX_SILK, at, capacity, clean, rule_cost, source_node


class _UnsupportedCondition(Exception):
    pass


class _SupplyState:
    def __init__(self, program):
        self.program = program
        self.levels = [-1] * len(program.node_names)
        self.active = bytearray(len(program.conditions))
        self.pending = set(range(len(program.conditions)))
        self.queue = []
        self.refresh = True
        self.regeneration = 0
        self.capacity = 9


class SilkSupply:

    def __init__(self, world, original):
        from .native_regions import native_rule_options
        from .requirement_rules import build_requirements_rule

        self.player = world.player
        nodes = world._silk_node_rules
        self.node_names = tuple(sorted(nodes))
        self.nodes = {name: i for i, name in enumerate(self.node_names)}
        self.queries = {at(name, amount): (i, amount)
                        for name, i in self.nodes.items() for amount in range(MAX_SILK + 1)}
        self.conditions = []
        condition_ids = {}
        self.item_conditions = defaultdict(set)
        self.region_conditions = defaultdict(set)
        self.actions = defaultdict(list)
        self.routes = [[] for _ in nodes]
        self.incoming = [[] for _ in nodes]
        self.source_conditions = [[] for _ in nodes]
        self.refill_conditions = {}
        self.refills = set()
        self._dependencies = {}
        options = native_rule_options(world)

        def resolve(rules):
            return build_requirements_rule(
                tuple(rules), extra_abstract_requirement_names=world._silksong_native_abstract_names,
                **options,
            ).resolve(world)

        def condition(rules):
            resolved = resolve(rules)
            if resolved.location_dependencies() or resolved.entrance_dependencies():
                raise _UnsupportedCondition()
            if resolved.always_false:
                return None
            if resolved not in condition_ids:
                index = len(self.conditions)
                condition_ids[resolved] = index
                self.conditions.append(resolved)
                for name in resolved.item_dependencies():
                    self.item_conditions[name].add(index)
                for name in resolved.region_dependencies():
                    self.region_conditions[name].add(index)
            return condition_ids[resolved]

        for node, index in self.nodes.items():
            refill = 'Silk refill: ' + node.removeprefix('Room Node: ')
            if original[refill]:
                self.refills.add(refill)
                predicate = condition((req(refill, crest=False),))
                if predicate is not None:
                    self.refill_conditions[index] = predicate
                    self.source_conditions[index].append(predicate)
                    self.actions[predicate].append(('refill', index))
            incoming = defaultdict(list)
            for rule in nodes[node]:
                source = source_node(rule)
                if source is None:
                    predicate = condition((clean(rule),))
                    if predicate is not None:
                        self.source_conditions[index].append(predicate)
                        self.actions[predicate].append(('seed', index))
                elif source in self.nodes:
                    incoming[source, rule_cost(rule)].append(replace(
                        clean(rule), all_of=tuple(n for n in clean(rule).all_of if n != source)))
            for (source, cost), rules in incoming.items():
                if cost > MAX_SILK:
                    continue
                predicate = condition(rules)
                if predicate is None:
                    continue
                edge = (self.nodes[source], index, cost, predicate)
                self.routes[edge[0]].append(edge)
                self.incoming[index].append(edge)
                self.actions[predicate].append(('route', edge))
        self.capacity_rules = [resolve((capacity(amount),)) for amount in range(10, MAX_SILK + 1)]
        self.capacity_items = {name for rule in self.capacity_rules for name in rule.item_dependencies()}
        self._compile_dependencies()

    def _compile_dependencies(self):
        dependencies = [(frozenset(rule.item_dependencies()), frozenset(rule.region_dependencies()))
                        for rule in self.conditions]
        atoms = sorted({('item', name) for items, regions in dependencies for name in items}
                       | {('region', name) for items, regions in dependencies for name in regions})
        indices = {atom: i for i, atom in enumerate(atoms)}
        conditions = [sum(1 << indices['item', name] for name in items)
                      | sum(1 << indices['region', name] for name in regions)
                      for items, regions in dependencies]
        bits = []
        for i in range(len(self.node_names)):
            value = 0
            for predicate in self.source_conditions[i]:
                value |= conditions[predicate]
            for source, target, cost, predicate in self.incoming[i]:
                value |= conditions[predicate]
            bits.append(value)
        pending = deque(range(len(bits)))
        queued = set(pending)
        while pending:
            source = pending.popleft()
            queued.remove(source)
            for _, target, cost, predicate in self.routes[source]:
                value = bits[target] | bits[source]
                if value != bits[target]:
                    bits[target] = value
                    if target not in queued:
                        queued.add(target)
                        pending.append(target)
        self.dependency_bits = bits
        self.dependency_atoms = atoms

    def dependencies(self, node):
        if node not in self._dependencies:
            items = self.capacity_items | {'Progressive Silkheart'}
            regions = set()
            bits = self.dependency_bits[node]
            while bits:
                flag = bits & -bits
                bits -= flag
                kind, name = self.dependency_atoms[flag.bit_length() - 1]
                (items if kind == 'item' else regions).add(name)
            self._dependencies[node] = frozenset(items), frozenset(regions)
        return self._dependencies[node]

    def _cached(self, state):
        caches = getattr(state, '_silksong_silk_supply', None)
        cached = caches.get(self.player) if caches is not None else None
        return cached if cached is not None and cached.program is self else None

    def collect(self, state, name):
        affected = self.item_conditions.get(name, ())
        refresh = name in self.capacity_items or name == 'Progressive Silkheart'
        if not affected and not refresh:
            return
        cached = self._cached(state)
        if cached is not None:
            cached.pending.update(p for p in affected if not cached.active[p])
            cached.refresh |= refresh

    def reached(self, state, region):
        affected = self.region_conditions.get(region.name)
        if not affected:
            return
        cached = self._cached(state)
        if cached is not None:
            cached.pending.update(p for p in affected if not cached.active[p])

    def evaluate(self, state, node, amount):
        if state.stale[self.player]:
            state.update_reachable_regions(self.player)
        cached = self._cached(state)
        if cached is None:
            cached = _SupplyState(self)
            if not hasattr(state, '_silksong_silk_supply'):
                state._silksong_silk_supply = {}
            state._silksong_silk_supply[self.player] = cached
        levels, active, pending, queue = cached.levels, cached.active, cached.pending, cached.queue
        if levels[node] >= amount:
            return True
        if not pending and not cached.refresh and not queue:
            return False

        def offer(index, value):
            if value >= 0:
                value = max(value, cached.regeneration)
                refill = self.refill_conditions.get(index)
                if refill is not None and active[refill]:
                    value = max(value, cached.capacity)
                if value > levels[index]:
                    levels[index] = value
                    heappush(queue, (-value, index))

        if cached.refresh:
            cached.regeneration = min(3, state.count('Progressive Silkheart', self.player))
            cached.capacity = 9
            for capacity_rule in self.capacity_rules:
                if not capacity_rule(state):
                    break
                cached.capacity += 1
            cached.refresh = False
            for index, value in enumerate(levels):
                if value >= 0:
                    offer(index, value)
        for predicate in tuple(pending):
            pending.remove(predicate)
            if active[predicate] or not self.conditions[predicate](state):
                continue
            active[predicate] = 1
            for kind, argument in self.actions[predicate]:
                if kind == 'seed':
                    offer(argument, 0)
                elif kind == 'refill':
                    if levels[argument] >= 0:
                        offer(argument, cached.capacity)
                else:
                    source, target, cost, _ = argument
                    offer(target, levels[source] - cost)
        while queue:
            negative, source = heappop(queue)
            value = -negative
            if value != levels[source]:
                continue
            for _, target, cost, predicate in self.routes[source]:
                if active[predicate]:
                    offer(target, value - cost)
        return levels[node] >= amount


@dataclass
class SilkSupplyRule(Rule, game='Hollow Knight: Silksong'):
    name: str

    def _instantiate(self, world):
        program = world._silk_supply
        node, amount = program.queries[self.name]
        return self.Resolved(node, amount, program, player=world.player)

    class Resolved(Rule.Resolved):
        node: int
        amount: int
        program: object
        force_recalculate = True

        def _evaluate(self, state):
            return self.program.evaluate(state, self.node, self.amount)

        def item_dependencies(self):
            return {name: {id(self)} for name in self.program.dependencies(self.node)[0]}

        def region_dependencies(self):
            return {name: {id(self)} for name in self.program.dependencies(self.node)[1]}

        def explain_str(self, state=None):
            room = self.program.node_names[self.node].removeprefix('Room Node: ')
            return f'Reach {room}' + (f' with at least {self.amount} silk' if self.amount else '')

        def explain_json(self, state=None):
            color = 'yellow' if state is None else 'green' if self(state) else 'salmon'
            return [{'type': 'color', 'color': color, 'text': self.explain_str(state)}]

        def __str__(self):
            return self.explain_str()


def compile_regions(world, original):
    from .entrance_randomization import enabled as entrances_enabled
    from .native_graph import compact_requirements
    from .silk_economy import enabled

    if not enabled(world) or entrances_enabled(world):
        return compact_requirements(world, original)
    try:
        supply = SilkSupply(world, original)
    except _UnsupportedCondition:
        return compact_requirements(world, original)
    graph = {name: rules for name, rules in original.items() if name not in supply.queries}
    result = compact_requirements(world, graph, additional_roots=supply.refills)
    roots = set(supply.nodes)
    for rules in (*result.values(), *world._progression_location_rules.values(),
                  *(original[name] for name in world._progression_events)):
        for rule in rules:
            roots.update(name for name in (*rule.all_of, *rule.any_of) if name.startswith('Silk ('))
    result.update((name, ()) for name in roots)
    supply.boundaries = frozenset(roots)
    world._enable_silk_supply(supply)
    world.explicit_indirect_conditions = False
    return result
