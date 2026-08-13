using SilksongRandomizer.Patches;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilksongRandomizer
{
    internal static class Utils
    {
        internal static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            List<string> names = new List<string>();
            for (Transform current = transform;
                 current != null;
                 current = current.parent)
            {
                names.Add(current.name);
            }

            names.Reverse();
            return string.Join("/", names);
        }

        public static bool ForceCrest(string toolName)
        {
            ToolCrest crest = ToolItemManager.GetCrestByName(toolName);
            if (crest == null)
            {
                Debug.LogWarning("[RANDOMIZER] Could not find ToolCrest ScriptableObject: " + toolName);
                return false;
            }

            bool previousUnlockBypass =
                ToolPatches.canCrestBeUnlockedByRandomizer;
            ToolPatches.canCrestBeUnlockedByRandomizer = true;
            try
            {
                // Initialize the native ToolCrestsData entry and default slots
                // before equipping an AP-owned crest. The direct randomizer
                // path avoids AutoEquip's inventory-pane/transfer side effect.
                crest.Unlock();
                return ToolPatches.SetRandomizerCrest(crest, false);
            }
            finally
            {
                ToolPatches.canCrestBeUnlockedByRandomizer =
                    previousUnlockBypass;
            }
        }

        public static T FindToolScriptableObject<T>(string toolName) where T : ScriptableObject
        {
            T[] tools = Resources.FindObjectsOfTypeAll<T>();

            for (int i = 0; i < tools.Length; i++)
            {
                T tool = tools[i];

                if (tool == null)
                {
                    continue;
                }

                if (tool.name == toolName)
                {
                    return tool;
                }
            }

            for (int i = 0; i < tools.Length; i++)
            {
                T tool = tools[i];

                if (tool == null)
                {
                    continue;
                }

                if (string.Equals(tool.name, toolName, StringComparison.OrdinalIgnoreCase))
                {
                    return tool;
                }
            }

            return null;
        }
    }
}
