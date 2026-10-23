from unittest import TestCase

from BaseClasses import CollectionState
from test.general import setup_multiworld

from .. import SilksongWorld
from ..requirement_rules import build_requirements_rule
from ..requirements import (
    INNATE_CAPABILITY_REQUIREMENTS, RANDOMIZED_INNATE_CAPABILITY_REQUIREMENTS,
    LEDGE_GRAB_CAPABILITY_REQUIREMENT, SWIM_CAPABILITY_REQUIREMENT,
    PATH_REQUIREMENTS, _compiled_room_clause_requirement, get_abstract_requirements,
)
from ..room_graph import LogicStatus, load_room_graph
from ..room_graph_logic import _compile_spec, compile_room_graph


class TestInnateAbilityGraphRows(TestCase):
    @classmethod
    def setUpClass(cls):
        cls.graph = load_room_graph()
        cls.multiworld = setup_multiworld(SilksongWorld, steps=(), seed=12345)
        cls.world = cls.multiworld.worlds[1]

    def permits(self, row, items=(), **options):
        self.assertEqual(row.status, LogicStatus.ACTIVE)
        rule = build_requirements_rule(tuple(
            _compiled_room_clause_requirement(clause)
            for clause in _compile_spec(row.requirement)
        ), randomized_crest_slots_enabled=False, **options).resolve(self.world)
        state = CollectionState(self.multiworld)
        for name in ("Crest: Hunter", *items):
            state.collect(self.world.create_item(name), True)
        return rule(state)

    def connection(self, room_id, source, target):
        matches = [row for row in self.graph.room_by_id[room_id].connections
                   if row.source_node_id == f"{room_id}#{source}"
                   and row.target_node_id == f"{room_id}#{target}"]
        self.assertEqual(len(matches), 1)
        return matches[0]

    def test_station_climb_uses_innate_or_received_ledge_grab(self):
        row = self.graph.connection_by_id[
            "greymoor/greymoor-bellway#bellway-zone>upper-entrance@pg1"]
        self.assertTrue(self.permits(row))
        self.assertFalse(self.permits(row, randomize_ledge_grab=True))
        for item in ("Ledge Grab", "Cling Grip", "Faydown Cloak"):
            with self.subTest(item=item):
                self.assertTrue(self.permits(row, (item,), randomize_ledge_grab=True))
        self.assertFalse(self.permits(row, ("Drifter's Cloak",), randomize_ledge_grab=True))

    def test_craw_entrance_swim_is_innate_unless_randomized(self):
        row = self.graph.transition_by_id["greymoor/greymoor-craw-lake-entrance@lr"]
        self.assertTrue(self.permits(row))
        self.assertFalse(self.permits(row, randomize_swim=True))
        self.assertTrue(self.permits(row, ("Swim",), randomize_swim=True))

    def test_west_bellshrine_sharpdart_keeps_its_dash_conjunction(self):
        room_id = "greymoor/greymoor-west-bellshrine-room"
        exit = self.graph.transition_by_id[room_id + "@tl"]
        row = self.connection(room_id, "upper-path-left", exit.source_node_id.split("#")[1])
        self.assertTrue(self.permits(row))
        self.assertFalse(self.permits(row, randomize_ledge_grab=True))
        self.assertFalse(self.permits(row, ("Sharpdart",), randomize_ledge_grab=True))
        self.assertTrue(self.permits(row, ("Sharpdart", "Swift Step"), randomize_ledge_grab=True))
        self.assertTrue(self.permits(row, ("Ledge Grab",), randomize_ledge_grab=True))
        self.assertTrue(self.permits(exit, randomize_ledge_grab=True))

    def test_bellhart_climb_and_return_keep_separate_requirements(self):
        climb = self.graph.connection_by_id["bellhart/bellhart-lower#hermit>top-exit@pl"]
        fall = self.graph.connection_by_id["bellhart/bellhart-lower#top-exit>hermit@pl"]
        self.assertFalse(self.permits(climb, randomize_ledge_grab=True))
        self.assertTrue(self.permits(climb, ("Ledge Grab",), randomize_ledge_grab=True))
        self.assertTrue(self.permits(fall, randomize_ledge_grab=True))

    def test_halfway_water_path_uses_innate_or_received_swim(self):
        room_id = "greymoor/greymoor-upper-halfway-home-path"
        for source, target in (("left-section", "right-section"), ("right-section", "left-section")):
            row = self.connection(room_id, source, target)
            self.assertTrue(self.permits(row))
            self.assertFalse(self.permits(row, randomize_swim=True))
            self.assertTrue(self.permits(row, ("Swim",), randomize_swim=True))
            self.assertFalse(self.permits(row, ("Faydown Cloak",), randomize_swim=True))

    def test_halfway_platform_path_keeps_directional_requirements(self):
        room_id = "greymoor/greymoor-upper-halfway-home-path"
        forward = self.connection(room_id, "long-platform", "short-platform")
        reverse = self.connection(room_id, "short-platform", "long-platform")
        self.assertFalse(self.permits(forward))
        self.assertTrue(self.permits(reverse))
        for row in (forward, reverse):
            self.assertFalse(self.permits(row, randomize_ledge_grab=True))
            self.assertTrue(self.permits(row, ("Faydown Cloak",), randomize_ledge_grab=True))
        options = dict(flea_brew_jump_logic=True)
        self.assertTrue(self.permits(forward, ("Flea Brew",), **options))
        self.assertFalse(self.permits(forward, ("Flea Brew",), randomize_ledge_grab=True, **options))
        self.assertFalse(self.permits(reverse, ("Flea Brew",), randomize_ledge_grab=True, **options))

    def test_throwing_ring_traversal_still_requires_swim_and_climbing(self):
        row = self.graph.connection_by_id[
            "bilewater/bilewater-waterfall#left-exit>trails-end@cr"]
        self.assertTrue(row.requirement.dnf)
        for clause in row.requirement.dnf:
            self.assertTrue({"item:faydown-cloak", "capability:swim", "item:cling-grip"} <= set(clause))
        checks = [check for check in self.graph.check_by_id.values()
                  if check.canonical_location == "Throwing Ring"]
        self.assertEqual(len(checks), 1)
        self.assertEqual(checks[0].node_id, row.target_node_id)
        self.assertTrue(all(any(atom.startswith("event:") for atom in clause)
                            for clause in checks[0].requirement.dnf))

    def test_craw_nest_checks_use_the_mapped_platform(self):
        check = next(check for check in self.graph.check_by_id.values()
                     if check.canonical_location == "Threefold Pin")
        self.assertEqual(check.node_id, "greymoor/greymoor-craw-lake#right-top-craw-nest")
        crossing = self.graph.connection_by_id[
            "greymoor/greymoor-craw-lake#left-top-craw-nest>right-top-craw-nest@g5"]
        clauses = crossing.requirement.dnf
        self.assertIn(("item:drifters-cloak",), clauses)
        self.assertIn(("item:clawline",), clauses)
        self.assertNotIn((), clauses)
        self.assertTrue(all("macro:progressive-swift-step-first" in clause
                            for clause in clauses if "item:sharpdart" in clause))

    def test_mapper_compiler_rejects_legacy_boundary_fallbacks(self):
        self.assertEqual(self.graph.assumptions["mapper_schema"], 3)
        with self.assertRaisesRegex(ValueError, "cannot use legacy traversal rules"):
            compile_room_graph(self.graph, legacy_rules=True)

    def test_abyss_escape_requires_clawline_or_proficient_movement(self):
        routes = PATH_REQUIREMENTS["Path: Abyss - Escape"]
        common = {"Path: Abyss - Void Tendrils", "Ancestral Art: Silk Soar",
                  "Ability: Drifter's Cloak", "Ancestral Art: Cling Grip", "Ability: Faydown Cloak"}
        self.assertTrue(routes)
        for route in routes:
            self.assertTrue(common <= set(route.all_of))
            self.assertTrue({"Ancestral Art: Clawline", "Option: Proficient Movement"} & set(route.all_of))

    def test_options_change_only_capability_owners(self):
        baseline = get_abstract_requirements()
        for ledge, swim in ((False, False), (True, False), (False, True), (True, True)):
            adjusted = get_abstract_requirements(randomize_ledge_grab=ledge, randomize_swim=swim)
            self.assertEqual(set(adjusted), set(baseline))
            allowed = {LEDGE_GRAB_CAPABILITY_REQUIREMENT, SWIM_CAPABILITY_REQUIREMENT}
            self.assertTrue({name for name in baseline if baseline[name] != adjusted[name]} <= allowed)
            for name, randomized in ((LEDGE_GRAB_CAPABILITY_REQUIREMENT, ledge),
                                     (SWIM_CAPABILITY_REQUIREMENT, swim)):
                table = RANDOMIZED_INNATE_CAPABILITY_REQUIREMENTS if randomized else INNATE_CAPABILITY_REQUIREMENTS
                self.assertEqual(adjusted[name], table[name])
