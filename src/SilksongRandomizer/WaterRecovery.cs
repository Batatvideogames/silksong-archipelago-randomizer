using System.Collections;
using System.Collections.Generic;
using GlobalEnums;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SilksongRandomizer
{
    internal static class WaterRecovery
    {
        private const int GroundLayers = 8448;
        private static readonly Collider2D[] Overlaps = new Collider2D[64];
        private static SaveState owner;
        private static int sceneHandle;
        private static BattleScene[] battles;
        private static Collider2D support, settlingSupport;
        private static Vector3 point, supportPosition, supportScale;
        private static Quaternion supportRotation;
        private static Bounds body;
        private static bool facing, pending;
        private static readonly Dictionary<int, int> ArenaStates = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> CurrentArenaStates = new Dictionary<int, int>();
        private static float nextSample, settledSince, resumeAt;

        private struct NativeMarker
        {
            internal PlayerData Player;
            internal Vector3 Position, Temporary;
            internal HazardRespawnMarker.FacingDirection Facing;
            internal int Scene;
        }

        internal static void Clear()
        {
            owner = null;
            support = settlingSupport = null;
            battles = null;
            pending = false;
            nextSample = resumeAt = 0f;
        }

        internal static void Remember(HeroController hero)
        {
            var scene = SceneManager.GetActiveScene();
            SaveState state = SaveState.Instance;
            if (state?.IsRoomBound != true || InnateAbilityManager.SwimEnabled)
            {
                Clear();
                return;
            }
            if (!ReferenceEquals(owner, state) || sceneHandle != scene.handle)
            {
                Clear();
                owner = state;
                sceneHandle = scene.handle;
                battles = Object.FindObjectsByType<BattleScene>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            }
            GameManager manager = GameManager.SilentInstance;
            if (hero != null && (hero.cState.dead || (hero.cState.hazardDeath && !pending))) support = null;
            if (hero == null || manager == null || manager.GameState != GameState.PLAYING ||
                !hero.isHeroInPosition || !hero.cState.onGround || hero.cState.swimming ||
                hero.cState.dead || hero.cState.hazardDeath || hero.cState.hazardRespawning ||
                hero.cState.transitioning || hero.cState.recoiling || hero.cState.recoilFrozen ||
                hero.cState.wallClinging || hero.cState.wallSliding || hero.cState.mantling ||
                hero.cState.dashing || hero.controlReqlinquished || !hero.CanInput() ||
                ToolItemManager.IsInCutscene || Time.time < resumeAt)
            {
                settlingSupport = null;
                return;
            }
            if (Time.time < nextSample) return;
            nextSample = Time.time + 0.1f;
            Collider2D collider = hero.GetComponent<Collider2D>();
            if (collider == null || !collider.enabled) return;
            Bounds bounds = collider.bounds;
            Vector3 position = hero.transform.position;
            if (!TryGround(position, bounds, out Collider2D ground) || !ClearBody(hero, bounds, ground))
            {
                settlingSupport = null;
                return;
            }
            if (settlingSupport != ground)
            {
                settlingSupport = ground;
                settledSince = Time.time;
                battles = Object.FindObjectsByType<BattleScene>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                return;
            }
            if (Time.time - settledSince < 0.2f) return;
            support = ground;
            supportPosition = ground.transform.position;
            supportRotation = ground.transform.rotation;
            supportScale = ground.transform.lossyScale;
            point = position;
            body = bounds;
            facing = hero.cState.facingRight;
            CaptureArenaState(battles, ArenaStates);
        }

        private static bool TryGround(Vector3 position, Bounds bounds, out Collider2D ground)
        {
            ground = null;
            float halfWidth = Mathf.Max(bounds.extents.x, 0.35f);
            for (int sample = -1; sample <= 1; sample++)
            {
                Vector2 origin = new Vector2(bounds.center.x + sample * halfWidth, bounds.min.y + 0.2f);
                if (!Helper.IsRayHittingNoTriggers(origin, Vector2.down, 0.45f, GroundLayers, out var hit) ||
                    hit.normal.y < 0.95f || hit.collider.gameObject.scene.handle != sceneHandle ||
                    hit.collider.GetComponentInParent<DamageHero>() != null ||
                    (hit.collider.attachedRigidbody != null && hit.collider.attachedRigidbody.bodyType != RigidbodyType2D.Static))
                    return false;
                if (sample == -1) ground = hit.collider;
                else if (hit.collider != ground) return false;
            }
            // The native respawn also casts down from Hornet's centre.
            return Helper.IsRayHittingNoTriggers(position, Vector2.down, bounds.size.y, GroundLayers, out var centre) &&
                centre.collider == ground;
        }

        private static bool ClearBody(HeroController hero, Bounds bounds, Collider2D ground)
        {
            Vector2 size = bounds.size;
            size.x += 0.1f;
            size.y -= 0.1f;
            int count = Physics2D.OverlapBox(bounds.center + Vector3.up * 0.05f, size, 0,
                new ContactFilter2D { useTriggers = true }, Overlaps);
            if (count == Overlaps.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Collider2D collider = Overlaps[i];
                if (collider == null || collider == ground || collider.transform.IsChildOf(hero.transform)) continue;
                var water = collider.GetComponentInParent<SurfaceWaterRegion>();
                var damage = collider.GetComponentInParent<DamageHero>();
                if ((water != null && water.isActiveAndEnabled) || (damage != null && damage.isActiveAndEnabled) ||
                    (!collider.isTrigger && ((1 << collider.gameObject.layer) & GroundLayers) != 0)) return false;
            }
            return true;
        }

        private static void CaptureArenaState(BattleScene[] scenes, Dictionary<int, int> result)
        {
            result.Clear();
            foreach (BattleScene battle in scenes)
            {
                if (battle == null || battle.gameObject.scene.handle != sceneHandle) continue;
                int flags = (battle.isActiveAndEnabled ? 1 : 0) |
                    (Patches.ArenaSoulGatePatches.Started(battle) ? 2 : 0) |
                    (Patches.ArenaSoulGatePatches.Completed(battle) ? 4 : 0);
                result[battle.GetInstanceID()] = flags;
                if (battle.gates == null) continue;
                foreach (Collider2D gate in battle.gates.GetComponentsInChildren<Collider2D>(true))
                    if (!gate.isTrigger)
                        result[gate.GetInstanceID()] = gate.enabled && gate.gameObject.activeInHierarchy ? 1 : 0;
            }
        }

        private static bool ArenasUnchanged()
        {
            CaptureArenaState(Object.FindObjectsByType<BattleScene>(FindObjectsInactive.Include, FindObjectsSortMode.None), CurrentArenaStates);
            if (ArenaStates.Count != CurrentArenaStates.Count) return false;
            foreach (var entry in ArenaStates)
                if (!CurrentArenaStates.TryGetValue(entry.Key, out int flags) || flags != entry.Value) return false;
            return true;
        }

        internal static void Begin(HeroController hero)
        {
            pending = support != null && ReferenceEquals(owner, SaveState.Instance) &&
                sceneHandle == SceneManager.GetActiveScene().handle;
        }

        internal static void CancelUnlessFalling(HeroController hero)
        {
            if (!hero.cState.hazardDeath) pending = false;
        }

        private static NativeMarker UsePending(HeroController hero)
        {
            if (!pending) return default;
            pending = false;
            bool valid = hero != null && !InnateAbilityManager.SwimEnabled &&
                ReferenceEquals(owner, SaveState.Instance) &&
                sceneHandle == SceneManager.GetActiveScene().handle && support != null &&
                support.enabled && support.gameObject.activeInHierarchy &&
                support.transform.position == supportPosition && support.transform.rotation == supportRotation &&
                support.transform.lossyScale == supportScale &&
                ArenasUnchanged() &&
                TryGround(point, body, out Collider2D ground) && ground == support && ClearBody(hero, body, support);
            support = settlingSupport = null;
            resumeAt = Time.time + 1f;
            if (!valid) return default;
            PlayerData player = hero.playerData;
            var marker = new NativeMarker { Player = player, Position = player.hazardRespawnLocation,
                Facing = player.hazardRespawnFacing, Temporary = point, Scene = sceneHandle };
            hero.SetHazardRespawn(point, facing);
            return marker;
        }

        internal static IEnumerator Respawn(HeroController hero, IEnumerator original)
        {
            NativeMarker marker = UsePending(hero);
            try
            {
                while (original.MoveNext()) yield return original.Current;
            }
            finally
            {
                (original as System.IDisposable)?.Dispose();
                RestoreMarker(marker);
            }
        }

        private static void RestoreMarker(NativeMarker marker)
        {
            if (marker.Player == null || marker.Scene != SceneManager.GetActiveScene().handle ||
                marker.Player.hazardRespawnLocation != marker.Temporary) return;
            marker.Player.hazardRespawnLocation = marker.Position;
            marker.Player.hazardRespawnFacing = marker.Facing;
        }
    }
}
