using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SilksongRandomizer.Patches
{
    internal class SilkHeartPatches
    {
        private const string HeartFsmName = "Heart Container Control";
        private const string HeartObjectName = "Heart Piece Instant";
        private const string BellBeastSourceScene = "Bone_05_boss";
        private const string BellBeastSourceObject = "Silk Heart";
        private const string BellBeastSourceFsmName = "Control";
        private const string BellBeastMemoryScene =
            "Memory_Silk_Heart_BellBeast";
        private const string BellBeastReturnScene = "Bone_05";
        private const string BellBeastReturnGate = "door_cinematicEnd";
        private const string BellBeastLocation = "Silk Heart: Bell Beast";
        private const string UnravelledReturnScene = "Ward_02";
        private const string UnravelledMemoryScene =
            "Memory_Silk_Heart_WardBoss";
        private const string UnravelledLocation =
            "Silk Heart: The Unravelled";
        private const string LaceTowerReturnScene = "Song_Tower_01";
        private const string LaceTowerMemoryScene =
            "Memory_Silk_Heart_LaceTower";
        private const string LaceTowerLocation =
            "Silk Heart: Lace (Cradle)";
        private enum BossHeartCompletion
        {
            Unknown = 0,
            BellBeast,
            Unravelled,
            LaceTower,
        }

        private sealed class BossHeartSource
        {
            internal readonly string SourceScene;
            internal readonly string MemoryScene;
            internal readonly string ReturnScene;
            internal readonly string LocationName;
            internal readonly BossHeartCompletion Completion;

            internal BossHeartSource(
                string sourceScene,
                string memoryScene,
                string returnScene,
                string locationName,
                BossHeartCompletion completion)
            {
                SourceScene = sourceScene;
                MemoryScene = memoryScene;
                ReturnScene = returnScene;
                LocationName = locationName;
                Completion = completion;
            }
        }

        private static readonly Dictionary<string, BossHeartSource>
            BossHeartSourcesByScene =
                new Dictionary<string, BossHeartSource>(
                    StringComparer.Ordinal)
                {
                    {
                        BellBeastSourceScene,
                        new BossHeartSource(
                            BellBeastSourceScene,
                            BellBeastMemoryScene,
                            BellBeastReturnScene,
                            BellBeastLocation,
                            completion: BossHeartCompletion.BellBeast)
                    },
                    {
                        UnravelledReturnScene,
                        new BossHeartSource(
                            UnravelledReturnScene,
                            UnravelledMemoryScene,
                            UnravelledReturnScene,
                            UnravelledLocation,
                            completion: BossHeartCompletion.Unravelled)
                    },
                    {
                        LaceTowerReturnScene,
                        new BossHeartSource(
                            LaceTowerReturnScene,
                            LaceTowerMemoryScene,
                            LaceTowerReturnScene,
                            LaceTowerLocation,
                            completion: BossHeartCompletion.LaceTower)
                    },
                };

        private static readonly Dictionary<string, string>
            HeartLocationsByScene =
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    {
                        "Memory_Silk_Heart_BellBeast",
                        "Silk Heart: Bell Beast"
                    },
                    {
                        "Memory_Silk_Heart_WardBoss",
                        UnravelledLocation
                    },
                    {
                        "Memory_Silk_Heart_LaceTower",
                        LaceTowerLocation
                    },
                };

        [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
        private static class SilkHeartSequencePatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.Last)]
            private static void Prefix(PlayMakerFSM __instance)
            {
                if (__instance == null ||
                    __instance.gameObject == null)
                {
                    return;
                }

                if (TryGetBossHeartSource(
                        __instance,
                        out BossHeartSource bossSource))
                {
                    if (IsActive(bossSource.LocationName) &&
                        PatchBossHeartSource(__instance, bossSource))
                    {
                        RandomizerPlugin.Log?.LogInfo(
                            "[RANDOMIZER] Silk Heart memory skipped and " +
                            "redirected to the AP check: " +
                            bossSource.LocationName + "."
                        );
                    }
                    if (IsActive(bossSource.LocationName) &&
                        string.Equals(
                            bossSource.SourceScene,
                            bossSource.ReturnScene,
                            StringComparison.Ordinal) &&
                        PatchBellBeastReturnRecovery(__instance))
                    {
                        RandomizerPlugin.Log?.LogInfo(
                            "[RANDOMIZER] Silk Heart return recovery patched: " +
                            bossSource.LocationName + "."
                        );
                    }

                    return;
                }

                if (IsBellBeastReturnHeart(__instance))
                {
                    if (IsActive(BellBeastLocation) &&
                        PatchBellBeastReturnRecovery(__instance))
                    {
                        RandomizerPlugin.Log?.LogInfo(
                            "[RANDOMIZER] Bell Beast Silk Heart return now " +
                            "uses the native no-regeneration rise path."
                        );
                    }

                    return;
                }

                if (!HeartLocationsByScene.TryGetValue(
                        __instance.gameObject.scene.name,
                        out string locationName) ||
                    !string.Equals(
                        __instance.FsmName,
                        HeartFsmName,
                        StringComparison.Ordinal) ||
                    !IsActive(locationName))
                {
                    return;
                }

                if (!IsSupportedHeartObject(__instance.gameObject.name))
                {
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Silk Heart source patch failed closed at " +
                        __instance.gameObject.scene.name + "/" +
                        __instance.gameObject.name + "/" +
                        __instance.FsmName +
                        ": the shipped Heart Piece Instant object identity " +
                        "did not match."
                    );
                    return;
                }

                if (PatchSequence(__instance, locationName))
                {
                    RandomizerPlugin.Log?.LogInfo(
                        "[RANDOMIZER] Silk Heart presentation skipped and " +
                        "redirected to the AP check: " + locationName + "."
                    );
                }
            }
        }

        private static bool IsSupportedHeartObject(string objectName)
        {
            return string.Equals(
                       objectName,
                       HeartObjectName,
                       StringComparison.Ordinal
                   ) ||
                   string.Equals(
                       objectName,
                       HeartObjectName + "(Clone)",
                       StringComparison.Ordinal
                   );
        }

        private static bool TryGetBossHeartSource(
            PlayMakerFSM fsm,
            out BossHeartSource source)
        {
            source = null;
            if (fsm == null || fsm.gameObject == null ||
                !string.Equals(
                    fsm.gameObject.name,
                    BellBeastSourceObject,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    fsm.FsmName,
                    BellBeastSourceFsmName,
                    StringComparison.Ordinal))
            {
                return false;
            }

            return BossHeartSourcesByScene.TryGetValue(
                fsm.gameObject.scene.name,
                out source
            );
        }

        private static bool IsBellBeastReturnHeart(PlayMakerFSM fsm)
        {
            return fsm != null &&
                   fsm.gameObject != null &&
                   string.Equals(
                       fsm.gameObject.scene.name,
                       BellBeastReturnScene,
                       StringComparison.Ordinal
                   ) &&
                   string.Equals(
                       fsm.gameObject.name,
                       BellBeastSourceObject,
                       StringComparison.Ordinal
                   ) &&
                   string.Equals(
                       fsm.FsmName,
                       BellBeastSourceFsmName,
                       StringComparison.Ordinal
                   );
        }

        private static bool PatchBellBeastReturnRecovery(PlayMakerFSM fsm)
        {
            FsmState upPause = fsm.Fsm?.GetState("Up Pause");
            FsmState regenLastSilk =
                fsm.Fsm?.GetState("Regen Last Silk");
            FsmState heroUp = fsm.Fsm?.GetState("Hero Up");
            FsmState heroUpMemoryFirst =
                fsm.Fsm?.GetState("Hero Up Memory 1st");
            FsmState heroUpMemorySecond =
                fsm.Fsm?.GetState("Hero Up Memory 2");
            FsmState getUpSoundTriggers =
                fsm.Fsm?.GetState("Get Up Sound Triggers");
            FsmState playAudio = fsm.Fsm?.GetState("Play Audio");
            FsmState end = fsm.Fsm?.GetState("End");
            FsmState collected = fsm.Fsm?.GetState("Collected");

            IntCompare comparison =
                regenLastSilk?.Actions != null &&
                regenLastSilk.Actions.Length == 5
                    ? regenLastSilk.Actions[4] as IntCompare
                    : null;

            if (MatchesBellBeastReturnRecovery(
                    upPause,
                    regenLastSilk,
                    heroUp,
                    heroUpMemoryFirst,
                    heroUpMemorySecond,
                    getUpSoundTriggers,
                    playAudio,
                    end,
                    collected,
                    comparison,
                    true))
            {
                return true;
            }

            if (!MatchesBellBeastReturnRecovery(
                    upPause,
                    regenLastSilk,
                    heroUp,
                    heroUpMemoryFirst,
                    heroUpMemorySecond,
                    getUpSoundTriggers,
                    playAudio,
                    end,
                    collected,
                    comparison,
                    false))
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Bell Beast Silk Heart return patch " +
                    "failed closed at " + fsm.gameObject.scene.name + "/" +
                    fsm.gameObject.name + "/" + fsm.FsmName +
                    ": the shipped prone-recovery sequence no longer " +
                    "matched."
                );
                return false;
            }

            // A randomized AP reward does not necessarily increase native silk
            // regen. The unblock and fader actions in Regen Last Silk remain.
            // Every comparison result uses its existing CANCEL transition so
            // Hornet follows the native prostrate-rise cleanup. Ward_02 and
            // Song_Tower_01 share this recovery layout with the Bell Beast return.
            comparison.equal = comparison.greaterThan;
            comparison.lessThan = comparison.greaterThan;
            return true;
        }

        private static bool MatchesBellBeastReturnRecovery(
            FsmState upPause,
            FsmState regenLastSilk,
            FsmState heroUp,
            FsmState heroUpMemoryFirst,
            FsmState heroUpMemorySecond,
            FsmState getUpSoundTriggers,
            FsmState playAudio,
            FsmState end,
            FsmState collected,
            IntCompare comparison,
            bool patched)
        {
            if (upPause?.Actions == null ||
                regenLastSilk?.Actions == null ||
                heroUp == null ||
                heroUpMemoryFirst == null ||
                heroUpMemorySecond?.Actions == null ||
                getUpSoundTriggers?.Actions == null ||
                playAudio?.Actions == null ||
                end?.Actions == null ||
                collected?.Actions == null ||
                comparison == null)
            {
                return false;
            }

            if (upPause.Actions.Length != 3 ||
                !(upPause.Actions[0] is Wait) ||
                !IsBoolTest(
                    upPause.Actions[1],
                    "Continued From Memory",
                    "MEMORY",
                    string.Empty
                ) ||
                !IsBoolTest(
                    upPause.Actions[2],
                    "Stay Kneeling",
                    "NEXT",
                    string.Empty
                ) ||
                regenLastSilk.Actions.Length != 5 ||
                !IsSilkHeartRegenUnblock(regenLastSilk.Actions[0]) ||
                !IsNamedEventSend(
                    regenLastSilk.Actions[1],
                    "WORLD FADER APPEAR"
                ) ||
                !IsEventRegister(
                    regenLastSilk.Actions[2],
                    "REGENERATED SILK CHUNK"
                ) ||
                !IsSilkRegenMaxRead(regenLastSilk.Actions[3]) ||
                !IsSilkRegenComparison(comparison, patched) ||
                heroUpMemorySecond.Actions.Length != 3 ||
                !(heroUpMemorySecond.Actions[0] is
                    Tk2dPlayAnimationWithEventsV3) ||
                !(heroUpMemorySecond.Actions[1] is
                    PlayRandomAudioClipTable) ||
                !(heroUpMemorySecond.Actions[2] is
                    Tk2dPlayAnimationWait) ||
                getUpSoundTriggers.Actions.Length != 1 ||
                !(getUpSoundTriggers.Actions[0] is
                    Tk2dWatchAnimationEventsV3) ||
                playAudio.Actions.Length != 1 ||
                !(playAudio.Actions[0] is PlayRandomAudioClipTable) ||
                end.Actions.Length != 2 ||
                !IsHeroMessage(end.Actions[0], "RegainControl") ||
                !IsHeroMessage(
                    end.Actions[1],
                    "StartAnimationControl"
                ) ||
                collected.Actions.Length != 3 ||
                !IsSilkRegenBlockAction(collected.Actions[0], false) ||
                !IsHeartCollectedEvent(collected.Actions[1]) ||
                !IsDisableInventoryFalse(collected.Actions[2]))
            {
                return false;
            }

            return HasOnlyTransitions(
                       upPause,
                       Tuple.Create("MEMORY", regenLastSilk),
                       Tuple.Create(FsmEvent.Finished.Name, heroUp),
                       Tuple.Create("NEXT", collected)
                   ) &&
                   HasOnlyTransitions(
                       regenLastSilk,
                       Tuple.Create("CANCEL", heroUpMemorySecond),
                       Tuple.Create(
                           "REGENERATED SILK CHUNK",
                           heroUpMemoryFirst
                       )
                   ) &&
                   HasOnlyTransition(
                       heroUpMemorySecond,
                       FsmEvent.Finished.Name,
                       getUpSoundTriggers
                   ) &&
                   HasOnlyTransitions(
                       getUpSoundTriggers,
                       Tuple.Create("PLAY AUDIO", playAudio),
                       Tuple.Create(FsmEvent.Finished.Name, end)
                   ) &&
                   HasOnlyTransition(
                       playAudio,
                       FsmEvent.Finished.Name,
                       getUpSoundTriggers
                   ) &&
                   HasOnlyTransition(
                       end,
                       FsmEvent.Finished.Name,
                       collected
                   ) &&
                   (collected.Transitions == null ||
                    collected.Transitions.Length == 0);
        }

        private static bool IsBoolTest(
            FsmStateAction action,
            string variableName,
            string trueEvent,
            string falseEvent)
        {
            BoolTest test = action as BoolTest;
            return test != null &&
                   test.boolVariable != null &&
                   string.Equals(
                       test.boolVariable.Name,
                       variableName,
                       StringComparison.Ordinal
                   ) &&
                   IsEventNamed(test.isTrue, trueEvent) &&
                   IsEventNamed(test.isFalse, falseEvent) &&
                   !test.everyFrame;
        }

        private static bool IsSilkHeartRegenUnblock(FsmStateAction action)
        {
            SendMessage sendMessage = action as SendMessage;
            return sendMessage != null &&
                   sendMessage.functionCall != null &&
                   string.Equals(
                       sendMessage.functionCall.FunctionName,
                       "SetSilkRegenBlockedSilkHeart",
                       StringComparison.Ordinal
                   ) &&
                   sendMessage.functionCall.BoolParameter != null &&
                   !sendMessage.functionCall.BoolParameter.Value;
        }

        private static bool IsNamedEventSend(
            FsmStateAction action,
            string eventName)
        {
            SendEventByNameV2 sendEvent = action as SendEventByNameV2;
            return sendEvent != null &&
                   sendEvent.sendEvent != null &&
                   string.Equals(
                       sendEvent.sendEvent.Value,
                       eventName,
                       StringComparison.Ordinal
                   );
        }

        private static bool IsEventRegister(
            FsmStateAction action,
            string eventName)
        {
            AddEventRegister register = action as AddEventRegister;
            return register != null &&
                   register.eventName != null &&
                   string.Equals(
                       register.eventName.Value,
                       eventName,
                       StringComparison.Ordinal
                   );
        }

        private static bool IsSilkRegenMaxRead(FsmStateAction action)
        {
            GetPlayerDataVariable read = action as GetPlayerDataVariable;
            return read != null &&
                   read.VariableName != null &&
                   string.Equals(
                       read.VariableName.Value,
                       "CurrentSilkRegenMax",
                       StringComparison.Ordinal
                   ) &&
                   read.StoreValue != null &&
                   string.Equals(
                       read.StoreValue.variableName,
                       "Regen Max",
                       StringComparison.Ordinal
                   );
        }

        private static bool IsSilkRegenComparison(
            IntCompare comparison,
            bool patched)
        {
            return comparison.integer1 != null &&
                   string.Equals(
                       comparison.integer1.Name,
                       "Regen Max",
                       StringComparison.Ordinal
                   ) &&
                   comparison.integer2 != null &&
                   comparison.integer2.Value == 1 &&
                   IsEventNamed(comparison.greaterThan, "CANCEL") &&
                   IsEventNamed(
                       comparison.equal,
                       patched ? "CANCEL" : string.Empty
                   ) &&
                   IsEventNamed(
                       comparison.lessThan,
                       patched ? "CANCEL" : string.Empty
                   ) &&
                   !comparison.everyFrame;
        }

        private static bool IsEventNamed(FsmEvent fsmEvent, string name)
        {
            string actualName = fsmEvent?.Name ?? string.Empty;
            return string.Equals(
                actualName,
                name ?? string.Empty,
                StringComparison.Ordinal
            );
        }

        private static bool PatchBossHeartSource(
            PlayMakerFSM fsm,
            BossHeartSource source)
        {
            FsmString memoryScene =
                fsm.FsmVariables?.FindFsmString("Memory Scene");
            FsmState memoryCheck = fsm.Fsm?.GetState("Memory?");
            FsmState fadeAudio = fsm.Fsm?.GetState("Fade Audio Down");
            FsmState memoryTransition = fsm.Fsm?.GetState("Memory Scene");
            FsmState setData = fsm.Fsm?.GetState("Set Data");
            FsmState dropToPlace = fsm.Fsm?.GetState("Drop To Place");
            FsmState fadeReturn = fsm.Fsm?.GetState("Fade Return");
            FsmState upPause = fsm.Fsm?.GetState("Up Pause");
            FsmState heroUp = fsm.Fsm?.GetState("Hero Up");
            FsmState end = fsm.Fsm?.GetState("End");
            FsmState collected = fsm.Fsm?.GetState("Collected");

            if (memoryScene != null &&
                string.Equals(
                    memoryScene.Value,
                    source.ReturnScene,
                    StringComparison.Ordinal
                ) &&
                memoryCheck?.Actions != null &&
                memoryCheck.Actions.Length == 2 &&
                IsSilkRegenBlockAction(memoryCheck.Actions[0], true) &&
                IsMemorySceneCompare(memoryCheck.Actions[1]) &&
                setData?.Actions != null &&
                setData.Actions.Length == 2 &&
                IsActivatedTrueAction(setData.Actions[0]) &&
                setData.Actions[1] is CompleteBossHeartSourceAction
                    patchedCompletion &&
                string.Equals(
                    patchedCompletion.LocationName,
                    source.LocationName,
                    StringComparison.Ordinal) &&
                patchedCompletion.Completion == source.Completion &&
                HasOnlyTransitions(
                    memoryCheck,
                    Tuple.Create("MEMORY", setData),
                    Tuple.Create(FsmEvent.Finished.Name, setData)
                ) &&
                HasOnlyTransition(
                    setData,
                    FsmEvent.Finished.Name,
                    dropToPlace
                ) &&
                HasOnlyTransition(
                    dropToPlace,
                    FsmEvent.Finished.Name,
                    memoryTransition
                ) &&
                MatchesPatchedBellBeastReturn(memoryTransition))
            {
                return true;
            }

            if (!MatchesBossHeartSourceSequence(
                    fsm,
                    source.MemoryScene,
                    memoryScene,
                    memoryCheck,
                    fadeAudio,
                    memoryTransition,
                    setData,
                    dropToPlace,
                    fadeReturn,
                    upPause,
                    heroUp,
                    end,
                    collected))
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Silk Heart source patch failed closed at " +
                    fsm.gameObject.scene.name + "/" +
                    fsm.gameObject.name + "/" + fsm.FsmName +
                    ": the shipped memory and cleanup sequence no longer " +
                    "matched."
                );
                return false;
            }

            memoryScene.Value = source.ReturnScene;

            FsmTransition memoryBranch = memoryCheck.Transitions.First(
                transition =>
                    transition != null &&
                    transition.FsmEvent != null &&
                    string.Equals(
                        transition.FsmEvent.Name,
                        "MEMORY",
                        StringComparison.Ordinal
                    )
            );
            memoryBranch.ToState = setData.Name;
            memoryBranch.ToFsmState = setData;

            CompleteBossHeartSourceAction completion =
                new CompleteBossHeartSourceAction(
                    source.LocationName,
                    source.Completion);
            completion.Init(setData);
            setData.Actions = new FsmStateAction[]
            {
                setData.Actions[1],
                completion,
            };

            dropToPlace.Transitions = new FsmTransition[]
            {
                new FsmTransition
                {
                    FsmEvent = FsmEvent.Finished,
                    ToState = memoryTransition.Name,
                    ToFsmState = memoryTransition,
                },
            };

            BeginSceneTransition returnTransition =
                memoryTransition.Actions[6] as BeginSceneTransition;
            returnTransition.entryGateName.Value = BellBeastReturnGate;
            memoryTransition.Actions =
                memoryTransition.Actions.Skip(1).ToArray();
            return true;
        }

        private static bool MatchesBossHeartSourceSequence(
            PlayMakerFSM fsm,
            string expectedMemoryScene,
            FsmString memoryScene,
            FsmState memoryCheck,
            FsmState fadeAudio,
            FsmState memoryTransition,
            FsmState setData,
            FsmState dropToPlace,
            FsmState fadeReturn,
            FsmState upPause,
            FsmState heroUp,
            FsmState end,
            FsmState collected)
        {
            if (fsm?.Fsm == null ||
                memoryScene == null ||
                !string.Equals(
                    memoryScene.Value,
                    expectedMemoryScene,
                    StringComparison.Ordinal
                ) ||
                memoryCheck?.Actions == null ||
                fadeAudio == null ||
                memoryTransition == null ||
                setData?.Actions == null ||
                dropToPlace == null ||
                fadeReturn == null ||
                upPause == null ||
                heroUp == null ||
                end?.Actions == null ||
                collected?.Actions == null)
            {
                return false;
            }

            if (memoryCheck.Actions.Length != 2 ||
                !IsSilkRegenBlockAction(
                    memoryCheck.Actions[0],
                    true
                ) ||
                !IsMemorySceneCompare(memoryCheck.Actions[1]) ||
                setData.Actions.Length != 2 ||
                !IsVanillaSilkHeartGrant(setData.Actions[0]) ||
                !IsActivatedTrueAction(setData.Actions[1]) ||
                !MatchesShippedBellBeastMemoryTransition(
                    memoryTransition
                ) ||
                end.Actions.Length != 2 ||
                !IsHeroMessage(end.Actions[0], "RegainControl") ||
                !IsHeroMessage(
                    end.Actions[1],
                    "StartAnimationControl"
                ) ||
                collected.Actions.Length != 3 ||
                !IsSilkRegenBlockAction(
                    collected.Actions[0],
                    false
                ) ||
                !IsHeartCollectedEvent(collected.Actions[1]) ||
                !IsDisableInventoryFalse(collected.Actions[2]))
            {
                return false;
            }

            return HasOnlyTransitions(
                       memoryCheck,
                       Tuple.Create("MEMORY", fadeAudio),
                       Tuple.Create(
                           FsmEvent.Finished.Name,
                           setData
                       )
                   ) &&
                   HasOnlyTransition(
                       setData,
                       FsmEvent.Finished.Name,
                       dropToPlace
                   ) &&
                   HasOnlyTransition(
                       dropToPlace,
                       FsmEvent.Finished.Name,
                       fadeReturn
                   ) &&
                   HasOnlyTransition(
                       fadeReturn,
                       FsmEvent.Finished.Name,
                       upPause
                   ) &&
                   HasOnlyTransitions(
                       upPause,
                       Tuple.Create("MEMORY", fsm.Fsm.GetState(
                           "Regen Last Silk"
                       )),
                       Tuple.Create(
                           FsmEvent.Finished.Name,
                           heroUp
                       ),
                       Tuple.Create("NEXT", collected)
                   ) &&
                   HasOnlyTransition(
                       heroUp,
                       FsmEvent.Finished.Name,
                       end
                   ) &&
                   HasOnlyTransition(
                       end,
                       FsmEvent.Finished.Name,
                       collected
                   );
        }

        private static bool MatchesShippedBellBeastMemoryTransition(
            FsmState state)
        {
            if (state?.Actions == null ||
                state.Actions.Length != 7 ||
                !(state.Actions[0] is StartPreloadingScene preload) ||
                !(state.Actions[1] is ScreenFader) ||
                !(state.Actions[2] is ClearHeroEffects) ||
                !(state.Actions[3] is Wait) ||
                !(state.Actions[4] is ClearHeroEffects) ||
                !(state.Actions[5] is HeroLockState) ||
                !(state.Actions[6] is BeginSceneTransition transition) ||
                !IsMemorySceneVariable(preload.SceneName) ||
                !IsMemorySceneVariable(transition.sceneName) ||
                transition.entryGateName == null ||
                !string.Equals(
                    transition.entryGateName.Value,
                    "door_wakeOnGround",
                    StringComparison.Ordinal
                ))
            {
                return false;
            }

            return state.Transitions == null ||
                   state.Transitions.Length == 0;
        }

        private static bool MatchesPatchedBellBeastReturn(FsmState state)
        {
            if (state?.Actions == null ||
                state.Actions.Length != 6 ||
                !(state.Actions[0] is ScreenFader) ||
                !(state.Actions[1] is ClearHeroEffects) ||
                !(state.Actions[2] is Wait) ||
                !(state.Actions[3] is ClearHeroEffects) ||
                !(state.Actions[4] is HeroLockState) ||
                !(state.Actions[5] is BeginSceneTransition transition) ||
                !IsMemorySceneVariable(transition.sceneName) ||
                transition.entryGateName == null ||
                !string.Equals(
                    transition.entryGateName.Value,
                    BellBeastReturnGate,
                    StringComparison.Ordinal
                ))
            {
                return false;
            }

            return state.Transitions == null ||
                   state.Transitions.Length == 0;
        }

        private static bool IsMemorySceneVariable(FsmString variable)
        {
            return variable != null &&
                   string.Equals(
                       variable.Name,
                       "Memory Scene",
                       StringComparison.Ordinal
                   );
        }

        private static bool IsSilkRegenBlockAction(
            FsmStateAction action,
            bool blocked)
        {
            HeroControllerMethods heroAction =
                action as HeroControllerMethods;
            return heroAction != null &&
                   heroAction.method ==
                       HeroControllerMethods.Method.SetSilkRegenBlocked &&
                   heroAction.parameters != null &&
                   heroAction.parameters.Length == 1 &&
                   heroAction.parameters[0] != null &&
                   heroAction.parameters[0].boolValue == blocked;
        }

        private static bool IsMemorySceneCompare(FsmStateAction action)
        {
            StringCompare compare = action as StringCompare;
            return compare != null &&
                   compare.stringVariable != null &&
                   string.Equals(
                       compare.stringVariable.Name,
                       "Memory Scene",
                       StringComparison.Ordinal
                   ) &&
                   compare.compareTo != null &&
                   string.IsNullOrEmpty(compare.compareTo.Value) &&
                   compare.equalEvent != null &&
                   string.Equals(
                       compare.equalEvent.Name,
                       FsmEvent.Finished.Name,
                       StringComparison.Ordinal
                   ) &&
                   compare.notEqualEvent != null &&
                   string.Equals(
                       compare.notEqualEvent.Name,
                       "MEMORY",
                       StringComparison.Ordinal
                   );
        }

        private static bool IsVanillaSilkHeartGrant(FsmStateAction action)
        {
            CallMethodProper call = action as CallMethodProper;
            return call != null &&
                   call.behaviour != null &&
                   string.Equals(
                       call.behaviour.Value,
                       "HeroController",
                       StringComparison.Ordinal
                   ) &&
                   call.methodName != null &&
                   string.Equals(
                       call.methodName.Value,
                       "AddToMaxSilkRegen",
                       StringComparison.Ordinal
                   ) &&
                   call.parameters != null &&
                   call.parameters.Length == 1 &&
                   call.parameters[0] != null &&
                   call.parameters[0].intValue == 1;
        }

        private static bool IsActivatedTrueAction(FsmStateAction action)
        {
            SetBoolValue setBool = action as SetBoolValue;
            return setBool != null &&
                   setBool.boolVariable != null &&
                   string.Equals(
                       setBool.boolVariable.Name,
                       "Activated",
                       StringComparison.Ordinal
                   ) &&
                   setBool.boolValue != null &&
                   setBool.boolValue.Value;
        }

        private static bool IsHeroMessage(
            FsmStateAction action,
            string functionName)
        {
            SendMessage sendMessage = action as SendMessage;
            return sendMessage != null &&
                   sendMessage.functionCall != null &&
                   string.Equals(
                       sendMessage.functionCall.FunctionName,
                       functionName,
                       StringComparison.Ordinal
                   );
        }

        private static bool IsHeartCollectedEvent(FsmStateAction action)
        {
            SendEventToRegister sendEvent = action as SendEventToRegister;
            return sendEvent != null &&
                   sendEvent.eventName != null &&
                   string.Equals(
                       sendEvent.eventName.Value,
                       "HEART COLLECTED",
                       StringComparison.Ordinal
                   );
        }

        private static bool IsDisableInventoryFalse(FsmStateAction action)
        {
            SetPlayerDataVariable setVariable =
                action as SetPlayerDataVariable;
            return setVariable != null &&
                   setVariable.VariableName != null &&
                   string.Equals(
                       setVariable.VariableName.Value,
                       "disableInventory",
                       StringComparison.Ordinal
                   ) &&
                   setVariable.SetValue != null &&
                   !setVariable.SetValue.boolValue;
        }

        private static bool HasOnlyTransitions(
            FsmState state,
            params Tuple<string, FsmState>[] expected)
        {
            if (state?.Transitions == null ||
                expected == null ||
                state.Transitions.Length != expected.Length)
            {
                return false;
            }

            return expected.All(pair =>
                pair != null &&
                state.Transitions.Any(transition =>
                    transition != null &&
                    transition.FsmEvent != null &&
                    string.Equals(
                        transition.FsmEvent.Name,
                        pair.Item1,
                        StringComparison.Ordinal
                    ) &&
                    pair.Item2 != null &&
                    string.Equals(
                        transition.ToState,
                        pair.Item2.Name,
                        StringComparison.Ordinal
                    )));
        }

        private static bool IsActive(string locationName)
        {
            SaveState state = SaveState.Instance;
            return state != null &&
                   state.IsRandomized(ItemType.SilkHeart) &&
                   state.IsLocationEnabled(locationName) &&
                   state.IsLocationInSeed(locationName);
        }

        private static bool PatchSequence(
            PlayMakerFSM fsm,
            string locationName)
        {
            FsmState init = fsm.Fsm?.GetState("Init");
            FsmState takeControl = fsm.Fsm?.GetState("Take Control?");
            FsmState get = fsm.Fsm?.GetState("Get");
            FsmState onGround = fsm.Fsm?.GetState("On Ground?");
            FsmState wait = fsm.Fsm?.GetState("Wait");
            FsmState ui = fsm.Fsm?.GetState("UI");
            FsmState save = fsm.Fsm?.GetState("Save");

            if (get != null &&
                save != null &&
                get.Actions != null &&
                save.Actions != null &&
                get.Actions.Any(action =>
                    action is SkipHeartPresentationAction) &&
                save.Actions.Any(action =>
                    action is CompleteHeartLocationAction complete &&
                    string.Equals(
                        complete.LocationName,
                        locationName,
                        StringComparison.Ordinal)))
            {
                return true;
            }

            if (!MatchesShippedSequence(
                    fsm,
                    init,
                    takeControl,
                    get,
                    onGround,
                    wait,
                    ui,
                    save))
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Silk Heart source patch failed closed at " +
                    fsm.gameObject.scene.name + "/" + fsm.name +
                    ": the Heart Piece Instant FSM no longer matched the " +
                    "shipped Get/UI/Save layout."
                );
                return false;
            }

            SkipHeartPresentationAction skipPresentation =
                new SkipHeartPresentationAction();
            skipPresentation.Init(get);

            SendEventToRegister collectedEvent =
                new SendEventToRegister
                {
                    eventName = "HEART PIECE COLLECTED",
                };
            collectedEvent.Init(save);

            CompleteHeartLocationAction completion =
                new CompleteHeartLocationAction(locationName, true);
            completion.Init(save);

            FsmStateAction[] patchedSaveActions =
                new FsmStateAction[save.Actions.Length + 2];
            Array.Copy(
                save.Actions,
                patchedSaveActions,
                save.Actions.Length
            );
            patchedSaveActions[patchedSaveActions.Length - 2] =
                collectedEvent;
            patchedSaveActions[patchedSaveActions.Length - 1] = completion;

            // Init and Take Control? still establish the native pickup's
            // blocker/ownership. Redirect Get straight to Save before Get can
            // stop Hornet's animation, zero gravity, play the prone pose or
            // create the vanilla Heart Piece UI. Save keeps its
            // RemoveHeroInputBlocker and disablePause cleanup actions, then the
            // appended actions report the AP check and restore the hero.
            save.Actions = patchedSaveActions;
            get.Actions = new FsmStateAction[]
            {
                skipPresentation,
            };
            get.Transitions = new FsmTransition[]
            {
                new FsmTransition
                {
                    FsmEvent = FsmEvent.Finished,
                    ToState = save.Name,
                    ToFsmState = save,
                },
            };
            return true;
        }

        private static bool MatchesShippedSequence(
            PlayMakerFSM fsm,
            FsmState init,
            FsmState takeControl,
            FsmState get,
            FsmState onGround,
            FsmState wait,
            FsmState ui,
            FsmState save)
        {
            if (fsm?.Fsm == null ||
                init?.Actions == null ||
                takeControl?.Actions == null ||
                get?.Actions == null ||
                onGround == null ||
                wait == null ||
                ui?.Actions == null ||
                save?.Actions == null ||
                fsm.Fsm.GetState("End") != null)
            {
                return false;
            }

            // These signatures come from heartpieceupgrade_assets_all.bundle.
            // If Team Cherry changes the
            // reward FSM, leave the vanilla sequence untouched instead of
            // guessing at control or blocker cleanup.
            if (init.Actions.Length != 2 ||
                !(init.Actions[0] is AddHeroInputBlocker) ||
                takeControl.Actions.Length != 4 ||
                !(takeControl.Actions[3] is CallMethodProper) ||
                get.Actions.Length != 14 ||
                ui.Actions.Length != 2 ||
                !(ui.Actions[0] is CreateObject) ||
                !(ui.Actions[1] is CreateObjectV2) ||
                save.Actions.Length != 2 ||
                !(save.Actions[0] is RemoveHeroInputBlocker) ||
                !(save.Actions[1] is SetPlayerDataVariable))
            {
                return false;
            }

            return HasOnlyTransition(get, FsmEvent.Finished.Name, onGround) &&
                   HasOnlyTransition(
                       ui,
                       "HEART PIECE SAVE",
                       save
                   ) &&
                   (save.Transitions == null ||
                    save.Transitions.Length == 0);
        }

        private static bool HasOnlyTransition(
            FsmState state,
            string eventName,
            FsmState destination)
        {
            if (state?.Transitions == null ||
                state.Transitions.Length != 1 ||
                destination == null)
            {
                return false;
            }

            FsmTransition transition = state.Transitions[0];
            return transition != null &&
                   transition.FsmEvent != null &&
                   string.Equals(
                       transition.FsmEvent.Name,
                       eventName,
                       StringComparison.Ordinal
                   ) &&
                   string.Equals(
                       transition.ToState,
                       destination.Name,
                       StringComparison.Ordinal
                   );
        }

        private sealed class SkipHeartPresentationAction : FsmStateAction
        {
            public override void OnEnter()
            {
                Finish();
            }
        }

        private sealed class CompleteHeartLocationAction : FsmStateAction
        {
            internal readonly string LocationName;
            internal readonly bool RestoreHero;

            internal CompleteHeartLocationAction(
                string locationName,
                bool restoreHero)
            {
                LocationName = locationName;
                RestoreHero = restoreHero;
            }

            public override void OnEnter()
            {
                try
                {
                    SaveState state = SaveState.Instance;
                    if (state != null &&
                        state.IsRandomized(ItemType.SilkHeart) &&
                        state.IsLocationEnabled(LocationName) &&
                        state.IsLocationInSeed(LocationName))
                    {
                        state.CheckLocation(LocationName);
                    }
                }
                catch (Exception ex)
                {
                    RandomizerPlugin.Log?.LogError(
                        "[RANDOMIZER] Failed to complete Silk Heart AP check " +
                        LocationName + ": " + ex
                    );
                }
                finally
                {
                    if (RestoreHero)
                    {
                        RestoreHeroAfterSkippedPresentation();
                    }

                    Finish();
                }
            }
        }

        private sealed class CompleteBossHeartSourceAction : FsmStateAction
        {
            internal readonly string LocationName;
            internal readonly BossHeartCompletion Completion;

            internal CompleteBossHeartSourceAction(
                string locationName,
                BossHeartCompletion completion)
            {
                LocationName = locationName;
                Completion = completion;
            }

            public override void OnEnter()
            {
                try
                {
                    PlayerData playerData = PlayerData.instance;
                    if (playerData == null)
                    {
                        throw new InvalidOperationException(
                            "PlayerData was unavailable."
                        );
                    }

                    switch (Completion)
                    {
                        case BossHeartCompletion.BellBeast:
                            playerData.defeatedBellBeast = true;
                            playerData.bonebottomQuestBoardFixed = true;
                            break;
                        case BossHeartCompletion.Unravelled:
                            playerData.wardBossDefeated = true;
                            break;
                        case BossHeartCompletion.LaceTower:
                            playerData.defeatedLaceTower = true;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(
                                nameof(Completion),
                                Completion,
                                "Unknown Silk Heart completion."
                            );
                    }

                    SaveState state = SaveState.Instance;
                    if (state != null &&
                        state.IsRandomized(ItemType.SilkHeart) &&
                        state.IsLocationEnabled(LocationName) &&
                        state.IsLocationInSeed(LocationName))
                    {
                        state.CheckLocation(LocationName);
                    }
                }
                catch (Exception ex)
                {
                    RandomizerPlugin.Log?.LogError(
                        "[RANDOMIZER] Failed to complete Silk Heart source " +
                        LocationName + ": " + ex
                    );
                }
                finally
                {
                    Finish();
                }
            }
        }
        private static void RestoreHeroAfterSkippedPresentation()
        {
            PlayerData playerData = PlayerData.instance;
            if (playerData != null)
            {
                playerData.isInvincible = false;
                playerData.disablePause = false;
            }

            HeroController hero = HeroController.instance;
            if (hero == null)
            {
                return;
            }

            try
            {
                hero.ResetVelocity();
                hero.AffectedByGravity(true);
                hero.ResetGravity();
                hero.RegainControl();
                hero.StartAnimationControl();
            }
            catch (Exception ex)
            {
                RandomizerPlugin.Log?.LogError(
                    "[RANDOMIZER] Silk Heart skip could not fully restore " +
                    "Hornet's gravity/control state: " + ex
                );
            }
        }

        [HarmonyPatch(typeof(PlayerData), "get_CurrentSilkRegenMax", new Type[0])]
        internal static class PlayerData_CurrentSilkRegenMax_Patch
        {
            private static void Prefix(out int __state)
            {
                __state = PlayerData.instance.silkRegenMax;

                if (SaveState.Instance == null ||
                    !SaveState.Instance.IsRandomized(ItemType.SilkHeart))
                {
                    return;
                }

                PlayerData.instance.silkRegenMax = Math.Max(
                    0,
                    Math.Min(3, SaveState.Instance.silkHeartLevel)
                );
            }

            private static Exception Finalizer(Exception __exception, int __state)
            {
                PlayerData.instance.silkRegenMax = __state;
                return __exception;
            }
        }
    }
}
