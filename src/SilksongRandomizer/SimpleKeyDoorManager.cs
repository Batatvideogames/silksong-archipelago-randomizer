using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SilksongRandomizer
{
    internal static class SimpleKeyDoorManager
    {
        internal const string WormwaysKey = "Simple Key (Wormways)";
        internal const string DeepDocksKey = "Simple Key (Deep Docks)";
        internal const string GreenPrinceKey = "Simple Key (Green Prince)";
        internal const string RosaryBankKey = "Simple Key (Rosary Bank)";

        private sealed class DoorDefinition
        {
            internal readonly string ItemName;
            internal readonly string SceneName;
            internal readonly string ObjectName;
            internal readonly string PersistentId;
            internal readonly string PlayerDataBool;

            internal DoorDefinition(
                string itemName,
                string sceneName,
                string objectName,
                string persistentId = null,
                string playerDataBool = null)
            {
                ItemName = itemName;
                SceneName = sceneName;
                ObjectName = objectName;
                PersistentId = persistentId;
                PlayerDataBool = playerDataBool;
            }

            internal bool UsesPersistentBool =>
                !string.IsNullOrEmpty(PersistentId);
        }

        private static readonly DoorDefinition[] Doors =
        {
            new DoorDefinition(
                WormwaysKey,
                "Crawl_02",
                "aspid_sealed_gate_stone",
                persistentId: "aspid_sealed_gate_stone"
            ),
            new DoorDefinition(
                DeepDocksKey,
                "Room_Forge",
                "song_knight_lock",
                playerDataBool: "openedSongGateDocks"
            ),
            new DoorDefinition(
                GreenPrinceKey,
                "Dust_02",
                "Key Receptactle",
                playerDataBool: "UnlockedDustCage"
            ),
            new DoorDefinition(
                RosaryBankKey,
                "Hang_06",
                "bank_door",
                persistentId: "bank_door"
            ),
        };

        private static readonly Dictionary<string, DoorDefinition> DoorsByItem =
            BuildDoorLookup();
        private static readonly FieldInfo IsActivatedField =
            AccessTools.Field(typeof(ItemReceptacle), "isActivated");
        private static readonly MethodInfo StartedActivatedMethod =
            AccessTools.Method(typeof(ItemReceptacle), "StartedActivated");
        private static readonly FieldInfo InspectEventTargetField =
            AccessTools.Field(typeof(ItemReceptacle), "inspectEventTarget");

        internal static void GrantWormwaysKey()
        {
            Grant(WormwaysKey);
        }

        internal static void GrantDeepDocksKey()
        {
            Grant(DeepDocksKey);
        }

        internal static void GrantGreenPrinceKey()
        {
            Grant(GreenPrinceKey);
        }

        internal static void GrantRosaryBankKey()
        {
            Grant(RosaryBankKey);
        }

        internal static bool TrySynchronizeReceivedKeys()
        {
            SaveState state = SaveState.Instance;
            if (state == null || state.receivedItems == null)
            {
                return false;
            }

            foreach (DoorDefinition door in Doors)
            {
                if (state.receivedItems.Contains(door.ItemName) &&
                    !TryApplyDoor(door, out string error))
                {
                    Debug.LogWarning(
                        "[RANDOMIZER] Could not restore " + door.ItemName +
                        " yet: " + error
                    );
                    return false;
                }
            }

            return true;
        }

        private static Dictionary<string, DoorDefinition> BuildDoorLookup()
        {
            Dictionary<string, DoorDefinition> result =
                new Dictionary<string, DoorDefinition>(
                    StringComparer.OrdinalIgnoreCase
                );
            foreach (DoorDefinition door in Doors)
            {
                result.Add(door.ItemName, door);
            }
            return result;
        }

        private static void Grant(string itemName)
        {
            if (!DoorsByItem.TryGetValue(itemName, out DoorDefinition door))
            {
                throw new ArgumentException(
                    "Unknown destination-specific Simple Key: " + itemName,
                    nameof(itemName)
                );
            }

            if (!TryApplyDoor(door, out string error))
            {
                throw new InvalidOperationException(
                    "Could not apply " + itemName + ": " + error
                );
            }
        }

        private static bool TryApplyDoor(
            DoorDefinition door,
            out string error)
        {
            error = null;

            if (door.UsesPersistentBool)
            {
                if (SceneData.instance == null)
                {
                    error = "SceneData is not ready.";
                    return false;
                }

                SceneData.instance.PersistentBools.SetValue(
                    new PersistentItemData<bool>
                    {
                        SceneName = door.SceneName,
                        ID = door.PersistentId,
                        Value = true,
                    }
                );
            }
            else
            {
                if (PlayerData.instance == null)
                {
                    error = "PlayerData is not ready.";
                    return false;
                }

                if (string.Equals(
                        door.PlayerDataBool,
                        "openedSongGateDocks",
                        StringComparison.Ordinal))
                {
                    PlayerData.instance.openedSongGateDocks = true;
                }
                else if (string.Equals(
                             door.PlayerDataBool,
                             "UnlockedDustCage",
                             StringComparison.Ordinal))
                {
                    PlayerData.instance.UnlockedDustCage = true;
                }
                else
                {
                    error = "Unsupported PlayerData door flag: " +
                            door.PlayerDataBool;
                    return false;
                }
            }

            return TryOpenLoadedDoor(door, out error);
        }

        private static bool TryOpenLoadedDoor(
            DoorDefinition door,
            out string error)
        {
            error = null;
            ItemReceptacle receptacle = FindLoadedReceptacle(door);
            if (receptacle == null)
            {
                return true;
            }

            if (IsActivatedField == null || StartedActivatedMethod == null)
            {
                error = "ItemReceptacle activation members were not found.";
                return false;
            }

            try
            {
                if (door.UsesPersistentBool)
                {
                    PersistentBoolItem persistent =
                        FindPersistentBool(receptacle, door.PersistentId);
                    if (persistent == null)
                    {
                        error = "The loaded door's PersistentBoolItem was not found.";
                        return false;
                    }

                    // The live component cannot write its former false
                    // value over the AP-owned state during the next game save.
                    persistent.SetValueOverride(true);
                    persistent.SaveStateNoCondition();
                    persistent.LoadIfNeverStarted();
                }

                bool alreadyActivated =
                    (bool)IsActivatedField.GetValue(receptacle);
                if (!alreadyActivated)
                {
                    PlayMakerFSM greenPrinceDoorFsm = null;
                    if (string.Equals(
                            door.ItemName,
                            GreenPrinceKey,
                            StringComparison.Ordinal))
                    {
                        if (InspectEventTargetField == null)
                        {
                            error = "ItemReceptacle.inspectEventTarget was not found.";
                            return false;
                        }

                        greenPrinceDoorFsm =
                            InspectEventTargetField.GetValue(receptacle) as
                            PlayMakerFSM;
                        if (greenPrinceDoorFsm == null)
                        {
                            error = "The loaded Green Prince Cell Door FSM was not found.";
                            return false;
                        }
                    }

                    // This is the game's own load-time final-state path. It
                    // calls UnlockablePropBase.Opened() without taking a native
                    // Simple Key or putting Hornet into the unlock cutscene.
                    StartedActivatedMethod.Invoke(receptacle, null);

                    // Green Prince's receptacle has no UnlockablePropBase and
                    // no onStartUnlocked callback. Its parent Cell Door FSM is
                    // the physical gate, so mirror the receptacle's serialized
                    // onUnlock event after the save flag has been set.
                    greenPrinceDoorFsm?.SendEvent("UNLOCK");
                    IsActivatedField.SetValue(receptacle, true);
                }

                return true;
            }
            catch (TargetInvocationException exception)
            {
                Exception cause = exception.InnerException ?? exception;
                error = cause.GetType().Name + ": " + cause.Message;
                return false;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        private static ItemReceptacle FindLoadedReceptacle(
            DoorDefinition door)
        {
            foreach (ItemReceptacle receptacle in
                     Resources.FindObjectsOfTypeAll<ItemReceptacle>())
            {
                if (receptacle == null || receptacle.gameObject == null)
                {
                    continue;
                }

                string sceneName = receptacle.gameObject.scene.name;
                if (string.IsNullOrEmpty(sceneName) ||
                    !string.Equals(
                        GameManager.GetBaseSceneName(sceneName),
                        door.SceneName,
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    !HasObjectInParentChain(
                        receptacle.transform,
                        door.ObjectName
                    ))
                {
                    continue;
                }

                return receptacle;
            }

            return null;
        }

        private static bool HasObjectInParentChain(
            Transform transform,
            string objectName)
        {
            for (Transform current = transform;
                 current != null;
                 current = current.parent)
            {
                if (string.Equals(
                        current.name,
                        objectName,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static PersistentBoolItem FindPersistentBool(
            ItemReceptacle receptacle,
            string persistentId)
        {
            for (Transform current = receptacle.transform;
                 current != null;
                 current = current.parent)
            {
                foreach (PersistentBoolItem persistent in
                         current.GetComponents<PersistentBoolItem>())
                {
                    if (persistent != null &&
                        string.Equals(
                            persistent.ItemData.ID,
                            persistentId,
                            StringComparison.Ordinal
                        ))
                    {
                        return persistent;
                    }
                }
            }

            return null;
        }
    }
}
