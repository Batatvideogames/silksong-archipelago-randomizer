from __future__ import annotations

import dataclasses
from typing import TYPE_CHECKING, Iterable

from BaseClasses import CollectionState, Item
from NetUtils import JSONMessagePart
from rule_builder.rules import (
    And,
    CanReachLocation,
    CanReachRegion,
    False_,
    Has,
    HasAny,
    HasFromList,
    Or,
    Rule,
    True_,
)

from .display_names import clean_item_display_name
from .items import get_vanilla_reward_name
from .locations import canonicalize_location_name
from .requirements import (
    CREST_ITEMS,
    REQUIREMENTS,
    DEFAULT_FLEA_HUNT_GOAL_COUNT,
    MEMORY_LOCKET_ITEM,
    NON_ARCHITECT_CREST_ITEMS,
    PROGRESSIVE_SWIFT_STEP_ITEM,
    STARTING_LOCATION_VANILLA,
    SWIFT_STEP_ITEM,
    TRAILS_END_REQUIREMENT_SHAKRA_STOCK,
    USABLE_SCUTTLEBRACE_REQUIREMENT,
    ItemCountRequirement,
    LocationRequirement,
    _has_named_requirement,
    get_abstract_requirements,
    get_goal_requirements,
    get_location_requirements,
    get_logic_item_dependencies,
    get_logic_item_references,
    is_logic_unknown_location,
)

if TYPE_CHECKING:
    from worlds.AutoWorld import World


GAME_NAME = "Hollow Knight: Silksong"


@dataclasses.dataclass()
class AbstractRequirementRule(Rule, game=GAME_NAME):
    """Evaluate one named node in Silksong's cyclic route graph.

    The route graph is solved as a least fixed point by requirements.py. Item
    and count gates outside that graph remain ordinary RuleBuilder leaves, so
    explanations show those requirements directly instead of hiding an entire
    location behind one callable.
    """

    requirement_name: str = ""
    split_dash_and_sprint: bool = False
    allow_bellways_before_bell_beast: bool = False
    skips_tier: int = 0
    randomized_crest_slots_enabled: bool = True
    starting_location: str = STARTING_LOCATION_VANILLA
    trails_end_requirement: str = TRAILS_END_REQUIREMENT_SHAKRA_STOCK
    scuttlebrace_logic_enabled: bool = True
    randomize_ledge_grab: bool = False
    randomize_swim: bool = False
    pollip_heart_count: int = 0

    def _instantiate(self, world: World) -> Rule.Resolved:
        return self.Resolved(
            self.requirement_name,
            self.split_dash_and_sprint,
            self.allow_bellways_before_bell_beast,
            self.skips_tier,
            self.randomized_crest_slots_enabled,
            self.starting_location,
            self.trails_end_requirement,
            self.scuttlebrace_logic_enabled,
            self.randomize_ledge_grab,
            self.randomize_swim,
            self.pollip_heart_count,
            player=world.player,
            caching_enabled=getattr(world, "rule_caching_enabled", False),
        )

    class Resolved(Rule.Resolved):
        requirement_name: str
        split_dash_and_sprint: bool
        allow_bellways_before_bell_beast: bool
        skips_tier: int
        randomized_crest_slots_enabled: bool
        starting_location: str
        trails_end_requirement: str
        scuttlebrace_logic_enabled: bool
        randomize_ledge_grab: bool
        randomize_swim: bool
        pollip_heart_count: int

        def _evaluate(self, state: CollectionState) -> bool:
            return _has_named_requirement(
                self.requirement_name,
                state,
                self.player,
                split_dash_and_sprint=self.split_dash_and_sprint,
                allow_bellways_before_bell_beast=(
                    self.allow_bellways_before_bell_beast
                ),
                skips_tier=self.skips_tier,
                randomized_crest_slots_enabled=(
                    self.randomized_crest_slots_enabled
                ),
                starting_location=self.starting_location,
                trails_end_requirement=self.trails_end_requirement,
                scuttlebrace_logic_enabled=(
                    self.scuttlebrace_logic_enabled
                ),
                randomize_ledge_grab=self.randomize_ledge_grab,
                randomize_swim=self.randomize_swim,
                pollip_heart_count=self.pollip_heart_count,
            )

        def item_dependencies(self) -> dict[str, set[int]]:
            return {
                item_name: {id(self)}
                for item_name in get_logic_item_references()
            }

        def explain_json(
            self,
            state: CollectionState | None = None,
        ) -> list[JSONMessagePart]:
            if state is None:
                prefix = "Can satisfy "
                color = "yellow"
            elif self(state):
                prefix = "Satisfied "
                color = "green"
            else:
                prefix = "Cannot satisfy "
                color = "salmon"
            return [
                {"type": "text", "text": prefix},
                {
                    "type": "color",
                    "color": color,
                    "text": self.requirement_name,
                },
            ]

        def explain_str(self, state: CollectionState | None = None) -> str:
            if state is None:
                return str(self)
            prefix = "Satisfied" if self(state) else "Cannot satisfy"
            return f"{prefix} {self.requirement_name}"

        def __str__(self) -> str:
            return f"Can satisfy {self.requirement_name}"


