using System;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace SilksongRandomizer
{
    internal static class ModMenuOptions
    {
        internal const string PluginId = "org.silksong-modding.modmenu";

        private sealed class MenuSetting
        {
            internal readonly string Label;
            internal readonly Func<string> Description;
            internal MenuSetting(string label, Func<string> description)
            {
                Label = label;
                Description = description;
            }
        }

        internal static ConfigDescription Describe(string group, string label, string description,
            AcceptableValueBase range = null, Func<string> menuDescription = null)
        {
            object[] tags = { new MenuSetting(label, menuDescription) };
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
                prefix: new HarmonyMethod(typeof(ModMenuOptions), nameof(FilterSetting)),
                postfix: new HarmonyMethod(typeof(ModMenuOptions), nameof(DescribeSetting)));
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

        private static void DescribeSetting(ConfigEntryBase entry, object menuElement, bool __result)
        {
            if (!__result || entry.ConfigFile != RandomizerPlugin.Instance?.Config ||
                !(Setting(entry)?.Description is Func<string> describe)) return;
            object element = menuElement;
            if (!(AccessTools.Field(element.GetType(), "DescriptionText")?.GetValue(element) is Text text)) return;
            text.fontSize = 20;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = describe();
            EventHandler<SettingChangedEventArgs> changed = (_, args) =>
            {
                if (args.ChangedSetting == entry && text != null) text.text = describe();
            };
            entry.ConfigFile.SettingChanged += changed;
            element.GetType().GetEvent("OnDispose").AddEventHandler(element,
                new Action(() => entry.ConfigFile.SettingChanged -= changed));
        }

        private static void NameSetting(ConfigEntryBase __0, ref string __result)
        {
            if (__0.ConfigFile == RandomizerPlugin.Instance?.Config && Setting(__0) is MenuSetting setting)
                __result = setting.Label;
        }
    }
}
