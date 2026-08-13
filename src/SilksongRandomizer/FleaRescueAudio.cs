using System;
using System.IO;
using UnityEngine;

namespace SilksongRandomizer
{
    internal static class FleaRescueAudio
    {
        // Bone_06/Flea Rescue Activation/Control/Rescue 2 uses this exact clip.
        internal const string VanillaFleaRescueClipAddress =
            "Assets/Audio/Voices/Fleas/Makoto/Flea_Howl_02.wav";
        private const string VanillaFleaRescueClipName = "Flea_Howl_02";
        private const string FleaSfxBundlePattern =
            "sfxstatic_assets_fleacaravan*.bundle";
        private const float ResolveRetryDelaySeconds = 0.5f;
        private const float FailedPlayRetryDelaySeconds = 0.25f;
        private const float NativeFleaRescueVolume = 1f;
        private const int MaxPlayAttempts = 3;

        private static AudioClip fleaHowlClip;
        private static bool clipResolutionFailed;
        private static int pendingPlays;
        private static int failedPlayAttempts;
        private static float nextPlayTime;
        private static float nextResolveAttemptTime;

        internal static void QueueForReceivedFlea()
        {
            if (clipResolutionFailed)
            {
                return;
            }

            if (pendingPlays < int.MaxValue)
            {
                pendingPlays++;
            }

            RandomizerPlugin.Log?.LogInfo(
                "[RANDOMIZER] Queued the Flea rescue sound for a received AP Flea."
            );
        }

        internal static void Update()
        {
            if (pendingPlays <= 0 || !HasStableGameplayContext() ||
                !TryResolveClip())
            {
                return;
            }

            HeroController hero = HeroController.UnsafeInstance;
            if (Time.unscaledTime < nextPlayTime || hero == null)
            {
                return;
            }

            try
            {
                // The cue matches the native Rescue 2 action. Its actor/SFX
                // source uses the game's mixer and its 39-unit near range.
                // PlayClipAtPoint creates a bare 3D source with a 1-unit near
                // range, so the camera's Z offset makes this howl very quiet.
                AudioEvent rescueSound = AudioEvent.Default;
                rescueSound.Clip = fleaHowlClip;
                rescueSound.Volume = NativeFleaRescueVolume;
                AudioSource playedSource = rescueSound.SpawnAndPlayOneShot(
                    GlobalSettings.Audio.DefaultAudioSourcePrefab,
                    hero.transform.position,
                    null
                );
                if (playedSource == null)
                {
                    failedPlayAttempts++;
                    if (failedPlayAttempts >= MaxPlayAttempts)
                    {
                        pendingPlays--;
                        failedPlayAttempts = 0;
                        RandomizerPlugin.Log?.LogWarning(
                            "[RANDOMIZER] Flea rescue sound could not create " +
                            "a native audio source."
                        );
                    }
                    nextPlayTime = Time.unscaledTime +
                        FailedPlayRetryDelaySeconds;
                    return;
                }

                pendingPlays--;
                failedPlayAttempts = 0;
                RandomizerPlugin.Log?.LogInfo(
                    "[RANDOMIZER] Played the Flea rescue sound."
                );
                nextPlayTime = Time.unscaledTime +
                    Mathf.Max(0.1f, fleaHowlClip.length);
            }
            catch (Exception ex)
            {
                pendingPlays--;
                failedPlayAttempts = 0;
                RandomizerPlugin.Log?.LogWarning(
                    "[RANDOMIZER] Failed to play the Flea rescue sound: " + ex.Message
                );
            }
        }

        internal static void ResetPending()
        {
            pendingPlays = 0;
            failedPlayAttempts = 0;
            nextPlayTime = 0f;
        }

        internal static bool CanResolveFleaAudio(
            bool hasGameManager,
            bool isGameplayScene,
            bool isLoadingSceneTransition,
            bool isInSceneTransition,
            bool hasHero)
        {
            return hasGameManager &&
                   isGameplayScene &&
                   !isLoadingSceneTransition &&
                   !isInSceneTransition &&
                   hasHero;
        }

        private static bool HasStableGameplayContext()
        {
            GameManager gameManager = GameManager.UnsafeInstance;
            return CanResolveFleaAudio(
                gameManager != null,
                gameManager != null && gameManager.IsGameplayScene(),
                gameManager != null &&
                    gameManager.IsLoadingSceneTransition,
                gameManager != null && gameManager.IsInSceneTransition,
                HeroController.UnsafeInstance != null
            );
        }

        private static bool TryResolveClip()
        {
            if (fleaHowlClip != null)
            {
                return true;
            }

            return TryResolveFromGameBundle();
        }

        private static bool TryResolveFromGameBundle()
        {
            try
            {
                return TryResolveFromGameBundleCore();
            }
            catch (Exception ex)
            {
                FailLoad(
                    "The native Flea rescue sound lookup failed: " + ex.Message
                );
                return false;
            }
        }

