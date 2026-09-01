using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using GlobalEnums;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeathLinkData =
    Archipelago.MultiClient.Net.BounceFeatures.DeathLink.DeathLink;

namespace SilksongRandomizer
{
    /// <summary>
    /// Owns DeathLink's network subscription and translates it to and from the
    /// game's native death flow. Archipelago callbacks only enqueue work.
    /// Every Unity/game object access happens from Update on the main thread.
    /// </summary>
    internal static class DeathLinkManager
    {
        private const float RemoteDeathStartTimeoutSeconds = 5f;
        private const float RemoteDeathSafeDelaySeconds = 0.5f;
        private const string KarmelitaMemorySceneName =
            "Memory_Ant_Queen";
        private const string NylethMemorySceneName =
            "Shellwood_11b_Memory";
        private const string CrustKingKhannMemorySceneName =
            "Memory_Coral_Tower";
        private const string PermadeathSceneName = "PermaDeath";
        private const string PermadeathBodySheetName = "Credits List";
        private const string PermadeathBodyKey = "PERMA_GAME_OVER_BODY";

        private sealed class RemoteDeathCocoonSnapshot
        {
            internal int HeldRosaries;
            internal string CorpseScene;
            internal Vector2 DeathScenePosition;
            internal Vector2 DeathSceneSize;
            internal byte[] CorpseMarkerGuid;
            internal HeroDeathCocoonTypes CorpseType;
            internal int CorpseRosaries;
            internal bool SilkSpoolBroken;

            internal static RemoteDeathCocoonSnapshot Capture(
                PlayerData playerData)
            {
                return new RemoteDeathCocoonSnapshot
                {
                    HeldRosaries = playerData.geo,
                    CorpseScene = playerData.HeroCorpseScene,
                    DeathScenePosition = playerData.HeroDeathScenePos,
                    DeathSceneSize = playerData.HeroDeathSceneSize,
                    CorpseMarkerGuid = playerData.HeroCorpseMarkerGuid == null
                        ? null
                        : (byte[])playerData.HeroCorpseMarkerGuid.Clone(),
                    CorpseType = playerData.HeroCorpseType,
                    CorpseRosaries = playerData.HeroCorpseMoneyPool,
                    SilkSpoolBroken = playerData.IsSilkSpoolBroken,
                };
            }

            internal void Restore(PlayerData playerData)
            {
                if (playerData == null)
                {
                    return;
                }

                playerData.geo = HeldRosaries;
                playerData.HeroCorpseScene = CorpseScene;
                playerData.HeroDeathScenePos = DeathScenePosition;
                playerData.HeroDeathSceneSize = DeathSceneSize;
                playerData.HeroCorpseMarkerGuid = CorpseMarkerGuid == null
                    ? null
                    : (byte[])CorpseMarkerGuid.Clone();
                playerData.HeroCorpseType = CorpseType;
                playerData.HeroCorpseMoneyPool = CorpseRosaries;
                playerData.IsSilkSpoolBroken = SilkSpoolBroken;

                try
                {
                    CurrencyCounter.ToValue(
                        playerData.geo,
                        CurrencyType.Money
                    );
                    GameMap gameMap = GameManager.SilentInstance?.gameMap;
                    gameMap?.PositionCompassAndCorpse();
                }
                catch (Exception exception)
                {
                    QueueStatus(
                        "DeathLink restored Rosaries but could not refresh " +
                        "their display: " + exception.Message
                    );
                }
            }
        }

        private sealed class NativeDeathCapture
        {
            internal bool IsNonLethal;
        }

        private static readonly object StateLock = new object();
        private static readonly Queue<string> QueuedStatusMessages =
            new Queue<string>();

