using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TeamCherry.Localization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SilksongRandomizer.Patches
{
    [HarmonyPatch(typeof(BasicNPC), "OnEndDialogue")]
    internal static class LoreTabletPatches
    {
        private const string ShellgraveLocationName =
            "Shellwood - Shellgrave Inscription";
        private const string BellhartOuterSignLocationName =
            "Bellhart - Outer Sign";
        private const string BlastedStepsEntranceSignLocationName =
            "Blasted Steps - Shellwood Entrance Sign";
        private const string TerminusCommandmentLocationName =
            "The Cradle - Terminus Commandment";

        private sealed class InactiveSourceRecord
        {
            internal readonly BasicNPC Npc;
            internal readonly LoreTabletManifest.Entry Entry;
            internal readonly Transform Parent;
            internal readonly Vector3 LocalPosition;
            internal readonly Quaternion LocalRotation;
            internal readonly Vector3 LocalScale;
            internal readonly bool ActiveSelf;

            internal InactiveSourceRecord(
                BasicNPC npc,
                LoreTabletManifest.Entry entry
            )
            {
                Npc = npc;
                Entry = entry;
                Parent = npc.transform.parent;
                LocalPosition = npc.transform.localPosition;
                LocalRotation = npc.transform.localRotation;
                LocalScale = npc.transform.localScale;
                ActiveSelf = npc.gameObject.activeSelf;
            }
        }

        private static readonly FieldInfo TalkTextField =
            AccessTools.Field(typeof(BasicNPC), "talkText");
        private static readonly Dictionary<int, LoreTabletManifest.Entry>
            DetachedEntries =
                new Dictionary<int, LoreTabletManifest.Entry>();
        private static InactiveSourceRecord detachedSource;
        private static BasicNPC inactiveSource;
        private static LoreTabletManifest.Entry inactiveEntry;
        private static int sourceSceneHandle = -1;

        [HarmonyPostfix]
        private static void Postfix(
            BasicNPC __instance,
            LocalisedString[] ___talkText
        )
        {
            SaveState state = SaveState.Instance;
            if (__instance == null ||
                state == null ||
                !state.IsRoomBound ||
                !state.IsRandomized(ItemType.LoreTablet) ||
                ___talkText == null ||
                ___talkText.Length == 0)
            {
                return;
            }

            LocalisedString firstText = ___talkText[0];
            LoreTabletManifest.Entry entry = null;
            DetachedEntries.TryGetValue(
                __instance.GetInstanceID(),
                out entry
            );
            if (entry == null)
            {
                entry = LoreTabletManifest.FindExactSource(
                    __instance.gameObject.scene.name,
                    Utils.GetHierarchyPath(__instance.transform),
                    firstText.Sheet,
                    firstText.Key
                );
            }
            if (entry == null ||
                !state.IsLocationEnabled(entry.LocationName) ||
                !state.IsLocationInSeed(entry.LocationName) ||
                state.IsLocationChecked(entry.LocationName))
            {
                return;
            }

            state.CheckLocation(entry.LocationName);
        }

        internal static bool ShouldUseInactiveSourceFallback(
            bool roomBound,
            bool randomized,
            bool enabled,
            bool inSeed,
            bool checkedLocation,
            bool blackThreadWorld,
            bool exactSource,
            bool sourceActive
        )
        {
            return roomBound &&
                   randomized &&
                   enabled &&
                   inSeed &&
                   !checkedLocation &&
                   blackThreadWorld &&
                   exactSource &&
                   !sourceActive;
        }

        internal static string InactiveFallbackLocationNameForScene(
            string sceneName
        )
        {
            if (string.Equals(
                    sceneName,
                    "Shellgrave",
                    StringComparison.OrdinalIgnoreCase))
            {
                return ShellgraveLocationName;
            }

            if (string.Equals(
                    sceneName,
                    "Belltown_07",
                    StringComparison.OrdinalIgnoreCase))
            {
                return BellhartOuterSignLocationName;
            }

            if (string.Equals(
                    sceneName,
                    "Coral_19",
                    StringComparison.OrdinalIgnoreCase))
            {
                return BlastedStepsEntranceSignLocationName;
            }

            if (string.Equals(
                    sceneName,
                    "Tube_Hub",
                    StringComparison.OrdinalIgnoreCase))
            {
                return TerminusCommandmentLocationName;
            }

            return null;
        }

        internal static void UpdateInactiveSources()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                ResetInactiveSources();
                return;
            }

            if (scene.handle != sourceSceneHandle)
            {
                ResetInactiveSources();
                sourceSceneHandle = scene.handle;
            }

            string locationName =
                InactiveFallbackLocationNameForScene(scene.name);
            if (locationName == null)
            {
                return;
            }

            SaveState state = SaveState.Instance;
            bool eligible = state != null &&
                ShouldUseInactiveSourceFallback(
                    state.IsRoomBound,
                    state.IsRandomized(ItemType.LoreTablet),
                    state.IsLocationEnabled(locationName),
                    state.IsLocationInSeed(locationName),
                    state.IsLocationChecked(locationName),
                    PlayerData.instance != null &&
                        PlayerData.instance.blackThreadWorld,
                    true,
                    false
                );
            if (detachedSource != null)
            {
                if (!eligible)
                {
                    RestoreDetachedSource();
                }
                return;
            }

            if (inactiveSource == null || inactiveEntry == null)
            {
                FindInactiveSource(scene, locationName);
            }

            bool exactSource = inactiveSource != null &&
                inactiveEntry != null;
            if (!ShouldUseInactiveSourceFallback(
                    state != null && state.IsRoomBound,
                    state != null &&
                        state.IsRandomized(ItemType.LoreTablet),
                    state != null &&
                        state.IsLocationEnabled(locationName),
                    state != null &&
                        state.IsLocationInSeed(locationName),
                    state != null &&
                        state.IsLocationChecked(locationName),
                    PlayerData.instance != null &&
                        PlayerData.instance.blackThreadWorld,
                    exactSource,
                    exactSource &&
                        inactiveSource.gameObject.activeInHierarchy
                ))
            {
                return;
            }

            detachedSource = new InactiveSourceRecord(
                inactiveSource,
                inactiveEntry
            );
            DetachedEntries[inactiveSource.GetInstanceID()] =
                inactiveEntry;
            inactiveSource.transform.SetParent(null, true);
            inactiveSource.gameObject.SetActive(true);
        }

        private static void FindInactiveSource(
            Scene scene,
            string locationName
        )
        {
            if (!scene.IsValid() || TalkTextField == null)
            {
                return;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (BasicNPC npc in
                         root.GetComponentsInChildren<BasicNPC>(true))
                {
                    LocalisedString[] talkText =
                        TalkTextField.GetValue(npc) as LocalisedString[];
                    if (talkText == null || talkText.Length == 0)
                    {
                        continue;
                    }

                    LoreTabletManifest.Entry entry =
                        LoreTabletManifest.FindExactSource(
                            scene.name,
                            Utils.GetHierarchyPath(npc.transform),
                            talkText[0].Sheet,
                            talkText[0].Key
                        );
                    if (entry == null ||
                        !string.Equals(
                            entry.LocationName,
                            locationName,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    inactiveSource = npc;
                    inactiveEntry = entry;
                    return;
                }
            }
        }

        private static void RestoreDetachedSource()
        {
            InactiveSourceRecord record = detachedSource;
            detachedSource = null;
            if (record == null || record.Npc == null)
            {
                DetachedEntries.Clear();
                return;
            }

            DetachedEntries.Remove(record.Npc.GetInstanceID());
            if (record.Parent == null)
            {
                record.Npc.gameObject.SetActive(false);
                return;
            }

            Transform transform = record.Npc.transform;
            transform.SetParent(record.Parent, false);
            transform.localPosition = record.LocalPosition;
            transform.localRotation = record.LocalRotation;
            transform.localScale = record.LocalScale;
            record.Npc.gameObject.SetActive(record.ActiveSelf);
        }

        internal static void ResetInactiveSources()
        {
            RestoreDetachedSource();
            DetachedEntries.Clear();
            inactiveSource = null;
            inactiveEntry = null;
            sourceSceneHandle = -1;
        }
    }
}
