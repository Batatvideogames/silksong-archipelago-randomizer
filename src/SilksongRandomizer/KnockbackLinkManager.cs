using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Packets;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SilksongRandomizer
{
    internal static class KnockbackLinkManager
    {
        private const string Tag = "KnockbackLink";
        private const double Lifetime = 3;
        private const double Cooldown = 1;
        private const float OutgoingStrength = 40f;
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, double> Seen = new Dictionary<string, double>();
        internal static readonly FieldInfo RecoilVectorField = typeof(HeroController).GetField("recoilVector", BindingFlags.Instance | BindingFlags.NonPublic);
        private static ArchipelagoSession session;
        private static ArchipelagoSocketHelperDelagates.PacketReceivedHandler handler;
        private static string source;
        private static string uuid;
        private static int generation;
        private static Vector2? pending;
        private static double expires;
        private static double lastSent = double.NegativeInfinity;
        private static double lastApplied = double.NegativeInfinity;
        private static bool applyingRemote;

        private static double Now => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
        private static double UnixNow => (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

        internal static void Configure(ArchipelagoSession connectedSession, string player, bool enabled)
        {
            Reset();
            if (!enabled || connectedSession == null)
                return;

            lock (Sync)
            {
                session = connectedSession;
                source = player;
                uuid = Guid.NewGuid().ToString();
                int current = generation;
                handler = packet => Receive(packet, current);
                session.Socket.PacketReceived += handler;
                session.ConnectionInfo.UpdateConnectionOptions(
                    session.ConnectionInfo.Tags.Concat(new[] { Tag }).Distinct().ToArray());
            }
        }

        internal static void Reset()
        {
            lock (Sync)
            {
                generation++;
                if (session != null && handler != null)
                    session.Socket.PacketReceived -= handler;
                if (session != null && session.Socket.Connected)
                {
                    try
                    {
                        session.ConnectionInfo.UpdateConnectionOptions(
                            session.ConnectionInfo.Tags.Where(tag => tag != Tag).ToArray());
                    }
                    catch (Exception ex)
                    {
                        RandomizerPlugin.Log?.LogWarning("[RANDOMIZER] Knockback Link disconnect: " + ex.Message);
                    }
                }
                session = null;
                handler = null;
                pending = null;
                Seen.Clear();
                lastSent = lastApplied = double.NegativeInfinity;
            }
        }

        internal static bool TryNormalize(double x, double y, out Vector2 direction)
        {
            direction = default;
            if (double.IsNaN(x) || double.IsInfinity(x) ||
                double.IsNaN(y) || double.IsInfinity(y))
                return false;
            double scale = Math.Max(Math.Abs(x), Math.Abs(y));
            if (scale == 0)
                return false;
            x /= scale;
            y /= scale;
            double length = Math.Sqrt(x * x + y * y);
            direction = new Vector2((float)(x / length), (float)(y / length));
            return true;
        }

        private static bool TryNumber(JToken token, out double value)
        {
            value = 0;
            if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer))
                return false;
            value = token.Value<double>();
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        internal static bool TryReadPacket(BouncedPacket packet, double utc,
            out Vector2 direction, out string sender, out string senderUuid, out string key)
        {
            direction = default;
            sender = senderUuid = key = null;
            if (packet?.Tags == null || !packet.Tags.Contains(Tag) || packet.Data == null ||
                !packet.Data.TryGetValue("source", out JToken sourceToken) || sourceToken.Type != JTokenType.String ||
                !packet.Data.TryGetValue("time", out JToken timeToken) || !TryNumber(timeToken, out double time) ||
                utc - time > Lifetime || time - utc > 30 ||
                !packet.Data.TryGetValue("value", out JToken value) || !(value is JObject vector) ||
                !TryNumber(vector["x"], out double x) || !TryNumber(vector["y"], out double y) ||
                !TryNormalize(x, y, out direction))
                return false;
            sender = sourceToken.Value<string>();
            if (string.IsNullOrWhiteSpace(sender) || sender.Length > 256)
                return false;
            if (packet.Data.TryGetValue("uuid", out JToken uuidToken))
            {
                if (uuidToken.Type != JTokenType.String)
                    return false;
                senderUuid = uuidToken.Value<string>();
                if (senderUuid.Length > 256)
                    return false;
            }
            key = sender + "\n" + senderUuid + "\n" + time.ToString("R", CultureInfo.InvariantCulture);
            return true;
        }

        private static void Receive(ArchipelagoPacketBase packet, int current)
        {
            try
            {
                if (!(packet is BouncedPacket bounced) ||
                    !TryReadPacket(bounced, UnixNow, out Vector2 direction,
                        out string sender, out string senderUuid, out string key))
                    return;
                lock (Sync)
                {
                    if (session == null || current != generation || senderUuid == uuid ||
                        (string.IsNullOrEmpty(senderUuid) && sender == source))
                        return;
                    double now = Now;
                    foreach (string old in Seen.Where(entry => now - entry.Value > 30).Select(entry => entry.Key).ToArray())
                        Seen.Remove(old);
                    if (Seen.ContainsKey(key) || Seen.Count >= 256)
                        return;
                    Seen[key] = now;
                    pending = direction;
                    expires = now + Lifetime;
                }
            }
            catch (Exception)
            {
            }
        }

        internal static void Update()
        {
            lock (Sync)
            {
                if (session == null || !session.Socket.Connected || !pending.HasValue)
                    return;
                double now = Now;
                if (now > expires)
                {
                    pending = null;
                    return;
                }
                if (now - lastApplied < Cooldown || !CanApply())
                    return;
                applyingRemote = true;
                try
                {
                    if (TrapManager.TryApplyLinkedStagger(pending.Value))
                    {
                        pending = null;
                        lastApplied = now;
                    }
                }
                catch (Exception ex)
                {
                    pending = null;
                    RandomizerPlugin.Log?.LogWarning("[RANDOMIZER] Knockback Link could not apply: " + ex.Message);
                }
                finally
                {
                    applyingRemote = false;
                }
            }
        }

        private static bool CanApply()
        {
            GameManager gm = GameManager.SilentInstance;
            HeroController hero = HeroController.instance;
            PlayerData pd = PlayerData.instance;
            return SaveState.Instance?.knockbackLink == true && gm != null && hero != null && pd != null &&
                hero.cState != null && gm.GameState == GlobalEnums.GameState.PLAYING && gm.IsGameplayScene() &&
                !gm.isPaused && !gm.IsLoadingSceneTransition && !gm.IsInSceneTransition && !gm.RespawningHero &&
                !TransitionPoint.IsTransitionBlocked && !BossSceneController.IsTransitioning &&
                !pd.HasStoredMemoryState && !pd.atBench && !pd.isInventoryOpen && !pd.disablePause &&
                !hero.IsInputBlocked() && !hero.cState.isInCutsceneMovement &&
                !GenericMessageCanvas.IsActive && InteractManager.BlockingInteractable == null &&
                !hero.cState.transitioning && !hero.cState.dead && !hero.cState.hazardDeath &&
                !hero.cState.hazardRespawning && !DeathLinkManager.HasPendingRemoteDeath &&
                hero.CanCustomRecoil();
        }

        private static void SendNativeRecoil(HeroController hero, int current)
        {
            lock (Sync)
            {
                if (session == null || current != generation || !session.Socket.Connected ||
                    SaveState.Instance?.knockbackLink != true || applyingRemote ||
                    RecoilVectorField == null || hero?.cState == null || !hero.cState.recoilFrozen ||
                    Now - lastSent < Cooldown)
                    return;
                Vector2 native = (Vector2)RecoilVectorField.GetValue(hero);
                if (!TryNormalize(native.x, native.y, out Vector2 direction))
                    return;
                lastSent = Now;
                var packet = new BouncePacket
                {
                    Tags = new List<string> { Tag },
                    Data = new Dictionary<string, JToken>
                    {
                        ["source"] = source,
                        ["uuid"] = uuid,
                        ["cause"] = source + " was knocked back.",
                        ["time"] = UnixNow,
                        ["value"] = new JObject { ["x"] = direction.x * OutgoingStrength,
                            ["y"] = direction.y * OutgoingStrength, ["z"] = 0 }
                    }
                };
                SendPacket(session, packet);
            }
        }

        private static async void SendPacket(ArchipelagoSession target, BouncePacket packet)
        {
            try { await target.Socket.SendPacketAsync(packet); }
            catch (Exception ex)
            {
                RandomizerPlugin.Log?.LogWarning("[RANDOMIZER] Knockback Link send failed: " + ex.Message);
            }
        }

        private static IEnumerator ObserveRecoil(HeroController hero, IEnumerator native, int current)
        {
            try
            {
                bool first = true;
                while (native.MoveNext())
                {
                    if (first)
                    {
                        first = false;
                        try { SendNativeRecoil(hero, current); }
                        catch (Exception ex)
                        {
                            RandomizerPlugin.Log?.LogWarning("[RANDOMIZER] Knockback Link capture failed: " + ex.Message);
                        }
                    }
                    yield return native.Current;
                }
            }
            finally { (native as IDisposable)?.Dispose(); }
        }

        [HarmonyPatch(typeof(HeroController), "StartRecoil")]
        private static class NativeRecoilPatch
        {
            [HarmonyPostfix]
            private static void Postfix(HeroController __instance, ref IEnumerator __result)
            {
                lock (Sync)
                {
                    if (session != null && !applyingRemote && __result != null)
                        __result = ObserveRecoil(__instance, __result, generation);
                }
            }
        }
    }
}
