using System;
using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using UnityEngine.SceneManagement;
using Bounds = UnityEngine.Bounds;

namespace SilksongRandomizer.Patches
{
    [HarmonyPatch(typeof(BattleScene))]
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
            new Arena("Shadow_18", "Battle Scene", "", "BG Control",
                new[] { "Battle Gate Swamp", "Battle Gate Swamp (2)" },
                new[] { "Battle Gate Swamp (1)", "Battle Gate Swamp (3)" }),
        };

        internal static readonly AccessTools.FieldRef<BattleScene, bool> Completed =
            AccessTools.FieldRefAccess<BattleScene, bool>("completed");
        internal static readonly AccessTools.FieldRef<BattleScene, bool> Started =
            AccessTools.FieldRefAccess<BattleScene, bool>("started");
        private static readonly AccessTools.FieldRef<BattleScene, int> StartupFrames =
            AccessTools.FieldRefAccess<BattleScene, int>("loopsUntilDeactivate");

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

        internal static bool TryGetInterior(BattleScene battle, Collider2D trigger, out Bounds interior)
        {
            interior = trigger.bounds;
            if (battle.gates == null) return false;
            string scene = battle.gameObject.scene.name;
            string path = Utils.GetHierarchyPath(battle.transform);
            Arena arena = Find(scene, path);
            string[] left = arena?.Left, right = arena?.Right;
            if (scene == "Hang_04_boss" && path == "Battle Scene")
            {
                left = new[] { "Battle Gate" };
                right = new[] { "Battle Gate (1)" };
            }
            if (left == null || right == null) return false;
            if (!TryGateEdge(battle.gates.transform, left, true, out float min) ||
                !TryGateEdge(battle.gates.transform, right, false, out float max) || min >= max) return false;
            Vector3 lower = interior.min, upper = interior.max;
            lower.x = min;
            upper.x = max;
            interior.SetMinMax(lower, upper);
            return true;
        }

        private static bool TryGateEdge(Transform root, string[] names, bool left, out float edge)
        {
            edge = left ? float.NegativeInfinity : float.PositiveInfinity;
            Transform nearest = null;
            foreach (string name in names)
            {
                Transform gate = root.Find(name);
                if (gate == null) return false;
                if (nearest == null || (left ? gate.position.x > nearest.position.x : gate.position.x < nearest.position.x))
                    nearest = gate;
            }
            bool found = false;
            foreach (BoxCollider2D collider in nearest.GetComponentsInChildren<BoxCollider2D>(true))
            {
                if (collider.isTrigger) continue;
                Vector2 half = collider.size * 0.5f;
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                    {
                        float value = collider.transform.TransformPoint(collider.offset + new Vector2(x * half.x, y * half.y)).x;
                        edge = left ? Mathf.Max(edge, value) : Mathf.Min(edge, value);
                    }
                found = true;
            }
            return found;
        }

        [HarmonyPostfix, HarmonyPatch("Start")]
        private static void StartPostfix(BattleScene __instance)
        {
            SaveState state = SaveState.Instance;
            if (state == null ||
                ((string.IsNullOrEmpty(state.enemySoulsJson) || state.enemySoulsJson == "[]") &&
                 (string.IsNullOrEmpty(state.bossSoulsJson) || state.bossSoulsJson == "[]")) ||
                Completed(__instance) || __instance.gates == null) return;
            string scene = __instance.gameObject.scene.name;
            string path = Utils.GetHierarchyPath(__instance.transform);
            Arena arena = Find(scene, path);
            if (arena == null || SoulsReady(__instance)) return;
            Transform source = arena.TriggerPath.Length == 0 ? __instance.transform : __instance.transform.Find(arena.TriggerPath);
            BoxCollider2D trigger = source == null ? null : source.GetComponent<BoxCollider2D>();
            PlayMakerFSM[] left = ResolveGates(__instance, arena.Left, arena.Fsm);
            PlayMakerFSM[] right = ResolveGates(__instance, arena.Right, arena.Fsm);
            if (trigger == null || !trigger.enabled || !trigger.isTrigger || left == null || right == null) return;
            var guard = source.gameObject.AddComponent<ArenaSoulGate>();
            guard.Configure(__instance, trigger, left, right, state);
        }

        internal static bool SoulsReady(BattleScene battle) => EnemySoulPatches.CanStartBattle(battle) &&
            BossSoulPatches.CanStartBattle(battle.gameObject.scene.name,
                Utils.GetHierarchyPath(battle.transform), SaveState.Instance);

        internal static bool PlayerReady(HeroController hero) => hero != null && hero.isHeroInPosition &&
            hero.cState != null && !hero.cState.dead && !hero.cState.hazardDeath &&
            !hero.IsInputBlocked() && GameManager.instance != null && !GameManager.instance.isPaused;

        [HarmonyPostfix, HarmonyPatch("OnTriggerEnter2D")]
        private static void TriggerEnterPostfix(BattleScene __instance, Collider2D collision)
        {
            SaveState state = SaveState.Instance;
            if (state == null ||
                ((string.IsNullOrEmpty(state.enemySoulsJson) || state.enemySoulsJson == "[]") &&
                 (string.IsNullOrEmpty(state.bossSoulsJson) || state.bossSoulsJson == "[]")) ||
                Started(__instance) || Completed(__instance) ||
                __instance.currentWave != 0 || StartupFrames(__instance) > 0 || !collision.CompareTag("Player")) return;
            HeroController hero = collision.GetComponent<HeroController>();
            Collider2D trigger = __instance.GetComponent<Collider2D>();
            if (hero == null || !hero.isHeroInPosition || trigger == null || !trigger.enabled ||
                !trigger.isTrigger || SoulsReady(__instance)) return;
            ArenaSoulResume wait = __instance.GetComponent<ArenaSoulResume>();
            if (wait == null) wait = __instance.gameObject.AddComponent<ArenaSoulResume>();
            wait.Configure(__instance, trigger, collision, state);
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
            if (hero == null || !hero.isHeroInPosition || ArenaSoulGatePatches.SoulsReady(battle)) return;
            float center = trigger.transform.TransformPoint(trigger.offset).x;
            if (!TrySelectExit(hero.transform.position.x, center, out bool closeRight)) return;
            PlayMakerFSM[] gates = closeRight ? right : left;
            foreach (PlayMakerFSM gate in gates) gate.SendEvent("BG CLOSE");
            RandomizerPlugin.Log?.LogDebug($"[Arena] {battle.gameObject.scene.name}: waiting for souls, " +
                $"{(closeRight ? "right" : "left")} exit closed, entry={hero.sceneEntryGate?.name ?? "unknown"}");
        }
    }

    internal sealed class ArenaSoulResume : MonoBehaviour
    {
        private BattleScene battle;
        private Collider2D player;
        private SoulArenaContact contact;
        private SaveState owner;
        private int receivedCount = -1;
        private bool ready, pending;
        private readonly ArenaSoulDiagnostic diagnostic = new ArenaSoulDiagnostic();

        internal void Configure(BattleScene encounter, Collider2D trigger, Collider2D hero, SaveState state)
        {
            battle = encounter;
            player = hero;
            owner = state;
            contact = new SoulArenaContact(trigger, encounter);
            receivedCount = -1;
            ready = false;
            pending = true;
            enabled = true;
        }

        internal bool TryResume(bool sameSave, bool active, bool completed, bool started,
            bool playerReady, bool inside, bool soulsReady)
        {
            if (!sameSave || !active || completed || started) pending = false;
            if (!pending || !playerReady || !inside || !soulsReady) return false;
            pending = false;
            return true;
        }

        private void Update()
        {
            if (!pending || owner != SaveState.Instance || battle == null ||
                !battle.isActiveAndEnabled || ArenaSoulGatePatches.Completed(battle) ||
                ArenaSoulGatePatches.Started(battle) || !contact.Source.enabled)
            {
                pending = false;
                enabled = false;
                return;
            }
            int count = owner.receivedItems?.Count ?? 0;
            if (receivedCount != count)
            {
                receivedCount = count;
                ready = ArenaSoulGatePatches.SoulsReady(battle);
            }
            HeroController hero = player == null ? null : player.GetComponent<HeroController>();
            bool playerReady = ArenaSoulGatePatches.PlayerReady(hero);
            bool inside = player != null && contact.Contains(player.bounds);
            var reason = !ready ? ArenaWaitReason.MissingSouls : !inside ? ArenaWaitReason.OutsideArena :
                !playerReady ? ArenaWaitReason.PlayerNotReady : ArenaWaitReason.Resuming;
            diagnostic.Battle(battle, reason, count);
            if (!TryResume(owner == SaveState.Instance, battle.isActiveAndEnabled,
                ArenaSoulGatePatches.Completed(battle), ArenaSoulGatePatches.Started(battle),
                playerReady, inside, ready)) return;
            enabled = false;
            battle.StartBattle();
        }
    }

    internal enum ArenaWaitReason { None, MissingSouls, OutsideArena, PlayerNotReady, Resuming }

    internal sealed class ArenaSoulDiagnostic
    {
        private ArenaWaitReason previous;
        private int previousItems = -1;
        private string lastMessage;

        internal bool Changed(ArenaWaitReason reason, int items)
        {
            if (reason != ArenaWaitReason.MissingSouls) items = 0;
            if (reason == previous && items == previousItems) return false;
            previous = reason;
            previousItems = items;
            return true;
        }

        internal void Battle(BattleScene battle, ArenaWaitReason reason, int items)
        {
            if (!Changed(reason, items)) return;
            Report(battle.gameObject.scene.name, Utils.GetHierarchyPath(battle.transform), null, null, reason);
        }

        internal void State(FsmState state, ArenaWaitReason reason, int items)
        {
            if (!Changed(reason, items)) return;
            Report(state.Fsm.GameObject.scene.name, Utils.GetHierarchyPath(state.Fsm.GameObject.transform),
                state.Fsm.Name, state.Name, reason);
        }

        private void Report(string scene, string path, string fsm, string state, ArenaWaitReason reason)
        {
            if (RandomizerPlugin.Log == null) return;
            string detail;
            if (reason == ArenaWaitReason.MissingSouls)
            {
                var missing = new List<string>();
                BossSoulPatches.DescribeMissingSouls(scene, path, fsm, state, SaveState.Instance, missing);
                detail = missing.Count == 0 ? "waiting for native wave soul check" : "waiting for " + string.Join(", ", missing);
            }
            else detail = reason == ArenaWaitReason.OutsideArena ? "waiting for player inside the arena" :
                reason == ArenaWaitReason.PlayerNotReady ? "waiting for player control" : "resuming encounter";
            string entry = HeroController.instance?.sceneEntryGate?.name ?? "unknown";
            string message = $"[Arena] {scene}/{path} {fsm}/{state}: {detail}, entry={entry}";
            if (message == lastMessage) return;
            lastMessage = message;
            RandomizerPlugin.Log.LogDebug(message);
        }
    }

    internal sealed class SoulArenaContact
    {
        internal readonly Collider2D Source;
        private readonly Bounds interior;
        private readonly bool bounded;
        private readonly int scene;
        private bool entered;

        internal SoulArenaContact(Collider2D source, BattleScene battle)
        {
            Source = source;
            scene = SceneManager.GetActiveScene().handle;
            bounded = battle != null && ArenaSoulGatePatches.TryGetInterior(battle, source, out interior);
        }

        internal static bool Fits(Bounds player, Bounds area) =>
            player.min.x > area.min.x && player.max.x < area.max.x &&
            player.center.y >= area.min.y && player.center.y <= area.max.y;

        internal bool Contains(Bounds player) => SceneManager.GetActiveScene().handle == scene &&
            Source != null && Source.enabled && Source.gameObject.activeInHierarchy &&
            (bounded ? Fits(player, interior) : Source.OverlapPoint(player.center));

        internal void Observe(Collider2D player)
        {
            if (player != null && Contains(player.bounds) && Source.OverlapPoint(player.bounds.center)) entered = true;
        }

        internal bool CanReplay(Collider2D player) => entered && player != null && Contains(player.bounds);
    }

    internal sealed class SoulTriggerContact
    {
        private static readonly Action<Trigger2dEvent, Collider2D> Enter =
            AccessTools.MethodDelegate<Action<Trigger2dEvent, Collider2D>>(AccessTools.Method(typeof(Trigger2dEvent), "TriggerEnter2D"));
        private static readonly Action<Trigger2dEvent, Collider2D> Stay =
            AccessTools.MethodDelegate<Action<Trigger2dEvent, Collider2D>>(AccessTools.Method(typeof(Trigger2dEvent), "TriggerStay2D"));
        private readonly Trigger2dEvent trigger;
        private readonly SoulArenaContact contact;

        private SoulTriggerContact(Trigger2dEvent action, Collider2D source)
        {
            trigger = action;
            contact = new SoulArenaContact(source, source.GetComponentInParent<BattleScene>());
        }

        internal static SoulTriggerContact Create(FsmStateAction action)
        {
            if (!(action is Trigger2dEvent trigger) || !trigger.Enabled ||
                (trigger.trigger != Trigger2DType.OnTriggerEnter2D && trigger.trigger != Trigger2DType.OnTriggerStay2D)) return null;
            GameObject target = trigger.Fsm.GetOwnerDefaultTarget(trigger.gameObject);
            Collider2D source = target == null ? null : target.GetComponent<Collider2D>();
            return source != null && source.isTrigger ? new SoulTriggerContact(trigger, source) : null;
        }

        internal void Observe(Collider2D player) => contact.Observe(player);
        internal bool CanReplay(Collider2D player) => contact.CanReplay(player);
        internal void Replay(Collider2D player)
        {
            if (trigger.trigger == Trigger2DType.OnTriggerEnter2D) Enter(trigger, player);
            else Stay(trigger, player);
        }
    }
}
