using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using GlobalEnums;
using System;
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

        private static readonly object StateLock = new object();
        private static readonly Queue<string> QueuedStatusMessages =
            new Queue<string>();

        private static Action<string> statusReporter;
        private static DeathLinkService service;
        private static HeroController subscribedHero;
        private static DeathLinkData pendingRemoteDeath;
        private static DeathLinkData remoteDeathInFlight;
        private static string sourcePlayer = string.Empty;
        private static bool enabled;
        private static bool localDeathReported;
        private static bool suppressNextLocalDeath;
        private static float remoteDeathStartedAt;
        private static string lastReceivedSource = string.Empty;
        private static string lastReceivedCause = string.Empty;

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
            bool shouldEnable)
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
                return;
            }

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
                pendingRemoteDeath = null;
                remoteDeathInFlight = null;
                lastReceivedSource = string.Empty;
                lastReceivedCause = string.Empty;
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
            suppressNextLocalDeath = false;
            remoteDeathStartedAt = 0f;
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
            suppressNextLocalDeath = false;
            remoteDeathStartedAt = 0f;
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

            DeathLinkService currentService;
            string currentSource;
            lock (StateLock)
            {
                if (!enabled || service == null)
                {
                    return;
                }

                currentService = service;
                currentSource = sourcePlayer;
            }

            string sceneName = GetCurrentSceneName();
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
            if (gameManager.GameState != GameState.PLAYING ||
                gameManager.isPaused ||
                gameManager.IsLoadingSceneTransition ||
                gameManager.IsInSceneTransition ||
                gameManager.RespawningHero ||
                playerData.disablePause ||
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

            if (!CurrencyLinkManager.CanApplyRemoteDeath(playerData))
            {
                return false;
            }

            return hero.CanTakeDamage();
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
