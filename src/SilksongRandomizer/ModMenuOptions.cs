using System;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;

namespace SilksongRandomizer
{
    internal static class ModMenuOptions
    {
        internal const string PluginId = "org.silksong-modding.modmenu";

        private sealed class MenuSetting
        {
            internal readonly string Label;
            internal MenuSetting(string label) => Label = label;
        }

        internal static ConfigDescription Describe(string group, string label, string description,
            AcceptableValueBase range = null)
        {
            object[] tags = { new MenuSetting(label) };
            if (Chainloader.PluginInfos.TryGetValue(PluginId, out var plugin) && plugin.Instance != null)
            {
                Assembly assembly = plugin.Instance.GetType().Assembly;
                Type text = assembly.GetType("Silksong.ModMenu.Elements.LocalizedText");
                Type subgroup = assembly.GetType("Silksong.ModMenu.Plugin.ConfigEntrySubgroup");
                if (text != null && subgroup != null)
                {
                    object title = text.GetMethod("Raw").Invoke(null, new object[] { group });
                    object page = Activator.CreateInstance(subgroup, title, Array.CreateInstance(text, 0));
                    tags = new[] { tags[0], page };
                }
            }
            return new ConfigDescription(description, range, tags);
        }

        internal static void Initialize(Harmony harmony, Assembly assembly)
        {
            harmony.Patch(AccessTools.Method(assembly.GetType("Silksong.ModMenu.Plugin.ConfigEntryFactory", true),
                    "GenerateMenuElement"),
                prefix: new HarmonyMethod(typeof(ModMenuOptions), nameof(FilterSetting)));
            harmony.Patch(AccessTools.Method(assembly.GetType("Silksong.ModMenu.Plugin.ConfigEntryFactoryExtensions", true),
                    "LabelName"),
                postfix: new HarmonyMethod(typeof(ModMenuOptions), nameof(NameSetting)));
        }

        private static MenuSetting Setting(ConfigEntryBase entry) =>
            entry.Description.Tags.OfType<MenuSetting>().FirstOrDefault();

        private static bool FilterSetting(ConfigEntryBase entry, object[] __args, ref bool __result)
        {
            if (entry.ConfigFile != RandomizerPlugin.Instance?.Config || Setting(entry) != null)
                return true;
            __args[1] = null;
            __result = false;
            return false;
        }

        private static void NameSetting(ConfigEntryBase __0, ref string __result)
        {
            if (__0.ConfigFile == RandomizerPlugin.Instance?.Config && Setting(__0) is MenuSetting setting)
                __result = setting.Label;
        }
    }
}