        private static bool TryResolveFromGameBundleCore()
        {
            if (fleaHowlClip != null)
            {
                return true;
            }

            if (clipResolutionFailed)
            {
                return false;
            }

            if (Time.unscaledTime < nextResolveAttemptTime)
            {
                return false;
            }

            AudioClip alreadyLoadedClip = FindAlreadyLoadedExpectedClip();
            if (alreadyLoadedClip != null)
            {
                fleaHowlClip = alreadyLoadedClip;
                RandomizerPlugin.Log?.LogInfo(
                    "[RANDOMIZER] Reused the already-loaded vanilla Flea " +
                    "rescue sound."
                );
                return true;
            }

            AudioClip borrowedClip = FindClipInAlreadyLoadedBundle();
            if (borrowedClip != null)
            {
                fleaHowlClip = borrowedClip;
                RandomizerPlugin.Log?.LogInfo(
                    "[RANDOMIZER] Loaded the Flea rescue sound from a " +
                    "game-owned SFX bundle."
                );
                return true;
            }

            string streamingAssets = Application.streamingAssetsPath;
            string aaRoot = Path.Combine(streamingAssets, "aa");
            string[] bundlePaths = Directory.Exists(aaRoot)
                ? Directory.GetFiles(
                    aaRoot,
                    FleaSfxBundlePattern,
                    SearchOption.AllDirectories
                )
                : Array.Empty<string>();

            if (bundlePaths.Length != 1)
            {
                FailLoad(
                    bundlePaths.Length == 0
                        ? "Could not find the Flea Caravan SFX bundle under " +
                          aaRoot + "."
                        : "Found multiple Flea Caravan SFX bundles under " +
                          aaRoot + "; refusing to choose one arbitrarily."
                );
                return false;
            }

            RandomizerPlugin.Log?.LogInfo(
                "[RANDOMIZER] Taking a temporary snapshot of the native " +
                "Flea Caravan SFX bundle."
            );

            AssetBundle ownedBundle = null;
            AudioClip snapshotClip = null;
            bool ownershipRace = false;
            try
            {
                // Bundle ownership remains inside this main-thread call. Retaining
                // the native bundle across frames prevents Addressables from
                // loading scenes which depend on the same bundle.
                ownedBundle = AssetBundle.LoadFromFile(bundlePaths[0]);
                if (ownedBundle == null)
                {
                    // Addressables may have completed ownership between the
                    // checks above and this call. Its visible copy can be
                    // borrowed but a bundle owned by the game is never unloaded.
                    snapshotClip = FindAlreadyLoadedExpectedClip() ??
                        FindClipInAlreadyLoadedBundle();
                    ownershipRace = snapshotClip == null;
                }
                else
                {
                    snapshotClip = LoadExactClip(ownedBundle);
                }
            }
            finally
            {
                if (ownedBundle != null)
                {
                    // false keeps snapshotClip alive while releasing the
                    // bundle identity so future scene dependencies can load.
                    ownedBundle.Unload(false);
                }
            }

            if (ownershipRace)
            {
                // The game's load can be in flight but not enumerable yet.
                // Resolution retries after it settles instead of permanently disabling
                // the cue or retaining a competing bundle.
                nextResolveAttemptTime = Time.unscaledTime +
                    ResolveRetryDelaySeconds;
                return false;
            }

            if (!IsExpectedClip(snapshotClip))
            {
                FailLoad(
                    "The Flea Caravan SFX bundle did not provide " +
                    VanillaFleaRescueClipAddress + "."
                );
                return false;
            }

            fleaHowlClip = snapshotClip;
            nextResolveAttemptTime = 0f;
            RandomizerPlugin.Log?.LogInfo(
                "[RANDOMIZER] Loaded the exact vanilla Flea rescue sound " +
                "and released the temporary SFX bundle."
            );
            return true;
        }

        private static AudioClip FindAlreadyLoadedExpectedClip()
        {
            foreach (AudioClip clip in
                     Resources.FindObjectsOfTypeAll<AudioClip>())
            {
                if (IsExpectedClip(clip))
                {
                    return clip;
                }
            }

            return null;
        }

        private static AudioClip FindClipInAlreadyLoadedBundle()
        {
            foreach (AssetBundle bundle in
                     AssetBundle.GetAllLoadedAssetBundles())
            {
                AudioClip clip = LoadExactClip(bundle);
                if (IsExpectedClip(clip))
                {
                    return clip;
                }
            }

            return null;
        }

        private static AudioClip LoadExactClip(AssetBundle bundle)
        {
            if (bundle == null)
            {
                return null;
            }

            string exactAssetPath = null;
            string[] assetNames = bundle.GetAllAssetNames();
            if (assetNames != null)
            {
                foreach (string assetName in assetNames)
                {
                    if (string.Equals(
                            assetName,
                            VanillaFleaRescueClipAddress,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        exactAssetPath = assetName;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(exactAssetPath))
            {
                return null;
            }

            return bundle.LoadAsset<AudioClip>(exactAssetPath);
        }

        private static bool IsExpectedClip(AudioClip clip)
        {
            return clip != null &&
                   string.Equals(
                       clip.name,
                       VanillaFleaRescueClipName,
                       StringComparison.Ordinal
                   );
        }

        private static void FailLoad(string reason)
        {
            clipResolutionFailed = true;
            pendingPlays = 0;
            RandomizerPlugin.Log?.LogWarning(
                "[RANDOMIZER] Could not load the vanilla Flea rescue sound: " + reason
            );
        }
    }
}