        private static Action<string> statusReporter;
        private static DeathLinkService service;
        private static HeroController subscribedHero;
        private static DeathLinkData pendingRemoteDeath;
        private static DeathLinkData remoteDeathInFlight;
        private static string sourcePlayer = string.Empty;
        private static string deathLinkCocoonMode =
            Archipelago.DeathLinkCocoonProtected;
        private static bool enabled;
        private static bool localDeathReported;
        private static NativeDeathCapture currentDeathCapture;
        private static bool suppressNextLocalDeath;
        private static bool protectRemoteCocoonInFlight;
        private static float remoteDeathStartedAt;
        private static float remoteDeathSafeSince;
        private static string lastReceivedSource = string.Empty;
        private static string lastReceivedCause = string.Empty;
        private static string appliedRemoteDeathSource = string.Empty;

        internal static bool IsEnabled
        {
            get
            {
                lock (StateLock)
                {
                    return enabled;
                }
            }
        }

        internal static bool HasPendingRemoteDeath
        {
            get
            {
                lock (StateLock)
                {
                    return pendingRemoteDeath != null ||
                           remoteDeathInFlight != null;
                }
            }
        }

        /// <summary>
        /// True only while the native Die iterator is being entered for a
        /// received DeathLink. Currency Link uses this narrow window to make
        /// the received death currency-neutral: the originating local death
        /// already owns the single debit from a team-shared Rosary pool.
        /// </summary>
        internal static bool IsRemoteDeathApplicationInFlight
        {
            get
            {
                lock (StateLock)
                {
                    return suppressNextLocalDeath &&
                           remoteDeathInFlight != null;
                }
            }
        }

        internal static bool IsRemoteCocoonProtectionInFlight
        {
            get
            {
                lock (StateLock)
                {
                    return protectRemoteCocoonInFlight;
                }
            }
        }

        internal static string LastReceivedSource
        {
            get
            {
                lock (StateLock)
                {
                    return lastReceivedSource;
                }
            }
        }

        internal static string LastReceivedCause
        {
            get
            {
                lock (StateLock)
                {
                    return lastReceivedCause;
                }
            }
        }

        internal static void Initialize(Action<string> reporter = null)
        {
            Reset();
            statusReporter = reporter;
        }

        /// <summary>
        /// Enables or disables DeathLink for the successfully authenticated
        /// session. This should only be called after slot/save compatibility
        /// checks have succeeded.
        /// </summary>
        internal static bool Configure(
            ArchipelagoSession session,
            string source,
            bool shouldEnable,
            string cocoonMode)
        {
            Reset();

            if (!shouldEnable)
            {
                return true;
            }

            if (session == null || string.IsNullOrWhiteSpace(source))
            {
                QueueStatus(
                    "DeathLink could not start because its session or slot " +
                    "name was missing."
                );
                return false;
            }

            if (!IsSupportedCocoonMode(cocoonMode))
            {
                QueueStatus(
                    "DeathLink could not start because its cocoon mode was " +
                    "invalid."
                );
                return false;
            }

            DeathLinkService createdService = null;
            try
            {
                createdService = session.CreateDeathLinkService();
                createdService.OnDeathLinkReceived += OnDeathLinkReceived;
                createdService.EnableDeathLink();

                lock (StateLock)
                {
                    service = createdService;
                    sourcePlayer = source.Trim();
                    deathLinkCocoonMode = cocoonMode;
                    enabled = true;
                }

                QueueStatus("DeathLink enabled for " + source.Trim() + ".");
                return true;
            }
            catch (Exception exception)
            {
                if (createdService != null)
                {
                    createdService.OnDeathLinkReceived -= OnDeathLinkReceived;
                    try
                    {
                        createdService.DisableDeathLink();
                    }
                    catch
                    {
                        // The original setup exception is the useful failure.
                    }
                }

                lock (StateLock)
                {
                    service = null;
                    sourcePlayer = string.Empty;
                    enabled = false;
                }

                QueueStatus(
                    "DeathLink could not be enabled: " + exception.Message
                );
                return false;
            }
        }

