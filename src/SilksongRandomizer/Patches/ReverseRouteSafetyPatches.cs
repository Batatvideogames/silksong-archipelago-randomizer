using HarmonyLib;
using HutongGames.PlayMaker;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    internal static class ReverseRouteSafetyPatches
    {
        private const string AqueductScene = "Aqueduct_01";
        private const string AqueductWallPath =
            "Breakable Wall Aqueduct Start";
        private const string ArboriumScene = "Arborium_11";
        private const string ArboriumWallPath = "Breakable Wall (1)";
        private const string ArboriumWallFsm = "breakable_wall_v2";
        private const string PeakScene = "Peak_04d";
        private const string PeakWallPath = "junk_pile_break_peak";
        private const string PeakWholePart = "junk_wall_break_pile";
        private const string PeakTerrainCollider =
            "terrain collider non slider";
        private const string PeakCameraLock = "CameraLockArea";
        private const string PeakRemasker = "Remasker New Sharp Ultra";
        private const string ShellwoodScene = "Shellwood_13";
        private const string ShellwoodWallPath = "Bell Wall Tall";
        private const string ShellwoodWallFsm = "Control";
        private const string ShellwoodWallPlayerData =
            "shellwood13_BellWall";
        private const string ShellwoodTerrainCollider =
            "Terrain Collider";
        private const string ShellwoodRemasker = "Remasker New";
        private const string ShellwoodPlayerDataVariable = "PD Bool";
        private const string ShellwoodFullyBreakVariable = "Fully Break";
        private const string ShellwoodDamageableRangeVariable =
            "Damageable Range";
        private const string ShellwoodTerrainColliderVariable =
            "Terrain Collider";
        private const string ShellwoodRangeCheckState = "Range Check 2";
        private const string ShellwoodHeavyBreakState = "Heavy Break";
        private const string ShellwoodHitState = "Hit";
        private const string ShellwoodStrikeEffectState = "Strike Effect";
        private const string ShellwoodBreakPauseState = "Break Pause";
        private const string ShellwoodBrokenState = "Broken";
        private const string ShellwoodFullyBrokenState = "Fully Broken";
        private const string ShellwoodBreakEvent = "BREAK";
        private const string WallInitState = "Init";
        private const string WallIdleState = "Idle";
        private const string WallBreakState = "Break";
        private const string WallBreakEvent = "QUICK BREAK";
        private const string WallMasksVariable = "Masks";
        private const string WallCameraLocksVariable = "Camera Locks";
        private const int WallSearchFrames = 180;
        private const int WallReleaseFrames = 60;

        internal static bool ShouldOpenSceneWall(
            bool isRoomBound,
            string sceneName,
            string targetScene
        )
        {
            return isRoomBound &&
                   string.Equals(
                       sceneName,
                       targetScene,
                       StringComparison.Ordinal
                   );
        }

        private static bool IsRoomBoundScene(
            HeroController hero,
            string sceneName
        )
        {
            SaveState state = SaveState.Instance;
            GameManager gameManager = GameManager.instance;
            return hero != null &&
                   state != null &&
                   gameManager != null &&
                   ShouldOpenSceneWall(
                       state.IsRoomBound,
                       gameManager.GetSceneNameString(),
                       sceneName
                   );
        }

        private static bool IsArboriumArrival(HeroController hero)
        {
            return IsRoomBoundScene(hero, ArboriumScene);
        }

        private static bool IsAqueductArrival(HeroController hero)
        {
            return IsRoomBoundScene(hero, AqueductScene);
        }

        private static bool IsPeakArrival(HeroController hero)
        {
            return IsRoomBoundScene(hero, PeakScene);
        }

        private static bool IsShellwoodArrival(HeroController hero)
        {
            return IsRoomBoundScene(hero, ShellwoodScene);
        }

        private static bool IsPeakRoomBound()
        {
            return IsRoomBoundScene(
                HeroController.SilentInstance,
                PeakScene
            );
        }

        private static bool IsAqueductRoomBound()
        {
            return IsRoomBoundScene(
                HeroController.SilentInstance,
                AqueductScene
            );
        }

        private static bool IsShellwoodRoomBound()
        {
            return IsRoomBoundScene(
                HeroController.SilentInstance,
                ShellwoodScene
            );
        }

        private static bool TryFindAqueductWall(out PlayMakerFSM wall)
        {
            wall = null;
            int matches = 0;
            foreach (PlayMakerFSM candidate in
                     Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
            {
                if (!IsExactAqueductWall(candidate))
                {
                    continue;
                }

                wall = candidate;
                matches++;
            }

            return matches == 1;
        }

        private static bool IsExactAqueductWall(PlayMakerFSM wall)
        {
            if (wall == null || wall.gameObject == null ||
                !string.Equals(
                    wall.gameObject.scene.name,
                    AqueductScene,
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    Utils.GetHierarchyPath(wall.transform),
                    AqueductWallPath,
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    wall.FsmName,
                    ArboriumWallFsm,
                    StringComparison.Ordinal
                ) ||
                Vector2.Distance(
                    wall.transform.position,
                    new Vector2(4.59f, 24.09f)
                ) > 0.05f)
            {
                return false;
            }

            return HasSingleComponentNamed(
                       wall.gameObject,
                       "UnityEngine.BoxCollider2D"
                   ) &&
                   HasNativeWallFsmShape(wall);
        }

        private static IEnumerator OpenAqueductWall()
        {
            bool foundIdentity = false;
            bool initializedVariables = false;
            bool validRuntimeObjects = false;
            string lastState = string.Empty;

            for (int frame = 0; frame < WallSearchFrames; frame++)
            {
                if (!IsAqueductRoomBound())
                {
                    yield break;
                }

                if (!TryFindAqueductWall(out PlayMakerFSM wall))
                {
                    yield return null;
                    continue;
                }

                foundIdentity = true;
                if (!TryResolveAqueductRuntimeObjects(
                        wall,
                        out GameObject masks,
                        out GameObject cameraLocks,
                        out bool variablesReady
                    ))
                {
                    initializedVariables |= variablesReady;
                    yield return null;
                    continue;
                }

                initializedVariables = true;
                validRuntimeObjects = true;
                if (HasNativeWallOpened(wall, masks, cameraLocks))
                {
                    yield break;
                }

                lastState = wall.ActiveStateName ?? string.Empty;
                if (string.Equals(
                        lastState,
                        WallIdleState,
                        StringComparison.Ordinal
                    ))
                {
                    wall.SendEvent(WallBreakEvent);
                    yield return VerifyNativeWallRelease(
                        wall,
                        masks,
                        cameraLocks,
                        "Aqueduct_01 left1"
                    );
                    yield break;
                }

                yield return null;
            }

            LogWallOpenFailure(
                "Aqueduct_01 left1",
                foundIdentity,
                initializedVariables,
                validRuntimeObjects,
                lastState
            );
        }

        private static bool TryFindArboriumWall(out PlayMakerFSM wall)
        {
            wall = null;
            int matches = 0;
            foreach (PlayMakerFSM candidate in
                     Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
            {
                if (!IsExactArboriumWall(candidate))
                {
                    continue;
                }

                wall = candidate;
                matches++;
            }

            return matches == 1;
        }

        private static bool IsExactArboriumWall(PlayMakerFSM wall)
        {
            if (wall == null || wall.gameObject == null ||
                !string.Equals(
                    wall.gameObject.scene.name,
                    ArboriumScene,
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    Utils.GetHierarchyPath(wall.transform),
                    ArboriumWallPath,
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    wall.FsmName,
                    ArboriumWallFsm,
                    StringComparison.Ordinal
                ) ||
                Vector2.Distance(
                    wall.transform.position,
                    new Vector2(204.08f, 32.15f)
                ) > 0.05f)
            {
                return false;
            }

            return HasSingleComponentNamed(
                       wall.gameObject,
                       "UnityEngine.BoxCollider2D"
                   ) &&
                   HasNativeWallFsmShape(wall);
        }

        private static IEnumerator OpenArboriumWall()
        {
            bool foundIdentity = false;
            bool initializedVariables = false;
            bool validRuntimeObjects = false;
            string lastState = string.Empty;

            for (int frame = 0; frame < WallSearchFrames; frame++)
            {
                if (!IsRoomBoundScene(
                        HeroController.SilentInstance,
                        ArboriumScene
                    ))
                {
                    yield break;
                }

                if (!TryFindArboriumWall(out PlayMakerFSM wall))
                {
                    yield return null;
                    continue;
                }

                foundIdentity = true;
                if (!TryResolveArboriumRuntimeObjects(
                        wall,
                        out GameObject masks,
                        out GameObject cameraLocks,
                        out bool variablesReady
                    ))
                {
                    initializedVariables |= variablesReady;
                    yield return null;
                    continue;
                }

                initializedVariables = true;
                validRuntimeObjects = true;
                if (HasNativeWallOpened(wall, masks, cameraLocks))
                {
                    yield break;
                }

                lastState = wall.ActiveStateName ?? string.Empty;
                if (string.Equals(
                        lastState,
                        WallIdleState,
                        StringComparison.Ordinal
                    ))
                {
                    wall.SendEvent(WallBreakEvent);
                    yield return VerifyNativeWallRelease(
                        wall,
                        masks,
                        cameraLocks,
                        "Arborium_11 wall"
                    );
                    yield break;
                }

                yield return null;
            }

            LogWallOpenFailure(
                "Arborium_11 wall",
                foundIdentity,
                initializedVariables,
                validRuntimeObjects,
                lastState
            );
        }

        private static bool TryFindShellwoodWall(out PlayMakerFSM wall)
        {
            wall = null;
            int matches = 0;
            foreach (PlayMakerFSM candidate in
                     Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
            {
                if (!IsExactShellwoodWall(candidate))
                {
                    continue;
                }

                wall = candidate;
                matches++;
            }

            return matches == 1;
        }

        private static bool IsExactShellwoodWall(PlayMakerFSM wall)
        {
            if (wall == null || wall.gameObject == null ||
                !string.Equals(
                    wall.gameObject.scene.name,
                    ShellwoodScene,
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    Utils.GetHierarchyPath(wall.transform),
                    ShellwoodWallPath,
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    wall.FsmName,
                    ShellwoodWallFsm,
                    StringComparison.Ordinal
                ) ||
                Vector2.Distance(
                    wall.transform.position,
                    new Vector2(139.059998f, 34.84f)
                ) > 0.01f ||
                wall.gameObject.GetComponents<PlayMakerFSM>().Length != 1 ||
                wall.transform.childCount != 5)
            {
                return false;
            }

            Component wallCollider = GetSingleComponentNamed(
                wall.gameObject,
                "UnityEngine.PolygonCollider2D"
            );
            Component wallRenderer = GetSingleComponentNamed(
                wall.gameObject,
                "UnityEngine.MeshRenderer"
            );
            return TryReadPolygonColliderShape(
                       wallCollider,
                       out bool wallIsTrigger,
                       out int wallPathCount,
                       out int wallPointCount
                   ) &&
                   wallIsTrigger && wallPathCount == 1 &&
                   wallPointCount == 8 && wallRenderer != null &&
                   HasNativeShellwoodWallFsmShape(wall);
        }

        private static bool HasNativeShellwoodWallFsmShape(
            PlayMakerFSM wall
        )
        {
            FsmString playerData = wall.FsmVariables
                ?.FindFsmString(ShellwoodPlayerDataVariable);
            FsmBool fullyBreak = wall.FsmVariables
                ?.FindFsmBool(ShellwoodFullyBreakVariable);
            FsmGameObject damageableRange = wall.FsmVariables
                ?.FindFsmGameObject(ShellwoodDamageableRangeVariable);
            FsmGameObject terrainCollider = wall.FsmVariables
                ?.FindFsmGameObject(ShellwoodTerrainColliderVariable);
            FsmState init = wall.Fsm?.GetState(WallInitState);
            FsmState idle = wall.Fsm?.GetState(WallIdleState);
            FsmState rangeCheck = wall.Fsm?.GetState(
                ShellwoodRangeCheckState
            );
            FsmState heavyBreak = wall.Fsm?.GetState(
                ShellwoodHeavyBreakState
            );
            FsmState hit = wall.Fsm?.GetState(ShellwoodHitState);
            FsmState strikeEffect = wall.Fsm?.GetState(
                ShellwoodStrikeEffectState
            );
            FsmState breakPause = wall.Fsm?.GetState(
                ShellwoodBreakPauseState
            );
            FsmState breakState = wall.Fsm?.GetState(WallBreakState);
            FsmState broken = wall.Fsm?.GetState(ShellwoodBrokenState);
            FsmState fullyBroken = wall.Fsm?.GetState(
                ShellwoodFullyBrokenState
            );

            return playerData != null &&
                   string.Equals(
                       playerData.Value,
                       ShellwoodWallPlayerData,
                       StringComparison.Ordinal
                   ) &&
                   fullyBreak != null && fullyBreak.Value &&
                   damageableRange != null && terrainCollider != null &&
                   init?.Actions?.Length == 17 &&
                   idle?.Actions?.Length == 1 &&
                   HasSingleTransition(
                       idle,
                       ShellwoodBreakEvent,
                       ShellwoodRangeCheckState
                   ) &&
                   rangeCheck?.Actions?.Length == 2 &&
                   HasSingleTransition(
                       rangeCheck,
                       "FINISHED",
                       ShellwoodHeavyBreakState
                   ) &&
                   heavyBreak?.Actions?.Length == 1 &&
                   HasSingleTransition(
                       heavyBreak,
                       "FINISHED",
                       ShellwoodHitState
                   ) &&
                   hit?.Actions?.Length == 9 &&
                   HasSingleTransition(
                       hit,
                       "FINISHED",
                       ShellwoodStrikeEffectState
                   ) &&
                   strikeEffect?.Actions?.Length == 4 &&
                   HasSingleTransition(
                       strikeEffect,
                       ShellwoodBreakEvent,
                       ShellwoodBreakPauseState
                   ) &&
                   breakPause?.Actions?.Length == 4 &&
                   HasSingleTransition(
                       breakPause,
                       "FINISHED",
                       WallBreakState
                   ) &&
                   breakState?.Actions?.Length == 12 &&
                   HasSingleTransition(
                       breakState,
                       "FINISHED",
                       ShellwoodBrokenState
                   ) &&
                   broken?.Actions?.Length == 12 &&
                   HasSingleTransition(
                       broken,
                       ShellwoodBreakEvent,
                       ShellwoodFullyBrokenState
                   ) &&
                   fullyBroken?.Actions?.Length == 1;
        }

        private static bool HasSingleTransition(
            FsmState state,
            string eventName,
            string targetState
        )
        {
            return state?.Transitions != null &&
                   state.Transitions.Count(transition =>
                       string.Equals(
                           transition.EventName,
                           eventName,
                           StringComparison.Ordinal
                       ) &&
                       string.Equals(
                           transition.ToState,
                           targetState,
                           StringComparison.Ordinal
                       )) == 1;
        }

        private static IEnumerator OpenShellwoodWall()
        {
            bool foundIdentity = false;
            bool validRuntimeObjects = false;
            string lastState = string.Empty;

            for (int frame = 0; frame < WallSearchFrames; frame++)
            {
                if (!IsShellwoodRoomBound())
                {
                    yield break;
                }

                if (!TryFindShellwoodWall(out PlayMakerFSM wall))
                {
                    yield return null;
                    continue;
                }

                foundIdentity = true;
                if (!TryResolveShellwoodWallObjects(
                        wall,
                        out Component wallCollider,
                        out Component wallRenderer,
                        out GameObject terrainCollider,
                        out GameObject remasker
                    ))
                {
                    yield return null;
                    continue;
                }

                validRuntimeObjects = true;
                if (HasNativeShellwoodWallOpened(
                        wall,
                        wallCollider,
                        wallRenderer,
                        terrainCollider,
                        remasker
                    ))
                {
                    yield break;
                }

                lastState = wall.ActiveStateName ?? string.Empty;
                if (string.Equals(
                        lastState,
                        WallIdleState,
                        StringComparison.Ordinal
                    ))
                {
                    wall.SendEvent(ShellwoodBreakEvent);
                    yield return VerifyNativeShellwoodWallRelease(
                        wall,
                        wallCollider,
                        wallRenderer,
                        terrainCollider,
                        remasker
                    );
                    yield break;
                }

                yield return null;
            }

            RandomizerPlugin.Log?.LogWarning(
                "[RANDOMIZER] Could not open Shellwood_13 right1: " +
                (foundIdentity
                    ? validRuntimeObjects
                        ? "wall FSM never reached Idle (last state '" +
                          lastState + "')."
                        : "the native wall objects did not initialize."
                    : "the matching native wall was not found.")
            );
        }

        private static bool TryResolveShellwoodWallObjects(
            PlayMakerFSM wall,
            out Component wallCollider,
            out Component wallRenderer,
            out GameObject terrainCollider,
            out GameObject remasker
        )
        {
            wallCollider = wall == null
                ? null
                : GetSingleComponentNamed(
                    wall.gameObject,
                    "UnityEngine.PolygonCollider2D"
                );
            wallRenderer = wall == null
                ? null
                : GetSingleComponentNamed(
                    wall.gameObject,
                    "UnityEngine.MeshRenderer"
                );
            terrainCollider = null;
            remasker = null;
            if (wall == null || wallCollider == null ||
                wallRenderer == null ||
                !TryGetSingleDirectChild(
                    wall.transform,
                    ShellwoodTerrainCollider,
                    out Transform terrainTransform
                ) ||
                terrainTransform.childCount != 4 ||
                Vector2.Distance(
                    terrainTransform.localPosition,
                    Vector2.zero
                ) > 0.001f)
            {
                return false;
            }

            Component terrainBox = GetSingleComponentNamed(
                terrainTransform.gameObject,
                "UnityEngine.BoxCollider2D"
            );
            if (!TryReadBoxColliderShape(
                    terrainBox,
                    out bool terrainIsTrigger,
                    out Vector2 terrainOffset,
                    out Vector2 terrainSize
                ) ||
                terrainIsTrigger ||
                Vector2.Distance(
                    terrainOffset,
                    new Vector2(0.14f, -0.4372468f)
                ) > 0.001f ||
                Vector2.Distance(
                    terrainSize,
                    new Vector2(2.35f, 8.021665f)
                ) > 0.001f ||
                !TryGetSingleDirectChild(
                    terrainTransform,
                    ShellwoodRemasker,
                    out Transform remaskerTransform
                ) ||
                remaskerTransform.childCount != 2 ||
                Vector2.Distance(
                    remaskerTransform.localPosition,
                    new Vector2(6.9f, -0.57f)
                ) > 0.001f)
            {
                return false;
            }

            Component remaskerBox = GetSingleComponentNamed(
                remaskerTransform.gameObject,
                "UnityEngine.BoxCollider2D"
            );
            FsmGameObject damageableRange = wall.FsmVariables
                ?.FindFsmGameObject(ShellwoodDamageableRangeVariable);
            FsmGameObject terrainVariable = wall.FsmVariables
                ?.FindFsmGameObject(ShellwoodTerrainColliderVariable);
            if (!TryReadBoxColliderShape(
                    remaskerBox,
                    out bool remaskerIsTrigger,
                    out Vector2 remaskerOffset,
                    out Vector2 remaskerSize
                ) ||
                !remaskerIsTrigger ||
                Vector2.Distance(
                    remaskerOffset,
                    new Vector2(1.107916f, 0f)
                ) > 0.001f ||
                Vector2.Distance(
                    remaskerSize,
                    new Vector2(2.894306f, 0.828125f)
                ) > 0.001f ||
                !HasSingleComponentNamed(
                    remaskerTransform.gameObject,
                    "Remasker"
                ) ||
                !HasSingleComponentNamed(
                    remaskerTransform.gameObject,
                    "PersistentBoolItem"
                ) ||
                damageableRange == null ||
                damageableRange.Value != null ||
                terrainVariable == null ||
                terrainVariable.Value != terrainTransform.gameObject)
            {
                return false;
            }

            terrainCollider = terrainTransform.gameObject;
            remasker = remaskerTransform.gameObject;
            return true;
        }

        private static bool HasNativeShellwoodWallOpened(
            PlayMakerFSM wall,
            Component wallCollider,
            Component wallRenderer,
            GameObject terrainCollider,
            GameObject remasker
        )
        {
            return wall != null &&
                   string.Equals(
                       wall.ActiveStateName,
                       ShellwoodFullyBrokenState,
                       StringComparison.Ordinal
                   ) &&
                   wallCollider is Behaviour wallColliderBehaviour &&
                   !wallColliderBehaviour.enabled &&
                   wallRenderer is Renderer wallRendererComponent &&
                   !wallRendererComponent.enabled &&
                   terrainCollider != null &&
                   !terrainCollider.activeInHierarchy &&
                   remasker != null && !remasker.activeInHierarchy;
        }

        private static IEnumerator VerifyNativeShellwoodWallRelease(
            PlayMakerFSM wall,
            Component wallCollider,
            Component wallRenderer,
            GameObject terrainCollider,
            GameObject remasker
        )
        {
            for (int frame = 0; frame < WallReleaseFrames; frame++)
            {
                if (HasNativeShellwoodWallOpened(
                        wall,
                        wallCollider,
                        wallRenderer,
                        terrainCollider,
                        remasker
                    ))
                {
                    RandomizerPlugin.Log?.LogInfo(
                        "[RANDOMIZER] Opened Shellwood_13 right1 with " +
                        "its native wall FSM."
                    );
                    yield break;
                }

                yield return null;
            }

            RandomizerPlugin.Log?.LogWarning(
                "[RANDOMIZER] Shellwood_13 right1 native wall did not " +
                "settle (state='" +
                (wall?.ActiveStateName ?? string.Empty) +
                "', wall collider enabled=" +
                (wallCollider is Behaviour wallColliderBehaviour &&
                 wallColliderBehaviour.enabled) +
                ", wall renderer enabled=" +
                (wallRenderer is Renderer wallRendererComponent &&
                 wallRendererComponent.enabled) +
                ", terrain active=" +
                (terrainCollider != null &&
                 terrainCollider.activeInHierarchy) +
                ", remasker active=" +
                (remasker != null && remasker.activeInHierarchy) + ")."
            );
        }

        private static bool TryFindPeakWall(out Breakable wall)
        {
            wall = null;
            int matches = 0;
            foreach (Breakable candidate in
                     Resources.FindObjectsOfTypeAll<Breakable>())
            {
                if (!IsExactPeakWall(candidate))
                {
                    continue;
                }

                wall = candidate;
                matches++;
            }

            return matches == 1;
        }

        private static bool IsExactPeakWall(Breakable wall)
        {
            if (wall == null || wall.gameObject == null ||
                !string.Equals(
                    wall.gameObject.scene.name,
                    PeakScene,
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    Utils.GetHierarchyPath(wall.transform),
                    PeakWallPath,
                    StringComparison.Ordinal
                ) ||
                Vector2.Distance(
                    wall.transform.position,
                    new Vector2(32.579811f, 8.444838f)
                ) > 0.01f ||
                wall.gameObject.GetComponents<Breakable>().Length != 1 ||
                !HasSingleComponentNamed(
                    wall.gameObject,
                    "NonBouncer"
                ))
            {
                return false;
            }

            return TryResolvePeakWallObjects(
                wall,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _
            );
        }

        private static IEnumerator OpenPeakWall()
        {
            for (int frame = 0; frame < WallSearchFrames; frame++)
            {
                if (!IsPeakRoomBound())
                {
                    yield break;
                }

                if (!TryFindPeakWall(out Breakable wall) ||
                    !TryResolvePeakWallObjects(
                        wall,
                        out Component wallCollider,
                        out PersistentBoolItem persistent,
                        out GameObject wholePart,
                        out GameObject terrainCollider,
                        out GameObject cameraLock,
                        out GameObject remasker
                    ))
                {
                    yield return null;
                    continue;
                }

                if (HasNativePeakWallOpened(
                        wall,
                        wallCollider,
                        wholePart,
                        terrainCollider,
                        cameraLock,
                        remasker
                    ))
                {
                    yield break;
                }

                wall.BreakSelf();
                yield return VerifyNativePeakWallRelease(
                    wall,
                    wallCollider,
                    persistent,
                    wholePart,
                    terrainCollider,
                    cameraLock,
                    remasker
                );
                yield break;
            }

            RandomizerPlugin.Log?.LogWarning(
                "[RANDOMIZER] Could not open Peak_04d left1: " +
                "the matching native Breakable was not found."
            );
        }

        private static bool TryResolvePeakWallObjects(
            Breakable wall,
            out Component wallCollider,
            out PersistentBoolItem persistent,
            out GameObject wholePart,
            out GameObject terrainCollider,
            out GameObject cameraLock,
            out GameObject remasker
        )
        {
            wallCollider = wall == null
                ? null
                : GetSingleComponentNamed(
                    wall.gameObject,
                    "UnityEngine.BoxCollider2D"
                );
            persistent = wall?.GetComponent<PersistentBoolItem>();
            wholePart = null;
            terrainCollider = null;
            cameraLock = null;
            remasker = null;
            if (wall == null || wallCollider == null ||
                persistent == null ||
                wall.gameObject.GetComponents<PersistentBoolItem>()
                    .Length != 1 ||
                !TryReadBoxColliderShape(
                    wallCollider,
                    out bool wallIsTrigger,
                    out Vector2 wallOffset,
                    out Vector2 wallSize
                ) ||
                !wallIsTrigger ||
                Vector2.Distance(
                    wallOffset,
                    new Vector2(-0.02312851f, 0.1389637f)
                ) > 0.001f ||
                Vector2.Distance(
                    wallSize,
                    new Vector2(5.490898f, 11.662786f)
                ) > 0.001f ||
                (wall.transform.childCount != 4 &&
                 (!wall.IsBroken || wall.transform.childCount != 2)) ||
                !string.Equals(
                    persistent.ItemData.ID,
                    PeakWallPath,
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    persistent.ItemData.SceneName,
                    PeakScene,
                    StringComparison.Ordinal
                ) ||
                persistent.ItemData.IsSemiPersistent)
            {
                return false;
            }

            GameObject[] wholeParts;
            PersistentBoolItem configuredPersistent;
            int hitsToBreak;
            bool ignoreDamagers;
            bool immuneToBreakableBreaker;
            try
            {
                Traverse fields = Traverse.Create(wall);
                wholeParts = fields.Field("wholeParts")
                    .GetValue<GameObject[]>();
                configuredPersistent = fields.Field("persistent")
                    .GetValue<PersistentBoolItem>();
                hitsToBreak = fields.Field("hitsToBreak")
                    .GetValue<int>();
                ignoreDamagers = fields.Field("ignoreDamagers")
                    .GetValue<bool>();
                immuneToBreakableBreaker = fields
                    .Field("immuneToBreakableBreaker")
                    .GetValue<bool>();
            }
            catch (Exception)
            {
                return false;
            }

            if (wholeParts == null || wholeParts.Length != 1 ||
                configuredPersistent != persistent ||
                hitsToBreak != 6 || ignoreDamagers ||
                !immuneToBreakableBreaker ||
                !TryGetSingleDirectChild(
                    wall.transform,
                    PeakWholePart,
                    out Transform wholeTransform
                ) ||
                wholeParts[0] != wholeTransform.gameObject ||
                wholeTransform.childCount != 24 ||
                Vector2.Distance(
                    wholeTransform.localPosition,
                    new Vector2(-0.37f, -0.32f)
                ) > 0.001f)
            {
                return false;
            }

            wholePart = wholeTransform.gameObject;
            if (!TryGetSingleDirectChild(
                    wholeTransform,
                    PeakTerrainCollider,
                    out Transform terrainTransform
                ) ||
                !TryGetSingleDirectChild(
                    wholeTransform,
                    PeakCameraLock,
                    out Transform cameraTransform
                ) ||
                !TryGetSingleDirectChild(
                    wholeTransform,
                    PeakRemasker,
                    out Transform remaskerTransform
                ))
            {
                return false;
            }

            Component terrain = GetSingleComponentNamed(
                terrainTransform.gameObject,
                "UnityEngine.BoxCollider2D"
            );
            CameraLockArea nativeCameraLock = cameraTransform
                .GetComponent<CameraLockArea>();
            Component cameraCollider = GetSingleComponentNamed(
                cameraTransform.gameObject,
                "UnityEngine.BoxCollider2D"
            );
            Component remaskerCollider = GetSingleComponentNamed(
                remaskerTransform.gameObject,
                "UnityEngine.BoxCollider2D"
            );
            if (!TryReadBoxColliderShape(
                    terrain,
                    out bool terrainIsTrigger,
                    out _,
                    out _
                ) || terrainIsTrigger ||
                !HasSingleComponentNamed(
                    terrainTransform.gameObject,
                    "NonThunker"
                ) ||
                nativeCameraLock == null || cameraCollider == null ||
                cameraTransform.gameObject
                    .GetComponents<CameraLockArea>().Length != 1 ||
                !TryReadBoxColliderShape(
                    cameraCollider,
                    out bool cameraIsTrigger,
                    out _,
                    out _
                ) || !cameraIsTrigger ||
                Math.Abs(nativeCameraLock.cameraXMin - 43.73f) > 0.01f ||
                Math.Abs(nativeCameraLock.cameraYMin - 9.6f) > 0.01f ||
                !HasExpectedPeakCameraXMax(nativeCameraLock) ||
                Math.Abs(nativeCameraLock.cameraYMax - 9.6f) > 0.01f ||
                nativeCameraLock.priority != 1 ||
                !TryReadBoxColliderShape(
                    remaskerCollider,
                    out bool remaskerIsTrigger,
                    out _,
                    out _
                ) || !remaskerIsTrigger ||
                !HasSingleComponentNamed(
                    remaskerTransform.gameObject,
                    "Remasker"
                ) ||
                !HasSingleComponentNamed(
                    remaskerTransform.gameObject,
                    "PersistentBoolItem"
                ))
            {
                return false;
            }

            terrainCollider = terrainTransform.gameObject;
            cameraLock = cameraTransform.gameObject;
            remasker = remaskerTransform.gameObject;
            return true;
        }

        private static bool HasExpectedPeakCameraXMax(
            CameraLockArea cameraLock
        )
        {
            if (cameraLock == null)
            {
                return false;
            }

            if (Math.Abs(cameraLock.cameraXMax + 1f) <= 0.01f)
            {
                return true;
            }

            CameraController cameraController =
                GameManager.instance?.cameraCtrl;
            return cameraController != null &&
                   Math.Abs(
                       cameraLock.cameraXMax - cameraController.xLimit
                   ) <= 0.01f;
        }

        private static bool TryGetSingleDirectChild(
            Transform parent,
            string childName,
            out Transform child
        )
        {
            child = null;
            if (parent == null)
            {
                return false;
            }

            Transform[] matches = parent.Cast<Transform>()
                .Where(candidate =>
                    string.Equals(
                        candidate.name,
                        childName,
                        StringComparison.Ordinal
                    ))
                .ToArray();
            if (matches.Length != 1)
            {
                return false;
            }

            child = matches[0];
            return child.parent == parent;
        }

        private static bool TryReadBoxColliderShape(
            Component collider,
            out bool isTrigger,
            out Vector2 offset,
            out Vector2 size
        )
        {
            isTrigger = false;
            offset = default;
            size = default;
            if (collider == null ||
                !string.Equals(
                    collider.GetType().FullName,
                    "UnityEngine.BoxCollider2D",
                    StringComparison.Ordinal
                ))
            {
                return false;
            }

            try
            {
                Traverse properties = Traverse.Create(collider);
                isTrigger = properties.Property("isTrigger")
                    .GetValue<bool>();
                offset = properties.Property("offset")
                    .GetValue<Vector2>();
                size = properties.Property("size")
                    .GetValue<Vector2>();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryReadPolygonColliderShape(
            Component collider,
            out bool isTrigger,
            out int pathCount,
            out int pointCount
        )
        {
            isTrigger = false;
            pathCount = 0;
            pointCount = 0;
            if (collider == null ||
                !string.Equals(
                    collider.GetType().FullName,
                    "UnityEngine.PolygonCollider2D",
                    StringComparison.Ordinal
                ))
            {
                return false;
            }

            try
            {
                Traverse properties = Traverse.Create(collider);
                isTrigger = properties.Property("isTrigger")
                    .GetValue<bool>();
                pathCount = properties.Property("pathCount")
                    .GetValue<int>();
                Vector2[] points = properties.Property("points")
                    .GetValue<Vector2[]>();
                pointCount = points?.Length ?? 0;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool HasNativePeakWallOpened(
            Breakable wall,
            Component wallCollider,
            GameObject wholePart,
            GameObject terrainCollider,
            GameObject cameraLock,
            GameObject remasker
        )
        {
            return wall != null && wall.IsBroken &&
                   wallCollider is Behaviour wallBehaviour &&
                   !wallBehaviour.enabled &&
                   wholePart != null && !wholePart.activeSelf &&
                   terrainCollider != null &&
                   !terrainCollider.activeInHierarchy &&
                   cameraLock != null && !cameraLock.activeInHierarchy &&
                   remasker != null && !remasker.activeInHierarchy;
        }

        private static IEnumerator VerifyNativePeakWallRelease(
            Breakable wall,
            Component wallCollider,
            PersistentBoolItem persistent,
            GameObject wholePart,
            GameObject terrainCollider,
            GameObject cameraLock,
            GameObject remasker
        )
        {
            for (int frame = 0; frame < WallReleaseFrames; frame++)
            {
                if (HasNativePeakWallOpened(
                        wall,
                        wallCollider,
                        wholePart,
                        terrainCollider,
                        cameraLock,
                        remasker
                    ))
                {
                    RandomizerPlugin.Log?.LogInfo(
                        "[RANDOMIZER] Opened Peak_04d left1 with its " +
                        "native Breakable."
                    );
                    yield break;
                }

                yield return null;
            }

            RandomizerPlugin.Log?.LogWarning(
                "[RANDOMIZER] Peak_04d left1 native Breakable did not " +
                "settle (broken=" + (wall != null && wall.IsBroken) +
                ", persistence=" +
                (persistent != null && persistent.GetCurrentValue()) +
                ", wall collider enabled=" +
                (wallCollider is Behaviour wallBehaviour &&
                 wallBehaviour.enabled) +
                ", whole part active=" +
                (wholePart != null && wholePart.activeSelf) +
                ", terrain active=" +
                (terrainCollider != null &&
                 terrainCollider.activeInHierarchy) +
                ", camera lock active=" +
                (cameraLock != null && cameraLock.activeInHierarchy) +
                ", remasker active=" +
                (remasker != null && remasker.activeInHierarchy) + ")."
            );
        }

        private static bool HasNativeWallFsmShape(PlayMakerFSM wall)
        {
            FsmState init = wall.Fsm?.GetState(WallInitState);
            FsmState idle = wall.Fsm?.GetState(WallIdleState);
            FsmState breakState = wall.Fsm?.GetState(WallBreakState);
            FsmGameObject masksVariable = wall.FsmVariables
                ?.FindFsmGameObject(WallMasksVariable);
            FsmGameObject cameraLocksVariable = wall.FsmVariables
                ?.FindFsmGameObject(WallCameraLocksVariable);

            return masksVariable != null &&
                   cameraLocksVariable != null &&
                   init?.Actions != null &&
                   init.Actions.Length == 15 &&
                   init.Actions.Count(action =>
                       action != null &&
                       string.Equals(
                           action.GetType().FullName,
                           "HutongGames.PlayMaker.Actions.SetParent",
                           StringComparison.Ordinal
                       )) == 2 &&
                   idle?.Transitions != null &&
                   idle.Transitions.Count(transition =>
                       string.Equals(
                           transition.EventName,
                           WallBreakEvent,
                           StringComparison.Ordinal
                       ) &&
                       string.Equals(
                           transition.ToState,
                           WallBreakState,
                           StringComparison.Ordinal
                       )) == 1 &&
                   breakState?.Actions != null &&
                   breakState.Actions.Length == 17;
        }

        private static bool TryResolveAqueductRuntimeObjects(
            PlayMakerFSM wall,
            out GameObject masks,
            out GameObject cameraLocks,
            out bool variablesInitialized
        )
        {
            return TryResolveRuntimeWallObjects(
                       wall,
                       AqueductScene,
                       out masks,
                       out cameraLocks,
                       out variablesInitialized
                   ) &&
                   HasExactCameraLock(
                       cameraLocks,
                       AqueductScene,
                       204.15f,
                       1
                   );
        }

        private static bool TryResolveArboriumRuntimeObjects(
            PlayMakerFSM wall,
            out GameObject masks,
            out GameObject cameraLocks,
            out bool variablesInitialized
        )
        {
            return TryResolveRuntimeWallObjects(
                       wall,
                       ArboriumScene,
                       out masks,
                       out cameraLocks,
                       out variablesInitialized
                   ) &&
                   HasExactCameraLock(
                       cameraLocks,
                       ArboriumScene,
                       207f,
                       1
                   );
        }

        private static bool TryResolveRuntimeWallObjects(
            PlayMakerFSM wall,
            string sceneName,
            out GameObject masks,
            out GameObject cameraLocks,
            out bool variablesInitialized
        )
        {
            FsmGameObject masksVariable = wall.FsmVariables
                ?.FindFsmGameObject(WallMasksVariable);
            FsmGameObject cameraLocksVariable = wall.FsmVariables
                ?.FindFsmGameObject(WallCameraLocksVariable);
            masks = masksVariable?.Value;
            cameraLocks = cameraLocksVariable?.Value;
            variablesInitialized = masks != null && cameraLocks != null;
            if (!variablesInitialized)
            {
                return false;
            }

            return IsExactDetachedWallObject(
                       wall,
                       masks,
                       sceneName,
                       WallMasksVariable
                   ) &&
                   IsExactDetachedWallObject(
                       wall,
                       cameraLocks,
                       sceneName,
                       WallCameraLocksVariable
                   );
        }

        private static bool IsExactDetachedWallObject(
            PlayMakerFSM wall,
            GameObject candidate,
            string sceneName,
            string expectedName
        )
        {
            return candidate != null &&
                   candidate.transform.parent == null &&
                   string.Equals(
                       candidate.name,
                       expectedName,
                       StringComparison.Ordinal
                   ) &&
                   string.Equals(
                       candidate.scene.name,
                       sceneName,
                       StringComparison.Ordinal
                   ) &&
                   Vector2.Distance(
                       candidate.transform.position,
                       wall.transform.position
                   ) <= 0.05f;
        }

        private static bool HasExactCameraLock(
            GameObject cameraLocks,
            string sceneName,
            float expectedXMax,
            int expectedPriority
        )
        {
            Transform lockTransform = cameraLocks?.transform.Find(
                "CameraLockArea (3)"
            );
            CameraLockArea cameraLock = lockTransform
                ?.GetComponent<CameraLockArea>();
            return cameraLock != null &&
                   lockTransform.parent == cameraLocks.transform &&
                   string.Equals(
                       cameraLock.gameObject.scene.name,
                       sceneName,
                       StringComparison.Ordinal
                   ) &&
                   Math.Abs(cameraLock.cameraXMin - 52.9f) < 0.05f &&
                   Math.Abs(cameraLock.cameraXMax - expectedXMax) < 0.05f &&
                   cameraLock.priority == expectedPriority;
        }

        private static bool HasNativeWallOpened(
            PlayMakerFSM wall,
            GameObject masks,
            GameObject cameraLocks
        )
        {
            Component wallCollider = wall == null
                ? null
                : GetSingleComponentNamed(
                    wall.gameObject,
                    "UnityEngine.BoxCollider2D"
                );
            bool colliderDisabled = wallCollider == null ||
                                      wallCollider is Behaviour behaviour &&
                                      !behaviour.enabled;
            return masks != null &&
                   !masks.activeSelf &&
                   cameraLocks != null &&
                   !cameraLocks.activeSelf &&
                   colliderDisabled;
        }

        private static IEnumerator VerifyNativeWallRelease(
            PlayMakerFSM wall,
            GameObject masks,
            GameObject cameraLocks,
            string routeName
        )
        {
            for (int frame = 0; frame < WallReleaseFrames; frame++)
            {
                if (HasNativeWallOpened(wall, masks, cameraLocks))
                {
                    RandomizerPlugin.Log?.LogInfo(
                        "[RANDOMIZER] Opened " + routeName +
                        " through native QUICK BREAK."
                    );
                    yield break;
                }

                yield return null;
            }

            RandomizerPlugin.Log?.LogWarning(
                "[RANDOMIZER] " + routeName +
                " native QUICK BREAK was sent, but the native wall " +
                "outcomes did not settle (Masks active=" +
                (masks != null && masks.activeSelf) +
                ", Camera Locks active=" +
                (cameraLocks != null && cameraLocks.activeSelf) +
                ", collider enabled=" +
                IsWallColliderEnabled(wall) + ")."
            );
        }

        private static bool IsWallColliderEnabled(PlayMakerFSM wall)
        {
            Component wallCollider = wall == null
                ? null
                : GetSingleComponentNamed(
                    wall.gameObject,
                    "UnityEngine.BoxCollider2D"
                );
            return wallCollider is Behaviour behaviour && behaviour.enabled;
        }

        private static void LogWallOpenFailure(
            string routeName,
            bool foundIdentity,
            bool initializedVariables,
            bool validRuntimeObjects,
            string lastState
        )
        {
            string detail;
            if (!foundIdentity)
            {
                detail = "matching wall was not found";
            }
            else if (!initializedVariables)
            {
                detail = "runtime Masks/Camera Locks variables never " +
                         "initialized";
            }
            else if (!validRuntimeObjects)
            {
                detail = "initialized Masks/Camera Locks references did " +
                         "not match the expected detached roots";
            }
            else
            {
                detail = "wall FSM never reached Idle (last state '" +
                         lastState + "')";
            }

            RandomizerPlugin.Log?.LogWarning(
                "[RANDOMIZER] Could not open " + routeName + ": " +
                detail + "."
            );
        }

        private static bool HasSingleComponentNamed(
            GameObject gameObject,
            string typeName
        )
        {
            return GetSingleComponentNamed(gameObject, typeName) != null;
        }

        private static Component GetSingleComponentNamed(
            GameObject gameObject,
            string typeName
        )
        {
            if (gameObject == null)
            {
                return null;
            }

            Component[] matches = gameObject.GetComponents<Component>()
                .Where(component =>
                    component != null &&
                    string.Equals(
                        component.GetType().FullName,
                        typeName,
                        StringComparison.Ordinal
                    ))
                .ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        [HarmonyPatch(typeof(HeroController), "SendHeroInPosition")]
        private static class RouteArrivalPatch
        {
            [HarmonyPostfix]
            private static void Postfix(HeroController __instance)
            {
                if (IsArboriumArrival(__instance))
                {
                    __instance.StartCoroutine(OpenArboriumWall());
                }

                if (IsAqueductArrival(__instance))
                {
                    __instance.StartCoroutine(OpenAqueductWall());
                }

                if (IsPeakArrival(__instance))
                {
                    __instance.StartCoroutine(OpenPeakWall());
                }

                if (IsShellwoodArrival(__instance))
                {
                    __instance.StartCoroutine(OpenShellwoodWall());
                }
            }
        }
    }
}
