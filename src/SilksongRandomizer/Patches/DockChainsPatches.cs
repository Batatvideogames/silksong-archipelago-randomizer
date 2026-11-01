using System;
using System.Collections;
using System.Linq;
using HarmonyLib;
using AnimatePositionBy = HutongGames.PlayMaker.Actions.AnimatePositionBy;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace SilksongRandomizer.Patches
{
    internal static class DockChainsPatches
    {
        private const string SceneName = "Dock_02b";
        private const string ReturnLeverName = "Dock Chain Return Lever";

        private static T Read<T>(object instance, string field) =>
            (T)AccessTools.Field(instance.GetType(), field).GetValue(instance);

        private static void Set(Lever_tk2d lever, string field, object value) =>
            AccessTools.Field(typeof(Lever_tk2d), field).SetValue(lever, value);

        [HarmonyPatch(typeof(HeroController), "SendHeroInPosition")]
        private static class ArrivalPatch
        {
            private static void Postfix()
            {
                if (SaveState.Instance?.IsRoomBound != true ||
                    GameManager.instance?.GetSceneNameString() != SceneName) return;
                try { Install(); }
                catch (Exception ex)
                {
                    RandomizerPlugin.Log?.LogError("[RANDOMIZER] Could not prepare the Dock chains return lever: " + ex);
                }
            }
        }

        private static void Install()
        {
            Scene scene = SceneManager.GetSceneByName(SceneName);
            if (!scene.IsValid() || !scene.isLoaded) return;
            GameObject[] roots = scene.GetRootGameObjects();
            if (roots.Any(g => g.name == ReturnLeverName)) return;
            Lever_tk2d original = roots.SingleOrDefault(g => g.name == "Song_lever_side")?.GetComponent<Lever_tk2d>();
            if (original == null) return;
            PlayMakerFSM lift = Read<PlayMakerFSM[]>(original, "fsmGates").Single();
            TimelineGate gate = Read<UnlockablePropBase[]>(original, "connectedGates").Single() as TimelineGate;
            AnimatePositionBy movement = lift.FsmStates.Single(s => s.Name == "Lower").Actions.OfType<AnimatePositionBy>().Single();
            PlayableDirector timeline = gate == null ? null : Read<PlayableDirector>(gate, "timeline");
            BoxCollider2D floor = gate?.transform.Find("Terrain Collider")?.GetComponent<BoxCollider2D>();
            if (lift.name != "Lift Dropper R" || gate?.name != "docks_giant_gate" || timeline == null || floor == null ||
                movement.shiftBy.Value.y >= 0 || movement.time.Value <= 0 ||
                gate.transform.Find("Active") == null || gate.transform.Find("Maskers") == null)
                throw new InvalidOperationException("Unexpected Dock chains layout.");

            GameObject staging = new GameObject("Dock Return Lever Setup");
            staging.SetActive(false);
            SceneManager.MoveGameObjectToScene(staging, scene);
            GameObject copy = null;
            try
            {
                copy = UnityEngine.Object.Instantiate(original.gameObject, staging.transform);
                copy.name = ReturnLeverName;
                copy.SetActive(false);
                foreach (PersistentBoolItem item in copy.GetComponentsInChildren<PersistentBoolItem>(true))
                    UnityEngine.Object.DestroyImmediate(item);
                Lever_tk2d lever = copy.GetComponent<Lever_tk2d>();
                Set(lever, "activated", false);
                Set(lever, "connectedGates", Array.Empty<UnlockablePropBase>());
                Set(lever, "fsmGates", Array.Empty<PlayMakerFSM>());
                Set(lever, "camLocks", Array.Empty<GameObject>());
                Set(lever, "setPlayerDataBool", "");
                Set(lever, "sendEventToRegister", "");
                lever.BeforeOpenDelay = new UnityEvent();
                lever.CustomGateOpen = new UnityEvent();
                lever.CustomGateActivated = new UnityEvent();
                copy.transform.SetParent(null, false);
                copy.transform.position = new Vector3(47.36f, 78.375f, original.transform.position.z);
                copy.transform.rotation = Quaternion.identity;
                ReturnControl control = copy.AddComponent<ReturnControl>();
                control.Initialize(original, lift, movement, gate, timeline, floor);
                lever.CustomGateOpen.AddListener(control.Raise);
                tk2dSpriteAnimator animator = copy.GetComponent<tk2dSpriteAnimator>();
                void Reveal()
                {
                    if (!Read<bool>(original, "activated")) return;
                    copy.SetActive(true);
                    animator.Play(animator.GetClipById(Read<int>(animator, "defaultClipId")));
                }
                original.CustomGateOpen.AddListener(Reveal);
                Reveal();
            }
            catch
            {
                if (copy != null) UnityEngine.Object.Destroy(copy);
                throw;
            }
            finally { UnityEngine.Object.Destroy(staging); }
        }

        [HarmonyPatch(typeof(Lever_tk2d), nameof(Lever_tk2d.Hit))]
        private static class ReturnHitPatch
        {
            private static bool Prefix(Lever_tk2d __instance, ref IHitResponder.HitResponse __result)
            {
                ReturnControl control = __instance.GetComponent<ReturnControl>();
                if (control == null || control.Ready) return true;
                __result = IHitResponder.Response.None;
                return false;
            }
        }

        private sealed class ReturnControl : MonoBehaviour
        {
            private Lever_tk2d original;
            private PlayMakerFSM lift;
            private AnimatePositionBy movement;
            private TimelineGate gate;
            private PlayableDirector timeline;
            private BoxCollider2D floor;
            private Vector3 lowerOffset;
            private bool busy;

            internal bool Ready => SaveState.Instance?.IsRoomBound == true && !busy &&
                Read<bool>(original, "activated") && (lift.ActiveStateName == "End" || lift.ActiveStateName == "Activate");

            internal void Initialize(Lever_tk2d original, PlayMakerFSM lift, AnimatePositionBy movement,
                TimelineGate gate, PlayableDirector timeline, BoxCollider2D floor)
            {
                this.original = original;
                this.lift = lift;
                this.movement = movement;
                this.gate = gate;
                this.timeline = timeline;
                this.floor = floor;
                lowerOffset = movement.shiftBy.Value;
            }

            internal void Raise() => StartCoroutine(Return());

            private bool FloorOccupied()
            {
                Collider2D hero = HeroController.instance?.GetComponent<Collider2D>();
                if (hero == null) return false;
                Vector3 size = Vector3.Scale(floor.size, floor.transform.lossyScale);
                Bounds bounds = new Bounds(floor.transform.TransformPoint(floor.offset),
                    new Vector3(Mathf.Abs(size.x) + .2f, Mathf.Abs(size.y) + .2f, 10));
                return bounds.Intersects(hero.bounds);
            }

            private IEnumerator Return()
            {
                busy = true;
                movement.shiftBy.Value = -lowerOffset;
                try
                {
                    lift.SetState("Lower");
                    while (lift.ActiveStateName != "End") yield return null;
                }
                finally { movement.shiftBy.Value = lowerOffset; }

                timeline.Stop();
                gate.transform.Find("Active").gameObject.SetActive(true);
                double duration = timeline.duration;
                for (float time = 0; time < 2f; time += Time.deltaTime)
                {
                    timeline.time = duration * (1 - time / 2f);
                    timeline.Evaluate();
                    floor.enabled = false;
                    yield return null;
                }
                timeline.time = 0;
                timeline.Evaluate();
                floor.enabled = false;
                gate.transform.Find("Maskers").gameObject.SetActive(true);
                while (FloorOccupied()) yield return null;
                floor.gameObject.SetActive(true);
                floor.enabled = true;
                busy = false;
            }
        }
    }
}