        internal static void Update()
        {
            FlushQueuedStatus();

            bool isEnabled;
            lock (StateLock)
            {
                isEnabled = enabled;
            }

            if (!isEnabled)
            {
                DetachHero();
                return;
            }

            SynchronizeHeroSubscription();
            RecoverTimedOutRemoteDeath();
            ResetLocalDeathLatchAfterRespawn();

            if (!TryGetSafeRemoteDeathTarget(
                    out HeroController hero,
                    out PlayerData playerData))
            {
                remoteDeathSafeSince = 0f;
                return;
            }

            if (remoteDeathSafeSince <= 0f)
            {
                remoteDeathSafeSince = Time.realtimeSinceStartup;
                return;
            }

            if (Time.realtimeSinceStartup - remoteDeathSafeSince <
                RemoteDeathSafeDelaySeconds)
            {
                return;
            }

            remoteDeathSafeSince = 0f;

            DeathLinkData remoteDeath;
            lock (StateLock)
            {
                remoteDeath = pendingRemoteDeath;
                if (remoteDeath == null)
                {
                    return;
                }

                pendingRemoteDeath = null;
                remoteDeathInFlight = remoteDeath;
            }

            suppressNextLocalDeath = true;
            remoteDeathStartedAt = Time.realtimeSinceStartup;
            RemoteDeathCocoonSnapshot cocoonSnapshot =
                ShouldProtectRemoteDeathCocoon(playerData)
                    ? RemoteDeathCocoonSnapshot.Capture(playerData)
                    : null;
            protectRemoteCocoonInFlight = cocoonSnapshot != null;

            try
            {
                // TakeDamage and TakeHealth are not used here:
                // tools and temporary invulnerability can prevent them. A
                // DeathLink must be deterministic once the hero is in a safe
                // gameplay state, while CheckDeathCatch retains the game's
                // real corpse, rosary, respawn and permadeath behavior.
                playerData.health = 0;
                try
                {
                    EventRegister.SendEvent(EventRegisterEvents.HealthUpdate);
                }
                catch (Exception exception)
                {
                    QueueStatus(
                        "DeathLink health UI refresh failed: " +
                        exception.Message
                    );
                }

                hero.CheckDeathCatch();
            }
            catch (Exception exception)
            {
                // An application failure does not suppress the player's next
                // real death. This death is restored unless a newer received
                // death is already waiting. Received events are coalesced while
                // unsafe.
                suppressNextLocalDeath = false;
                remoteDeathStartedAt = 0f;

                lock (StateLock)
                {
                    if (pendingRemoteDeath == null)
                    {
                        pendingRemoteDeath = remoteDeathInFlight;
                    }

                    remoteDeathInFlight = null;
                }

                QueueStatus(
                    "DeathLink could not be applied yet: " +
                    exception.Message
                );
            }
            finally
            {
                cocoonSnapshot?.Restore(playerData);
                protectRemoteCocoonInFlight = false;
            }
        }

        internal static void Reset()
        {
            DeathLinkService oldService;
            lock (StateLock)
            {
                enabled = false;
                oldService = service;
                service = null;
                sourcePlayer = string.Empty;
                deathLinkCocoonMode =
                    Archipelago.DeathLinkCocoonProtected;
                pendingRemoteDeath = null;
                remoteDeathInFlight = null;
                lastReceivedSource = string.Empty;
                lastReceivedCause = string.Empty;
                appliedRemoteDeathSource = string.Empty;
                QueuedStatusMessages.Clear();
            }

            if (oldService != null)
            {
                oldService.OnDeathLinkReceived -= OnDeathLinkReceived;
                try
                {
                    oldService.DisableDeathLink();
                }
                catch (Exception exception)
                {
                    LogDirect(
                        "DeathLink cleanup warning: " + exception.Message,
                        warning: true
                    );
                }
            }

            DetachHero();
            localDeathReported = false;
            currentDeathCapture = null;
            suppressNextLocalDeath = false;
            protectRemoteCocoonInFlight = false;
            remoteDeathStartedAt = 0f;
            remoteDeathSafeSince = 0f;
        }

