using System;
using System.Collections;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace SilksongRandomizer.Patches
{
    internal static class GreymoorBridgePatches
    {
        private const string SceneName = "Greymoor_02";
        private const string LeverName = "Greymoor Stand Lever - Bridge";
        private const string ReturnLeverName = "Greymoor Bridge Return Lever";

        internal static bool CanToggle(bool active, bool busy, bool firstHit, bool nailDamage) =>
            active && !busy && firstHit && nailDamage;

        private static T Read<T>(object instance, string field) =>
            (T)AccessTools.Field(instance.GetType(), field).GetValue(instance);

        [HarmonyPatch(typeof(HeroController), "SendHeroInPosition")]
        private static class ArrivalPatch
        {
            [HarmonyPostfix]
            private static void Postfix(HeroController __instance)
            {
                if (SaveState.Instance?.IsRoomBound != true ||
                    GameManager.instance?.GetSceneNameString() != SceneName) return;
                try
                {
                    Install();
                }
                catch (Exception ex)
                {
                    RandomizerPlugin.Log?.LogError("[RANDOMIZER] Could not prepare the Greymoor bridge return lever: " + ex);
                }
            }
        }

        private static void Install()
        {
            Scene scene = SceneManager.GetSceneByName(SceneName);
            if (!scene.IsValid() || !scene.isLoaded) return;
            GameObject[] roots = scene.GetRootGameObjects();
            GameObject bridge = roots.SingleOrDefault(x => x.name == "Bridge");
            Lever original = roots.SingleOrDefault(x => x.name == LeverName)?.GetComponent<Lever>();
            if (bridge == null || original == null || bridge.GetComponent<BridgeControl>() != null) return;
            if (roots.Any(x => x.name == ReturnLeverName)) return;
            BoxCollider2D floor = bridge.GetComponent<BoxCollider2D>();
            Gate[] sections = bridge.GetComponentsInChildren<Gate>(true);
            Animator[] animators = sections.Select(x => x.GetComponent<Animator>()).ToArray();
            PersistentBoolItem persistence = original.GetComponent<PersistentBoolItem>();
            if (floor == null || floor.isTrigger || sections.Length != 9 || persistence == null ||
                animators.Any(x => x == null || !x.HasState(0, Animator.StringToHash("Opening"))))
                throw new InvalidOperationException("Unexpected bridge layout.");
            if (sections.Any(x => x.name != "greymoor_flip_bridge"))
                throw new InvalidOperationException("Unexpected bridge section.");

            Vector3 center = floor.transform.TransformPoint(floor.offset);
            Vector3 size = Vector3.Scale(floor.size, floor.transform.lossyScale);
            Rect span = new Rect(center.x - Mathf.Abs(size.x) / 2f,
                center.y - Mathf.Abs(size.y) / 2f, Mathf.Abs(size.x), Mathf.Abs(size.y));
            int terrain = LayerMask.GetMask("Terrain");
            Vector2 leftProbe = new Vector2(span.xMin - 3f, span.yMax + 6f);
            RaycastHit2D leftGround = Physics2D.RaycastAll(leftProbe, Vector2.down, 12f, terrain)
                .FirstOrDefault(x => x.collider != null && !x.collider.isTrigger &&
                    x.collider != floor && x.normal.y > 0.8f && Mathf.Abs(x.point.y - span.yMax) < 3f);
            if (leftGround.collider == null)
                throw new InvalidOperationException("No suitable terrain beside the left bridge landing.");
            RaycastHit2D rightGround = Physics2D.RaycastAll(
                (Vector2)original.transform.position + Vector2.up, Vector2.down, 5f, terrain)
                .FirstOrDefault(x => x.collider != null && !x.collider.isTrigger && x.normal.y > 0.8f);
            if (rightGround.collider == null)
                throw new InvalidOperationException("No terrain beneath the original bridge lever.");
            float baseOffset = original.transform.position.y - rightGround.point.y;
            Vector3 position = new Vector3(leftProbe.x, leftGround.point.y + baseOffset,
                original.transform.position.z);

            GameObject staging = new GameObject("Greymoor Bridge Lever Setup");
            staging.SetActive(false);
            SceneManager.MoveGameObjectToScene(staging, scene);
            GameObject copy = null;
            BridgeControl control = null;
            try
            {
                copy = UnityEngine.Object.Instantiate(original.gameObject, staging.transform);
                copy.name = ReturnLeverName;
                copy.SetActive(false);
                Lever left = copy.GetComponent<Lever>();
                AccessTools.Field(typeof(Lever), "persistent").SetValue(left, null);
                foreach (PersistentBoolItem item in copy.GetComponentsInChildren<PersistentBoolItem>(true))
                    UnityEngine.Object.DestroyImmediate(item);
                AccessTools.Field(typeof(Lever), "unlockables").SetValue(left, Array.Empty<UnlockablePropBase>());
                AccessTools.Field(typeof(Lever), "fsmGates").SetValue(left, Array.Empty<PlayMakerFSM>());
                left.OnHit = new UnityEvent();
                left.OnHitDelayed = new UnityEvent();
                left.OnActivated = new UnityEvent();
                left.OnStartActivated = new UnityEvent();
                copy.transform.SetParent(null, false);
                copy.transform.position = position;
                control = bridge.AddComponent<BridgeControl>();
                control.Initialize(original, left, floor, animators, persistence, span);
                original.gameObject.AddComponent<BridgeLever>().Control = control;
                copy.AddComponent<BridgeLever>().Control = control;
                copy.SetActive(true);
                control.RefreshLevers();
                RandomizerPlugin.Log?.LogInfo("[RANDOMIZER] Greymoor bridge return lever ready at " + position);
            }
            catch
            {
                if (copy != null) UnityEngine.Object.Destroy(copy);
                if (control != null) UnityEngine.Object.Destroy(control);
                BridgeLever link = original.GetComponent<BridgeLever>();
                if (link != null) UnityEngine.Object.Destroy(link);
                throw;
            }
            finally
            {
                UnityEngine.Object.Destroy(staging);
            }
        }

        [HarmonyPatch(typeof(Lever), nameof(Lever.Hit))]
        private static class LeverHitPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(Lever __instance, HitInstance damageInstance,
                ref IHitResponder.HitResponse __result)
            {
                BridgeControl control = __instance.GetComponent<BridgeLever>()?.Control;
                if (control == null) return true;
                __result = control.Hit(__instance, damageInstance);
                return false;
            }
        }

        private sealed class BridgeLever : MonoBehaviour
        {
            internal BridgeControl Control;
        }

        private sealed class BridgeControl : MonoBehaviour
        {
            private Lever original;
            private Lever left;
            private BoxCollider2D floor;
            private Animator[] sections;
            private PersistentBoolItem persistence;
            private Rect span;
            private bool open;
            private bool busy;
            private AudioEvent hitSound;
            private CameraShakeTarget hitShake;

            internal void Initialize(Lever original, Lever left, BoxCollider2D floor,
                Animator[] sections, PersistentBoolItem persistence, Rect span)
            {
                this.original = original;
                this.left = left;
                this.floor = floor;
                this.sections = sections;
                this.persistence = persistence;
                this.span = span;
                open = persistence.GetCurrentValue();
                hitSound = Read<AudioEvent>(original, "hitSound");
                hitShake = Read<CameraShakeTarget>(original, "hitCameraShake");
            }

            internal void RefreshLevers()
            {
                foreach (Lever lever in new[] { original, left })
                {
                    if (lever == null) continue;
                    lever.SetActivatedInert(open);
                    lever.HitBlocked = busy;
                    GameObject tinker = Read<GameObject>(lever, "activatedTinker");
                    if (tinker != null) tinker.SetActive(false);
                    Animator animator = lever.GetComponent<Animator>();
                    if (animator != null) animator.Rebind();
                }
            }

            internal IHitResponder.HitResponse Hit(Lever lever, HitInstance hit)
            {
                if (!CanToggle(SaveState.Instance?.IsRoomBound == true, busy,
                    hit.IsFirstHit, hit.IsNailDamage)) return IHitResponder.Response.None;
                if (open && HeroOverlapsBridge()) return IHitResponder.Response.None;
                busy = true;
                original.HitBlocked = true;
                left.HitBlocked = true;
                Animator animator = lever.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.SetFloat("Hit Direction", HeroController.instance != null &&
                        HeroController.instance.transform.position.x < lever.transform.position.x ? 1f : -1f);
                    animator.SetTrigger("Hit");
                }
                StartCoroutine(Toggle());
                hitSound.SpawnAndPlayOneShot(lever.transform.position);
                hitShake.DoShake(lever);
                return IHitResponder.Response.GenericHit;
            }

            private bool HeroOverlapsBridge()
            {
                Collider2D body = HeroController.instance?.GetComponent<Collider2D>();
                if (body == null) return false;
                Bounds bounds = body.bounds;
                return span.Overlaps(new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y));
            }

            private void SampleBridge(float position)
            {
                foreach (Animator animator in sections)
                {
                    animator.enabled = true;
                    animator.fireEvents = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.Play("Opening", 0, position);
                    animator.Update(0f);
                    animator.enabled = false;
                }
            }

            private IEnumerator Toggle()
            {
                bool next = !open;
                try
                {
                    yield return new WaitForSeconds(0.15f);
                    floor.enabled = false;
                    for (float elapsed = 0f; elapsed < 0.6f; elapsed += Time.deltaTime)
                    {
                        float progress = Mathf.Clamp01(elapsed / 0.6f);
                        SampleBridge(next ? progress : 1f - progress);
                        yield return null;
                    }
                    if (!next)
                        while (HeroOverlapsBridge()) yield return null;
                    SampleBridge(next ? 1f : 0f);
                    open = next;
                    original.SetActivatedInert(open);
                    persistence.SetValueOverride(open);
                    persistence.SaveState();
                    GameManager.instance?.QueueSaveGame();
                }
                finally
                {
                    busy = false;
                    if (floor != null)
                    {
                        SampleBridge(open ? 1f : 0f);
                        floor.enabled = !open;
                        Transform cameraLock = transform.Find("CameraLockArea Bridge");
                        if (cameraLock != null) cameraLock.gameObject.SetActive(!open);
                        RefreshLevers();
                    }
                }
            }
        }
    }
}
