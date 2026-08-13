using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Converters;
using Archipelago.MultiClient.Net.Enums;
using HarmonyLib;
using Newtonsoft.Json.Converters;
using System;

namespace SilksongRandomizer.Patches
{
    // Unity can treat this converter as serializer-wide. The library's original
    // implementation also claims string and int, then returns a Permissions value
    // for them, which breaks RoomInfo string collections.
    internal static class ArchipelagoJsonCompatibilityPatches
    {
        internal static bool IsPermissionsType(Type objectType)
        {
            return objectType == typeof(Permissions);
        }

        internal static bool IsNumericProtocolEnumType(Type objectType)
        {
            Type targetType = Nullable.GetUnderlyingType(objectType) ?? objectType;
            return targetType == typeof(ItemsHandlingFlags) ||
                targetType == typeof(ArchipelagoClientState) ||
                targetType == typeof(HintStatus);
        }

        internal static bool IsArchipelagoPacketType(Type objectType)
        {
            // The converter constructs concrete packet types internally, so it
            // must only claim the abstract socket-list element type. Claiming a
            // concrete packet here would recursively invoke the converter.
            return objectType == typeof(ArchipelagoPacketBase);
        }

        [HarmonyPatch(typeof(ArchipelagoPacketConverter), nameof(ArchipelagoPacketConverter.CanConvert))]
        private static class ArchipelagoPacketConverter_CanConvert_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(Type objectType, ref bool __result)
            {
                __result = IsArchipelagoPacketType(objectType);
                return false;
            }
        }

        [HarmonyPatch(typeof(PermissionsEnumConverter), nameof(PermissionsEnumConverter.CanConvert))]
        private static class PermissionsEnumConverter_CanConvert_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(Type objectType, ref bool __result)
            {
                __result = IsPermissionsType(objectType);
                return false;
            }
        }

        [HarmonyPatch(typeof(StringEnumConverter), nameof(StringEnumConverter.CanConvert))]
        private static class StringEnumConverter_CanConvert_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(Type objectType, ref bool __result)
            {
                if (!IsNumericProtocolEnumType(objectType))
                {
                    return true;
                }

                __result = false;
                return false;
            }
        }
    }
}