        private static void OnDeathLinkReceived(DeathLinkData deathLink)
        {
            if (deathLink == null)
            {
                return;
            }

            string remoteSource = string.IsNullOrWhiteSpace(deathLink.Source)
                ? "Another player"
                : deathLink.Source;
            string remoteCause = string.IsNullOrWhiteSpace(deathLink.Cause)
                ? remoteSource + " died."
                : deathLink.Cause;

            lock (StateLock)
            {
                if (!enabled)
                {
                    return;
                }

                // Only the newest pending event remains while the game is unsafe.
                // This avoids a stack of unavoidable deaths after one long
                // cutscene while retaining the exact source/cause being acted
                // upon and logging every received event.
                pendingRemoteDeath = deathLink;
                lastReceivedSource = remoteSource;
                lastReceivedCause = remoteCause;
                QueuedStatusMessages.Enqueue(
                    "DeathLink received from " + remoteSource + ": " +
                    remoteCause
                );
            }
        }

        private static void SynchronizeHeroSubscription()
        {
            HeroController currentHero = HeroController.SilentInstance;
            if (ReferenceEquals(currentHero, subscribedHero))
            {
                return;
            }

            DetachHero();
            subscribedHero = currentHero;
            if (subscribedHero != null)
            {
                subscribedHero.OnDeath += OnHeroDeath;
            }

            localDeathReported = false;
            currentDeathCapture = null;
            suppressNextLocalDeath = false;
            protectRemoteCocoonInFlight = false;
            remoteDeathStartedAt = 0f;
            remoteDeathSafeSince = 0f;
            lock (StateLock)
            {
                remoteDeathInFlight = null;
            }
        }

        private static void DetachHero()
        {
            if (subscribedHero != null)
            {
                subscribedHero.OnDeath -= OnHeroDeath;
            }

            subscribedHero = null;
        }

        private static void OnHeroDeath()
        {
            NativeDeathCapture capture = currentDeathCapture;
            currentDeathCapture = null;
            if (capture != null && capture.IsNonLethal)
            {
                return;
            }

            if (localDeathReported)
            {
                return;
            }

            localDeathReported = true;

            if (suppressNextLocalDeath)
            {
                suppressNextLocalDeath = false;
                remoteDeathStartedAt = 0f;

                DeathLinkData appliedDeath;
                lock (StateLock)
                {
                    appliedDeath = remoteDeathInFlight;
                    remoteDeathInFlight = null;
                    appliedRemoteDeathSource = appliedDeath == null
                        ? string.Empty
                        : string.IsNullOrWhiteSpace(appliedDeath.Source)
                            ? "another player"
                            : appliedDeath.Source.Trim();
                }

                if (appliedDeath != null)
                {
                    string remoteSource =
                        string.IsNullOrWhiteSpace(appliedDeath.Source)
                            ? "another player"
                            : appliedDeath.Source;
                    string remoteCause =
                        string.IsNullOrWhiteSpace(appliedDeath.Cause)
                            ? remoteSource + " died."
                            : appliedDeath.Cause;
                    QueueStatus(
                        "DeathLink applied from " + remoteSource + ": " +
                        remoteCause
                    );
                }

                return;
            }

            string sceneName = GetCurrentSceneName();

            DeathLinkService currentService;
            string currentSource;
            lock (StateLock)
            {
                appliedRemoteDeathSource = string.Empty;
                if (IsDeathLinkMemoryFightScene(sceneName))
                {
                    return;
                }

                if (!enabled || service == null)
                {
                    return;
                }

                currentService = service;
                currentSource = sourcePlayer;
            }

            string cause = currentSource + " died";
            if (!string.IsNullOrWhiteSpace(sceneName))
            {
                cause += " in " + sceneName;
            }

            cause += ".";

            try
            {
                currentService.SendDeathLink(
                    new DeathLinkData(currentSource, cause)
                );
                QueueStatus("DeathLink sent: " + cause);
            }
            catch (Exception exception)
            {
                QueueStatus(
                    "DeathLink could not be sent: " + exception.Message
                );
            }
        }

        [HarmonyPatch(
            typeof(HeroController),
            "Die",
            new Type[] { typeof(bool), typeof(bool) })]
        private static class HeroControllerDiePatch
        {
            [HarmonyPostfix]
            private static void Postfix(
                bool nonLethal,
                ref IEnumerator __result)
            {
                if (__result != null)
                {
                    __result = CaptureNativeDeath(__result, nonLethal);
                }
            }

