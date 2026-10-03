using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SilksongRandomizer.Patches
{
    internal static class NpcSoulPatches
    {
        private static readonly Dictionary<GameObject, (SaveState state, string[] npcs)> Hidden =
            new Dictionary<GameObject, (SaveState, string[])>();

        private static bool Hold(Transform transform)
        {
            if (!NpcSoulState.FindActor(transform, out GameObject actor, out string[] npcs) ||
                !NpcSoulState.MissingAny(SaveState.Instance, npcs)) return false;
            Hidden[actor] = (SaveState.Instance, npcs);
            actor.SetActive(false);
            return true;
        }

        private static readonly List<GameObject> Actors = new List<GameObject>();
        private static bool initialized;
        private static SaveState boundState;
        private static string boundConfiguration;

        private static void Scan(Scene scene)
        {
            if (string.IsNullOrEmpty(SaveState.Instance?.npcSoulsJson) || SaveState.Instance.npcSoulsJson == "[]") return;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                    if (NpcSoulState.TryGetActor(scene.name, Utils.GetHierarchyPath(transform), out _))
                        Actors.Add(transform.gameObject);
            if (string.Equals(scene.name, "Tut_04", StringComparison.OrdinalIgnoreCase))
            {
                string[] shamans = { "Caretaker", "Chapel Maid", "Bell Hermit" };
                var missing = new List<string>();
                foreach (string npc in shamans)
                    if (NpcSoulState.IsMissing(SaveState.Instance, npc)) missing.Add(npc);
                RandomizerPlugin.Log?.LogInfo("[RANDOMIZER] Shaman scene NPC Souls: " +
                    (missing.Count == 0 ? "all available" : "missing " + string.Join(", ", missing)));
            }
        }

        internal static void Update()
        {
            if (!initialized)
            {
                initialized = true;
                SceneManager.sceneLoaded += (scene, mode) => Scan(scene);
            }
            if (boundState != SaveState.Instance || boundConfiguration != SaveState.Instance?.npcSoulsJson)
            {
                boundState = SaveState.Instance;
                boundConfiguration = SaveState.Instance?.npcSoulsJson;
                Actors.Clear();
                for (int i = 0; i < SceneManager.sceneCount; i++) Scan(SceneManager.GetSceneAt(i));
            }
            for (int i = Actors.Count - 1; i >= 0; i--)
                if (Actors[i] == null) Actors.RemoveAt(i);
                else if (Actors[i].activeInHierarchy) Hold(Actors[i].transform);
            if (Hidden.Count == 0) return;
            var release = new List<GameObject>();
            foreach (var pair in Hidden)
                if (pair.Key == null || pair.Value.state != SaveState.Instance ||
                    !NpcSoulState.MissingAny(pair.Value.state, pair.Value.npcs)) release.Add(pair.Key);
            foreach (GameObject actor in release)
            {
                var saved = Hidden[actor];
                Hidden.Remove(actor);
                if (actor != null && saved.state == SaveState.Instance) actor.SetActive(true);
            }
        }

        [HarmonyPatch(typeof(FsmState), nameof(FsmState.OnEnter))]
        private static class WakeWithoutSherma
        {
            private static bool Prefix(FsmState __instance)
            {
                Fsm fsm = __instance.Fsm;
                if (__instance.Name != "Dialogue A" || fsm?.GameObject == null ||
                    fsm.Name != "Cutscene Control" || fsm.GameObject.name != "door_act3_wakeUp" ||
                    !string.Equals(fsm.GameObject.scene.name, "Song_Enclave", StringComparison.OrdinalIgnoreCase) ||
                    !NpcSoulState.IsMissing(SaveState.Instance, "Sherma")) return true;
                HeroController.instance?.RegainControl();
                fsm.SetState("Dialogue End");
                return false;
            }
        }

        [HarmonyPatch(typeof(InteractableBase), "get_IsBlocked")]
        private static class BlockInteraction
        {
            private static void Postfix(InteractableBase __instance, ref bool __result)
            {
                if (__instance is NPCControlBase && !NpcSoulState.CanInteract(__instance.transform)) __result = true;
            }
        }

        [HarmonyPatch]
        private static class StartDialogue
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(NPCControlBase), nameof(NPCControlBase.StartDialogueMove));
                yield return AccessTools.Method(typeof(NPCControlBase), nameof(NPCControlBase.StartDialogueImmediately));
            }
            private static bool Prefix(NPCControlBase __instance) => NpcSoulState.CanInteract(__instance.transform);
        }

        [HarmonyPatch(typeof(NPCControlBase), "OnEnable")]
        private static class EnableNpc
        {
            private static void Postfix(NPCControlBase __instance) => Hold(__instance.transform);
        }

        [HarmonyPatch(typeof(PlayMakerFSM), "OnEnable")]
        private static class EnableNpcFsm
        {
            private static bool Prefix(PlayMakerFSM __instance) => !Hold(__instance.transform);
        }
    }
}