NativeSourceInventoryKey = tuple[
    int,
    tuple[tuple[str, int], ...],
    bool,
]
NativeSourceMemoKey = tuple[int, NativeSourceInventoryKey]


def _enable_native_source_memo(multiworld) -> None:
    setattr(
        multiworld,
        "_silksong_native_source_memo",
        {
            "inventory_keys": {},
            "results": {},
        },
    )


def _lookup_native_source_memo(
    rule: Rule.Resolved,
    state: CollectionState,
) -> tuple[
    dict[NativeSourceMemoKey, bool],
    NativeSourceMemoKey,
    bool | None,
] | None:
    memo = getattr(
        state.multiworld,
        "_silksong_native_source_memo",
        None,
    )
    if memo is None:
        return None

    inventory_key: NativeSourceInventoryKey = (
        rule.player,
        tuple(sorted(state.prog_items[rule.player].items())),
        state.allow_partial_entrances,
    )
    canonical_inventory_key = memo["inventory_keys"].setdefault(
        inventory_key,
        inventory_key,
    )
    key: NativeSourceMemoKey = (id(rule), canonical_inventory_key)
    results = memo["results"]
    return results, key, results.get(key)


@dataclasses.dataclass()
class NativeSourceRule(Rule, game=GAME_NAME):
    """Preserve the vanilla-source self-reward assumption.

    RuleBuilder's built-in item rules read ``prog_items`` directly. A custom
    rule is required here because the temporary native reward intentionally
    exists only while evaluating its own addressless source.
    """

    location_name: str = ""
    reward_name: str = ""
    split_dash_and_sprint: bool = False
    allow_bellways_before_bell_beast: bool = False
    skips_tier: int = 0
    randomized_crest_slots_enabled: bool = True
    starting_location: str = STARTING_LOCATION_VANILLA
    trails_end_requirement: str = TRAILS_END_REQUIREMENT_SHAKRA_STOCK
    scuttlebrace_logic_enabled: bool = True
    pollip_heart_count: int = 0
    anchor_requirement_name: str | None = None
    randomize_ledge_grab: bool = False
    randomize_swim: bool = False

    def _instantiate(self, world: World) -> Rule.Resolved:
        child_rule = build_location_rule(
            self.location_name,
            split_dash_and_sprint=self.split_dash_and_sprint,
            allow_bellways_before_bell_beast=(
                self.allow_bellways_before_bell_beast
            ),
            skips_tier=self.skips_tier,
            randomized_crest_slots_enabled=(
                self.randomized_crest_slots_enabled
            ),
            starting_location=self.starting_location,
            trails_end_requirement=self.trails_end_requirement,
            scuttlebrace_logic_enabled=self.scuttlebrace_logic_enabled,
            pollip_heart_count=self.pollip_heart_count,
            native_abstract_regions=True,
            anchor_requirement_name=self.anchor_requirement_name,
            randomize_ledge_grab=self.randomize_ledge_grab,
            randomize_swim=self.randomize_swim,
        )
        return self.Resolved(
            self.location_name,
            self.reward_name,
            self.anchor_requirement_name,
            child_rule.resolve(world),
            world.create_item(self.reward_name),
            player=world.player,
            caching_enabled=getattr(world, "rule_caching_enabled", False),
        )

    class Resolved(Rule.Resolved):
        location_name: str
        reward_name: str
        anchor_requirement_name: str | None
        child: Rule.Resolved
        assumed_item: Item

        def __post_init__(self) -> None:
            object.__setattr__(
                self,
                "force_recalculate",
                self.force_recalculate or self.child.force_recalculate,
            )
            super().__post_init__()

        def _evaluate(self, state: CollectionState) -> bool:
            memo_entry = _lookup_native_source_memo(self, state)
            if memo_entry is not None:
                results, key, cached_result = memo_entry
                if cached_result is not None:
                    return cached_result

            assumed_state = state.copy()
            assumed_state.collect(self.assumed_item, True)
            if (
                self.anchor_requirement_name is not None
                and not assumed_state.can_reach_region(
                    self.anchor_requirement_name,
                    self.player,
                )
            ):
                result = False
            else:
                result = self.child(assumed_state)
            if memo_entry is not None:
                results[key] = result
            return result

        def item_dependencies(self) -> dict[str, set[int]]:
            return {
                item_name: {id(self)}
                for item_name in get_logic_item_references()
            }

        def region_dependencies(self) -> dict[str, set[int]]:
            region_names = set(self.child.region_dependencies())
            if self.anchor_requirement_name is not None:
                region_names.add(self.anchor_requirement_name)
            return {
                region_name: {id(self)}
                for region_name in region_names
            }

        def explain_json(
            self,
            state: CollectionState | None = None,
        ) -> list[JSONMessagePart]:
            if state is None:
                color = "yellow"
                prefix = "Can reach native source "
            elif self(state):
                color = "green"
                prefix = "Reached native source "
            else:
                color = "salmon"
                prefix = "Cannot reach native source "
            return [
                {"type": "text", "text": prefix},
                {
                    "type": "color",
                    "color": color,
                    "text": self.location_name,
                },
                {
                    "type": "text",
                    "text": f" (assumes its {self.reward_name} reward)",
                },
            ]

        def explain_str(self, state: CollectionState | None = None) -> str:
            if state is None:
                prefix = "Can reach"
            else:
                prefix = "Reached" if self(state) else "Cannot reach"
            return (
                f"{prefix} native source {self.location_name} "
                f"(assumes its {self.reward_name} reward)"
            )

        def __str__(self) -> str:
            return (
                f"Can reach native source {self.location_name} "
                f"(assumes its {self.reward_name} reward)"
            )


