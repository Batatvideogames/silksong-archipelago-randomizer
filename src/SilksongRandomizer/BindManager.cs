using System;
using System.Linq;
using UnityEngine;

namespace SilksongRandomizer
{
    internal static class BindManager
    {
        internal static bool Enabled(SaveState save) => save?.IsRoomBound == true && save.bindRandomization != "vanilla";

        internal static readonly string[] Crests = { "Hunter", "Wanderer", "Reaper", "Beast", "Architect", "Witch", "Shaman" };

        private static bool Owns(SaveState save, string crest) => Crests.Contains(crest) &&
            save.receivedItems.Contains(save.bindRandomization == "shared" ? "Bind" : crest + " Bind");

        internal static bool CanBind(SaveState save) => !Enabled(save) ||
            (PlayerData.instance?.IsAnyCursed == true ? HasUsableBind(save) :
                Owns(save, CrestNames.GetPublicCrestName(PlayerData.instance?.CurrentCrestID)));

        internal static bool HasUsableBind(SaveState save) => !Enabled(save) ||
            ToolItemManager.GetAllCrests().Any(crest => crest != null && crest.IsUnlocked &&
                Owns(save, CrestNames.GetPublicCrestName(crest.name)));

        internal static bool BlocksBoss(SaveState save, string boss) =>
            (boss == "Boss: Widow" || boss == "Boss: First Sinner" || boss == "Boss: Grand Mother Silk") &&
            !HasUsableBind(save);

        internal static bool IsShrine(string scene, string owner) =>
            (owner == "Crest Get Shrine" && (scene == "Chapel_Wanderer" || scene == "Ant_19" ||
                scene == "Greymoor_20c" || scene == "Tut_04" || scene == "Tut_05" || scene == "Under_20")) ||
            (owner == "Shrine Weaver Ability" && (scene == "Bone_East_05" || scene == "Shellwood_10" ||
                scene == "Abyss_08" || scene == "Mosstown_02" || scene == "Crawl_05" ||
                scene == "Slab_10b" || scene == "Under_18" || scene == "Greymoor_22")) ||
            (scene == "Under_18" && owner == "Shrine Weaver HarpoonDash") ||
            (scene == "Slab_10b" && (owner == "Shrine First Weaver" || owner == "Shrine First Weaver NPC")) ||
            (scene == "Cradle_03_Destroyed" && owner == "Silk Needle Spell Get");

        internal static bool CanInteract(Transform transform)
        {
            if (!Enabled(SaveState.Instance)) return true;
            for (Transform current = transform; current != null; current = current.parent)
                if (IsShrine(current.gameObject.scene.name, current.name))
                    return current.gameObject.scene.name == "Slab_10b"
                        ? HasUsableBind(SaveState.Instance) : CanBind(SaveState.Instance);
            return true;
        }
    }
}
