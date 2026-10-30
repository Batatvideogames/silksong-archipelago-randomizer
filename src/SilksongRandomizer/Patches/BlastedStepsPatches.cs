using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SilksongRandomizer.Patches
{
    internal static class BlastedStepsPatches
    {
        private static bool Applies(Lever_tk2d lever) =>
            SaveState.Instance?.IsRoomBound == true && lever.name == "Song_lever_side" &&
            GameManager.GetBaseSceneName(lever.gameObject.scene.name) == "Coral_03";

        [HarmonyPatch(typeof(Lever_tk2d), "Start")]
        private static class StartPatch
        {
            [HarmonyPrefix]
            private static void Prefix(Lever_tk2d __instance)
            {
                if (!Applies(__instance) || __instance.GetComponent<StationaryCages>() != null) return;
                StationaryCages cages = __instance.gameObject.AddComponent<StationaryCages>();
                try
                {
                    PlayMakerFSM[] gates = (PlayMakerFSM[])AccessTools.Field(typeof(Lever_tk2d), "fsmGates")
                        .GetValue(__instance);
                    Transform[] holders = Enumerable.Range(1, 4).Select(i =>
                        gates.Single(g => g != null && g.name == "Hornet_cage_rect move" + i)
                            .transform.Find("Holder")).ToArray();
                    if (holders.Any(h => h == null || h.Find("Plat Collider")?.GetComponent<BoxCollider2D>() == null ||
                        h.GetComponentsInChildren<PersistentBoolItem>(true).Length != 0))
                        throw new InvalidOperationException("Unexpected cage layout.");
                    cages.Prepare(holders);
                }
                catch (Exception ex)
                {
                    cages.Clear();
                    RandomizerPlugin.Log?.LogError("[RANDOMIZER] Could not preserve the Blasted Steps cages: " + ex);
                }
            }

            [HarmonyPostfix]
            private static void Postfix(Lever_tk2d __instance, bool ___activated)
            {
                StationaryCages cages = __instance.GetComponent<StationaryCages>();
                if (cages == null) return;
                PersistentBoolItem persistent = __instance.GetComponent<PersistentBoolItem>();
                if (persistent != null) persistent.OnSetSaveState += value => { if (value) cages.Show(); };
                if (___activated) cages.Show();
            }
        }

        [HarmonyPatch(typeof(Lever_tk2d), nameof(Lever_tk2d.Hit))]
        private static class HitPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(Lever_tk2d __instance, ref IHitResponder.HitResponse __result)
            {
                if (!Applies(__instance) || __instance.GetComponent<StationaryCages>()?.Ready == true) return true;
                __result = IHitResponder.Response.None;
                return false;
            }

            [HarmonyPostfix]
            private static void Postfix(Lever_tk2d __instance, bool ___activated)
            {
                if (___activated) __instance.GetComponent<StationaryCages>()?.Show();
            }
        }

        private sealed class StationaryCages : MonoBehaviour
        {
            private GameObject group;
            internal bool Ready { get; private set; }

            internal void Prepare(Transform[] holders)
            {
                group = new GameObject("Blasted Steps Stationary Cages");
                group.SetActive(false);
                SceneManager.MoveGameObjectToScene(group, gameObject.scene);
                foreach (Transform holder in holders)
                {
                    GameObject copy = Instantiate(holder.gameObject, group.transform, true);
                    copy.name = holder.parent.name;
                }
                Ready = true;
            }

            internal void Show()
            {
                if (Ready && group != null && !group.activeSelf) group.SetActive(true);
            }

            internal void Clear()
            {
                Ready = false;
                if (group != null) Destroy(group);
                group = null;
            }

            private void OnDestroy() => Clear();
        }
    }
}
