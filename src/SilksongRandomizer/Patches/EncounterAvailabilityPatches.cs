using System;
using System.Linq;
using GlobalEnums;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;

namespace SilksongRandomizer.Patches
{
    internal static class EncounterAvailabilityPatches
    {
        [HarmonyPatch(typeof(DeactivateIfPlayerdataTrue), "ForceEvaluate")]
        private static class DocksLaceDeparturePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(DeactivateIfPlayerdataTrue __instance) =>
                !KeepDocksLace(__instance.gameObject.scene.name,
                    Utils.GetHierarchyPath(__instance.transform), __instance.boolName);
        }

        internal static bool KeepDocksLace(string scene, string path, string flag) =>
            SaveState.Instance != null && scene == "Bone_East_12" && path == "Boss Scene" &&
            (flag == "laceLeftDocks" || flag == "visitedCitadel" || flag == "encounteredLace1Grotto");

        [HarmonyPatch(typeof(PlayerDataBoolTest), nameof(PlayerDataBoolTest.OnEnter))]
        private static class DocksLaceIntroPatch
        {
            [HarmonyPrefix]
            private static void Prefix(PlayerDataBoolTest __instance)
            {
                Fsm fsm = __instance.Fsm;
                if (fsm?.GameObject != null && fsm.GameObject.scene.name == "Bone_East_12" &&
                    Utils.GetHierarchyPath(fsm.GameObject.transform) == "Boss Scene/Lace Boss1" &&
                    fsm.Name == "Control" && __instance.State.Name == "Grotto Refight Setup")
                    KeepDocksIntro(__instance.State);
            }
        }

        private static void KeepDocksIntro(FsmState state)
        {
            if (SaveState.Instance == null || state == null) return;
            foreach (var action in state.Actions.OfType<PlayerDataBoolTest>())
                if (action.boolName.Value == "encounteredLace1Grotto") action.isTrue = action.isFalse;
        }

        internal static bool CanStartRagingConchfly(string scene, string path) =>
            scene != "Coral_27" || path != "Battle Scene" ||
            ProgressionShufflePatches.StoryCredit("Boss: Great Conchflies", true);

        [HarmonyPatch(typeof(BattleScene), "Awake")]
        private static class RagingConchflyAvailabilityPatch
        {
            [HarmonyPostfix]
            private static void Postfix(BattleScene __instance)
            {
                if (!CanStartRagingConchfly(__instance.gameObject.scene.name,
                    Utils.GetHierarchyPath(__instance.transform))) __instance.gameObject.SetActive(false);
            }
        }