            private static IEnumerator CaptureNativeDeath(
                IEnumerator nativeRoutine,
                bool nonLethal)
            {
                NativeDeathCapture capture = new NativeDeathCapture
                {
                    IsNonLethal = nonLethal,
                };
                currentDeathCapture = capture;
                try
                {
                    while (nativeRoutine.MoveNext())
                    {
                        yield return nativeRoutine.Current;
                    }
                }
                finally
                {
                    // OnDeath normally consumes this before the first yield.
                    // Clear it here only if that event never reached us.
                    if (ReferenceEquals(currentDeathCapture, capture))
                    {
                        currentDeathCapture = null;
                    }

                    (nativeRoutine as IDisposable)?.Dispose();
                }
            }
        }

        private static bool TryGetSafeRemoteDeathTarget(
            out HeroController hero,
            out PlayerData playerData)
        {
            hero = subscribedHero;
            playerData = null;

            lock (StateLock)
            {
                if (!enabled ||
                    pendingRemoteDeath == null ||
                    remoteDeathInFlight != null)
                {
                    return false;
                }
            }

            GameManager gameManager = GameManager.SilentInstance;
            if (gameManager == null ||
                hero == null ||
                hero.cState == null ||
                !PlayerData.HasInstance)
            {
                return false;
            }

            playerData = PlayerData.instance;
            if (WidowSequenceSafety.ShouldDeferDeathLink())
            {
                return false;
            }

            if (gameManager.GameState != GameState.PLAYING ||
                gameManager.isPaused ||
                gameManager.IsLoadingSceneTransition ||
                gameManager.IsInSceneTransition ||
                gameManager.RespawningHero ||
                playerData.disablePause ||
                playerData.isInventoryOpen ||
                playerData.isInvincible ||
                hero.transitionState !=
                    HeroTransitionState.WAITING_TO_TRANSITION ||
                hero.cState.transitioning ||
                hero.cState.dead ||
                hero.cState.hazardDeath ||
                hero.cState.hazardRespawning ||
                BossSceneController.IsTransitioning)
            {
                return false;
            }

            if (hero.controlReqlinquished ||
                hero.IsInputBlocked() ||
                !hero.CanInput() ||
                ToolItemManager.ActiveState != ToolsActiveStates.Active)
            {
                return false;
            }

            if (!CurrencyLinkManager.CanApplyRemoteDeath(playerData))
            {
                return false;
            }

            return hero.CanTakeDamage();
        }

        private static bool IsSupportedCocoonMode(string mode)
        {
            return string.Equals(
                       mode,
                       Archipelago.DeathLinkCocoonVanilla,
                       StringComparison.Ordinal
                   ) ||
                   string.Equals(
                       mode,
                       Archipelago.DeathLinkCocoonless,
                       StringComparison.Ordinal
                   ) ||
                   string.Equals(
                       mode,
                       Archipelago.DeathLinkCocoonProtected,
                       StringComparison.Ordinal
                   );
        }

        private static bool ShouldProtectRemoteDeathCocoon(
            PlayerData playerData)
        {
            if (playerData == null ||
                string.Equals(
                    deathLinkCocoonMode,
                    Archipelago.DeathLinkCocoonVanilla,
                    StringComparison.Ordinal
                ))
            {
                return false;
            }

            if (string.Equals(
                    deathLinkCocoonMode,
                    Archipelago.DeathLinkCocoonless,
                    StringComparison.Ordinal
                ))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(
                       playerData.HeroCorpseScene
                   ) ||
                   playerData.HeroCorpseMarkerGuid != null ||
                   playerData.HeroCorpseMoneyPool > 0;
        }