@dataclasses.dataclass()
class CrestSlotMemoryLocketRule(Rule, game=GAME_NAME):
    """Require the world's frozen randomized Crest Slot Locket budget."""

    def _instantiate(self, world: World) -> Rule.Resolved:
        return self.Resolved(
            player=world.player,
            caching_enabled=getattr(world, "rule_caching_enabled", False),
        )

    class Resolved(Rule.Resolved):
        force_recalculate = True

        def _required_count(self, state: CollectionState) -> int:
            from .rules import get_crest_slot_memory_locket_count

            world = state.multiworld.worlds[self.player]
            return get_crest_slot_memory_locket_count(world)

        def _evaluate(self, state: CollectionState) -> bool:
            return state.count(
                MEMORY_LOCKET_ITEM,
                self.player,
            ) >= self._required_count(state)

        def item_dependencies(self) -> dict[str, set[int]]:
            return {MEMORY_LOCKET_ITEM: {id(self)}}

        def explain_json(
            self,
            state: CollectionState | None = None,
        ) -> list[JSONMessagePart]:
            if state is None:
                return [
                    {"type": "text", "text": "Has the required "},
                    {
                        "type": "item_name",
                        "flags": 0b001,
                        "text": MEMORY_LOCKET_ITEM,
                        "player": self.player,
                    },
                    {"type": "text", "text": " count for Crest Slots"},
                ]
            required = self._required_count(state)
            found = state.count(MEMORY_LOCKET_ITEM, self.player)
            color = "green" if found >= required else "salmon"
            return [
                {"type": "text", "text": "Has "},
                {
                    "type": "color",
                    "color": color,
                    "text": f"{found}/{required}",
                },
                {"type": "text", "text": f" {MEMORY_LOCKET_ITEM}"},
            ]

        def explain_str(self, state: CollectionState | None = None) -> str:
            if state is None:
                return str(self)
            required = self._required_count(state)
            found = state.count(MEMORY_LOCKET_ITEM, self.player)
            return f"Has {found}/{required} {MEMORY_LOCKET_ITEM}"

        def __str__(self) -> str:
            return f"Has the required {MEMORY_LOCKET_ITEM} count for Crest Slots"


