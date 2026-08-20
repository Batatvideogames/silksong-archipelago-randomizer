using HarmonyLib;
using HutongGames.PlayMaker;
using System;
using System.Collections;
using System.Linq;
using TeamCherry.NestedFadeGroup;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    [HarmonyPatch(typeof(HeroController), "SendHeroInPosition")]
    internal static class Shellwood25RouteSafetyPatches
    {
        private const string SceneName = "Shellwood_25";
        private const string WallPath = "Shellwood Twig Wall (3)";
        private const string ControlFsmName = "Control";
        private const string PauseState = "Pause";
        private const string InitState = "Init";
        private const string CheckPlayerDataState = "Check PD";
        private const string IdleState = "Idle";
        private const string PlayerDataState = "PD Bool";
        private const string HitFourState = "Hit 4";
        private const string EndState = "End";
        private const string ActivatedState = "Activated";
        private const string DestroyEvent = "DESTROY";
        private const string ActivatedEvent = "ACTIVATED";
        private const string ActivatedVariable = "Activated";
        private const string PlayerDataVariable = "PD Bool";
        private const string TerrainVariable = "Terrain";
        private const string MaskVariable = "Mask";
        private const string MaskInverseVariable = "Mask Inverse";
        private const string DestroyEffectsVariable = "Destroy Effects";
        private const int SearchFrames = 180;
        private const int ReleaseFrames = 60;

        private static readonly string[] VineVariables =
        {
            "Vine 1",
            "Vine 2",
            "Vine 3",
            "Vine 4",
        };

        [HarmonyPostfix]
        private static void Postfix(HeroController __instance)
        {
            if (!IsExactArrival(__instance))
            {
                return;
            }

            __instance.StartCoroutine(OpenLeftEntryVines());
        }

        private static bool IsExactArrival(HeroController hero)
        {
            SaveState state = SaveState.Instance;
            GameManager gameManager = GameManager.instance;
            return hero != null &&
                   state != null &&
                   gameManager != null &&
                   state.IsRoomBound &&
                   string.Equals(
                       gameManager.GetSceneNameString(),
                       SceneName,
                       StringComparison.Ordinal
                   );
        }

        private static bool IsStillInTargetScene()
        {
            return SaveState.Instance?.IsRoomBound == true &&
                   string.Equals(
                       GameManager.instance?.GetSceneNameString(),
                       SceneName,
                       StringComparison.Ordinal
                   );
        }

        private static IEnumerator OpenLeftEntryVines()
        {
            bool foundWall = false;
            bool resolvedObjects = false;
            string lastState = string.Empty;

            for (int frame = 0; frame < SearchFrames; frame++)
            {
                if (!IsStillInTargetScene())
                {
                    yield break;
                }

                if (!TryFindExactWall(out PlayMakerFSM wall))
                {
                    yield return null;
                    continue;
                }

                foundWall = true;
                if (!TryResolveNativeObjects(
                        wall,
                        out PersistentBoolItem persistent,
                        out Component wallCollider,
                        out FsmBool activated,
                        out GameObject terrain,
                        out NestedFadeGroup mask,
                        out GameObject maskInverse
                    ))
                {
                    yield return null;
                    continue;
                }

                resolvedObjects = true;
                if (HasNativeWallOpened(
                        persistent,
                        wallCollider,
                        activated,
                        terrain,
                        mask
                    ))
                {
                    yield break;
                }

                lastState = wall.ActiveStateName ?? string.Empty;
                if (!string.Equals(
                        lastState,
                        IdleState,
                        StringComparison.Ordinal
                    ))
                {
                    yield return null;
                    continue;
                }

                wall.SendEvent(DestroyEvent);
                yield return VerifyNativeWallRelease(
                    persistent,
                    wallCollider,
                    activated,
                    terrain,
                    mask,
                    maskInverse
                );
                yield break;
            }

            string detail = !foundWall
                ? "matching vine wall was not found"
                : !resolvedObjects
                    ? "native vine wall objects did not resolve"
                    : "Control FSM never reached Idle (last state '" +
                      lastState + "')";
            RandomizerPlugin.Log?.LogWarning(
                "[RANDOMIZER] Could not open Shellwood_25 left1: " +
                detail + "."
            );
        }

        private static bool TryFindExactWall(out PlayMakerFSM wall)
        {
            wall = null;
            int matches = 0;
            foreach (PlayMakerFSM candidate in
                     Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
            {
                if (!IsExactWall(candidate))
                {
                    continue;
                }

                wall = candidate;
                matches++;
            }

            return matches == 1;
        }

        private static bool IsExactWall(PlayMakerFSM wall)
        {
            if (wall == null || wall.gameObject == null ||
                !string.Equals(
                    wall.gameObject.scene.name,
                    SceneName,
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    Utils.GetHierarchyPath(wall.transform),
                    WallPath,
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    wall.FsmName,
                    ControlFsmName,
                    StringComparison.Ordinal
                ) ||
                Vector2.Distance(
                    wall.transform.position,
                    new Vector2(4.4147f, 8.11f)
                ) > 0.01f ||
                wall.transform.childCount != 9 ||
                wall.gameObject.GetComponents<PlayMakerFSM>().Length != 1 ||
                wall.gameObject.GetComponents<PersistentBoolItem>().Length !=
                1)
            {
                return false;
            }

            Component collider = GetSingleComponentNamed(
                wall.gameObject,
                "UnityEngine.BoxCollider2D"
            );
            return TryReadBoxColliderShape(
                       collider,
                       out bool isTrigger,
                       out Vector2 offset,
                       out Vector2 size
                   ) &&
                   !isTrigger &&
                   Vector2.Distance(
                       offset,
                       new Vector2(-0.796622f, 1.1466031f)
                   ) <= 0.01f &&
                   Vector2.Distance(
                       size,
                       new Vector2(2.8187232f, 11.752305f)
                   ) <= 0.01f &&
                   HasNativeControlShape(wall);
        }

        private static bool HasNativeControlShape(PlayMakerFSM wall)
        {
            FsmState pause = wall.Fsm?.GetState(PauseState);
            FsmState init = wall.Fsm?.GetState(InitState);
            FsmState checkPlayerData = wall.Fsm?.GetState(
                CheckPlayerDataState
            );
            FsmState idle = wall.Fsm?.GetState(IdleState);
            FsmState playerData = wall.Fsm?.GetState(PlayerDataState);
            FsmState hitFour = wall.Fsm?.GetState(HitFourState);
            FsmState end = wall.Fsm?.GetState(EndState);
            FsmState activatedState = wall.Fsm?.GetState(ActivatedState);
            FsmBool activated = wall.FsmVariables?.FindFsmBool(
                ActivatedVariable
            );
            FsmString playerDataName = wall.FsmVariables?.FindFsmString(
                PlayerDataVariable
            );

            return activated != null &&
                   playerDataName != null &&
                   string.IsNullOrEmpty(playerDataName.Value) &&
                   pause?.Actions?.Length == 2 &&
                   HasSingleTransition(pause, "FINISHED", InitState) &&
                   init?.Actions?.Length == 4 &&
                   HasSingleTransition(
                       init,
                       ActivatedEvent,
                       ActivatedState
                   ) &&
                   HasSingleTransition(
                       init,
                       "FINISHED",
                       CheckPlayerDataState
                   ) &&
                   checkPlayerData?.Actions?.Length == 2 &&
                   HasSingleTransition(
                       checkPlayerData,
                       ActivatedEvent,
                       ActivatedState
                   ) &&
                   HasSingleTransition(
                       checkPlayerData,
                       "FINISHED",
                       IdleState
                   ) &&
                   idle?.Actions?.Length == 1 &&
                   HasSingleTransition(
                       idle,
                       DestroyEvent,
                       PlayerDataState
                   ) &&
                   HasSingleTransition(
                       idle,
                       ActivatedEvent,
                       ActivatedState
                   ) &&
                   playerData?.Actions?.Length == 2 &&
                   HasSingleTransition(
                       playerData,
                       "FINISHED",
                       HitFourState
                   ) &&
                   hitFour?.Actions?.Length == 8 &&
                   HasSingleTransition(
                       hitFour,
                       "FINISHED",
                       EndState
                   ) &&
                   end?.Actions?.Length == 6 &&
                   activatedState?.Actions?.Length == 10;
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

        private static bool TryResolveNativeObjects(
            PlayMakerFSM wall,
            out PersistentBoolItem persistent,
            out Component wallCollider,
            out FsmBool activated,
            out GameObject terrain,
            out NestedFadeGroup mask,
            out GameObject maskInverse
        )
        {
            persistent = wall?.GetComponent<PersistentBoolItem>();
            wallCollider = GetSingleComponentNamed(
                wall?.gameObject,
                "UnityEngine.BoxCollider2D"
            );
            activated = wall?.FsmVariables?.FindFsmBool(
                ActivatedVariable
            );
            terrain = GetVariableObject(wall, TerrainVariable);
            GameObject maskObject = GetVariableObject(wall, MaskVariable);
            mask = maskObject?.GetComponent<NestedFadeGroup>();
            maskInverse = GetVariableObject(wall, MaskInverseVariable);
            GameObject destroyEffects = GetVariableObject(
                wall,
                DestroyEffectsVariable
            );

            if (persistent == null || wallCollider == null ||
                activated == null ||
                !IsExactDirectChild(wall, terrain, TerrainVariable) ||
                GetSingleComponentNamed(
                    terrain,
                    "UnityEngine.BoxCollider2D"
                ) == null ||
                !HasSingleComponentNamed(terrain, "NonSlider") ||
                !HasSingleComponentNamed(terrain, "NonThunker") ||
                !IsExactDirectChild(wall, maskObject, MaskVariable) ||
                mask == null ||
                maskObject.GetComponents<NestedFadeGroup>().Length != 1 ||
                Vector2.Distance(
                    maskObject.transform.position,
                    new Vector2(-5.1053f, 8.09f)
                ) > 0.01f ||
                !IsExactDirectChild(
                    wall,
                    maskInverse,
                    MaskInverseVariable
                ) ||
                !IsExactDirectChild(
                    wall,
                    destroyEffects,
                    DestroyEffectsVariable
                ))
            {
                return false;
            }

            for (int index = 0; index < VineVariables.Length; index++)
            {
                string variableName = VineVariables[index];
                GameObject vine = GetVariableObject(wall, variableName);
                if (!IsExactDirectChild(wall, vine, variableName))
                {
                    return false;
                }
            }

            return true;
        }

        private static GameObject GetVariableObject(
            PlayMakerFSM wall,
            string variableName
        )
        {
            return wall?.FsmVariables
                ?.FindFsmGameObject(variableName)
                ?.Value;
        }

        private static bool IsExactDirectChild(
            PlayMakerFSM wall,
            GameObject candidate,
            string expectedName
        )
        {
            return wall != null &&
                   candidate != null &&
                   candidate.transform.parent == wall.transform &&
                   string.Equals(
                       candidate.name,
                       expectedName,
                       StringComparison.Ordinal
                   ) &&
                   string.Equals(
                       candidate.scene.name,
                       SceneName,
                       StringComparison.Ordinal
                   ) &&
                   string.Equals(
                       Utils.GetHierarchyPath(candidate.transform),
                       WallPath + "/" + expectedName,
                       StringComparison.Ordinal
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
            if (collider == null)
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

        private static bool HasNativeWallOpened(
            PersistentBoolItem persistent,
            Component wallCollider,
            FsmBool activated,
            GameObject terrain,
            NestedFadeGroup mask
        )
        {
            bool maskReleased = mask != null &&
                                (!mask.gameObject.activeInHierarchy ||
                                 Mathf.Approximately(
                                     mask.AlphaSelf,
                                     0f
                                 ));
            return persistent != null &&
                   persistent.GetCurrentValue() &&
                   wallCollider is Behaviour wallBehaviour &&
                   !wallBehaviour.enabled &&
                   activated != null &&
                   activated.Value &&
                   terrain != null &&
                   !terrain.activeInHierarchy &&
                   maskReleased;
        }

        private static IEnumerator VerifyNativeWallRelease(
            PersistentBoolItem persistent,
            Component wallCollider,
            FsmBool activated,
            GameObject terrain,
            NestedFadeGroup mask,
            GameObject maskInverse
        )
        {
            for (int frame = 0; frame < ReleaseFrames; frame++)
            {
                if (HasNativeWallOpened(
                        persistent,
                        wallCollider,
                        activated,
                        terrain,
                        mask
                    ))
                {
                    RandomizerPlugin.Log?.LogInfo(
                        "[RANDOMIZER] Opened Shellwood_25 left1 with " +
                        "the vine wall's native DESTROY route."
                    );
                    yield break;
                }

                yield return null;
            }

            RandomizerPlugin.Log?.LogWarning(
                "[RANDOMIZER] Shellwood_25 left1 native vine break did " +
                "not settle (persistence=" +
                (persistent != null && persistent.GetCurrentValue()) +
                ", Activated=" +
                (activated != null && activated.Value) +
                ", wall collider enabled=" +
                (wallCollider is Behaviour wallBehaviour &&
                 wallBehaviour.enabled) +
                ", Terrain active=" +
                (terrain != null && terrain.activeInHierarchy) +
                ", Mask active=" +
                (mask != null && mask.gameObject.activeInHierarchy) +
                ", Mask alpha=" +
                (mask != null ? mask.AlphaSelf : -1f) +
                ", Mask Inverse active=" +
                (maskInverse != null && maskInverse.activeInHierarchy) +
                ")."
            );
        }
    }
}