        [HarmonyPatch(typeof(BattleScene), nameof(BattleScene.StartBattle))]
        private static class RagingConchflyStartPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(BattleScene __instance) =>
                CanStartRagingConchfly(__instance.gameObject.scene.name, Utils.GetHierarchyPath(__instance.transform));
        }

        [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
        private static class BossEncounterAvailabilityPatch
        {
            [HarmonyPrefix]
            private static void Prefix(PlayMakerFSM __instance)
            {
                string scene = __instance.gameObject.scene.name;
                if (scene == "Coral_11" && __instance.FsmName == "Control" &&
                    Utils.GetHierarchyPath(__instance.transform) ==
                    "Black Thread States Thread Only Variant/Normal World/Boss Scene")
                    KeepGreatConchflies(__instance.Fsm.GetState("State"));
                else if (scene == "Bone_East_10_Room" && __instance.FsmName == "Ruckus Control" &&
                    Utils.GetHierarchyPath(__instance.transform) == "RestBench")
                    LogRhinogrundRoom(__instance);
                else if (scene == "Slab_14" && __instance.FsmName == "Control")
                    KeepSlabAmbush(__instance);
            }
        }

        private static void KeepSlabAmbush(PlayMakerFSM fsm)
        {
            if (SaveState.Instance == null) return;
            string path = Utils.GetHierarchyPath(fsm.transform);
            string soul = path == "Slab Prisoner Fly New" ? "Puny Penitent" :
                path == "Slab Prisoner Leaper New" ? "Penitent" : null;
            FsmState rest = fsm.Fsm.GetState("FG Rest");
            if (soul == null || rest == null || rest.Actions.Any(action => action is SlabAmbushWake)) return;
            var wake = new SlabAmbushWake { Soul = soul };
            wake.Init(rest);
            rest.Actions = rest.Actions.Concat(new FsmStateAction[] { wake }).ToArray();
        }

        private sealed class SlabAmbushWake : FsmStateAction
        {
            internal string Soul;
            private PersistentBoolItem chain;

            public override void OnEnter()
            {
                chain = Fsm.GameObject.scene.GetRootGameObjects()
                    .FirstOrDefault(obj => obj.name == "slab_item_chain")?.GetComponent<PersistentBoolItem>();
                OnUpdate();
            }

            public override void OnUpdate()
            {
                SaveState save = SaveState.Instance;
                if (save == null || chain == null || !chain.ItemData.Value ||
                    EnemySoulState.IsMissing(save, Soul)) return;
                Fsm.Event("KEY CHAIN BROKEN");
                Finish();
            }
        }

        private static void KeepGreatConchflies(FsmState state)
        {
            if (state == null || SaveState.Instance?.progressionShuffle?.TryGetBossDefeat(
                "Boss: Great Conchflies", out _) != true) return;
            state.Actions = state.Actions.Where(action =>
                !(action is PlayerDataBoolTrueAndFalse solo && solo.trueBool.Value == "coralDrillerSoloReady" &&
                  solo.falseBool.Value == "defeatedCoralDrillerSolo")).ToArray();
        }

        [HarmonyPatch(typeof(PlayerDataBoolTest), nameof(PlayerDataBoolTest.OnEnter))]
        private static class MarrowSkullTyrantStatePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(PlayerDataBoolTest __instance)
            {
                Fsm fsm = __instance.Fsm;
                if ((__instance.boolName.Value != "skullKingInvaded" && __instance.boolName.Value != "skullKingWillInvade") ||
                    fsm?.GameObject == null || fsm.GameObject.scene.name != "Bone_15" ||
                    fsm.Name != "Behaviour" || __instance.State?.Name != "State Check" ||
                    Utils.GetHierarchyPath(fsm.GameObject.transform) != "Boss Scene/Skull King" ||
                    SaveState.Instance?.progressionShuffle?.TryGetBossDefeat(
                        "Boss: Skull Tyrant (The Marrow)", out _) != true) return true;
                __instance.Finish();
                return false;
            }
        }

        internal static void UpdateRhinogrundInvasion(PlayerData data, MapZone zone)
        {
            if (!WorldInteractionPatches.IsActive || data == null || !data.rhinoChurchUnlocked ||
                data.churchRhinoKilled || !data.PilgrimsRestDoorBroken || zone == MapZone.WILDS ||
                data.respawnScene == "Bone_East_10_Room") return;
            data.rhinoRuckus = true;
            data.didRhinoRuckus = true;
        }

        private static string RhinogrundState(PlayerData data) =>
            $"apBound={WorldInteractionPatches.IsActive} released={data.rhinoChurchUnlocked} " +
            $"mechanismBroken={data.PilgrimsRestDoorBroken} killed={data.churchRhinoKilled} " +
            $"visitedCitadel={data.visitedCitadel} bench={data.respawnScene} " +
            $"rhinoRuckus={data.rhinoRuckus} didRhinoRuckus={data.didRhinoRuckus} " +
            $"rampageCompleted={data.rhinoRampageCompleted} blackThreadWorld={data.blackThreadWorld}";

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.TimePasses))]
        private static class RhinogrundInvasionPatch
        {
            [HarmonyPrefix]
            private static void Prefix(GameManager __instance, out bool __state) =>
                __state = __instance.playerData?.rhinoRuckus == true;

            [HarmonyPostfix]
            private static void Postfix(GameManager __instance, bool __state)
            {
                PlayerData data = __instance.playerData;
                if (SaveState.Instance == null || data == null) return;
                MapZone zone = __instance.GetCurrentMapZoneEnum();
                MapZone timePassZone = WorldInteractionPatches.TimePassZone(zone);
                bool afterNative = data.rhinoRuckus;
                UpdateRhinogrundInvasion(data, timePassZone);
                RandomizerPlugin.Log?.LogInfo(
                    $"[Rhinogrund] TimePasses: scene={__instance.sceneName} zone={zone} " +
                    $"timePassZone={timePassZone} before={__state} afterNative={afterNative} " +
                    RhinogrundState(data));
            }
        }

        private static void LogRhinogrundRoom(PlayMakerFSM fsm)
        {
            PlayerData data = PlayerData.instance;
            if (SaveState.Instance == null || data == null) return;
            var root = fsm.gameObject.scene.GetRootGameObjects()
                .FirstOrDefault(obj => obj.name == "Black Thread States");
            var rhino = root == null ? null : root.transform.Find("Normal World/Rhino");
            string actor = rhino == null ? "missing" :
                $"activeSelf={rhino.gameObject.activeSelf} activeInHierarchy={rhino.gameObject.activeInHierarchy}";
            RandomizerPlugin.Log?.LogInfo($"[Rhinogrund] Pilgrims Rest loaded: actor={actor} " +
                RhinogrundState(data));
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.TimePasses))]
        private static class SkullTyrantInvasionPatch
        {
            [HarmonyPostfix]
            private static void Postfix(GameManager __instance)
            {
                SaveState save = SaveState.Instance;
                PlayerData data = PlayerData.instance;
                if (save == null || data == null || data.skullKingKilled || data.blackThreadWorld ||
                    !ProgressionShufflePatches.StoryCredit("Boss: Skull Tyrant (The Marrow)", data.skullKingDefeated) ||
                    !(data.visitedCitadel || data.visitedCoral || data.visitedDustpens)) return;
                var progress = save.progressionShuffle;
                if (progress?.TryGetBossDefeat("Boss: Skull Tyrant (Bone Bottom)", out bool defeated) == true && defeated)
                    return;
                if (QuestManager.GetQuest("Soul Snare")?.IsAccepted == true &&
                    progress?.TryGetBossDefeat("Boss: Skull Tyrant (Bone Bottom)", out _) != true) return;
                MapZone zone = WorldInteractionPatches.TimePassZone(__instance.GetCurrentMapZoneEnum());
                if (zone != MapZone.BONETOWN && zone != MapZone.PATH_OF_BONE && zone != MapZone.MOSS_CAVE)
                    data.skullKingWillInvade = true;
            }
        }

        [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
        private static class NuuAvailabilityPatch
        {
            [HarmonyPrefix]
            private static void Prefix(PlayMakerFSM __instance)
            {
                if (SaveState.Instance == null ||
                    !string.Equals(__instance.gameObject.scene.name, "Halfway_01", StringComparison.OrdinalIgnoreCase)) return;
                string path = Utils.GetHierarchyPath(__instance.transform);
                if (path == "_NPCs/Hunter Fan Control" && __instance.FsmName == "Control")
                {
                    FsmState check = __instance.Fsm.GetState("Check");
                    if (check != null)
                        check.Actions = check.Actions.Where(action =>
                            !(action is PlayerDataBoolTest act3 && act3.boolName.Value == "blackThreadWorld"))
                            .Select(action => {
                                if (!(action is PlayerDataBoolTest home) || home.boolName.Value != "nuuIsHome") return action;
                                var replacement = new NuuHomeCheck();
                                replacement.Init(check);
                                return replacement;
                            }).ToArray();
                    KeepScrolls(__instance.Fsm.GetState("Left"));
                }
                else if (path == "_NPCs/Hunter Fan Control/Nuu" && __instance.FsmName == "Dialogue")
                    KeepScrolls(__instance.Fsm.GetState("Footsteps"));
            }
        }

        private static void KeepScrolls(FsmState state)
        {
            if (state == null) return;
            foreach (var action in state.Actions.OfType<ActivateGameObject>())
                if (action.gameObject.GameObject.Name == "Nuu_Scrolls") action.activate.Value = true;
        }

        private sealed class NuuHomeCheck : FsmStateAction
        {
            public override void OnEnter()
            {
                PlayerData data = PlayerData.instance;
                FullQuestBase quest = QuestManager.GetQuest("Journal");
                bool needsJournal = JournalRandomization.Enabled
                    ? !SaveState.Instance.IsLocationChecked(JournalRandomization.JournalLocation)
                    : !data.hasJournal;
                data.nuuIsHome = !data.MetHalfwayHunterFan || needsJournal || quest == null ||
                    (!quest.IsAccepted && !quest.IsCompleted) || (data.blackThreadWorld && !data.nuuIntroAct3);
                var scrolls = Fsm.GameObject.transform.Find("Nuu_Scrolls/Inspect Region");
                scrolls?.gameObject.SetActive(!data.nuuIsHome || NpcSoulState.IsMissing(SaveState.Instance, "Nuu"));
                Fsm.Event(data.nuuIsHome ? "HOME" : "AWAY");
                Finish();
            }
        }
    }
}