def _and_rules(rules: Iterable[Rule]) -> Rule:
    children = tuple(rules)
    if not children:
        return True_()
    if len(children) == 1:
        return children[0]
    return And(*children)


def _or_rules(rules: Iterable[Rule]) -> Rule:
    children = tuple(rules)
    if not children:
        return False_()
    if len(children) == 1:
        return children[0]
    return Or(*children)


def _compile_item_count(count_requirement: ItemCountRequirement) -> Rule:
    item_names = tuple(
        clean_item_display_name(item_name)
        for item_name in count_requirement.item_names
    )
    if len(item_names) == 1:
        return Has(item_names[0], count_requirement.minimum)
    return HasFromList(*item_names, count=count_requirement.minimum)


def _compile_named_requirement(
    requirement_name: str,
    *,
    abstract_requirement_names: frozenset[str],
    split_dash_and_sprint: bool,
    allow_bellways_before_bell_beast: bool,
    skips_tier: int,
    randomized_crest_slots_enabled: bool,
    starting_location: str,
    trails_end_requirement: str,
    scuttlebrace_logic_enabled: bool,
    randomize_ledge_grab: bool,
    randomize_swim: bool,
    pollip_heart_count: int,
    native_abstract_regions: bool = False,
) -> Rule:
    if (
        not scuttlebrace_logic_enabled
        and requirement_name == USABLE_SCUTTLEBRACE_REQUIREMENT
    ):
        return False_()
    if requirement_name in abstract_requirement_names:
        if native_abstract_regions:
            return CanReachRegion(requirement_name)
        return AbstractRequirementRule(
            requirement_name=requirement_name,
            split_dash_and_sprint=split_dash_and_sprint,
            allow_bellways_before_bell_beast=(
                allow_bellways_before_bell_beast
            ),
            skips_tier=skips_tier,
            randomized_crest_slots_enabled=(
                randomized_crest_slots_enabled
            ),
            starting_location=starting_location,
            trails_end_requirement=trails_end_requirement,
            scuttlebrace_logic_enabled=scuttlebrace_logic_enabled,
            randomize_ledge_grab=randomize_ledge_grab,
            randomize_swim=randomize_swim,
            pollip_heart_count=pollip_heart_count,
        )

    item_name = clean_item_display_name(requirement_name)
    if split_dash_and_sprint and item_name == SWIFT_STEP_ITEM:
        return Has(PROGRESSIVE_SWIFT_STEP_ITEM, 2)

    dependencies = get_logic_item_dependencies(
        split_dash_and_sprint
    ).get(item_name, ())
    item_rules: list[Rule] = [Has(item_name)]
    for dependency_name in sorted(set(dependencies)):
        item_rules.append(
            Has(
                clean_item_display_name(dependency_name),
                dependencies.count(dependency_name),
            )
        )
    return _and_rules(item_rules)


