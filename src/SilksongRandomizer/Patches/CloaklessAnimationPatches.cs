using System;
using HarmonyLib;

namespace SilksongRandomizer.Patches
{
    [HarmonyPatch(typeof(HeroAnimationController), nameof(HeroAnimationController.GetClip))]
    internal static class CloaklessAnimationPatches
    {
        private static tk2dSpriteAnimationClip cachedOriginal;
        private static tk2dSpriteAnimationClip cachedStandard;
        private static tk2dSpriteAnimationClip cachedFixed;

        private static bool IsWakeAnimation(string name) =>
            name == "Prostrate Rise" || name == "Prostrate Rise Slow";

        [HarmonyPostfix]
        private static void Postfix(HeroAnimationController __instance, string clipName,
            ref tk2dSpriteAnimationClip __result)
        {
            if (!IsWakeAnimation(clipName) || SaveState.Instance?.IsRoomBound != true ||
                PlayerData.instance?.CurrentCrestID != "Cloakless" ||
                __instance.animator == null || __result == null) return;

            var standard = __instance.animator.GetClipByName(clipName);
            if (ReferenceEquals(__result, cachedOriginal) && ReferenceEquals(standard, cachedStandard))
            {
                __result = cachedFixed;
                return;
            }
            cachedOriginal = __result;
            cachedStandard = standard;
            cachedFixed = RestoreWakeEvents(__result, standard);
            __result = cachedFixed;
        }

        internal static tk2dSpriteAnimationClip RestoreWakeEvents(
            tk2dSpriteAnimationClip original, tk2dSpriteAnimationClip standard)
        {
            if (original == null || standard == null || ReferenceEquals(original, standard) ||
                !IsWakeAnimation(original.name) || standard.name != original.name ||
                original.wrapMode != tk2dSpriteAnimationClip.WrapMode.Once ||
                standard.wrapMode != tk2dSpriteAnimationClip.WrapMode.Once ||
                original.frames == null || original.frames.Length < 3 ||
                standard.frames == null || standard.frames.Length < 3) return original;

            foreach (var frame in original.frames)
                if (frame == null || frame.triggerEvent) return original;
            int eventCount = 0;
            foreach (var frame in standard.frames)
            {
                if (frame == null) return original;
                if (frame.triggerEvent) eventCount++;
            }
            if (eventCount != 2) return original;

            var result = new tk2dSpriteAnimationClip(original);
            int previousFrame = -1;
            for (int i = 0; i < standard.frames.Length; i++)
            {
                var source = standard.frames[i];
                if (!source.triggerEvent) continue;
                int index = (int)Math.Round((double)i * (result.frames.Length - 1) /
                    (standard.frames.Length - 1));
                if (index <= previousFrame) return original;
                var target = result.frames[index];
                target.triggerEvent = true;
                target.eventInfo = source.eventInfo;
                target.eventInt = source.eventInt;
                target.eventFloat = source.eventFloat;
                previousFrame = index;
            }
            return result;
        }
    }
}
