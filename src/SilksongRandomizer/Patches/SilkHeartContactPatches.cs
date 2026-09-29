using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using QuestPlaymakerActions;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using SetPlayerDataBoolAction = HutongGames.PlayMaker.Actions.SetPlayerDataBool;

namespace SilksongRandomizer.Patches
{
    internal partial class SilkHeartPatches
    {
        private static readonly string[] ContactStates =
        {
            "Form Effects", "Fall Down", "Bob Up", "Bob Down",
            "Bob Up N", "Bob Down N",
        };

        private static readonly MethodInfo AdditiveStart =
            typeof(SceneAdditiveLoadConditional).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null);
        private static readonly FieldInfo AdditiveAlt =
            typeof(SceneAdditiveLoadConditional).GetField("loadAlt", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo AdditiveLoaded =
            typeof(SceneAdditiveLoadConditional).GetField("sceneLoaded", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo AdditiveName =
            typeof(SceneAdditiveLoadConditional).GetField("sceneNameToLoad", BindingFlags.Instance | BindingFlags.NonPublic);

        private static void PatchContactPickup(Fsm fsm, BossHeartSource source)
        {
            if (SaveState.Instance?.fasterSilkheartAnimation != true)
            {
                return;
            }

            try
            {
                FsmState pickup = fsm.GetState("Take Control");
                if (pickup?.Actions?.Length == 1 && pickup.Actions[0] is CollectBossHeart)
                {
                    return;
                }

                if (!MatchesContactPickup(fsm, pickup))
                {
                    throw new InvalidOperationException("The contact states did not match.");
                }

                CollectBossHeart action = new CollectBossHeart(source, pickup.Actions);
                action.Init(pickup);
                pickup.Actions = new FsmStateAction[] { action };
            }
            catch (Exception ex)
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Silk Heart contact pickup unavailable: " + ex.Message);
            }
        }

        private static bool MatchesContactPickup(Fsm fsm, FsmState pickup)
        {
            if (fsm?.Variables?.FindFsmBool("Activated") == null ||
                !HasActions(pickup,
                    typeof(SendEventToRegister), typeof(StopParticleEmitter),
                    typeof(GetPosition), typeof(GetPosition), typeof(SetVector3XYZ),
                    typeof(CallMethodProper), typeof(SendMessage),
                    typeof(HeroControllerMethods), typeof(SendMessage),
                    typeof(SendMessage), typeof(SendMessage), typeof(HeroLockState),
                    typeof(SetPlayerDataVariable), typeof(SetVelocity2d),
                    typeof(CheckTargetDirection)) ||
                !IsHeroMessage(pickup.Actions[8], "RelinquishControl") ||
                !IsHeroMessage(pickup.Actions[9], "StopAnimationControl") ||
                !HasOnlyTransitions(pickup,
                    Tuple.Create("L", fsm.GetState("Hero Face R")),
                    Tuple.Create("R", fsm.GetState("Hero Face L"))))
            {
                return false;
            }

            foreach (string name in ContactStates)
            {
                FsmState state = fsm.GetState(name);
                if (!MatchesContactState(state, name) || state.Transitions == null ||
                    state.Transitions.Count(t => IsEventNamed(t.FsmEvent, "TOUCH")) != 1 ||
                    !state.Transitions.Any(t => IsEventNamed(t.FsmEvent, "TOUCH") &&
                        t.ToState == pickup.Name))
                {
                    return false;
                }

                Trigger2dEvent[] triggers = state.Actions?.OfType<Trigger2dEvent>().ToArray();
                if (triggers == null || triggers.Length != 1 ||
                    !triggers[0].Enabled ||
                    triggers[0].gameObject?.OwnerOption != OwnerDefaultOption.UseOwner ||
                    triggers[0].trigger != Trigger2DType.OnTriggerEnter2D ||
                    !IsEventNamed(triggers[0].sendEvent, "TOUCH"))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool MatchesContactState(FsmState state, string name)
        {
            switch (name)
            {
                case "Form Effects":
                    return HasActions(state, typeof(ActivateGameObject), typeof(ActivateGameObject),
                        typeof(DoCameraShake), typeof(Wait), typeof(SetCircleCollider),
                        typeof(Trigger2dEvent), typeof(PlayParticleEmitter));
                case "Fall Down":
                    return HasActions(state, typeof(GetPosition), typeof(AnimatePositionTo),
                        typeof(Trigger2dEvent), typeof(PlayParticleEmitter), typeof(Wait));
                case "Bob Up":
                case "Bob Up N":
                    return HasActions(state, typeof(CheckHeroPerformanceRegion),
                        typeof(AnimatePositionBy), typeof(Trigger2dEvent), typeof(SetAudioPitch));
                case "Bob Down":
                case "Bob Down N":
                    return HasActions(state, typeof(CheckHeroPerformanceRegion),
                        typeof(Trigger2dEvent), typeof(AnimatePositionBy), typeof(SetAudioPitch));
                default:
                    return false;
            }
        }

        private static bool MatchesContactReturn(Fsm fsm)
        {
            FsmState save = fsm?.GetState("Save");
            FsmState quest = fsm?.GetState("Silk Quest?");
            return HasActions(save, typeof(HeroControllerMethods),
                    typeof(SetPlayerDataVariable), typeof(SetDeathRespawnMarker),
                    typeof(global::SaveGame)) &&
                save.Actions.All(action => action.Enabled) &&
                IsSilkRegenBlockAction(save.Actions[0], false) &&
                IsDisableInventoryFalse(save.Actions[1]) &&
                HasActions(quest, typeof(BoolTest), typeof(BeginQuest)) &&
                quest.Actions.All(action => action.Enabled) &&
                IsBoolTest(quest.Actions[0], "Start Final Silk Quest", "", "FINISHED") &&
                HasOnlyTransition(quest, FsmEvent.Finished.Name, save);
        }

        private static bool HasActions(FsmState state, params Type[] types)
        {
            return state?.Actions != null && state.Actions.Length == types.Length &&
                state.Actions.Select((action, i) =>
                    action != null && action.GetType() == types[i]).All(match => match);
        }

        private static GameObject SceneObject(Scene scene, string path)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }
            string[] parts = path.Split(new[] { '/' }, 2);
            GameObject[] roots = scene.GetRootGameObjects()
                .Where(root => root.name == parts[0]).ToArray();
            if (roots.Length != 1)
            {
                return null;
            }
            return parts.Length == 1 ? roots[0] :
                roots[0].transform.Find(parts[1])?.gameObject;
        }

        private static PlayMakerFSM SceneFsm(Scene scene, string path, string name)
        {
            return SceneObject(scene, path)?.GetComponents<PlayMakerFSM>()
                .SingleOrDefault(fsm => fsm.FsmName == name);
        }

        private static GameObject RequiredObject(Scene scene, string path)
        {
            return SceneObject(scene, path) ??
                throw new InvalidOperationException("Missing " + scene.name + "/" + path);
        }

        private sealed class HeartContactCompletion
        {
            internal readonly BossHeartSource Source;
            internal readonly Scene Scene;
            internal readonly PlayMakerFSM Return;
            internal readonly RespawnMarker Checkpoint;
            internal readonly FullQuestBase Quest;
            internal readonly PlayMakerFSM Exit;
            internal readonly GameObject[] Enable;
            internal readonly GameObject[] Disable;
            internal readonly SceneAdditiveLoadConditional Bellway;
            internal readonly SceneAdditiveLoadConditional BossLoader;

            internal HeartContactCompletion(BossHeartSource source)
            {
                Source = source;
                Scene = SceneManager.GetSceneByName(source.ReturnScene);
                string parent = source.Completion == BossHeartCompletion.Unravelled ?
                    "Boss Scene Parent/" : source.Completion == BossHeartCompletion.LaceTower ?
                    "Boss Scene/" : "";
                string returnPath = parent + "Silk Heart Memory Return";
                Return = SceneFsm(Scene, returnPath, "Silk Heart Memory Return");
                FsmState save = Return?.Fsm.GetState("Save");
                FsmState quest = Return?.Fsm.GetState("Silk Quest?");
                if (!MatchesContactReturn(Return?.Fsm) || Return.ActiveStateName != "Init")
                {
                    throw new InvalidOperationException("The checkpoint sequence did not match.");
                }

                GameObject marker = RequiredObject(Scene,
                    returnPath + "/Silk Heart Memory Respawn Marker");
                SetDeathRespawnMarker setMarker = (SetDeathRespawnMarker)save.Actions[2];
                Checkpoint = marker.GetComponent<RespawnMarker>();
                if (!Checkpoint || setMarker.Target?.OwnerOption != OwnerDefaultOption.SpecifyGameObject ||
                    setMarker.Target.GameObject?.Value != marker)
                {
                    throw new InvalidOperationException("The respawn marker did not match.");
                }
                bool startQuest = Return.FsmVariables.FindFsmBool("Start Final Silk Quest")?.Value
                    ?? throw new InvalidOperationException("Missing quest condition.");
                if (startQuest != (source.Completion == BossHeartCompletion.LaceTower))
                {
                    throw new InvalidOperationException("The quest condition did not match.");
                }
                Quest = startQuest ? ((BeginQuest)quest.Actions[1]).Quest?.Value as FullQuestBase : null;
                if (startQuest && (!Quest || !QuestManager.IsQuestInList(Quest)))
                {
                    throw new InvalidOperationException("Missing Silk Heart quest.");
                }

                switch (source.Completion)
                {
                    case BossHeartCompletion.BellBeast:
                        Scene bossScene = SceneManager.GetSceneByName(source.SourceScene);
                        Exit = SceneFsm(bossScene, "Boss Scene", "Battle End");
                        if (!MatchesBellBeastExit(Exit?.Fsm, Exit?.ActiveStateName))
                        {
                            throw new InvalidOperationException("The Bell Beast exits did not match.");
                        }
                        Disable = new[]
                        {
                            RequiredObject(bossScene, "Boss Scene/CamLock Battle"),
                            RequiredObject(bossScene, "Boss Scene/CamLock PreBattle"),
                        };
                        Enable = Array.Empty<GameObject>();
                        ValidateBellBeastTargets(Exit, bossScene, Disable);
                        Bellway = RequiredObject(Scene, "Bellway Additive Loader")
                            .GetComponent<SceneAdditiveLoadConditional>();
                        BossLoader = RequiredObject(Scene, "Boss Additive Loader")
                            .GetComponent<SceneAdditiveLoadConditional>();
                        if (!Bellway || !BossLoader || AdditiveStart == null ||
                            AdditiveAlt == null || AdditiveLoaded == null ||
                            AdditiveName == null || AdditiveStart.ReturnType != typeof(void) ||
                            AdditiveAlt.FieldType != typeof(bool) ||
                            AdditiveLoaded.FieldType != typeof(bool) ||
                            AdditiveName.FieldType != typeof(string) ||
                            (string)AdditiveName.GetValue(Bellway) != "Bone_05_bellway" ||
                            (string)AdditiveName.GetValue(BossLoader) != BellBeastSourceScene ||
                            SceneAdditiveLoadConditional.LoadInSequence ||
                            !RandomizerPlugin.Instance)
                        {
                            throw new InvalidOperationException("The Bellway loaders did not match.");
                        }
                        break;
                    case BossHeartCompletion.Unravelled:
                        Exit = SceneFsm(Scene, parent + "Pipe_Vent_Hatch", "Open At Battle End");
                        if (!MatchesHatchExit(Exit?.Fsm, Exit?.ActiveStateName,
                                PlayerData.instance.wardBossHatchOpened) ||
                            !Exit.gameObject.GetComponent<Trapdoor>())
                        {
                            throw new InvalidOperationException("The Unravelled hatch did not match.");
                        }
                        Disable = new[]
                        {
                            RequiredObject(Scene, parent + "ward_slide_hatch"),
                            RequiredObject(Scene, parent + "ward_slide_hatch bottom"),
                        };
                        Enable = new[]
                        {
                            RequiredObject(Scene, parent + "ward_slide_hatch_return"),
                            RequiredObject(Scene, parent + "ward_slide_hatch_return bottom"),
                            RequiredObject(Scene, "Song Region"),
                        };
                        break;
                    case BossHeartCompletion.LaceTower:
                        Exit = SceneFsm(Scene, "State Control/song_tower_right_gate", "Control");
                        if (!MatchesLaceExit(Exit?.Fsm, Exit?.ActiveStateName,
                                PlayerData.instance.laceTowerDoorOpened) ||
                            !Exit.gameObject.GetComponent<Gate>())
                        {
                            throw new InvalidOperationException("The Lace exit did not match.");
                        }
                        Disable = new[]
                        {
                            RequiredObject(Scene, "State Control/CameraLockArea - Battle"),
                            RequiredObject(Scene, "State Control/CameraLockArea - BattleWide"),
                            RequiredObject(Scene, parent + "Battle Particles"),
                        };
                        Enable = new[] { RequiredObject(Scene, parent + "Lace Return Corpse") };
                        break;
                    default:
                        throw new InvalidOperationException("Unknown boss heart.");
                }
            }

            internal void CompleteWorld()
            {
                PlayerData pd = PlayerData.instance;
                switch (Source.Completion)
                {
                    case BossHeartCompletion.BellBeast:
                        pd.defeatedBellBeast = true;
                        pd.bonebottomQuestBoardFixed = true;
                        Exit.SetState("End");
                        break;
                    case BossHeartCompletion.Unravelled:
                        pd.wardBossDefeated = true;
                        if (!pd.wardBossHatchOpened)
                        {
                            Exit.SendEvent("HEART COLLECTED");
                        }
                        break;
                    case BossHeartCompletion.LaceTower:
                        pd.defeatedLaceTower = true;
                        if (!pd.laceTowerDoorOpened)
                        {
                            Exit.SendEvent("DEFEATED LACE");
                        }
                        break;
                }
                foreach (GameObject obj in Disable)
                {
                    obj.SetActive(false);
                }
                foreach (GameObject obj in Enable)
                {
                    obj.SetActive(true);
                }
                EventRegister.SendEvent("HEART COLLECTED");
                if (Quest && !Quest.IsAccepted && !Quest.IsCompleted)
                {
                    Quest.BeginQuest(null, showPrompt: false);
                }
                HeroController.instance.SetBenchRespawn(Checkpoint, Source.ReturnScene, 0);
            }

            internal IEnumerator RefreshBellway(SaveState state)
            {
                if (!Bellway || SaveState.Instance != state || !Scene.isLoaded)
                {
                    yield break;
                }
                if (!(bool)AdditiveLoaded.GetValue(Bellway))
                {
                    try
                    {
                        AdditiveAlt.SetValue(Bellway, false);
                        AdditiveStart.Invoke(Bellway, null);
                    }
                    catch (Exception ex)
                    {
                        RandomizerPlugin.Log?.LogError(
                            "[RANDOMIZER] Bellway load failed: " + ex);
                        yield break;
                    }
                }
                float deadline = Time.realtimeSinceStartup + 20f;
                while (Bellway && SaveState.Instance == state && Scene.isLoaded &&
                    !(bool)AdditiveLoaded.GetValue(Bellway) &&
                    Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }
                if (!Bellway || SaveState.Instance != state || !Scene.isLoaded)
                {
                    yield break;
                }
                if (!(bool)AdditiveLoaded.GetValue(Bellway))
                {
                    RandomizerPlugin.Log?.LogWarning(
                        "[RANDOMIZER] Bellway load did not finish. The arena exits remain open.");
                    yield break;
                }
                if (BossLoader)
                {
                    BossLoader.enabled = false;
                }
            }
        }

        private static void ValidateBellBeastTargets(
            PlayMakerFSM battle, Scene scene, GameObject[] cameras)
        {
            FsmState end = battle.Fsm.GetState("End");
            for (int i = 0; i < 2; i++)
            {
                ActivateGameObject camera = (ActivateGameObject)end.Actions[i];
                GameObject gate = RequiredObject(scene, "Boss Scene/Threaded Bell Gate" +
                    (i == 0 ? "" : " (1)"));
                SendEventByName open = (SendEventByName)end.Actions[i + 2];
                PlayMakerFSM control = gate.GetComponents<PlayMakerFSM>()
                    .SingleOrDefault(fsm => fsm.FsmName == "Control");
                if (!camera.Enabled || camera.activate == null || camera.activate.Value ||
                    !TargetsObject(camera.gameObject, cameras[i]) ||
                    !open.Enabled || open.eventTarget == null ||
                    open.eventTarget.target != FsmEventTarget.EventTarget.GameObject ||
                    !TargetsObject(open.eventTarget.gameObject, gate) ||
                    control?.Fsm.GlobalTransitions?.Any(t =>
                        IsEventNamed(t.FsmEvent, "BG OPEN") && t.ToState == "Open") != true)
                {
                    throw new InvalidOperationException("A Bell Beast gate target did not match.");
                }
            }
        }

        private static bool TargetsObject(FsmOwnerDefault owner, GameObject target)
        {
            return owner?.OwnerOption == OwnerDefaultOption.SpecifyGameObject &&
                owner.GameObject?.Value == target;
        }

        private static bool MatchesBellBeastExit(Fsm fsm, string activeState)
        {
            FsmState end = fsm?.GetState("End");
            return HasActions(end, typeof(ActivateGameObject), typeof(ActivateGameObject),
                    typeof(SendEventByName), typeof(SendEventByName),
                    typeof(SetPlayerDataBoolAction), typeof(SetPlayerDataBoolAction),
                    typeof(AudioPlayerOneShotSingle)) &&
                ((SendEventByName)end.Actions[2]).sendEvent?.Value == "BG OPEN" &&
                ((SendEventByName)end.Actions[3]).sendEvent?.Value == "BG OPEN" &&
                IsPlayerBool(end.Actions[4], "defeatedBellBeast") &&
                IsPlayerBool(end.Actions[5], "bonebottomQuestBoardFixed") &&
                activeState == "Activate Silk Heart";
        }

        private static bool MatchesHatchExit(Fsm fsm, string activeState, bool opened)
        {
            FsmState waiting = fsm?.GetState("State 1");
            FsmState open = fsm?.GetState("State 2");
            return HasActions(open, typeof(SetPlayerDataBoolAction), typeof(CallMethodProper)) &&
                IsPlayerBool(open.Actions[0], "wardBossHatchOpened") &&
                IsMethod(open.Actions[1], "Trapdoor", "OpenDoorCustom") &&
                waiting?.Transitions?.Any(t => IsEventNamed(t.FsmEvent, "HEART COLLECTED") &&
                    t.ToState == open.Name) == true &&
                (activeState == waiting.Name || opened);
        }

        private static bool MatchesLaceExit(Fsm fsm, string activeState, bool opened)
        {
            FsmState init = fsm?.GetState("Init");
            FsmState waiting = fsm?.GetState("Defeated Lace");
            FsmState open = fsm?.GetState("Open");
            return HasActions(init, typeof(PlayerDataBoolTest), typeof(PlayerDataBoolTest)) &&
                ((PlayerDataBoolTest)init.Actions[1]).boolName?.Value == "defeatedLaceTower" &&
                HasOnlyTransitions(init,
                    Tuple.Create("OPENED", fsm.GetState("Opened")),
                    Tuple.Create("DEFEATED LACE", waiting)) &&
                HasActions(waiting, typeof(FindNamedChild), typeof(Trigger2dEvent)) &&
                HasOnlyTransition(waiting, "ENTER", open) &&
                HasActions(open, typeof(SetPlayerDataBoolAction), typeof(CallMethodProper)) &&
                IsPlayerBool(open.Actions[0], "laceTowerDoorOpened") &&
                IsMethod(open.Actions[1], "Gate", "Open") &&
                (activeState == init.Name || activeState == waiting.Name || opened);
        }

        private static bool IsPlayerBool(FsmStateAction action, string name)
        {
            return action is SetPlayerDataBoolAction set && set.Enabled &&
                set.boolName?.Value == name && set.value != null && set.value.Value;
        }

        private static bool IsMethod(FsmStateAction action, string type, string method)
        {
            return action is CallMethodProper call && call.Enabled &&
                call.gameObject?.OwnerOption == OwnerDefaultOption.UseOwner &&
                call.behaviour?.Value == type && call.methodName?.Value == method &&
                !call.EveryFrame;
        }

        private sealed class CollectBossHeart : FsmStateAction
        {
            private readonly BossHeartSource source;
            private readonly FsmStateAction[] original;
            private bool completed;

            internal CollectBossHeart(BossHeartSource source, FsmStateAction[] original)
            {
                this.source = source;
                this.original = original;
            }

            private void ResumeSequence(Exception error)
            {
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Using the Silk Heart sequence: " + error.Message);
                State.Actions = original;
                foreach (FsmStateAction action in original)
                {
                    action.Init(State);
                }
                Fsm.SetState(State.Name);
            }

            public override void OnEnter()
            {
                if (completed)
                {
                    Owner.SetActive(false);
                    return;
                }

                HeartContactCompletion completion;
                try
                {
                    if (!IsActive(source.LocationName) || PlayerData.instance == null ||
                        !HeroController.instance || !GameManager.instance)
                    {
                        throw new InvalidOperationException("The randomizer session is not ready.");
                    }
                    completion = new HeartContactCompletion(source);
                }
                catch (Exception ex)
                {
                    ResumeSequence(ex);
                    return;
                }

                SaveState state = SaveState.Instance;
                try
                {
                    completion.CompleteWorld();
                }
                catch (Exception ex)
                {
                    ResumeSequence(ex);
                    return;
                }
                completed = true;
                Fsm.Variables.FindFsmBool("Activated").Value = true;
                Owner.SetActive(false);
                try
                {
                    state.CheckLocation(source.LocationName);
                }
                catch (Exception ex)
                {
                    RandomizerPlugin.Log?.LogError(
                        "[RANDOMIZER] Silk Heart check failed: " + ex);
                }
                finally
                {
                    try
                    {
                        GameManager.instance.SaveGame(null);
                    }
                    catch (Exception ex)
                    {
                        RandomizerPlugin.Log?.LogError(
                            "[RANDOMIZER] Silk Heart save failed: " + ex);
                        RandomizerPlugin.Instance?.RequestDisconnectSave();
                    }
                }
                if (completion.Bellway)
                {
                    RandomizerPlugin.Instance.StartCoroutine(completion.RefreshBellway(state));
                }
            }
        }
    }
}
