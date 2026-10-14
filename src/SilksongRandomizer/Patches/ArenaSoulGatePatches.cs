using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    [HarmonyPatch(typeof(BattleScene), "Start")]
    internal static class ArenaSoulGatePatches
    {
        internal sealed class Arena
        {
            internal readonly string Scene, BattlePath, TriggerPath, Fsm;
            internal readonly string[] Left, Right;

            internal Arena(string scene, string battle, string trigger, string fsm, string[] left, string[] right)
            {
                Scene = scene;
                BattlePath = battle;
                TriggerPath = trigger;
                Fsm = fsm;
                Left = left;
                Right = right;
            }
        }

        internal static readonly Arena[] Arenas =
        {
            new Arena("Ant_04_mid", "Black Thread States Thread Only Variant/Normal World/Battle Scene", "", "BG Control",
                new[] { "Battle Gate Ant (1)" }, new[] { "Battle Gate Ant" }),
            new Arena("Ant_04_mid", "Black Thread States Thread Only Variant/Black Thread World/Battle Scene Black Thread", "", "Control",
                new[] { "Black Thread Battle Gate (1)", "Black Thread Battle Gate (3)" },
                new[] { "Black Thread Battle Gate", "Black Thread Battle Gate (2)" }),
            new Arena("Room_Forge", "Battle Scene", "", "BG Control",
                new[] { "Battle Gate" }, new[] { "Battle Gate (1)" }),
            new Arena("Song_04", "Black Thread States/Normal World/Scene States/Battle Scene", "Wake Scene", "BG Control",
                new[] { "Battle Gate" }, new[] { "Battle Gate (1)" }),
            new Arena("Chapel_Wanderer", "Battle Scene", "", "BG Control",
                new[] { "Battle Gate Bone" }, new[] { "Battle Gate Bone (1)" }),
            new Arena("Slab_23", "Battle Scene", "", "BG Control",
                new[] { "Battle Gate Slab (3)", "Battle Gate Slab (2)" },
                new[] { "Battle Gate Slab", "Battle Gate Slab (1)" }),
        };

        internal static readonly AccessTools.FieldRef<BattleScene, bool> Completed =
            AccessTools.FieldRefAccess<BattleScene, bool>("completed");

        internal static Arena Find(string scene, string path)
        {
            foreach (Arena arena in Arenas)
                if (string.Equals(scene, arena.Scene, StringComparison.OrdinalIgnoreCase) && path == arena.BattlePath)
                    return arena;
            return null;
        }

        private static PlayMakerFSM[] ResolveGates(BattleScene battle, string[] names, string fsmName)
        {
            var gates = new List<PlayMakerFSM>();
            foreach (string name in names)
            {
                Transform gate = battle.gates.transform.Find(name);
                PlayMakerFSM controller = gate == null ? null : gate.GetComponent<PlayMakerFSM>();
                if (controller == null || controller.FsmName != fsmName) return null;
                gates.Add(controller);
            }
            return gates.ToArray();
        }

        private static void Postfix(BattleScene __instance)
        {
            SaveState state = SaveState.Instance;
            if (state == null || string.IsNullOrEmpty(state.enemySoulsJson) || state.enemySoulsJson == "[]" ||
                Completed(__instance) || __instance.gates == null) return;
            string scene = __instance.gameObject.scene.name;
            string path = Utils.GetHierarchyPath(__instance.transform);
            Arena arena = Find(scene, path);
            if (arena == null || EnemySoulPatches.CanStartBattle(scene, path, state)) return;
            Transform source = arena.TriggerPath.Length == 0 ? __instance.transform : __instance.transform.Find(arena.TriggerPath);
            BoxCollider2D trigger = source == null ? null : source.GetComponent<BoxCollider2D>();
            PlayMakerFSM[] left = ResolveGates(__instance, arena.Left, arena.Fsm);
            PlayMakerFSM[] right = ResolveGates(__instance, arena.Right, arena.Fsm);
            if (trigger == null || !trigger.enabled || !trigger.isTrigger || left == null || right == null) return;
            var guard = source.gameObject.AddComponent<ArenaSoulGate>();
            guard.Configure(__instance, trigger, left, right, state);
        }
    }

    internal sealed class ArenaSoulGate : MonoBehaviour
    {
        private BattleScene battle;
        private BoxCollider2D trigger;
        private PlayMakerFSM[] left, right;
        private SaveState owner;
        private bool closed;

        internal void Configure(BattleScene encounter, BoxCollider2D boundary, PlayMakerFSM[] leftGates,
            PlayMakerFSM[] rightGates, SaveState state)
        {
            battle = encounter;
            trigger = boundary;
            left = leftGates;
            right = rightGates;
            owner = state;
        }

        private void OnTriggerEnter2D(Collider2D other) => CloseExit(other);
        private void OnTriggerStay2D(Collider2D other) => CloseExit(other);

        internal bool TrySelectExit(float heroX, float centerX, out bool closeRight)
        {
            closeRight = heroX <= centerX;
            if (closed) return false;
            closed = true;
            return true;
        }

        private void CloseExit(Collider2D other)
        {
            if (closed || owner != SaveState.Instance || battle == null ||
                ArenaSoulGatePatches.Completed(battle) || !other.CompareTag("Player")) return;
            HeroController hero = other.GetComponent<HeroController>();
            if (hero == null || !hero.isHeroInPosition || EnemySoulPatches.CanStartBattle(battle)) return;
            float center = trigger.transform.TransformPoint(trigger.offset).x;
            if (!TrySelectExit(hero.transform.position.x, center, out bool closeRight)) return;
            PlayMakerFSM[] gates = closeRight ? right : left;
            foreach (PlayMakerFSM gate in gates) gate.SendEvent("BG CLOSE");
        }
    }
}