def _compile_requirement(
    requirement: LocationRequirement,
    *,
    abstract_requirement_names: frozenset[str],
    split_dash_and_sprint: bool,
    allow_bellways_before_bell_beast: bool,
    skips_tier: int,
    randomized_crest_slots_enabled: bool,
    starting_location: str,
    trails_end_requirement: str,
    scuttlebrace_logic_enabled: bool,
    randomize_ledge_grab: bool,
    randomize_swim: bool,
    pollip_heart_count: int,
    native_abstract_regions: bool = False,
    anchor_requirement_name: str | None = None,
    required_location_stack: tuple[str, ...] = (),
) -> Rule:
    if requirement.minimum_skip_tier > skips_tier:
        return False_()

    def compile_named(name: str) -> Rule:
        return _compile_named_requirement(
            name,
            abstract_requirement_names=abstract_requirement_names,
            split_dash_and_sprint=split_dash_and_sprint,
            allow_bellways_before_bell_beast=(
                allow_bellways_before_bell_beast
            ),
            skips_tier=skips_tier,
            randomized_crest_slots_enabled=(
                randomized_crest_slots_enabled
            ),
            starting_location=starting_location,
            trails_end_requirement=trails_end_requirement,
            scuttlebrace_logic_enabled=scuttlebrace_logic_enabled,
            randomize_ledge_grab=randomize_ledge_grab,
            randomize_swim=randomize_swim,
            pollip_heart_count=pollip_heart_count,
            native_abstract_regions=native_abstract_regions,
        )

    def compile_required_location(location_name: str) -> Rule:
        if not native_abstract_regions:
            return CanReachLocation(location_name)
        canonical_name = canonicalize_location_name(location_name)
        if is_logic_unknown_location(canonical_name):
            return True_()
        if canonical_name in required_location_stack:
            return False_()
        alternatives = REQUIREMENTS.get(canonical_name)
        if not alternatives:
            return False_()
        next_stack = (*required_location_stack, canonical_name)
        return _or_rules(
            _compile_requirement(
                alternative,
                abstract_requirement_names=abstract_requirement_names,
                split_dash_and_sprint=split_dash_and_sprint,
                allow_bellways_before_bell_beast=(
                    allow_bellways_before_bell_beast
                ),
                skips_tier=skips_tier,
                randomized_crest_slots_enabled=(
                    randomized_crest_slots_enabled
                ),
                starting_location=starting_location,
                trails_end_requirement=trails_end_requirement,
                scuttlebrace_logic_enabled=scuttlebrace_logic_enabled,
                randomize_ledge_grab=randomize_ledge_grab,
                randomize_swim=randomize_swim,
                pollip_heart_count=pollip_heart_count,
                native_abstract_regions=True,
                required_location_stack=next_stack,
            )
            for alternative in alternatives
        )

    if anchor_requirement_name in requirement.all_of:
        requirement = dataclasses.replace(
            requirement,
            all_of=tuple(
                name
                for name in requirement.all_of
                if name != anchor_requirement_name
            ),
        )

    rules: list[Rule] = []
    if requirement.require_any_crest:
        rules.append(HasAny(*sorted(CREST_ITEMS)))
    if requirement.require_silk_spear:
        rules.extend(
            (
                Has("Silkspear"),
                HasAny(*sorted(NON_ARCHITECT_CREST_ITEMS)),
            )
        )
    rules.extend(compile_named(name) for name in requirement.all_of)
    if requirement.any_of:
        rules.append(
            _or_rules(compile_named(name) for name in requirement.any_of)
        )
    rules.extend(
        _compile_item_count(count_requirement)
        for count_requirement in requirement.item_counts
    )
    rules.extend(
        compile_required_location(location_name)
        for location_name in requirement.required_locations
    )
    return _and_rules(rules)


def build_requirements_rule(
    requirements: tuple[LocationRequirement, ...],
    *,
    split_dash_and_sprint: bool = False,
    allow_bellways_before_bell_beast: bool = False,
    skips_tier: int = 0,
    randomized_crest_slots_enabled: bool = True,
    starting_location: str = STARTING_LOCATION_VANILLA,
    trails_end_requirement: str = TRAILS_END_REQUIREMENT_SHAKRA_STOCK,
    scuttlebrace_logic_enabled: bool = True,
    randomize_ledge_grab: bool = False,
    randomize_swim: bool = False,
    pollip_heart_count: int = 0,
    native_abstract_regions: bool = False,
    anchor_requirement_name: str | None = None,
) -> Rule:
    """Compile declarative Silksong requirements into a RuleBuilder tree."""

    abstract_requirement_names = frozenset(
        get_abstract_requirements(
            allow_bellways_before_bell_beast,
            randomized_crest_slots_enabled,
            starting_location,
            trails_end_requirement,
            pollip_heart_count,
            randomize_ledge_grab=randomize_ledge_grab,
            randomize_swim=randomize_swim,
        )
    )
    return _or_rules(
        _compile_requirement(
            requirement,
            abstract_requirement_names=abstract_requirement_names,
            split_dash_and_sprint=split_dash_and_sprint,
            allow_bellways_before_bell_beast=(
                allow_bellways_before_bell_beast
            ),
            skips_tier=skips_tier,
            randomized_crest_slots_enabled=(
                randomized_crest_slots_enabled
            ),
            starting_location=starting_location,
            trails_end_requirement=trails_end_requirement,
            scuttlebrace_logic_enabled=scuttlebrace_logic_enabled,
            randomize_ledge_grab=randomize_ledge_grab,
            randomize_swim=randomize_swim,
            pollip_heart_count=pollip_heart_count,
            native_abstract_regions=native_abstract_regions,
            anchor_requirement_name=anchor_requirement_name,
        )
        for requirement in requirements
    )