        private static void ResetLocalDeathLatchAfterRespawn()
        {
            if (!localDeathReported ||
                suppressNextLocalDeath ||
                subscribedHero == null ||
                subscribedHero.cState == null ||
                !PlayerData.HasInstance)
            {
                return;
            }

            PlayerData playerData = PlayerData.instance;
            if (!subscribedHero.cState.dead &&
                !subscribedHero.cState.hazardDeath &&
                !subscribedHero.cState.hazardRespawning &&
                playerData.health > 0)
            {
                localDeathReported = false;
            }
        }

        private static void RecoverTimedOutRemoteDeath()
        {
            if (!suppressNextLocalDeath ||
                Time.realtimeSinceStartup - remoteDeathStartedAt <
                    RemoteDeathStartTimeoutSeconds)
            {
                return;
            }

            DeathLinkData timedOutDeath;
            lock (StateLock)
            {
                timedOutDeath = remoteDeathInFlight;
                remoteDeathInFlight = null;
            }

            bool deathStarted =
                subscribedHero != null &&
                subscribedHero.cState != null &&
                subscribedHero.cState.dead;

            suppressNextLocalDeath = false;
            remoteDeathStartedAt = 0f;

            if (!deathStarted && timedOutDeath != null)
            {
                lock (StateLock)
                {
                    if (pendingRemoteDeath == null)
                    {
                        pendingRemoteDeath = timedOutDeath;
                    }
                }

                QueueStatus(
                    "DeathLink death did not start; it will retry when safe."
                );
            }
        }

        private static string GetCurrentSceneName()
        {
            GameManager gameManager = GameManager.SilentInstance;
            if (gameManager != null &&
                !string.IsNullOrWhiteSpace(gameManager.sceneName))
            {
                return gameManager.sceneName;
            }

            return SceneManager.GetActiveScene().name;
        }

        private static bool IsDeathLinkMemoryFightScene(string sceneName)
        {
            return string.Equals(
                       sceneName,
                       KarmelitaMemorySceneName,
                       StringComparison.Ordinal
                   ) ||
                   string.Equals(
                       sceneName,
                       NylethMemorySceneName,
                       StringComparison.Ordinal
                   ) ||
                   string.Equals(
                       sceneName,
                       CrustKingKhannMemorySceneName,
                       StringComparison.Ordinal
                   );
        }

        internal static string ResolvePermadeathBodyText(
            string key,
            string sheetTitle,
            string original)
        {
            if (!string.Equals(
                    SceneManager.GetActiveScene().name,
                    PermadeathSceneName,
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    sheetTitle,
                    PermadeathBodySheetName,
                    StringComparison.Ordinal
                ) ||
                !string.Equals(
                    key,
                    PermadeathBodyKey,
                    StringComparison.Ordinal
                ))
            {
                return original;
            }

            string appliedSource;
            lock (StateLock)
            {
                appliedSource = appliedRemoteDeathSource;
            }

            return string.IsNullOrWhiteSpace(appliedSource)
                ? original
                : "Got death link'd by " + appliedSource;
        }

        private static void QueueStatus(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            lock (StateLock)
            {
                QueuedStatusMessages.Enqueue(message);
            }
        }

        private static void FlushQueuedStatus()
        {
            while (true)
            {
                string message;
                lock (StateLock)
                {
                    if (QueuedStatusMessages.Count == 0)
                    {
                        return;
                    }

                    message = QueuedStatusMessages.Dequeue();
                }

                LogDirect(message, warning: false);
            }
        }

        private static void LogDirect(string message, bool warning)
        {
            try
            {
                statusReporter?.Invoke(message);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[RANDOMIZER] DeathLink status reporter failed: " +
                    exception.Message
                );
            }

            if (RandomizerPlugin.Log != null)
            {
                if (warning)
                {
                    RandomizerPlugin.Log.LogWarning(
                        "[RANDOMIZER] " + message
                    );
                }
                else
                {
                    RandomizerPlugin.Log.LogInfo(
                        "[RANDOMIZER] " + message
                    );
                }
            }
            else if (warning)
            {
                Debug.LogWarning("[RANDOMIZER] " + message);
            }
            else
            {
                Debug.Log("[RANDOMIZER] " + message);
            }
        }
    }
}
