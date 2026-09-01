using HarmonyLib;
using System;
using UnityEngine;

namespace SilksongRandomizer.Patches
{
    internal static class MottledSkarrSourcePatches
    {
        private const string CurveclawLocation = "Curveclaw";
        private const string CurveclawScene = "Ant_21";
        private const string CurveclawObject = "ant_item_string";
        private const float CurveclawX = 44.78f;
        private const float CurveclawY = 76.93f;

        private const string FracturedMaskLocation = "Fractured Mask";
        private const string FracturedMaskScene = "Ant_Merchant";
        private const string FracturedMaskItem = "Fractured Mask";
        private const string FracturedMaskPath =
            "_NPCs/Ant Merchant States/Ant Merchant Dead/" +
            "Collectable Item Pickup";
        private const float FracturedMaskX = 20.9f;
        private const float FracturedMaskY = 15.19f;
        private const float PositionTolerance = 0.25f;

        private static bool IsActiveLocation(
            SaveState state,
            string locationName)
        {
            return state != null &&
                   state.IsRandomized(ItemType.Tool) &&
                   state.IsLocationEnabled(locationName) &&
                   state.IsLocationInSeed(locationName);
        }

        private static bool IsAt(
            Transform transform,
            float x,
            float y)
        {
            if (transform == null)
            {
                return false;
            }

            Vector2 offset =
                (Vector2)transform.position - new Vector2(x, y);
            return offset.sqrMagnitude <=
                   PositionTolerance * PositionTolerance;
        }

        private static bool IsCurveclawFallback(
            ToolGameObjectActivator activator)
        {
            return activator != null &&
                   activator.gameObject != null &&
                   string.Equals(
                       activator.gameObject.scene.name,
                       CurveclawScene,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       activator.gameObject.name,
                       CurveclawObject,
                       StringComparison.Ordinal) &&
                   IsAt(activator.transform, CurveclawX, CurveclawY);
        }

        private static bool IsFracturedMaskFallback(
            CollectableItemPickup pickup)
        {
            return pickup != null &&
                   pickup.gameObject != null &&
                   pickup.Item != null &&
                   string.Equals(
                       pickup.gameObject.scene.name,
                       FracturedMaskScene,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       pickup.Item.name,
                       FracturedMaskItem,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       Utils.GetHierarchyPath(pickup.transform),
                       FracturedMaskPath,
                       StringComparison.Ordinal) &&
                   IsAt(
                       pickup.transform,
                       FracturedMaskX,
                       FracturedMaskY);
        }

        [HarmonyPatch(typeof(ToolGameObjectActivator), "Evaulate")]
        private static class CurveclawFallbackPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(ToolGameObjectActivator __instance)
            {
                SaveState state = SaveState.Instance;
                if (!IsActiveLocation(state, CurveclawLocation) ||
                    !IsCurveclawFallback(__instance))
                {
                    return true;
                }

                bool isChecked =
                    state.IsLocationChecked(CurveclawLocation);
                Traverse fields = Traverse.Create(__instance);
                GameObject activate = fields
                    .Field("activateGameObject")
                    .GetValue<GameObject>();
                GameObject deactivate = fields
                    .Field("deactivateGameObject")
                    .GetValue<GameObject>();

                if (activate != null)
                {
                    activate.SetActive(!isChecked);
                }
                if (deactivate != null)
                {
                    deactivate.SetActive(isChecked);
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(CollectableItemPickup), "CheckActivation")]
        private static class FracturedMaskFallbackPatch
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(CollectableItemPickup __instance)
            {
                SaveState state = SaveState.Instance;
                if (!IsActiveLocation(state, FracturedMaskLocation) ||
                    !IsFracturedMaskFallback(__instance))
                {
                    return true;
                }

                bool isChecked =
                    state.IsLocationChecked(FracturedMaskLocation);
                __instance.SetActivation(isChecked);
                if (isChecked)
                {
                    __instance.OnPickedUp?.Invoke();
                    __instance.OnPreviouslyPickedUp?.Invoke();
                    __instance.gameObject.SetActive(false);
                }
                return false;
            }
        }
    }
}
