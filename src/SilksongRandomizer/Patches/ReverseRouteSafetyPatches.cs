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
        [HarmonyPatch(typeof(TempGate), "SetStartingState")]
        private static class SaunaReturnButton
        {
            private static void Prefix(TempGate __instance, ref PersistentBoolItem ___readPersistent,
                out PersistentBoolItem __state)
            {
                __state = null;
                SaveState save = SaveState.Instance;
                if (save?.IsRoomBound != true || __instance.gameObject.scene.name != "Dock_04" ||
                    Utils.GetHierarchyPath(__instance.transform) != "dock_steam_door") return;
                if (HeroController.instance?.GetEntryGateName() == "right3")
                    save.saunaReturnButtonUnlocked = true;
                if (!save.saunaReturnButtonUnlocked) return;
                __state = ___readPersistent;
                ___readPersistent = null;
            }

            private static void Postfix(ref PersistentBoolItem ___readPersistent, PersistentBoolItem __state)
            {
                if (__state != null) ___readPersistent = __state;
            }
        }

        [HarmonyPatch(typeof(HeroController), nameof(HeroController.EnterScene))]
        private static class BrokenBellReturnEntry
        {
            private static void Prefix(TransitionPoint enterGate)
            {
                if (SaveState.Instance?.IsRoomBound != true || enterGate == null ||
                    enterGate.gameObject.scene.name != "Room_Diving_Bell_Abyss" ||
                    enterGate.name != "left1") return;
                enterGate.entryOffset = new Vector2(3f, enterGate.entryOffset.y);
            }
        }

        private const string AqueductScene = "Aqueduct_01";
        private const string AqueductWallPath =
            "Breakable Wall Aqueduct Start";
        private const string ArboriumScene = "Arborium_11";
        private const string ArboriumWallPath = "Breakable Wall (1)";
        private const string ArboriumWallFsm = "breakable_wall_v2";
        private const string WormwaysScene = "Crawl_09";
        private const string WormwaysWallPath = "Breakable Wall (1)";
        private const string PeakScene = "Peak_04d";
        private const string PeakWallPath = "junk_pile_break_peak";
        private const string PeakWholePart = "junk_wall_break_pile";
        private const string PeakTerrainCollider =
            "terrain collider non slider";
        private const string PeakCameraLock = "CameraLockArea";
        private const string PeakRemasker = "Remasker New Sharp Ultra";
        private const string ShellwoodScene = "Shellwood_13";
        private const string ShellwoodWallPath = "Bell Wall Tall";
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

        private static bool IsFarFieldsArrival(HeroController hero)
        {
            return IsRoomBoundScene(hero, "Bone_East_11") &&
                (hero.GetEntryGateName() == "left1" || hero.GetEntryGateName() == "right1");
        }

        private static bool IsSethArrival(HeroController hero)
        {
            return IsRoomBoundScene(hero, "Shellwood_22") &&
                (hero.GetEntryGateName() == "door1" || hero.GetEntryGateName() == "right1");
        }

        private static IEnumerator RevealGatedEntry(bool farFields)
        {
            string scene = farFields ? "Bone_East_11" : "Shellwood_22";
            string rootPath = farFields
                ? "Bone East 11 Cross Over Group/Gate"
                : "Boss Scene/Flower Gate/vine_wall";
            string firstName = farFields ? "CameraLockArea (1)" : "CameraLockArea (4)";
            string secondName = farFields ? "CameraLockArea (2)" : "CameraLockArea (5)";
            for (int frame = 0; frame < WallSearchFrames; frame++)
            {
                HeroController hero = HeroController.SilentInstance;
                if (!(farFields ? IsFarFieldsArrival(hero) : IsSethArrival(hero))) yield break;
                CameraLockArea[] locks = Resources.FindObjectsOfTypeAll<CameraLockArea>()
                    .Where(area => area != null && area.gameObject.scene.name == scene &&
                        (Utils.GetHierarchyPath(area.transform) == rootPath + "/" + firstName ||
                         Utils.GetHierarchyPath(area.transform) == rootPath + "/" + secondName)).ToArray();
                if (locks.Length != 2)
                {
                    yield return null;
                    continue;
                }

                if (farFields)
                {
                    Remasker[] masks = Resources.FindObjectsOfTypeAll<Remasker>()
                        .Where(mask => mask != null && mask.gameObject.scene.name == scene &&
                            Utils.GetHierarchyPath(mask.transform) == rootPath + "/centre_mask/Remasker New Sharp").ToArray();
                    if (masks.Length != 1)
                    {
                        yield return null;
                        continue;
                    }
                    masks[0].enabled = false;
                    masks[0].AlphaSelf = 1f;
                }

                foreach (CameraLockArea area in locks) area.gameObject.SetActive(false);
                yield break;
            }
            RandomizerPlugin.Log?.LogWarning("[RANDOMIZER] Could not resolve " + scene + " entry camera locks.");
        }

        private static bool IsWormwaysArrival(HeroController hero)
        {
            return IsRoomBoundScene(hero, WormwaysScene) &&
                   hero.GetEntryGateName() == "left1";
        }

        private static IEnumerator RevealSideEntry(string scene, string gate)
        {
            yield return null;
            for (int frame = 0; frame < WallSearchFrames; frame++)
            {
                HeroController hero = HeroController.SilentInstance;
                if (!IsRoomBoundScene(hero, scene) || hero.GetEntryGateName() != gate)
                    yield break;
                if (!TryResolveEntryVisuals(scene, out GameObject[] covers, out GameObject[] locks))
                {
                    yield return null;
                    continue;
                }
                foreach (GameObject cover in covers) cover.SetActive(false);
                foreach (GameObject cameraLock in locks) cameraLock.SetActive(false);
                yield return null;
                if (!IsRoomBoundScene(hero, scene) || hero.GetEntryGateName() != gate)
                    yield break;
                foreach (GameObject cover in covers)
                    if (cover != null) cover.SetActive(false);
                foreach (GameObject cameraLock in locks)
                    if (cameraLock != null) cameraLock.SetActive(false);
                RandomizerPlugin.Log?.LogInfo("[RANDOMIZER] Disabled " + covers.Length +
                    " wall cover groups and " + locks.Length + " camera lock groups in " + scene + " " + gate + ".");
                yield break;
            }
            RandomizerPlugin.Log?.LogWarning("[RANDOMIZER] Could not resolve entry covers and camera locks in " +
                scene + " " + gate + ".");
        }

        private static bool TryResolveEntryVisuals(string scene, out GameObject[] covers,
            out GameObject[] locks)
        {
            covers = null;
            locks = null;
            Transform[] objects = Resources.FindObjectsOfTypeAll<Transform>()
                .Where(candidate => candidate != null && candidate.gameObject.scene.name == scene).ToArray();
            GameObject Find(string path)
            {
                Transform[] matches = objects.Where(
                    candidate => Utils.GetHierarchyPath(candidate) == path).Take(2).ToArray();
                return matches.Length == 1 ? matches[0].gameObject : null;
            }
            if (scene == WormwaysScene || scene == "Bone_16")
            {
                GameObject[] walls = scene == "Bone_16"
                    ? new[] { Find("Breakable Wall (1)"), Find("Breakable Wall (2)") }
                    : new[] { Find("Breakable Wall"), Find(WormwaysWallPath) };
                if (walls.Any(wall => wall == null)) return false;
                PlayMakerFSM[] fsms = walls.Select(wall => wall.GetComponents<PlayMakerFSM>()
                    .FirstOrDefault(fsm => fsm.FsmName == ArboriumWallFsm)).ToArray();
                covers = fsms.Select(fsm => fsm?.FsmVariables?.FindFsmGameObject(WallMasksVariable)?.Value).ToArray();
                locks = fsms.Select(fsm => fsm?.FsmVariables?.FindFsmGameObject(WallCameraLocksVariable)?.Value).ToArray();
            }
            else if (scene == ShellwoodScene)
            {
                covers = new[] {
                    "Bell Wall Tall/Terrain Collider/Remasker New",
                    "Bell Wall Tall (1)/Terrain Collider/Remasker New",
                    "Bell Wall Tall (1)/Terrain Collider/Remasker New (1)",
                    "Bell Wall Tall (2)/Terrain Collider/Remasker New",
                    "Bell Wall Tall (2)/Terrain Collider/Remasker New (1)"
                }.Select(Find).ToArray();
                locks = new[] {
                    "Bell Wall Tall (1)/Terrain Collider/CameraLockArea (10)",
                    "Bell Wall Tall (2)/Terrain Collider/CameraLockArea (10)",
                    "Bell Wall Tall (2)/Terrain Collider/CameraLockArea (8)"
                }.Select(Find).ToArray();
            }
            else if (scene == "Shellwood_25")
            {
                covers = new[] {
                    "Shellwood Twig Wall (3)/Mask",
                    "Shellwood Twig Wall (2)/Mask",
                    "Shellwood Twig Wall (1)/Mask"
                }.Select(Find).ToArray();
                locks = new[] {
                    "Shellwood Twig Wall (2)/Terrain/CameraLockArea (13)",
                    "Shellwood Twig Wall (1)/Terrain/CameraLockArea (13)",
                    "Shellwood Twig Wall (1)/Terrain/CameraLockArea (14)"
                }.Select(Find).ToArray();
            }
            else if (scene == "Arborium_06")
            {
                covers = Array.Empty<GameObject>();
                locks = new[] {
                    "Coral Crust Wall Tall (1)/Damager/CameraLockArea (2)",
                    "Coral Goomba Ambush/Crust Wall/Damager/CameraLockArea (1)",
                    "Coral Crust Wall Tall/Terrain Collider/CameraLockArea (3)"
                }.Select(Find).ToArray();
            }
            else if (scene == "Arborium_09")
            {
                covers = new[] {
                    "Moss Vine Cluster/Mask",
                    "Moss Vine Cluster (1)/Mask"
                }.Select(Find).ToArray();
                locks = new[] {
                    "Moss Vine Cluster (1)/Hero Blocker/CameraLockArea (8)"
                }.Select(Find).ToArray();
            }
            else return false;
            return covers.All(cover => IsEntryVisual(cover, scene) &&
                       cover.GetComponentsInChildren<SpriteRenderer>(true).Length > 0) &&
                   locks.All(cameraLock => IsEntryVisual(cameraLock, scene) &&
                       cameraLock.GetComponentsInChildren<CameraLockArea>(true).Length > 0);
        }

        private static bool IsEntryVisual(GameObject visual, string scene)
        {
            return visual != null && visual.scene.name == scene &&
                visual.GetComponentsInChildren<Collider2D>(true).All(collider => collider.isTrigger) &&
                visual.GetComponentsInChildren<PlayMakerFSM>(true).Length == 0 &&
                visual.GetComponentsInChildren<Breakable>(true).Length == 0;
        }

        private static bool IsPeakArrival(HeroController hero)
        {
            return IsRoomBoundScene(hero, PeakScene) &&
                   hero.GetEntryGateName() == "left1";
        }

        private static bool IsShellwoodArrival(HeroController hero)
        {
            return IsRoomBoundScene(hero, ShellwoodScene) &&
                   hero.GetEntryGateName() == "right1";
        }

        private static bool IsAqueductRoomBound()
        {
            return IsRoomBoundScene(
                HeroController.SilentInstance,
                AqueductScene
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

        private static IEnumerator RevealPeakEntry()
        {
            for (int frame = 0; frame < WallSearchFrames; frame++)
            {
                if (!IsPeakArrival(HeroController.SilentInstance))
                    yield break;

                if (TryFindPeakWall(out Breakable wall) &&
                    TryResolvePeakWallObjects(wall, out _, out _, out _,
                        out _, out GameObject cameraLock, out GameObject remasker))
                {
                    remasker.SetActive(false);
                    cameraLock.SetActive(false);
                    yield break;
                }

                yield return null;
            }

            RandomizerPlugin.Log?.LogWarning(
                "[RANDOMIZER] Could not resolve Peak_04d left1 wall visuals.");
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

        private static IEnumerator PrepareFarFieldsBridge()
        {
            const string scene = "Bone_East_11";
            const string path = "Bone East 11 Cross Over Group/Gate";
            for (int frame = 0; frame < WallSearchFrames; frame++)
            {
                if (!IsRoomBoundScene(HeroController.SilentInstance, scene)) yield break;
                Gate[] gates = Resources.FindObjectsOfTypeAll<Gate>().Where(gate =>
                    gate != null && gate.gameObject.scene.name == scene &&
                    Utils.GetHierarchyPath(gate.transform) == path).Take(2).ToArray();
                if (gates.Length == 1)
                {
                    if (gates[0].GetComponent<FarFieldsWindGap>() == null)
                        gates[0].gameObject.AddComponent<FarFieldsWindGap>();
                    yield break;
                }
                yield return null;
            }
            RandomizerPlugin.Log?.LogWarning("[RANDOMIZER] Could not resolve the Far Fields bridge.");
        }

        internal static void ClipBridgeTriangle(Vector2[] triangle, float[] worldX,
            float edge, bool keepLeft, System.Collections.Generic.List<Vector2> vertices,
            System.Collections.Generic.List<ushort> indices)
        {
            var polygon = new System.Collections.Generic.List<Vector2>(4);
            for (int i = 0; i < 3; i++)
            {
                int previous = (i + 2) % 3;
                bool inside = keepLeft ? worldX[i] <= edge : worldX[i] >= edge;
                bool wasInside = keepLeft ? worldX[previous] <= edge : worldX[previous] >= edge;
                if (inside != wasInside)
                {
                    float amount = (edge - worldX[previous]) / (worldX[i] - worldX[previous]);
                    polygon.Add(triangle[previous] + (triangle[i] - triangle[previous]) * amount);
                }
                if (inside) polygon.Add(triangle[i]);
            }
            if (polygon.Count < 3) return;
            int start = vertices.Count;
            if (start + polygon.Count > ushort.MaxValue)
                throw new InvalidOperationException("Bridge sprite geometry is too large.");
            vertices.AddRange(polygon);
            for (int i = 1; i + 1 < polygon.Count; i++)
            {
                indices.Add((ushort)start);
                indices.Add((ushort)(start + i));
                indices.Add((ushort)(start + i + 1));
            }
        }

        private sealed class FarFieldsWindGap : MonoBehaviour
        {
            private readonly System.Collections.Generic.List<Sprite> sprites =
                new System.Collections.Generic.List<Sprite>();

            private void LateUpdate()
            {
                if (SaveState.Instance == null || !SaveState.Instance.IsRoomBound) return;
                Animator animator = GetComponent<Animator>();
                if (animator == null) { enabled = false; return; }
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                if (!state.IsName("Open") || state.normalizedTime < 1f) return;
                enabled = false;
                try
                {
                    Apply(animator);
                }
                catch (Exception exception)
                {
                    RandomizerPlugin.Log?.LogWarning("[RANDOMIZER] Could not preserve the Far Fields wind route: " + exception.Message);
                }
            }

            private void Apply(Animator animator)
            {
                Transform bridge = transform.Find("extender bridges/ant bridge left");
                Transform tallWind = transform.Find("Updraft Region (3)");
                Transform shortWind = transform.Find("Updraft Region Short");
                BoxCollider2D floor = bridge?.Find("terrain collider")?.GetComponent<BoxCollider2D>();
                BoxCollider2D wind = tallWind?.Find("Enter Region")?.GetComponent<BoxCollider2D>();
                if (floor == null || wind == null || shortWind == null || floor.isTrigger ||
                    !wind.isTrigger || floor.usedByEffector || floor.attachedRigidbody != null ||
                    Mathf.Abs(floor.transform.right.x - 1f) > 0.001f)
                    throw new InvalidOperationException("Unexpected bridge or wind collision.");

                Vector3 windCentre = wind.transform.TransformPoint(wind.offset);
                float left = windCentre.x - 1f;
                float right = windCentre.x + 1f;
                Bounds bounds = floor.bounds;
                if (left - bounds.min.x < 0.75f || bounds.max.x - right < 0.75f)
                    throw new InvalidOperationException("The wind opening would leave too little bridge.");

                var replacements = new System.Collections.Generic.Dictionary<SpriteRenderer, Sprite>();
                foreach (SpriteRenderer renderer in bridge.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer.sprite == null || renderer.drawMode != SpriteDrawMode.Simple)
                        throw new InvalidOperationException("Unexpected bridge sprite.");
                    if (renderer.bounds.max.x <= left || renderer.bounds.min.x >= right) continue;
                    Sprite source = renderer.sprite;
                    Vector2[] original = source.vertices;
                    ushort[] triangles = source.triangles;
                    var vertices = new System.Collections.Generic.List<Vector2>();
                    var indices = new System.Collections.Generic.List<ushort>();
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        Vector2[] triangle = { original[triangles[i]], original[triangles[i + 1]], original[triangles[i + 2]] };
                        float[] worldX = triangle.Select(vertex => renderer.transform.TransformPoint(new Vector3(
                            renderer.flipX ? -vertex.x : vertex.x,
                            renderer.flipY ? -vertex.y : vertex.y, 0f)).x).ToArray();
                        ClipBridgeTriangle(triangle, worldX, left, true, vertices, indices);
                        ClipBridgeTriangle(triangle, worldX, right, false, vertices, indices);
                    }
                    Sprite replacement = null;
                    if (indices.Count > 0)
                    {
                        replacement = Instantiate(source);
                        sprites.Add(replacement);
                        replacement.OverrideGeometry(vertices.Select(vertex =>
                            vertex * source.pixelsPerUnit + source.pivot).ToArray(), indices.ToArray());
                    }
                    replacements.Add(renderer, replacement);
                }
                if (replacements.Count == 0)
                    throw new InvalidOperationException("No bridge artwork covers the wind opening.");

                float minimum = floor.offset.x - floor.size.x / 2f;
                float maximum = floor.offset.x + floor.size.x / 2f;
                float localLeft = floor.transform.InverseTransformPoint(new Vector3(left, bounds.center.y, bounds.center.z)).x;
                float localRight = floor.transform.InverseTransformPoint(new Vector3(right, bounds.center.y, bounds.center.z)).x;
                Vector2 size = floor.size;
                Vector2 offset = floor.offset;
                BoxCollider2D otherSide = floor.gameObject.AddComponent<BoxCollider2D>();
                otherSide.sharedMaterial = floor.sharedMaterial;
                otherSide.edgeRadius = floor.edgeRadius;
                otherSide.size = new Vector2(maximum - localRight, size.y);
                otherSide.offset = new Vector2((maximum + localRight) / 2f, offset.y);
                floor.size = new Vector2(localLeft - minimum, size.y);
                floor.offset = new Vector2((minimum + localLeft) / 2f, offset.y);
                foreach (var replacement in replacements)
                {
                    if (replacement.Value == null) replacement.Key.enabled = false;
                    else replacement.Key.sprite = replacement.Value;
                }
                animator.enabled = false;
                shortWind.gameObject.SetActive(false);
                tallWind.gameObject.SetActive(true);
            }

            private void OnDestroy()
            {
                foreach (Sprite sprite in sprites)
                    if (sprite != null) Destroy(sprite);
            }
        }

        [HarmonyPatch(typeof(HeroController), "SendHeroInPosition")]
        private static class RouteArrivalPatch
        {
            [HarmonyPostfix]
            private static void Postfix(HeroController __instance)
            {
                if (IsRoomBoundScene(__instance, "Arborium_06") ||
                    IsRoomBoundScene(__instance, "Arborium_09"))
                {
                    string scene = GameManager.instance.GetSceneNameString();
                    string gate = __instance.GetEntryGateName();
                    if (gate == "right1" || gate == "right2" || gate == "left1" || gate == "bot1")
                        __instance.StartCoroutine(RevealSideEntry(scene, gate));
                }

                if (IsRoomBoundScene(__instance, "Bone_16") && __instance.GetEntryGateName() == "top1")
                    __instance.StartCoroutine(RevealSideEntry("Bone_16", "top1"));

                if (IsRoomBoundScene(__instance, "Bone_East_11"))
                    __instance.StartCoroutine(PrepareFarFieldsBridge());

                if (IsFarFieldsArrival(__instance))
                    __instance.StartCoroutine(RevealGatedEntry(true));
                if (IsSethArrival(__instance))
                    __instance.StartCoroutine(RevealGatedEntry(false));

                if (IsArboriumArrival(__instance))
                {
                    __instance.StartCoroutine(OpenArboriumWall());
                }

                if (IsAqueductArrival(__instance))
                {
                    __instance.StartCoroutine(OpenAqueductWall());
                }

                if (IsWormwaysArrival(__instance))
                {
                    __instance.StartCoroutine(RevealSideEntry(WormwaysScene, "left1"));
                }

                if (IsRoomBoundScene(__instance, "Shellwood_25") && __instance.GetEntryGateName() == "left1")
                    __instance.StartCoroutine(RevealSideEntry("Shellwood_25", "left1"));

                if (IsPeakArrival(__instance))
                {
                    __instance.StartCoroutine(RevealPeakEntry());
                }

                if (IsShellwoodArrival(__instance))
                {
                    __instance.StartCoroutine(RevealSideEntry(ShellwoodScene, "right1"));
                }
            }
        }
    }
}
