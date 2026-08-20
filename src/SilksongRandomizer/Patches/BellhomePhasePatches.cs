using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    internal static class BellhomePhaseManager
    {
        internal const string BellhomeSceneName =
            "Belltown_Room_Spare";
        internal const string BellhomeEntryGateName = "left1";
        private const string BellhartSceneName = "Belltown";
        private const string BellhartEntryGateName = "door5";
        private const string BellhomeBenchObjectName = "RestBench";
        private const string BellhomeBenchFsmName = "Bench Control";
        private const string BellhomeBenchRestingStateName = "Resting";
        private const string BellhomeDoorLockObjectName = "Door Lock";
        private const string BellhomeExteriorRootName =
            "Hornet House States";
        private const string BellhomeExteriorNoneName = "None";
        private const string BellhomeExteriorHalfName = "Half";
        private const string BellhomeExteriorFullName = "Full";

        private static readonly FieldInfo DialogueYesNoBoxInstanceField =
            AccessTools.Field(typeof(DialogueYesNoBox), "_instance");

        private static bool promptOpen;
        private static bool phaseChangeInProgress;
        private static GameObject bellhomeExteriorRoot;
        private static GameObject bellhomeExteriorNone;
        private static GameObject bellhomeExteriorHalf;
        private static GameObject bellhomeExteriorFull;

        internal static void Update()
        {
            EnsureBellhomeUnlocked();

            SaveState state = SaveState.Instance;
            PlayerData playerData = PlayerData.instance;
            if (state == null ||
                !state.IsRoomBound ||
                playerData == null)
            {
                return;
            }

            if (playerData.blackThreadWorld &&
                playerData.act3_wokeUp)
            {
                state.bellhomePhaseToggleUnlocked = true;
            }
        }

        internal static void EnsureBellhomeUnlocked()
        {
            SaveState state = SaveState.Instance;
            PlayerData playerData = PlayerData.instance;
            if (state == null ||
                !state.IsRoomBound ||
                playerData == null)
            {
                return;
            }

            // Bellhome's construction/furnishing state is independent. Only
            // remove the key lock so its vanilla door remains usable forever.
            playerData.BelltownHouseUnlocked = true;
            EnsureBellhomeExteriorPresent();
        }

        private static bool EnsureBellhomeExteriorPresent()
        {
            GameManager gameManager = GameManager.instance;
            if (gameManager == null ||
                !string.Equals(
                    GameManager.GetBaseSceneName(
                        gameManager.sceneName ?? string.Empty
                    ),
                    BellhartSceneName,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                ClearBellhomeExteriorCache();
                return false;
            }

            if (bellhomeExteriorRoot == null)
            {
                ResolveBellhomeExterior(
                    GameObject.Find(BellhomeExteriorRootName)
                );
            }

            return ApplyBellhomeExteriorPresentation();
        }

        internal static bool TryOverrideBellhomeExteriorActivator(
            TestGameObjectActivator activator)
        {
            SaveState state = SaveState.Instance;
            if (state == null ||
                !state.IsRoomBound ||
                activator == null ||
                activator.gameObject == null ||
                !string.Equals(
                    activator.gameObject.name,
                    BellhomeExteriorRootName,
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    GameManager.GetBaseSceneName(
                        activator.gameObject.scene.name
                    ),
                    BellhartSceneName,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return false;
            }

            ResolveBellhomeExterior(activator.gameObject);
            return ApplyBellhomeExteriorPresentation();
        }

        private static bool ApplyBellhomeExteriorPresentation()
        {
            if (bellhomeExteriorRoot == null ||
                bellhomeExteriorNone == null ||
                bellhomeExteriorHalf == null ||
                bellhomeExteriorFull == null)
            {
                return false;
            }

            // The save-owned construction enum drives two Bellhart wishes.
            // The saved construction phase remains unchanged while the
            // completed physical house exposes the independently unlocked door.
            if (!bellhomeExteriorFull.activeSelf)
            {
                bellhomeExteriorFull.SetActive(true);
            }
            if (bellhomeExteriorNone.activeSelf)
            {
                bellhomeExteriorNone.SetActive(false);
            }
            if (bellhomeExteriorHalf.activeSelf)
            {
                bellhomeExteriorHalf.SetActive(false);
            }

            return true;
        }

        private static void ResolveBellhomeExterior(GameObject root)
        {
            if (root == bellhomeExteriorRoot &&
                bellhomeExteriorNone != null &&
                bellhomeExteriorHalf != null &&
                bellhomeExteriorFull != null)
            {
                return;
            }

            ClearBellhomeExteriorCache();

            if (root == null ||
                !string.Equals(
                    GameManager.GetBaseSceneName(root.scene.name),
                    BellhartSceneName,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return;
            }

            bellhomeExteriorRoot = root;
            foreach (Transform child in root.transform)
            {
                if (child == null || child.gameObject == null)
                {
                    continue;
                }

                if (string.Equals(
                    child.name,
                    BellhomeExteriorNoneName,
                    StringComparison.Ordinal
                ))
                {
                    bellhomeExteriorNone = child.gameObject;
                }
                else if (string.Equals(
                    child.name,
                    BellhomeExteriorHalfName,
                    StringComparison.Ordinal
                ))
                {
                    bellhomeExteriorHalf = child.gameObject;
                }
                else if (string.Equals(
                    child.name,
                    BellhomeExteriorFullName,
                    StringComparison.Ordinal
                ))
                {
                    bellhomeExteriorFull = child.gameObject;
                }
            }
        }

        private static void ClearBellhomeExteriorCache()
        {
            bellhomeExteriorRoot = null;
            bellhomeExteriorNone = null;
            bellhomeExteriorHalf = null;
            bellhomeExteriorFull = null;
        }

        internal static bool TryInterceptBellhomeNeedolin(
            ListenForDreamNail action)
        {
            if (!IsBellhomeBenchNeedolinListener(action) ||
                promptOpen ||
                phaseChangeInProgress)
            {
                return false;
            }

            SaveState state = SaveState.Instance;
            PlayerData playerData = PlayerData.instance;
            GameManager gameManager = GameManager.instance;
            if (state == null ||
                !state.IsRoomBound ||
                playerData == null ||
                gameManager == null ||
                !playerData.atBench ||
                gameManager.isPaused ||
                !(state.bellhomePhaseToggleUnlocked ||
                  playerData.act3_wokeUp))
            {
                return false;
            }

            InputHandler inputHandler =
                gameManager.GetComponent<InputHandler>();
            if (inputHandler == null ||
                inputHandler.inputActions == null ||
                !inputHandler.inputActions.DreamNail.WasPressed ||
                !inputHandler.inputActions.Dash.IsPressed ||
                !IsDialoguePromptReady())
            {
                return false;
            }

            bool targetBlackThreadWorld =
                !playerData.blackThreadWorld;
            string prompt = GetPrompt(
                targetBlackThreadWorld,
                playerData.act3_wokeUp
            );

            inputHandler.ForceDreamNailRePress = true;
            promptOpen = true;
            try
            {
                DialogueYesNoBox.Open(
                    () =>
                    {
                        promptOpen = false;
                        BeginPhaseChange(targetBlackThreadWorld);
                    },
                    () => promptOpen = false,
                    true,
                    prompt
                );
            }
            catch (Exception ex)
            {
                promptOpen = false;
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Could not open the Bellhome phase " +
                    "confirmation: " + ex.Message
                );
                return false;
            }

            return true;
        }

        internal static bool IsBellhomeDoorLock(
            ItemReceptacle receptacle)
        {
            return receptacle != null &&
                   receptacle.gameObject != null &&
                   string.Equals(
                       GameManager.GetBaseSceneName(
                           receptacle.gameObject.scene.name
                       ),
                       BellhartSceneName,
                       StringComparison.OrdinalIgnoreCase
                   ) &&
                   string.Equals(
                       receptacle.gameObject.name,
                       BellhomeDoorLockObjectName,
                       StringComparison.Ordinal
                   );
        }

        private static bool IsBellhomeBenchNeedolinListener(
            ListenForDreamNail action)
        {
            return action != null &&
                   action.Owner != null &&
                   action.Fsm != null &&
                   action.State != null &&
                   string.Equals(
                       GameManager.GetBaseSceneName(
                           action.Owner.scene.name
                       ),
                       BellhomeSceneName,
                       StringComparison.OrdinalIgnoreCase
                   ) &&
                   string.Equals(
                       action.Owner.name,
                       BellhomeBenchObjectName,
                       StringComparison.Ordinal
                   ) &&
                   string.Equals(
                       action.Fsm.Name,
                       BellhomeBenchFsmName,
                       StringComparison.Ordinal
                   ) &&
                   string.Equals(
                       action.State.Name,
                       BellhomeBenchRestingStateName,
                       StringComparison.Ordinal
                   );
        }

        private static bool IsDialoguePromptReady()
        {
            if (DialogueYesNoBoxInstanceField == null)
            {
                return false;
            }

            DialogueYesNoBox instance =
                DialogueYesNoBoxInstanceField.GetValue(null) as
                    DialogueYesNoBox;
            return instance != null;
        }

        private static string GetPrompt(
            bool targetBlackThreadWorld,
            bool act3WokeUp)
        {
            if (!targetBlackThreadWorld)
            {
                return "Return Pharloom to its late Act 2 state?\n\n" +
                       "Your Act 3 progress will be preserved.";
            }

            if (!act3WokeUp)
            {
                return "Switch Pharloom to its Act 3 world state for " +
                       "logic testing?";
            }

            return "Restore the Black Thread world and return to Act 3?" +
                   "\n\nYour Act 3 progress will be preserved.";
        }

        private static void BeginPhaseChange(
            bool targetBlackThreadWorld)
        {
            if (phaseChangeInProgress || RandomizerPlugin.Instance == null)
            {
                return;
            }

            phaseChangeInProgress = true;
            RandomizerPlugin.Instance.StartCoroutine(
                ApplyPhaseChange(targetBlackThreadWorld)
            );
        }

        private static IEnumerator ApplyPhaseChange(
            bool targetBlackThreadWorld)
        {
            // The native confirmation box restores its HUD and input state.
            yield return null;

            SaveState state = SaveState.Instance;
            PlayerData playerData = PlayerData.instance;
            GameManager gameManager = GameManager.instance;
            if (state == null ||
                !state.IsRoomBound ||
                playerData == null ||
                gameManager == null ||
                !IsBellhomeSceneLoaded() ||
                !HasBellhomeEntryGate())
            {
                phaseChangeInProgress = false;
                RandomizerPlugin.Instance?.ReportBlockingError(
                    "Bellhome's phase switch could not validate its safe " +
                    "reload point. No story state was changed."
                );
                yield break;
            }

            bool previousBlackThreadWorld =
                playerData.blackThreadWorld;
            bool previousAct3WokeUp = playerData.act3_wokeUp;
            bool previousToggleUnlocked =
                state.bellhomePhaseToggleUnlocked;

            state.bellhomePhaseToggleUnlocked = true;
            playerData.blackThreadWorld = targetBlackThreadWorld;
            // StartAct3's destructive conversion is neither queued nor replayed.
            playerData.act3_wokeUp = true;
            EnsureBellhomeUnlocked();

            bool saveFinished = false;
            bool saveSucceeded = false;
            try
            {
                gameManager.SaveGame(success =>
                {
                    saveSucceeded = success;
                    saveFinished = true;
                });
            }
            catch (Exception ex)
            {
                RandomizerPlugin.Log?.LogError(
                    "[RANDOMIZER] Bellhome phase save failed: " + ex
                );
                saveFinished = true;
            }

            while (!saveFinished)
            {
                yield return null;
            }

            if (!saveSucceeded)
            {
                playerData.blackThreadWorld =
                    previousBlackThreadWorld;
                playerData.act3_wokeUp = previousAct3WokeUp;
                state.bellhomePhaseToggleUnlocked =
                    previousToggleUnlocked;
                phaseChangeInProgress = false;
                RandomizerPlugin.Instance?.ReportBlockingError(
                    "Bellhome's phase switch could not save safely. " +
                    "The story phase was left unchanged."
                );
                yield break;
            }

            RandomizerPlugin.Log?.LogInfo(
                "[RANDOMIZER] Bellhome shifted Pharloom to the " +
                (targetBlackThreadWorld
                    ? "Act 3 Black Thread"
                    : "late Act 2") +
                " world phase."
            );

            phaseChangeInProgress = false;
            gameManager.ChangeToScene(
                BellhomeSceneName,
                BellhomeEntryGateName,
                0f
            );
        }

        private static bool IsBellhomeSceneLoaded()
        {
            GameManager gameManager = GameManager.instance;
            return gameManager != null &&
                   string.Equals(
                       GameManager.GetBaseSceneName(
                           gameManager.sceneName ?? string.Empty
                       ),
                       BellhomeSceneName,
                       StringComparison.OrdinalIgnoreCase
                   );
        }

        private static bool HasBellhomeEntryGate()
        {
            return Resources.FindObjectsOfTypeAll<TransitionPoint>()
                .Any(transition =>
                    transition != null &&
                    transition.gameObject != null &&
                    transition.gameObject.activeInHierarchy &&
                    string.Equals(
                        GameManager.GetBaseSceneName(
                            transition.gameObject.scene.name
                        ),
                        BellhomeSceneName,
                        StringComparison.OrdinalIgnoreCase
                    ) &&
                    string.Equals(
                        transition.gameObject.name,
                        BellhomeEntryGateName,
                        StringComparison.Ordinal
                    ) &&
                    string.Equals(
                        transition.targetScene,
                        BellhartSceneName,
                        StringComparison.Ordinal
                    ) &&
                    string.Equals(
                        transition.entryPoint,
                        BellhartEntryGateName,
                        StringComparison.Ordinal
                    ));
        }
    }

    [HarmonyPatch(
        typeof(ListenForDreamNail),
        nameof(ListenForDreamNail.OnUpdate)
    )]
    internal static class BellhomeNeedolinPhaseTogglePatch
    {
        private static bool Prefix(ListenForDreamNail __instance)
        {
            return !BellhomePhaseManager
                .TryInterceptBellhomeNeedolin(__instance);
        }
    }

    [HarmonyPatch(typeof(ItemReceptacle), "Start")]
    internal static class BellhomeDoorAlwaysUnlockedPatch
    {
        private static void Prefix(ItemReceptacle __instance)
        {
            if (BellhomePhaseManager.IsBellhomeDoorLock(__instance))
            {
                BellhomePhaseManager.EnsureBellhomeUnlocked();
            }
        }
    }

    [HarmonyPatch(typeof(TestGameObjectActivator), "Evaluate")]
    internal static class BellhomeExteriorAlwaysPresentPatch
    {
        private static bool Prefix(TestGameObjectActivator __instance)
        {
            return !BellhomePhaseManager
                .TryOverrideBellhomeExteriorActivator(__instance);
        }
    }
}