def build_location_rule(
    location_name: str,
    *,
    split_dash_and_sprint: bool = False,
    allow_bellways_before_bell_beast: bool = False,
    skips_tier: int = 0,
    randomized_crest_slots_enabled: bool = True,
    starting_location: str = STARTING_LOCATION_VANILLA,
    trails_end_requirement: str = TRAILS_END_REQUIREMENT_SHAKRA_STOCK,
    scuttlebrace_logic_enabled: bool = True,
    randomize_ledge_grab: bool = False,
    randomize_swim: bool = False,
    pollip_heart_count: int = 0,
    native_abstract_regions: bool = False,
    anchor_requirement_name: str | None = None,
) -> Rule:
    if is_logic_unknown_location(location_name):
        return True_()
    return build_requirements_rule(
        get_location_requirements(
            location_name,
            pollip_heart_count=pollip_heart_count,
        ),
        split_dash_and_sprint=split_dash_and_sprint,
        allow_bellways_before_bell_beast=allow_bellways_before_bell_beast,
        skips_tier=skips_tier,
        randomized_crest_slots_enabled=randomized_crest_slots_enabled,
        starting_location=starting_location,
        trails_end_requirement=trails_end_requirement,
        scuttlebrace_logic_enabled=scuttlebrace_logic_enabled,
        randomize_ledge_grab=randomize_ledge_grab,
        randomize_swim=randomize_swim,
        pollip_heart_count=pollip_heart_count,
        native_abstract_regions=native_abstract_regions,
        anchor_requirement_name=anchor_requirement_name,
    )


def build_goal_rule(
    goal_key: str,
    *,
    split_dash_and_sprint: bool = False,
    flea_hunt_count: int = DEFAULT_FLEA_HUNT_GOAL_COUNT,
    pollip_heart_count: int = 0,
    spelling_bee_item_names: tuple[str, ...] | None = None,
    allow_bellways_before_bell_beast: bool = False,
    skips_tier: int = 0,
    randomized_crest_slots_enabled: bool = True,
    starting_location: str = STARTING_LOCATION_VANILLA,
    trails_end_requirement: str = TRAILS_END_REQUIREMENT_SHAKRA_STOCK,
    scuttlebrace_logic_enabled: bool = True,
    randomize_ledge_grab: bool = False,
    randomize_swim: bool = False,
    native_abstract_regions: bool = False,
    anchor_requirement_name: str | None = None,
) -> Rule:
    return build_requirements_rule(
        get_goal_requirements(
            goal_key,
            flea_hunt_count,
            pollip_heart_count,
            spelling_bee_item_names,
        ),
        split_dash_and_sprint=split_dash_and_sprint,
        allow_bellways_before_bell_beast=allow_bellways_before_bell_beast,
        skips_tier=skips_tier,
        randomized_crest_slots_enabled=randomized_crest_slots_enabled,
        starting_location=starting_location,
        trails_end_requirement=trails_end_requirement,
        scuttlebrace_logic_enabled=scuttlebrace_logic_enabled,
        randomize_ledge_grab=randomize_ledge_grab,
        randomize_swim=randomize_swim,
        pollip_heart_count=pollip_heart_count,
        native_abstract_regions=native_abstract_regions,
        anchor_requirement_name=anchor_requirement_name,
    )


def build_native_source_rule(
    location_name: str,
    category: str,
    *,
    split_dash_and_sprint: bool = False,
    allow_bellways_before_bell_beast: bool = False,
    skips_tier: int = 0,
    randomized_crest_slots_enabled: bool = True,
    starting_location: str = STARTING_LOCATION_VANILLA,
    trails_end_requirement: str = TRAILS_END_REQUIREMENT_SHAKRA_STOCK,
    scuttlebrace_logic_enabled: bool = True,
    pollip_heart_count: int = 0,
    anchor_requirement_name: str | None = None,
    randomize_ledge_grab: bool = False,
    randomize_swim: bool = False,
) -> Rule:
    if is_logic_unknown_location(location_name):
        return True_()
    return NativeSourceRule(
        location_name,
        get_vanilla_reward_name(location_name, category),
        split_dash_and_sprint,
        allow_bellways_before_bell_beast,
        skips_tier,
        randomized_crest_slots_enabled,
        starting_location,
        trails_end_requirement,
        scuttlebrace_logic_enabled,
        pollip_heart_count,
        anchor_requirement_name,
        randomize_ledge_grab,
        randomize_swim,
    )
